#!/usr/bin/env python3
"""Download one pinned official OFF bulk snapshot with bounded range resume.
This optional research import is not run by the application at startup.
"""
import urllib.request,time,pathlib,hashlib
B=pathlib.Path(__file__).resolve().parent
SOURCE='https://huggingface.co/datasets/openfoodfacts/product-database/resolve/2fa5c6b88b9132d565b627576620a697f7ead869/food.parquet'
EXPECTED_BYTES=7950707784
EXPECTED_SHA256='c74c716ae028a2dcc1520101cc5dddefa79652261052499b1c396427544f463c'
UA='ToushirWholesaleCatalog/1.0 (https://github.com/AH-AMINE3)'
def download():
 o=urllib.request.build_opener(urllib.request.ProxyHandler({}));part=B/'bulk-food.parquet.part';final=B/'bulk-food.parquet'
 if final.exists() and final.stat().st_size==EXPECTED_BYTES:return final
 for attempt in range(6):
  n=part.stat().st_size if part.exists() else 0;initial=n;start=time.monotonic();headers={'User-Agent':UA}
  if n:headers['Range']=f'bytes={n}-'
  try:
   with o.open(urllib.request.Request(SOURCE,headers=headers),timeout=45) as response:
    if n and response.status!=206:raise RuntimeError('Source did not honor verified range resume')
    with part.open('ab' if n else 'wb') as out:
     last=n
     while data:=response.read(4*1024*1024):
      out.write(data);n+=len(data)
      if n-last>=256*1024*1024:print('Bulk snapshot',round(n/1e9,2),'GB /7.95GB',round((n-initial)/(time.monotonic()-start)/1e6,1),'MB/s',flush=True);last=n
   if n==EXPECTED_BYTES:
    h=hashlib.sha256()
    with part.open('rb') as data:
     while block:=data.read(8*1024*1024):h.update(block)
    if h.hexdigest()!=EXPECTED_SHA256:raise RuntimeError('Pinned source checksum mismatch; discard this partial file before retrying')
    part.replace(final);print('Pinned source snapshot checksum verified',flush=True);return final
   print('Source stream interrupted; resuming verified byte offset',n,flush=True)
  except Exception as error:print('Bulk source retry',attempt+1,type(error).__name__,flush=True)
  time.sleep(min(30,2**attempt))
 raise RuntimeError('Bulk source unavailable after bounded retries; application continues to use its bundled source catalog')
if __name__=='__main__':download()
