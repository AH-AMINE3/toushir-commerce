---
name: TOUSHIR Commerce
description: The Toushir Figma system for active, connected wholesale commerce.
colors:
  ink: "#151917"
  brand: "#0f2c2c"
  green: "#087f4f"
  action: "#06dd81"
  action-ink: "#10281c"
  action-hover: "#00cb76"
  mint: "#d9eddf"
  aqua: "#ccffff"
  aqua-ink: "#145b5b"
  surface: "#ffffff"
  canvas-web: "#f5f7f6"
  canvas-mobile: "#f4f6f4"
  sidebar: "#fafbfa"
  line: "#e7ebe7"
  border-web: "#e4e9e6"
  muted-web: "#66736b"
  muted-mobile: "#5f6d65"
  muted-astra: "#5f6d65"
  muted-mobile-detail: "#6b736f"
  account-on-dark: "#bacabf"
  danger-web: "#a92d2d"
  danger-mobile: "#b42332"
  danger-surface-web: "#fff0ef"
  danger-surface-mobile: "#fff0f1"
  warning-web: "#145b5b"
  warning-mobile: "#6c5a92"
  warning-surface-web: "#d9f5f2"
  warning-surface-mobile: "#f0ecf7"
  info: "#456b8c"
  info-surface: "#eaf1f7"
  pending: "#6c5a92"
  pending-surface: "#f0ecf7"
  reward: "#6c5a92"
  reward-surface: "#f0ecf7"
  chart-muted: "#abc8ba"
  dark-canvas: "#0e1713"
  dark-surface: "#17231c"
  dark-sidebar: "#111d17"
  dark-soft: "#1d2d23"
  dark-line: "#2c3d32"
  dark-border: "#304338"
  dark-muted: "#a5b8ab"
  dark-ink: "#edf3ee"
  dark-brand: "#e4f2e8"
  dark-green: "#75e7a8"
  dark-mint: "#224c34"
  dark-aqua: "#17464b"
  dark-aqua-ink: "#adf5ef"
  dark-danger: "#ffb1b1"
  dark-danger-surface: "#452627"
  dark-warning: "#adf5ef"
  dark-warning-surface: "#17464b"
  dark-info: "#a7c9e5"
  dark-info-surface: "#203849"
  dark-pending: "#d0bfea"
  dark-pending-surface: "#382e49"
  dark-chart-muted: "#729486"
typography:
  headline-web:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "30px"
    fontWeight: 700
    lineHeight: 1.3
    letterSpacing: "-0.03em"
  operating-title:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "23px"
    fontWeight: 700
    letterSpacing: "-0.03em"
  operating-metric:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "clamp(24px, 2.2vw, 35px)"
    fontWeight: 700
    lineHeight: 1.3
    letterSpacing: "-0.035em"
  overview-metric:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "clamp(25px, 2vw, 32px)"
    fontWeight: 700
    lineHeight: "40px"
    letterSpacing: "-0.03em"
  title-web:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "18px"
    fontWeight: 700
    lineHeight: 1.4
    letterSpacing: "-0.015em"
  body-web:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "14px"
    fontWeight: 400
  label-web:
    fontFamily: '"DM Sans", sans-serif'
    fontSize: "13px"
    fontWeight: 700
  heading-mobile:
    fontFamily: "DM Sans"
    fontSize: "27px"
    fontWeight: 600
    lineHeight: "34px"
    letterSpacing: "-0.65px"
  title-mobile:
    fontFamily: "DM Sans"
    fontSize: "18px"
    fontWeight: 600
    lineHeight: "25px"
    letterSpacing: "-0.3px"
  body-mobile:
    fontFamily: "DM Sans"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: "21px"
  label-mobile:
    fontFamily: "DM Sans"
    fontSize: "13px"
    fontWeight: 600
    lineHeight: "19px"
  merchant-title:
    fontFamily: "DM Sans"
    fontSize: "23px"
    fontWeight: 700
    lineHeight: "30px"
    letterSpacing: "-0.45px"
  courier-title:
    fontFamily: "DM Sans"
    fontSize: "28px"
    fontWeight: 700
    lineHeight: "40px"
    letterSpacing: "-0.5px"
  merchant-tab-title:
    fontFamily: "DM Sans"
    fontSize: "28px"
    fontWeight: 700
    lineHeight: "32px"
    letterSpacing: "-0.45px"
  account-title:
    fontFamily: "DM Sans"
    fontSize: "22px"
    fontWeight: 700
    lineHeight: "30px"
  account-points:
    fontFamily: "DM Sans"
    fontSize: "40px"
    fontWeight: 700
    lineHeight: "46px"
    letterSpacing: "-0.6px"
  account-stat:
    fontFamily: "DM Sans"
    fontSize: "24px"
    fontWeight: 700
    lineHeight: "29px"
    letterSpacing: "-0.6px"
  button-mobile:
    fontFamily: "DM Sans"
    fontSize: "13px"
    fontWeight: 600
    lineHeight: "20px"
  filter-label:
    fontFamily: "DM Sans"
    fontSize: "13px"
    fontWeight: 600
    lineHeight: "18px"
