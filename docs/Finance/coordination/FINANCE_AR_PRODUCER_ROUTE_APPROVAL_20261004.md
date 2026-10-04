---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: ready_for_review
integration_decision: include
candidate_branch: codex/finance-ar-producer-route-approval
candidate_head: 3baf1bca8
base_commit: 8a54cb47d3792c2cfbf10873356de91e009dcc2d
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: passed
integration_commit: pending
pull_request: pending
---

# Finance AR trusted producer route at final approval

## Objective and ownership

- Preserve the certified customer-invoice producer route when the Finance workflow's last approver releases the invoice.
- Finance owns the correction because the route is lost inside `FinanceApprovalsController`, at the Finance approval/posting boundary.
- Sales/Estate/Inventory/Fixed Assets continue to own their source records and route-specific accounting evidence; this work does not change those modules' derivation rules.

## Workspace and scope

- Branch: `codex/finance-ar-producer-route-approval`.
- Worktree: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.w/finance-ar-producer-route-approval`.
- Exact base: merged PR #343 commit `8a54cb47d3792c2cfbf10873356de91e009dcc2d`.
- Changed production surface: final AR approval dispatch only.
- No migrations, database mutation, deployment, force-push, merge or source-document repair are authorized.

## Implementation

- Final approval resolves the trusted document-level `CustomerInvoice` route from `FinanceSourceDimensionAssignments`.
- The resolver accepts only the four routes already allowed by canonical AR lifecycle services: manual Finance AR, fixed-asset disposal sale, inventory-disposal auction and Sales-order customer invoice.
- A certified route is passed back to `IInvoiceService.SendInvoiceAsync`; legacy invoices without certified route evidence retain the existing producer-less overload.
- Conflicting certified producer routes fail closed.

## Completed commits

- `3baf1bca8` - `fix(finance): preserve AR producer route on approval`.

## Verification evidence

- `dotnet restore tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj -p:TdcFastEfBuild=true`: passed.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -p:TdcFastEfBuild=true -p:UseSharedCompilation=false -m:1 --verbosity:minimal`: passed with 0 errors; the final build reported 43 pre-existing warnings.
- Test discovery confirmed six `FinanceApprovalInvoiceProducerRouteTests` cases: four canonical producers, the legacy no-route branch and conflicting-route fail-closed behavior.
- Focused final run covering `FinanceApprovalInvoiceProducerRouteTests` and `SalesOrderInvoiceGuardTests`: 22 passed, 0 failed, 0 skipped.
- `git diff --check`: passed; Git reported only expected LF-to-CRLF working-copy notices.

## Known failures

- None in the verified scope.

## Remaining work

- Push the branch and create a separate PR against `master`.
- After review, merge and deploy through the normal release path.
- Ask the reporter to retry final approval on the retained Sales-generated invoice after deployment; do not recreate or edit it merely to bypass the failure.
