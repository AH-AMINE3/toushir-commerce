#!/usr/bin/env python3
"""Bounded, cached fallback after official country CSV failed (HTTP 503).
One read-only search every >=6.5s (<10/min); never per-product crawling.
"""
import json,pathlib,time,urllib.request,urllib.parse,urllib.error,datetime,sys
B=pathlib.Path(__file__).resolve().parent;C=B/'api-pages';C.mkdir(exist_ok=True)
o=urllib.request.build_opener(urllib.request.ProxyHandler({}));products=[];requests=0;started=time.monotonic()
fields='code,product_name,product_name_fr,product_name_ar,brands,quantity,categories_tags,countries_tags,image_front_url,completeness,unique_scans_n,last_modified_t,obsolete'
for page in range(1,42):
 f=C/f'algeria-{page:02}.json'
 if not f.exists():
  if page==1 and (B/'algeria-sample-100.json').exists():f.write_bytes((B/'algeria-sample-100.json').read_bytes())
  else:
   q=urllib.parse.urlencode({'countries_tags':'en:algeria','page_size':100,'page':page,'fields':fields})
   url='https://world.openfoodfacts.org/api/v2/search?'+q
   for attempt in range(3):
    try:
     req=urllib.request.Request(url,headers={'User-Agent':'ToushirWholesaleCatalog/1.0 (https://github.com/AH-AMINE3)'})
     with o.open(req,timeout=35) as r:data=r.read()
     payload=json.loads(data)
     if not isinstance(payload.get('products'),list):raise ValueError('Invalid source response')
     f.write_bytes(data);requests+=1;break
    except Exception as e:
     delay=30*(attempt+1);print('Source backoff',page,type(e).__name__,delay,'s',flush=True);time.sleep(delay)
   else:print('Unrecoverable source failure on page',page,flush=True);sys.exit(2)
   time.sleep(6.5)
 p=json.load(open(f));products+=p['products']
 print('API fallback cachedpage',page,'sourceproducts',len(products),flush=True)
 if len(p['products'])<100:break
json.dump({'dataset':'https://world.openfoodfacts.org/api/v2/search?countries_tags=en:algeria','count':p['count'],'products':products,'readSearchRequests':requests,'rateLimitSeconds':6.5,'fallbackReason':'Official country CSV HTTP503; official Parquet image-column extraction slow','retrievedAt':datetime.datetime.now(datetime.timezone.utc).isoformat()},open(B/'algeria-api-fallback.json','w'),ensure_ascii=False)
print('API fallback complete',len(products),'requests',requests,'elapsed',round(time.monotonic()-started),flush=True)
