# Finance Exchange-Rate Demo Blockers - 2026-10-05

## Objective

Restore the exchange-rate maker/checker demo path: make pending submissions visible to the Accounts Officer reviewer, make edit Cancel close the dialog, and give create/update operations an unambiguous single-submit progress state.

## Scope and source evidence

- Branch: codex/finance-uat-remediation-20261004
- Worktree: .w/fin-uat-remediation-20261004
- Exact base for this corrective set: 7acca585c8be5f8bef241232da705cc73aedb004
- Existing PR: #352
- User evidence: multiple USD/GHS Daily/Mid rates display Pending; a new request is not visible when logged in as Accounts Officer; edit Cancel remains open; the first Create Rate click persists a request without progress feedback and a repeat click receives the duplicate-pending HTTP 400.

## Diagnosis and mismatch classification

| ID | Evidence | Classification | Resolution |
| --- | --- | --- | --- |
| FX-DEMO-01 | Accounts Officer cannot see pending ExchangeRate requests in the currently running local API | Stale local runtime, not a new source authority mismatch | The branch already contains ExchangeRate workbench allowlisting, Accounts Officer workflow permissions/stage role, atomic submission, and orphan reconciliation. The local API process started at 07:22, before its remediation DLL was rebuilt at 07:34, so the running process retained the old assembly. A local restart from this worktree is required; no merge to master is needed locally. |
| FX-DEMO-02 | Edit Cancel only clears editingRate while the dialog owns its own open state | Confirmed UI state-control defect | Make each edit dialog controlled by the matching editingRate.id; closing or Cancel clears edit state and resets the form. |
| FX-DEMO-03 | Create/update actions have no in-flight state and allow repeat submission | Confirmed UI idempotency/feedback defect | Add guarded create/update state, disable action/cancel controls while saving, and render progress spinners/text. |
| FX-DEMO-04 | Proposed direct approval-status column update | Unsafe data workaround | Rejected as a troubleshooting path: it would bypass workflow history, approver identity/timestamps, and schedule lifecycle, and could conceal orphan evidence. No UAT data was changed. |

## Existing approval remediation retained

- ExchangeRate is included in the Finance approval workbench entity allowlist.
- The Accounts Officer role has Finance.Workflow.Approve and Finance.Workflow.Reject.
- The first exchange-rate approval stage accepts Accounts Officer and Senior Accountant.
- Submission creates the pending rate and workflow instance atomically.
- Startup reconciliation repairs Pending exchange rates that have no workflow history and retain a durable initiator.

## Changed files

- frontend/src/app/finance/exchange-rates/page.tsx
- frontend/src/app/finance/exchange-rates/page.test.ts
- this ledger

## Verification

- Focused exchange-rate source contracts: passed 6/6.
- ESLint for the changed exchange-rate page and contract test: passed.
- Full frontend type-check remains blocked by unrelated repository baseline errors; no reported error identifies the exchange-rate page.
- Final diff/build checks: pending.

## Migrations and application state

- Database migrations: none.
- UAT data mutation: none.
- Deployment: not performed.
- Codex service restart: not performed.
- The user authorized a manual local API restart from the remediation worktree. A restart against a shared UAT database must be treated as a data-affecting action because startup reconciliation can create missing workflow records.

## Remaining work

1. Complete final diff checks, commit, and push the corrective set to PR #352.
2. Rebuild and restart the frontend to load the UI changes.
3. Restart the local API from the remediation worktree to load the already-built approval remediation.
4. Re-test the Accounts Officer queue with the new request and verify its workflow instance/current stage.
5. Merge, guarded UAT deployment, and any direct UAT data operation remain separately authorized actions.

## Authorization boundaries

Push/update of PR #352 is authorized. Do not merge, deploy, bypass the master-only guard, apply migrations, or directly edit UAT exchange-rate/workflow data without separate authorization.
