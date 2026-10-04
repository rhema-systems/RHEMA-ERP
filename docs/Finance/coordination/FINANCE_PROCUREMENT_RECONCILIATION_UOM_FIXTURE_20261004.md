---
integration_cycle: FIN-UAT-2026-10-04-B
integration_status: pr_open
integration_decision: include
candidate_branch: codex/finance-procurement-uom-fixture-hotfix
candidate_head: e73ab5a87
base_commit: aa004703a
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: scoped_pass_with_known_baseline_failures
integration_commit: pending
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/346
---

# Finance procurement reconciliation UOM fixture correction

## Objective and ownership

- Restore the Finance integration gate after commercial UOM governance made active Inventory UOM evidence mandatory for procurement quantity lines.
- The production guard is operating as designed; this work updates only stale Finance reconciliation test data.
- Procurement remains the owner of production purchase-order and receipt quantity capture.

## Workspace and scope

- Branch: `codex/finance-procurement-uom-fixture-hotfix`.
- Worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.w/finance-procurement-uom-fixture-hotfix`.
- Exact base: merged PR #345 commit `aa004703a`.
- Changed surface: `ProcurementFinanceReconciliationTests` fixture data only.
- No production behavior, migration, database mutation, deployment, force-push or merge is authorized.

## Implementation

- Give the purchase-order and receipt lines stable `EA` UOM identity plus frozen precision snapshots, matching already-governed production records.
- Persist the matching active tenant-scoped `EA` master row in the scenario''s original save for later inspection-line resolution.
- Copy that evidence to the additional purchase-order line while preserving the fixture''s original single-save lifecycle.

## Verification plan

- Run the reported reconciliation test.
- Run the related inspection-line lookup test.
- Compare the full `ProcurementFinanceReconciliationTests` class with the unchanged baseline.
- Run `git diff --check` and compile the focused test surface.

## Completed commits

- `e73ab5a87` - `test(finance): seed governed UOM reconciliation evidence`.

## Verification evidence

- Test-project restore with `TdcFastEfBuild=true`: passed.
- Focused test surface compiled successfully.
- Reported `Reconciliation_ShouldComposeExistingCommitmentReceiptApGlRetentionAndMilestones`: passed, 1/1.
- Related `Reconciliation_ShouldNotCountAcceptancePostedAfterCutoff`: passed, 1/1, confirming the persisted active UOM supports later inspection-line resolution.
- Final full reconciliation-class snapshot: 15 passed, 4 failed. The unchanged pre-patch binary passed 1 and failed 18, with the UOM exception masking the broader baseline state.
- `git diff --check`: passed with only expected LF-to-CRLF working-copy notices.

## Known failures outside this hotfix

- `Reconciliation_ShouldKeepSettlementActiveBeforeLaterControlledReversal`: test-created reversal omits governed `AccountingBookId`.
- `Reconciliation_ShouldWeightPaymentPostingByAllEffectiveAllocations`: in-memory update targets an entity not present in the store.
- `Reconciliation_ShouldExcludeReversedAllocationOriginalAtCutoff`: same in-memory update/concurrency fixture defect.
- `Reconciliation_ShouldExplainUnbalancedAndUncontrolledReversalGaps`: expected reversal/retention issues are not emitted.
- These failures are distinct from UOM governance and were not expanded into this focused CI hotfix.

## Local current-master runtime verification

- Clean runtime worktree fast-forwarded to `origin/master` at `aa004703a`.
- Database initialization completed without the former `CashTransactions` migration exception.
- Port 5000 is reserved by Windows System, so the API was started on `http://localhost:5057`.
- Swagger responds with HTTP 200; aggregate `/health` responds with HTTP 503 because at least one configured dependency remains unhealthy.

## Remaining work

- Run the Finance integration gate and merge through normal review.
