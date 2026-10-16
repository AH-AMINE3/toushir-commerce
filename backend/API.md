# Toushir wholesale API v1

Base `/api`, JSON camelCase, amounts in DZD (decimal; never cents). Authentication: `Authorization: Bearer <token>` for native clients; same-origin HttpOnly session cookie also supported. Every response/error has HTTP status; errors `{error:{code,message,details?}}`. Role names `ADMIN`, `MERCHANT`. Global product identities are public provenance; tenant offers/orders/users are isolated.

## Bootstrap and authentication

- `POST /demo/session {role?:"MERCHANT"|"ADMIN",workspaceToken?:string}` → `{token,workspaceToken,user,workspace:{id,name,isDemo:true,pricingMode:"ON_REQUEST"}}`. Omitting workspaceToken creates isolated fictional workspace. Preserve workspaceToken to switch persona within it. Available only when `Demo:Enabled=true`.
- `POST /auth/login {email,password}` → `{token,user,workspace}`; `POST /auth/register {name,shopName,email,phone,password,address:{label,wilaya,commune,street}}` → same shape, approval `PENDING`. Production owner must approve merchant before requesting/placing orders.
- `GET /auth/me` → `{user,workspace}`. `POST /auth/logout` revokes session.
- `POST /auth/change-password {currentPassword,newPassword}` verifies the old password, changes the hash and revokes all sessions (sign in again).
- `GET/PATCH /profile`; `GET/POST /addresses`, `PUT/DELETE /addresses/{id}`.

## Catalog and cart

- `GET /products?q=&category=&brand=&inStock=&page=1&pageSize=24&sort=name|price_asc|price_desc|newest` → `{items,total,page,pageSize,totalPages}`.
- `GET /products/{id}` → product; `GET /categories` → `[{id,name,count}]`; `GET /brands` → `[{name,count}]`.
- Product `{id,barcode,name,brand,category,imageUrl,unit,caseSize:number|null,minimumQuantity,price:number|null,stock:number|null,pricingMode:"ON_REQUEST"|"FIXED",offerApproved,source,sourceUrl,sourceLicense,imageLicense,countryTags}`. Unknown stock/price remain null. Case size defaults 1 only for request quantity and is not a verified trade pack.
- `GET /favorites` → products; `POST /favorites/{productId}`, `DELETE /favorites/{productId}`.
- `GET /cart` → quote; `PUT /cart {items:[{productId,quantity}]}` replaces saved cart and returns quote. `POST /cart/quote {items:[...]}` quotes without saving.
- Quote `{items:[{product,quantity,unitPrice,lineTotal,available,issues:[]}],subtotal:number|null,deliveryFee:number|null,total:number|null,needsQuote,canOrder,currency:"DZD"}`. Unknown prices never create a made-up total.
- A delivery fee remains null until agreed in a quote or explicitly configured as `Wholesale__DeliveryFee`. Without that tariff direct PLACE_ORDER returns QUOTE_REQUIRED, even with published product offers. Admin product responses additionally include `physicalStock`, `reserved`, `quarantined`, `savedPrice` and `savedCaseSize` for lossless offer editing.
- `GET /catalog/provenance`, `GET /catalog/export` expose source and license metadata; public `/catalog/products.json` and `/catalog/README.md` make licensed catalog data and attribution available. Retired manifest products disappear from new catalogs while historical order snapshots remain intact.

## Merchant orders / requests

- `POST /orders {idempotencyKey,addressId,items:[{productId,quantity}],note?,mode?:"REQUEST_QUOTE"|"PLACE_ORDER"}` → order. REQUEST_QUOTE creates `REQUESTED`, no stock reservation. PLACE_ORDER requires every published approved offer/stock and creates `SUBMITTED`, reserving stock atomically. Same key/payload returns same order, changed payload 409.
- `GET /orders?status=&q=&page=1&pageSize=20` → `{items:[OrderSummary],total,page,pageSize,totalPages}`, scoped to merchant unless ADMIN. Summary includes id/number/status/paymentStatus/total/date/merchant/itemCount/quantity/imageUrl/balance, without full items or timeline; fetch GET `/orders/{id}` for complete detail.
- `GET /orders/{id}` → order; `POST /orders/{id}/accept-quote {version}` atomically accepts current unexpired quote and reserves confirmed stock → `CONFIRMED`.
- `GET /orders/{id}/document` → escaped, printable quote/order HTML under the same customer/workspace authorization. Browser uses its HttpOnly cookie; do not put tokens in document URLs. This is a commercial document, not a fiscal invoice.
- `POST /orders/{id}/decline {reason?}` → `DECLINED`; `POST /orders/{id}/cancel {reason}` allowed merchant from REQUESTED/QUOTED/SUBMITTED/CONFIRMED. Cancelling releases reservations only when held.
- `POST /orders/{id}/reorder` replaces current cart from original requested quantities and returns recalculated quote (does not duplicate order).
- Order `{id,number,status,paymentStatus,merchant:{id,name,shopName,phone},items:[{id,productId,name,brand,imageUrl,requestedQuantity,quantity,unitPrice,lineTotal,caseSize,acceptedQuantity,refusedQuantity}],subtotal,deliveryFee,total,currency,quoteVersion,quoteValidUntil,createdAt,updatedAt,address:{...},note,adminNote,balance:{due,collected,remaining,refundDue},timeline:[{id,action,at,actor,details}],payments:[...],reservationHeld,isDemo}`.

