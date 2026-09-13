# Finance multi-book GL cutover deployment readiness

Status: **code candidate only — deployment is not authorized**

Frozen integrated candidate: `cbc0d3c91142c63c4ea40f08f11d633752268367`

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
- EF model parity: no pending model changes. No-connect migration discovery: 456 entries, ending at C8.
- The 13-migration idempotent cutover artifact contains 13 migration-history rows, is 265,374 bytes, and
  has SHA-256 `CDBC813157F551BF192845DFBC05D74D55C6029620EA54A41A99E35769CF3D01`.

This evidence is repository-local. It does not replace independent review or environment rehearsal.

## Migration inventory and unapplied set

Repository-only discovery must return exactly **456** migrations and end at
`20260908120000_AddProducerIntentGroupsC8`. Migration discovery must be run with `--no-connect`; model
parity and script generation use EF design-time metadata and must not be given an environment connection
string. These checks do not prove that any environment has applied the migrations.

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

The code-candidate check generated an idempotent script for the full 13-migration cutover range (from the
last recorded applied migration through C8). Full-history idempotent generation from migration zero remains
blocked by a pre-existing legacy `AspNetRoles` data-operation/model-metadata mismatch. Deployment does not
require replaying the repository's entire history against an existing database, but the exact pending range
must be regenerated and independently checked after deployment-time discovery.

## Required preflight and rehearsal

Deployment remains blocked until every item below has durable evidence and an independent reviewer sign-off.

The Stage A `RehearseClone` mode remains a historical regression path and intentionally accepts only its
documented Phase 4 stop. The operational cutover path is `RehearseFinalClone`. It does not use an application
configuration fallback: both connection strings and all three disabled flags must be explicit process values.
It also requires the exact independently reviewed commit and tree as process values and refuses a descendant,
another tree, or any tracked/untracked workspace change before parsing a connection or contacting SQL Server.
It first binds that clean executed HEAD/tree into evidence, builds, proves EF model parity, discovers exactly 456 migrations ending at C8, captures source history,
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
recreates only `RhemaERP`, applies the exact unique ordered 456/C8 list, seeds twice with byte-identical Finance
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
- [ ] Re-run backend/frontend, EF model-parity, 456-migration discovery, idempotent-script, credential-scan,
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
exact 456 no-connect migration discovery; idempotent SQL artifact checksum; explicit disabled flag values; and
named deployment, Finance, DBA, independent-review, and rollback owners.

No item in this document authorizes migration application or feature enablement by itself.

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
