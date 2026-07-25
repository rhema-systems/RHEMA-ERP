# FR-GL-006 — Recurring Journals Implementation Plan

## Requirement

Provide recurring general-ledger journals with frequency, effective dates, review, and automatic generation, while reusing the current journal-entry, approval, posting, audit, fiscal-period, notification, and permission foundations.

Priority: High
Owner: Akwasi Adu-Kyeremeh
Gap: Finance — no complete recurring-journal product workflow is currently exposed.

## Executive recommendation

Implement recurring journals as a separate **schedule/template aggregate** that generates ordinary journal-entry occurrences. Do not use a `JournalEntry` row itself as both template and accounting document.

The confirmed target workflow is:

1. An authorized user creates a balanced recurring-journal template and submits it through the existing workflow engine for activation approval.
2. Once approved and Active, a tenant-aware scheduler creates one Draft occurrence for each due date.
3. The system automatically submits every generated Draft to the existing journal-entry approval workflow.
4. Every occurrence receives human approval and is then explicitly posted through the existing posting action.
5. Generation never changes account balances; only the existing posting engine does.
6. A unique occurrence key makes generation idempotent across retries and multiple API instances.

This gives clear separation between standing instructions and legal accounting records, preserves maker-checker controls, and minimizes changes to proven posting logic.

## Current implementation assessment

### Foundations to reuse

- `JournalEntryService` already creates balanced Draft journals, validates open fiscal periods and posting accounts, supports attachments, approval, posting, reversal, audit events, and tenant isolation.
- `JournalEntryController` already exposes the journal lifecycle and checks granular permissions for edit, delete, submit, approve, post, and reverse.
- `IFinancePostingEngine` and `FinancePostingEvent` already provide controlled, idempotent posting; generated journals should use this existing route after approval.
- The UI already provides journal list, create, edit, detail, approval queue, and posting actions.
- The application already runs periodic hosted services and has notification and finance-audit foundations.
- `JournalEntry` already contains `IsRecurring`, `RecurringTemplateId`, `RecurrenceFrequency`, and `NextRecurrenceDate` fields.

### Confirmed gaps

- Recurrence fields are not exposed in the journal DTOs or frontend types/forms.
- There is no recurring-template service, API, management page, scheduler, generation history, pause/resume workflow, or recurrence-specific permissions.
- There is no complete recurrence definition: start/end rules, timezone, day/month pattern, end-of-month behavior, business-day adjustment, missed-run handling, or review mode.
- `RecurringTemplateId` is not configured as an enforced relationship, and no unique schedule/occurrence constraint protects against duplicate generation.
- Existing recurrence fields mix template and occurrence concepts and are insufficient for an auditable scheduler.

## Proposed scope

### Phase 1 — Controlled recurring journals (confirmed launch scope)

- Fixed-amount templates with header and balanced debit/credit lines.
- Frequencies: weekly, semi-monthly, monthly, quarterly, annually, and custom intervals/rules.
- Start date, optional end date, tenant timezone, and next due date.
- Monthly rules: selected day or last day of month.
- Non-business-day behavior: no adjustment, previous business day, or next business day.
- Lifecycle: Draft, Pending Approval, Approved/Active, Rejected, Paused, Completed, Cancelled.
- Automatic generation of Draft occurrences followed immediately by submission to the existing `JournalEntry` approval workflow.
- Mandatory human approval and explicit posting using the existing workflow.
- Optional automatic reversal configured per template.
- Preview of the next occurrence and generation history.
- Pause, resume, terminate, and “generate now” controls with audit events.
- Notifications for generated, failed, awaiting review, approaching end date, and suspended schedules.

### Phase 2 — Advanced ERP capabilities

- Variable amounts, percentage allocations, statistical drivers, and indexed amounts.
- Template versioning with effective-dated line changes.
- Bulk activation, mass review, and operational dashboards.
- Optional auto-post for narrowly approved low-risk templates, controlled by policy, separate permission, approval ceiling, and exception queue.

## Domain and data design

Add `RecurringJournalTemplate` as the schedule header:

