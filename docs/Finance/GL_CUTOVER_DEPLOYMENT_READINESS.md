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
Finance invariant evidence. Failures preserve evidence and any exact target/backup; the harness never drops or
overwrites either automatically. Raw `sqlcmd` output is held only in an outside-package temporary file, sanitized,
then deleted; the package validator scans JSON, text, logs, checksums and SQL, independently re-derives pending/
orphan sets and the exact PASS target-history union, and verifies artifact, identity and ordered backup/restore markers.
Evidence must be external to the repository or below the dedicated ignored `.artifacts/finance-gl-rehearsal` root.

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
- [ ] Take a `COPY_ONLY` backup with `CHECKSUM`, record the SHA-256 checksum, run `RESTORE VERIFYONLY`, and
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
