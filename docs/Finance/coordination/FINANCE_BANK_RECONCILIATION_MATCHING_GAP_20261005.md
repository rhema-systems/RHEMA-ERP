# Finance bank-reconciliation matching gap - 2026-10-05

## Objective and scope

Diagnose why the 2026-10-05 ABC Bank reconciliation could neither auto-match nor manually match the posted deposit `DEP-202610-0001` against imported statement line `LQE-202610-00002`, and determine whether the displayed GHS 5,760 book balance is authoritative.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Investigation starting commit: `283a8d35811eab0e16d7779e78db05d5ae91915e`
- Pull request: no PR creation, update, or push is authorized for this workstream yet.

## Evidence and classification

- Uploaded workbook: `C:/Users/Akwas/Downloads/bank-statement-import-template (1).xlsx`.
- `Statement Lines!A2:F2` correctly records a 2026-10-05 GHS 2,880 deposit in the Credit column; the template defines Credit as money entering the bank account.
- The posted cash transaction has `CashTransactionType.Deposit`, GHS 2,880, reference `SLIP001`, and is linked to a posted journal.
- The server matching engine correctly treats `Deposit` as compatible with a statement Credit for both manual and automatic matching.
- The frontend `isDirectionCompatible` helper handles Receipt, Payment, and Transfer but omits Deposit and ReturnedCheque. It therefore disables manual matching for a valid deposit before calling the server.
- Classification: confirmed frontend contract drift / manual-match blocker.
- Auto-match assigns amount 40 points and same-date 30 points. Because `SLIP001` does not match `LQE-202610-00002` and the descriptions do not overlap, the pair scores 70, below the deliberate 80-point confidence threshold. Classification: expected conservative auto-match behavior, not a direction defect.
- Read-only UAT evidence shows the GHS 5,760 book balance double-counts the same `BankDepositBatch` across the default BASE book and its USD_PARALLEL replicated journal. Both rows retain GHS transaction-currency debit evidence of 2,880, and `CalculatePostedBookBalanceAsync` sums both books.
- Classification: confirmed multi-book reconciliation balance defect. The operational bank balance must be calculated once from the authoritative/default book, not once per replicated accounting book.
- Product-model decision: reconciliation remains anchored to the physical bank account and its operational cash transactions. The tenant's default primary accounting book is the authoritative GL context for the displayed book balance; parallel-book replicas must not be aggregated or reconciled independently against the same bank statement.
- Industry comparison: Microsoft Dynamics 365 Finance and Business Central reconcile statement lines to bank-account transactions / bank-account ledger entries within the legal-entity context, while Oracle recommends a unique GL cash account per bank account for book-to-bank reconciliation. This supports bank-account-first reconciliation with one authoritative ledger context, not an all-books balance.
- Follow-up adjustment UX: the reconciliation-adjustment offset account used a plain select over as many as 1,000 active accounts. It now uses the existing Command/Popover searchable-combobox pattern, searches account number and name, and preserves the existing posting-account eligibility filter and selected account ID payload.

## Changed files

- `frontend/src/app/finance/cash/reconciliation/page.tsx`
- `frontend/src/app/finance/cash/reconciliation/reconciliation-matching.ts`
- `frontend/src/app/finance/cash/reconciliation/reconciliation-matching.test.ts`
- `src/ErpSystem.Api/Services/Finance/Cash/BankReconciliationService.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankReconciliationPostingMigrationTests.cs`

## Migrations and application state

- No migration is expected for these fixes.
- No UAT data was changed during diagnosis.

## Verification evidence

- Workbook values inspected read-only.
- Client and server direction helpers compared directly.
- Manual-match server guard confirmed to accept Deposit-to-Credit.
- Auto-match scoring and its 80-point threshold confirmed from code and contract tests.
- UAT rows inspected read-only: one BASE GHS 2,880 debit and one USD_PARALLEL translated replica with GHS transaction debit 2,880 for the same source document; the current balance query sums both to GHS 5,760.
- Frontend direction contract: 6 tests passed.
- Targeted frontend ESLint: passed.
- Focused ESLint after converting the adjustment offset account to a searchable combobox: passed.
- Exact backend parallel-book regression `BookBalance_ShouldExcludeParallelBookReplicaOfSameBankMovement`: passed (1/1).
- Full `BankReconciliationPostingMigrationTests` class: 7 passed and 12 failed before reconciliation assertions because the legacy test fixture does not provide the now-required governed source-book authority (`SOURCE_BOOK_AUTHORITY_MISSING`). The new regression is independent of that fixture failure and passes in isolation.
- `git diff --check`: passed; Git reported only expected LF-to-CRLF working-copy warnings.

## Remaining work

- Repair the legacy bank-reconciliation test fixture's source-book-authority setup in its own atomic test-maintenance change; the production reconciliation fix does not bypass that governance requirement.
- Do not alter the conservative auto-match threshold without a separate policy decision; manual matching is the intended reviewed fallback for this pair.

## Authorization boundaries

- Diagnosis, implementation, tests, and a local commit are authorized.
- Do not push, create or update a PR, deploy, restart services, or mutate UAT data without separate authorization.
