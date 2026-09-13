# GL cutover Stage A readiness and producer inventory

Status: Stage A.1 historical rehearsal approved and integrated; the former 456/C8 executable chain is superseded
by the D3 disposable-development current-model baseline. Production/final cutover remains blocked and requires a
separate forward-compatible migration decision recorded in `GL_CUTOVER_DEPLOYMENT_READINESS.md`.

Stage A.1 exact base: `7bc24b22c0624aec9acae580ba8049d5f5a83425`

## Stage A.1 correction and rehearsal result

Stage A.1 repaired the three independently verified deployment blockers and then followed every newly
exposed clean-chain failure to a deterministic, data-preserving correction. The application
`apply-migrations` command now registers `IHttpContextAccessor` before the audited DbContext dependencies.
The singleton-role index uses SQL Server-supported predicates while retaining the exact rule: Cash and Bank
may repeat; every other non-null role is unique per tenant/book. The historical clean chain now reconciles
the redundant empty CRM graph before promotion, preserves the vendor-invoice table through its missing
rename, and corrects two AR compatibility batches which previously referenced columns outside the actual
`BusinessPartner` model or altered a column after creating its dependent index.

The clean reset also exposed two deterministic Finance seed defects. Payroll accounts were added after
mandatory COMPANY/NATURAL_ACCOUNT identity assignment, and the protected balance-sheet layouts requested
obsolete root codes. Payroll accounts now enter the same canonical identity pass as the standard chart,
and protected layouts use the manifest's stable `ASSETS`, `LIABILITIES`, and `EQUITY_ROOT` codes.

Final post-review fresh rehearsal `RHEMAERP_GL_REHEARSAL_EMPTY_A5` completed all 448 migrations through
`20260904003118_AddGovernedAccountSegmentIdentity`, exercised the real application `apply-migrations`
command, and completed two real `seed-db` passes from the final committed code. Both Finance invariant snapshots had SHA-256
`F51CEBF3ABCFD1C92BB64ACB8FCF9B2740D90D0AFB322C275A8363A3729A5549`. The independent SQL Server
full-chain regression also passed, including downgrade from the CRM promotion to the immediate campaign
predecessor, continued downgrade through the sales predecessor, and the Projects forward/backward boundary.

Representative post-review clone `RHEMAERP_GL_REHEARSAL_CLONE_A2` was created from a `COPY_ONLY`, checksum-protected
backup, verified, restored, and checked with `DBCC CHECKDB ... PHYSICAL_ONLY`. SQL Server Express rejected
backup compression, so the successful retry deliberately omitted only `COMPRESSION`; `COPY_ONLY` and
`CHECKSUM` remained enforced. Phase 1, Phase 2, and Phase 3 applied on the clone. Phase 4 then stopped exactly
as designed because one historical FX revaluation batch lacks the immutable book-scoped policy evidence
that cannot be fabricated. Clone history at the stop contained 449 rows and ended at
`20260903130000_AddFinancialStatementClassificationSnapshots`; the extra count includes the source's three
orphan historical IDs documented below.

The configured `RhemaERP` source fingerprint was identical before and after the clone rehearsal:
`446|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28` (migration count, maximum applied
ID, live FX batches, active currency links, journal entries). No source migration, seed, or data mutation
was executed. All rehearsal databases and the temporary backup were removed after prefix revalidation; a
final server query returned zero `RHEMAERP_GL_REHEARSAL_*` databases.

This stage rehearses the migration/reset path and inventories the remaining V1 producers. It does not
change a producer, remove V1, or authorize a developer to point the rehearsal tooling at `RhemaERP`.

The 448-migration and expected Phase 4 stop statements below are retained as exact Stage A.1 historical
evidence. They must not be interpreted as the final cutover terminal gate. The pre-D3 frozen candidate historically
discovered 456 migrations ending at `20260908120000_AddProducerIntentGroupsC8`; D3 repository-only discovery now
returns only `20260913162402_DisposableDevelopmentCurrentModelBaseline`. Authorized final clone work uses
`RehearseFinalClone`, derives the pending set from a fresh source-history read, and either completes that exact
delta or returns fail-closed NO-GO evidence without bypassing any migration preflight. That operational mode now
also requires exact independently reviewed commit/tree process values and a wholly clean tracked/untracked worktree
before connection parsing; retained evidence binds reviewed and executed identities and is independently reconciled.
Its current final-cutover invariant snapshot includes row content for C2 balances/exposures, C4 period/opening
authority and material journal/transaction/posting-event state. Active legacy currency links force pre-backup
`REVIEW`, and backup creation uses atomic `FileMode.CreateNew` reservation plus no-overwrite SQL media identity.
The D3 baseline correction also restores the complete current database-trigger contract: deterministic archive
evaluation selects 355/355 snapshot trigger names plus 89 still-active non-model triggers, preserves 24 later
chronological definition patches, and adds the separate 15-trigger C5-C8 authority set for 459 unique triggers.
The audited final non-trigger definitions are
one function and one view, with no final archived procedures, synonyms, security policies, or sequences. Each
definition is an isolated migration SQL operation and the generated zero-to-current SQL is checked against the
same exact name/object set. This repository-only result does not authorize a reset retry.

## Database safety boundary

The configured development source was inspected with `SELECT` statements only outside the separately
authorized `COPY_ONLY` backup operation. Its sanitized target is `<local SQL Server instance> / RhemaERP`;
credentials and machine identifiers are neither logged nor copied into retained evidence.

