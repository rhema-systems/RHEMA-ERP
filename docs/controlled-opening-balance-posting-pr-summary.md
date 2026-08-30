# Controlled Opening-Balance Posting and Migration Sign-Off Preparation PR Summary

## Summary

Implemented a tenant-scoped opening-balance posting foundation for balanced GL trial-balance batches.

Opening-balance journals now post through `IFinancePostingEngine` and create immutable posted GL, `FinancePostingEvent`, and source batch back-references. Unsafe `ALL_ACTIVE_BOOKS`, unbalanced imports, direct account balance mutation, and direct bank opening-balance mutation remain rejected.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/OpeningBalanceBatch.cs`
- `src/ErpSystem.Core/DTOs/Finance/OpeningBalanceDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IOpeningBalanceService.cs`
- `src/ErpSystem.Api/Services/Finance/Migration/OpeningBalanceService.cs`
- `src/ErpSystem.Api/Controllers/Finance/OpeningBalancesController.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260709120000_AddOpeningBalancePostingFoundation.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Api/Services/DatabaseSeedingService.cs`
- `src/ErpSystem.Api/Services/SimpleWorkflowService.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/ControlledOpeningBalancePostingTests.cs`
- `docs/controlled-opening-balance-posting-foundation.md`
- `docs/finance-go-live-limitations-register.md`
- `docs/finance-legacy-posting-path-lockdown.md`

## Migration

Added migration `20260709120000_AddOpeningBalancePostingFoundation` for:

- `OpeningBalanceBatches`
- `OpeningBalanceLines`

The migration is schema-only. It does not post opening balances or mutate account/bank snapshots.

## Supported Scope

- Balanced GL trial-balance opening balances.
- Single explicit book classification such as `IFRS`.
- Existing workflow engine approval route for `OpeningBalanceBatch`.
- Posting through `IFinancePostingEngine`.
- Idempotent duplicate posting behavior.
- Backend diagnostics for unposted, failed, duplicate, or reference-incomplete opening-balance batches.

## Not Supported In This Batch

- `ALL_ACTIVE_BOOKS`.
- Single-sided imports with automatic suspense/equity plug.
- Free-form foreign-currency GL opening lines without explicitly modelled original foreign debit/credit amounts. Governed foreign bank openings were added subsequently with approved-rate provenance and separate native/functional values.
- AP/AR/fixed-asset subledger opening-document migration.
- Final production migration execution or accountant sign-off.

## Tests Added

`ControlledOpeningBalancePostingTests`:

- `BalancedGlOpeningBalanceBatch_ShouldPostThroughFinancePostingEngine`
- `UnbalancedOpeningBalanceBatch_ShouldBeRejectedWithoutPosting`
- `ClosedPeriodOpeningBalancePosting_ShouldBeRejectedByPostingEngine`
- `CrossTenantAccount_ShouldBeRejected`
- `InactiveOrNonPostingAccount_ShouldBeRejected`
- `DuplicateOpeningBalancePosting_ShouldReturnExistingJournal`
- `OpeningBalancePosting_ShouldRequireWorkflowApproval_WhenWorkflowIsConfigured`
- `RejectedOpeningBalanceBatch_ShouldNotPost`
- `AllActiveBooksOpeningBalance_ShouldRemainRejected`
- `BankNonzeroOpeningBalance_ShouldRemainBlockedUnlessPostedThroughOpeningBalanceFlow`
- `OpeningBalancePosting_ShouldNotUseLegacySubledgerPostingService`
- `PostingBackReferenceRepairDryRun_ShouldDetectMissingLinksWithoutMutation`
- `PostingBackReferenceRepair_ShouldRepairOnlyUnambiguousLinks`
- `PostingBackReferenceRepair_ShouldRefuseAmbiguousMatches`
- `BankSnapshotDiagnosticAndRepair_ShouldUsePostedGlOnly`
- `SubledgerOpeningMigrationDecision_ShouldRejectGlOnlySubledgerOpenings`

## Build And Test Results

- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`: passed.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`: passed.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`: passed.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter FullyQualifiedName~ControlledOpeningBalancePostingTests --logger "console;verbosity=minimal"`: passed `16/16`.

## Limitations Register

- `FIN-LIM-0006`: resolved for balanced GL opening-balance posting through `IFinancePostingEngine`; `ALL_ACTIVE_BOOKS` remains rejected by design.
- `FIN-LIM-0007`: resolved for tenant-safe posting back-reference diagnostic and repair tooling.
- `FIN-LIM-0008`: resolved for bank snapshot diagnostic and rebuild tooling.
- `FIN-LIM-0017`: narrowed for migration/sign-off preparation.
- `FIN-LIM-0048`: added for AP/AR/fixed-asset subledger opening-document migration where production cutover requires source subledger opening balances.

## Rollback Considerations

Rolling back the migration drops the opening-balance batch and line tables only.

Any opening-balance journals already posted through the engine remain posted GL and require controlled reversal/environment reset rather than schema rollback.
