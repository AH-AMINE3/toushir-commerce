# Verification

Use Node 24, .NET SDK 10 and Python 3. Build/start with `bash scripts/build-all.sh` and `bash scripts/run-local.sh`. API suites need an explicitly enabled demo server and create isolated workspaces.

```sh
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-api.mjs
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-operations.mjs
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-routes.mjs
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-oidc.mjs
python3 scripts/test-engagement.py http://127.0.0.1:5860
python3 scripts/test-warehouse.py http://127.0.0.1:5860
```

`VERIFICATION_REPORT` redirects each Node report; `TOUSHIR_TEST_RESULTS` redirects the Python report. Reports omit credentials/capabilities and record actual assertions, URL and time.

| Suite | Purpose |
| --- | --- |
| Business API | Quote revisions, concurrent reservations, receipt, claims, credits and refunds |
| Operations | Six roles, scoped live updates, preparation, assignment, GPS, QR replay and collection |
| Routes | Capacity, ordered manifest, atomic custody, depot returns, float and cash variance |
| OIDC | Discovery/JWKS, exact redirects, S256, antiforgery login, replay, signed tokens, refresh/revocation |
| Engagement | Supplier receipts/inspection, messages/consent, promotions and points/reward concurrency |
| Warehouse | Explicit counted lots, FEFO, scan waves, concurrent last-unit reservation, quarantine and identified physical returns |
| Production | Explicit owner, real restart and backup restoration, isolated PostgreSQL migration SQL, static routing, distribution and rate limits |

Browser evidence checks navigation, real forms, secure sign-in, errors, responsive and dark layouts. Native evidence records variant, API origin, build, device/simulator and executed interactions. Hosted tests must run on the final HTTPS origin and persistent database. Compilation is not native execution; local passes are not hosted passes.

Figma acceptance is separate: the screen matrix and derived 122 checks retain unverified states and external evidence requirements. Source presence alone does not accept an original requirement.
