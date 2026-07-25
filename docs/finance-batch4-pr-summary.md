# Finance Go-Live Batch 4 PR Summary

## Batch

Batch 4: Posting Engine Foundation and Idempotency

## Definition of Done

- [x] Central posting engine contract added.
- [x] Posting event/idempotency table added.
- [x] Posting validation covers tenant, source ownership, fiscal period, open period, active accounts, balanced debits/credits, posting date, and currency placeholders.
- [x] Posted GL entries are created as posted `JournalEntry` and `AccountTransaction` records.
- [x] New engine does not mutate `Account.Balance`; posted GL is the accounting source of truth.
- [x] Duplicate source-document/action posting returns the existing posting result safely by default.
- [x] Reversal/adjustment path is defined through a reversal-plan API without posting reversal journals yet.
- [x] Tenant isolation is enforced below controller level.
- [x] Automated tests added and passing.
- [x] Backend build completed.

## Files Changed

- `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`
  - Added `FinancePostingRequestDto`, `FinancePostingLineDto`, `FinancePostingResultDto`, and `FinanceReversalPlanDto`.
- `src/ErpSystem.Core/Interfaces/Finance/IFinancePostingEngine.cs`
  - Added the central Finance posting boundary.
- `src/ErpSystem.Core/Entities/Finance/FinancePostingEvent.cs`
  - Added source-document posting/idempotency event entity.
- `src/ErpSystem.Data/ApplicationDbContext.cs`
  - Added `DbSet<FinancePostingEvent>` and EF mapping/indexes.
- `src/ErpSystem.Data/Migrations/20260704110000_AddFinancePostingEvents.cs`
  - Added the `FinancePostingEvents` table and tenant-scoped idempotency indexes.
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
  - Added the `FinancePostingEvent` entity metadata and relationships for future migration consistency.
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
  - Added the first implementation of the controlled posting engine.
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
  - Registered `IFinancePostingEngine`.
- `tests/ErpSystem.Api.Tests/Services/Finance/FinancePostingEngineTests.cs`
  - Added Batch 4 posting engine tests.
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
  - Included the new test file.
- `docs/sql/finance-batch3-cash-bank-tenant-preflight.sql`
  - Batch 3 pre-migration diagnostic gate for cash/bank tenant backfill duplicates and ownership sign-off.

## Posting Engine Design

`IFinancePostingEngine` is the single new posting boundary for Finance workflows. Batch 4 establishes the contract and core validation only; AP, AR, cash/bank, tax, FX, fixed assets, and migration workflows still need to be migrated to this engine in later batches.

`FinancePostingEngine.PostAsync` accepts a normalized posting request with:

- tenant-owned source document identity
- source module and source document type
- posting action, such as `Post`, `Reverse`, `Revalue`, or `Depreciate`
- posting date and fiscal period
- functional currency placeholder
- balanced debit/credit lines

The engine creates:

- one posted `JournalEntry`
- posted `AccountTransaction` lines
- one `FinancePostingEvent`

The engine intentionally does not update `Account.Balance`. Stored balances must be treated as rebuildable read models or controlled snapshots generated from posted ledger entries in later batches.

## Posting Event and Idempotency Model

`FinancePostingEvents` tracks source-document posting actions per tenant.

Primary idempotency key:

- `TenantId`
- `SourceModule`
- `SourceDocumentType`
- `SourceDocumentId`
- `PostingAction`

Optional caller idempotency key:

- `TenantId`
- `IdempotencyKey`

This model is intended to cover:

- journal entries
- AP invoices
- AP payments
- AR invoices
- AR receipts
- bank/cash transactions
- reconciliation adjustments
- tax postings
- FX revaluation
- depreciation
- asset disposal
- migration adjustments

Duplicate postings return the existing event/journal result by default. Callers can set `ReturnExistingOnDuplicate = false` to fail fast.

## Validation Added

`FinancePostingEngine` validates:

- current tenant context is present
- tenant exists
- source document tenant, where supplied, matches current tenant
- source module/type/action/reference values are valid lengths
- posting date is present
- fiscal period exists for the tenant
- posting date falls inside the fiscal period
- fiscal period is open, not closed, and not locked
- at least one debit and one credit line exist
- each line has exactly one debit or credit amount
- posting totals balance after money rounding
- every GL account belongs to the tenant
- every GL account is active
- foreign-currency lines include exchange-rate snapshots and original foreign amounts
- duplicate source-document/action is not posted twice

