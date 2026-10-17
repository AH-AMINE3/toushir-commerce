#!/usr/bin/env python3
"""Independent order-change consent and rollback audit, using only actual APIs."""
import concurrent.futures,contextlib,datetime as dt,json,os,pathlib,sys,time,urllib.error,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5864').rstrip('/')+'/api'
OUT=pathlib.Path(os.environ.get('TOUSHIR_TEST_RESULTS',str(pathlib.Path(__file__).resolve().parents[1]/'verification/journey/amendment-audit.json')))
checks=[];requests=[];success=False
def key():return uuid.uuid4().hex
def at(s=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(seconds=s)).isoformat()
def require(x,message='Required business outcome did not hold'):
    if not x:raise AssertionError(message)
def call(path,token=None,body=None,expected=200,method=None):
    method=method or ('POST' if body is not None else 'GET')
    for attempt in range(4):
        req=urllib.request.Request(BASE+path,method=method,data=json.dumps(body).encode() if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
        try:
            with urllib.request.urlopen(req,timeout=45) as r:status=r.status;data=json.load(r)
        except urllib.error.HTTPError as e:
            status=e.code
            try:data=json.load(e)
            except ValueError:data={}
            if status==429 and attempt<3:
                delay=min(60,max(1,int(e.headers.get('Retry-After','10'))));requests.append({'method':method,'path':path,'status':status,'retryAfterSeconds':delay});time.sleep(delay);continue
        record={'method':method,'path':path,'status':status}
        if isinstance(data,dict) and data.get('error'):record['errorCode']=data['error'].get('code')
        requests.append(record)
        allowed=expected if isinstance(expected,tuple) else (expected,)
        require(status in allowed,f'{method} {path}: expected {allowed}, got {status}, code {record.get("errorCode")}')
        return data
    raise AssertionError('Rate limit did not clear')
@contextlib.contextmanager
def case(name):
    item={'name':name,'pass':False};start=len(requests)
    try:yield;item['pass']=True;print('PASS '+name,flush=True)
    except Exception as e:item['error']=str(e);print('FAIL '+name+': '+str(e),flush=True);raise
    finally:item['requests']=requests[start:];checks.append(item)
def offer(pid,price,stock):return call('/admin/products/'+pid,A,{'price':price,'stock':stock,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
def direct(pid=None,quantity=3):
    pid=pid or p0;items=[{'productId':pid,'quantity':quantity}];c=call('/cart/quote',M,{'items':items});require(c['canOrder'])
    return call('/orders',M,{'idempotencyKey':key(),'addressId':address['id'],'items':items,'mode':'PLACE_ORDER','pricingToken':c['pricingToken']})
def proposed(order,items=None,**extra):
    body={'version':order['version'],'items':items or [{'productId':p1,'quantity':2}],'reason':'EXEMPLE proposition explicitement revue','expiresAt':at(3600),'idempotencyKey':key(),**extra}
    return call('/admin/orders/'+order['id']+'/amendments',A,body),body
def proposal_url(order,proposal,decision):return '/orders/'+order['id']+'/amendments/'+proposal['id']+'/'+decision
def accept_body(p,**extra):return {'version':p['version'],'idempotencyKey':key(),'termsHash':p['termsHash'],**extra}
def get(order):return call('/orders/'+order['id'],M)
def stock(pid):
    value=call('/products/'+pid,A);return (value['physicalStock'],value['reserved'],value['quarantined'])
def all_keys(value):
    if isinstance(value,dict):
        for k,v in value.items():yield k;yield from all_keys(v)
    elif isinstance(value,list):
        for v in value:yield from all_keys(v)
try:
    admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token']
    def persona(role):return call('/demo/session',body={'role':role,'workspaceToken':admin['workspaceToken']})
    merchant=persona('MERCHANT');M=merchant['token'];W=persona('WAREHOUSE')['token'];P=persona('DISPATCHER')['token'];driver=persona('DRIVER');D=driver['token']
    outsider=call('/demo/session',body={'role':'ADMIN'});X=outsider['token']
    inv=call('/admin/demo/invitations',A,{'label':'EXEMPLE QA consentement','hours':24,'maxUses':1})
    other=call('/demo/join',body={'inviteToken':inv['inviteToken'],'joinKey':key(),'name':'EXEMPLE QA autre personne','shopName':'EXEMPLE QA autre magasin','commune':'Oran','street':'EXEMPLE'});M2=other['token']
    products=call('/products?pageSize=4',M)['items'];p0,p1,p2,p3=[p['id'] for p in products];offer(p0,100,500);offer(p1,200,500);offer(p2,50,3);offer(p3,25,100)
    address=call('/addresses',M)[0];original=direct();oid=original['id']
    with case('Amendment history requires authentication'):call('/orders/'+oid+'/amendments',expected=401)
    with case('Another shop cannot inspect the contract-change dossier'):call('/orders/'+oid+'/amendments',M2,expected=404)
    with case('Another workspace cannot inspect the contract-change dossier'):call('/orders/'+oid+'/amendments',X,expected=404)
    with case('Warehouse role cannot inspect the commercial change dossier'):call('/orders/'+oid+'/amendments',W,expected=403)
    with case('An eligible unpaid unprepared order allows an owner proposal'):require(call('/orders/'+oid+'/amendments',A)['canPropose'])
    valid={'version':original['version'],'items':[{'productId':p1,'quantity':2}],'reason':'EXEMPLE nouvelle proposition','expiresAt':at(3600),'idempotencyKey':key()}
    with case('Merchant cannot author a wholesaler change proposal'):call('/admin/orders/'+oid+'/amendments',M,valid,expected=403)
    with case('Proposal rejects an outdated base order version'):call('/admin/orders/'+oid+'/amendments',A,{**valid,'version':original['version']-1},expected=409)
    with case('Proposal rejects an empty replacement basket'):call('/admin/orders/'+oid+'/amendments',A,{**valid,'items':[]},expected=400)
    with case('Proposal requires a meaningful reason'):call('/admin/orders/'+oid+'/amendments',A,{**valid,'reason':'x'},expected=400)
    with case('Proposal expiry must leave a meaningful review window'):call('/admin/orders/'+oid+'/amendments',A,{**valid,'expiresAt':at(60)},expected=400)
    with case('Proposal is a separate immutable contract suggestion with no reservation side effect'):
        before0,before1=stock(p0),stock(p1);result,body=proposed(original);proposal=result['proposal'];require(proposal['after']['total']==400 and get(original)['total']==300 and stock(p0)==before0 and stock(p1)==before1)
    with case('Proposal retries return the original pending suggestion'):require(call('/admin/orders/'+oid+'/amendments',A,body)['proposal']['id']==proposal['id'])
    with case('An already-used proposal key cannot change contractual quantities'):call('/admin/orders/'+oid+'/amendments',A,{**body,'items':[{'productId':p1,'quantity':3}]},expected=409)
    with case('Only one live proposal can await the shop decision'):call('/admin/orders/'+oid+'/amendments',A,{**body,'version':result['order']['version'],'idempotencyKey':key()},expected=409)
    with case('Merchant frozen before and after projections do not expose private tariff provenance'):
        visible=call('/orders/'+oid+'/amendments',M)['items'][0];forbidden={'termsJson','overrideReason','actorId','ruleId','ruleVersion'}
        require(not ((set(all_keys(visible['before']))|set(all_keys(visible['after'])))&forbidden),'Private pricing provenance leaked through a frozen proposal projection')
        require(visible['canAccept'] and visible['after']['total']==400 and visible['before']['total']==300)
    with case('Wholesaler cannot impersonate merchant acceptance'):call(proposal_url(original,proposal,'accept'),A,accept_body(proposal),expected=403)
    with case('Another shop cannot accept this merchant proposal'):call(proposal_url(original,proposal,'accept'),M2,accept_body(proposal),expected=404)
    with case('Merchant must present consent to the exact displayed terms'):call(proposal_url(original,proposal,'accept'),M,accept_body(proposal,termsHash='changed'),expected=409)
    with case('Merchant decision must use the current proposal version'):call(proposal_url(original,proposal,'accept'),M,accept_body(proposal,version=proposal['version']-1),expected=409)
    with case('Explicit acceptance atomically replaces products quantities totals and reservations'):
        decision=accept_body(proposal);accepted=call(proposal_url(original,proposal,'accept'),M,decision);amended=accepted['order']
        require(amended['total']==400 and len(amended['items'])==1 and amended['items'][0]['productId']==p1 and amended['items'][0]['quantity']==2 and amended['reservationHeld'])
        require(stock(p0)[1]==before0[1]-3 and stock(p1)[1]==before1[1]+2)
    with case('Acceptance retries preserve one committed amended contract'):
        before=stock(p1);replayed=call(proposal_url(original,proposal,'accept'),M,decision);require(replayed['order']['version']==amended['version'] and stock(p1)==before)
    with case('A second independent decision cannot consume an accepted proposal'):call(proposal_url(original,proposal,'accept'),M,accept_body(proposal),expected=409)
    with case('A rejected decision cannot overwrite accepted history'):call(proposal_url(original,proposal,'reject'),M,{**accept_body(proposal),'reason':'EXEMPLE trop tard'},expected=409)
    with case('Rejection preserves the original products money and physical reservations'):
        o=direct();r,_=proposed(o);p=r['proposal'];before=stock(p0);rejected=call(proposal_url(o,p,'reject'),M,{**accept_body(p),'reason':'EXEMPLE je conserve ma commande'});require(rejected['order']['total']==300 and rejected['proposal']['status']=='REJECTED' and stock(p0)==before)
    with case('Only the wholesaler may withdraw its pending proposal'):
        o=direct();r,_=proposed(o);p=r['proposal'];withdraw='/admin'+proposal_url(o,p,'withdraw');call(withdraw,M,{**accept_body(p),'reason':'EXEMPLE retrait interdit'},expected=403)
    with case('Withdrawal closes the suggestion without replacing the contract'):
        withdrawal_body={'version':p['version'],'idempotencyKey':key(),'reason':'EXEMPLE proposition remplacée'}
        withdrawn=call(withdraw,A,withdrawal_body);require(withdrawn['proposal']['status']=='WITHDRAWN' and withdrawn['order']['total']==300)
    with case('Withdrawal replay preserves one history decision and the unchanged contract'):
        retry=call(withdraw,A,withdrawal_body);require(retry['proposal']['version']==withdrawn['proposal']['version'] and retry['order']['version']==withdrawn['order']['version'])
    with case('Withdrawn terms cannot later be accepted'):call(proposal_url(o,p,'accept'),M,accept_body(p),expected=409)
    with case('A price change after proposal fails closed and rolls back original reservations'):
        o=direct();r,_=proposed(o);p=r['proposal'];offer(p1,210,500);before0,before1=stock(p0),stock(p1)
        error=call(proposal_url(o,p,'accept'),M,accept_body(p),expected=409);require(error['error']['code']=='AMENDMENT_TERMS_CHANGED' and get(o)['total']==300 and stock(p0)==before0 and stock(p1)==before1)
    with case('A frozen historical proposal does not silently adopt a later catalogue price'):
        historical=call('/orders/'+o['id']+'/amendments',M)['items'][0];require(historical['after']['total']==400)
    with case('Stock loss after proposal rolls back the attempted contract replacement'):
        o=direct();r,_=proposed(o,[{'productId':p3,'quantity':4}]);p=r['proposal'];offer(p3,25,0);before0,before3=stock(p0),stock(p3)
        error=call(proposal_url(o,p,'accept'),M,accept_body(p),expected=409);require(error['error']['code']=='AMENDMENT_STOCK_OR_TERMS_CHANGED' and get(o)['total']==300 and stock(p0)==before0 and stock(p3)==before3);offer(p3,25,100)
    with case('Fully reserved own stock can be reduced without requiring unrelated spare stock'):
        o=direct(p2,3);require(stock(p2)[1]==3);r,_=proposed(o,[{'productId':p2,'quantity':2}]);p=r['proposal'];changed=call(proposal_url(o,p,'accept'),M,accept_body(p));require(changed['order']['total']==100 and stock(p2)==(3,2,0))
    with case('An actual payment blocks proposing a commercial rewrite'):
        o=direct();o=call('/admin/orders/'+o['id']+'/transition',A,{'status':'CONFIRMED','reason':'EXEMPLE confirmation'});call('/admin/orders/'+o['id']+'/payments',A,{'amount':1,'method':'CASH','reference':'EXEMPLE acompte réel','idempotencyKey':key()});o=get(o)
        call('/admin/orders/'+o['id']+'/amendments',A,{**valid,'version':o['version'],'idempotencyKey':key()},expected=409)
    with case('Driver assignment blocks a proposal after operational handoff'):
        o=direct();o=call('/admin/orders/'+o['id']+'/transition',A,{'status':'CONFIRMED','reason':'EXEMPLE confirmation'});call('/operations/orders/'+o['id']+'/assign',P,{'version':o['version'],'driverId':driver['user']['id']});o=get(o)
        call('/admin/orders/'+o['id']+'/amendments',A,{**valid,'version':o['version'],'idempotencyKey':key()},expected=409)
    with case('Beginning physical preparation invalidates an already-open proposal'):
        o=direct();o=call('/admin/orders/'+o['id']+'/transition',A,{'status':'CONFIRMED','reason':'EXEMPLE confirmation'});r,_=proposed(o);p=r['proposal'];call('/operations/orders/'+o['id']+'/preparation',W,{'version':r['order']['version'],'items':[{'productId':p0,'preparedQuantity':1}]},method='PATCH')
        visible=call('/orders/'+o['id']+'/amendments',M)['items'][0];require(visible['applicability']=='STALE' and not visible['canAccept']);call(proposal_url(o,p,'accept'),M,accept_body(p),expected=409)
    with case('Canceling the original order prevents later acceptance of a stale suggestion'):
        o=direct();r,_=proposed(o);p=r['proposal'];call('/orders/'+o['id']+'/cancel',M,{'reason':'EXEMPLE annulation'});call(proposal_url(o,p,'accept'),M,accept_body(p),expected=409);require(get(o)['total']==0)
    with case('Concurrent independent acceptance attempts commit one replacement exactly once'):
        o=direct();r,_=proposed(o,[{'productId':p3,'quantity':2}]);p=r['proposal'];before0,before3=stock(p0),stock(p3)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda _:call(proposal_url(o,p,'accept'),M,accept_body(p),expected=(200,409)),range(2)))
        require(sum('order' in x for x in results)==1 and get(o)['total']==50 and stock(p0)[1]==before0[1]-3 and stock(p3)[1]==before3[1]+2)
    success=True
finally:
    result={'success':success,'base':BASE,'at':at(),'passed':sum(x['pass'] for x in checks),'failed':sum(not x['pass'] for x in checks),'httpRequests':len(requests),'scope':'Independent isolated actual API audit of versioned order amendments, merchant consent, privacy, stock rollback and concurrency. No browser or native runtime acceptance.','checks':checks}
    OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:result[k] for k in ('success','passed','failed','httpRequests')}),flush=True)
    if not success:sys.exit(1)