- Identity and tenant: `Id`, `TenantId`, `TemplateNumber`, `Name`, `Description`.
- Journal defaults: `JournalType`, `ReferencePattern`, `BookClassification`, `Currency`, `Notes`, `EntryTag`.
- Schedule: `Frequency`, `Interval`, `StartDate`, `EndDate`, `TimezoneId`, `DayOfWeek`, `DayOfMonth`, `UseMonthEnd`, `BusinessDayConvention`, `NextDueDate`, `LastGeneratedDueDate`.
- Controls: `Status`, `GenerationMode` (fixed as `SubmitForApproval` at launch), `RequiresReview`, `FailureCount`, `LastFailure`, `SuspendedAt`, `SuspendedReason`.
- Reversal controls: `AutoReverse`, `ReversalRule`, optional offset/value, and reversal description/reference patterns.
- Ownership and review: `OwnerUserId`, optional reviewer/workflow reference, `ActivatedBy`, `ActivatedAt`.
- Standard audit/concurrency fields, including a row-version/concurrency token.

Add `RecurringJournalTemplateLine`:

- `TemplateId`, `LineNumber`, `AccountId`, debit/credit direction, fixed amount, description/reference patterns, currency and exchange-rate policy, and dimensions currently supported by journal transaction lines.

Add `RecurringJournalOccurrence` as the generation ledger:

- `TemplateId`, `ScheduledDate`, `EffectivePostingDate`, `JournalEntryId`, `Status`, `AttemptCount`, `GeneratedAt`, `GeneratedBy`, error details, and template version/snapshot reference.
- Enforce a filtered unique index on `(TenantId, TemplateId, ScheduledDate)` so a due occurrence can be generated only once.

Existing journal linkage:

- Keep `JournalEntry.RecurringTemplateId` as the link from generated journal to template and configure its foreign key with restricted delete behavior.
- Add `RecurringOccurrenceId` or derive through the occurrence table; a direct FK is preferable for traceability and uniqueness.
- Treat `JournalEntry.IsRecurring` as legacy/compatibility metadata. New code should identify a generated occurrence through the FK, not the boolean alone.
- Move schedule ownership (`RecurrenceFrequency`, `NextRecurrenceDate`) to the template. Deprecate these journal-level fields after migration rather than immediately dropping them.

## Schedule and generation rules

- Store business dates separately from UTC execution timestamps. Evaluate schedules in the tenant/template IANA timezone and persist UTC audit timestamps.
- Calculate the next due date from the last successfully claimed scheduled date, not from the worker execution time, to prevent schedule drift.
- Claim due occurrences transactionally, relying on the unique occurrence index for cross-instance safety.
- Generate through a dedicated recurring-journal service that calls shared journal validation/creation logic; do not duplicate balancing, account, currency, book, or fiscal-period validation.
- Resolve the fiscal period at occurrence generation. If the effective date is closed, do not silently move the accounting date. Put the occurrence in Exception status and notify the owner/reviewer.
- On downtime recovery, generate every missed occurrence in chronological order, capped per worker batch. Continue in later batches and expose backlog age/count. Never collapse missed periods into one latest occurrence.
- If a template line becomes invalid (inactive/non-posting/cross-tenant account, missing exchange rate, invalid dimension), fail that occurrence only, preserve the due date, notify, and suspend after a configurable consecutive-failure threshold.
- Generated Draft journals are snapshots. Later template edits affect future occurrences only.

## Review, approval, and accounting controls

- Template activation is a controlled action distinct from approving generated journals.
- Recommended segregation of duties: creator cannot activate their own template where maker-checker is enabled; journal approver cannot approve their own generated occurrence under the existing self-approval rules.
- Editing an Active template creates a new effective version or returns it to Draft for reactivation. Never rewrite already generated journals.
- Generated journals remain editable only under the existing Draft rules. If users change material values, record the variance from the template snapshot in the audit trail.
- Auto-submit is mandatory. If Draft creation succeeds but workflow submission fails, retain it as a submission exception and retry idempotently without creating another journal.
- Auto-post should not be included in MVP unless the client explicitly accepts the governance model. If later enabled, require a separate permission, approved template version, monetary ceiling, open period, valid accounts/rates, and a complete posting audit event.
- Posted occurrences are corrected only through the existing reversal process.
- Cancellation stops future generation and retains all history; templates and occurrences should not be hard-deleted.

