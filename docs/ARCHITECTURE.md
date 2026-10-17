# Architecture and business boundaries

## Runtime

The .NET 10 host initializes the ABP module, dependency injection and `AbpDbContext`. Domain services own mutations; endpoint groups adapt HTTP and actor permissions. EF Core persists commercial state, audit events, sessions and OpenIddict grants. PostgreSQL supports staging and durable hosted records; SQLite supports local development. Provider-specific migrations preserve each dialect.

The API serves the React desktop at `/` and the React Native web export at `/mobile`. Production uses one HTTPS origin. Three.js and MapLibre are deferred until delivery views open. Images and Figma assets are served with the application.

## Identity and isolation

Professional clients use authorization code with S256 PKCE and the server's hashed-password login. Exact redirects, nonce, audience and live session/grant checks apply. Access tokens expire after 15 minutes; rotating refresh tokens after eight hours. Native secrets use SecureStore; browser bearer state is per-tab. The authorization cookie is HttpOnly. Credentials never belong in source, analytics or screenshots.

Demo mode is explicitly configured. An expiring random owner capability creates personas inside one fictional workspace. Merchant invitations are separately scoped and cannot grant owner access. Every resource checks actor/workspace ownership; logout, suspension, password changes and invitation revocation are checked server-side.

SQLite serializes writes in its single application process. PostgreSQL mutations acquire transaction-scoped advisory locks derived from the dedicated schema and validated workspace, so a long example-history transaction cannot block another visitor's basket or courier bootstrap. Identity entry points use a separate short-lived scope; catalogue imports acquire a shared catalogue lock before their own workspace lock. These PostgreSQL locks also coordinate overlapping service instances during deployment and release automatically with commit/rollback. See the [PostgreSQL lock contract](https://www.postgresql.org/docs/17/explicit-locking.html#ADVISORY-LOCKS) and [parameterized EF SQL](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries).

| Role | Scope |
| --- | --- |
| ADMIN | Own workspace, merchants, offers, quotations, team and audit |
| MERCHANT | Own basket, addresses, orders, decisions, receipts, conversations and wallet |
| DRIVER | Assigned manifest, custody, active-route GPS, receipt proof and actual collection |
| WAREHOUSE | Preparation, physical custody/returns, supplier receipt, quality and fulfilment |
| DISPATCHER | Assignment, route plan/order and operational exceptions |
| FINANCE | Collection/refunds, independent approvals, cash count and reconciliation |

GPS permission is separate from login. Freshness, coordinate bounds and assigned-driver ownership are validated. Finance views do not expose GPS.

Shop membership separates the actual person (`Actor.Id`/OIDC subject) from the validated shop (`ResourceOwnerId`). Personal baskets/favorites remain individual; shared shop orders retain actual creator/event/receipt attribution. Owner, Manager, Buyer and Viewer authority is live-validated across HTTP, token exchange, receipt completion and SignalR publication. Session/authority groups are rechecked at publication and serialize with revocation; stale account/order groups do not retain access. The current service is single-instance, without a distributed hub backplane. See [shop identity and revocation](SHOP-MEMBERS.md).

## Commercial state

Quotes capture explicit quantities, base/applied prices, fees, expiry and versions. Only acceptance of the current valid version reserves stock atomically. Oversized/concurrent claims fail without partial stock changes. Preparation persists actual line quantities. Departure consumes physical stock and releases reservations without inventing payment.

A short-lived, one-use customer proof binds quantities, version and both actors. Failed/refused deliveries remain under depot-return/quarantine accountability. Claim decision, physical return, inspection, commercial credit and actual refund are distinct records.

Published campaign terms are copied into quote snapshots. A revised quote cannot discount an already discounted price again. Free goods have separate reservations and custody/proof/return states. Challenge progress counts accepted and paid quantities. Loyalty reconciles credit reversals before further spending; points and reward stock reserve atomically.

## Money

Customer pricing uses a batched scoped pricebook and a pure priority resolver shared by catalogue, basket, simulation and orders. Independently published immutable rules select shop contract, commercial segment, standard schedule and approved fallback in that order. A five-minute protected basket proof binds the actual actor and reviewed commercial terms before direct checkout; order creation uses one evaluated plan. Explicit quote versions append frozen provenance, preserving expected/base/final prices and any reasoned negotiation. Campaign selection and unit rounding are shared between preview and commitment. See [customer pricing](CUSTOMER-PRICING.md).

Order API amounts use exact decimal DZD with at most two fractional digits. Supplier costs and discount totals explicitly ending `Minor` use integer centimes: 100 = 1 DZD. Interfaces convert only at named boundaries; backend accounting does not use binary floating point.

Physical receipt is not a payment. A payment records actual funds; reconciliation reviews an existing entry. Courier cash distinguishes tendered, change and net. A route cashbox combines opening float and actual net receipts. Driver declaration and finance count are independently attributed. A shortage is internal custody and never increases customer debt.

## Live state and integrations

Authenticated SignalR groups receive scoped invalidations after commit. Engagement notifications have a durable outbox. Clients refetch authoritative state and poll on connection loss; they cannot publish arbitrary mutations through the hub.

The depot projects real order states onto illustrative geometry. GPS markers represent authorized observations with timestamps, accuracy and stale labels. Synthetic movement is never presented as actual GPS, camera evidence or payment proof.

In-app messaging is persisted. SMS, email, WhatsApp, remote push, bank settlement, fiscal invoicing and external route optimization require configured providers and separate evidence. They are not simulated as successful external actions.

## Deployment

Docker compiles desktop, mobile web and .NET, then runs as a non-root user. Database credentials and stable identity certificates are host secrets. A validated dedicated PostgreSQL schema isolates staging from other applications. The free database expires on 15 November 2026. Commercial installations need owner-supplied offers, legal settings and rehearsed backup/restore.

## Supplier document lifecycle

The [supplier dossier](SUPPLIER-DOSSIER.md) stores bounded private PDF bytes and SHA256 metadata with immutable revisions and independent confirmation review. Before receiving, accepted terms may amend a purchase once; after receiving, evidence may be completed only with identical frozen terms. Actual receiving, commercial evidence completeness and actual funds remain distinct. Three new provider-specific tables are additive and follow the same dedicated-schema/workspace isolation and transaction/version contracts.
