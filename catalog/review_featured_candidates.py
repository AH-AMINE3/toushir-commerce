import pathlib,json,collections,textwrap
from PIL import Image,ImageOps,ImageDraw,ImageFont
B=pathlib.Path(__file__).resolve().parent;O=B/'photo-audit';O.mkdir(exist_ok=True)
p={x['barcode']:x for x in json.load(open(B/'products.json'))};q=json.load(open(B/'image-quality.json'));featured=set(json.load(open(B/'featured-barcodes.json')));exclude={'6130433000590','3017620420047','3183280020320'}
a=[(p[x['barcode']],x) for x in q if x['barcode'] in p and x['barcode'] not in featured|exclude and x['whiteBorderRatio']>=.45 and x['edgeSharpness']>=40]
a.sort(key=lambda v:(not v[0]['barcode'].startswith('613'),-v[1]['whiteBorderRatio'],-v[1]['edgeSharpness']))
# Diversify without fabricating popularity or stock.
selected=[];brand=collections.Counter()
for x,m in a:
 b=x['brand'].casefold()
 if brand[b]>=4:continue
 selected.append((x,m));brand[b]+=1
 if len(selected)>=64:break
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',14)
for k in range(0,len(selected),16):
 im=Image.new('RGB',(1040,1280),'#eeeeee');d=ImageDraw.Draw(im)
 for i,(x,m) in enumerate(selected[k:k+16]):
  px=i%4*260;py=i//4*320;a=ImageOps.exif_transpose(Image.open(B/'images'/(x['barcode']+'.jpg'))).convert('RGB');a.thumbnail((240,225));im.paste(a,(px+(260-a.width)//2,py+2+(225-a.height)//2))
  d.text((px+6,py+231),str(k+i+1)+'. '+x['barcode'],font=font,fill='black');d.text((px+6,py+250),x['brand'][:30],font=font,fill='black')
  for j,line in enumerate(textwrap.wrap(x['name'],width=32)[:2]):d.text((px+6,py+269+j*16),line,font=font,fill='black')
  d.text((px+6,py+303),str(x.get('unit') or 'Quantité non renseignée'),font=font,fill='black')
 im.save(O/f'featured-review-{k//16+1}.jpg',quality=93)
json.dump([{'position':i+1,'barcode':x['barcode'],'name':x['name'],'brand':x['brand'],'unit':x['unit'],'imageMetrics':m} for i,(x,m) in enumerate(selected)],open(O/'featured-review-candidates.json','w'),ensure_ascii=False,indent=2)
print('White-background metric candidates',len(a),'selected for visual screen',len(selected),flush=True)
