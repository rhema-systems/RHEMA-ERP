# Finance Performance and Volume-Readiness Foundation

Date: 2026-08-10

Requirement: `NFR-PER`

Scope: Finance-only assurance; no external-module interface changes

## Outcome

This slice establishes a repeatable release gate for the Finance journeys most likely to affect
TDC operations as transaction volumes grow. It does not claim that a developer laptop result is a
production service-level agreement. Instead, it gives the team one reviewed workload catalogue,
one representative UAT volume baseline, deterministic query-count and database-time budgets, and
a targeted ledger index derived from the actual report predicates.

The governed workloads are:

1. central Finance posting;
2. journal inquiry and drill-down;
3. trial balance generation;
4. accounts-payable aging;
5. accounts-receivable aging;
6. bank reconciliation; and
7. period-close readiness.

## Why this matters to TDC

Financial correctness is not enough if a month-end trial balance, supplier-aging report or close
workspace becomes unusable at realistic volume. Conversely, adding many speculative indexes can
slow posting and complicate database maintenance. This foundation treats performance as an
evidence-based release decision: exercise representative data, record the query count and p95
database time, and tune only a workload that breaches its reviewed budget.

## Volume profiles

`FinancePerformanceVolumeProfile` defines two profiles:

- `DeveloperSmoke` is a fast query-shape check for ordinary pull requests.
- `TdcRepresentative` is the initial UAT baseline: 1,000 accounts, 50,000 journal entries,
  100,000 ledger lines, 25,000 AP documents, 25,000 AR documents, 50,000 bank-statement lines and
  500 close tasks.

The representative profile is a starting capacity assumption, not a production storage ceiling.
Before go-live, Finance and infrastructure owners must compare it with TDC's expected transaction
growth and increase it where the expected peak is higher.

## Release budgets

The central `FinancePerformanceReadinessPolicy` stores query-count and p95 database-time budgets for
every workload. Query count detects N+1 regressions and accidental per-row database access. Database
time detects degraded query plans and unbounded reads. A run passes only if both budgets pass.

The elapsed budgets exclude browser rendering, wide-area network latency and background contention.
Those layers require separate end-to-end UAT monitoring; the values must not be presented as a
contractual user-facing SLA.

## Database change

The existing trial-balance calculation scopes ledger lines by tenant, posting book and date before
grouping by account. Journal drill-down uses the same leading predicates and may then narrow to one
account. `AccountTransactions` therefore receives a composite index on:

`(TenantId, BookClassification, TransactionDate, AccountId)`

The account key is last so both all-account financial reports and single-account inquiries can use
the same index prefix. Existing settlement and close-workspace composite indexes already match their
critical read shapes, so this slice does not duplicate them.

## Repeatable verification

### Pull-request gate

Run the provider-neutral policy tests:

```powershell
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj `
  --filter FullyQualifiedName~FinancePerformanceReadinessTests `
  --configuration Release
```

These tests prove that all critical workloads remain governed, the representative profile cannot be
silently reduced below the agreed baseline, percentile calculation is deterministic, and a workload
cannot pass by meeting only one of its two budgets.

### SQL Server UAT gate

1. Apply the pending migration chain to a disposable/UAT database first.
2. Load or generate the `TdcRepresentative` profile for one tenant. Do not use live production data
   without the approved masking and access procedure.
3. Warm each workload once so database startup and first compilation do not distort the sample.
4. Execute at least 20 measured samples per workload using the same application build, database
   service tier and connection settings.
5. Record SQL command count and database elapsed milliseconds for each sample.
6. Evaluate the samples through `FinancePerformanceReadinessPolicy.Assess` and retain the result,
   execution plan for any failure, database specification and build commit in the UAT evidence pack.
7. Investigate failures; do not simply raise the central budget without Finance-owner and technical
   review of the query plan and end-user impact.

## Operational interpretation

- A query-count failure normally indicates an N+1 query or an accidental loop over database calls.
- A time-only failure normally requires plan, statistics, blocking and resource analysis.
- A pass on `DeveloperSmoke` does not replace the representative SQL Server gate.
- A single fast run is not p95 evidence; retain at least 20 measured executions.
- Index changes must be assessed for their posting-write cost as well as report-read benefit.

## Evidence and ownership

- Policy and profiles: `src/ErpSystem.Core/Performance/FinancePerformanceReadiness.cs`
- Ledger query/index mapping: `src/ErpSystem.Data/ApplicationDbContext.cs`
- Provider-neutral release tests:
  `tests/ErpSystem.Api.Tests/Services/Finance/FinancePerformanceReadinessTests.cs`
- Database migration: `AddFinanceLedgerPerformanceIndex`
- Finance owner handbook: local, Git-ignored `local-artifacts/finance-module-handbook`

The Finance product owner owns workload relevance and acceptable user experience. The database/API
owner owns measurement integrity and query-plan remediation. Infrastructure owns the UAT topology
and monitoring context. All three must approve any relaxation of a central release threshold.

## Boundaries

This foundation does not implement production observability, capacity procurement, disaster
recovery, availability, backup validation or cross-module end-to-end load testing. It provides the
Finance-owned workload evidence needed before those broader operational-readiness decisions.
