---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: ready
integration_decision: include
candidate_branch: codex/fin-uat-exchange-workflow-20261004
candidate_head: 5ddda43dda3deff0d57d136e7da2eb80c91199ca
base_commit: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: focused-test-passed
integration_commit: pending
pull_request: pending
---

# Finance UAT workstream: Exchange-rate approval workflow provisioning

## Objective and scope

Ensure every active Finance tenant receives a usable, published Exchange Rate approval workflow before exchange-rate creation attempts to start approval, without bypassing maker-checker governance.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\fin-uat-exchange-workflow-20261004`
- Branch: `codex/fin-uat-exchange-workflow-20261004`
- Exact base commit: `6b59a3e841c61f5c0e295f9cce2571fc0d664d8c`
- Candidate implementation commit: `5ddda43dda3deff0d57d136e7da2eb80c91199ca`
- Working tree status before this ledger commit: clean.

## Implementation record

- Confirmed the canonical workflow catalog already contains `ExchangeRate` and exchange-rate creation correctly requests that entity key.
- Identified an ordering failure mode: the full Finance catalogue was seeded in one broad guarded loop, so an earlier unrelated workflow failure could prevent the later ExchangeRate seed while startup logged and continued.
- Extracted common Finance workflow-spec provisioning and provisions the critical ExchangeRate workflow before the rest of the catalogue.
- Preserves the existing rule that deliberate tenant workflow configuration is not overwritten and does not introduce auto-approval.
- Added regression coverage for one active published definition, the three Finance approval stages, and idempotent rerun behavior.
- Implementation commit: `5ddda43dda3deff0d57d136e7da2eb80c91199ca` (`fix(finance): prioritize exchange rate workflow seed`).

## Migration record

None. Workflow configuration is application data. No database mutation was performed.

## Verification evidence

- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter FullyQualifiedName~EnsureFinanceWorkflowsSeededAsync_ShouldCreateMissingAndPreserveExistingPaymentDefinitions`: passed 1, failed 0, skipped 0.
- The regression asserts `ExchangeRate` is active, published, has Accounts Officer, Finance Manager, and Financial Controller approval stages, and remains a single definition after a second provisioning run.
- `git diff --check`: exit 0 before commit; line-ending conversion warnings only.

## Known failures and risks

- The existing UAT tenant still needs the explicit Finance baseline provisioning command after consolidated integration; that database mutation was not authorized or run.
- Browser UAT for create, approve, and reject remains part of consolidated-cycle verification.

## Remaining work

Integrate the candidate, explicitly provision the UAT environment under separate authorization, then verify the exchange-rate approval lifecycle in the browser.

## Authorization boundaries

The user authorized implementation and local commits. Push, PR creation, deployment, database mutation, worktree removal, and branch deletion remain unauthorized.

## Integration outcome

Pending consolidated integration.
