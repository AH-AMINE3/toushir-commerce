# Figma implementation evidence review

Reviewed 17 October 2026. This record separates the captured design, source presence, executed scenarios, and exact screen/device acceptance. Figma cites a separate 79-page specification whose contents were not supplied. The 122 rows are therefore derived checks allocated from the 18 family ranges; their original individual wording and ID correspondence remain unverified.

## Executed API evidence

- Engagement: **51/51 checks**, `2026-10-17T06:47:58.331387+00:00`, real .NET 10/ABP API and SQLite. Evidence: `Figma-ENGAGEMENT-TEST-RESULTS.json` and `scripts/test-engagement.py`. Covers persistent messages/private notes, opt-in broadcasts, supplier receipt/invoice review, independent campaign publication, actual quote discounts and free goods, version history, partial delivery/return corrections, separate gift custody, concurrent challenge claims, point reservations and fulfillment. A physical return stops earning points immediately; stale-wallet spending fails before a commercial credit, and the later credit does not subtract the same return twice.
- Warehouse: **29/29 checks**, `2026-10-17T06:48:31.295933+00:00`, real API and SQLite upgraded from the earlier module schema. Evidence: `Figma-WAREHOUSE-TEST-RESULTS.json` and `scripts/test-warehouse.py`. Covers explicit counted-lot activation, FEFO split and concurrent last-unit reserve, barcode/batch validation, persistent waves, idempotent scans, independent gift tasks, physical recount versions, supplier batches, quarantine, returned-batch identity and shipped-quantity caps, and routed depot return before restock.
- Core backend evidence is stored separately: `backend/verification.json` (15 API checks), `operations-verification.json` (9 operational/live checks), `route-verification.json` (6 route/cash checks), `oidc-verification.json` (6 authorization checks), and `production-verification.json` (7 production-mode/restart checks). Those artifacts identify their own run time and scope. They do not establish production PostgreSQL performance, every browser state, or native-device acceptance.

The warehouse provider migrations create five new tables and indexes without altering or dropping existing tables. The backend owner generated and reviewed both SQLite and PostgreSQL migrations; real deployment/provider evidence remains separate from these local SQLite scenario runs.

## Derived checklist counts

- API scenario tested; not original requirement acceptance: 48
- Missing full workflow: 5
- Partial scope: 27
- Requires environment/business evidence: 3
- Source reviewed; not full acceptance: 39

No row is marked as acceptance of all original requirements. Each row keeps an evidence path and its practical limit. All 265 screen matrix entries remain individually unaccepted until that exact screen/state has been visually and functionally checked. A shared route, component, or module test cannot establish complete Figma parity.

## Remaining scope and release gates

1. The current scope supports a reviewable commerce demo. A full manufacturer recall lifecycle, supplier confirmation document lifecycle, CRM visits/tasks/mandated assisted ordering, multiple members per shop, and a complete customer tariff matrix remain incomplete. The missing original specification prevents a truthful claim that all 122 original requirements are accepted.
2. Lot control is opt-in after a physical count; aggregate legacy stock is explicitly untracked. The implemented warehouse path includes real scans and FEFO. Bins/aisles, automated expired-lot reallocation, camera-scanner hardware acceptance, unit serialization and recall communication are not claimed. Route capacity uses quantity units, including gifts, rather than physical weight/volume optimization.
3. In-app messaging and durable invalidation notices are real. SMS, WhatsApp, email and native push providers, delivered/read acknowledgements, attachment lifecycle and delivery cost/rate policies require their actual integrations. An outbox notice is not proof of external delivery.
4. Seller/tax identities and document numbering, bank-settlement authority, funded program terms, data retention, FR/AR/RTL, target load/performance, and demonstrated backup restoration/RPO/RTO need concrete business or environment evidence before real trading. Native simulator/device and public hosting results belong to their own acceptance records.
5. The 3D depot is illustrative. Actual GPS observations, freshness and simulation must remain visibly distinct. Animation does not authenticate preparation, movement, receipt or payment.

## Focused independent domain/security review

The reviewed fixes include atomic campaign-grant claim; reconciliation before points spending/fulfillment; immediate physical-return point correction without a later double subtraction; prospective challenge publication; bounded supplier movements; current-quote gift filtering; route capacity and preserved stop order; depot custody before managed returned stock is sellable; and per-batch return quantity limits. Claim status/received/inspected/credited fields now participate in optimistic concurrency so a stale source row cannot consume another return's quarantine.

The authentication/schema review covered OIDC configuration/endpoints, session adapters, database configuration, schema initialization and module upgrades. Dedicated PostgreSQL identifiers are validated and shared-public search paths excluded. Missing dedicated PostgreSQL schema fails closed. Signed tokens still require a live session/account/invitation; authorization code flow requires S256 PKCE and exact client redirects. This is a focused code review and executed scenario record, not a security certification.
