import fs from "node:fs/promises";
import { createHash, randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const base = process.env.WEB_URL || "http://127.0.0.1:5861";
const reportDir = process.env.REPORT_DIR || "reports";
await fs.mkdir(reportDir, { recursive: true });
const fixture = fileURLToPath(
  new URL("../fixtures/supplier-example.pdf", import.meta.url),
);
const fixtureHash = createHash("sha256")
  .update(await fs.readFile(fixture))
  .digest("hex");
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const page = await browser.newPage({ viewport: { width: 1600, height: 1040 } });
page.setDefaultTimeout(25000);
const checks = [],
  javascriptErrors = [];
page.on("pageerror", (error) => javascriptErrors.push(error.message));
const button = (name) => page.getByRole("button", { name, exact: true });
const nav = (name) =>
  page
    .getByRole("navigation")
    .getByRole("button", { name, exact: true })
    .click();
async function submit(name, suffix, scope = page) {
  const pending = page.waitForResponse(
    (response) =>
      response.request().method() === "POST" &&
      new URL(response.url()).pathname.endsWith(suffix),
  );
  await scope.getByRole("button", { name, exact: true }).click();
  const response = await pending;
  const result = await response.json();
  if (!response.ok())
    throw Error(
      result?.error?.message || `POST ${suffix}: ${response.status()}`,
    );
  return result;
}
async function upload(kind, reference) {
  await button("Ajouter une pièce").click();
  const form = page.locator(".supplier-dossier form").filter({
    has: page.getByRole("heading", { name: "Ajouter un PDF", exact: true }),
  });
  await form.getByLabel("Type de pièce").selectOption(kind);
  await form
    .getByLabel("Référence de la pièce", { exact: true })
    .fill(reference);
  await form
    .getByLabel("Date de la pièce", { exact: true })
    .fill(new Date().toISOString().slice(0, 10));
  await form.getByLabel("Fichier PDF").setInputFiles(fixture);
  if (kind === "PURCHASE_ORDER")
    await captureDossier("supplier-upload-desktop.png", "Ajouter un PDF");
  if (kind === "RECEIPT") {
    const select = form.getByLabel("Réception physique concernée");
    await select.selectOption(
      await select.locator("option").nth(1).getAttribute("value"),
    );
  }
  const result = await submit("Conserver la pièce", "/documents", form);
  await form.waitFor({ state: "hidden" });
  await page
    .locator(".supplier-file")
    .getByText(reference, { exact: true })
    .waitFor();
  return result.document;
}
async function captureDossier(
  name,
  headingName = "Pièces et accord fournisseur",
) {
  const heading = page.getByRole("heading", {
    name: headingName,
    exact: true,
  });
  await heading.evaluate((element) =>
    element.scrollIntoView({ block: "start" }),
  );
  await page.evaluate(() => window.scrollBy(0, -90));
  const toastClose = page
    .locator(".toast")
    .getByRole("button", { name: "Fermer", exact: true });
  if (await toastClose.isVisible()) await toastClose.click();
  await page.screenshot({ path: reportDir + "/" + name, fullPage: false });
}
try {
  await page.goto(base, { waitUntil: "domcontentloaded" });
  await page
    .getByRole("heading", { name: "Votre activité, en clair.", exact: true })
    .waitFor({ timeout: 60000 });
  await nav("Achats fournisseurs");
  const supplierName = "EXEMPLE QA navigateur · " + Date.now();
  await button("Nouveau fournisseur").click();
  await page.locator('input[name="name"]').fill(supplierName);
  await page.locator('input[name="contact"]').fill("Équipe de test fictive");
  await page.locator('input[name="phone"]').fill("EXEMPLE");
  await submit("Enregistrer le fournisseur", "/admin/suppliers");
  await button("Créer un achat").click();
  await page
    .locator('select[name="supplier"] option')
    .getByText(supplierName, { exact: true })
    .waitFor({ state: "attached" });
  await page
    .locator('select[name="supplier"]')
    .selectOption({ label: supplierName });
  await page
    .locator('input[name="expected"]')
    .fill(new Date(Date.now() + 3 * 86400000).toISOString().slice(0, 16));
  await page
    .locator('select[name="product-0"] option')
    .nth(1)
    .waitFor({ state: "attached" });
  const pid = await page
    .locator('select[name="product-0"] option')
    .nth(1)
    .getAttribute("value");
  await page.locator('select[name="product-0"]').selectOption(pid);
  await page.locator('input[name="qty-0"]').fill("10");
  await page.locator('input[name="cost-0"]').fill("100");
  const created = await submit(
    "Créer le brouillon d’achat",
    "/admin/purchases",
  );
  const purchasePath = "/api/admin/purchases/" + created.order.id;
  await button("Approuver avec le compte Finance de démonstration").waitFor();
  await submit("Approuver avec le compte Finance de démonstration", "/approve");
  await page
    .getByRole("heading", { name: "Réception physique", exact: true })
    .waitFor();
  checks.push(
    "Created purchase and independently approved ten units through real browser forms",
  );
  await upload("PURCHASE_ORDER", "PO-EXEMPLE-UI");
  const confirmationDocument = await upload(
    "SUPPLIER_CONFIRMATION",
    "CF-EXEMPLE-UI",
  );
  checks.push(
    "Attached real fixture PDFs with dates and immutable SHA256 evidence",
  );
  await button("Saisir l’accord fournisseur").click();
  const confirmationForm = page.locator(".supplier-dossier form").filter({
    has: page.getByRole("heading", {
      name: "Termes reçus du fournisseur",
      exact: true,
    }),
  });
  await confirmationForm
    .getByLabel("Pièce justificative")
    .selectOption(confirmationDocument.id);
  await confirmationForm
    .getByLabel("Référence fournisseur", { exact: true })
    .fill("CF-EXEMPLE-UI");
  await confirmationForm
    .getByLabel("Quantité confirmée", { exact: true })
    .fill("8");
  await confirmationForm
    .getByLabel("Coût unitaire confirmé · DA", { exact: true })
    .fill("120");
  await submit(
    "Soumettre la confirmation à examen",
    "/confirmations",
    confirmationForm,
  );
  await confirmationForm.waitFor({ state: "hidden" });
  await page
    .getByLabel("Motif de la décision", { exact: true })
    .fill("EXEMPLE · termes contrôlés par Finance");
  await captureDossier("supplier-review-desktop.png");
  await submit("Enregistrer l’examen distinct", "/review");
  await page
    .locator(".supplier-dossier .badge")
    .getByText("Acceptée", { exact: true })
    .waitFor();
  await page.waitForFunction(
    () =>
      document
        .querySelector('form input[name^="qty-"]')
        ?.getAttribute("max") === "8",
  );
  checks.push(
    "Supplier proposal changed ten units at 100 DA to eight at 120 DA only after separate Finance review",
  );
  const receiving = page.locator("form").filter({
    has: page.getByRole("heading", {
      name: "Réception physique",
      exact: true,
    }),
  });
  await receiving.locator('input[name^="qty-"]').fill("5");
  await receiving.locator('input[name^="quarantine-"]').fill("0");
  await receiving.locator('input[name^="lot-"]').fill("EXEMPLE-UI-LOT");
  await receiving
    .locator('input[name^="expiry-"]')
    .fill(new Date(Date.now() + 365 * 86400000).toISOString().slice(0, 10));
  await submit("Enregistrer les marchandises reçues", "/receive", receiving);
  await button("Clôturer le reliquat").waitFor();
  await page.waitForFunction(
    () =>
      document
        .querySelector('form input[name^="qty-"]')
        ?.getAttribute("max") === "3",
  );
  await upload("RECEIPT", "REC-EXEMPLE-UI");
  await upload("INVOICE", "FACT-EXEMPLE-UI");
  const invoice = page
    .locator("form")
    .filter({ has: page.locator('input[name="ref"]') });
  await invoice.locator('input[name="ref"]').fill("FACT-EXEMPLE-UI");
  await invoice.locator('input[name="amount"]').fill("600");
  await submit(
    await invoice.getByRole("button", { name: /Comparer/ }).innerText(),
    "/invoice-review",
    invoice,
  );
  await page.getByText("Chaîne des pièces complète", { exact: true }).waitFor();
  checks.push(
    "Recorded five physically received units and completed the receipt/invoice chain for exactly 600 DA",
  );
  await captureDossier("supplier-dossier-desktop.png");
  await button("Clôturer le reliquat").click();
  await page
    .getByLabel("Motif de clôture", { exact: true })
    .fill("EXEMPLE · fournisseur ne livrera pas les trois restantes");
  await submit("Confirmer la clôture", "/close-remainder");
  await button("Clôturer le reliquat").waitFor({ state: "hidden" });
  const token = await page.evaluate(() =>
    sessionStorage.getItem("toushir-web-token"),
  );
  const actualResponse = await page.request.get(base + purchasePath, {
    headers: { Authorization: "Bearer " + token },
  });
  const actual = await actualResponse.json();
  if (
    actual.order.status !== "SHORT_CLOSED" ||
    actual.items[0].receivedQuantity !== 5 ||
    actual.items[0].cancelledQuantity !== 3 ||
    actual.items[0].outstandingQuantity !== 0
  )
    throw Error("Incorrect closed quantities");
  checks.push(
    "Independently closed three undelivered units while preserving the actual receipt",
  );
  const workspaceToken = await page.evaluate(() =>
    localStorage.getItem("toushir-demo-workspace"),
  );
  const driverSession = await page.request.post(base + "/api/demo/session", {
    data: { role: "DRIVER", workspaceToken },
  });
  if (!driverSession.ok())
    throw Error("Could not change isolated cookie identity");
  const documentUrl =
    base + purchasePath + "/documents/" + confirmationDocument.id + "/content";
  const cookieOnly = await page.request.get(documentUrl);
  if (cookieOnly.status() !== 403)
    throw Error("Driver cookie unexpectedly authorized for private PDF");
  const row = page
    .locator(".supplier-file")
    .filter({ has: page.getByText("CF-EXEMPLE-UI", { exact: true }) });
  const downloadEvent = page.waitForEvent("download");
  await row.getByRole("button", { name: "Télécharger", exact: true }).click();
  const download = await downloadEvent;
  const hash = createHash("sha256")
    .update(await fs.readFile(await download.path()))
    .digest("hex");
  if (hash !== fixtureHash)
    throw Error("Downloaded private PDF differs from original");
  checks.push(
    "Private browser download uses the active Admin bearer even when another role owns the HttpOnly cookie",
  );
  await page.reload({ waitUntil: "domcontentloaded" });
  await page
    .getByRole("heading", { name: "Commandes fournisseurs", exact: true })
    .waitFor();
  await page.getByRole("row").filter({ hasText: created.order.number }).click();
  await page.getByText("Chaîne des pièces complète", { exact: true }).waitFor();
  checks.push(
    "Reload restored purchase history and complete supplier evidence",
  );
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForFunction(
    () => document.querySelector(".sidebar").getBoundingClientRect().right <= 0,
  );
  await captureDossier("supplier-dossier-narrow.png");
  const actionHeights = await page
    .locator(".supplier-file-actions button")
    .evaluateAll((elements) =>
      elements.map((element) => element.getBoundingClientRect().height),
    );
  if (actionHeights.some((height) => height < 44))
    throw Error("Narrow document actions are below the touch-target floor");
  if (
    await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    )
  )
    throw Error("Supplier screen overflows narrow viewport");
  checks.push(
    "Supplier evidence remains usable at 390 pixels without page overflow",
  );
  await button("Ajouter une pièce").click();
  const uploadForm = page.locator(".supplier-dossier form").filter({
    has: page.getByRole("heading", { name: "Ajouter un PDF", exact: true }),
  });
  await uploadForm
    .getByLabel("Référence de la pièce", { exact: true })
    .fill("PDF-EXEMPLE-PHONE");
  await uploadForm.getByLabel("Fichier PDF").setInputFiles(fixture);
  await captureDossier("supplier-upload-narrow.png", "Ajouter un PDF");
  const supplierActions = await page
    .locator(".supplier-dossier button.button")
    .evaluateAll((elements) =>
      elements.map((element) => element.getBoundingClientRect().height),
    );
  if (supplierActions.some((height) => height < 44))
    throw Error("Supplier form action below minimum phone target");
  const bounds = await uploadForm.boundingBox();
  if (!bounds || bounds.x < 0 || bounds.x + bounds.width > 390)
    throw Error("Narrow document form leaves viewport");
  await uploadForm
    .getByRole("button", { name: "Annuler", exact: true })
    .click();
  checks.push(
    "Narrow PDF form remains inside the viewport and can be cancelled without a write",
  );
  const secondResponse = await page.request.post(
    base + "/api/admin/purchases",
    {
      headers: { Authorization: "Bearer " + token },
      data: {
        supplierId: created.order.supplierId,
        expectedAt: new Date(Date.now() + 86400000).toISOString(),
        items: [{ productId: pid, quantity: 2, unitCostMinor: 10000 }],
        note: "EXEMPLE · sélection concurrente",
        idempotencyKey: randomUUID(),
      },
    },
  );
  if (!secondResponse.ok())
    throw Error("Could not create selection-race fixture");
  const second = await secondResponse.json();
  await button("Actualiser").click();
  const secondRow = page
    .getByRole("row")
    .filter({ hasText: second.order.number });
  await secondRow.click();
  const secondHeading = page.getByRole("heading", {
    name: second.order.number + " · " + supplierName,
    exact: true,
  });
  await secondHeading.waitFor();
  let releaseFirst, signalHeld;
  const hold = new Promise((resolve) => {
    releaseFirst = resolve;
  });
  const held = new Promise((resolve) => {
    signalHeld = resolve;
  });
  await page.route(base + purchasePath, async (route) => {
    if (route.request().method() === "GET") {
      signalHeld();
      await hold;
    }
    await route.continue();
  });
  await page.getByRole("row").filter({ hasText: created.order.number }).click();
  await held;
  await secondRow.click();
  await secondHeading.waitFor();
  const lateResponse = page.waitForResponse(
    (response) =>
      response.request().method() === "GET" &&
      response.url() === base + purchasePath,
  );
  releaseFirst();
  await (await lateResponse).finished();
  await page.evaluate(
    () =>
      new Promise((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(resolve)),
      ),
  );
  if (!(await secondHeading.isVisible()))
    throw Error("Late purchase response overwrote the selected dossier");
  await page.unroute(base + purchasePath);
  checks.push(
    "A delayed response for an old purchase cannot replace the newly selected dossier",
  );
  if (javascriptErrors.length) throw Error("Browser JavaScript errors");
} catch (error) {
  checks.push("FAIL: " + error.message);
  const banners = await page.locator(".error-banner").allTextContents();
  if (banners.length) checks.push(...banners);
  process.exitCode = 1;
  await page.screenshot({
    path: reportDir + "/supplier-dossier-failure.png",
    fullPage: true,
  });
} finally {
  const result = {
    at: new Date().toISOString(),
    base,
    checks,
    javascriptErrors,
  };
  await fs.writeFile(
    reportDir + "/supplier-documents-browser.json",
    JSON.stringify(result, null, 2),
  );
  console.log(JSON.stringify(result));
  await browser.close();
}
