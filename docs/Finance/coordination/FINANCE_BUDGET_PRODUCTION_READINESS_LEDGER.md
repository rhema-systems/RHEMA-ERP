# Finance Budget Production-Readiness Ledger

## Approved objective

Implement the four user-approved Finance budget release slices:

1. dimension-aware official-budget revisions;
2. atomic AP cancellation/release and immutable posted-reversal evidence;
3. SQL Server migration/concurrency release gates;
4. read-only operational reconciliation for reservation and posting exceptions.

## Boundaries

- Base branch: `codex/finance-budget-posting-evidence`
- Reconciled base HEAD: `f868dd1b30a27ecabee2f865bd9b73687a5dfaba`
- Primary worktree is dirty and contains user-owned and earlier Finance changes; unrelated files remain untouched.
- Finance and the existing AP Finance adapter are in scope. Other module implementations remain read-only.
- The user explicitly authorized applying the budgeting migration to the local demo database on 2026-09-28.
- No remote push, PR, or merge is authorized.

## Execution packages

| Package | Risk tier | Status | Dependencies |
| --- | --- | --- | --- |
| BUD-PR-01 dimension-aware revisions | High: schema/accounting identity | Implemented, tested, and applied to the local demo database | Existing dimension-grained budget model |
| BUD-PR-02 AP terminal lifecycle/reversal evidence | High: workflow/posting/reversal | Implemented and regression verified | Existing commitment boundary |
| BUD-PR-03 SQL Server release gates | High: concurrency/migrations | Implemented; disposable SQL Server gates and local schema verification pass | Packages 01-02 |
| BUD-PR-04 operational reconciliation | High: audit/exception detection | Implemented and regression verified | Existing reservation/posting evidence |

## Application state

- API is running at `http://127.0.0.1:5012` with `SkipStartupInitialization=true` from the freshly compiled source.
- Startup initialization and seeders remain skipped.
- Next development server is running at `http://localhost:3002` with `NEXT_PUBLIC_API_URL=http://127.0.0.1:5012/api`.
- Local demo database `RHEMAERP` has the dimension-aware revision schema change and matching migration-history evidence.

## Verification ledger

| Check | Result |
| --- | --- |
| Initial repository/branch/status reconciliation | Complete |
| Live-port reconciliation | Complete; coordinator processes stopped |
| Migration application | Authorized and applied atomically to local demo database `RHEMAERP` |
| Migration preflight | Normal EF update rejected because the demo database reports a divergent history with hundreds of unrelated migrations pending |
| Scoped schema application | Passed: prerequisite tables and legacy index checked; only the new column, two indexes, FK, and one history row were changed |
| Post-migration schema verification | Passed: new column/index/FK present, legacy index absent, migration history recorded |
| Isolated API build | Passed; existing warnings only |
| Isolated test assembly build | Passed; existing warnings only |
| New focused Finance budget/AP tests | 6 passed |
| SQL Server dimension-revision migration up/down gate | Passed against prefix-safe disposable database |
| SQL Server simultaneous-spend gate | Passed: one 700 GHS reservation accepted, one rejected with `BUDGET_INSUFFICIENT`, total reserved remained 700 GHS |
| Targeted frontend ESLint | Passed |
| Repository-wide frontend type check | Blocked by pre-existing errors outside changed budgeting files; no budgeting-file error reported |
| Broader focused backend run | 63 passed, 0 failed; includes the corrected durable consumed-reservation retry path |
| API smoke test | Live health 200, Swagger 200, budget reconciliation route 401 without authentication as expected |
| Frontend smoke test | Budget reconciliation route rendered on port 3002; the browser session has no API authentication token, so the read-only data request correctly returned 401 and authenticated result rendering remains a user-login smoke step |
| Readiness health | Database and startup checks healthy; aggregate readiness is 503 only because the separately configured file-virus-scanner check is unavailable |

## Next safe action

Run an authenticated browser smoke test of dimension-aware revision creation, AP cancellation/release, and the reconciliation screen. The remaining aggregate readiness warning is outside budgeting and should be assessed separately if the demo environment requires the file-virus-scanner integration.
