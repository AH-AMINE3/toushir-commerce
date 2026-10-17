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
const errors = [],
  failed = [],
  requests = [];
page.on("pageerror", (e) => errors.push(e.message));
page.on("requestfailed", (r) =>
  failed.push({ path: r.url().split("?")[0], error: r.failure()?.errorText }),
);
page.on("response", (r) => {
  if (r.url().includes("openfreemap"))
    requests.push({ path: r.url().split("?")[0], status: r.status() });
});
try {
  await page.goto(process.env.WEB_URL || "http://127.0.0.1:5860", {
    waitUntil: "domcontentloaded",
  });
  await page
    .getByRole("heading", { name: "Votre activité, en clair." })
    .waitFor({ timeout: 60000 });
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Livraisons", exact: true })
    .click();
  await page.getByRole("button", { name: "Carte GPS", exact: true }).click();
  await page.waitForTimeout(12000);
  await page.screenshot({ path: reportDir + "/desktop-gps.png" });
  console.log(
    JSON.stringify({
      errors,
      failed,
      requests,
      canvases: await page.locator(".gps-map canvas").count(),
      status: await page.locator(".gps-error").allTextContents(),
    }),
  );
  await writeFile(
    reportDir + "/map-check.json",
    JSON.stringify(
      { at: new Date().toISOString(), errors, failed, requests },
      null,
      2,
    ),
  );
if(errors.length || !(await page.locator(".gps-map canvas").count()))throw new Error("Map canvas did not initialize cleanly");
} finally {
  await browser.close();
}
