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
| SALES | Currently assigned CRM portfolios, declared contacts/visits and exact merchant-authorized RFQs |
| FINANCE | Collection/refunds, independent approvals, cash count and reconciliation |

GPS permission is separate from login. Freshness, coordinate bounds and assigned-driver ownership are validated. Finance views do not expose GPS.

Shop membership separates the actual person (`Actor.Id`/OIDC subject) from the validated shop (`ResourceOwnerId`). Personal baskets/favorites remain individual; shared shop orders retain actual creator/event/receipt attribution. Owner, Manager, Buyer and Viewer authority is live-validated across HTTP, token exchange, receipt completion and SignalR publication. Session/authority groups are rechecked at publication and serialize with revocation; stale account/order groups do not retain access. The current service is single-instance, without a distributed hub backplane. See [shop identity and revocation](SHOP-MEMBERS.md).

## Commercial state

Quotes capture explicit quantities, base/applied prices, fees, expiry and versions. Only acceptance of the current valid version reserves stock atomically. Oversized/concurrent claims fail without partial stock changes. Preparation persists actual line quantities. Departure consumes physical stock and releases reservations without inventing payment.

A short-lived, one-use customer proof binds quantities, version and both actors. Failed/refused deliveries remain under depot-return/quarantine accountability. Claim decision, physical return, inspection, commercial credit and actual refund are distinct records.

Published campaign terms are copied into quote snapshots. A revised quote cannot discount an already discounted price again. Free goods have separate reservations and custody/proof/return states. Challenge progress counts accepted and paid quantities. Loyalty reconciles credit reversals before further spending; points and reward stock reserve atomically.

### Connected order journey

Catalogue, favorites and basket use the same scoped offers and eligible physical stock. Direct checkout first obtains `/api/cart/quote`; the merchant reviews priced lines, fees, benefits and delivery address before submitting `/api/orders` with that protected pricing proof. An unpriced or otherwise ineligible offer remains unavailable for direct checkout. The submitted order immediately appears in the wholesaler queue and in the shop's shared order history.

The operational progression is `SUBMITTED → CONFIRMED → PREPARING → READY → OUT_FOR_DELIVERY`, followed by the actual delivery outcome. Quotation requests retain their separate request/quotation/acceptance branch. Warehouse preparation, dispatcher assignment and driver custody use persisted business records, versions and role checks. A timeline renders these records; it does not advance an order through a decorative animation. Customer conversations retain their order context, and staff assignment only accepts roles permitted to open the support inbox.

Merchant order tracking combines the persisted preparation quantities, order milestones, assigned courier and authorized GPS observations. Receipt verification binds the driver, shop, order version and reviewed quantities to a short-lived challenge. Delivery acceptance, payment method/reference and actual collection remain separate operations. A delivered order can still have a balance due; a paid cancellation can still have a refund due.

### Merchant-approved order amendments

`OrderAmendments` stores the immutable before/after terms, proposed lines, original order version, expiry, terms hash, proposer, decision and idempotency hashes. An ADMIN can propose a change only for an unpaid reserved `SUBMITTED` or `CONFIRMED` order before any preparation, wave, delivery assignment or receipt. Creating the proposal leaves the original contract and reservations in force.

The merchant sees the proposed quantities, prices, fees and benefits together and explicitly accepts or rejects. Acceptance requires live shop write authority, the current proposal/order versions and the exact terms hash. The shared pricing engine re-evaluates those terms before atomically releasing old reservations and reserving the replacement contract. Changed prices, stock, benefits or operational state cause a conflict with full rollback. Internal pricing rule identifiers are removed from the merchant projection. Only one pending proposal can exist per order; expired or stale applicability prevents acceptance even before an expired proposal is persisted as `EXPIRED` during replacement.

