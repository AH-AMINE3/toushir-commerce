#!/usr/bin/env python3
import pathlib,json,hashlib,collections,re,datetime,statistics
from PIL import Image,ImageStat,ImageFilter
from build_catalog import valid_gtin,quantity_conflict
B=pathlib.Path(__file__).resolve().parent

def inspect(p):
 f=B/'images'/(p['barcode']+'.jpg')
 if not f.exists():return None,'missing'
 try:
  with Image.open(f) as original:
   original.load();w,h=original.size
   if min(w,h)<40 or max(w,h)<120:return None,'too_small'
   im=original.convert('RGB');g=im.convert('L');sd=ImageStat.Stat(g).stddev[0]
   if sd<5:return None,'near_blank'
   im.thumbnail((80,80));gray=im.convert('L');pixels=list(im.get_flattened_data()) if hasattr(im,'get_flattened_data') else list(im.getdata());edge=[]
   iw,ih=im.size
   for y in range(ih):
    for x in range(iw):
     if x<max(1,iw//10) or x>=iw-max(1,iw//10) or y<max(1,ih//10) or y>=ih-max(1,ih//10):edge.append(pixels[y*iw+x])
   white=sum(min(rgb)>225 and max(rgb)-min(rgb)<25 for rgb in edge)/max(1,len(edge))
   # Rank only. These do not certify photography or product identity.
   edges=ImageStat.Stat(gray.filter(ImageFilter.FIND_EDGES)).stddev[0]
   return {'width':w,'height':h,'variance':round(sd,2),'edgeSharpness':round(edges,2),'whiteBorderRatio':round(white,3),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()},None
 except:return None,'decode_error'

def run():
 p=json.load(open(B/'products.json'));errors=[];images=[];hashes=collections.defaultdict(list)
 ids=[x['id'] for x in p];codes=[x['barcode'] for x in p]
 if len(ids)!=len(set(ids)):errors.append('duplicate_ids')
 if len(codes)!=len(set(codes)):errors.append('duplicate_barcodes')
 if len(codes)!=len(set(x.zfill(14) for x in codes)):errors.append('duplicate_canonical_gtin14_aliases')
 for x in p:
  code=x['barcode']
  if not valid_gtin(code):errors.append('invalid_gtin:'+code)
  if 'en:algeria' not in x.get('countryTags',[]):errors.append('not_algeria_tagged:'+code)
  if x.get('price') is not None or x.get('stock') is not None or x.get('offerApproved') or x.get('pricingMode')!='ON_REQUEST':errors.append('invented_commercial_data:'+code)
  if quantity_conflict(x['name'],x.get('unit')):errors.append('quantity_conflict:'+code)
  im,e=inspect(x)
  if e:errors.append('image_'+e+':'+code)
  else:
   im['barcode']=code;images.append(im);hashes[im['sha256']].append(code)
  folder=''.join(re.findall(r'/images/products/([0-9/]+)/',x.get('imageSourceUrl',''))).replace('/','')
  if folder!=code:errors.append('image_barcode_path_mismatch:'+code)
 duplicates=[v for v in hashes.values() if len(v)>1]
 result={'checkedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'products':len(p),'uniqueBarcodes':len(set(codes)),'algeriaTagged':sum('en:algeria' in x['countryTags'] for x in p),'localDecodedImages':len(images),'pricePublished':sum(x.get('price') is not None for x in p),'stockPublished':sum(x.get('stock') is not None for x in p),'offerApproved':sum(bool(x.get('offerApproved')) for x in p),'errors':errors,'exactImageDuplicates':duplicates,'imageStatistics':{'whiteBorderRatioMedian':statistics.median(x['whiteBorderRatio'] for x in images) if images else 0},'identityVerification':'Source identity remains community supplied. Automatic checks do not certify every image or brand.'}
 json.dump(result,open(B/'catalog-validation.json','w'),ensure_ascii=False,indent=2);json.dump(images,open(B/'image-quality.json','w'),ensure_ascii=False,indent=2)
 print(json.dumps({k:v for k,v in result.items() if k not in ['identityVerification']},ensure_ascii=False),flush=True)
 return result
if __name__=='__main__':run()
