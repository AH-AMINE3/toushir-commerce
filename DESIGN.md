# Toushir Gros — mobile ordering and wholesale operations

## User-approved direction, 16 October 2026

The user rejected the previous forest/lime interface and charter typography. This replaces the visual direction, rather than polishing it. Their explicit preference for familiar BEES/Sary/Uber/Lime product clarity overrides the earlier concept assignment. Preserve the actual Toushir mark and all real product/workflow behavior.

Two distinct surfaces: a React Native merchant mobile app and a minimal wholesaler operating platform. The browser merchant preview follows the phone composition; it does not become an administration dashboard.

## Core system

- White #FFFFFF is the principal surface. Ground #F6F7F7, ink #171B19, secondary text #606864, separators #E4E8E6.
- One commercial accent: green #087A41, white text on primary buttons, support surface #F0F4F2. No acid lime, yellow, pistachio, gradients or large dark-green blocks.
- Inter for titles, body, fields and figures. Familiar text is intentional per the user's brief. No novelty display face. Tabular figures for operating data. Headings 24–28, section titles 18, body 14–16, compact metadata 12–13. Avoid tiny all-caps eyebrows.
- Spacing 4/8/12/16/24/32. Phone gutters 20, desktop 32. Touch controls 48, minimum effective target 44. Radius 8–12 for controls and panels. Borders or elevation, not both.
- Green marks the selected destination or main next action. Status text and icons retain meaning without color. Red is reserved for real errors/destructive decisions. Pending information stays neutral.

## Merchant app

Slim location/store header on white, full-width search, compact browsing shortcut, clear product groups. Genuine packaging photographs lead product tiles. Two columns on phones; one obvious add/quantity action per product. At most five persistent bottom destinations, with a clear basket. Basket and checkout prioritize quantity, delivery address and the next commercial step. Quote approval, delivery timeline, reorder and claims remain reachable in order detail. No huge promotional hero or desktop sidebar.

Unknown prices remain “Sur devis”; unknown fees and stock remain unconfirmed. Public retail references never become invented wholesale offers.

## Wholesaler platform

White sidebar, quiet active row, black labels, readable central work area. A compact overview supports decisions; the request queue is the main operating surface. Search and status filters lead to one order with progressive tabs for treatment, money and history. Quote preparation, dispatch, receipt, returned goods, claims, explicit credits and actual collection are distinct auditable actions. Catalog, merchant approvals and inventory have distinct destinations.

## Behavior and verification

Native safe areas, scrollable registration with keyboard clearance, one icon set, visible keyboard focus, preserved form drafts, loading/disabled/error/empty states, screen-reader labels, no assumed hover on mobile. Image failure never substitutes another brand. API data is not cached offline. Public static assets may be cached. Validate rendered phone and desktop views, not only source styles.

## Reference evidence

- Official Sary app listing/screenshots: https://play.google.com/store/apps/details?id=com.sary.sary (visually inspected mobile location/search/category hierarchy).
- Official BEES ordering and partner operations: https://www.bees.com/solutions?noredirect=1 and https://play.google.com/store/apps/details?id=com.abinbev.android.tapwiser.beesMexico
- Toushir charter retained logo evidence, page 11; user explicitly overrides its font and broad palette here.
- Applied Impeccable, UI/UX Pro Max, frontend-design, redesign-existing-projects and relevant minimalist/interface guidance. The general product recommendation misclassified this as a marketing page and was discarded; verified React Native touch/navigation recommendations informed implementation. App task clarity takes precedence over marketing effects.
