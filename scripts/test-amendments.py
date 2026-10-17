#!/usr/bin/env python3
"""Actual persisted order-amendment consent, isolation, retries and stock rollback.
Run against an isolated test server with Wholesale__DeliveryFee=0. Creates fictional data only.
"""
import datetime as dt,json,os,pathlib,sys,urllib.error,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5865').rstrip('/')+'/api'
checks=[];requests=0;success=False
def key():return uuid.uuid4().hex
def at(hours=1):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(hours=hours)).isoformat()
def call(path,token=None,body=None,status=200,method=None):
 global requests
 requests+=1
 req=urllib.request.Request(BASE+path,method=method or ('POST' if body is not None else 'GET'),data=json.dumps(body).encode() if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
 try:
  with urllib.request.urlopen(req,timeout=60) as r:code=r.status;data=json.load(r)
 except urllib.error.HTTPError as e:
  code=e.code
  try:data=json.load(e)
  except Exception:data={}
 assert code==status,(path,code,status,data.get('error',{}).get('code'))
 return data
def ok(name,condition=True):
 assert condition,name
 checks.append(name);print('PASS '+name,flush=True)
try:
 admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token'];workspace=admin['workspaceToken']
 owner=call('/demo/session',body={'role':'MERCHANT','workspaceToken':workspace});M=owner['token']
 driver=call('/demo/session',body={'role':'DRIVER','workspaceToken':workspace});D=driver['token']
 foreign=call('/demo/session',body={'role':'MERCHANT'});X=foreign['token']
 products=call('/products?pageSize=3',A)['items'];p1,p2=[p['id'] for p in products[:2]]
 def offer(pid,price,stock=100):return call('/admin/products/'+pid,A,{'price':price,'stock':stock,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
 offer(p1,100);offer(p2,200);address=call('/addresses',M)[0]
 def lines(qty=3,pid=p1):return [{'productId':pid,'quantity':qty}]
 def create(qty=3):
  cart=call('/cart/quote',M,{'items':lines(qty)})
  assert cart['canOrder'] and cart['deliveryFee']==0,'Set Wholesale__DeliveryFee=0'
  return call('/orders',M,{'idempotencyKey':key(),'addressId':address['id'],'items':lines(qty),'mode':'PLACE_ORDER','pricingToken':cart['pricingToken']})
 def current(o):return call('/orders/'+o['id'],M)
 def stock(pid):return call('/products/'+pid,A)['reserved']
 def propose(o,items=None,**extra):
  body={'version':o['version'],'items':items or [{'productId':p1,'quantity':2},{'productId':p2,'quantity':1}],'reason':'EXEMPLE · substitution convenue à vérifier','expiresAt':at(),'idempotencyKey':key(),**extra}
  return call('/admin/orders/'+o['id']+'/amendments',A,body),body
 def decide(o,p,action,token=M,status=200,**extra):
  return call('/orders/'+o['id']+'/amendments/'+p['id']+'/'+action,token,{'version':p['version'],'idempotencyKey':key(),'termsHash':p['termsHash'],'reason':'EXEMPLE · décision personnelle du magasin',**extra},status=status)
 o=create();old_total=o['total'];s1,s2=stock(p1),stock(p2)
 call('/orders/'+o['id']+'/amendments',status=401)
 call('/orders/'+o['id']+'/amendments',D,status=403);call('/orders/'+o['id']+'/amendments',X,status=404)
 call('/admin/orders/'+o['id']+'/amendments',M,{'version':o['version'],'items':lines(),'reason':'EXEMPLE interdit','expiresAt':at(),'idempotencyKey':key()},status=403)
 ok('anonymous, driver, foreign workspace and merchant-proposer boundaries fail closed')
 proposed,body=propose(o);p=proposed['proposal'];o=proposed['order']
 again=call('/admin/orders/'+o['id']+'/amendments',A,body)
 call('/admin/orders/'+o['id']+'/amendments',A,{**body,'reason':'EXEMPLE autre requête'},status=409)
 call('/admin/orders/'+o['id']+'/amendments',A,{**body,'version':o['version'],'idempotencyKey':key()},status=409)
 ok('one immutable pending proposal preserves original lines, price and reservations; exact retry returns same proposal',p['before']['total']==old_total and p['after']['total']==400 and o['total']==old_total and stock(p1)==s1 and stock(p2)==s2 and again['proposal']['id']==p['id'])
 decide(o,p,'accept',termsHash='incorrect',status=409)
 ok('acceptance requires explicit consent to the exact frozen terms hash',current(o)['total']==old_total and stock(p1)==s1)
 reject_key=key();rejected=decide(o,p,'reject',idempotencyKey=reject_key)
 replay=decide(o,p,'reject',idempotencyKey=reject_key)
 decide(o,p,'accept',status=409)
 ok('rejection and same-key retry preserve the original contract without a second decision',rejected['proposal']['status']=='REJECTED' and replay['proposal']['id']==p['id'] and current(o)['total']==old_total and stock(p1)==s1 and stock(p2)==s2)
 members={}
 for role in ['BUYER','VIEWER']:
  invitation=call('/shop/team/invitations',M,{'email':'amendment-'+key()+'@example.invalid','role':role,'expiresAt':at()})
  members[role]=call('/auth/shop-invitation/accept',body={'token':invitation['token'],'name':'EXEMPLE · '+role,'phone':'EXEMPLE','password':'Amendment-'+key()})
 proposed,_=propose(current(o));p=proposed['proposal'];o=proposed['order']
 viewer=call('/orders/'+o['id']+'/amendments',members['VIEWER']['token'])
 decide(o,p,'accept',members['VIEWER']['token'],status=403)
 ok('Viewer can inspect terms but cannot consent',not next(x for x in viewer['items'] if x['id']==p['id'])['canAccept'])
 accept_key=key();accepted=decide(o,p,'accept',members['BUYER']['token'],idempotencyKey=accept_key)
 replay=decide(o,p,'accept',members['BUYER']['token'],idempotencyKey=accept_key)
 decide(o,p,'accept',members['BUYER']['token'],status=409)
 changed=accepted['order']
 ok('authorized Buyer consent replaces the basket through shared pricing and reserves exactly once',changed['total']==400 and len(changed['items'])==2 and stock(p1)==s1-1 and stock(p2)==s2+1 and replay['order']['id']==o['id'])
 ok('actual person, original terms and new contract version remain traceable',accepted['proposal']['decidedBy']==members['BUYER']['user']['id'] and accepted['proposal']['before']['total']==300 and accepted['proposal']['after']['total']==400 and any(e['action']=='AMENDMENT_ACCEPTED' and e['actorId']==members['BUYER']['user']['id'] for e in changed['timeline']))
 # Stock changes after a proposal must roll back the original release and every new write.
 rollback=create(1);proposed,_=propose(rollback,lines(10,p2));p=proposed['proposal'];rollback=proposed['order'];before1,before2=stock(p1),stock(p2)
 offer(p2,200,before2)
 decide(rollback,p,'accept',status=409)
 unchanged=current(rollback);retained=call('/orders/'+rollback['id']+'/amendments',M)
 ok('insufficient stock atomically rolls back releases, replacement lines and proposal decision',unchanged['total']==rollback['total'] and unchanged['version']==rollback['version'] and unchanged['items'][0]['productId']==p1 and stock(p1)==before1 and stock(p2)==before2 and retained['items'][0]['status']=='PENDING')
 offer(p2,200)
 stale=create(1);proposed,_=propose(stale);p=proposed['proposal'];stale=proposed['order']
 call('/admin/orders/'+stale['id']+'/transition',A,{'status':'CONFIRMED','reason':'EXEMPLE · équipe de service'})
 stale_list=call('/orders/'+stale['id']+'/amendments',M)
 decide(stale,p,'accept',status=409)
 ok('an order version change makes the proposal visibly stale and prevents acceptance',stale_list['items'][0]['applicability']=='STALE' and not stale_list['items'][0]['canAccept'])
 withdrawn=call('/admin/orders/'+stale['id']+'/amendments/'+p['id']+'/withdraw',A,{'version':p['version'],'idempotencyKey':key(),'reason':'EXEMPLE · nouvelle version nécessaire'})
 ok('administrator withdrawal closes stale proposals without rewriting contract terms',withdrawn['proposal']['status']=='WITHDRAWN' and withdrawn['order']['total']==100)
 fresh=withdrawn['order'];call('/admin/orders/'+fresh['id']+'/transition',A,{'status':'PREPARING','reason':'EXEMPLE · préparation commencée'})
 blocked=call('/orders/'+fresh['id']+'/amendments',A)
 call('/admin/orders/'+fresh['id']+'/amendments',A,{'version':blocked['orderVersion'],'items':lines(),'reason':'EXEMPLE · trop tard','expiresAt':at(),'idempotencyKey':key()},status=409)
 ok('preparation blocks amendments even when quantities have not yet been scanned',not blocked['canPropose'])
 paid=create(1);paid=call('/admin/orders/'+paid['id']+'/transition',A,{'status':'CONFIRMED','reason':'EXEMPLE'})
 paid=call('/admin/orders/'+paid['id']+'/payments',A,{'amount':1,'method':'CASH','reference':'EXEMPLE','idempotencyKey':key()})
 call('/admin/orders/'+paid['id']+'/amendments',A,{'version':paid['version'],'items':lines(),'reason':'EXEMPLE · paiement déjà enregistré','expiresAt':at(),'idempotencyKey':key()},status=409)
 ok('an existing payment prevents contract replacement and remains recorded',paid['balance']['collected']==1)
 success=True
finally:
 report={'suite':'order-amendments','success':success,'checks':checks,'httpRequests':requests,'at':dt.datetime.now(dt.timezone.utc).isoformat(),'baseUrl':BASE,'scope':'Actual API mutations; fictional isolated fixtures; no external messages or payments.'}
 path=pathlib.Path(os.environ.get('AMENDMENTS_REPORT','amendments-verification.json'));path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n')
print(f'{len(checks)} checks passed; {requests} HTTP requests.')
