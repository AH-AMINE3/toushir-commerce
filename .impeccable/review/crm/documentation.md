# CRM and assisted quote requests — implementation record

Recorded on 2026-10-17 by the fresh Impeccable documenter role. This record closes the completed ordinary CRM extension in Operate mode. It compares the finished source with the incumbent system and records the supplied finish-review evidence. It does not establish a new visual direction or specify the later merchant/frontend/workflow redesign and videos.

## Scope and disposition

The implemented surface is the wholesaler's **Portefeuilles & visites** workspace, the merchant's **Mon Toushir → Demandes assistées** flow, and their local navigation/session integration. The relevant source is `apps/web/src/Crm.tsx`, the CRM block in `apps/web/src/style.css`, CRM integration in `apps/web/src/main.tsx`, `apps/mobile/src/AssistedRequests.tsx` and its entry in `apps/mobile/src/MerchantAccount.tsx`.

`review-round-1.md` identified one material issue, R1: an open adviser dossier and form persisted after portfolio authority disappeared. `review-round-2.md` scores that issue **Resolved** and records **ship** at the bounded R1-resolution scope. The second pass is not a new full-surface audit. There are no unresolved items in that bounded finding list.

The documenter read PRODUCT.md, DESIGN.md, `.impeccable/design.json`, `contract.md`, both review rounds, the source above and Impeccable's `reference/document.md`. The existing design system remains authoritative. No context loader, detector, browser, build or test was run during this documentation pass. This file is the only documenter write.

## Continuity with the incumbent system

| Incumbent pattern | Finished CRM expression | Classification |
| --- | --- | --- |
| Supplied Toushir Figma world, original assets and DM Sans | Existing desktop shell and merchant account shell carry the new entries. The CRM uses the existing merchant navigation asset; the account entry reuses its existing row and settings asset. Mobile's historical `Inter` family key continues to represent DM Sans as documented by the incumbent system. | Inherited identity; no new asset or font family. |
| Quiet white/neutral surfaces, mint selection, green actions and written semantic states | Shared web panels, fields, badges, primary/secondary buttons and filters organize the CRM. Shared native `Button`, `Field`, `Icon` and `Notice` components organize consent and history. Priority is labelled Haute/Normale; authorization and task states remain written. | Existing primitives composed for this task. |
| Flat operational content with restrained borders and gentle corners | CRM task and history rows use one-pixel separators. Qualification and form panels retain the existing container language. The mobile flow uses separated text sections and address rows, without a new illustration or ornamental panel style. | Local composition; no new elevation system. |
| Compact hierarchy and explicit commercial state | The web page title leads into upcoming actions and qualification. Named person, shop, due time, status and next action remain visible. Merchant review shows exact products, quantities, address, optional note, expiry and consent. | Local information hierarchy within the existing type family. |
| Existing desktop dark tokens and responsive shell | CRM colours resolve through the inherited web variables; datetime inputs declare a dark colour scheme. The merchant preview retains the existing centred phone shell, including at wide browser sizes. | Inherited theme and shell; no mobile dark-theme or tablet redesign claim. |

The retained Figma reference is G28, node `179:83` in file `4IffhmSg2gHg82narIGfMF`, stored as `figma-reference.png`. The contract and first review identify its task-first left column and **Qualifier, puis agir** right column as the applicable composition/workflow reference. Its example records are not commercial data or exact-content targets. The documenter relies on those recorded visual inspections and does not claim a fresh pixel comparison.

## Actual local layout and component patterns

The web workspace uses a 24px vertical rhythm. A labelled portfolio selector, limited to 440px when space permits, sits beside the existing follow-up filters. The first content row uses a flexible 1.6:1 split with a 300px minimum qualification column. Upcoming actions lead; the qualification panel provides the selected shop, contact details, assigned adviser and available actions. The lower dossier is a two-column collection of assisted requests, contact reports, follow-up history and recent orders.

