import fs from "node:fs/promises";
const reportDir = process.env.REPORT_DIR || "reports";
await fs.mkdir(reportDir, { recursive: true });
import { chromium } from "playwright";
import { writeFile } from "node:fs/promises";
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const page = await browser.newPage({ viewport: { width: 1600, height: 1040 } });
page.setDefaultTimeout(20000);
const checks = [],
  errors = [];
page.on("pageerror", (e) => errors.push(e.message));
const nav = (n) =>
  page
    .getByRole("navigation")
    .getByRole("button", { name: n, exact: true })
    .click();
const saved = async () => {
  await page.locator(".toast").waitFor();
  if (await page.locator(".error-banner").count())
    throw Error(
      (await page.locator(".error-banner").allTextContents()).join("; "),
    );
  await page.waitForTimeout(500);
};
try {
  await page.goto(process.env.WEB_URL || "http://127.0.0.1:5860", {
    waitUntil: "domcontentloaded",
  });
  await page
    .getByRole("heading", { name: "Votre activité, en clair." })
    .waitFor({ timeout: 60000 });
  await nav("Achats fournisseurs");
  await page
    .getByRole("button", { name: "Nouveau fournisseur", exact: true })
    .click();
  await page.locator('input[name="name"]').fill("Partenaire QA navigateur");
  await page.locator('input[name="contact"]').fill("Équipe achats");
  await page.locator('input[name="phone"]').fill("0500000000");
  await page
    .getByRole("button", { name: "Enregistrer le fournisseur", exact: true })
    .click();
  await saved();
  checks.push("Supplier created through actual form");
  await page
    .getByRole("button", { name: "Créer un achat", exact: true })
    .click();
  await page
    .locator('select[name="supplier"]')
    .selectOption({ label: "Partenaire QA navigateur" });
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
  await page.locator('input[name="cost-0"]').fill("42.5");
  await page
    .getByRole("button", { name: "Créer le brouillon d’achat", exact: true })
    .click();
  await saved();
  await page
    .getByRole("button", {
      name: "Approuver avec le compte Finance de démonstration",
      exact: true,
    })
    .click();
  await saved();
  await page
    .getByRole("heading", { name: "Réception physique", exact: true })
    .waitFor();
  checks.push(
    "Purchase submitted in DZD, independently approved and ready for receipt",
  );
  await page.locator('input[name^="qty-"]').fill("8");
  await page.locator('input[name^="quarantine-"]').fill("1");
  await page.locator('input[name^="lot-"]').fill("QA-BROWSER-001");
  await page
    .locator('input[name^="expiry-"]')
    .fill(new Date(Date.now() + 365 * 86400000).toISOString().slice(0, 10));
  await page
    .getByRole("button", {
      name: "Enregistrer les marchandises reçues",
      exact: true,
    })
    .click();
  await saved();
  await page
    .getByRole("heading", {
      name: "Contrôler le lot QA-BROWSER-001",
      exact: true,
    })
    .waitFor();
  checks.push("Partial receipt persisted with actual lot and quarantine");
  await page.screenshot({
    path: reportDir + "/desktop-purchases.png",
    fullPage: true,
  });
  await nav("Messages");
  await page
    .getByRole("button", { name: "Nouvelle conversation", exact: true })
    .click();
  await page
    .locator('select[name="merchant"] option')
    .nth(1)
    .waitFor({ state: "attached" });
  await page
    .locator('select[name="merchant"]')
    .selectOption(
      await page
        .locator('select[name="merchant"] option')
        .nth(1)
        .getAttribute("value"),
    );
  await page.locator('input[name="subject"]').fill("Suivi de la commande QA");
  await page
    .getByRole("button", { name: "Ouvrir la conversation", exact: true })
    .click();
  await saved();
  await page
    .locator('textarea[name="text"]')
    .fill("Votre demande est prise en charge.");
  await page
    .getByRole("button", { name: "Envoyer dans l’app", exact: true })
    .click();
  await saved();
  await page
    .getByText("Votre demande est prise en charge.", { exact: true })
    .waitFor();
  checks.push("Real conversation and customer message persisted");
  await page.screenshot({
    path: reportDir + "/desktop-messages.png",
    fullPage: true,
  });
  await nav("Campagnes");
  await page
    .getByRole("button", { name: "Nouvelle campagne", exact: true })
    .click();
  await page.locator('input[name="title"]').fill("Offre QA navigateur");
  await page
    .locator('textarea[name="description"]')
    .fill("Cinq unités commandées, remise de 5 %, conditions explicites.");
  await page
    .locator('select[name="productId"] option')
    .nth(1)
    .waitFor({ state: "attached" });
  await page
    .locator('select[name="productId"]')
    .selectOption(
      await page
        .locator('select[name="productId"] option')
        .nth(1)
        .getAttribute("value"),
    );
  await page.locator('input[name="percent"]').fill("5");
  await page
    .locator('input[name="start"]')
    .fill(new Date(Date.now() - 3600000).toISOString().slice(0, 16));
  await page
    .locator('input[name="end"]')
    .fill(new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16));
  await page
    .getByRole("button", { name: "Enregistrer le brouillon", exact: true })
    .click();
  await saved();
  await page
    .getByRole("button", {
      name: "Publier avec l’approbateur Finance de démonstration",
      exact: true,
    })
    .click();
  await saved();
  await page.locator('input[name="qty"]').fill("5");
  await page
    .getByRole("button", { name: "Calculer l’avantage", exact: true })
    .click();
  await saved();
  await page.getByText("Conditions remplies", { exact: true }).waitFor();
  checks.push("Campaign independently approved and eligibility computed");
  await page.screenshot({
    path: reportDir + "/desktop-campaigns.png",
    fullPage: true,
  });
} catch (e) {
  checks.push("FAIL: " + e.message);
  errors.push(...(await page.locator(".error-banner").allTextContents()));
  process.exitCode = 1;
  await page.screenshot({
    path: reportDir + "/business-form-failure.png",
    fullPage: true,
  });
} finally {
  console.log(JSON.stringify({ checks, errors }));
  await writeFile(
    reportDir + "/business-forms.json",
    JSON.stringify({ at: new Date().toISOString(), checks, errors }, null, 2),
  );
  await browser.close();
}
