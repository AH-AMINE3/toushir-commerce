import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright";
const base = process.env.WEB_URL || "http://127.0.0.1:5864";
const dir = process.env.REPORT_DIR || ".impeccable/review/expanded";
await fs.mkdir(dir, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.CHROME_EXECUTABLE,
  args: [
    "--no-sandbox",
    "--disable-dev-shm-usage",
    "--enable-webgl",
    "--use-angle=swiftshader",
  ],
});
const ctx = await browser.newContext({
  viewport: { width: 1600, height: 1040 },
  reducedMotion: "reduce",
  ...(process.env.BROWSER_STATE
    ? { storageState: process.env.BROWSER_STATE }
    : {}),
});
const w = await ctx.newPage(),
  m = await ctx.newPage();
await m.setViewportSize({ width: 402, height: 874 });
const checks = [],
  failures = [],
  errors = [],
  captures = [];
for (const p of [w, m]) {
  p.setDefaultTimeout(15000);
  p.on("pageerror", (e) => errors.push(e.message));
}
const btn = (p, name) => p.getByRole("button", { name, exact: true });
async function shot(p, name, fromTop = true) {
  if (await p.getByRole("alert").count()) {
    await p
      .getByRole("alert")
      .last()
      .click()
      .catch(() => {});
  }
  if (fromTop)
    await p.evaluate(() => {
      window.scrollTo(0, 0);
      for (const el of document.querySelectorAll("*"))
        if (el.scrollTop > 0) el.scrollTop = 0;
    });
  await p.waitForTimeout(300);
  const file = path.join(dir, name + ".png");
  await p.screenshot({ path: file, fullPage: fromTop, animations: "disabled" });
  const bytes = await fs.readFile(file);
  assert.equal(bytes.subarray(1, 4).toString(), "PNG");
  const width = bytes.readUInt32BE(16),
    height = bytes.readUInt32BE(20);
  assert.equal(width, p.viewportSize().width);
  assert(height >= p.viewportSize().height);
  captures.push({ file, width, height });
}
async function step(name, f) {
  if (
    process.env.TEST_GROUPS &&
    !new RegExp(process.env.TEST_GROUPS).test(name)
  )
    return;
  try {
    await f();
    checks.push(name);
    console.log("PASS " + name);
  } catch (e) {
    failures.push({ name, error: e.message });
    console.log("FAIL " + name + ": " + e.message);
    await fs.writeFile(
      path.join(dir, "failure-" + failures.length + "-web.txt"),
      await w.locator("body").innerText(),
    );
    await fs.writeFile(
      path.join(dir, "failure-" + failures.length + "-merchant.txt"),
      await m.locator("body").innerText(),
    );
    await shot(m, "failure-" + failures.length).catch(() => {});
  }
}
async function nav(name) {
  if (await btn(w, "Fermer le dossier").isVisible())
    await btn(w, "Fermer le dossier").click();
  await w
    .getByRole("navigation")
    .getByRole("button", { name, exact: false })
    .click();
  await w.waitForTimeout(500);
}
async function posted(p, url, action) {
  const pending = p.waitForResponse(
    (r) =>
      new URL(r.url()).pathname === "/api" + url &&
      r.request().method() === "POST",
  );
  await action();
  const r = await pending;
  assert(r.ok(), url + " " + r.status());
  return r.json();
}
let created, admin, workspace, merchantSession;
async function api(url, token, body, method) {
  const r = await ctx.request.fetch(base + "/api" + url, {
    method: method || (body ? "POST" : "GET"),
    headers: token ? { Authorization: "Bearer " + token } : {},
    ...(body ? { data: body } : {}),
  });
  if (r.status() === 429) {
    await new Promise((resolve) =>
      setTimeout(
        resolve,
        1000 * Math.min(60, Number(r.headers()["retry-after"]) || 5),
      ),
    );
    return api(url, token, body, method);
  }
  const j = await r.json();
  assert(r.ok(), url + ": " + (j.error?.message || r.status()));
  return j;
}
let privateFixture;
if (process.env.PRIVATE_FIXTURE) {
  try {
    privateFixture = JSON.parse(
      await fs.readFile(process.env.PRIVATE_FIXTURE, "utf8"),
    );
    if (privateFixture.base !== base) privateFixture = null;
  } catch {}
}
const adminFixture =
  privateFixture?.admin ||
  (await api("/demo/session", null, { role: "ADMIN" }));
