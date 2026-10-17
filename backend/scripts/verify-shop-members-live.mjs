import assert from "node:assert/strict";
import { readFile, writeFile } from "node:fs/promises";

assert(
  process.env.MEMBERSHIP_FIXTURE,
  "Run the API suite with a private MEMBERSHIP_FIXTURE path first.",
);
const fixture = JSON.parse(
  await readFile(process.env.MEMBERSHIP_FIXTURE, "utf8"),
);
const {
  base,
  owner,
  buyer,
  viewer,
  foreign,
  admin,
  driver,
  order,
  member,
  viewerMember,
  receipt,
  receiptItems,
  productId,
  threadId,
} = fixture;
const checks = [],
  sockets = [];
let requests = 0,
  success = false;
async function request(
  path,
  { token, body, method, form, cookie, status = 200 } = {},
  retry = 0,
) {
  requests++;
  const response = await fetch(base + path, {
    method: method || (body || form ? "POST" : "GET"),
    redirect: "manual",
    headers: {
      ...(token ? { Authorization: "Bearer " + token } : {}),
      ...(body ? { "Content-Type": "application/json" } : {}),
      ...(form ? { "Content-Type": "application/x-www-form-urlencoded" } : {}),
      ...(cookie ? { Cookie: cookie } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
    ...(form ? { body: new URLSearchParams(form) } : {}),
  });
  const text = await response.text();
  if (response.status === 429 && retry < 2) {
    const seconds = Math.min(
      60,
      Math.max(1, Number(response.headers.get("Retry-After") || 60)),
    );
    console.log("Respecting authentication rate limit before continuing.");
    await new Promise((resolve) => setTimeout(resolve, seconds * 1000));
    return request(
      path,
      { token, body, method, form, cookie, status },
      retry + 1,
    );
  }
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    data = text;
  }
  assert.equal(
    response.status,
    status,
    path + ": " + (data?.error?.code || data?.error || response.status),
  );
  return { response, data };
}
const api = async (path, token, body, status = 200, method) =>
  (await request("/api" + path, { token, body, status, method })).data;
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
async function until(check, timeout = 6000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    if (check()) return;
    await wait(30);
  }
  throw Error("Expected live event did not arrive");
}
function pass(name) {
  checks.push(name);
  console.log("PASS " + name);
}
async function connect(token) {
  const { data: negotiated } = await request(
    "/hubs/commerce/negotiate?negotiateVersion=1",
    { token, method: "POST" },
  );
  const socket = new WebSocket(
    base.replace(/^http/, "ws") +
      "/hubs/commerce?id=" +
      encodeURIComponent(negotiated.connectionToken) +
      "&access_token=" +
      encodeURIComponent(token),
  );
  const messages = [];
  sockets.push(socket);
  socket.addEventListener("message", (event) => {
    for (const part of String(event.data).split("\x1e").filter(Boolean))
      try {
        messages.push(JSON.parse(part));
      } catch {}
  });
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });
  socket.send(JSON.stringify({ protocol: "json", version: 1 }) + "\x1e");
  await until(() => messages.some((x) => Object.keys(x).length === 0));
  return {
    socket,
    messages,
    invoke: async (target, args = []) => {
      const id = crypto.randomUUID();
      socket.send(
        JSON.stringify({ type: 1, invocationId: id, target, arguments: args }) +
          "\x1e",
      );
      await until(() =>
        messages.some((x) => x.type === 3 && x.invocationId === id),
      );
      return messages.find((x) => x.type === 3 && x.invocationId === id);
    },
  };
}
async function oidc(token) {
  const verifier = Buffer.from(
    crypto.getRandomValues(new Uint8Array(48)),
  ).toString("base64url");
  const challenge = Buffer.from(
    await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier)),
  ).toString("base64url");
  const redirect = "toushircommerce://auth/callback",
    client = "toushir-mobile";
  const authorization =
    "/connect/authorize?" +
    new URLSearchParams({
      client_id: client,
      redirect_uri: redirect,
      response_type: "code",
      scope: "openid profile commerce_api offline_access",
      code_challenge: challenge,
      code_challenge_method: "S256",
      state: "member-revocation",
      nonce: "member-revocation",
    });
  const result = await request(authorization, {
    cookie: "toushir_wholesale=" + token,
    status: 302,
  });
  const callback = new URL(result.response.headers.get("location"));
  assert(callback.searchParams.get("code"));
  const tokens = (
    await request("/connect/token", {
      form: {
        grant_type: "authorization_code",
        client_id: client,
        redirect_uri: redirect,
        code: callback.searchParams.get("code"),
        code_verifier: verifier,
      },
    })
  ).data;
  return tokens;
}
try {
  const tokens = await oidc(buyer.token),
    me = await api("/auth/me", tokens.access_token);
  const subject = JSON.parse(
    Buffer.from(tokens.access_token.split(".")[1], "base64url"),
  ).sub;
  assert.equal(subject, buyer.user.id);
  assert.equal(me.user.id, buyer.user.id);
  assert.equal(me.user.shop.id, owner.user.id);
  assert.equal(
    (await api("/orders/" + order.id, tokens.access_token)).id,
    order.id,
  );
  pass(
    "PKCE signed tokens keep the actual member subject and validated shared-shop access",
  );
  if (fixture.buyerPassword) {
    const signedIn = await api("/auth/login", null, {
      email: buyer.user.email,
      password: fixture.buyerPassword,
    });
    assert.equal(signedIn.user.id, buyer.user.id);
    assert.equal(signedIn.user.shop.id, owner.user.id);
    pass("The invited person signs in with their own hashed-password account");
  }
  const memberLive = await connect(tokens.access_token),
    ownerLive = await connect(owner.token),
    viewerLive = await connect(viewer.token),
    foreignLive = await connect(foreign.token);
  assert(!(await memberLive.invoke("SubscribeOrder", [order.id])).error);
  assert((await foreignLive.invoke("SubscribeOrder", [order.id])).error);
  await api("/conversations/" + threadId + "/messages", owner.token, {
    text: "EXEMPLE · vérification de présence avant révocation",
    internal: false,
    idempotencyKey: crypto.randomUUID(),
  });
  await until(() =>
    memberLive.messages.some(
      (x) =>
        x.target === "engagement.updated" &&
        x.arguments?.[0]?.aggregateId === threadId,
    ),
  );
  pass(
    "A real member socket receives its shop notices while foreign order subscription is denied",
  );
  await api("/shop/team/members/" + member.id + "/revoke", owner.token, {
    version: member.version,
    reason: "EXEMPLE · fin de mission du membre connecté",
  });
  await api("/auth/me", buyer.token, undefined, 401);
  await api("/auth/me", tokens.access_token, undefined, 401);
  await request("/connect/token", {
    form: {
      grant_type: "refresh_token",
      client_id: "toushir-mobile",
      refresh_token: tokens.refresh_token,
    },
    status: 400,
  });
  if (fixture.buyerPassword)
    await api(
      "/auth/login",
      null,
      { email: buyer.user.email, password: fixture.buyerPassword },
      401,
    );
  pass(
    "Revocation rejects existing opaque sessions, signed access tokens, refresh grants and new password login",
  );
  assert((await memberLive.invoke("SubscribeOrder", [order.id])).error);
  await api(
    "/courier/orders/" + order.id + "/complete",
    driver.token,
    { challenge: receipt.challenge, items: receiptItems },
    409,
  );
  pass(
    "A receipt code issued before revocation cannot complete the physical delivery",
  );
  // Drain any notices authorized before revocation. Only newly created IDs are checked below.
  await wait(2400);
  const revokedCount = memberLive.messages.length,
    foreignCount = foreignLive.messages.length;
  const updated = await api("/orders/" + order.id + "/tracking", owner.token);
  await api("/courier/location", driver.token, {
    latitude: 35.7,
    longitude: -0.6,
    accuracy: 12,
    consent: true,
    capturedAt: new Date().toISOString(),
  });
  await until(() =>
    ownerLive.messages.some(
      (x) =>
        x.target === "tracking.updated" &&
        x.arguments?.[0]?.orderId === order.id,
    ),
  );
  assert.equal(
    (await api("/orders/" + order.id + "/tracking", owner.token)).location
      .source,
    "DEVICE_GPS",
  );
  await api(
    "/orders/" + order.id + "/tracking",
    tokens.access_token,
    undefined,
    401,
  );
  const fresh = await api(
    "/orders/" + order.id + "/receipt-challenge",
    owner.token,
    { version: updated.version, items: receiptItems },
  );
  const before = ownerLive.messages.length;
  const completed = await api(
    "/courier/orders/" + order.id + "/complete",
    driver.token,
    {
      challenge: fresh.challenge,
      items: receiptItems,
      note: "EXEMPLE · nouvelle confirmation du propriétaire",
    },
  );
  assert.equal(completed.order.status, "DELIVERED");
  assert.equal(completed.order.balance.collected, 0);
  await until(() =>
    ownerLive.messages
      .slice(before)
      .some(
        (x) =>
          x.target === "order.updated" &&
          x.arguments?.[0]?.orderId === order.id,
      ),
  );
  await api("/conversations/" + threadId + "/messages", owner.token, {
    text: "EXEMPLE · notice créée après révocation",
    internal: false,
    idempotencyKey: crypto.randomUUID(),
  });
  await wait(2700);
  const appEvents = (messages) =>
    messages.filter((x) =>
      [
        "order.updated",
        "tracking.updated",
        "operations.updated",
        "engagement.updated",
      ].includes(x.target),
    );
  assert.equal(appEvents(memberLive.messages.slice(revokedCount)).length, 0);
  assert.equal(appEvents(foreignLive.messages.slice(foreignCount)).length, 0);
  pass(
    "An already connected revoked socket receives no new order, tracking or outbox metadata; the owner still receives updates",
  );
  const delivered = await api("/orders/" + order.id, owner.token);
  assert.equal(delivered.status, "DELIVERED");
  assert(delivered.timeline.some((x) => x.actorId === buyer.user.id));
  await api("/shop/team/members/" + viewerMember.id + "/revoke", owner.token, {
    version: viewerMember.version,
    reason: "EXEMPLE · clôture de l’accès lecture seule",
  });
  assert.equal(
    (await api("/orders/" + order.id, owner.token)).total,
    delivered.total,
  );
  assert.equal(
    (await api("/orders/" + order.id + "/tracking", owner.token)).receipt.id,
    completed.receipt.id,
  );
  pass(
    "Revoking another member preserves accepted order history and the consumed receipt",
  );
  await api("/auth/me", viewer.token, undefined, 401);
  assert((await viewerLive.invoke("SubscribeOrder", [order.id])).error);
  pass(
    "Viewer revocation applies to existing HTTP and connected hub access without a standalone-shop fallback",
  );
  success = true;
} finally {
  for (const socket of sockets) socket.close();
  const report = {
    at: new Date().toISOString(),
    base,
    scope:
      "Actual PKCE, HTTP sessions, live WebSockets, outbox metadata, issued receipt revocation and immutable consumed delivery evidence.",
    passed: checks.length,
    failed: success ? 0 : 1,
    requests,
    checks,
  };
  await writeFile(
    process.env.MEMBERSHIP_LIVE_REPORT ||
      new URL("../shop-members-live-verification.json", import.meta.url),
    JSON.stringify(report, null, 2),
  );
  console.log(JSON.stringify({ passed: checks.length, requests }));
}
