# Banking & Settlement

## Purpose

Banking & Settlement separates customer receipting from the physical or electronic
movement into a bank account. Cash, cheque, card, and mobile-money receipts first post
to a dedicated liquidity control account. A bank deposit then settles selected receipt
and eligible payment lines to a real bank account. The posted deposit creates exactly
one bank-facing cash transaction for the net amount, matching the normal one-line-per-
deposit-slip bank statement behaviour.

Direct bank methods (`BankTransfer`, `EFT`, `DirectDebit`, and `StandingOrder`) bypass
the banking queue and post directly to the selected bank account.

## Accounting model

Every liquidity location owns a separate GL control account:

| Liquidity type | Standard GL | Purpose |
| --- | --- | --- |
| Undeposited Cash | 1020 | Cash collected but not yet deposited |
| Cheques Awaiting Deposit | 1021 | Customer cheques held for banking |
| Mobile Money Clearing | 1022 | Mobile-money receipts awaiting settlement |
| Card Settlement Clearing | 1023 | Card receipts awaiting acquirer settlement |
| Bank | Existing bank GL | Actual bank statement balance |

Example:

1. Receipt A: Dr Undeposited Cash 700 / Cr AR 700.
2. Receipt B: Dr Undeposited Cash 300 / Cr AR 300.
3. Posted cash expense: Dr Expense 150 / Cr Undeposited Cash 150.
4. Deposit: Dr Bank 850 / Cr Undeposited Cash 850.

The deposit retains source allocations of 700, 300, and a 150 deduction while exposing
one GHS 850 transaction to bank reconciliation.

## Tenant provisioning

The standard development COA seeds the four holding GLs and liquidity masters.
Existing/custom charts of accounts are never silently changed. When a tenant has
missing required liquidity types, `/finance/cash/liquidity-accounts` opens a setup
wizard and requires an administrator to map each type to an existing posting-enabled
GL account. The wizard also captures the deposit-intact/net-banking policy.

Additional cash tills, provider accounts, or settlement locations can be created from
`/finance/cash/liquidity-accounts/new`. Do not create an "undeposited funds" bank
account; use a non-bank liquidity account linked to its own GL control account.

## Receipt routing

The customer receipt screen routes by configured payment-method type:

- Cash -> Undeposited Cash
- Cheque -> Cheques Awaiting Deposit
- Mobile Money -> Mobile Money Clearing
- Card -> Card Settlement Clearing
- Bank Transfer/EFT/Direct Debit/Standing Order -> selected bank account

Cheque receipts preserve cheque number and drawer bank. Partial deposit allocation is
supported for divisible collection types, including splitting one source receipt across
different deposits or bank accounts, provided currencies match. A physical cheque is
an atomic instrument: it must be deposited for its full remaining amount in one batch
and cannot be divided between bank accounts.

## Registering payments for net banking

The banking UI does not accept free-form deductions. A payment becomes eligible only
when a posted GL journal contains a net credit to a mapped non-bank liquidity control
account. From the new-deposit screen, select the posted journal line and classify it as:

- Cash expense
- Petty-cash replenishment
- Customer refund
- Other payment

Registration creates an immutable decreasing liquidity entry; it does not post a
second journal. This makes the existing posted journal the auditable source document.
The unique `(TenantId, SourceDocumentType, SourceDocumentId)` index prevents the same
posted line from entering the queue twice.

Tenants using `DepositIntact` cannot select deduction lines. Tenants using
`ControlledNetBanking` can apply deductions subject to the configured amount and
percentage limits.

## Deposit lifecycle

1. Create a draft at `/finance/cash/deposits/new`.
2. Select receipt and, when allowed, deduction lines. Each line can be partially used.
3. Enter the physical deposit date and deposit-slip/reference number.
4. Attach a primary deposit slip or equivalent evidence (PDF, PNG, or JPEG).
5. Submit. Allocations remain reserved while approval is pending.
6. A user in the `Chief Accountant` role approves, rejects, or returns it.
7. Final approval posts automatically when the tenant setting is enabled. If automatic
   posting is disabled, a user with journal-posting permission uses **Post deposit**.
8. The resulting `CashTransaction` is eligible for bank reconciliation.
9. After the bank acknowledges the physical deposit, an authorized user records the
   bank confirmation reference and date, with optional acknowledgement evidence.
10. The deposit register and detail page show reconciliation status derived from that
    existing `CashTransaction` and its `BankReconciliation`.

Bank confirmation does not post another journal and does not mark a transaction as
reconciled. It records the bank's acknowledgement of the deposit as a separate
operational control. Reconciliation remains the accounting comparison to the imported
bank statement and continues to use the existing reconciliation workflow.

The seeded development account is:

- Username: `chief.accountant`
- Email: `chief.accountant@default.com`
- Password: `Finance123!`

These credentials are development data only and must not be used in production.

## Evidence

