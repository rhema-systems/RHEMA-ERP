# FX Batch 18 - Realized And Unrealized FX Revaluation And Settlement Accounting

Date: 2026-07-06

Scope boundary: this batch implements realized FX for posted AP/AR settlements, unrealized AP/AR monetary revaluation, foreign bank/cash revaluation, revaluation reversal support, tenant FX account mappings, audit events, diagnostics, and tests. It does not implement frontend UI, fixed assets, tax reporting/export, data migration/sign-off, financial statement export, or exchange-rate workflow routing.

## Rate Direction

The Batch 17 rate convention remains canonical:

- `ExchangeRate.BaseCurrencyCode` = tenant functional currency.
- `ExchangeRate.TargetCurrencyCode` = transaction currency.
- `ExchangeRate.Rate` means `1 TargetCurrency unit = Rate BaseCurrency units`.

Example: functional currency `GHS`, transaction currency `USD`, rate `12` means `1 USD = 12 GHS`.

All realized and unrealized FX calculations use this same direction.

## Files And Modules

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
- `tests/ErpSystem.Api.Tests/Services/Finance/FxRealizedUnrealizedRevaluationTests.cs`

## Realized FX Settlement Design

Realized FX is calculated from posted source-document snapshots, not mutable invoice paid fields.

AP basis:

- Historical basis: posted AP invoice control-account line.
- Settlement basis: posted AP payment control-account line.
- Positive settlement delta means the functional-currency payable increased before settlement and posts an FX loss.
- Negative settlement delta means the payable decreased and posts an FX gain.

AR basis:

- Historical basis: posted AR invoice control-account line.
- Settlement basis: posted AR receipt control-account line.
- Positive settlement delta means the receivable increased before settlement and posts an FX gain.
- Negative settlement delta means the receivable decreased and posts an FX loss.

Settlement journals are posted through `IFinancePostingEngine` using:

- AP source document type `VendorPaymentAllocation`
- AR source document type `PaymentAllocation`
- posting action `RealizedFx`
- tenant-scoped idempotency key per allocation

Settlement currency guard:

- FX Batch 18 supports foreign-currency invoices settled in the same foreign currency.
- Functional-currency settlement of a foreign invoice and third-currency settlement are rejected clearly in AP/AR payment posting and in `CurrencyRevaluationService`.
- This avoids a silent skip of realized FX when payment currency differs from invoice transaction currency.
- Full AP/AR cross-currency settlement accounting is tracked as `FIN-LIM-0022`.

The original posted invoice/payment/receipt journals are not mutated.

## Unrealized Revaluation Design

The exposure basis is derived from posted GL lines and currency snapshots:

- posted AP control-account foreign-currency lines
- posted AR control-account foreign-currency lines
- posted foreign bank/cash GL movement
- prior unreversed FX revaluation batch lines

The service does not rely on operational `PaidAmount`, `CreditedAmount`, bank snapshot balances, or mutable source-document status fields as the accounting basis.

Closing rates:

- month-end requests use `ExchangeRateType.MonthEnd`
- quarter-end requests use `ExchangeRateType.QuarterEnd`
- year-end requests use `ExchangeRateType.YearEnd`
- rates must be tenant-owned, active, approved or auto-approved, positive, and effective for the revaluation date

Unrealized revaluation journals are posted through `IFinancePostingEngine` using source document type `FxRevaluationBatch` and posting action `UnrealizedRevaluation`.

## Revaluation Signs

AR/open receivable:

- rate increase: debit AR, credit unrealized FX gain
- rate decrease: debit unrealized FX loss, credit AR

AP/open payable:

- rate increase: debit unrealized FX loss, credit AP
- rate decrease: debit AP, credit unrealized FX gain

Foreign bank/cash asset:

- rate increase: debit bank/cash, credit unrealized FX gain
- rate decrease: debit unrealized FX loss, credit bank/cash

## Reversal Policy

`IFxAccountingService.ReverseRevaluationBatchAsync` posts a reversing journal through `IFinancePostingEngine`.

Rules:

