---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/fin-uat-journal-batch-20261004
candidate_head: 72665ddd6
base_commit: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
target_ref: origin/master
depends_on: none
migration_status: none_required
verification_status: focused_tests_passed
integration_commit: 641cfcf4d
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/352
---

# Journal Batch UAT corrections - 2026-10-04

## Objective and scope

- Correct the five Journal Batch gaps reported during Finance UAT: import-page crash, ineffective draft-journal editing, stale eligible-draft state after detach, failed reattachment, and missing maker-checker/workbench enforcement.
- Keep this workstream isolated for later consolidation with other Finance UAT fixes.
- No migration is required.

## Workspace

- Branch: `codex/fin-uat-journal-batch-20261004`.
- Worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.w/fin-uat-journal-batch-20261004`.
- Exact base: `origin/master` at `6b59a3e841c61f5c0e295f9cce2571fc0d664d8c`.

## Implementation

- Added a defensive Journal Batch money formatter so blank or malformed preview currency codes cannot crash the import route.
- Made Edit scroll to, populate and focus the draft-entry editor instead of scrolling past it to the page bottom.
- Refreshes eligible draft journals immediately after detaching an item from a batch.
- Reattachment restores only a pristine soft-deleted draft membership; reviewed, posted or posting-linked history is never revived.
- Added Journal Batch to the Finance Approval Workbench as a detail-page decision item for eligible approvers.
- Enforced maker-checker in both the workbench projection and `JournalBatchService`; creators/submitters cannot review their own batch.
- Blocked the generic Finance approval endpoint from bypassing Journal Batch per-entry decisions.

## Completed commits

- `72665ddd6` - `fix(finance): close journal batch UAT gaps`.

## Changed files

- `frontend/src/app/finance/journal-batches/[id]/page.tsx`
- `frontend/src/app/finance/journal-batches/[id]/page.test.tsx`
- `frontend/src/app/finance/journal-batches/import/page.tsx`
- `frontend/src/lib/finance/journal-batch-money.ts`
- `frontend/src/lib/finance/journal-batch-money.test.ts`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Services/Finance/GL/JournalBatchService.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceApprovalQueueProjectionTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/JournalBatchServiceTests.cs`

## Verification evidence

- Focused frontend Vitest run: 3 files passed, 18 tests passed.
- Targeted ESLint over all changed frontend files: passed with no findings.
- Focused backend test run covering `JournalBatchServiceTests` and `FinanceApprovalQueueProjectionTests`: 30 passed, 0 failed, 0 skipped.
- The post-hardening backend rerun used `--no-build --no-restore --blame-hang-timeout 2m` after an earlier test-host invocation stalled silently; the bounded rerun completed successfully in 49 seconds.
- Final `git diff --check`: passed; Git reported only expected LF-to-CRLF working-copy notices.

## Known failures

- None in the focused automated scope.

## Remaining work

- Include implementation commit `72665ddd6` in the later consolidated Finance UAT pull request.
- After integration/deployment, browser-UAT all five reported flows with distinct maker and approver users.

## Authorization boundaries

- Authorized: local implementation, focused tests, lint, and coordination documentation.
- Not authorized: push, pull request creation, merge, deployment, database migration/application, or production/UAT data repair.

## 2026-10-05 post-integration approval authorization audit

- UAT observed ACCESS_FORBIDDEN when accounts.officer submitted a complete journal-batch review, even though the batch appeared in that user's approval workbench and the detail page exposed the review controls.
- The intended contract is unambiguous: the first workflow stage includes Accounts Officer, ReviewStage requires Finance.JournalBatches.Approve, and the Accounts Officer baseline role includes that permission.
- A read-only check of the exact API database, `RHEMAERP_BOOKV2_UAT_20260922`, confirmed that Accounts Officer is granted `Finance.JournalBatches.Approve`, `Finance.Workflow.Approve`, and `Finance.Workflow.Reject`.
- This is not an intended role restriction and is a remediation candidate. The generic ACCESS_FORBIDDEN response can originate either in permission middleware or in the workflow-assignment guard, so the next UAT pass must capture the failing request, authenticated claims, and current workflow step to distinguish stale authorization context from workflow-state drift.
- No database or deployment mutation was performed.
