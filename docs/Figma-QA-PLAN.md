# Toushir — independent Figma acceptance plan

This is a test plan derived from the Figma contracts, not a record of executed or passed tests. Test against both SQLite local and PostgreSQL deployment, fresh database and upgrade database. Keep request/response, actor, version, ledger changes and screenshots as evidence.

## Release-blocking transaction checks

| ID | Scenario | Required result |
|---|---|---|
| Q01 | Replay order creation, same key/body | Same order; one stock effect; changed body with same key rejected. |
| Q02 | Two shops reserve the last carton concurrently | One accepted reservation; one conflict; no negative available. |
| Q03 | Accept V2 after V3 is current | 409; no reserve/preparation on stale version. |
| Q04 | Amendment refused or times out | Existing contract preserved; no silent acceptance/cancellation. |
| Q05 | Pick20 with P100/R20, then depart | ATP80 throughout; departure P80/R0; replay no second movement. |
| Q06 | Wrong barcode, lot or version at pick | Line blocked; no substitution or valid completion. |
| Q07 | Vehicle capacity exceeded | Assignment refused server-side; map cannot bypass. |
| Q08 | Wrong driver/depot/manifest at custody QR | Refused; no physical stock or custody mutation. |
| Q09 | Merchant refuses2 of10 Cola; accepts4 Candia | Accepted12/refused2;18600 payable; refused2 remain accounted for. |
| Q10 | Merchant approves then receipt lines change | Old QR invalid; fresh explicit approval required. |
| Q11 | Wrong store/driver/expired QR/reused nonce | Refused; valid same-key retry returns original proof. |
| Q12 | QR verified without cash entry | Receipt verified; payment still unpaid. |
| Q13 | Gross20000/change1400 against18600 | Net18600 once; merchant balance0; receipt same on all roles. |
| Q14 | Gross18000 or incorrect change | No fully-paid state; clear difference and no lost proof. |
| Q15 | Route expected64000/count63500 | Internal discrepancy500; merchant's18600 remains settled. |
| Q16 | Return1 accepted Candia after payment | Quarantine first; credit2000 once; refund separate and capped. |
| Q17 | Two concurrent allocations/refunds | Cannot exceed available funds/credit; replay has one effect. |
| Q18 | Supplier100 expected/96 received/6 quarantined |90 sellable+6 quarantine+4 outstanding; second retry no duplicate stock. |
| Q19 |1280 points reserve900, replay, fulfill |380 available/900 reserved then0 reserved; no second debit. |
| Q20 | Concurrent rewards exceed points or stock | Only affordable/in-stock reservation succeeds; no negative ledger. |
| Q21 | Cancel reward twice | One release; fulfilled reward cannot be silently canceled. |
| Q22 |1000-bottle challenge |166 cartons insufficient;167 qualifies only accepted/paid units; returns corrected. |
| Q23 | Buy20/get1; buy40 | One gift maximum; no points or double progress for free units. |
| Q24 | Promotional opt-out before dispatch | Excluded then; service recall remains scoped and separate. |
| Q25 | Merchant guesses another tenant/order/file ID |403/404 without data leak; same check on exports and hub groups. |
| Q26 | Role/session revoked while screen open | Next mutation/subscription refused; local state cannot override. |
| Q27 | Internal note and public message | Only authorized staff see note; merchant sees its own thread only. |
| Q28 | Crash/offline before and after server commit | Recover original result by stable key; no second order/payment/message. |
| Q29 | Stop app with unsynced proof | Preserved locally; no “authenticated” claim before server verification. |
| Q30 | Push/SMS unavailable after receipt | Receipt retained; notification retries independently. |
| Q31 | GPS stale/out-of-order/future | Last timestamp visible; stale movement stops; unauthorized updates refused. |
| Q32 | Change store address after order sent | Order retains accepted snapshot; current profile separate. |
| Q33 | Reopen/update quote with new prices | Previous accepted contract and decisions remain auditable. |
| Q34 | DB restart/backup restore and event replay | Orders, proofs, balances, stock and event IDs reconcile; no duplicate effects. |

## Interface and device checks

Compare only the screen under test against its Figma node with real local assets and measured geometry: desktop1600×1040 and1280; mobile402×874 and320/360/390/430. Verify exact product/brand assets, aspect ratio, cart dock, five tabs, screen gutters, DM Sans, both themes, readable numbers and long values. Test keyboard focus, drawer escape/focus return, screen-reader names, enlarged text, safe areas, keyboard avoidance, FR/AR/RTL and reduced motion. Every filter/search/back path must retain relevant context and unsent drafts.

Use actual Android/iOS simulators for camera permission denied, scan fallback, GPS permission revoked, low accuracy, app background/resume, process death and network loss. A browser viewport is not evidence of native-device behavior. Use two concurrent accounts and a separate tenant to exercise live refresh and isolation.

## Completion evidence

Record each screen's result in Figma-SCREEN-MATRIX.csv only after visual and functional testing. Keep original requirement wording unresolved until the separately cited79-page PDF is available. Source review, compiled code, passing unit tests, native simulation and publicly reachable staging are separate evidence categories.


## Executed evidence available

The plan above remains a scenario checklist. Concrete execution records are `Figma-ENGAGEMENT-TEST-RESULTS.json` (51 real API checks) and `Figma-WAREHOUSE-TEST-RESULTS.json` (29 real API checks), together with the backend verification artifacts listed in `Figma-IMPLEMENTATION-REVIEW.md`. These records cover defined local scenarios; they do not turn unexecuted device, provider, performance or exact-screen checks into passes.
