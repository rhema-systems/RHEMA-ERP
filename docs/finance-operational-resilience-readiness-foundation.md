# Finance Operational-Resilience Readiness Foundation

Date: 2026-08-12

Requirements: `NFR-AVL`, `NFR-REL`, `NFR-BCK`, `NFR-SUP`

Scope: Finance-owned controls and repeatable release evidence; infrastructure execution remains owned by IT

## Outcome

This slice turns Finance operational resilience from an informal checklist into a repeatable release gate.
It separates API liveness from dependency readiness, returns safe structured health diagnostics, and adds
one executable policy that refuses to pass unless current evidence exists for:

1. API availability;
2. database readiness;
3. backup freshness;
4. a successful restore drill;
5. retry-safe Finance posting after an interrupted/ambiguous response; and
6. a support diagnostic and escalation rehearsal.

The policy complements the existing Finance posting engine, `FinancePostingEvent` idempotency,
migration-sign-off diagnostics, and performance gate. It does not create a second posting or monitoring
framework.

## Availability probe correction

The application already exposed `/health`, `/health/ready`, and `/health/live`, but the endpoints used the
same unfiltered check set. A database or Redis outage could therefore fail liveness and encourage a host
or orchestrator to restart a healthy API process repeatedly.

The routes now have distinct meanings:

- `/health/live` executes process-only checks tagged `live`;
- `/health/ready` executes traffic/dependency checks tagged `ready`;
- `/health` remains the aggregate operator view; and
- `/health/shutdown` retains the shutdown-specific check set.

Every route returns the same JSON shape: overall status, UTC observation time, total duration, and sorted
check names/status/descriptions/durations/tags. Exception details and connection information are not
serialized.

## Evidence policy

`FinanceOperationalResilienceReadinessPolicy` evaluates the latest retained record for each required
evidence category. Missing, failed, stale, unreferenced, or implausibly future-dated evidence blocks a
pass. Older records may remain in the pack for trend analysis, but they cannot override the latest run.

The initial evidence-age windows are:

| Evidence | Initial maximum age |
|---|---:|
| Availability probe | 1 hour |
| Database readiness | 1 hour |
| Backup freshness | 26 hours |
| Restore drill | 95 days |
| Posting retry | 31 days |
| Support diagnostics | 31 days |

These are conservative development/UAT release gates. They are not contractual TDC availability,
recovery-point (RPO), or recovery-time (RTO) commitments. IT and Management must approve the production
service targets, backup schedule, retention, restore topology, escalation owners, and maintenance window.

## Repeatable UAT evidence procedure

### 1. Availability and readiness

From a machine outside the application host, retain the response, HTTP status, timestamp, environment,
build commit, and monitoring-run reference for:

```powershell
Invoke-RestMethod "$ApiBaseUrl/health/live"
Invoke-RestMethod "$ApiBaseUrl/health/ready"
Invoke-RestMethod "$ApiBaseUrl/health"
```

Prove separately that a disposable database outage fails readiness while liveness continues to pass.
Do not perform that test against an uncontrolled production environment.

### 2. Backup freshness

The infrastructure/database owner must retain the successful SQL Server backup job record and verify that
the backup covers the application database, attachments/file storage, configuration/secrets recovery
procedure, report artifacts that require retention, and audit evidence. A database-only backup is not a
complete `NFR-BCK` claim.

For SQL Server database evidence, record the latest full/differential/log backup timestamps and media set
from `msdb.dbo.backupset`/`msdb.dbo.backupmediafamily`. Do not copy credentials or unrestricted connection
strings into the evidence pack.

### 3. Restore drill

Restore the selected backup to an explicitly named disposable/UAT database, never over the source database.
Record start/end time, backup identifiers, restore target, operator, build commit, and every recovery step.
Then:

1. run SQL Server integrity checks approved by the database owner;
2. start the API against the restored target without applying unreviewed schema changes;
3. verify `/health/ready` and the migration state;
4. run Finance migration-sign-off diagnostics;
5. reconcile trial balance, AP/AR control, bank, fixed assets, WHT, and opening evidence;
6. open representative attachments and controlled report outputs; and
7. delete the disposable restore only after the evidence pack is reviewed.

The measured restore duration informs TDC's eventual RTO; backup timestamps and proven recoverability
inform the RPO. A successful `RESTORE VERIFYONLY` is useful media evidence but does not replace a full
restore and application-level reconciliation.

### 4. Posting retry and controlled recovery

Use a disposable/UAT tenant and one supported source workflow (for example AP invoice/payment, AR
invoice/receipt, cash/bank, or an approved opening batch):

1. submit a valid posting with a retained idempotency/source key;
2. simulate an ambiguous client outcome only in the controlled test environment;
3. retry the identical request;
4. prove that exactly one posted journal and one authoritative `FinancePostingEvent` exist;
5. prove the source document links to that journal or is repairable through the existing back-reference
   diagnostic; and
6. prove no partial operational status or balance mutation survived a failed transaction.

Retain the source identifier, posting event, journal number, retry result, diagnostic output, and test-run
reference. Never repair ledger data with direct SQL as part of this rehearsal.

### 5. Support diagnostic rehearsal

The support owner must demonstrate that an operator can distinguish process health, dependency health,
pending schema/migration work, duplicate/missing posting evidence, and source back-reference drift without
accessing secrets or editing posted records. Retain the issue/ticket number, first responder, escalation
route, diagnostic timestamps, resolution/decision, and verification outcome.

## Release decision

Feed the retained records into `FinanceOperationalResilienceReadinessPolicy.Assess`. The result is a
technical evidence decision, not an accountant or management signature. A pass requires every category;
there is no averaging and no waiver hidden inside the evaluator. Any accepted exception must be recorded
separately in the limitation/risk register by the authorized owner.

## Verification

```powershell
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj `
  --filter "Batch=FinanceOperationalResilience" `
  --configuration Release
```

Focused tests cover complete evidence, missing/failed/stale/unreferenced evidence, future timestamps,
latest-run precedence, and the structured health response's non-disclosure of exception details.

## Boundaries and outstanding acceptance

- Finance code can make posting atomic, retry-safe, diagnosable, and probeable; it cannot itself procure
  uptime, schedule backups, operate SQL Server, or approve TDC's SLA/RPO/RTO.
- Production monitoring configuration, alert routing, backup jobs, off-site retention, restore execution,
  infrastructure capacity, and support staffing remain IT/Management responsibilities.
- Cross-module recovery must be rehearsed after source-interface owners supply their production contracts.
- `FIN-LIM-0017` remains open until representative cutover and accountant sign-off are complete.
- `FIN-LIM-0055` tracks final operational execution and TDC acceptance of this foundation.
