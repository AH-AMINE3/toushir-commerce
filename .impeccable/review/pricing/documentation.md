# Pricing extension — design-system documentation check

Date: 17 October 2026. Mode: ordinary operational extension of the incumbent Toushir Figma world.

## Decision

Preserve `DESIGN.md` and `.impeccable/design.json` unchanged. The final pricing, quotation and basket states fit the existing system: original Toushir assets, DM Sans, white/cool neutral surfaces, mint priority and selection, green actions, labelled semantic feedback and the recorded web dark palette. No new visual world or replacement token system is established.

The documenter wrote only this review note. The three final corrections are component behavior and alignment fixes; they do not warrant new system primitives or regenerated sidecar snippets.

## Evidence checked

- `PRODUCT.md`, `DESIGN.md` and `.impeccable/design.json`, including palette values, typography roles, shapes, component snippets and named rules.
- `.impeccable/review/pricing/contract.md` and retained `figma-reference.png` (G18 / original frame 179:73).
- `apps/web/src/style.css`: global light/dark variables, headings, buttons, fields, drawer and pricing rules (pricing section at lines 2711–3060).
- `apps/web/src/Pricing.tsx`: priority list, published/draft states, table and narrow rows, proposal form, explicit date/quantity units, simulation, quotation controls and frozen provenance.
- `apps/web/src/main.tsx`: existing labelled order dialog and integration of tariff choice, disabled manual prices and explicit quotation submission.
- `apps/mobile/src/Merchant.tsx`: reviewed basket total, stale-price recovery, separate confirmation and the visible recovery action. Sampled `theme.ts`, `components.tsx` and the DM Sans registration in `App.tsx` to verify the inherited mobile primitives and historical `Inter` alias.
- `.impeccable/review/pricing/verdict.md`: independent Ultra disposition **ship**, limited to the three fixes.
- `verification/pricing/browser.json`: nine recorded scenarios and an empty `javascriptErrors` array. This documentation pass inspected that report; it did not rerun those scenarios.

All eight required current browser captures were inspected:

| Capture | Size | Observed fit |
| --- | --- | --- |
| `pricing-empty-desktop.png` | 1440 × 1917 | Original shell, mint first-priority block, useful empty state and inline simulation. |
| `pricing-proposal-desktop.png` | 1440 × 2281 | Visible form labels, standard fields and green submission; checkbox and caption share one left-aligned row. |
| `pricing-active-desktop.png` | 1440 × 2391 | Flat published-rule table, labelled active state, explicit terms and 390 DA simulation. |
| `pricing-active-dark.png` | 1440 × 2391 | Existing dark surface/ink/semantic pairs; date-control calendar affordance remains visible. |
| `pricing-active-mobile.png` | 390 × 4397 | Stacked priority blocks and simulation, readable rule details and contained controls at the narrow width. |
| `merchant-reviewed-price.png` | 402 × 874 | Existing merchant assets, sourced packshot, mint support action and distinct 390 DA confirmation. |
| `merchant-reprice-confirmation.png` | 402 × 874 | Labelled red feedback, actual “Actualiser le panier” action, visible 450 DA total and separate confirmation. |
| `pricing-order-quote.png` | 740 × 1000 | Existing focused dossier, standard fields, neutral simulation notice and green quotation submission. |

## Final three corrections

1. `style.css:2980` applies `color-scheme: dark` to pricing datetime controls in the existing dark theme. It changes the native browser affordance without introducing a colour token.
2. `style.css:2883` keeps the proposal checkbox and caption in a horizontal, left-aligned row; the checkbox overrides the general full-width field rule with an 18px control. `Pricing.tsx` retains a wrapping label.
3. `Merchant.tsx:993` renders “Actualiser le panier” for basket recovery. The existing stale-price handler refreshes the quoted basket and asks the user to confirm again; the capture shows the changed 450 DA total and a distinct confirmation button.

## Five-line system summary

1. Palette: Toushir forest and ink on white/cool neutrals; mint priority/selection, action green and labelled red/amber states; recorded dark replacements remain intact.
2. Type: DM Sans throughout; web 30px headline, 18px standard panel title, 14px body and 13px action label; mobile shared 27/18/14/13px hierarchy with existing surface variants.
3. Form: flat bordered panels, restrained 16px web panel corners, 9px fields, 10px web buttons and the existing larger mobile action targets.
4. Layout: incumbent sidebar/header and drawer; pricing-specific 1420px/640px adaptations and 19px panel titles remain local surface choices, not new global tokens.
5. Named rules: State Has a Label, One Family and Contained Overflow remain applicable; Category Meaning and Observed Position remain unchanged outside this extension.

## Not canonized or repaired

The inherited “Dossier TOUSHIR” micro-kicker (`main.tsx:1538`, visible in the quotation capture) remains a carried craft defect, not a reusable heading pattern. Existing merchant search fill `#F3F4F4` (`Merchant.tsx`) remains a local neutral outside the recorded canvas primitive. Neither is promoted into `DESIGN.md` or repaired in this three-fix extension. The pricing-specific sizes and breakpoints are recorded here as local implementation facts, without expanding the incumbent token vocabulary.

## Preservation and verification boundary

Both system files were preserved byte-for-byte, verified with SHA-256 before and after this pass:

- `DESIGN.md`: `921446655bd52b9bbf72f45f930dfe00a49560a9bad3e284672b3d00bf6fbf55`
- `.impeccable/design.json`: `45d35f0b0c44bf5c11781d1eafd083461ffe5d9b6c61d04b2275e6dde73346f0`

No browser/server, detector, context rerun, native tooling or simulator was used by this documentation pass. The captures are browser evidence, including React Native web; no native runtime acceptance or full ecosystem/Figma-state certification is claimed.
