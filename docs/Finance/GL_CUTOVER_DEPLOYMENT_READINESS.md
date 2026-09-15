# Finance multi-book GL cutover deployment readiness

Status: **attempt-13 zero-to-current lifecycle correction — independent review required; reset retry, deployment,
and feature activation are not authorized**

Current correction implementation: `9d78e942cefa67bb2a16a0cdf4bd4717b01dd732`, tree
`3647cd72df1143969cad7ba96fccfa36b531a5b2`, atop lifecycle implementation
`6995b944aa27e68094b4e35f92afa52d8242d2cc` and the exact attempt-13 operational base
`22789f39e550f24bd1ed6197821de4ebc0541bdd`. The merged model retains one true zero-to-current baseline, 596
archived migration identities, 493 active trigger definitions, five active functions, one active view, and 675
exact archived-final check constraints within 835 current-model checks. C6/C7/C8 remain false. This correction did
not access a database or alter preserved evidence/backups. Independent Sol High review is required before any new
operation.

Attempt 13 executed its separately authorized exact operational base once. The bounded 600-second/no-retry CLI
profile worked, but zero-to-current migration stopped fail-closed on SQL Server error 3728 while a mixed historical
trigger-patch operation attempted to drop absent predecessor constraint
`CK_ProcurementExceptionalSourcingControls_Lifecycle`. Durable phase is `DATABASE_RECREATED`, failed operation is
`APPLY_MIGRATIONS`, terminal history is empty, and C6/C7/C8 remained false. No retry, restore, cleanup, or activation
followed. Attempt-owned backup/evidence remain preserved externally.

The correction materializes the exhaustive archived final check-constraint lifecycle declaratively in the current
EF model and emits only the exact trigger-definition fragment from each selected mixed historical operation. The
675/675 archived-final check name/definition set is present with zero missing and zero drift; generated SQL contains
no predecessor-dependent top-level constraint drop, warehouse backfill, redundant Stock Adjustment column alter,
or Physical Count constraint downgrade. Offline zero-to-current SQL passes TSql160 grammar and retains exactly 493
triggers, five functions, one view, and 108 ordered patches. The inspector now records 870 exact ordered lifecycle
events and refuses malformed, removed, duplicated, reordered, ambiguous, or otherwise unclassified check authority.
Patch provenance binds the full normalized source operation plus the exact retained fragment, marker, transformation,
and deterministic boundary; mutation self-tests prove that source-prefix, marker, boundary, and fragment tampering
cannot pass generation or deterministic verification.

D3 historical status: authorized attempt 11 executed exact Sol-High-approved commit
`008e29ee0427620f62a21d89b24a98d2af43ddc4`, tree
`4fa31991cadb4a55ea143f036b45410279d52cc2`, and completed `PASS` / `COMPLETE`. The current local disposable
`RhemaERP` has that candidate's exact one-row baseline, two successful seed passes, byte-identical canonical
Finance invariants, DBCC PASS, GLF003 zero, and the complete 459-trigger plus function/view governance surface.
Its C6/C7/C8 process flags remain false. That local database does **not** contain the later merged baseline body:
the merged repository deliberately retains the same migration identity while its regenerated baseline now represents
1,629 tables and 493 triggers. EF's no-pending-model check compares the current model with the current compiled
snapshot; it cannot prove that an already-applied row bearing the same migration ID executed this newer baseline
content. The local database is therefore not accepted as the merged integration model or activation-ready. A newly
authorized disposable reset from the exact independently approved merged candidate must prove the 1,629-table /
493-trigger surface and all reset, seed, invariant, DBCC, and evidence gates before staged activation can proceed.
External attempt evidence and verified recovery backups from attempts 01-11 remain preserved and must not be changed.
Enabling any Finance feature flag or performing another database mutation requires separate explicit authorization.

Historical Stage B5 frozen integrated candidate: `cbc0d3c91142c63c4ea40f08f11d633752268367`

Candidate provenance: exact Stage B5 commit `cc132ac329b6a4e7187e57bd8108cbc8492822d6` was
independently approved by GPT-5.6 Sol High, integrated locally as
`0a27f50bee597d15429cfb5ab6598122ca689ec9`, and reconciled in the readiness-only commit
`cbc0d3c91142c63c4ea40f08f11d633752268367`. No database operation occurred in that review or integration.

This checklist is the release boundary for the Finance multi-book GL cutover. It records what may be
reviewed from the repository without connecting to a database and what must still be performed by an
authorized operator against an isolated rehearsal target. Preparing this candidate did not connect to,
apply migrations to, seed, or otherwise inspect or mutate the configured `RHEMAERP` database.

## Code-candidate gates

- [x] All named producer owners are stacked on the candidate base: Finance, Procurement, Inventory,
  Sales, and HR/Payroll.
- [x] The active posting and external-producer boundaries are V2-only. Requests carry one explicit
  `AccountingBookCode`; producer JSON has no `bookClassification` input. `FIN-INT-001` is version `2.1`
  for the final V2-only surface.
- [x] External source evidence uses the canonical V2 domain and binds the exact accounting-book code;
  tampering with that code changes the evidence hash. Its published `2.0` hash domain remains stable so
  existing V2 evidence stays verifiable.
- [x] The single-book engine rejects the `ALL_ACTIVE_BOOKS` pseudo-book.
- [x] Procurement and HR/Payroll no longer create or classify Finance accounts directly. HR supplies
  stable account intent to the Finance-owned provisioning service.
- [x] C6 `Finance:AccountingEvents`, C7 `Finance:ProducerIntents`, and C8
  `Finance:ProducerIntentGroups` are disabled by default. Absence of configuration is false.
- [x] Independent Sol High review approved exact candidate commit `cc132ac329b6a4e7187e57bd8108cbc8492822d6`.
- [x] The coordinator integrated it as `0a27f50bee597d15429cfb5ab6598122ca689ec9`, reproduced the
  final gates, and recorded the frozen integrated candidate at `cbc0d3c91142c63c4ea40f08f11d633752268367`.

Immutable historical database columns, migration SQL, reporting/read-model fields, and audit evidence
that contain the legacy classification label are retained intentionally. They are stored evidence, not an
active producer request surface.

## Candidate verification evidence

- Test-project build: zero errors (existing repository warnings only).
- V2 contract, evidence, posting-engine, year-close, FX, inventory-valuation, and supplier-return tests:
  197 passed after the exact year-close reversal correction.
- C6-C15 producer-chain, Sales relational handoff, Payroll provisioning, and legacy-path lockdown tests:
  136 passed.
- Finance accounting-book frontend tests: 33 passed; targeted ESLint passed.
- Active non-migration V1 type/catalogue scan: zero matches. Procurement and HR account-writer scan:
  zero matches.
- EF model parity: no pending model changes. No-connect migration discovery now returns exactly one compiled
  disposable-development baseline, `20260913162402_DisposableDevelopmentCurrentModelBaseline`.
- Historical evidence: the pre-D3 13-migration idempotent cutover artifact contained 13 migration-history rows, was 265,374 bytes, and
  has SHA-256 `CDBC813157F551BF192845DFBC05D74D55C6029620EA54A41A99E35769CF3D01`.

This evidence is repository-local. It does not replace independent review or environment rehearsal.

## Migration inventory and unapplied set

Repository-only discovery on the D3 candidate must return exactly **one** migration:
`20260913162402_DisposableDevelopmentCurrentModelBaseline`. Migration discovery must be run with `--no-connect`; model
parity and script generation use EF design-time metadata with an explicit synthetic unreachable process-scoped
connection override so application configuration is never inspected or used. These checks do not prove that any
environment has applied the migrations.

