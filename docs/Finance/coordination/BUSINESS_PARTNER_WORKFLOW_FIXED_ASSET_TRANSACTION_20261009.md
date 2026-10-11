---
integration_cycle: FIN-UAT-2026-10-10-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/business-partner-workflow-fixed-asset-transaction
candidate_head: da2f39e4fc42c4e6db272bc003d3026db8deda83
base_commit: dd6fb224a1eaa7512087ed3bf0dd767d490c0f12
target_ref: origin/master
depends_on: none
migration_status: 20261009143000 locally applied; remote unapplied
verification_status: focused checks passed
integration_commit: 74bb13b6f
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/398
---

# Business Partner Workflow and Fixed Asset Transaction — 2026-10-09

## Objective and scope

- Prevent the legacy `BUSINESS_PARTNER` workflow alias from shadowing the canonical `BusinessPartner` workflow configuration and silently activating a submitted partner.
- Consolidate retained Business Partner workflow definitions and instances onto the canonical entity type, then retire the duplicate safely.
- Run fixed-asset capitalization submission transactions inside the configured EF Core execution strategy.
- Add focused regressions for both UAT failures.

## Branch and worktree

- Branch: `codex/business-partner-workflow-fixed-asset-transaction`
- Worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\bp-workflow-fixed-asset-transaction`
- Exact base: `origin/master` at `dd6fb224a1eaa7512087ed3bf0dd767d490c0f12`

## Changes

- Implementation commit: `14f1bb5c6` (`Fix business partner workflow and asset submission transaction`).
- `WorkflowEntityTypeRepository` now resolves an exact code before an exact display name.
- Added an unapplied data-repair migration that reassigns legacy definitions/instances and soft-retires the duplicate Business Partner entity type.
- Fixed-asset submission now owns its serializable transaction within `CreateExecutionStrategy().ExecuteAsync(...)`, with retry tracker cleanup.
- The submission audit is written before transaction commit so the governed state and audit evidence commit atomically.

Changed files:

- `src/ErpSystem.Data/Repositories/WorkflowEntityTypeRepository.cs`
- `src/ErpSystem.Data/Migrations/20261009143000_ConsolidateBusinessPartnerWorkflowEntityType.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `tests/ErpSystem.Api.Tests/Controllers/WorkflowEntitySummaryTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Procurement/BusinessPartnerLifecycleGovernanceTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetCapitalizationFoundationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`

## Migrations

- `20261009143000_ConsolidateBusinessPartnerWorkflowEntityType` — applied on 2026-10-10 to local `RHEMAERP_BOOKV2_UAT_20260922`; not applied to any remote database.
- Verified copy-only backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.EXPRESS22\MSSQL\Backup\RHEMAERP_BOOKV2_UAT_20260922_pre_BP_workflow_20261010.bak`.
- Post-application verification: one active/non-deleted `BusinessPartner` row, one retired/deleted legacy alias, and the canonical row owns 1 workflow definition and 4 retained workflow instances.

## Verification

- `dotnet restore tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj` — passed; emitted existing ImageSharp vulnerability advisories.
- Full dependency/test-project compilation — passed with repository baseline warnings and no errors.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --no-dependencies` — passed (45 warnings, 0 errors).
- Focused test run covering `WorkflowEntitySummaryTests`, `BusinessPartnerLifecycleGovernanceTests`, and `DirectCapitalizationSubmissionStartsTransactionInsideRetryingExecutionStrategy` — passed 6/6.
- `git diff --check` — passed; only configured LF-to-CRLF notices were emitted.

## Known failures and remaining work

- Apply the migration to remote environments only through the normal reviewed deployment process; it remains unapplied remotely.
- Deploy the corrected build and migration, then submit a newly created Business Partner and verify that an approval instance appears for the configured approver. Do not use `SUP260002` as maker-checker evidence because its earlier activation did not create valid workflow evidence.

## Authorization boundaries

- Authorized: local implementation, tests, local commits, and the 2026-10-10 migration application to local `RHEMAERP_BOOKV2_UAT_20260922`.
- Authorized on 2026-10-10: push and consolidated pull-request creation for the outstanding code and documentation changes.
- Not authorized: changing any remote database, merge, deployment, or changing remote runtime state.

## Integration outcome

- Integrated onto `codex/finance-uat-consolidated-20261010` from current `origin/master` without conflicts.
- Integration content head before outcome-ledger updates: `74bb13b6f`.
- Combined verification and pull-request details are recorded in `FIN_UAT_2026_10_10_A_CONSOLIDATION_LEDGER.md`.
