# Shop team — ordinary-extension documentation

Recorded on 17 October 2026 after the round-2 fixed-list verdict.

## Outcome and authority

The finished shop-team addition fits the established Toushir visual system at the supplied browser-capture and source-inspection scope. It extends the merchant account with personal shop memberships, invitation acceptance and visible role limits. It introduces no replacement identity or durable design-system change.

The authority remains `PRODUCT.md`, the incumbent `DESIGN.md` and `.impeccable/design.json`, and the supplied Toushir Figma world identified in `contract.md`. The retained M16 store reference is evidence of that world; it is not an exact specification for a team screen. The contract’s Operate-mode hierarchy, Ultra design brief and web-preview-only constraint govern this addition.

This handoff follows the ordinary-extension instruction in `impeccable/reference/new-work.md` and the documentation guidance in `impeccable/reference/document.md`: compare the finished build, preserve the incumbent system files, and record evidence and existing drift. No refresh, overwrite or merge of either system file was performed or needed for this extension.

## Evidence checked

Read `PRODUCT.md`, `DESIGN.md`, `.impeccable/design.json`, this folder’s `contract.md`, `review-round-1.md` and `review-round-2.md`. Inspected the complete team and acceptance implementation in `apps/mobile/src/ShopTeam.tsx`, the team entries and identity context in `MerchantAccount.tsx`, and the role-aware conversation branches in `Engagement.tsx`. Shared `theme.ts`, `components.tsx` and the font registration in `apps/mobile/App.tsx` were checked to resolve the inherited palette, control behavior and historical font-family alias.

Opened all 17 supplied application captures below, plus `figma-store-reference.png`. The captures contain the named content rather than blank or failed-render regions. They are saved evidence; no new browser session or runtime capture was produced for this documentation pass.

| Captured state | Files in this folder | Viewport |
| --- | --- | --- |
| Empty owner team | `team-empty-phone.png` | 402 × 874 |
| Populated owner team | `team-active-narrow.png`, `team-active-wide.png` | 390 × 844; 1440 × 1000 |
| Invitation and action region | `team-invite-phone.png`, `team-invite-actions-phone.png` | 402 × 874 |
| Invitation acceptance and action region | `team-accept-phone.png`, `team-accept-actions-phone.png` | 402 × 874 |
| Buyer and viewer team views | `team-buyer-phone.png`, `team-viewer-phone.png` | 402 × 874 |
| Role change and action region | `team-role-change-phone.png`, `team-role-change-actions-phone.png` | 402 × 874 |
| Changed-session and used-link recovery | `team-access-changed-phone.png`, `team-used-invitation-phone.png` | 402 × 874 |
| Viewer basket and messages | `team-viewer-cart-phone.png`, `team-viewer-messages-phone.png` | 402 × 874 |
| Owner mutation and acceptance failures at the action position | `team-invite-error-actions-phone.png`, `team-accept-error-actions-phone.png` | 402 × 874 |

The long-form pairs document different positions in the app’s internal scroll area. The role-change pair repeats the same complete editing region, as the reviewer already recorded. The wide capture retains the established centered mobile shell, rather than introducing a separate desktop team layout.

## Fit with the incumbent system

**Identity and typography.** The existing Toushir logo, merchant navigation and account assets remain the visual anchors. The new source uses the shared `Inter` family key, which `App.tsx` registers with `DMSans.ttf`; this is the documented DM Sans alias, not a second typeface. The shop title follows the incumbent account title’s 22px/30px bold treatment. Body, labels and supporting descriptions reuse the shared styles. Invitation acceptance reuses the existing entry heading and logo.

**Palette and surfaces.** Team rows sit on white with quiet dividers. Mint marks explanatory notices and the selected role; written role names and radio indicators carry the same meaning without relying on color alone. The existing ink button anchors invitation and acceptance actions. Saving rights uses the existing green action variant; revocation and failed submissions reuse the paired red notice/action treatment. Rounded role choices, summary panels and fields use the incumbent corner vocabulary. There are no new illustrations, ornamental backgrounds or elevation effects.

**Composition and access context.** The first team viewport identifies the actual shop, signed-in person and role, distinguishes shared orders and receipts from personal accounts and baskets, and identifies the primary owner. The empty state explains the next step. Owner and restricted-member views retain the same hierarchy, with the management controls gated by the returned actor permissions. The narrow populated capture preserves the member name, role, long email and Gérer action. Existing account rows provide entry from Mon Toushir and Mon magasin.

**Interaction and feedback.** Role choices explain consequences in French and expose selected/disabled radio semantics. The team’s compact management actions locally raise the shared small-button target to 44px; the common button definition is not redefined. Acceptance names the shop, role and expiration before personal account fields. Pending states and disabled submission conditions are explicit in source. Used-link recovery omits the account-creation form. The invitation result requires an explicit copy/share action and does not claim external delivery.

**Final corrections.** The two failure captures show the existing red notice beside the submitting ink button at the action position, with entered values retained. Source applies the local error placement to invitation creation, member role/revocation, invitation revocation and acceptance. The revised viewer Messages empty state explains read-only access, names who may write and tells the viewer where available shop exchanges appear. Its compose gate remains intact. These corrections use the established components and hierarchy; they do not require new system tokens.

## Existing differences retained

The contract and first review already separate incumbent account/store composition and shared-component differences from this addition. The M16 reference shows a store profile with a delivery-address panel, availability panel and contact/adviser rows; the existing `MerchantAccount.tsx` shop branch instead presents its title, contact details, status and address list. This documentation does not claim exact M16 reproduction or change that composition.

Focused text inputs in the supplied browser captures show a blue browser outline, while the shared field source sets its focus border to green and the incumbent document describes green native focus. This is a previously identified shared browser-presentation difference, not a new team color token. It is recorded without changing shared controls or rewriting the system record.

The current native palette remains light, as the incumbent design record already states. Web dark-theme tokens in the system are not evidence of a native dark appearance. No additional broad drift audit or repair was undertaken.

## Review and execution boundaries

`review-round-1.md` returned `disposition: fix` with F1 and F2 as its entire required list. `review-round-2.md` returned `disposition: ship` and scored both listed findings **resolved**. That second verdict is a fixed-list decision at browser-capture and source-review scope; this document does not broaden it into complete ecosystem acceptance.

Functional API, domain, provider and browser reports retain their own dates and execution scopes. This handoff does not freeze their evolving counts or independently certify their results. No tests, server, browser, detector, context command or simulator were run by the documenter. No application file was edited.

The user explicitly requires web previews with native simulators off on the 8GB Mac. Source includes React Native scrolling, acceptance keyboard accommodation and semantic controls, but the supplied evidence does not establish iOS/Android execution, native keyboard/gesture behavior, large-text layout, screen-reader operation, native dark appearance or device performance.

## Preserved system files

The before-and-after SHA-256 values match the supplied incumbent hashes:

| Protected file | SHA-256 |
| --- | --- |
| `DESIGN.md` | `921446655bd52b9bbf72f45f930dfe00a49560a9bad3e284672b3d00bf6fbf55` |
| `.impeccable/design.json` | `45d35f0b0c44bf5c11781d1eafd083461ffe5d9b6c61d04b2275e6dde73346f0` |

The only file written by this documentation pass is `.impeccable/review/shop-members/documentation.md`.
