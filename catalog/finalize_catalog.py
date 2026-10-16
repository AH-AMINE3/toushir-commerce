#!/usr/bin/env python3
"""Keep exactly 2500 unique factual products with usable local source photos.
Replace failed, nearly blank or byte-identical duplicate photographs from the
qualified source pool. Never invent a variant to reach the requested count.
"""
import json,pathlib,subprocess,sys,collections,datetime
from audit_catalog import inspect,run
B=pathlib.Path(__file__).resolve().parent

def main():
 candidates=json.load(open(B/'candidates.json'));candidates_by={x['barcode']:x for x in candidates};excluded={};target=2500
 for attempt in range(7):
  current=json.load(open(B/'products.json'));good=[];hash_seen={}
  for p in current:
   code=p['barcode'];im,e=inspect(p)
   if e:excluded[code]=e;continue
   if im['sha256'] in hash_seen:excluded[code]='exact_photo_duplicate_of_'+hash_seen[im['sha256']];continue
   hash_seen[im['sha256']]=code;p['imageQuality']={k:im[k] for k in ['width','height','whiteBorderRatio']};good.append(p)
  if len(good)>=target:
   good=good[:target]
   for p in good:p.pop('_scans',None);p.pop('_completeness',None)
   json.dump(good,open(B/'products.json','w'),ensure_ascii=False,indent=2);break
  existing={p['barcode'] for p in good};eligible=[p for p in candidates if p['barcode'] not in existing and p['barcode'] not in excluded]
  needed=target-len(good);print('Photo QA pass',attempt+1,'kept',len(good),'need',needed,'remainingcandidates',len(eligible),flush=True)
  if len(eligible)<needed:raise RuntimeError('Fewer than2500 qualified actual source products remain; refusing synthetic padding')
  new=good+eligible[:needed]
  for p in new:p.pop('_scans',None);p.pop('_completeness',None)
  json.dump(new,open(B/'products.json','w'),ensure_ascii=False,indent=2)
  subprocess.run([sys.executable,'-u',str(B/'cache_images.py')],cwd=B,check=True)
 else:raise RuntimeError('Photo quality completion exhausted bounded passes')
 # Rebuild a manifest for the final set only; existing selected-front files are reused.
 subprocess.run([sys.executable,'-u',str(B/'cache_images.py')],cwd=B,check=True)
 result=run()
 if result['errors'] or result['exactImageDuplicates'] or result['products']!=target:raise RuntimeError('Final catalog validation failed')
 json.dump({'excludedPhotoCount':len(excluded),'excludedPhotos':excluded,'selectedCount':target,'completedAt':datetime.datetime.now(datetime.timezone.utc).isoformat()},open(B/'photo-quality-selection.json','w'),ensure_ascii=False,indent=2)
 print('FINAL STRUCTURE-CHECKED CATALOG: 2500uniquechecksum-validbarcodes,2500Algeria-tagged,2500decodedlocalphotos,0publishedoffers',flush=True)
if __name__=='__main__':main()
