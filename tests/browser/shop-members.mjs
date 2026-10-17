import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { chromium } from "playwright";
const base = process.env.WEB_URL || "http://127.0.0.1:5861",
  dir = process.env.REPORT_DIR || "reports";
await fs.mkdir(dir, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const ownerPage = await browser.newPage({
  viewport: { width: 402, height: 874 },
});
const pages = [ownerPage],
  checks = [],
  javascriptErrors = [];
let memberPage;
let passed = false;
const watch = (page) =>
  page.on("pageerror", (e) => javascriptErrors.push(e.message));
watch(ownerPage);
const button = (p, name) => p.getByRole("button", { name, exact: true });
async function capture(page, name) {
  await page.screenshot({
    path: dir + "/" + name,
    fullPage: true,
    animations: "disabled",
  });
}
async function visibleInViewport(page, locator) {
  const box = await locator.boundingBox();
  const size = page.viewportSize();
  assert(
    box && box.y >= 0 && box.y + box.height <= size.height,
    "The failure and recovery must stay in the submitted viewport",
  );
}
async function response(page, path, action) {
  const waiting = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === path && r.request().method() === "POST",
  );
  await action();
  const r = await waiting,
    data = await r.json();
  assert(r.ok(), data.error?.code || r.status());
  return data;
}
async function team(page) {
  await page.getByRole("button", { name: /^Équipe du magasin/ }).click();
  await page.getByText("Équipe du magasin", { exact: true }).waitFor();
}
async function api(path, token, body, method) {
  const r = await ownerPage.request.fetch(base + "/api" + path, {
    method: method || (body ? "POST" : "GET"),
    headers: { Authorization: "Bearer " + token },
    ...(body ? { data: body } : {}),
  });
  const data = await r.json();
  assert(r.ok(), path + ": " + (data.error?.code || r.status()));
  return data;
}
async function top(page) {
  await page.evaluate(() => {
    document.querySelectorAll("*").forEach((el) => {
      if (el.scrollTop) el.scrollTop = 0;
    });
  });
}
try {
  await ownerPage.addInitScript(() => {
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: {
        writeText: async (value) => {
          window.__memberClipboard = value;
        },
      },
    });
  });
  await ownerPage.goto(base + "/mobile/merchant#account", {
    waitUntil: "domcontentloaded",
  });
  await ownerPage
    .getByRole("button", { name: /^Ma carte Toushir/ })
    .waitFor({ timeout: 60000 });
  const ownerToken = await ownerPage.evaluate(() =>
    sessionStorage.getItem("toushir-token"),
  );
  const owner = await api("/auth/me", ownerToken);
  assert.equal(owner.user.shop.role, "OWNER");
  await team(ownerPage);
  await ownerPage
    .getByText("Vous gérez le magasin seul", { exact: true })
    .waitFor();
  await top(ownerPage);
  await capture(ownerPage, "team-empty-phone.png");
  checks.push(
    "The shop owner reaches a useful team empty state from the actual account screen",
  );
  await button(ownerPage, "Inviter une personne").click();
  const email =
    "exemple.equipe-" + crypto.randomUUID().slice(0, 8) + "@example.invalid";
  await ownerPage
    .getByRole("textbox", { name: "E-mail de la personne", exact: true })
    .fill(email);
  await top(ownerPage);
  await capture(ownerPage, "team-invite-phone.png");
  await button(ownerPage, "Créer le lien privé").scrollIntoViewIfNeeded();
  await capture(ownerPage, "team-invite-actions-phone.png");
  await ownerPage
    .getByRole("textbox", { name: "E-mail de la personne", exact: true })
    .fill(owner.user.email);
  await button(ownerPage, "Créer le lien privé").scrollIntoViewIfNeeded();
  const rejectedInvite = ownerPage.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api/shop/team/invitations" &&
      r.request().method() === "POST",
  );
  await button(ownerPage, "Créer le lien privé").click();
  assert.equal((await rejectedInvite).status(), 409);
  const ownerError = ownerPage.getByText(
    "Cette adresse possède déjà un compte. Utilisez une adresse personnelle distincte.",
    { exact: true },
  );
  await ownerError.waitFor();
  await visibleInViewport(ownerPage, ownerError);
  await visibleInViewport(ownerPage, button(ownerPage, "Créer le lien privé"));
  await capture(ownerPage, "team-invite-error-actions-phone.png");
  assert.equal(
    await ownerPage
      .getByRole("textbox", { name: "E-mail de la personne", exact: true })
      .inputValue(),
    owner.user.email,
  );
  await ownerPage
    .getByRole("textbox", { name: "E-mail de la personne", exact: true })
    .fill(email);
  checks.push(
    "A real rejected owner invitation keeps its error, retry action and entered value at the action position",
  );
  const invitation = await response(
    ownerPage,
    "/api/shop/team/invitations",
    () => button(ownerPage, "Créer le lien privé").click(),
  );
  await ownerPage
    .getByText("Votre lien privé est prêt", { exact: true })
    .waitFor();
  await button(ownerPage, "Copier le lien privé").click();
  assert.equal(
    await ownerPage.evaluate(() => window.__memberClipboard),
    base + invitation.joinPath,
  );
  const copiedToast = ownerPage
    .getByRole("alert")
    .filter({ hasText: "Lien privé copié." });
  if (await copiedToast.count()) await copiedToast.click();
  checks.push(
    "The real invitation form persists an individual buyer role and passes its private fragment URL to the copy handler",
  );
  const context = await browser.newContext({
    viewport: { width: 402, height: 874 },
  });
  memberPage = await context.newPage();
  pages.push(memberPage);
  watch(memberPage);
  let demoPosts = 0;
  memberPage.on("request", (r) => {
    if (
      new URL(r.url()).pathname === "/api/demo/session" &&
      r.method() === "POST"
    )
      demoPosts++;
  });
  await memberPage.goto(base + invitation.joinPath, {
    waitUntil: "domcontentloaded",
  });
  await memberPage
    .getByText("Votre place dans l’équipe.", { exact: true })
    .waitFor();
  await memberPage
    .getByRole("textbox", { name: "Votre e-mail personnel", exact: true })
    .waitFor();
  assert.equal(
    await memberPage
      .getByRole("textbox", { name: "Votre e-mail personnel", exact: true })
      .inputValue(),
    email,
  );
  await memberPage
    .getByRole("textbox", { name: "Votre nom", exact: true })
    .fill("EXEMPLE · Lina du magasin");
  await memberPage
    .getByRole("textbox", { name: "Votre téléphone", exact: true })
    .fill("EXEMPLE");
  const password = "Toushir-member-" + crypto.randomUUID();
  await memberPage
    .getByLabel("Votre mot de passe", { exact: true })
    .fill(password);
  await memberPage
    .getByLabel("Confirmer le mot de passe", { exact: true })
    .fill("different");
  assert(await button(memberPage, "Rejoindre le magasin").isDisabled());
  await memberPage
    .getByLabel("Confirmer le mot de passe", { exact: true })
    .fill(password);
  await top(memberPage);
  await capture(memberPage, "team-accept-phone.png");
  await button(memberPage, "Rejoindre le magasin").scrollIntoViewIfNeeded();
  await capture(memberPage, "team-accept-actions-phone.png");
  const joined = await response(
    memberPage,
    "/api/auth/shop-invitation/accept",
    () => button(memberPage, "Rejoindre le magasin").click(),
  );
  await memberPage
    .getByRole("button", { name: /^Ma carte Toushir/ })
    .waitFor({ timeout: 30000 });
  assert.equal(joined.user.id === owner.user.id, false);
  assert.equal(joined.user.shop.id, owner.user.id);
  assert.equal(demoPosts, 0);
  assert.equal(
    await memberPage.evaluate(() =>
      localStorage.getItem("toushir-demo-workspace"),
    ),
    null,
  );
  await team(memberPage);
  await memberPage.getByText(/EXEMPLE · Lina du magasin · Acheteur/).waitFor();
  assert.equal(await button(memberPage, "Inviter une personne").count(), 0);
  await top(memberPage);
  await capture(memberPage, "team-buyer-phone.png");
  checks.push(
    "A person creates a password account through the actual invitation screen and joins the existing shop without a demo-owner capability",
  );
  // Real concurrent revocation after preview exercises submission failure without a mocked response.
  const revokedInvitation = await api("/shop/team/invitations", ownerToken, {
    email: "qa-expiring-" + crypto.randomUUID() + "@example.invalid",
    role: "BUYER",
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  });
  const failurePage = await browser.newPage({
    viewport: { width: 402, height: 874 },
  });
  pages.push(failurePage);
  watch(failurePage);
  await failurePage.goto(base + revokedInvitation.joinPath, {
    waitUntil: "domcontentloaded",
  });
  await failurePage
    .getByRole("textbox", { name: "Votre nom", exact: true })
    .fill("EXEMPLE · accès révoqué");
  await failurePage
    .getByRole("textbox", { name: "Votre téléphone", exact: true })
    .fill("EXEMPLE");
  await failurePage
    .getByLabel("Votre mot de passe", { exact: true })
    .fill(password);
  await failurePage
    .getByLabel("Confirmer le mot de passe", { exact: true })
    .fill(password);
  await api(
    "/shop/team/invitations/" + revokedInvitation.invitation.id + "/revoke",
    ownerToken,
    { version: 1, reason: "EXEMPLE · révoqué pendant la saisie" },
  );
  await button(failurePage, "Rejoindre le magasin").scrollIntoViewIfNeeded();
  const rejectedAcceptance = failurePage.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api/auth/shop-invitation/accept" &&
      r.request().method() === "POST",
  );
  await button(failurePage, "Rejoindre le magasin").click();
  assert.equal((await rejectedAcceptance).status(), 409);
  const acceptError = failurePage.getByText(
    "Cette invitation est expirée, utilisée ou révoquée.",
    { exact: true },
  );
  await acceptError.waitFor();
  await visibleInViewport(failurePage, acceptError);
  await visibleInViewport(
    failurePage,
    button(failurePage, "Rejoindre le magasin"),
  );
  assert.equal(
    await failurePage
      .getByRole("textbox", { name: "Votre nom", exact: true })
      .inputValue(),
    "EXEMPLE · accès révoqué",
  );
  await capture(failurePage, "team-accept-error-actions-phone.png");
  await failurePage.close();
  checks.push(
    "A link revoked after preview shows its real acceptance error beside the retry action without losing the entered name",
  );
  await memberPage.reload();
  await memberPage.getByRole("button", { name: /^Ma carte Toushir/ }).waitFor();
  await team(memberPage);
  assert.equal(demoPosts, 0);
  checks.push(
    "The personal member identity survives reload without silently creating a demonstration",
  );
  // The owner refreshes the authoritative list, then changes the actual member role.
  await button(ownerPage, "Masquer le lien").click();
  await button(ownerPage, "Retour à Mon Toushir").click();
  await team(ownerPage);
  await button(ownerPage, "Gérer EXEMPLE · Lina du magasin").click();
  await ownerPage
    .getByRole("radio")
    .filter({ hasText: "Lecture seule" })
    .click();
  await ownerPage
    .getByRole("textbox", { name: "Motif du changement", exact: true })
    .fill("EXEMPLE · accès de consultation");
  await ownerPage
    .getByText("Modifier cet accès", { exact: true })
    .scrollIntoViewIfNeeded();
  await capture(ownerPage, "team-role-change-phone.png");
  await button(ownerPage, "Révoquer cet accès").scrollIntoViewIfNeeded();
  await capture(ownerPage, "team-role-change-actions-phone.png");
  const member = (await api("/shop/team", ownerToken)).members.find(
    (x) => x.accountId === joined.user.id,
  );
  await response(
    ownerPage,
    "/api/shop/team/members/" + member.id + "/role",
    () => button(ownerPage, "Enregistrer les droits").click(),
  );
  await memberPage.reload();
  await memberPage
    .getByText("Bienvenue sur Toushir.", { exact: true })
    .waitFor({ timeout: 30000 });
  assert.equal(demoPosts, 0);
  await capture(memberPage, "team-access-changed-phone.png");
  checks.push(
    "The owner changes actual role permissions with an explicit reason, and the old member session returns to personal sign-in",
  );
  const viewerSession = await api("/auth/login", null, { email, password });
  assert.equal(viewerSession.user.shop.role, "VIEWER");
  await memberPage.evaluate((token) => {
    sessionStorage.setItem("toushir-token", token);
    sessionStorage.setItem("toushir-personal-account", "true");
  }, viewerSession.token);
  await memberPage.reload();
  await memberPage.getByRole("button", { name: /^Ma carte Toushir/ }).waitFor();
  await team(memberPage);
  await memberPage
    .getByText(/EXEMPLE · Lina du magasin · Lecture seule/)
    .waitFor();
  assert.equal(await button(memberPage, "Inviter une personne").count(), 0);
  assert.equal(
    await button(memberPage, "Gérer EXEMPLE · Lina du magasin").count(),
    0,
  );
  await top(memberPage);
  await capture(memberPage, "team-viewer-phone.png");
  await button(memberPage, "Retour à Mon Toushir").click();
  await memberPage.getByRole("tab", { name: "Accueil", exact: true }).click();
  const add = memberPage
    .getByRole("button", { name: /^Ajouter .+ au panier$/ })
    .first();
  await add.waitFor();
  const saved = memberPage.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api/cart" &&
      r.request().method() === "PUT",
  );
  await add.click();
  assert((await saved).ok());
  await button(memberPage, "Voir le panier").click();
  await button(memberPage, "Accès en lecture seule").waitFor();
  assert(await button(memberPage, "Accès en lecture seule").isDisabled());
  await capture(memberPage, "team-viewer-cart-phone.png");
  checks.push(
    "A viewer retains a personal basket while the actual checkout control is disabled",
  );
  await memberPage.getByRole("tab", { name: "Messages", exact: true }).click();
  assert.equal(await button(memberPage, "Écrire à Toushir").count(), 0);
  assert.equal(
    await button(memberPage, "Préférences de communication").count(),
    0,
  );
  await memberPage
    .getByText(/^Accès en lecture seule\. Le propriétaire/)
    .waitFor();
  await capture(memberPage, "team-viewer-messages-phone.png");
  checks.push(
    "Read-only members cannot send shared messages or change shop marketing consent",
  );
  await top(ownerPage);
  await ownerPage.setViewportSize({ width: 390, height: 844 });
  await ownerPage.waitForTimeout(400);
  await capture(ownerPage, "team-active-narrow.png");
  assert(
    await ownerPage.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  );
  await ownerPage.setViewportSize({ width: 1440, height: 1000 });
  await ownerPage.waitForTimeout(400);
  await top(ownerPage);
  await capture(ownerPage, "team-active-wide.png");
  checks.push(
    "The team list remains readable at 390 pixels and in the wide browser preview",
  );
  await ownerPage.setViewportSize({ width: 402, height: 874 });
  await button(ownerPage, "Gérer EXEMPLE · Lina du magasin").click();
  await ownerPage
    .getByRole("textbox", { name: "Motif du changement", exact: true })
    .fill("EXEMPLE · fin de la mission de consultation");
  await response(
    ownerPage,
    "/api/shop/team/members/" + member.id + "/revoke",
    () => button(ownerPage, "Révoquer cet accès").click(),
  );
  await ownerPage
    .getByText("Lecture seule · Accès révoqué", { exact: true })
    .waitFor();
  await memberPage.reload();
  await memberPage
    .getByText("Bienvenue sur Toushir.", { exact: true })
    .waitFor();
  assert.equal(demoPosts, 0);
  checks.push(
    "Revoking the person through the real form preserves their historical row and prevents demo fallback on reload",
  );
  const expired = await browser.newPage({
    viewport: { width: 402, height: 874 },
  });
  pages.push(expired);
  watch(expired);
  await expired.goto(base + invitation.joinPath, {
    waitUntil: "domcontentloaded",
  });
  await expired
    .getByText("Cette invitation est expirée, utilisée ou révoquée.", {
      exact: true,
    })
    .waitFor();
  assert.equal(await button(expired, "Rejoindre le magasin").count(), 0);
  await capture(expired, "team-used-invitation-phone.png");
  checks.push(
    "A consumed invitation shows a truthful recovery state without another account-creation form",
  );
  assert.deepEqual(javascriptErrors, []);
  passed = true;
} finally {
  await fs.writeFile(
    dir + "/shop-members-browser.json",
    JSON.stringify(
      {
        at: new Date().toISOString(),
        base,
        passed: checks.length,
        failed: passed ? 0 : 1,
        checks,
        javascriptErrors,
      },
      null,
      2,
    ),
  );
  console.log(
    JSON.stringify({ passed: checks.length, checks, javascriptErrors }),
  );
  if (!passed)
    await capture(memberPage || ownerPage, "team-browser-failure.png");
  await browser.close();
}