rounded:
  badge: "7px"
  compact: "8px"
  field: "9px"
  button-web: "10px"
  panel-mobile: "12px"
  panel-narrow: "13px"
  panel-web: "16px"
  account-card: "20px"
  product-add: "22px"
  filter-pill: "24px"
spacing:
  xs: "8px"
  sm: "12px"
  md: "16px"
  field-group: "18px"
  lg: "20px"
  panel-web: "23px"
  xl: "24px"
  drawer: "28px"
  page: "32px"
components:
  button-primary:
    backgroundColor: "{colors.action}"
    textColor: "{colors.action-ink}"
    typography: "{typography.label-web}"
    rounded: "{rounded.button-web}"
    padding: "10px 17px"
  button-primary-hover:
    backgroundColor: "{colors.action-hover}"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.label-web}"
    rounded: "{rounded.button-web}"
    padding: "10px 17px"
  button-primary-mobile:
    backgroundColor: "{colors.action}"
    textColor: "{colors.ink}"
    typography: "{typography.button-mobile}"
    rounded: "{rounded.panel-mobile}"
    padding: "12px 18px"
  button-ink-mobile:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.surface}"
    typography: "{typography.button-mobile}"
    rounded: "{rounded.panel-mobile}"
    padding: "12px 18px"
  filter-pill:
    backgroundColor: "{colors.canvas-mobile}"
    textColor: "{colors.muted-mobile}"
    typography: "{typography.filter-label}"
    rounded: "{rounded.filter-pill}"
    padding: "0 14px"
  filter-pill-selected:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.surface}"
  product-add-mobile:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.surface}"
    rounded: "{rounded.product-add}"
    width: "44px"
    height: "44px"
  merchant-account-card:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.surface}"
    rounded: "{rounded.account-card}"
    padding: "24px"
  account-shortcut-reward:
    backgroundColor: "{colors.reward-surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel-web}"
    padding: "16px"
  account-shortcut-neutral:
    backgroundColor: "{colors.canvas-mobile}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel-web}"
    padding: "16px"
  field-web:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "10px 12px"
  status-green:
    backgroundColor: "{colors.mint}"
    textColor: "{colors.green}"
    rounded: "{rounded.badge}"
    padding: "5px 10px"
  navigation-active:
    backgroundColor: "{colors.mint}"
    textColor: "{colors.green}"
    rounded: "{rounded.compact}"
    padding: "9px 12px"
  panel-web:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.panel-web}"
    padding: "23px"
  category-beverage:
    backgroundColor: "{colors.mint}"
    rounded: "{rounded.panel-web}"
    height: "60px"
    width: "81.5px"
  category-fresh:
    backgroundColor: "{colors.aqua}"
    rounded: "{rounded.panel-web}"
    height: "60px"
    width: "81.5px"
---

# Design System: TOUSHIR Commerce

## Overview

**Creative North Star: "Toushir's connected wholesale workspace"**

