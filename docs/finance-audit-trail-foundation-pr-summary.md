# Finance Audit Trail Foundation PR Summary

## Scope

Implemented Finance-specific audit semantics on top of the existing central `AuditLogs` infrastructure. This batch does not add new audit tables and does not migrate AP, AR, cash/bank, tax, FX, fixed asset, reporting, or frontend workflows.

## Definition Of Done

- [x] Finance audit event taxonomy defined.
- [x] Finance audit writes use the existing `AuditLogs` table.
- [x] Audit records include `TenantId`, user ID, timestamp, action, source module, source document, journal entry, posting event, reason/comment where supplied, and correlation ID where available.
- [x] Manual journal create, submit, approve/reject/withdraw/status change, post, reverse, duplicate post attempt, and failed post attempt have Finance audit coverage.
- [x] Posting engine writes posting-event and duplicate-attempt audit events.
- [x] Finance audit reads are tenant-guarded.
- [x] No migrations added.
- [x] Automated tests added and passing.

## Architecture

Finance audit now uses a typed adapter, `IFinanceAuditService`, over the central `AuditLog` entity/table. The adapter enforces the active Finance tenant via `ICurrentUserService.GetRequiredFinanceTenantId()` before writing or reading audit history.

Audit context is stored in `AuditLog.NewValues` as structured JSON:

- `financeAudit.tenantId`
- `financeAudit.eventType`
- `financeAudit.sourceModule`
- `financeAudit.sourceDocumentType`
- `financeAudit.sourceDocumentId`
- `financeAudit.journalEntryId`
- `financeAudit.postingEventId`
- `financeAudit.workflowInstanceId`
- `financeAudit.workflowApprovalId`
- `financeAudit.reason`
- `financeAudit.comment`
- `financeAudit.correlationId`
- `financeAudit.recordedAt`
- `values`
- `context`

`AuditLog.OldValues` stores before values where applicable. No normal Finance path edits existing audit rows.

## Event Taxonomy

Defined in `src/ErpSystem.Shared/FinanceAuditEvents.cs`.

- `Finance.JournalEntry.Created`
- `Finance.JournalEntry.Updated`
- `Finance.JournalEntry.Deleted`
- `Finance.JournalEntry.SubmittedForApproval`
- `Finance.JournalEntry.Approved`
- `Finance.JournalEntry.Rejected`
- `Finance.JournalEntry.ApprovalWithdrawn`
- `Finance.JournalEntry.Returned`
- `Finance.JournalEntry.StatusChanged`
- `Finance.JournalEntry.Posted`
- `Finance.JournalEntry.PostingFailed`
- `Finance.JournalEntry.Reversed`
- `Finance.JournalEntry.ReversalCreated`
- `Finance.PostingEvent.Created`
- `Finance.PostingEvent.DuplicateAttempt`
- `Finance.PostingEvent.Failed`
- `Finance.SourceDocument.Approved`
- `Finance.SourceDocument.Posted`
- `Finance.SourceDocument.Reversed`
- `Finance.AccountingPeriod.Closed`
- `Finance.AccountingPeriod.Reopened`
- `Finance.Report.Exported`
- `Finance.Report.Printed`
- `Finance.Migration.DiagnosticRun`
- `Finance.Migration.AdjustmentPosted`
- `Finance.Migration.AccountantSignOffRecorded`

## Files Changed

- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
  - Added and completed the Finance audit event taxonomy.
- `src/ErpSystem.Core/DTOs/Finance/FinanceAuditDtos.cs`
  - Added `FinanceAuditEventDto`.
- `src/ErpSystem.Core/Interfaces/Finance/IFinanceAuditService.cs`
  - Added typed Finance audit write/read contract.
- `src/ErpSystem.Api/Services/Finance/FinanceAuditService.cs`
  - Added tenant-aware adapter over `AuditLogs`.
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
  - Registered `IFinanceAuditService`.
- `src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs`
  - Integrated Finance audit into manual journal lifecycle and posting/reversal results.
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
  - Added audit writes for posting event creation and duplicate posting attempts.
- `src/ErpSystem.Api/Controllers/Finance/JournalEntryController.cs`
  - Routed journal audit trail reads through `IFinanceAuditService`.
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceAuditFoundationTests.cs`
  - Added audit foundation coverage.
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
  - Included the new test file.
- `docs/finance-follow-up-opening-balances-all-active-books.md`
  - Added follow-up ticket for opening balances.

## Test Coverage

Added `FinanceAuditFoundationTests`:

- manual journal submit creates a tenant-aware Finance audit event
- manual journal post creates a journal audit event linked to the posting event
- manual journal reversal creates audit events linked to original and reversal journals
- duplicate manual journal post attempt is audited
- cross-tenant audit association is rejected
- cross-tenant audit history read is rejected

Regression filters also passed:

- Batch 4 posting engine tests
- Batch 5 journal lifecycle tests

## Build And Test Results

- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed, 0 warnings, 0 errors.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed, 37 warnings, 0 errors.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed, 5 warnings, 0 errors.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceAuditFoundation" -v:minimal`
  - Passed, 6/6.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceGoLive-4|Batch=FinanceGoLive-5" -v:minimal`
  - Passed, 19/19.

## Migrations

None.

## Rollback Considerations

Rollback is code-only for this batch:

- remove `IFinanceAuditService` registration
- revert journal/posting audit integrations
- remove the new DTO/interface/service/test files
- revert the journal audit trail endpoint to direct `AuditLogs` querying if needed

No schema rollback is required.

## Tenant Isolation Verification

- Finance audit writes reject events whose `TenantId` differs from the current Finance tenant.
- Finance audit reads reject requests for a tenant other than the current Finance tenant.
- Journal audit endpoint still verifies the journal belongs to the current tenant before returning audit history.
- Tests cover cross-tenant write and read rejection.

## Accounting Impact

This batch does not change posting amounts, account balances, GL source-of-truth behavior, journal validation, or fiscal period logic. It adds traceability around Finance state transitions and posting events so future AP, AR, cash/bank, tax, FX, fixed asset, and migration posting work can rely on consistent audit semantics.

## Known Limitations

- AP, AR, cash/bank, tax, FX, fixed asset, reporting exports, period close/reopen, and migration sign-off workflows still need to call `IFinanceAuditService` in their own batches.
- Existing non-Finance audit APIs can still expose central audit logs according to their current authorization behavior. This batch tenant-guards the Finance journal audit endpoint and Finance audit service reads.
- Manual journal create/update/delete audit writes are not wrapped in an explicit service-level transaction with their source state changes. Posting-engine audit events are written within the posting transaction.
- `ALL_ACTIVE_BOOKS` opening-balance posting remains disabled until it is migrated to the central posting engine. See `docs/finance-follow-up-opening-balances-all-active-books.md`.
