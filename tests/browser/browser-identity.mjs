import fs from "node:fs/promises";
const reportDir = process.env.REPORT_DIR || "reports";
await fs.mkdir(reportDir, { recursive: true });
import { chromium } from "playwright";
import { randomBytes } from "node:crypto";
import { writeFile } from "node:fs/promises";
const base = process.env.WEB_URL || "http://127.0.0.1:5860";
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(25000);
const errors = [], javascriptErrors = [],
  checks = [];
page.on("pageerror", (e) => javascriptErrors.push(e.message));
page.on("console", (e) => {
  if (e.type() === "error") errors.push(e.text());
});
page.on("requestfailed", (r) =>
  errors.push(new URL(r.url()).pathname + ": " + r.failure()?.errorText),
);
try {
  await page.goto(base, { waitUntil: "domcontentloaded" });
  await page
    .getByRole("heading", { name: "Votre activité, en clair." })
    .waitFor({ timeout: 60000 });
  const password = randomBytes(22).toString("base64url") + "aA4!";
  const email = "browser-" + Date.now() + "@toushir.test";
  await page.evaluate(
    async ({ email, password }) => {
      const token = sessionStorage.getItem("toushir-web-token");
      const r = await fetch("/api/operations/team", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer " + token,
        },
        body: JSON.stringify({
          name: "Contrôle préparation navigateur",
          email,
          password,
          role: "WAREHOUSE",
          phone: "0500000000",
        }),
      });
      if (!r.ok)
        throw Error("Could not create isolated staff test account " + r.status);
    },
    { email, password },
  );
  await page.getByTitle("Se déconnecter", { exact: true }).click();
  await page
    .getByRole("button", { name: "Se connecter avec mon compte professionnel" })
    .click();
  await page.getByLabel("E-mail professionnel", { exact: true }).fill(email);
  await page.getByLabel("Mot de passe", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Se connecter", exact: true }).click();
  await page.waitForURL((u) => u.pathname === "/" && u.hash === "#picking", {
    timeout: 20000,
    waitUntil: "domcontentloaded",
  });
  await page
    .getByRole("heading", { name: "Chaque carton, sous contrôle." })
    .waitFor();
  checks.push(
    "System authorization page completed code+PKCE and returned a staff bearer session",
  );
  const menu = await page
    .getByRole("navigation")
    .getByRole("button")
    .allTextContents();
  if (
    menu.some((x) =>
      ["Finance", "Équipe & paramètres", "Catalogue"].includes(x),
    )
  )
    throw Error("Unauthorized staff navigation");
  checks.push("Warehouse permissions restrict desktop navigation");
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Points & cadeaux", exact: true })
    .click();
  await page.waitForTimeout(1000);
  if ((await page.locator(".error-banner").allTextContents()).length)
    throw Error("Warehouse gifts page error");
  checks.push("Warehouse gifts are usable without finance or wallet access");
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Livraisons", exact: true })
    .click();
  await page.waitForTimeout(500);
  if (
    await page
      .getByRole("button", { name: "Planifier une tournée", exact: true })
      .count()
  )
    throw Error("Warehouse dispatch control exposed");
  checks.push("Warehouse can view custody but cannot plan dispatch");
  await page.screenshot({
    path: reportDir + "/desktop-staff-identity.png",
    fullPage: false,
  });
  await page.reload();
  await page
    .getByRole("heading", { name: "La livraison, sous contrôle." })
    .waitFor();
  checks.push("Reload restored authenticated staff session");
} catch (e) {
  checks.push("FAIL: " + e.message);
  checks.push("At " + new URL(page.url()).origin + new URL(page.url()).pathname);
  checks.push((await page.locator("body").innerText()).slice(0, 1600));
  await page.screenshot({ path: reportDir + "/identity-failure.png" });
  process.exitCode = 1;
} finally {
  await writeFile(
    reportDir + "/browser-identity.json",
    JSON.stringify(
      { at: new Date().toISOString(), base, checks, javascriptErrors, consoleDiagnostics:errors },
      null,
      2,
    ),
  );
  console.log(JSON.stringify({ checks, javascriptErrors, consoleDiagnostics:errors }));
if(javascriptErrors.length)process.exitCode=1;
  await browser.close();
}
