# Fixed Asset Acquisition And Capitalization Foundation

Date: 2026-07-07

Scope boundary: this batch implements tenant-scoped fixed asset acquisition and capitalization only. It does not implement depreciation runs, revaluation, impairment, transfers, disposals, broad fixed asset reporting, fixed asset frontend UI, data migration/sign-off, or print/export.

## Architecture Decision

Posted GL remains the accounting source of truth. Fixed asset register values are operational subledger records tied back to posted journal entries and `FinancePostingEvent` records.

Capitalization journals are posted through `IFinancePostingEngine`; this batch does not introduce a parallel fixed asset posting path and does not mutate `Account.Balance`.

## Files And Modules

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAsset.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetBookValue.cs`
- `src/ErpSystem.Core/Entities/Finance/AccountsPayable.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFixedAssetService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetsController.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetCategoryService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260706183000_AddFixedAssetCapitalizationFoundation.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetCapitalizationFoundationTests.cs`

## Asset Lifecycle

Batch 19 supports these states and transitions:

- `Draft`: asset metadata can be created and edited.
- `Acquired` / `Capitalized`: asset cost is tied to a posted capitalization journal and posting event.
- `Active`: asset can be activated only after capitalization or a controlled opening/import basis.

Capitalized assets reject destructive cost edits. Corrections must be handled later through controlled reversal or adjustment journals.

## Asset Category And Account Mapping

Each fixed asset category is tenant-scoped. Capitalization requires an active same-tenant fixed asset cost account that allows direct posting.

Category account mapping validation now checks:

- fixed asset cost account: same tenant, active, direct-posting, asset account
- accumulated depreciation placeholder: same tenant, active, direct-posting, asset account
- depreciation expense placeholder: same tenant, active, direct-posting, expense account
- impairment, disposal, revaluation, and AUC/CIP placeholders: same-tenant account validation where configured

Posting logic never hardcodes asset accounts.

## AP Invoice Asset Acquisition

AP invoice lines can be explicitly marked as fixed asset/capitalizable using `LineItemType` and `FixedAssetId`.

Rules:

- A fixed asset AP line must reference a same-tenant asset.
- The asset must have a same-tenant category with a valid fixed asset cost account.
- The AP invoice posts through `IFinancePostingEngine`.
- Fixed asset lines debit the configured asset cost account and credit AP control.
- Fixed asset lines do not also debit an expense account.
- Capitalization is idempotent for the same AP invoice line and posting event.
- The AP line stores `CapitalizationJournalEntryId`, `CapitalizationPostingEventId`, and `CapitalizedAt`.
- The asset stores source document, source line, journal entry, posting event, functional currency, transaction currency, exchange-rate snapshot, and capitalization date.

Fixed asset capitalization through GRV/procurement accrual clearing is guarded and deferred. Unsafe GRV-linked fixed asset capitalization fails clearly instead of silently posting.

## Direct Capitalization

Direct capitalization is exposed through `POST /api/finance/fixed-assets/{id}/capitalize`.

Rules:

- Requires `IFinancePostingEngine`.
- Requires an explicit credit account or a same-tenant category AUC/CIP account.
- Debits the category fixed asset cost account.
- Credits the supplied credit account or category AUC/CIP account.
- Validates tenant, category, accounts, currency, posting date, open period, and idempotency through the posting engine.
- Blocks duplicate capitalization when an asset already has a capitalization posting event.

Direct capitalization workflow approval routing is not implemented in this batch and is tracked as `FIN-LIM-0029`.

## Tax Treatment

AP fixed asset lines use the Ghana tax foundation already implemented.

- Recoverable input VAT/NHIL/GETFund is not capitalized.
- Non-recoverable tax is capitalized only when configured tax treatment marks it non-recoverable.
- Tax accounts and tax groups remain tenant-scoped.
- This batch does not implement fixed asset statutory tax reporting.

## Currency And FX

Fixed asset book cost is stored in functional currency to tie to the GL. Foreign-currency acquisitions preserve:

- transaction currency
- functional currency
- exchange rate used
- exchange-rate ID where available
- exchange-rate date

Later exchange-rate edits do not mutate the asset cost or posted capitalization journal. Asset FX revaluation or translation is not implemented in this batch.

## Workflow And Permissions

This batch uses the existing authorization and audit infrastructure. It does not introduce a fixed-asset-specific approval system.

Current behavior:

- Fixed asset controller mutations remain protected by existing Finance fixed asset permissions.
- Category/account mapping changes are tenant-validated and audited.
- Direct capitalization is permission-controlled and audited.

Workflow-engine routing for direct capitalization approval remains a go-live limitation unless direct capitalization is disabled or leadership accepts the control design. See `FIN-LIM-0029`.

## Audit Events

The batch adds or verifies Finance audit event types for:

- `Finance.FixedAsset.Created`
- `Finance.FixedAsset.Updated`
- `Finance.FixedAsset.CategoryCreated`
- `Finance.FixedAsset.CategoryUpdated`
- `Finance.FixedAsset.CategoryAccountMappingChanged`
- `Finance.FixedAsset.Acquired`
- `Finance.FixedAsset.Capitalized`
- `Finance.FixedAsset.CapitalizationFailed`
- `Finance.FixedAsset.Activated`
- `Finance.FixedAsset.CapitalizationConfigurationUsed`
- `Finance.FixedAsset.CapitalizationBlockedClosedPeriod`
- `Finance.FixedAsset.CrossTenantRejected`

Audit events include tenant ID, source document references where applicable, and posting/journal references where available.

## Diagnostics

Run these against production-like data before fixed asset sign-off. Names follow the current EF table mappings.

### Assets With Invalid Tenant Or Category References

```sql
SELECT fa.Id, fa.TenantId, fa.AssetNumber, fa.FixedAssetCategoryId
FROM FixedAssets fa
LEFT JOIN Tenants t ON t.Id = fa.TenantId
LEFT JOIN FixedAssetCategories c ON c.Id = fa.FixedAssetCategoryId
WHERE t.Id IS NULL
   OR c.Id IS NULL
   OR c.TenantId <> fa.TenantId;
