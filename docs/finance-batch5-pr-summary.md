# Finance Go-Live Batch 5 PR Summary

## Batch

Batch 5: Journal Lifecycle, Immutability, and Manual Journal Posting Integration

## Batch 4 Hardening Confirmation

1. `FinancePostingEvent` idempotency is database-enforced.
   - Batch 4 had a unique filtered index on `TenantId`, `SourceModule`, `SourceDocumentType`, `SourceDocumentId`, `PostingAction`.
   - Batch 5 adds the stricter requested unique filtered index on `TenantId`, `SourceDocumentType`, `SourceDocumentId`, `PostingAction`.

2. Posting is atomic.
   - `FinancePostingEngine.PostAsync` wraps journal header, journal lines/account transactions, reversal metadata where applicable, and `FinancePostingEvent` in one EF transaction.

3. Duplicate/concurrent posting is safe.
   - Sequential duplicates return the existing result by default.
   - Concurrent duplicate races are protected by database unique indexes; Batch 5 catches `DbUpdateException`, clears the failed change tracker state, reloads the existing posting, and returns it when `ReturnExistingOnDuplicate` is true.

4. Validation is below controller level.
   - Posting validation is inside `FinancePostingEngine`.
   - Manual journal readiness validation is inside `JournalEntryService`.

5. `Account.Balance` is not mutated by the new posting engine.
   - Manual journal posting and reversal now use posted GL rows and posting events. Stored balances remain future rebuildable read models.

6. Workflow approval is not bypassed.
   - Manual journals must be `Approved` before posting.
   - Submission still uses the existing workflow service from `JournalEntryController`.

7. Reversal/adjustment support is implemented for manual journals.
   - Manual reversal creates a new posted reversal journal through `IFinancePostingEngine`.
   - The original journal is linked to the reversal journal and marked `Reversed`.

## Definition of Done

- [x] Manual journal posting uses `IFinancePostingEngine`.
- [x] Legacy manual posting path no longer mutates `Account.Balance`.
- [x] Draft journals remain editable.
- [x] Pending/submitted journals cannot post before approval.
- [x] Approved journals post through the central posting engine.
- [x] Posted journals cannot be directly edited, deleted, or have attachments modified.
- [x] Manual reversals create a new posted journal and link back to the original.
- [x] Closed-period, cross-tenant account, inactive/non-postable account, and unbalanced journal validations are enforced below controller level.
- [x] Existing workflow engine remains the approval path; no parallel approval system was added.
- [x] Automated tests added and passing.

## Lifecycle Table

| State | Allowed Actions | Blocked Actions |
| --- | --- | --- |
| Draft | edit, delete, attach/unattach, submit for approval | post, reverse |
| Pending Approval | approve, reject, withdraw | edit, delete, post |
| Approved | post | edit, delete |
| Rejected/Withdrawn | return to Draft through existing status update path | post |
| Posted | reverse | edit, delete, attach/unattach, repost duplicate |
| Reversed | view/report | edit, delete, reverse again |

## Files Changed

- `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`
  - Added existing-journal and reversal metadata fields for posting-engine requests.
- `src/ErpSystem.Core/Interfaces/Finance/IJournalEntryService.cs`
  - Added `ValidateJournalEntryReadyForSubmissionAsync`.
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
  - Added existing-journal posting support, reversal linking support, stricter duplicate detection, and duplicate race handling.
- `src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs`
  - Migrated manual posting and reversal to `IFinancePostingEngine`.
  - Added manual journal readiness, account, line, tenant, and fiscal-period validations.
  - Restricted attachment mutation to Draft journals.
- `src/ErpSystem.Api/Controllers/Finance/JournalEntryController.cs`
  - Validates journal readiness before starting the existing approval workflow.
- `src/ErpSystem.Data/ApplicationDbContext.cs`
  - Added stricter `FinancePostingEvent` unique index mapping.
- `src/ErpSystem.Data/Migrations/20260705100000_HardenFinancePostingEventIdempotency.cs`
  - Adds unique index on `TenantId`, `SourceDocumentType`, `SourceDocumentId`, `PostingAction`.
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
  - Updated snapshot for the new index.
- `tests/ErpSystem.Api.Tests/Services/Finance/JournalEntryLifecycleBatch5Tests.cs`
  - Added Batch 5 lifecycle, posting, immutability, reversal, and strict-index tests.
- `tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs`
  - Added explicit permission-policy coverage for journal approval submission.
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
  - Included the new Batch 5 test file.

## Removed or Bypassed Legacy Posting Paths

- `JournalEntryService.PostJournalEntryAsync` no longer applies account balance movements directly.
- Manual journal posting now calls `IFinancePostingEngine.PostAsync` with `ExistingJournalEntryId`.
- Manual journal reversal now calls `IFinancePostingEngine.PostAsync` with reversal metadata.
- The old `ALL_ACTIVE_BOOKS` manual posting shortcut is disabled until it can be migrated to the posting engine without direct balance mutation.

## Migration Impact

Migration added:

- `20260705100000_HardenFinancePostingEventIdempotency`

Before applying to production-like data, confirm no duplicate rows exist in `FinancePostingEvents` for:

- `TenantId`
- `SourceDocumentType`
- `SourceDocumentId`
- `PostingAction`

Batch 4 did not backfill historical posting events, so this index should be low risk unless manual rows were inserted outside the approved migration path.

## API Impact

No public route shape changed.

Behavior changes:

- `POST /api/finance/journal-entries/{id}/post` requires the journal to be `Approved`.
- `POST /api/finance/journal-entries/{id}/request-approval` validates journal readiness before workflow start.
- Posted journals reject direct update/delete/attachment mutation.

## Frontend Impact

No frontend files were changed in Batch 5.

The frontend may need later UX adjustments to reflect stricter lifecycle errors, especially “approved before posting” and disabled `ALL_ACTIVE_BOOKS` posting.

## Tests Added or Updated

Batch 5 tests cover:

- strict `FinancePostingEvent` tenant/source/action unique index metadata
- draft journal can be edited
- submitted journal cannot post without approval
- approved journal posts through `IFinancePostingEngine`
- unbalanced journal cannot submit or post
- posted journal cannot be edited or deleted
- duplicate manual post does not create a duplicate event
- cross-tenant account line is rejected
- closed-period posting is rejected
- posted journal reversal creates a new linked reversal journal
- journal submission route maps to the required Finance permission policy

## Build and Test Results

- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceGoLive-5" -v:minimal`
  - Passed: 10 tests.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceGoLive-4" -v:minimal`
  - Passed: 9 tests.

## Known Limitations

- AP, AR, cash/bank, tax, FX, fixed assets, and reporting workflows were not migrated in Batch 5.
- `ALL_ACTIVE_BOOKS` opening-balance posting is disabled for manual posting until it is safely migrated to the posting engine.
- Manual journal return/request-changes remains represented by the existing reject/withdraw paths; a dedicated return action can be added if the workflow definition requires it.
- Historical journals are not backfilled into `FinancePostingEvents` in this batch.
- Stored account balances from legacy posting paths still exist and require later read-model/snapshot cleanup.

## Rollback Considerations

- Code rollback restores the prior journal posting path, but that path mutates `Account.Balance`.
- Database rollback drops only the new strict idempotency index.
- If manual journals are posted through this batch in a shared environment, rollback should reconcile `JournalEntries`, `AccountTransactions`, and `FinancePostingEvents` first.

## Suggested Next Batch

Proceed to the Audit Trail Foundation batch or the next posting-engine migration batch only after Batch 5 acceptance. The next workflow migrated should be a controlled source such as AP invoice posting, not tax/FX/fixed assets yet.
