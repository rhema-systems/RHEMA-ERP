# Ghana Statutory Tax Engine and Tax Posting Foundation

## Scope

This batch hardens the existing Finance tax subsystem. It does not create a parallel posting path: AP invoices, AR invoices, AP payment WHT, and AR receipt withholding continue to post through `IFinancePostingEngine`.

Implemented or hardened files:

- `src/ErpSystem.Core/Enums/TaxEnums.cs`
- `src/ErpSystem.Core/Entities/Finance/TaxEntities.cs`
- `src/ErpSystem.Core/Entities/Finance/AccountsPayable.cs`
- `src/ErpSystem.Core/Entities/Finance/Invoice.cs`
- `src/ErpSystem.Core/Entities/Finance/CustomerPayment.cs`
- `src/ErpSystem.Core/Entities/Procurement/BusinessPartnerEntities.cs`
- `src/ErpSystem.Core/Entities/Procurement/ProcurementEntities.cs`
- `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/InvoiceDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/PaymentDtos.cs`
- `src/ErpSystem.Core/DTOs/AR/PaymentCrudDtos.cs`
- `src/ErpSystem.Api/Services/Finance/Taxation/TaxCalculationEngine.cs`
- `src/ErpSystem.Api/Services/Finance/Taxation/TaxConfigurationService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorPaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/InvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260706143000_AddExplicitFinanceTaxTreatmentAndWithholdingFields.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/GhanaStatutoryTaxEngineTests.cs`

## Statutory Assumptions

- COVID-19 Health Recovery Levy is abolished for current Ghana postings and must not be active for current effective dates.
- Standard Ghana VAT treatment is configured as VAT 15%, NHIL 2.5%, and GETFund 2.5% on the same taxable base, for a 20% total add-on where applicable.
- NHIL and GETFund can be configured as input-tax deductible where tenant policy and statutory treatment require it.
- VAT withholding and WHT use tenant-owned, effective-dated configuration and accounts.
- Rates are seeded/configured data. Posting code resolves tenant-owned effective-dated configuration and does not hardcode statutory rates.

## Configuration Model

- `Tax` defines tenant-owned tax codes, rates, category, applicability, deductibility, effective date, and optional payable/receivable accounts.
- `TaxRateHistory` preserves previous rates when a rate is changed for a future effective date.
- `TaxGroup` and `TaxGroupComponent` define tenant-owned sales/purchase groups and component ordering.
- `TaxRule` remains the routing layer for transaction/customer/service/product conditions.
- `TaxCalculation` stores source-document tax calculation snapshots for posted AP/AR tax lines.
- `TaxTreatment` is now explicit at invoice-line level: `Standard`, `Exempt`, `ZeroRated`, `OutOfScope`, with `NonTaxable` as an alias for `OutOfScope`.

AP and AR line tax modeling is now aligned:

- AR `InvoiceLineItem` has `TaxGroupId` and `TaxTreatment`.
- AP `VendorInvoiceLineItem` has `TaxGroupId` and `TaxTreatment`.
- Exempt, zero-rated, and out-of-scope lines must carry zero tax amount/rate and do not create tax posting lines.

## Posting Design

AP invoice posting:

- Calculates VAT/NHIL/GETFund through `ITaxCalculationEngine`.
- Debits configured recoverable/input tax receivable accounts for deductible tax.
- Includes non-deductible tax in the expense/asset line account when the tax breakdown marks it non-deductible.
- Fails if configured tax calculation does not reconcile to the invoice tax total.
- Fails if a required tax account is missing or belongs to another tenant.
- Does not fall back to a generic tenant tax control account for new/current postings.

AR invoice posting:

- Calculates VAT/NHIL/GETFund through `ITaxCalculationEngine`.
- Credits configured output tax payable accounts.
- Fails if configured tax calculation does not reconcile to the invoice tax total.
- Fails if a required tax account is missing or belongs to another tenant.
- Does not fall back to a generic tenant tax control account for new/current postings.

AP payment WHT:

- Existing AP payment/allocation WHT amounts post through `IFinancePostingEngine`.
- WHT credits the explicit payment WHT account or a configured tenant WHT payable account.
- Generic tax-control fallback was removed from the live AP payment WHT path.

AR receipt withholding:

- Minimal backend fields now support WHT and VAT withholding amount, tax reference, account reference, certificate/reference number, and certificate/reference date.
- WHT and VAT withholding debit configured tenant receivable/clearing accounts.
- Cash received plus WHT/VAT withholding must reconcile to the receipt allocation before posting.
- Certificate/reference reporting and CSV export are implemented in `docs/ghana-statutory-tax-reporting-export-foundation.md`; portal-specific filing packs and full certificate issuance/receipt workflow remain tracked in `FIN-LIM-0047`.

