# TDC Finance Period-Close Workspace

**Implementation date:** 2026-08-02  
**Work package:** WP3 — Finance period close, first controlled vertical slice  
**Limitation resolved in code:** `FIN-LIM-0034`  
**Deployment status:** Initial and template migrations applied to RHEMAERP; UAT pending

## Outcome

The existing `FiscalPeriodService` is now the single owner of period evaluation, preparation,
close, and reopen. The implementation retains its existing posting-integrity checks and wraps
them in a persisted, numbered Finance close cycle. The former General Ledger compatibility
methods delegate to this service; they no longer update fiscal-period state independently.

The Fiscal Periods screen no longer displays unconditional green checklist items. Opening the
close workspace evaluates live server controls and persists an immutable snapshot of each result.

## Control lifecycle

1. An authorised closer evaluates an open, unlocked period. The service creates or reuses the
   active numbered cycle and appends evaluation snapshots.
2. Automated tasks mirror the latest snapshot result. Mandatory failures remain blocked and
   cannot be bypassed by a request flag.
3. When all mandatory checks pass, a preparer signs a declaration of at least 20 characters.
4. A different authorised user reviews the latest evidence, signs a reviewer declaration, and
   approves the close. The API re-evaluates immediately before changing period status.
5. If a later evaluation finds a blocker, the active preparation certificate is superseded and a
   fresh declaration is required.
6. Reopening requires the existing `Finance.PeriodReopen` permission and a reason. It retires the
   active certificate and marks the prior cycle `Reopened`; the next evaluation starts cycle N+1.

Evaluation, preparation, close, and reopen commands are serialized per tenant and fiscal period
with serializable isolation plus a SQL Server transaction-owned application lock.

## Persisted evidence

- `FinanceCloseCycle`: numbered attempt, template version, status, evaluation count, and
  preparation/close/reopen timestamps.
- `FinanceCloseTask`: copied version-one task catalogue, dependency, assignment, due date,
  completion state, and evidence summary.
- `FinanceCloseCheckSnapshot`: immutable per-evaluation result, severity, exception count/amount,
  exact JSON evidence, evaluator, and timestamp.
- `FinanceCloseCertification`: preparer declaration plus second-person review and approval; old
  certificates remain stored when superseded.

The migration is `20260802153959_AddFinancePeriodCloseWorkspace`.

## First-slice automated checks

- General-ledger and Finance posting integrity, including unposted/unapproved source documents,
  orphaned posting events, missing document posting events, tenant-reference mismatches, and
  unresolved failed posting events.
- Trial-balance equality and posted-journal balance.
- Cash and bank completion, including unfinalized reconciliations and posted unreconciled cash or
  bank transactions.
- Fixed-asset depreciation completeness for every due active, capitalized depreciable asset,
  failed depreciation runs, and posted schedules with missing/invalid same-tenant journal or
  posting-event evidence.
- Foreign-currency revaluation when posted foreign-currency activity exists.

## `FIN-LIM-0034` policy resolution

`FinanceSettings.RequireDepreciationBeforePeriodClose` controls whether depreciation is a
mandatory blocker. Its TDC and SQL defaults are `true`. An administrator can explicitly disable
it in Finance Settings; the existing Finance control-policy audit records the change. When it is
disabled, the snapshot records the check as not applicable under tenant policy rather than
pretending depreciation passed.

## API and UI

- `POST /api/finance/periods/{id}/close-workspace/evaluate`
- `POST /api/finance/periods/{id}/close-workspace/prepare`
- `POST /api/finance/periods/{id}/close`
- `POST /api/finance/periods/{id}/reopen`

Workspace evaluation, task/evidence maintenance, and preparation now require
`Finance.PeriodClose.Workspace.Maintain`; final close retains `Finance.PeriodClose`, and reopen
retains `Finance.PeriodReopen`.

## Deliberately remaining WP3 work

The second WP3 slice now provides configurable, versioned month/quarter/year templates,
maker-checker template activation, copied task due dates, and manual task completion with retained
evidence summaries. See `docs/Finance/tdc-finance-close-templates-and-manual-tasks.md`.

The subsequent WP3 slices now provide AP/AR control reconciliation, unapplied-balance review,
budget-adoption and recurring-journal providers, controlled binary task evidence, and a
different-user evidence-fingerprinted waiver workflow. See
`docs/Finance/tdc-finance-close-evidence-and-exception-waivers.md`. Remaining work is
printable close packs and a higher-tier reopen approval route. Aging/escalation notifications are
implemented in `docs/Finance/tdc-finance-close-aging-and-escalations.md`.

## Verification

Focused API tests cover persisted snapshots, two-person close approval, self-approval rejection,
reopen/reclose cycle history, the TDC depreciation default, invalid depreciation posting evidence,
and the explicit policy exception. Frontend type checking reaches only the two pre-existing
Syncfusion PDF viewer module-resolution errors; the close workspace introduces no TypeScript
errors.