Task rows use a 76px priority/type column, flexible description and separate result action, with 16px gaps and 20px/24px padding. Rows show the responsible person and the displayed Africa/Algiers deadline. Record bodies preserve entered line breaks and allow long content to wrap. Product-summary quantities remain distinct from product names. Qualification/form titles use the existing 18px web tier; local record headings use 14px, descriptive copy 13px and metadata 12px.

The local layout collapses the first content row at 1200px. At 700px, the toolbar and lower dossier stack, task rows use a 64px first column and 12px gap, task actions occupy their own row, forms become one column, and product-summary lines stack. Form/record horizontal padding reduces to 18px. CRM buttons receive a 44px minimum height at that narrow breakpoint. These two breakpoints and their row arrangements are local CRM rules, not additions to the system sidecar's global breakpoint vocabulary.

Forms open inline below the first row rather than in a new overlay. The form heading receives programmatic focus, the selected shop stays in the introductory text, labels remain visible, and the shared fields/buttons carry their established focus treatment. Form spacing is 20px; the panel uses 28px padding before its narrow-screen reduction. The result form explicitly distinguishes a declared observation, retained report, required next action and optional reminder. Scheduling inputs state that their entry time follows the browser's local time; rendered record timestamps use Africa/Algiers.

The merchant assistance entry is an ordinary account row, using the existing 78px minimum row height. Its detail surface has a 22px/30px title, 18px/26px local section titles and shared body/label styles. The content uses 24px section spacing, 20px form spacing and one-pixel record separators. Address choices are 12px rounded rows with 16px padding and a 56px minimum height; the consent row has a 48px minimum. Compact history actions explicitly override the shared small-button minimum to 44px. These are observed local source measurements, not a claim of platform-wide touch-target conformance.

## Implemented interaction and authority patterns

**Portfolio and tasks.** The administrator can assign a named adviser with a reason, then plan contacts or visits. The selected shop conditions available actions. Task forms support creation, rescheduling, completion and cancellation; completed/cancelled tasks stay in history. Contact and visit copy states that the connected person declares the report, without claiming verified calls or physical attendance. An empty portfolio explains that an administrator must assign a shop; a selected shop without a portfolio offers the appropriate assignment action.

**Role-aware entry and recovery.** `main.tsx` exposes the CRM under Relation client, gives SALES the CRM route, directs SALES navigation/search back to it and labels the role Conseil commercial. The shell refreshes through its existing live connection and 15-second timer. Professional-session recovery retains the professional sign-in path rather than silently replacing the person with an administrator demo session. These are implementation observations, not a new global role specification in PRODUCT.md.

**Grant review before use.** The merchant fetches the current adviser and personal basket, then prepares a snapshot after rechecking them. The expiry is the earlier of 24 hours from preparation or the access deadline; an access window with five minutes or less remaining is rejected. The visible authorization names the adviser and shop and lists the exact products, quantities, address, optional note and expiry. Address and note changes clear the acknowledgement. The radio and checkbox controls declare their checked/disabled accessibility states. The submit action requires explicit consent and an address; a separate action keeps the basket without authorizing.

The server-provided `canAuthorize` flag gates the grant action. The non-authorizing merchant view explains that an owner or manager may authorize and keeps history readable. A missing address routes to the existing shop details. The flow states that the basket remains intact, the definitive price/fees will arrive in the quote, and no promotion or free quantity is promised. The documented contract limits this authority to one REQUEST_QUOTE; the adviser cannot use it to accept the quote, receive products or pay for the merchant.

On the web, an available grant is actionable only for its named adviser. **Vérifier avant de transmettre** opens an immutable summary before the explicit send action. The RFQ submission retains a request key for the grant, while the visible copy distinguishes transmission from quote acceptance and reservation. Authorization history uses the same written status vocabulary on both surfaces: À transmettre, Demande transmise, Révoquée, Expirée, Accès modifié and Adresse modifiée. An eligible unused grant can be revoked through a reason form and explicit confirmation. The exact server authority/invalidation guarantees are recorded by the contract and supporting API review, rather than established by this documenter's UI-source inspection.

