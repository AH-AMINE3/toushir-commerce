import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';

// Exercises only new, isolated, explicitly fictional demo workspaces through HTTP.
// No database credentials, direct SQL, timestamp fixtures or real owner account.
const base=process.env.API_URL?.replace(/\/$/,'');
assert(base,'Set API_URL to the testable hosted API origin.');
const checks=[];let requests=0,merchant,admin,foreign,products,initialOrder,basket,orders;
async function call(path,{method='GET',body,token=admin?.token,status=200}={}){
 requests++;const r=await fetch(base+'/api'+path,{method,headers:{...(body!==undefined?{'Content-Type':'application/json'}:{}),...(token?{Authorization:'Bearer '+token}:{})},...(body!==undefined?{body:JSON.stringify(body)}:{})});
 const text=await r.text();let data;try{data=JSON.parse(text);}catch{data=text;}
 assert.equal(r.status,status,method+' '+path+': '+r.status);return data;
}
async function test(name,fn){try{await fn();checks.push({name,pass:true});console.log('PASS '+name);}catch(e){checks.push({name,pass:false,error:e.message});console.error('FAIL '+name+': '+e.message);throw e;}}
async function all(path,token=admin.token){const rows=[];for(let page=1;;page++){const response=await call(path+(path.includes('?')?'&':'?')+'page='+page+'&pageSize=100',{token});rows.push(...response.items);if(page>=response.totalPages)return rows;}}
const cents=value=>Math.round(value*100);
const day=value=>new Intl.DateTimeFormat('en-CA',{timeZone:'Africa/Algiers',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date(value));
const inPeriod=(value,from,to)=>day(value)>=from&&day(value)<=to;
try{
 await test('Hosted PostgreSQL and the complete source catalog are connected',async()=>{
  const health=await call('/health',{token:null});assert.equal(health.status,'ok');assert.equal(health.database,'connected');assert.equal(health.catalogCount,2500);assert.match(health.storage,/PostgreSQL/);assert.equal(health.demoEnabled,true);
  merchant=await call('/demo/session',{method:'POST',token:null,body:{role:'MERCHANT'}});
  admin=await call('/demo/session',{method:'POST',token:null,body:{role:'ADMIN',workspaceToken:merchant.workspaceToken}});
  foreign=await call('/demo/session',{method:'POST',token:null,body:{role:'ADMIN'}});
  await call('/admin/demo/scenario',{method:'POST',token:null,status:401});await call('/admin/demo/scenario',{method:'POST',token:merchant.token,status:403});
  products=await all('/admin/products');assert.equal(products.length,2500);assert(products.every(p=>p.price===null&&p.physicalStock===null&&!p.offerApproved));
 });
 await test('Scenario generation is explicit, idempotent, isolated and preserves current merchant work',async()=>{
  const featured=JSON.parse(await readFile(new URL('../../catalog/featured-barcodes.json',import.meta.url),'utf8'));
  const p=products.find(p=>!featured.includes(p.barcode)),address=(await call('/addresses',{token:merchant.token}))[0];
  initialOrder=await call('/orders',{method:'POST',token:merchant.token,body:{idempotencyKey:randomUUID(),addressId:address.id,items:[{productId:p.id,quantity:3}],note:'EXEMPLE · demande préexistante de vérification'}});
  await call('/cart',{method:'PUT',token:merchant.token,body:{items:[{productId:p.id,quantity:7}]}});basket=await call('/cart',{token:merchant.token});
  const results=await Promise.all([call('/admin/demo/scenario',{method:'POST'}),call('/admin/demo/scenario',{method:'POST'})]);
  assert.equal(results.filter(r=>r.seeded).length,1);assert.equal(results.reduce((n,r)=>n+r.orders,0),40);
  const summaries=await all('/orders');assert.equal(summaries.length,41);
  orders=[];for(const row of summaries)orders.push(await call('/orders/'+row.id));
  const seeded=orders.filter(o=>o.id!==initialOrder.id);assert.equal(seeded.length,40);assert.equal(new Set(seeded.map(o=>o.merchant.id)).size,4);
  assert(seeded.every(o=>o.number.startsWith('EXEMPLE-')&&o.note.includes('scénario fictif')&&o.isDemo));
  assert.equal((await call('/orders',{token:foreign.token})).total,0);
  assert.deepEqual(await call('/orders/'+initialOrder.id),initialOrder);assert.deepEqual(await call('/cart',{token:merchant.token}),basket);
  assert.deepEqual(await call('/admin/demo/scenario',{method:'POST'}),{seeded:false,orders:0});
 });
 await test('PostgreSQL stock, reservations and return quarantine reconcile with the full physical ledger',async()=>{
  products=await all('/admin/products');assert(products.every(p=>p.price===null&&!p.offerApproved));
  const changed=products.filter(p=>p.physicalStock!==null);assert.equal(changed.length,3);
  const ledger=await all('/admin/inventory');
  for(const p of changed){const entries=ledger.filter(e=>e.productId===p.id);assert.equal(entries.reduce((n,e)=>n+e.delta,0),p.physicalStock);assert.equal(entries.reduce((n,e)=>n+e.reservedDelta,0),p.reserved);assert.equal(entries.reduce((n,e)=>n+e.quarantineDelta,0),p.quarantined);assert(p.physicalStock>=p.reserved&&p.reserved>=0&&p.quarantined>=0);
   assert.equal(p.reserved,orders.filter(o=>o.reservationHeld).flatMap(o=>o.items).filter(l=>l.productId===p.id).reduce((n,l)=>n+l.quantity,0));
   assert.equal(p.quarantined,orders.flatMap(o=>o.items).filter(l=>l.productId===p.id).reduce((n,l)=>n+l.refusedQuantity-l.inspectedQuantity,0));
  }
  const audit=await all('/admin/audit');assert.equal(audit.filter(e=>e.action==='DEMO_SCENARIO_CREATED').length,1);
 });
 await test('Historical charts and comparative KPIs reconcile to actual scoped orders and independent cash entries',async()=>{
  for(const days of [7,30]){
   const report=await call('/admin/analytics?days='+days),period=report.period;
   assert.equal(period.timezone,'Africa/Algiers');assert.equal(report.daily.length,days);assert.equal(report.previousDaily.length,days);
   for(const [prefix,from,to] of [['value',period.from,period.to],['previous',period.previousFrom,period.previousTo]]){
    const selected=orders.filter(o=>inPeriod(o.createdAt,from,to));const delivered=orders.filter(o=>o.deliveredAt&&['DELIVERED','PARTIALLY_DELIVERED','DELIVERY_REFUSED'].includes(o.status)&&inPeriod(o.deliveredAt,from,to));
    const payments=orders.flatMap(o=>o.payments).filter(p=>inPeriod(p.at,from,to));
    assert.equal(report.metrics.orders[prefix],selected.length);assert.equal(cents(report.metrics.revenue[prefix]),cents(delivered.reduce((n,o)=>n+(o.total||0),0)));assert.equal(cents(report.metrics.collected[prefix]),cents(payments.reduce((n,p)=>n+(p.isRefund?-p.amount:p.amount),0)));assert.equal(report.metrics.customers[prefix],new Set(selected.map(o=>o.merchant.id)).size);
   }
   assert.equal(cents(report.daily.reduce((n,p)=>n+p.revenue,0)),cents(report.metrics.revenue.value));assert.equal(report.daily.reduce((n,p)=>n+p.orders,0),report.metrics.orders.value);
   assert.equal(cents(report.previousDaily.reduce((n,p)=>n+p.collected,0)),cents(report.metrics.collected.previous));
   const current=orders.filter(o=>o.total!==null&&['CONFIRMED','PREPARING','READY','OUT_FOR_DELIVERY','DELIVERED','PARTIALLY_DELIVERED','DELIVERY_REFUSED'].includes(o.status));
   assert.equal(cents(report.operations.remaining),cents(current.reduce((n,o)=>n+Math.max(0,o.total-o.balance.collected),0)));
   assert.equal(cents(report.aging.reduce((n,a)=>n+a.amount,0)),cents(report.operations.remaining));
  }
  const denied=await call('/admin/analytics',{token:foreign.token});assert.equal(denied.metrics.orders.value,0);assert.equal(denied.metrics.revenue.value,0);
 });
 await test('All operating statuses and financial ledgers remain consistent without publishing fictitious catalog prices',async()=>{
  const statuses=new Set(orders.map(o=>o.status));for(const status of ['REQUESTED','QUOTED','CONFIRMED','PREPARING','READY','OUT_FOR_DELIVERY','DELIVERED','PARTIALLY_DELIVERED','CANCELLED'])assert(statuses.has(status),status);
  for(const o of orders){assert.equal(cents(o.payments.reduce((n,p)=>n+(p.isRefund?-p.amount:p.amount),0)),cents(o.balance.collected));if(o.status==='CANCELLED'){assert.equal(o.total,0);assert.equal(o.balance.refundDue,o.balance.collected);}if(o.status==='QUOTED')assert(new Date(o.quoteValidUntil)>new Date());if(o.status==='REQUESTED')assert.equal(o.total,null);}
  assert.equal((await call('/auth/me',{token:merchant.token})).user.id,merchant.user.id);assert.equal((await call('/auth/me')).user.id,admin.user.id);
  assert.equal((await call('/products?pageSize=1',{token:merchant.token})).total,2500);
 });
}catch{}finally{
 const report={base,at:new Date().toISOString(),scope:'Hosted PostgreSQL HTTP workflows in new fictional demo workspaces only',passed:checks.filter(x=>x.pass).length,failed:checks.filter(x=>!x.pass).length,requests,checks};
 await writeFile(new URL('../hosted-scenario-verification.json',import.meta.url),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({passed:report.passed,failed:report.failed,requests}));if(report.failed||checks.length!==5)process.exitCode=1;
}
