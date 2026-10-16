import pathlib,json,random,collections,textwrap
from PIL import Image,ImageDraw,ImageFont,ImageOps
B=pathlib.Path(__file__).resolve().parent;O=B/'qa-sheets';O.mkdir(exist_ok=True)
p=json.load(open(B/'products.json'));q={x['barcode']:x for x in json.load(open(B/'image-quality.json'))} if (B/'image-quality.json').exists() else {}
p=[x for x in p if (B/'images'/(x['barcode']+'.jpg')).exists()]
random.seed(613);random.shuffle(p)
# Representative sample, category-balanced and deterministic.
categories=collections.defaultdict(list)
for x in p:categories[x['category']].append(x)
representative=[]
for values in categories.values():representative.extend(values[:8])
representative=representative[:112]
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',12)
for k in range(0,len(representative),28):
 group=representative[k:k+28];im=Image.new('RGB',(1120,1000),'#eeeeee');d=ImageDraw.Draw(im)
 for i,x in enumerate(group):
  px=(i%7)*160;py=(i//7)*250;f=B/'images'/(x['barcode']+'.jpg')
  if f.exists():
   a=ImageOps.exif_transpose(Image.open(f)).convert('RGB');a.thumbnail((150,150));im.paste(a,(px+(160-a.width)//2,py+5))
  d.text((px+4,py+160),x['barcode'],font=font,fill='black')
  d.text((px+4,py+177),x['brand'][:22],font=font,fill='black')
  for j,line in enumerate(textwrap.wrap(x['name'],width=23)[:2]):d.text((px+4,py+194+j*15),line,font=font,fill='black')
  d.text((px+4,py+226),str(x.get('unit') or 'Quantité non renseignée')[:23],font=font,fill='black')
 file=O/f'representative-{k//28+1}.jpg';im.save(file,quality=90);print(file,flush=True)
json.dump([{'barcode':x['barcode'],'name':x['name'],'brand':x['brand'],'category':x['category']} for x in representative],open(O/'sample.json','w'),ensure_ascii=False,indent=2)
