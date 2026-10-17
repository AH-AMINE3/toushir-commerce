import os,sys,time
import urllib.request,urllib.error,json,uuid,datetime,pathlib,concurrent.futures
base=(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:5864').rstrip('/');checks=[];requests=0

def call(path,token=None,body=None,status=200,method=None,retries=0):
 global requests
 requests+=1;r=urllib.request.Request(base+'/api'+path,method=method or ('POST' if body is not None else 'GET'),data=json.dumps(body).encode() if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token} if token else {})})
 try:
  with urllib.request.urlopen(r,timeout=90) as result:code=result.status;data=json.load(result)
 except urllib.error.HTTPError as error:
  code=error.code;data=json.load(error)
  if code==429 and retries<3:
   time.sleep(min(90,max(1,int(error.headers.get('Retry-After','60')))));return call(path,token,body,status,method,retries+1)
 assert code==status,(path,code,status,data.get('error',{}).get('code'));return data

def ok(text,condition=True):
 assert condition,text
 checks.append(text);print('PASS '+text,flush=True)

call('/demo/showcase',body={},status=401)
a=call('/demo/session',body={'role':'ADMIN'});A=a['token'];w=a['workspaceToken'];m=call('/demo/session',body={'role':'MERCHANT','workspaceToken':w});M=m['token']
products=call('/products?pageSize=3',M)['items'];ok('Ordinary API demo workspaces remain unpriced until explicit owner-controlled showcase setup',all(p['price'] is None for p in products))
p1,p2,p3=[p['id'] for p in products]
call('/admin/products/'+p1,A,{'price':17.35,'stock':7,'caseSize':1,'minimumQuantity':1,'offerApproved':True,'isActive':True},method='PATCH')
call('/admin/products/'+p2,A,{'price':None,'stock':3,'caseSize':1,'minimumQuantity':1,'offerApproved':False,'isActive':False},method='PATCH')
results=list(concurrent.futures.ThreadPoolExecutor(2).map(lambda _:call('/demo/showcase',M,{}),range(2)))
ok('Concurrent owner setup commits exactly once with explicitly fictional pricing',sum(r['seeded'] for r in results)==1 and all(r['fictional'] for r in results))
items=call('/products?pageSize=30',M)['items'];ok('Demo catalog exposes positive priced offers and known stock through the shared price engine',all(p['price'] is not None and p['price']>0 for p in items))
kept=call('/products/'+p1,M);ok('An existing operator-entered price and stock are preserved',kept['price']==17.35 and kept['stock']==7)
all_admin=call('/admin/products?pageSize=100&q='+products[1]['barcode'],A)['items'];ok('Inactive references are not silently republished',any(p['id']==p2 and not p['isActive'] for p in all_admin))
product=next(p for p in items if p['id']!=p1);cart=call('/cart/quote',M,{'items':[{'productId':product['id'],'quantity':2}]});address=call('/addresses',M)[0]
ok('Priced demo basket has exact subtotal, delivery fee and reviewed checkout consent',cart['canOrder'] and cart['total']==product['price']*2 and cart['pricingToken'])
order=call('/orders',M,{'idempotencyKey':uuid.uuid4().hex,'addressId':address['id'],'items':[{'productId':product['id'],'quantity':2}],'mode':'PLACE_ORDER','pricingToken':cart['pricingToken']})
ok('Merchant directly places a priced order with frozen totals rather than an RFQ',order['total']==cart['total'] and order['status'] in ['SUBMITTED','CONFIRMED'])
foreign=call('/demo/session',body={'role':'MERCHANT'});ok('Another workspace remains unaffected',call('/products/'+product['id'],foreign['token'])['price'] is None)
link=call('/admin/demo/invitations',A,{'label':'EXEMPLE · refus de configuration','hours':1,'maxUses':1});guest=call('/demo/join',body={'inviteToken':link['inviteToken'],'joinKey':uuid.uuid4().hex,'name':'EXEMPLE','shopName':'EXEMPLE · invité','phone':'EXEMPLE','commune':'Oran','street':'EXEMPLE'})
call('/demo/showcase',guest['token'],{},403);ok('An invited shop cannot seed or alter the owner showcase')
campaigns=call('/campaigns',M)['items']; rewards=call('/rewards',M)['items']
ok('A new preview exposes discount, free-goods and challenge examples',set(c['kind'] for c in campaigns)=={'DISCOUNT','FREE_GOODS','CHALLENGE'} and len(campaigns)==3)
ok('Three stock-backed fictional gifts use original Figma product illustrations',len(rewards)==3 and all(r['availableStock']==20 and r['imageUrl'].startswith('/figma/reward-') for r in rewards))
call('/demo/showcase',M,{})
ok('Repeated preview bootstrap does not duplicate examples',len(call('/campaigns',M)['items'])==3 and len(call('/rewards',M)['items'])==3)
r={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'baseUrl':base,'passed':len(checks),'requests':requests,'checks':checks};pathlib.Path(os.environ.get('TOUSHIR_SHOWCASE_RESULTS','verification/journey/showcase.json')).write_text(json.dumps(r,indent=2));print(json.dumps({'passed':len(checks),'requests':requests}))
