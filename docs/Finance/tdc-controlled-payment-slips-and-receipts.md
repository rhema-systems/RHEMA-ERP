# TDC Controlled Payment Slips and Official Receipts

**Implemented:** 2026-08-03  
**Requirement:** `FR-CB-008` and WP5 build item 3  
**Scope:** Finance-owned document output only; no bank, mobile-money, POS, customer-portal, or other-module interface

## Outcome

Finance can now issue a controlled A4 payment slip from a posted Finance cash/bank
payment and an official receipt from a posted AR customer payment. Both documents are
derived from the existing canonical transactions and journals; issuing a document does
not create or modify accounting entries.

The first successfully issued copy is the Original. Every later copy is a Replacement,
requires a reason of at least 20 characters, carries a visible `REPLACEMENT COPY`
watermark on every page, and is written to an append-only issue register. This removes
the uncontrolled browser-print path that previously could not distinguish an original
from a later copy.

## Canonical source rules

### Payment slip

- Source: `CashTransaction` with transaction type `Payment`.
- The transaction, approval state, and linked journal must all be posted.
- The payment account, final counter-account, bank allocation, reference, narration,
  amount in words, journal number, posting date, and accounting lines are rendered from
  the retained Finance records.
- A compensating reversal row is not treated as a new payable document. If the source
  payment has been reversed, the document visibly states that adverse status.

### Official receipt

- Source: the canonical non-credit-note `CustomerPayment`.
- The receipt must have a posted journal and be in `Posted`, `Cleared`, `Bounced`, or
  `Reversed` state.
- Customer, payment method, settlement account, allocation, withholding, reference,
  journal, and accounting-line details come from the existing AR receipt chain.
- Bounced and reversed receipts remain available as historical evidence, but the PDF
  visibly states the adverse status so that an old document cannot be mistaken for a
  currently valid receipt.

These rules deliberately build on the posting, bank-scope, reversal, reconciliation,
and transaction-trace foundations already in Finance. There is no parallel payment or
receipt ledger.

## Copy control and retained audit

`FinanceControlledDocumentIssue` is the durable, tenant-scoped issue register shared by
both document types. Each row retains:

- document and canonical source identities;
- document number, copy number, and Original/Replacement classification;
- required replacement reason;
- issuing user identity and UTC timestamp;
- linked journal entry where applicable;
- emitted filename, MIME type, and SHA-256 hash of the exact PDF bytes; and
- the normal entity creation metadata.

Issuance uses a serializable database transaction and a unique active-copy-number index.
The service rechecks the sequence inside that transaction, so simultaneous browser tabs
cannot both receive an Original. Database check constraints independently enforce the
copy number, allowed copy states, replacement-reason rule, and 64-character content
hash.

Successful issue and replacement actions emit separate Finance audit events for payment
slips and customer receipts. The source detail endpoints return the current issuance
summary so the UI shows whether an Original exists, the latest copy number, and the last
issuer/time before offering the next action.

## Permissions and TDC defaults

- `Finance.CashBank.Documents.Issue` permits the first controlled copy.
- `Finance.CashBank.Documents.Reprint` is additionally required for every Replacement.
- Issue permission is seeded for Finance Clerk, Accounts Officer, Accounts Payable
  Officer, Accounts Receivable Officer, Senior Accountant, Finance Manager, Chief
  Accountant, Financial Controller, and administrator roles.
- Replacement permission is deliberately narrower: Finance Manager, Chief Accountant,
  Financial Controller, and administrator roles.

The source transaction's existing tenant and bank/liquidity access checks continue to
apply in addition to these document permissions. The narrower replacement grant is the
reasonable TDC default because a later official copy should require accountable Finance
supervision without forcing routine original issuance to senior management.

## User workflow

The cash-payment and AR-receipt detail pages expose a controlled issue action only when
the canonical source is eligible. The first action issues and downloads the Original.
After that, an authorized supervisor must open the Replacement dialog, enter a
substantive reason, and submit it before the server renders and records the next copy.

The client cannot assign copy numbers, choose Original after one exists, omit the reason,
or turn a preview into an unrecorded official copy. Those decisions remain server-owned.

## Deployment and verification

- Migration: `20260803193523_AddControlledCashBankDocumentIssuance`
- Applied database: `RHEMAERP`
- Seed command reconciled the new permission catalogue and TDC role grants.
- Focused tests cover original issue, duplicate-original refusal, reason-required
  replacement, retained PDF hash and audit, AR receipt issue, source eligibility, and
  permission routing.
- The affected frontend files pass ESLint. The repository-wide TypeScript check still
  reports only the pre-existing missing Syncfusion PDF viewer declarations in the two
  unrelated document-viewer components.

## Limitation disposition

This slice resolves WP5 build item 3 and strengthens `FR-CB-008`. It does not claim to
resolve a separate `FIN-LIM-*` row. In particular, cross-currency transfers and
reconciliation remain open under `FIN-LIM-0021`, while broader optional formatted
Finance report packs remain the accepted non-blocking remainder of `FIN-LIM-0046`.
