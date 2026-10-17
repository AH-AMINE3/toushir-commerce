# CRM and assisted RFQ — independent finish review, round 1

## 1. Brief and authority

Review scope is the ordinary CRM/assisted-request extension: `apps/web/src/Crm.tsx`, its CRM styles and integration, `apps/mobile/src/AssistedRequests.tsx`, the MerchantAccount entry, and relevant session/API behavior. Operate mode and Ultra design judgment mean clear authority, precise tasks and trustworthy commercial state inside the established Toushir identity. This is not whole-ecosystem readiness, recall-module acceptance, or a redesign of the inherited shell.

Authority checked: PRODUCT.md, DESIGN.md, `.impeccable/design.json`, the CRM contract and supplied Figma node 179:83 retained as `figma-reference.png`. No approved replacement comp exists. The reference establishes the task-first left column, qualification right column, DM Sans and white/mint/ink world; its illustrative rows are not exact-content acceptance criteria. Craft-floor and both native platform references were read. The existing identity and shared browser/native differences remain outside this local change.

DESIGN.md SHA256 remains `921446655bd52b9bbf72f45f930dfe00a49560a9bad3e284672b3d00bf6fbf55`; sidecar SHA256 remains `45d35f0b0c44bf5c11781d1eafd083461ffe5d9b6c61d04b2275e6dde73346f0`. Neither was edited.

## 2. Evidence validity

All fifteen application images and the reference were opened at original resolution. The long captures were inspected as full-resolution images, including their lower forms, records and actions. Pixel dimensions were checked. All show the named application state, with loaded text and intact surfaces; no blank, black, half-loaded or mismatched image requires recapture.

| Capture | Dimensions | Validity and inspected region |
| --- | --- | --- |
| `crm-empty-desktop.png` | 1600×1040 | Top of document; empty task and qualification state. |
| `crm-active-desktop.png` | 1600×1971 | Top through footer; task, shop, contact, history and orders. |
| `crm-active-dark.png` | 1600×1971 | Same complete active composition in inherited dark theme. |
| `crm-active-narrow.png` | 390×3545 | Full document from top; stacked tasks, actions and records. |
| `crm-portfolio-form-desktop.png` | 1600×2340 | Top and complete assignment form, focus state and action row. |
| `crm-contact-form-desktop.png` | 1600×2613 | Top and complete entered report with next action and submit controls. |
| `crm-future-visit-error-actions.png` | 1600×1040 | Deliberate scrolled viewport; retained input, actual rejection and action row. |
| `crm-assisted-rfq-review-desktop.png` | 1600×2335 | Top and complete named authorization/RFQ review and lower history. |
| `crm-reassigned-adviser-desktop.png` | 1600×1040 | Valid empty portfolio after reassignment and page reload. |
| `crm-merchant-assistance-phone.png` | 402×874 | Initial merchant assistance screen. |
| `crm-merchant-authorization-phone.png` | 402×874 | Top of consent form and exact product quantity. |
| `crm-merchant-authorization-actions.png` | 402×874 | Deliberate internal-scroll region; address, note, expiry, checked consent and actions. |
| `crm-merchant-authorized-phone.png` | 402×874 | Authorization created; history begins below the main action. |
| `crm-merchant-authorized-wide.png` | 1440×1000 | Inherited centered 520px preview; authorization details and revocation action visible. |
| `crm-viewer-assistance-phone.png` | 402×874 | Viewer explanation and read-only authorization history. |

The fixed-height React Native web ScrollView explains why the phone form is supplied in paired scroll positions. These are valid browser-preview captures, not iOS/Android runtime evidence. Full web captures start at the document top with settled sticky chrome; the two action captures are explicitly scrolled regions. `figma-reference.png` is a valid 1600×1040 reference only.

The saved reports were read: `verification/crm/api.json` records 19 passed scenarios over 102 HTTP requests; `browser.json` records 11 passed checks and no JavaScript errors; `domain-91.trx` records 91 executed/passed tests. They support the exercised behavior, not visual approval. The browser reassignment check explicitly calls `salesPage.reload()` at `tests/browser/crm.mjs:364`; its empty-state image therefore does not demonstrate revocation of an already-open dossier during an ordinary refresh. No browser, server, test suite, detector or context command was run by this reviewer. The handoff reports the earlier single detector run as `[]` and no outstanding hooks.

## 3. Visual and native-source review

The desktop first viewport gives the selected shop and upcoming work clear priority, with qualification beside it. White panels, restrained borders, original shell assets and the compact DM Sans hierarchy belong to the supplied world. The small demo dataset leaves quiet space without suggesting fabricated work. Light and dark captures keep labels and statuses readable. At 390px the CRM stacks coherently; long task titles wrap and the local action rows remain intact. The additional records use separators and real content, with no decorative asset substitution.

