# FX Realized And Unrealized Revaluation - PR Summary

Date: 2026-07-06

## Scope

Implemented FX Batch 18 only, then completed the focused hardening pass: realized FX on AP/AR settlement, unrealized AP/AR monetary revaluation, foreign bank/cash revaluation, revaluation reversal, FX account mapping audit, diagnostics, and expanded coverage. Frontend UI, fixed assets, tax reporting/export, data migration/sign-off, financial statement export, and exchange-rate workflow routing were not started.

## Definition Of Done

- [x] Backend build impact verified.
- [x] No frontend build/type-check impact; no frontend files were intentionally changed in this batch.
- [x] Tests added and focused FX Batch 18 suite passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Limitations register updated with resolved/split limitation IDs.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/FxAccountingEntities.cs`
- `src/ErpSystem.Core/Entities/Finance/FinanceSettings.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceSettingsDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFxAccountingService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/MultiCurrency/CurrencyRevaluationService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorPaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/Settings/FinanceSettingsService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260706170000_AddFxRealizedAndRevaluationAccounting.cs`
- `src/ErpSystem.Data/Seeders/FinanceDataSeeder.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FxRealizedUnrealizedRevaluationTests.cs`
- `docs/fx-realized-unrealized-revaluation-foundation.md`
- `docs/fx-realized-unrealized-revaluation-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Migration

Added `20260706170000_AddFxRealizedAndRevaluationAccounting`.

Schema impact:

- Adds separate unrealized FX gain/loss account mappings to `FinanceSettings`.
- Adds `FxRealizedSettlements` for per-allocation AP/AR realized FX snapshots.
- Adds `FxRevaluationBatches` and `FxRevaluationLines` for unrealized AP/AR/bank revaluation batches, rate snapshots, journal references, posting-event references, and reversal links.
- Adds unique tenant/idempotency indexes for realized settlements and revaluation batches.

Rollback:

- Safe only before production FX settlement/revaluation postings.
- After production use, rollback would remove audit-critical realized settlement and revaluation batch records and requires accountant-approved export/reconciliation first.

## Tests

Added and expanded `FxRealizedUnrealizedRevaluationTests`:

| Scenario | Test method |
|---|---|
| AP realized FX gain/loss signs | `ApSettlementRateIncreasePostsRealizedLoss`, `ApSettlementRateDecreasePostsRealizedGain` |
| AR realized FX gain/loss signs | `ArSettlementRateIncreasePostsRealizedGain`, `ArSettlementRateDecreasePostsRealizedLoss` |
| Partial AP/AR proportional settlement | `PartialApSettlementCalculatesProportionalFx`, `PartialArReceiptCalculatesProportionalFx` |
| Multiple allocations at different historical rates | `ApPaymentAllocatedToMultipleInvoicesCalculatesFxPerAllocation`, `ArReceiptAllocatedToMultipleInvoicesCalculatesFxPerAllocation` |
| Same-rate and duplicate realized FX idempotency | `SameRateSettlementProducesNoRealizedFxJournal`, `DuplicateRealizedFxPostingReturnsExistingSettlement` |
| Missing/cross-tenant FX account mappings | `MissingRealizedFxMappingRejectsSettlementPosting`, `MissingUnrealizedFxMappingRejectsRevaluationPosting`, `CrossTenantRealizedFxAccountMappingIsRejected`, `CrossTenantUnrealizedFxAccountMappingIsRejected` |
| Cross-tenant exchange-rate references | `CrossTenantExchangeRateSnapshotIsRejectedForSettlement`, `CrossTenantClosingRateIsRejectedForRevaluation` |
| Closed-period FX posting rejection | `ClosedPeriodRealizedFxSettlementIsRejectedThroughPostingEngine`, `ClosedPeriodRevaluationIsRejected` |
| Functional/third-currency settlement edge cases | `FunctionalOrThirdCurrencySettlementOfForeignInvoiceIsRejected` |
| WHT/VAT withholding settlement interaction | `WithholdingSettlementDoesNotOverstateRealizedFxBasis` |
| Unrealized AP/AR/bank signs and posting engine usage | `UnrealizedRevaluationPostsApArAndForeignBankSignsThroughPostingEngine`, `ForeignBankRateIncreasePostsBankDebitAndUnrealizedGain`, `ForeignBankRateDecreasePostsUnrealizedLossAndBankCredit` |
| Bank source of truth | `ForeignBankRevaluationUsesPostedGlSnapshotsNotBankCurrentBalance` |
| Missing closing rate and rate type selection | `MissingClosingRateBlocksUnrealizedRevaluation`, `RevaluationUsesRequestedQuarterEndRateType` |
| Revaluation and reversal idempotency | `DuplicateRevaluationIsIdempotentAndReversalPostsInOpenPeriod` |
| Posted snapshot immutability after rate edits | `LaterExchangeRateEditDoesNotMutatePostedRealizedFxSnapshot`, `LaterExchangeRateEditDoesNotMutatePostedUnrealizedRevaluationSnapshot` |
| Repeat-period revaluation behavior | `NextPeriodRevaluationAfterUnreversedPriorBatchUsesPriorCarryingAdjustment`, `RevaluationAfterReversalDoesNotDoubleCountPriorAdjustment` |

