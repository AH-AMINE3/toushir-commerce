import pathlib,json,urllib.request,concurrent.futures,re,html,hashlib,time
B=pathlib.Path(__file__).resolve().parent;D=B/'price-evidence';D.mkdir(exist_ok=True)
o=urllib.request.build_opener(urllib.request.ProxyHandler({}));p=json.load(open(B/'public-price-references.json'))
def one(r):
 try:
  req=urllib.request.Request(r['sourceUrl'],headers={'User-Agent':'ToushirCatalogResearch/1.0 (public product reference check)'})
  with o.open(req,timeout=20) as x:s=x.read(2000000).decode('utf-8','replace')
  (D/(r['id']+'.html')).write_text(s)
  n=re.search(r'<h1[^>]*class="product_title entry-title"[^>]*>(.*?)</h1>',s,re.S)
  title=html.unescape(re.sub('<[^>]*>','',n[1])).strip() if n else None
  prices=re.findall(r'"price"\s*:\s*"?([0-9.]+)',s)
  directprices=sorted(set(float(x) for x in prices))
  r['sourceTitle']=title;r['directPagePriceValues']=directprices;r['directPageVerified']=bool(title and r['price'] in directprices)
  r['evidenceSha256']=hashlib.sha256(s.encode()).hexdigest()
  if not r['directPageVerified']:r['matchingStatus']='REVIEW_REQUIRED'
  return r
 except Exception as e:r['directPageVerified']=False;r['verificationError']=type(e).__name__;return r
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:p['references']=list(pool.map(one,p['references']))
json.dump(p,open(B/'public-price-references.json','w'),ensure_ascii=False,indent=2)
print('Individual retailer pages verified',sum(x['directPageVerified'] for x in p['references']),'/',len(p['references']),flush=True)
