# Engagement, rewards and supply API

All routes are authenticated under `/api` and inherit the application transaction filter. JSON uses camelCase. Mutations perform explicit workspace/object/role checks. Supply costs use integer **centimes DZD** (`unitCostMinor`); the existing customer quote/payment API uses decimal **dinars DZD**. Do not mix these units.

## Conversations and broadcast

- `GET /conversations` → `{items,total}` scoped to own merchant, assigned driver delivery, or authorized staff.
- `POST /conversations {contextType:GENERAL|ORDER|DELIVERY|GIFT,contextId?,subject,merchantId?}`. General context is canonical per merchant. Order/delivery/gift objects must belong to the merchant. Drivers can open only their assigned delivery.
- `GET /conversations/{id}/messages` → `{conversation,items,deliveryChannel:IN_APP}`. Internal notes excluded for merchants/drivers.
- `POST /conversations/{id}/messages {text,internal,idempotencyKey}` → persisted message. Same key/body returns original; changed body returns409. Notes require staff. Closed threads reject messages.
- `PATCH /conversations/{id} {version,status:OPEN|CLOSED,assignedTo?}` → staff triage; assignee must be active staff in this workspace.
- `GET /preferences/marketing`; `PUT /preferences/marketing {optIn}`. Default opt-out; only the merchant edits its own preference.
- `GET/POST /admin/broadcasts`; creation `{purpose:PROMOTIONAL|SERVICE,title,body,merchantIds,idempotencyKey}`. `POST /admin/broadcasts/{id}/dispatch` rechecks approval and promotional consent then creates at most one in-app message per target. Returns sent/excluded counts and recipient statuses. This does not send SMS, WhatsApp, email or push.

## Campaigns

- `GET /campaigns` → published campaigns; `GET /admin/campaigns` → drafts and published.
- `POST /admin/campaigns {title,description,kind:DISCOUNT|FREE_GOODS|CHALLENGE,productId,minimumQuantity,discountPercent,giftProductId?,giftQuantity,goalQuantity,unitFactor,rewardId?,awardLimit,startsAt,endsAt,claimUntil?}`. ADMIN creates draft.
- `POST /admin/campaigns/{id}/publish {version}` requires ADMIN or FINANCE **different from creator**. Published terms are immutable; future revisions require a new campaign.
- `POST /campaigns/{id}/preview {quantity}` → threshold/period/rounding-aware preview. Discount uses current offer price. Free goods cap at one configured gift per order. The preview itself does not mutate an order; actual quote integration is described below.
- `GET /campaigns/{id}/progress?merchantId=` uses accepted, paid units for the product during the campaign, subtracts physically received returns, and multiplies the explicit unit factor.
- `POST /admin/campaigns/{id}/award {merchantId}` verifies goal after campaign end and before claim expiry; atomically consumes award budget; same merchant gets the same grant. Claiming a challenge gift costs zero points.

## Points and gifts

- `GET /loyalty?merchantId=` → `{available,reserved,spent,correctionDue,entries}`. Merchants see their own wallet; staff may request an authorized shop.
- `POST /loyalty/sync {merchantId?}` reconciles completed paid orders with the immutable ledger. One point per full 100 DA of accepted paid goods, excluding shipping. Physically received returns stop earning immediately. Eligibility is bounded by both retained goods and the net commercial subtotal, so a later credit does not reverse the same return twice. Repeat is a no-op. A correction beyond available points is recorded as `correctionDue`; available never becomes negative. Future accrual/cancellation settles this review balance first. Fulfillment is blocked while such correction remains.
- `GET /rewards` → `{items}`; ADMIN `POST /admin/rewards {name,description,imageUrl?,pointsCost,availableStock,isActive}` and `PATCH /admin/rewards/{id}` with the same body plus `version`.
- Merchant `POST /rewards/{id}/reserve {idempotencyKey,grantId?}` atomically checks points and stock. Returns `GiftRequest` in RESERVED state. A grant must belong to this merchant/reward and remain eligible.
- `GET /gift-requests`; staff may use `merchantId` filter.
- `POST /admin/gift-requests/{id}/transition {version,status:READY|FULFILLED|CANCELLED,proofReference?,reason?}`. Fulfillment requires READY and a proof reference; consumes reserved points once without another available debit. Cancellation before fulfillment releases stock/points once. Completed delivery cannot be silently canceled. Repeated terminal state returns existing object; conflicting proof rejected.

## Suppliers and receiving

