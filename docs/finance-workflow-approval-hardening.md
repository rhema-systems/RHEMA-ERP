# Finance Workflow Approval Hardening and High-Risk Action Routing

Date: 2026-07-09

## Scope

This batch hardens approval routing for high-risk Finance actions that were previously permission-controlled but not consistently routed through the configurable workflow engine.

Implemented scope:

- Exchange-rate creation/update approval routing.
- Fixed asset direct capitalization approval routing.
- Fixed asset depreciation run approval routing.
- Fixed asset revaluation/impairment approval routing.
- Finance approval queue/outcome support for the new workflow entity types.
- Finance audit events for workflow submission, approval, rejection, blocked posting, duplicate/invalid outcome, and approver permission failures.

Not included:

- Opening-balance/migration posting.
- AP/AR, cash/bank, or fixed asset reversal/correction redesign.
- Frontend workflow UI.
- Final go-live scenario pack.
- New parallel approval tables or approval services.

## Workflow Inventory

| Finance action | Classification after this batch | Notes |
|---|---|---|
| Manual journal approval/posting | Already workflow-controlled | Continues through `JournalEntryService` and Finance approval queue. |
| Fiscal period close/reopen/unlock | Permission, reason, audit controlled | No additional workflow route was added in this batch. |
| AP invoice/payment approval | Already workflow or lifecycle controlled | Existing AP services remain on their established approval paths. |
| AR invoice/receipt/credit-note approval | Already workflow or lifecycle controlled | Existing AR services remain on their established approval paths. |
| Cash/bank approval flows | Already workflow/lifecycle controlled | Existing cash/bank approval controls were left unchanged. |
| Exchange-rate create/update | Workflow-controlled | New or edited rates become `Pending`; only approved/auto-approved rates can be resolved for posting. |
| Tax rule/config changes | Permission and audit controlled | No tax workflow routing was added in this batch. |
| Fixed asset direct capitalization | Workflow-controlled | Direct capitalization must be submitted and approved before posting when workflow is configured. |
| Fixed asset depreciation run posting | Workflow-controlled | Runs can be calculated, then must be approved before posting when workflow is configured. |
| Fixed asset revaluation/impairment posting | Workflow-controlled | Valuations enter workflow and cannot post until approved when workflow is configured. |
| Fixed asset transfer/disposal approval | Already workflow-controlled | Existing asset transfer/disposal routing was left unchanged. |
| Migration/opening-balance posting | Deferred/not production scope | Remains tracked under `FIN-LIM-0006`. |

## Workflow Model

The implementation uses existing workflow infrastructure only:

- `IWorkflowService`
- `SimpleWorkflowService`
- workflow entity types
- workflow definitions, step instances, and approvals
- `FinanceApprovalsController`

No parallel Finance approval store or alternate approval service was added.

## Entity Routing

New/hardened workflow entity keys:

- `ExchangeRate`
- `FixedAsset`
- `FixedAssetDepreciationRun`
- `AssetValuation`

Workflow context resolution was added to `SimpleWorkflowService` so workflow definitions can evaluate entity fields such as rate pair, approval status, asset code, valuation amount, depreciation period, and run amount.

## Approval Outcomes

Approval outcomes apply tenant-scoped state changes only:

- `ExchangeRate`: `Pending` or `Rejected` rates cannot be used; approved rates become usable by existing rate resolution. Used rates remain locked from destructive edits.
- `FixedAsset`: direct capitalization requests move from `PendingApproval` to `Acquired` on approval, then capitalization posts through `IFinancePostingEngine`.
- `FixedAssetDepreciationRun`: approved runs can be posted through `PostApprovedRunAsync`, which posts through `IFinancePostingEngine`.
- `AssetValuation`: approved revaluation/impairment records can post through the existing valuation posting path, which uses `IFinancePostingEngine`.

Rejected records are blocked from posting and retain rejection reason/comment where the current model supports it.

## Permission And SoD Behavior

Finance approval processing now checks:

