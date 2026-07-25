# FX Batch 17 - Functional Currency And Exchange Rate Governance Foundation

Date: 2026-07-06

Scope boundary: this batch establishes tenant functional currency governance, tenant-owned exchange-rate governance, and posted currency/rate snapshots. It does not implement realized FX, unrealized revaluation, AP/AR settlement gain/loss journals, bank FX revaluation, revaluation UI, fixed assets, migration sign-off, or report/export packs.

## Source-Of-Truth Decision

The posted General Ledger remains the accounting source of truth. Functional-currency debit and credit amounts are posted through `IFinancePostingEngine`; transaction-currency amounts, functional currency, exchange-rate references, and rate snapshots are stored on posted `AccountTransaction` lines and summarized on `FinancePostingEvent`.

Later exchange-rate edits cannot reinterpret posted accounting. Used exchange rates are locked against destructive update/delete, and posted lines retain the rate value, effective date, rate source, and rate ID used at posting time.

## Tenant Functional Currency Model

Canonical functional currency is stored per tenant in `FinanceSettings.BaseCurrency`.

Files/classes/methods:

- `src/ErpSystem.Core/Entities/Finance/FinanceSettings.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceSettingsDtos.cs`
- `src/ErpSystem.Api/Services/Finance/Settings/FinanceSettingsService.cs`
  - `UpdateSettingsAsync`
  - `HasAccountingActivityAsync`
  - `MapToDto`
- `src/ErpSystem.Data/Migrations/20260706153000_AddFxFunctionalCurrencyAndRateSnapshots.cs`

Rules:

- Functional currency is tenant-scoped.
- `BaseCurrency` is normalized to a three-letter currency code.
- Functional currency can be changed before accounting activity exists.
- Accounting activity is detected from `FinancePostingEvents`, `JournalEntries`, and `AccountTransactions`.
- Once accounting activity exists, attempted functional-currency changes fail and are audited with `Finance.FunctionalCurrencyChangeRejected`.
- `FinanceSettings.FunctionalCurrencyLocked`, `FunctionalCurrencyLockedAt`, and `FunctionalCurrencyLockedReason` preserve lock state for operations and audit review.

## Exchange Rate Governance

Exchange rates remain tenant-owned records in `ExchangeRate`.

Files/classes/methods:

- `src/ErpSystem.Core/Entities/Finance/ExchangeRate.cs`
- `src/ErpSystem.Core/DTOs/Finance/ExchangeRateDtos.cs`
- `src/ErpSystem.Api/Services/Finance/MultiCurrency/ExchangeRateService.cs`
  - `GetCurrentRateAsync`
  - `CreateExchangeRateAsync`
  - `UpdateExchangeRateAsync`
  - `DeleteExchangeRateAsync`
  - `EnsureNoOverlappingRateAsync`
  - `IsRateUsedAsync`

Rules:

- Rates are tenant-scoped by `TenantId`.
- Source/target currency codes are normalized.
- Zero or negative rates are rejected.
- Active approved rates are resolved deterministically by transaction date, effective date, priority, and created date.
- Duplicate effective rates for the same tenant/source/target/rate type/effective date are blocked by a database unique index.
- Overlapping effective windows are rejected by service validation.
- Rates used in posted accounting are marked with `HasBeenUsedInTransactions`, usage dates, and `TransactionCount`.
- Used rates cannot be destructively updated or deleted.

## Posting Currency Snapshot Design

Files/classes/methods:

- `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`
- `src/ErpSystem.Core/Entities/Finance/AccountTransaction.cs`
- `src/ErpSystem.Core/Entities/Finance/FinancePostingEvent.cs`
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
  - `ResolveTenantFunctionalCurrencyAsync`
  - `ResolveExchangeRateSnapshotAsync`
  - `MarkExchangeRatesUsedAsync`
  - `RecordForeignCurrencyPostingBlockedAuditAsync`
  - `RecordCurrencySnapshotAuditAsync`

Posted line fields:

- `FunctionalCurrencyCode`
- `TransactionCurrency`
- `TransactionDebitAmount`
- `TransactionCreditAmount`
- `ForeignCurrencyAmount`
- `ExchangeRateId`
- `ExchangeRate`
- `ExchangeRateSource`
- `ExchangeRateDate`

Posting event fields:

- `FunctionalCurrencyCode`
- `HasForeignCurrencyLines`
- `PrimaryTransactionCurrencyCode`
- `PrimaryExchangeRateId`
- `PrimaryExchangeRate`
- `PrimaryExchangeRateDate`

Validation:

