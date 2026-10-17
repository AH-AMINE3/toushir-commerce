# Toushir — Figma audit and implementation baseline

Captured read-only on 17 October 2026 from file `4IffhmSg2gHg82narIGfMF`. No canvas nodes, text, assets, components, variables or prototype actions were edited. Page activation was used to load each page before reading it.

## Evidence and limits

All 14 pages were inventoried, and every distinct text string was extracted in document traversal order. Repeated instance text is deduplicated; this is not a visual assertion about every screen. The capture includes all 332 local variables, 139 local text styles, and per-screen prototype action type/target summaries. It does not contain every raw conditional-action payload or screenshot; high-fidelity design context is separately required for implementation.

`Figma-SOURCE-SNAPSHOT.json` retains the full captured evidence. `Figma-SCREEN-MATRIX.csv` lists 265 screen/state/reference frames individually: 85 wholesaler frames (84 screens + QR dialog), 111 merchant frames (93 approved + 17 receiving states + menu), 46 driver states, 8 dark previews and 15 cross-role journey boards. Matrix statuses are conservative source-review observations, not completed acceptance tests.

The Figma cites a separate 79-page document, `TOUSHIR_Cahier_des_charges_Grossiste_2026-10-16.pdf`, with 122 requirements in 18 families and 55 acceptance scenarios. Its individual requirement wording and all 55 original scenarios are not embedded in this file and were not supplied for this audit. `Figma-REQUIREMENTS-122.csv` therefore provides 122 **derived checks**, with C-prefixed IDs, mapped to the 18 authoritative family ranges. It must not be described as a verbatim extraction or confirmed mapping of each original ID.

## Interface contract

DM Sans throughout. Action `#06DD81` uses dark `#151917` text. Links `#087F4F`; mint `#D9EDDF`; aqua `#CCFFFF`; brand ink `#0F2C2C`; surfaces `#FFFFFF`/`#F4F6F4`; lines `#E7EBE7`; muted `#6B736F`. Product photos and supplier marks retain their original colors and aspect ratios.

Desktop reference: 1600×1040, sidebar 224, header 64, content padding 32, useful content 1312. Cards radius 16; buttons 40 high/radius 10/padding 12/gap 8. Titles 30–32/40–42; card heading 18/25; body 13–14/18–20; table 12/17. Mobile reference: 402×874, side gutters 20, useful width 362, two columns 175+12+175. Buttons 52 high/radius 12; bottom tabs 68 excluding real safe area. Cart dock 64 high; action 152×44; arrow 20; no cart pictogram. Screen title 24/28 bold, section 22/30, product price 18/25, body 14/19. Test 320–430 widths, enlarged text, FR/AR/RTL and reduced motion.

Five stable merchant tabs: Accueil, Offres, Commandes, Messages, Mon Toushir. The cart remains global and survives navigation. The prototype's one-new-request restriction is a demonstration limit; the application must support multiple independent requests.

## Business invariants

Commercial approval, fulfillment, reception proof, payment and driver cash handover are separate states. A received request is not a firm promise. Amendments preserve old versions; only the explicitly accepted current version can be prepared. No silent product substitution, price change, address rewrite or automatic acceptance on timeout.

Stock availability is eligible physical stock minus reservation and safety; picked stock stays physical and reserved until departure. Reserve atomically. Loading consumes stock and reservation once. Refused cartons stay under driver custody until returned and inspected; returned stock is quarantined before disposition. Supplier receipt example: 100 expected, 96 received = 90 conforming + 6 quarantine, with 4 outstanding.

Depot QR transfers custody against the assigned driver and exact manifest version. Merchant QR binds an approved receipt snapshot to driver, store, shipment, stop, version, nonce and expiry. Consume atomically; reject wrong actor, expired/reused codes and stale versions. The same idempotency key returns the original result. A QR never creates a payment. Losing connectivity before verification must never display authenticated success.

Reference delivery: 10 Cola×1200 + 4 Candia×2000 + delivery1000 =21000 DA. Refuse 2 Cola: accept 12 cartons for18600 DA. Gross20000 minus change1400 =18600 net. Another gross25000 minus6400 gives the same net. Gross18000 is insufficient by600. Route fixture5000 float+19300+21100+18600=64000 expected; counted63500 creates an internal500 discrepancy while the merchant stays settled. Full receipt is an alternative scenario of21000 net, not a second payment.

Figma specifies integer minor monetary units in storage, explicit DZD, UTC storage and Africa/Algiers display. Existing API uses validated decimal DZD; this is an implementation divergence requiring explicit boundary conversion/rounding documentation rather than mixing centimes and dinars.

Rewards example:1280 available→380 available+900 reserved. Delivery consumes reserved900 without another available debit; authorized cancellation releases once. Challenge goal1000 bottles requires167 cartons×6=1002 eligible units. Campaign reward costs zero points, unlike an ordinary point redemption. Only accepted, paid, eligible goods count, with returns corrected. Cola discount at5 cartons:5×1200 less5%=5700 goods; delivery1000. Buy20 cartons/get1 Ifri caps at1 gift per order, including40 cartons.

Messages carry store and exact object context. Internal notes stay internal; read receipts are not commercial acceptance or payment evidence. Promotional preferences are rechecked at dispatch; service recalls remain distinct from marketing. Broadcast fixture62 targeted−7 opted out−3 invalid=52 eligible.

## 3D and live operations

DepotSimulation (`217:2026`) is an illustrative native isometric model with loading/departure/dispatched states. Geometry is not a survey of a real warehouse. A production map must distinguish authenticated observations from simulation/replay, show observedAt/receivedAt/accuracy/freshness, stop invented movement after staleness and expose a readable 2D/list alternative. Merchants see only their shipment; drivers only their assigned stops. 3D animation never authorizes departure, delivery or payment.

## Initial implementation priorities (historical review)

1. Preserve server authorization, stock concurrency, version checks, single-use QR and payment idempotence through every UI action.
2. Route-level manifest custody, bounded capacity, return accountability and independent expected/proposed/counted cash settlement are needed beyond a per-payment reconciliation flag.
3. Purchasing/receiving, contextual messaging, campaigns and point/reward ledgers were absent at initial review. They now have persistent backend implementation and executed API evidence; exact screen/state parity remains a separate acceptance task.
4. Immutable amendment history, lots/expiry/recall, durable outbox/offline replay, real device camera/GPS, notification connectors and legal document sequences require specific evidence; neither a Figma animation nor source presence proves them.
5. The separate 79-page source specification, commercial configuration, payment/stock authority, asset permissions and operational deployment checks must be reconciled before a real merchant pilot.

See `Figma-QA-PLAN.md` for independent acceptance scenarios. The old Figma handoff's “41 tests passed” refers to prior in-memory reference tests, not this application's current backend or deployment.


Current evidence is recorded in `Figma-IMPLEMENTATION-REVIEW.md`: 51 engagement API checks and 29 warehouse API checks passed. The 122 derived rows retain conservative statuses and all 265 exact screen states remain individually unaccepted. Initial gap descriptions above are historical findings; the review identifies which gaps are now closed and which remain.
