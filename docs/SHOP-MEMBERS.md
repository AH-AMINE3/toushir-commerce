# Personal accounts in one shop

Open **Mon Toushir → Équipe du magasin** in the merchant browser preview. The owner creates a private, single-use invitation, chooses its role and copies the link for that person. The invitee sees the named shop, role and expiration, then creates a personal password account. No email, SMS or WhatsApp delivery is implied by creating or copying a link.

| Shop role | Authority |
| --- | --- |
| Owner | Shop commerce, shared contacts/preferences/rewards, and all member roles |
| Manager | Shop commerce, contacts/preferences/rewards; invite/manage Buyers and Viewers |
| Buyer | Request/accept quotes, place orders, receipt quantities, claims and shared conversations |
| Viewer | Read shop commerce and conversations; maintain a personal basket/favorites without committing a shop order |

Wholesale roles remain separate. A shop role cannot grant ADMIN, FINANCE, WAREHOUSE, DISPATCHER or DRIVER powers. A Manager cannot manage another Manager or promote somebody to Manager. Membership cannot be nested or transferred between shops through these APIs.

## Identity and resource ownership

`Actor.Id`, OIDC `sub`, sessions, carts and favorites identify the actual person. `Actor.ResourceOwnerId` identifies the validated primary shop for prices, addresses, orders, receipts, wallet and conversations. New orders, order events, messages and receipt challenges retain their actual human actor. Existing order/event/challenge rows receive empty attribution columns during upgrade; no historical person is invented.

`ShopAccess` resolves current membership, role/version, workspace validity, account/shop suspension and the primary shop's guest invitation before an HTTP request, token exchange, live recipient publication or receipt completion. Revoked membership rows are retained permanently, preventing fallback to a new independent shop. New member sessions do not contain the demo workspace-owner capability. The browser remembers personal-account mode and returns to sign-in when authority expires instead of silently making a new demo.

Revoking/changing a member requires its current version and a meaningful reason. Sessions are deleted; unused receipt challenges issued by that person expire. Signed access and refresh grants still require the current live session, so old grants fail immediately. Consumed receipt history and accepted orders remain intact. A change of role requires a fresh sign-in. A pending invitation also fails if its issuer has lost the authority needed to grant that role.

## Invitations and concurrency

Only a hash of the random invitation bearer token is stored. The private URL carries it in a fragment; preview/accept send it in a POST body. Treat that URL as a secret. The token is returned once when issued and cannot be retrieved from the team list. Acceptance atomically creates one account and one permanent membership; concurrent acceptance has one winner.

The API bounds invitation validity to five minutes–seven days, and never beyond the primary shop's valid access. The UI defaults to at most 24 hours. This is the deadline to **join**, rather than an automatic expiration of an already accepted member. Accepted access remains subject to the workspace, primary guest invitation and current membership. Limits are 30 active members and 100 pending invitations per shop. New pending invitations to the same email replace the earlier pending links within the caller's management authority.

PostgreSQL anonymous acceptance takes the identity and workspace advisory locks; role changes/revocation use the same workspace mutation contract. Mutations revalidate authority after acquiring the lock. SQLite serializes mutations in the single process. Provider-specific additive migrations create `ShopMemberships`, `ShopMemberInvitations`, `ShopAccessEvents` and the three historical attribution columns.

## Live state

SignalR connections join session-and-authority groups. `LiveAudience` revalidates recipients at publication, including membership version and the actual session, instead of trusting an old account/order group. Publication serializes with revocation through the PostgreSQL workspace lock or the SQLite outbox gate. Clients receive scoped invalidations and must read current authorized data. GPS coordinates remain behind the authorized tracking endpoint. This describes the current single hosted instance; no distributed SignalR backplane or cross-instance delivery guarantee is claimed.

## API and verification

- Anonymous: `POST /api/auth/shop-invitation/preview`, `POST /api/auth/shop-invitation/accept`.
- Merchant team: `GET /api/shop/team`, `POST /api/shop/team/invitations`.
- Versioned/reasoned changes: `POST /api/shop/team/invitations/{id}/revoke`, `POST /api/shop/team/members/{id}/role`, `POST /api/shop/team/members/{id}/revoke`.

Run `scripts/test-shop-members.py` against an explicitly enabled demo API. For the separate live/OIDC suite, set `MEMBERSHIP_FIXTURE` to a private temporary file while running that API suite, then run `backend/scripts/verify-shop-members-live.mjs` with the same variable and delete the fixture. It contains temporary credentials and must never be committed or uploaded as a report. `MEMBERSHIP_REPORT` and `MEMBERSHIP_LIVE_REPORT` control the credential-free reports.

Recorded evidence is under `verification/shop-members/`, with actual run URLs, times and assertions. Browser tests in `tests/browser/shop-members.mjs` exercise the real forms, joined identity, recovery, restrictions and two actual server-rejected submissions. The independent design review is under `.impeccable/review/shop-members/`. Provider-upgrade evidence distinguishes an executed SQLite backup migration from generated alternate-schema PostgreSQL SQL; actual hosted results are separate.

Native simulators remain off at the user's request. The current browser captures verify the React Native web export; they do not establish device keyboard, gesture, accessibility or performance behavior. Android 1.1.0 downloads predate this team extension and remain separately verified pricing-release packages until rebuilt.
