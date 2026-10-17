import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { cp, mkdtemp, writeFile, readFile, readdir, mkdir, rm } from 'node:fs/promises';
import { resolve as resolvePath, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const backend = resolvePath(dirname(fileURLToPath(import.meta.url)), '..');
const dotnet = process.env.DOTNET_BIN || '/Users/amine/Library/Developer/Toushir/toolchains/dotnet/dotnet';
const port = Number(process.env.TEST_PORT || 5870);
const base = `http://127.0.0.1:${port}`;
const restorePort = Number(process.env.TEST_RESTORE_PORT || port + 1);
assert.notEqual(restorePort, port, 'Restore verification must use a separate port.');
const restoreBase = `http://127.0.0.1:${restorePort}`;
await mkdir(resolvePath(backend, '.test-data'), { recursive: true, mode: 0o700 });
const data = await mkdtemp(resolvePath(backend, '.test-data/production-'));
let restoreData;
const master = JSON.parse(await readFile(resolvePath(backend, '../catalog/products.json'), 'utf8'));
const seed = (Array.isArray(master) ? master : master.products).slice(0, 2);
assert.equal(seed.length, 2);
const fixture = resolvePath(data, 'catalog.json');
await writeFile(fixture, JSON.stringify(seed));
const staticRoot = resolvePath(data, 'wwwroot');
await mkdir(resolvePath(staticRoot, 'mobile/_expo/static/js/web'), { recursive: true });
await mkdir(resolvePath(staticRoot, 'downloads'), { recursive: true });
await writeFile(resolvePath(staticRoot, 'index.html'), '<html>desktop test fallback</html>');
await writeFile(resolvePath(staticRoot, 'mobile/index.html'), '<html>mobile test fallback</html>');
await writeFile(resolvePath(staticRoot, 'mobile/_expo/static/js/web/asset.js'), 'globalThis.__toushirAssetTest = true;');
await writeFile(resolvePath(staticRoot, 'downloads/toushir-merchant.apk'), 'isolated test artifact');
await writeFile(resolvePath(staticRoot, 'downloads/manifest.json'), JSON.stringify({ merchant: { apiOrigin: `https://127.0.0.1:${port}` }, driver: { apiOrigin: 'http://10.0.2.2:5860' } }));

const ownerPassword = crypto.randomUUID() + crypto.randomUUID();
const merchantPassword = crypto.randomUUID();
const newPassword = crypto.randomUUID();
// These tests always own disposable SQLite data, even when the invoking shell has hosting secrets.
const env = {
  ...process.env,
  DATABASE_URL: '', Database__Schema: '', Oidc__Issuer: '', RENDER_EXTERNAL_URL: '',
  Oidc__signingCertificateBase64: '', Oidc__encryptionCertificateBase64: '',
  Oidc__signingCertificatePassword: '', Oidc__encryptionCertificatePassword: '',
  DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: 'true', ASPNETCORE_ENVIRONMENT: 'Production',
  ASPNETCORE_URLS: base, Demo__Enabled: 'false', DataPath: data, Catalog__Path: fixture,
  Bootstrap__Email: 'owner-test@example.invalid', Bootstrap__Password: ownerPassword,
  ForwardedHeaders__TrustPlatformProxy: 'true', Cors__Origins: 'https://wholesale.example.invalid',
};
const checks = [];
let child, logs = '', requests = 0, owner, merchant, order, paidOrder, originalPublicKeys;
const backup = resolvePath(data, 'backups/verified.db');

async function launch(configuration = env, origin = base) {
  logs = '';
  child = spawn(dotnet, [resolvePath(backend, 'bin/Release/net10.0/Toushir.Wholesale.dll'), '--contentRoot', configuration.DataPath], {
    cwd: backend, env: configuration, stdio: ['ignore', 'pipe', 'pipe'],
  });
  child.stdout.on('data', x => logs = (logs + x).slice(-12000));
  child.stderr.on('data', x => logs = (logs + x).slice(-12000));
  for (let i = 0; i < 300; i++) {
    if (child.exitCode !== null || child.signalCode !== null) throw Error('Server exited: ' + logs);
    try { if ((await fetch(origin + '/api/health')).ok) return; } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw Error('Startup timed out: ' + logs);
}
async function stop() {
  const stopping = child;
  if (!stopping || stopping.exitCode !== null || stopping.signalCode !== null) return;
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      if (stopping.exitCode === null && stopping.signalCode === null) {
        stopping.kill('SIGKILL'); reject(Error('Shutdown timed out'));
      }
    }, 6000);
    timer.unref();
    stopping.once('exit', () => { clearTimeout(timer); resolve(); });
    stopping.kill('SIGINT');
  });
}
async function python(program, args = []) {
  await new Promise((resolve, reject) => {
    const process = spawn(env.PYTHON_BIN || 'python3', ['-c', program, ...args], { stdio: ['ignore', 'pipe', 'pipe'] });
    let error = '';
    process.stderr.on('data', x => error += x);
    process.on('error', reject);
    process.on('exit', code => code === 0 ? resolve() : reject(Error(error)));
  });
}
async function call(path, { method = 'GET', body, token, status = 200, headers = {}, origin = base } = {}) {
  requests++;
  const response = await fetch(origin + '/api' + path, {
    method,
    headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: 'Bearer ' + token } : {}), ...headers },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  const text = await response.text();
  let value;
  try { value = JSON.parse(text); } catch { value = text; }
  assert.equal(response.status, status, method + ' ' + path + ': ' + text);
  return { data: value, response };
}
async function publicKeys(origin) {
  requests++;
  const response = await fetch(origin + '/.well-known/jwks', { headers: { 'X-Forwarded-Proto': 'https' } });
  assert.equal(response.status, 200, 'Restored identity authority must expose its public signing keys.');
  const value = await response.json();
  assert(value.keys.length > 0);
  assert(value.keys.every(key => !key.d));
  return value.keys;
}
async function test(name, action) {
  try { await action(); checks.push({ name, pass: true }); console.log('PASS ' + name); }
  catch (error) { checks.push({ name, pass: false, error: error.message }); console.error('FAIL ' + name + ': ' + error.message); throw error; }
}
try {
  await launch();
  await test('Production mode disables public demo and provisions only explicit owner', async () => {
    assert.equal((await call('/health')).data.demoEnabled, false);
    const distribution = (await call('/distribution')).data;
    assert.equal(distribution.merchant.available, false); assert.equal(distribution.driver.available, false);
    await call('/demo/session', { method: 'POST', body: { role: 'ADMIN' }, status: 404 });
    owner = await call('/auth/login', { method: 'POST', body: { email: 'owner-test@example.invalid', password: ownerPassword } });
    assert.equal(owner.data.user.role, 'ADMIN');
    assert(owner.response.headers.get('set-cookie').includes('secure'));
    await call('/auth/login', { method: 'POST', body: { email: 'owner-test@example.invalid', password: 'wrong-password' }, status: 401 });
  });
  await test('Registration requires real password and merchant approval', async () => {
    const input = { name: 'Nadia · Test', shopName: 'Boutique de test', phone: '0000000000', email: 'merchant-test@example.invalid', password: merchantPassword, address: { label: 'Test', wilaya: 'Oran', commune: 'Oran', street: 'Adresse fictive de test' } };
    await call('/auth/register', { method: 'POST', body: { ...input, password: 'short' }, status: 400 });
    merchant = (await call('/auth/register', { method: 'POST', body: input })).data;
    assert.equal(merchant.user.approvalStatus, 'PENDING');
    const addresses = (await call('/addresses', { token: merchant.token })).data;
    const products = (await call('/products?pageSize=1', { token: merchant.token })).data;
    const request = { idempotencyKey: crypto.randomUUID(), addressId: addresses[0].id, items: [{ productId: products.items[0].id, quantity: 2 }] };
    await call('/orders', { method: 'POST', token: merchant.token, body: request, status: 403 });
    await call('/admin/merchants/' + merchant.user.id, { method: 'PATCH', token: owner.data.token, body: { approvalStatus: 'APPROVED' } });
    order = (await call('/orders', { method: 'POST', token: merchant.token, body: request })).data;
    assert.equal(order.status, 'REQUESTED');
    assert.equal(order.address.street, input.address.street);
    assert.equal(order.address.commune, input.address.commune);
  });
  await test('HTTPS browser writes work behind trusted platform proxy and foreign origins fail', async () => {
    const profile = { name: 'Nadia · Test', shopName: 'Boutique de test', phone: '0000000000' };
    await call('/profile', { method: 'PATCH', token: merchant.token, headers: { Origin: 'https://wholesale.example.invalid', 'X-Forwarded-Proto': 'https', 'X-Forwarded-For': '198.51.100.20' }, body: profile });
    await call('/profile', { method: 'PATCH', token: merchant.token, headers: { Origin: `https://127.0.0.1:${port}`, 'X-Forwarded-Proto': 'https', 'X-Forwarded-For': '198.51.100.20' }, body: profile });
    await call('/profile', { method: 'PATCH', token: merchant.token, headers: { Origin: 'https://untrusted.example.invalid' }, body: profile, status: 403 });
  });
  await test('Mobile assets retain JavaScript MIME, navigation uses HTML and canonical downloads support GET HEAD and range', async () => {
    requests++;
    const script = await fetch(base + '/mobile/_expo/static/js/web/asset.js');
    assert.equal(script.status, 200); assert(script.headers.get('content-type').includes('javascript'));
    assert.equal(await script.text(), 'globalThis.__toushirAssetTest = true;');
    requests++;
    const navigation = await fetch(base + '/mobile/merchant');
    assert.equal(navigation.status, 200); assert.equal(await navigation.text(), '<html>mobile test fallback</html>');
    requests++;
    assert.equal((await fetch(base + '/mobile/missing.js')).status, 404);
    requests++;
    const apk = await fetch(base + '/downloads/toushir-merchant.apk');
    assert.equal(apk.status, 200); assert.equal(apk.headers.get('content-type'), 'application/vnd.android.package-archive');
    assert.equal(await apk.text(), 'isolated test artifact');
    requests++;
    const head = await fetch(base + '/downloads/toushir-merchant.apk', { method: 'HEAD' });
    assert.equal(head.status, 200); assert.equal(Number(head.headers.get('content-length')), 22); assert.equal(await head.text(), '');
    requests++;
    const range = await fetch(base + '/downloads/toushir-merchant.apk', { headers: { Range: 'bytes=0-3' } });
    assert.equal(range.status, 206); assert.equal(await range.text(), 'isol');
    const distribution = (await call('/distribution', { headers: { 'X-Forwarded-Proto': 'https' } })).data;
    assert.equal(distribution.merchant.available, true); assert.equal(distribution.merchant.apiOrigin, `https://127.0.0.1:${port}`);
    assert.equal(distribution.driver.available, false);
  });
  await test('Static assets preserve commerce quota and exhausted API requests return structured retry responses', async () => {
    const headers = { 'X-Forwarded-Proto': 'https', 'X-Forwarded-For': '203.0.113.78' };
    async function batch(count, action) {
      for (let start = 0; start < count; start += 20)
        await Promise.all(Array.from({ length: Math.min(20, count - start) }, (_, index) => action(start + index)));
    }
    await batch(650, async index => {
      requests++;
      const response = await fetch(base + '/downloads/missing-qa-' + index + '.apk', { headers });
      assert.equal(response.status, 404, 'Missing downloads must neither consume API quota nor serve SPA HTML.');
      await response.arrayBuffer();
    });
    await call('/health', { headers });
    await batch(599, async index => {
      await call('/rate-limit-test-' + index, { headers, status: 404 });
    });
    requests++;
    const rejected = await fetch(base + '/api/health', { headers });
    assert.equal(rejected.status, 429);
    assert(Number(rejected.headers.get('retry-after')) > 0);
    assert.equal((await rejected.json()).error.code, 'RATE_LIMIT');
  });
  await test('Database, legacy address snapshots, hashed sessions and orders survive server restart', async () => {
    await stop();
    await python("import sqlite3,json,sys; c=sqlite3.connect(sys.argv[1]); row=c.execute('select AddressJson from Orders where Id=?',(sys.argv[2],)).fetchone(); a=json.loads(row[0]); legacy={k[:1].upper()+k[1:]:v for k,v in a.items()}; c.execute('update Orders set AddressJson=? where Id=?',(json.dumps(legacy),sys.argv[2])); c.commit()", [resolvePath(data, 'toushir-wholesale.db'), order.id]);
    await launch();
    assert.equal((await call('/auth/me', { token: merchant.token })).data.user.id, merchant.user.id);
    const detail = (await call('/orders/' + order.id, { token: merchant.token })).data;
    assert.equal(detail.id, order.id); assert.equal(detail.total, null);
    assert.equal(detail.address.street, 'Adresse fictive de test'); assert.equal(detail.address.commune, 'Oran');
    assert((await call('/orders/' + order.id + '/document', { token: merchant.token })).data.includes('Adresse fictive de test'));
    assert.equal((await call('/orders', { token: owner.data.token })).data.total, 1);
  });
  await test('Retired catalog references disappear while existing order snapshots survive', async () => {
    await writeFile(fixture, JSON.stringify(seed.filter(x => x.id !== order.items[0].productId)));
    await stop(); await launch();
    assert.equal((await call('/health')).data.products, 1);
    const catalog = (await call('/products', { token: merchant.token })).data;
    assert.equal(catalog.total, 1);
    assert(!catalog.items.some(x => x.id === order.items[0].productId));
    assert.equal((await call('/orders/' + order.id, { token: merchant.token })).data.items[0].name, order.items[0].name);
  });
  await test('Password changes verify old secret and revoke all sessions', async () => {
    await call('/auth/change-password', { method: 'POST', token: merchant.token, body: { currentPassword: 'incorrect', newPassword }, status: 401 });
    await call('/auth/change-password', { method: 'POST', token: merchant.token, body: { currentPassword: merchantPassword, newPassword } });
    await call('/auth/me', { token: merchant.token, status: 401 });
    await call('/auth/login', { method: 'POST', body: { email: merchant.user.email, password: merchantPassword }, status: 401 });
    const fresh = (await call('/auth/login', { method: 'POST', body: { email: merchant.user.email, password: newPassword } })).data;
    assert.equal(fresh.user.id, merchant.user.id);
    merchant = fresh;
  });
  await test('Online SQLite backup preserves account, order and actual payment records consistently', async () => {
    const product = (await call('/products?pageSize=1', { token: merchant.token })).data.items[0];
    const address = (await call('/addresses', { token: merchant.token })).data[0];
    paidOrder = (await call('/orders', { method: 'POST', token: merchant.token, body: { idempotencyKey: crypto.randomUUID(), addressId: address.id, items: [{ productId: product.id, quantity: 2 }], mode: 'REQUEST_QUOTE' } })).data;
    paidOrder = (await call('/admin/orders/' + paidOrder.id + '/quote', { method: 'POST', token: owner.data.token, body: { version: paidOrder.version, campaignIds: [], items: [{ productId: product.id, quantity: 2, unitPrice: 1250, availableStock: 10, caseSize: 1 }], deliveryFee: 200, validUntil: new Date(Date.now() + 86400000).toISOString() } })).data;
    paidOrder = (await call('/orders/' + paidOrder.id + '/accept-quote', { method: 'POST', token: merchant.token, body: { version: paidOrder.quoteVersion } })).data;
    paidOrder = (await call('/admin/orders/' + paidOrder.id + '/payments', { method: 'POST', token: owner.data.token, body: { amount: 2700, method: 'CASH', reference: 'RESTORE-QA-RECEIPT', idempotencyKey: crypto.randomUUID() } })).data;
    assert.equal(paidOrder.balance.remaining, 0); assert.equal(paidOrder.payments.length, 1);
    originalPublicKeys = await publicKeys(base);
    await new Promise((resolve, reject) => {
      const process = spawn(env.PYTHON_BIN || 'python3', [resolvePath(backend, 'scripts/backup.py'), '--database', resolvePath(data, 'toushir-wholesale.db'), '--destination', backup], { stdio: ['ignore', 'pipe', 'pipe'] });
      let error = ''; process.stderr.on('data', x => error += x); process.on('error', reject);
      process.on('exit', code => code === 0 ? resolve() : reject(Error(error)));
    });
    await python("import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); assert d.execute('pragma integrity_check').fetchone()[0]=='ok'; assert d.execute('select count(*) from Orders').fetchone()[0]==2; assert d.execute('select count(*) from Payments').fetchone()[0]==1; assert all(len(r[0])==64 for r in d.execute('select TokenHash from Sessions'))", [backup]);
  });
  await test('Restored backup and identity keys serve authenticated account, historical order and payment APIs on a separate port', async () => {
    await stop();
    restoreData = await mkdtemp(resolvePath(backend, '.test-data/restore-'));
    await cp(backup, resolvePath(restoreData, 'toushir-wholesale.db'));
    await cp(resolvePath(data, 'keys'), resolvePath(restoreData, 'keys'), { recursive: true });
    const restoredFixture = resolvePath(restoreData, 'catalog.json');
    await cp(fixture, restoredFixture);
    for (const name of await readdir(resolvePath(data, 'keys'))) {
      assert.equal(Buffer.compare(await readFile(resolvePath(data, 'keys', name)), await readFile(resolvePath(restoreData, 'keys', name))), 0, 'Restored identity key material must match; private bytes are never reported.');
    }
    await launch({ ...env, ASPNETCORE_URLS: restoreBase, DataPath: restoreData, Catalog__Path: restoredFixture, Bootstrap__Email: '', Bootstrap__Password: '' }, restoreBase);
    assert.equal((await call('/health', { origin: restoreBase })).data.demoEnabled, false);
    const restoredOwner = (await call('/auth/login', { origin: restoreBase, method: 'POST', body: { email: owner.data.user.email, password: ownerPassword } })).data;
    const restoredMerchant = (await call('/auth/login', { origin: restoreBase, method: 'POST', body: { email: merchant.user.email, password: newPassword } })).data;
    assert.equal(restoredOwner.user.id, owner.data.user.id); assert.equal(restoredMerchant.user.id, merchant.user.id);
    assert.equal(restoredMerchant.user.approvalStatus, 'APPROVED'); assert.equal(restoredMerchant.workspace.id, merchant.workspace.id);
    assert.equal((await call('/auth/me', { origin: restoreBase, token: merchant.token })).data.user.id, merchant.user.id);
    await call('/auth/login', { origin: restoreBase, method: 'POST', body: { email: merchant.user.email, password: merchantPassword }, status: 401 });
    const historical = (await call('/orders/' + order.id, { origin: restoreBase, token: restoredMerchant.token })).data;
    assert.equal(historical.items[0].name, order.items[0].name); assert.equal(historical.address.street, 'Adresse fictive de test');
    const restoredOrder = (await call('/orders/' + paidOrder.id, { origin: restoreBase, token: restoredMerchant.token })).data;
    assert.equal(restoredOrder.total, 2700); assert.equal(restoredOrder.balance.collected, 2700); assert.equal(restoredOrder.balance.remaining, 0);
    assert.equal(restoredOrder.items[0].unitPrice, 1250); assert.equal(restoredOrder.payments.length, 1);
    assert.equal(restoredOrder.payments[0].id, paidOrder.payments[0].id); assert.equal(restoredOrder.payments[0].reference, 'RESTORE-QA-RECEIPT');
    assert.deepEqual(await publicKeys(restoreBase), originalPublicKeys);
    const reconciled = (await call('/admin/payments/' + restoredOrder.payments[0].id + '/reconcile', { origin: restoreBase, token: restoredOwner.token, method: 'POST', body: {} })).data;
    assert.equal(reconciled.payments[0].reconciled, true);
    await python("import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); assert d.execute('select Reconciled from Payments').fetchone()[0]==0", [backup]);
  });
} catch {} finally {
  await stop();
  await rm(data, { recursive: true, force: true });
  if (restoreData) await rm(restoreData, { recursive: true, force: true });
  const report = { at: new Date().toISOString(), passed: checks.filter(x => x.pass).length, failed: checks.filter(x => !x.pass).length, requests, checks };
  await writeFile(resolvePath(backend, 'production-verification.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, failed: report.failed, requests }));
  if (report.failed || checks.length !== 10) process.exitCode = 1;
}
