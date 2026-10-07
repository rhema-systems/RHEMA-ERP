---
integration_cycle: FIN-UAT-2026-10-07-A
integration_status: pr_open
integration_decision: include
candidate_branch: codex/fin-uat-bank-recon-fx-override-20261007-v2
candidate_head: 905eed2c3
base_commit: 28c067ae2fbe847cc4b1c24a9fa7781a0c420ed7
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: passed_with_documented_baseline_drift
integration_commit: 8372d2847
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/364
---

# Bank Reconciliation UAT Hardening Ledger

## Objective

Close the bank-reconciliation UAT gaps confirmed during the deployed walkthrough: reviewer read authorization, return-for-correction lifecycle, direct Approval Inbox review routing, client-facing authorization feedback, and an independently runnable standard bank-reconciliation report.

## Scope

- Correct `GetMatches` so read-only reviewers require `Finance.Read`, not `Finance.BankReconciliation.Perform`.
- Return a completed reconciliation for correction with a mandatory reason, preserved matches/history, `InProgress` status, and fresh finalization/approval on resubmission.
- Deep-link Approval Inbox review directly to the identified reconciliation.
- Replace raw HTTP 403 presentation on the reconciliation and Finance report surfaces with actionable permission feedback.
- Add a printable, independently runnable bank-reconciliation report under Finance Reports.
- Preserve all unrelated existing working-tree changes.

## Repository state

- Integration worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\fin-uat-bank-recon-fx-20261007-v2`
- Branch: `codex/fin-uat-bank-recon-fx-override-20261007-v2`
- Exact base: `28c067ae2fbe847cc4b1c24a9fa7781a0c420ed7`
- The primary checkout and the earlier failed candidate worktree are preserved and were not swept into this branch.

## Confirmed gaps

- `FinancePermissionPolicyMap.BankReconciliationPolicy` classifies `GetMatches` as a mutation because its action name contains `Match`.
- The Finance Approval Inbox generic reject path marks bank reconciliation `Rejected` instead of reopening it for correction.
- Bank-reconciliation detail has no return-for-correction action.
- Approval Inbox falls back to `/finance/approvals` for bank reconciliation rather than opening the identified record.
- Reconciliation workspace and report errors can expose raw `HTTP 403` text.
- The current workspace print output is not a standard independently runnable bank-reconciliation report.

## Completed work

- [x] Inspected the deployed UAT screenshots and reproduced the authorization-policy cause in source.
- [x] Traced the generic Finance approval rejection and bank-reconciliation outcome branches.
- [x] Confirmed workflow rejection currently preserves workflow history but maps the business entity to terminal `Rejected`.
- [x] Corrected `GetMatches` to require `Finance.Read` while retaining `Finance.BankReconciliation.Perform` for matching mutations.
- [x] Added a mandatory-reason return-for-correction endpoint and UI action that reopens the same reconciliation as `InProgress`, clears prior approval fields, preserves matches, appends notes, retains workflow history, and emits a dedicated audit event.
- [x] Updated the generic Finance approval outcome so bank-reconciliation rejection means return for correction rather than terminal rejection.
- [x] Added bank-reconciliation-specific “Return for correction” wording to the Approval Workbench without changing rejection wording for other document types.
- [x] Deep-linked Approval Workbench review to the exact reconciliation and made the workspace load that reconciliation without showing the bank-account picker first.
- [x] Replaced empty raw HTTP 403 fallbacks with actionable client-facing permission feedback.
- [x] Added an independently runnable printable Bank Reconciliation report under Finance Reports and Cash Management Reports.
- [x] Added focused authorization, route, and lifecycle tests.
- [x] Ran targeted backend/frontend verification.

## Changed files

- `docs/Finance/coordination/BANK_RECONCILIATION_UAT_HARDENING_LEDGER.md`
- `frontend/src/app/finance/cash/reconciliation/page.tsx`
- `frontend/src/app/finance/cash/reports/page.tsx`
- `frontend/src/app/finance/reports/bank-reconciliation/page.tsx`
- `frontend/src/app/finance/reports/page.tsx`
- `frontend/src/components/approvals/approval-workbench.tsx`
- `frontend/src/lib/finance/approval-queue-definitions.ts`
- `frontend/src/services/api.service.ts`
- `frontend/src/services/finance/cash-management-data.service.ts`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Api/Controllers/Finance/BankReconciliationController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/BankReconciliationService.cs`
- `src/ErpSystem.Core/DTOs/Finance/BankReconciliationDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ICashManagementServices.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankReconciliationPostingMigrationTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/CashBankPermissionContractTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceRouteContractTests.cs`

## Commits

- `dfb38fae0` — combined scoped implementation commit for bank-reconciliation hardening and transaction-specific FX overrides.
- `8372d2847` — integration commit resolved against current `origin/master` while retaining current source-book authority controls.

## Migrations and application status

No migration is currently planned; return reasons can be retained in workflow history, Finance audit events, and reconciliation notes. Nothing has been applied or deployed.

## Known failures / constraints

- The local Windows sandbox helper rejects ordinary repository reads; reviewed read-only elevated commands are required.
- The live deployed database and local `RHEMAERP_BOOKV2_UAT_20260922` database are distinct evidence sources. No deployed mutation is authorized in this task.
- Repository-wide `npm run type-check` remains red because of pre-existing TypeScript errors in unrelated frontend files. None of the reported diagnostics referenced a file changed by this workstream.

## Verification evidence

- Integrated API build — **passed with 0 errors and 45 existing warnings**.
- Bank-reconciliation focused backend coverage — **25/25 passed** across the suite rerun and isolated auto-match regression test.
- Related Finance permission and route tests are included in the additional focused run — **33/33 passed** together with FX override tests.
- Approval Workbench Vitest — **5/5 passed**.
- Targeted ESLint — **passed** for the changed reconciliation/report/workbench files.
- `git diff --cached --check` — **passed** before the integration commit.

## Remaining work

Create and review the combined PR. Deploy and execute the live UAT path only when separately authorized.

## Authorization boundaries

Authorized: edit repository source/tests/docs for the requested fixes and run local verification.

Authorized on 2026-10-07: create scoped commits, push the clean integration branch, and create the combined pull request.

Not authorized: deploy, modify the deployed database, apply migrations, merge the pull request, remove worktrees/branches, or remove temporary deployed permissions.

## Related workstream — transaction-specific exchange-rate overrides

The privileged transaction-only exchange-rate override was implemented under its own ledger and integrated into the same authorized consolidation cycle and pull request.
