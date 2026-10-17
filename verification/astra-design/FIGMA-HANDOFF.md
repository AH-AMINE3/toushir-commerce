# TOUSHIR — Astra v2 · Figma handoff

The editable revision is in the original TOUSHIR file, on **40 — TOUSHIR · Astra v2 · Référence produit**. The original screens and shared components remain available for comparison.

[Open the design cover](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=267-2376) · [Open the prototype](https://www.figma.com/proto/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=267-2376)

The supplied page 158:271 is the original index. It now contains the added **Astra v2 — Version à intégrer** entry, node **267:2406**, linking to this prototype. Existing index boards 160:2 and 243:2 were retained.

## Scope and evidence

- **41 editable screen states**, 5,007 native nodes, 1,351 text nodes, 552 linked instances from 94 original components, 1,833 auto-layout containers and 67 image-fill nodes.
- Source composition retained: DM Sans, native logo/product imagery and Lucide/Feather components, original navigation, card proportions and cart dock.
- Recursive prototype audit: **460 internal navigation actions, zero broken internal destinations, zero references to the original prototype variables**.
- 149 prototype variables isolated in the v2 collection. The first-add flow initializes an empty basket, adds one Cola carton, and carries that state into the basket.
- 74 distinct peripheral destinations open the original prototype in a separate tab. They are explicitly described on the cover as original references outside this revision.
- Structural checks cover all 41 states. Visual checks cover the core Home, Offers, Orders, Account, Activity, Overview, empty-home, first-add, submission, recovery and amendment screens, plus the cover. Conditional branches have not all been manually replayed.
- No claim of complete 265-state application parity. Dark semantic values are defined, but this milestone validates the light reference layouts.

## Main frame handoff

| Surface | Original | Astra v2 |
|---|---|---|
| Home, active order | 93:48 | [261:21643](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=261-21643) |
| Offers | 93:260 | [261:21868](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=261-21868) |
| Orders | 93:406 | [261:22049](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=261-22049) |
| Account | 93:688 | [261:22307](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=261-22307) |
| Activity | 97:3472 | [261:22473](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=261-22473) |
| Wholesaler overview | 179:56 | [261:24927](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=261-24927) |
| First-add starting state | New conditional variant | [265:28696](https://www.figma.com/proto/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=265-28696) |

The complete source-to-version mapping and affected-node ledger are in `final-node-ledger.json`. `final-audit.json` records current frame bounds, native structure, retained original references and reused component identities. `final-prototype-ledger.json` records variable isolation, remapping and explicit peripheral exits. Earlier discovery/ledger files are historical snapshots; the three **final-** files supersede their incomplete counts.

## Visual decisions

Offers retains the brand rail, large discount hierarchy, product backplate and green full-width CTA. Its filter controls are 44px tall. Account retains the compact black 362×179 loyalty card; rewards use lavender; the document row says “Documents et règlements.” Orders retains the delivery hero, ETA, driver, amount and tracking CTA. Activity retains the compact payment summary, paired metrics and recent orders.

Merchant reference geometry is 402×874: status 30, header 64, bottom navigation 68, safe area 18 and cart dock 64. Dock panel is 362×56 and action 152×44.

Overview retains four independent 316×146 white KPI cards, then an 872×330 chart beside a 424×330 priority card, then three lower cards. Frame is 1600×1040, sidebar 224, topbar 64 and content inset 32. Native package icon replaces the unsupported rate ring. The chart distinguishes sales in green from net collections in slate blue.

## Semantic roles

New collection: `VariableCollectionId:261:21630` — **Toushir / Astra v2 / Semantic roles**. Original collections are unchanged.

| Role | Reference | Dark |
|---|---|---|
| Information / transit | #456B8C | #A7C9E5 |
| Information surface | #EAF1F7 | #203849 |
| Pending / reward | #6C5A92 | #D0BFEA |
| Pending / reward surface | #F0ECF7 | #382E49 |
| Secondary text | #5F6D65 | #A5B8AB |

The Dark values above are aligned with the accepted, tested implementation; the original collections and Reference values are unchanged. Exact variable mutations and readback evidence are recorded in `final-audit.json`.

Core ink #151917, white, green #087F4F and original bright action green remain authoritative. Amber status bindings in the revised screens were replaced with lavender. Measured light text pairs pass 4.5:1; see `token-contrast.json`.

## Data and interaction contract

Figma values are demonstration content, not live business facts. The application must use its actual server data, preserve empty states and conditionally omit the active-order banner. Promotion percentage/minimum are examples; the final discounted price comes from the quote/campaign preview endpoint.

Overview uses received sales, open received/submitted requests, receptions recorded in the period and net collections. Do not derive a reception percentage by dividing receptions in the period by newly created orders: they are different cohorts. The third priority uses pending merchant verification, not an unsupported unread-message count. Sales and collections are cumulative over 30 days and use their own truthful scale. No fabricated comparison percentage or cash-custody metric.

The submitted-order screen uses “Total transmis,” because wholesaler acceptance is still pending. Acceptance/refusal, submission recovery, offline tracking, partial reception, consent, QR expiry and payment-pending states are retained as native screens.

## Motion

- First-add prototype: 265:28696 → 265:28921. Cart dock 265:28993 enters with translation Y 6→0 and opacity .72→1 over **180ms, EASE_OUT**. Semantic quantity changes immediately.
- Chart selection: 261:24927 ↔ 265:29146. Tooltip 265:29226 changes opacity .4→1 over **120ms, EASE_OUT**. Selected example is 8 October, 535,200 DA.
- No whole-page entrance. Cancel outdated transitions; keep at most one or two active feedback events in a view. Reduced motion uses immediate state changes with no transform/transition.
- `basket-feedback-native.mp4` is exported from the actual Figma timeline. Its visible change was checked at 0, 0.1 and 0.3 seconds; `native-motion-phases.png` shows the three phases.

## Preview files

Core references: `home-v2.png`, `offers-v2.png`, `orders-v2.png`, `account-v2.png`, `activity-v2.png`, `overview-v2.png`. Additional states: `home-empty-v2.png`, `basket-added-v2.png`, `submission-v2.png`, `recovery-v2.png`, `amendment-v2.png`. Cover: `cover-v2.png`.