The supplied Figma file `4IffhmSg2gHg82narIGfMF` remains the visual authority. This document records how that existing world is implemented in the wholesaler workspace and merchant/courier applications: original Toushir assets, DM Sans, white surfaces, mint selection, bright green actions and dense operational content. It extends that world; it does not establish a replacement identity. Preserve the proportions of the original mark and navigation assets, and the identity of sourced product packaging.

The atmosphere is practical and active. Headings orient the user, tonal panels group work, and restrained status colours make current state visible. Merchant discovery gives product packaging, prices and direct ordering a compact commerce hierarchy. Operational views connect current metrics to the next action; deliberate state and progress motion makes changes legible without hiding the content. A commercial action has an explicit label and outcome. Unknown prices remain unconfirmed, demo content stays labelled, and illustrations never masquerade as observations. The source evidence is `apps/web/src/style.css`, `apps/web/src/main.tsx`, `apps/mobile/src/theme.ts`, `apps/mobile/src/components.tsx`, `apps/mobile/src/Merchant.tsx`, `apps/mobile/src/MerchantAccount.tsx`, `apps/mobile/src/Engagement.tsx`, `apps/mobile/src/Driver.tsx`, `apps/mobile/src/MerchantDiscovery.tsx`, `apps/mobile/src/MerchantActivity.tsx`, `apps/mobile/src/Motion.tsx`, `apps/web/src/OperationsDashboard.tsx`, `apps/web/src/Warehouse.tsx` and the DM Sans font registration in `apps/mobile/App.tsx`.

The authorized Astra revision preserves the original Figma core and uses a separate editable page (`261:21629`), with review cover `267:2376`. Its 41 structurally audited native-layer design states describe a wider prototype scope; current frontend translation centers on merchant Home, Offers, Orders, Account and Activity plus wholesaler G01. [The Astra implementation record](docs/ASTRA-DESIGN.md) separates the frame map, implemented geometry, motion intent and evidence. This update preserves unrelated CRM, pricing and operational workflows.

The previous expanded browser packet (`.impeccable/review/expanded/packet.json`) is historical evidence for the earlier implementation. Astra source builds passed. Final evidence records 18 passing journey/amendment groups, 20 passing final desktop/responsive checks and 9 passing mobile browser smoke checks, with zero reported JavaScript errors. The independent final design review gives a scoped ship verdict for the six inspected core surfaces. The Figma structural audit covers 41 editable states and 460 internal navigation actions with zero broken internal destinations; conditional branches were not all manually replayed. See the [final Figma handoff](verification/astra-design/FIGMA-HANDOFF.md) and [independent review](verification/astra-design/final-review.md). The `final-audit.json`, `final-node-ledger.json` and `final-prototype-ledger.json` files supersede earlier incomplete ledgers. This evidence does not certify complete 265-state parity, native-runtime acceptance, CI or deployment readiness.

**Key Characteristics:**
- Original Figma identity and assets across three operational workspaces.
- White and soft neutral surfaces with mint selection, green actions, blue information and purple pending/reward states.
- Compact DM Sans commerce hierarchy with tabular financial data on the web.
- Flat content, gently curved containers, transient overlays and measured state motion.
- Accessible chart exploration, explicit commercial states, demo labels and observed-data boundaries.

## Colors

Green provides continuity across navigation, actions and successful states; neutrals carry most of the interface. The frontmatter owns exact values, including the small platform-specific differences already present in source. Sidecar tonal ramps are derived swatch previews, not additional production colour tokens.

### Primary
- **Toushir forest** (`brand`): desktop heading and brand anchor; the courier tour summary also uses this dark foundation.
- **Operational green** (`green`): links, selection text, current steps and affirmative states.
- **Action green** (`action`): primary action backgrounds. Web buttons pair it with `action-ink`; mobile buttons use `ink`. The action fill remains stable in web dark mode.

### Secondary
- **Mint** (`mint`): active navigation, positive status surfaces, notices, and the beverage category shortcut. Account rewards use the separate reward surface.
- **Fresh aqua** (`aqua`): the fresh/milk category shortcut and legacy web informational status treatment. Astra informational content uses the separate blue pair. Other unselected merchant categories retain the soft neutral background. Selected category tiles use mint with an operational-green border and label.