- Foreign-currency posting requires tenant functional currency configuration.
- Foreign-currency posting requires a tenant-owned, active, approved, positive exchange rate effective for the posting date.
- Caller-supplied exchange-rate values must match the resolved tenant rate.
- Functional-currency debit and credit totals must balance.
- Transaction-currency amounts are preserved for foreign-currency documents.
- Account currency restrictions are enforced where `Account.CurrencyCode` and `Account.IsMultiCurrency` are configured.
- Closed/locked period and idempotency checks remain enforced by `IFinancePostingEngine`.

## AP/AR/Cash Foundation Integration

The existing live AP, AR, and cash/bank posting callers now pass functional currency and transaction-currency data into `IFinancePostingEngine` where available:

- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorPaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/InvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs`

This batch does not implement realized FX on AP/AR settlement. Foreign-currency payment/receipt settlement gain/loss belongs to FX Batch 18.

## Audit Events

Added or verified Finance audit events in `src/ErpSystem.Shared/FinanceAuditEvents.cs`:

- `Finance.FunctionalCurrencyConfigured`
- `Finance.FunctionalCurrencyChangeRejected`
- `Finance.ExchangeRateCreated`
- `Finance.ExchangeRateUpdated`
- `Finance.ExchangeRateDeactivated`
- `Finance.ExchangeRateUsedInPosting`
- `Finance.ExchangeRateEditRejectedAfterUse`
- `Finance.ForeignCurrencyPostingBlockedInvalidRate`
- `Finance.CurrencySnapshotCapturedInPosting`

## Diagnostics

Run these diagnostics per environment before enabling foreign-currency production posting.

Tenants without functional currency:

```sql
SELECT t.Id AS TenantId, t.Name
FROM Tenants t
LEFT JOIN FinanceSettings fs ON fs.TenantId = t.Id AND fs.IsDeleted = 0
WHERE fs.Id IS NULL OR fs.BaseCurrency IS NULL OR LEN(fs.BaseCurrency) <> 3;
```

Tenants with accounting activity but unlocked functional currency:

```sql
SELECT fs.TenantId, fs.BaseCurrency, fs.FunctionalCurrencyLocked
FROM FinanceSettings fs
WHERE fs.IsDeleted = 0
  AND fs.FunctionalCurrencyLocked = 0
  AND (
      EXISTS (SELECT 1 FROM FinancePostingEvents fpe WHERE fpe.TenantId = fs.TenantId AND fpe.IsDeleted = 0)
      OR EXISTS (SELECT 1 FROM JournalEntries je WHERE je.TenantId = fs.TenantId AND je.IsDeleted = 0)
      OR EXISTS (SELECT 1 FROM AccountTransactions atx WHERE atx.TenantId = fs.TenantId AND atx.IsDeleted = 0)
  );
```

Zero or negative exchange rates:

```sql
SELECT Id, TenantId, BaseCurrencyCode, TargetCurrencyCode, Rate, EffectiveDate
FROM ExchangeRates
WHERE IsDeleted = 0 AND Rate <= 0;
```

Duplicate effective rates:

```sql
SELECT TenantId, BaseCurrencyCode, TargetCurrencyCode, RateType, EffectiveDate, COUNT(*) AS RateCount
FROM ExchangeRates
WHERE IsDeleted = 0
GROUP BY TenantId, BaseCurrencyCode, TargetCurrencyCode, RateType, EffectiveDate
HAVING COUNT(*) > 1;
```

Overlapping rate windows:

```sql
SELECT r1.Id AS RateId, r2.Id AS OverlappingRateId, r1.TenantId, r1.BaseCurrencyCode, r1.TargetCurrencyCode, r1.RateType
FROM ExchangeRates r1
JOIN ExchangeRates r2
  ON r1.TenantId = r2.TenantId
 AND r1.BaseCurrencyCode = r2.BaseCurrencyCode
 AND r1.TargetCurrencyCode = r2.TargetCurrencyCode
 AND r1.RateType = r2.RateType
 AND r1.Id <> r2.Id
 AND r1.IsDeleted = 0
 AND r2.IsDeleted = 0
 AND r1.EffectiveDate <= COALESCE(r2.EndDate, '9999-12-31')
 AND COALESCE(r1.EndDate, '9999-12-31') >= r2.EffectiveDate;
```

Foreign-currency posted lines missing snapshots:

```sql
SELECT atx.Id, atx.TenantId, atx.JournalEntryId, atx.TransactionCurrency, atx.FunctionalCurrencyCode, atx.ExchangeRateId
FROM AccountTransactions atx
WHERE atx.IsDeleted = 0
  AND atx.TransactionCurrency IS NOT NULL
  AND atx.TransactionCurrency <> atx.FunctionalCurrencyCode
  AND (
      atx.FunctionalCurrencyCode IS NULL
      OR atx.ExchangeRateId IS NULL
      OR atx.ExchangeRate IS NULL
      OR atx.ExchangeRateDate IS NULL
      OR (COALESCE(atx.TransactionDebitAmount, 0) = 0 AND COALESCE(atx.TransactionCreditAmount, 0) = 0)
  );
