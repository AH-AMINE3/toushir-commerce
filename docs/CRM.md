# Commercial portfolios and assisted quote requests

Open **Portefeuilles & visites** in the wholesaler application. An administrator assigns a primary shop to an individual ADMIN or SALES adviser, with a reason and current portfolio version. SALES sees only assigned shops and CRM navigation. It receives no warehouse, finance, GPS, team administration or general order-access authority. Reassignment removes the old scope, revokes unused named grants and clears an already open dossier/form during ordinary refresh; stale responses cannot restore it.

## Contacts and visits

Contact and visit tasks retain the named assignee, title, priority, UTC due time, status and version. A visit has a 15-minute–8-hour window. Replanning and cancellation require a reason. Completion retains an immutable contact outcome, actual author, observed time and required next action; an optional follow-up creates a separate task. Completion of a future visit is rejected until its permitted window. These are self-reported contact/attendance records, not authenticated call or location evidence. Completed/cancelled tasks and audit events are retained.

## Exact merchant authority

The merchant opens **Mon Toushir → Demandes assistées**. Only the owner or a current Manager may authorize the currently assigned named adviser. Buyer and Viewer see read-only history. The merchant reviews the exact basket quantities, address, note and expiry, then gives explicit consent. The one-use authorization expires in five minutes–seven days, within any primary guest-shop lifetime, and binds the issuer's current shop role/version and the portfolio version.

The adviser reviews the immutable scope and submits one REQUESTED RFQ. No changed price, quantity, address or direct-order mode supplied to the use endpoint can replace that scope. The request records the actual adviser, leaves the personal basket intact and reserves no stock. The wholesaler still issues a normal quote through the shared pricing engine; the shop must accept it. The adviser cannot accept, receive or pay for the shop.

Same-key retries return the original RFQ; another key cannot consume the grant again. Concurrent use has one winner. Revocation, changed address, changed issuer authority, reassignment, suspension or revoked primary guest access prevents use. A later adviser never inherits the old named mandate. Historical used grants/orders remain.

## API and persistence

- Staff: `GET /api/crm`, `GET /api/crm/shops/{id}`; versioned portfolio assignment, task create/reschedule/finish/cancel, contact report and `POST /api/crm/assistance/{id}/use`.
- Merchant: `GET/POST /api/shop/assistance`, `POST /api/shop/assistance/{id}/revoke`.

Five additive provider-specific tables retain portfolios, tasks, contacts, assistance and events. Workspace mutation serialization and live authority revalidation apply. CRM overview refresh currently polls every 15 seconds; it is not an external call, CRM provider, live camera or physical presence integration.

## Executed evidence

`verification/crm/` records 19 API scenarios over 102 requests, 11 browser checks with no JavaScript errors, 91 domain tests, actual SQLite backup upgrade preserving 62 business table counts and alternate-schema PostgreSQL SQL/model checks. Regression reports retain their individual scopes, URLs and timestamps. The browser test keeps a selected adviser dossier and filled form open through reassignment, then releases a held formerly authorized response and confirms no stale detail returns. Independent review scored its single R1 fix resolved; documentation preserved the incumbent design files.

These are local API/browser and provider-generation scopes. Hosted execution is recorded separately after deployment. Native simulators remain off; current React Native browser captures do not certify device keyboard, camera, gestures, accessibility or performance. Android 1.1.0 packages predate this extension until the final native rebuild.