### Tertiary
- **Information blue** (`info`, `info-surface`): Astra information, transport and the G01 net-collection series. Native `blue` aliases use the same pair.
- **Pending and reward purple** (`pending`, `pending-surface`, `reward`, `reward-surface`): labelled pending outcomes, reserved/due balances and reward/challenge surfaces. Native `amber` aliases resolve to the purple pending pair.
- **Legacy web caution** (`warning-web`, `warning-surface-web`): the existing cool aqua treatment outside the revised G01 surface. Its historical `--yellow` alias does not authorize yellow or beige. Preserve unrelated workflow styling.
- **Error red** (`danger-*` and `danger-surface-*`): rejected, failed, destructive and error states. Status labels accompany colour.

### Neutral
- **Ink** (`ink`): primary light-theme text; also the merchant loyalty card, selected filter pills, compact product-add control and native ink button variant. These dark components are part of the light interface, not a mobile dark theme.
- **Account-on-dark** (`account-on-dark`): secondary labels within the ink loyalty card; main point values remain white.
- **White surface** (`surface`): content panels, fields, product-image backplates and navigation bars.
- **Canvas** (`canvas-web`, `canvas-mobile`) and **sidebar** (`sidebar`): broad page context and subtle surface separation. The web soft fill uses the mobile canvas colour.
- **Divider and field border** (`line`, `border-web`): quiet one-pixel separation. Mobile shared dividers use `line`.
- **Muted** (`muted-web`, `muted-mobile`): secondary descriptions, metadata and timestamps. Astra native copy and G01 use `muted-astra`; the global web muted token remains unchanged. `muted-mobile-detail` remains an incumbent metadata value outside the scope of this token refresh.
- **Web dark palette** (`dark-*`): source-defined replacements for canvas, surfaces, text, dividers and semantic pairs. It belongs to the wholesaler web theme; the mobile source currently uses the light palette. The original desktop mark retains a white brand tile.

### Named Rules

**The State Has a Label Rule.** Colour supports a named state; it never supplies the only explanation of a commercial outcome.

**The Cool Pending Rule.** Use the Astra purple pending pair with a written state; retain the existing cool web caution pair outside this revision. Beige and yellow are excluded by the approved system change.

**The Category Meaning Rule.** Keep beverage mint and fresh/milk aqua in the merchant rail; extend other categories from the existing neutral treatment rather than inventing a rainbow.

## Typography

**Display Font:** DM Sans, with a sans-serif fallback on the web.
**Body Font:** DM Sans throughout. The mobile source's historical `Inter` family key loads `assets/DMSans.ttf`; it is an implementation alias, not a second typeface.

**Character:** Compact, direct and readable. Hierarchy comes from size and weight within one family, with modest negative tracking on headings. Financial figures in web tables, metric values and designated numeric fields use tabular numerals.

### Hierarchy
- **Web headline:** `headline-web` for page titles; it reduces at tablet/phone widths. The metric number uses the same large scale with a tighter line height and tabular figures.
- **Web title:** `title-web` for panel headings; smaller subsection headings use the observed 16px tier. G01 uses `overview-metric` for four separate KPI cards, reducing to 25px at 1350px and 24px/32px on phone layouts. Its compact chart reading is 17px/23px. The older operating-title and operating-metric tiers remain available to incumbent operational views.
- **Web body and labels:** `body-web` and `label-web`; operational tables and many descriptions use the compact 12px tier. Paragraph line height is 1.6.
- **Mobile heading:** `heading-mobile` for shared entry views; `merchant-title` and `courier-title` preserve the shipped surface-specific heading hierarchy.
- **Mobile title, body and label:** `title-mobile`, `body-mobile` and `label-mobile`; muted copy is 13px/20px and small supporting copy is 12px/18px.
- **Merchant tabs and account:** `merchant-tab-title` leads Offers, Messages, Orders and Account; account detail titles use `account-title`. The card uses `account-points` for available points, with `account-stat` for purchases/payment metrics. Shared action labels use `button-mobile`; filters use `filter-label`.

