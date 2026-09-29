# Finance cash, banking, reconciliation, till and FX production-readiness audit

Date: 2026-09-29
Scope: bank-account administration, cash receipts/payments/transfers, cashier tills, liquidity accounts, bank deposits and confirmations, returned cheques, statement import and reconciliation, cash-position reporting, controlled cash/bank documents, and foreign-currency revaluation/reversal.

## Executive verdict

The in-scope implementation is **code-ready for controlled UAT** after the corrections in this audit. The focused release gate passes **200/200 backend tests**, **14/14 focused frontend tests**, and ESLint for every changed cash/bank page.

It is **not yet responsible to declare the repository or a production tenant go-live-ready**. Two existing governance gates remain outside this code correction:

1. `FIN-LIM-0017`: representative tenant migration, cleanup, opening-balance/subledger/GL reconciliation, and accountant sign-off are still open.
2. `FIN-LIM-0014`: tenant-aware role-grant hardening or explicit product/security acceptance is still open. Runtime data access is tenant-scoped, but the role-grant model requires a production decision.

The repository-wide Finance test run and frontend type-check also have unrelated baseline failures. These must be cleared, formally quarantined with owners, or accepted by the release authority before a production deployment. They are not cash/bank functional failures, but a red global gate cannot support an unconditional production sign-off.

## What was vetted

The review followed each path from route and permission, through service validation and workflow, to central posting, persisted operational evidence, audit history, reversal/correction, reporting, and UI exposure.

| Area | Controls verified |
|---|---|
| Bank accounts and liquidity | Tenant-scoped lookup and mutation; same-tenant GL links; active-account rules; operational balance snapshots; repair from posted GL; access scopes |
| Cash receipts, payments and transfers | Draft/approval/post lifecycle; posting-engine use; exact account/book/period evidence; idempotency; same- and cross-currency transfer pairs; reversal lineage; reconciliation eligibility |
| Cashier tills | Active configured till; one active custody session; assigned cashier ownership; denomination counts; variance reason; independent review; row-version concurrency; immutable correction/reopen session |
| Deposits | Eligible source selection; reservation against double banking; evidence attachment; frozen submission evidence; maker/checker review; separate posting authority; one net bank movement; bank acknowledgement; reconciliation link |
| Returned cheques | Posted cheque/deposit provenance; bank-return evidence; independent approval; bank debit and AR reopening; charge treatment; immutable original deposit; rejection/correction |
| Statements and reconciliation | Controlled import; account/currency scope; auto/manual matching; direction and amount checks; explicit reviewed date exception; posted bank-only adjustment; completion; independent approval and lock |
| FX revaluation | Exact accounting book and period; approved rate evidence; functional-currency governance; deterministic preview/fingerprint; foreign AP/AR and bank/cash exposure; posting; idempotency; reversal and audit |
| Reporting/documents | Cash-position aggregation; GL-derived reconciliation balance; controlled original/replacement payment slips and receipts; retained hashes and evidence |
| Security and operability | Central permission convention plus explicit controller policies; UI action visibility; tenant isolation; audit events; retries/idempotency; concurrency and stale-version handling |

## Gaps found and rectified

### 1. Legacy AR credit-note writes could bypass their fail-closed guard

**Risk:** The retired `CustomerPayment.IsCreditNote` path was rejected inside an execution-strategy callback. An alternate/composed `IUnitOfWork` that did not invoke that callback could swallow the invariant and permit a new legacy credit record.

**Correction:** The guard now runs before execution-strategy delegation and before any database work. New customer credits must use the governed Sales credit-note workflow. The previously failing regression now passes.

### 2. Deposit posting required two permissions accidentally

**Risk:** The controller required `Finance.Banking.Deposits.Submit` while the central convention required `Finance.Workflow.PostAfterApproval`. ASP.NET composes those policies, so a properly assigned post-after-approval operator was denied unless they also had the preparer/submission permission. This weakened separation-of-duties design and made the production entitlement matrix inaccurate.

**Correction:** The explicit controller and central policy now both require only `Finance.Workflow.PostAfterApproval` for manual deposit posting.

