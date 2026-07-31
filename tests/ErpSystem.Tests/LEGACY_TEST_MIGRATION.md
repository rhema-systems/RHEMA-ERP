# Legacy test migration register

## Purpose

`ErpSystem.Tests` contains an active compatible subset and a set of historical
finance tests written against services and repositories that no longer exist.
The historical files are explicitly excluded in `ErpSystem.Tests.csproj`.
This is a controlled quarantine, not a wildcard exclusion: any new test file is
compiled by default and can fail the build.

The current production application uses `ApplicationDbContext`,
module-owned services, and `IFinancePostingEngine`. The quarantined tests use
removed contracts such as `IFinancialRepository`, `IGLSegmentSecurityService`,
`SubledgerJournalService`, `CreditNotePostingService`, and generic repository
constructors that the current services no longer accept.

The maintained subset currently contains 66 passing tests. In particular,
`UnitTypeServiceTests`, `UnitAccountServiceTests`, and
`UnitJournalEntryServiceTests` were migrated to `IGenericRepository<T>`, EF
async query providers, the current workflow/numbering collaborators, and the
transactional posting/reversal contract instead of being quarantined.

## Quarantined files and current coverage

| Historical test area | Files | Current replacement or disposition |
|---|---|---|
| General ledger and journal lifecycle | `GeneralLedgerServiceTests`, `JournalEntryReversalTests`, `JournalEntryServicePeriodTests` | Superseded by `JournalEntryLifecycleBatch5Tests`, `FinancePostingEngineTests`, `AccountingPeriodClosePostingDateTests`, `ControlledOpeningBalancePostingTests`, and journal-batch rollback/concurrency tests in `ErpSystem.Api.Tests`. |
| Journal attachments and budget checks | `JournalEntryAttachmentTests`, `JournalEntryBudgetTests` | Historical constructor-level tests are quarantined. Their still-valid attachment authorization and budget-control scenarios must be ported to current API/service tests before these source files are deleted. |
| Legacy subledger posting | `SubledgerJournalServiceIntegrationTests`, `CreditNotePostingIntegrationTests` | The runtime path is intentionally disabled. Replacement coverage is in `LegacyPostingPathLockdownTests` and the AP/AR posting-migration suites. |
| AP/AR invoice posting | `VendorInvoiceServiceTests`, `InvoicePostingIntegrationTests` | Superseded by `ApInvoicePostingMigrationTests`, `ArInvoicePostingMigrationTests`, settlement-read-model tests, and posting-engine tests. |
| Sales returns and transaction tax | `CustomerReturnServiceTests`, `ReturnOrderIntegrationTests`, `TransactionTaxIntegrationTests` | Replaced by `ArCreditNotePostingMigrationTests`, `ArReceiptPostingMigrationTests`, posting-engine tests, and Ghana statutory-tax tests. |
| Tax reports | `TaxReportServiceTests` | Replaced by `GhanaTaxReportingExportFoundationTests` and backend reporting/export foundation tests. |
| Foreign exchange | `ExchangeRateServiceTests` | Superseded by `FxFunctionalCurrencyGovernanceTests` and `FxRealizedUnrealizedRevaluationTests`. |
| Landed cost | `LandedCostServiceTests` | The constructor-level repository mocks no longer match the current landed-cost orchestration. Port the still-valid allocation scenarios to current procurement service tests before deleting the historical file. |

## Rules

1. Do not add wildcard `Compile Remove` patterns for finance tests.
2. New tests belong in `ErpSystem.Api.Tests` unless they exercise a still-current
   unit that is intentionally maintained in this project.
3. Delete a quarantined source file only after its still-valid scenarios are
   represented in an active test project.
4. The CI solution build and test command must include `ErpSystem.Tests` so
   future interface drift fails immediately.
