#!/usr/bin/env python3
"""Normalize factual OFF identity only; never generate offers, stock, or prices."""
import json,re,unicodedata,hashlib,collections,datetime,pathlib
BASE=pathlib.Path(__file__).resolve().parent

def clean(s):
 return re.sub(r'\s+',' ',str(s or '')).strip()
def name_of(p):
 n=p.get('product_name')
 if isinstance(n,list):
  langs={x.get('lang'):clean(x.get('text')) for x in n if isinstance(x,dict)}
  return langs.get('fr') or langs.get(p.get('lang')) or langs.get('en') or langs.get('ar') or next((x for x in langs.values() if x),'')
 return clean(p.get('product_name_fr')) or clean(n)
def valid_gtin(code):
 if not re.fullmatch(r'(?:\d{8}|\d{12,14})',code):return False
 return sum(int(d)*(3 if i%2==0 else 1) for i,d in enumerate(code[-2::-1]))%10 == (10-int(code[-1]))%10

def normalized_pack_amounts(text):
 text=clean(text).lower().replace(',', '.')
 found={}
 for match in re.finditer(r'(?<![\d.])(?:(\d+(?:\.\d+)?)\s*[x×]\s*)?(\d+(?:\.\d+)?)\s*(kg|gr|g|ml|cl|l)(?![a-z])',text):
  multiple=float(match[1] or 1);value=float(match[2]);unit=match[3]
  dimension='mass' if unit in ['kg','gr','g'] else 'volume'
  factor={'kg':1000,'gr':1,'g':1,'l':1000,'cl':10,'ml':1}[unit]
  found.setdefault(dimension,[]).append(multiple*value*factor)
 return found

def quantity_conflict(name,quantity):
 a=normalized_pack_amounts(name);b=normalized_pack_amounts(quantity)
 for dimension in set(a)&set(b):
  # Multi-value names (e.g. a mixed assortment) stay unasserted; do not infer case sizes.
  if len(a[dimension])==1 and len(b[dimension])==1:
   av,bv=a[dimension][0],b[dimension][0]
   if abs(av-bv)>max(av,bv)*.05:return True
 return False

def category(p):
 t=' '.join(p.get('categories_tags') or []).lower()
 n=unicodedata.normalize('NFKD',name_of(p)).lower()
 n=''.join(c for c in n if not unicodedata.combining(c))
 if any(x in t for x in ['baby-foods','baby-milks','infant']):return 'Bébé'
 if any(x in t for x in ['ice-creams','ice-pops','ice-cubes','sorbets','frozen-desserts']):return 'Surgelés'
 if 'frozen' in t and not any(x in t for x in ['beverages','juice','waters','sodas','colas']):return 'Surgelés'
 if any(x in t for x in ['waters','eaux']):return 'Eaux'
 if any(x in t for x in ['coffee','tea','cocoa-powders']):return 'Café & thé'
 if any(x in t for x in ['beverages','fruit-juices','sodas','nectars','energy-drinks','en:juice','boisson','jus-']):return 'Boissons & jus'
 if any(x in t for x in ['dairies','cheeses','yogurts','milks','butters','margarines','dairy-desserts']):return 'Lait & produits laitiers'
 if any(x in t for x in ['biscuits','chocolates','confectioneries','sweet-snacks','candies','cakes','cereal-bars','vegecao','chocolatee']):return 'Biscuits & confiserie'
 if any(x in t for x in ['salty-snacks','crisps','chips','crackers','appetizers','popcorn','snacks']):return 'Snacks salés'
 if any(x in t for x in ['canned','preserved','tuna','sardines','tomato-pastes','tomato-concentrates']):return 'Conserves'
 if any(x in t for x in ['vegetable-oils','olive-oils','sunflower-oils','fats','sauces','condiments','vinegars','seasonings','salt','spices']):return 'Huiles, sauces & épices'
 if any(x in t for x in ['sugars','honeys','jams','sweet-spreads','breakfast-cereals','breakfast']):return 'Petit-déjeuner'
 if any(x in t for x in ['pasta','couscous','semolina','rice','flour','cereals-and-potatoes','cereal-grains','legumes','lentils','chickpeas','beans']):return 'Pâtes, riz & céréales'
 if any(w in n for w in ['lait infantile','cerelac','cerelac','1er age','2eme age','bledina']):return 'Bébé'
 if any(w in n for w in ['glace','surgelé','surgele']):return 'Surgelés'
 if any(w in n for w in ['eau minerale','eau de source','mineral water','مياه معدنية']):return 'Eaux'
 if any(w in n for w in ['cafe','coffee','the vert','the noir','nesquik','cacao en poudre','قهوة','قهوه']):return 'Café & thé'
 if any(w in n for w in ['fromage','lait','yaourt','yoghurt','yogurt','brasse','creme fraiche','creme liquide',"l\'ben",'lben','raib','جبن','حليب','لبن']):return 'Lait & produits laitiers'
 if any(w in n for w in ['jus','boisson','cola','gazouz','limonade','citronnade','nectar','عصير','مشروب']):return 'Boissons & jus'
 if any(w in n for w in ['chocolat','chocolate','biscuit','gaufrette','bonbon','cookies','gateau','شكلاط','شوكولات','بسكوي','بسكويت']):return 'Biscuits & confiserie'
 if any(w in n for w in ['huile','mayonnaise','ketchup','moutarde','sauce','vinaigre','poivre','cannelle','زيت','صلصة']):return 'Huiles, sauces & épices'
 if any(w in n for w in ['thon','sardine','conserve','tomate concentree','tomate concentre','corned beef','تونة','طماطم']):return 'Conserves'
 if any(w in n for w in ['sucre','miel','confiture','سكر','عسل','مربى']):return 'Petit-déjeuner'
 if any(w in n for w in ['semoule','couscous','farine','pates','spaghetti','macaroni','vermicelle','lentille','pois chiche','haricot','riz ','rice','ارز','أرز','كسكس','سميد','دقيق','عدس','حمص','لسان العصفور']):return 'Pâtes, riz & céréales'
 if any(w in n for w in ['chips','crackers','cacahuete','salty']):return 'Snacks salés'
 return 'Épicerie'

