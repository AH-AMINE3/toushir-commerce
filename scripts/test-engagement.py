#!/usr/bin/env python3
"""Run against an explicitly enabled demo API; creates its own isolated workspace."""
import concurrent.futures, datetime as dt, json, os, sys, time, urllib.error, urllib.parse, urllib.request, uuid

BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5882').rstrip('/')+'/api'
checks=[]
def key(): return uuid.uuid4().hex
def at(seconds=0): return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(seconds=seconds)).isoformat()
def call(method,path,body=None,token=None,expected=200,retries=0):
    req=urllib.request.Request(BASE+path,data=json.dumps(body).encode() if body is not None else None,method=method,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
    try:
        with urllib.request.urlopen(req,timeout=90) as r: status=r.status; data=json.load(r)
    except urllib.error.HTTPError as e:
        status=e.code
        try:data=json.load(e)
        except Exception:data={}
    if status==429 and retries<35:
        time.sleep(2)
        return call(method,path,body,token,expected,retries+1)
    assert status in expected if isinstance(expected,tuple) else status==expected,(method,path,status,expected,data)
    return data
def ok(name,condition=True):
    assert condition,name
    checks.append(name)
    print('PASS',name,flush=True)

call('GET','/conversations',expected=401);ok('anonymous cannot read conversations')
admin=call('POST','/demo/session',{'role':'ADMIN'})
workspace=admin['workspaceToken']; A=admin['token']
def persona(role):return call('POST','/demo/session',{'role':role,'workspaceToken':workspace})
merchant=persona('MERCHANT');M=merchant['token'];mid=merchant['user']['id']
finance=persona('FINANCE');F=finance['token']
warehouse=persona('WAREHOUSE');W=warehouse['token']
driver=persona('DRIVER');D=driver['token']
outsider=call('POST','/demo/session',{'role':'ADMIN'});X=outsider['token']
product=call('GET','/products?pageSize=1',token=M)['items'][0];pid=product['id']

supplier=call('POST','/admin/suppliers',{'name':'QA fournisseur fictif','contact':'Équipe QA','phone':'Exemple','note':'Test isolé'},A)
po_key=key();payload={'supplierId':supplier['id'],'expectedAt':at(86400),'items':[{'productId':pid,'quantity':100,'unitCostMinor':120000}],'note':'QA','idempotencyKey':po_key}
po=call('POST','/admin/purchases',payload,A);poid=po['order']['id']
again=call('POST','/admin/purchases',payload,A);ok('supply order creation is idempotent',again['order']['id']==poid)
call('POST',f'/admin/purchases/{poid}/approve',{'version':1},A,409);ok('purchase creator cannot approve own purchase')
po=call('POST',f'/admin/purchases/{poid}/approve',{'version':1},F)
receive={'version':po['order']['version'],'idempotencyKey':key(),'lines':[{'productId':pid,'receivedQuantity':96,'quarantineQuantity':6,'lotNumber':'QA-LOT-01','expiresAt':at(86400*365)}],'note':'96 reçus dont6 isolés'}
po=call('POST',f'/admin/purchases/{poid}/receive',receive,W)
line=po['items'][0];rl=po['receiptLines'][0]
ok('partial supplier receipt reconciles 96/6/4',line['receivedQuantity']==96 and line['quarantineQuantity']==6 and line['outstandingQuantity']==4)
call('POST',f'/admin/purchases/{poid}/receive',receive,W)
stock=call('GET','/admin/products?q='+urllib.parse.quote(product['name'])+'&pageSize=100',token=A)['items'];p=next(x for x in stock if x['id']==pid)
ok('receipt replay adds exactly90 sellable units',p['physicalStock']==90 and p['quarantined']==6)
changed={**receive,'lines':[{**receive['lines'][0],'receivedQuantity':95}]}
call('POST',f'/admin/purchases/{poid}/receive',changed,W,409);ok('changed receipt with same key rejected')
inspection={'receiptLineId':rl['id'],'version':rl['version'],'quantity':4,'disposition':'RELEASE','reason':'Lot contrôlé','idempotencyKey':key()}
inspect=call('POST',f'/admin/purchases/{poid}/inspect',inspection,W);again=call('POST',f'/admin/purchases/{poid}/inspect',inspection,W);ok('quality inspection is idempotent',again['id']==inspect['id'])
po=call('GET',f'/admin/purchases/{poid}',token=A)
invoice=call('POST',f'/admin/purchases/{poid}/invoice-review',{'version':po['order']['version'],'invoiceReference':'QA-INV','invoiceMinor':96*120000},F)
ok('supplier invoice matches received cost without payment',invoice['order']['invoiceMatchStatus']=='MATCHED' and invoice['paymentRecorded'] is False)
call('GET','/admin/purchases',token=M,expected=403);call('GET',f'/admin/purchases/{poid}',token=X,expected=404);ok('supply records are role and workspace isolated')

thread=call('POST','/conversations',{'contextType':'GENERAL','contextId':None,'subject':'Question QA','merchantId':None},M);tid=thread['id']
message={'text':'Bonjour Toushir','internal':False,'idempotencyKey':key()}
msg=call('POST',f'/conversations/{tid}/messages',message,M)
ok('message replay returns same message',call('POST',f'/conversations/{tid}/messages',message,M)['id']==msg['id'])
call('POST',f'/conversations/{tid}/messages',{**message,'text':'Changed'},M,409)
call('POST',f'/conversations/{tid}/messages',{'text':'Note confidentielle équipe','internal':True,'idempotencyKey':key()},A)
visible=call('GET',f'/conversations/{tid}/messages',token=M)
ok('internal notes excluded from merchant projection',len(visible['items'])==1 and not visible['items'][0]['internal'])
call('POST',f'/conversations/{tid}/messages',{'text':'Note interdite','internal':True,'idempotencyKey':key()},M,403)
call('GET',f'/conversations/{tid}/messages',token=X,expected=404)
call('GET',f'/conversations/{tid}/messages',token=D,expected=404);ok('thread object authorization enforced')
pref=call('GET','/preferences/marketing',token=M);ok('marketing defaults to opt out',not pref['optIn'])
def broadcast(purpose):
    b=call('POST','/admin/broadcasts',{'purpose':purpose,'title':'Message QA','body':'Contenu QA','merchantIds':[mid],'idempotencyKey':key()},A)
    return call('POST','/admin/broadcasts/'+b['id']+'/dispatch',{},A)
b=broadcast('PROMOTIONAL');ok('opted-out promotional audience excluded',b['broadcast']['sent']==0 and b['broadcast']['excluded']==1)
call('PUT','/preferences/marketing',{'optIn':True},M)
b=broadcast('PROMOTIONAL');ok('opted-in promotional in-app message persisted',b['broadcast']['sent']==1)
repeat=call('POST','/admin/broadcasts/'+b['broadcast']['id']+'/dispatch',{},A);ok('dispatch replay does not duplicate messages',repeat['broadcast']['sent']==1)
call('PUT','/preferences/marketing',{'optIn':False},M)
b=broadcast('SERVICE');ok('service message distinct from promotional preference',b['broadcast']['sent']==1)

call('POST','/admin/inventory/adjust',{'productId':pid,'delta':200,'reason':'QA réserve'},A)
call('PATCH','/admin/products/'+pid,{'price':1200,'stock':294,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},A)
address=call('GET','/addresses',token=M)[0]
reward=call('POST','/admin/rewards',{'name':'Cadeau QA','description':'Récompense test','imageUrl':'','pointsCost':900,'availableStock':2,'isActive':True},A);rid=reward['id']
challenge_end=at(8)
challenge={'title':'QA challenge publié avant achat','description':'Achats acceptés réglés après publication','kind':'CHALLENGE','productId':pid,'minimumQuantity':1,'discountPercent':0,'giftProductId':None,'giftQuantity':1,'goalQuantity':700,'unitFactor':6,'rewardId':rid,'awardLimit':1,'startsAt':at(-1),'endsAt':challenge_end,'claimUntil':at(86400)}
challenge_campaign=call('POST','/admin/campaigns',challenge,A);call('POST','/admin/campaigns/'+challenge_campaign['id']+'/publish',{'version':1},F)
order=call('POST','/orders',{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':128}],'note':'QA points','mode':'REQUEST_QUOTE'},M);oid=order['id']
order=call('POST',f'/admin/orders/{oid}/quote',{'items':[{'productId':pid,'quantity':128,'unitPrice':1000,'caseSize':1,'availableStock':None}],'deliveryFee':1000,'validUntil':at(86400),'note':'QA'},A)
order=call('POST',f'/orders/{oid}/accept-quote',{'version':order['quoteVersion']},M)
for state in ['PREPARING','READY','OUT_FOR_DELIVERY']:order=call('POST',f'/admin/orders/{oid}/transition',{'status':state,'reason':'QA'},A)
order=call('POST',f'/admin/orders/{oid}/delivery',{'items':[{'productId':pid,'acceptedQuantity':128,'refusedQuantity':0}],'note':'QA'},A)
call('POST',f'/rewards/{rid}/reserve',{'idempotencyKey':key(),'grantId':None},M,409);ok('unpaid goods cannot create spendable points')
call('POST',f'/admin/orders/{oid}/payments',{'amount':129000,'method':'CASH','reference':'QA','idempotencyKey':key()},A)
loyalty=call('POST','/loyalty/sync',{},M);ok('paid accepted goods earn1280 points excluding delivery',loyalty['available']==1280)
again=call('POST','/loyalty/sync',{},M);ok('point accrual repeated without double credit',again['available']==1280 and len(again['entries'])==len(loyalty['entries']))
reserve={'idempotencyKey':key(),'grantId':None};gift=call('POST',f'/rewards/{rid}/reserve',reserve,M)
again=call('POST',f'/rewards/{rid}/reserve',reserve,M);ok('gift reservation replay returns original',again['id']==gift['id'])
wallet=call('GET','/loyalty',token=M);ok('1280 becomes380 available plus900 reserved',wallet['available']==380 and wallet['reserved']==900)
call('POST',f'/rewards/{rid}/reserve',{'idempotencyKey':key(),'grantId':None},M,409);ok('second gift cannot overdraw points')
gift=call('POST','/admin/gift-requests/'+gift['id']+'/transition',{'version':gift['version'],'status':'READY','proofReference':None,'reason':None},A)
fulfilled={'version':gift['version'],'status':'FULFILLED','proofReference':'QA-BENEFICIARY-PROOF','reason':None};gift=call('POST','/admin/gift-requests/'+gift['id']+'/transition',fulfilled,W)
call('POST','/admin/gift-requests/'+gift['id']+'/transition',fulfilled,W)
wallet=call('GET','/loyalty',token=M);ok('gift delivery consumes reserved points exactly once',wallet['available']==380 and wallet['reserved']==0 and wallet['spent']==900)
call('POST','/admin/gift-requests/'+gift['id']+'/transition',{**fulfilled,'proofReference':'DIFFERENT'},W,409)
call('POST','/admin/gift-requests/'+gift['id']+'/transition',{'version':gift['version'],'status':'CANCELLED','proofReference':None,'reason':'Too late'},A,409);ok('fulfilled reward cannot be canceled or assigned new proof')
small=call('POST','/admin/rewards',{'name':'Cadeau200','description':'QA','imageUrl':'','pointsCost':200,'availableStock':1,'isActive':True},A)
g=call('POST','/rewards/'+small['id']+'/reserve',{'idempotencyKey':key(),'grantId':None},M)
cancel={'version':g['version'],'status':'CANCELLED','proofReference':None,'reason':'Annulation QA'}
call('POST','/admin/gift-requests/'+g['id']+'/transition',cancel,A);call('POST','/admin/gift-requests/'+g['id']+'/transition',cancel,A)
wallet=call('GET','/loyalty',token=M);ok('reward cancellation releases points once',wallet['available']==380 and wallet['reserved']==0)

campaign={'title':'QA remise5%','description':'Dès5 unités; prix fictifs','kind':'DISCOUNT','productId':pid,'minimumQuantity':5,'discountPercent':5,'giftProductId':None,'giftQuantity':1,'goalQuantity':0,'unitFactor':1,'rewardId':None,'awardLimit':1,'startsAt':at(-86400),'endsAt':at(86400),'claimUntil':None}
c=call('POST','/admin/campaigns',campaign,A)
call('POST','/admin/campaigns/'+c['id']+'/publish',{'version':1},A,409)
c=call('POST','/admin/campaigns/'+c['id']+'/publish',{'version':1},F);ok('campaign publication requires independent reviewer',c['approvedBy']==finance['user']['id'])
discount_id=c['id']
preview=call('POST','/campaigns/'+c['id']+'/preview',{'quantity':5},M);ok('campaign discount deterministic at threshold',preview['goodsAfter']==5700 and preview['discount']==300)
preview=call('POST','/campaigns/'+c['id']+'/preview',{'quantity':4},M);ok('discount absent below threshold',preview['discount']==0)
free={**campaign,'title':'QA20+1','kind':'FREE_GOODS','minimumQuantity':20,'discountPercent':0,'giftProductId':pid}
c=call('POST','/admin/campaigns',free,A);call('POST','/admin/campaigns/'+c['id']+'/publish',{'version':1},F)
free_id=c['id']
preview=call('POST','/campaigns/'+c['id']+'/preview',{'quantity':40},M);ok('free goods cap at one gift per order',preview['freeQuantity']==1)
c=challenge_campaign
while dt.datetime.now(dt.timezone.utc)<dt.datetime.fromisoformat(challenge_end):time.sleep(.2)
progress=call('GET','/campaigns/'+c['id']+'/progress',token=M);ok('challenge counts accepted paid units times sales factor',progress['eligibleQuantity']==768)
grant=call('POST','/admin/campaigns/'+c['id']+'/award',{'merchantId':mid},F)
again=call('POST','/admin/campaigns/'+c['id']+'/award',{'merchantId':mid},F);ok('challenge award is unique per merchant',again['id']==grant['id'])
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    claims=list(pool.map(lambda _:call('POST','/rewards/'+rid+'/reserve',{'idempotencyKey':key(),'grantId':grant['id']},M,(200,409)),range(2)))
claimed=[r for r in claims if 'id' in r]
ok('concurrent challenge claims create exactly one gift',len(claimed)==1)
gift=claimed[0]
wallet=call('GET','/loyalty',token=M);ok('campaign reward reserves stock without debiting points',gift['pointsCost']==0 and wallet['available']==380)
call('GET','/loyalty?merchantId='+mid,token=X,expected=404)
ok('loyalty projection is workspace isolated')

# Actual quotes use explicit campaign selection and immutable snapshots, beyond the preview endpoint.
def requested(quantity):
    return call('POST','/orders',{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':quantity}],'note':'QA promotion réelle','mode':'REQUEST_QUOTE'},M)
def quoted(o,quantity,campaign_id):
    return call('POST','/admin/orders/'+o['id']+'/quote',{'version':o['version'],'campaignIds':[campaign_id],'items':[{'productId':pid,'quantity':quantity,'unitPrice':1200,'caseSize':1,'availableStock':None}],'deliveryFee':1000,'validUntil':at(86400),'note':'Prix de base avant campagne'},A)
def active_promotion(o):
    records=call('GET','/orders/'+o['id']+'/promotions',token=M)['items']
    return next(p for p in records if p['quoteVersion']==o['quoteVersion'] and p['status']!='SUPERSEDED')
def dispatched(o):
    for state in ['PREPARING','READY','OUT_FOR_DELIVERY']:o=call('POST','/admin/orders/'+o['id']+'/transition',{'status':state,'reason':'QA promotion'},A)
    return o
o=requested(5);o=quoted(o,5,discount_id)
ok('published selected discount applies to actual quote',o['items'][0]['unitPrice']==1140 and o['total']==6700)
o=quoted(o,5,discount_id)
history=call('GET','/orders/'+o['id']+'/promotions',token=M)['items']
ok('quote revision preserves previous campaign snapshot',len(history)==2 and any(x['status']=='SUPERSEDED' for x in history))
call('POST','/orders/'+o['id']+'/accept-quote',{'version':1},M,409);ok('stale campaign quote cannot be accepted')
o=call('POST','/orders/'+o['id']+'/accept-quote',{'version':2},M);o=dispatched(o)
o=call('POST','/admin/orders/'+o['id']+'/delivery',{'items':[{'productId':pid,'acceptedQuantity':4,'refusedQuantity':1}],'note':'Un refus'},A)
p=active_promotion(o)
ok('partial receipt preserves agreed unit price and reverses refused discount',o['total']==5560 and o['items'][0]['unitPrice']==1140 and p['effectiveDiscountMinor']==24000)
claim=call('POST','/orders/'+o['id']+'/claims',{'productId':pid,'quantity':1,'reason':'Retour QA'},M)
call('POST','/admin/claims/'+claim['id']+'/resolve',{'status':'ACCEPTED','note':'Retour autorisé'},A)
call('POST','/admin/claims/'+claim['id']+'/receive',{'quantity':1,'note':'Retour physique reçu'},A)
p=active_promotion(o);ok('physical post-delivery return reverses matching promotion once',p['returnedTriggerQuantity']==1 and p['effectiveDiscountMinor']==18000)

o=requested(40);o=quoted(o,40,free_id);o=call('POST','/orders/'+o['id']+'/accept-quote',{'version':o['quoteVersion']},M)
p=active_promotion(o);ok('actual free-goods quote reserves exactly one separate gift',p['giftQuantity']==1 and p['giftStockState']=='RESERVED' and len(o['items'])==1)
o=dispatched(o);p=active_promotion(o);ok('promotional free-gift stock follows actual departure',p['giftStockState']=='IN_TRANSIT')
o=call('POST','/admin/orders/'+o['id']+'/delivery',{'items':[{'productId':pid,'acceptedQuantity':19,'refusedQuantity':21}],'note':'Seuil perdu à la remise'},A)
p=active_promotion(o);ok('partial delivery below gift threshold requires physical return',p['giftStockState']=='RETURN_REQUIRED')
gift_path='/orders/'+o['id']+'/promotions/'+p['id']
call('POST',gift_path+'/gift-receipt',{'version':p['version'],'acceptedQuantity':1,'refusedQuantity':0,'proofReference':'Invalid eligible gift','idempotencyKey':key()},A,409)
p=call('POST',gift_path+'/gift-receipt',{'version':p['version'],'acceptedQuantity':0,'refusedQuantity':1,'proofReference':'Cadeau refusé sous garde','idempotencyKey':key()},A)
ret={'version':p['version'],'disposition':'QUARANTINE','reason':'Reçu au dépôt','idempotencyKey':key()}
p=call('POST',gift_path+'/gift-return',ret,W);again=call('POST',gift_path+'/gift-return',ret,W)
ok('promotional gift return enters quarantine once',p['giftStockState']=='RETURNED_QUARANTINE' and p['id']==again['id'])
p=call('POST',gift_path+'/gift-inspect',{'version':p['version'],'disposition':'RELEASE','reason':'Produit intact contrôlé','idempotencyKey':key()},W)
ok('returned free gift restocks only after quality decision',p['giftStockState']=='RESTOCKED')
o=requested(20);o=quoted(o,20,free_id);o=call('POST','/orders/'+o['id']+'/accept-quote',{'version':o['quoteVersion']},M);o=dispatched(o)
o=call('POST','/admin/orders/'+o['id']+'/delivery',{'items':[{'productId':pid,'acceptedQuantity':20,'refusedQuantity':0}],'note':'Tous acceptés'},A)
p=active_promotion(o);gift_path='/orders/'+o['id']+'/promotions/'+p['id'];gift_proof={'version':p['version'],'acceptedQuantity':1,'refusedQuantity':0,'proofReference':'Cadeau remis au bon bénéficiaire','idempotencyKey':key()}
call('POST',gift_path+'/gift-receipt',gift_proof,A,409);ok('unpaid trigger goods cannot release promotional gift')
call('POST','/admin/orders/'+o['id']+'/payments',{'amount':25000,'method':'CASH','reference':'QA promotion','idempotencyKey':key()},A)
p=call('POST',gift_path+'/gift-receipt',gift_proof,A);again=call('POST',gift_path+'/gift-receipt',gift_proof,A)
ok('accepted paid trigger goods allow one proven gift handover',p['giftStockState']=='DELIVERED' and p['giftAcceptedQuantity']==1 and p['id']==again['id'])

# A credit already reduces the order's subtotal; reconciliation must not subtract it twice.
before_credit=call('POST','/loyalty/sync',{},M)
cost_before_correction=before_credit['available']-5
correction_reward=call('POST','/admin/rewards',{'name':'QA solde avant correction','description':'Test de solde courant','imageUrl':'','pointsCost':cost_before_correction,'availableStock':1,'isActive':True},A)
claim=call('POST','/orders/'+oid+'/claims',{'productId':pid,'quantity':1,'reason':'QA correction de points'},M)
call('POST','/admin/claims/'+claim['id']+'/resolve',{'status':'ACCEPTED','note':'QA'},A)
call('POST','/admin/claims/'+claim['id']+'/receive',{'quantity':1,'note':'QA retour'},A)
call('POST','/admin/claims/'+claim['id']+'/credit',{'amount':1000,'reason':'QA avoir','idempotencyKey':key()},A)
call('POST','/rewards/'+correction_reward['id']+'/reserve',{'idempotencyKey':key(),'grantId':None},M,409)
ok('recent credit cannot be spent through a stale wallet before manual sync')
wallet=call('POST','/loyalty/sync',{},M)
source_entries=[x for x in wallet['entries'] if x['sourceRef']==oid]
ok('commercial credit corrects earned points once without double subtraction',sum(x['orderPointDelta'] for x in source_entries)==1270)
result={'passed':len(checks),'checks':checks,'base':BASE,'executedAt':at()}
print(json.dumps(result,ensure_ascii=False,indent=2))
if os.environ.get('TOUSHIR_TEST_RESULTS'):open(os.environ['TOUSHIR_TEST_RESULTS'],'w').write(json.dumps(result,ensure_ascii=False,indent=2))
