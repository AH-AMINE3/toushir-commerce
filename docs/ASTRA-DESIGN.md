# Astra Figma-first implementation record

This record describes the authorized revision of six principal Toushir surfaces. The original Figma core remains preserved. The revision lives on [page 261:21629](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF?node-id=261-21629), with [review cover 267:2376](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF?node-id=267-2376). The broader prototype contains 41 structurally audited editable native-layer states, documented in the [final Figma handoff](../verification/astra-design/FIGMA-HANDOFF.md). This record does not equate that prototype inventory with implemented frontend coverage.

## Principal surface map

| Surface | Original frame | Revised frame | Main implementation |
| --- | --- | --- | --- |
| M01 Home | 93:48 | 261:21643 | `apps/mobile/src/Merchant.tsx` |
| M02 Offers | 93:260 | 261:21868 | `apps/mobile/src/Engagement.tsx` |
| M03 Orders | 93:406 | 261:22049 | `apps/mobile/src/Merchant.tsx` |
| M05 Account | 93:688 | 261:22307 | `apps/mobile/src/MerchantAccount.tsx` |
| M12 Activity | 97:3472 | 261:22473 | `apps/mobile/src/MerchantActivity.tsx` |
| G01 Overview | 179:56 | 261:24927 | `apps/web/src/OperationsDashboard.tsx`, `apps/web/src/style.css` |

Mobile reference frames are 402×874. The `final-node-ledger.json`, `final-audit.json` and `final-prototype-ledger.json` files in `../verification/astra-design/` record the final frame map, native structure and prototype wiring. They supersede the earlier incomplete ledgers. The initial source map remains historical pre-implementation evidence.

## Composition implemented in source

Home restores the original spacing rhythm: a white header, 20px horizontal gutters, a 50px search input with 11px top separation, a category rail with 81.5×60px tiles and 12px gaps, and two product columns. Product name, pack, price and quantity action form the product stack. Source packaging identity remains intact.

Offers uses a promotion hero with the actual campaign benefit, threshold, product and publisher. Discount cards use mint, challenges use reward purple and other gift campaigns use information blue. The eligibility preview remains a simulation; it does not replace the wholesaler's confirmed terms.

Account preserves the ink loyalty card and original white Toushir mark. Its reference height is 179px; source derives the height from the content, 24px padding and 18px gaps. Available and reserved points remain separate actual wallet values. Reward shortcuts use purple and activity shortcuts use the neutral surface.

Orders has a 447px minimum delivery hero and a 168px position area. Delivery status, receipt and payment remain distinct. Figma sample GPS and ETA content is demonstration material; it does not establish live tracking. Missing or stale production position data must keep its explicit source-driven state.

Activity starts with a 154px minimum ink paid-total card, paired 94px minimum statistic cards, and a 72px minimum purple due-balance row. Its period controls, selectable chart bars, refund handling, refresh feedback and financial basis remain connected to the existing service data.

G01 restores four independent white KPI cards with a 146px minimum height and 16px gaps. The chart and priorities occupy an 872:424 grid with 330px minimum panel heights. Three lower panels have a 260px minimum height. At 1350px the lower grid becomes two columns; at 1100px KPIs become two columns and the chart/priorities stack; at 700px priorities and lower panels become a single column. KPI labels describe available server measures rather than implying unsupported on-time or cash-custody measures.

## Semantic palette

`DESIGN.md` owns exact token primitives. Astra retains ink, white and action green, uses the shared Astra muted tone, and separates information blue from pending/reward purple. G01 defines corresponding dark information and pending pairs. Native historical `blue` and `amber` aliases resolve to the new semantic pairs; legacy web caution tokens outside G01 remain unchanged. Beige and yellow treatments are excluded from this revision.

The six recorded light-palette contrast checks in `token-contrast.json` pass the normal-text threshold. They cover those foreground/background pairs only and are not a complete accessibility audit or dark-theme certification.

## Motion implementation and intent

`CartDockArrival` in `apps/mobile/src/Motion.tsx` implements one first-basket arrival: 180ms, 6px upward translation, opacity 0.72→1 and ease-out. A per-mount flag prevents replay on quantity changes. Reduced motion renders the resting state and starts enabled while the device preference loads. This is a finite arrival, not a loop.

The Figma chart-exploration concept specifies 120ms. This duration is not asserted as implemented: current G01 source disables the old curve-reveal animation, uses immediate chart selection, 160ms hover transitions and a 220ms distribution update. G01 reduced-motion CSS disables its animations and transitions. Existing 280ms state arrival and 420ms measured progress remain in other native workflows.

## Evidence and acceptance boundary

Source builds passed. Final browser evidence records 18 passing journey/amendment groups, 20 passing final desktop/responsive checks and 9 passing mobile browser smoke checks, with zero reported JavaScript errors. The [independent final design review](../verification/astra-design/final-review.md) gives a scoped ship verdict for the six inspected core surfaces, including the corrected medium-width chart legend. These browser checks do not constitute native-device execution.

The final Figma audit covers 41 editable states and 460 internal navigation actions with zero broken internal destinations and zero references to the original prototype variables. Its 149 variables are isolated; 74 distinct peripheral exits intentionally open original reference screens outside this revision. Structural coverage is complete for the 41 states, while conditional branches were not all manually replayed. Figma visual acceptance covers the light reference layouts; source dark-theme browser captures are separate evidence.

`../verification/astra-design/` contains the final handoff, audits, frame maps, contrast checks, parity captures and browser results. Historical `.impeccable/review/expanded/packet.json` belongs to the earlier implementation. No CI or deployment-readiness claim is made by this design record.

The frontend revision preserves actual data bindings, server price/quote authority, campaign eligibility, payment/receipt/custody distinctions, CRM and pricing workflows. It does not certify every prototype state, native-device execution, provider integration, deployment, domain or security acceptance.