The complete merged 596-migration source chain is retained under
`src/ErpSystem.Data/LegacyMigrationsArchive` and explicitly excluded from compilation. It is recoverable audit
source, not an executable chain and not fake history. The baseline is a true zero-to-current migration from the
current model; its snapshot is current, future EF migrations continue normally from it, and its `Up` carries the
final reviewed C5-C8 database-only trigger authority after creating the C1-C8 relational schema. The baseline is
authorized only for the explicitly disposable local-development reset. It is not an upgrade path for databases
that contain any legacy migration history.
The archive contains 879 C# sources and 596 exact unique migration source identities. The exact reviewed D3
baseline migration, designer, and snapshot are retained separately under `D3BaselineArchive`; only the regenerated
merged snapshot remains compiled.

The merged inspector deterministically evaluates all 1,813 raw-SQL operations across those 596 identities and
reconciles them with the current snapshot. The baseline carries 386 current-model triggers and 92 still-active
non-model archived triggers, plus the distinct 15-trigger C5-C8 authority set, for 493 unique final trigger names.
It applies 108 chronological definition patches and retains the final five functions
(`InventoryAdjustmentExpectedLineValue`, `InventoryAdjustmentExpectedUnitCost`, `WorkflowApprovalEntityKey`,
`WorkflowApprovalRequiredAtSubmission`, and `fn_ProcurementRfqSourceLineIdentity`) plus
`vw_ProcurementReceiptDocumentReconciliation`. A checked manifest binds provenance, target, lifecycle disposition,
and body hash. Offline extraction reconciles 16,212 unambiguous inserted/deleted references against 1,629 baseline
tables. Generated SQL must reproduce that exact surface and the sole baseline history identity before another reset
can be reviewed.

The last authorized Stage A read-only fingerprint of configured `RHEMAERP` ended at
`20260902140000_AddFixedAssetDepreciationConventionEvidence`. On that evidence, the minimum known cutover
set that requires an isolated clone rehearsal before production is, in order:

1. `20260903044911_FinanceBookClassificationFoundation`
2. `20260903120000_EnforceFinanceClassificationSystemRoleCardinality`
3. `20260903130000_AddFinancialStatementClassificationSnapshots`
4. `20260903190453_AddBookScopedFxRevaluationPolicy`
5. `20260904003118_AddGovernedAccountSegmentIdentity`
6. `20260905151918_AddStablePostingAccountingBookIdentity`
7. `20260905182403_AddBookAwareBalanceFoundation`
8. `20260905213000_AddGovernedAccountingBookLifecycle`
9. `20260906140533_AddAccountingBookPeriodInitializationFoundation`
10. `20260906190846_AddAccountingBookApplicabilityFoundation`
11. `20260907071922_AddAccountingEventOrchestrationFoundation`
12. `20260907190000_AddProducerIntentStagingC7`
13. `20260908120000_AddProducerIntentGroupsC8`

This is a last-recorded minimum, not a substitute for deployment-time discovery. The authorized operator
must obtain a new read-only migration-history snapshot immediately before rehearsal and record the exact
pending delta. Historical/orphan history IDs recorded in Stage A must be preserved and investigated; they
must not be deleted or silently stamped to make counts agree.

The pre-D3 code-candidate check generated an idempotent script for the full 13-migration cutover range (from the
last recorded applied migration through C8). Full-history idempotent generation from migration zero remains
blocked by a pre-existing legacy `AspNetRoles` data-operation/model-metadata mismatch. Deployment does not
require replaying the repository's entire history against an existing database, but the exact pending range
must be regenerated and independently checked after deployment-time discovery.

## Required preflight and rehearsal

Deployment remains blocked until every item below has durable evidence and an independent reviewer sign-off.

The Stage A `RehearseClone` and `RehearseEmpty` names now fail immediately before any connection-capable work;
their mutation implementations and fake migration-history stamping have been removed. Their Phase 4 and empty-chain
results remain historical evidence only. The operational cutover path is `RehearseFinalClone`. It does not use an application
configuration fallback: both connection strings and all three disabled flags must be explicit process values.
It also requires the exact independently reviewed commit and tree as process values and refuses a descendant,
another tree, or any tracked/untracked workspace change before parsing a connection or contacting SQL Server.
On the D3 candidate it first binds that clean executed HEAD/tree into evidence, builds, proves EF model parity,
and discovers exactly the one baseline identity. Any source with legacy migration history is outside this
disposable baseline and must fail closed; `RehearseFinalClone` is not authorized to apply the baseline to such a
source or to fabricate/stamp replacement history. The historical final-clone behavior then captures source history,
derives and records the exact pending delta, generates and hashes its bounded idempotent SQL artifact, and runs
read-only readiness diagnostics. Any `BLOCKER` or explicit `REVIEW` finding produces
durable `NO_GO_PREFLIGHT` evidence before a backup or target exists. Only a clean preflight may proceed through
the target-derived absent backup check, `COPY_ONLY`/`CHECKSUM`, `RESTORE VERIFYONLY`, prefix-safe restore,
`DBCC CHECKDB ... PHYSICAL_ONLY`, exact pending-delta application, two real seed passes, and byte-identical
Finance invariant evidence. That canonical content includes C2 balances/exposures, C4 period/initialization
authority, and the material JournalEntry/AccountTransaction/FinancePostingEvent rows—not merely counts.
Any active legacy currency link is `REVIEW` and stops before backup unless separately reviewed proof establishes
complete Phase 4 equivalence. Failures preserve evidence and any exact target/backup; the harness never drops or
overwrites either automatically. Raw `sqlcmd` output is held only in an outside-package temporary file, sanitized,
then deleted; the package validator scans JSON, text, logs, checksums and SQL, independently re-derives pending/
orphan sets and the exact PASS target-history union, and verifies artifact, identity and ordered backup/restore markers.
Evidence must be external to the repository or below the dedicated ignored `.artifacts/finance-gl-rehearsal` root.
If phase-07 marker publication fails after exact target history was already captured and reconciled, failure evidence
keeps the prior durable phase but reports the validated baseline as materially applied. Failed V2 packages after source
capture must bind their source fingerprint file, terminal status, source-history count/latest and any final
destructive-boundary fingerprint exactly; the validator rejects re-manifested divergence.
The shared sqlcmd transport sets the supported maximum variable-width display and screen width
(`-y 8000 -w 8000`), omits conflicting `-W`/`-Y`, and fails closed if any emitted line reaches that boundary.
Canonical rows are `nvarchar(max)` before SQL-side `SHA2_256` hashing and retain only a bounded stable key plus
the fixed hash in transport; offline evidence proves a single unwrapped sentinel beyond character 4,000 survives.
Every required native-command artifact is atomically published with its sanitized command identity, explicit
success status and exit code, including successful commands such as `git diff --check` that emit no ordinary output.
When fresh discovery finds zero pending migrations, no dotnet generation command is fabricated: the generation
artifact instead uses the bound `NOT_REQUIRED`/`ZERO_PENDING_MIGRATIONS` schema, which validation accepts only
after independently deriving an empty pending set.
The exact target-derived backup path is atomically reserved with OS `FileMode.CreateNew`; SQL then uses a fresh
media identity with `NOINIT`, `NOSKIP`, and `MEDIANAME`, so a concurrent/pre-existing file is never overwritten.

The approved exact `e66ba924d3575da04abe4370f9c0b60a43c789c7` FINAL-03 run safely reached
`NO_GO_PREFLIGHT`. Its source fingerprint was unchanged and it discovered 13 pending migrations. Readiness
stopped on an absent classification schema, three historical-layout `REVIEW` findings, one live historical-FX
batch `BLOCKER`, and nine active legacy-currency-link `REVIEW` findings. No backup or target was created
(`backupCreated=false`, `targetCreated=false`). The raw FINAL-03 directory is preserved; package validation was
rejected only because the successful zero-output `git diff --check` had not created `git-diff-check.log`.
FINAL-01 and FINAL-02 evidence also remain preserved. This tooling correction does not convert the operational
NO-GO into approval; a new exact independently reviewed candidate and new evidence directory remain required.