admin = adminFixture.token;
workspace = adminFixture.workspaceToken;
merchantSession =
  privateFixture?.merchant ||
  (await api("/demo/session", admin, {
    role: "MERCHANT",
    workspaceToken: workspace,
  }));
if (process.env.PRIVATE_FIXTURE) {
  await fs.mkdir(path.dirname(process.env.PRIVATE_FIXTURE), {
    recursive: true,
  });
  await fs.writeFile(
    process.env.PRIVATE_FIXTURE,
    JSON.stringify({ base, admin: adminFixture, merchant: merchantSession }),
    { mode: 0o600 },
  );
}
await api("/demo/showcase", admin, {});
await api("/admin/demo/scenario", admin, {});
await ctx.addInitScript(
  ({ a, m, cap }) => {
    localStorage.setItem("toushir-demo-workspace", cap);
    if (location.pathname.startsWith("/mobile/merchant"))
      sessionStorage.setItem("toushir-token", m);
    else sessionStorage.setItem("toushir-web-token", a);
  },
  { a: admin, m: merchantSession.token, cap: workspace },
);
try {
  if (process.env.TEST_GROUPS) {
    await w.goto(base);
    await w
      .getByRole("heading", { name: "Votre activité, en clair.", exact: true })
      .waitFor({ timeout: 60000 });
  }
  await step("Actual wholesale dashboard loads seeded records", async () => {
    await w.goto(base);
    await w
      .getByRole("heading", { name: "Votre activité, en clair.", exact: true })
      .waitFor({ timeout: 60000 });
    await w.waitForFunction(
      () =>
        document
          .querySelector(".workspace-actions")
          ?.getAttribute("aria-busy") === "false",
    );
    admin = await w.evaluate(() => sessionStorage.getItem("toushir-web-token"));
    workspace = await w.evaluate(() =>
      localStorage.getItem("toushir-demo-workspace"),
    );
    await shot(w, "desktop-overview");
    await btn(w, "Encaissements").click();
    await btn(w, "Demandes").click();
    const slider = w.getByRole("slider", {
      name: "Explorer les jours",
      exact: true,
    });
    await slider.fill("10");
    await slider.press("ArrowRight");
    await shot(w, "desktop-overview-chart");
  });
  await step(
    "Merchant catalogue shares the wholesale workspace and positive demo prices",
    async () => {
      await m.goto(base + "/mobile/merchant");
      await m
        .getByRole("textbox", {
          name: "Rechercher un produit, une marque ou un code-barres",
          exact: true,
        })
        .waitFor({ timeout: 60000 });
      await m
        .getByRole("button", { name: /^Ajouter .+ au panier$/ })
        .first()
        .waitFor();
      assert.equal(
        await m.evaluate(() => localStorage.getItem("toushir-demo-workspace")),
        workspace,
      );
      await m.getByText("365 DA", { exact: true }).first().waitFor();
      await shot(m, "merchant-catalog");
      assert.equal(
        (await api("/auth/me", merchantSession.token)).workspace.id,
        (await api("/auth/me", admin)).workspace.id,
      );
    },
  );
  await step(
    "Full category grid, semantic icons and category selection work",
    async () => {
      await m
        .getByText("Vos rayons", { exact: true })
        .locator("..")
        .getByRole("button", { name: "Tout voir", exact: true })
        .click();
      await m
        .getByRole("textbox", { name: "Rechercher un rayon", exact: true })
        .waitFor();
      assert(
        (await m.getByRole("button", { name: /produits$/ }).count()) >= 13,
      );
      await shot(m, "merchant-categories");
      await m.getByRole("button", { name: /^Eaux, / }).click();
      await m.getByText(/74 produits/).waitFor();
      await shot(m, "merchant-category-products");
      await btn(m, "Effacer les filtres").click();
    },
  );
  await step(
    "Brand directory opens, searches and filters actual products",
    async () => {
      await m
        .getByText("Vos marques", { exact: true })
        .locator("..")
        .getByRole("button", { name: "Tout voir", exact: true })
        .click();
      await m
        .getByRole("textbox", { name: "Rechercher une marque", exact: true })
        .fill("Ifri");
      await shot(m, "merchant-brands");
      await m.getByText("Ifri", { exact: true }).first().click();
      await btn(m, "Effacer les filtres").waitFor();
      await btn(m, "Effacer les filtres").click();
    },
  );
  await step("Product filters and exact barcode lookup work", async () => {
    await m.goto(base + "/mobile/merchant#catalog");
    await btn(m, "Filtrer et trier les produits").click();
    await m.getByText("Trier les résultats", { exact: true }).waitFor();
    await shot(m, "merchant-filters");
    await btn(m, "Fermer la découverte").click();
    await btn(m, "Rechercher un produit par code-barres").click();
    await m
      .getByRole("textbox", { name: "Code-barres du produit", exact: true })
      .fill("6130093010069");
    await shot(m, "merchant-barcode");
    await btn(m, "Rechercher ce code-barres").click();
    await m
      .getByText("Eau minérale naturelle", { exact: true })
      .first()
      .waitFor();
    await shot(m, "merchant-barcode-result");
    await btn(m, "Voir ce produit").click();
    await btn(m, "Fermer la fiche produit").click();
  });
  await step(
    "Direct checkout submits priced order and shows confirmation in merchant app",
    async () => {
      await m.goto(base + "/mobile/merchant#catalog");
      const add = m
        .getByRole("button", { name: /^Ajouter .+ au panier$/ })
        .first();
      await add.waitFor();
      const saved = m.waitForResponse(
        (r) =>
          new URL(r.url()).pathname === "/api/cart" &&
          r.request().method() === "PUT",
      );
      await add.click();
      assert((await saved).ok());
      await m.getByRole("button", { name: /^Voir le panier,/ }).click();
      await btn(m, "Confirmer la commande").waitFor();
      await shot(m, "merchant-checkout");
      created = await posted(m, "/orders", () =>
        btn(m, "Confirmer la commande").click(),
      );
      await m.getByText("Votre commande est reçue.", { exact: true }).waitFor();
      assert(created.total > 0);
      await shot(m, "merchant-order-success");
      await m.getByText("Total transmis", { exact: true }).waitFor();
      assert.equal(
        await m.getByText("Total confirmé", { exact: true }).count(),
        0,
      );
    },
  );
  await step(
    "The exact mobile order is visible in wholesaler operations",
    async () => {
      assert(created?.number, "Direct checkout must have completed");
      await nav("Commandes");
      assert(
        (await api("/admin/orders?pageSize=100", admin)).items.some(
          (x) => x.id === created.id,
        ),
        "Exact mobile order missing from admin API",
      );
      await w
        .getByText(created.number, { exact: true })
        .first()
        .waitFor({ timeout: 35000 });
      await w.getByText(created.number, { exact: true }).first().click();
      await w.getByText(created.number, { exact: true }).first().waitFor();
      await shot(w, "desktop-mobile-order");
    },
  );
  await step(
    "Merchant account card, actual 7/30/90 day activity, measures and bar drilldown work",
    async () => {
      if (await btn(m, "Retour aux commandes").isVisible())
        await btn(m, "Retour aux commandes").click();
      await m.getByRole("tab", { name: "Mon Toushir", exact: true }).click();
      await m.getByRole("button", { name: /^Ma carte Toushir/ }).waitFor();
      await shot(m, "merchant-account");
      await btn(m, "Mon activité").click();
      await btn(m, "Actualiser mon activité").waitFor();
      await shot(m, "merchant-activity-summary");
      for (const d of [7, 30, 90]) {
        const req = m.waitForResponse(
          (r) =>
            r.url().includes("/api/profile/activity?days=" + d) &&
            r.status() === 200,
        );
        await btn(m, d + " jours").click();
        const data = await (await req).json();
        assert.equal(data.period.days, d);
      }
      await btn(m, "Explorer mon activité").click();
      await btn(m, "Encaissé net").click();
      await btn(m, "Commandé").click();
      await m
        .getByRole("button", { name: /commandes$/ })
        .first()
        .click();
      await shot(m, "merchant-activity");
      await m
        .locator('[aria-label="Évolution des commandes sur 90 jours"]')
        .evaluate((el) => el.scrollIntoView({ block: "center" }));
      await shot(m, "merchant-activity-chart", false);
    },
  );
  await step(
    "All campaign mechanisms and actual gift catalogue render",
    async () => {
      await btn(m, "Retour à Mon Toushir").click();
      await m.getByRole("tab", { name: "Offres", exact: true }).click();
      await m.getByText("Remises", { exact: true }).waitFor();
      await shot(m, "merchant-offers");
      for (const kind of ["Remises", "Cadeaux", "Challenges"]) {
        await btn(m, kind).click();
        await m.waitForTimeout(200);
        await shot(
          m,
          "merchant-offers-" +
            kind
              .toLowerCase()
              .normalize("NFD")
              .replace(/[\u0300-\u036f]/g, ""),
        );
      }
      await m.getByRole("tab", { name: "Mon Toushir", exact: true }).click();
      await btn(m, "Mes cadeaux").click();
      await m.getByText("Actualiser mes points", { exact: true }).waitFor();
      await m.getByText("Écouteurs sans fil · démo", { exact: true }).waitFor();
      await shot(m, "merchant-gifts");
    },
  );
  await step(
    "Merchant orders and real preparation tracking remain connected",
    async () => {
      await m.getByRole("tab", { name: "Commandes", exact: true }).click();
      await m
        .getByRole("button", { name: /^Ouvrir / })
        .first()
        .waitFor();
      await shot(m, "merchant-orders");
      const list = await api("/orders?pageSize=100", merchantSession.token);
      const ready = list.items.find((x) => x.status === "READY");
      assert(ready, "A ready scenario exists");
      await m
        .getByRole("button", { name: "Ouvrir " + ready.number, exact: true })
        .click();
      await m.getByText("Mes produits", { exact: true }).waitFor();
      await shot(m, "merchant-tracking");
    },
  );
  await step(
    "Analytics has real periods, metric switching, keyboard graph and custom dates",
    async () => {
      await nav("Analyses");
      for (const n of [7, 30, 90]) {
        const r = w.waitForResponse(
          (r) =>
            r.url().includes("/api/admin/analytics?days=" + n) &&
            r.status() === 200,
        );
        await btn(w, n + " jours").click();
        await r;
      }
      await w.getByRole("button", { name: /^Encaissements nets/ }).click();
      await w.getByRole("button", { name: /^Demandes reçues/ }).click();
      await w
        .getByRole("slider", { name: "Explorer les jours", exact: true })
        .fill("12");
      await w.getByLabel("Du", { exact: true }).fill("2026-10-01");
      await w.getByLabel("Au", { exact: true }).fill("2026-10-17");
      const r = w.waitForResponse(
        (r) =>
          r.url().includes("/api/admin/analytics?from=2026-10-01") &&
          r.status() === 200,
      );
      await btn(w, "Appliquer").click();
      await r;
      await shot(w, "desktop-analytics");
      await w
        .getByRole("button", { name: "Activer le thème sombre", exact: true })
        .click();
      await shot(w, "desktop-dark");
      await w
        .getByRole("button", { name: "Activer le thème clair", exact: true })
        .click();
    },
  );
  await step(
    "Shops filter by actual region, name or phone and export the filtered list",
    async () => {
      await nav("Magasins");
      const filter = w.getByRole("textbox", {
        name: "Rechercher un magasin par nom ou téléphone",
        exact: true,
      });
      await filter.waitFor();
      const shops = await api("/admin/merchants?pageSize=100", admin);
      const s = shops.items.find((x) => x.region);
      assert(s);
      await w
        .getByRole("combobox", { name: "Région des magasins", exact: true })
        .selectOption(s.region);
      await filter.fill(s.shopName.slice(0, 10));
      const dl = w.waitForEvent("download");
      await w.getByRole("button", { name: /^Exporter la liste/ }).click();
      const download = await dl;
      const csv = await fs.readFile(await download.path(), "utf8");
      assert(csv.includes(s.shopName));
      await shot(w, "desktop-stores");
      await filter.fill("NOT_A_REAL_SHOP_69757");
      assert.equal(await w.locator("tbody tr").count(), 0);
      await filter.fill("");
    },
  );
  await step(
    "Campaign editor exposes explicit regional audience targeting",
    async () => {
      await nav("Campagnes");
      await btn(w, "Nouvelle campagne").click();
      await w.getByLabel("Liste de magasins ciblés", { exact: true }).check();
      await w
        .getByRole("heading", {
          name: "Les magasins de cette campagne",
          exact: true,
        })
        .waitFor();
      await w.locator(".audience-selector select").selectOption({ index: 1 });
      await w
        .getByRole("button", {
          name: /^Sélectionner les .* magasins affichés$/,
        })
        .click();
      await shot(w, "desktop-campaign-audience");
    },
  );
  await step(
    "Delivery loads attributed 3D truck model and explicit illustrative replay controls",
    async () => {
      await nav("Livraisons");
      await w
        .locator('[data-vehicle-model="loaded"]')
        .waitFor({ timeout: 30000 });
      await btn(w, "Voir le mouvement illustratif").click();
      await btn(w, "Arrêter l’animation").waitFor();
      assert.equal(
        await w.evaluate(
          () => matchMedia("(prefers-reduced-motion: reduce)").matches,
        ),
        true,
      );
      await shot(w, "desktop-delivery-3d");
      await btn(w, "Arrêter l’animation").click();
      await btn(w, "Carte GPS").click();
      await shot(w, "desktop-delivery-gps");
    },
  );
  await step(
    "Wholesaler responsive overview renders at 390px without page overflow",
    async () => {
      await nav("Vue d’ensemble");
      await w.setViewportSize({ width: 390, height: 844 });
      await shot(w, "wholesale-mobile");
      assert(
        await w.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
      );
      await w.setViewportSize({ width: 1600, height: 1040 });
    },
  );
  await step(
    "Live work screen runs merchant and wholesale in two actual shared-workspace frames",
    async () => {
      const hub = await ctx.newPage();
      await hub.goto(base + "/live-work.html");
      await hub.waitForTimeout(3500);
      const frames = hub.frames().filter((f) => f !== hub.mainFrame());
      assert.equal(frames.length, 2);
      assert(frames.some((f) => f.url().includes("/mobile/merchant")));
      assert(frames.some((f) => f.url() === base + "/"));
      await shot(hub, "live-work-hub");
      await hub.close();
    },
  );
} finally {
  await fs.writeFile(
    path.join(dir, (process.env.REPORT_NAME || "expanded-browser") + ".json"),
    JSON.stringify(
      {
        at: new Date().toISOString(),
        base,
        checks,
        failures,
        errors,
        captures,
      },
      null,
      2,
    ),
  );
  console.log(
    JSON.stringify({
      passed: checks.length,
      failed: failures.length,
      errors,
      captures: captures.length,
    }),
  );
  await browser.close();
  if (failures.length || errors.length) process.exitCode = 1;
}
