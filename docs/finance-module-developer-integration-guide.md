# Finance Module Developer Integration Guide

This guide is for backend, frontend, and integration developers who need to call Finance module features in dev/UAT.

This PR is an integration foundation, not production go-live approval. Production migration and accountant sign-off remain outside this PR.

The maintained integration artifacts are:

- [Finance Integration Contract Catalogue](Finance/finance-integration-contract-catalogue.md) — stable contract IDs, owners, versions and delivery status.
- [Finance Integration Adapter Checklist](Finance/finance-integration-adapter-checklist.md) — joint producer/Finance design and review checklist.
- [Finance Integration Consumer-Test Template](Finance/finance-integration-consumer-test-template.md) — reusable request-capture assertions and CI expectations.
- [FIN-INT-006 Fixed Asset Disposal Reference Contract](Finance/fixed-asset-disposal-ar-tax-cash-contract.md) — first complete orchestration example.
- [FIN-INT-015 Procurement Budget Commitment Contract](Finance/procurement-finance-budget-commitment-contract.md) — Finance-owned availability/reservation boundary for Procurement consumers.
- [Finance Coding Dimensions Architecture](Finance/finance-coding-dimensions-architecture.md) — structural-account versus transaction-dimension ownership, posting, reporting and budget rollout.

## Quick Integration Overview

Use this section when briefing developers who need to connect invoicing, procurement, sales, maintenance, projects, or any other operational module into Finance.

The Finance module is posting-engine driven. If another module creates a transaction with accounting impact, it must create or update its own source document first, then ask Finance to post that document through `IFinancePostingEngine.PostAsync(...)`. It must not create `JournalEntry`, `AccountTransaction`, `FinancePostingEvent`, AP/AR control-account rows, or settlement rows directly.

Good codebase examples:

