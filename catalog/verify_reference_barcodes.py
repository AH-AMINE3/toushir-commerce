import urllib.request,urllib.parse,json,pathlib,time,unicodedata,re
B=pathlib.Path(__file__).resolve().parent;D=B/'price-evidence';o=urllib.request.build_opener(urllib.request.ProxyHandler({}));p=json.load(open(B/'public-price-references.json'))
for r in p['references']:
 code=r.get('barcode')
 if not code:continue
 f=D/('off-'+code+'.json')
 if not f.exists():
  url='https://world.openfoodfacts.org/api/v3/product/'+code+'?'+urllib.parse.urlencode({'fields':'code,product_name,product_name_fr,brands,quantity,countries_tags,image_front_url'})
  try:
   req=urllib.request.Request(url,headers={'User-Agent':'ToushirWholesaleCatalog/1.0 (https://github.com/AH-AMINE3)'})
   with o.open(req,timeout=12) as x:data=json.load(x)
   json.dump(data,open(f,'w'),ensure_ascii=False)
  except Exception as e:r['matchingStatus']='SOURCE_CATALOG_LOOKUP_UNAVAILABLE';r['offLookupError']=type(e).__name__;time.sleep(4.5);continue
  time.sleep(4.5)
 data=json.load(open(f));product=data.get('product',{})
 if not product:r['matchingStatus']='BARCODE_NOT_IN_OFF';continue
 r['offProductName']=product.get('product_name_fr') or product.get('product_name');r['offBrand']=product.get('brands');r['offQuantity']=product.get('quantity');r['offSourceUrl']='https://world.openfoodfacts.org/product/'+code
 def norm(s):return re.sub(r'[^a-z0-9 ]','',unicodedata.normalize('NFKD',s or '').encode('ascii','ignore').decode().lower())
 tokens=[t for t in norm(product.get('brands')).replace(',',' ').split() if len(t)>2]
 title=norm(r['title']);r['brandTokenAgreement']=any(t in title for t in tokens)
 r['matchingStatus']='BARCODE_AND_BRAND_MATCH_REVIEW_PACKAGE' if r['brandTokenAgreement'] else 'NO_AUTOMATIC_BRAND_AGREEMENT_REVIEW_REQUIRED'
 r['limitations']='Prix public de détail observé chez un tiers. Ni tarif de gros, ni offre Toushir, ni stock du grossiste. Le SKU du vendeur ressemble à un GTIN valide; identité et conditionnement nécessitent la vérification mentionnée.'
 print(code,r['matchingStatus'],r.get('offProductName'),flush=True)
json.dump(p,open(B/'public-price-references.json','w'),ensure_ascii=False,indent=2)