### 3. Returned-cheque approval required an unrelated deposit-approval permission

**Risk:** Returned-cheque approve/reject explicitly required `Finance.Banking.ReturnedCheques.Manage`, while the convention added `Finance.Banking.Deposits.Approve`. A correctly provisioned returned-cheque reviewer therefore received a false denial.

**Correction:** Both layers now consistently use `Finance.Banking.ReturnedCheques.Manage`. Contract tests assert the convention and controller attributes together so this double-policy defect cannot silently return.

### 4. High-risk UI actions were exposed based on status alone

**Risk:** The API rejected unauthorized calls, but bank/deposit/reconciliation screens displayed actions to users who did not hold the corresponding permission. This caused misleading workflows and increased avoidable 403/error traffic.

**Correction:** UI visibility is now permission-aware for:

- bank-account creation;
- receipt/payment shortcuts;
- reconciliation entry, statement import, match/adjust/finalize and approval;
- deposit create/edit/evidence, submit, approve/return/reject, post and confirmation;
- returned-cheque capture, dimension edit and approval.

Backend authorization remains authoritative; the UI is now aligned with it.

### 5. Cash-position fallback no longer satisfied its production DTO contract

**Risk:** The bank-account page's locally built `CashPositionSummary` omitted `asOfDate`, breaking TypeScript validation and losing the measurement timestamp.

**Correction:** The summary now records an ISO `asOfDate`. The in-scope frontend TypeScript error is gone.

### 6. Controlled cash-document tests used obsolete journal fixtures

**Risk:** The payment-slip and customer-receipt tests created posted journals without the now-required stable `AccountingBookId`. Three release-gate tests failed before exercising the document controls, creating a false loss of coverage.

**Correction:** The fixture now creates an active default IFRS book and assigns its stable identity to each journal and transaction line. All four controlled-document tests pass.

### 7. Permission regression file was not discoverable by the curated test project

**Risk:** The test project disables implicit compile items, so merely adding the tests would not place them in CI.

**Correction:** The new cash/bank permission contract tests are explicitly included in the test project. Six permission-contract cases pass.

## Verification evidence

### Passing in-scope gates

- Backend cash/bank/till/reconciliation/FX release selection: **200 passed, 0 failed, 0 skipped**.
- Controlled cash/bank document tests: **4 passed**.
- Cash/bank permission contract tests: **6 passed**.
- Focused frontend banking policy, statement import, and FX revaluation tests: **14 passed across 4 files**.
- ESLint for all changed cash/bank pages: **passed with no findings**.
- The in-scope cash-account TypeScript error was removed.

### Repository-level gates that still fail

- Broad Finance backend run: **1,644 passed, 105 failed, 61 skipped; 1,810 total**. The sampled failures are existing accounting-book/test-fixture and other Finance-domain drift, including missing `AccountingBookId`/base-book shape in older fixtures. They are not failures of the focused cash/bank gate, but they block an unconditional repository-wide release claim.
- Frontend `npm run type-check`: still fails in unrelated development, inventory, procurement, reporting, AP/AR test, and mock-data files. After this correction it reports no error in the changed cash/bank pages.
- Production migration rehearsal and accountant sign-off remain open under `FIN-LIM-0017`.
- Tenant-specific role-grant governance remains open under `FIN-LIM-0014`.

## Manual production-acceptance test plan

Run this against a restored production-like database after all migrations, never first against production. Retain screenshots, exported reports, API correlation IDs, journal numbers, workflow decisions, and signed reconciliation evidence.

### Test identities and setup

Create separate users; do not give one user every permission:

1. Cashier: `Finance.CashTills.Operate`, `Finance.CashBank.Transactions.Record`, deposit create/submit as required.
2. Till reviewer: `Finance.CashTills.Closures.Review`; a separate controlled user for `Finance.CashTills.Sessions.Reopen`.
3. Deposit reviewer: `Finance.Banking.Deposits.Approve`.
4. Deposit poster: `Finance.Workflow.PostAfterApproval` only for posting.
5. Bank confirmer: `Finance.Banking.Deposits.Confirm`.
6. Returned-cheque preparer and a different returned-cheque reviewer, both with `Finance.Banking.ReturnedCheques.Manage`.
7. Reconciliation preparer: `Finance.BankReconciliation.Perform`.
8. Reconciliation approver: `Finance.BankReconciliation.Approve`.
9. FX operator: `Finance.FX.Revaluation.Run`; use a different user for FX-rate approval.
10. Read-only Finance user and a user in a second tenant for negative tests.

