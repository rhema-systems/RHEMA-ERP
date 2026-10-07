---
integration_cycle: FIN-UAT-2026-10-07-A
integration_status: merged_deployment_triggered
integration_decision: include
candidate_branch: codex/fix-external-logout-finance-build
candidate_head: 0472d49c925
base_commit: f25faf38ee08fb2954746f689125e386eb014c39
target_ref: origin/master
depends_on: none
migration_status: generated_and_applied_to_named_local_uat_only
verification_status: hotfix_frontend_build_and_focused_tests_passed
integration_commit: 0875a5a3e30
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/364
hotfix_pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/368
hotfix_merge_commit: 2da59240dfe
deployment_workflow_run: https://github.com/rhema-systems/RHEMA-ERP/actions/runs/37620014990
---

# Finance UAT consolidation: bank reconciliation and transaction FX overrides

## Objective and scope

Create one clean pull request against the latest `origin/master` containing only:

1. bank-reconciliation approval, correction, permission-feedback, deep-link, and reporting hardening; and
2. privileged transaction-specific exchange-rate override controls, workflow evidence, posting enforcement, migration, UI integrations, and tests.

Unrelated primary-checkout changes, generated local artifacts, historical worktrees, and temporary patches are excluded.

## Source candidates

- Bank reconciliation ledger: `BANK_RECONCILIATION_UAT_HARDENING_LEDGER.md`
- FX override ledger: `TRANSACTION_EXCHANGE_RATE_OVERRIDE_LEDGER.md`
- Shared scoped source commit: `dfb38fae0`

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Integration worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\fin-uat-bank-recon-fx-20261007-v2`
- Integration branch: `codex/fin-uat-bank-recon-fx-override-20261007-v2`
- Exact target base: `28c067ae2fbe847cc4b1c24a9fa7781a0c420ed7`
- Integrated commits: `8372d2847` (implementation) and `905eed2c3` (initial ledgers), with this ledger update following before push.

## Migration record

- `20261006150254_AddFinanceTransactionExchangeRateOverrides`: generated in source.
- Applied only to the explicitly authorized local UAT database `RHEMA-AKWASI\EXPRESS22 / RHEMAERP_BOOKV2_UAT_20260922`.
- Not deployed by this consolidation task.

## Verification evidence

- API build: passed with 0 errors and 45 existing warnings.
- Bank-reconciliation focused backend coverage: 25/25 passed across the suite rerun and isolated auto-match regression test.
- Finance permission/route/FX override focused backend coverage: 33/33 passed.
- Approval Workbench Vitest: 5/5 passed.
- Targeted ESLint: passed for every changed frontend file except the AP payment detail page, whose only finding is the pre-existing `@typescript-eslint/no-non-null-assertion` at line 227 on `origin/master`; this PR does not change that line.
- `git diff --cached --check`: passed before integration commit.
- EF reports 12 pending operations, but a model-differ probe identified only pre-existing Security/Audit/Estate operations. No pending operation touches `FinanceExchangeRateOverrideRequests` or this PR's migration.

## Post-merge build regression and recovery

- PR #364 was merged as `0875a5a3e30` and was included in `master` at `f25faf38ee08fb2954746f689125e386eb014c39`.
- The Windows VPS workflow's Next.js production build then failed because `frontend/src/app/finance/cash/reconciliation/page.tsx` declared `requestedReconciliationId` and `requestedReconciliationQuery` twice.
- Recovery branch: `codex/fix-external-logout-finance-build`, based exactly on `f25faf38ee08fb2954746f689125e386eb014c39`.
- The duplicated declaration, query, and effect were removed; the retained query still loads a requested reconciliation and selects its bank account.
- The same recovery also fixes an external-portal logout race in which navigation reached `/login` before asynchronous token cleanup, allowing the login page to redirect the stale session to `/tenant-select`. Tenant selection now handles missing or rejected authentication as a direct login redirect and suppresses expected `/auth/me` 401 console noise.
- Focused tenant-selection regression coverage: 7/7 passed.
- Targeted ESLint: passed for all seven changed frontend files.
- Next.js 15.5.24 production build: passed, including compilation, page-data collection, static-page generation, and build-trace collection.
- `git diff --check`: passed.

## Remaining work

PR #368 passed the required release-contract check and was merged as `2da59240dfe`. Windows VPS workflow run `37620014990` was manually dispatched from `master` with release build and test-VPS deployment enabled. Per the user's instruction, deployment monitoring is deferred until the user reports that the run has completed.

## Authorization boundaries

The original cycle stopped at PR creation. The user subsequently authorized merging the combined work and deploying through the GitHub Actions Windows VPS workflow. This recovery remains limited to the failed frontend build and external-portal logout/session behavior. No database mutation, migration application, destructive cleanup, or unrelated-file collection is authorized.
