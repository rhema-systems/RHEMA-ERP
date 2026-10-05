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

## Changed files

- None. This turn was a read-only diagnosis.

## Migrations and application state

- No migration is expected for these fixes.
- No UAT data was changed during diagnosis.

## Verification evidence

- Workbook values inspected read-only.
- Client and server direction helpers compared directly.
- Manual-match server guard confirmed to accept Deposit-to-Credit.
- Auto-match scoring and its 80-point threshold confirmed from code and contract tests.
- UAT rows inspected read-only: one BASE GHS 2,880 debit and one USD_PARALLEL translated replica with GHS transaction debit 2,880 for the same source document; the current balance query sums both to GHS 5,760.

## Remaining work

- Add frontend contract tests covering Deposit-to-Credit and ReturnedCheque-to-Debit, then align the client helper with the server engine.
- Add backend contract coverage proving reconciliation book balance excludes parallel-book replicas.
- Change the posted book-balance query to use the tenant's authoritative/default accounting book (or equivalent canonical-book rule) while retaining transaction-currency evidence for foreign-currency bank accounts.
- Run focused frontend reconciliation tests and backend bank-reconciliation tests.
- Do not alter the conservative auto-match threshold without a separate policy decision; manual matching is the intended reviewed fallback for this pair.

## Authorization boundaries

- Diagnosis and local documentation are authorized.
- No implementation was requested in the diagnosing turn.
- Do not push, create or update a PR, deploy, restart services, or mutate UAT data without separate authorization.