```

### Categories Missing Or Cross-Tenant Cost Accounts

```sql
SELECT c.Id, c.TenantId, c.Code, c.AssetCostAccountId
FROM FixedAssetCategories c
LEFT JOIN Accounts a ON a.Id = c.AssetCostAccountId
WHERE c.AssetCostAccountId IS NULL
   OR a.Id IS NULL
   OR a.TenantId <> c.TenantId
   OR a.IsActive = 0
   OR a.AllowDirectPosting = 0;
```

### Capitalized Assets Missing Posting References

```sql
SELECT fa.Id, fa.TenantId, fa.AssetNumber, fa.JournalEntryId, fa.PostingEventId
FROM FixedAssets fa
WHERE fa.CapitalizedAt IS NOT NULL
  AND (fa.JournalEntryId IS NULL OR fa.PostingEventId IS NULL);
```

### Fixed Asset Journals Missing Register References

```sql
SELECT je.Id, je.TenantId, je.EntryNumber, je.SourceDocumentType, je.SourceDocumentId
FROM JournalEntries je
WHERE je.SourceDocumentType IN ('FixedAsset', 'VendorInvoice')
  AND je.Status = 2
  AND NOT EXISTS (
      SELECT 1
      FROM FixedAssets fa
      WHERE fa.TenantId = je.TenantId
        AND fa.JournalEntryId = je.Id
  )
  AND NOT EXISTS (
      SELECT 1
      FROM VendorInvoiceLineItem vil
      WHERE vil.TenantId = je.TenantId
        AND vil.CapitalizationJournalEntryId = je.Id
  );
```

### AP Fixed Asset Lines Missing Asset Linkage

```sql
SELECT vil.Id, vil.TenantId, vil.VendorInvoiceId, vil.LineItemType, vil.FixedAssetId
FROM VendorInvoiceLineItem vil
WHERE vil.LineItemType IN ('FixedAsset', 'Fixed Asset')
  AND vil.FixedAssetId IS NULL;
```

### AP Fixed Asset Lines Capitalized More Than Once

```sql
SELECT vil.TenantId, vil.Id AS VendorInvoiceLineItemId, COUNT(at.Id) AS RegisterTransactions
FROM VendorInvoiceLineItem vil
JOIN AssetTransactions at
  ON at.TenantId = vil.TenantId
 AND at.SourceDocumentLineId = vil.Id
