"""Human-review sheets for the final source photos; the originals stay unchanged."""
import json,pathlib,collections,textwrap
from PIL import Image,ImageOps,ImageDraw,ImageFont
from audit_catalog import inspect
B=pathlib.Path(__file__).resolve().parent;O=B/'photo-audit';O.mkdir(exist_ok=True)
p=json.load(open(B/'products.json'));quality=[]
for x in p:
 q,e=inspect(x)
 if q:quality.append((x,q))

def diversified(items,n=24):
 out=[];brands=collections.Counter();cats=collections.Counter()
 for x,q in items:
  b=x['brand'].casefold()
  if brands[b]>=2 or cats[x['category']]>=5:continue
  out.append((x,q));brands[b]+=1;cats[x['category']]+=1
  if len(out)==n:break
 if len(out)<n:
  used={x['barcode'] for x,q in out}
  for x,q in items:
   if x['barcode'] not in used:out.append((x,q));used.add(x['barcode'])
   if len(out)==n:break
 return out
best=diversified(sorted(quality,key=lambda x:(-x[1]['whiteBorderRatio'],-x[1]['edgeSharpness'],-x[1]['variance'])))
worst=diversified(sorted(quality,key=lambda x:(x[1]['whiteBorderRatio'],x[1]['edgeSharpness'],x[1]['variance'])))
fonts={'title':ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',25),'body':ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',14),'small':ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',12)}
records=[]
for mode,items in [('best',best),('needs-review',worst)]:
 im=Image.new('RGB',(1440,1470),'#ededed');d=ImageDraw.Draw(im)
 d.text((14,12),'TOUSHIR — '+('24 photos au meilleur cadrage présumé' if mode=='best' else '24 photos à vérifier en priorité'),font=fonts['title'],fill='black')
 d.text((14,45),'Tri automatique; validation visuelle limitée aux échantillons. Aucun original modifié.',font=fonts['body'],fill='#444444')
 for index,(x,q) in enumerate(items):
  col=index%6;row=index//6;px=col*240;py=80+row*340
  a=ImageOps.exif_transpose(Image.open(B/'images'/(x['barcode']+'.jpg'))).convert('RGB');a.thumbnail((220,220));im.paste(a,(px+(240-a.width)//2,py+(220-a.height)//2))
  d.text((px+7,py+226),f'{index+1}. '+x['barcode'],font=fonts['body'],fill='black')
  d.text((px+7,py+246),x['brand'][:29],font=fonts['body'],fill='black')
  for j,line in enumerate(textwrap.wrap(x['name'],width=31)[:2]):d.text((px+7,py+267+j*17),line,font=fonts['body'],fill='black')
  d.text((px+7,py+307),f"Marge blanche {q['whiteBorderRatio']:.0%} / netteté {q['edgeSharpness']:.0f}",font=fonts['small'],fill='#555555')
  records.append({'sheet':mode,'position':index+1,'barcode':x['barcode'],'name':x['name'],'brand':x['brand'],'sourceUrl':x['sourceUrl'],'imageSourceUrl':x['imageSourceUrl'],'imagePath':'images/'+x['barcode']+'.jpg','automaticImageMetrics':{k:q[k] for k in ['width','height','variance','edgeSharpness','whiteBorderRatio']},'visualAudit':{'status':'PENDING_SAMPLE_REVIEW','roomAroundProduct':None,'cropRotation':None,'blur':None,'labelLegibility':None,'needsOwnerCheck':mode=='needs-review'}})
 d.text((14,1445),'Photos: Open Food Facts contributors — CC BY-SA3.0. Source produit liée dans le rapport.',font=fonts['small'],fill='#555555')
 im.save(O/('photo-audit-'+mode+'.jpg'),quality=92)
json.dump({'scope':'48 source photos sampled; not an exhaustive human verification of2500products.','selection':'Automated border/edge metrics with brand/category diversity, followed by human sample review. Metrics are not identity certification.','photos':records},open(O/'photo-audit.json','w'),ensure_ascii=False,indent=2)
print('Photo audit sheets generated',len(records),flush=True)