- only posted batches can be reversed
- reversal reason is required
- reversal cannot post into closed/locked periods because the posting engine enforces period status
- duplicate reversal requests return the existing reversal safely
- reversal links are stored on `FxRevaluationBatch`

## Tenant FX Account Mappings

`FinanceSettings` now supports separate mappings for:

- realized FX gain
- realized FX loss
- unrealized FX gain
- unrealized FX loss

All mappings must point to active same-tenant GL accounts. Missing or cross-tenant mappings fail posting clearly and emit `Finance.FX.PostingBlockedInvalidConfiguration` where audit infrastructure is available.

## Audit Events

Added or verified actual event values:

- `Finance.FX.AccountMappingChanged`
- `Finance.FX.RealizedCalculated`
- `Finance.FX.RealizedPosted`
- `Finance.FX.RealizedPostingFailed`
- `Finance.FX.UnrealizedRevaluationBatchCreated`
- `Finance.FX.UnrealizedRevaluationCalculated`
- `Finance.FX.UnrealizedRevaluationPosted`
- `Finance.FX.UnrealizedRevaluationReversed`
- `Finance.FX.UnrealizedRevaluationPostingFailed`
- `Finance.FX.ExchangeRateUsedForRevaluation`
- `Finance.FX.ForeignBankRevaluationPosted`
- `Finance.FX.PostingBlockedInvalidConfiguration`

Actual call sites verified:

- `FinanceSettingsService` emits `Finance.FX.AccountMappingChanged` when FX account mappings change.
- `CurrencyRevaluationService` emits realized FX calculated, posted, failed, unrealized batch created/calculated/posted/reversed/failed, exchange-rate-used, foreign-bank-posted, and invalid-configuration events.
- Focused tests assert realized calculated/posted, realized failure, invalid configuration, unrealized posted, exchange-rate-used, foreign-bank-posted, and reversal events.

## Diagnostics

Tenants missing FX mappings:

```sql
SELECT TenantId, RealizedFxGainAccountId, RealizedFxLossAccountId, UnrealizedFxGainAccountId, UnrealizedFxLossAccountId
FROM FinanceSettings
WHERE IsDeleted = 0
  AND (
      RealizedFxGainAccountId IS NULL
      OR RealizedFxLossAccountId IS NULL
      OR UnrealizedFxGainAccountId IS NULL
      OR UnrealizedFxLossAccountId IS NULL
  );
```

FX mappings pointing to another tenant or inactive accounts:

```sql
SELECT fs.TenantId, v.MappingName, v.AccountId, a.TenantId AS AccountTenantId, a.Status
FROM FinanceSettings fs
CROSS APPLY (VALUES
    ('RealizedFxGainAccountId', fs.RealizedFxGainAccountId),
    ('RealizedFxLossAccountId', fs.RealizedFxLossAccountId),
    ('UnrealizedFxGainAccountId', fs.UnrealizedFxGainAccountId),
    ('UnrealizedFxLossAccountId', fs.UnrealizedFxLossAccountId)
) v(MappingName, AccountId)
LEFT JOIN Accounts a ON a.Id = v.AccountId AND a.IsDeleted = 0
WHERE fs.IsDeleted = 0
  AND v.AccountId IS NOT NULL
  AND (a.Id IS NULL OR a.TenantId <> fs.TenantId OR a.Status <> 1);
```

Open foreign AP/AR control balances missing posted rate snapshots:

```sql
SELECT atx.Id, atx.TenantId, atx.JournalEntryId, atx.SourceModule, atx.SourceDocumentType, atx.SourceDocumentId
FROM AccountTransactions atx
JOIN FinanceSettings fs ON fs.TenantId = atx.TenantId AND fs.IsDeleted = 0
WHERE atx.IsDeleted = 0
  AND atx.PostingStatus = 'Posted'
  AND atx.TransactionCurrency IS NOT NULL
  AND atx.TransactionCurrency <> atx.FunctionalCurrencyCode
  AND atx.AccountId IN (fs.ControlAccountApId, fs.ControlAccountArId)
  AND (atx.ExchangeRateId IS NULL OR atx.ExchangeRate IS NULL OR atx.ExchangeRate <= 0);
```