- AR invoice posting: `src/ErpSystem.Api/Services/Finance/AR/InvoiceService.cs`
- AP invoice posting: `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- AP GRV/receipt posting: `src/ErpSystem.Api/Services/Finance/AP/FinancePurchaseOrderReceiptPostingService.cs`
- AP/AR subledger adjustment posting: `src/ErpSystem.Api/Services/Finance/SubledgerAdjustmentJournalService.cs`
- Finance API frontend service: `frontend/src/services/finance/finance-data.service.ts`
- AR frontend service: `frontend/src/services/ar-service.ts`
- AP frontend service: `frontend/src/services/accountsPayableService.ts`

### Required Posting Request Shape

Every posting-capable integration should build a `FinancePostingRequestDto` with:

- `SourceModule`
- `OriginModuleCode`
- `SourceDocumentType`
- `SourceDocumentId`
- `SourceDocumentTenantId`
- `SourceDocumentReference`
- `PostingAction`
- `PostingDate`
- `JournalType`
- `FunctionalCurrencyCode`
- deterministic `IdempotencyKey`
- balanced `FinancePostingLineDto` lines

`FinancePostingLineDto.Dimensions` is an additive Finance 1.1 input. During adapter certification it
is optional. A producer supplies dimension codes plus a configured value code or canonical source-
entity lineage; it must not select a stored Finance dimension-set ID. Finance resolves the immutable
set. Required dimension rules become posting errors only after the producer adapter is certified.

Source metadata is not optional. Audit trail, duplicate-posting protection, reversal planning, report drill-through, settlement diagnostics, and migration sign-off all depend on stable source metadata.

### Account Resolution Rules

Do not hard-code GL account IDs or account numbers inside integrating modules. Resolve accounts from Finance configuration, source-document setup, or validated source lines.

Common Finance Settings fields:

- `ControlAccountArId`
- `ControlAccountApId`
- `ControlAccountInventoryId`
- `ControlAccountGRVAccrualId`
- tax control accounts
- discount allowed/received accounts
- realized/unrealized FX accounts
- suspense account
- migration clearing account

For AP/AR, the current standard model uses configured AP/AR control accounts. Partner-specific AP/AR control accounts are not the current integration path unless Finance explicitly extends that model.

Control accounts should be touched only through the relevant subledger flow. A normal user-selected contra account should not be an AP/AR/inventory/tax control account.

### AP and AR Subledger Rule

If a transaction affects a customer or supplier balance, the integration must preserve both:

- the posted GL position through `IFinancePostingEngine`
- the AP/AR source or settlement data needed by aging, statements, detailed ledgers, allocation, reconciliation, and migration diagnostics

Do not treat GL posting alone as enough for AP/AR. Customer and supplier reports depend on posted source documents, posted allocations, credit notes, withholding, settlement read-model facts, and subledger adjustments.

### Invoicing Guidance

For AR:

- Use the Finance AR invoice flow for customer receivables.
- Normal AR invoices post through `InvoiceService.PostAsync(...)` or the existing send/post lifecycle.
- Use configured AR control and validated revenue/tax/inventory/COGS accounts.
- Do not post normal AR invoices through the legacy subledger posting service.

For AP:

- Use the Finance AP vendor invoice flow for supplier liabilities.
- Normal AP invoices post through `VendorInvoiceService.PostAsync(...)`.
- Use configured AP control and validated expense, inventory, GRV accrual, tax, discount, and fixed-asset accounts where applicable.
- PO/GRV integrations should follow the Finance purchase receipt to vendor invoice pattern instead of inventing a parallel invoice source.

Opening balances are special:

- Invoice-by-invoice customer/supplier opening balances should be loaded as opening AR/AP invoices when aging and allocation must operate at invoice level.
- One-line customer/supplier opening balances can use AP/AR subledger adjustment journals.
- Opening-balance AP/AR adjustments must use the configured migration clearing account as contra.
- Opening balances are not evidence of production readiness until accountant-reviewed cutover evidence exists.

### Frontend Integration Rules

Frontend Finance work should use the existing service layer instead of ad hoc `fetch`/`axios` calls:

- shared Finance APIs: `financeDataService`
- AR APIs: `arService`
- AP APIs: `accountsPayableService`

Finance screens live under `frontend/src/app/finance/...`. Keep lifecycle and navigation consistent with existing screens: draft, submit, approve, post, view journal, reverse/void where applicable. When a posting creates a journal, route users to the resulting journal entry when practical.

### Budget Commitment Integration

Finance is the source of truth for the adopted budget. Producer modules call
`IFinanceBudgetCommitmentService`; they must not write `BudgetEntry`,
`FinanceBudgetReservation` or `FinanceBudgetReservationOperation` directly and must not maintain
an unrelated accounting ceiling as authority.

Use the exact Finance `BudgetEntryId` selected from `GetEligibleBudgetCellsAsync`. Finance validates
the effective adopted scenario, fiscal period, budget-tracked expense account,
department/cost-centre combination, functional currency and approved exchange-rate evidence.
Availability is `approved - posted actual - active reservations`.

Producer lifecycle rules:

- evaluate drafts without mutation;
- reserve once when the controlled source reaches its agreed approved state;
- amend using an absolute target amount and the returned reservation version;
- release on rejection/cancellation/unused closure;
- inherit the requisition commitment at PO issue rather than reserving twice;
- reduce/consume only after the exact related Finance posting event commits;
- never write actuals through the commitment service—posted GL is the actual.

All mutations require deterministic idempotency and correlation keys. Retrying the same key and
payload returns the original result; using the key for changed evidence fails closed. See
[FIN-INT-015](Finance/procurement-finance-budget-commitment-contract.md) for the full lifecycle,
currency, receipt/GRNI, supplier-invoice and reversal rules.

### Developer Checklist for New Integrations

Before merging a feature that touches Finance:

- Confirm the source document is tenant-scoped.
- Confirm the source document has a stable ID and human-readable document number/reference.
- Confirm the lifecycle status allows posting.
- Confirm posting is idempotent with a deterministic idempotency key.
- Confirm all accounts are active, same-tenant, and valid for direct posting unless they are controlled by a subledger flow.
- Confirm the fiscal period is open/unlocked for the posting date.
- Confirm the posting request is balanced in functional currency.
- Confirm returned `JournalEntryId` and posting references are saved as repairable back-references.
- Confirm AP/AR transactions update or feed the settlement/reporting read models expected by aging and statements.
- Add regression tests proving the integration uses `IFinancePostingEngine`.
- Use `ShouldSatisfyPostingContract(...)` from the shared consumer-test assertions.
- Add negative tests for cross-tenant account/source references and duplicate posting.

### One-Sentence Rule

Create the operational document in its owning module, then post it through the Finance posting engine with stable source metadata, configured accounts, tenant validation, idempotency, and subledger/reporting support where applicable.

## Integration Status

- Ready for dev/UAT integration.
- Not approved as production migration evidence.
- Contract availability is tracked by `FIN-INT-###` in the Finance Integration Contract Catalogue; a planned entry is not a callable promise.
- `FIN-LIM-0017` remains open until representative tenant dry-run and accountant-reviewed evidence exist.
- `FIN-LIM-0048` is implementation-resolved following merged PR #66; representative advance/WHT/fixed-asset cutover rehearsal and accountant sign-off remain release gates.

## Core Rule

Normal Finance posting must go through `IFinancePostingEngine`.

Do not create `JournalEntry`, `AccountTransaction`, or `FinancePostingEvent` records directly from AP, AR, cash/bank, tax, FX, fixed asset, opening-balance, or migration workflows.

Approved boundaries:

- `IFinancePostingEngine.PostAsync(...)` for operational/subledger postings.
- `JournalEntryService` lifecycle for controlled manual journals.
- `IOpeningBalanceService` for controlled GL opening-balance batches.
- `IMigrationSignOffService` diagnostics/repair APIs for UAT migration readiness.

Forbidden or guarded:

- Legacy `ISubledgerPostingService` must not be used by normal runtime flows.
- Direct `GeneralLedgerService.PostJournalEntryAsync` is a disabled legacy path.
- `Account.Balance` and `BankAccount.CurrentBalance` are read-side snapshots only and must not be used as posting inputs.

