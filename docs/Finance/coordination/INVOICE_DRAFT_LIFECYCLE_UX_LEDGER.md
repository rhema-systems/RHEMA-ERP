---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/fin-uat-invoice-lifecycle-20261004
candidate_head: cfa7a7e820a5b7c61f898be93b61e48e02e51c53
base_commit: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: focused-passed-browser-uat-pending
integration_commit: 641cfcf4d
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/352
---

# Finance UAT workstream: AR/AP draft invoice lifecycle and list consistency

## Delivered behavior

- Added a real AR Draft/Rejected invoice edit route backed by the existing guarded update API.
- Hydrates customer, dates, currency/rate evidence, discounts, tax, lines, and Finance dimensions.
- AP invalid forms now show actionable feedback and focus/scroll toward the first highlighted field instead of appearing to do nothing.
- Successful AP/AR create or update invalidates list/detail queries before navigation; AP also refreshes the route.
- Added permission-aware Draft-only delete actions to AR/AP lists and detail screens.
- Delete uses the existing server guards, destructive confirmation, success/error feedback, and query cleanup.

## Workspace and commit

- Worktree: .w/fin-uat-invoice-lifecycle-20261004
- Branch: codex/fin-uat-invoice-lifecycle-20261004
- Base: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
- Implementation: cfa7a7e820a5b7c61f898be93b61e48e02e51c53 - fix(finance): complete draft invoice lifecycle

## Verification

- Focused Vitest: 22 passed, 0 failed.
- Changed-file ESLint: passed.
- Full frontend type-check retains unrelated baseline failures; zero diagnostics referenced changed invoice files.
- git diff --check: passed.
- Browser UAT remains pending on the consolidated runtime.

## Migrations and authorization

- No migration or database mutation.
- Local commits are authorized. Push, PR creation, deployment, worktree removal, and branch deletion remain unauthorized.

## Integration outcome

Integrated in PR #352; follow-up UAT regression correction recorded below.

## 2026-10-05 UAT regression correction

- Commit `f34a61617` normalizes nullable optional AP draft fields before Zod validation and reports the exact failing field path/message instead of a generic hidden-validation toast.
- AP create/update now evicts the inactive paginated invoice-list cache before redirecting, preventing a stale list from painting after navigation.
- Invoice exchange-rate resolution now uses the API's transaction-to-functional `inverseRate`, matching server validation and posting semantics.
- Foreign-currency AR and AP forms retain the governed read-only approved rate and now provide a direct **Manage exchange rates** link.
- Null tax-treatment values no longer coerce to numeric zero during AP edit hydration.
- Verification: 13 AP invoice form tests passed; 21 exchange-rate/tax tests passed; all changed frontend files passed ESLint.
