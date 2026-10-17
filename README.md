# TOUSHIR Commerce

One .NET 10 / ABP backend connects the wholesaler workspace, merchant app and courier app. The implementation follows the supplied TOUSHIR Figma identity. Preparation, custody, customer receipt and actual payment are independent, audited operations.

## Applications

| Application | Local URL | Source |
| --- | --- | --- |
| Wholesaler, warehouse, dispatch and finance | http://localhost:5860/ | `apps/web` — React, TypeScript |
| Connected side-by-side preview | http://localhost:5860/live-work.html | Wholesaler and merchant in one shared test workspace |
| Merchant browser preview | http://localhost:5860/mobile/merchant | `apps/mobile` — React Native, Expo |
| Courier browser preview | http://localhost:5860/mobile/driver | `apps/mobile` — separate native identity |
| API health | http://localhost:5860/api/health | `backend` — .NET 10, ABP 10.6.1 |
| Identity discovery | http://localhost:5860/.well-known/openid-configuration | OpenIddict, code + S256 PKCE |

Native apps have separate package IDs, icons and callback schemes. Browser previews use the same React Native source and backend. See [native distribution](docs/NATIVE-DISTRIBUTION.md).

Signature-verified Android test packages: [Merchant APK](https://toushir-commerce-staging.onrender.com/downloads/toushir-merchant.apk) and [Courier APK](https://toushir-commerce-staging.onrender.com/downloads/toushir-driver.apk). These standalone signed ARM64 previews use the public API and need no development server. The latest design is verified in the browser previews; earlier native runtime reports retain their actual execution dates. Android requires installation consent. The supplied iOS archives are local-API Simulator builds, with their separate execution limits recorded in the mobile verification notes.

Public test service: [Connected preview](https://toushir-commerce-staging.onrender.com/live-work.html), [Wholesaler](https://toushir-commerce-staging.onrender.com/), [Merchant](https://toushir-commerce-staging.onrender.com/mobile/merchant), [Courier](https://toushir-commerce-staging.onrender.com/mobile/driver). The free service may need time to wake. Test records use persistent PostgreSQL; the free database expires on **15 November 2026 at 17:52 UTC**. See [release boundaries](docs/RELEASE.md).

Open the wholesaler first, then its merchant/courier links in the same browser to explore one shared demo. The first history is prepared through the business workflow; the free host creates 20 labelled example orders. To invite another person on a separate device, use **Magasins → Créer un lien commerçant** and send that invitation. Each invited shop has its own orders inside your workspace. Keep the owner access capability private.

Within an existing shop, use **Mon Toushir → Équipe du magasin** to invite a Manager, Buyer or Viewer with their own password account. Shop orders are shared; personal baskets and favorites stay separate. Role changes and revocation take effect on existing sessions. See [shop membership](docs/SHOP-MEMBERS.md).

Use **Portefeuilles & visites** to assign an individual commercial adviser, plan contacts/visits and retain their outcomes. A merchant may explicitly authorize one exact quote request through **Demandes assistées**; the adviser cannot accept or pay for the shop. See [CRM authority](docs/CRM.md).

For an actual courier account, use **Équipe & paramètres** to create a Livreur, assign an order from **Livraisons**, then sign in through the courier app's secure professional login. An independent courier demo starts with an empty assigned manifest. The 3D depot supports drag to rotate, wheel to zoom and vehicle selection; actual GPS is a separate view and requires the assigned courier's location consent.

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
- Individual shop members, bounded private invitations, role-aware shared orders and immediate access revocation.
- Named commercial portfolios, versioned contact/visit tasks and merchant-authorized one-use assisted RFQs.
- Independently approved shop/segment/standard tariffs, quantity/date bands, real customer simulation, explicit basket price consent and frozen quote provenance.
- Direct priced checkout with explicit reviewed-basket consent, and quote requests where actual terms are still unknown.
- Versioned product/quantity changes with immutable before/after terms, explicit merchant acceptance or refusal, expiry and atomic stock rollback.
- Per-line preparation, named team handoffs, courier assignment, live order milestones and contextual support.
- Ordered manifests, commercial-unit vehicle capacity, custody QR, permitted GPS, customer quantity proof, partial refusal and depot returns.
- Actual cash, tendered amount/change, route float, driver declaration, independent finance count/reconciliation. Shortages never recreate customer debt.
- Claims, quarantine, inspection, commercial credits and separately recorded refunds.
- Supplier approval by another account, privately stored PDF revisions, independently reviewed supplier terms, partial receipts, lot/expiry capture, quality control, invoice evidence and explicit remainder closure.
- Contextual in-app conversations, private notes, consent-filtered in-app broadcasts, approved campaigns/challenges, immutable loyalty entries and stock-backed gifts.
- Lot recalls with exact batch/expiry containment, affected depot/orders/shops, merchant notices, bounded physical quarantine, safe preparation cancellation and explicit residual exceptions.
- Region/name/phone shop filters, safe CSV export, targeted campaign audiences, interactive period analytics and merchant activity drilldown.
- Interactive Three.js depot and a separate MapLibre view of actual GPS observations. The depot is illustrative, not a camera feed.

The current expanded workflow and verification scope are described in [connected commerce](docs/CONNECTED-COMMERCE.md).

The API enforces ADMIN, MERCHANT, DRIVER, WAREHOUSE, DISPATCHER, FINANCE and restricted SALES permissions. SignalR invalidations and polling refresh persisted state across interfaces.

## Evidence and source

See [verification](docs/VERIFICATION.md), [architecture](docs/ARCHITECTURE.md), [engagement contracts](docs/Figma-ENGAGEMENT-API.md), [supplier dossiers](docs/SUPPLIER-DOSSIER.md), [release boundaries](docs/RELEASE.md) and [catalogue provenance](catalog/README.md).

The [Figma audit](docs/Figma-AUDIT.md), [screen matrix](docs/Figma-SCREEN-MATRIX.csv) and [implementation review](docs/Figma-IMPLEMENTATION-REVIEW.md) retain source evidence and acceptance gaps. The referenced original 79-page specification was not supplied; the 122 checks are derived, not a claim of original requirement acceptance. Local, browser, native and hosted results are recorded separately.

Open product identity is not real wholesale availability. Commercial documents are quote/order records. Fiscal invoicing and external messaging, bank or carrier services require actual business/provider configuration.
