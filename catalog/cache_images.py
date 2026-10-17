#!/usr/bin/env python3
"""Cache exact selected-front 400px photographs, one connection/request at a time.
The bulk AWS mirror contains raw uploads, not the selected crop/revision. Using the
selected crop avoids mismatched orientation/crop. Never synthesize brand artwork.
"""
import json,pathlib,http.client,time,hashlib,io,ssl,urllib.parse
from PIL import Image
BASE=pathlib.Path(__file__).resolve().parent;OUT=BASE/'images';OUT.mkdir(exist_ok=True)
UA='ToushirWholesaleCatalog/1.0 (https://github.com/AH-AMINE3)'

def main():
 products=json.load(open(BASE/'products.json'));manifest=[];failed=[];conn=None
 for index,p in enumerate(products,1):
  code=p['barcode'];path=OUT/(code+'.jpg');source=p.get('imageSourceUrl');meta=None
  if path.exists():
   try:
    with Image.open(path) as im:im.verify()
    with Image.open(path) as im:w,h=im.size
    meta={'barcode':code,'file':path.name,'bytes':path.stat().st_size,'width':w,'height':h,'source':source,'kind':'OFF_SELECTED_FRONT','sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
   except:pass
  if not meta and source:
   u=urllib.parse.urlparse(source)
   if u.hostname!='images.openfoodfacts.org':failed.append(code);continue
   for attempt in range(3):
    try:
     if conn is None:conn=http.client.HTTPSConnection('images.openfoodfacts.org',timeout=15,context=ssl.create_default_context())
     conn.request('GET',u.path+('?' + u.query if u.query else ''),headers={'User-Agent':UA,'Accept':'image/jpeg'})
     response=conn.getresponse();data=response.read(4000000)
     if response.status in (404,403):break
     if response.status==429:
      conn.close();conn=None;time.sleep(45);continue
     if response.status!=200 or 'image' not in (response.getheader('Content-Type') or ''):raise ValueError('Image response '+str(response.status))
     with Image.open(io.BytesIO(data)) as im:im.verify()
     with Image.open(io.BytesIO(data)) as im:
      w,h=im.size
      if min(w,h)<40 or max(w,h)<120:break
     temp=path.with_suffix('.part');temp.write_bytes(data);temp.replace(path)
     meta={'barcode':code,'file':path.name,'bytes':len(data),'width':w,'height':h,'source':source,'kind':'OFF_SELECTED_FRONT','sha256':hashlib.sha256(data).hexdigest()};break
    except Exception:
     if conn:
      try:conn.close()
      except:pass
     conn=None;time.sleep(1.5*(attempt+1))
   time.sleep(.03)
  if meta:
   manifest.append(meta);p['imageUrl']='/catalog/images/'+code+'.jpg';p['imageCached']=True;p['imageCachedSource']='OFF_SELECTED_FRONT'
  else:failed.append(code);p['imageCached']=False
  if index%100==0:print('Selected front photos',index,'/',len(products),'cached',len(manifest),'failed',len(failed),flush=True)
 if conn:conn.close()
 json.dump(products,open(BASE/'products.json','w'),ensure_ascii=False,indent=2)
 json.dump({'count':len(manifest),'total':len(products),'failed':failed,'images':manifest,'method':'Exact selected-front crop, 400px, strictly serial'},open(BASE/'image-manifest.json','w'),ensure_ascii=False,indent=2)
 print('Final',len(manifest),'of',len(products),'local selected front photos',flush=True)
if __name__=='__main__':main()
