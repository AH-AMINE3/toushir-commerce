#!/usr/bin/env python3
"""Cache source packaging photos. AWS bulk mirror first; OFF fallbacks strictly serial."""
import json,pathlib,urllib.request,urllib.error,time,concurrent.futures,hashlib,io,os,threading
from PIL import Image
BASE=pathlib.Path(__file__).resolve().parent;OUT=BASE/'images';OUT.mkdir(exist_ok=True)
UA='ToushirWholesaleCatalog/1.0 (https://github.com/AH-AMINE3)'
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
lock=threading.Lock()
def download(p,source,kind):
 path=OUT/(p['barcode']+'.jpg')
 if path.exists():
  try:
   with Image.open(path) as im:im.verify()
   return {'barcode':p['barcode'],'file':path.name,'bytes':path.stat().st_size,'source':source,'kind':'CACHED','sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
  except:pass
 if not source:return None
 for attempt in range(2):
  try:
   request=urllib.request.Request(source,headers={'User-Agent':UA})
   with opener.open(request,timeout=16) as r:
    data=r.read(4000000)
    if r.status!=200 or 'image' not in r.headers.get('Content-Type',''):return None
   with Image.open(io.BytesIO(data)) as im:
    im.verify()
   with Image.open(io.BytesIO(data)) as im:
    w,h=im.size
    if min(w,h)<40:return None
   tmp=path.with_suffix('.part');tmp.write_bytes(data);tmp.replace(path)
   return {'barcode':p['barcode'],'file':path.name,'bytes':len(data),'width':w,'height':h,'source':source,'kind':kind,'sha256':hashlib.sha256(data).hexdigest()}
  except urllib.error.HTTPError as e:
   if e.code in [403,404]:return None
   if e.code==429:time.sleep(30)
  except Exception:pass
  time.sleep(2**attempt)
 return None

def main():
 products=json.load(open(BASE/'products.json'));manifest=[];fail=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
  futures={pool.submit(download,p,p.get('imageBulkSourceUrl'),'AWS_SOURCE_FRONT_PHOTO'):p for p in products}
  for i,f in enumerate(concurrent.futures.as_completed(futures),1):
   p=futures[f];res=f.result()
   if res:manifest.append(res)
   else:fail.append(p)
   if i%100==0:print('Bulk photos',i,'/',len(products),'cached',len(manifest),'fallback',len(fail),flush=True)
 print('Serial OFF selected-front fallbacks',len(fail),flush=True)
 for i,p in enumerate(fail,1):
  res=download(p,p.get('imageSourceUrl'),'OFF_SELECTED_FRONT')
  if res:manifest.append(res)
  if i%50==0:print('Fallbacks',i,'/',len(fail),'cached',len(manifest),flush=True)
  time.sleep(.06)
 indexed={p['barcode']:p for p in manifest}
 for p in products:
  if p['barcode'] in indexed:
   p['imageUrl']='/catalog/images/'+p['barcode']+'.jpg';p['imageCached']=True;p['imageCachedSource']=indexed[p['barcode']]['kind']
  else:p['imageCached']=False
 json.dump(products,open(BASE/'products.json','w'),ensure_ascii=False,indent=2)
 json.dump({'count':len(manifest),'total':len(products),'failed':[p['barcode'] for p in products if not p.get('imageCached')],'images':manifest},open(BASE/'image-manifest.json','w'),ensure_ascii=False,indent=2)
 print('Final',len(manifest),'of',len(products),'local photos',flush=True)
if __name__=='__main__':main()