WHERE vil.FixedAssetId IS NOT NULL
GROUP BY vil.TenantId, vil.Id
HAVING COUNT(at.Id) > 1;
```

### Active Assets Without Capitalization

```sql
SELECT fa.Id, fa.TenantId, fa.AssetNumber, fa.Status, fa.CapitalizedAt
FROM FixedAssets fa
WHERE fa.Status = 2
  AND fa.CapitalizedAt IS NULL
  AND fa.SourceDocumentType <> 'OpeningImport';
```

### Asset Register Cost Does Not Match Posted GL Cost Movement

```sql
SELECT fa.Id, fa.TenantId, fa.AssetNumber, fa.AcquisitionCost, gl.FunctionalCost
FROM FixedAssets fa
JOIN FixedAssetCategories c ON c.Id = fa.FixedAssetCategoryId AND c.TenantId = fa.TenantId
OUTER APPLY (
    SELECT SUM(CASE WHEN atx.DebitAmount > 0 THEN atx.DebitAmount ELSE -atx.CreditAmount END) AS FunctionalCost
    FROM AccountTransactions atx
    JOIN JournalEntries je ON je.Id = atx.JournalEntryId AND je.TenantId = atx.TenantId
    WHERE atx.TenantId = fa.TenantId
      AND atx.AccountId = c.AssetCostAccountId
      AND je.Status = 2
      AND (atx.Reference LIKE '%' + CONVERT(varchar(36), fa.Id) + '%'
           OR atx.Notes LIKE '%' + CONVERT(varchar(36), fa.Id) + '%'
           OR fa.JournalEntryId = je.Id)
) gl
WHERE fa.CapitalizedAt IS NOT NULL
  AND ABS(ISNULL(gl.FunctionalCost, 0) - fa.AcquisitionCost) > 0.01;
```

### Foreign-Currency Assets Missing Rate Snapshots

```sql
SELECT fa.Id, fa.TenantId, fa.AssetNumber, fa.TransactionCurrencyCode, fa.FunctionalCurrencyCode, fa.ExchangeRate
FROM FixedAssets fa
WHERE fa.TransactionCurrencyCode IS NOT NULL
  AND fa.FunctionalCurrencyCode IS NOT NULL
  AND fa.TransactionCurrencyCode <> fa.FunctionalCurrencyCode
  AND (fa.ExchangeRate IS NULL OR fa.ExchangeRate <= 0 OR fa.ExchangeRateDate IS NULL);
```

### Capitalization Journals Posted To Closed Or Locked Periods

```sql
SELECT je.Id, je.TenantId, je.EntryNumber, fp.Status
FROM JournalEntries je
JOIN FiscalPeriods fp ON fp.Id = je.FiscalPeriodId AND fp.TenantId = je.TenantId
WHERE je.SourceDocumentType IN ('FixedAsset', 'VendorInvoice')
  AND je.Status = 2
  AND fp.Status IN (2, 3);
```

### Unbalanced Capitalization Journals

```sql
SELECT atx.TenantId, atx.JournalEntryId, SUM(atx.DebitAmount) AS Debits, SUM(atx.CreditAmount) AS Credits
FROM AccountTransactions atx
JOIN JournalEntries je ON je.Id = atx.JournalEntryId AND je.TenantId = atx.TenantId
WHERE je.SourceDocumentType IN ('FixedAsset', 'VendorInvoice')
  AND je.Status = 2
GROUP BY atx.TenantId, atx.JournalEntryId
HAVING ABS(SUM(atx.DebitAmount) - SUM(atx.CreditAmount)) > 0.01;
```

### Potential Destructive Cost Edits

```sql
SELECT fa.Id, fa.TenantId, fa.AssetNumber, fa.AcquisitionCost, fa.JournalEntryId, fa.PostingEventId
FROM FixedAssets fa
WHERE fa.CapitalizedAt IS NOT NULL
  AND fa.JournalEntryId IS NOT NULL
  AND fa.PostingEventId IS NOT NULL;