| Endpoint under `/api` | Authority and effect |
| --- | --- |
| `GET /orders/{id}/amendments` | Authorized shop reader or ADMIN; current applicability and retained history |
| `POST /admin/orders/{id}/amendments` | ADMIN; propose without changing the contract |
| `POST /orders/{id}/amendments/{proposalId}/accept` or `/reject` | Approved shop writer; explicit versioned decision |
| `POST /admin/orders/{id}/amendments/{proposalId}/withdraw` | ADMIN; withdraw the proposal |

### Campaign audiences and activity

`CampaignAudiences` stores the explicit eligible primary-shop identities selected when a campaign is created; an empty selection means all otherwise eligible shops in that workspace. The server validates at most 500 distinct approved shops. Audience eligibility applies consistently to catalogue visibility, preview, quotations, checkout, challenge progress and awards. Notifications use that same recipient scope. Merchant responses do not disclose the full target-shop list. Name, phone and wilaya/commune filters support staff discovery and recipient selection; geography is derived from actual stored delivery addresses.

`GET /api/profile/activity?days=7|30|90` scopes activity to the live `ResourceOwnerId`. Day boundaries use `Africa/Algiers`. Current contractual order totals, event-date payments/refunds and current outstanding balances have explicit, separate bases. A later credit can revise current contract value; payment history retains actual payment dates. Category totals describe ordered line value, rather than asserting net sales after credits.

## Money

Customer pricing uses a batched scoped pricebook and a pure priority resolver shared by catalogue, basket, simulation and orders. Independently published immutable rules select shop contract, commercial segment, standard schedule and approved fallback in that order. A five-minute protected basket proof binds the actual actor and reviewed commercial terms before direct checkout; order creation uses one evaluated plan. Explicit quote versions append frozen provenance, preserving expected/base/final prices and any reasoned negotiation. Campaign selection and unit rounding are shared between preview and commitment. See [customer pricing](CUSTOMER-PRICING.md).

Order API amounts use exact decimal DZD with at most two fractional digits. Supplier costs and discount totals explicitly ending `Minor` use integer centimes: 100 = 1 DZD. Interfaces convert only at named boundaries; backend accounting does not use binary floating point.

Physical receipt is not a payment. A payment records actual funds; reconciliation reviews an existing entry. Courier cash distinguishes tendered, change and net. A route cashbox combines opening float and actual net receipts. Driver declaration and finance count are independently attributed. A shortage is internal custody and never increases customer debt.

## Live state and integrations

Authenticated SignalR groups receive scoped invalidations after commit. Engagement notifications have a durable outbox. Clients refetch authoritative state and poll on connection loss; they cannot publish arbitrary mutations through the hub.

The depot projects real order states onto illustrative geometry. GPS markers represent authorized observations with timestamps, accuracy and stale labels. Synthetic movement is never presented as actual GPS, camera evidence or payment proof.

The delivery truck asset is an adapted, attributed Cesium illustration. Its optional replay animates an operational illustration; the timestamped GPS view remains the source for actual courier position. Reduced-motion preferences disable illustrative movement.

In-app messaging is persisted. SMS, email, WhatsApp, remote push, bank settlement, fiscal invoicing and external route optimization require configured providers and separate evidence. They are not simulated as successful external actions.

## Deployment

Docker compiles desktop, mobile web and .NET, then runs as a non-root user. Database credentials and stable identity certificates are host secrets. A validated dedicated PostgreSQL schema isolates staging from other applications. The free database expires on 15 November 2026. Commercial installations need owner-supplied offers, legal settings and rehearsed backup/restore.

## Supplier document lifecycle

The [supplier dossier](SUPPLIER-DOSSIER.md) stores bounded private PDF bytes and SHA256 metadata with immutable revisions and independent confirmation review. Before receiving, accepted terms may amend a purchase once; after receiving, evidence may be completed only with identical frozen terms. Actual receiving, commercial evidence completeness and actual funds remain distinct. Three new provider-specific tables are additive and follow the same dedicated-schema/workspace isolation and transaction/version contracts.

## Commercial portfolios

