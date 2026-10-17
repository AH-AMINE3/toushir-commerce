import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright";
const base = process.env.WEB_URL || "http://127.0.0.1:5865",
  dir = process.env.REPORT_DIR || ".impeccable/review/expanded",
  f = JSON.parse(await fs.readFile(process.env.PRIVATE_FIXTURE, "utf8"));
assert.equal(f.base, base);
const b = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});
const c = await b.newContext({
  viewport: { width: 402, height: 874 },
  reducedMotion: "reduce",
});
await c.addInitScript(
  ({ token, cap }) => {
    sessionStorage.setItem("toushir-token", token);
    localStorage.setItem("toushir-demo-workspace", cap);
  },
  { token: f.merchant.token, cap: f.admin.workspaceToken },
);
const p = await c.newPage(),
  checks = [],
  errors = [];
p.on("pageerror", (e) => errors.push(e.message));
p.setDefaultTimeout(20000);
async function shot(name, top = true) {
  if (top)
    await p.evaluate(() => {
      window.scrollTo(0, 0);
      for (const e of document.querySelectorAll("*"))
        if (e.scrollTop > 0) e.scrollTop = 0;
    });
  await p.screenshot({
    path: path.join(dir, name + ".png"),
    animations: "disabled",
    fullPage: top,
  });
}
try {
  await p.goto(base + "/mobile/merchant");
  const add = p.getByRole("button", { name: /^Ajouter .+ au panier$/ }).first();
  await add.waitFor({ timeout: 60000 });
  const box = await add.boundingBox(),
    nav = await p
      .getByRole("tab", { name: "Accueil", exact: true })
      .boundingBox();
  assert(
    box.y + box.height <= nav.y,
    "First product add action must end above bottom navigation",
  );
  assert(
    box.height >= 44 && box.width >= 44,
    "First add target must be at least44px",
  );
  const price = await p
    .getByText("365 DA", { exact: true })
    .first()
    .boundingBox();
  assert(
    price && price.y + price.height <= nav.y,
    "First product price must appear above bottom navigation",
  );
  await p.getByText("Votre commande est reçue", { exact: true }).waitFor();
  await shot("merchant-catalog");
  await shot("merchant-home-active");
  checks.push(
    "At402x874 with an active received order, first product price and44px add action appear above bottom navigation",
  );
  await p
    .getByRole("button", { name: "Filtrer les produits", exact: true })
    .click();
  const input = p.getByRole("textbox", {
    name: "Trouver une marque",
    exact: true,
  });
  const ib = await input.boundingBox(),
    rb = await p
      .getByRole("radio", { name: /Toutes les marques/ })
      .boundingBox();
  assert.equal(ib.height, 48);
  assert(
    rb.y >= ib.y + ib.height,
    "Brand choices must follow, without covering, the48px input",
  );
  await input.fill("Ifri");
  await shot("merchant-filters");
  await p
    .getByRole("button", { name: "Fermer la découverte", exact: true })
    .click();
  checks.push(
    "Brand filter input is48px high and the non-growing rail does not overlap it",
  );
  const saved = p.waitForResponse(
    (r) => r.url().endsWith("/api/cart") && r.request().method() === "PUT",
  );
  await add.click();
  assert((await saved).ok());
  await p.getByRole("button", { name: "Voir le panier", exact: true }).click();
  const pending = p.waitForResponse(
    (r) => r.url().endsWith("/api/orders") && r.request().method() === "POST",
  );
  await p
    .getByRole("button", { name: "Confirmer la commande", exact: true })
    .click();
  const response = await pending;
  assert(response.ok());
  const order = await response.json();
  assert.equal(order.status, "SUBMITTED");
  await p.getByText("Reçue", { exact: true }).first().waitFor();
  await p
    .getByText(
      "Votre grossiste a reçu la commande. Sa confirmation est attendue avant la préparation.",
      { exact: true },
    )
    .waitFor();
  await shot("merchant-order-success");
  checks.push(
    "An actual SUBMITTED checkout reads Reçue and explicitly awaits wholesaler confirmation",
  );
  await p
    .getByRole("button", { name: "Retour aux commandes", exact: true })
    .click();
  await p.getByRole("tab", { name: "Mon Toushir", exact: true }).click();
  await p.getByRole("button", { name: "Mon activité", exact: true }).click();
  await p.getByRole("button", { name: "90 jours", exact: true }).click();
  await p.getByRole("button", { name: "Encaissé net", exact: true }).click();
  await p
    .locator('[aria-label="Évolution des encaissements sur 90 jours"]')
    .evaluate((el) => el.scrollIntoView({ block: "center" }));
  if (await p.getByRole("alert").count())
    await p.getByRole("alert").last().click();
  await shot("merchant-activity-payments-chart", false);
  checks.push(
    "Actual90-day net-payments chart is visible in its scrolled app viewport",
  );
  console.log(JSON.stringify({ passed: checks.length, errors }));
} catch (e) {
  checks.push("FAIL: " + e.message);
  console.log(e.message);
  await shot("review-regression-failure");
  process.exitCode = 1;
} finally {
  await fs.writeFile(
    path.join(dir, "expanded-review-regression.json"),
    JSON.stringify(
      { at: new Date().toISOString(), base, checks, errors },
      null,
      2,
    ),
  );
  if (errors.length) process.exitCode = 1;
  await b.close();
}