The approved FINAL-04 operational run executed exact HEAD
`3d7a9413bb07e46101d0d45537c105f81bf50c22`, tree
`3d673ffcdda54aacca8ab6cb675e035492cbbe9d`, from a clean repository. Repository discovery proved 456
migrations ending at `20260908120000_AddProducerIntentGroupsC8`; source discovery left 13 pending and preserved
the unchanged fingerprint `446|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28`. All three
cutover flags were explicitly false. Readiness correctly returned `NO_GO_PREFLIGHT` for two blockers
(`CLASSIFICATION_SCHEMA_ABSENT=1`, `LIVE_FX_REVALUATION_BATCH=1`) and two review sets
(`HISTORICAL_PUBLISHED_LAYOUTS=3`, `ACTIVE_LEGACY_CURRENCY_LINKS=9`). It created neither backup nor target,
and an independent check found zero target databases (`backupCreated=false`, `targetCreated=false`, target DB
count `0`). Final-package validation passed and wrote the manifest. The external FINAL-04 evidence directory
remains preserved and is not copied into this repository. This is durable proof of a safe stop, not cutover
approval; every blocker and review finding remains open.

### FINAL-04 remediation decision packet

A follow-up read-only assessment on 2026-09-13 identified the exact shape of the four findings without changing
the source. The three historical versions are published layouts: `BS_LEGACY_IFRS` version 1 (9 rows/37 mappings),
`IFRS_BS` version 1 (22 rows/27 mappings), and `IFRS_IS` version 1 (21 rows/19 mappings). Their immutable
publication membership and book identity need an explicit preservation decision; the migration must not infer
those snapshots silently from mutable current mappings.

The single live FX batch is posted, has two lines and durable journal/posting references, and has not been
reversed. It therefore must not be deleted, soft-deleted, reclassified, or reset merely to pass Phase 4. The
safe history-preserving option is a separately reviewed backfill that reconstructs and verifies the exact
book, classification, rate, policy and reversal evidence from durable source records. Reset/reseed is a distinct
destructive option suitable only if the database owner explicitly declares this source disposable.

The nine active currency links comprise three zero-history links for `000-8884-0000`, three zero-history links
for `000-9994-0000`, one three-transaction USD link for `000-9995-0000`, and one USD link each for `1100` and
`2000`. Every link has three enabled active posting-book mappings and exactly one default posting-book mapping.
The reviewed manifest resolves `1100` to `RECEIVABLE_CONTROL` and `2000` to `PAYABLE_CONTROL`, whose revaluation
default agrees with the legacy enabled flag. The three test-account codes are not reviewed manifest identities:
their classifications or controlled inactivation require an explicit Finance decision, and the history-bearing
`000-9995-0000` link cannot be removed.

The classification table is absent because its foundation is the first of the 13 pending migrations. The
current all-at-once migration entry point cannot seed or review classifications between Phase 1 and the Phase 4
preflight, while the final harness refuses the absent schema before creating a clone. Proceeding therefore
requires one of these explicit decisions:

1. **Recommended — preserve history:** authorize design and isolated-clone rehearsal of a staged, independently
   reviewed compatibility backfill. It must create/seed Phase 1 authority before Phase 4, preserve the three
   published-layout snapshots, bind the existing posted FX batch and lines to exact book/classification/policy
   evidence, and resolve all nine links without changing their economics. Production remains read-only until the
   backfill and full clone rehearsal pass and a separate migration-application authorization is granted.
2. **Disposable-source reset/reseed:** the user has now explicitly declared the local `RhemaERP` development
   data disposable and authorized a backup-verified destructive reset and deterministic reseed. This authorization
   is development-only and does not apply to production, shared, remote, or retained accounting data.

Phase D2 verified-hash correction completed offline as exact implementation commit
`7492452fadffb9b5affffa66b829b3b5a969af7d` (tree `d3cb8cb51f957ec33a996fb7178ac41392b5c760`)
after review of exact `7811cfe4`. Phase-04 now preserves the original verified backup hash even if current bytes later
change; phase-03 requires completed backup markers, positive length and captured-hash reconciliation. Execution
remains prohibited until the corrected ledger-only descendant receives Sol High approval; no database mutation has
occurred under this authorization.

A narrow follow-up correction after review of exact `c101f693` completed as implementation commit
`e629e57f6867745cac3fcf1dae85e63e16393885` (tree `55fc2a68f8b9ec2b11a793843e3818606e654f0e`).
The disposable-reset validator now requires the atomic-reservation and COPY_ONLY CHECKSUM completion markers whenever
terminal evidence says `backupCompleted=true`, including a truthful material-backup failure before phase-03 marker
publication. Independent tamper fixtures remove each marker and re-manifest the otherwise valid package to prove the
semantic refusal. Reset execution remains prohibited pending Sol High approval.

The approved runtime attempt at exact `22b27ab18a19a92fa6b1222c05add817574e74fe` stopped fail-closed before
phase-03 and before any reset in preserved external evidence directory `GL-Disposable-Reset-Evidence-20260913-01`.
The source remained unchanged. Its no-overwrite COPY_ONLY backup is preserved at 453,042,176 bytes with SHA-256
`7F07CD03EED8F178ED45208936C3CC8F6E9F8D1336FFB3BF371C076D8F343743`; a separate independent `RESTORE VERIFYONLY
WITH CHECKSUM` passed. The historical run must remain preserved and must not be resumed or reused. The harness had
collapsed the SQL result records and progress messages into one 399-character physical line, while its post-backup
gate expected standalone lines and falsely reported `BACKUP_PATH_ATOMICALLY_RESERVED` absent.

The bounded offline correction completed as implementation commit
`9a8b0e3ffe9df35c832efa099ab6b67b786d7ac8` (tree `9f302c880af66f96f15dbab7981acdab5761c19c`).
Producer and validator now parse exact whitespace-delimited evidence tokens, require each expected database/media and
backup/VERIFY marker exactly once and in order, and reject absence, reordering, embedded-marker forgery and
duplication even when sqlcmd emits one physical line. Sanitized evidence publication also preserves genuine input
line records rather than string-collapsing nested arrays. Any new reset attempt requires a new absent external evidence
directory plus independent review and approval of the ledger-only descendant.

Sol High review of exact `1ff91c5c` found the catch/recovery helper still used standalone-line matching for the
VERIFYONLY completion marker. The P1 correction completed at exact implementation boundary
`d96bb149bd13f842b8d461ee718c4bc8f24076c9` (tree `93e6366c9bf79adab98de14038c73d46eee3b44d`).
Recovery now uses the same unique ordered token contract and trusts VERIFY evidence only when `DATABASE=RhemaERP` and
the exact `BACKUP_MEDIA_ID` agree with the durable phase-03 database/media identity. Coalesced recovery tests cover a
positive verified state; missing, duplicate, reordered, embedded, wrong-database and wrong-media tokens; and
post-verification material mutation with truthful verification downgrade. The preserved failed runtime evidence and
backup were not accessed or changed. A new attempt remains prohibited pending Sol High approval.

The fixed backup filename would itself prevent a fresh attempt while correctly preserving the first verified backup.
The operational correction completed as exact implementation commit
`d76df29d2e382116e59299aa561de0310a2ba7fb` (tree `9349b7dc1c067f2968452229cb3e9e2678a03e3a`).
Each attempt now generates its 32-lowerhex media ID before path resolution and derives the sole allowed filename
`RhemaERP_DISPOSABLE_RESET_COPYONLY_<mediaId>.bak` beneath the server backup root. Phase-03/04, terminal status,
recovery, current/verified hash records and the package validator bind that exact media/filename pair. The historical
fixed-name package remains validator-compatible and untouched. Tests prove the prior file can coexist with two unique
attempts, while collisions, malformed/path-like media IDs, wrong filenames/media and hash-record tampering fail closed.
Another runtime attempt remains prohibited until the ledger-only descendant receives Sol High approval.

