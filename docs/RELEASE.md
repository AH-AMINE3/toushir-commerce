# Release verification and launch boundaries

## Data

Product identities and selected front photographs are sourced from Open Food Facts. The catalogue is a public reference catalogue, not a local supplier inventory feed. Wholesale prices and stock are never invented. Unknown offers remain quote-only. Original photos remain canonical; AI studio experiments are visual studies and are not silently substituted for source packaging where small print or artwork differs.

## Architecture

.NET 10, EF Core SQLite, Expo 58, React Native 0.88 and React 19.3. Real authenticated accounts and isolated anonymous demo workspaces use the same business workflow. Source code and deployment branch remain private. The original CRM main branch is independent.

## Verification

Backend machine-readable reports record the actual latest scenarios and request totals. API tests cover role/workspace boundaries, quotations and version acceptance, atomic stock reservations, cancellation, dispatch, partial receipt, quarantine/inspection, collection, reconciliation, credits, refunds and idempotency. Production tests cover owner setup, approval, forwarded HTTPS origins, session/order persistence through restart, historical catalogue identity preservation, password revocation and online backup integrity. Native compilation is distinct from runtime testing and store distribution.

## Dependency review

The initial supported Expo dependency installation reported 22 npm advisories (7 moderate, 15 high; no critical). The reported leaf issues are in transitive build/development tooling, including braces, node-forge and uuid, with affected tooling parent packages reported as well. The production web host serves static JavaScript using .NET, not Metro or an Expo development server. This limits runtime exposure; it is not proof of zero vulnerabilities. A forced Expo downgrade proposed by npm is not an accepted fix. Reassess upstream patched versions before a production release.

## Hosting and deployment

The free hosted demonstration may sleep and uses ephemeral SQLite. It must not hold live customer transactions or be represented as durable business hosting. Docker Compose supports a durable volume for a business installation, with owner-controlled backups and restore testing. Real business stock, prices, fees, legal document/tax basis and merchant acceptance policies require owner approval.

Native iOS simulator and Android builds are separate deliverables. Store signing, publication, device distribution and the actual API origin must be verified against the final build; source configuration alone is not a successful store release.

Advisory sources: https://github.com/advisories/GHSA-vfj7-8cjw-p6xm ; https://github.com/advisories/GHSA-86w9-cpqp-85rv ; https://github.com/advisories/GHSA-w5hq-g745-h8pq . Build tooling is not exposed publicly.
