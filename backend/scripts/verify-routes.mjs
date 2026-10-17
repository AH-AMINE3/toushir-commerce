import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';

const base = (process.env.API_URL || 'http://127.0.0.1:5860').replace(/\/$/, '');
const checks = []; let requests = 0;
async function call(path, session, body, status = 200, method = body === undefined ? 'GET' : 'POST') {
  requests++;
  const response = await fetch(base + '/api' + path, { method, headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(session ? { Authorization: 'Bearer ' + session.token } : {}) }, ...(body !== undefined ? { body: JSON.stringify(body) } : {}) });
  const value = await response.json().catch(() => null);
  assert.equal(response.status, status, `${method} ${path}: ${value?.error?.code || response.status} ${value?.error?.message || ''}`); return value;
}
async function test(name, action) { try { await action(); checks.push({ name, pass: true }); console.log('PASS ' + name); } catch (error) { checks.push({ name, pass: false, error: error.message }); console.error('FAIL ' + name + ': ' + error.message); throw error; } }
let merchant, admin, driver, warehouse, dispatcher, finance, outsider, product, address, route, challenge, oldChallenge;
const orders = [];
try {
  await test('Route personas retain independent workspace and role access', async () => {
    merchant = await call('/demo/session', null, { role: 'MERCHANT' });
    for (const role of ['ADMIN', 'DRIVER', 'WAREHOUSE', 'DISPATCHER', 'FINANCE']) {
      const value = await call('/demo/session', null, { role, workspaceToken: merchant.workspaceToken });
      if (role === 'ADMIN') admin = value; if (role === 'DRIVER') driver = value; if (role === 'WAREHOUSE') warehouse = value; if (role === 'DISPATCHER') dispatcher = value; if (role === 'FINANCE') finance = value;
    }
    outsider = await call('/demo/session', null, { role: 'DRIVER' });
    await call('/operations/routes', merchant, undefined, 403); await call('/courier/routes', warehouse, undefined, 403);
    product = (await call('/products?pageSize=1', merchant)).items[0]; address = (await call('/addresses', merchant))[0];
  });
  await test('Three prepared orders reproduce the Figma route cash fixture', async () => {
    for (const [quantity, unitPrice] of [[2, 9650], [2, 10550], [3, 9300]]) {
      let order = await call('/orders', merchant, { idempotencyKey: crypto.randomUUID(), addressId: address.id, items: [{ productId: product.id, quantity }], mode: 'REQUEST_QUOTE' });
      order = await call('/admin/orders/' + order.id + '/quote', admin, { items: [{ productId: product.id, quantity, unitPrice, availableStock: 40, caseSize: 1 }], deliveryFee: 0, validUntil: new Date(Date.now() + 3600000).toISOString() });
      order = await call('/orders/' + order.id + '/accept-quote', merchant, { version: order.quoteVersion });
      const tracking = await call('/operations/orders/' + order.id + '/preparation', warehouse, { version: order.version, items: [{ productId: product.id, preparedQuantity: quantity }] }, 200, 'PATCH');
      assert.equal(tracking.order.status, 'READY'); orders.push(tracking.order);
    }
    await call('/operations/routes',dispatcher,{name:'Surcharge refusée',driverId:driver.user.id,orderIds:orders.map(x=>x.id),openingFloat:0,capacityUnits:6},409);
    route = await call('/operations/routes', dispatcher, { name: 'Tournée C03 · exemple', driverId: driver.user.id, orderIds: orders.map(x => x.id), openingFloat: 5000,capacityUnits:100, vehicleReference: 'Véhicule de test' });
    assert.equal(route.stops.length, 3);assert.deepEqual(route.stops.map(x=>x.id),orders.map(x=>x.id));assert.equal(route.load.totalUnits,7); assert.equal(route.cash.expected, 5000); assert.equal(route.status, 'PLANNED');
    await call('/courier/routes/' + route.id, outsider, undefined, 404);
    assert.equal((await call('/courier/routes', driver)).items[0].id, route.id);
    const manifest = await call('/courier/manifest', driver); assert(manifest.items.every(x => x.assignment.routeId === route.id && x.assignment.custodyMode === 'ROUTE_MANIFEST'));
  });
  await test('Warehouse-to-driver custody binds the whole manifest and is consumed atomically once', async () => {
    await call('/operations/routes/' + route.id + '/manifest-challenge', dispatcher, { version: route.version }, 403);
    await call('/courier/orders/' + orders[0].id + '/pickup', driver, { version: route.stops[0].version }, 409);
    oldChallenge = await call('/operations/routes/' + route.id + '/manifest-challenge', warehouse, { version: route.version });
    route=await call('/operations/routes/'+route.id+'/reorder',dispatcher,{version:route.version,orderIds:orders.map(x=>x.id).reverse(),capacityUnits:100});assert.deepEqual(route.stops.map(x=>x.id),orders.map(x=>x.id).reverse());
    await call('/operations/routes/'+route.id+'/reorder',dispatcher,{version:route.version-1,orderIds:orders.map(x=>x.id),capacityUnits:100},409);
    challenge = await call('/operations/routes/' + route.id + '/manifest-challenge', warehouse, { version: route.version });
    await call('/courier/routes/' + route.id + '/accept-custody', driver, { version: route.version, challenge: oldChallenge.challenge }, 409);
    await call('/courier/routes/' + route.id + '/accept-custody', outsider, { version: route.version, challenge: challenge.challenge }, 404);
    await call('/courier/routes/' + route.id + '/accept-custody', driver, { version: route.version - 1, challenge: challenge.challenge }, 409);
    const originalVersion = route.version;
    route = await call('/courier/routes/' + route.id + '/accept-custody', driver, { version: route.version, challenge: challenge.challenge });
    assert.equal(route.status, 'ACTIVE'); assert(route.custody); assert.equal(route.custody.warehouseActorId, warehouse.user.id); assert.equal(route.custody.acceptedVersion, originalVersion);
    assert(route.stops.every(x => x.status === 'OUT_FOR_DELIVERY' && x.assignment.status === 'PICKED_UP'));
    await call('/courier/routes/' + route.id + '/accept-custody', driver, { version: route.version, challenge: challenge.challenge }, 409);
    await call('/courier/routes/' + route.id + '/declare-cash', driver, { version: route.version, declaredAmount: 5000 }, 409);
  });
  await test('Merchant proofs, partial refusal and three cash receipts produce 64,000 DZD expected custody cash', async () => {
    for (let i = 0; i < orders.length; i++) {
      const order = orders[i]; await call('/courier/orders/' + order.id + '/status', driver, { status: 'EN_ROUTE' });
      const tracking = await call('/courier/orders/' + order.id + '/status', driver, { status: 'ARRIVED' });
      const items = [{ productId: product.id, acceptedQuantity: 2, refusedQuantity: i === 2 ? 1 : 0 }];
      const proof = await call('/orders/' + order.id + '/receipt-challenge', merchant, { version: tracking.version, items });
      assert.equal(proof.routeId, route.id); assert.equal(proof.routeVersion, route.version);
      const completed = await call('/courier/orders/' + order.id + '/complete', driver, { challenge: proof.challenge, items });
      const expected = [19300, 21100, 18600][i]; assert.equal(completed.order.total, expected); assert.equal(completed.order.balance.collected, 0);
      const collected = await call('/courier/orders/' + order.id + '/collect', driver, { amount: expected, tendered: expected + 100, change: 100, method: 'CASH', idempotencyKey: crypto.randomUUID() });
      assert.equal(collected.order.balance.remaining, 0);
    }
    route = await call('/courier/routes/' + route.id, driver); assert.equal(route.cash.expected, 64000); assert.equal(route.cash.tendered, 59300); assert.equal(route.cash.change, 300); assert.equal(route.cash.netReceipts, 59000);
    assert.equal((await call('/orders/' + orders[2].id, merchant)).total, 18600);
  });
  await test('Driver declaration freezes route collection; finance separately counts an internal shortage', async () => {
    await call('/courier/routes/' + route.id + '/declare-cash', finance, { version: route.version, declaredAmount: 64000 }, 403);
    await call('/courier/routes/'+route.id+'/declare-cash',driver,{version:route.version,declaredAmount:64000},409);
    const returned=await call('/orders/'+orders[2].id,merchant);
    route=await call('/operations/routes/'+route.id+'/returns',warehouse,{orderId:returned.id,version:returned.version,items:[{productId:product.id,quantity:1}],note:'Une unité physiquement reçue au dépôt'});
    route = await call('/courier/routes/' + route.id + '/declare-cash', driver, { version: route.version, declaredAmount: 64000, note: 'Caisse remise pour comptage' });
    assert.equal(route.cash.state, 'DECLARED'); assert.equal(route.cash.expectedAtDeclaration, 64000);
    await call('/courier/orders/' + orders[0].id + '/collect', driver, { amount: 1, tendered: 1, change: 0, method: 'CASH', idempotencyKey: crypto.randomUUID() }, 409);
    await call('/operations/routes/' + route.id + '/cash-count', dispatcher, { version: route.version, countedAmount: 63500, note: 'Interdit' }, 403);
    await call('/operations/routes/' + route.id + '/cash-count', finance, { version: route.version, countedAmount: 63500 }, 400);
    route = await call('/operations/routes/' + route.id + '/cash-count', finance, { version: route.version, countedAmount: 63500, note: 'Écart interne de caisse de 500 DZD à examiner' });
    assert.equal(route.cash.shortage, 500); assert.equal(route.cash.surplus, 0); assert.equal(route.cash.state, 'COUNTED');
    for (const order of orders) { const detail = await call('/orders/' + order.id, merchant); assert.equal(detail.balance.remaining, 0); assert.equal(detail.balance.refundDue, 0); assert.equal(detail.paymentStatus, 'COLLECTED'); }
  });
  await test('Route reconciliation retains shortage liability without reopening merchant debt', async () => {
    await call('/operations/routes/' + route.id + '/reconcile', finance, { version: route.version }, 400);
    route = await call('/operations/routes/' + route.id + '/reconcile', finance, { version: route.version, note: 'Manque interne conservé pour investigation, commerçants quittes' });
    assert.equal(route.status, 'SETTLED'); assert.equal(route.cash.state, 'RECONCILED'); assert.equal(route.cash.shortage, 500); assert.equal(route.cash.counted, 63500); assert.equal(route.cash.merchantDebtUnaffected, true);
    const third = await call('/orders/' + orders[2].id, merchant); assert.equal(third.total, 18600); assert.equal(third.balance.collected, 18600); assert.equal(third.balance.remaining, 0);
    const audit = await call('/admin/audit?pageSize=100', admin); assert(audit.items.some(x => x.action === 'ROUTE_CASH_RECONCILED'));
  });
} catch {} finally {
  const report = { base, at: new Date().toISOString(), passed: checks.filter(x => x.pass).length, failed: checks.filter(x => !x.pass).length, requests, checks };
  await writeFile(process.env.VERIFICATION_REPORT || new URL('../route-verification.json', import.meta.url), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, failed: report.failed, requests })); if (report.failed) process.exitCode = 1;
}