## Audit and Snapshots

Implemented Finance audit event calls:

- `Finance.Tax.RuleCreated` in `TaxConfigurationService.CreateTaxAsync`
- `Finance.Tax.RuleUpdated` in `TaxConfigurationService.UpdateTaxAsync`
- `Finance.Tax.RuleDeactivated` in `TaxConfigurationService.DeleteTaxAsync`
- `Finance.Tax.AccountMappingChanged` in `TaxConfigurationService.UpdateTaxAsync`
- `Finance.Tax.CalculatedOnAPInvoice` in `VendorInvoiceService.PostAsync`
- `Finance.Tax.CalculatedOnARInvoice` in `InvoiceService.PostAsync`
- `Finance.Tax.Posted` in AP/AR invoice posting after successful posting-engine result
- `Finance.Tax.PostingFailed` in AP/AR invoice posting failure paths
- `Finance.Tax.ConfigurationUsedInPosting` in AP/AR invoice posting

Posted AP/AR tax calculations are snapshotted in `TaxCalculations` using tenant, document type, document ID, tax ID, tax group ID, taxable base, applied rate, tax amount, compound basis, calculation order, calculation date, and manual-override flag. Later tax configuration edits must not mutate posted journals or existing snapshots.

## Diagnostics

Run these checks per tenant before tax sign-off. Table names match current EF mappings: `Taxes`, `TaxRateHistory`, `TaxGroups`, `TaxGroupComponents`, `TaxCalculations`, `VendorInvoice`, `VendorInvoiceLineItem`, `VendorPayment`, `CustomerPayment`, `Invoices`, `InvoiceLineItem`, `JournalEntries`, `AccountTransactions`, `FinancePostingEvents`, and `Accounts`.