Posted foreign AP settlements missing realized FX where rates differ:

```sql
SELECT a.TenantId, a.Id AS AllocationId, a.VendorPaymentId, a.VendorInvoiceId
FROM VendorPaymentAllocation a
JOIN VendorPayment p ON p.Id = a.VendorPaymentId AND p.TenantId = a.TenantId AND p.IsDeleted = 0
JOIN VendorInvoice i ON i.Id = a.VendorInvoiceId AND i.TenantId = a.TenantId AND i.IsDeleted = 0
WHERE a.IsDeleted = 0
  AND a.IsReversal = 0
  AND p.JournalEntryId IS NOT NULL
  AND i.JournalEntryId IS NOT NULL
  AND p.CurrencyCode <> (SELECT TOP 1 BaseCurrency FROM FinanceSettings fs WHERE fs.TenantId = a.TenantId AND fs.IsDeleted = 0)
  AND NOT EXISTS (
      SELECT 1 FROM FxRealizedSettlements fx
      WHERE fx.TenantId = a.TenantId
        AND fx.SettlementAllocationId = a.Id
        AND fx.SettlementDocumentType = 'VendorPayment'
        AND fx.IsDeleted = 0
  );
```

Posted foreign AR settlements missing realized FX where rates differ:

```sql
SELECT a.TenantId, a.Id AS AllocationId, a.CustomerPaymentId, a.InvoiceId
FROM PaymentAllocation a
JOIN CustomerPayment p ON p.Id = a.CustomerPaymentId AND p.TenantId = a.TenantId AND p.IsDeleted = 0
JOIN Invoices i ON i.Id = a.InvoiceId AND i.TenantId = a.TenantId AND i.IsDeleted = 0
WHERE a.IsDeleted = 0
  AND a.IsReversal = 0
  AND p.JournalEntryId IS NOT NULL
  AND i.JournalEntryId IS NOT NULL
  AND p.CurrencyCode <> (SELECT TOP 1 BaseCurrency FROM FinanceSettings fs WHERE fs.TenantId = a.TenantId AND fs.IsDeleted = 0)
  AND NOT EXISTS (
      SELECT 1 FROM FxRealizedSettlements fx
      WHERE fx.TenantId = a.TenantId
        AND fx.SettlementAllocationId = a.Id
        AND fx.SettlementDocumentType = 'CustomerPayment'
        AND fx.IsDeleted = 0
  );
```

Duplicate realized FX postings for one settlement allocation:

```sql
SELECT TenantId, SettlementDocumentType, SettlementDocumentId, SettlementAllocationId, COUNT(*) AS FxCount
FROM FxRealizedSettlements
WHERE IsDeleted = 0
GROUP BY TenantId, SettlementDocumentType, SettlementDocumentId, SettlementAllocationId
HAVING COUNT(*) > 1;
```

Realized FX records without valid same-tenant journal or posting-event references:

```sql
SELECT fx.Id, fx.TenantId, fx.SourceModule, fx.SettlementAllocationId, fx.JournalEntryId, fx.PostingEventId
FROM FxRealizedSettlements fx
LEFT JOIN JournalEntries je ON je.Id = fx.JournalEntryId AND je.TenantId = fx.TenantId AND je.IsDeleted = 0
LEFT JOIN FinancePostingEvents pe ON pe.Id = fx.PostingEventId AND pe.TenantId = fx.TenantId AND pe.IsDeleted = 0
WHERE fx.IsDeleted = 0
  AND (fx.JournalEntryId IS NULL OR fx.PostingEventId IS NULL OR je.Id IS NULL OR pe.Id IS NULL);
```

Revaluation batches without posted journals:

```sql
SELECT Id, TenantId, BatchNumber, RevaluationDate, Status, JournalEntryId, PostingEventId
FROM FxRevaluationBatches
WHERE IsDeleted = 0
  AND Status = 'Posted'
  AND (JournalEntryId IS NULL OR PostingEventId IS NULL);
```

Posted FX revaluation journals without batch references:

