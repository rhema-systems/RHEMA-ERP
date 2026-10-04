---
integration_cycle: none
integration_status: pr_open
integration_decision: include
candidate_branch: codex/finance-invoice-approval-serializable
candidate_head: 7d038b33a08b5843396b13689594c44c6fe7a350
base_commit: 9350973b375c3ece2c2a2c77513639abbea6178a
target_ref: master
depends_on: none
migration_status: none
verification_status: passed
integration_commit: pending
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/349
---

# Finance invoice approval serializable-transaction hotfix

## Objective and scope

- Fix final customer-invoice approval failing with `AR_INVOICE_POSTING_BLOCKED` and reference `SOURCE_BOOK_AUTHORITY_SERIALIZABLE_REQUIRED`.
- Keep workflow completion, invoice posting, and source-book authority transition atomic inside the existing execution strategy.
- Apply the same transaction contract to vendor-invoice final approval because it reaches the same governed source-book authority boundary.

## Workspace

- Branch: `codex/finance-invoice-approval-serializable`.
- Worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.codex-worktrees/finance-invoice-approval-serializable`.
- Exact base: `9350973b375c3ece2c2a2c77513639abbea6178a` (`master`).

## Implementation

- `FinanceApprovalsController` selects `Serializable` isolation for `ExchangeRate`, `Invoice`, and `VendorInvoice` approval outcomes.
- A focused regression theory makes the isolation policy explicit and confirms unrelated approval outcomes remain `ReadCommitted`.

## Changed files

- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceConcurrencyHardeningTests.cs`
- `docs/Finance/coordination/FINANCE_INVOICE_APPROVAL_SERIALIZABLE_HOTFIX_20261004.md`

## Completed commits

- `7d038b33a08b5843396b13689594c44c6fe7a350` - `fix(finance): serialize invoice approval outcomes` (production code and regression tests).
- `27f17e71573feee13a2c63a765c8ec59532b03b8` - `docs(finance): record invoice approval hotfix` (coordination and verification record).

## Verification

- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -p:TdcFastEfBuild=true -p:UseSharedCompilation=false -m:1 --filter "FullyQualifiedName~ApprovalOutcomeTransaction_ShouldUseSerializableIsolationWhenOutcomeTransitionsBookAuthority|FullyQualifiedName~InvoiceApprovalPostingFailure_ShouldPreserveSafeValidationMessage|FullyQualifiedName~FinanceApprovalInvoiceProducerRouteTests" --verbosity:minimal`: passed, 13 tests, 0 failed, 0 skipped.
- The targeted run compiled the API and test projects successfully; reported warnings pre-date this hotfix.
- `git diff --check`: passed before the casing correction and will be rerun before commit.

## Known failures

- An initial overlapping test invocation left `ErpSystem.Api.Tests.dll` locked by a stale test host; the clean rerun completed normally after the process released the file.
- The broader `FinanceConcurrencyHardeningTests` class has five unrelated clean-`master` failures in cash/bank source inspection, FX form inspection, migration discovery, and year-end source inspection. The four first-draft hotfix failures exposed and led to correction of an uppercase normalization mismatch; the corrected targeted test passes.

## Remaining work

- Review and integrate the local hotfix commit through the normal release path.
- After deployment, ask the reporter to retry the retained pending customer invoice; do not recreate or edit it to bypass the failure.
- Review, push, PR creation, merge, deployment, database mutation, and UAT record repair remain unauthorized.

## Authorization boundaries

- Authorized: local diagnosis, implementation, tests, ledger maintenance, and local commit.
- Not authorized: push, PR creation, merge, deployment, migration application, database mutation, or editing/recreating the reporter's invoice.