All currently supported mutating rehearsal operations must use
[`scripts/finance/Invoke-GlCutoverRehearsal.ps1`](../../scripts/finance/Invoke-GlCutoverRehearsal.ps1)
and a process-scoped connection-string environment variable. The script rejects every target whose
database name does not match `^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$`, as well as attach-file, user-instance,
missing-server and missing-database connections. It logs only server/database, refuses an existing target,
never invokes `rebuild-db`, preserves a failed target for inspection, and requires a separate explicit
`DropRehearsal -ConfirmDrop` action for cleanup.

`RehearseEmpty` and `RehearseClone` are permanently disabled at process entry, before repository resolution,
connection lookup/parsing, evidence creation, native commands, or SQL access. Their old fake-history stamping and
clone mutation implementations have been removed. The Stage A.1 commands and results in this document are historical
facts only and must not be rerun. New disposable-development work uses `ResetDisposableDevelopment`; production-style
readiness inspection uses `RehearseFinalClone`, each under its own independent-review contract.

The historical representative clone used the same harness rather than an undocumented manual procedure. It required
the source to resolve exactly to `RhemaERP`, requires source and target on the same SQL Server instance,
derives the backup/data/log paths only from the prefix-validated target, refuses existing targets/backups,
and performs `COPY_ONLY` + `CHECKSUM`, `RESTORE VERIFYONLY`, restore, and `DBCC CHECKDB ... PHYSICAL_ONLY`.
It builds the current HEAD before backup, records the source fingerprint before and after, and accepts only
the documented Phase 4 historical-FX-evidence stop and exact Phase 3 history state:

`DropRehearsal` deletes only the exact prefix-validated database and its target-derived COPY_ONLY backup,
then fails unless both are absent and writes `cleanup.json`. Raw `.artifacts` output remains local and
ignored. The sanitized, hash-validated review package is retained at
`docs/Finance/evidence/gl-cutover-stage-a1/review-2`.

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
2. `dotnet run --no-build --configuration Debug --project src/ErpSystem.Api/ErpSystem.Api.csproj -- apply-migrations`
   for every forward migration after the supported consolidated baseline;
3. `seed-db` (real `IDatabaseSeedingService.SeedAsync` path);
4. Finance invariant query and canonical natural-key snapshot;
5. the same `seed-db` command again;
6. invariant query and byte-identical semantic snapshot comparison;
7. EF no-pending-model gate.

The original Stage A application-command failure is superseded. Stage A.1 added
`IHttpContextAccessor` to the `apply-migrations` temporary service collection, retained the audited
`ApplicationDbContext`, and exercised the repaired real command successfully in the 448-migration empty
rehearsal. The guarded harness now uses that application command for forward migration; EF tooling remains
responsible only for materializing the repository's supported consolidated baseline.

The invariant query checks canonical books, classifications, mapping lineage, singleton roles, exact
COMPANY/NATURAL_ACCOUNT identity, six separate transaction dimensions, protected Draft layouts, absence of
publication/revaluation/posting evidence, and complete migration history. Surrogate GUIDs and audit
timestamps are excluded from the repeat-seed comparison.

The production full-seed entry point currently seeds the full Finance manifest only for tenant code
`DEFAULT`. Manifest components accept a tenant ID, but tenant provisioning does not orchestrate the complete
Finance manifest for every active tenant. That is a cutover limitation: either only DEFAULT is intended at
reset, or a separately reviewed all-active-tenant Finance provisioning orchestration is required before a
multi-tenant reset.

### Original Stage A empty rehearsal result (superseded by Stage A.1)

The empty rehearsal did **not** reach seeding. After materializing the consolidated baseline and successfully
applying the first three forward migrations, `20260317115118_AddCrmEntities` failed. The baseline/preceding
chain leaves both `QuoteLineItem` and `QuoteLineItems`; that migration executes
`sp_rename N'[QuoteLineItem]', N'QuoteLineItems'`, which SQL Server rejects because the target already exists.
This is an exact-base historical migration-chain defect outside the Finance implementation boundary. No
historical migration was edited or silently stamped. Repeat-seed idempotency and Finance invariant validation
remain blocked until the migration owner supplies and reviews a supported empty-chain repair/baseline.

### Original Stage A representative-clone result (superseded by Stage A.1)

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

## CRM and Projects migration-owner compatibility contract

The historical `20260315081834_AddCrmSalesEntities` and
`20260315150304_AddCrmCampaignEntities` migrations created a second plural CRM graph beside the singular
graph already supplied by `InitialBaseline`. `20260317115118_AddCrmEntities` may discard that predecessor
graph only when all seven tables are present and empty; partial or populated conflicts fail before any
drop. Its `Down` path reconstructs the exact two predecessor migrations so downgrade to
`20260315150304`, and then through `20260315081834`, remains valid. The authoritative singular graph is
renamed and retained in both directions; a conflicting populated plural graph is rejected before mutation.

Likewise, `20260407033921_AddProjectPackageBoqFoundation` preserves the baseline `VendorInvoices` table by
renaming it to the runtime `VendorInvoice` identity. Its `Down` preflights the lineage before dropping any
Project tables, then renames the same table back so rows, keys and dependent foreign keys survive. These
migrations may already be recorded as applied on developer databases. Owners must not remove, simplify or
replace the compatibility blocks without rerunning:

- `MigrationRehearsalCorrectionTests` for generated Up/Down operation shape;
- `MigrationCompatibilitySqlServerTests` for complete-empty, partial, populated, row-preserving and
  immediate-predecessor downgrade behavior;
- `QuantitySurveyArchitectureSqlServerMigrationTests` for the shared production-chain baseline plus its
  Quantity Survey schema contract;
- the guarded empty and representative-clone rehearsals documented above.

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
