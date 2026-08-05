# TDC Finance Controlled Period Reopen Approval

## Purpose

This slice closes the final Finance period-close control gap: possession of
`Finance.PeriodReopen` no longer changes posting state immediately. A certified period remains
closed while a separate higher-tier user reviews a retained business case and affected-period
snapshot.

## TDC default authority

- Finance Manager receives `Finance.PeriodReopen` to submit the maker request.
- Chief Accountant receives `Finance.PeriodReopen.Approve` to record the independent decision.
- Financial Controller, Tenant Administrator, and Super Administrator retain the complete Finance
  permission catalogue, but the service still prevents any requester from reviewing their own
  request.
- Fiscal-year reopen and emergency lock/unlock operations remain separate controlled actions; a
  period request cannot be used as an implicit year or permanent-lock override.

## Workflow

1. The maker submits a reason and a separate affected-period assessment, each containing at least
   20 characters.
2. Finance validates the target period, fiscal year, latest numbered close cycle, active signed
   maker-checker certificate, and every later active period in the same fiscal year.
3. The request retains immutable validation JSON and a SHA-256 fingerprint. The period remains
   `Closed` and posting remains disabled.
4. A different user with `Finance.PeriodReopen.Approve` approves or rejects with a mandatory
   review declaration.
5. Approval recomputes the validation. A changed fingerprint, locked/closed fiscal year, locked
   target, missing certificate, or changed close-cycle identity rejects the approval as stale.
6. An approved decision atomically opens the period, marks cycle N `Reopened`, supersedes its
   certificate, and creates `InProgress` cycle N+1 from the current approved template. There is no
   intermediate "approved but not applied" state.

## Affected-period policy

TDC's safe default is reverse-chronological reopening. A later `Closed` or `Locked` period blocks
an earlier request because its downstream certificate cannot remain protected while an earlier
balance changes. Later `Open` periods are retained as affected warnings and must be reviewed before
their next close certification. This rule stays entirely inside Finance; no inter-module interface
is required.

## Persistence and audit

`FinancePeriodReopenRequest` retains the target period, superseded cycle, reason, impact assessment,
validation evidence/fingerprint, maker, reviewer, decision, and resulting cycle N+1. A filtered
unique database index permits only one non-deleted `PendingApproval` request per tenant/period.

Audit events are recorded for request, approval, rejection, and the applied reopen. Existing close
pack history remains intact because the original certificate is superseded rather than deleted.

## Notification and UI

The existing Finance close alert monitor sends idempotent initial review notifications to users
with the new approval permission, excludes the maker, and escalates requests older than 24 hours to
TDC's Chief Accountant/Financial Controller tier. The Fiscal Periods screen shows pending status,
the retained reason and impact, later affected periods, warnings, and approve/reject actions.

## Verification

- API build succeeds with no new compiler errors.
- Fifteen focused accounting-period tests pass, including maker-checker approval, certificate
  supersession, immediate cycle N+1, audit evidence, and reverse-chronological downstream blocking.
- Four focused Finance close alert tests pass, including permission-derived reopen review delivery
  and retained request linkage.
- Touched frontend files pass ESLint. The repository-wide TypeScript check remains blocked only by
  the two existing Syncfusion PDF viewer module-resolution errors.