Configure an active default accounting book, open fiscal period, functional currency, approved FX rates, bank GL accounts, liquidity/till holding accounts, workflow routes, FX gain/loss accounts, and a test bank statement.

### A. Authorization and tenant isolation

1. Sign in as the read-only user. Verify create, submit, approve, post, confirm, adjustment, finalization and revaluation actions are not shown.
2. Call the same APIs directly and verify `403`, not a successful mutation.
3. Give the deposit poster only `Finance.Workflow.PostAfterApproval`. Verify they can post an approved deposit and do not need `Finance.Banking.Deposits.Submit`.
4. Give the returned-cheque reviewer `Finance.Banking.ReturnedCheques.Manage` without deposit approval. Verify approve/reject succeeds.
5. Attempt to read or mutate tenant A's bank, till, deposit, reconciliation and FX IDs using tenant B. Expect not-found/forbidden behavior and no audit/data change in either tenant.

### B. Bank account and liquidity controls

1. Create a bank account linked to a same-tenant posting GL account. Verify currency, active state, account identity and audit event.
2. Try a GL account from another tenant, a non-posting/control-ineligible account, and a duplicate bank identity. Expect rejection.
3. Create till and settlement liquidity mappings. Deactivate one with open operational use and verify the guard.
4. Compare bank-account operational balance with the exact-book GL balance. Run diagnostics/rebuild on deliberately altered non-production snapshot data; verify only the snapshot changes and no journal is created.

### C. Cashier-till custody lifecycle

1. Cashier A opens an assigned active till with an opening float. Verify a second active session on the same till is rejected.
2. Cashier B attempts to operate A's session. Expect denial.
3. Record receipts/payments against the session and verify expected cash changes from posted activity only.
4. Submit denomination counts whose total matches expected cash. A different reviewer closes the session.
5. Repeat with a non-zero variance and no reason; expect rejection. Supply a meaningful reason and verify review can proceed.
6. Test a stale row version with two browser sessions; one succeeds and the other receives a concurrency error.
7. Reopen the most recent closed session with an authorized separate user. Verify an immutable linked correction session is created and the original record is unchanged. Try reopening an older/non-latest session and expect rejection.

### D. Cash receipts, payments and transfers

1. Create, submit, independently approve and post one receipt and one payment. Confirm balanced journals, exact book/period, bank/liquidity legs, audit events and operational snapshots.
2. Repeat the post/request with the same idempotency key. Verify one journal and one cash transaction only.
3. Create a same-currency bank transfer. Verify linked OUT/IN legs, equal native/functional amounts, one governed posting event, and independent reconciliation eligibility.
4. Create a cross-currency transfer: preview first, confirm destination amount, use an approved rate, then post. Verify immutable source/destination native amounts, rate evidence and realized gain/loss.
5. Alter or withdraw the rate/preview after preview and before posting. Expect stale evidence rejection.
6. Reverse posted transactions through the controlled reversal action. Verify compensating entries and preserved original history; verify already reconciled or otherwise unsafe items are blocked as designed.

### E. Deposit end-to-end

1. Create posted eligible receipt/payment sources in a till/holding account.
2. Build deposit A with partial allocations. Start deposit B with the same amounts and verify reservations prevent double banking.
3. Attach the deposit slip and finance dimensions. Submit and verify evidence becomes immutable.
4. Attempt maker self-approval and expect the maker-checker control to reject it. Approve with the deposit reviewer.
5. Sign in as the dedicated poster and post. Verify one net bank cash transaction and one balanced posting-engine journal; repeat the request and verify idempotency.
6. Record bank confirmation with bank reference/date and optional evidence. Verify no second journal is created. Reuse the bank reference and expect duplicate rejection.
7. Verify the posted deposit appears in reconciliation exactly once, with confirmation and reconciliation status visible.
8. Exercise return, correction, rejection and cancellation branches and verify reservation release/freeze behavior.