```

Review the rows above against Finance audit logs for post-capitalization updates to cost fields.

## Tests

Focused suite: `FixedAssetCapitalizationFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetCapitalization`.

| Scenario | Test method |
|---|---|
| Draft asset creation is tenant-scoped and audited | `CreateDraftFixedAsset_ShouldBeTenantScopedAndAudited` |
| AP asset line posts to asset account and not expense | `ApInvoiceCapitalizableLine_ShouldPostAssetCostThroughPostingEngineAndNotExpense` |
| AP capitalization retry does not duplicate register transactions | `DuplicateApInvoiceCapitalizationRetry_ShouldNotDuplicateAssetRegisterTransactions` |
| Cross-tenant fixed asset reference rejected | `CrossTenantFixedAssetReference_ShouldBeRejectedBeforePosting` |
| Cross-tenant category account rejected | `CrossTenantAssetCategoryAccount_ShouldBeRejected` |
| Missing category cost account blocks direct capitalization | `CategoryMissingCostAccount_ShouldBlockDirectCapitalization` |
| Recoverable tax is not capitalized | `RecoverableTaxOnApAssetLine_ShouldNotBeCapitalized` |
| Non-recoverable tax is capitalized when configured | `NonRecoverableTaxOnApAssetLine_ShouldBeCapitalizedWhenTaxConfigMarksItNonRecoverable` |
| Foreign-currency AP asset acquisition preserves rate snapshot | `ForeignCurrencyApAssetAcquisition_ShouldPreserveCurrencyAndRateSnapshot` |
| Closed-period direct capitalization rejected through posting engine | `DirectCapitalizationIntoClosedPeriod_ShouldBeRejectedByPostingEngineAndAudited` |
| Direct capitalization without credit/AUC account is guarded | `DirectCapitalizationWithoutClearingAccountOrCreditAccount_ShouldBeGuarded` |
| Capitalized asset rejects destructive cost edit | `CapitalizedAsset_ShouldRejectDestructiveCostEdit` |
| Activation requires capitalization and preserves placed-in-service date | `AssetCannotActivateBeforeCapitalization_ButActivationAfterCapitalizationPreservesPlacedInServiceDate` |

## Migration And Rollback

Migration: `20260706183000_AddFixedAssetCapitalizationFoundation`.

Schema impact:

- Adds fixed asset linkage and capitalization back-references to `VendorInvoiceLineItem`.
- Adds source document, journal, posting event, currency, exchange-rate, and capitalization snapshot fields to `FixedAssets`.
- Adds capitalization/source references to `FixedAssetBookValues`.
- Adds tenant-aware indexes and foreign keys for asset/source/posting lookup.

Rollback is safe only before fixed asset capitalization postings are used. After production capitalization, rollback would remove audit-critical source-document, journal, posting-event, and currency snapshot links and would require accountant-approved export/reconciliation first.

## Limitations Register

- `FIN-LIM-0016`: narrowed; acquisition/capitalization foundation implemented, broader lifecycle remains open.
- `FIN-LIM-0023`: depreciation posting remains open.
- `FIN-LIM-0024`: revaluation/impairment remains open.
- `FIN-LIM-0025`: asset transfers remain open.
- `FIN-LIM-0026`: disposals remain open.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open.
- `FIN-LIM-0028`: procurement/GRV-origin capitalization remains open and is guarded.
- `FIN-LIM-0029`: direct capitalization workflow routing remains open.
- `FIN-LIM-0030`: capitalization reversal/adjustment remains open.

None of these open fixed asset limitations blocks starting the depreciation batch because the acquisition/capitalization accounting base is now available. They remain go-live blockers unless explicitly accepted by accounting/product leadership.

## Definition Of Done

- [x] Fixed asset acquisition/capitalization scope kept separate from depreciation and disposal lifecycle work.
- [x] Capitalization uses `IFinancePostingEngine`.
- [x] Fixed asset AP lines do not double-expense capitalized cost.
- [x] Recoverable and non-recoverable tax capitalization behavior is tested.
- [x] Functional/transaction currency snapshots are preserved for foreign acquisitions.
- [x] Tenant account/category/source checks are enforced.
- [x] Posted/capitalized asset cost is protected from destructive edits.
- [x] Audit events are added for key fixed asset events.
- [x] Diagnostics and limitations register entries are documented.