The [CRM contract](CRM.md) limits SALES to current assigned primary shops. An assisted RFQ consumes a named, expiring one-use merchant grant after live issuer, address, shop and assignment validation. It uses the existing RFQ/pricing engine, preserves actual actor attribution and personal baskets, and grants no quote acceptance, delivery, finance or GPS powers. Task/contact and portfolio history are persisted separately from orders. Five additive tables follow the existing provider/workspace transaction contract.

## Physical lot recall and containment

A recall requires a counted, lot-managed warehouse lot. Its identity is the hash of product identity, exact lot number and normalized UTC expiry, including an explicitly absent expiry. The same identity follows quality splits and identified returns. The return-identification path rejects a claimed expiry that does not match an existing physical lot with that number. Aggregate stock without a recorded lot is explicitly unknown historical scope and never presented as traced exposure.

| Persistent record | Responsibility |
| --- | --- |
| `RecallCases` | Unique workspace/physical identity, active containment, administrative state and unknown scope |
| `RecallPositions` | Depot, isolated stock, reserved preparation or dispatched allocation exposure; recovered and explicit residual quantities |
| `RecallNotices` | Recipient-scoped merchant/driver instructions and actual acknowledgement attribution |
| `RecallEvents` | Actor-attributed containment, physical recovery, exception and closure history |
| `RecallOperations` | Actor/workspace-scoped idempotency key and request hash |

Opening a recall immediately holds the identity. Catalogue/favorites/basket availability, allocation, preparation, release from quarantine, departure and positive customer acceptance apply that hold. An already dispatched load can be refused and returned; containment does not prevent the ordinary return path. Closing the administrative dossier leaves `ContainmentActive=true`, so closure never authorizes redistribution.

Physical recovery is bounded by actual stock and traceable receipts. Depot recovery moves unreserved on-hand units into quarantine with an inventory entry. Outbound recovery links an already received, identified `RETURN` quarantine lot from the same order and identity; it creates no second stock increment. Quantities cannot be linked twice. A notice acknowledgement only confirms receipt of instructions and does not increase recovered stock or prove physical action.

ADMIN may cancel a recalled `PREPARING`/`READY` order before actual courier custody, using the recall and order versions plus a recorded reason. This releases reservations, cancels any uncollected assignment, detaches it from its route and invalidates old route/receipt challenges. Existing funds and refund liabilities remain recorded; physical isolation must still occur separately. WAREHOUSE can perform recall recovery; residual exceptions and closure require ADMIN. Depot stock cannot be replaced by a residual exception. Closure requires all known exposure resolved, no remaining on-hand/reserved units of the identity, and a reasoned decision for any untracked historical scope.

The authenticated API exposes `/api/admin/recalls` and `/{id}` plus versioned `/recover`, `/cancel-preparation`, `/exceptions` and `/close` mutations. Merchants and drivers read only their notices through `/api/recalls/inbox` and acknowledge through `/api/recalls/notices/{id}/ack`; merchant Viewers cannot acknowledge. No recall operation invents an SMS delivery, commercial credit or refund.

## Explicit demonstration fixtures

`POST /api/demo/showcase` requires the live owner capability for an explicitly configured demo workspace. It adds clearly fictional positive DZD prices and opening stock to eligible unknown offers while preserving operator-entered terms, inactive references, managed physical lots and historical orders. The same owner setup provides labeled example discount, free-goods and challenge campaigns plus reward images. Its automatic persona approvals are demo fixtures, rather than human approval of real commercial terms. Ordinary raw API demo sessions remain unpriced until owner setup; invited shops and real business workspaces cannot invoke it.

Campaign audiences, amendments and recall persistence use two additive migrations per database provider: `AddCampaignAudienceAndOrderAmendments` and `AddLotRecallContainment`. PostgreSQL migrations and snapshots honor the validated configured schema; SQLite upgrades the existing installation in place. The release verification generates alternate-schema PostgreSQL SQL and executes SQLite startup upgrades on backup copies, preserving all original business values. Hosted PostgreSQL execution is verified separately after deployment.