```

Posted lines referencing another tenant's exchange rate:

```sql
SELECT atx.Id, atx.TenantId AS LineTenantId, er.TenantId AS RateTenantId, atx.ExchangeRateId
FROM AccountTransactions atx
JOIN ExchangeRates er ON er.Id = atx.ExchangeRateId
WHERE atx.IsDeleted = 0
  AND er.IsDeleted = 0
  AND atx.TenantId <> er.TenantId;
```

Foreign-currency posting events whose functional-currency debits and credits do not balance:

```sql
SELECT fpe.Id, fpe.TenantId, fpe.SourceModule, fpe.SourceDocumentType, fpe.TotalDebitAmount, fpe.TotalCreditAmount
FROM FinancePostingEvents fpe
WHERE fpe.IsDeleted = 0
  AND fpe.HasForeignCurrencyLines = 1
  AND ROUND(fpe.TotalDebitAmount - fpe.TotalCreditAmount, 2) <> 0;
```

Foreign-currency posting events missing primary snapshot fields:

```sql
SELECT fpe.Id, fpe.TenantId, fpe.SourceModule, fpe.SourceDocumentType, fpe.SourceDocumentId
FROM FinancePostingEvents fpe
WHERE fpe.IsDeleted = 0
  AND fpe.HasForeignCurrencyLines = 1
  AND (
      fpe.PrimaryTransactionCurrencyCode IS NULL
      OR fpe.PrimaryExchangeRateId IS NULL
      OR fpe.PrimaryExchangeRate IS NULL
      OR fpe.PrimaryExchangeRateDate IS NULL
  );
```

## Test Coverage

Test class: `tests/ErpSystem.Api.Tests/Services/Finance/FxFunctionalCurrencyGovernanceTests.cs`

| Test method | Scenario |
|---|---|
| `TenantCanConfigureFunctionalCurrencyBeforeAccountingActivity` | Tenant can configure functional currency before accounting activity |
| `FunctionalCurrencyChangeAfterAccountingActivityIsRejectedAndAudited` | Functional currency change after accounting activity is rejected and audited |
| `ForeignCurrencyPostingWithoutFunctionalCurrencyConfigurationIsRejected` | Foreign-currency posting without functional currency configuration fails |
| `SameCurrencyPostingDoesNotRequireExchangeRateLookup` | Same-currency posting does not require exchange-rate lookup |
| `ForeignCurrencyPostingRequiresEffectiveTenantExchangeRate` | Foreign-currency posting requires an effective tenant-owned rate |
| `CrossTenantExchangeRateIsRejected` | Cross-tenant exchange-rate reference is rejected |
| `ExchangeRateServiceRejectsZeroNegativeAndOverlappingRates` | Zero/negative and overlapping rates are rejected |
| `PostingCapturesRateSnapshotLocksRateAndEmitsAudit` | Posting captures rate snapshot, locks the used rate, and emits audit |
| `UsedExchangeRateCannotBeEditedOrDeletedAndSnapshotRemainsStable` | Used rates cannot be destructively changed and posted snapshot remains stable |
| `DeterministicRateSelectionUsesEffectiveRateForPostingDate` | Rate selection uses transaction date and effective-date ordering |
| `ClosedPeriodForeignCurrencyPostingIsRejectedThroughPostingEngine` | Closed-period foreign-currency posting is rejected centrally |
| `AccountCurrencyRestrictionRejectsForeignPostingToSingleCurrencyAccount` | Single-currency account restrictions are enforced |

Focused result: `12/12` passed for `Batch=FinanceGoLive-FXFoundation`.

## Deferred Work

- `FIN-LIM-0015`: realized FX on AP/AR settlement, unrealized FX revaluation, bank FX account revaluation, cross-currency bank/reconciliation handling, and revaluation journals remain in FX Batch 18.
- `FIN-LIM-0020`: exchange-rate approval routing through the workflow engine remains a go-live control item. This batch stores/uses approval status and blocks unapproved rates, but does not implement workflow routing for rate approval.

## Definition Of Done

- [x] Tenant functional currency is configurable before accounting activity.
- [x] Functional currency changes after accounting activity are rejected and audited.
- [x] Exchange rates are tenant-scoped, effective-dated, positive, approval-aware, and usage-locked after posting.
- [x] Posting engine captures immutable currency/rate snapshots.
- [x] Same-currency posting remains valid without exchange-rate lookup.
- [x] Foreign-currency posting fails without valid tenant rate configuration.
- [x] Closed-period and idempotency behavior remain enforced by `IFinancePostingEngine`.
- [x] Migration added for functional currency lock and rate snapshot fields.
- [x] Limitations register updated.
- [x] Automated FX foundation tests added and passing.