```sql
-- Active tax rules with missing GL accounts.
select TenantId, Code, Name, Category, Applicability, TaxPayableAccountId, TaxReceivableAccountId
from Taxes
where IsDeleted = 0
  and IsActive = 1
  and (
    (Category in (1, 2, 7, 8) and (TaxPayableAccountId is null or TaxReceivableAccountId is null))
    or (Category = 3 and TaxPayableAccountId is null)
  );

-- True overlapping effective-dated rates for a tax.
select h1.TenantId, h1.TaxId, h1.EffectiveFrom, h1.EffectiveTo, h2.EffectiveFrom as OverlapFrom, h2.EffectiveTo as OverlapTo
from TaxRateHistory h1
join TaxRateHistory h2
  on h2.TenantId = h1.TenantId
 and h2.TaxId = h1.TaxId
 and h2.Id <> h1.Id
 and h2.IsDeleted = 0
where h1.IsDeleted = 0
  and h1.EffectiveFrom <= coalesce(h2.EffectiveTo, '9999-12-31')
  and h2.EffectiveFrom <= coalesce(h1.EffectiveTo, '9999-12-31');

-- Tax accounts from another tenant or missing account rows.
select t.TenantId, t.Code, 'Payable' as AccountRole, t.TaxPayableAccountId as AccountId, a.TenantId as AccountTenantId
from Taxes t
left join Accounts a on a.Id = t.TaxPayableAccountId
where t.IsDeleted = 0
  and t.TaxPayableAccountId is not null
  and (a.Id is null or a.TenantId <> t.TenantId)
union all
select t.TenantId, t.Code, 'Receivable', t.TaxReceivableAccountId, a.TenantId
from Taxes t
left join Accounts a on a.Id = t.TaxReceivableAccountId
where t.IsDeleted = 0
  and t.TaxReceivableAccountId is not null
  and (a.Id is null or a.TenantId <> t.TenantId);

-- Posted AP/AR documents with tax amounts but missing tax snapshots.
select vi.TenantId, 'VendorInvoice' as DocumentType, vi.Id, vi.InvoiceNumber, vi.TaxAmount
from VendorInvoice vi
where vi.TaxAmount > 0
  and exists (
    select 1 from FinancePostingEvents e
    where e.TenantId = vi.TenantId
      and e.SourceDocumentType = 'VendorInvoice'
      and e.SourceDocumentId = vi.Id
      and e.PostingAction = 'Post'
      and e.PostingStatus = 'Posted'
  )
  and not exists (
    select 1 from TaxCalculations tc
    where tc.TenantId = vi.TenantId
      and tc.DocumentType = 'VendorInvoice'
      and tc.DocumentId = vi.Id
      and tc.IsDeleted = 0
  )
union all
select i.TenantId, 'CustomerInvoice', i.Id, i.InvoiceNumber, i.TaxAmount
from Invoices i
where i.TaxAmount > 0
  and exists (
    select 1 from FinancePostingEvents e
    where e.TenantId = i.TenantId
      and e.SourceDocumentType = 'CustomerInvoice'
      and e.SourceDocumentId = i.Id
      and e.PostingAction = 'Post'
      and e.PostingStatus = 'Posted'
  )
  and not exists (
    select 1 from TaxCalculations tc
    where tc.TenantId = i.TenantId
      and tc.DocumentType = 'CustomerInvoice'
      and tc.DocumentId = i.Id
      and tc.IsDeleted = 0
  );

-- Posted AP/AR tax snapshots without valid same-tenant tax configuration.
select tc.TenantId, tc.DocumentType, tc.DocumentId, tc.TaxId, tc.TaxGroupId
from TaxCalculations tc
left join Taxes t on t.Id = tc.TaxId and t.TenantId = tc.TenantId and t.IsDeleted = 0
left join TaxGroups tg on tg.Id = tc.TaxGroupId and tg.TenantId = tc.TenantId and tg.IsDeleted = 0
where tc.IsDeleted = 0
  and (t.Id is null or (tc.TaxGroupId is not null and tg.Id is null));

-- VAT/NHIL/GETFund snapshot mismatch against posted AP/AR document tax totals.
select TenantId, DocumentType, DocumentId, sum(TaxAmount) as SnapshotTaxAmount
from TaxCalculations
where IsDeleted = 0
  and DocumentType in ('VendorInvoice', 'CustomerInvoice')
group by TenantId, DocumentType, DocumentId
having abs(sum(TaxAmount) - (
    case
      when DocumentType = 'VendorInvoice' then (
        select coalesce(max(TaxAmount), 0) from VendorInvoice vi
        where vi.TenantId = TaxCalculations.TenantId and vi.Id = TaxCalculations.DocumentId
      )
      when DocumentType = 'CustomerInvoice' then (
        select coalesce(max(TaxAmount), 0) from Invoices i
        where i.TenantId = TaxCalculations.TenantId and i.Id = TaxCalculations.DocumentId
      )
      else 0
    end
  )) > 0.01;

-- VAT withholding/WHT documents missing configured accounts.
select TenantId, 'VendorPayment' as DocumentType, Id, PaymentNumber, WithholdingTaxAmount
from VendorPayment
where IsDeleted = 0
  and WithholdingTaxAmount > 0
  and WithholdingTaxAccountId is null
union all
select TenantId, 'CustomerPayment-WHT', Id, PaymentNumber, WithholdingTaxAmount
from CustomerPayment
where IsDeleted = 0
  and WithholdingTaxAmount > 0
  and WithholdingTaxAccountId is null
union all
select TenantId, 'CustomerPayment-VAT-WHT', Id, PaymentNumber, VatWithholdingAmount
from CustomerPayment
where IsDeleted = 0
  and VatWithholdingAmount > 0
  and VatWithholdingAccountId is null;

-- Active current COVID-19 Health Recovery Levy configuration.
select TenantId, Code, Name, Rate, EffectiveFrom, IsActive
from Taxes
where IsDeleted = 0
  and IsActive = 1
  and EffectiveFrom <= cast(getutcdate() as date)
  and (Code like '%COVID%' or Name like '%COVID%' or Name like '%Health Recovery%');

-- Tax posting lines in closed or locked periods.
select at.TenantId, at.Id as AccountTransactionId, at.TransactionTag, je.Id as JournalEntryId, fp.PeriodCode, fp.PeriodStatus
from AccountTransactions at
join JournalEntries je on je.Id = at.JournalEntryId and je.TenantId = at.TenantId
join FiscalPeriods fp on fp.Id = je.FiscalPeriodId and fp.TenantId = at.TenantId
where at.IsDeleted = 0
  and je.IsDeleted = 0
  and (at.TransactionTag like 'AP-Tax-%' or at.TransactionTag like 'AR-Tax-%' or at.TransactionTag in ('AP-WHT', 'AR-WHT', 'AR-VAT-WHT'))
  and (fp.IsOpen = 0 or fp.IsClosed = 1 or fp.IsLocked = 1);
```

## Test Coverage

Test class: `tests/ErpSystem.Api.Tests/Services/Finance/GhanaStatutoryTaxEngineTests.cs`