### Named Rules

**The One Family Rule.** Extend the DM Sans hierarchy and the confirmed Figma text styles before introducing another typeface.

## Layout

The desktop shell has a fixed sidebar (224px), sticky header (64px), page gutter (32px) and content maximum (1920px). Standard transaction metric rows retain four equal columns with a 16px gap. G01 uses four separate white KPI cards, each at least 146px high, with a 16px gap. Its chart-and-priorities grid uses an 872:424 ratio, 16px gap and 330px minimum panel height. Three lower panels use a 260px minimum height. At 1350px the lower grid becomes two columns with delivery spanning the row; at 1100px KPIs become two columns and the main region stacks; at 700px priorities and lower panels become one column. These surface-specific proportions are recorded in the Astra implementation record. Older transaction overview panels retain their own three-column/two-column-span composition. Content panels use the frontmatter panel padding; compact metric panels and phone layouts reduce density deliberately.

The base web layout adapts at 1250px, 1000px and 760px, with a wider-layout adjustment at 1800px. At the tablet tier, the sidebar reduces to 190px, overview becomes two columns and standard metrics become two columns. At the phone tier the sidebar slides in, its menu remains accessible in the header, the content gutter becomes 16px, overview stacks and the header is 58px high. Wide operational tables scroll inside their panel instead of shrinking columns into illegibility. The general data table has a 760px minimum width; permissions use a 600px minimum.

Merchant and courier layouts center a full-width phone surface within a 520px maximum. Their headers and content use 20px horizontal spacing, with 16px checkout/basket padding where implemented. Safe-area insets, scrolling and keyboard accommodation belong to the native composition. The merchant catalogue has two product columns, a horizontal category rail and persistent bottom navigation. Catalogue category tiles are 81.5px by 60px with a 12px rail gap, restoring the reference Home spacing. Product imagery uses a compact 120px height, reducing to 112px below a 370px app width; the first merchant viewport keeps price and add actions within the discovery hierarchy. Courier route content uses stacked task sections with a persistent four-item bottom bar.

Merchant Offers, Messages, Orders and Account use title-first headers with contextual controls; the catalogue retains its logo, address and search composition. Account content stacks the loyalty card, an actual reserved-points row, paired gift/activity shortcuts, purchase metrics and account rows. The loyalty card uses 24px padding and an 18px gap; its 179px reference height is content-derived in source. The two shortcut tiles use 16px padding, a 12px gap and a 95px minimum height. Orders uses a 447px minimum delivery hero with a 168px map/position area; Activity begins with a 154px minimum paid-total card, 94px minimum statistic cards and a 72px minimum due-balance row. Offers uses distinct discount, challenge and gift campaign heroes with live campaign terms.

### Named Rules

**The Contained Overflow Rule.** Preserve readable operational data and contain horizontal table scrolling within its panel.

## Elevation & Depth

Content is flat at rest: one-pixel borders, soft tonal fills and whitespace establish hierarchy. Shadows belong to transient overlays and geographic markers, not every panel. The depot's 3D scene is explicitly illustrative; the actual GPS map is a separate mode with capture time, accuracy, stale-signal treatment and an empty state when no position exists.

### Shadow Vocabulary
- **Toast:** `0 12px 36px #0f2c2c25`; temporary confirmation above content.
- **Order drawer:** `-15px 0 50px #0c271b1a`; a focused dossier over a translucent backdrop (`#0b211b45`).
- **Open phone navigation:** `20px 0 60px #0f2c2c30`; separates the revealed sidebar from the page.
- **GPS marker:** `0 4px 15px #14362930`; local map-marker separation.

Web buttons transition background and transform over 0.15s; their pressed state moves down 1px. The drawer enters over 0.22s with `cubic-bezier(0.16, 1, 0.3, 1)`. Phone navigation slides over 0.18s. Legacy activity curves reveal over 0.65s and aging bars transition height over 0.5s with the same easing; textual values are available independently. G01 explicitly disables the curve reveal. Its card/priority hover transitions use 160ms and distribution segments use 220ms; reduced motion disables all G01 transitions and animations. The Figma 120ms chart-exploration concept is design intent, not an observed source timing. The web reduced-motion preference collapses animation and transition duration to 0.01ms and disables smooth scrolling.

