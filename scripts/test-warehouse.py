#!/usr/bin/env python3
"""Real API regression for counted lots, FEFO, scans, quality and return identification."""
import concurrent.futures,datetime as dt,json,os,sys,time,urllib.error,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5883').rstrip('/')+'/api';checks=[]
def key():return uuid.uuid4().hex
def at(days=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(days=days)).isoformat()
def call(method,path,body=None,token=None,expected=200,retries=0):
 req=urllib.request.Request(BASE+path,data=json.dumps(body).encode() if body is not None else None,method=method,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
 try:
  with urllib.request.urlopen(req,timeout=90) as r:status=r.status;data=json.load(r)
 except urllib.error.HTTPError as e:
  status=e.code
  try:data=json.load(e)
  except Exception:data={}
 if status==429 and retries<35:time.sleep(2);return call(method,path,body,token,expected,retries+1)
 assert status in expected if isinstance(expected,tuple) else status==expected,(method,path,status,expected,data)
 return data
def ok(name,condition=True):
 assert condition,name
 checks.append(name);print('PASS',name,flush=True)
admin=call('POST','/demo/session',{'role':'ADMIN'});A=admin['token'];space=admin['workspaceToken']
def persona(role):return call('POST','/demo/session',{'role':role,'workspaceToken':space})
merchant=persona('MERCHANT');M=merchant['token'];W=persona('WAREHOUSE')['token'];F=persona('FINANCE')['token']
products=call('GET','/products?pageSize=2',token=M)['items'];pid=products[0]['id'];p2=products[1]['id'];barcode=products[0]['barcode'];address=call('GET','/addresses',token=M)[0]
def offer(product,stock):return call('PATCH','/admin/products/'+product,{'price':1200,'stock':stock,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},A)
def request(product,qty):return call('POST','/orders',{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':product,'quantity':qty}],'mode':'REQUEST_QUOTE','note':'Warehouse QA'},M)
def quote(order,product,qty):return call('POST','/admin/orders/'+order['id']+'/quote',{'version':order['version'],'campaignIds':[],'items':[{'productId':product,'quantity':qty,'unitPrice':1200,'caseSize':1,'availableStock':None}],'deliveryFee':1000,'validUntil':at(1),'note':'QA'},A)
offer(pid,15);expires_a=at(10);expires_b=at(30)
activate={'productId':pid,'expectedPhysicalStock':15,'reason':'Comptage physique initial QA','proofReference':'COUNT-QA-001','idempotencyKey':key(),'lots':[{'lotNumber':'LOT-A','expiresAt':expires_a,'quantity':5,'quarantined':0},{'lotNumber':'LOT-B','expiresAt':expires_b,'quantity':10,'quarantined':0}]}
lots=call('POST','/admin/warehouse/activate',activate,W);again=call('POST','/admin/warehouse/activate',activate,W)
ok('count activation is explicit and idempotent',len(lots['controls'])==1 and len(again['items'])==2)
selected=call('GET','/admin/warehouse/lots?productId='+pid,token=W);ok('warehouse count binds visible selected product and scoped physical stock',selected['product']['id']==pid and selected['stock']=={'onHand':15,'reserved':0,'quarantined':0})
call('POST','/admin/inventory/adjust',{'productId':pid,'delta':1,'reason':'Forbidden aggregate edit'},A,409);ok('managed stock cannot bypass lot controls through aggregate adjustment')
call('PATCH','/admin/products/'+pid,{'price':1250,'stock':15,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},A);ok('price edit can preserve managed physical stock')
o=quote(request(pid,7),pid,7);o=call('POST','/orders/'+o['id']+'/accept-quote',{'version':o['quoteVersion']},M)
lots=call('GET','/admin/warehouse/lots?productId='+pid,token=W)['items'];a=next(x for x in lots if x['lotNumber']=='LOT-A');b=next(x for x in lots if x['lotNumber']=='LOT-B')
ok('FEFO reserves earlier expiry first',a['reserved']==5 and b['reserved']==2)
call('PATCH','/operations/orders/'+o['id']+'/preparation',{'version':o['version'],'items':[{'productId':pid,'preparedQuantity':7}]},W,409);ok('manual prepared quantity cannot bypass required scans')
wave_body={'name':'Vague QA','orderIds':[o['id']],'idempotencyKey':key()};wave=call('POST','/admin/warehouse/waves',wave_body,W);again=call('POST','/admin/warehouse/waves',wave_body,W)
ok('wave creation is persistent and idempotent',wave['wave']['id']==again['wave']['id'] and len(wave['items'])==2)
t=next(x for x in wave['items'] if x['lotNumber']=='LOT-A')
call('POST','/admin/warehouse/tasks/'+t['id']+'/scan',{'version':t['version'],'barcode':'0000000000000','lotNumber':'LOT-A','quantity':5,'idempotencyKey':key()},W,422)
call('POST','/admin/warehouse/tasks/'+t['id']+'/scan',{'version':t['version'],'barcode':barcode,'lotNumber':'LOT-B','quantity':5,'idempotencyKey':key()},W,422);ok('wrong barcode and wrong reserved lot are rejected')
scan={'version':t['version'],'barcode':barcode,'lotNumber':'LOT-A','quantity':5,'idempotencyKey':key()};wave=call('POST','/admin/warehouse/tasks/'+t['id']+'/scan',scan,W);wave=call('POST','/admin/warehouse/tasks/'+t['id']+'/scan',scan,W)
ok('scan replay does not duplicate picked quantity',wave['picked']==5)
t=next(x for x in wave['items'] if x['lotNumber']=='LOT-B');wave=call('POST','/admin/warehouse/tasks/'+t['id']+'/scan',{'version':t['version'],'barcode':barcode,'lotNumber':'LOT-B','quantity':2,'idempotencyKey':key()},W)
o=call('GET','/orders/'+o['id'],token=M);ok('completed scans mark wave and order ready',wave['wave']['status']=='COMPLETE' and o['status']=='READY' and o['items'][0]['preparedQuantity']==7)
o=call('POST','/admin/orders/'+o['id']+'/transition',{'status':'OUT_FOR_DELIVERY','reason':'QA départ'},A)
lots=call('GET','/admin/warehouse/lots?productId='+pid,token=W)['items'];a=next(x for x in lots if x['lotNumber']=='LOT-A');b=next(x for x in lots if x['lotNumber']=='LOT-B')
ok('departure consumes physical and reserved lots once',a['onHand']==0 and b['onHand']==8 and a['reserved']==0 and b['reserved']==0)
o=call('POST','/admin/orders/'+o['id']+'/delivery',{'items':[{'productId':pid,'acceptedQuantity':7,'refusedQuantity':0}],'note':'QA livré'},A)
claim=call('POST','/orders/'+o['id']+'/claims',{'productId':pid,'quantity':1,'reason':'QA retour identifié'},M)
call('POST','/admin/claims/'+claim['id']+'/resolve',{'status':'ACCEPTED','note':'QA'},A);call('POST','/admin/claims/'+claim['id']+'/receive',{'quantity':1,'note':'QA retour physique'},A)
call('POST','/admin/claims/'+claim['id']+'/inspect',{'quantity':1,'disposition':'RESTOCK','reason':'Missing lot proof'},A,409);ok('returned managed stock cannot restock without identified lot')
return_body={'sourceType':'CLAIM','sourceId':claim['id'],'quantity':1,'lotNumber':'LOT-A','expiresAt':expires_a,'proofReference':'Retour physique batchA','idempotencyKey':key()}
call('POST','/admin/warehouse/returns/identify',{**return_body,'lotNumber':'UNKNOWN','idempotencyKey':key()},W,422);ok('returned batch must belong to original shipment')
call('POST','/admin/warehouse/returns/identify',return_body,W);call('POST','/admin/warehouse/returns/identify',return_body,W)
call('POST','/admin/claims/'+claim['id']+'/inspect',{'quantity':1,'disposition':'RESTOCK','reason':'LotA contrôlé'},A)
lots=call('GET','/admin/warehouse/lots?productId='+pid,token=W)['items'];returned=[x for x in lots if x['sourceRef']=='CLAIM:'+claim['id']]
ok('identified return releases one traceable sellable unit',len(returned)==1 and returned[0]['onHand']==1 and returned[0]['quarantined']==0)
claim_cap=call('POST','/orders/'+o['id']+'/claims',{'productId':pid,'quantity':5,'reason':'QA source batch cap'},M)
call('POST','/admin/claims/'+claim_cap['id']+'/resolve',{'status':'ACCEPTED','note':'QA'},A);call('POST','/admin/claims/'+claim_cap['id']+'/receive',{'quantity':5,'note':'QA counted return'},A)
call('POST','/admin/warehouse/returns/identify',{'sourceType':'CLAIM','sourceId':claim_cap['id'],'quantity':5,'lotNumber':'LOT-A','expiresAt':expires_a,'proofReference':'QA cannot relabel all as batchA','idempotencyKey':key()},W,409)
ok('returned batch quantity cannot exceed its original shipped allocation')

supplier=call('POST','/admin/suppliers',{'name':'Warehouse QA supplier','contact':'QA','phone':'Example','note':''},A)
po=call('POST','/admin/purchases',{'supplierId':supplier['id'],'expectedAt':at(1),'items':[{'productId':pid,'quantity':10,'unitCostMinor':100000}],'note':'QA','idempotencyKey':key()},A)
po=call('POST','/admin/purchases/'+po['order']['id']+'/approve',{'version':po['order']['version']},F)
po=call('POST','/admin/purchases/'+po['order']['id']+'/receive',{'version':po['order']['version'],'lines':[{'productId':pid,'receivedQuantity':8,'quarantineQuantity':2,'lotNumber':'LOT-C','expiresAt':at(20)}],'note':'QA','idempotencyKey':key()},W)
rl=po['receiptLines'][0];lots=call('GET','/admin/warehouse/lots?productId='+pid,token=W)['items'];c=next(x for x in lots if x['sourceKey']=='SUPPLY:'+rl['id'])
ok('supplier receiving materializes exact managed lot quantities',c['onHand']==6 and c['quarantined']==2)
call('POST','/admin/purchases/'+po['order']['id']+'/inspect',{'receiptLineId':rl['id'],'version':rl['version'],'quantity':1,'disposition':'RELEASE','reason':'QA','idempotencyKey':key()},W)
lots=call('GET','/admin/warehouse/lots?productId='+pid,token=W)['items'];c=next(x for x in lots if x['sourceKey']=='SUPPLY:'+rl['id'])
ok('supplier quality decision updates both source and managed lot',c['onHand']==7 and c['quarantined']==1)

offer(p2,20);pending=quote(request(p2,1),p2,1);pending=call('POST','/orders/'+pending['id']+'/accept-quote',{'version':pending['quoteVersion']},M)
activation2={'productId':p2,'expectedPhysicalStock':20,'reason':'QA physical count','proofReference':'COUNT-QA-002','idempotencyKey':key(),'lots':[{'lotNumber':'ONLY','expiresAt':at(20),'quantity':1,'quarantined':0}]}
call('POST','/admin/warehouse/activate',activation2,W,409);ok('activation cannot relabel existing reserved legacy stock')
call('POST','/orders/'+pending['id']+'/cancel',{'reason':'QA cancellation'},M)
call('POST','/admin/warehouse/activate',activation2,W)
one=quote(request(p2,1),p2,1);two=quote(request(p2,1),p2,1)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 results=list(pool.map(lambda order:call('POST','/orders/'+order['id']+'/accept-quote',{'version':order['quoteVersion']},M,(200,409)),[one,two]))
ok('concurrent last-lot reservation has one winner',[r.get('status') for r in results].count('CONFIRMED')==1)
winner=next(r for r in results if r.get('status')=='CONFIRMED');call('POST','/orders/'+winner['id']+'/cancel',{'reason':'QA release'},M)
p2lots=call('GET','/admin/warehouse/lots?productId='+p2,token=W)['items'];only=p2lots[0]
quality={'version':only['version'],'quantity':1,'disposition':'QUARANTINE','reason':'QA quality hold','idempotencyKey':key()};call('POST','/admin/warehouse/lots/'+only['id']+'/quality',quality,W);call('POST','/admin/warehouse/lots/'+only['id']+'/quality',quality,W)
p2view=call('GET','/admin/warehouse/lots?productId='+p2,token=W);hold=next(x for x in p2view['items'] if x['sourceKey'].startswith('QUALITY:'))
ok('quality isolation is idempotent and separates its source hold',p2view['stock']['onHand']==0 and p2view['stock']['quarantined']==1 and hold['quarantined']==1)
call('POST','/admin/warehouse/lots/'+hold['id']+'/quality',{'version':hold['version'],'quantity':1,'disposition':'RELEASE','reason':'QA released','idempotencyKey':key()},W)
p2view=call('GET','/admin/warehouse/lots?productId='+p2,token=W);ok('quality release restores exact aggregate and batch availability',p2view['stock']['onHand']==1 and sum(x['eligibleAvailable'] for x in p2view['items'])==1)

# Gift stock has its own FEFO reservation and scan tasks, then its own return custody.
free={'title':'Warehouse QA2+1','description':'Separate gift lot custody','kind':'FREE_GOODS','productId':pid,'minimumQuantity':2,'discountPercent':0,'giftProductId':p2,'giftQuantity':1,'goalQuantity':0,'unitFactor':1,'rewardId':None,'awardLimit':10,'startsAt':at(-1),'endsAt':at(1),'claimUntil':None}
c=call('POST','/admin/campaigns',free,A);c=call('POST','/admin/campaigns/'+c['id']+'/publish',{'version':1},F)
g=request(pid,2);g=call('POST','/admin/orders/'+g['id']+'/quote',{'version':g['version'],'campaignIds':[c['id']],'items':[{'productId':pid,'quantity':2,'unitPrice':1200,'caseSize':1,'availableStock':None}],'deliveryFee':0,'validUntil':at(1),'note':'Separate gifts QA'},A);g=call('POST','/orders/'+g['id']+'/accept-quote',{'version':g['quoteVersion']},M)
gwave=call('POST','/admin/warehouse/waves',{'name':'Gift wave','orderIds':[g['id']],'idempotencyKey':key()},W)
gift_task=next(t for t in gwave['items'] if t['isGift']);ok('promotional gift gets separate managed stock and scan task',gift_task['productId']==p2 and gift_task['quantity']==1 and gift_task['fefoManaged'])
for task in [t for t in gwave['items'] if not t['isGift']]:
 gwave=call('POST','/admin/warehouse/tasks/'+task['id']+'/scan',{'version':task['version'],'barcode':task['barcode'],'lotNumber':task['lotNumber'],'quantity':task['quantity'],'idempotencyKey':key()},W)
call('POST','/admin/orders/'+g['id']+'/transition',{'status':'READY','reason':'Unscanned gift must block readiness'},A,409);ok('unscanned gift blocks manual ready transition')
gift_task=next(t for t in gwave['items'] if t['isGift']);gwave=call('POST','/admin/warehouse/tasks/'+gift_task['id']+'/scan',{'version':gift_task['version'],'barcode':gift_task['barcode'],'lotNumber':gift_task['lotNumber'],'quantity':1,'idempotencyKey':key()},W)
call('POST','/admin/orders/'+g['id']+'/transition',{'status':'OUT_FOR_DELIVERY','reason':'Gift departure'},A);call('POST','/admin/orders/'+g['id']+'/delivery',{'items':[{'productId':pid,'acceptedQuantity':2,'refusedQuantity':0}],'note':'Paid goods accepted; gift refused'},A)
promo=call('GET','/orders/'+g['id']+'/promotions',token=A)['items'][0]
promo=call('POST','/orders/'+g['id']+'/promotions/'+promo['id']+'/gift-receipt',{'version':promo['version'],'acceptedQuantity':0,'refusedQuantity':1,'proofReference':'QA gift refused physically','idempotencyKey':key()},A)
promo=call('POST','/orders/'+g['id']+'/promotions/'+promo['id']+'/gift-return',{'version':promo['version'],'disposition':'QUARANTINE','reason':'Received gift depot','idempotencyKey':key()},W)
call('POST','/orders/'+g['id']+'/promotions/'+promo['id']+'/gift-inspect',{'version':promo['version'],'disposition':'RELEASE','reason':'No lot identity yet','idempotencyKey':key()},W,409);ok('managed gift cannot restock without separate return lot identity')
call('POST','/admin/warehouse/returns/identify',{'sourceType':'PROMOTION','sourceId':promo['id'],'quantity':1,'lotNumber':gift_task['lotNumber'],'expiresAt':gift_task['expiresAt'],'proofReference':'QA returned gift batch','idempotencyKey':key()},W)
call('POST','/orders/'+g['id']+'/promotions/'+promo['id']+'/gift-inspect',{'version':promo['version'],'disposition':'RELEASE','reason':'Gift lot checked','idempotencyKey':key()},W)
p2view=call('GET','/admin/warehouse/lots?productId='+p2,token=W);ok('gift return inspection restores one independently traced unit',p2view['stock']['onHand']==1 and p2view['stock']['reserved']==0 and p2view['stock']['quarantined']==0)

counted=next(x for x in p2view['items'] if x['sourceRef']=='PROMOTION:'+promo['id'])
count_body={'version':counted['version'],'quantity':2,'reason':'QA physical recount','proofReference':'COUNT-QA-003','idempotencyKey':key()}
counted_view=call('POST','/admin/warehouse/lots/'+counted['id']+'/count',count_body,W);call('POST','/admin/warehouse/lots/'+counted['id']+'/count',count_body,W)
call('POST','/admin/warehouse/lots/'+counted['id']+'/count',{**count_body,'quantity':3,'idempotencyKey':key()},W,409)
ok('per-lot physical recount is idempotent and stale versions cannot change stock',counted_view['stock']['onHand']==2 and next(x for x in counted_view['items'] if x['id']==counted['id'])['onHand']==2)

# A routed refusal cannot become sellable until the depot accepts physical custody.
driver=persona('DRIVER');D=driver['token']
r=request(pid,1);r=quote(r,pid,1);r=call('POST','/orders/'+r['id']+'/accept-quote',{'version':r['quoteVersion']},M)
rwave=call('POST','/admin/warehouse/waves',{'name':'Routed returns QA','orderIds':[r['id']],'idempotencyKey':key()},W)
for task in rwave['items']:
 rwave=call('POST','/admin/warehouse/tasks/'+task['id']+'/scan',{'version':task['version'],'barcode':task['barcode'],'lotNumber':task['lotNumber'],'quantity':task['quantity'],'idempotencyKey':key()},W)
rtask=rwave['items'][0]
route=call('POST','/operations/routes',{'name':'Routed managed return QA','driverId':driver['user']['id'],'orderIds':[r['id']],'openingFloat':0,'capacityUnits':10,'vehicleReference':'QA'},A)
challenge=call('POST','/operations/routes/'+route['id']+'/manifest-challenge',{'version':route['version']},W)
route=call('POST','/courier/routes/'+route['id']+'/accept-custody',{'version':route['version'],'challenge':challenge['challenge']},D)
call('POST','/courier/orders/'+r['id']+'/status',{'status':'EN_ROUTE'},D);tracking=call('POST','/courier/orders/'+r['id']+'/status',{'status':'ARRIVED'},D)
receipt_items=[{'productId':pid,'acceptedQuantity':0,'refusedQuantity':1}]
receipt=call('POST','/orders/'+r['id']+'/receipt-challenge',{'version':tracking['version'],'items':receipt_items},M)
call('POST','/courier/orders/'+r['id']+'/complete',{'challenge':receipt['challenge'],'items':receipt_items},D)
r=call('GET','/orders/'+r['id'],token=M)
ridentity={'sourceType':'REFUSAL','sourceId':r['items'][0]['id'],'quantity':1,'lotNumber':rtask['lotNumber'],'expiresAt':rtask['expiresAt'],'proofReference':'QA routed lot physically checked','idempotencyKey':key()}
call('POST','/admin/warehouse/returns/identify',ridentity,W,409);ok('routed managed refusal cannot bypass physical depot receipt')
call('POST','/operations/routes/'+route['id']+'/returns',{'orderId':r['id'],'version':r['version'],'items':[{'productId':pid,'quantity':1}],'note':'QA counted physical return'},W)
call('POST','/admin/warehouse/returns/identify',ridentity,W)
call('POST','/admin/orders/'+r['id']+'/returns/inspect',{'productId':pid,'quantity':1,'disposition':'RESTOCK','reason':'QA physical receipt and batch verified'},A)
ok('routed managed return restocks only after depot receipt and lot identity')
call('GET','/admin/warehouse/lots',token=M,expected=403);ok('warehouse lot and task projections exclude merchants')
result={'passed':len(checks),'checks':checks,'base':BASE,'executedAt':at()};print(json.dumps(result,indent=2))
if os.environ.get('TOUSHIR_WAREHOUSE_RESULTS'):open(os.environ['TOUSHIR_WAREHOUSE_RESULTS'],'w').write(json.dumps(result,indent=2))
