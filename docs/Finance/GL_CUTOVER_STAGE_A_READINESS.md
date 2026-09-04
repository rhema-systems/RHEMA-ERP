# GL cutover Stage A readiness and producer inventory

Status: review required

Exact base: `3c5620a3de9c5d3038be4b07b5026149815f4296`

This stage rehearses the migration/reset path and inventories the remaining V1 producers. It does not
change a producer, remove V1, or authorize a developer to point the rehearsal tooling at `RhemaERP`.

## Database safety boundary

The configured development source was inspected with `SELECT` statements only. Its resolved target was
`RHEMA-AKWASI\EXPRESS22 / RhemaERP`; credentials were neither logged nor copied into evidence.

All mutating rehearsal operations must use
[`scripts/finance/Invoke-GlCutoverRehearsal.ps1`](../../scripts/finance/Invoke-GlCutoverRehearsal.ps1)
and a process-scoped connection-string environment variable. The script rejects every target whose
database name does not match `^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$`, as well as attach-file, user-instance,
missing-server and missing-database connections. It logs only server/database, refuses an existing target,
never invokes `rebuild-db`, preserves a failed target for inspection, and requires a separate explicit
`DropRehearsal -ConfirmDrop` action for cleanup.

Example (the value itself must come from a secure local secret source and must not be committed):

```powershell
$env:RHEMA_GL_REHEARSAL_CONNECTION = '<local SQL Server connection; safe rehearsal database name>'
./scripts/finance/Invoke-GlCutoverRehearsal.ps1 -Mode RehearseEmpty
./scripts/finance/Invoke-GlCutoverRehearsal.ps1 -Mode DropRehearsal -ConfirmDrop
Remove-Item Env:RHEMA_GL_REHEARSAL_CONNECTION
```

The ordinary `rebuild-db` command is not a migration rehearsal: it uses `EnsureCreated`, stamps migrations,
and bypasses migration bodies and their preflight controls. The three large SQL snapshots are reference
artifacts and are not inputs to this procedure.

## Migration-chain reconciliation

The repository has 448 unique registered migration bodies from
`20260304155434_RecreateHRTables` through
`20260904003118_AddGovernedAccountSegmentIdentity`. Historical designers provide the authoritative full
Release registration; fast Debug builds substitute `FastBuildMigrationMetadata`. Stage A found four
Finance migration bodies whose generated designers had no `DbContext` association in that fast path.
The accompanying metadata correction restores Debug/focused discovery from 444 to all 448 migrations.
The real `InitialBaseline` designer is included only while generating its baseline SQL.

The first four migrations are upgrade-only deltas against a pre-existing schema. A raw migration from zero
therefore is not a supported empty-database path. The repository's established SQL Server migration test
materializes `20260313114533_InitialBaseline`, stamps that baseline and its four superseded predecessors,
then applies every later migration through normal EF APIs. The rehearsal harness implements the same
sequence before invoking EF's controlled forward update.

The configured `RhemaERP` history contains 446 rows and is missing these five migrations, in order:

1. `20260903044911_FinanceBookClassificationFoundation`
2. `20260903120000_EnforceFinanceClassificationSystemRoleCardinality`
3. `20260903130000_AddFinancialStatementClassificationSnapshots`
4. `20260903190453_AddBookScopedFxRevaluationPolicy`
5. `20260904003118_AddGovernedAccountSegmentIdentity`

The configured source also contains three applied IDs that are absent from the exact-base migration assembly;
they are historical/orphan history evidence and must not be deleted merely to make counts agree:
`20260817030000_AddFixedAssetLocationMasterLinks`,
`20260817050000_AddTenantFixedAssetLocationPolicy`, and
`20260818113000_AddApRemittanceAdviceLifecycle`.

### Preflight dependencies

- Phase 1 creates classifications and the account/book classification link.
- Phase 2 refuses duplicate singleton `SystemRole` values per tenant/book. Cash and Bank may repeat.
- Phase 3 adds immutable publication evidence. Existing published/retired versions require explicit
  compatibility review because newly added snapshot columns cannot recreate historical membership.
- Phase 4 refuses any live historical FX revaluation batch. It also refuses an active currency link unless
  its enabled account/book/classification lineage yields exactly one unambiguous equivalent to the legacy
  `RevaluationRequired` value.
- Phase 5 refuses duplicate segment codes/positions/natural-account definitions, duplicate assignments,
  and missing/deleted/cross-tenant account, segment, or lookup lineage before installing composite keys/FKs.

The real `seed-db` command migrates before it invokes `DatabaseSeedingService.SeedAsync`. Consequently,
post-migration Finance seeding cannot repair a Phase 4 preflight failure. Existing databases require a
separately reviewed pre-migration backfill or the approved reset path.

