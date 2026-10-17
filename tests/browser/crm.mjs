import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { chromium } from "playwright";
const base = process.env.WEB_URL || "http://127.0.0.1:5864",
  dir = process.env.REPORT_DIR || "reports";
await fs.mkdir(dir, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const page = await browser.newPage({ viewport: { width: 1600, height: 1040 } });
page.setDefaultTimeout(25000);
const checks = [],
  errors = [];
let success = false,
  mobile,
  salesPage;
const watch = (p) => p.on("pageerror", (e) => errors.push(e.message));
watch(page);
const button = (p, name) => p.getByRole("button", { name, exact: true });
async function capture(p, name, fullPage = true) {
  if (fullPage) {
    await p.evaluate(() => window.scrollTo(0, 0));
    await p.evaluate(
      () =>
        new Promise((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(resolve)),
        ),
    );
  }
  await p.screenshot({
    path: dir + "/" + name,
    fullPage,
    animations: "disabled",
  });
}
async function posted(p, path, action, status = 200) {
  const waiting = p.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api" + path &&
      r.request().method() === "POST",
  );
  await action();
  const response = await waiting;
  assert.equal(response.status(), status);
  return await response.json();
}
async function request(path, token, body, method) {
  const response = await page.request.fetch(base + "/api" + path, {
    method: method || (body ? "POST" : "GET"),
    headers: { Authorization: "Bearer " + token },
    ...(body ? { data: body } : {}),
  });
  const value = await response.json();
  assert(response.ok(), path + ": " + (value.error?.code || response.status()));
  return value;
}
async function nav(p) {
  await p
    .getByRole("navigation")
    .getByRole("button", { name: "Portefeuilles & visites", exact: true })
    .click();
  await p
    .getByRole("heading", {
      name: "Le suivi commercial qui fait avancer.",
      exact: true,
    })
    .waitFor();
}
try {
  await page.goto(base, { waitUntil: "domcontentloaded" });
  await page
    .getByRole("heading", { name: "Votre activité, en clair.", exact: true })
    .waitFor({ timeout: 60000 });
  await page.waitForFunction(
    () =>
      document
        .querySelector(".workspace-actions")
        ?.getAttribute("aria-busy") === "false",
  );
  const token = await page.evaluate(() =>
    sessionStorage.getItem("toushir-web-token"),
  );
  const workspace = await page.evaluate(() =>
    localStorage.getItem("toushir-demo-workspace"),
  );
  const owner = await request("/demo/session", token, {
    role: "MERCHANT",
    workspaceToken: workspace,
  });
  const admin = await request("/auth/me", token);
  const email = "qa-crm-browser-" + crypto.randomUUID() + "@example.invalid",
    password = "Browser-crm-" + crypto.randomUUID();
  const adviser = await request("/operations/team", token, {
    name: "EXEMPLE · Sara, conseillère",
    email,
    password,
    role: "SALES",
    phone: "EXEMPLE",
  });
  const signed = await request("/auth/login", null, { email, password });
  // The API login changes the HttpOnly cookie; restore the active admin's explicit bearer before UI actions.
  await nav(page);
  await page
    .getByText("Une prochaine action claire.", { exact: true })
    .waitFor();
  await capture(page, "crm-empty-desktop.png");
  checks.push(
    "The actual CRM route shows a useful empty state in the established wholesale shell",
  );
  await page
    .getByRole("combobox", { name: "Magasin du portefeuille", exact: true })
    .selectOption(owner.user.id);
  await button(page, "Affecter un conseiller").waitFor();
  await button(page, "Affecter un conseiller").click();
  await page
    .getByRole("combobox", { name: "Conseiller responsable", exact: true })
    .selectOption(adviser.id);
  await page
    .getByRole("textbox", { name: "Motif du changement", exact: true })
    .fill("EXEMPLE · accompagnement du magasin");
  await capture(page, "crm-portfolio-form-desktop.png");
  await posted(page, "/crm/shops/" + owner.user.id + "/portfolio", () =>
    button(page, "Affecter ce conseiller").click(),
  );
  await button(page, "Planifier un suivi").waitFor();
  checks.push(
    "The owner assigns an actual named adviser through the reasoned portfolio form",
  );
  await button(page, "Planifier un suivi").click();
  await page
    .getByRole("textbox", { name: "Objet du suivi", exact: true })
    .fill("EXEMPLE · confirmer le besoin du magasin");
  const task = await posted(
    page,
    "/crm/shops/" + owner.user.id + "/tasks",
    () => button(page, "Enregistrer le suivi").click(),
  );
  const taskRow = page.locator(".crm-task-row").filter({ hasText: task.title });
  await taskRow
    .getByRole("button", { name: owner.user.shopName, exact: true })
    .click();
  await button(page, "Changer de conseiller").waitFor();
  await taskRow
    .getByRole("button", { name: "Consigner le résultat", exact: true })
    .click();
  await page
    .getByRole("combobox", { name: "Issue du contact", exact: true })
    .selectOption("CALL_BACK");
  await page
    .getByRole("textbox", { name: "Compte rendu", exact: true })
    .fill("EXEMPLE · le commerçant souhaite être rappelé");
  await page
    .getByRole("textbox", { name: "Prochaine action", exact: true })
    .fill("EXEMPLE · rappeler pour le réassort");
  await capture(page, "crm-contact-form-desktop.png");
  await posted(page, "/crm/tasks/" + task.id + "/finish", () =>
    button(page, "Enregistrer le compte rendu").click(),
  );
  await page
    .getByRole("heading", { name: "À rappeler", exact: true })
    .waitFor();
  checks.push(
    "An actual task records its contact outcome and next action without creating an order",
  );
  await button(page, "Planifier une visite").click();
  await page
    .getByRole("textbox", { name: "Objet du suivi", exact: true })
    .fill("EXEMPLE · visite de suivi");
  const visit = await posted(
    page,
    "/crm/shops/" + owner.user.id + "/tasks",
    () => button(page, "Enregistrer le suivi").click(),
  );
  await page
    .locator(".crm-task-row")
    .filter({ hasText: visit.title })
    .getByRole("button", { name: "Consigner le résultat", exact: true })
    .click();
  await page
    .getByRole("textbox", { name: "Compte rendu", exact: true })
    .fill("EXEMPLE · tentative avant le créneau");
  await page
    .getByRole("textbox", { name: "Prochaine action", exact: true })
    .fill("EXEMPLE · attendre le créneau convenu");
  await posted(
    page,
    "/crm/tasks/" + visit.id + "/finish",
    () => button(page, "Enregistrer le compte rendu").click(),
    409,
  );
  const error = page
    .getByRole("alert")
    .filter({ hasText: "La visite n'a pas encore commencé." });
  await error.waitFor();
  const errorBox = await error.boundingBox();
  assert(errorBox && errorBox.y >= 0 && errorBox.y + errorBox.height <= 1040);
  await capture(page, "crm-future-visit-error-actions.png", false);
  await button(page, "Fermer le formulaire").click();
  await page.evaluate(() => window.scrollTo(0, 0));
  await capture(page, "crm-active-desktop.png");
  checks.push(
    "A real future-visit rejection stays beside the submission action with the entered report retained",
  );
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(400);
  await page.evaluate(() => window.scrollTo(0, 0));
  assert(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  );
  await capture(page, "crm-active-narrow.png");
  await page.setViewportSize({ width: 1600, height: 1040 });
  await page.waitForTimeout(400);
  await page
    .getByRole("button", { name: "Activer le thème sombre", exact: true })
    .click();
  await capture(page, "crm-active-dark.png");
  await page
    .getByRole("button", { name: "Activer le thème clair", exact: true })
    .click();
  checks.push(
    "The CRM task and dossier layout fits 390 pixels and inherits the wholesale dark theme",
  );
  const mc = await browser.newContext({
    viewport: { width: 402, height: 874 },
  });
  await mc.addInitScript(
    ({ capability, bearer }) => {
      localStorage.setItem("toushir-demo-workspace", capability);
      sessionStorage.setItem("toushir-token", bearer);
    },
    { capability: workspace, bearer: owner.token },
  );
  mobile = await mc.newPage();
  watch(mobile);
  await mobile.goto(base + "/mobile/merchant", {
    waitUntil: "domcontentloaded",
  });
  const add = mobile
    .getByRole("button", { name: /^Ajouter .+ au panier$/ })
    .first();
  await add.waitFor({ timeout: 60000 });
  const basketSaved = mobile.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api/cart" &&
      r.request().method() === "PUT",
  );
  await add.click();
  assert((await basketSaved).ok());
  const basketToast = mobile
    .getByRole("alert")
    .filter({ hasText: "Ajouté au panier" });
  if (await basketToast.count()) await basketToast.click();
  await mobile.getByRole("tab", { name: "Mon Toushir", exact: true }).click();
  await mobile.getByRole("button", { name: /^Demandes assistées/ }).click();
  await mobile
    .getByText("EXEMPLE · Sara, conseillère", { exact: true })
    .waitFor();
  await capture(mobile, "crm-merchant-assistance-phone.png");
  await button(mobile, "Vérifier les produits à autoriser").click();
  await mobile
    .getByText("Autoriser EXEMPLE · Sara, conseillère", { exact: true })
    .waitFor();
  assert(await button(mobile, "Autoriser une demande de devis").isDisabled());
  await mobile.evaluate(() => {
    document.querySelectorAll("*").forEach((e) => {
      if (e.scrollTop) e.scrollTop = 0;
    });
  });
  await capture(mobile, "crm-merchant-authorization-phone.png");
  await mobile.getByRole("checkbox").click();
  await button(
    mobile,
    "Autoriser une demande de devis",
  ).scrollIntoViewIfNeeded();
  await capture(mobile, "crm-merchant-authorization-actions.png");
  const authorized = await posted(mobile, "/shop/assistance", () =>
    button(mobile, "Autoriser une demande de devis").click(),
  );
  const grant = authorized.items[0];
  assert.equal(grant.advisorId, adviser.id);
  await mobile.getByText("À transmettre", { exact: true }).waitFor();
  const grantToast = mobile
    .getByRole("alert")
    .filter({ hasText: "Autorisation enregistrée" });
  if (await grantToast.count()) await grantToast.click();
  await mobile
    .getByText("À transmettre", { exact: true })
    .scrollIntoViewIfNeeded();
  await capture(mobile, "crm-merchant-authorized-phone.png");
  await mobile.setViewportSize({ width: 1440, height: 1000 });
  await mobile.waitForTimeout(400);
  await mobile.evaluate(() => {
    document.querySelectorAll("*").forEach((e) => {
      if (e.scrollTop) e.scrollTop = 0;
    });
  });
  await capture(mobile, "crm-merchant-authorized-wide.png");
  checks.push(
    "The real merchant reviews exact basket products and address and explicitly authorizes the named adviser once",
  );
  const sc = await browser.newContext({
    viewport: { width: 1600, height: 1040 },
  });
  await sc.addInitScript((value) => {
    sessionStorage.setItem("toushir-web-token", value);
    sessionStorage.setItem("toushir-web-personal-account", "true");
  }, signed.token);
  salesPage = await sc.newPage();
  watch(salesPage);
  let salesDemoPosts = 0;
  salesPage.on("request", (r) => {
    if (
      new URL(r.url()).pathname === "/api/demo/session" &&
      r.method() === "POST"
    )
      salesDemoPosts++;
  });
  await salesPage.goto(base + "/#crm", { waitUntil: "domcontentloaded" });
  await salesPage
    .getByRole("heading", {
      name: "Le suivi commercial qui fait avancer.",
      exact: true,
    })
    .waitFor();
  assert.equal(
    await salesPage.getByRole("navigation").getByRole("button").count(),
    1,
  );
  await salesPage
    .getByRole("combobox", { name: "Magasin du portefeuille", exact: true })
    .selectOption(owner.user.id);
  await button(salesPage, "Vérifier avant de transmettre").waitFor();
  assert.equal(await button(salesPage, "Changer de conseiller").count(), 0);
  await button(salesPage, "Vérifier avant de transmettre").click();
  await capture(salesPage, "crm-assisted-rfq-review-desktop.png");
  const rfq = await posted(
    salesPage,
    "/crm/assistance/" + grant.id + "/use",
    () => button(salesPage, "Transmettre la demande de devis").click(),
  );
  assert.equal(rfq.status, "REQUESTED");
  assert.equal(rfq.total, null);
  assert.equal(rfq.createdBy, adviser.id);
  const basket = await request("/cart", owner.token);
  assert(basket.items.length);
  await salesPage.getByText("Demande transmise", { exact: true }).waitFor();
  checks.push(
    "The restricted adviser submits the actual one-use RFQ, preserving the customer basket and requiring later quote acceptance",
  );
  await salesPage.getByRole("link", { name: "TOUSHIR", exact: true }).click();
  assert.equal(new URL(salesPage.url()).hash, "#crm");
  await salesPage
    .getByRole("textbox", {
      name: "Rechercher une commande, un produit ou un magasin",
      exact: true,
    })
    .fill("El Bahia");
  assert.equal(new URL(salesPage.url()).hash, "#crm");
  checks.push(
    "The adviser navigation and search stay inside the permitted CRM surface",
  );
  await button(salesPage, "Consigner un contact").click();
  await salesPage
    .getByRole("textbox", { name: "Compte rendu", exact: true })
    .fill("EXEMPLE · dossier à fermer après réaffectation");
  let releaseOldDetail,
    markOldDetailReady,
    holdNextDetail = true;
  const oldDetailReleased = new Promise((resolve) => {
    releaseOldDetail = resolve;
  });
  const oldDetailReady = new Promise((resolve) => {
    markOldDetailReady = resolve;
  });
  const detailPattern = "**/api/crm/shops/" + owner.user.id;
  await salesPage.route(detailPattern, async (route) => {
    if (!holdNextDetail) return route.continue();
    holdNextDetail = false;
    const response = await route.fetch();
    assert.equal(response.status(), 200);
    markOldDetailReady();
    await oldDetailReleased;
    await route.fulfill({ response });
  });
  await salesPage.getByTitle("Actualiser les données", { exact: true }).click();
  await oldDetailReady;
  await page.evaluate(() => window.scrollTo(0, 0));
  await button(page, "Changer de conseiller").click();
  await page
    .getByRole("combobox", { name: "Conseiller responsable", exact: true })
    .selectOption(admin.user.id);
  await page
    .getByRole("textbox", { name: "Motif du changement", exact: true })
    .fill("EXEMPLE · portefeuille réaffecté");
  await posted(page, "/crm/shops/" + owner.user.id + "/portfolio", () =>
    button(page, "Affecter ce conseiller").click(),
  );
  await salesPage.getByTitle("Actualiser les données", { exact: true }).click();
  await salesPage
    .getByText("Votre portefeuille commence ici.", { exact: true })
    .waitFor();
  await salesPage
    .getByText(
      "L’accès à ce magasin a changé. Sélectionnez un magasin de votre portefeuille actuel.",
      { exact: true },
    )
    .waitFor();
  assert.equal(
    await salesPage
      .getByRole("combobox", { name: "Magasin du portefeuille", exact: true })
      .inputValue(),
    "",
  );
  assert.equal(await salesPage.locator(".crm-shop-heading").count(), 0);
  assert.equal(await salesPage.locator(".crm-form-panel").count(), 0);
  releaseOldDetail();
  await salesPage.waitForResponse(
    (r) => new URL(r.url()).pathname === "/api/crm/shops/" + owner.user.id,
  );
  await salesPage.evaluate(
    () =>
      new Promise((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(resolve)),
      ),
  );
  assert.equal(await salesPage.locator(".crm-shop-heading").count(), 0);
  assert.equal(await salesPage.locator(".crm-form-panel").count(), 0);
  await salesPage.unroute(detailPattern);
  await capture(salesPage, "crm-reassigned-adviser-desktop.png");
  checks.push(
    "Reassignment clears the open adviser dossier and form through ordinary refresh without reloading, and a delayed earlier authorized response cannot restore them",
  );
  await request("/auth/logout", signed.token, {});
  await salesPage.reload();
  await button(
    salesPage,
    "Se connecter avec mon compte professionnel",
  ).waitFor();
  assert.equal(salesDemoPosts, 0);
  checks.push(
    "A revoked professional adviser session returns to sign-in without silently creating an administrator demo",
  );
  const invitation = await request("/shop/team/invitations", owner.token, {
    email: "qa-crm-viewer-" + crypto.randomUUID() + "@example.invalid",
    role: "VIEWER",
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  });
  const viewer = await request("/auth/shop-invitation/accept", null, {
    token: invitation.token,
    name: "EXEMPLE · observatrice",
    phone: "EXEMPLE",
    password: "Viewer-crm-" + crypto.randomUUID(),
  });
  const vc = await browser.newContext({
    viewport: { width: 402, height: 874 },
  });
  await vc.addInitScript((value) => {
    sessionStorage.setItem("toushir-token", value);
    sessionStorage.setItem("toushir-personal-account", "true");
  }, viewer.token);
  const viewPage = await vc.newPage();
  watch(viewPage);
  await viewPage.goto(base + "/mobile/merchant#account", {
    waitUntil: "domcontentloaded",
  });
  await viewPage.getByRole("button", { name: /^Demandes assistées/ }).click();
  await viewPage
    .getByText(
      /^Le propriétaire ou un responsable du magasin peut donner cette autorisation/,
    )
    .waitFor();
  assert.equal(
    await button(viewPage, "Vérifier les produits à autoriser").count(),
    0,
  );
  await capture(viewPage, "crm-viewer-assistance-phone.png");
  checks.push(
    "A real viewer sees the authorization history and a clear restriction without a grant action",
  );
  assert.deepEqual(errors, []);
  success = true;
} finally {
  await fs.writeFile(
    dir + "/crm-browser.json",
    JSON.stringify(
      {
        at: new Date().toISOString(),
        base,
        passed: checks.length,
        failed: success ? 0 : 1,
        checks,
        javascriptErrors: errors,
      },
      null,
      2,
    ),
  );
  console.log(
    JSON.stringify({ passed: checks.length, checks, javascriptErrors: errors }),
  );
  if (!success)
    await capture(salesPage || mobile || page, "crm-browser-failure.png");
  await browser.close();
}