Assignment and contact forms preserve shop context, visible labels, entered content and an evident primary action. The focused field treatment is visible. The future-visit rejection is adjacent to submission and preserves the report. The assisted RFQ review names both issuer and adviser, shows the exact products/address and expiry, and states that later quote acceptance remains with the merchant. Unknown prices stay “Sur devis” or explicitly await the quote.

The merchant preview keeps consent separate from browsing the personal basket. The paired authorization captures show the immutable product snapshot, selected address, optional note, bounded expiry, explicit checkbox and clear continue/cancel choices. Authorization history names the parties and status; the Viewer screen explains who can authorize and offers history access without the grant action. The wide preview correctly retains the inherited phone shell rather than claiming an expanded native tablet design.

Source review confirms semantic web sections/labels, pressed filter state, busy fieldsets, retained failed form input, and a stable request key for RFQ submission. Merchant source declares radio/checkbox accessibility states and clears consent after address/note changes; address rows have a 56px minimum and consent a 48px minimum. Local compact history actions explicitly retain the contract's 44px minimum. That source observation is not a claim of Android's 48dp guarantee or comprehensive OS conformance. The existing shared type, icon, light-theme and navigation conventions were not reopened. Native safe areas, keyboard accommodation, gesture/back behavior, screen-reader output, Dynamic Type/font scaling, dark appearance and device performance remain unexecuted under the simulator-off constraint.

Backend contracts and CrmService preserve the distinction between a declared contact and a commercial request. The server checks current portfolio authority and consumes a named, bounded grant to create one RFQ. The material issue below is the UI's response to lost portfolio authority, not evidence that the server accepts an unauthorized mutation.

## 4. Bounded material findings

### R1 — Clear an open dossier when refreshed portfolio authority disappears (P2)

**Exact evidence:** `apps/web/src/Crm.tsx:63–93` updates the overview independently from the selected dossier. The overview success path calls `setData` but never reconciles `shopId`, `dossier` or `form` against the newly authorized shop list. The detail failure path at line 89 only sets an error. `load()` at lines 63–67 has the same retention behavior. `apps/web/src/main.tsx:359–367` and `:475–483` refresh the SALES overview and increment the revision, including every 15 seconds. `backend/Services/CrmService.cs:29–34` returns 404 `NOT_FOUND` when the adviser no longer owns that portfolio. The qualification, form and history render from retained `dossier` at Crm.tsx lines 335, 407 and 722. `crm-reassigned-adviser-desktop.png` is valid evidence of the post-reload state, while `tests/browser/crm.mjs:364` confirms the uncovered open-page transition.

**Trigger and impact:** leave a selected shop dossier or RFQ/contact form open in the adviser's browser, reassign that shop as administrator elsewhere, then allow the normal refresh or choose Actualiser. The overview loses the shop and the detail call is rejected, but the existing dossier/form stays in state. The screen can continue displaying the old shop's contacts, orders, authorization summary and actionable controls beside an error and empty task list. It misrepresents current responsibility and access, and sends the user back into actions that the server will refuse. A full page reload is currently required to reach the clean empty portfolio shown in the capture.

**Required resolution:** reconcile selected state against the successful, unfiltered authorized-shop overview; clear the selection, dossier and associated form when its shop is no longer permitted. Clear inaccessible selected detail on relevant authorization/not-found responses and guard in-flight responses so an earlier authorized detail response cannot restore a cleared dossier. Keep ordinary recoverable network failure behavior distinct. Explain the changed assignment in a brief recoverable notice, then expose the remaining permitted shops or the existing empty state.

**Resolution evidence:** a focused browser check must keep the former adviser's page open, with a selected dossier and an open form, reassign the shop in another session, then verify the ordinary refresh/poll clears all former detail and actions without `reload()`. Confirm the remaining selector/empty state is usable and a delayed old detail response cannot repopulate it. Recapture `crm-reassigned-adviser-desktop.png` at the same viewport from document top for that live transition, and return the same required capture matrix after the fix batch for the bounded verdict pass. Existing API rejection remains necessary supporting evidence, not a substitute for the UI check.

This is the complete material finding list for this round. No replacement visual world or general shell redesign is required.

## 5. Disposition

`fix`

One local state-reconciliation correction is required before this CRM/assisted-RFQ extension can pass its finish review. The browser captures are valid and the established visual direction is sound. This disposition does not certify native execution, exact acceptance of all 265 Figma states, the unsupplied 79-page specification, hosted release readiness, or the remainder of the commerce ecosystem.