Sol High review of `703eac84` required final ownership and evidence-version hardening. The correction completed as
`1532d77c18bdcad245ad509a6dede606dcce9507` (tree `8b3caab03f549a44ab6cea5b3a00077f409fa0c0`).
An attempt owns no backup until its atomic `CreateNew` succeeds; a derived-path collision therefore cannot be hashed or
reported as created/preserved. New packages use explicit V2/media-bound identity and bind the secret full server path
only by SHA-256 through phase-03/04, status, recovery and validation. Only exact approved historical commit `22b27ab`
may use the fixed-name legacy shape; deleting version/name/path-hash fields from a new package is refused. Full offline
gates passed; runtime remains prohibited pending Sol High approval.

`ResetDisposableDevelopment` is deliberately separate from final-clone cutover. It requires an exact local
case-sensitive `RhemaERP` connection from `RHEMA_GL_DISPOSABLE_DEVELOPMENT_CONNECTION`, explicit
`-ConfirmDisposableDevelopmentReset`, the exact process attestation shown below, reviewed commit/tree equality,
a wholly clean repository before connection parsing and immediately before mutation, all C6-C8 flags false, and
a new absent external evidence directory. It rejects failover, multisubnet, read-only-intent, network-library,
attach-file and user-instance ambiguity, then binds an exact binary `RhemaERP` name to the actual local instance.
Before any `SINGLE_USER`/drop it captures source history/fingerprint, atomically reserves a no-overwrite
target-derived backup, takes `COPY_ONLY CHECKSUM` with random media identity, records backup creation before a
separate `RESTORE VERIFYONLY`, and records SHA-256. It then persists monotonic `RESET_STARTED=true` evidence before
one SQL batch acquires the reset lock, rechecks server machine/instance/name/endpoint plus database/backup identity,
quiesces the source, proves the
exact captured source history and fingerprint, and immediately performs the sole drop/recreate. A successful reset
recreates only `RhemaERP`, applies the one exact current-model baseline, seeds twice with byte-identical Finance
invariants, proves zero-orphan history, and completes DBCC. Terminal success and failure packages use the dedicated
`DisposableReset` validator, bind every artifact by SHA-256, scan retained text/SQL/Markdown for machine or secret
material, and atomically write a complete manifest. Catch independently reconciles nonempty backup length and a
distinct current-material SHA even when phase-03 publication failed; it preserves the immutable phase-04 verified
hash and never calls current bytes verified unless both hashes match and VERIFYONLY evidence remains valid. Recovery records
`Last durable phase` separately from `Failed operation`. Any failure preserves backup/evidence and performs no
automatic retry, restore, cleanup, or second drop.

After Sol High approval only, an operator may prepare a new external evidence path and run:

```powershell
$env:RHEMA_GL_DISPOSABLE_DEVELOPMENT_CONNECTION = '<explicit local connection; exact RhemaERP catalog>'
$env:RHEMA_GL_DISPOSABLE_DEVELOPMENT_RESET_ATTESTATION = 'I_ATTEST_RHEMAERP_DEVELOPMENT_DATA_IS_DISPOSABLE'
$env:RHEMA_GL_REVIEWED_COMMIT = '<exact independently reviewed 40-hex commit>'
$env:RHEMA_GL_REVIEWED_TREE = '<exact independently reviewed 40-hex tree>'
$env:Finance__AccountingEvents__Enabled = 'false'
$env:Finance__ProducerIntents__Enabled = 'false'
$env:Finance__ProducerIntentGroups__Enabled = 'false'
./scripts/finance/Invoke-GlCutoverRehearsal.ps1 -Mode ResetDisposableDevelopment `
  -ConfirmDisposableDevelopmentReset -EvidenceDirectory '<new absent external directory>'
./scripts/finance/Test-GlCutoverEvidencePackage.ps1 -PackageKind DisposableReset `
  -EvidenceDirectory '<same external directory>'
```

```powershell
$env:RHEMA_GL_SOURCE_READONLY_CONNECTION = '<secure same-server connection; exact RhemaERP catalog>'
$env:RHEMA_GL_REHEARSAL_CONNECTION = '<same server; absent RHEMAERP_GL_REHEARSAL_* catalog>'
$env:RHEMA_GL_REVIEWED_COMMIT = '<exact independently reviewed 40-hex commit>'
$env:RHEMA_GL_REVIEWED_TREE = '<exact independently reviewed 40-hex tree>'
$env:Finance__AccountingEvents__Enabled = 'false'
$env:Finance__ProducerIntents__Enabled = 'false'
$env:Finance__ProducerIntentGroups__Enabled = 'false'
./scripts/finance/Invoke-GlCutoverRehearsal.ps1 -Mode RehearseFinalClone `
  -EvidenceDirectory '<new or empty raw-evidence directory>'
./scripts/finance/Test-GlCutoverEvidencePackage.ps1 -PackageKind FinalClone `
  -EvidenceDirectory '<raw-evidence directory>' -WriteManifest
```

An operator must review a NO-GO package; it is not permission to repair, backfill, stamp, weaken a preflight,
or retry against a different source. Cleanup remains a separate explicit `DropRehearsal -ConfirmDrop` action.

- [ ] Freeze the exact reviewed commit SHA/tree, set both exact process values, and prove HEAD/tree equality plus
  a completely clean tracked/untracked repository before any SQL contact.
- [ ] Capture the source's sanitized server/database identity, current migration history, schema fingerprint,
  Finance control counts, explicit disabled cutover flags, and the absent target identity without exposing credentials;
  verify the restored target history equals that fresh source history before applying anything.
- [ ] Run every migration preflight in order. Do not weaken or bypass the Phase 2 singleton-role, Phase 3
  immutable-layout, Phase 4 historical-FX, Phase 5 governed-segment, or C1-C8 lineage checks.
- [ ] Atomically reserve the absent target-derived backup with `FileMode.CreateNew`; take a `COPY_ONLY` backup
  with `CHECKSUM`, `NOINIT`, `NOSKIP` and the recorded fresh media identity; record the SHA-256, run `RESTORE VERIFYONLY`, and
  prove that the backup can restore to a new prefix-validated rehearsal database.
- [ ] Run `DBCC CHECKDB ... PHYSICAL_ONLY` on the restored rehearsal database before migration.
- [ ] Use only `scripts/finance/Invoke-GlCutoverRehearsal.ps1` with a target matching
  `^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$`. Never point the rehearsal connection at `RHEMAERP`.
- [ ] Apply the exact pending migration delta to the isolated restore; run the controlled application seed
  twice and prove the Finance invariant snapshot is byte-identical across both passes.
- [ ] Exercise representative Finance, Procurement, Inventory, Sales, and Payroll postings and exact retries
  on every enabled statutory book. Reconcile journal, account-transaction, posting-event, AccountingEvent,
  producer-receipt, and owner back-reference identities.
- [ ] Exercise failure injection after owner mutation and before commit, prove rollback, then prove durable
  failure evidence is written only after rollback.
- [ ] Re-run backend/frontend, EF model-parity, exact one-baseline discovery, zero-to-current script, credential-scan,
  ancestry, diff, and clean-status gates from the frozen candidate.
- [ ] Obtain independent Finance/DBA review of migration evidence, balances, source identity, retry behavior,
  feature flags, backup/restore evidence, and rollback decision points.

## Feature-flag release sequence

All three cutover flags must remain false during schema deployment, seeding, validation, and the initial
post-deployment observation window:

