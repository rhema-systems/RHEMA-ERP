---
integration_cycle: FIN-UAT-2026-10-05-A
integration_status: ready
integration_decision: include
candidate_branch: codex/finance-uat-remediation-20261004
candidate_head: 09b9a630782218bb765ba4ef6d0275233062ec45
base_commit: e1c25df6558f3df70d27d3027ad104dffdd43166
target_ref: origin/master
depends_on: bank-deposit-acknowledgement-lifecycle
migration_status: 20261005225306-applied-RHEMAERP_BOOKV2_UAT_20260922
verification_status: focused-passed-baseline-typecheck-failures
integration_commit: pending
pull_request: pending
---

# Finance budgeting hybrid scope and unused-Draft lifecycle - 2026-10-05

## Objective and scope

Implement governed hybrid account-segment + Finance-dimension Budget Return scope, then implement safe edit/delete management for genuinely unused Draft budget scenarios.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Investigation starting commit: `e1c25df65`
- Implementation base commit: `5389fe6bcb28db3a37f7a21fe41fee93b3fc9909`
- Completed implementation commit: `90a2c4ad1` (`feat(finance): govern budget return scope and draft lifecycle`)
- Corrective migration commit: `ae42e678c` (`fix(finance): use compatible budget return index filter`)
- Searchable assignee follow-up commit: `dfae8481b` (`fix(finance): make budget return assignee searchable`)
- Pull request: no PR creation, update, or push is authorized for this workstream yet.

## Product decisions and evidence

- A Draft scenario should be editable while it has no budget returns, lines, allocations, workflow instances, approvals, derived versions, or other dependent evidence.
- An unused Draft should be deletable under the same dependency rule, using a server-authoritative atomic eligibility check and an audited soft delete/tombstone rather than client-only hiding or physical erasure.
- Once dependent evidence exists, structural fields such as fiscal year, currency, and dimension structure must lock.
- The current Create Budget Return dialog exposes `Worksheet distribution dimension` and then a value from that scenario control dimension.
- The current client does not expose `segmentValueId`, although that optional property remains in the client/server DTOs, entity, duplicate key, reporting, and budget-control logic.
- Therefore the training instruction to select `Segment Type COMPANY` and then `Department / Unit DEFAULT` describes two selections that the current UI cannot reproduce. A scenario configured with the DEPARTMENT control dimension can select its DEFAULT value, but cannot also select COMPANY as a separate segment value.
- Classification: confirmed training/product contract drift. A product decision is required before implementation: retire the legacy segment axis and update the guide, or restore an explicit governed segment selector and define its relationship to Finance dimensions.
- Industry comparison completed against current Oracle Planning, SAP Analytics Cloud Planning, and Microsoft Dynamics 365 Finance guidance:
  - Oracle models an approval unit as scenario + version + entity, with optional secondary dimensions for finer approval scope.
  - SAP generates planning tasks from a driving dimension, usually an organizational hierarchy; other model dimensions remain planning context and filters.
  - Dynamics 365 uses an organizational hierarchy to distribute budget planning responsibility, while budget lines carry the selected financial dimensions.
- Corrected product decision: RHEMA budgeting is a hybrid account-segment + Finance-dimension model. A Budget Return is a responsibility/workflow envelope whose governed scope may include both an account segment value (for example `COMPANY=DEFAULT`) and an organizational Finance dimension value (for example `DEPARTMENT=DEFAULT`) when those axes have distinct accounting meanings.
- `COMPANY` is an account-code segment in the current RHEMA model. It is not automatically equivalent to the tenant/legal-entity context and must not be removed or suppressed on that assumption.
- `SegmentValueId` remains an active control input: revisions, commitments, and budget-control matching consume it. The current UI exposes only `DistributionDimensionValueId`, so the training flow identifies a genuine missing segment-selection path rather than merely stale wording.
- Recommended compatibility path: retain segment-based budgeting, restore a governed segment structure/value selector for returns that require it, and keep Finance dimension distribution as a separate selector. The server must validate both values against the tenant and the scenario's configured budget grain.
- Budget Return assignee lists can be large; the create dialog now uses a searchable name/email combobox while retaining the explicit `Leave unassigned` option and the existing assignee ID contract.

## Changed files

- `src/ErpSystem.Core/Entities/Finance/BudgetScenario.cs`
  - Adds scenario-owned `BudgetScenarioControlSegment` declarations.
- `src/ErpSystem.Core/DTOs/Finance/BudgetDtos.cs`
  - Carries segment structure metadata and editable Draft structure fields through API contracts.
- `src/ErpSystem.Core/Interfaces/Finance/IBudgetService.cs`
  - Requires the scenario row version for deletion.
- `src/ErpSystem.Data/ApplicationDbContext.cs`
  - Maps scenario segment controls and changes Budget Return uniqueness to the combined segment + dimension grain.
- `src/ErpSystem.Data/Migrations/20261005225306_AddBudgetScenarioSegmentControls.cs` and designer/snapshot
  - Creates the control table, backfills declarations from existing segment-scoped returns, and replaces the two over-restrictive indexes with one combined unique index.
- `src/ErpSystem.Api/Services/Finance/Budget/BudgetService.cs`
  - Validates tenant-owned active lookup segments, requires configured segment/dimension selections, maps both independently, records full structure-change audit evidence, and enforces the unused-Draft dependency guard.
  - Soft-deletes eligible Drafts and their grain declarations with optimistic concurrency.
