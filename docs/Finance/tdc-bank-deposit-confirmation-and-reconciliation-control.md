# TDC Bank-Deposit Confirmation and Reconciliation Control

## Outcome

The Finance-owned bank-deposit workflow now covers the operational chain from draft
preparation through approval, posting, bank acknowledgement, and statement
reconciliation without introducing a live bank interface.

This delivery extends `BankDepositBatch`, its existing workflow, the posted
bank-facing `CashTransaction`, and `BankReconciliation`. It does not create a parallel
deposit workflow or a second reconciliation ledger.

## Control chain

1. A preparer creates a deposit, selects eligible liquidity entries, records the
   physical deposit date and slip reference, and attaches the required primary slip.
2. Submission reserves the selected allocations and freezes submission evidence.
3. The existing workflow enforces independent approval and posts one net bank-facing
   cash transaction through the central Finance posting path.
4. After the bank acknowledges the deposit, a different authorized user records the
   acknowledgement reference and date, optional notes, and optional validated evidence.
5. The deposit register projects reconciliation status from the posted cash transaction
   and its canonical bank reconciliation.

The slip reference proves what was lodged, the bank-confirmation reference proves what
the bank acknowledged, and reconciliation proves how the posted book movement matched
the bank statement. These controls are intentionally separate.

## Bank-confirmation rules

- The deposit must be posted.
- The bank reference and confirmation date are required.
- The confirmation date must be on or after the physical deposit date and cannot be in
  the future.
- The reference must be unique for the tenant and destination bank account.
- The submitting user cannot confirm the same deposit.
- The client must provide the current row version; stale changes fail with a concurrency
  response instead of silently overwriting data.
- An identical retry is safe and idempotent. A completed acknowledgement cannot be
  replaced with different confirmation facts.
- Optional evidence must be a tenant-owned, validated PDF, PNG, JPG, or JPEG upload.
- Confirmation retains the user, time, reference, date, notes, and evidence link and
  emits the `BankDepositConfirmed` Finance audit event.

## Authorization and TDC default

The dedicated permission is `Finance.Banking.Deposits.Confirm`. The seeded TDC
`Chief Accountant` role receives it because confirming bank acknowledgements is an
operational Finance control, while maker-checker enforcement still prevents the
deposit submitter from completing their own confirmation. Roles configured to receive
all Finance permissions continue to receive it through the existing seed path.

## Reconciliation visibility

The deposit API and user interface expose whether the posted bank-facing transaction
has been reconciled, the reconciliation identifier and status, and relevant completion
and approval times. These fields are calculated from `CashTransaction` and
`BankReconciliation`; they are not mutable deposit fields.

This design prevents contradictory states such as a deposit showing “reconciled” while
the reconciliation workspace still treats its bank transaction as open. Bank
confirmation may happen before or after statement matching because it captures a
different control fact.

## User interface

- The deposit detail page presents the confirmation form only for posted deposits that
  are still awaiting acknowledgement.
- The form accepts the required reference and date plus optional evidence and notes.
- The detail page displays retained confirmation evidence and links to the associated
  reconciliation when one exists.
- The deposit register highlights deposits awaiting bank confirmation and posted,
  unreconciled deposits as separate operational queues.

## Data and deployment

Migration `20260803182450_AddBankDepositConfirmationControls` adds the confirmation
columns, evidence relationship, status/date lookup index, and filtered unique bank
reference index. It is applied to `RHEMAERP`.

The application seed has also been run. Database verification confirmed all seven new
confirmation columns, the new permission, and its `Chief Accountant` grant.

## Verification

- Finance API build: passed with no errors.
- Focused frontend lint for the changed deposit screens and service/types: passed.
- Consolidated Finance release gate: 96 tests passed.
- Coverage includes posting preconditions, maker-checker enforcement, date validation,
  duplicate reference prevention, evidence/audit retention, initially unreconciled
  visibility, and reconciliation projection after completion.

The repository-wide frontend type check remains blocked only by the pre-existing
missing Syncfusion PDF viewer package in the central and procedure document viewers;
no changed bank-deposit file reports a type error.

## Scope boundary

This slice records manually supplied bank acknowledgement facts and consumes the
existing manual bank-statement reconciliation capability. It does not connect to a
bank, mobile-money provider, POS service, or another ERP module.
