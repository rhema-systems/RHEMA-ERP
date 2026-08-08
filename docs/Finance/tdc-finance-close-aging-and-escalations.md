# TDC Finance Close Aging and Escalations

**Date:** 2026-08-03  
**Work package:** WP3 — Finance period close, sixth controlled vertical slice  
**Migration:** `20260803084838_AddFinanceCloseAgingEscalations`  
**Database status:** Applied to `RHEMAERP`; 207 migrations recorded

## Outcome

The existing Finance close workspace now turns persisted task deadlines and maker-checker states
into idempotent in-app notifications. Delivery uses the ERP's unified notification service and
dispatcher; it does not introduce a Finance-only inbox, email engine, or parallel workflow.

`FinanceCloseAlertDelivery` retains the reason, recipient, control deadline, attempts, delivery
status, generic notification identifier, and any last error. The close workspace exposes these
records so reviewers can verify that missed deadlines and pending decisions were followed up.

## TDC default policy

TDC uses GMT throughout the year, so the UTC deadlines used by the processor are also the local
business time shown to Finance users.

- An assigned incomplete task receives a reminder during the 24 hours before its due date.
- An assigned incomplete task receives an overdue alert when its deadline passes.
- An unassigned task due within 24 hours, or already overdue, is sent to the Finance Manager and
  Chief Accountant roles for assignment.
- A task still incomplete 24 hours after its deadline is escalated to Chief Accountant and
  Financial Controller.
- A waiver request immediately notifies users who have
  `Finance.PeriodClose.Waivers.Approve`; the requester is excluded even when they hold that
  permission.
- A waiver still awaiting review after 24 hours is escalated to Chief Accountant and Financial
  Controller, again excluding the requester.
- A prepared close cycle immediately notifies users who have `Finance.PeriodClose`; the preparer
  is excluded because final closure requires a different user.
- A prepared cycle still awaiting approval after 24 hours is escalated to Chief Accountant and
  Financial Controller, excluding the preparer.

The initial approval audiences are permission-derived, so a tenant can continue to use the
existing role-permission administration. Only the TDC higher-tier escalation route uses the
seeded Finance authority roles.

## Idempotency, retries, and tenant isolation

Each delivery has a stable key made from alert type, source control row, and recipient. A filtered
unique database index on tenant plus that key prevents two API nodes or repeated 15-minute scans
from creating duplicate alerts.

The processor commits a `Pending` claim before calling the shared notification service. Successful
delivery records the generic `NotificationId`; failure records the error and can retry after one
hour. A shutdown leaves the claim pending and therefore cannot trigger an immediate duplicate on
restart. Database errors are suppressed only when a competing claim is proven to exist.

Recipient discovery requires an active user and matching primary or active user-tenant
relationship. Tests verify that a reviewer from another tenant is not notified even when they hold
the same Finance role and permission.

## Accounting-control boundary

Notification outcomes never complete a task, approve a waiver, change a check result, prepare a
cycle, close a period, or reopen a period. The existing `FiscalPeriodService` remains the single
owner of those state transitions. This separation ensures a delivery outage cannot weaken or
bypass a mandatory close control.

The alert slice does not change the non-waivable close-provider list. In particular,
`FIXED_ASSET_DEPRECIATION` remains non-waivable, preserving the `FIN-LIM-0034` resolution.

## User experience

Notifications navigate to `/finance/fiscal-periods` and carry cycle, fiscal-period, task/waiver,
alert type, and control deadline metadata. The global notification dispatcher retains its existing
responsibility for delivery.

The close dialog includes a compact delivery audit showing the latest alert type, recipient,
status, and delivery/deadline time. It is intentionally read-only: Finance users resolve the
underlying task, waiver, or close approval rather than dismissing the accounting escalation from
inside the close workflow.

## Verification

- Focused service tests cover repeated-run deduplication, controlled retry delay, maker-checker
  exclusion, 24-hour higher-tier escalation, and cross-tenant recipient isolation.
- 109 focused close, controlled-upload, alert, and Finance-controller security tests pass.
- The API project builds with zero errors; existing unrelated warnings remain.
- EF reports no pending model changes after the migration.
- Frontend ESLint passes for the changed close files. TypeScript checking reports only the two
  pre-existing Syncfusion PDF-viewer module-resolution errors and no error in this slice.
- `RHEMAERP` contains the alert-delivery table, unique deduplication index, and migration-history
  row. The verified pre-change backup is
  `RHEMAERP_PreCloseAgingEscalations_20260803_085644.bak`.

## Remaining WP3 work

Signed/printable close packs and a higher-tier reopen approval route remain. The identified
Finance-only automated providers, templates, evidence, exception waivers, and aging/escalation
delivery are implemented.
