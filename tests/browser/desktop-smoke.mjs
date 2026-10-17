import fs from "node:fs/promises";
const reportDir = process.env.REPORT_DIR || "reports";
await fs.mkdir(reportDir, { recursive: true });
import { chromium } from "playwright";
import { mkdir, writeFile } from "node:fs/promises";
const out = reportDir;
await mkdir(out, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const page = await browser.newPage({ viewport: { width: 1600, height: 1040 } });
page.setDefaultTimeout(15000);
const errors = [];
page.on("pageerror", (e) => errors.push(e.message));
try {
await page.goto(process.env.WEB_URL || "http://127.0.0.1:5860/", {
  waitUntil: "domcontentloaded", timeout: 60000,
});
await page
  .getByRole("heading", { name: "Votre activité, en clair." })
  .waitFor({ timeout: 60000 });
await page.waitForTimeout(1800);
await page.screenshot({ path: out + "/desktop-overview.png", fullPage: true });
const rows = [];
for (const name of [
  "Commandes",
  "Préparation",
  "Livraisons",
  "Catalogue",
  "Stock & qualité",
  "Magasins",
  "Achats fournisseurs",
  "Messages",
  "Campagnes",
  "Points & cadeaux",
  "Finance",
  "Analyses",
  "Équipe & paramètres",
]) {
  await page
    .getByRole("navigation")
    .getByRole("button", { name, exact: true })
    .click();
  await page.waitForTimeout(name === "Livraisons" ? 2500 : 650);
  rows.push({
    name,
    errorBanners: await page.locator(".error-banner").allTextContents(),
    tableRows: await page.locator("tbody tr").count(),
  });
  if (name === "Livraisons")
    await page.screenshot({
      path: out + "/desktop-delivery.png",
      fullPage: true,
    });
}
await page
  .getByRole("navigation")
  .getByRole("button", { name: "Commandes", exact: true })
  .click();
await page.locator("tbody tr").first().click();
await page.getByRole("dialog").waitFor();
await page.screenshot({ path: out + "/desktop-order.png", fullPage: true });
await page.getByRole("button", { name: "Fermer le dossier" }).click();
await page
  .getByRole("navigation")
  .getByRole("button", { name: "Livraisons", exact: true })
  .click();
await page.getByRole("button", { name: "Carte GPS", exact: true }).click();
await page.waitForTimeout(2000);
await page.screenshot({ path: out + "/desktop-gps.png", fullPage: false });
await page.getByRole("button", { name: /thème sombre/i }).click();
await page.waitForTimeout(400);
await page.screenshot({ path: out + "/desktop-dark.png", fullPage: false });
await page.setViewportSize({ width: 390, height: 844 });
await page.waitForTimeout(450);
await page.evaluate(() => window.scrollTo(0, 0));
const menuTrigger = page.getByRole("button", { name: "Ouvrir le menu", exact: true });
await menuTrigger.click();
await page.getByRole("navigation").getByRole("button", { name: "Commandes", exact: true }).click();
await page.getByRole("heading", { name: "Commandes", exact: true }).waitFor();
await menuTrigger.waitFor({ state: "visible" });
await page.screenshot({
  path: out + "/desktop-responsive.png",
  fullPage: true,
});
await writeFile(
  out + "/desktop-smoke.json",
  JSON.stringify(
    { at: new Date().toISOString(), errors, routes: rows },
    null,
    2,
  ),
);
console.log(JSON.stringify({ errors, routes: rows }));
if(errors.length||rows.some(r=>r.errorBanners.length))throw new Error("Unexpected browser error or module error banner");

} finally {
await browser.close();

}
