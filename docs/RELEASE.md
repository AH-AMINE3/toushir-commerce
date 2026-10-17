# Release verification and launch boundaries

## Data

Product identities and selected front photographs are sourced from Open Food Facts. The catalogue is a public reference catalogue, not a local supplier inventory feed. Wholesale prices and stock are never invented. Unknown offers remain quote-only. Original photos remain canonical; AI studio experiments are visual studies and are not silently substituted for source packaging where small print or artwork differs.

## Architecture

.NET 10, EF Core with PostgreSQL for hosted testing and SQLite for local/durable-volume installations, Expo 58, React Native 0.88 and React 19.3. Real authenticated accounts and isolated anonymous demo workspaces use the same business workflow. Source code and deployment branch remain private. The original CRM main branch is independent.

## Verification

Backend machine-readable reports record the actual latest scenarios and request totals. API tests cover role/workspace boundaries, quotations and version acceptance, atomic stock reservations, cancellation, dispatch, partial receipt, quarantine/inspection, collection, reconciliation, credits, refunds and idempotency. Production tests cover owner setup, approval, forwarded HTTPS origins, session/order persistence through restart, historical catalogue identity preservation, password revocation and online backup integrity. Native compilation is distinct from runtime testing and store distribution.

## Dependency review

The initial supported Expo dependency installation reported 22 npm advisories (7 moderate, 15 high; no critical). The reported leaf issues are in transitive build/development tooling, including braces, node-forge and uuid, with affected tooling parent packages reported as well. The production web host serves static JavaScript using .NET, not Metro or an Expo development server. This limits runtime exposure; it is not proof of zero vulnerabilities. A forced Expo downgrade proposed by npm is not an accepted fix. Reassess upstream patched versions before a production release.

## Hosting and deployment

The free staging service prepares 20 fictional history orders per new workspace; local/default configuration prepares 40. `Demo__ScenarioOrders` permits 20–80, with all current operating states retained. Synthetic demo personas have no password credential and use expiring owner/invitation capabilities. Real team and business accounts retain the configured password hashing and professional sign-in. Live invalidations are coalesced in the web client to avoid a history update issuing one full dashboard refresh per historical order.

The hosted test deployment uses an internal-only Render PostgreSQL 17 database, separate from the web container. Test orders survive web-service redeploys. The free database expires on **15 November 2026 at 17:52 UTC**; export and migrate or explicitly upgrade before expiry. Free web compute may sleep and need time to wake. No paid plan has been enabled. This is a test deployment with a time-limited free database, not indefinite free commercial hosting. Docker Compose also supports durable SQLite storage. Real business stock, prices, fees, legal document/tax basis and merchant acceptance policies require owner approval.

Each visitor may explore an isolated demo. A wholesaler can create a merchant invitation for its own demo: up to 50 shops by default, no passwords required, up to 7 days (bounded by the workspace lifetime). Invited merchants can only access their own orders. Their requests reach the inviting wholesaler, whose private access link must never be shared with merchants. Revocation closes the corresponding guest sessions. Owner capability and invitation tokens are separate random secrets, stored as hashes on the server. Browser owner recovery is stored locally; the personal owner link can reopen the same test workspace on another device. All demo workspaces expire after 7 days.

Native iOS simulator and Android builds are separate deliverables. Store signing, publication, device distribution and the actual API origin must be verified against the final build; source configuration alone is not a successful store release.

Advisory sources: https://github.com/advisories/GHSA-vfj7-8cjw-p6xm ; https://github.com/advisories/GHSA-86w9-cpqp-85rv ; https://github.com/advisories/GHSA-w5hq-g745-h8pq . Build tooling is not exposed publicly.

The SQLite native dependency was upgraded to SQLitePCLRaw 2.1.14; the loaded engine is 3.54.0. The verified NuGet dependency tree reports no known vulnerable packages at this release check. The provider probe and invitation verification reports record their actual checks.