1. keep C6 `Finance:AccountingEvents` false;
2. keep C7 `Finance:ProducerIntents` false;
3. keep C8 `Finance:ProducerIntentGroups` false.

Enabling a flag is a separate, explicitly approved operational change. Enable in C6, then C7, then C8 order,
with health, reconciliation, failure-evidence, and retry checks between steps. Never enable C7 while C6 is
false or C8 while either C6 or C7 is false. Record the actor, timestamp, environment, approved change ticket,
pre/post values, and rollback owner for each transition.

## Backup, restore, and rollback decision

The deployment owner must name the restore point and confirm the recovery-time and recovery-point objectives
before any migration. The restore plan must include application quiescence, connection termination, backup
selection by checksum, restore under DBA control, post-restore integrity checks, migration-history verification,
and Finance reconciliation before traffic resumes.

Do not attempt a source-code-only rollback after any C1-C8 migration or multi-book posting has committed.
If a migration fails, a preflight invariant changes, balances do not reconcile, producer identity diverges,
or an exact retry creates additional accounting effects, stop the rollout, keep all cutover flags false, retain
failure evidence, and invoke the approved database restore plan. Resume only from a newly reviewed candidate
after root-cause correction and another isolated rehearsal.

## Go/no-go record

A release is **NO-GO** unless all of the following are attached to the change record: independently approved
candidate SHA; exact pending-migration list; successful isolated clone rehearsal; verified backup and restore;
two-pass seed checksum; module posting/retry reconciliation; zero-error code and frontend gates; EF parity;
exact one-baseline no-connect migration discovery; zero-to-current SQL artifact checksum; explicit disabled flag values; and
named deployment, Finance, DBA, independent-review, and rollback owners.

No item in this document authorizes migration application or feature enablement by itself.

### Disposable reset attempt 04 and zero-history correction

The separately authorized attempt 04 ran exact reviewed commit
`2de5f800c47e58fe62ca1715db0ac4aef5001327`, tree
`8a2f30bead3500138657c85a8447765330500515`, against the corrected exact local endpoint and stopped fail-closed at
`SOURCE_CAPTURE`. The source was the already recreated schema-empty development database: fingerprint
`0|EMPTY|0|0|0`, zero migration-history rows, no backup, and no reset mutation in this attempt. Its last durable phase
was `OFFLINE_GATES_COMPLETE`; the terminal package validator passed and external attempt-01/02/03/04 evidence remains
preserved.

The defect was evidence publication, not database state: piping an empty PowerShell array to `Set-Content` did not
create `source-migration-history.txt`, so phase-02 could not hash the zero-row history. Correction
`9554038d207b7ac7ef541f18ba493d277363c53e` (tree
`3f3918222e15e6064110bd1918a0ce2fe2d726b1`) replaces every reset and FinalClone migration-set writer with one atomic
`RHEMA_MIGRATION_HISTORY_V1` record. It publishes an explicit `COUNT=0|STATE=EMPTY` marker for zero rows and a
count-bound ordered list for populated history, then parses the written artifact back before fingerprint/delta use.
Reset phase-02 binds the exact source-history SHA-256; terminal status/summary and manifests bind the format and
artifact hashes. Independent validation refuses missing, empty, count/state-tampered, duplicate, malformed, or
out-of-order records. No database, process connection variable, or preserved external evidence was accessed during
the correction. A new reset attempt requires independent Sol High review and separate authorization.

The V2 early-state/legacy discriminator correction completed as `5cf447bac84d14a67a77bd955de28176b4a21774`
(tree `0c43d41ee271a2eab70c33777feb1df4d01541ab`). Empty media/name/path-hash is valid only for unowned,
uncreated, incomplete pre-phase-03 failures at NOT_STARTED, OFFLINE or SOURCE_CAPTURE. Once path identity is resolved,
the complete strict tuple is mandatory. Legacy acceptance is limited to the exact documented `22b27ab` commit,
`13f1eb03` tree, failed SOURCE_CAPTURE outcome and absent V2/ownership fields. Same-basename recovery from another
full path is refused by path-hash mismatch. Runtime remains prohibited pending approval.

Owned-empty/unresolved-state correction `6f308860c1ff4cdfb4281c7f960b365e527d75a7` (tree
`d4d9e0ded646bd1a83fb315fef66162dd8437270`) distinguishes a successful zero-byte owned reservation from material
backup and requires present-exact-empty unresolved V2 fields with no backup/reset claims. The strict 22b historical
fixture binds its known tree, media, 453042176-byte length and SHA. Offline gates pass; runtime remains prohibited.

The exact-state validation correction completed as `1798895595500bcc3b0d76157e4bb0255457e608` (tree
`a89147ac9eec17b5b5a956cd16ca09b098a5dfcc`). Every V2 reset state property and identity field must now be present,
non-null, and of its exact JSON type before state-specific values are evaluated. Unresolved and owned-empty packages
therefore cannot use omitted, null, string-coerced, or contradictory values to obtain a manifest. The approved
historical exception is encoded entirely by its exact reviewed commit/tree, failed SOURCE_CAPTURE outcome, media ID,
453042176-byte current backup and SHA-256, false completion/verification/reset claims, optional exact source
fingerprint, and absent V2-only identity fields. The complete reset safety/evidence and FinalClone safety/evidence
suites passed serially from the clean implementation commit. No database, connection variable, preserved evidence or
backup was accessed; runtime remains prohibited pending independent Sol High review.

The material-reconciliation state correction completed as `e29e92949da5bd09d5f4b27c69f800c81b655651` (tree
`5d7530a75dd10a1a49c9bf1173877e510e0d9cc9`). V2 terminal status always records
`backupMaterialStateReconciled=true`; the validator requires that property to exist, be non-null, have Boolean type,
and remain true for unresolved, resolved-unowned, owned-empty, and materialized paths. Both non-material resolved
states also require `backupHashMatchesVerified=false`. The one approved historical package now requires the exact
recorded source fingerprint and Boolean reconciliation state in addition to its existing fixed identity and outcome.
Deletion, null, wrong-type, false-reconciliation, false historical identity and contradictory hash-match tampering are
refused. All reset and FinalClone offline suites passed serially from the clean implementation commit. No database,
connection variable, preserved evidence or backup was accessed; runtime remains prohibited pending Sol High review.

### Disposable reset attempt 06 and baseline SQL grammar correction

The separately authorized attempt 06 ran exact reviewed commit
`6bf2e211d05146def54eaa49e4f76175aa4456d8`, tree
`1e0ebed923749312d71c83548fe7847861953c34`, once. It completed the verified attempt-unique COPY_ONLY backup and
database recreate, then stopped fail-closed at `APPLY_MIGRATIONS` with last durable phase `DATABASE_RECREATED`.
Source fingerprint was `0|EMPTY|0|0|0`; the migration transaction rolled back, leaving zero target-history rows and
only `__EFMigrationsHistory`. Backup media `1e2c11f6f5de410e809e54b3b4edff1e` remains preserved outside the repository
as `RhemaERP_DISPOSABLE_RESET_COPYONLY_1e2c11f6f5de410e809e54b3b4edff1e.bak`, 3,198,976 bytes, SHA-256
`ABED861CA4DC2EAFE5C230B210A7019FF67EF5C9101866081437B76E01ECC7F1`; VERIFYONLY passed and the current hash still
matches. No seed, retry, restore, cleanup, feature enablement, or additional mutation followed.