The Astra cart dock arrives once, on its first visible basket state: 180ms ease-out, 6px upward travel and opacity from 0.72 to 1. It does not replay for quantity updates or loop; reduced motion renders the resting state. Existing native state arrival moves 6px into place over 280ms with exponential ease-out; it never fades essential content from invisible. Measured progress animates a bounded, finite 0–100% value over 420ms and exposes a labelled progressbar. Native reduced motion starts enabled while preference loads, subscribes to changes, eliminates arrival offset and makes progress immediate. The illustrative truck replay advances only when replay is enabled and reduced motion is off; otherwise trucks rest at depot positions. This replay is independent of GPS and carries model attribution.

### Named Rules

**The Readable Without Motion Rule.** Text, numbers and operational status remain visible while animation is disabled, loading or complete.

**The Observed Position Rule.** Keep illustrative depot geometry distinct from authorized GPS; never turn an animation, missing signal or stale observation into a live-location claim.

## Shapes

Gently curved rectangles organize the interface. Principal web panels use `panel-web`, shared native panels use `panel-mobile`, narrow web panels use `panel-narrow`, and fields, buttons and badges use their more compact radii. The loyalty card uses `account-card`; paired account shortcuts use the existing 16px corner treatment. Shared filter pills use `filter-pill`, distinct from the rectangular category rail. The compact product-add control is a 44px circle using `product-add`; avatars and progress nodes are circular. Product packaging sits in a white image container with contain sizing to preserve its silhouette. Original community photographs may carry a grey field within their own pixels; that source background is preserved rather than retouched into invented studio imagery. Original Figma icon assets retain their proportions; new control icons use SVG paths or the existing native icon component, not decorative text glyphs.

## Components

### Buttons

Confident actions with a clear label. Web primary and secondary variants share the recorded minimum height and padding; the secondary adds a one-pixel border and soft-fill hover. Mobile primary buttons use the larger native minimum height, while small buttons use a 38px minimum. Native secondary buttons use mint; ghost buttons are transparent. The native `ink` variant uses ink with white text and the same sizing/states as other native buttons; it anchors the “Écrire à Toushir” support action. The catalogue add control is a separate compact dark circle with a white 22px plus icon, a 44px target and 0.7 pressed opacity; after adding, the existing quantity control takes its place. Loading and disabled state are explicit. Web controls use a three-pixel green focus outline with a three-pixel offset; native buttons show a green focus border. Keep the web dark-theme focus colour on action green.

### Chips

Compact status chips pair a semantic surface with legible text, a modest radius and a written status. Mobile statuses also include a small dot. Web badges use the 10px text tier; mobile chips use the shared small text style. Shared filter pills in Orders, Offers and Messages use ink with white labels when selected, and the soft mobile canvas with muted labels otherwise. Their visible surface is at least 36px high inside a 44px press target, with 14px horizontal padding and 0.7 pressed opacity. The merchant category rail is separate: beverage mint, fresh/milk aqua, neutral remaining tiles, with mint, a green border and green text for selection.

### Cards / Containers

White operational panels use a quiet border, rounded corners and internal spacing from the frontmatter. They remain flat. Metrics pair a muted label with a large value. Merchant product cards preserve white space around sourced packshots, then group product name, pack, price and the quantity action; the revised Home removes the separate brand line from this stack. Unknown price/availability is stated instead of displayed as a zero-price offer. Open Food Facts photographs retain their actual packaging, colour and community-image field; a white container is not a promise that every source image has a white background. Current source-photo quality remains a content limitation until the owner supplies approved replacements.

The merchant account card is a flat ink surface with the original white Toushir asset, large white available-point total and quieter secondary labels. It opens the real loyalty history. The row beneath it reports actual reserved points, not an estimated future earning amount. Gift and activity shortcuts pair the reward-purple surface and neutral fills. Purchase metrics show actual delivered orders and net amounts paid. “Documents et règlements” leads to commercial order documents and payment information; order slips are not presented as fiscal invoices.

