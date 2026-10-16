# Toushir Gros

Two connected products for wholesale ordering in Oran: a React Native merchant mobile application and a separate wholesaler workspace, backed by ASP.NET Core .NET 10 and an EF Core SQLite database.

## Workflows

- Merchant approval, product search, categories, favorites, saved basket and delivery addresses.
- Request for quotation → priced offer → merchant approval → transactional stock reservation → preparation → dispatch → physical receipt.
- Partial/refused deliveries, quarantined returns and explicit inspection before stock re-entry.
- Actual cash/transfer collection, reconciliation, commercial claims, explicit credits and separately recorded refunds.
- Date-filtered analytics with comparison periods, daily trends, customer/product/category performance, receivable aging and documented calculation bases.
- Exact open-data product identities and associated photos, with source links and licenses. Catalog presence is not a claim of local stock or an approved wholesale price.

## Run a real business installation

Requirements: Docker, a durable volume, and owner-supplied real prices, inventory and delivery terms.

1. Copy `.env.example` to `.env` and set the owner email and a strong unique password locally. Never commit credentials.
2. Run `docker compose up --build -d`.
3. Open `http://localhost:5860/admin` and sign in with that owner account. Merchant registration requires approval.
4. Confirm actual product offers and stock, or quote merchants’ requests individually. Configure a delivery fee before enabling direct checkout.
5. Put an HTTPS reverse proxy in front of the application, configure allowed origins/hosts and trusted forwarding for that known hosting environment, and test backup restoration before commercial use.

The Compose volume preserves SQLite and session keys. Follow `backend/OPERATIONS.md` for backup and security operations. The free public demo uses explicit isolated demo workspaces and temporary storage; it is not a durable production database.

## Develop

- Backend: `dotnet run --project backend/Toushir.Wholesale.csproj --urls http://localhost:5860`
- Set `Demo__Enabled=true` only when intentionally running an isolated demonstration. Production defaults to demo disabled and requires an explicit durable `DataPath`.
- Frontend: `cd apps/mobile && npm ci && npm run web`.
- Native: set `EXPO_PUBLIC_API_URL` to the reachable HTTPS API URL, then use Expo’s Android/iOS build commands. The Android emulator uses `10.0.2.2` for a local Mac API; an iOS simulator can use `localhost`. Physical devices require a reachable host.
- Web distribution: run `npm run build:web` in `apps/mobile`, then `node scripts/finalize-web.mjs --install` from the repository root. The API serves the resulting browser application.

## Checks and scope

`apps/mobile`: `npm run typecheck`, `npm run check:expo`.
`backend/scripts`: API, production persistence/security, storage guards and analytics verification scripts with machine-readable results.

The first version implements two roles: MERCHANT and ADMIN. The owner handles operations, stock, delivery and finance. Separate warehouse/driver/finance permissions, live carrier/payment integrations and store publication are additional integrations, not claimed as implemented. Commercial documents are order/quotation records; local legal invoice/tax configuration must be supplied by the owner.

Package audit findings and release limitations are recorded in `docs/RELEASE.md`. No claim of a completed external penetration test or an App Store release is made.