Results:

- API build: passed.
- Test assembly build: passed.
- Focused FX Batch 18 suite: `31/31` passed for `Batch=FinanceGoLive-FXSettlementRevaluation`.
- Finance go-live regression slice: `233/233` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- FX settlement and revaluation queries use the current finance `TenantId`.
- FX account mappings are resolved only from same-tenant active GL accounts.
- Realized settlement records and revaluation batches/lines store `TenantId`.
- Posting requests pass `SourceDocumentTenantId`.
- Revaluation exchange rates are resolved only from tenant-owned effective rates.
- Cross-tenant account/rate references are rejected by the FX service and posting engine.
- Unsupported AP/AR cross-currency settlement is rejected before normal posting can silently bypass realized FX.

## Accounting Impact

- AP realized FX:
  - rate increase on payable settlement posts realized FX loss.
  - rate decrease on payable settlement posts realized FX gain.
- AR realized FX:
  - rate increase on receivable settlement posts realized FX gain.
  - rate decrease on receivable settlement posts realized FX loss.
- Unrealized revaluation:
  - revalues open posted AP, AR, and foreign bank/cash monetary balances.
  - uses closing rates by revaluation date and rate type.
  - posts only functional-currency adjustment journals through `IFinancePostingEngine`.
- Original invoice, payment, receipt, and bank journals are immutable and not recalculated.
- Statutory tax is not recalculated when exchange rates change.
- AR withholding no longer inflates realized FX exposure; AP withholding remains included in AP settlement amount because the payment clears the gross payable.

## Limitations Register

- `FIN-LIM-0015`: resolved.
- `FIN-LIM-0001`: narrowed; FX exposure now uses posted GL/currency snapshots, but AP/AR aging still needs a rebuildable settlement read model.
- `FIN-LIM-0020`: remains open for exchange-rate workflow routing; not next-batch-blocking.
- `FIN-LIM-0021`: added for cross-currency bank transfer and reconciliation hardening; not next-batch-blocking for fixed assets.
- `FIN-LIM-0022`: added for AP/AR cross-currency settlement where invoice currency differs from payment/receipt currency; not next-batch-blocking for fixed assets because such settlements are now rejected clearly.

## FX Assumptions

- Exchange-rate direction remains `1 transaction currency unit = Rate functional currency units`.
- Revaluation uses `MonthEnd`, `QuarterEnd`, or `YearEnd` rates based on request type.
- Realized FX is calculated per allocation, not as a blended payment total.
- Foreign bank/cash balances are derived from posted GL movement, not `BankAccount.CurrentBalance`.
- Supported AP/AR realized FX settlement requires payment/receipt currency to match the allocated invoice currency. Functional-currency settlement of a foreign invoice and third-currency settlement are rejected until `FIN-LIM-0022` is resolved.

## Safe To Proceed

Safe to proceed to fixed assets from an FX foundation perspective. `FIN-LIM-0020`, `FIN-LIM-0021`, and `FIN-LIM-0022` remain go-live blockers, but none weakens the fixed-asset accounting foundation because exchange rates can be approved/seeded, cross-currency bank transfers/reconciliation remain out of scope, and unsupported AP/AR cross-currency settlements are rejected rather than posted incorrectly.
