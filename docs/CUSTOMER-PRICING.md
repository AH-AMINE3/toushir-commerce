# Customer pricing and explicit price consent

The Figma G18 frame (`179:73`) establishes one shared reference engine and the priority of a shop contract, commercial segment and standard schedule. The implementation extends that existing operational world with actual proposal, approval, simulation and historical-price controls.

## Resolution

`PricingPolicy` selects the first eligible scope: MERCHANT, SEGMENT, STANDARD, then the approved depot Offer. Priority is not a cheapest-price contest. A shop has at most one commercial segment. Rules are isolated by workspace, product, target, inclusive unit-quantity band and half-open UTC interval. Publication and withdrawal also bound eligibility. Inactive products cannot be enabled through tariffs. Unknown, zero or unapproved fallback prices remain quote-only.

Commercial definitions are immutable. A replacement is a new proposal for the same product, scope, target and quantity band, tied to its predecessor version. Independent publication schedules withdrawal of the predecessor at the replacement's effective start; a future change preserves today's price. Conflicting published intervals and quantity bands are rejected. An independent, reasoned withdrawal may shorten a scheduled cutoff without rewriting historical terms.

`PricingService.LoadBook` batches the customer's applicable rules and segment into one pricebook, avoiding a database lookup for every catalogue product. Product detail, effective-price sorting, favourites, basket, staff simulation and actual order creation use the same resolver. Simulation retains the real staff actor and validates its requested shop; it never impersonates a merchant or issues checkout consent. A future simulation uses currently published rules with current physical stock and configured delivery fees, and says so.

## Commercial amounts and campaigns

Tariff definitions and structured provenance use integer DZD centimes (`unitPriceMinor`: 100 = 1 DA). Existing order amounts remain exact decimal DZD. Quantities are units, not cartons; the confirmed Offer's case size and minimum purchase still apply to direct orders.

Campaigns apply after base-price selection. A custom tariff forbids stacking unless its definition explicitly permits it. Explicit selection of an incompatible campaign fails. `CampaignPricing` shares selection and per-unit rounding between previews and committed snapshots. The strongest eligible discount wins, then free goods, then a stable campaign identifier; there is no implicit stacking. A discount that rounds an actual unit price to zero fails closed and permits a separate RFQ. Gift availability includes both purchased units and all gift demand using the same physical product. Existing independent reservation, custody, receipt and return controls remain authoritative.

## Reviewed basket and checkout

The explicitly fictional demo configures `Demo:DeliveryFee=0`, allowing testers to exercise reviewed direct checkout after they confirm an example offer. This fee is used only for a database-validated demo workspace when no wholesale fee is configured. Real business workspaces still require their own `Wholesale:DeliveryFee`; the demo fallback cannot authorize real checkout. Manual quotation fees remain explicit commercial terms.

Every new `PLACE_ORDER` requires the protected `pricingToken` from a previously displayed, orderable basket. ASP.NET Data Protection binds it for five minutes to workspace, actual authenticated actor, shop and a fingerprint of product quantities, case/minimum terms, base/final centime prices, chosen rule/version, campaign/version/gift terms, delivery fee and currency. Changes to unrelated available-stock counts do not change this commercial fingerprint; actual reservations still enforce availability atomically.

The server builds one authoritative plan under its existing workspace mutation transaction, verifies consent before effects, then applies promotions at the same evaluated instant and checks that committed prices equal the preview. Missing, tampered, expired, other-actor or commercially stale proofs return `409 PRICE_CONFIRMATION_REQUIRED`. A committed idempotent retry returns the original order before proof revalidation; the proof is a precondition, not part of order identity.

The React Native source uses the basket already shown to the user. It does not fetch a fresh price and silently accept it during confirmation. A price/stock conflict refreshes the visible basket and requires another explicit press. A cart/signature mismatch or pending save blocks confirmation. Basket edits are disabled during checkout. `X-Toushir-Actor` additionally binds queued web/native mutations to the expected signed-in actor; a mismatch returns `403 ACTOR_CHANGED` and cannot change another account's cart. The optional header is not an authentication credential.

Accepted quotes use their own explicit version/expiry agreement and do not require a basket token. RFQs remain compatible with unknown prices or stock. Android preview 1.1.0 (versionCode 2) and the updated browser exports carry consent protocol 1. Older native binaries without the protocol cannot perform new direct checkout after this API update. Both updated APKs passed compiler, signature, origin and bundled-protocol checks, without launching a native simulator or device. Native compilation, runtime execution and distribution retain their separate actual verification dates.

## Frozen order provenance

Every newly priced quote version and direct order appends `OrderPricingSnapshot` rows: actual actor/shop, original decision, expected/base/final price, rule/version/raw terms, campaign permission, explicit override reason and evaluated instant. Current and historical quotes are never recalculated after a tariff change. Explicit manual negotiation remains possible; departing from an applicable custom tariff requires an explanation of at least eight characters. An unavailable zero-quantity line records UNAVAILABLE rather than a zero-price offer.

History is owner/workspace scoped. ADMIN and FINANCE may inspect private rule provenance; merchants see their own order's commercial prices without another customer's rule identifiers, raw agreement definitions or internal override notes. Drivers and warehouse staff cannot read the pricing matrix or history endpoint. Legacy orders truthfully state when no structured provenance exists.

## API and storage

| Endpoint | Scope |
| --- | --- |
| `GET /api/admin/pricing` | ADMIN / FINANCE: rules, segments, assignments and recent actor-attributed events |
| `POST /api/admin/pricing/segments` | ADMIN: create a uniquely named segment |
| `POST /api/admin/pricing/assignments/{merchantId}` | ADMIN: versioned assignment/removal with a reason |
| `POST /api/admin/pricing/rules` | ADMIN: idempotent immutable proposal |
| `POST /api/admin/pricing/rules/{id}/review` | Independent ADMIN / FINANCE: publish or reject a current version |
| `POST /api/admin/pricing/rules/{id}/retire` | Independent ADMIN / FINANCE: reasoned withdrawal |
| `POST /api/pricing/simulate` | Own merchant, or ADMIN / FINANCE with a validated shop |
| `GET /api/orders/{id}/pricing-history` | ADMIN / FINANCE or the owning merchant; paginated |

Five additive tables are migrated separately for SQLite and PostgreSQL: PriceSegments, MerchantPriceSegments, TariffRules, PricingEvents and OrderPricingSnapshots. PostgreSQL migration generation honours an alternate validated dedicated schema. Existing mutation serialization, transaction rollback and optimistic versions coordinate concurrent publication. No new external service or secret is needed; staging keeps its private Data Protection keyring.

## Verification scope

The pricing API suite executes 38 scenarios / 154 HTTP requests, including actual direct checkout, re-confirmation rejection, immutable quote versions, scoped history, negotiated deviation, publication races, campaign compatibility, shared rounding and gift availability. The domain suite has 77 checks, including signed expired consent and tampering. Regression suites cover the existing core, engagement, warehouse, supplier and analytics paths. Real browser checks exercise the actual pricing forms, responsive/dark layouts, mobile checkout and wholesaler quotation controls. Provider evidence distinguishes executed SQLite backup upgrade from generated PostgreSQL SQL and later hosted execution. Reports retain actual dates and origins under `verification/pricing/` when collected for release.

The original 79-page specification and every exact Figma state have separate acceptance requirements. Provider-funded campaign budgets, payment/fiscal authority and external message delivery are not established by customer-price resolution.
