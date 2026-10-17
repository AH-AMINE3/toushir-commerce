#!/usr/bin/env python3
"""Actual shop membership, shared-resource and revocation acceptance scenarios."""
import concurrent.futures,datetime as dt,json,os,pathlib,sys,time,urllib.error,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5861').rstrip('/')
checks=[];requests=0;success=False;passwords={}
def key():return uuid.uuid4().hex
def at(hours=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(hours=hours)).isoformat()
def call(path,token=None,body=None,status=200,method=None):
 global requests
 for attempt in range(3):
  requests+=1
  req=urllib.request.Request(BASE+'/api'+path,method=method or ('POST' if body is not None else 'GET'),data=json.dumps(body).encode() if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
  try:
   with urllib.request.urlopen(req,timeout=45) as r:code=r.status;value=json.load(r)
  except urllib.error.HTTPError as e:
   code=e.code
   try:value=json.load(e)
   except Exception:value={}
   if code==429 and attempt<2:
    delay=min(60,max(1,int(e.headers.get('Retry-After','60'))));print('Respecting authentication rate limit before continuing.',flush=True);time.sleep(delay);continue
  assert code==status,(path,code,status,value.get('error',{}).get('code') if isinstance(value,dict) else 'unexpected response')
  return value
 raise AssertionError('Rate limit did not clear')
def ok(name,condition=True):
 assert condition,name
 checks.append(name);print('PASS '+name,flush=True)
try:
 admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token'];workspace=admin['workspaceToken']
 def persona(role):return call('/demo/session',body={'role':role,'workspaceToken':workspace})
 merchant=persona('MERCHANT');M=merchant['token'];shop=merchant['user']['id'];driver=persona('DRIVER');D=driver['token'];W=persona('WAREHOUSE')['token']
 foreign=call('/demo/session',body={'role':'MERCHANT'});X=foreign['token']
 call('/shop/team',status=401);call('/shop/team',A,status=403);call('/shop/team',D,status=403)
 team=call('/shop/team',M);ok('only merchant accounts can read their own shop team',team['owner']['id']==shop and team['actor']['role']=='OWNER' and not team['members'])
 def invite(role='BUYER',token=M,**extra):return call('/shop/team/invitations',token,{'email':'qa-'+key()+'@example.invalid','role':role,'expiresAt':at(12),**extra})
 call('/shop/team/invitations',M,{'email':'invalid','role':'ADMIN','expiresAt':at(12)},status=400)
 call('/shop/team/invitations',M,{'email':'qa-'+key()+'@example.invalid','role':'BUYER','expiresAt':at(24*8)},status=400)
 ok('owner cannot invite a wholesale role or exceed bounded invitation expiry')
 def accept(inv,name='EXEMPLE · personne',expected=200,**extra):
  password='Membership-'+key()
  session=call('/auth/shop-invitation/accept',body={'token':inv['token'],'name':name,'phone':'EXEMPLE','password':password,**extra},status=expected)
  if expected==200:passwords[session['user']['id']]=password
  return session
 invitations={role:invite(role) for role in ['MANAGER','BUYER','VIEWER']}
 for inv in invitations.values():
  preview=call('/auth/shop-invitation/preview',body={'token':inv['token']});assert preview['shop']==merchant['user']['shopName'] and preview['role']==inv['invitation']['role']
 accepted={role:accept(inv,'EXEMPLE · '+role) for role,inv in invitations.items()}
 manager=accepted['MANAGER'];G=manager['token'];buyer=accepted['BUYER'];B=buyer['token'];viewer=accepted['VIEWER'];V=viewer['token']
 ok('one-use private invitations create individual password accounts in the existing shop',all(x['user']['shop']['id']==shop and x['user']['shop']['isMember'] and not x['workspace']['canSwitchRole'] for x in accepted.values()))
 accept(invitations['BUYER'],expected=409)
 call('/auth/shop-invitation/preview',body={'token':'0'*64},status=404)
 ok('used and unknown tokens cannot create duplicate or standalone shops')
 revoked=invite();call('/shop/team/invitations/'+revoked['invitation']['id']+'/revoke',M,{'version':1,'reason':'EXEMPLE · lien remplacé'})
 accept(revoked,expected=409)
 call('/shop/team/invitations/'+invitations['MANAGER']['invitation']['id']+'/revoke',X,{'version':1,'reason':'EXEMPLE · intrusion'},status=404)
 ok('invitation revocation and shop boundaries are checked at acceptance and management')
 call('/shop/team/invitations',B,{'email':'qa-'+key()+'@example.invalid','role':'VIEWER','expiresAt':at(12)},status=403)
 call('/shop/team/invitations',G,{'email':'qa-'+key()+'@example.invalid','role':'MANAGER','expiresAt':at(12)},status=403)
 extra=invite('VIEWER',G);extra_member=accept(extra)
 ok('a manager can invite buyers or viewers but cannot grant manager or owner powers')
 team=call('/shop/team',M);members={x['role']:x for x in team['members'] if x['accountId'] in [manager['user']['id'],buyer['user']['id'],viewer['user']['id']]}
 call('/shop/team/members/'+members['BUYER']['id']+'/role',G,{'version':1,'role':'MANAGER','reason':'EXEMPLE · escalade'},status=403)
 call('/shop/team/members/'+members['MANAGER']['id']+'/revoke',G,{'version':1,'reason':'EXEMPLE · auto modification'},status=403)
 call('/shop/team/members/'+members['BUYER']['id']+'/role',M,{'version':0,'role':'VIEWER','reason':'EXEMPLE · version périmée'},status=409)
 ok('manager escalation, manager modification and stale access versions fail closed')
 pid=call('/products?pageSize=1',M)['items'][0]['id'];address=call('/addresses',B)[0]
 assert address['id']==call('/addresses',M)[0]['id']
 call('/admin/products/'+pid,A,{'price':100,'stock':100,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
 call('/cart',B,{'items':[{'productId':pid,'quantity':2}]},method='PUT')
 call('/cart',V,{'items':[{'productId':pid,'quantity':1}]},method='PUT')
 ok('shared shop addresses coexist with strictly personal baskets',call('/cart',B)['items'][0]['quantity']==2 and call('/cart',V)['items'][0]['quantity']==1 and not call('/cart',M)['items'])
 call('/favorites/'+pid,B,{})
 ok('favorites belong to the person rather than the shared shop',any(x['id']==pid for x in call('/favorites',B)) and not call('/favorites',V) and not call('/favorites',M))
 call('/admin/products/'+pid,B,{'price':1},method='PATCH',status=403)
 ok('a shop buyer cannot acquire wholesale administration powers')
 proof=call('/cart',B)
 call('/orders',M,{'idempotencyKey':key(),'addressId':address['id'],'mode':'PLACE_ORDER','items':[{'productId':pid,'quantity':2}],'pricingToken':proof['pricingToken']},status=409)
 ordered=call('/orders',B,{'idempotencyKey':key(),'addressId':address['id'],'mode':'PLACE_ORDER','items':[{'productId':pid,'quantity':2}],'pricingToken':proof['pricingToken']})
 oid=ordered['id']
 ok('reviewed price consent binds the actual buyer and shop; creation retains the person',ordered['merchant']['id']==shop and ordered['createdBy']==buyer['user']['id'] and ordered['timeline'][-1]['actorId']==buyer['user']['id'])
 ok('owner, manager and viewer read the same shop order and frozen prices',all(call('/orders/'+oid,t)['id']==oid for t in [M,G,V]) and call('/orders/'+oid+'/pricing-history',V)['items'][0]['appliedUnitPriceMinor']==10000)
 call('/orders/'+oid,X,status=404);call('/orders/'+oid+'/tracking',X,status=404)
 call('/orders',V,{'idempotencyKey':key(),'addressId':address['id'],'items':[{'productId':pid,'quantity':1}]},status=403)
 call('/orders/'+oid+'/cancel',V,{'reason':'EXEMPLE · lecture seule'},status=403)
 call('/orders/'+oid+'/accept-quote',V,{'version':1},status=403)
 call('/addresses',B,{'label':'EXEMPLE','wilaya':'Oran','commune':'Oran','street':'EXEMPLE','isDefault':False},status=403)
 call('/preferences/marketing',B,{'optIn':True},method='PUT',status=403)
 call('/rewards/unknown/reserve',B,{'idempotencyKey':key()},status=403)
 ok('viewer cannot commit or change shared commerce; buyers cannot change contacts or spend gifts')
 call('/profile',B,{'name':'EXEMPLE · prénom','phone':'EXEMPLE','shopName':'Magasin renommé sans accord'},method='PATCH',status=403)
 changed=call('/profile',B,{'name':'EXEMPLE · prénom','phone':'EXEMPLE','shopName':merchant['user']['shopName']},method='PATCH')
 ok('a person can edit their own profile without renaming the primary shop',changed['name']=='EXEMPLE · prénom' and call('/profile',M)['shopName']==merchant['user']['shopName'])
 thread=call('/conversations',B,{'contextType':'ORDER','contextId':oid,'subject':'EXEMPLE · conversation partagée'})
 msg=call('/conversations/'+thread['id']+'/messages',B,{'text':'EXEMPLE · depuis le compte acheteur','internal':False,'idempotencyKey':key()})
 call('/conversations/'+thread['id']+'/messages',V,{'text':'EXEMPLE interdit','internal':False,'idempotencyKey':key()},status=403)
 ok('shared messages retain their actual sender and remain read-only for viewers',msg['senderId']==buyer['user']['id'] and call('/conversations/'+thread['id']+'/messages',V)['items'][-1]['id']==msg['id'])
 customers=call('/admin/merchants?pageSize=100',A)['items'];price_targets=call('/admin/pricing',A)['merchants']
 ok('people are excluded from wholesale customer and customer-tariff target lists',all(x['id'] not in [y['user']['id'] for y in accepted.values()] for x in customers+price_targets))
 pending_by_manager=invite('VIEWER',G)
 call('/shop/team/members/'+members['MANAGER']['id']+'/role',M,{'version':1,'role':'BUYER','reason':'EXEMPLE · changement de mission'})
 call('/auth/me',G,status=401);accept(pending_by_manager,expected=409)
 ok('changing a manager role revokes existing sessions and unaccepted delegated invitations')
 extra_row=next(x for x in call('/shop/team',M)['members'] if x['accountId']==extra_member['user']['id'])
 call('/shop/team/members/'+extra_row['id']+'/revoke',M,{'version':1,'reason':'EXEMPLE · fin de mission'})
 call('/auth/me',extra_member['token'],status=401)
 ok('revoked membership is retained and cannot fall back to an independent shop',next(x for x in call('/shop/team',M)['members'] if x['id']==extra_row['id'])['status']=='REVOKED')
 call('/admin/merchants/'+shop,A,{'approvalStatus':'SUSPENDED'},method='PATCH')
 for t in [M,B,V]:call('/auth/me',t,status=401)
 call('/admin/merchants/'+shop,A,{'approvalStatus':'APPROVED'},method='PATCH')
 ok('suspending the primary shop disables every member account immediately')
 # Race two acceptances of one bearer invitation. The winner is the only account.
 race=invite('VIEWER');race_password='Membership-'+key()
 def attempt_join(_):
  try:
   call('/auth/shop-invitation/accept',body={'token':race['token'],'name':'EXEMPLE · course','phone':'EXEMPLE','password':race_password})
   return True
  except AssertionError as failure:
   assert failure.args[0][1:3]==(409,200),failure
   return False
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(attempt_join,[1,2]))
 ok('concurrent acceptance commits exactly one account and one permanent membership',sum(results)==1 and sum(x['email']==race['invitation']['email'] for x in call('/shop/team',M)['members'])==1)
 guest_invitation=call('/admin/demo/invitations',A,{'label':'EXEMPLE · durée héritée','hours':1,'maxUses':1})
 guest=call('/demo/join',body={'inviteToken':guest_invitation['inviteToken'],'joinKey':key(),'name':'EXEMPLE · propriétaire invité','shopName':'EXEMPLE · magasin invité','phone':'EXEMPLE','commune':'Oran','street':'EXEMPLE'})
 call('/shop/team/invitations',guest['token'],{'email':'qa-'+key()+'@example.invalid','role':'BUYER','expiresAt':at(2)},status=409)
 ok('a guest shop cannot issue a member invitation beyond its own authority lifetime')
 child_invite=invite('BUYER',guest['token'],expiresAt=at(.5))
 child=accept(child_invite,'EXEMPLE · membre du magasin invité')
 call('/admin/demo/invitations/'+guest_invitation['id']+'/revoke',A,{})
 call('/auth/me',guest['token'],status=401);call('/auth/me',child['token'],status=401)
 ok('revoking a primary guest invitation immediately disables its shop and child members')
 # Prepare a real fictional delivery through the existing operational APIs.
 call('/admin/orders/'+oid+'/transition',A,{'status':'CONFIRMED','note':'EXEMPLE'})
 ordered=call('/orders/'+oid,M)
 tracking=call('/operations/orders/'+oid+'/preparation',W,{'version':ordered['version'],'items':[{'productId':pid,'preparedQuantity':2}]},method='PATCH')
 tracking=call('/operations/orders/'+oid+'/assign',A,{'driverId':driver['user']['id'],'version':tracking['version']})
 tracking=call('/courier/orders/'+oid+'/pickup',D,{'version':tracking['version']})
 call('/courier/orders/'+oid+'/status',D,{'status':'EN_ROUTE'});tracking=call('/courier/orders/'+oid+'/status',D,{'status':'ARRIVED'})
 receipt_items=[{'productId':pid,'acceptedQuantity':2,'refusedQuantity':0}]
 receipt=call('/orders/'+oid+'/receipt-challenge',B,{'version':tracking['version'],'items':receipt_items})
 ok('delivery consent records the issuing person while binding the primary shop',receipt['merchantId']==shop and receipt['issuedBy']==buyer['user']['id'])
 if os.environ.get('MEMBERSHIP_FIXTURE'):
  target=pathlib.Path(os.environ['MEMBERSHIP_FIXTURE']);target.parent.mkdir(parents=True,exist_ok=True)
  target.write_text(json.dumps({'base':BASE,'admin':admin,'owner':merchant,'buyer':buyer,'viewer':viewer,'foreign':foreign,'driver':driver,'order':ordered,'productId':pid,'member':members['BUYER'],'viewerMember':members['VIEWER'],'receipt':receipt,'receiptItems':receipt_items,'threadId':thread['id'],'buyerPassword':passwords[buyer['user']['id']]}));target.chmod(0o600)
 success=True
finally:
 report={'at':at(),'base':BASE,'scope':'Actual membership API, actor/shop boundaries, personal carts, shared commerce and existing delivery proof issuance. Live sockets and OIDC revocation are separate.','passed':len(checks),'failed':0 if success else 1,'requests':requests,'checks':checks}
 pathlib.Path(os.environ.get('MEMBERSHIP_REPORT','shop-members-verification.json')).write_text(json.dumps(report,indent=2,ensure_ascii=False));print(json.dumps({'passed':len(checks),'requests':requests}),flush=True)
