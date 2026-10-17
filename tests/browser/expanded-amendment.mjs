import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright";
const base = process.env.WEB_URL || "http://127.0.0.1:5865",
  dir = process.env.REPORT_DIR || ".impeccable/review/expanded";
const fixture = JSON.parse(
  await fs.readFile(process.env.PRIVATE_FIXTURE, "utf8"),
);
assert.equal(fixture.base, base);
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const ctx = await browser.newContext({
  viewport: { width: 1600, height: 1040 },
  reducedMotion: "reduce",
});
await ctx.addInitScript(
  ({ a, m, cap }) => {
    localStorage.setItem("toushir-demo-workspace", cap);
    sessionStorage.setItem(
      location.pathname.startsWith("/mobile/merchant")
        ? "toushir-token"
        : "toushir-web-token",
      location.pathname.startsWith("/mobile/merchant") ? m : a,
    );
  },
  {
    a: fixture.admin.token,
    m: fixture.merchant.token,
    cap: fixture.admin.workspaceToken,
  },
);
const w = await ctx.newPage(),
  m = await ctx.newPage();
await m.setViewportSize({ width: 402, height: 874 });
for (const p of [w, m]) p.setDefaultTimeout(20000);
const checks = [],
  errors = [];
for (const p of [w, m]) p.on("pageerror", (e) => errors.push(e.message));
async function api(url, token, body, method) {
  const r = await ctx.request.fetch(base + "/api" + url, {
    method: method || (body ? "POST" : "GET"),
    headers: { Authorization: "Bearer " + token },
    ...(body ? { data: body } : {}),
  });
  const j = await r.json();
  assert(r.ok(), url + ": " + (j.error?.message || r.status()));
  return j;
}
async function shot(p, name, fromTop = true) {
  if (fromTop)
    await p.evaluate(() => {
      window.scrollTo(0, 0);
      for (const e of document.querySelectorAll("*"))
        if (e.scrollTop > 0) e.scrollTop = 0;
    });
  await p.screenshot({
    path: path.join(dir, name + ".png"),
    fullPage: fromTop,
    animations: "disabled",
  });
}
try {
  const products = await api("/products?pageSize=10", fixture.merchant.token);
  const product =
    products.items.find((p) => p.price > 0 && p.availableStock > 10) ||
    products.items.find((p) => p.price > 0);
  assert(product);
  const items = [
      { productId: product.id, quantity: product.minimumQuantity || 1 },
    ],
    basket = await api("/cart", fixture.merchant.token, { items }, "PUT"),
    address = (await api("/addresses", fixture.merchant.token))[0];
  const order = await api("/orders", fixture.merchant.token, {
    idempotencyKey: crypto.randomUUID(),
    addressId: address.id,
    items,
    mode: "PLACE_ORDER",
    pricingToken: basket.pricingToken,
    note: "EXEMPLE · test navigateur de la proposition avec accord",
  });
  await w.goto(base + "/?audit=narrow");
  await w
    .getByRole("heading", { name: "Votre activité, en clair.", exact: true })
    .waitFor({ timeout: 60000 });
  await w.waitForFunction(
    () =>
      document
        .querySelector(".workspace-actions")
        ?.getAttribute("aria-busy") === "false",
  );
  await w.setViewportSize({ width: 390, height: 844 });
  await shot(w, "wholesale-mobile");
  assert(
    await w.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  );
  await w.setViewportSize({ width: 1600, height: 1040 });
  await w.goto(base + "/?audit=amendment#orders");
  await w.getByText(order.number, { exact: true }).first().click();
  await w
    .getByRole("button", {
      name: "Proposer des produits ou quantités",
      exact: true,
    })
    .click();
  await w
    .getByRole("spinbutton", {
      name: "Quantité proposée pour " + product.name,
      exact: true,
    })
    .fill(String((product.minimumQuantity || 1) + 1));
  await w
    .getByLabel("Motif présenté au magasin", { exact: true })
    .fill(
      "EXEMPLE · le magasin peut accepter ou refuser cette quantité proposée.",
    );
  await shot(w, "desktop-amendment-form");
  await w
    .getByRole("button", {
      name: "Envoyer la proposition au magasin",
      exact: true,
    })
    .scrollIntoViewIfNeeded();
  await shot(w, "desktop-amendment-controls", false);
  let waiting = w.waitForResponse(
    (r) => r.url().endsWith("/amendments") && r.request().method() === "POST",
  );
  await w
    .getByRole("button", {
      name: "Envoyer la proposition au magasin",
      exact: true,
    })
    .click();
  assert((await waiting).ok());
  checks.push(
    "Wholesaler sends a reasoned quantity proposal through the actual form",
  );
  await m.goto(base + "/mobile/merchant#orders");
  await m
    .getByRole("button", { name: "Ouvrir " + order.number, exact: true })
    .click();
  await m
    .getByRole("button", {
      name: "Comparer les produits et les prix",
      exact: true,
    })
    .click();
  const accept = m.getByRole("button", { name: /^Accepter · / });
  assert(await accept.isDisabled());
  await shot(m, "merchant-amendment-before-consent");
  await m
    .getByText("Commande lors de la proposition", { exact: true })
    .evaluate((el) => el.scrollIntoView({ block: "start" }));
  await shot(m, "merchant-amendment-terms", false);
  await m
    .getByRole("checkbox", {
      name: "J’ai vérifié les produits, quantités, prix et nouveau total",
      exact: true,
    })
    .click();
  assert(await accept.isEnabled());
  await shot(m, "merchant-amendment-consent");
  await accept.scrollIntoViewIfNeeded();
  await shot(m, "merchant-amendment-consent-controls", false);
  waiting = m.waitForResponse(
    (r) =>
      r.url().includes("/amendments/") &&
      r.url().endsWith("/accept") &&
      r.request().method() === "POST",
  );
  await accept.click();
  assert((await waiting).ok());
  const changed = await api("/orders/" + order.id, fixture.admin.token);
  assert.equal(changed.items[0].quantity, (product.minimumQuantity || 1) + 1);
  await m.getByText("Modification acceptée", { exact: true }).waitFor();
  await shot(m, "merchant-amendment-accepted");
  checks.push(
    "Merchant compares exact before/after terms, must acknowledge, then atomically accepts persisted new quantity",
  );
  console.log(JSON.stringify({ passed: checks.length, errors }));
} catch (e) {
  checks.push("FAIL: " + e.message);
  await shot(w, "desktop-amendment-failure");
  await shot(m, "merchant-amendment-failure");
  console.log(e.message);
  process.exitCode = 1;
} finally {
  await fs.writeFile(
    path.join(dir, "expanded-amendment.json"),
    JSON.stringify(
      { at: new Date().toISOString(), base, checks, errors },
      null,
      2,
    ),
  );
  if (errors.length) process.exitCode = 1;
  await browser.close();
}
