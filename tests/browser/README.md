# Browser verification

Install with `npm ci` and `npx playwright install chromium`, then set `WEB_URL` to the explicitly enabled demo environment. Use `npm run smoke`, `npm run identity`, `npm run business`, `npm run warehouse` and `npm run map` individually. Reports/screenshots default to `reports/`; set `REPORT_DIR` to change it. Optional `CHROME_EXECUTABLE` selects an installed Chrome binary. Otherwise Playwright uses its managed Chromium.

Suites create isolated test records and retain credentials only in memory. They must not target an unapproved commercial workspace. Initial unauthenticated requests and logout checks intentionally receive 401 responses. The identity report distinguishes browser console HTTP diagnostics from JavaScript exceptions; evaluate the named assertions as well.

Public demo creation is deliberately rate limited. Run suites with appropriate spacing or against a fresh local API process; do not weaken the public limit for tests.
