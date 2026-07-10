# Finance Module Developer Integration Guide

This guide is for backend, frontend, and integration developers who need to call Finance module features in dev/UAT.

This PR is an integration foundation, not production go-live approval. Production migration and accountant sign-off remain outside this PR.

## Integration Status

- Ready for dev/UAT integration.
- Not approved as production migration evidence.
- `FIN-LIM-0017` remains open until representative tenant dry-run and accountant-reviewed evidence exist.
- `FIN-LIM-0048` remains globally open unless real cutover data proves source-level AP/AR/fixed-asset openings are not required or those openings are loaded through proper source-document/import paths.

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
