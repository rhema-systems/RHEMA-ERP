# Finance Legacy Posting Path Lockdown and Posting-Engine Bypass Elimination - PR Summary

## Scope

Implemented posting-engine bypass lockdown only.

No frontend UI, data migration/sign-off execution, reversal redesign, workflow-routing hardening, or final go-live scenario pack was implemented.

## Files Changed

- `src/ErpSystem.Api/Controllers/Finance/FinanceController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinancePurchaseOrderController.cs`
- `src/ErpSystem.Api/Controllers/Finance/SupplierReturnsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Api/Program.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorPaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/InvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/SubledgerPostingService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/BankAccountService.cs`
- `src/ErpSystem.Api/Services/Finance/SubledgerAdjustmentJournalService.cs`
- `src/ErpSystem.Core/Interfaces/IGeneralLedgerService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerPostingService.cs`
- `src/ErpSystem.Core/Services/Sales/ReturnOrderService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/LegacyPostingPathLockdownTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- AP/AR/tax/fixed-asset focused tests updated for constructor changes where stale legacy mocks were passed.
- `docs/finance-legacy-posting-path-lockdown.md`
- `docs/finance-legacy-posting-path-lockdown-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Posting Path Inventory Summary

Normal runtime posting surfaces now classify as:

- AP invoice/payment: `IFinancePostingEngine`
- AR invoice/receipt/credit note: `IFinancePostingEngine`
- cash/bank receipt/payment/transfer: `IFinancePostingEngine`
- bank reconciliation adjustment: cash/bank posting path through `IFinancePostingEngine`
- subledger AP/AR adjustment journal: `IFinancePostingEngine`
- tax accounting: AP/AR/payment posting paths through `IFinancePostingEngine`
- FX settlement/revaluation: `IFinancePostingEngine`
- fixed asset capitalization/depreciation/valuation/disposal: `IFinancePostingEngine`
- manual journals: approved `JournalEntryService` lifecycle
- GRV receipt: `IFinancePostingEngine`
- supplier debit note: `IFinancePostingEngine`
- generic direct GL post endpoint: disabled and audited
- `GeneralLedgerService.PostJournalEntryAsync`: disabled
- `ISubledgerPostingService`: obsolete, non-registered, retained only as a non-runtime legacy reference
- opening-balance/migration posting: deferred under `FIN-LIM-0006`
- bank account nonzero opening-balance create path: disabled until `FIN-LIM-0006`

## Legacy Paths Removed, Disabled, Or Guarded

- Removed normal DI registration of `ISubledgerPostingService`.
- Removed normal runtime constructor dependencies on `ISubledgerPostingService`.
- Migrated GRV receipt posting to `IFinancePostingEngine`.
- Migrated supplier debit-note posting to `IFinancePostingEngine`.
- Disabled the old `post-finance-grv` maintenance command.
- Disabled generic direct GL posting through `FinanceController`.
- Disabled `GeneralLedgerService.PostJournalEntryAsync`.
- Migrated subledger AP/AR adjustment journal posting to `IFinancePostingEngine`.
- Disabled normal bank account nonzero opening-balance posting pending `FIN-LIM-0006`.
- Marked the legacy subledger interface/service obsolete and documented it as not a normal posting path.

## Audit Events

Added:

- `Finance.LegacyPostingPath.Blocked`
- `Finance.LegacyPostingPath.MigrationOnlyAttempted`
- `Finance.PostingEngine.BypassRejected`

Verified actual runtime call:

- `Finance.PostingEngine.BypassRejected` is emitted when the disabled generic direct GL posting endpoint is invoked.

## Tenant Isolation Verification

The migrated GRV and supplier debit-note posting helpers load tenant-owned source records, finance settings, accounts, currencies, and related source documents before creating a posting request.

Final same-tenant, account, currency, period, balance, idempotency, and posting-event validation remains centralized in `IFinancePostingEngine`.

## Accounting Impact

No new accounting method was introduced.

GRV receipt and supplier debit-note postings now create journals and posting events through the central posting engine rather than the old subledger service. Debit/credit accounting direction is preserved, but posting now inherits central period, tenant, currency, idempotency, and audit behavior.

## Migrations

No migration required.

## Tests

Added:

- `LegacyPostingPathLockdownTests.NormalRuntime_ShouldNotRegisterLegacySubledgerPostingService`
- `LegacyPostingPathLockdownTests.NormalRuntime_ShouldNotDependOnLegacySubledgerPostingService`
- `LegacyPostingPathLockdownTests.GenericFinanceControllerPostingEndpoint_ShouldRejectBypassInsteadOfCallingGeneralLedgerDirectPost`
- `LegacyPostingPathLockdownTests.CurrentFinancePostingServices_ShouldRouteThroughPostingEngine`
- `LegacyPostingPathLockdownTests.BankOpeningBalancePosting_ShouldRemainDisabledUntilPostingEngineMigration`

Updated constructor call sites in focused AP/AR/tax/fixed asset tests after removing stale legacy dependency parameters.

Results:

- API build: passed
- API test project build: passed
- focused legacy posting lockdown tests: `5/5`
- Finance service regression slice: `351/351`

Full solution build was attempted earlier but timed out in this workspace; targeted API and Finance regression verification completed successfully.

## Limitations Register

- `FIN-LIM-0018`: resolved for normal runtime Finance posting paths.
- `FIN-LIM-0006`: remains open for opening-balance and migration posting through a future controlled posting-engine batch.
- `FIN-LIM-0009`, `FIN-LIM-0010`, `FIN-LIM-0011`, `FIN-LIM-0012`, `FIN-LIM-0030`, `FIN-LIM-0033`, and `FIN-LIM-0037` remain open because reversal/correction accounting was not part of this batch.

## Rollback Considerations

Rollback is code-only. No schema or data migration was added.

Rollback would restore legacy DI registration, restore direct GL posting behavior, restore GRV/debit-note calls to `ISubledgerPostingService`, remove audit constants, and remove the lockdown architecture tests/docs.

## Safe To Proceed

Safe to proceed to workflow hardening or migration/sign-off preparation from a posting-engine boundary perspective.

Opening-balance/migration posting still needs the separate `FIN-LIM-0006` batch before final go-live sign-off.