## API design

Add `/api/finance/recurring-journals` endpoints:

- `GET /` with status, owner, frequency, and next-due filters.
- `GET /{id}` including lines, schedule, next preview, and recent history.
- `POST /` create Draft template.
- `PUT /{id}` update Draft/Paused template with optimistic concurrency.
- `POST /{id}/activate`, `/pause`, `/resume`, `/cancel`.
- `POST /{id}/preview-next` without persistence.
- `POST /{id}/generate-now` with explicit scheduled/effective date and idempotency key.
- `GET /{id}/occurrences` and `GET /occurrences/exceptions`.
- `POST /occurrences/{id}/retry` after the underlying issue is corrected.

Extend journal DTOs with read-only recurrence provenance (`RecurringTemplateId`, template number/name, occurrence ID, scheduled date). Do not let the normal journal update endpoint alter provenance.

## Permissions

Add and seed:

- `Finance.RecurringJournals.View`
- `Finance.RecurringJournals.Create`
- `Finance.RecurringJournals.Edit`
- `Finance.RecurringJournals.Activate`
- `Finance.RecurringJournals.Pause`
- `Finance.RecurringJournals.Cancel`
- `Finance.RecurringJournals.Generate`
- `Finance.RecurringJournals.Retry`

Continue using existing journal submit, approve, post, and reverse permissions for generated occurrences. If auto-post is introduced, add `Finance.RecurringJournals.ConfigureAutoPost`; do not infer it from ordinary posting permission.

## User experience

- Add **Recurring Journals** under General Ledger, separate from **Journal Entries**.
- List page: template number/name, frequency, status, owner, last generated, next due, mode, amount, and exception indicator.
- Create/edit wizard: Basics → Journal lines → Schedule → Review & activation.
- Show a human-readable schedule summary and the next 3–5 occurrence dates before activation.
- Detail page: schedule controls, current template version, balanced lines, next preview, occurrence history, linked journals, errors, and audit trail.
- Add “Generated from recurring journal …” provenance and a link on journal detail/list views.
- Add an exception queue with corrective guidance rather than relying only on logs.
- Reuse the existing Finance approval workbench and add focused queue views for `RecurringJournalTemplate` activation and generated `JournalEntry` occurrences. Also retain both in the unified Finance Workflow Approvals queue.

## Workflow integration and seeding

The platform already seeds a three-stage `JournalEntry` workflow and routes journal approvals through the Finance approval workbench. Reuse that workflow for every generated occurrence.

Add a workflow entity type and published seed definition for `RecurringJournalTemplate`, named **Recurring Journal Template Activation**, initially using the existing finance stages:

1. Accounts Officer review.
2. Finance Manager approval.
3. Financial Controller final approval.

Final approval activates the template and calculates `NextDueDate`. Rejection returns it to a correctable state. Cancellation or retirement stops future generation without removing history.

After generating and validating a Draft occurrence, start `StartApprovalWorkflowAsync("JournalEntry", journalEntryId)`. Existing approver assignment, escalation, notification, audit, and posting controls remain authoritative. The focused approval queue is only a filtered view over the same workflow data, not a second approval system.

## Recommended accounting policies

### Weekends and holidays

Confirmed: use a configurable convention per template, defaulting to **Next Business Day**. Preserve both the original `ScheduledDate` and adjusted effective date. For month-end templates, default to **Previous Business Day** when moving forward crosses into the next fiscal period.

The current codebase already has overlapping tenant holiday models:

- Payroll exposes maintained `PayrollHoliday` and `PayrollNonWorkingDay` records through `PayrollService`, `PayrollController`, and the Payroll administration UI.
- HR Leave has a separate `PublicHoliday` entity used by leave-day calculations.
- Workflow governance stores another working-day/holiday calendar for SLA calculations.