Primary evidence is required before submission by default. PDF, PNG, JPG, and JPEG
files are accepted up to 10 MB. Evidence reuses the
existing file-upload records and virus-scan state used elsewhere in Finance. The
banking link stores document type, primary-evidence flag, uploader, and timestamp.
Attachments are frozen once a deposit is submitted.

## Bank confirmation

Only a posted deposit can be bank-confirmed. The confirmation reference and date are
required; the date cannot precede the physical deposit date or be in the future. The
reference is unique within the tenant and destination bank account so that one bank
acknowledgement cannot be attributed to two deposits.

The submitter cannot confirm their own deposit. The command also uses the deposit row
version so a stale browser cannot overwrite newer data. An exact retry is idempotent,
but a completed confirmation cannot be replaced with different values. PDF, PNG, JPG,
and JPEG acknowledgement evidence is optional and reuses tenant-owned, validated
Finance upload records.

The seeded TDC `Chief Accountant` baseline receives
`Finance.Banking.Deposits.Confirm`. Confirmation writes the retained confirmer,
timestamp, reference, date, notes and evidence link, and emits the
`BankDepositConfirmed` Finance audit event.

## Returned cheques

Create a returned-cheque case from `/finance/cash/returned-cheques`. The case remains
linked to the original cheque receipt, bank deposit, bank account, cheque number, and
drawer bank. Primary bank-return evidence is required before submission. Chief
Accountant approval:

- credits the bank for the returned amount and bank charges;
- reopens AR for the returned receipt and the customer-recoverable charge;
- sends the tenant-defined portion of the charge to Bank Charges expense;
- marks the original customer payment bounced and reverses its allocations; and
- creates a bank-facing returned-cheque cash transaction for reconciliation.

The default charge treatment and clearing-period days are tenant settings.

## Finance coding dimensions

Bank deposits and returned cheques use the generic Finance source-assignment and settlement-evidence
stores; no route-specific dimension columns are added. Both routes deploy as `CaptureOptional`.
Supplied, Fixed and Prohibited values are always validated, and missing account-required coding fails
before workflow approval or posting.

For a deposit, the batch ID is the stable destination-bank line and each persisted allocation ID is
its stable settlement line. Draft allocation edits update retained rows in place, so reopening a draft
hydrates the same assignments. Each allocation stores its canonical set, frozen snapshot, exact amount
and available source exchange-rate comparison evidence.

For a returned cheque, stable bank, customer-balance and expense identities are deterministic from the
case ID. Principal and discount components copy the original receipt's exact frozen allocation evidence.
Derived reversals remain split by combination; proportional rounding assigns the final residual to the
last deterministic evidence line so the journal reconciles exactly. A future `Enforced` route rejects a
case whose originating receipt evidence is unavailable.

Internal deposit detail and returned-cheque inquiry screens expose the source and settlement evidence.
Supplier/customer-facing documents are unchanged.

## Reconciliation

Statement import remains in `/finance/cash/reconciliation`. Download the statement
template from that screen, enter the bank's transaction date, description, reference,
debit/credit, and balance columns, then import it. Posted deposits and returned cheques
appear in the book-side matching queue as one transaction per bank statement movement.

Deposit reconciliation indicators are projections of the canonical bank-facing
`CashTransaction` and its reconciliation record. The deposit entity deliberately does
not store a second reconciliation status, preventing the deposit register and the
reconciliation workspace from drifting apart.

## Controlled payment slips and official receipts

Posted Finance cash/bank payments and posted AR customer payments now use the shared
controlled-document pipeline instead of the browser print command. The first PDF is an
Original. Every later copy requires the dedicated replacement permission and a reason
of at least 20 characters, is watermarked `REPLACEMENT COPY` on every page, and retains
its issuer, timestamp, copy number, reason, exact PDF SHA-256 hash, source transaction,
and journal link in the append-only issue register.

Issuing a slip or receipt never posts another journal. Reversed or bounced source
documents remain printable as historical evidence but carry a prominent adverse-status
notice. See
`docs/Finance/tdc-controlled-payment-slips-and-receipts.md` for source eligibility,
permissions, audit events, UI behavior, and deployment evidence.

## Key implementation points

- Domain: `BankingSettlement.cs`, `CustomerPayment.cs`, and `FinanceSettings.cs`
- API: `BankingSettlementController` and `BankingSettlementService`
- Posting: all new accounting journals use `IFinancePostingEngine`
- Workflow entity types: `BankDepositBatch` and `ReturnedChequeCase`
- Tenant settings: Finance Settings -> Banking & Settlement Controls
- UI: liquidity accounts, deposits, returned cheques, and bank reconciliation under
  Finance -> Cash & Bank
- Detailed control note: `docs/Finance/tdc-bank-deposit-confirmation-and-reconciliation-control.md`
- Controlled document note: `docs/Finance/tdc-controlled-payment-slips-and-receipts.md`

When adding a new payment source, post it to the correct liquidity GL first and register
an immutable `LiquidityAccountEntry` against the source document. Never manufacture a
bank transaction before the settlement is actually approved and posted.