### F. Returned cheque

1. Use a posted cheque receipt that is included in a posted deposit. Create a return case with bank advice, reference, date, charge treatment and dimensions.
2. Verify a non-cheque, undeposited, already-returned or wrong-bank source is rejected.
3. Submit, then approve as a different user. Verify the original deposit remains immutable, the bank debit posts once, the customer balance reopens, and charge allocation follows CustomerRecoverable/Expense/Split selection.
4. Repeat the approval call and verify no duplicate journal or AR reopening.
5. Test rejection/correction and confirm the audit trail retains each decision and evidence link.

### G. Statement import and reconciliation

1. Import valid CSV and XLSX statements. Test malformed dates, both debit and credit populated, duplicate lines, incorrect account and incorrect currency; expect clear rejection.
2. Start reconciliation and verify book balance is derived from posted GL through the statement date.
3. Auto-match exact references/amounts/directions within the configured date tolerance.
4. Manually match an amount/direction-compatible pair outside the date tolerance and retain the reviewed exception. Verify opposite debit/credit direction and unequal amounts cannot match.
5. Remove a match while open. Post a genuine bank fee/interest adjustment and verify a real cash transaction plus balanced journal is created exactly once.
6. Finalize with a non-zero difference and expect rejection. Bring the difference to zero and finalize.
7. Attempt preparer self-approval and expect workflow/SoD rejection. Approve as the reconciliation approver and verify the reconciliation is locked against further edits.

### H. Foreign-currency revaluation

1. Create foreign AP, AR and bank/cash monetary balances in an active exact book and open period, with approved historical and closing rates.
2. Preview revaluation. Recalculate expected functional carrying value and gain/loss independently; agree every line to the preview.
3. Change an exposure or approved closing rate after preview. Run with the old fingerprint and expect stale-preview rejection.
4. Run with current evidence. Verify one balanced journal, correct gain/loss accounts, exact `AccountingBookId`, rate IDs/snapshots, batch fingerprint, audit event and exposure links.
5. Retry the same command/idempotency key and verify no duplicate batch/journal.
6. Verify functional-currency balances, excluded/ineligible accounts, closed periods and unapproved/missing rates do not post.
7. Reverse the batch in an allowed period. Verify a linked compensating journal, original immutability and prevention of double reversal.

### I. Controlled documents and operational reporting

1. Issue one original payment slip and one original customer receipt from posted canonical sources. Verify PDF, source/journal identity, hash, private retained storage and audit event.
2. Request a second original and expect rejection. Request a replacement without a sufficiently detailed reason and expect rejection; then issue a watermarked numbered replacement with reason.
3. Compare the cash-position screen by account/currency with exact-book GL and bank snapshots as of the displayed timestamp.
4. Export/retain reconciliation, deposit, till, cash-position and FX evidence required by the accountant sign-off pack.

### J. Concurrency and failure recovery

For till open/close, deposit approve/post, confirmation, returned-cheque approval, reconciliation finalization/approval and FX run, send two near-simultaneous requests. Verify one durable outcome, no duplicate journal/transaction, a deterministic conflict or duplicate response, and an audit trail that explains the result. Interrupt a request after central posting but before the client receives its response; retry and verify the operational record heals/links to the existing idempotent journal.

## Required final go-live evidence

Do not promote until all of the following are attached to the release decision:

- migration status from the exact production candidate build;
- representative-tenant dry run and rollback rehearsal;
- signed bank-account/GL, till, deposit, reconciliation, AP/AR, FX and opening-balance tie-outs;
- approved entitlement/SoD matrix, including an explicit decision on `FIN-LIM-0014`;
- closure or formally approved quarantine of the broad backend and frontend global gate failures;
- accountant and security sign-off under `FIN-LIM-0017`;
- backups, monitoring/alerting, audit-log retention, private evidence-storage access and restore proof.
