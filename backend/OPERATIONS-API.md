# Commerce operations API, version 2

This application runs ABP Framework 10.6.1, ASP.NET Core .NET 10, an ABP EF Core context, PostgreSQL or SQLite, and authenticated SignalR. Existing catalogue, merchant, quotation, inventory, claims and administrative endpoints remain available under `/api`.

## Identity and roles

`POST /api/demo/session` accepts `{role,workspaceToken?}` and returns `{token,workspaceToken,user,workspace}`. Roles are `ADMIN`, `MERCHANT`, `WAREHOUSE`, `DISPATCHER`, `DRIVER` and `FINANCE`. Omitting the capability creates an independent fictional workspace. Switching roles inside an existing workspace requires its random owner capability; a merchant invitation never grants that capability. Workspaces expire after seven days, sessions after sixteen hours. Each account retains at most 32 sessions. Suspension and expired/revoked invitations are checked on authenticated requests.

Real accounts use server-side password hashes and revocable, randomly generated session tokens stored only as hashes. Native clients use the returned Bearer token and platform secure storage. Browsers receive an HttpOnly, SameSite Strict cookie, Secure outside development. Cross-origin writes are checked against exact configured origins. `POST /api/operations/team` creates a real approved team member; only ADMIN may use it, with `{name,email,password,role,phone?}` and a password of 14–128 characters. Production owner provisioning and merchant approval remain explicit.

Authorization is enforced inside API services. WAREHOUSE may prepare and inspect returns; DISPATCHER may assign deliveries; DRIVER may see only assigned deliveries, publish their own GPS during an active assignment, and record physically completed delivery and cash; FINANCE may record payments/refunds and reconcile. ADMIN retains the existing manual operational administration endpoints, which are separately audited; courier receipt verification does not remove that administrative authority.

OpenIddict provides genuine OIDC authorization code with mandatory S256 PKCE. Discovery is `/.well-known/openid-configuration`; protocol endpoints are `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/revoke` and `/connect/logout`. Public clients are `toushir-web` and `toushir-mobile`, with exact configured callback URLs and native `toushircommerce://auth/callback` / `toushirlivreur://auth/callback`. Code expiry is two minutes, access tokens fifteen minutes, refresh tokens eight hours with rotation and replay rejection. API JWT validation also checks the live backing session/account/invitation. Interactive `/auth/login` validates antiforgery and existing password hashes; successful POST returns an HTML relay to a fresh same-origin authorization GET so CSP also permits registered external/native callbacks. Password grant is disabled. Stable deployed certificate keys and an HTTPS issuer must be configured for long-lived deployments. Legacy opaque sessions remain supported for demos and compatibility; they are distinct from OIDC tokens.

## Operational reads

- `GET /api/operations/dashboard`: counts for awaitingQuote, preparing, ready, enRoute and delivered; prepared units, current outstanding balance and fresh driver count. STAFF roles only; FINANCE does not receive live coordinates.
- `GET /api/operations/orders`: `{items:[OperationalOrder],total}`, up to 250 latest workspace orders. ADMIN, WAREHOUSE, DISPATCHER or FINANCE.
- `GET /api/operations/orders/{id}`: full scoped order including timeline, payment records, balances and promotion snapshots; allowed operations staff and no GPS.
- `GET /api/operations/team`: `{items:[{id,name,role,phone,approvalStatus}]}`. ADMIN, WAREHOUSE or DISPATCHER.
- `GET /api/courier/manifest`: the same operational order collection, scoped to the authenticated DRIVER's assignments.
- `GET /api/orders/{id}/tracking`: merchant's own order, assigned driver or permitted operations team. Returns `{order,version,preparation,assignment,location,receipt}`.

`OperationalOrder` includes `id,number,status,paymentStatus,total,version,createdAt,updatedAt,merchant:{id,name,shopName,phone},items:[{id,productId,name,brand,imageUrl,quantity,preparedQuantity,acceptedQuantity,refusedQuantity,unitPrice,lineTotal}],address:{id,label,wilaya,commune,street},assignment,balance:{due,collected,remaining,refundDue},note,adminNote,currency`.

`preparation` includes `{prepared,total,percentage,items}`. Assignment includes `{id,driverId,driverName,driverPhone,status,assignedAt,pickedUpAt,arrivedAt,failureReason}`. A visible location includes `{latitude,longitude,accuracy,capturedAt,receivedAt,isFresh,ageSeconds,source:'DEVICE_GPS'}`. Coordinates appear only during that active delivery with consent and are hidden after completion/failure/revocation. A position older than 120 seconds is visibly stale; the API never fabricates vehicle movement or interprets device coordinates as verified carrier telemetry.

## Preparation, dispatch and GPS

`PATCH /api/operations/orders/{id}/preparation` accepts `{version,items:[{productId,preparedQuantity}]}`. ADMIN/WAREHOUSE only; CONFIRMED/PREPARING only. Quantities cannot exceed agreed order quantities. Partial preparation sets PREPARING; all lines prepared sets READY. Stale versions return `409 VERSION_CONFLICT`.

`POST /api/operations/orders/{id}/assign` accepts `{driverId,version}`. ADMIN/DISPATCHER only, same-workspace approved DRIVER, before pickup. Assignment may precede complete preparation.

`POST /api/courier/orders/{id}/pickup` accepts `{version}`. Assigned DRIVER only, READY with every line fully prepared. This records physical pickup, performs the existing atomic inventory dispatch and moves the order OUT_FOR_DELIVERY.

`POST /api/courier/orders/{id}/status` accepts `{status:'EN_ROUTE'|'ARRIVED'|'FAILED',reason?}`. It requires physical pickup. FAILED requires a reason and does not invent a delivery or reverse stock/payment. Retrying the route requires an explicit subsequent EN_ROUTE action.

