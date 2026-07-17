# Sales/Finance Business-Partner Identity Consolidation

## Purpose

Sales return, credit-note, and refund documents previously retained a legacy CRM `Customer` key while Finance AR invoices use the tenant-scoped `BusinessPartner` key. This change removes that identity mismatch at the accounting boundary so a Sales credit cannot be applied to an unrelated Finance AR counterparty.

## Design

- `ReturnOrder`, `CreditNote`, and `Refund` now persist `BusinessPartnerId` and configure a foreign key to `BusinessPartners`.
- A return derives its business partner from its same-tenant source `SalesOrder`; callers cannot supply the counterparty.
- Credit-note and refund creation require an active same-tenant business partner and validate that linked return orders, invoices, and credit notes use that same partner.
- Invoice-linked credit notes remain restricted to their original Finance invoice.
- A standalone credit note may be applied only after Finance posting and only to an explicitly selected, posted Finance invoice for the same tenant and business partner.
- Workflow displays and the return/credit/refund API DTOs use the canonical identity. Query filters are now named `businessPartnerId`.

## Migration And Dev-Data Policy

Migration `20260717090000_UseBusinessPartnersForSalesReturnAccounting` renames the three accounting-document foreign keys and creates BusinessPartner foreign keys.

It intentionally refuses to run while `ReturnOrders`, `CreditNotes`, or `Refunds` contain data. A legacy CRM `CustomerId` cannot safely be treated as a Finance `BusinessPartnerId`; in development, those test documents must be reset and recreated through the current API and workflow paths. The migration does not manufacture an ID mapping or mutate posted GL.

This is a development-stage schema cut. A production database with retained legacy return documents would require a separately approved, tenant-safe customer-to-business-partner mapping and reconciled migration plan.

## Accounting And Tenant Controls

- Posted GL and `FinancePostingEvent` remain the source of truth.
- The change does not alter credit-note debit/credit accounting or bypass `IFinancePostingEngine`.
- Cross-tenant source sales orders, delivery notes, business partners, invoices, credit notes, and refunds are rejected.
- A standalone credit note cannot be applied without a target Finance invoice, cannot target another partner's invoice, and cannot over-settle an invoice with prior posted receipts or Sales credit notes.
- The compiled but disabled `ISubledgerPostingService.PostSalesCreditNoteAsync` legacy path now references `BusinessPartner` so it cannot break the build or reintroduce the old identity model.

## Interface Impact

- `CreateReturnOrderDto` no longer accepts `customerId` or `businessPartnerId`; the source Sales order supplies the counterparty.
- Credit-note and refund create/detail DTOs use `businessPartnerId`.
- Return-order, credit-note, and refund list endpoints now accept `businessPartnerId` rather than `customerId`.
- Consumers must refresh generated API clients or update request/response contracts before calling these endpoints.

## Verification

- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj -c Release --no-restore -clp:ErrorsOnly`: passed with 6 pre-existing warnings.
- Focused AR tests: `50/50` passed for credit-note posting, receipt posting, and settlement read-model coverage.
- Finance go-live regression slice: `419/419` passed for `Batch~FinanceGoLive`.
- `frontend` type-check was attempted with `npm.cmd run type-check`; it remains blocked by unrelated baseline errors in Fleet inspection, session timeout, and duplicate CRM service methods. No return-order type errors were reported.
- `tests/ErpSystem.Tests` remains structurally broken by pre-existing missing/renamed Finance types and interface members. The obsolete return-create DTO assignments were removed but this change does not repair that legacy suite.

## Limitation Register

- `FIN-LIM-0049` is resolved by the canonical `BusinessPartnerId` accounting identity and explicit same-partner application checks.
- `FIN-LIM-0045` is resolved for functional-currency advance reporting and application by `docs/ap-ar-advance-settlement-credit-note-reversal-pr-summary.md`.
- `FIN-LIM-0012` is resolved for immutable Sales credit-note reversal and correction accounting by `docs/ap-ar-advance-settlement-credit-note-reversal-pr-summary.md`.