## Configured source findings

Read-only diagnostics found:

- 1 live FX revaluation batch;
- 9 active account-currency links;
- 360 enabled account/book mappings;
- no `AccountClassifications` table yet;
- 3 published or retired financial-statement layout versions;
- no duplicate segment stable codes, active positions, natural-account segments, assignments, or assignment
  positions;
- no detected cross-tenant segment/account/lookup lineage corruption.

The live FX batch alone is an intentional Phase 4 hard stop. The active links plus absent classifications
also make their required classification-default equivalence impossible before normal post-migration seeding.
The three historical published/retired layout versions cannot acquire truthful immutable classification
membership retrospectively. These findings support reset/reseed rather than weakening the migrations.

## Empty and representative rehearsals

The guarded empty rehearsal uses the real application seed command and normal EF migration APIs:

1. materialize and stamp the supported consolidated baseline;
2. `dotnet ef database update` for all forward migrations;
3. `seed-db` (real `IDatabaseSeedingService.SeedAsync` path);
4. Finance invariant query and canonical natural-key snapshot;
5. the same `seed-db` command again;
6. invariant query and byte-identical semantic snapshot comparison;
7. EF no-pending-model gate.

The application `apply-migrations` entry point is itself currently unusable: its temporary service collection
registers the audited DbContext but omits `IHttpContextAccessor`, so host validation fails before migration.
The harness deliberately uses EF tooling and records this deployment-command defect rather than weakening
the audited context.

The invariant query checks canonical books, classifications, mapping lineage, singleton roles, exact
COMPANY/NATURAL_ACCOUNT identity, six separate transaction dimensions, protected Draft layouts, absence of
publication/revaluation/posting evidence, and complete migration history. Surrogate GUIDs and audit
timestamps are excluded from the repeat-seed comparison.

The production full-seed entry point currently seeds the full Finance manifest only for tenant code
`DEFAULT`. Manifest components accept a tenant ID, but tenant provisioning does not orchestrate the complete
Finance manifest for every active tenant. That is a cutover limitation: either only DEFAULT is intended at
reset, or a separately reviewed all-active-tenant Finance provisioning orchestration is required before a
multi-tenant reset.

### Actual empty rehearsal result

The empty rehearsal did **not** reach seeding. After materializing the consolidated baseline and successfully
applying the first three forward migrations, `20260317115118_AddCrmEntities` failed. The baseline/preceding
chain leaves both `QuoteLineItem` and `QuoteLineItems`; that migration executes
`sp_rename N'[QuoteLineItem]', N'QuoteLineItems'`, which SQL Server rejects because the target already exists.
This is an exact-base historical migration-chain defect outside the Finance implementation boundary. No
historical migration was edited or silently stamped. Repeat-seed idempotency and Finance invariant validation
remain blocked until the migration owner supplies and reviews a supported empty-chain repair/baseline.

### Actual representative-clone result

SQL Server reported backup and restore support, so Stage A created a `COPY_ONLY`, checksum-protected backup of
`RhemaERP`, restored it only as `RHEMAERP_GL_REHEARSAL_CLONE_20260904A`, removed the temporary backup, and ran
the forward migration chain against the clone. Phase 1 applied. Phase 2 then failed while creating
`IX_AccountClassifications_TenantId_AccountingBookId_SystemRole`: SQL Server does not accept the filtered-index
predicate `[SystemRole] NOT IN (1, 2)` in this DDL context (`SQL error 102, Incorrect syntax near 'NOT'`). The
clone was then explicitly dropped after prefix revalidation. Therefore the later Phase 3/4/5 preflights were
not executed on the clone; the read-only source diagnostics still predict the independent Phase 4 hard stops
listed above.

All disposable targets used during harness development and the clone rehearsal were explicitly dropped.
A final `sys.databases` query confirmed that no `RHEMAERP_GL_REHEARSAL_*` database remains. `RhemaERP` was
never used as a mutation target.

## Exact V1 production inventory

Five active request-building paths remain in four production files. No Finance-owned runtime builder creates
V1, but Finance still owns the compatibility DTO, overload, adapter, evidence, and catalogue surface.