Offline parsing identified one exact historical grammar defect in
`TR_ProducerIntentGroupAttempts_C8Immutable`, sourced from archived migration
`20260908120000_AddProducerIntentGroupsC8`: the outer `IF EXISTS (` was missing its closing parenthesis immediately
before the C8 attempt-authority THROW. Correction `0915baf8` adds only that delimiter to the compiled baseline
helper, without changing the predicate or economics. The complete regenerated zero-to-current script now parses
under TSql160Parser with all 1,280 THROW statements; a deliberate removal of the same delimiter reproduces
TSql160Parser grammar error 46005 near THROW. SQL Server error 102 remains the separately observed attempt-06 runtime
fact. The terminal evidence validator also accepts canonical signed nonzero Int32 process exits such as
`-532462766`, rejects malformed/zero/overflow/duplicate markers, and binds an `APPLY_MIGRATIONS` failure to the
matching failed dotnet evidence. A separate copy of attempt-06 evidence validates and manifests with this correction;
the preserved external package and backup were not modified. Offline reset/FinalClone safety and tamper suites,
deterministic archived-SQL inspection, Data/API Release builds, exact one-baseline discovery, no-pending-model, and
zero-to-current generation pass. No further reset is authorized until independent Sol High approval of the final
clean candidate and a new explicit operational authorization.

Sol High follow-up correction `a7644fdb17d69ec4b225bd45a3d378e31dac5d60` separates a native
`APPLY_MIGRATIONS` failure from a successful migration command followed by
`CAPTURE_TARGET_MIGRATION_HISTORY` or phase-07 publication failure. Completed offline, migration, target-history,
seed/invariant, and DBCC phases now require their exact retained artifacts and SHA-256 status bindings; deleting an
artifact or its binding cannot be repaired by re-manifesting. Native migration failure requires one signed nonzero
Int32 failure marker and no target history, while post-command capture failure requires the exact success/zero marker
and no validated history, and phase-07 publication failure requires success/zero plus the exact validated baseline
history. Behavioral fixtures cover both truthful outcomes and deletion, binding, operation-downgrade, and marker
tampering. Reset and FinalClone offline safety/evidence suites and the full generated-SQL grammar gate pass. No
database, connection environment, preserved runtime evidence, or backup was accessed by this correction.

### Disposable reset attempt 07 and seed dependency correction

The separately authorized attempt 07 ran exact reviewed commit
`8885db288b93f7392dfd05d6671ab2d76136ba97`, tree
`4040a6ab43ca0fd55255d98c709c0622748900ee`, once. It completed phases 01-07, applied the exact baseline
`20260913162402_DisposableDevelopmentCurrentModelBaseline`, and then stopped fail-closed during seed pass 1 with
last durable phase `MIGRATIONS_APPLIED` and failed operation `SEED_AND_INVARIANTS`. The source fingerprint was
`0|EMPTY|0|0|0`; final migration count is one, latest is the exact repository baseline, and orphan count is zero.
The harness did not claim seed, invariant, or DBCC completion.

The attempt-owned COPY_ONLY backup remains preserved outside the repository as
`RhemaERP_DISPOSABLE_RESET_COPYONLY_317044e55b4e4b2db6b4dcb1f1ee1e61.bak`: 16,900,096 bytes, media ID
`317044e55b4e4b2db6b4dcb1f1ee1e61`, SHA-256
`762AD9FC047EC713D90D5F341BFF33FD93D08CBE51665CBE218E96624E155120`. Backup creation, CHECKSUM/VERIFYONLY,
preservation, and current/verified hash equality are recorded. The DisposableReset package validator passed and
wrote a manifest whose file SHA-256 is
`C0590BBB53DB4F7DA953C1A8AA523527B9B6332408C92F851B4C412E489C235E`. No automatic retry, restore, drop, cleanup,
or feature-flag enablement followed.

The failure was an API command-host dependency graph defect, not a migration or Finance schema defect:
`AddDatabaseSeeding()` registered `ProcurementSupplierOnboardingTestSeeder` but the reduced seed host did not register
its required `IFinanceAccountProvisioningService`. Correction
`d1237ffb6df13e93ec1e54e8f6ffc84d95821b52` (tree
`2f9dfb22f1126e217040f2ebf36441b17d85e077`) centralizes the production
`FinanceAccountProvisioningService` as a scoped, TryAdd-based Finance boundary and declares it in the seeding
composition with one scoped default-tenant system context. Existing request-backed production registrations retain
precedence. Procurement continues to submit provisioning intent through Finance; no direct Account writer, stub,
legacy adapter, migration, schema, or feature-flag change was introduced. Offline dependency validation resolves all
11 registered seeders under `ValidateOnBuild`/`ValidateScopes`, verifies standalone supplier-command composition,
scope isolation, and host override behavior; Payroll provisioning and Data/API Release gates pass. A future reset is
not authorized until this exact descendant is independently approved and a new attempt is explicitly authorized.

### Archived migration test compatibility after the D3 baseline

The ordinary `ErpSystem.Api.Tests` build had 42 `CS0246` errors in 31 test files after the 456-migration source
chain became the deliberately uncompiled `LegacyMigrationsArchive`. The errors named 37 historical migration
types; they were test-compile defects, not a reason to restore the obsolete chain to product migration discovery.
The corrected tests inspect exact archived source and SQL literals for historical transition contracts while
checking current schema/discovery assertions against the sole baseline
`20260913162402_DisposableDevelopmentCurrentModelBaseline`. Classification authority/cardinality,
preflight-before-mutation, bounded-down, idempotency, and compatibility assertions remain active.

Eight C2-C8 migration bodies are linked explicitly into the test project only so the existing prefix-safe disposable
SQL Server predecessor rehearsals still exercise their original transitions when their opt-in test connection is
provided. They are not compiled by `ErpSystem.Data`, are not discoverable by the application migrations assembly,
and cannot restore the legacy runtime chain. The CRM/project shared-chain test now validates chronological archived
SQL contracts because EF can no longer migrate to deliberately archived IDs. Ordinary test-project compilation,
58/58 selected converted contracts plus one environment-gated skip, the 3 archive-reader safety tests, the 4/4 seed
dependency graph, and the 3/3 Payroll Finance-provisioning gates pass offline. No database, connection environment,
preserved evidence package, or backup was accessed. A further disposable reset remains unauthorized pending Sol High
review of the final clean candidate and a new explicit operational authorization.

### Disposable reset attempt 08 and retry-safe Finance provisioning

The separately authorized attempt 08 executed exact approved commit
`d1565ebc615879ee728f3004c0a5911410dbb5af`, tree
`a668571f87c4dd75a501362281cb8b5aba294966`, exactly once. It completed through durable phase
`MIGRATIONS_APPLIED`, with source fingerprint
`1|20260913162402_DisposableDevelopmentCurrentModelBaseline|0|0|0`, repository/final migration count one, exact
latest baseline, and zero orphan migrations. It then stopped fail-closed during seed pass 1 at
`SEED_AND_INVARIANTS`: `FinanceSegmentDimensionManifestSeeder` began a user transaction outside the configured
`SqlServerRetryingExecutionStrategy` while serving Finance provisioning for Procurement supplier onboarding. No
seed/invariant/DBCC success was claimed and no automatic retry, restore, drop, cleanup, or flag transition followed.

The attempt-owned COPY_ONLY backup remains preserved outside the repository as
`RhemaERP_DISPOSABLE_RESET_COPYONLY_8adbdc98e0ac434eb74fb44bdbc32b04.bak`: 37,806,080 bytes, media ID
`8adbdc98e0ac434eb74fb44bdbc32b04`, verified/current SHA-256
`29719F61D782865B0D35F55A09F5F5531C1A920AB1C76E3EBC1A16149CE44B18`, and bound path SHA-256
`C2244CF3C064D204A405C19F46DC886510A821231C3C53F7A5C82FA5977EB431`. The DisposableReset package validator
passed and wrote a manifest whose file SHA-256 is
`6917B90D07AB40A93025181DCA60BF484A8E56F3FB512802D96704660164F684`. All C6/C7/C8 flags remained false.

