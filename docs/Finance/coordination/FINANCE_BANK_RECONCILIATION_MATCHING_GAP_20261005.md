---
integration_cycle: FIN-UAT-2026-10-05-A
integration_status: in_progress
integration_decision: include
candidate_branch: codex/finance-uat-remediation-20261004
candidate_head: pending-final-commit
base_commit: 283a8d35811eab0e16d7779e78db05d5ae91915e
target_ref: origin/master
depends_on: bank-deposit-acknowledgement-lifecycle,budget-scenario-draft-lifecycle
migration_status: 20261006010000-authored-unapplied
verification_status: focused-passed-known-legacy-fixture-failure
integration_commit: pending
pull_request: pending
---

# Finance bank-reconciliation matching gap - 2026-10-05

## Objective and scope

Diagnose why the 2026-10-05 ABC Bank reconciliation could neither auto-match nor manually match the posted deposit `DEP-202610-0001` against imported statement line `LQE-202610-00002`, determine whether the displayed GHS 5,760 book balance is authoritative, and correct the subsequent rematch and adjustment-posting blockers found during UAT.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Investigation starting commit: `283a8d35811eab0e16d7779e78db05d5ae91915e`
- Matching and primary-book correction commit: `dcec41cdb` (`fix(finance): reconcile bank deposits across parallel books`)
- Searchable adjustment-account follow-up commit: `ffb80426b` (`fix(finance): make reconciliation offset account searchable`)
- Rematch and adjustment-posting correction commit: `635f3594e` (`fix(finance): restore reconciliation rematch and adjustments`)
- Cash-posting execution-strategy correction commit: `d45532e3b` (`fix(finance): execute cash posting in retry strategy`)
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
- Rematch evidence: reconciliation `7585ce3e-1843-4dbf-a53d-c967cec6ff28` retains soft-deleted match `bdbc26b7-94fe-4d68-9d3f-7165c0c0b520`; the statement line is reopened, but the unfiltered unique statement-line index rejects the replacement match. Classification: confirmed persistence-contract defect. Uniqueness must apply to active (`IsDeleted = 0`) matches so audit history and one-active-match governance coexist.
- Adjustment-action evidence: ABC Bank has a valid GL link in UAT, and the entered amount and replacement offset account were valid. The page loads `/finance/bank-accounts/active`, but `GetActiveAccountsAsync` omitted `GLAccountId` from its DTO projection. The hidden bank-GL prerequisite therefore arrived as undefined and silently disabled posting. Classification: confirmed API projection / UI contract defect, not user input error.
- Account `DEFAULT-6600` remains ineligible as an offset because it is configured `IsControlAccount = true` and `AllowDirectPosting = false`. Reclassifying that account is a separate configuration-governance decision; this workstream does not mutate UAT master data.
- Adjustment posting then exposed a separate SQL Server transaction-strategy defect: `CashTransactionService.PostAsync` opened its serializable user transaction directly even though production uses `SqlServerRetryingExecutionStrategy`. Classification: confirmed backend transaction-boundary defect. The entire cash posting unit now runs through `Database.CreateExecutionStrategy().ExecuteAsync(...)`, with the transaction created inside that retry scope.

## Changed files

- `frontend/src/app/finance/cash/reconciliation/page.tsx`
- `frontend/src/app/finance/cash/reconciliation/reconciliation-matching.ts`
- `frontend/src/app/finance/cash/reconciliation/reconciliation-matching.test.ts`
- `src/ErpSystem.Api/Services/Finance/Cash/BankReconciliationService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/BankAccountService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20261006010000_AllowBankReconciliationRematchAfterUnmatch.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankAccountTenantIsolationTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankReconciliationPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceConcurrencyHardeningTests.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `frontend/src/app/finance/approvals/page.test.tsx`
- `frontend/src/components/approvals/approval-workbench.tsx`
- `frontend/src/lib/finance/approval-queue-definitions.ts`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceApprovalAuthorityContractTests.cs`

## Migrations and application state