**Pending, failure and saved states.** Web actions use a pending guard plus disabled/busy fieldsets. Failed submissions leave the form and entered content in place and show an adjacent alert. If a save succeeds but reload fails, the message states that the record was saved and requests a refresh. Merchant actions similarly guard repeated submissions, show loading/disabled states and retain errors within the relevant draft or revocation area. Success text reports the completed operation and preserves the boundary between authorization, request and accepted order.

**R1 resolution.** `Crm.tsx` reconciles the selected shop against the successful unfiltered overview, independent of the user's search. Lost selection or a relevant 401/403/404 clears the selected shop, dossier, form and associated errors, invalidates earlier loads and supplies a changed-access notice. Request generations and a current-selection reference prevent obsolete responses from restoring a cleared dossier. Ordinary recoverable errors follow the separate retry path. Both manual refresh and normal revision-driven loading use this reconciled loader; the executed scenario cited by round 2 specifically covers manual refresh and delayed-response rejection.

## Evidence and limits

The finish reviews are the source of the following executed/visual evidence. No results were rerun or expanded by this documenter.

| Evidence | Recorded result and practical boundary |
| --- | --- |
| `review-round-1.md` | All fifteen application captures and the retained reference were inspected at original resolution. It accepted the supplied visual evidence and established the single R1 finding. |
| `review-round-2.md` | All fifteen replacement captures were inspected as valid continuity evidence; R1 was scored resolved and the bounded disposition was ship. The new reassignment capture shows the live changed-access notice, empty selection and cleared detail/form. |
| `verification/crm/browser.json`, as inspected by round 2 | Report dated `2026-10-17T12:52:31.554Z`: 11 passed checks, zero failed checks and no JavaScript errors. The reassignment scenario leaves a filled form open, holds a real earlier authorized response, reassigns in another session, refreshes without reload and verifies that releasing the old response cannot restore detail. |
| API/domain reports, as inspected by round 1 | 19 passed API scenarios over 102 HTTP requests and 91 executed/passed domain tests. These support the exercised behavior and do not constitute a visual or native-runtime verdict. |

The capture matrix includes empty, active light/dark/narrow, assignment form, contact form, retained future-visit error, assisted-RFQ review, live reassignment, merchant assistance, paired consent-form/action regions, authorized phone/wide and Viewer states. Desktop viewport evidence is 1600×1040, with full-document captures where required; the narrow web capture is 390×3545 from a 390×844 viewport. Phone previews are 402×874; the wide merchant preview is 1440×1000. Full-document images begin at the top with settled chrome; the future-visit and merchant consent-action images are explicitly scrolled action regions.

Phone images are React Native browser previews. Native runtime, safe-area behavior, keyboard accommodation, gestures/back behavior, screen-reader output, Dynamic Type/font scaling, dark appearance and device performance remain unexecuted under the simulator-off constraint. The source declares relevant semantics and local dimensions; that does not certify their OS behavior. Exact acceptance of all 265 Figma states, the unsupplied 79-page original specification, hosted release readiness, recall-module completion and whole-ecosystem readiness remain outside this record.

## Preservation decision

This extension introduces task-specific compositions and authority-aware states using the existing tokens and components. No durable visual token, shared primitive, new identity, motion vocabulary or global layout rule requires promotion into DESIGN.md or its sidecar. Their bytes were preserved; the following SHA-256 values matched before the documentation write and were rechecked afterward:

- DESIGN.md: `921446655bd52b9bbf72f45f930dfe00a49560a9bad3e284672b3d00bf6fbf55`
- `.impeccable/design.json`: `45d35f0b0c44bf5c11781d1eafd083461ffe5d9b6c61d04b2275e6dde73346f0`

The subsequent broader redesign is a separate task. This record preserves what the completed CRM extension actually implemented and what its bounded review established; it does not select a future visual world, alter the incumbent system files or preempt that later work.