Correction `0d580a1d12e405c1124ad3f688ae38aff05a79cb` (tree
`676e778fb4b9d28b4d5ecc431f1f69100f3c92d1`) moves the complete service-owned provisioning unit—manifest and
account reads/writes, `SaveChanges`, and commit—inside `CreateExecutionStrategy().ExecuteAsync`. An existing EF or
ambient caller transaction remains caller-owned: the service neither nests nor commits it. Failed owned attempts
restore the caller's exact tracked state before retry, preventing duplicate stable IDs after a post-write transient;
the operation timestamp is stable across exact retries. Six focused relational tests cover SQL Server strategy
selection without connecting, owned retry/idempotency, post-write transient recovery, caller rollback, logical
failure rollback, and transaction-neutral leaf boundaries. The combined provisioning/seed-DI/Payroll slice passes
19/19, and Data, API, and ordinary API-test builds pass with zero errors. No migration, schema, Finance ownership,
feature flag, database, connection environment, or preserved evidence was changed or accessed by this correction.
A future reset can safely treat the one-baseline partially seeded database as a disposable source only through a
new, independently approved harness run that re-captures and rechecks its exact history/fingerprint, verifies a new
attempt-owned backup, and performs the normal drop/recreate flow. No further runtime attempt is presently authorized.

Sol High concurrency follow-up `0b36d069a58012c3d4d7295d217e954f693ebcfd` (tree
`55c76944f42aef87129bc3173f11e5af4bf95f8f`) closes the remaining read/insert race without a schema change.
Every SQL Server call acquires two exclusive transaction-scoped application locks in a fixed order before any
manifest or account read: a tenant manifest lock protects deterministic segment, dimension, classification and book
materialization, then a tenant/canonical-account lock protects account-code/number convergence. Concurrent callers
therefore converge on one durable account ID and one manifest/mapping set; an exact later call is read-only.
Caller-owned EF or ambient transactions retain both locks and remain caller-committed.

The service-owned retry path now refuses added/deleted entities, temporary keys, loaded or materialized navigation
graphs, and modified relationships before its first database operation. Scalar modified state remains supported and
is restored exactly after a failed attempt; operation-created tracked entities are detached after rollback or
successful commit so sequential provisioning remains retry-safe. Offline relational tests cover canonical lock
identity, no-write exact retry, post-write transient restoration, caller transaction rollback, logical rollback, and
pretracked added/scalar-modified/navigation/FK state. Ten tests pass. A genuine two-independent-DbContext SQL Server
race gate is also present; it was skipped because the explicit `RHEMA_TEST_SQLSERVER` opt-in was absent, so no
connection string was read and no database was accessed. Independent Sol High approval and new operational
authorization remain required before another reset.

### Disposable reset attempt 09 and seed-order tracker isolation

The separately authorized attempt 09 executed exact approved commit
`4a6795bfe2bd22f4ba708a6cf3c8497923077aed`, tree
`4f5e257af8c5fb67ae2bc70b806464f78fa09606`, exactly once. The operator's `Tee-Object` wrapper failed after
starting its child process; the already-running harness was monitored to its terminal state and was not reinvoked.
It completed through durable phase `MIGRATIONS_APPLIED`, with the exact single baseline history row
`20260913162402_DisposableDevelopmentCurrentModelBaseline`, then stopped fail-closed during seed pass 1 at
`SEED_AND_INVARIANTS` with `FINANCE_ACCOUNT_PROVISIONING_TRACKER_NOT_RETRY_SAFE`. Source fingerprint was
`1|20260913162402_DisposableDevelopmentCurrentModelBaseline|0|0|0`; final migration count was one, latest was the
exact baseline, and orphan count was zero. C6/C7/C8 remained false. The terminal DisposableReset package validator
passed and wrote its manifest. No automatic retry, restore, drop, cleanup, or feature transition followed.

Attempt-owned recovery media remains preserved outside the repository as
`RhemaERP_DISPOSABLE_RESET_COPYONLY_0482dc7537504e69ab772fc98feedb83.bak`: 90,038,272 bytes, media ID
`0482dc7537504e69ab772fc98feedb83`, verified/current SHA-256
`34E436FFDEBB3296844ADF18C63F95A14B8DC939010221E9DFCED83DD2C05D64`, and bound path SHA-256
`5A5B53F4EFFDAC5DC2ECD4B56816B97D93896080DDB024C0957E42752B757499`. The current disposable database remains
one-baseline and partially seeded; it is eligible only for a fresh separately reviewed and authorized reset that
repeats every fingerprint, clean-tree, identity, and attempt-owned-backup control.

The root cause was the real `DatabaseSeedingService` pass order: `FinanceDataSeeder` leaves a large, entirely
unchanged materialized graph in its scoped `ApplicationDbContext` before Procurement supplier onboarding invokes
Finance provisioning. That graph is legitimate read state, not caller-pending work, but the prior service-owned retry
guard correctly refused it because retry restoration could not safely reconstruct navigation fixup.

Correction `c76945b830477262a2bfd6a0d5b6002c510295dd` (tree
`52213fdbd45689e4e156ecd9c84ffa0579b1b86d`) isolates service-owned provisioning in a child DI scope with a distinct
`ApplicationDbContext` bound to the exact same provider and connection identity. The parent scoped current-user
service is retained, so tenant/user authority cannot drift. Parent pending entity, temporary-key, scalar, FK, or
relationship state is rejected before database work, while an arbitrary unchanged/materialized parent graph remains
untouched. The isolated retry unit may clear only its own tracker after a failed transaction. Existing EF or ambient
caller transactions still use the original context, acquire the same Finance locks, and remain caller-committed.
Procurement defers its independent tenant mutation until the three Finance provisioning calls finish, without adding
an independent save/commit boundary.

The actual Finance-then-Procurement seed order passes twice in one long-lived relational context while preserving
more than 50 unchanged tracked entries and materialized relationships; it converges to exactly three provisioned
accounts, two structures, six dimensions, six account segment values, and nine book mappings. Provisioning retry and
transaction tests pass 14/14, including a post-write transient retry and pending-relationship refusal; seed dependency
and Payroll provisioning tests pass 7/7. Data and API Release builds pass with zero errors.

For the next separately authorized run, operator output must be captured by a parent-owned process wrapper that
atomically creates the absent log before `Process.Start`, supplies arguments via `ProcessStartInfo.ArgumentList`,
uses `UseShellExecute=false`, redirects and concurrently drains both output streams, and awaits the exact child exit.
Do not put `Tee-Object` or another fallible pipeline stage around the one authorized harness invocation. No database,
process connection variable, preserved evidence package, or backup was accessed by this correction.

Sol High identity-boundary correction `95fcbbf628e02b55b16913335553499f6f268d7b` (tree
`e4ab0258e2c63c43755581df20fcd8861c35b9a6`) removes comparison of mutable live connection strings. The child-scope
boundary now derives both identities only from immutable EF relational options, requires distinct contexts and
connections, and compares exact provider plus canonical non-secret connection material. SQL Server identity binds
normalized data source, exact database, integrated/SQL/connection-string token authentication mode and principal,
encryption, trust, application intent, failover, multisubnet and every other supported non-secret builder option;
password material is removed before identity construction and is never logged. A raw token or configured mutable
`DbConnection` without an immutable, non-secret principal identity refuses isolation. SQLite retains exact configured
target/options matching for relational offline tests.

The provider-realistic offline regression simulates SqlClient's post-open password redaction by changing only the
parent live connection while retaining its immutable configured options; the fresh child still matches. Different
server, database, integrated/SQL user, Azure managed-identity principal, encryption, trust, read intent, multisubnet,
or failover settings refuse. Provisioning tests pass 19/19; the full production seed-order test passes 1/1 and the
seed-DI/Payroll slice passes 7/7. No database, configured environment, preserved evidence, or backup was accessed.
Runtime remains prohibited pending independent Sol High approval and separate authorization.