- current user identity
- tenant-scoped pending approval row
- role/approver matching through the existing workflow approval assignment model
- submitter self-approval blocking for high-risk Finance entity types

The implementation does not redesign global role assignment. Tenant-aware role-grant hardening remains tracked separately by `FIN-LIM-0014`.

## Posting-Engine Boundary

Workflow approval only changes workflow/action state. Accounting entries still post through existing central posting paths:

- direct capitalization: `IFinancePostingEngine`
- depreciation run posting: `IFinancePostingEngine`
- valuation/revaluation/impairment posting: `IFinancePostingEngine`
- exchange-rate usage: existing posting/revaluation services resolve only approved tenant-owned rates

No new direct journal writer was introduced.

## Audit Events

Added Finance audit constants and call sites for:

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

Module-specific existing audit events continue to be emitted by the posting services after approved actions post.

## Tenant Isolation

Approval queue queries are tenant-filtered. Outcome application re-loads target records by current finance tenant and target entity ID before changing status. Exchange-rate, fixed asset, depreciation run, and valuation service changes also use tenant-filtered queries and posting-engine tenant validation.

## Tests

Focused tests added:

- `FxFunctionalCurrencyGovernanceTests.ExchangeRateRequiresWorkflowApprovalBeforeUse`
- `FixedAssetCapitalizationFoundationTests.DirectCapitalizationRequiresWorkflowApprovalBeforePosting`
- `FixedAssetDepreciationFoundationTests.DepreciationRunRequiresWorkflowApprovalBeforePosting`
- `FixedAssetRevaluationImpairmentFoundationTests.ValuationRequiresWorkflowApprovalBeforePosting`

Scenario mapping:

- Exchange-rate cannot be used before approval where workflow is required: `ExchangeRateRequiresWorkflowApprovalBeforeUse`
- Approved exchange rate can be used: `ExchangeRateRequiresWorkflowApprovalBeforeUse`
- Fixed asset direct capitalization cannot post before approval: `DirectCapitalizationRequiresWorkflowApprovalBeforePosting`
- Fixed asset direct capitalization posts after approval through the posting engine: `DirectCapitalizationRequiresWorkflowApprovalBeforePosting`
- Depreciation run cannot post before approval: `DepreciationRunRequiresWorkflowApprovalBeforePosting`
- Depreciation run posts after approval through the posting engine: `DepreciationRunRequiresWorkflowApprovalBeforePosting`
- Revaluation/impairment cannot post before approval: `ValuationRequiresWorkflowApprovalBeforePosting`
- Revaluation/impairment posts after approval through the posting engine: `ValuationRequiresWorkflowApprovalBeforePosting`

## Migrations

No migration required. The new fixed asset approval statuses use the existing enum-backed `FixedAsset.Status` column, and the hardened workflow routes use existing workflow tables.

## Rollback Considerations

Rollback is code and seed-configuration only:

- remove workflow entity seed additions for `ExchangeRate` and `FixedAssetDepreciationRun`
- remove pending-approval gates from exchange-rate, direct capitalization, depreciation, and valuation services
- remove Finance approval queue outcome handling for the new entity keys
- remove the focused tests/docs

Rollback would weaken go-live approval controls and should not be used after production workflow definitions are configured.

## Limitations Register

Resolved in this batch:

- `FIN-LIM-0020`
- `FIN-LIM-0029`
- `FIN-LIM-0032`
- `FIN-LIM-0036`

Still open:

- `FIN-LIM-0006`: controlled opening-balance/migration posting
- `FIN-LIM-0014`: tenant-aware role-assignment hardening
- reversal/correction limitations that were explicitly out of scope

## PR Definition Of Done

- High-risk routes use the existing workflow engine.
- Pending/rejected high-risk actions cannot post or be used by posting.
- Approved actions still post only through `IFinancePostingEngine`.
- Approval outcomes are tenant-scoped and audited.
- Focused tests pass.
- Limitations register is updated.