```sql
SELECT je.Id, je.TenantId, je.JournalEntryNumber, je.EntryDate
FROM JournalEntries je
LEFT JOIN FxRevaluationBatches b
  ON b.JournalEntryId = je.Id
 AND b.TenantId = je.TenantId
 AND b.IsDeleted = 0
WHERE je.IsDeleted = 0
  AND je.SourceModule = 'FX'
  AND je.ReferenceNumber LIKE 'FXR-%'
  AND b.Id IS NULL;
```

Unreversed revaluation batches where reversal policy requires reversal:

```sql
SELECT Id, TenantId, BatchNumber, RevaluationDate, AutoReverseNextPeriod, ReversalJournalEntryId
FROM FxRevaluationBatches
WHERE IsDeleted = 0
  AND Status = 'Posted'
  AND AutoReverseNextPeriod = 1
  AND ReversalJournalEntryId IS NULL;
```

FX postings with cross-tenant account or rate references:

```sql
SELECT atx.Id, atx.TenantId, atx.JournalEntryId, atx.AccountId, a.TenantId AS AccountTenantId,
       atx.ExchangeRateId, er.TenantId AS RateTenantId
FROM AccountTransactions atx
LEFT JOIN Accounts a ON a.Id = atx.AccountId AND a.IsDeleted = 0
LEFT JOIN ExchangeRates er ON er.Id = atx.ExchangeRateId AND er.IsDeleted = 0
WHERE atx.IsDeleted = 0
  AND atx.SourceModule = 'FX'
  AND (
      a.Id IS NULL OR a.TenantId <> atx.TenantId
      OR (atx.ExchangeRateId IS NOT NULL AND (er.Id IS NULL OR er.TenantId <> atx.TenantId))
  );
```

Foreign bank/cash accounts missing currency configuration:

```sql
SELECT Id, TenantId, AccountNumber, AccountName, Currency, GLAccountId
FROM BankAccounts
WHERE IsDeleted = 0
  AND IsActive = 1
  AND (Currency IS NULL OR LEN(Currency) <> 3 OR GLAccountId IS NULL);
```

FX journals posted to closed/locked periods:

```sql
SELECT je.Id, je.TenantId, je.JournalEntryNumber, je.EntryDate, fp.PeriodStatus, fp.IsClosed, fp.IsLocked
FROM JournalEntries je
JOIN FiscalPeriods fp ON fp.Id = je.FiscalPeriodId
WHERE je.IsDeleted = 0
  AND je.SourceModule = 'FX'
  AND (fp.IsClosed = 1 OR fp.IsLocked = 1 OR fp.IsOpen = 0);
```

FX postings whose functional-currency debits/credits do not balance:

```sql
SELECT je.Id, je.TenantId, je.JournalEntryNumber,
       SUM(atx.DebitAmount) AS TotalDebit,
       SUM(atx.CreditAmount) AS TotalCredit
FROM JournalEntries je
JOIN AccountTransactions atx ON atx.JournalEntryId = je.Id AND atx.IsDeleted = 0
WHERE je.IsDeleted = 0
  AND je.SourceModule = 'FX'
GROUP BY je.Id, je.TenantId, je.JournalEntryNumber
HAVING SUM(atx.DebitAmount) <> SUM(atx.CreditAmount);
```

Cross-currency bank transfers or reconciliations attempted while `FIN-LIM-0021` remains open:

```sql
SELECT ct.Id, ct.TenantId, ct.TransactionNumber, ct.TransactionType, ct.Currency,
       src.Currency AS SourceBankCurrency, dst.Currency AS DestinationBankCurrency
FROM CashTransaction ct
LEFT JOIN BankAccounts src ON src.Id = ct.BankAccountId AND src.TenantId = ct.TenantId AND src.IsDeleted = 0
LEFT JOIN BankAccounts dst ON dst.Id = ct.ToBankAccountId AND dst.TenantId = ct.TenantId AND dst.IsDeleted = 0
WHERE ct.IsDeleted = 0
  AND ct.TransactionType = 3 -- CashTransactionType.Transfer
  AND src.Currency IS NOT NULL
  AND dst.Currency IS NOT NULL
  AND src.Currency <> dst.Currency;
```

## Tests

