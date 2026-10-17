# TOUSHIR commerce design system

## Authority

The supplied Figma file `4IffhmSg2gHg82narIGfMF` controls the current merchant, courier and wholesaler implementation. The audit retains frame IDs, source evidence, text styles and variables. Static brand/navigation assets are downloaded from Figma and retain their proportions. Real product packaging images preserve identity and source attribution.

## Tokens

DM Sans is self-hosted. Main ink #151917, brand #0F2C2C, white surface, canvas #F4F6F4, line #E7EBE7, muted #6B736F, green #087F4F, action #06DD81 with dark text, mint #D9EDDF and aqua #CCFFFF follow the inspected Figma system. Warnings and errors use semantic restrained yellow/red surfaces, never decoration. Dark mode has its own readable foreground/background tokens and preserves the original mark on a white brand tile.

Desktop sidebar224px, header64px, page gutter32px, principal panels16px radius and controls40px follow the source composition. Phone layouts use safe areas, scrolling and native touch conventions. Numeric financial data is tabular. Layout collapses at tablet/phone widths; wide operational tables scroll within their panel.

## Interaction

One obvious next action per commercial state; secondary actions remain explicit. Forms preserve unknown versus zero values. Stale versions, conflicts, failed requests, loading and empty states are visible. Keyboard focus, selection, caret and scrollbars use the same palette. Drawers support Escape, focus containment and screen-reader dialog labels.

Preparation, custody, receipt, payment and reconciliation use separate controls. Scans validate actual server product/batch expectations. Every success follows a persisted server mutation. Animated depot geometry is labelled illustrative; actual GPS includes timestamp/accuracy/staleness and a visible empty state without invented drivers.

## Verification

Inspect real populated screens, narrow layouts and both themes in a browser. Native execution has independent evidence. A shared component or route is not proof that all 265 exact Figma states were individually accepted. The screen matrix preserves those distinctions.
