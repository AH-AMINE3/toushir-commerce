#!/usr/bin/env python3
"""Executed CRM portfolio, declared contact/visit and merchant-authorized RFQ scenarios."""
import concurrent.futures,datetime as dt,json,os,pathlib,sys,time,urllib.error,urllib.request,uuid
BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5864').rstrip('/')
checks=[];requests=0;success=False
def key():return uuid.uuid4().hex
def at(hours=0):return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(hours=hours)).isoformat()
def call(path,token=None,body=None,status=200,method=None):
 global requests
 for attempt in range(3):
  requests+=1
  request=urllib.request.Request(BASE+'/api'+path,method=method or ('POST' if body is not None else 'GET'),data=json.dumps(body).encode() if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
  try:
   with urllib.request.urlopen(request,timeout=60) as r:code=r.status;data=json.load(r)
  except urllib.error.HTTPError as error:
   code=error.code
   try:data=json.load(error)
   except Exception:data={}
   if code==429 and attempt<2:
    print('Respecting authentication rate limit.',flush=True);time.sleep(min(60,max(1,int(error.headers.get('Retry-After','60')))));continue
  assert code==status,(path,code,status,data.get('error',{}).get('code'))
  return data
 raise AssertionError('Rate limit did not clear')
def ok(name,condition=True):
 assert condition,name
 checks.append(name);print('PASS '+name,flush=True)
try:
 admin=call('/demo/session',body={'role':'ADMIN'});A=admin['token'];workspace=admin['workspaceToken']
 owner=call('/demo/session',body={'role':'MERCHANT','workspaceToken':workspace});M=owner['token'];shop=owner['user']['id']
 driver=call('/demo/session',body={'role':'DRIVER','workspaceToken':workspace});D=driver['token']
 foreign=call('/demo/session',body={'role':'MERCHANT'});X=foreign['token']
 advisors=[]
 for name in ['Sara','Meriem']:
  email='crm-'+key()+'@example.invalid';password='Crm-preview-'+key()
  person=call('/operations/team',A,{'name':'EXEMPLE · '+name,'email':email,'phone':'EXEMPLE','password':password,'role':'SALES'})
  session=call('/auth/login',body={'email':email,'password':password});advisors.append(session)
 sales,other=advisors;S=sales['token'];O=other['token']
 call('/crm',status=401);call('/crm',M,status=403);call('/crm',D,status=403)
 call('/admin/dashboard',S,status=403);call('/operations/orders',S,status=403);call('/operations/team',S,status=403)
 ok('commercial staff sign in with individual accounts without wholesale, warehouse or team administration powers',not call('/crm',S)['shops'])
 def assign(advisor,version,token=A,shop_id=shop,status=200):return call('/crm/shops/'+shop_id+'/portfolio',token,{'advisorId':advisor,'version':version,'reason':'EXEMPLE · secteur commercial'},status=status)
 def new_task(kind='CONTACT',hours=1,token=S,**extra):return call('/crm/shops/'+shop+'/tasks',token,{'kind':kind,'title':'EXEMPLE · confirmer le besoin','priority':'HIGH','dueAt':at(hours),'visitEnd':at(hours+1) if kind=='VISIT' else None,**extra})
 call('/crm/shops/'+shop+'/tasks',A,{'kind':'CONTACT','title':'EXEMPLE','priority':'NORMAL','dueAt':at(1)},status=409)
 assign(driver['user']['id'],0,status=409);assign(sales['user']['id'],0,shop_id=foreign['user']['id'],status=404)
 dossier=assign(sales['user']['id'],0)
 ok('only an eligible assigned adviser obtains the primary shop portfolio',dossier['portfolio']['version']==1 and call('/crm',S)['shops'][0]['id']==shop and not call('/crm',O)['shops'])
 assign(other['user']['id'],0,status=409);assign(other['user']['id'],1,token=S,status=403)
 call('/crm/shops/'+shop,O,status=404);call('/crm/shops/'+foreign['user']['id'],S,status=404)
 ok('portfolio versions, role boundaries and foreign-shop isolation fail closed')
 task=new_task();tid=task['id']
 call('/crm/tasks/'+tid+'/finish',O,{'version':1,'outcome':'RESPONDED','note':'EXEMPLE','nextAction':'EXEMPLE','observedAt':at()},status=404)
 call('/crm/tasks/'+tid+'/reschedule',S,{'version':0,'title':'EXEMPLE','priority':'NORMAL','dueAt':at(1),'reason':'EXEMPLE · rappel déplacé'},status=409)
 finished=call('/crm/tasks/'+tid+'/finish',S,{'version':1,'outcome':'CALL_BACK','note':'EXEMPLE · personne occupée','nextAction':'EXEMPLE · rappeler demain','observedAt':at(),'followUpAt':at(24)})
 call('/crm/tasks/'+tid+'/finish',S,{'version':2,'outcome':'RESPONDED','note':'EXEMPLE','nextAction':'EXEMPLE','observedAt':at()},status=409)
 dossier=call('/crm/shops/'+shop,S)
 ok('contact completion retains the actual author, outcome and next action and creates a bounded follow-up',finished['contact']['actorId']==sales['user']['id'] and finished['task']['status']=='DONE' and len(dossier['contacts'])==1 and len(dossier['tasks'])==2)
 visit=new_task('VISIT',24)
 call('/crm/tasks/'+visit['id']+'/finish',S,{'version':1,'outcome':'VISIT_COMPLETED','note':'EXEMPLE','nextAction':'EXEMPLE','observedAt':at()},status=409)
 visit=call('/crm/tasks/'+visit['id']+'/reschedule',S,{'version':1,'title':'EXEMPLE · visite convenue','priority':'NORMAL','dueAt':at(),'visitEnd':at(1),'reason':'EXEMPLE · nouveau créneau convenu'})
 done=call('/crm/tasks/'+visit['id']+'/finish',S,{'version':visit['version'],'outcome':'VISIT_COMPLETED','note':'EXEMPLE · compte rendu déclaré','nextAction':'EXEMPLE · attendre la demande du magasin','observedAt':at()})
 ok('a future visit cannot be reported complete before its window; a rescheduled visit records self-reported attendance',done['contact']['kind']=='VISIT' and done['task']['completedBy']==sales['user']['id'])
 cancelled=new_task()
 call('/crm/tasks/'+cancelled['id']+'/cancel',S,{'version':1,'reason':'court'},status=400)
 cancelled=call('/crm/tasks/'+cancelled['id']+'/cancel',S,{'version':1,'reason':'EXEMPLE · contact reporté avec accord'})
 contact=call('/crm/shops/'+shop+'/contacts',S,{'kind':'CONTACT','outcome':'UNAVAILABLE','note':'EXEMPLE · appel déclaré sans réponse','nextAction':'EXEMPLE · attendre un retour','observedAt':at()})
 ok('reasoned cancellation preserves tasks and direct contact reports remain separate records',cancelled['status']=='CANCELLED' and contact['taskId'] is None)
 pid=call('/products?pageSize=1',M)['items'][0]['id'];address=call('/addresses',M)[0];foreign_address=call('/addresses',X)[0]
 call('/admin/products/'+pid,A,{'price':100,'stock':100,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
 call('/cart',M,{'items':[{'productId':pid,'quantity':3}]},method='PUT')
 def grant(token=M,advisor=None,version=1,status=200,**extra):
  result=call('/shop/assistance',token,{'advisorId':advisor or sales['user']['id'],'portfolioVersion':version,'addressId':address['id'],'items':[{'productId':pid,'quantity':2}],'note':'EXEMPLE · besoin exact du magasin','expiresAt':at(1),**extra},status=status)
  return result['items'][0] if status==200 else result
 call('/shop/assistance',S,status=403)
 grant(advisor=other['user']['id'],status=409);grant(expiresAt=at(24*8),status=400);grant(addressId=foreign_address['id'],status=400)
 ok('the shop authorizes only its current named adviser, bounded expiry and its own address')
 permission=grant();gid=permission['id']
 call('/crm/assistance/'+gid+'/use',O,{'version':1,'idempotencyKey':key()},status=404)
 call('/crm/assistance/'+gid+'/use',A,{'version':1,'idempotencyKey':key()},status=403)
 call('/crm/assistance/'+gid+'/use',S,{'version':0,'idempotencyKey':key()},status=409)
 use_key=key();rfq=call('/crm/assistance/'+gid+'/use',S,{'version':1,'idempotencyKey':use_key,'mode':'PLACE_ORDER','items':[{'productId':pid,'quantity':9999}],'unitPrice':0})
 order=call('/orders/'+rfq['id'],M)
 ok('assistance creates only the exact authorized RFQ under the actual adviser identity',rfq['status']=='REQUESTED' and rfq['total'] is None and order['items'][0]['quantity']==2 and order['createdBy']==sales['user']['id'] and order['timeline'][-1]['actorId']==sales['user']['id'])
 ok('the assisted RFQ neither clears the personal basket nor reserves physical stock',call('/cart',M)['items'][0]['quantity']==3 and call('/products/'+pid,M)['stock']==100)
 again=call('/crm/assistance/'+gid+'/use',S,{'version':1,'idempotencyKey':use_key})
 call('/crm/assistance/'+gid+'/use',S,{'version':2,'idempotencyKey':key()},status=409)
 call('/shop/assistance/'+gid+'/revoke',M,{'version':2,'reason':'EXEMPLE · déjà transmis'},status=409)
 call('/orders/'+rfq['id'],S,status=404)
 ok('same-key replay returns the existing request; a different use cannot create another order',again['id']==rfq['id'])
 quoted=call('/admin/orders/'+order['id']+'/quote',A,{'version':order['version'],'items':[{'productId':pid,'quantity':2,'unitPrice':100,'caseSize':1}],'deliveryFee':0,'validUntil':at(1),'campaignIds':[],'useTariffPrices':True})
 assert quoted['total']==200
 accepted=call('/orders/'+order['id']+'/accept-quote',M,{'version':quoted['quoteVersion']})
 ok('the normal shared price engine produces a quote that still requires the shop own acceptance',accepted['status']=='CONFIRMED' and accepted['total']==200)
 revoked=grant();call('/shop/assistance/'+revoked['id']+'/revoke',M,{'version':1,'reason':'EXEMPLE · besoin abandonné'})
 call('/crm/assistance/'+revoked['id']+'/use',S,{'version':1,'idempotencyKey':key()},status=409)
 ok('unused authorization revocation immediately prevents the adviser from submitting it')
 changed_address=grant()
 call('/addresses/'+address['id'],M,{'label':address['label'],'wilaya':address['wilaya'],'commune':address['commune'],'street':'EXEMPLE · autre destination','isDefault':True},method='PUT')
 call('/crm/assistance/'+changed_address['id']+'/use',S,{'version':1,'idempotencyKey':key()},status=409)
 ok('an address changed after consent cannot be substituted into the authorized RFQ')
 call('/addresses/'+address['id'],M,{'label':address['label'],'wilaya':address['wilaya'],'commune':address['commune'],'street':address['street'],'isDefault':True},method='PUT')
 pending=grant();dossier=assign(other['user']['id'],1)
 call('/crm/shops/'+shop,S,status=404)
 call('/crm/tasks/'+tid+'/cancel',S,{'version':2,'reason':'EXEMPLE · ancien conseiller'},status=404)
 call('/crm/assistance/'+pending['id']+'/use',O,{'version':2,'idempotencyKey':key()},status=403)
 ok('reassignment removes the former adviser scope and never transfers an old named authorization',dossier['portfolio']['version']==2 and any(x['id']==pending['id'] and x['status']=='REVOKED' for x in dossier['assistance']))
 members={}
 for role in ['MANAGER','BUYER','VIEWER']:
  invitation=call('/shop/team/invitations',M,{'email':'crm-member-'+key()+'@example.invalid','role':role,'expiresAt':at(1)})
  members[role]=call('/auth/shop-invitation/accept',body={'token':invitation['token'],'name':'EXEMPLE · '+role,'phone':'EXEMPLE','password':'Crm-member-'+key()})
 for role in ['BUYER','VIEWER']:grant(token=members[role]['token'],advisor=other['user']['id'],version=2,status=403)
 manager_permission=grant(token=members['MANAGER']['token'],advisor=other['user']['id'],version=2)
 member=next(x for x in call('/shop/team',M)['members'] if x['accountId']==members['MANAGER']['user']['id'])
 call('/shop/team/members/'+member['id']+'/role',M,{'version':1,'role':'VIEWER','reason':'EXEMPLE · fin du mandat de responsable'})
 call('/crm/assistance/'+manager_permission['id']+'/use',O,{'version':1,'idempotencyKey':key()},status=409)
 ok('only owner or manager may authorize, and a manager authority change invalidates pending delegated requests')
 race=grant(advisor=other['user']['id'],version=2)
 def use_once(_):
  try:return call('/crm/assistance/'+race['id']+'/use',O,{'version':1,'idempotencyKey':key()})['id']
  except AssertionError as failure:
   assert failure.args[0][1:3]==(409,200),failure
   return None
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:race_results=list(pool.map(use_once,[1,2]))
 ok('concurrent use of a one-use mandate commits exactly one RFQ',sum(x is not None for x in race_results)==1)
 suspended=grant(advisor=other['user']['id'],version=2)
 call('/admin/merchants/'+shop,A,{'approvalStatus':'SUSPENDED'},method='PATCH')
 call('/crm/assistance/'+suspended['id']+'/use',O,{'version':1,'idempotencyKey':key()},status=409)
 call('/admin/merchants/'+shop,A,{'approvalStatus':'APPROVED'},method='PATCH')
 ok('suspending the primary shop blocks pending assistance even for an otherwise active adviser')
 guest_link=call('/admin/demo/invitations',A,{'label':'EXEMPLE · assistance invitée','hours':1,'maxUses':1})
 guest=call('/demo/join',body={'inviteToken':guest_link['inviteToken'],'joinKey':key(),'name':'EXEMPLE · invité','shopName':'EXEMPLE · magasin invité CRM','phone':'EXEMPLE','commune':'Oran','street':'EXEMPLE'})
 assign(other['user']['id'],0,shop_id=guest['user']['id'])
 guest_address=call('/addresses',guest['token'])[0]
 guest_input={'advisorId':other['user']['id'],'portfolioVersion':1,'addressId':guest_address['id'],'items':[{'productId':pid,'quantity':1}],'expiresAt':at(2)}
 call('/shop/assistance',guest['token'],guest_input,status=400)
 guest_input['expiresAt']=at(.5);guest_grant=call('/shop/assistance',guest['token'],guest_input)['items'][0]
 call('/admin/demo/invitations/'+guest_link['id']+'/revoke',A,{})
 call('/crm/assistance/'+guest_grant['id']+'/use',O,{'version':1,'idempotencyKey':key()},status=409)
 ok('assistance inherits the primary guest invitation lifetime and live revocation')
 final=call('/crm/shops/'+shop,O)
 ok('CRM keeps completed/cancelled tasks, immutable contacts and actual-actor audit events',len(final['contacts'])==3 and any(e['kind']=='CRM_ASSISTANCE_USED' and e['actorId']==sales['user']['id'] for e in final['events']))
 if os.environ.get('CRM_FIXTURE'):
  path=pathlib.Path(os.environ['CRM_FIXTURE']);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps({'base':BASE,'admin':admin,'owner':owner,'sales':sales,'other':other,'productId':pid,'address':address}));path.chmod(0o600)
 success=True
finally:
 report={'at':at(),'base':BASE,'scope':'Actual assigned portfolios, self-reported contacts/visits and one-use merchant-authorized RFQs; no proof of external calls/physical attendance or native execution.','passed':len(checks),'failed':0 if success else 1,'requests':requests,'checks':checks}
 pathlib.Path(os.environ.get('CRM_REPORT','crm-verification.json')).write_text(json.dumps(report,indent=2,ensure_ascii=False));print(json.dumps({'passed':len(checks),'requests':requests}),flush=True)
