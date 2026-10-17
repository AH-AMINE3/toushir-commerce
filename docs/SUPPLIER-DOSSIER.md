# Supplier evidence and confirmations

The purchase dossier records supplier PDFs, proposed terms and an independent review alongside actual receiving. Uploading or accepting a document never receives goods or records a payment. These are records entered by the operator; the application does not authenticate a supplier signature or issue a fiscal invoice.

## Operational flow

1. An administrator creates a purchase. A different authorized account approves it.
2. Admin, Warehouse or Finance attaches a purchase-order PDF. Admin or Warehouse records the supplier's confirmation against an active confirmation PDF, including every line and zero for unavailable references.
3. Another Admin or Finance account accepts or rejects the proposal. Acceptance changes quantities, unit costs and expected date only before physical receiving. A stale proposal cannot overwrite changed terms.
4. Warehouse records actual goods, batch identity, expiry and quarantine through the existing receiving flow. Each physical receipt can have its own PDF. The supplier invoice is visible only to Admin/Finance and is compared with the value actually received.
5. A document chain is complete only with an active purchase-order PDF, independently accepted confirmation backed by an active PDF, PDFs covering every actual receipt, and an invoice PDF whose reference matches a successful amount comparison. This label is evidence completeness, not authenticity or fiscal certification.
6. Another authorized account may close an undelivered balance. Five units received out of eight remain five actual receipts and three cancelled units. A closed balance cannot be received later.

After receiving, missing confirmation evidence can be added and independently reviewed only with the exact existing quantity, cost and expected date. This is explicitly recorded as evidence added after receipt. Historical costs and physical quantities remain unchanged.

## Immutable records and access

Each private PDF has its actual SHA256, original bytes, author, date, reference and version. A revision preserves its predecessor. Replacing accepted evidence requires a new confirmation review; it does not inherit approval. Accepted supporting evidence cannot be voided in place. Voiding any other active piece records an actor, time and reason while retaining the original.

Downloads require the same workspace and role checks as the dossier and return an attachment with `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`. The browser sends its current bearer identity, independent of another role's browser cookie. Warehouse cannot list financial document metadata, see those document events or download invoice/credit-note bytes. Merchants, drivers, anonymous callers and other workspaces have no supplier-file access.

Files are bounded PDFs: 2 MiB each and 50 MiB retained storage per business workspace; demo limits are 512 KiB and 2 MiB. Superseded/voided originals still count toward quota. Each dossier permits 100 documents (12 in demo) and 50 confirmations. Database transactions, version checks, idempotency hashes and workspace mutation locks protect writes. PostgreSQL dossier reads use one repeatable-read snapshot.

## API and storage

The authenticated `/api/admin/purchases/{id}` group adds:

| Method and suffix | Action |
| --- | --- |
| GET `/dossier` | Versioned evidence metadata, confirmation history and completeness |
| POST `/documents` | Bounded private PDF or explicit revision |
| GET `/documents/{documentId}/content` | Authorized original-byte attachment |
| POST `/documents/{documentId}/void` | Versioned reasoned void |
| POST `/confirmations` | Immutable supplier proposal or late evidence |
| POST `/confirmations/{confirmationId}/review` | Independent accept/reject |
| POST `/cancel` | Cancel an unreceived purchase |
| POST `/close-remainder` | Close only the outstanding part of a partial receipt |

`SupplierDocuments`, `SupplierConfirmations` and `SupplyProcurementEvents` are additive provider-specific migrations. PostgreSQL uses the configured dedicated schema; it never falls back to `public`. SQLite supports both new installations and upgrades of earlier data. A rejected operation changes no stock, received quantity or balance.

## Verification

`scripts/test-supplier-documents.py` exercises 32 API checks and 112 requests. `tests/browser/supplier-documents.mjs` runs ten real-form checks, including independently reviewed changed terms, five received/three cancelled units, private download bytes, reload, 390-pixel controls and delayed response selection races. CI runs both suites in isolated API processes so public demo rate limits remain enabled.

Executed reports under `verification/supplier/` retain their actual date, URL and scope. They include existing engagement/warehouse regressions, domain tests and upgrade evidence. PostgreSQL SQL generation is distinguished from an actual hosted migration. Review screenshots are real browser captures; native simulators remain off at the user's request.

The browser suite deliberately holds the previous approval’s dashboard refresh and verifies that the next receiving action is disabled until it finishes. Workspace controls use the actual pending mutation state, preventing a fast follow-up click from being silently ignored. Separate browser regressions cover existing purchase, messaging, campaign and13desktop navigation paths.