### Inputs / Fields

White fields have a one-pixel border and the recorded field radius. Web fields are at least 40px high; native fields are at least 48px high with 12px vertical and 14px horizontal padding. Labels remain visible, placeholders are secondary, and caret colour is green on the web. Native focus changes the border to green; errors use the paired red treatment and text. Multiline fields grow vertically. Brand search in the merchant brand/filter panels fixes the field at 48px high with no flex shrink, preserving its touch target when surrounding content scrolls.

### Navigation

Desktop navigation combines the original Figma icons with compact labels, a mint active row and operational-green active text. At phone width, the menu button reveals the same navigation. Merchant and courier bottom bars keep icons and labels together, mark the selected item in green and expose their selected state. Existing section labels are navigation structure, not a pattern for decorative eyebrows elsewhere.

### Operational charts

Web charts plot the current service records with visible basis text, a current solid line, a dashed previous-period comparison when requested, zero-aware scales and an accessible SVG description. Pointer exploration is supplemented by a labelled keyboard range control and a polite live reading of date and value. Preset or custom periods and metric controls expose their selected state; drilldowns connect metrics to the relevant work. The merchant activity view offers 7/30/90-day ranges, purchase/net-payment measures, labelled selectable bars, negative refund values below zero and category progress with textual totals. It refreshes every 15 seconds, shows the refresh time and retains explicit loading, retry and empty feedback. Financial bases distinguish current confirmed-order totals, payment-date receipts less refunds, outstanding balances and categories.

### Order dossier and tracking

The web order drawer anchors to the right, spans up to 740px, scrolls internally and has a sticky header and horizontally scrollable tabs. Escape, focus containment and a labelled dialog preserve access to the dossier. Preparation, custody, receipt, actual payment and reconciliation retain separate controls and states. Successful feedback follows the server operation; visible unknown, loading, empty, error and conflict states remain part of the component behavior. `SUBMITTED` is “Reçue”, explicitly awaiting wholesaler confirmation; `CONFIRMED` is “Confirmée”. The merchant progress sequence keeps preparation, ready, departure, actual receipt and actual payment distinct. Amendments present immutable before/after terms with explicit merchant acceptance or refusal. Campaign audiences use real selected shops and region filters; fictional demonstration presets remain labelled.

## Do's and Don'ts

### Do:
- **Do** extend the supplied Figma world and preserve the original Toushir assets and sourced packaging identity.
- **Do** use green actions, mint selection, blue information and purple pending/reward states on quiet neutral surfaces.
- **Do** retain beverage mint, fresh/milk aqua and neutral unselected categories in the merchant rail.
- **Do** distinguish that category rail from ink-selected filter pills and preserve the pills’ 44px press target.
- **Do** show actual available/reserved points and identify commercial documents accurately.
- **Do** preserve sourced product-photo identity, including an original grey field, until approved owner photography replaces it.
- **Do** preserve keyboard focus, native touch targets, safe areas and contained table scrolling.
- **Do** label demo content, unknown commercial values, stale GPS and illustrative depot geometry explicitly.
- **Do** keep preparation, custody, receipt, payment and reconciliation visibly distinct.
- **Do** expose chart values, source basis, period and current selection in text and accessible controls.
- **Do** keep operational text visible and respect reduced motion for state arrivals, measured progress and truck replay.

### Don't:
- **Don't** replace unknown prices with zero or present catalogue provenance as an approved commercial offer.
- **Don't** invent pending earned points, turn order slips into fiscal-invoice claims or retouch source photography into a different product depiction.
- **Don't** imply that illustrative 3D or a missing/stale GPS signal is a live observed position.
- **Don't** spread overlay shadows across flat operational panels or replace original icons with decorative text glyphs.
- **Don't** reintroduce beige/yellow pending surfaces or turn legacy colour-alias names into new palette choices.
- **Don't** infer exact Figma-screen acceptance, native execution, external-provider success or domain/security acceptance from this design record.
