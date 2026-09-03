# Finance Agent Coordination Protocol

## Purpose

This protocol lets a primary Finance coordinator manage isolated implementing and reviewing tasks without requiring the user to copy messages between them. Repository evidence, not conversational memory, is authoritative.

## Roles

- **Coordinator:** owns the primary Finance branch, dependency ledger, review decisions, ordered integration, and user escalation.
- **Implementer:** works only in its assigned isolated worktree and bounded scope; commits verified units incrementally.
- **Independent reviewer:** inspects the requirement, commit range, tests, migration impact, and integration risks without editing the implementer's work unless explicitly reassigned.
- **User:** supplies product judgment and authorization only at the escalation gates below.

The coordinator may perform the reviewer role directly. It should request an independent review for high-risk accounting logic, schema changes, security/authorization changes, cross-module contracts, concurrency, reversals, or destructive operations.

## Authoritative state

For each active package, the coordinator maintains a ledger under `docs/Finance/coordination/`. It records:

- objective and approved boundaries;
- primary base and current primary HEAD;
- task/thread, worktree, branch, and commit range;
- dependencies and required integration order;
- tests and checks executed;
- migrations created and whether they remain unapplied;
- review findings and correction cycles;
- current status and next authorized action.

Before acting, every participant reconciles the relevant Git and worktree state read-only. Existing modified or untracked files are preserved.

## Lifecycle

1. The coordinator translates the approved objective into a bounded phase with acceptance criteria and explicit exclusions.
2. The implementer works in a dedicated branch/worktree based on the exact supplied commit.
3. The implementer commits completed, verified units and returns a structured handoff.
4. The coordinator reviews the requirement, code, tests, migrations, conflicts, and cross-module boundaries.
5. If changes are required, the coordinator sends concrete findings directly to the implementer and repeats the review.
6. If approved, the coordinator may cherry-pick clean commits onto the primary Finance branch in dependency order and run proportionate integration verification.
7. The coordinator updates the ledger and automatically issues the next already-approved phase.
8. The user is contacted only at an escalation gate or when the approved objective is complete.

## Handoff contract

Every implementing task reports:

```text
STATUS: REVIEW_REQUIRED | DECISION_REQUIRED | BLOCKED | COMPLETE
OBJECTIVE:
EXACT_BASE:
BRANCH:
WORKTREE:
COMMITS_IN_ORDER:
CHANGED_FILES:
BEHAVIOR_DELIVERED:
TESTS_AND_RESULTS:
MIGRATIONS_AND_DATABASE_STATE:
DEPENDENCIES_AND_CONFLICT_RISKS:
CROSS_MODULE_IMPACT:
UNRESOLVED_FINDINGS:
NEXT_RECOMMENDATION:
```

Review findings must identify severity, evidence, affected file or behavior, required correction, and the validation needed for closure.

## User escalation gates

Stop and obtain the user's direction before:

- applying a migration, resetting or mutating a database, or posting/approving accounting data;
- pushing, opening a PR, merging a PR, or otherwise changing a remote system;
- deleting or destructively cleaning data, branches, worktrees, or files;
- modifying another module's implementation rather than an explicitly authorized additive Finance contract/adapter boundary;
- resolving a conflict that could overwrite concurrent or user-owned work;
- choosing between materially different accounting, product, security, or data-retention policies;
- expanding the agreed scope.

Clean local commits may be reviewed and cherry-picked into the primary Finance branch without another confirmation when they remain in scope and do not trigger a gate.

## Connectivity recovery

Connectivity loss is an execution interruption, not permission to restart or duplicate work.

1. Before remote or task-to-task operations, persist the latest verified phase, commit, tests, and next action in the ledger or task handoff.
2. Classify failures. Retry only errors reasonably attributable to connectivity, DNS, transport timeout, service availability, or authentication-session interruption. Do not relabel code, test, merge, permission, or validation failures as connectivity failures.
3. Retry read-only and idempotent operations with bounded backoff. Use short retries while the task is active, then periodic task/heartbeat checks rather than a blocking sleep.
4. Never blindly retry a potentially completed mutation. Before retrying a commit integration, push, PR operation, migration, database action, approval, or posting, reconcile whether the first attempt succeeded and verify its idempotency key or durable evidence.
5. If connectivity remains unavailable, set status to `WAITING_FOR_CONNECTIVITY`, retain the worktree and uncommitted state untouched, and record the exact command/tool and safe resume point.
6. When connectivity returns, fetch or inspect remote/task state, confirm the base and branch have not diverged, then continue from the last verified incomplete action. Do not repeat completed implementation or tests without a reason.
7. Escalate to the user only if credentials are required, remote state has materially changed, retries repeatedly fail after connectivity returns, or safe resumption is ambiguous.

An app heartbeat may be configured separately to wake the coordinator periodically. The protocol alone cannot execute while the machine, Codex app, or model connection is fully offline.

## RHEMA ERP boundaries

- Finance owns Finance implementation and additive Finance contracts/adapters.
- Procurement, Inventory, HR/Payroll, Sales, Estate, Projects, and other module implementation remains owned by their developers.
- Workers must not edit the primary dirty UAT worktree.
- Migrations may be authored when necessary but remain unapplied until explicitly authorized.
- Generated artifacts and unrelated working-tree changes stay out of commits.
- All accepted Finance work is ultimately consolidated into the planned Finance PR.