| Scenario | Test method |
|---|---|
| Current Ghana VAT/NHIL/GETFund calculation from effective-dated config | `CurrentGhanaVatNhiltGetfund_ShouldCalculateFromEffectiveDatedTenantConfigWithoutCovidLevy` |
| COVID-19 levy inactive for current postings | `CurrentGhanaVatNhiltGetfund_ShouldCalculateFromEffectiveDatedTenantConfigWithoutCovidLevy` |
| Active current COVID-19 levy rejected | `TaxConfiguration_ShouldRejectCurrentActiveCovidLevyAndCrossTenantTaxAccounts` |
| AP configured input tax posts to configured same-tenant account | `ApInvoicePosting_ShouldUseConfiguredInputTaxAccountsAndCreateTaxSnapshots` |
| AR configured output tax posts to configured same-tenant account | `ArInvoicePosting_ShouldUseConfiguredOutputTaxAccountsAndCreateTaxSnapshots` |
| AP invoice total reconciles to net plus tax | `ApInvoicePosting_ShouldUseConfiguredInputTaxAccountsAndCreateTaxSnapshots` |
| AR invoice total reconciles to net plus tax | `ArInvoicePosting_ShouldUseConfiguredOutputTaxAccountsAndCreateTaxSnapshots` |
| Exempt line produces no tax posting | `ExplicitNoTaxTreatments_ShouldPostWithoutTaxLinesOrSnapshots` |
| Zero-rated line produces zero tax/no tax posting | `ExplicitNoTaxTreatments_ShouldPostWithoutTaxLinesOrSnapshots` |
| Out-of-scope/non-taxable line produces no tax posting | `ExplicitNoTaxTreatments_ShouldPostWithoutTaxLinesOrSnapshots` |
| Cross-tenant tax group rejected | `TaxCalculation_ShouldRejectCrossTenantTaxGroup` |
| Cross-tenant tax account rejected | `TaxConfiguration_ShouldRejectCurrentActiveCovidLevyAndCrossTenantTaxAccounts` |
| Cross-tenant manual tax selection rejected | `TaxCalculation_ShouldRejectCrossTenantManualTaxSelection` |
| Cross-tenant tax rule not applied | `TaxCalculation_ShouldNotApplyCrossTenantTaxRule` |
| Effective-date conflict/backdated destructive rate change rejected | `TaxConfiguration_ShouldRejectBackdatedRateChangesAndAuditConfigurationEvents` |
| Historical transaction date uses historical effective rate | `TaxCalculation_ShouldUseHistoricalEffectiveRateAndDeterministicRounding` |
| Deterministic rounding | `TaxCalculation_ShouldUseHistoricalEffectiveRateAndDeterministicRounding` |
| Closed-period tax posting rejected through `IFinancePostingEngine` | `ClosedPeriodTaxPosting_ShouldBeRejectedThroughFinancePostingEngine` |
| Invalid tax configuration fails instead of generic-control fallback | `ApAndArInvoicePosting_ShouldRejectInvalidTaxConfigurationInsteadOfFallingBack` |
| Later tax config edits do not mutate posted tax snapshots/accounting | `PostedTaxSnapshots_ShouldRemainUnchangedAfterLaterTaxConfigurationEdits` |
| Tax posting creates Finance audit events | `ApInvoicePosting_ShouldUseConfiguredInputTaxAccountsAndCreateTaxSnapshots`; `ArInvoicePosting_ShouldUseConfiguredOutputTaxAccountsAndCreateTaxSnapshots` |
| AP payment WHT uses configured payable account | `ApPaymentPosting_ShouldUseConfiguredWithholdingPayableAccount` |
| AR receipt WHT/VAT withholding uses configured receivable accounts | `ArReceiptPosting_ShouldUseConfiguredVatWithholdingAndWhtReceivableAccounts` |
| Tax config audit events are emitted | `TaxConfiguration_ShouldRejectBackdatedRateChangesAndAuditConfigurationEvents` |

Current result: `17/17` Ghana tax test cases pass.

## Known Limitations

All known limitations are tracked centrally in `docs/finance-go-live-limitations-register.md`.

- `FIN-LIM-0003`: Resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md` for backend Ghana statutory tax reports and CSV exports.
- `FIN-LIM-0004`: Resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md` for backend WHT/VAT withholding certificate/reference reporting and CSV exports.
- `FIN-LIM-0047`: Portal-specific filing pack formats and full withholding certificate issuance/receipt workflow remain open if required before final sign-off.
- `FIN-LIM-0019`: The AP/AR generic tax-control fallback for new/current invoices is resolved by this hardening pass.

## PR Definition Of Done

- Backend build passes through isolated output directory because a local `ErpSystem.Api` process is locking normal debug binaries.
- Ghana tax batch tests pass.
- Finance go-live regression slice passes.
- Migration `20260706143000_AddExplicitFinanceTaxTreatmentAndWithholdingFields` is present for model changes.
- No frontend changes.
- No separate tax posting path.
- Tenant isolation enforced for tax groups, tax rules, tax accounts, AP/AR posting, WHT/VAT withholding accounts, snapshots, and audit events.
- `docs/finance-go-live-limitations-register.md` is updated for new/resolved limitations.