- `src/ErpSystem.Api/Services/Finance/Budget/BudgetService.Revisions.cs`
  - Copies segment declarations into immutable revision successors and preserves both return scope values.
- `src/ErpSystem.Api/Controllers/Finance/BudgetController.cs`
  - Documents and exposes row-version-protected unused-Draft deletion.
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
  - Adds the budget-scenario deletion audit event.
- `frontend/src/types/budget.ts` and `frontend/src/services/finance/budget-data.service.ts`
  - Align client contracts and row-version deletion.
- `frontend/src/app/finance/budgeting/scenarios/page.tsx`
  - Lets scenario creators declare governed account-segment structures separately from Finance dimensions.
- `frontend/src/app/finance/budgeting/scenarios/[id]/page.tsx`
  - Restores Segment Type -> Segment Value before the separate Distribution Dimension -> Dimension Value selection.
  - Shows Edit/Delete only for a permissioned Draft with no returned rows; the server remains authoritative.
  - Makes the Create Budget Return `Assign To` control searchable by display name or email.
- `tests/ErpSystem.Api.Tests/Services/Finance/BudgetServiceHardeningTests.cs`
  - Adds hybrid-scope, combined-grain, unused-Draft edit/delete, and dependency-lock regressions.
- `tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs`
  - Pins update/delete to `Finance.Budgeting.Write` / `MaintainBudgets`.

## Migrations and application state

- New migration: `20261005225306_AddBudgetScenarioSegmentControls`.
- Migration status: created and SQL-script validated, deliberately **not applied**.
- Compatibility: existing non-deleted returns with `SegmentValueId` backfill their scenario/segment-structure declarations.
- The combined unique active-return key is `(TenantId, BudgetScenarioId, SegmentValueId, DistributionDimensionValueId)` with the SQL Server-compatible filter `[IsDeleted] = 0`. This permits the same `COMPANY` value across different departmental distributions, rejects an exact duplicate responsibility scope, and permits only one unscoped legacy return per scenario.
- No UAT or local database data was changed.

## Verification evidence

- Inspected `CreateBudgetReturnDto` in client and server contracts.
- Inspected the Create Budget Return dialog in `frontend/src/app/finance/budgeting/scenarios/[id]/page.tsx`.
- Inspected `BudgetService.CreateReturnAsync` validation and persistence behavior.
- Inspected budget hardening tests proving the current required distribution control is a scenario Finance dimension value.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj -c Release --no-restore`: passed with 0 errors (baseline warnings remain).
- `dotnet test ... --filter FullyQualifiedName~BudgetServiceHardeningTests`: 29 passed, 0 failed.
- `dotnet test ... --filter FullyQualifiedName~BudgetSegmentMigration_UsesSqlServerCompatibleCombinedScopeFilter`: 1 passed, 0 failed; the contract asserts the exact combined key and rejects a filtered-index predicate containing `OR`.
- `dotnet test ... --filter FullyQualifiedName~CriticalFinanceActions_ShouldMapToExpectedPermissions`: 92 passed, 0 failed.
- Changed-file ESLint for both scenario pages, the budget service, and budget types: passed.
- Focused ESLint after converting Create Budget Return `Assign To` to a searchable combobox: passed.
- `npm run type-check`: repository-wide baseline remains red on unrelated existing development, portal, HR, inventory, reporting, and test fixture errors; no error references the changed budgeting files.
- Corrected migration SQL generated idempotently from immediate predecessor `20261005211546_ReorderBankDepositAcknowledgementBeforePosting`; the isolated script contains only the intended control table, legacy backfill, and combined-index operations, with `WHERE [IsDeleted] = 0` on the combined unique index.
- First local startup application attempt failed before completion because SQL Server rejected the original filtered-index predicate containing `OR` (`Error 156`). The corrected migration uses `[IsDeleted] = 0`; no restart or repeat application was performed by this task.
- On 2026-10-06, `RHEMAERP_BOOKV2_UAT_20260922` migration history and schema verification confirmed the corrected migration is applied: `BudgetScenarioControlSegments` exists and the combined active Budget Return uniqueness index has the expected `[IsDeleted] = 0` filter.
- `git diff --check`: passed (only Windows line-ending notices).
- Broader `FinanceControllerSecurityTests` run: 134 passed and 2 unrelated existing diagnostics failed:
  - `VendorInvoiceService.ReceiptAccounts.cs` contains an existing `Guid.Empty` tenant fallback.
  - `CurrenciesController.GetActive` maps to existing composite policy `Finance.Policy.ProjectCurrencyLookup`, which the broad registry assertion does not recognize.

## Remaining work

- The migration is applied to the named local UAT database; manually verify create/edit/delete and return distribution before promoting beyond this environment.
- The current downstream model supports one account `SegmentValueId` plus one distribution dimension per return. Scenarios may govern multiple eligible segment structures, but a single return selects one structure/value. Supporting multiple account segment values on one return would require a separate normalized return-scope collection and is outside this authorized change.
- A repository-wide TypeScript cleanup and the two unrelated Finance security diagnostics remain separate workstreams.

## Authorization boundaries

- Local implementation, tests, migration authoring, documentation, and local commit are authorized.
- The user has now authorized consolidation onto latest `origin/master`, pushing the integration branch, and creating one unified PR.
- Do not merge, deploy, restart services, apply further migrations, mutate UAT data, or remove branches/worktrees without separate authorization.