## Wholesaler administration

- `GET /admin/dashboard?days=30` → `{summary:{requests,newOrders,activeOrders,deliveredOrders,sales,collected,remaining,merchants,products,lowStock},daily:[{date,orders,sales,collected}],statusCounts:[{status,count}],topProducts:[{productId,name,quantity,revenue}],recentOrders:[...]}`.
- `GET /admin/orders` uses same paging as `/orders`.
- `POST /admin/orders/{id}/quote {items:[{productId,quantity,unitPrice,caseSize?,availableStock?}],deliveryFee,validUntil,note?}` → versioned QUOTED. Every requested line must be represented; quantity may be 0 for unavailable. Stock must be confirmed before merchant can accept. `availableStock` updates total physically sellable stock with a ledger entry, never below existing reservations.
- `POST /admin/orders/{id}/transition {status,reason?}` allowed SUBMITTED→CONFIRMED→PREPARING→READY→OUT_FOR_DELIVERY; REQUESTED/QUOTED/SUBMITTED→REJECTED; SUBMITTED/CONFIRMED→CANCELLED. Quotes require merchant acceptance.
- `POST /admin/orders/{id}/delivery {items:[{productId,acceptedQuantity,refusedQuantity}],note?}` after OUT_FOR_DELIVERY. Each line must reconcile all shipped units. Refused quantities enter quarantine; DELIVERED/PARTIALLY_DELIVERED/DELIVERY_REFUSED. Payment stays independent.
- `POST /admin/orders/{id}/payments {amount,method:"CASH"|"TRANSFER",reference?,idempotencyKey}` records actual collection, never inferred from shipping. `POST /admin/payments/{id}/reconcile` records reconciliation. `POST /admin/orders/{id}/refunds {amount,reference,reason,idempotencyKey}` records refund when overpaid.
- `POST /admin/orders/{id}/returns/inspect {productId,quantity,disposition:"RESTOCK"|"WRITE_OFF",reason}` handles physically returned quarantine, audited.
- `GET /admin/products` paginated catalog plus unpublished/unknown offers; `PATCH /admin/products/{id} {price:number|null,stock:number|null,caseSize,minimumQuantity,offerApproved,isActive}` publishes only explicitly approved complete offers.
- `POST /admin/inventory/adjust {productId,delta,reason}` physical stock adjustment, cannot consume reservations. `GET /admin/inventory?productId=&page=&pageSize=` → ledger.
- `GET /admin/merchants?q=&status=&page=&pageSize=` → paged; `PATCH /admin/merchants/{id} {approvalStatus:"APPROVED"|"PENDING"|"SUSPENDED"}`.
- `GET /admin/audit?page=&pageSize=` → paged append-only audit.
- `POST /admin/catalog/import` `{csv:"barcode,price,stock,caseSize,minimumQuantity,offerApproved\n..."}` validates the whole import before applying, identity barcode must exist, no silent new brand/media creation.
- `GET /admin/export/orders` CSV scoped to current wholesaler workspace.

Production must explicitly provision owner credentials and a durable database location. Public demo catalogs use real source identities but unknown own prices and stock; example orders, when provided, are explicitly fictional. No real payments, messaging, carrier or credit provider is implied.

## Post-delivery claims and explicit commercial credits

- `POST /orders/{id}/claims {productId,quantity,reason}` is merchant only after DELIVERED/PARTIALLY_DELIVERED. Open/accepted claim quantities cannot together exceed the actually accepted units. GET order detail adds `claims`, `credits`, `creditTotal`.
- Claim `{id,orderId,productId,productName,quantity,reason,status:"OPEN"|"ACCEPTED"|"REJECTED",resolutionNote,createdAt,resolvedAt,receivedQuantity,inspectedQuantity,creditedAmount}`.
- `GET /admin/claims?status=&page=&pageSize=` returns paged claims with orderNumber/shopName.
- `POST /admin/claims/{id}/resolve {status:"ACCEPTED"|"REJECTED",note}` records a decision; no inventory, order value or payment changes occur from that decision.
- `POST /admin/claims/{id}/receive {quantity,note}` explicitly receives authorized physical units into quarantine, bounded by approved quantity.
- `POST /admin/claims/{id}/inspect {quantity,disposition:"RESTOCK"|"WRITE_OFF",reason}` handles received, uninspected units only; physical inventory and ledger change explicitly.
- `POST /admin/claims/{id}/credit {amount,reason,idempotencyKey}` explicitly grants a commercial credit, only after accepted claim and physical receipt. Maximum cumulative credit is received units × the original immutable unit price. The current subtotal/total reduce by that explicit amount; original product/quantity/unit-price snapshots stay intact. GET detail and printable document show separate credit records. An overpayment creates refundDue, and `/admin/orders/{id}/refunds` must separately record the actual repayment. Repeated identical credit key is idempotent; changed payload 409. Acceptance/receipt/inspection never silently implies a credit or refund.
