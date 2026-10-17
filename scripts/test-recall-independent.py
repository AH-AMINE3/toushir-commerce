#!/usr/bin/env python3
"""Independent recalled-catalog and in-flight receipt safety audit. Real isolated API only."""
import contextlib,datetime as dt,json,os,pathlib,sys,time,urllib.error,urllib.parse,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5864').rstrip('/')+'/api'
OUT=pathlib.Path(os.environ.get('TOUSHIR_TEST_RESULTS',str(pathlib.Path(__file__).resolve().parents[1]/'verification/journey/recall-independent.json')))
checks=[];requests=[];observed={};completed=False
def key():return uuid.uuid4().hex
def at(days=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(days=days)).isoformat()
def require(x,message='Required business outcome did not hold'):
    if not x:raise AssertionError(message)
def call(path,token=None,body=None,expected=200,method=None):
    method=method or ('POST' if body is not None else 'GET')
    for attempt in range(4):
        request=urllib.request.Request(BASE+path,method=method,data=json.dumps(body).encode() if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
        try:
            with urllib.request.urlopen(request,timeout=45) as r:status=r.status;data=json.load(r)
        except urllib.error.HTTPError as e:
            status=e.code
            try:data=json.load(e)
            except ValueError:data={}
            if status==429 and attempt<3:
                delay=max(1,min(60,int(e.headers.get('Retry-After','60'))));requests.append({'method':method,'path':path,'status':status,'retryAfterSeconds':delay});time.sleep(delay);continue
        record={'method':method,'path':path,'status':status}
        if isinstance(data,dict) and data.get('error'):record['errorCode']=data['error'].get('code')
        requests.append(record);require(status==expected,f'{method} {path}: expected{expected}, got{status}, code{record.get("errorCode")}');return data
    raise AssertionError('Rate limit did not clear')
@contextlib.contextmanager
def case(name):
    item={'name':name,'pass':False};offset=len(requests)
    try:yield;item['pass']=True;print('PASS '+name,flush=True)
    except AssertionError as e:item['error']=str(e);print('FAIL '+name+': '+str(e),flush=True)
    finally:item['requests']=requests[offset:];checks.append(item)
try:
    admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token']
    def persona(role):return call('/demo/session',body={'role':role,'workspaceToken':admin['workspaceToken']})
    merchant=persona('MERCHANT');M=merchant['token'];W=persona('WAREHOUSE')['token'];driver=persona('DRIVER');D=driver['token']
    product=call('/products?pageSize=1',M)['items'][0];pid=product['id'];address=call('/addresses',M)[0]
    call('/admin/products/'+pid,A,{'price':100,'stock':7,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
    expiry=at(30);number='EXEMPLE-INDEPENDENT-RECALL'
    activated=call('/admin/warehouse/activate',W,{'productId':pid,'expectedPhysicalStock':7,'reason':'EXEMPLE lot physiquement compté','proofReference':'EXEMPLE-COUNT-QA','idempotencyKey':key(),'lots':[{'lotNumber':number,'expiresAt':expiry,'quantity':7,'quarantined':0}]})
    lot=activated['items'][0]
    cart=call('/cart/quote',M,{'items':[{'productId':pid,'quantity':2}]});require(cart['canOrder'])
    order=call('/orders',M,{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':2}],'mode':'PLACE_ORDER','pricingToken':cart['pricingToken']});oid=order['id']
    order=call('/admin/orders/'+oid+'/transition',A,{'status':'CONFIRMED','reason':'EXEMPLE confirmation'})
    wave=call('/admin/warehouse/waves',W,{'name':'EXEMPLE audit rappel en transport','orderIds':[oid],'idempotencyKey':key()})
    for task in wave['items']:call('/admin/warehouse/tasks/'+task['id']+'/scan',W,{'version':task['version'],'barcode':task['barcode'],'lotNumber':task['lotNumber'],'quantity':task['quantity'],'idempotencyKey':key()})
    order=call('/orders/'+oid,M)
    track=call('/operations/orders/'+oid+'/assign',A,{'driverId':driver['user']['id'],'version':order['version']})
    call('/courier/orders/'+oid+'/pickup',D,{'version':track['version']});call('/courier/orders/'+oid+'/status',D,{'status':'EN_ROUTE'});track=call('/courier/orders/'+oid+'/status',D,{'status':'ARRIVED'})
    accepted=[{'productId':pid,'acceptedQuantity':2,'refusedQuantity':0}]
    pre_hold=call('/orders/'+oid+'/receipt-challenge',M,{'version':track['version'],'items':accepted})
    lot=next(x for x in call('/admin/warehouse/lots?productId='+pid,W)['items'] if x['id']==lot['id'])
    recall=call('/admin/recalls',W,{'lotId':lot['id'],'version':lot['version'],'reason':'EXEMPLE rappel fabricant pendant transport','safetyInstruction':'EXEMPLE ne pas remettre les unités; conserver en quarantaine','idempotencyKey':key()});rid=recall['recall']['id']
    with case('Recall establishes a hold without claiming that physical stock moved'):
        lots=call('/admin/warehouse/lots?productId='+pid,W);require(lots['stock']=={'onHand':5,'reserved':0,'quarantined':0})
    with case('Merchant product details exclude recalled stock immediately'):
        detail=call('/products/'+pid,M);observed['detailStockWhileHold']=detail['stock'];require(detail['stock']==0)
    with case('Merchant catalogue excludes held units before physical isolation'):
        listed=next(x for x in call('/products?q='+urllib.parse.quote(product['barcode']),M)['items'] if x['id']==pid);observed['listStockWhileHold']=listed['stock'];require(listed['stock']==0,f'Catalogue reports{listed["stock"]} held units available; detail reports0')
    with case('In-stock catalogue filter excludes a product whose only batch is held'):
        rows=call('/products?inStock=true&q='+urllib.parse.quote(product['barcode']),M)['items'];observed['inStockContainsHeldProduct']=any(x['id']==pid for x in rows);require(not any(x['id']==pid for x in rows),'Held-only product still passes inStock=true')
    with case('Saved favorites exclude held batch stock using the same policy'):
        call('/favorites/'+pid,M,{});favorite=next(x for x in call('/favorites',M) if x['id']==pid);observed['favoriteStockWhileHold']=favorite['stock'];require(favorite['stock']==0,f'Favorite reports{favorite["stock"]} held units available')
    with case('Checkout refuses recalled-only stock even before depot isolation'):
        quote=call('/cart/quote',M,{'items':[{'productId':pid,'quantity':1}]});require(not quote['canOrder'] and quote.get('pricingToken') is None)
    with case('A receipt QR issued before recall cannot hand recalled goods to the merchant'):
        denied=call('/courier/orders/'+oid+'/complete',D,{'challenge':pre_hold['challenge'],'items':accepted},expected=409);require(denied['error']['code']=='LOT_RECALLED')
    with case('A new accepted receipt cannot bypass the active recalled batch hold'):
        denied=call('/orders/'+oid+'/receipt-challenge',M,{'version':track['version'],'items':accepted},expected=409);require(denied['error']['code']=='LOT_RECALLED')
    with case('Merchant acknowledgement records instruction receipt without stock recovery'):
        notice=next(x['notice'] for x in call('/recalls/inbox',M)['items'] if x['recall']['id']==rid)
        call('/recalls/notices/'+notice['id']+'/ack',M,{'version':notice['version'],'idempotencyKey':key()});recall=call('/admin/recalls/'+rid,W)
        require(all(x['recoveredQuantity']==0 for x in recall['positions']) and call('/admin/warehouse/lots?productId='+pid,W)['stock']['quarantined']==0)
    with case('All-refused physical receipt remains possible so recalled driver custody can return'):
        refused=[{'productId':pid,'acceptedQuantity':0,'refusedQuantity':2}]
        proof=call('/orders/'+oid+'/receipt-challenge',M,{'version':track['version'],'items':refused})
        result=call('/courier/orders/'+oid+'/complete',D,{'challenge':proof['challenge'],'items':refused,'note':'EXEMPLE rappel: aucune unité remise'});require(result['order']['status']=='DELIVERY_REFUSED' and result['order']['total']==0)
    with case('Recalled refused goods enter quarantine rather than silently becoming available stock'):
        require(call('/admin/warehouse/lots?productId='+pid,W)['stock']=={'onHand':5,'reserved':0,'quarantined':2})
    order=call('/orders/'+oid,M)
    with case('Managed recalled returns reject a changed timestamp on the same expiry date'):
        changed_expiry=dt.datetime.fromisoformat(expiry).replace(hour=(dt.datetime.fromisoformat(expiry).hour+1)%24)
        require(changed_expiry.date()==dt.datetime.fromisoformat(expiry).date())
        denied=call('/admin/warehouse/returns/identify',W,{'sourceType':'REFUSAL','sourceId':order['items'][0]['id'],'quantity':2,'lotNumber':number,'expiresAt':changed_expiry.isoformat(),'proofReference':'EXEMPLE même date mais identité différente','idempotencyKey':key()},expected=422)
        require(denied['error']['code']=='LOT_MISMATCH' and call('/admin/warehouse/lots?productId='+pid,W)['stock']=={'onHand':5,'reserved':0,'quarantined':2})
    equivalent_expiry=dt.datetime.fromisoformat(expiry).astimezone(dt.timezone(dt.timedelta(hours=1))).isoformat()
    identity=call('/admin/warehouse/returns/identify',W,{'sourceType':'REFUSAL','sourceId':order['items'][0]['id'],'quantity':2,'lotNumber':number,'expiresAt':equivalent_expiry,'proofReference':'EXEMPLE vérification du lot retourné','idempotencyKey':key()})
    returned=next(x for x in identity['items'] if x['sourceRef']=='REFUSAL:'+order['items'][0]['id'])
    with case('Physical returned-batch identity can reconcile outbound exposure without duplicating stock'):
        recall=call('/admin/recalls/'+rid,W);outbound=next(x for x in recall['positions'] if x['kind']=='OUTBOUND');before=call('/admin/warehouse/lots?productId='+pid,W)['stock']
        recall=call('/admin/recalls/'+rid+'/recover',W,{'version':recall['recall']['version'],'positionId':outbound['id'],'quantity':2,'quarantineLotId':returned['id'],'proofReference':'EXEMPLE retour physiquement rapproché','idempotencyKey':key()})
        require(call('/admin/warehouse/lots?productId='+pid,W)['stock']==before and next(x for x in recall['positions'] if x['id']==outbound['id'])['recoveredQuantity']==2)
    with case('Final depot isolation and closure preserve all seven physical units in quarantine'):
        depot=next(x for x in recall['positions'] if x['kind']=='DEPOT');recall=call('/admin/recalls/'+rid+'/recover',W,{'version':recall['recall']['version'],'positionId':depot['id'],'quantity':5,'proofReference':'EXEMPLE isolement des cinq unités dépôt','idempotencyKey':key()})
        recall=call('/admin/recalls/'+rid+'/close',A,{'version':recall['recall']['version'],'note':'EXEMPLE toutes les unités physiquement isolées','idempotencyKey':key()})
        require(recall['recall']['status']=='CLOSED' and recall['recall']['containmentActive'] and call('/admin/warehouse/lots?productId='+pid,W)['stock']=={'onHand':0,'reserved':0,'quarantined':7})
    completed=True
finally:
    report={'success':completed and all(x['pass'] for x in checks),'at':at(),'base':BASE,'passed':sum(x['pass'] for x in checks),'failed':sum(not x['pass'] for x in checks),'httpRequests':len(requests),'observed':observed,'scope':'Independent actual isolated API audit: held stock presentation, in-flight receipt safety, all-refused return, real quarantine reconciliation. No camera or native device acceptance.','checks':checks}
    OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:report[k] for k in ('success','passed','failed','httpRequests','observed')}),flush=True)
    if not report['success']:sys.exit(1)
