import fs from "node:fs/promises";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { chromium } from "playwright";

const base = process.env.WEB_URL || "http://127.0.0.1:5861";
const reportDir = process.env.REPORT_DIR || "reports";
await fs.mkdir(reportDir, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(25000);
const checks = [],
  javascriptErrors = [];
page.on("pageerror", (e) => javascriptErrors.push(e.message));
let mobile;
const button = (name) => page.getByRole("button", { name, exact: true });
const ready = () =>
  page.waitForFunction(
    () =>
      document
        .querySelector(".workspace-actions")
        ?.getAttribute("aria-busy") === "false",
  );
async function submit(name, suffix, scope = page) {
  await ready();
  const pending = page.waitForResponse(
    (r) =>
      r.request().method() === "POST" &&
      new URL(r.url()).pathname.endsWith(suffix),
  );
  await scope.getByRole("button", { name, exact: true }).click();
  const response = await pending,
    data = await response.json();
  assert(response.ok(), data?.error?.message || suffix);
  await ready();
  return data;
}
async function capture(name) {
  if (await page.locator(".toast button").count())
    await page.locator(".toast button").click();
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: reportDir + "/" + name,
    fullPage: true,
    animations: "disabled",
  });
}
try {
  await page.goto(base, { waitUntil: "domcontentloaded" });
  await page
    .getByRole("heading", { name: "Votre activité, en clair." })
    .waitFor({ timeout: 60000 });
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Tarifs & conditions", exact: true })
    .click();
  await page
    .getByRole("heading", { name: "La priorité s’explique", exact: true })
    .waitFor();
  await page.getByText("Vos accords commencent ici", { exact: true }).waitFor();
  await capture("pricing-empty-desktop.png");
  checks.push(
    "Figma pricing hierarchy and useful empty state render at 1440 pixels",
  );
  const adminToken = await page.evaluate(() =>
    sessionStorage.getItem("toushir-web-token"),
  );
  const owner = await page.evaluate(() =>
    localStorage.getItem("toushir-demo-workspace"),
  );
  async function api(path, token = adminToken, body, method = "POST") {
    const response = await page.request.fetch(base + "/api" + path, {
      method: body === undefined ? "GET" : method,
      headers: { Authorization: "Bearer " + token },
      ...(body !== undefined ? { data: body } : {}),
    });
    const data = await response.json();
    assert(response.ok(), JSON.stringify(data));
    return data;
  }
  const merchant = await api("/demo/session", adminToken, {
    role: "MERCHANT",
    workspaceToken: owner,
  });
  const finance = await api("/demo/session", adminToken, {
    role: "FINANCE",
    workspaceToken: owner,
  });
  const createSegment = page.locator("form").filter({
    has: page.getByRole("heading", { name: "Créer un groupe", exact: true }),
  });
  await createSegment
    .getByLabel("Nom du segment", { exact: true })
    .fill("EXEMPLE · Épiceries QA");
  const segment = await submit("Créer le segment", "/segments", createSegment);
  const assignment = page.locator("form").filter({
    has: page.getByRole("heading", {
      name: "Affecter un magasin",
      exact: true,
    }),
  });
  await assignment
    .locator('select[name="merchantId"]')
    .selectOption(merchant.user.id);
  await assignment.locator('select[name="segmentId"]').selectOption(segment.id);
  await assignment
    .getByLabel("Motif de l’affectation", { exact: true })
    .fill("EXEMPLE · accord de proximité");
  await submit(
    "Enregistrer l’affectation",
    "/assignments/" + merchant.user.id,
    assignment,
  );
  checks.push(
    "A real segment and versioned shop assignment are saved through their forms",
  );
  await button("Proposer un tarif").click();
  const form = page.locator(".pricing-rule-form");
  await form
    .locator('select[name="productId"] option')
    .nth(1)
    .waitFor({ state: "attached" });
  const pid = await form
    .locator('select[name="productId"] option')
    .nth(1)
    .getAttribute("value");
  await form.locator('select[name="productId"]').selectOption(pid);
  await form.locator('select[name="targetId"]').selectOption(merchant.user.id);
  await form.locator('input[name="price"]').fill("130");
  await form.locator('input[name="reference"]').fill("EXEMPLE-UI-130");
  await capture("pricing-proposal-desktop.png");
  const rule = await submit("Soumettre à l’approbation", "/rules", form);
  await page
    .getByRole("region", { name: "Conditions EXEMPLE-UI-130", exact: true })
    .waitFor();
  const detail = page.getByRole("region", {
    name: "Conditions EXEMPLE-UI-130",
    exact: true,
  });
  await detail
    .getByLabel("Motif de la décision", { exact: true })
    .fill("EXEMPLE · accord vérifié par Finance");
  const published = await submit(
    "Enregistrer la décision avec Finance",
    "/review",
    detail,
  );
  await page.getByText("Actif", { exact: true }).first().waitFor();
  assert.notEqual(published.createdBy, published.reviewedBy);
  checks.push(
    "A proposed customer contract is independently published by the distinct Finance persona",
  );
  await api(
    "/admin/products/" + pid,
    adminToken,
    {
      price: 100,
      stock: 100,
      caseSize: 1,
      minimumQuantity: 1,
      offerApproved: true,
      isActive: true,
    },
    "PATCH",
  );
  const simulation = page.locator(".pricing-simulation");
  await simulation
    .locator('select[name="merchantId"]')
    .selectOption(merchant.user.id);
  await simulation
    .locator('select[name="simulationProduct"] option[value="' + pid + '"]')
    .waitFor({ state: "attached" });
  await simulation
    .locator('select[name="simulationProduct"]')
    .selectOption(pid);
  await simulation.locator('input[name="simulationQuantity"]').fill("3");
  const simulated = await submit(
    "Simuler les conditions",
    "/simulate",
    simulation,
  );
  assert.equal(simulated.quote.total, 390);
  await simulation
    .getByRole("heading", { name: "Conditions calculées", exact: true })
    .waitFor();
  await capture("pricing-active-desktop.png");
  await button("Activer le thème sombre").click();
  await capture("pricing-active-dark.png");
  await button("Activer le thème clair").click();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForFunction(
    () =>
      document.querySelector(".sidebar")?.getBoundingClientRect().right <= 1,
  );
  await capture("pricing-active-mobile.png");
  const layout = await page.evaluate(() => ({
    width: innerWidth,
    scroll: document.documentElement.scrollWidth,
    elements: [...document.querySelectorAll("body *")]
      .map((e) => ({
        tag: e.tagName,
        className: e.className,
        name: e.getAttribute("name"),
        right: Math.round(e.getBoundingClientRect().right),
        width: Math.round(e.getBoundingClientRect().width),
      }))
      .filter((e) => e.right > innerWidth + 2 && e.width > 0)
      .slice(0, 25),
  }));
  assert(
    layout.scroll <= layout.width,
    "Pricing page overflows the narrow viewport: " + JSON.stringify(layout),
  );
  checks.push(
    "Published conditions simulate the actual 390 DA basket and remain usable at 390 pixels",
  );
  await page.setViewportSize({ width: 1440, height: 1000 });

  await api(
    "/cart",
    merchant.token,
    { items: [{ productId: pid, quantity: 3 }] },
    "PUT",
  );
  mobile = await browser.newPage({ viewport: { width: 402, height: 874 } });
  mobile.setDefaultTimeout(25000);
  mobile.on("pageerror", (e) => javascriptErrors.push(e.message));
  await mobile.addInitScript(
    ({ token, owner }) => {
      sessionStorage.setItem("toushir-token", token);
      localStorage.setItem("toushir-demo-workspace", owner);
    },
    { token: merchant.token, owner },
  );
  await mobile.goto(base + "/mobile/merchant#cart", {
    waitUntil: "domcontentloaded",
  });
  const confirm = mobile.getByRole("button", {
    name: "Confirmer la commande",
    exact: true,
  });
  await confirm.waitFor({ timeout: 60000 });
  await mobile.screenshot({
    path: reportDir + "/merchant-reviewed-price.png",
    fullPage: true,
  });
  const replacement = await api("/admin/pricing/rules", adminToken, {
    productId: pid,
    scope: "MERCHANT",
    targetId: merchant.user.id,
    minimumQuantity: 1,
    maximumQuantity: null,
    unitPriceMinor: 15000,
    startsAt: new Date().toISOString(),
    endsAt: new Date(Date.now() + 86400000).toISOString(),
    allowCampaigns: false,
    reference: "EXEMPLE-UI-150",
    note: "Fictif",
    idempotencyKey: randomUUID(),
    supersedesId: rule.id,
    supersedesVersion: published.version,
  });
  await api(
    "/admin/pricing/rules/" + replacement.id + "/review",
    finance.token,
    {
      version: 1,
      decision: "PUBLISH",
      reason: "EXEMPLE · nouveau prix vérifié",
    },
  );
  const attempts = [];
  mobile.on("request", (r) => {
    if (r.method() === "POST" && new URL(r.url()).pathname === "/api/orders")
      attempts.push(r.postDataJSON());
  });
  const staleResponse = mobile.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api/orders" &&
      r.request().method() === "POST",
  );
  await confirm.click();
  assert.equal((await staleResponse).status(), 409);
  await mobile
    .getByText(
      "Récapitulatif actualisé. Vérifiez les montants et les conditions, puis confirmez à nouveau.",
      { exact: true },
    )
    .waitFor();
  await mobile.screenshot({
    path: reportDir + "/merchant-reprice-confirmation.png",
    fullPage: true,
  });
  await mobile.waitForTimeout(400);
  assert.equal(
    attempts.length,
    1,
    "Updated prices were accepted automatically",
  );
  checks.push(
    "Merchant web checkout rejects an unseen price change, updates the visible basket and requires another explicit confirmation",
  );
  let release, reached;
  const hold = new Promise((resolve) => {
      release = resolve;
    }),
    committed = new Promise((resolve) => {
      reached = resolve;
    });
  await mobile.route("**/api/orders", async (route) => {
    if (route.request().method() !== "POST") return route.continue();
    const response = await route.fetch();
    reached();
    await hold;
    await route.fulfill({ response });
  });
  const actualResponse = mobile.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api/orders" &&
      r.request().method() === "POST",
  );
  await confirm.click();
  await committed;
  try {
    assert.equal(await confirm.getAttribute("aria-disabled"), "true");
    const quantities = mobile.getByRole("button", {
      name: /^(Augmenter|Diminuer)/,
    });
    assert(
      (await quantities.count()) >= 2,
      "Basket quantity controls are missing",
    );
    for (const control of await quantities.all())
      assert.equal(await control.getAttribute("aria-disabled"), "true");
  } finally {
    release();
  }
  const final = await (await actualResponse).json();
  await mobile.unrouteAll({ behavior: "wait" });
  assert.equal(final.total, 450);
  assert.equal(attempts.length, 2);
  assert.equal(attempts[0].idempotencyKey, attempts[1].idempotencyKey);
  await mobile.getByText(final.number, { exact: true }).first().waitFor();
  checks.push(
    "Slow checkout disables basket edits, commits the reviewed 450 DA total and preserves its idempotency key across re-confirmation",
  );

  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Commandes", exact: true })
    .click();
  await page.getByText(final.number, { exact: true }).first().click();
  await page.getByText("Origine & versions des prix", { exact: true }).click();
  await page.getByText("Contrat du magasin", { exact: true }).last().waitFor();
  assert(
    (
      await api("/orders/" + final.id + "/pricing-history", adminToken)
    ).items.some((x) => x.appliedUnitPriceMinor === 15000),
  );
  checks.push(
    "The actual order exposes its frozen customer-price provenance in the wholesaler dossier",
  );
  await button("Fermer le dossier").click();
  const address = (await api("/addresses", merchant.token))[0];
  const rfq = await api("/orders", merchant.token, {
    idempotencyKey: randomUUID(),
    addressId: address.id,
    items: [{ productId: pid, quantity: 3 }],
    mode: "REQUEST_QUOTE",
  });
  await button("Actualiser").click();
  await page.getByText(rfq.number, { exact: true }).first().click();
  await page
    .getByLabel("Origine des prix", { exact: true })
    .selectOption("TARIFF");
  assert(await page.locator('input[name="price-' + pid + '"]').isDisabled());
  await page
    .getByRole("button", { name: "Consulter les tarifs actuels", exact: true })
    .click();
  await page
    .getByText("Référence actuelle · simulation", { exact: true })
    .waitFor();
  const quoted = await submit("Envoyer la proposition", "/quote");
  assert.equal(quoted.total, 450);
  assert.equal(quoted.pricing.items[0].baseUnitPriceMinor, 15000);
  await page
    .getByRole("heading", { name: "Réviser la proposition", exact: true })
    .waitFor();
  await page
    .getByRole("button", { name: "Envoyer la proposition", exact: true })
    .waitFor();
  if (await page.locator(".toast button").count())
    await page.locator(".toast button").click();
  await page.locator(".quote-pricing-controls").scrollIntoViewIfNeeded();
  await page.locator(".quote-pricing-controls").evaluate((el) => {
    const drawer = el.closest(".drawer");
    drawer.scrollTop +=
      el.getBoundingClientRect().top -
      drawer.getBoundingClientRect().top -
      drawer.querySelector("header").getBoundingClientRect().height -
      16;
  });
  await page
    .locator(".drawer")
    .screenshot({
      path: reportDir + "/pricing-order-quote.png",
      animations: "disabled",
    });
  checks.push(
    "The actual wholesaler quote form simulates and applies published customer prices without submitting disabled manual values",
  );
  await button("Fermer le dossier").click();
  const standard = await api("/admin/pricing/rules", adminToken, {
    productId: pid,
    scope: "STANDARD",
    targetId: "",
    minimumQuantity: 1,
    maximumQuantity: null,
    unitPriceMinor: 17000,
    startsAt: new Date().toISOString(),
    endsAt: new Date(Date.now() + 86400000).toISOString(),
    allowCampaigns: false,
    reference: "EXEMPLE-UI-STANDARD",
    note: "Fictif",
    idempotencyKey: randomUUID(),
  });
  await api("/admin/pricing/rules/" + standard.id + "/review", finance.token, {
    version: 1,
    decision: "PUBLISH",
    reason: "EXEMPLE · barème standard vérifié",
  });
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Catalogue", exact: true })
    .click();
  const product = await api("/products/" + pid, adminToken);
  await page
    .getByRole("textbox", { name: "Rechercher dans le catalogue", exact: true })
    .fill(product.barcode);
  const row = page
    .locator("tbody tr")
    .filter({ has: page.getByText(product.barcode, { exact: false }) });
  await row.waitFor();
  await row.click();
  assert.equal(await page.locator('input[name="price"]').inputValue(), "100");
  assert.equal(await page.locator('input[name="stock"]').inputValue(), "100");
  await page
    .getByText(/Un barème standard publié est actuellement retenu/)
    .waitFor();
  checks.push(
    "Editing the depot offer preserves its stored 100 DA base and physical stock while clearly explaining the published standard tariff",
  );
} catch (error) {
  checks.push("FAIL: " + error.message);
  process.exitCode = 1;
  await page.screenshot({
    path: reportDir + "/pricing-failure.png",
    fullPage: true,
  });
  if (mobile)
    await mobile.screenshot({
      path: reportDir + "/pricing-mobile-failure.png",
      fullPage: true,
    });
} finally {
  await fs.writeFile(
    reportDir + "/pricing-browser.json",
    JSON.stringify(
      { at: new Date().toISOString(), base, checks, javascriptErrors },
      null,
      2,
    ),
  );
  console.log(JSON.stringify({ checks, javascriptErrors }));
  if (javascriptErrors.length) process.exitCode = 1;
  await browser.close();
}
