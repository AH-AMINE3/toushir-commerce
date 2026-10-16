import pathlib,time,sys,subprocess,json,hashlib,datetime
B=pathlib.Path(__file__).resolve().parent
while not (B/'bulk-food.parquet').exists():time.sleep(5)
sys.path.insert(0,str(B/'python-deps'));import duckdb
start=time.monotonic();c=duckdb.connect();c.execute('SET threads=4')
q="""SELECT code,product_name,brands,categories,categories_tags,quantity,countries_tags,images,obsolete,completeness,unique_scans_n,last_modified_t,lang,photographers FROM read_parquet(?) WHERE list_contains(countries_tags,'en:algeria')"""
x=c.execute(q,[str(B/'bulk-food.parquet')]);names=[r[0] for r in x.description];products=[dict(zip(names,r)) for r in x.fetchall()]
json.dump({'dataset':'https://huggingface.co/datasets/openfoodfacts/product-database/resolve/2fa5c6b88b9132d565b627576620a697f7ead869/food.parquet','snapshotCommit':'2fa5c6b88b9132d565b627576620a697f7ead869','count':len(products),'products':products,'retrievedAt':datetime.datetime.now(datetime.timezone.utc).isoformat()},open(B/'algeria-bulk.json','w'),ensure_ascii=False)
print('LOCAL EXTRACT COMPLETE',len(products),'Algeria-tagged products',round(time.monotonic()-start),'seconds',flush=True)
subprocess.run([sys.executable,str(B/'build_catalog.py')],cwd=B,check=True)
subprocess.run([sys.executable,'-u',str(B/'cache_images.py')],cwd=B,check=True)
print('FIRST 2500 CATALOG AND IMAGE CACHE COMPLETE',flush=True)