- Migration `20261006010000_AllowBankReconciliationRematchAfterUnmatch` changes the cash-transaction and bank-statement-line unique indexes to filtered unique indexes over active matches (`[IsDeleted] = 0`).
- The migration is included locally but has not been applied to any database.
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
- Focused ESLint after replacing the silent adjustment disable guard with explicit submit validation: passed.
- Exact rematch migration contract `RematchMigration_ShouldLimitUniquenessToActiveMatches`: passed (1/1).
- Combined focused regressions for the rematch migration and active-bank-account GL projection: passed (2/2).
- Cash posting execution-strategy architecture regression `CashPostingTransaction_ShouldStartInsideSqlServerExecutionStrategy`: passed (1/1); the build completed with baseline warnings and no errors.
- Exact backend parallel-book regression `BookBalance_ShouldExcludeParallelBookReplicaOfSameBankMovement`: passed (1/1).
- Full `BankReconciliationPostingMigrationTests` class: 7 passed and 12 failed before reconciliation assertions because the legacy test fixture does not provide the now-required governed source-book authority (`SOURCE_BOOK_AUTHORITY_MISSING`). The new regression is independent of that fixture failure and passes in isolation.
- `git diff --check`: passed; Git reported only expected LF-to-CRLF working-copy warnings.

## Remaining work

- Repair the legacy bank-reconciliation test fixture's source-book-authority setup in its own atomic test-maintenance change; the production reconciliation fix does not bypass that governance requirement.
- Decide separately whether `DEFAULT-6600 Bank Charges` should cease being a protected control account and allow direct posting; no seed or UAT configuration change is included here.
- Do not alter the conservative auto-match threshold without a separate policy decision; manual matching is the intended reviewed fallback for this pair.

## Authorization boundaries

- Diagnosis, implementation, tests, and a local commit are authorized.
- The user authorized consolidation onto latest `origin/master`, pushing the integration branch, and creating one unified PR.
- Do not merge, deploy, restart services, apply migrations, mutate UAT data, or remove branches/worktrees without separate authorization.
## Maker-checker and printable evidence follow-up (2026-10-06)

- Added server-side enforcement preventing the user recorded in `ReconciledBy` from approving the same reconciliation.
- Hid the approval action from that maker in the reconciliation workspace and display a different-authorized-user message.
- Added printable reconciliation evidence during preparation and after finalization, labelled `WORKING DRAFT — NOT APPROVED`, `FINALIZED — AWAITING APPROVAL`, or `APPROVED`, with balances, matches, and maker/checker timestamps.
- Added focused regression `Finalizer_ShouldNotApproveOwnReconciliation`.
- Finalization now creates the `BankReconciliation` approval workflow atomically with the Completed transition so the assigned checker receives a Finance Approval Workbench item.
- The workbench requires the banking approval permission, enforces submitter/finalizer separation, and routes review to the exact reconciliation.
- `Return for correction` requires a reason, preserves matches and audit evidence, clears finalization/approval actors, returns the reconciliation to `InProgress`, and requires a new finalization/workflow cycle.
- The workbench labels the action and confirmation consistently as `Return for correction` rather than terminal rejection.
- `Unreconcile` / reopening an already approved reconciliation is explicitly deferred as a separate high-risk correction-version workstream.
- Focused backend command covering `Finalizer_ShouldNotApproveOwnReconciliation` and `BankReconciliationWorkbench_UsesMakerCheckerAndReturnsForCorrection`: 2/2 passed.
- The same run including legacy `ValidSameTenantReconciliation_ShouldFinalizeAndCreateAuditEvent`: the two new tests passed; the legacy test failed before reconciliation assertions on the documented `SOURCE_BOOK_AUTHORITY_MISSING` fixture gap.
- Targeted Approval Workbench Vitest: 5/5 passed.
- Targeted ESLint for the changed reconciliation/workbench files: passed.
- Intended-file `git diff --check`: passed; unrelated user-owned HR files elsewhere in the worktree retain pre-existing trailing whitespace and remain excluded.
