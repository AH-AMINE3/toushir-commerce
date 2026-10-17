# Customer pricing extension review

Original request: implement the complete Toushir commerce ecosystem from the supplied Figma world, with exceptional design/architecture and actual testing. The user expressly requests Ultra design judgment and browser previews; native simulators must stay off on the 8 GB Mac.

Mode: Operate. This is an ordinary extension of the incumbent Figma system. G18 / original frame179:73 is preserved in figma-reference.png. Preserve DM Sans, the Toushir mark/navigation assets, white/cool neutral surfaces, mint priority/status, green actions, existing sidebar/header and dark tokens. New approval, simulation and history states extend that operational system; the original static price example is not a mandate to invent real prices or render a promotional landing page. No new world/comp-generation gate applies.

THESIS: one understandable customer-price hierarchy with explicit approval and reviewed order commitment.
OWN-WORLD: the Figma white/mint operational interface, real referenced product photographs and existing dark tokens.
STORY: propose immutable shop/segment/standard terms, independently approve, simulate a real customer's basket, and retain the prices actually proposed or ordered.
FIRST VIEWPORT: the Figma priority hierarchy followed by actionable rules and simulation in the existing wholesaler shell.
FORM: inline forms; flat table on desktop and flat readable rows at narrow widths; explicit quantity/date units and states; original pricing details and quote controls remain usable. Current API checks, browser and domain evidence live in verification/pricing/; pixels alone cannot certify authorization or payment correctness.

Required screenshots, all real browser captures checked by the build thread:
- pricing-empty-desktop.png1440 wide, full document from top; useful initial state.
- pricing-proposal-desktop.png1440 wide, full document; actual proposal form.
- pricing-active-desktop.png1440 wide, full document; published contract and actual customer simulation.
- pricing-active-dark.png1440 wide, full document; the same state after finite transitions are settled.
- pricing-active-mobile.png390 wide, full document; explicit row details, no horizontal page overflow.
- merchant-reviewed-price.png402×874; React Native web basket at its reviewed390DA total.
- merchant-reprice-confirmation.png402×874; real rejected stale price and updated450DA basket, requiring another explicit confirmation.
- pricing-order-quote.png740×1000; focused scrollable order drawer, actual tariff selection and simulation in a quotation. It is a focused drawer capture rather than a full document or native execution claim.

Source: apps/web/src/Pricing.tsx, main.tsx, style.css; apps/mobile/src/Merchant.tsx, Engagement.tsx, api.ts. Scope includes proposal/review/segment/simulation, order tariff controls and the new stale-price basket state. PRODUCT.md, DESIGN.md and .impeccable/design.json define the incumbent system. docs/CUSTOMER-PRICING.md records the domain contract. Browser report covers nine actual scenarios with no JavaScript errors. Native runtime acceptance is deliberately not claimed for these changes.

The web detector ran once earlier in this continuous session and returned[] in .impeccable/review/detector.json. Do not run a second detector. No new marketing concept or token replacement is authorized. Independent review should name material findings and exact evidence, not certify the entire ecosystem or all original Figma screens.
