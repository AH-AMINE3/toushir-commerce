import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';

const base = (process.env.API_URL || 'http://127.0.0.1:5860').replace(/\/$/, '');
const checks = []; let requests = 0;
async function call(path, { method = 'GET', body, session, status = 200 } = {}) {
  requests++;
  const response = await fetch(base + path, { method, headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(session ? { Authorization: 'Bearer ' + session.token } : {}) }, ...(body !== undefined ? { body: JSON.stringify(body) } : {}) });
  const result = await response.json().catch(() => null);
  assert.equal(response.status, status, `${method} ${path}: ${result?.error?.code || response.status} ${result?.error?.message || ''}`);
  return result;
}
async function test(name, action) {
  try { await action(); checks.push({ name, pass: true }); console.log('PASS ' + name); }
  catch (error) { checks.push({ name, pass: false, error: error.message }); console.error('FAIL ' + name + ': ' + error.message); throw error; }
}
const post = (path, session, body, status = 200) => call('/api' + path, { method: 'POST', body, session, status });
const get = (path, session, status = 200) => call('/api' + path, { session, status });
let merchant, admin, driver, warehouse, dispatcher, finance, outsider, product, order, address, tracking, proof;
let connection; const messages = []; let invocation = 0;
async function connect(session) {
  const negotiated = await call('/hubs/commerce/negotiate?negotiateVersion=1', { method: 'POST', session });
  const url = base.replace(/^http/, 'ws') + '/hubs/commerce?id=' + encodeURIComponent(negotiated.connectionToken) + '&access_token=' + encodeURIComponent(session.token);
  const socket = new WebSocket(url);
  socket.addEventListener('message', event => { for (const part of String(event.data).split('\x1e').filter(Boolean)) { try { messages.push(JSON.parse(part)); } catch {} } });
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', () => reject(new Error('WebSocket connection failed')), { once: true }); });
  socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
  await waitFor(() => messages.some(message => Object.keys(message).length === 0));
  return socket;
}
async function waitFor(check, timeout = 4000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { if (check()) return; await new Promise(resolve => setTimeout(resolve, 25)); }
  throw new Error('Expected live response did not arrive');
}
async function invoke(target, args) {
  const id = String(++invocation);
  connection.send(JSON.stringify({ type: 1, invocationId: id, target, arguments: args }) + '\x1e');
  await waitFor(() => messages.some(message => message.type === 3 && message.invocationId === id));
  return messages.find(message => message.type === 3 && message.invocationId === id);
}
try {
  await test('ABP startup, six scoped roles and owner capability switching', async () => {
    const health = await get('/health'); assert.equal(health.abpVersion, '10.6.1'); assert.equal(health.realtime, 'SignalR');
    merchant = await post('/demo/session', null, { role: 'MERCHANT' });
    for (const role of ['ADMIN', 'DRIVER', 'WAREHOUSE', 'DISPATCHER', 'FINANCE']) {
      const session = await post('/demo/session', null, { role, workspaceToken: merchant.workspaceToken });
      assert.equal(session.workspace.id, merchant.workspace.id); assert.equal(session.user.role, role);
      if (role === 'ADMIN') admin = session; if (role === 'DRIVER') driver = session; if (role === 'WAREHOUSE') warehouse = session; if (role === 'DISPATCHER') dispatcher = session; if (role === 'FINANCE') finance = session;
    }
    outsider = await post('/demo/session', null, { role: 'DRIVER' });
    await post('/demo/session', null, { role: 'ADMIN', workspaceToken: 'invalid-owner-capability' }, 401);
    await get('/operations/orders', merchant, 403); await get('/operations/orders', driver, 403); await get('/courier/manifest', warehouse, 403);
    await get('/operations/team', finance, 403); await get('/admin/dashboard', driver, 403);
    const team = await get('/operations/team', dispatcher); assert(team.items.some(item => item.id === driver.user.id));
  });
  await test('Authenticated SignalR rejects anonymous, cross-workspace subscription and client publishing', async () => {
    await call('/hubs/commerce/negotiate?negotiateVersion=1', { method: 'POST', status: 401 });
    connection = await connect(merchant);
    assert((await invoke('SubscribeOrder', ['unknown-order'])).error);
    assert((await invoke('PublishLocation', [0, 0])).error);
  });
  await test('RFQ and versioned quote retain original transactional inventory workflow', async () => {
    product = (await get('/products?pageSize=1', merchant)).items[0];
    address = (await get('/addresses', merchant))[0];
    order = await post('/orders', merchant, { idempotencyKey: crypto.randomUUID(), addressId: address.id, items: [{ productId: product.id, quantity: 5 }], mode: 'REQUEST_QUOTE' });
    const quoted = await post('/admin/orders/' + order.id + '/quote', admin, { items: [{ productId: product.id, quantity: 5, unitPrice: 120, availableStock: 20, caseSize: 1 }], deliveryFee: 20, validUntil: new Date(Date.now() + 3600000).toISOString() });
    order = await post('/orders/' + order.id + '/accept-quote', merchant, { version: quoted.quoteVersion });
    assert.equal(order.total, 620); assert.equal(order.reservationHeld, true);
    assert.equal((await invoke('SubscribeOrder', [order.id])).error, undefined);
    await get('/orders/' + order.id + '/tracking', outsider, 404);
    await get('/orders/' + order.id + '/tracking', finance, 403);
    await post('/orders', driver, { idempotencyKey: crypto.randomUUID(), addressId: address.id, items: [{ productId: product.id, quantity: 1 }] }, 403);
  });
  await test('Per-line preparation validates quantities, roles and stale versions; commit emits live notice', async () => {
    await call('/api/operations/orders/' + order.id + '/preparation', { method: 'PATCH', session: dispatcher, body: { version: order.version, items: [{ productId: product.id, preparedQuantity: 1 }] }, status: 403 });
    await call('/api/operations/orders/' + order.id + '/preparation', { method: 'PATCH', session: warehouse, body: { version: order.version - 1, items: [{ productId: product.id, preparedQuantity: 1 }] }, status: 409 });
    await call('/api/operations/orders/' + order.id + '/preparation', { method: 'PATCH', session: warehouse, body: { version: order.version, items: [{ productId: product.id, preparedQuantity: 6 }] }, status: 400 });
    tracking = await call('/api/operations/orders/' + order.id + '/preparation', { method: 'PATCH', session: warehouse, body: { version: order.version, items: [{ productId: product.id, preparedQuantity: 3 }] } });
    assert.equal(tracking.preparation.percentage, 60); assert.equal(tracking.order.status, 'PREPARING');
    await waitFor(() => messages.some(message => message.target === 'order.updated' && message.arguments?.[0]?.orderId === order.id));
  });
  await test('Dispatcher assignment isolates driver manifests and pickup requires complete preparation', async () => {
    await post('/operations/orders/' + order.id + '/assign', warehouse, { driverId: driver.user.id, version: tracking.version }, 403);
    await post('/operations/orders/' + order.id + '/assign', dispatcher, { driverId: outsider.user.id, version: tracking.version }, 404);
    tracking = await post('/operations/orders/' + order.id + '/assign', dispatcher, { driverId: driver.user.id, version: tracking.version });
    assert.equal((await get('/courier/manifest', driver)).items.length, 1); assert.equal((await get('/courier/manifest', outsider)).items.length, 0);
    await post('/courier/orders/' + order.id + '/pickup', driver, { version: tracking.version }, 409);
    tracking = await call('/api/operations/orders/' + order.id + '/preparation', { method: 'PATCH', session: warehouse, body: { version: tracking.version, items: [{ productId: product.id, preparedQuantity: 5 }] } });
    assert.equal(tracking.order.status, 'READY');
    tracking = await post('/courier/orders/' + order.id + '/pickup', driver, { version: tracking.version });
    assert.equal(tracking.order.status, 'OUT_FOR_DELIVERY');
    const stock = await get('/products/' + product.id, admin); assert.equal(stock.physicalStock, 15); assert.equal(stock.reserved, 0);
    await post('/operations/orders/' + order.id + '/assign', dispatcher, { driverId: driver.user.id, version: tracking.version }, 409);
  });
  await test('GPS requires active assignment and consent; stale, future and reordered fixes are rejected', async () => {
    await post('/courier/location', outsider, { latitude: 35.7, longitude: -0.6, accuracy: 12, consent: true, capturedAt: new Date().toISOString() }, 409);
    await post('/courier/location', driver, { latitude: 35.7, longitude: -0.6, accuracy: 12, consent: true, capturedAt: new Date(Date.now() - 3600000).toISOString() }, 409);
    await post('/courier/location', driver, { latitude: 35.7, longitude: -0.6, accuracy: 12, consent: true, capturedAt: new Date(Date.now() + 3600000).toISOString() }, 409);
    const capturedAt = new Date().toISOString();
    await post('/courier/location', driver, { latitude: 35.7, longitude: -0.6, accuracy: 12, consent: true, capturedAt });
    tracking = await get('/orders/' + order.id + '/tracking', merchant); assert.equal(tracking.location.isFresh, true); assert.equal(tracking.location.source, 'DEVICE_GPS');
    await post('/courier/location', driver, { latitude: 35.7, longitude: -0.6, accuracy: 12, consent: true, capturedAt }, 409);
    await post('/courier/location', driver, { latitude: 0, longitude: 0, accuracy: 0, consent: false, capturedAt });
    assert.equal((await get('/orders/' + order.id + '/tracking', merchant)).location, null);
  });
  await test('Receipt challenge binds merchant, assigned driver, order version, quantities and one-time use', async () => {
    const items = [{ productId: product.id, acceptedQuantity: 4, refusedQuantity: 1 }];
    await post('/orders/' + order.id + '/receipt-challenge', merchant, { version: tracking.version, items }, 409);
    tracking = await post('/courier/orders/' + order.id + '/status', driver, { status: 'EN_ROUTE' });
    tracking = await post('/courier/orders/' + order.id + '/status', driver, { status: 'ARRIVED' });
    await post('/orders/' + order.id + '/receipt-challenge', driver, { version: tracking.version, items }, 403);
    await post('/orders/' + order.id + '/receipt-challenge', merchant, { version: tracking.version - 1, items }, 409);
    proof = await post('/orders/' + order.id + '/receipt-challenge', merchant, { version: tracking.version, items });
    assert(Date.parse(proof.expiresAt) - Date.now() <= 180000); assert.equal(proof.driverId, driver.user.id);
    await post('/courier/orders/' + order.id + '/complete', outsider, { challenge: proof.challenge, items }, 404);
    await post('/courier/orders/' + order.id + '/complete', driver, { challenge: proof.challenge, items: [{ productId: product.id, acceptedQuantity: 5, refusedQuantity: 0 }] }, 409);
    tracking = await post('/courier/orders/' + order.id + '/complete', driver, { challenge: proof.challenge, items, note: 'Réception physique de test' });
    assert.equal(tracking.order.status, 'PARTIALLY_DELIVERED'); assert.equal(tracking.order.total, 500); assert.equal(tracking.order.balance.collected, 0); assert.equal(tracking.receipt.merchantId, merchant.user.id);
    await post('/courier/orders/' + order.id + '/complete', driver, { challenge: proof.challenge, items }, 409);
    assert.equal(tracking.location, null);
  });
  await test('Cash tender and change are explicit, bounded and idempotent; finance reconciles separately', async () => {
    const collection = { amount: 500, tendered: 1000, change: 500, method: 'CASH', idempotencyKey: crypto.randomUUID(), reference: 'TEST-CASH-RECEIPT' };
    await post('/courier/orders/' + order.id + '/collect', driver, { ...collection, change: 400 }, 409);
    tracking = await post('/courier/orders/' + order.id + '/collect', driver, collection); assert.equal(tracking.order.balance.collected, 500);
    tracking = await post('/courier/orders/' + order.id + '/collect', driver, collection); assert.equal(tracking.order.balance.collected, 500);
    await post('/courier/orders/' + order.id + '/collect', driver, { ...collection, tendered: 500, change: 0 }, 409);
    await post('/courier/orders/' + order.id + '/collect', driver, { ...collection, amount: 1, tendered: 1, change: 0, idempotencyKey: crypto.randomUUID() }, 409);
    const detail = await get('/orders/' + order.id, merchant); assert.equal(detail.payments.length, 1);
    await post('/operations/payments/' + detail.payments[0].id + '/reconcile', warehouse, {}, 403);
    const reconciled = await post('/operations/payments/' + detail.payments[0].id + '/reconcile', finance, {}); assert.equal(reconciled.paymentStatus, 'RECONCILED');
  });
  await test('Refused units enter quarantine and warehouse inspection is explicitly separate', async () => {
    let stock = await get('/products/' + product.id, admin); assert.equal(stock.quarantined, 1); assert.equal(stock.physicalStock, 15);
    await post('/operations/orders/' + order.id + '/returns/inspect', driver, { productId: product.id, quantity: 1, disposition: 'RESTOCK', reason: 'Interdit' }, 403);
    await post('/operations/orders/' + order.id + '/returns/inspect', warehouse, { productId: product.id, quantity: 1, disposition: 'RESTOCK', reason: 'Unité physiquement contrôlée' });
    stock = await get('/products/' + product.id, admin); assert.equal(stock.quarantined, 0); assert.equal(stock.physicalStock, 16);
    await post('/operations/orders/' + order.id + '/returns/inspect', warehouse, { productId: product.id, quantity: 1, disposition: 'RESTOCK', reason: 'Double contrôle interdit' }, 400);
  });
} catch {} finally {
  connection?.close();
  const report = { base, at: new Date().toISOString(), passed: checks.filter(x => x.pass).length, failed: checks.filter(x => !x.pass).length, requests, checks };
  await writeFile(process.env.VERIFICATION_REPORT || new URL('../operations-verification.json', import.meta.url), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, failed: report.failed, requests }));
  if (report.failed) process.exitCode = 1;
}
