# FR-GL-007 Journal Batch — Subsequent Version Roadmap

## Scope decision

Recurring batch templates, scheduled occurrence generation, and unattended auto-post are explicitly outside the initial Journal Batch release. They require durable background execution, occurrence claiming, retry and recovery operations, and a separately governed system-posting identity. The initial release remains complete without these capabilities only if the client accepts this version schedule.

The dates below assume the initial production release stabilizes during August 2026 and the team works in two-week sprints. Reconfirm dates after production acceptance and capacity planning.

## Proposed schedule

| Version | Target | Included capability | Estimated engineering effort |
|---|---|---|---|
| 1.1 | September 2026 | Recurring batch templates, recurrence rules, next-occurrence preview, activate/pause, manual “generate now,” copy from an existing batch, and occurrence history | 8–12 engineering days |
| 1.2 | October 2026 | Durable scheduled generation worker, database-backed occurrence claiming, duplicate prevention, retry/backoff, dead-letter state, monitoring, and operator replay/cancel controls | 10–15 engineering days |
| 1.3 | November–December 2026 | Policy-controlled auto-submit and auto-post, dedicated system identity, permission and maker-checker rules, posting-window controls, notification/outbox delivery, exception queue, and audit reporting | 12–20 engineering days |

Estimates exclude client UAT, environment deployment lead time, and any new workflow-policy decisions.

## Version 1.1 acceptance gate — recurring templates

- A template stores batch controls and independently balanced journal entries without carrying approvals, posting links, or prior occurrence identifiers.
- Supported rules include weekly, monthly, period-end, start/end date, maximum occurrences, and documented business-day adjustment.
- Users can preview future dates before activation.
- “Generate now” is idempotent and creates a Draft batch linked to its template and occurrence.
- Copying a normal or rejected batch remains a separate manual convenience and does not create a recurring schedule.
- Template changes are versioned; historical occurrences retain the exact source version.

## Version 1.2 acceptance gate — scheduled generation

- One occurrence can be claimed by only one worker, including during failover or concurrent worker execution.
- A unique tenant/template/occurrence key prevents duplicate batches.
- Failed generation is retryable without partially created batches.
- Operators can inspect, retry, cancel, or regenerate failed occurrences with a mandatory reason.
- Metrics and alerts cover overdue occurrences, repeated failures, queue depth, and worker health.
- Time-zone and daylight-saving behavior is explicit per tenant.

## Version 1.3 acceptance gate — controlled auto-post

- Auto-post is opt-in per template and disabled by default.
- The posting identity is a non-interactive, tenant-scoped system principal with only the required journal-batch permissions.
- Auto-post never bypasses an approval requirement unless an approved policy explicitly permits straight-through processing for that template.
- Closed periods, invalid accounts, control-total variances, currency failures, and posting-engine failures route to an exception queue; they do not retry indefinitely.
- Batch posting and its outbox notification records commit atomically.
- Reprocessing uses stable idempotency keys and cannot duplicate ledger postings.
- Every automated decision records template version, occurrence, system identity, policy, timestamps, and exception/retry history.

## Release governance

Before starting version 1.3, the client must approve:

1. which templates may bypass human approval, if any;
2. who can enable or change auto-post;
3. the posting time zone and permitted posting windows;
4. period-close behavior;
5. retry and escalation thresholds; and
6. the operational owner for the exception queue.
