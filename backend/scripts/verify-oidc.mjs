import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';

const base = (process.env.API_URL || 'http://127.0.0.1:5882').replace(/\/$/, '');
const redirect = base + '/auth/callback';
const checks = []; let requests = 0;
async function request(path, { method = 'GET', body, token, cookie, status, form } = {}) {
  requests++;
  const response = await fetch(base + path, { method, redirect: 'manual', headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(form ? { 'Content-Type': 'application/x-www-form-urlencoded' } : {}), ...(token ? { Authorization: 'Bearer ' + token } : {}), ...(cookie ? { Cookie: cookie } : {}) }, ...(body !== undefined || form ? { body: form ? new URLSearchParams(form) : JSON.stringify(body) } : {}) });
  const text = await response.text(); let value; try { value = JSON.parse(text); } catch { value = text; }
  if (status !== undefined) assert.equal(response.status, status, `${method} ${path.split('?')[0]} returned ${response.status}; protocol error ${value?.error || value?.error?.code || ''}`);
  return { response, value };
}
async function test(name, action) { try { await action(); checks.push({ name, pass: true }); console.log('PASS ' + name); } catch (error) { checks.push({ name, pass: false, error: error.message }); console.error('FAIL ' + name + ': ' + error.message); throw error; } }
const b64 = bytes => Buffer.from(bytes).toString('base64url');
const cookies = response => response.headers.getSetCookie().map(value => value.split(';')[0]).join('; ');
let discovery, demo, admin, account, loginCookie, tokens, refreshed;
const password = 'Oidc-Test-' + crypto.randomUUID();
const verifier = b64(crypto.getRandomValues(new Uint8Array(48)));
const challenge = b64(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier)));
function authorization(extra = {}) { return '/connect/authorize?' + new URLSearchParams({ client_id: 'toushir-web', redirect_uri: redirect, response_type: 'code', scope: 'openid profile email commerce_api offline_access', code_challenge: challenge, code_challenge_method: 'S256', state: 'test-state', nonce: 'test-nonce', ...extra }); }
async function code() { const result = await request(authorization(), { cookie: loginCookie, status: 302 }); const destination = new URL(result.response.headers.get('location')); assert.equal(destination.origin, base); assert.equal(destination.searchParams.get('state'), 'test-state'); assert(destination.searchParams.get('code')); return destination.searchParams.get('code'); }
async function exchange(form, status = 200) { return (await request('/connect/token', { method: 'POST', form, status })).value; }
try {
  await test('Standards discovery publishes authorization code, refresh, S256 and signed public keys', async () => {
    discovery = (await request('/.well-known/openid-configuration', { status: 200 })).value;
    assert(discovery.authorization_endpoint.endsWith('/connect/authorize')); assert(discovery.token_endpoint.endsWith('/connect/token'));
    assert(discovery.grant_types_supported.includes('authorization_code')); assert(discovery.code_challenge_methods_supported.includes('S256')); assert(!discovery.code_challenge_methods_supported.includes('plain'));
    const keys = (await request(new URL(discovery.jwks_uri).pathname, { status: 200 })).value; assert(keys.keys.length > 0); assert(keys.keys.every(key => !key.d));
  });
  await test('Exact client redirects and mandatory PKCE reject malicious or downgraded authorization', async () => {
    const evil = await request(authorization({ redirect_uri: 'https://attacker.invalid/callback' })); assert.equal(evil.response.status, 400); assert(!evil.response.headers.get('location'));
    for (const changed of [{ code_challenge: '', code_challenge_method: '' }, { code_challenge_method: 'plain' }]) {
      const result = await request(authorization(changed)); assert([302, 400].includes(result.response.status));
      if (result.response.status === 302) assert(new URL(result.response.headers.get('location')).searchParams.has('error'));
    }
    await exchange({ grant_type: 'password', client_id: 'toushir-web', username: 'x', password: 'x' }, 400);
  });
  await test('Server sign-in form validates antiforgery and authenticates an existing hashed-password account', async () => {
    demo = (await request('/api/demo/session', { method: 'POST', body: { role: 'MERCHANT' }, status: 200 })).value;
    admin = (await request('/api/demo/session', { method: 'POST', body: { role: 'ADMIN', workspaceToken: demo.workspaceToken }, status: 200 })).value;
    account = (await request('/api/operations/team', { method: 'POST', token: admin.token, body: { name: 'OIDC Test Operator', email: 'oidc-' + crypto.randomUUID() + '@example.invalid', password, role: 'WAREHOUSE', phone: 'TEST' }, status: 200 })).value;
    const authorize = await request(authorization(), { status: 302 }); assert(authorize.response.headers.get('location').startsWith('/auth/login?'));
    const page = await request(authorize.response.headers.get('location'), { status: 200 });
    const csrf = page.value.match(/name='__RequestVerificationToken' value='([^']+)'/)?.[1]; assert(csrf); assert(page.response.headers.get('content-security-policy').includes("frame-ancestors 'none'"));
    await request('/auth/login', { method: 'POST', form: { email: account.email, password, returnUrl: authorization() }, status: 400 });
    const login = await request('/auth/login', { method: 'POST', cookie: cookies(page.response), form: { __RequestVerificationToken: csrf, email: account.email, password, returnUrl: authorization() }, status: 200 });
    assert(login.value.includes("http-equiv='refresh'")); assert(login.value.includes("url=/connect/authorize?"));
    loginCookie = cookies(login.response); assert(loginCookie.includes('toushir_wholesale='));
  });
  await test('Code exchange enforces verifier, issues signed OIDC tokens and rejects authorization-code replay', async () => {
    const wrong = await code(); await exchange({ grant_type: 'authorization_code', client_id: 'toushir-web', redirect_uri: redirect, code: wrong, code_verifier: 'x'.repeat(64) }, 400);
    const authorizationCode = await code();
    tokens = await exchange({ grant_type: 'authorization_code', client_id: 'toushir-web', redirect_uri: redirect, code: authorizationCode, code_verifier: verifier });
    assert(tokens.access_token); assert(tokens.id_token); assert(tokens.refresh_token); assert.equal(tokens.token_type, 'Bearer');
    const identity = JSON.parse(Buffer.from(tokens.id_token.split('.')[1], 'base64url')); assert.equal(identity.sub, account.id); assert.equal(identity.aud, 'toushir-web'); assert.equal(identity.nonce, 'test-nonce');
    await exchange({ grant_type: 'authorization_code', client_id: 'toushir-web', redirect_uri: redirect, code: authorizationCode, code_verifier: verifier }, 400);
    // OpenIddict correctly revokes the authorization family on code replay; use a new grant for subsequent API checks.
    tokens = await exchange({ grant_type: 'authorization_code', client_id: 'toushir-web', redirect_uri: redirect, code: await code(), code_verifier: verifier });
  });
  await test('JWT adapter preserves server role/workspace boundaries and rejects forged or wrong-audience tokens', async () => {
    const me = (await request('/api/auth/me', { token: tokens.access_token, status: 200 })).value; assert.equal(me.user.id, account.id); assert.equal(me.user.role, 'WAREHOUSE'); assert.equal(me.workspace.id, demo.workspace.id);
    await request('/api/operations/orders', { token: tokens.access_token, status: 200 }); await request('/api/admin/dashboard', { token: tokens.access_token, status: 403 });
    const parts = tokens.access_token.split('.'); const payload = JSON.parse(Buffer.from(parts[1], 'base64url')); payload.role = 'ADMIN'; parts[1] = b64(JSON.stringify(payload));
    await request('/api/auth/me', { token: parts.join('.'), status: 401 }); await request('/api/auth/me', { token: tokens.id_token, status: 401 });
    const info = (await request('/connect/userinfo', { token: tokens.access_token, status: 200 })).value; assert.equal(info.sub, account.id); assert.equal(info.role, 'WAREHOUSE');
  });
  await test('Refresh tokens rotate; explicit revocation and account-session logout prevent further use', async () => {
    refreshed = await exchange({ grant_type: 'refresh_token', client_id: 'toushir-web', refresh_token: tokens.refresh_token }); assert(refreshed.access_token); assert(refreshed.refresh_token); assert.notEqual(refreshed.refresh_token, tokens.refresh_token);
    await request('/connect/revoke', { method: 'POST', form: { client_id: 'toushir-web', token: refreshed.refresh_token, token_type_hint: 'refresh_token' }, status: 200 });
    await exchange({ grant_type: 'refresh_token', client_id: 'toushir-web', refresh_token: refreshed.refresh_token }, 400);
    await request('/api/auth/logout', { method: 'POST', body: {}, token: refreshed.access_token, status: 200 });
    await request('/api/auth/me', { token: refreshed.access_token, status: 401 });
  });
} catch {} finally {
  const report = { base, at: new Date().toISOString(), passed: checks.filter(x => x.pass).length, failed: checks.filter(x => !x.pass).length, requests, checks };
  await writeFile(process.env.VERIFICATION_REPORT || new URL('../oidc-verification.json', import.meta.url), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, failed: report.failed, requests })); if (report.failed) process.exitCode = 1;
}