Do not add a Finance-only holiday table. Introduce a module-neutral `IBusinessCalendarProvider` contract and initially adapt it to the maintained Payroll holiday setup, with weekends as the standard fallback. Keep Finance recurrence code dependent only on that contract. Add an architecture note and migration path to consolidate HR/Payroll/Workflow calendars later, because the eventual HR/Payroll PR may change the authoritative source. Template preview and generation must use the same calendar-provider version and record the adjustment explanation.

### Missed occurrences after downtime

Generate **every missed occurrence**, oldest first, with a configurable batch cap. This preserves period-specific accrual, approval, and reversal evidence. Closed-period occurrences go to an exception queue and are never silently moved. Finance may explicitly select a permitted current-period date with a reason and approval trail, or mark an occurrence intentionally skipped with approval.

### Editing generated Drafts

Allow limited editing before approval because exchange rates, descriptions, dimensions, and final estimates may need refinement. Require a reason and record template-versus-final variance. Material changes—account, debit/credit direction, amount beyond tolerance, currency, book, or effective date—must restart the approval workflow.

Template defects should be corrected on the template for future occurrences. Provide **Correct template and regenerate** for an unsubmitted or rejected occurrence; retain the superseded occurrence record. Once Pending Approval, edits remain blocked until rejection or withdrawal under the existing lifecycle.

## Formula-based accruals and allocations

Fixed amounts suit rent, subscriptions, insurance, and standard charges. Formula-based entries calculate an occurrence from current business data instead of copying a constant amount. Common ERP patterns are:

- Accrual from source balances, such as received-but-not-invoiced purchases or unbilled revenue.
- Percentage accrual based on payroll, revenue, contract value, or another account/activity balance.
- Straight-line spreading of prepaid cost or deferred income, with final-period rounding.
- Allocation of a source pool across cost centres, projects, or departments using percentages, headcount, floor area, machine hours, or revenue.
- True-up entries that post only the difference between the required accrual and existing accrued balance.
- Indexed amounts adjusted using exchange rates, CPI, or contract escalation factors.

These require source definitions, cut-off rules, missing-data behavior, residual/rounding rules, calculation snapshots, and explainable previews. Recommendation: launch with fixed amounts plus percentage allocation across lines if departmental splits are needed. Keep live-data formulas, statistical drivers, and true-ups in Phase 2 unless the client supplies concrete launch scenarios.

Confirmed launch allocation scope: a fixed template total may be split across destination lines by configured percentages. Percentages must total 100%, use deterministic currency rounding, and assign any rounding residual to a designated residual line (defaulting to the largest allocation). Store both the percentage rule and calculated amount snapshot on each occurrence.

Allocation dimensions are metadata-driven, not hard-coded. The line editor must discover the tenant's active accounting dimensions and allow destinations such as cost centre, department, project, branch, business unit, or future configured dimensions. Persist dimension key/value assignments generically and validate them using the same dimension rules as ordinary journal lines.

## Custom recurrence rule builder

The launch rule builder supports, without exposing raw cron syntax:

- Every N days, weeks, months, quarters, or years.
- One or more selected weekdays.
- The nth weekday of a month, including first through fifth and last.
- Last calendar day or last business day of a month.
- Selected months of the year.
- Semi-monthly any-two-day configuration, with 1st/15th and 15th/month-end presets.

Rules combine only through explicit, human-readable controls and must show the next 5 calculated dates before submission. Persist a structured/versioned recurrence specification rather than cron text, so accounting-calendar adjustment remains deterministic and explainable.

## Automatic reversal workflow

An optional reversal instruction may use next calendar day, first day of the next fiscal period, or a configurable day offset. On the reversal due date, create a separate reversal Draft linked to the original occurrence and journal, then automatically submit it to the existing `JournalEntry` approval workflow. Human approval and explicit posting are mandatory at launch. Failure or delay must appear in the exception/work queue and never silently mark the original as reversed.

## Bulk posting controls

The **Approved — Ready to Post** worklist supports selection and bulk posting. Each selected journal is validated and posted independently through the existing posting engine, retaining its own journal number, posting event, idempotency key, accounting date, success/failure result, and audit trail. The batch operation returns a per-journal result and does not roll back successful independent postings merely because another selected journal fails. Only users with the existing journal-posting permission may execute it.

