# Operations and data ownership

This is a .NET 10 ASP.NET Core application with EF Core 10 and SQLite. Orders, users, hashed passwords, hashed session tokens, carts, addresses, approved offers, stock reservations, payments, inventory movements and audit events are persistent database records. Money calculations use `decimal`, accept at most two fractional digits and are persisted exactly by EF SQLite; no binary floating point is used for money. API amounts are DZD, never centimes.

## Local run

Run from `backend/` after restoring/building the project:

```sh
Demo__Enabled=true ASPNETCORE_URLS=http://localhost:5860 dotnet run
```

The catalog defaults to `../catalog/products.json`. Set `Catalog__Path` to the imported JSON file and `Catalog__Directory` to its directory if packaging changes. Real product identities refresh on startup, preserving all workspace offers and order snapshots. Identities removed from the validated manifest are marked inactive and disappear from new catalog/cart requests; historical line snapshots remain intact. An explicit compatibility upgrade adds the `IsCatalogActive` column when opening the earlier local preview database. The frontend web export belongs in `backend/wwwroot/`. API and assets use the same origin in production. Unknown `/api/...` routes return JSON 404, and other paths use the SPA fallback.

Production deployments must run with **Demo__Enabled=false** and an explicitly configured **DataPath** on a persistent, backed-up volume. Startup refuses production hosting without an explicit data path and refuses a temporary directory for real business mode. A free demonstration may explicitly set Demo__Enabled=true and DataPath=/tmp/toushir-wholesale-demo; its records are ephemeral and must be labelled as such. SQLite supports a single app instance for this deployment; do not mount the same file on unrelated machines or scale this configuration to multiple instances. WAL journaling, a busy timeout, a database transaction around every mutation and conditional reservation SQL prevent overselling; the application also serializes local mutations. Stock reservation changes roll back with the order if any line fails.

For the first production start set:

- `Bootstrap__Email`: owner's real sign-in address.
- `Bootstrap__Password`: a unique password of at least 14 characters, provided through the host's secret settings.
- `Wholesale__Name`: actual wholesaler display name.
- `DataPath`: absolute persistent data directory.
- `Cors__Origins`: semicolon separated exact trusted web origins if the frontend is hosted separately. Do not use wildcards with credentials.

Bootstrap creates one production workspace and its owner only when no non-demo owner exists. Remove bootstrap secrets from the deployment after first sign-in. Never commit them. Merchants register with status PENDING; only an administrator in that workspace can approve them. A suspended merchant's sessions immediately lose access. Password changes require the current password and revoke every session. Passwords use salted PBKDF2 via ASP.NET Core's PasswordHasher; tokens are random 256-bit values and only SHA-256 hashes are stored. Tokens expire after 16 hours. Behind a platform reverse proxy such as Render, explicitly set `ForwardedHeaders__TrustPlatformProxy=true` only when the application port is accessible exclusively through that platform. It trusts one forwarding hop, including the client IP for rate limits and HTTPS scheme. For a directly exposed server keep this off and configure known proxy addresses through the hosting configuration instead. Origin checks accept the exact same HTTPS authority and explicit trusted CORS origins; an unrelated origin is rejected. Native apps use Bearer tokens; the browser also receives an HttpOnly, SameSite=Strict session cookie, Secure in production. Authentication and demo creation are rate limited. Every endpoint scopes commercial data to its authenticated workspace and customer; the administrator role does not cross workspace boundaries.

The demo endpoint is available only when explicitly enabled. Each new visitor creates an independent fictional workspace with a merchant and a wholesaler persona. A separate random workspace access key is required to switch between those personas. Demo workspaces contain real-source product identities but **no fabricated own prices or stock**. A demo administrator may enter test offers and demonstrate the order lifecycle inside that isolated space. Clearly communicate the difference between a demonstration host and a real business deployment. A free ephemeral host can reset all its demo records after sleep/restart/redeploy; it is not a production database.

## Backup and restore

Use `scripts/backup.py` for SQLite's online backup API. It copies a consistent snapshot while the API is running, including committed WAL transactions. Store encrypted off-host backups, restrict access and test restoration. Avoid copying only the `.db` file while a live WAL exists. Example:

```sh
python3 scripts/backup.py --database /data/toushir-wholesale.db --destination /secure-backups/toushir-2026-10-16.db
```

Restore while the API is stopped: retain a rollback copy of the current database, restore the verified snapshot, remove stale `-wal`/`-shm` companion files only while stopped, then restart and verify health and the expected order count. Restore is deliberately an operator procedure, not a public administrator endpoint. Do not silently delete audit history or reseed fabricated commercial records. This release creates its initial schema with EF `EnsureCreated`; before changing an existing production schema, introduce and review explicit versioned migrations and rehearse backup/restore on a copy.

## Real commercial configuration still required

Open Food Facts identifies products and supplies licensed package images. It does not provide this wholesaler's negotiated buying/selling prices, available warehouse stock, packs, suppliers, delivery routes, local Oran availability, credit limits or tax position. Those fields remain unknown until the wholesaler confirms them. Direct priced checkout additionally requires an explicit `Wholesale__DeliveryFee` tariff; without it checkout requires a quote and does not silently assume free delivery. A quote can explicitly agree a zero delivery fee. The first-order workflow is an RFQ: request → versioned priced quote with expiry and confirmed physical stock → merchant acceptance → reservation → preparation → dispatch → accepted/refused delivery quantities. Public retail reference prices must never silently become wholesale offers.

Delivery and payment are separate. Dispatch decreases physical stock and releases reservations. Refused units enter quarantine until physical inspection explicitly returns them to sale or writes them off. Delivery recalculates the accepted goods amount while retaining the agreed delivery fee. Actual cash/bank collection, refund and reconciliation each require explicit records. A partial refusal after advance collection can create a visible refund due; it never silently erases cash history. Post-delivery claims are customer-scoped and bounded by accepted quantities. A decision to accept a claim does not change stock or money. Explicit physical receipt enters quarantine; explicit inspection restocks or writes off received units. Only a separate manual commercial credit can reduce the order amount, capped by physically received units at their original unit price. An actual refund is recorded separately after a resulting overpayment. Commercial credits and original order line snapshots remain auditable. The commercial document is a quote/order document, **not a fiscal invoice**. Configure the actual legal entity and consult its accounting requirements before issuing fiscal invoices.

No bank settlement, carrier connection, SMS, email delivery, credit underwriting or external accounting sync is implied. The software records the business operations; a real provider integration must be configured and verified before claiming that it performs those external actions. Web exports run in browsers; native source must be signed using the owner's Apple/Google accounts for store distribution. Credentials, customer data and source-provenance licenses require normal owner administration.

## Verification

`node scripts/verify-api.mjs` runs isolated demonstration records through API authorization, unknown-price catalog/cart, request idempotency, quote revisions, concurrent stock acceptance, reservation release, immutable prices, dispatch, partial delivery, quarantine inspection, independent COD/refunds, import rollback, analytics and logout. Results are written to `verification.json` without session tokens or passwords. `scripts/verify-production.mjs` additionally verifies real account registration/approval/passwords, disabled demo mode and persistence across a server restart on a separate temporary test database.