def image_info(p):
 url=p.get('image_front_url')
 selected=p.get('images') or []
 if isinstance(selected,list):
  images={i.get('key'):i for i in selected if isinstance(i,dict)}
 else:images=selected
 code=str(p.get('code',''))
 folder='/'.join([code[:3],code[3:6],code[6:9],code[9:]]) if len(code)>8 else code
 for lang in ['fr',p.get('lang'),'en','ar']:
  key='front_'+str(lang)
  img=images.get(key)
  if isinstance(img,dict) and img.get('rev') is not None:
   url=f'https://images.openfoodfacts.org/images/products/{folder}/{key}.{img["rev"]}.400.jpg'
   raw=img.get('imgid')
   return url, f'https://openfoodfacts-images.s3.eu-west-3.amazonaws.com/data/{folder}/{raw}.400.jpg' if raw else None, img.get('uploader')
 return url,None,None

def build():
 f=BASE/'algeria-bulk.json'
 if not f.exists():f=BASE/'algeria-sample-100.json'
 raw=json.load(open(f));products=raw['products'];out=[];seen=set();excluded=collections.Counter()
 holds=json.load(open(BASE/'manual-source-holds.json')) if (BASE/'manual-source-holds.json').exists() else {}
 for p in products:
  code=clean(p.get('code')); source_name=name_of(p);name=re.sub(r'^[\s\-_/\\!?.:;]+','',source_name).strip();brand=clean(p.get('brands'));url,aws,uploader=image_info(p)
  raw_quantity=clean(p.get('quantity')); display_quantity=raw_quantity if any(c.isalpha() for c in raw_quantity) else None
  if display_quantity and re.search(r'\d[\d.,]*\s*(?:da|dzd|dinars?|دج|د\.ج|€|eur)(?:$|[ ,;])',display_quantity,re.I):display_quantity=None
  if code in holds:excluded['manual_source_identity_hold']+=1;continue
  if code in seen:excluded['duplicate_barcode']+=1;continue
  if not valid_gtin(code):excluded['not_valid_gtin']+=1;continue
  if re.search(r'(\d)\1{6,}',code):excluded['placeholder_like_barcode']+=1;continue
  if any(w in name.casefold() for w in ['lugol', 'iodine solution','paracetamol','ibuprofene','ibuprofène']):excluded['outside_grocery_food_scope']+=1;continue
  tags=set(p.get('categories_tags') or [])
  if 'en:non-food-products' in tags:excluded['outside_grocery_food_scope']+=1;continue
  if 'en:dietary-supplements' in tags:
   conventional=any(v in tags for v in ['en:protein-bars','en:snacks','en:bars','en:desserts','en:dairy-desserts','en:yogurts','en:energy-drinks','en:breakfast-cereals','en:dehydrated-beverages'])
   if not conventional:excluded['specialist_supplement_scope']+=1;continue
  if not name or len(name)<2:excluded['missing_product_name']+=1;continue
  if any(w in name.casefold() for w in ['interdit en europe','interdit en europé','warning:','do not buy','ne pas acheter','test product','produit test','a supprimer','à supprimer','erreur de code','fake product','\ufffd']):excluded['warning_or_test_product_name']+=1;continue
  if sum(c.isalpha() for c in name)<2 or len(name)>140 or name.casefold() in {'test','unknown','produit','product','sans nom','n/a','none','null','xxx','aaaa'}:excluded['unusable_product_name']+=1;continue
  if re.search(r'(\w)\1{4,}',name):excluded['gibberish_product_name']+=1;continue
  if any(v in name.lower() for v in ['http://','https://','www.','<script','</']):excluded['unusable_product_name']+=1;continue
  if not brand:excluded['missing_brand']+=1;continue
  if re.search(r'\d[\d.,]*\s*(?:da|dzd|dinars?|دج|د\.ج|€|eur)(?:$|[ ,;])|(?:da|dzd|dinars?|دج|د\.ج)[ .,:-]*\d',brand,re.I):excluded['price_like_brand']+=1;continue
  if any(v in brand.lower() for v in ['http://','https://','www.','@','<','>']):excluded['unusable_brand']+=1;continue
  if re.search(r'\d{2,}|(?:^|[ ,;.])\d+\s*(?:kg|gr|g|ml|cl|l)(?:\b|$)',brand,re.I):excluded['polluted_brand_number_or_size']+=1;continue
  if len(brand)>90 or sum(c.isalpha() for c in brand)<2 or brand.casefold() in {'test','unknown','sans marque','no brand','none','n/a','algerie','algérie','algeria','dz','made in algeria','produit','produits','maroc','morocco','france','tunisie','tunisia','turquie','turkey','espagne','spain','italie','italy','allemagne','germany','chine','china'}:excluded['unusable_brand']+=1;continue
  if p.get('obsolete') in (True,'1','on'):excluded['obsolete']+=1;continue
  if quantity_conflict(name,p.get('quantity')):excluded['quantity_conflict']+=1;continue
  if 'en:algeria' not in (p.get('countries_tags') or []):excluded['not_algeria_tagged']+=1;continue
  if not url:excluded['missing_front_image']+=1;continue
  seen.add(code)
  out.append({'id':'off-'+code,'barcode':code,'name':name,'sourceName':source_name,'gtin14':code.zfill(14),'brand':brand,'category':category(p),'sourceCategories':p.get('categories_tags') or [],'categoryMethod':'NORMALIZED_FROM_SOURCE_TAGS_AND_NAME','imageUrl':url,'imageSourceUrl':url,'imageBulkSourceUrl':aws,'sourceUrl':'https://world.openfoodfacts.org/product/'+code,'source':'Open Food Facts','sourceLicense':'ODbL-1.0','imageLicense':'CC-BY-SA-3.0','imageAttribution':'Open Food Facts contributors','countryTags':p.get('countries_tags') or [],'unit':display_quantity,'sourceQuantity':raw_quantity or None,'caseSize':None,'price':None,'stock':None,'pricingMode':'ON_REQUEST','offerApproved':False,'productDataVerified':False,'algeriaAvailability':'COUNTRY_TAG_ONLY','sourceModifiedAt':datetime.datetime.fromtimestamp(p['last_modified_t'],datetime.timezone.utc).isoformat() if p.get('last_modified_t') else None,'_scans':int(p.get('unique_scans_n') or 0),'_completeness':float(p.get('completeness') or 0)})
 # Relevance over quantity: prefer declared Algeria-local GTIN prefix, then scan prominence/metadata completeness.
 known=['cevital','bimo','ifri','hamoud','ramy','rouiba','soummam','candia','danone','palmary','bifa','mordjene','cebon','sim','amor benamor','belle','safina','saida','saco','nouara','gaous','guedila','kheddidja','toudja','youkous','texenna','mouzaia','nestlé','nestle','pepsi','coca','fanta','schweppes','sprite','vache','tartino','président','lactel']
 out.sort(key=lambda p:(not p['barcode'].startswith('613'),not any(b in p['brand'].casefold() for b in known),not bool(p.get('unit')),-p['_scans'],-p['_completeness'],p['barcode']))
 candidates=out
 json.dump(candidates,open(BASE/'candidates.json','w'),ensure_ascii=False,indent=2)
 out=out[:2500]
 for p in out:p.pop('_scans',None);p.pop('_completeness',None)
 # Round-robin visible categories to make the first catalogue page representative.
 groups=collections.defaultdict(list)
 for p in out:groups[p['category']].append(p)
 order=['Eaux','Boissons & jus','Lait & produits laitiers','Biscuits & confiserie','Pâtes, riz & céréales','Café & thé','Huiles, sauces & épices','Conserves','Petit-déjeuner','Snacks salés','Épicerie','Bébé','Surgelés']
 interleaved=[]
 while any(groups.values()):
  for cat in order:
   if groups[cat]:interleaved.append(groups[cat].pop(0))
 json.dump(interleaved,open(BASE/'products.json','w'),ensure_ascii=False,indent=2)
 json.dump({'sourceFile':f.name,'sourceCount':raw['count'],'candidateCount':len(candidates),'selectedCount':len(out),'excluded':dict(excluded),'categories':dict(collections.Counter(p['category'] for p in out)),'algeriaTagged':sum('en:algeria' in p['countryTags'] for p in out),'sourceImages':sum(bool(p['imageUrl']) for p in out),'prices':0,'publishedStock':0,'timestamp':datetime.datetime.now(datetime.timezone.utc).isoformat()},open(BASE/'quality-summary.json','w'),ensure_ascii=False,indent=2)
 print(json.dumps(json.load(open(BASE/'quality-summary.json')),ensure_ascii=False),flush=True)
if __name__=='__main__':build()