### Disposable reset attempt 10 and GLF003 lineage correction

The separately authorized attempt 10 executed exact approved commit
`65bd3c311be8af9427c93418ec831916a81181b2`, tree
`f2d77951afd6e12819694293890686bf3144705f`, exactly once. It completed the exact one-row baseline migration and
seed pass 1, then stopped fail-closed at `SEED_AND_INVARIANTS` when the first invariant reported
`GLF003: enabled account/book mapping has invalid lineage`. Last durable phase is `MIGRATIONS_APPLIED`; seed pass 2,
the second invariant snapshot, and DBCC were not run or claimed. Source fingerprint was
`1|20260913162402_DisposableDevelopmentCurrentModelBaseline|0|0|0`; final history contains only that exact baseline
with zero orphans. C6/C7/C8 remained false, and no automatic retry, restore, drop, cleanup, or feature transition
followed.

Attempt-owned recovery media remains preserved outside the repository: media
`3e2b84116d7641d8be554d5f7ee86fa1`, file
`RhemaERP_DISPOSABLE_RESET_COPYONLY_3e2b84116d7641d8be554d5f7ee86fa1.bak`, 90,103,808 bytes, verified/current
SHA-256 `0DC9330B0F5E54EDE4193582F38F83B452C9289AB169425EB9FE70E019E96A9A`, and bound path SHA-256
`80475AF48214C8F3253F14E6B6801CEE259C52CA48A1F8B99303E038BC2FF51D`. The terminal DisposableReset package
validated and its manifest-file SHA-256 is
`EDC43CC9DDDD2EBC7243674B052515D000725006505B5EA093DA2FA6600F1DD5`.

Authorized read-only diagnosis found exactly 144 GLF003 rows: 48 Finance-reviewed accounts across the three
`IFRS`, `LOCAL_STATUTORY`, and `MANAGEMENT` books. The sanitized ordered identity-set SHA-256 is
`90AF39B3830AFFE58EDB66D7626FF0841173C8CDF9EE72DB6D2C3D87EE4D18C9`. Every account, classification, tenant,
book, and core-account-type relationship was exact; all classifications were active posting leaves. The sole defect
on every row was that the manifest had enabled applicability while each freshly created book was still
`Configuring`, inactive, and disallowed posting.

Correction `86cf81044b9d7e48ea69807b2d6b33f24c1f1a2b` (tree
`3947a87af6d87b2b4eec08b172b2718cc8ada917`) preserves those exact prepared mappings but enables them only when
the governed book is active and allows posting. It repairs only untouched `FIN-CLASSIFICATION-3.0` rows; an
administrator-owned invalid enabled row is never rewritten and instead fails the seed's complete enabled-lineage
audit. Existing active/posting books retain enabled provisioning behavior. Production-order relational coverage runs
Finance then Procurement supplier onboarding twice in separate scopes, proves GLF003 is zero after each pass and
canonical Finance authority state is byte-identical, then proves an exact manifest/provisioning retry performs zero
authority writes. Focused classification/provisioning/DI/Payroll tests pass 64/64, and Data, API, and ordinary API-test
builds pass with zero errors. All four clean-tree rehearsal safety/evidence suites pass. No operational retry is
authorized; independent Sol High review and a new explicit attempt authorization remain required.

Sol High provenance correction `21be805158fba806a39487eb701875486d8ffa15` (tree
`5dcab656cd00ce931331fcb7c3a0699c7de5efdd`) removes classification-ownership laundering from the legacy broad-leaf
upgrade. Automatic classification repair and inactive-book disablement now require the mapping row itself to be an
exact untouched `System (FIN-CLASSIFICATION-1.0)`, `2.0`, or `3.0` row. An administrator-created row with no
`UpdatedBy`, and a system-created row later updated by an administrator, are both preserved; invalid enabled lineage
then fails closed instead of being rewritten. Exact V1 system-owned repair remains supported. Classification tests
pass 39/39, production-order seed coverage passes 1/1, and provisioning/DI/Payroll tests pass 26/26. No database,
connection environment, preserved evidence, or reset operation was accessed.

### Disposable reset attempt 11 PASS

The separately authorized attempt 11 executed exact Sol-High-approved commit
`008e29ee0427620f62a21d89b24a98d2af43ddc4`, tree
`4fa31991cadb4a55ea143f036b45410279d52cc2`, exactly once and reached terminal `PASS` / `COMPLETE`. The source and
final database fingerprint is
`1|20260913162402_DisposableDevelopmentCurrentModelBaseline|0|0|0`. The exact sole baseline migration was applied
with zero orphans, seed passes 1 and 2 completed, their canonical invariant files are byte-identical at SHA-256
`7F5BE0778FAD94C74A2C9E7836AA0B061B0DDFF612054BDEE0CF68766A607D29`, DBCC passed, and all C6/C7/C8 process flags
remained false.

Attempt-owned recovery media is preserved outside the repository: media
`200133be1fb445d3ab328d1ca0170b01`, file
`RhemaERP_DISPOSABLE_RESET_COPYONLY_200133be1fb445d3ab328d1ca0170b01.bak`, 103,800,832 bytes, verified/current
SHA-256 `332E61F0A309F511596A399AFFDD51E369B96B4033D0436BBEAC1F483A94AAD7`, and bound path SHA-256
`875DB16E5607512C70131C1647983DE824956C6151A3C75FB43CBF5A66F0DE4A`. COPY_ONLY/CHECKSUM creation and
RESTORE VERIFYONLY evidence passed. The package validator passed and wrote a 39-entry manifest whose file SHA-256
is `6073783A3F67316F1F5D8216F118770593B07BB3E2A1FA8E3C7446EA19D88868`; the separate operator-log SHA-256 is
`7458540AFD36319F990EF13B1C54D24C8C294D9647B81FD55AB052FA97F81DFA`.

Independent read-only verification found 1,557 user tables, exactly 459 enabled database triggers (ordered name-set
SHA-256 `0351569ED92F0C2657193E1F4A57A06811616D0E2C14F723E1D58437E6B49926`), the sole required scalar function
`fn_ProcurementRfqSourceLineIdentity`, and the sole required view
`vw_ProcurementReceiptDocumentReconciliation`. GLF003 is zero. Provisioned accounts `1040`, `2210`, and `4930`
exist exactly once, with six total segment values and nine mappings: three each to the exact `ASSET_OTHER`,
`OUTPUT_TAX`, and `OTHER_INCOME` classifications across the three Configuring books. All nine mappings remain
disabled because those books are inactive and do not allow posting; no automatic book approval occurred. The
external attempt-11 evidence, backup, and log remain preserved and were not copied into the repository. No retry,
restore, drop, cleanup, or post-PASS database mutation followed.

### Scratch-15 check-constraint dual-authority checkpoint

The attempt-15 scratch diagnostic remains a review-only terminal capture: 835 exact source rows and 835 exact SQL
Server storage rows were durably published, the owned scratch catalog was dropped with absence proof, and no
schema/DBCC PASS or feature-activation claim was made. Offline exhaustive reconciliation proves the analyzer's 230
reported drifts were all built-in function-identifier casing artifacts; the corrected closed structural analysis is
835 matches, zero drift, and zero unsupported nodes.

The committed dual authority now binds exact source raw identity, exact captured storage raw identity, and a
collision-safe structural semantic identity for every ordered table/name tuple to the attempt-15 terminal manifest
SHA-256 `858FC627204D5A78DF181E1351828E56DAE1AAC1296939CA2184C770A5EE35EC`. It preserves literal, N-prefix,
collation, type/style, operator, NOT-scope, and SQL three-valued-logic distinctions and fails closed for unknown
functions or unsupported syntax. This offline proof does not authorize a new scratch execution, RhemaERP reset, or
C6/C7/C8 activation.