`tests/ErpSystem.Api.Tests/Services/Finance/FxRealizedUnrealizedRevaluationTests.cs` now contains 31 focused FX tests.

Scenario mapping:

| Scenario | Test method |
|---|---|
| AP realized FX gain/loss signs | `ApSettlementRateIncreasePostsRealizedLoss`, `ApSettlementRateDecreasePostsRealizedGain` |
| AR realized FX gain/loss signs | `ArSettlementRateIncreasePostsRealizedGain`, `ArSettlementRateDecreasePostsRealizedLoss` |
| Partial AP/AR proportional settlement | `PartialApSettlementCalculatesProportionalFx`, `PartialArReceiptCalculatesProportionalFx` |
| Multiple allocations at different historical rates | `ApPaymentAllocatedToMultipleInvoicesCalculatesFxPerAllocation`, `ArReceiptAllocatedToMultipleInvoicesCalculatesFxPerAllocation` |
| Same-rate and duplicate realized FX idempotency | `SameRateSettlementProducesNoRealizedFxJournal`, `DuplicateRealizedFxPostingReturnsExistingSettlement` |
| Missing and cross-tenant FX mappings | `MissingRealizedFxMappingRejectsSettlementPosting`, `MissingUnrealizedFxMappingRejectsRevaluationPosting`, `CrossTenantRealizedFxAccountMappingIsRejected`, `CrossTenantUnrealizedFxAccountMappingIsRejected` |
| Cross-tenant rate rejection | `CrossTenantExchangeRateSnapshotIsRejectedForSettlement`, `CrossTenantClosingRateIsRejectedForRevaluation` |
| Closed-period FX posting rejection | `ClosedPeriodRealizedFxSettlementIsRejectedThroughPostingEngine`, `ClosedPeriodRevaluationIsRejected` |
| Payment currency edge cases | `FunctionalOrThirdCurrencySettlementOfForeignInvoiceIsRejected` |
| Withholding does not break FX basis | `WithholdingSettlementDoesNotOverstateRealizedFxBasis` |
| AP/AR/bank unrealized signs and posting engine usage | `UnrealizedRevaluationPostsApArAndForeignBankSignsThroughPostingEngine`, `ForeignBankRateIncreasePostsBankDebitAndUnrealizedGain`, `ForeignBankRateDecreasePostsUnrealizedLossAndBankCredit` |
| Bank exposure source of truth | `ForeignBankRevaluationUsesPostedGlSnapshotsNotBankCurrentBalance` |
| Missing closing rate and rate type selection | `MissingClosingRateBlocksUnrealizedRevaluation`, `RevaluationUsesRequestedQuarterEndRateType` |
| Duplicate revaluation and reversal idempotency | `DuplicateRevaluationIsIdempotentAndReversalPostsInOpenPeriod` |
| Posted snapshot immutability after rate edits | `LaterExchangeRateEditDoesNotMutatePostedRealizedFxSnapshot`, `LaterExchangeRateEditDoesNotMutatePostedUnrealizedRevaluationSnapshot` |
| Repeat-period revaluation behavior | `NextPeriodRevaluationAfterUnreversedPriorBatchUsesPriorCarryingAdjustment`, `RevaluationAfterReversalDoesNotDoubleCountPriorAdjustment` |

Focused test result after hardening: `31/31` passed for `Batch=FinanceGoLive-FXSettlementRevaluation`.

## Limitations Register

- `FIN-LIM-0015`: resolved.
- `FIN-LIM-0001`: narrowed for FX exposure basis; AP/AR aging read-model work remains open.
- `FIN-LIM-0020`: remains open and non-next-batch-blocking for exchange-rate workflow routing.
- `FIN-LIM-0021`: added for cross-currency bank transfer and reconciliation hardening.
- `FIN-LIM-0022`: added for AP/AR cross-currency settlement where invoice currency differs from payment/receipt currency.

## Rollback Considerations

The migration can be rolled back before production FX postings. Rolling back after FX settlement/revaluation postings would remove audit-critical realized settlement and revaluation batch tables, so production rollback requires accountant-approved export and journal reconciliation first.