## Reminder and notification integration

Reuse the existing notification foundation. Journal lifecycle actions already send unified notifications, the workflow engine publishes assignment/escalation activity, and admin-configurable notification topics support recipient rules and delivery channels.

Seed recurring-journal notification topics for template submitted/approved/rejected, occurrence generated/submission failed, occurrence awaiting approval, approved awaiting posting, reversal due/overdue, generation failure/suspension, waived occurrence, and schedule approaching completion. Publish through `INotificationTopicPublisher` where configurable audiences/channels are needed and retain direct owner notification through the existing unified service where appropriate.

The reminder worker must be idempotent by `(TenantId, EntityId, ReminderType, ThresholdDate)` and record the last-sent threshold to prevent repeated alerts on every polling cycle. Recipients include the template owner, current workflow approvers, Finance posting-role recipients for approved items, and configured notification-topic recipients. Near-period-close reminders use fiscal-period dates rather than month-end assumptions and link directly to the relevant approval, posting, or exception view.

## Template versioning, end conditions, and waiver

Support no end date, a specific end date, and maximum occurrence count. Material amendment creates a new Draft version and submits it for activation approval while the currently approved version remains effective. On approval, the new version becomes effective from its configured effective date; prior generated occurrences retain their original version snapshot.

A due occurrence may be skipped only through a workflow-approved **Waive occurrence** action with mandatory reason, requester, approver, timestamps, scheduled date, and audit event. Waiver never deletes an occurrence and is prohibited after its journal has posted.

Generated reference patterns support validated tokens including `{TemplateNumber}`, `{ScheduledDate}`, `{EffectiveDate}`, `{Period}`, `{Sequence}`, and `{Version}`. Resolve and snapshot the final reference at generation; later template or calendar changes do not rewrite it.

## Service implementation outline

1. Extract/reuse journal draft validation and creation behind an internal command that accepts actor context (`User` or `System`) and recurrence provenance.
2. Implement `IRecurringJournalService` for template lifecycle, preview, due-date calculation, generation, retry, and history.
3. Implement a lightweight `RecurringJournalGenerationBackgroundService` using the current hosted-service pattern. Run frequently, process bounded batches, and make correctness independent of any single worker run.
4. Use an explicit system actor for generated records and finance audit events while retaining template owner/creator provenance.
5. Register services and configuration (poll interval, batch size, retry/suspension thresholds).
6. Add structured metrics/logging: due, claimed, generated, failed, duplicate prevented, processing latency, and backlog age.

## Migration and compatibility

- Create the three recurring-journal tables, indexes, relationships, and permissions in an EF Core migration.
- Do not automatically convert historical `JournalEntry.IsRecurring = true` rows without verified business meaning.
- Produce a one-time diagnostic report of rows with recurrence fields populated. Migrate only confirmed templates; otherwise retain them as legacy journals.
- Keep existing journal API behavior unchanged for non-recurring journals.
- Feature-flag the scheduler and UI per environment/tenant for controlled rollout.

## Testing strategy

### Unit tests

- Due-date calculation for every frequency, leap years, month-end, short months, business-day conventions, start/end boundaries, and timezone/DST behavior.
- State transitions and invalid transitions.
- Template balance, account, currency, dimension, and period validation.
- Next-date calculation without drift.

### Integration tests

- One due date creates exactly one Draft journal with correct lines and provenance.
- Concurrent workers/retries cannot create duplicates.
- Missed-run recovery, capped catch-up, failure retry, and suspension threshold.
- Closed-period and invalid-account exceptions do not post or advance incorrectly.
- Auto-submit enters the existing approval workflow; approval and posting retain segregation of duties.
- Template edits do not alter past occurrences.
- Tenant isolation on templates, lines, occurrences, journals, and scheduler queries.
- Audit events and notifications on every lifecycle action and failure.

### End-to-end tests

- Create → preview → activate → generate → review → approve → post → trace back to template.
- Pause/resume/cancel and exception-retry flows.
- Permission-based visibility and disabled actions.

