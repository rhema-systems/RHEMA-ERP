# TDC Finance Report Automation (FR-RP-010)

## Outcome

The Finance module can schedule an existing **Published Finance report template**, generate its PDF or XLSX output without an open browser, retain the artifact privately, notify selected in-app recipients, and expose the complete attempt/download history under **Reports > Report Automation**.

This implementation extends the shared `Report`, `ReportTemplate`, `ReportSchedule`, `ReportExecution`, and `ReportExport` model. It does not create a competing Finance-only report engine.

## Why this design fits the requirement

FR-RP-010 calls for scheduled report generation and distribution. The implementation provides the Finance-owned portion of both capabilities:

- generation is driven by a five-minute hosted due-schedule scan;
- the exact published template version is pinned on the schedule and execution;
- PDF/XLSX bytes are generated through the existing report-template lifecycle service;
- artifacts are stored in the private `finance-report-artifacts` category;
- selected active tenant users receive an in-app notification linking to the workspace;
- an authorised user downloads through a tenant- and permission-checked endpoint.

External SMTP/Microsoft 365 distribution remains a deliberate integration boundary. The UI and handbook state this plainly rather than describing a queued email as delivered.

## Operational lifecycle

1. An authorised Finance Manager/Chief Accountant selects a Published Finance template.
2. The user chooses frequency, Ghana run time, PDF/XLSX output and in-app recipients.
3. The schedule pins its template ID/version and the creating user as the controlled run-as identity.
4. The hosted processor claims each due slot through one durable `ReportExecution` occurrence.
5. The existing template/report engine generates the result.
6. Storage persists the bytes privately; the export record receives content type, size, SHA-256 checksum and seven-year retention date.
7. The execution succeeds and the next recurrence is calculated, or the same occurrence is retried up to the configured maximum.
8. Recipients receive an in-app notification; a notification transport failure does not invalidate an already retained report.

## Recurrence rules

- Supported frequencies: Daily, Weekly, Monthly, Quarterly, Yearly.
- Persisted wall-clock times use UTC because Ghana does not apply daylight-saving changes.
- Weekly schedules require Sunday (0) through Saturday (6).
- Monthly/quarterly/yearly schedules require day 1 through 31.
- A requested day beyond the length of a month clamps to that month's final day. A month-end pack is therefore not skipped in February.
- Resume recalculates from the current time so an intentional pause does not create a burst of stale outputs.

## Concurrency and retry controls

The filtered unique index on `(TenantId, ReportScheduleId, ScheduledFor)` permits one execution record for one scheduled slot. A worker that loses the database claim explicitly detaches its attempted row and exits without generating or notifying; `ReportSchedule` and `ReportExecution` also use SQL row versions. These controls allow multiple API nodes to poll safely.

A scheduled failure leaves the due slot in place and reuses its execution row. After `MaximumRetryAttempts`, the schedule moves to `Error` and requires human review. Manual **Run now** attempts are separately identified by the `Manual` trigger and never advance the production recurrence.

## Security and permissions

| Permission | Purpose |
|---|---|
| `Finance.Reports.Schedules.View` | View schedules/history and download retained artifacts |
| `Finance.Reports.Schedules.Manage` | Create, update, pause and resume schedules |
| `Finance.Reports.Schedules.Run` | Run a schedule immediately or process the due queue |

All operations fail closed on missing tenant/user context. Templates, recipients, schedules, executions and exports are checked against the current tenant. Recipient checks recognise both a user's primary tenant and active, unexpired `UserTenant` memberships, matching the ERP authorization model. The API streams artifacts without exposing their storage path.

Default TDC role grants are intentionally graduated: Senior Accountant/Managing Director can view, Finance Manager/Chief Accountant can manage and run, and Financial Controller retains the complete Finance permission set.

## API surface

- `GET /api/finance/report-automation`
- `POST /api/finance/report-automation/schedules`
- `PUT /api/finance/report-automation/schedules/{id}`
- `POST /api/finance/report-automation/schedules/{id}/pause`
- `POST /api/finance/report-automation/schedules/{id}/resume`
- `POST /api/finance/report-automation/schedules/{id}/run-now`
- `POST /api/finance/report-automation/process-due`
- `GET /api/finance/report-automation/artifacts/{exportId}`

## Database change

Migration `20260809003309_AddFinanceReportAutomation` adds pinned-template, recipient, retry, pause and concurrency evidence to `ReportSchedules`; scheduled occurrence/version/trigger/export evidence to `ReportExecutions`; and secure artifact metadata to `ReportExports`.

Apply the migration before opening the workspace or starting the hosted processor against a database running this code.

## Verification

`FinanceReportAutomationTests` is the focused FR-RP-010 release gate. It covers recurrence edge cases, permission mapping, permission discovery, private artifact metadata, SHA-256 evidence, notification delivery and duplicate-slot prevention. Targeted frontend ESLint and the backend build are also required before release.

Representative-data UAT should create a published template, run one schedule, retry a deliberately failed schedule, pause/resume it, download the artifact as an authorised user, and confirm that another tenant/user cannot access it.
