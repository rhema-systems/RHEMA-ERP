# Tenant-Level Dry-Run Migration Sign-Off Preflight

## Context

- Tenant: Default Tenant
- Tenant ID: 00000000-0000-0000-0000-000000000001
- Database: RhemaERP
- Server: RHEMA-AKWASI\EXPRESS22
- Checked at: 2026-07-09 16:12:48
- Run type: Dry-run preflight
- Production migration executed: No
- Production go-live approved: No
- Posted GL mutated: No
- Read-side repairs/rebuilds performed: No

## Result

Final dry-run recommendation: No-Go.

Reason: the configured database schema is behind the current code and cannot run the final migration sign-off pack end to end.

## Commands Run

- Read-only tenant inventory query against configured API database.
- Read-only fiscal-period inventory query against configured API database.
- Read-only EF migration history query.
- Read-only table/column presence checks for finance sign-off dependencies.

No API mutation endpoint, repair command, rebuild command, database migration, or posting command was run.

## Tenant/Cutover Evidence

- Tenant exists: Yes.
- Candidate tenant: Default Tenant.
- Candidate fiscal periods exist: Yes.
- Latest visible open period: June 2026.
- Cutover date supplied by business/accounting: No.
- Fiscal period explicitly supplied for dry-run: No.
- AP/AR/fixed-asset cutover data-shape declaration supplied: No.
- Limitation acceptance decisions supplied by leadership/accounting: No.

## Schema Preflight

Latest applied EF migration in the configured database:

- 20260628223428_AddOpeningBalanceInvoiceFlags

Required current finance sign-off tables were not present:

- FinancePostingEvents
- OpeningBalanceBatches
- OpeningBalanceLines
- SubledgerSettlementBalances
- SubledgerSettlementApplications
- FixedAssetBookValues

The configured database also still exposes older currency/settings shape, for example:

- FinanceSettings.BaseCurrency
- AccountTransactions.TransactionCurrency
- AccountTransactions.ForeignCurrencyAmount
- JournalEntries.PrimaryCurrency
- JournalEntries.IsMultiCurrency

The current final sign-off service expects the newer posting-event, opening-balance, settlement read-model, and fixed-asset reporting structures. Because those tables are missing, running the sign-off API/service against this database would fail before producing accountant-reviewable evidence.

## Pass/Fail By Area

| Area | Status | Notes |
| --- | --- | --- |
| Tenant context | Pass | Default Tenant exists. |
| Database connectivity | Pass | Configured SQL Server database is reachable. |
| Code-to-database schema alignment | Fail | Current finance migration tables are missing. |
| Cutover date/fiscal period input | Fail | Not supplied for this dry-run. |
| Opening-balance evidence | Not run | Blocked by missing `OpeningBalanceBatches` and `FinancePostingEvents`. |
| Posting back-reference diagnostics | Not run | Blocked by missing current posting-event schema. |
| Bank snapshot diagnostics | Not run | Blocked by schema mismatch and missing approved dry-run inputs. |
| AP/AR settlement read model | Not run | Blocked by missing settlement read-model tables. |
| AP/AR aging/control reconciliation | Not run | Blocked by missing settlement read-model tables. |
| Fixed asset reporting/reconciliation | Not run | Blocked by missing fixed-asset book-value/reporting tables. |
| Tax evidence | Not run | Blocked by overall sign-off schema mismatch. |
| Workflow/posting-boundary evidence | Not run | Blocked before final sign-off pack execution. |
| Evidence export manifest | Not run | Final sign-off pack could not execute. |
| FIN-LIM-0048 decision | Not evaluated | Cutover data shape was not supplied. |

## Variances Found

1. The UAT/local database is not migrated to the current Finance sign-off schema.
2. Final cutover inputs were not supplied.
3. `FIN-LIM-0048` cannot be accepted or marked not applicable without AP/AR/fixed-asset cutover data-shape decisions.

## Repairs/Rebuilds Performed

None.

No bank snapshot rebuild was run.
No posting back-reference repair was run.
No AP/AR settlement read-model rebuild was run.
No database migration was applied.
No posted GL record was mutated.

## Limitation Treatment

| Limitation | Dry-run classification | Notes |
| --- | --- | --- |
| FIN-LIM-0017 | Go-live blocking | Final migration/sign-off cannot proceed because the configured database cannot execute the sign-off pack and no accountant-reviewed evidence exists. |
| FIN-LIM-0048 | Undetermined / potentially blocking | Requires explicit cutover data-shape declaration. If source-level AP/AR/fixed-asset openings are required and unsupported, sign-off must fail. |

## Accountant Review Items

- Confirm whether this configured database is the intended UAT dry-run target.
- Confirm cutover date and fiscal period.
- Confirm whether the tenant cutover includes open AP invoices, open AR invoices, unapplied payments/receipts or advances, WHT/VAT certificate balances, foreign-currency open AP/AR balances, fixed asset opening registers, accumulated depreciation/impairment openings, and detailed cashbook history.
- Confirm which open limitations are accepted non-blocking or not applicable for this tenant.
- Approve applying current EF migrations to a UAT/test copy before rerunning the sign-off pack.

## Next Required Action

Prepare a migrated UAT database or approve migrating the configured local test database, then rerun the final sign-off endpoint with explicit tenant, cutover date, fiscal period, cutover data shape, and limitation acceptance inputs.