## Delivery slices and acceptance gates

1. **Domain foundation:** schema, recurrence calculator, permissions, audit event definitions. Gate: migration and unit tests pass.
2. **Template lifecycle:** service/API for CRUD, preview, activate/pause/resume/cancel. Gate: lifecycle and authorization integration tests pass.
3. **Generation engine:** occurrence claiming, Draft creation, retry/exception handling, notifications, hosted service. Gate: concurrency/idempotency and tenant tests pass.
4. **Review integration:** provenance DTOs/UI, optional auto-submit, approval queue linkage. Gate: full journal lifecycle test passes.
5. **Product UI:** list, wizard, detail/history, exceptions. Gate: role-based E2E flows pass.
6. **Operational rollout:** feature flag, metrics, runbook, pilot tenant, data diagnostic. Gate: pilot produces no duplicate or untraceable occurrence and finance signs off.

## Definition of done

- Authorized users can configure, preview, activate, pause, resume, and cancel recurring templates.
- Every due schedule generates exactly one traceable ordinary journal occurrence.
- Generated journals follow the configured review mode and existing approval/posting controls.
- No generated Draft affects the GL until posted through the existing posting engine.
- Closed periods and invalid master data produce visible, actionable exceptions without silent date changes.
- All actions and automated attempts are tenant-safe, idempotent, auditable, observable, and covered by automated tests.

## Confirmed product decisions

1. Generation creates a Draft and automatically submits it for approval; it does not automatically post.
2. Every occurrence requires human approval through the existing workflow.
3. Launch includes weekly, semi-monthly, monthly, quarterly, annual, and custom recurrence.
4. Automatic reversal is optional and configured per template.
5. Template activation goes through the existing workflow engine.
6. Business-day defaults are Next Business Day, with Previous Business Day at month-end when moving forward crosses a fiscal period.
7. Custom schedules use a user-friendly rule builder; raw cron input is excluded.
8. Semi-monthly schedules allow any two configured days and provide 1st/15th and 15th/month-end presets.
9. Launch supports fixed amounts plus percentage allocations.
10. Automatic reversal supports next calendar day, first day of next fiscal period, and configurable day offset.
11. Generated Drafts allow limited audited editing; material changes restart approval.
12. Template activation uses the seeded Accounts Officer → Finance Manager → Financial Controller chain.
13. Automatically generated reversals require human approval and explicit posting.
14. Percentage allocations use a fixed template total at launch; live module/account-derived totals are deferred.
15. Allocation destinations use tenant-configurable accounting dimensions rather than a fixed dimension list.
16. Custom recurrence includes selected weekdays, nth weekday, last business day, and selected-month patterns.
17. Holiday changes do not alter already generated occurrences; only future occurrences are recalculated.
18. Bulk posting of approved occurrences is supported, while every journal retains an independent posting event and audit result.
19. End conditions support no end date, specific end date, and maximum occurrence count.
20. Material amendments use activation-approved template versions while the current approved version remains effective.
21. Skipping requires a workflow-approved Waive occurrence action with a mandatory reason.
22. Existing notification services, notification topics, and workflow escalation are reused for approval/posting reminders.
23. Generated references support template, schedule/effective date, fiscal period, sequence, and version tokens.
24. Allocation rounding uses a user-designated residual line, defaulting to the largest allocation.
25. Ordinary generated occurrences remain manually posted after final approval.
26. Reminder defaults are approval at 3 and 1 business days before fiscal-period close, posting at 2 and 1 business days before close, and daily overdue escalation after close until resolved.
27. Waive occurrence uses a Finance Manager → Financial Controller approval chain.
28. Material template amendments cannot take retrospective effect; prior periods use controlled catch-up or adjustment journals.
29. When both end date and maximum occurrence count are configured, the first condition reached ends the schedule.
30. Reference patterns combine validated free text and tokens with preview and uniqueness validation.

## Product clarification status

All material launch-scope and accounting-policy questions raised during planning are resolved. Any further implementation questions should be treated as technical design decisions unless they change the confirmed controls above.
