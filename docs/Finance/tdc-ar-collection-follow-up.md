# TDC Accounts Receivable Collection Follow-up

## Purpose and requirement coverage

This workspace implements the Finance-owned portion of **FR-AR-009**: overdue customer balances can be turned into assigned, prioritised follow-up work with promises, reminders, outcomes, escalation evidence, and a readable activity history. It closes the operational gap between an AR ageing report and the work Finance officers perform to recover the debt.

The workspace deliberately does not create another receivables ledger. Outstanding amounts, due dates, and settlement state are derived from posted `SubledgerSettlementBalances`, so the collection queue remains consistent with the AR subledger and general-ledger posting evidence.

## User workflow

1. Open **Finance > Accounts Receivable > Collection Follow-up**.
2. Review the summary and filter the queue by ageing, status, assignee, or customer/document search.
3. Generate tasks from currently overdue posted exposures, or create a task for a selected exposure.
4. Assign an officer, set priority and next follow-up date, and record a promise to pay when applicable.
5. Record reminders or contact notes as `Prepared` or officer-confirmed `Dispatched` evidence.
6. Resolve, write off, or escalate the task with an outcome or explanatory note.
7. Review the task history and Finance audit trail when supervising collections or supporting an audit.

Generation is idempotent: the database permits one active primary task per tenant, invoice, and collection context. Re-running generation refreshes existing tasks from the live posted balance instead of creating a parallel task. A task is automatically resolved when the posted outstanding balance becomes zero. If a later receipt or credit-note reversal restores the posted debt, the same task is automatically reactivated so Finance retains the earlier history without leaving a live exposure in a terminal state.

## Important controls

- Every query is tenant-scoped.
- Assignees must be active users in the current tenant.
- Promise amounts must be positive and cannot exceed the live posted outstanding balance.
- Resolving, writing off, or escalating requires an outcome or explanatory note.
- Optimistic concurrency uses `RowVersion` so one officer cannot silently overwrite another officer's update.
- Reminder records distinguish preparation from confirmed dispatch. The application does not claim that email, SMS, or a letter was sent by an external provider.
- External communications delivery and Legal case management are explicit integration boundaries, not hidden or simulated Finance behavior.
- Operational history is readable in the workspace; Finance audit events retain the before/after control evidence.

## Permissions

| Permission | Purpose |
| --- | --- |
| `Finance.AR.Collections.View` | View summaries, queues, assignees, and history. |
| `Finance.AR.Collections.Manage` | Generate, create, assign, prioritise, update, resolve, and escalate tasks. |
| `Finance.AR.Collections.Reminders.Record` | Record reminder/contact evidence and follow-up dates. |

## API surface

The controller is rooted at `/api/finance/ar/collections`.

| Method and route | Function |
| --- | --- |
| `GET /summary` | Portfolio collection indicators as of a selected date. |
| `GET /work-queue` | Paged and filtered overdue-exposure work queue. |
| `GET /assignees` | Active tenant users eligible for assignment. |
| `POST /tasks/generate` | Idempotently generate or refresh tasks from posted AR evidence. |
| `POST /tasks` | Create a primary task for one eligible exposure. |
| `PUT /tasks/{taskId}` | Update ownership, status, promise, priority, and outcome. |
| `POST /tasks/{taskId}/reminders` | Record prepared/dispatched reminder evidence. |
| `GET /tasks/{taskId}/history` | Retrieve the readable follow-up timeline. |

## Data and deployment notes

Migration `20260808232712_AddArCollectionFollowUpWorkspace` extends the existing `CollectionActivities` aggregate with collection context, primary-task uniqueness, parent history links, reminder evidence, completion evidence, and concurrency control. It does not introduce a separate collection-task table because the existing aggregate already models customer collection activity.

The Finance seeder creates demonstration tasks only where seeded posted AR settlement evidence is overdue. It is idempotent and never invents a debt that is absent from the subledger.

## Verification and UAT

Automated service tests cover tenant isolation, posted-balance sourcing, idempotent generation, audit evidence, automatic settlement resolution, the filtered unique index, and concurrency configuration. TDC representative-data UAT should additionally verify:

- ageing and outstanding amounts against the AR ageing/subledger reports;
- role assignment for view, management, and reminder-recording duties;
- the chosen operational meanings and approval process for `WrittenOff` and `Escalated`;
- reminder wording and evidence retention expectations; and
- supervisor review of promises due, overdue follow-ups, and auto-resolved tasks.
