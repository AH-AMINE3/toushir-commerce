#!/usr/bin/env python3
"""Focused real-API acceptance for merchant activity, geographic filters and campaign audiences."""
import contextlib,datetime as dt,json,os,pathlib,sys,time,urllib.error,urllib.parse,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5864').rstrip('/')+'/api'
OUT=pathlib.Path(os.environ.get('TOUSHIR_TEST_RESULTS',str(pathlib.Path(__file__).resolve().parents[1]/'verification/journey/activity-audience.json')))
checks=[];requests=[];success=False
def key():return uuid.uuid4().hex
def at(s=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(seconds=s)).isoformat()
def require(x,message='Required outcome did not hold'):
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
        requests.append({'method':method,'path':path,'status':status})
        require(status==expected,f'{method} {path}: expected {expected}, got {status}, code {data.get("error",{}).get("code") if isinstance(data,dict) else "none"}')
        return data
    raise AssertionError('Rate limit did not clear')
@contextlib.contextmanager
def case(name):
    item={'name':name,'pass':False};start=len(requests)
    try:
        yield;item['pass']=True;print('PASS '+name,flush=True)
    except Exception as e:item['error']=str(e);raise
    finally:item['requests']=requests[start:];checks.append(item)
try:
    admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token']
    def persona(role):return call('/demo/session',body={'role':role,'workspaceToken':admin['workspaceToken']})
    merchant=persona('MERCHANT');M=merchant['token'];mid=merchant['user']['id'];F=persona('FINANCE')['token']
    outsider=call('/demo/session',body={'role':'MERCHANT'});X=outsider['token'];foreign=outsider['user']['id']
    inv=call('/admin/demo/invitations',A,{'label':'EXEMPLE QA audience','hours':24,'maxUses':1})
    second=call('/demo/join',body={'inviteToken':inv['inviteToken'],'joinKey':key(),'name':'EXEMPLE second propriétaire','shopName':'EXEMPLE Alger QA','phone':'0555987654','commune':'Bab Ezzouar','street':'EXEMPLE'});M2=second['token'];mid2=second['user']['id']
    address=call('/addresses',M)[0];address2=call('/addresses',M2)[0];pid=call('/products?pageSize=1',M)['items'][0]['id']
    call('/admin/products/'+pid,A,{'price':100,'stock':100,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
    with case('Merchant activity requires an authenticated session'):call('/profile/activity?days=7',expected=401)
    with case('Activity rejects staff instead of leaking a shop aggregate'):call('/profile/activity?days=7',A,expected=403)
    with case('Activity rejects unsupported arbitrary date windows'):call('/profile/activity?days=8',M,expected=400)
    with case('Empty merchant activity has truthful zero values and real daily dates'):
        empty=call('/profile/activity?days=7',M);require(empty['orders']['total']==0 and empty['financial']['netPaid']==0 and len(empty['daily'])==7 and empty['period']['timezone']=='Africa/Algiers')
    with case('Default activity has thirty daily entries'):require(len(call('/profile/activity',M)['daily'])==30)
    with case('Ninety-day activity has a bounded actual date series'):require(len(call('/profile/activity?days=90',M)['daily'])==90)
    with case('Shop geography follows its default address'):
        call('/addresses/'+address2['id'],M2,{**address2,'wilaya':'Alger','commune':'Bab Ezzouar','isDefault':True},method='PUT')
        shops=call('/admin/merchants',A)['items'];require(next(x for x in shops if x['id']==mid2)['region']=='Alger')
    with case('Wholesaler filters stores by region without mixing other regions'):
        shops=call('/admin/merchants?region=Alger',A)['items'];require(len(shops)==1 and shops[0]['id']==mid2 and shops[0]['commune']=='Bab Ezzouar')
    with case('Region filters compare names without case differences'):require(call('/admin/merchants?region=alger',A)['total']==1)
    with case('Store text search includes commune'):require(call('/admin/merchants?q=Bab%20Ezzouar',A)['items'][0]['id']==mid2)
    with case('Combined region and telephone filters retain only matching stores'):require(call('/admin/merchants?region=Alger&q=0555987654',A)['total']==1 and call('/admin/merchants?region=Oran&q=0555987654',A)['total']==0)
    with case('CRM projects the same actual geography'):
        overview=call('/crm',A);shops=overview['shops'];require(next(x for x in shops if x['id']==mid2)['region']=='Alger')
    def draft(targets,**extra):
        return {'title':'EXEMPLE QA audience','description':'Périmètre publié et prix fictifs','kind':'DISCOUNT','productId':pid,'minimumQuantity':1,'discountPercent':10,'giftQuantity':1,'goalQuantity':0,'unitFactor':1,'awardLimit':1,'startsAt':at(-5),'endsAt':at(3600),'targetMerchantIds':targets,**extra}
    with case('Campaign creation rejects duplicate target shops'):call('/admin/campaigns',A,draft([mid,mid]),expected=400)
    with case('Campaign creation rejects a target from another workspace'):call('/admin/campaigns',A,draft([foreign]),expected=400)
    with case('Campaign creation rejects a missing or inactive target'):call('/admin/campaigns',A,draft(['missing-shop']),expected=400)
    with case('Campaign draft persists the exact selected shop audience'):
        targeted=call('/admin/campaigns',A,draft([mid]));require(targeted['targetMerchantIds']==[mid] and targeted['targetCount']==1)
    with case('A targeted draft stays invisible to the selected merchant'):require(not any(x['id']==targeted['id'] for x in call('/campaigns',M)['items']))
    with case('Independent publication preserves the chosen audience'):
        targeted=call('/admin/campaigns/'+targeted['id']+'/publish',F,{'version':targeted['version']});require(targeted['targetMerchantIds']==[mid] and targeted['status']=='PUBLISHED')
    with case('Published targeted campaign appears to its selected shop'):require(any(x['id']==targeted['id'] for x in call('/campaigns',M)['items']))
    with case('Published targeted campaign stays absent from another shop'):require(not any(x['id']==targeted['id'] for x in call('/campaigns',M2)['items']))
    with case('Excluded shop cannot inspect the targeted preview'):call('/campaigns/'+targeted['id']+'/preview',M2,{'quantity':3},expected=404)
    with case('Excluded shop cannot inspect targeted challenge or campaign progress'):call('/campaigns/'+targeted['id']+'/progress',M2,expected=404)
    with case('Targeted preview and actual basket resolve the selected discount'):
        preview=call('/campaigns/'+targeted['id']+'/preview',M,{'quantity':3});c=call('/cart/quote',M,{'items':[{'productId':pid,'quantity':3}]});require(preview['goodsAfter']==270 and c['total']==270)
    with case('Excluded shop keeps its ordinary published price at checkout'):
        excluded=call('/cart/quote',M2,{'items':[{'productId':pid,'quantity':3}]});require(excluded['total']==300 and excluded['items'][0]['promotion'] is None)
    with case('Staff simulation uses the same audience-aware pricing engine'):
        simulation=call('/pricing/simulate',A,{'merchantId':mid2,'items':[{'productId':pid,'quantity':3}]});require(simulation['items'][0]['unitPrice']==100)
    with case('Direct order commits only the eligible targeted promotion'):
        order=call('/orders',M,{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':3}],'mode':'PLACE_ORDER','pricingToken':c['pricingToken']});oid=order['id'];require(order['total']==270)
    with case('Explicit manual quote cannot force a targeted discount onto an excluded shop'):
        rfq=call('/orders',M2,{'idempotencyKey':key(),'addressId':address2['id'],'items':[{'productId':pid,'quantity':3}],'mode':'REQUEST_QUOTE'})
        denied=call('/admin/orders/'+rfq['id']+'/quote',A,{'version':rfq['version'],'items':[{'productId':pid,'quantity':3,'unitPrice':100,'caseSize':1}],'deliveryFee':0,'validUntil':at(3600),'campaignIds':[targeted['id']]},expected=409)
        require(denied['error']['code']=='CAMPAIGN_NOT_APPLICABLE' and call('/orders/'+rfq['id'],M2)['status']=='REQUESTED')
    with case('Activity reflects the new actual merchant order and outstanding contract'):
        activity=call('/profile/activity?days=7',M);require(activity['orders']['total']==1 and activity['financial']['orderedTotal']==270 and activity['financial']['outstanding']==270)
    with case('Another merchant activity cannot be switched by a merchantId parameter'):
        isolated=call('/profile/activity?days=7&merchantId='+mid,M2);require(isolated['orders']['total']==1 and isolated['financial']['orderedTotal']==0)
    for state in ['CONFIRMED','PREPARING','READY','OUT_FOR_DELIVERY']:order=call('/admin/orders/'+oid+'/transition',A,{'status':state,'reason':'EXEMPLE parcours activité'})
    call('/admin/orders/'+oid+'/delivery',A,{'items':[{'productId':pid,'acceptedQuantity':2,'refusedQuantity':1}],'note':'EXEMPLE activité après réception'})
    with case('Activity separates accepted delivered amount from the original ordered amount'):
        activity=call('/profile/activity?days=7',M);require(activity['orders']['delivered']==1 and activity['financial']['orderedTotal']==180 and activity['financial']['netPaid']==0)
    call('/admin/orders/'+oid+'/payments',A,{'amount':180,'method':'CASH','reference':'EXEMPLE QA activité','idempotencyKey':key()})
    with case('Activity money and daily chart come from the real recorded payment'):
        activity=call('/profile/activity?days=7',M);require(activity['financial']['paid']==180 and activity['financial']['netPaid']==180 and activity['financial']['outstanding']==0 and sum(x['paid'] for x in activity['daily'])==180)
    with case('Staff campaign overview retains the exact selected audience for future audits'):
        stored=next(x for x in call('/admin/campaigns',A)['items'] if x['id']==targeted['id']);require(stored['targetMerchantIds']==[mid] and stored['targetCount']==1)
    with case('Support handoff rejects a SALES adviser who cannot open the support dossier'):
        sales=call('/operations/team',A,{'name':'EXEMPLE QA conseiller restreint','email':key()+'@example.invalid','password':key()+'A!','role':'SALES','phone':''})
        thread=call('/conversations',M,{'contextType':'GENERAL','contextId':None,'subject':'EXEMPLE suivi restreint','merchantId':None})
        call('/conversations/'+thread['id'],A,{'version':thread['version'],'status':'OPEN','assignedTo':sales['id']},method='PATCH',expected=400)
        current=call('/conversations/'+thread['id']+'/messages',M)['conversation'];require(not current['assignedTo'])
    success=True
finally:
    result={'success':success,'base':BASE,'at':at(),'passed':sum(x['pass'] for x in checks),'failed':sum(not x['pass'] for x in checks),'httpRequests':len(requests),'scope':'Actual isolated local API acceptance. Geographic filters, persisted campaign audiences, shared checkout pricing and merchant activity; no individual Figma screen or native-device acceptance.','checks':checks}
    OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:result[k] for k in ('success','passed','failed','httpRequests')}),flush=True)
    if not success:sys.exit(1)