## Tenant Isolation Verification

Tenant isolation is enforced in the engine, not only at controller level:

- tenant is resolved via `FinanceTenantGuard.GetRequiredFinanceTenantId`
- source-document tenant mismatch is rejected when provided
- fiscal period lookup is filtered by `TenantId`
- account lookup is filtered by `TenantId`
- posting event lookup is filtered by `TenantId`
- reversal plan lookup is filtered by `TenantId`

## Accounting Impact

This batch establishes the accounting-safe posting foundation:

- debit/credit balancing is mandatory
- posted GL rows are the source of truth
- duplicate source-document postings are blocked/idempotent
- closed or locked periods cannot receive postings
- inactive or cross-tenant accounts cannot be used
- corrections are expected through reversal/adjustment flow, not direct mutation

The existing legacy posting paths still remain in place. Later batches must migrate each Finance workflow to this engine before go-live.

## Migration Impact

Migration added:

- `20260704110000_AddFinancePostingEvents`

The migration creates `FinancePostingEvents`, foreign keys to `Tenants` and `JournalEntries`, and filtered unique indexes for source-document/action and optional idempotency key.

No data backfill is required for Batch 4 because no existing workflow is migrated to the new table yet.

Batch 3 migration caution still applies: do not apply the cash/bank tenant-isolation migration to production-like data until `docs/sql/finance-batch3-cash-bank-tenant-preflight.sql` is run and duplicate/ownership issues are cleaned or signed off.

## API Impact

No public controller API was changed in Batch 4.

The new service can be injected as:

- `IFinancePostingEngine`

## Frontend Impact

None.

## Tests Added

`FinancePostingEngineTests` covers:

- balanced same-tenant posting succeeds
- posted ledger rows and posting event are created
- `Account.Balance` is not mutated by the new engine
- unbalanced posting fails
- duplicate source-document/action returns the existing result safely
- cross-tenant source document is rejected
- closed period is rejected
- inactive account is rejected
- missing tenant context is rejected
- other-tenant GL account is rejected
- reversal-plan path returns opposite lines without creating a reversal journal

## Build and Test Results

- `dotnet build src/ErpSystem.Core/ErpSystem.Core.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed: 0 errors, 0 warnings.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceGoLive-4" -v:minimal`
  - Passed: 9 tests.

## Known Limitations

- Existing AP, AR, cash/bank, tax, FX, fixed asset, depreciation, disposal, and migration posting workflows have not yet been migrated to `IFinancePostingEngine`.
- Full reversal posting is not implemented in Batch 4; only a reversal plan is exposed.
- Workflow approvals are not changed in Batch 4.
- Action-level Finance permissions from Batch 2 are not expanded in Batch 4.
- Full functional-currency configuration and tenant currency lock are deferred to the multi-currency batches.
- Full FX realized/unrealized revaluation is deferred to the FX batches.
- Balance snapshots/read models and rebuild tooling are deferred to the reporting/read-model batches.
- Audit trail enrichment for posting/reversal events should be handled by the planned Audit Trail Foundation batch.

## Risks

- Until legacy workflows are migrated, the system still has more than one effective posting path.
- Legacy posting services still mutate `Account.Balance`; that must be removed or converted to rebuildable read-model updates in later batches.
- The new posting event table is not backfilled for historical journals in this batch.
- The optional `SourceDocumentTenantId` guard depends on future workflow adapters supplying the source tenant when the source document is loaded outside the engine.

## Rollback Considerations

- Code rollback is straightforward because no existing callers use `IFinancePostingEngine` yet.
- Database rollback drops `FinancePostingEvents`; safe before workflows begin writing to it.
- After later workflow migration, rollback must first reconcile source documents against posting events and posted journals.

## Suggested Next Batch

Proceed to Batch 5 only after Batch 4 is accepted. Recommended next focus:

- journal lifecycle and posted-entry immutability hardening
- begin adapting manual journal posting to `IFinancePostingEngine`
- add audit trail hooks for posting/reversal history if Audit Trail Foundation is scheduled before journal migration
