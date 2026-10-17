#!/usr/bin/env python3
"""Keep up to 2500 unique factual products with usable local source photos.
Replace failed, nearly blank or byte-identical duplicate photographs from the
qualified source pool. Never invent a variant to reach the requested count.
"""
import json,pathlib,subprocess,sys,collections,datetime
from audit_catalog import inspect,run
B=pathlib.Path(__file__).resolve().parent

def main():
 candidates=json.load(open(B/'candidates.json'));excluded={};target=2500
 for attempt in range(7):
  current=json.load(open(B/'products.json'));good=[];hash_seen={}
  for p in current:
   code=p['barcode'];im,e=inspect(p)
   if e:excluded[code]=e;continue
   if im['sha256'] in hash_seen:excluded[code]='exact_photo_duplicate_of_'+hash_seen[im['sha256']];continue
   hash_seen[im['sha256']]=code;p['imageQuality']={k:im[k] for k in ['width','height','whiteBorderRatio']};good.append(p)
  existing={p['barcode'] for p in good};eligible=[p for p in candidates if p['barcode'] not in existing and p['barcode'] not in excluded]
  if len(good)>=target or not eligible:
   good=good[:target]
   for p in good:p.pop('_scans',None);p.pop('_completeness',None)
   json.dump(good,open(B/'products.json','w'),ensure_ascii=False,indent=2);break
  needed=target-len(good);print('Photo QA pass',attempt+1,'kept',len(good),'need',needed,'remainingcandidates',len(eligible),flush=True)
  new=good+eligible[:needed]
  for p in new:p.pop('_scans',None);p.pop('_completeness',None)
  json.dump(new,open(B/'products.json','w'),ensure_ascii=False,indent=2)
  subprocess.run([sys.executable,'-u',str(B/'cache_images.py')],cwd=B,check=True)
 else:raise RuntimeError('Photo quality completion exhausted bounded passes')
 # Rebuild a manifest for the final set only; existing selected-front files are reused.
 subprocess.run([sys.executable,'-u',str(B/'cache_images.py')],cwd=B,check=True)
 result=run()
 if result['errors'] or result['exactImageDuplicates']:raise RuntimeError('Final catalog validation failed')
 actual=result['products']
 summary=json.load(open(B/'quality-summary.json'));summary.update({'selectedCount':actual,'requestedCount':target,'missingToRequested':max(0,target-actual),'categories':dict(collections.Counter(p['category'] for p in good)),'algeriaTagged':actual,'sourceImages':actual,'timestamp':datetime.datetime.now(datetime.timezone.utc).isoformat()});json.dump(summary,open(B/'quality-summary.json','w'),ensure_ascii=False,indent=2)
 json.dump({'status':'QUALIFIED_SOURCE_POOL_SHORTFALL' if actual<target else 'REQUESTED_COUNT_REACHED','excludedPhotoCount':len(excluded),'excludedPhotos':excluded,'requestedCount':target,'selectedCount':actual,'missingToRequested':max(0,target-actual),'completedAt':datetime.datetime.now(datetime.timezone.utc).isoformat()},open(B/'photo-quality-selection.json','w'),ensure_ascii=False,indent=2)
 print(f'FINAL STRUCTURE-CHECKED CATALOG: {actual} unique checksum-valid barcodes, {actual} Algeria-tagged, {actual} decoded local photos, 0 published offers. Shortfall to requested count: {max(0,target-actual)}.',flush=True)
if __name__=='__main__':main()
