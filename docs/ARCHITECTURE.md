# Architecture and business boundaries

## Runtime

The .NET 10 host initializes the ABP module, dependency injection and `AbpDbContext`. Domain services own mutations; endpoint groups adapt HTTP and actor permissions. EF Core persists commercial state, audit events, sessions and OpenIddict grants. PostgreSQL supports staging and durable hosted records; SQLite supports local development. Provider-specific migrations preserve each dialect.

The API serves the React desktop at `/` and the React Native web export at `/mobile`. Production uses one HTTPS origin. Three.js and MapLibre are deferred until delivery views open. Images and Figma assets are served with the application.

## Identity and isolation

Professional clients use authorization code with S256 PKCE and the server's hashed-password login. Exact redirects, nonce, audience and live session/grant checks apply. Access tokens expire after 15 minutes; rotating refresh tokens after eight hours. Native secrets use SecureStore; browser bearer state is per-tab. The authorization cookie is HttpOnly. Credentials never belong in source, analytics or screenshots.

Demo mode is explicitly configured. An expiring random owner capability creates personas inside one fictional workspace. Merchant invitations are separately scoped and cannot grant owner access. Every resource checks actor/workspace ownership; logout, suspension, password changes and invitation revocation are checked server-side.

| Role | Scope |
| --- | --- |
| ADMIN | Own workspace, merchants, offers, quotations, team and audit |
| MERCHANT | Own basket, addresses, orders, decisions, receipts, conversations and wallet |
| DRIVER | Assigned manifest, custody, active-route GPS, receipt proof and actual collection |
| WAREHOUSE | Preparation, physical custody/returns, supplier receipt, quality and fulfilment |
| DISPATCHER | Assignment, route plan/order and operational exceptions |
| FINANCE | Collection/refunds, independent approvals, cash count and reconciliation |

GPS permission is separate from login. Freshness, coordinate bounds and assigned-driver ownership are validated. Finance views do not expose GPS.

## Commercial state

Quotes capture explicit quantities, base/applied prices, fees, expiry and versions. Only acceptance of the current valid version reserves stock atomically. Oversized/concurrent claims fail without partial stock changes. Preparation persists actual line quantities. Departure consumes physical stock and releases reservations without inventing payment.

A short-lived, one-use customer proof binds quantities, version and both actors. Failed/refused deliveries remain under depot-return/quarantine accountability. Claim decision, physical return, inspection, commercial credit and actual refund are distinct records.

Published campaign terms are copied into quote snapshots. A revised quote cannot discount an already discounted price again. Free goods have separate reservations and custody/proof/return states. Challenge progress counts accepted and paid quantities. Loyalty reconciles credit reversals before further spending; points and reward stock reserve atomically.

## Money

Order API amounts use exact decimal DZD with at most two fractional digits. Supplier costs and discount totals explicitly ending `Minor` use integer centimes: 100 = 1 DZD. Interfaces convert only at named boundaries; backend accounting does not use binary floating point.

Physical receipt is not a payment. A payment records actual funds; reconciliation reviews an existing entry. Courier cash distinguishes tendered, change and net. A route cashbox combines opening float and actual net receipts. Driver declaration and finance count are independently attributed. A shortage is internal custody and never increases customer debt.

## Live state and integrations

Authenticated SignalR groups receive scoped invalidations after commit. Engagement notifications have a durable outbox. Clients refetch authoritative state and poll on connection loss; they cannot publish arbitrary mutations through the hub.

The depot projects real order states onto illustrative geometry. GPS markers represent authorized observations with timestamps, accuracy and stale labels. Synthetic movement is never presented as actual GPS, camera evidence or payment proof.

In-app messaging is persisted. SMS, email, WhatsApp, remote push, bank settlement, fiscal invoicing and external route optimization require configured providers and separate evidence. They are not simulated as successful external actions.

## Deployment

Docker compiles desktop, mobile web and .NET, then runs as a non-root user. Database credentials and stable identity certificates are host secrets. A validated dedicated PostgreSQL schema isolates staging from other applications. The free database expires on 15 November 2026. Commercial installations need owner-supplied offers, legal settings and rehearsed backup/restore.
