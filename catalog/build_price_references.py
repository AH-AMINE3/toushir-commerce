import json,re,html,datetime,pathlib,hashlib
b=pathlib.Path(__file__).resolve().parent
s=(b/'reference-coursesnet-page14.html').read_text();date='2026-10-16'
refs=[]
# Each card has an explicit retailer product link and displayed retail price.
for block in re.split(r'<div[^>]*class="[^"]*product type-product',s)[1:]:
 n=re.search(r'<h3 class="product-title"><a href="([^"]+)"[^>]*>(.*?)</a></h3>',block,re.S)
 p=re.search(r'<span class="woocommerce-Price-amount amount"><bdi>([0-9.,]+)',block)
 if not n or not p:continue
 name=html.unescape(re.sub('<[^>]+>','',n[2])).strip();url=html.unescape(n[1]);price=float(p[1].replace(',',''))
 refs.append({'id':'ref-'+hashlib.sha256(url.encode()).hexdigest()[:12],'title':name,'brand':None,'barcode':None,'price':price,'currency':'DZD','priceType':'PUBLIC_RETAIL_REFERENCE','retailer':'Courses Net','sourceUrl':url,'observedAt':date,'sourcePriceDate':None,'taxTreatment':'NOT_STATED','stockClaim':None,'offerApproved':False,'useAsWholesaleOffer':False,'matchingStatus':'UNMATCHED_NO_BARCODE','limitations':'Prix public de détail observé chez un tiers. Ni tarif de gros, ni offre Toushir, ni stock du grossiste. Conditionnement/GTIN non vérifié.'})
json.dump({'purpose':'REFERENCE_ONLY_NOT_SELLABLE_OFFERS','observedAt':date,'references':refs},open(b/'public-price-references.json','w'),ensure_ascii=False,indent=2)
print('Retail references with direct product links:',len(refs))
