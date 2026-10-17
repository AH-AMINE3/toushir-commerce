import os,json,time,sys
sys.path.insert(0,os.path.join(os.path.dirname(__file__),'python-deps'))
import duckdb
base=os.path.dirname(os.path.abspath(__file__))
c=duckdb.connect(config={'custom_user_agent':'ToushirWholesaleCatalog/1.0 (https://github.com/AH-AMINE3)'});c.execute("SET home_directory='"+base.replace("'","''")+"'");c.execute('INSTALL httpfs');c.execute('LOAD httpfs');c.execute('SET threads=4');c.execute('SET http_timeout=25');c.execute('SET http_retries=2');c.execute('SET allow_asterisks_in_http_paths=true')
source='https://huggingface.co/datasets/openfoodfacts/product-database/resolve/2fa5c6b88b9132d565b627576620a697f7ead869/food.parquet'
u=os.environ.get('TOUSHIR_OFF_PARQUET',source)
start=time.time();print('Extracting Algeria subset from official bulk snapshot',flush=True)
q="""SELECT code,product_name,brands,categories,categories_tags,quantity,countries_tags,images,obsolete,completeness,unique_scans_n,last_modified_t,lang,photographers FROM read_parquet(?) WHERE list_contains(countries_tags,'en:algeria')"""
rows=c.execute(q,[u]); cols=[r[0] for r in rows.description]
products=[dict(zip(cols,r)) for r in rows.fetchall()]
json.dump({'dataset':source,'count':len(products),'products':products},open(os.path.join(base,'algeria-bulk.json'),'w'),ensure_ascii=False)
print('Extracted',len(products),'in',round(time.time()-start),'seconds',flush=True)
