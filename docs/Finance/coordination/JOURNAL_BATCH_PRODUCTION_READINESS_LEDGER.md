# Journal Batch Production Readiness Ledger

## Objective and boundary

- Objective: close the Journal Batch production-readiness gaps identified on 2026-09-29 across governed accounting-book identity, exact-book periods, account/dimension/FX validation, spreadsheet exchange, approvals, attachments, permission-aware UI, and release tests.
- Base: `8c45108ced691e29d655ed45fa4aec846f1b9a06`.
- Branch: `codex/journal-batches-production-readiness`.
- Worktree: `C:/Users/Akwas/Documents/DEV_WORK/RHEMA-ERP-journal-batches-production-readiness`.
- Primary checkout: `C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP` on `codex/finance-budget-posting-evidence`; unrelated dirty files must remain untouched.
- Database policy: migration may be authored and tested; it must not be applied to a retained/user database without explicit approval.
- Cross-module boundary: Finance-only implementation. Existing shared workflow, file upload, identity, and journal services may be consumed but not replaced.

## Journal Batch adaptation map

- Stack/database: ASP.NET Core 8, EF Core 8, SQL Server; Next.js/React/TypeScript; ClosedXML plus Open XML package inspection.
- Tenant boundary: authenticated Finance tenant ID on every query, mutation, index, and relationship.
- Existing journal aggregate: `JournalEntry` plus `AccountTransaction`; retain standalone behavior and batch membership guard.
- Existing authoritative validator: `JournalEntryService.ValidateJournalEntryReadyForSubmissionAsync` plus Finance dimension and FX validation.
- Existing posting engine: `FinancePostingEngine`; batch orchestration remains the outer transaction owner.
- Existing reversal behavior: `JournalBatchService.CreateReversalBatchAsync` with generated inverse journals and linked source items.
- Period authority: tenant `FiscalPeriod` outer gate plus exact `AccountingBookPeriod` inner gate.
- Workflow: one `JournalBatch` workflow with assignment checks and per-item immutable decisions.
- Numbering: `IDocumentNumberingService` / `FinanceDocumentTypes.JournalBatch`.
- Permissions: dedicated `Finance.JournalBatches.*` policies enforced by the Finance authorization convention; UI intersects lifecycle with current-user permissions.
- Audit/notification: `IFinanceAuditService`; posting notifications remain post-commit.
- Spreadsheet: versioned `.xlsx` preview/commit/export through `JournalBatchSpreadsheetService`.
- Concurrency: row versions, filtered unique indexes, atomic posting claims, execution-strategy transactions.
- Book eligibility decision derived from governed Finance policy: direct manual batches may target the active/postable `PrimaryFull` book or an active/postable `Delta` book whose exact period is open. `ParallelFull` is replication-driven and is excluded from direct batch targeting.
- Schema change: mandatory `JournalBatch.AccountingBookId`, with `BookClassification` retained as the code snapshot; tenant/book/period index and restricted FK included in migration `20260929102006_JournalBatchStableAccountingBook`.
- Modified components: batch DTO/service/controller/configuration/migration, spreadsheet service, batch frontend types/service/pages, targeted service/component tests, and this ledger.
- Deferred and excluded: recurring templates, scheduled generation, auto-post, statistical batches, remote push/PR, and applying the migration.

## Phase ledger

