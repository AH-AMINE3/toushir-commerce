import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
const base=(process.env.API_URL||'').replace(/\/$/,'');assert(base,'Set API_URL to the hosted test origin.');
const checks=[];let history;
async function api(path,token,method='GET',body,timeout=20000){
 const response=await fetch(base+'/api'+path,{method,signal:AbortSignal.timeout(timeout),headers:{...(token?{Authorization:'Bearer '+token}:{}),...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});
 const value=await response.json();assert.equal(response.status,200,path+': '+(value.error?.code||response.status));return value;
}
try{
 const health=await api('/health');assert.match(health.storage,/PostgreSQL/);
 const first=await api('/demo/session',null,'POST',{role:'ADMIN'});
 const second=await api('/demo/session',null,'POST',{role:'MERCHANT'});
 const product=(await api('/products?pageSize=1',second.token)).items[0];
 let finished=false;
 history=api('/admin/demo/scenario',first.token,'POST',{},150000).then(value=>({value}),error=>({error})).finally(()=>{finished=true});
 await new Promise(resolve=>setTimeout(resolve,1000));assert.equal(finished,false,'History must still be running to exercise overlap');
 const started=performance.now();
 const basket=await api('/cart',second.token,'PUT',{items:[{productId:product.id,quantity:2}]});
 assert.equal(basket.items[0].quantity,2);assert.equal(finished,false,'Other workspace cart must commit before the history finishes');
 checks.push({name:'Another workspace saves its cart while the first history transaction is still running',pass:true,elapsedMs:Math.round(performance.now()-started)});
 const roleStarted=performance.now();
 const driver=await api('/demo/session',second.token,'POST',{role:'DRIVER',workspaceToken:second.workspaceToken});
 assert.equal(driver.user.role,'DRIVER');assert.equal(driver.workspace.id,second.workspace.id);assert.equal(finished,false,'Role bootstrap must not queue behind unrelated history');
 checks.push({name:'Courier role bootstrap remains independent of another workspace history',pass:true,elapsedMs:Math.round(performance.now()-roleStarted)});
 const outcome=await history;if(outcome.error)throw outcome.error;assert.equal(outcome.value.orders,40);
 assert.equal((await api('/orders',second.token)).total,0);assert.equal((await api('/cart',second.token)).items[0].quantity,2);
 checks.push({name:'History commits forty labelled orders without changing the other workspace cart or orders',pass:true});
}catch(error){checks.push({name:error.message,pass:false});process.exitCode=1;}finally{
 const report={base,at:new Date().toISOString(),passed:checks.filter(c=>c.pass).length,failed:checks.filter(c=>!c.pass).length,checks};
 await writeFile(process.env.VERIFICATION_REPORT||new URL('../concurrent-workspaces-verification.json',import.meta.url),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
}
