---
integration_cycle: FIN-UAT-2026-10-05-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/finance-uat-consolidated-20261005
candidate_head: pending-final-evidence-commit
base_commit: e2831b83104a09413324e39e79e25d5c60036d53
target_ref: origin/master
depends_on: none
migration_status: three-migrations-mixed-application-state
verification_status: passed-with-documented-legacy-fixture-failures
integration_commit: pending-final-evidence-commit
pull_request: pending
---

# Consolidated Finance UAT follow-up cycle FIN-UAT-2026-10-05-A

## Objective and scope

Integrate the Finance gaps implemented after merged PR #356 into one reviewable PR against the latest `origin/master`, without sweeping user-owned HR edits, stat-only worktree noise, historical branches, or deferred product decisions into scope.

Included workstreams:

1. Bank-deposit acknowledgement before posting.
2. Bank reconciliation direction, authoritative-book balance, rematch, adjustment posting, maker/checker, workbench return-for-correction, and printable evidence.
3. Budget scenario hybrid segment/dimension scope, unused-Draft edit/delete, migration correction, and searchable return assignee.

Explicitly excluded:

- Reopen/Unreconcile of an approved bank reconciliation.
- Reclassification of `DEFAULT-6600 Bank Charges` as directly postable.
- Changes to the conservative bank auto-match score threshold.
- The documented legacy bank-reconciliation source-book-authority fixture repair.
- Unrelated dirty HR/frontend files in the source worktree.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Source worktree: `.w/fin-uat-remediation-20261004`
- Source branch/head: `codex/finance-uat-remediation-20261004` at `ca7e4e46c`
- Clean integration worktree: `.w/finance-uat-consolidated-20261005`
- Integration branch: `codex/finance-uat-consolidated-20261005`
- Exact refreshed base: `e2831b83104a09413324e39e79e25d5c60036d53` (`origin/master` fetched 2026-10-06)
- Current integration head before the final evidence commit: `2dca3ac96`
- Source commits integrated in order: `95ad224a5`, `e1c25df65`, `dcec41cdb`, `2af8a9bdb`, `06c8a07da`, `5389fe6bc`, `90a2c4ad1`, `dba417a58`, `ae42e678c`, `dc14d36cd`, `ffb80426b`, `870a8305a`, `635f3594e`, `77f1a993b`, `d45532e3b`, `dfae8481b`, `09b9a6307`, `ca7e4e46c`.
- Cherry-pick result: clean; no conflict resolution was required.

## Implementation record

- The three source ledgers are tagged for this cycle and remain the detailed design/verification authority.
- The documented candidate-inventory script `scripts/finance/Get-FinanceUatConsolidationCandidates.ps1` is absent from both the candidate branch and latest `origin/master`; equivalent inventory was performed with `git cherry`, merge-base, worktree/status, and patch-range checks.
- Independent high-risk review initially identified three blockers: non-atomic direct approval, acceptance of an auto-completed or step-less workflow, and a reconciliation query cache key that omitted the requested reconciliation ID.
- Commit `2dca3ac96` wraps direct approval and workflow progression in the provider execution strategy and one transaction, requires an actionable independent approval step before finalization, and includes the requested reconciliation ID in the client query key.
- Independent re-review confirmed those three blockers were resolved and identified one additional P1: Approval Workbench approval did not revalidate the current authoritative GL balance. The final correction adds one shared domain validator used by both approval surfaces, invokes it inside the workbench's serializable workflow/outcome transaction before consuming the task, and returns a controlled failed result while preserving `Completed`/pending states when the balance has drifted.

## Migration record

- `20261005211546_ReorderBankDepositAcknowledgementBeforePosting`: authored and already applied to `RHEMAERP_BOOKV2_UAT_20260922` under prior explicit authorization.
- `20261005225306_AddBudgetScenarioSegmentControls`: authored, corrected, and unapplied.
- `20261006010000_AllowBankReconciliationRematchAfterUnmatch`: authored and unapplied.
- This consolidation does not apply or reapply any migration and does not mutate any database.

## Verification evidence

- Candidate reconciliation/workbench backend regressions: 2/2 passed.
- Candidate Approval Workbench Vitest: 5/5 passed.
- Candidate changed-file ESLint: passed.
- Latest-master Release API build: passed with 0 errors (688 existing warnings).
- Combined focused frontend Vitest: 3 files, 16 tests passed.
- Changed-file frontend ESLint: passed.
- Exact changed/new backend regression gate: 10/10 passed, covering active-only rematch uniqueness, authoritative primary-book balance, finalizer maker/checker, fail-closed actionable workflow, approval-time post-finalization balance drift, pending-workflow preservation in the workbench, workbench return-for-correction, SQL retry-safe posting transaction, and SQL Server-compatible budget scope migration.
- Broad bank-reconciliation/backend hardening selection: 42 passed and 12 failed. All 12 failures terminate in the documented legacy test setup with `SOURCE_BOOK_AUTHORITY_MISSING` before their intended assertions; no production source-book governance was weakened to make these fixtures pass.
- `git diff --check`: passed; line-ending conversion warnings only.

## Known failures and risks

- The existing `ValidSameTenantReconciliation_ShouldFinalizeAndCreateAuditEvent` fixture fails before reconciliation assertions with `SOURCE_BOOK_AUTHORITY_MISSING`; the new focused reconciliation regressions pass independently and do not weaken source-book governance.
- Repository-wide frontend type-check has documented unrelated baseline failures; changed-file lint and focused tests are the acceptance gate for this cycle.

## Remaining work

- Obtain final independent approval of the balance-revalidation correction.
- Commit this final evidence and correction, push the integration branch, and create one PR to `master`.

## Authorization boundaries

- Authorized: fetch latest master, integrate the recorded Finance changes, commit, push the integration branch, and create one unified PR.
- Not authorized: merge the PR, deploy, restart services, apply migrations, mutate databases, delete branches/worktrees, or add deferred/unrelated changes.

## Integration outcome

Implementation and verification are complete, subject to final independent approval. Push and PR creation remain pending.
