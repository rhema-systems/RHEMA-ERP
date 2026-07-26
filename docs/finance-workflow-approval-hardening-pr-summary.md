# Finance Workflow Approval Hardening and High-Risk Action Routing - PR Summary

## Scope

Implemented workflow approval hardening only.

No migration/sign-off execution, opening-balance posting, reversal redesign, frontend UI, or final go-live scenario pack was implemented.

## Files Changed

- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetsController.cs`
- `src/ErpSystem.Api/Services/DatabaseSeedingService.cs`
- `src/ErpSystem.Api/Services/SimpleWorkflowService.cs`
- `src/ErpSystem.Api/Services/Finance/MultiCurrency/ExchangeRateService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetDepreciationService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/AssetValuationService.cs`
- `src/ErpSystem.Core/Enums/FixedAssetEnums.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFixedAssetService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFixedAssetDepreciationService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FxFunctionalCurrencyGovernanceTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetCapitalizationFoundationTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDepreciationFoundationTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetRevaluationImpairmentFoundationTests.cs`
- `docs/finance-workflow-approval-hardening.md`
- `docs/finance-workflow-approval-hardening-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Workflow Inventory Summary

- Exchange-rate create/update: hardened to workflow-controlled.
- Fixed asset direct capitalization: hardened to workflow-controlled before posting.
- Fixed asset depreciation run posting: hardened to workflow-controlled before posting.
- Fixed asset revaluation/impairment posting: hardened to workflow-controlled before posting.
- Fixed asset transfer/disposal: already workflow-controlled and left unchanged.
- Manual journals, AP/AR, cash/bank, and reconciliation workflows: inspected and left on existing lifecycle/workflow controls.
- Opening-balance/migration posting: still deferred under `FIN-LIM-0006`.

## Workflow Routes Added Or Hardened

- Added Finance approval queue facts and outcome application for `ExchangeRate`, `FixedAsset`, `FixedAssetDepreciationRun`, and `AssetValuation`.
- Added workflow context data for the same entity types in `SimpleWorkflowService`.
- Added workflow seed definitions for `ExchangeRate` and `FixedAssetDepreciationRun`.
- Forced exchange-rate create/update/bulk upload to `Pending` when workflow routing is configured.
- Blocked use of unapproved or rejected exchange rates through existing approved-rate resolution.
- Added direct fixed asset capitalization submission and approval-gated posting.
- Added depreciation run calculate-then-approve-then-post flow.
- Added valuation/revaluation/impairment approval-gated posting.

## Permission And SoD Verification

The Finance approval endpoint now rejects:

- approvals not assigned to the current approver/role
- high-risk submitter self-approval for ExchangeRate, FixedAsset, FixedAssetDepreciationRun, AssetDepreciationSchedule, and AssetValuation
- non-Finance workflow entity approvals

Tenant-scoped approval queries and tenant-scoped outcome loading prevent cross-tenant approval application.

## Posting-Engine Boundary Verification

Approval outcomes do not create journals directly.

Posting remains centralized through:

- `IFinancePostingEngine` for fixed asset capitalization
- `IFinancePostingEngine` for depreciation run posting
- `IFinancePostingEngine` for revaluation/impairment posting
- approved exchange-rate resolution for existing posting/revaluation services

## Audit Events

Added or verified:

- `Finance.Workflow.Submitted`
- `Finance.Workflow.Approved`
- `Finance.Workflow.Rejected`
- `Finance.Workflow.ApprovalFailed`
- `Finance.Workflow.PostingBlockedPendingApproval`
- `Finance.Workflow.PostingBlockedAfterRejection`
- `Finance.Workflow.OutcomeApplied`
- `Finance.Workflow.OutcomeDuplicateRejected`
- `Finance.Workflow.CrossTenantApprovalRejected`
- `Finance.Workflow.ApproverPermissionRejected`

## Migrations

No migration required.

The batch uses existing workflow tables and existing fixed asset status storage. Added enum values are code-level statuses over the existing column.

## Tests

Added:

- `FxFunctionalCurrencyGovernanceTests.ExchangeRateRequiresWorkflowApprovalBeforeUse`
- `FixedAssetCapitalizationFoundationTests.DirectCapitalizationRequiresWorkflowApprovalBeforePosting`
- `FixedAssetDepreciationFoundationTests.DepreciationRunRequiresWorkflowApprovalBeforePosting`
- `FixedAssetRevaluationImpairmentFoundationTests.ValuationRequiresWorkflowApprovalBeforePosting`

Scenario mapping:

- exchange-rate cannot be used before workflow approval: `ExchangeRateRequiresWorkflowApprovalBeforeUse`
- approved exchange-rate can be used: `ExchangeRateRequiresWorkflowApprovalBeforeUse`
- direct capitalization cannot post before approval: `DirectCapitalizationRequiresWorkflowApprovalBeforePosting`
- approved direct capitalization posts through posting engine: `DirectCapitalizationRequiresWorkflowApprovalBeforePosting`
- depreciation run cannot post before approval: `DepreciationRunRequiresWorkflowApprovalBeforePosting`
- approved depreciation run posts through posting engine: `DepreciationRunRequiresWorkflowApprovalBeforePosting`
- valuation cannot post before approval: `ValuationRequiresWorkflowApprovalBeforePosting`
- approved valuation posts through posting engine: `ValuationRequiresWorkflowApprovalBeforePosting`

Results:

- API build: passed
- focused workflow approval hardening tests: `4/4`

## Limitations Register

Resolved:

- `FIN-LIM-0020`
- `FIN-LIM-0029`
- `FIN-LIM-0032`
- `FIN-LIM-0036`

Still open:

- `FIN-LIM-0006`: controlled opening-balance/migration posting
- `FIN-LIM-0014`: tenant-aware role-assignment hardening
- reversal/correction limitations intentionally outside this batch

## Accounting Impact

No new accounting method was introduced. The batch changes when high-risk accounting actions are allowed to post, not the debit/credit logic after approval.

Unapproved or rejected high-risk actions now fail before they can affect posted GL. Approved actions still use the central posting engine and preserve posted GL as the accounting source of truth.

## Rollback Considerations

Rollback is code and seed-definition only. No data migration rollback is needed.

Rollback would re-open governance gaps for exchange rates, direct capitalization, depreciation runs, and valuation posting, so it should not be used after production workflow configuration begins.

## Safe To Proceed

Safe to proceed to controlled opening-balance/migration and sign-off preparation from a workflow-routing perspective.

`FIN-LIM-0006` remains the main blocker before migration execution and final accounting sign-off.
