## verdict

- **TSH-F01 — resolved.** `merchant-checkout-received-toast.png` visibly says “Commande reçue. La confirmation de votre grossiste est attendue.” The supplied actual checkout returned SUBMITTED, and the reviewed source now conditions this text on `result.status === "SUBMITTED"`. Re-read `merchant-order-success.png` also shows “Reçue” and explicitly awaits confirmation; `merchant-catalog.png` uses “Votre commande est reçue.”
- **TSH-F02 — resolved.** Re-read `merchant-catalog.png` at 402×874 shows both 365 DA and 385 DA, with complete add controls above navigation while the active-order strip remains visible.
- **TSH-F03 — resolved.** Re-read `merchant-filters.png` shows the full brand-search field with entered “Ifri” text, followed by a separated choice rail without overlap.
- No material regression from this fix batch is visible in the scored captures. This verdict scores only the three original findings; it is not a new whole-surface or physical-native review.

## remaining

Clear. Ship covers the scored fixes, not the whole surface. The scope and truth boundaries in `reviewer-round1.md` remain in force, including the planned design-record update and separate backend/native/external-provider evidence.

disposition: ship
