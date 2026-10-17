# Verification

Hosted evidence is under `verification/cloud-*.json` plus `backend/hosted-scenario-verification.json`. The concurrent-workspace regression proves one visitor can save a basket and bootstrap its courier while another history transaction is active. The persistence probe records an existing merchant session, accepted order, actual payment and unchanged signing keys across a confirmed Render deployment. Each report retains its actual URL and execution time; earlier suites are not silently relabelled as runs of later source revisions.

Use Node 24, .NET SDK 10 and Python 3. Build/start with `bash scripts/build-all.sh` and `bash scripts/run-local.sh`. API suites need an explicitly enabled demo server and create isolated workspaces.

```sh
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-api.mjs
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-operations.mjs
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-routes.mjs
API_URL=http://127.0.0.1:5860 node backend/scripts/verify-oidc.mjs
python3 scripts/test-engagement.py http://127.0.0.1:5860
python3 scripts/test-warehouse.py http://127.0.0.1:5860
python3 scripts/test-supplier-documents.py http://127.0.0.1:5860
# Pricing suite requires a separately started test API with Wholesale__DeliveryFee=0.
python3 scripts/test-pricing.py http://127.0.0.1:5860
python3 scripts/test-shop-members.py http://127.0.0.1:5860
python3 scripts/test-crm.py http://127.0.0.1:5860
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
| Pricing | Independent customer tariffs, protected reviewed-basket consent, actual direct checkout, frozen quote history, campaign compatibility and centime rounding |
| Shop membership | Actual-person/shop separation, personal carts/favorites, bounded single-use invitations, role hierarchy, shared commerce, primary invitation inheritance and session/receipt/live revocation |
| CRM | Named portfolio scope, versioned declared tasks/visits, exact one-use merchant authority, shared RFQ/price engine and authority-loss recovery |
| Production | Explicit owner, real restart and backup restoration, isolated PostgreSQL migration SQL, static routing, distribution and rate limits |

Browser evidence checks navigation, real forms, secure sign-in, errors, responsive and dark layouts. Native evidence records variant, API origin, build, device/simulator and executed interactions. Hosted tests must run on the final HTTPS origin and persistent database. Compilation is not native execution; local passes are not hosted passes.

Figma acceptance is separate: the screen matrix and derived 122 checks retain unverified states and external evidence requirements. Source presence alone does not accept an original requirement.

## Merchant account finish

The added merchant summary is tested for unauthenticated/role denial, owner/workspace isolation, actual collections minus refunds, and separate partial delivery counts. Browser checks exercise the points card, activity-to-orders navigation, account address form and communication consent after the Figma finish. The catalogue audit decodes all2,500 local photographs and verifies source hashes and provenance; it does not certify community label correctness or local wholesale availability.

Supplier document/confirmation evidence and browser captures are under `verification/supplier/`. The suite checks retained private PDFs, independently reviewed terms, late evidence, exact receipt documents, invoice reference/amount comparison and explicit cancellation/short close. The upgrade report distinguishes actual SQLite migration on a backup from PostgreSQL generated SQL; each report retains its own execution scope. Native simulators are deliberately stopped on the 8 GB development machine; current UI acceptance uses browser previews.

Shop team evidence is under `verification/shop-members/`. The separate actual WebSocket/OIDC verifier requires the private fixture produced by the API suite and checks revoked opaque/signed/refresh sessions, password sign-in, live recipients and receipt issuer validity. See [the test contract](SHOP-MEMBERS.md). Browser failures use real backend rejections and assert the message and retry action remain in the submitted viewport. Domain, upgrade, production restore and regression reports retain their individual scopes. Android 1.1.0 packages predate this UI addition.

CRM evidence is under `verification/crm/`; [CRM.md](CRM.md) records the authority and test limits. Browser reassignment is exercised without reloading and with a delayed former authorized response. Reports distinguish local execution, generated PostgreSQL SQL, executed backup upgrade and later hosted execution. The broader requested frontend/workflow redesign is subsequent work and is not implied by this CRM record.

## Expanded connected-commerce pass ·17 October 2026

Actual latest business API role-play:267/267 cases,711 HTTP requests, across six suites. The requested50+50 matrix contains63 wholesaler-side and69 merchant cases; additional suites cover audience/activity, immutable order amendments, recalls and warehouse regression. `verification/journey/latest-guard-summary.json` records each suite and hashes of the two independently reproduced recall fixes. Historical failing witnesses remain explicitly labelled before-fix, not as current failures. The new showcase suite passed12 checks/25 HTTP for explicit fictional pricing, isolation, owner-only setup, three campaign mechanics, three gifts and idempotent seeding.

Engineering verification is recorded independently in `verification/expanded/engineering.json`:352 executed checks across15 groups, including92 domain invariants, pricing/engagement/warehouse regressions,11 production/restore checks and1,303HTTP,7 financial analytics checks/68 HTTP, and5 provider/upgrade checks. These totals overlap business suites and repeats; do not add them as unique acceptance requirements. The copied SQLite upgrade preserved all original column values across59 business tables including1,142 saved orders. Both provider models matched; configured alternate-schema PostgreSQL SQL was nondestructive.

The browser and hosted reports retain their own scope and execution timestamps. The final static-server change explicitly serves GLB/GLTF types; the actual 369,980-byte truck returned200 with model/gltf-binary after restart. Full browser render, physical GPS, native execution and exact 265-state Figma acceptance remain distinct evidence categories.