| Owner | Production file and path | Current authority problem | Phase B requirement |
| --- | --- | --- | --- |
| Procurement | `src/ErpSystem.Core/Services/Procurement/ProcurementSupplierOnboardingTokenService.cs` (`BuildPostingRequest`) | V1 default silently chooses IFRS | Build V2 with an explicitly governed concrete `AccountingBookCode` |
| Procurement | `src/ErpSystem.Core/Services/Procurement/TenderBidService.cs` (`BuildTenderFeePostingRequest`) | V1 default silently chooses IFRS | Build V2 with an explicitly governed concrete book |
| Inventory | `src/ErpSystem.Api/Services/Inventory/InventoryDisposalService.cs` | V1 plus literal IFRS | Build V2; resolve a concrete book and retain book-qualified idempotency |
| Sales | `src/ErpSystem.Core/Services/Sales/ReturnOrderService.cs` ordinary credit note | V1 plus literal IFRS | Build V2 with governed concrete book |
| Sales | same file, credit-note reversal | Rebuilds V1 lines from historical evidence | Call Finance's trusted exact `ReverseAsync` path |

HR/Payroll and other modules have no active V1 request constructor. Inventory's
`StockAdjustmentService.BookClassification` uses are source storage/read-model fields; its Finance adapter
already produces V2 and rejects `ALL_ACTIVE_BOOKS` at the single-book boundary. They are not V1 constructors.
Finance's remaining `BookClassification` references primarily preserve immutable storage/report evidence and
must not be bulk-renamed as though they were public producer DTOs.

V1 ownership remains in:

- `src/ErpSystem.Core/DTOs/Finance/FinancePostingDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFinancePostingEngine.cs`
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceExternalProducerDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IExternalFinancePostingAdapter.cs`
- `src/ErpSystem.Api/Services/Finance/GL/ExternalFinancePostingAdapter.cs`
- `src/ErpSystem.Core/Finance/Integration/FinanceExternalPostingEvidence.cs`
- `src/ErpSystem.Core/Finance/Integration/FinanceIntegrationContractCatalog.cs`

No runtime source was found constructing the V1 external envelope.

## Direct Finance Account writers

Finance-owned account creation remains in `AccountService`, `GeneralLedgerService`,
`AccountCombinationService`, `FinanceAccountProvisioningService`, and `FinanceDataSeeder`. These are governed
Finance boundaries or seeds and must retain Phase 5 identity validation.

Two non-Finance owners still write Finance account master data directly:

- Procurement's executable `ProcurementSupplierOnboardingTestSeeder` creates/edits accounts and display-text
  categories. Despite its name it runs from application seeding paths and is production-executable code.
- HR/Payroll's `PayrollService.EnsurePayrollFinanceAccountsAsync` inserts/updates accounts and category text
  from the Oracle mapping seed command.

No Inventory, Sales, or other production account writer was found.

## Owner-scoped Phase B packets

### Finance integration owner

After every owner packet is stacked, remove the V1 request/envelope, posting overloads, V1 evidence domain,
and catalogue entry. Do not rename immutable historical storage as part of the producer cutover. Gate on zero
active `FinancePostingRequestDto`/V1 envelope references, V2-only JSON and golden SHA evidence, central book
and mapping denial tests, exact reversal tests, and complete producer compilation.

### Procurement owner

Change the two producer services above and their supplier-onboarding/tender tests to V2 with explicit book
authority. Replace the executable seeder's direct `Account` writes with `IFinanceAccountProvisioningService`
and stable intent/code. Prove tenant/mapping denial, idempotent retry, canonical segments and repeatable
provisioning.

### Inventory owner

Change `InventoryDisposalService` and its E2E/unit tests to V2 with an explicit governed book. Prove balanced
evidence, retry idempotency and tenant/mapping denial. Preserve the existing source-side handling that keeps
`ALL_ACTIVE_BOOKS` away from the single-book engine.

### Sales owner

Change ordinary return/credit-note posting to V2 and use the trusted Finance reversal API. Extend
`ArCreditNotePostingMigrationTests` for posting/retry, mapping/tenant denial, immutable original book and exact
reversal/retry.

### HR/Payroll owner

Replace the direct account write in `PayrollService` with `IFinanceAccountProvisioningService`. Add missing
focused tests for repeat idempotency, wrong-type conflict, canonical identity, enabled classified mappings,
tenant isolation and absence of display-text inference. Payroll posting economics remain unchanged.

## No-active-V1 acceptance gate

The coordinated final cutover is accepted only when:

1. production and test projects contain no `FinancePostingRequestDto` or V1 external envelope;
2. V2 JSON contains `accountingBookCode` and no `bookClassification`;
3. V2 golden canonical string/hash and tamper tests pass;
4. all producer tests cover explicit book selection, mapping/tenant denial and idempotency;
5. no pseudo-book reaches Finance's single-book engine;
6. Procurement and HR direct-account-writer scans are clean;
7. Finance removes V1 and increments the final contract version in the same integration train.

Tests, migration history/designers, documentation, generated SQL and storage/read-model fields are classified
separately and do not create false runtime-producer failures.