| Phase | Status | Evidence / next action |
|---|---|---|
| 0. Reconcile and policy confirmation | Complete | Repository playbook and C3/C5 accounting-book governance read; exact base/worktree recorded above. |
| 1. Governed book domain/schema | Complete | Stable FK, server-side eligible-book API, exact-period policy, fail-before-mutation backfill and mismatch guards, migration script and model-drift gates passed. |
| 2. Draft/service validation | Complete | Create/update/attach/validate/post recheck book lifecycle, exact book period and account mapping; date bounds enforced; journal validator remains authoritative for dimensions and FX. |
| 3. Workflow/audit | Complete | Final approval actor/time populated; attachment link/unlink audited; copy/reversal preserve FX evidence and coding dimensions; copy is transactional. |
| 4. Spreadsheet | Complete | Template v2 resolves stable book ID and validates exact period, mapping, dimensions and approved effective FX evidence at preview and commit; export round-trips new evidence columns. |
| 5. Frontend | Complete | Server-driven Primary/Delta list, explicit configured-book labels, date and book-aware entry controls, coding dimensions, permission-aware actions, edit/delete/copy/reversal/evidence workflows, actual currency labels. |
| 6. Verification and review | Complete with deployment gates | Focused backend/service and component suites green; targeted lint, API build, idempotent migration scripting and pending-model check green. SQL Server-only release tests require the configured release-gate database; repository-wide TypeScript baseline remains red outside Journal Batches. |

## Baseline verification

- Targeted backend run before changes: 121 passed, 1 failed. Failure: spreadsheet round-trip fixture saved a `JournalEntry` without its required `AccountingBookId`.
- Frontend type-check baseline unavailable in the primary checkout because dependencies were not installed there.
- Existing browser tests are mocked and hard-code `IFRS`; they are not an adequate governed-book release gate.

## Final verification evidence

- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --nologo -clp:ErrorsOnly`: passed, 0 errors (existing warning backlog remains).
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter FullyQualifiedName~JournalBatch --no-restore --nologo -clp:ErrorsOnly`: 17 passed, 0 failed, 11 skipped SQL Server release-gate tests.
- `npx vitest run src/components/finance/journal-batches/eligible-draft-journal-combobox.test.tsx`: 2 passed.
- Targeted ESLint for Journal Batch pages/service/types: passed.
- `dotnet ef migrations script ... --idempotent --output NUL`: passed; the migration was scripted but not applied.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: passed with “No changes have been made to the model since the last migration.”
- `git diff --check`: passed.
- Repository-wide `npm run type-check`: remains blocked by pre-existing errors in Civil Engineering, Cash, Inventory, Procurement, unrelated Finance tests, Quantity Survey, Reports, and mock data. No diagnostic referenced a Journal Batch file.
- Dependency installation reported 27 existing npm audit findings (1 low, 7 moderate, 13 high, 6 critical); no automated dependency upgrade was attempted inside this feature change.

## Release disposition

- Feature implementation is code-ready for staging.
- Before production deployment, run the migration against a current production clone: its deliberate `THROW 51000/51001` guards will stop before the non-null FK if any legacy book code is ambiguous/unresolved or any batch contains a journal from another book.
- Run the 11 SQL Server release-gate tests against the designated release database and perform role-based browser smoke tests for create/import/review/post/reversal and attachment download.
- Repository-wide TypeScript baseline and npm dependency findings remain platform-level release risks; they are not introduced by this change and must be resolved or formally waived by the release owner.

## Escalation state

- No database migration has been applied.
- No accounting data has been posted, approved, reversed, or otherwise mutated.
- No remote branch, pull request, or deployment has been changed.
- 2026-09-29 UAT completion request: target database named `RHEMAERP_BOOKV2_UAT_20260922`; SQL Server endpoint/authentication and UAT web/test-role sessions are not configured in this worktree or process environment.
- Read-only connectivity probe: the documented `localhost`, `localhost\\SQL2017`, and `.\\SQLEXPRESS` instances were unavailable; `(localdb)\\MSSQLLocalDB` was reachable but did not contain the named UAT database.
- Release-gate fixture follow-up: SQL Server Journal Batch seed/reversal rows now carry mandatory `AccountingBookId`; split verification passed 11 service tests and 5 spreadsheet tests after the combined filter became resource-bound.
- Safe resume point: obtain explicit authorization to apply migration `20260929102006_JournalBatchStableAccountingBook` to the named UAT database after read-only preflight; obtain a connection endpoint whose test login may create/drop disposable databases for the 11 SQL Server gates; obtain UAT web URL and role sessions for browser smoke tests.