- ADMIN `POST /admin/suppliers {name,contact,phone,note?}`; staff `GET /admin/suppliers`.
- ADMIN `POST /admin/purchases {supplierId,expectedAt,items:[{productId,quantity,unitCostMinor}],note?,idempotencyKey}` → `{order,supplier,items,receipts,receiptLines,totalMinor,receivedMinor}`.
- `GET /admin/purchases` → summaries; `GET /admin/purchases/{id}` → full detail. Customer merchants/drivers have no access.
- `POST /admin/purchases/{id}/approve {version}` requires ADMIN/FINANCE different from creator.
- ADMIN/WAREHOUSE `POST /admin/purchases/{id}/receive {version,idempotencyKey,lines:[{productId,receivedQuantity,quarantineQuantity,lotNumber,expiresAt?}],note?}`. Quantities cannot exceed outstanding; expired items must be entirely quarantined. In one transaction, conforming units increase sellable physical stock, quarantine increases isolated stock and a receipt/lot/movement records evidence. Replays do not increase stock twice.
- `POST /admin/purchases/{id}/inspect {receiptLineId,version,quantity,disposition:RELEASE|SUPPLIER_RETURN|DESTROY,reason,idempotencyKey}`. Release only nonexpired isolated quantity; all dispositions bounded and recorded.
- FINANCE/ADMIN `POST /admin/purchases/{id}/invoice-review {version,invoiceReference,invoiceMinor}` compares invoice to actual received units×cost. Returns MATCHED/DISCREPANCY and no payment is created.

## Live invalidation and tests

The module appends `EngagementNotices` in the same transaction as a business operation. A background publisher sends `engagement.updated {eventId,kind,aggregateId,at}` to authorized workspace staff and the relevant merchant; no message text crosses this channel. Internal note events never go to merchant groups. Invalidation is at least once; clients refetch authorized projections. Pending attempts remain inspectable at `GET /admin/engagement/outbox` and do not undo operations.

## Promotions on actual orders

`POST /admin/orders/{id}/quote` accepts optional `campaignIds:[id,…]` and `version` alongside normal quote fields. An empty list selects none; omission selects one deterministic applicable rule per product (highest discount first, otherwise free goods, stable ID tie-break). Explicit selections must be published, active, in this workspace, above the threshold and at most one per product. No implicit stacking. `items[].unitPrice` in the request is the **base** price. The returned order line contains the effective discounted price. Editors revising a quote must use the snapshot's `baseUnitPrice` as their base input; do not submit its already discounted price as a new base.

`PromotionsService` freezes campaign ID/version, base/applied price, threshold, gift and discount totals at each quote version. Revising a quote keeps the previous snapshot as SUPERSEDED. Merchant acceptance reserves normal goods and promised free goods atomically; departure consumes both stocks once. Accepted quotes keep their exact unit price after campaign expiry or partial refusal. Refused/returned units reverse only their own recorded discount benefit; they never trigger an undisclosed price increase on accepted units.

`GET /orders/{orderId}/promotions` and `order.promotions` expose `{items,events,selectionPolicy,…}`. Each snapshot includes `id,quoteVersion,campaignTitle,kind,productId,baseUnitPrice,appliedUnitPrice,quotedDiscountMinor,effectiveDiscountMinor,giftProductId,giftProductName,giftQuantity,giftStockState,giftAcceptedQuantity,giftRefusedQuantity,eligibilityReviewRequired,version`. Discount totals are integer centimes; unit prices are decimal DZD. Free goods are separate zero-priced custody records; they never enter paid line quantities, payment totals, point acquisition or challenge progress.

- Driver/admin `POST /orders/{orderId}/promotions/{id}/gift-receipt {version,acceptedQuantity,refusedQuantity,proofReference,idempotencyKey}` requires ordinary receipt complete and, for any gift accepted, the qualifying goods accepted and paid. Wrong actor, insufficient trigger quantity, stale version or missing payment is rejected. If the threshold is lost, the gift must return; no extra customer debt is invented.
- Depot `POST /orders/{orderId}/promotions/{id}/gift-return {version,disposition:QUARANTINE,reason,idempotencyKey}` records physical return into isolation once.
- Depot `POST /orders/{orderId}/promotions/{id}/gift-inspect {version,disposition:RELEASE|DESTROY,reason,idempotencyKey}` releases or destroys only the actually returned quantity.

A post-delivery return that invalidates an already handed-over gift opens `eligibilityReviewRequired`; it does not invent a physical return, reclaim money or silently modify the original proof. Inventory movements and promotion events retain their actor and operation key.

Run `python3 scripts/test-engagement.py http://127.0.0.1:5882` against an explicitly enabled demo server. It creates its own isolated workspace and checks real persistence, state guards, object isolation, replay, money units, reward balances, supplier receiving and campaign rules. The final local SQLite run passed 51/51 checks; see `Figma-ENGAGEMENT-TEST-RESULTS.json`. This result does not establish production PostgreSQL, device hardware, or every Figma screen state.
