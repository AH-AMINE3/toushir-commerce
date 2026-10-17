# Shop team — fixed-list verdict, round 2

## 1. Brief and authority

This verdict inherits the scope, Figma-world authority and constraints of review-round-1.md. It scores only F1 and F2. It is not a new full-surface review, a redesign review or native runtime acceptance.

## 2. Evidence validity

Opened and checked the three requested actual browser captures: team-invite-error-actions-phone.png, team-accept-error-actions-phone.png and team-viewer-messages-phone.png. Each is 402×874, loaded correctly and shows its claimed state. The form captures retain their action-position scroll state and visibly include the error and submitting control.

Read the changed error locations in ShopTeam.tsx, the restricted empty-state branch in Engagement.tsx and verification/shop-members/browser.json. The recorded browser suite reports 12 passing checks, zero failures and no JavaScript errors; its two new checks cover an owner invitation rejection and acceptance of a link revoked after preview, including retained values and action-position feedback. I did not rerun the suite, open a browser, run a detector or launch a simulator.

## 3. Visual and source verification

Both failure captures place the existing red notice directly above the relevant ink action. The owner rejection shows the retained email and an actionable instruction to use a distinct personal address. The acceptance rejection shows the error alongside Rejoindre le magasin while keeping the entered identity and credentials in the form. Neither notice overlaps the fixed navigation. Source now places the shared error before actions in invitation creation, member role/revocation, invitation revocation and acceptance; the top notice is limited to failures outside an active form or before acceptance preview.

The viewer Messages capture now states read-only access, names the owner/responsible/buyer as authorized senders, and explains that shop exchanges can be consulted here. The copy is fully readable in the first viewport, with filters and existing hierarchy preserved. Source retains the canSend gate on composition and the original copy for members who can send.

## 4. Fixed-list scores

| Finding | Score | Evidence against the original criterion |
| --- | --- | --- |
| F1 — Show mutation failures at the form action region | resolved | Both required failure captures show feedback and the submit/retry control in the viewport. Entered values remain visible. ShopTeam.tsx uses the same local error treatment for the other named team mutation forms. |
| F2 — Explain read-only access in the empty Messages state | resolved | The replacement viewer capture visibly explains the restriction and who can write, without exposing a compose action; the source preserves authorized messaging behavior. |

No listed finding remains partial or unresolved. No new findings were introduced in this verdict pass.

## 5. Disposition

Both round-1 fixes are resolved at the supplied browser-capture and source-review scope. This verdict does not claim complete ecosystem acceptance or iOS/Android execution. Simulators remain excluded by the user's instruction.

disposition: ship
