# TDC Cashier Till Control and AR Credit-Note Compatibility Retirement

**Implemented:** 2026-08-03  
**Scope:** Finance-owned physical cash custody, count, review, correction, and legacy AR write-path control  
**Related work:** WP2, WP5, `FIN-LIM-0013`

## Outcome

RHEMA ERP now controls a physical cashier till as a custody session over the existing Finance
liquidity subledger. The implementation deliberately reuses `LiquidityAccountType.CashTill` and
immutable `LiquidityAccountEntry` records instead of creating a parallel cash ledger. A cashier
opens a till with an approved opening float, records ordinary Finance receipts and payments against
that till, submits a denomination count, and an independent reviewer approves or returns the count.

The same slice closes `FIN-LIM-0013`. The weaker `CustomerPayment.IsCreditNote` compatibility model
remains visible only for historical audit/reporting. Every new credit-note write and every mutation
of a historical compatibility credit note is rejected and directed to the canonical Sales
credit-note workflow.

## Till accounting and custody rules

The expected physical cash amount is server-owned:

`opening float + signed liquidity movement - net posted deposit allocations`

Only immutable liquidity entries created from session opening through the frozen activity cutoff
are included. An increase adds cash and a decrease removes cash. Only a **posted** bank-deposit
allocation reduces physical custody; a draft or submitted deposit is still in the cashier's hands
and therefore cannot reduce the expected count.

The close snapshot stores transaction movement, deposited amount, expected amount, counted amount,
variance, threshold, custody-entry count, denomination lines, and activity cutoff. Later liquidity
activity cannot silently change an already submitted or approved close.

## TDC control defaults

- One open or pending-review session is allowed per till. The service uses serializable opening
  control and SQL Server also enforces a filtered unique index.
- A cashier may submit only the till currently assigned to that user.
- Cash receipts that explicitly select a physical till require a current open custody session owned
  by the cashier. Posted liquidity payments apply the same control when their destination/source is
  a cash till.
- Denomination totals are calculated by the server from denomination and whole-number quantity.
- Every non-zero variance requires a reason.
- `CashTillVarianceApprovalThreshold` defaults to GHS 100 and is stored with each submitted close so
  a later settings change cannot rewrite the historical review basis.
- `RequireIndependentCashTillClosure` defaults to true. A cashier cannot approve their own count.
- A variance above the configured threshold also requires reviewer comments.
- A reviewer may return a session for recount; the cashier resubmits a fresh frozen count snapshot.
- An approved close is immutable. A correction creates a linked new session and retains the source
  session, reason, actor, timestamps, evidence, and audit events.
- Optional opening and closing evidence references use tenant-owned controlled file records.
- Optimistic row-version checks prevent a stale browser command from overwriting a concurrent
  cashier or reviewer action.

## Permissions and API

The seeded Finance permission catalogue includes:

- `Finance.CashTills.Operate` for opening an assigned till and submitting counts.
- `Finance.CashTills.Closures.Review` for approval or return-for-recount.
- `Finance.CashTills.Sessions.Reopen` for creating an immutable correction session.

Authenticated Finance read access is required for session lists and details. The API is under
`/api/finance/cashier-tills` and provides session list/detail, open, submit-count, approve-closure,
return-for-recount, and reopen-as-correction commands. Tenant boundaries and ownership are repeated
inside the service rather than relying only on the controller policy.

The Finance navigation now exposes `/finance/cash/till-sessions`. The workspace supports operator
and reviewer flows, live expected cash, custody activity, denomination entry, variance explanation,
review comments, and immutable correction creation.

## Seed and migration

The idempotent Finance seed adds `TILL-MAIN-GHS` (`TDC Main Cashier Till (GHS)`) as an active
`CashTill`, using the existing tenant cash-control GL account. It does not replace an existing till
or account override. Numbering uses `TILL-{YYYY}{MM}{DD}-{####}` with a monthly reset.

Migration `20260803163336_AddCashierTillControlWorkspace` creates session/count storage, control
indexes and settings columns. It was applied successfully to `RHEMAERP` on 2026-08-03. The
idempotent `seed-db` path then completed and database verification confirmed the migration, till,
GHS 100 threshold, independent-review flag, and three permissions.

## `FIN-LIM-0013` resolution boundary

The compatibility flag remains in the entity/DTO so historical documents can still be inspected.
The following operations reject a compatibility credit note:

- general customer-payment creation with `IsCreditNote = true`;
- the legacy `CreateCreditNoteAsync` entry point;
- update and posting;
- allocate, reverse allocation, and ordinary receipt reversal;
- clear and bounce operations.

No historical conversion was added. This follows the approved development-phase rule that legacy
data and backward compatibility are not required. The stronger Sales credit-note workflow remains
the sole path for new customer credits, approvals, applications, posting, reversal, and correction.

## Verification

- API and test projects compile with zero errors. Existing unrelated nullable/code-quality warnings
  remain unchanged.
- The focused till/compatibility suite passes 3/3 tests for canonical expected-cash derivation,
  independent approval, overlapping-custody prevention, unexplained-variance rejection, and legacy
  credit-note write retirement.
- The combined release gate passes 95/95 tests across till controls, banking-settlement release
  gates, Finance permission authorization, controller security, and frontend/backend route parity.
- Targeted frontend ESLint passes for the till page, Finance cash service/types, and navigation.
- The full TypeScript check reaches only the two pre-existing missing
  `@syncfusion/ej2-react-pdfviewer` imports in unrelated PDF viewers; it reports no till diagnostics.

## Remaining WP5 work

This is the first WP5 slice, not completion of the entire package. The next Finance-only work is to
extend the existing deposit workflow with evidence and confirmation/reconciliation states, then add
controlled receipt/payment-slip copies and cash/bank operational reports. Cross-currency transfer
and reconciliation remains a separately tracked WP2/WP5 item. Live bank, mobile-money, POS, email,
or other external interfaces remain excluded.
