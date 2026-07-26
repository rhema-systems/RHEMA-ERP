# FX Functional Currency Governance Foundation - PR Summary

Date: 2026-07-06

## Scope

Implemented FX Batch 17 foundation only: tenant functional currency governance, exchange-rate governance, and posting currency/rate snapshots. Realized FX, unrealized FX revaluation, AP/AR settlement gain/loss, bank FX revaluation, fixed assets, data migration, frontend UI, and export/reporting work were not started.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/FinanceSettings.cs`
- `src/ErpSystem.Core/Entities/Finance/AccountTransaction.cs`
- `src/ErpSystem.Core/Entities/Finance/FinancePostingEvent.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceSettingsDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/ExchangeRateDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/Settings/FinanceSettingsService.cs`
- `src/ErpSystem.Api/Services/Finance/MultiCurrency/ExchangeRateService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260706153000_AddFxFunctionalCurrencyAndRateSnapshots.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FxFunctionalCurrencyGovernanceTests.cs`
- `docs/fx-functional-currency-governance-foundation.md`
- `docs/finance-go-live-limitations-register.md`

## Migrations

Added `20260706153000_AddFxFunctionalCurrencyAndRateSnapshots`.

Schema impact:

- Adds functional-currency lock metadata to `FinanceSettings`.
- Narrows `FinanceSettings.BaseCurrency` to `nvarchar(3)`.
- Adds transaction-currency debit/credit, functional currency, and exchange-rate reference fields to `AccountTransactions`.
- Adds foreign-currency summary snapshot fields to `FinancePostingEvents`.
- Adds tenant/rate governance and snapshot indexes.
- Adds FKs from posted lines/posting events to tenant-owned `ExchangeRates`.

Rollback:

- The migration `Down` removes snapshot fields/indexes/FKs and restores prior column shapes.
- Rolling back after foreign-currency postings would remove audit-critical rate snapshots, so rollback should happen only before production foreign-currency posting or after accountant-approved export/backup.

## Tests

Added `FxFunctionalCurrencyGovernanceTests` with 12 tests:

- `TenantCanConfigureFunctionalCurrencyBeforeAccountingActivity`
- `FunctionalCurrencyChangeAfterAccountingActivityIsRejectedAndAudited`
- `ForeignCurrencyPostingWithoutFunctionalCurrencyConfigurationIsRejected`
- `SameCurrencyPostingDoesNotRequireExchangeRateLookup`
- `ForeignCurrencyPostingRequiresEffectiveTenantExchangeRate`
- `CrossTenantExchangeRateIsRejected`
- `ExchangeRateServiceRejectsZeroNegativeAndOverlappingRates`
- `PostingCapturesRateSnapshotLocksRateAndEmitsAudit`
- `UsedExchangeRateCannotBeEditedOrDeletedAndSnapshotRemainsStable`
- `DeterministicRateSelectionUsesEffectiveRateForPostingDate`
- `ClosedPeriodForeignCurrencyPostingIsRejectedThroughPostingEngine`
- `AccountCurrencyRestrictionRejectsForeignPostingToSingleCurrencyAccount`

Build/test results:

- Backend API build: passed (`dotnet build src\ErpSystem.Api\ErpSystem.Api.csproj --no-restore -m:1 -o .codex-build\api-final /p:WarningLevel=0 /p:RunAnalyzers=false /clp:ErrorsOnly`).
- Test assembly build: passed (`dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-restore -m:1 -o .codex-build\tests-verify /p:WarningLevel=0 /p:RunAnalyzers=false /clp:ErrorsOnly`).
- Focused FX test slice: `12/12` passed for `Batch=FinanceGoLive-FXFoundation`.
- Finance go-live regression slice: `202/202` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Functional currency is read and changed through tenant-scoped `FinanceSettings`.
- Exchange-rate create/update/delete/read paths enforce `TenantId`.
- Posting-engine rate resolution filters by tenant and rejects cross-tenant exchange-rate IDs.
- Posted line and posting-event snapshots store same-tenant exchange-rate references.

## Accounting Impact

- Foreign-currency postings now require tenant functional currency and an effective tenant-owned approved exchange rate.
- Posted GL functional-currency amounts remain the measurement source for financial statements.
- Posted transaction-currency amounts and exchange-rate snapshots are preserved for IAS 21-style settlement and revaluation batches.
- Exchange-rate edits after usage cannot mutate or reinterpret posted accounting.

## Limitations Register

- `FIN-LIM-0005`: Resolved.
- `FIN-LIM-0015`: Narrowed to Batch 18 realized/unrealized FX, bank FX, settlement gain/loss, and cross-currency reconciliation.
- `FIN-LIM-0020`: Added for exchange-rate approval routing through workflow engine.

## Definition Of Done

- [x] Backend build impact verified for Data project and FX-focused tests.
- [x] No frontend build/type-check impact; no frontend files were intentionally changed in this batch.
- [x] Tests added and focused FX test slice passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Limitations register updated.

## Safe To Proceed

Safe to proceed to FX Batch 18 for realized/unrealized FX and settlement accounting. Batch 18 should use the posted rate snapshots introduced here and must update `FIN-LIM-0015`.
