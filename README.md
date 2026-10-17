# TOUSHIR Commerce

One .NET 10 / ABP backend connects the wholesaler workspace, merchant app and courier app. The implementation follows the supplied TOUSHIR Figma identity. Preparation, custody, customer receipt and actual payment are independent, audited operations.

## Applications

| Application | Local URL | Source |
| --- | --- | --- |
| Wholesaler, warehouse, dispatch and finance | http://localhost:5860/ | `apps/web` — React, TypeScript |
| Merchant browser preview | http://localhost:5860/mobile/merchant | `apps/mobile` — React Native, Expo |
| Courier browser preview | http://localhost:5860/mobile/driver | `apps/mobile` — separate native identity |
| API health | http://localhost:5860/api/health | `backend` — .NET 10, ABP 10.6.1 |
| Identity discovery | http://localhost:5860/.well-known/openid-configuration | OpenIddict, code + S256 PKCE |

Native apps have separate package IDs, icons and callback schemes. Browser previews use the same React Native source and backend. See [native distribution](docs/NATIVE-DISTRIBUTION.md).

## Start locally

Install .NET SDK 10 and Node.js 24. From this repository:

```sh
bash scripts/build-all.sh
bash scripts/run-local.sh
```

The launcher explicitly enables a demo and preserves SQLite records under `work/local-data`. Each browser creates an isolated fictional workspace. The wholesaler demo creates an example history through the business services; prices, stock and operations are labelled fictitious. Open the merchant/courier links in the same browser to explore that workspace, or create a merchant invitation from **Magasins** for another tester. Keep the workspace owner capability private.

For development, run the API and `npm run dev` in `apps/web`. Its proxy targets port 5860. Use `npm run web` in `apps/mobile` for the mobile development preview. A physical device needs a reachable HTTPS API; emulator configuration is documented separately.

## Durable installation

`compose.yaml` starts PostgreSQL 17 and the application, with separate persistent volumes for records and identity keys. Copy `.env.example` to `.env`, replace every placeholder, configure an HTTPS public origin/reverse proxy, then run `docker compose up --build -d`.

Business mode disables anonymous demos and creates the configured owner. Sign in through the professional authorization page, create the team, approve merchants, and enter actual prices, stock and delivery terms. Remove bootstrap secrets after setup. See [operations](backend/OPERATIONS.md).

## Connected workflows

- Approval, sourced catalogue, search, favourites, saved basket and addresses.
- Quote requests, explicit promotion selection, versioned prices/expiry, merchant acceptance, atomic reservation, per-line preparation and assignment.
- Ordered manifests, commercial-unit vehicle capacity, custody QR, permitted GPS, customer quantity proof, partial refusal and depot returns.
- Actual cash, tendered amount/change, route float, driver declaration, independent finance count/reconciliation. Shortages never recreate customer debt.
- Claims, quarantine, inspection, commercial credits and separately recorded refunds.
- Supplier approval by another account, partial receipts, lot/expiry capture, quality control and invoice comparison.
- Contextual in-app conversations, private notes, consent-filtered in-app broadcasts, approved campaigns/challenges, immutable loyalty entries and stock-backed gifts.
- Interactive Three.js depot and a separate MapLibre view of actual GPS observations. The depot is illustrative, not a camera feed.

The API enforces ADMIN, MERCHANT, DRIVER, WAREHOUSE, DISPATCHER and FINANCE permissions. SignalR invalidations and polling refresh persisted state across interfaces.

## Evidence and source

See [verification](docs/VERIFICATION.md), [architecture](docs/ARCHITECTURE.md), [engagement contracts](docs/Figma-ENGAGEMENT-API.md), [release boundaries](docs/RELEASE.md) and [catalogue provenance](catalog/README.md).

The [Figma audit](docs/Figma-AUDIT.md), [screen matrix](docs/Figma-SCREEN-MATRIX.csv) and [implementation review](docs/Figma-IMPLEMENTATION-REVIEW.md) retain source evidence and acceptance gaps. The referenced original 79-page specification was not supplied; the 122 checks are derived, not a claim of original requirement acceptance. Local, browser, native and hosted results are recorded separately.

Open product identity is not real wholesale availability. Commercial documents are quote/order records. Fiscal invoicing and external messaging, bank or carrier services require actual business/provider configuration.
