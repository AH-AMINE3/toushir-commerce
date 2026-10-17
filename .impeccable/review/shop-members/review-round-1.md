# Shop team — independent finish review, round 1

## 1. Brief and authority

This is an Operate-mode, code-led extension of the existing Toushir merchant account. The authority is the supplied Figma world, PRODUCT.md, DESIGN.md, .impeccable/design.json and this extension's contract.md. The original M16 store image is a visual-world reference, not an approved team-screen comp. The wholesale staff matrix is outside this review. No replacement identity or additional design system is warranted.

Scope: ShopTeam.tsx and its invitation acceptance screen; the account entry and personal identity in MerchantAccount.tsx; session recovery in App.tsx; the local role-aware affordances in Merchant.tsx, MerchantDelivery.tsx, Claims.tsx and Engagement.tsx. This review does not reopen the incumbent store/account composition or shared-component drift. It does not certify the complete commerce ecosystem.

## 2. Evidence validity

Opened every supplied PNG. The thirteen phone captures are 402×874; team-active-narrow.png is 390×844; team-active-wide.png is 1440×1000. Each actual capture is loaded and shows the named flow or state, without blank/failed-render regions. The paired action captures show the internal scroll position of a fixed app viewport. The two role-change images show the same complete editing region; they are redundant but usable. The wide image correctly preserves the incumbent centered 520px mobile browser shell. figma-store-reference.png is separately treated as reference evidence.

Reviewed actual captures: team-empty-phone.png, team-invite-phone.png, team-invite-actions-phone.png, team-accept-phone.png, team-accept-actions-phone.png, team-buyer-phone.png, team-role-change-phone.png, team-role-change-actions-phone.png, team-access-changed-phone.png, team-viewer-phone.png, team-viewer-cart-phone.png, team-viewer-messages-phone.png, team-used-invitation-phone.png, team-active-narrow.png and team-active-wide.png.

Read the relevant source and the recorded functional reports. They record 19 API scenarios/92 requests, 8 live/OIDC scenarios/28 requests, 10 browser checks and 89 passing domain checks. provider-upgrade.json states its SQLite execution and PostgreSQL generation-only boundary. These reports support behavior at their recorded scope; they are not a visual verdict. I ran no tests, server, browser, detector, context command or simulator. No application/system file was changed.

Native runtime is explicitly excluded by the user's web-preview-only constraint. Browser captures and source do not establish iOS/Android keyboard behavior, system gestures, large-text layout, dark appearance, screen-reader operation or device performance. The native references informed source inspection without turning those unexecuted checks into acceptance claims.

## 3. Visual and native-source review

The new team surface belongs to the established world: DM Sans hierarchy, white ground, mint selection and notices, flat member rows, restrained dividers and a clear ink invitation action. The first viewport names the shop and signed-in person/role, explains shared orders versus personal accounts, and identifies the owner. The empty state gives a useful next step. The populated narrow view keeps the long example email and Gérer action readable, and the wide view avoids stretching a phone workflow into a new desktop design.

Role choices explain their consequences in ordinary French and include a selected radio indicator. Acceptance names the shop, role and expiration before account creation. The role-change region distinguishes save, revoke and keep-access actions and explains the consequence of revocation. Used invitations show recovery without another account form. Personal-session recovery is explicit. The viewer basket retains a personal basket with checkout disabled, consistent with the recorded product boundary.

Source uses the existing safe-area root, ScrollView, iOS KeyboardAvoidingView on acceptance, semantic role controls and labelled shared buttons. The new form action targets use the existing native button sizes; these observations are source evidence, not native interaction verification. Existing browser focus styling and platform-wide navigation/type differences remain shared-system matters outside this extension's fix list.

## 4. Material findings

### F1 — Show mutation failures at the form action region

**Priority:** P2. **Evidence:** source-backed finding, with the supplied scrolled captures showing the relevant distance; the failure was not executed in this review.

**Capture/region:** team-invite-actions-phone.png at Créer le lien privé; team-role-change-actions-phone.png at Enregistrer les droits / Révoquer cet accès; team-accept-actions-phone.png at Rejoindre le magasin. The invitation-revocation form has the same source arrangement.

**Source:** apps/mobile/src/ShopTeam.tsx, run catch at lines 133–145 and sole team error notice at lines 176–183; acceptance error at line 621 and submission catch at lines 702–704. Team mutation forms set the same top-of-screen error, and acceptance renders its submission error above the shop summary. Neither path moves the viewport/focus to that error or provides feedback beside the submitting control.

**Impact:** while submitting at the bottom of these long forms, a server rejection, expired invitation, version conflict or connection failure can leave the visible form looking unchanged once loading stops. The person must discover the failure by scrolling back up. An alert semantic alone does not make the result visible to a sighted user.

**Required resolution:** keep the failure and its recovery visible in the active form/action region, or deliberately reveal/focus the error after a failed submit. Preserve entered values and prevent overlap with fixed navigation. Apply this consistently to invitation creation, member role/revocation, invitation revocation and invitation acceptance; no global error-system redesign is needed.

**Visible resolution evidence:** at 402×874, submit from the action position and trigger a failed owner-side mutation and a failed acceptance. Each resulting capture must show the failure message and an appropriate recovery action in the viewport without manual upward scrolling. Source must show the same treatment covers the other named team mutations. Add these failure captures beside the existing paired form captures.

### F2 — Explain read-only access in the empty Messages state

**Priority:** P2. **Evidence:** directly visible in team-viewer-messages-phone.png.

**Capture/region:** the central empty state below the message filters. It says the adviser answers replenishment questions but gives no indication that this person cannot start or send a conversation. The compose action is absent, so the screen can read as incomplete.

**Source:** apps/mobile/src/Engagement.tsx, lines 340–361. The empty message is based on MERCHANT/DRIVER only, while canSend hides the merchant compose panel. The explicit read-only notice at lines 231–237 exists only inside an opened conversation.

**Impact:** this violates the extension's requirement that conversation limits remain visible. A viewer with no threads cannot tell why there is no way to contact the adviser.

**Required resolution:** give the viewer empty state a concise visible read-only explanation, identify that an authorized shop member starts/sends messages, and say that available shop conversations can be consulted here. Preserve the existing hierarchy, filters and absence of a send control.

**Visible resolution evidence:** replace team-viewer-messages-phone.png at 402×874 with a capture showing the restriction and useful next step in the first viewport, with readable copy and no new compose affordance. Source should retain the existing owner/buyer messaging behavior.

These two findings are the entire required fix list. A subsequent verdict pass must score only F1 and F2 as resolved, partial or unresolved; it must not introduce a new aesthetic hunt or imply a full native pass.

## 5. Disposition

The established visual direction and captured layouts are sound for this ordinary extension. Resolve F1 and F2 in one batch, then recapture the named evidence and return for a verdict on that list. There is no wholesale fidelity failure and no need to recapture the existing set for evidence validity.

disposition: fix
