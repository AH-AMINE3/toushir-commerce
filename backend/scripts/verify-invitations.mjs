import assert from 'node:assert/strict';
import {randomBytes,randomUUID} from 'node:crypto';
import {writeFile} from 'node:fs/promises';

// Creates only clearly labelled fictional accounts in new isolated demo spaces.
// No credentials or invitation capabilities are written to the report.
const base=(process.env.API_URL||'http://127.0.0.1:5882').replace(/\/$/,'');
const checks=[];let requests=0;
async function call(path,{method='GET',body,token,status=200}={}) {
 requests++;
 const response=await fetch(base+'/api'+path,{method,headers:{...(body!==undefined?{'Content-Type':'application/json'}:{}),...(token?{Authorization:'Bearer '+token}:{})},...(body!==undefined?{body:JSON.stringify(body)}:{})});
 const text=await response.text();let data;try{data=JSON.parse(text);}catch{data=text;}
 assert.equal(response.status,status,`${method} ${path.replace(/\b[a-f0-9]{64}\b/g,'[capability]')}: ${response.status}`);
 return data;
}
async function test(name,fn){try{await fn();checks.push({name,pass:true});console.log('PASS '+name);}catch(e){checks.push({name,pass:false,error:e.message});console.error('FAIL '+name+': '+e.message);throw e;}}
const joinKey=()=>randomBytes(32).toString('hex');
let owner,baseMerchant,otherOwner,invite,guestA,guestB,order,product,keyA;
try {
 await test('Only a demo owner can create or revoke an invitation in its workspace',async()=>{
  const health=await call('/health');assert.equal(health.demoEnabled,true);
  owner=await call('/demo/session',{method:'POST',body:{role:'ADMIN'}});
  baseMerchant=await call('/demo/session',{method:'POST',body:{role:'MERCHANT',workspaceToken:owner.workspaceToken}});
  otherOwner=await call('/demo/session',{method:'POST',body:{role:'ADMIN'}});
  const input={label:'EXEMPLE · vérification invitations',hours:1,maxUses:2};
  await call('/admin/demo/invitations',{method:'POST',body:input,status:401});
  await call('/admin/demo/invitations',{method:'POST',body:input,token:baseMerchant.token,status:403});
  invite=await call('/admin/demo/invitations',{method:'POST',body:input,token:owner.token});
  assert.match(invite.inviteToken,/^[a-f0-9]{64}$/);assert.equal(invite.maxUses,2);
  assert.equal(invite.joinPath,'/join/'+invite.inviteToken);
  await call('/admin/demo/invitations/'+invite.id+'/revoke',{method:'POST',token:otherOwner.token,status:404});
  const listed=await call('/admin/demo/invitations',{token:owner.token});
  assert(listed.items.some(x=>x.id===invite.id));assert(!JSON.stringify(listed).includes(invite.inviteToken));assert(!JSON.stringify(listed).includes('tokenHash'));
 });
 await test('An invitation creates only a merchant in the owner workspace without owner capabilities',async()=>{
  const description=await call('/demo/invitations/'+invite.inviteToken);assert.equal(description.isDemo,true);assert.equal(description.available,true);
  keyA=joinKey();guestA=await call('/demo/join',{method:'POST',body:{inviteToken:invite.inviteToken,joinKey:keyA,name:'Commerçant A · EXEMPLE',shopName:'Commerce A · EXEMPLE',commune:'Oran',street:'Adresse fictive de vérification',role:'ADMIN'}});
  assert.equal(guestA.user.role,'MERCHANT');assert.equal(guestA.workspace.id,owner.workspace.id);assert.equal(guestA.workspace.canSwitchRole,false);assert.equal(guestA.workspaceToken,null);
  const me=await call('/auth/me',{token:guestA.token});assert.equal(me.workspace.canSwitchRole,false);assert.equal(me.user.id,guestA.user.id);
  await call('/admin/dashboard',{token:guestA.token,status:403});
  await call('/admin/demo/invitations',{token:guestA.token,status:403});
  await call('/demo/session',{method:'POST',body:{role:'ADMIN',workspaceToken:invite.inviteToken},status:401});
 });
 await test('A stable private join key resumes the same account and consumes only one invitation use',async()=>{
  const repeat=await call('/demo/join',{method:'POST',body:{inviteToken:invite.inviteToken,joinKey:keyA,name:'Ignored duplicate',shopName:'Ignored duplicate'}});
  assert.equal(repeat.user.id,guestA.user.id);assert.equal(repeat.workspaceToken,null);
  let row=(await call('/admin/demo/invitations',{token:owner.token})).items.find(x=>x.id===invite.id);assert.equal(row.uses,1);
  guestB=await call('/demo/join',{method:'POST',body:{inviteToken:invite.inviteToken,joinKey:joinKey(),name:'Commerçant B · EXEMPLE',shopName:'Commerce B · EXEMPLE'}});
  assert.notEqual(guestB.user.id,guestA.user.id);assert.equal(guestB.workspace.id,owner.workspace.id);
  row=(await call('/admin/demo/invitations',{token:owner.token})).items.find(x=>x.id===invite.id);assert.equal(row.uses,2);
  await call('/demo/join',{method:'POST',body:{inviteToken:invite.inviteToken,joinKey:joinKey(),name:'EXEMPLE',shopName:'EXEMPLE'},status:409});
  assert.equal((await call('/demo/invitations/'+invite.inviteToken)).available,false);
 });
 await test('Invited merchants send real test orders to their owner while remaining isolated from other merchants',async()=>{
  product=(await call('/products?pageSize=1',{token:guestA.token})).items[0];
  const address=(await call('/addresses',{token:guestA.token}))[0];
  order=await call('/orders',{method:'POST',token:guestA.token,body:{idempotencyKey:randomUUID(),addressId:address.id,items:[{productId:product.id,quantity:2}],note:'EXEMPLE · commande de vérification d’invitation'}});
  assert.equal(order.status,'REQUESTED');assert.equal(order.merchant.id,guestA.user.id);
  assert.equal((await call('/orders/'+order.id,{token:owner.token})).id,order.id);
  assert((await call('/orders',{token:owner.token})).items.some(x=>x.id===order.id));
  assert.equal((await call('/orders',{token:guestB.token})).total,0);
  await call('/orders/'+order.id,{token:guestB.token,status:404});
  await call('/orders/'+order.id+'/document',{token:guestB.token,status:404});
  await call('/orders/'+order.id,{token:otherOwner.token,status:404});
  await call('/orders/'+order.id,{token:baseMerchant.token,status:404});
 });
 await test('The owner can quote and fulfill the invited merchant order using the normal stock contract',async()=>{
  const validity=new Date(Date.now()+2*3600000).toISOString().replace('Z','+01:00');
  const quoted=await call('/admin/orders/'+order.id+'/quote',{method:'POST',token:owner.token,body:{items:[{productId:product.id,quantity:2,unitPrice:123.45,availableStock:6,caseSize:1}],deliveryFee:0,validUntil:validity,note:'EXEMPLE · devis fictif'}});
  assert.equal(quoted.total,246.9);assert.equal(quoted.status,'QUOTED');
  const accepted=await call('/orders/'+order.id+'/accept-quote',{method:'POST',token:guestA.token,body:{version:quoted.quoteVersion}});
  assert.equal(accepted.status,'CONFIRMED');assert.equal(accepted.reservationHeld,true);
  for(const status of ['PREPARING','READY','OUT_FOR_DELIVERY'])await call('/admin/orders/'+order.id+'/transition',{method:'POST',token:owner.token,body:{status,reason:'EXEMPLE · vérification'}});
  const received=await call('/admin/orders/'+order.id+'/delivery',{method:'POST',token:owner.token,body:{items:[{productId:product.id,acceptedQuantity:2,refusedQuantity:0}],note:'EXEMPLE · réception fictive'}});
  assert.equal(received.status,'DELIVERED');assert.equal(received.paymentStatus,'UNPAID');
  assert.equal((await call('/orders/'+order.id,{token:guestA.token})).status,'DELIVERED');
 });
 await test('Revocation invalidates existing guest sessions and prevents resumed joins',async()=>{
  await call('/admin/demo/invitations/'+invite.id+'/revoke',{method:'POST',token:owner.token});
  await call('/auth/me',{token:guestA.token,status:401});await call('/auth/me',{token:guestB.token,status:401});
  await call('/demo/join',{method:'POST',body:{inviteToken:invite.inviteToken,joinKey:keyA,name:'EXEMPLE',shopName:'EXEMPLE'},status:404});
  await call('/demo/invitations/'+invite.inviteToken,{status:404});
  assert.equal((await call('/orders/'+order.id,{token:owner.token})).id,order.id);
 });
 await test('Concurrent guest joins respect an invitation use cap',async()=>{
  const capped=await call('/admin/demo/invitations',{method:'POST',token:owner.token,body:{label:'EXEMPLE · accès unique',hours:1,maxUses:1}});
  const results=await Promise.all([0,1].map(async i=>{
   requests++;const r=await fetch(base+'/api/demo/join',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({inviteToken:capped.inviteToken,joinKey:joinKey(),name:'EXEMPLE '+i,shopName:'EXEMPLE '+i})});return r.status;
  }));
  assert.deepEqual(results.sort(),[200,409]);
  const row=(await call('/admin/demo/invitations',{token:owner.token})).items.find(x=>x.id===capped.id);assert.equal(row.uses,1);
 });
}catch{}finally{
 const report={base,at:new Date().toISOString(),passed:checks.filter(x=>x.pass).length,failed:checks.filter(x=>!x.pass).length,requests,checks};
 await writeFile(new URL('../invitation-verification.json',import.meta.url),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({passed:report.passed,failed:report.failed,requests}));if(report.failed||checks.length!==7)process.exitCode=1;
}