`POST /api/courier/location` accepts `{latitude,longitude,accuracy,capturedAt,consent}`. True consent requires a current picked-up assignment, finite valid coordinates, accuracy 0–10,000 metres, a capture time no older than five minutes or more than thirty seconds in the future, and a capture time newer than the previous fix. `consent:false` immediately hides sharing; the other fields may be zero/default when stopping. Native clients still need the operating system's location permission.

## Merchant receipt proof and cash

`POST /api/orders/{id}/receipt-challenge` is merchant only, for their own order after the assigned driver records ARRIVED. Input is `{version,items:[{productId,acceptedQuantity,refusedQuantity}]}`. Every shipped product must appear once; accepted plus refused must exactly equal shipped quantity. It returns `{challenge,expiresAt,orderId,version,merchantId,driverId,qrPayload}`. The random token is stored only as a hash and bound to workspace, order, current order version, merchant, assigned driver and normalized quantity hash. It expires after three minutes; generating another invalidates previous pending proofs.

`POST /api/courier/orders/{id}/complete` accepts `{challenge,items:[{productId,acceptedQuantity,refusedQuantity}],note?}`. Assigned DRIVER only, ARRIVED. Quantities must match the merchant's proof, which is consumed once inside the delivery transaction. A proof record is preserved. Delivery updates accepted/refused quantities and quarantines refused goods. It does not record payment.

`POST /api/courier/orders/{id}/collect` accepts `{amount,tendered,change,method:'CASH',idempotencyKey,reference?}` after completed receipt. Net cash must satisfy `tendered - change == amount`; net cannot exceed the outstanding amount. Matching retries return the same financial outcome. Reusing a key with changed tender/change/amount returns 409. Cash records are separate from payment reconciliation.

FINANCE/ADMIN endpoints are `POST /api/operations/orders/{id}/payments` using the existing PaymentInput, `/refunds` using RefundInput, and `/api/operations/payments/{id}/reconcile`. WAREHOUSE/ADMIN use `POST /api/operations/orders/{id}/returns/inspect` with `{productId,quantity,disposition:'RESTOCK'|'WRITE_OFF',reason}`. Quarantined quantities never return to sale merely because delivery was refused.

## Live updates and transactions

SignalR hub: `/hubs/commerce`. Authenticate with same-origin cookie or native Bearer/access-token transport. Hub connections close at authentication expiry. `SubscribeOrder(orderId)` rechecks the live session and record scope; arbitrary group names and client publishing methods are unavailable. Account, operations and order groups include workspace identity. Events `order.updated`, `tracking.updated` and `operations.updated` carry minimal invalidation metadata, not live commercial documents or coordinates. Clients refetch the authorized API and should retain polling/reconnect recovery.

All writes use the existing serialized database transaction boundary. The ABP context is scoped so each request's services share that transaction. Events publish only after commit; a failed operation cannot publish a successful update. Live transport delivery failure does not roll back already committed business records. This is immediate invalidation, not a durable outbox; reconnecting clients must refetch.

## Database upgrade and verification

The additive `20261017-operations-v1` upgrade preserves existing records, adds order version/prepared quantity columns, creates operational tables/indexes using the provider's schema mapping, and records `CommerceSchemaHistory`. Startup is repeatable. Existing compatibility upgrades for catalogue, claims and invitations remain. SQLite is a single-instance deployment; PostgreSQL supports the same models and exact decimal money. PostgreSQL's generated schema is provider-tested separately; deployment-specific behavior requires hosted verification.

`scripts/verify-operations.mjs` exercises real HTTP and authenticated WebSocket flows in isolated fictional workspaces: roles and capability, RFQ, reservation, preparation/version conflicts, assignment isolation, pickup, GPS consent/freshness, merchant quantity proof, courier receipt, cash/change idempotency, finance reconciliation and return quarantine inspection. Reports contain no credentials or receipt tokens.

## Route capacity, stop order and depot return

`POST /api/operations/routes` requires `{name,driverId,orderIds,openingFloat,vehicleReference?,capacityUnits}`. `orderIds` determines ranked stop order. Capacity is 1–1,000,000 commercial units; route load counts paid quantities plus separately promised physical gifts. No weight is inferred. `409 ROUTE_OVER_CAPACITY` rejects overload. `POST /api/operations/routes/{id}/reorder` accepts `{version,orderIds,capacityUnits}`, requires the exact same stop set before custody, and invalidates earlier manifest proofs by incrementing route version.

Route responses include `capacityUnits`, `load:{paidUnits,giftUnits,totalUnits,capacityUnits,remainingUnits,unit:'COMMERCIAL_UNIT'}` and `returns:[{orderId,version,number,state,items:[{productId,name,quantity,remainingQuantity}],requiresReceipt}]`. Return states are PENDING_RETURN, DEPOT_RECEIVED or INSPECTED. `POST /api/operations/routes/{id}/returns` is ADMIN/WAREHOUSE and accepts `{orderId,version,items:[{productId,quantity}],note}`; `version` is the **order version**. Quantities must match the remaining complete depot return, including failed stops. This records physical receipt into quarantine and never restocks automatically. Cash declaration requires every included order resolved, regular returns received/inspected and separate gift custody completed or returned.

All JSON field amounts retain their named unit: order/payment/route fields use decimal DZD; supply `unitCostMinor` and `invoiceMinor` use integer centimes. Quotes accept optional expected order `version` and `campaignIds` (`null` auto-selects eligible rules, `[]` applies none, explicit IDs select validated rules); stale supplied versions reject with409. Existing quote adapters that omit version remain supported.