## Main Backend Entry Points

### Posting Engine

- Interface: `IFinancePostingEngine`
- DTOs: `FinancePostingRequestDto`, `FinancePostingLineDto`, `FinancePostingResultDto`
- Purpose: create posted `JournalEntry`, `AccountTransaction`, and `FinancePostingEvent` atomically.

Required caller behavior:

- Provide tenant-scoped source document identifiers.
- Provide source module, source document type, source document ID, posting action, and idempotency key.
- Validate source document status before posting.
- Preserve returned journal and posting-event references on the source document.

### AP

- Controllers:
  - `api/ap/invoices`
  - `api/ap/payments`
  - `api/ap/payment-batches`
  - `api/ap/reports`
- Services:
  - `IVendorInvoiceService`
  - `IVendorPaymentService`
  - `IApReportsService`

AP invoice and payment posting uses the posting engine. AP aging/control reports use the settlement read model, not mutable paid fields.

### AR

- Controllers:
  - `api/ar/invoices`
  - `api/ar/payments`
  - `api/ar/reports`
  - `api/ar/customers`
- Services:
  - `IInvoiceService`
  - `IPaymentService`
  - `IArReportsService`

Current AR source tables are `Invoices` and `InvoiceLineItem`. Legacy `ContractorInvoice` is not the Finance AR invoice source model.

AR aging/control reports use the settlement read model, not mutable paid/credited fields.

### Cash and Bank

- Controllers:
  - `api/finance/bank-accounts`
  - `api/finance/cash-transactions`
  - `api/finance/bank-reconciliation`
  - `api/finance/cash-reports`

Cash/bank postings use the posting engine. Bank stored balances are read-side snapshots and must reconcile to posted GL movement.

### Fixed Assets

- Controllers:
  - `api/finance/fixed-assets`
  - `api/finance/fixed-asset-categories`
- Services:
  - `IFixedAssetService`
  - `IFixedAssetDepreciationService`
  - `IFixedAssetReportsService`

Supported backend lifecycle foundation:

- acquisition/capitalization
- straight-line depreciation
- revaluation and impairment foundation
- custody/location/segment transfer
- whole-asset disposal/write-off/sale disposal
- reporting and GL reconciliation

Unsupported paths fail clearly and remain tracked in the limitations register.

### Tax

- Controllers:
  - `api/finance/tax`
  - `api/finance/tax/rules`
  - `api/finance/tax-reports`

Tax reports use posted source documents, `TaxCalculation` snapshots, posted GL tax lines, and same-tenant tax accounts. Do not recalculate historical tax using current rates.

### Reporting and Export

- Controller: `api/finance/report-exports`
- Interface: `IFinanceReportExportService`

Exports use the same backend report services as view paths. CSV is the supported foundation format for core backend reports.

### Opening Balances and Migration Sign-Off

- Controllers:
  - `api/finance/opening-balances`
  - `api/finance/migration-signoff`
- Interfaces:
  - `IOpeningBalanceService`
  - `IMigrationSignOffService`

Opening balances support controlled balanced GL trial-balance batches through the posting engine. Do not use `ALL_ACTIVE_BOOKS`.

Migration sign-off can run UAT diagnostics and evidence generation, but production go-live approval requires real accountant-reviewed cutover evidence.

## Workflow and Permissions

Use the existing workflow and Finance permission infrastructure.

High-risk actions now route through workflow where configured:

- exchange-rate approval
- fixed asset direct capitalization
- depreciation runs
- revaluation/impairment
- opening-balance posting

Workflow approval does not replace Finance permissions. Finance permission alone does not bypass required workflow.

## Tenant Isolation

All Finance calls must be tenant-scoped.

Callers must not pass cross-tenant account, supplier, customer, source-document, tax, bank, segment, or fixed-asset IDs. Tenant guardrails reject or flag cross-tenant references.

## Migration and UAT Notes

Current UAT technical smoke result:

- database: `RhemaERP_UAT_DryRun`
- tenant: `Default Tenant`
- run: `SIGNOFF-20260710010353-7bd1d05b`
- status: `PassedWithAcceptedLimitations`
- blockers: `0`
- warnings: `1`

This is not production evidence.

Real cutover requires approved business/accounting inputs:

- cutover date
- fiscal period
- book classification
- balanced opening trial balance
- AP/AR/fixed-asset/bank declarations
- limitation acceptance matrix
- tenant-specific `FIN-LIM-0048` decision

## Developer Checklist

Before wiring a feature to Finance:

- Confirm the source document has a tenant ID.
- Confirm the source document has a stable ID and document number/reference.
- Confirm posting is idempotent.
- Confirm workflow status allows posting.
- Confirm the fiscal period is open.
- Confirm all accounts are same-tenant, active, and direct-posting where required.
- Call the service/controller boundary, not direct GL tables.
- Persist returned `JournalEntryId` and `FinancePostingEventId`.
- Add a regression test proving the call uses `IFinancePostingEngine`.
- Add a tenant-isolation negative test for cross-tenant references.
