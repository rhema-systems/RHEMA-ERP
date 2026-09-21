# Finance initialization UX and demonstration pass — 2026-09-21

## Objective

Remove misleading initialization amount editing, verify behavior across Delta and full-book modes, and document the safe next step for an IFRS + Delta stakeholder demonstration.

## Boundaries

- No database mutation, posting, approval, migration application, push, or merge.
- Existing dirty worktree changes remain preserved.
- Base + Delta reporting was explicitly approved for the Trial Balance reporting view; the accounting-book route remains a shortcut.

## Completed

- Debit and Credit are now read-only governed evidence in every initialization mode.
- Independent openings explain that the amounts are derived from posted exact-book authority.
- Base-book copy explains that the source amounts are reproduced exactly and cannot be overridden.
- Base balances with opening adjustments permits editing only the Adjustment field and derives Debit/Credit from `Base signed + Adjustment`.
- Delta initialization explicitly explains its zero-only evidence and that adjustment journals are posted only after activation.
- Added focused coverage for independent full-book, adjusted parallel-book, and zero-only Delta behavior.
- Added a guided setup checklist and a state-derived **Next required action** on book readiness without collapsing the independent initialization, period, and activation approvals.
- Added **Single book / Base + Delta** reporting views to Trial Balance, with the governed base book inferred from the selected active Delta layer.
- Added a dedicated `Finance.BaseDeltaReport` document builder, shared Finance export permission enforcement, and PDF print/export from both Trial Balance and the accounting-book shortcut.
- Confirmed that controlled opening-balance lines do not require transaction-dimension values. Transaction dimensions remain journal coding; segmented COA identity is a separate account-combination concern. No speculative business dimension masters were seeded during the initialization pass.

## Authorized follow-on: TDC demo transaction dimensions

The user subsequently authorized a missing-only TDC demonstration baseline so Finance demos and other module integrations can use stable transaction-dimension codes.

- Added the explicit `seed-finance-demo-dimensions` command. It does not run migrations, provision books, post journals, create account rules, or assign journal defaults.
- Seeded 22 values: 7 Department, 2 Project, 3 Estate/Site, 3 Contract, 3 Funding Source, and 4 Activity/Programme.
- Department codes and names are mirrored from the existing TDC organisation-unit authority. They remain controlled Finance lookup values because the existing dimension definition and journal contract are lookup-backed; no unsupported cross-module foreign key was fabricated.
- Invented Project, Estate/Site, and Contract rows use `DEMO-` codes and `(Demo)` names.
- Existing values are preserved by exact dimension/code identity, including administrator edits. Re-running the command is idempotent.

### Follow-on verification and database state

- Data project build: **passed** (pre-existing warnings only).
- Focused manifest and demo-dimension tests: **2 passed, 0 failed**.
- Applied the dedicated command twice to the DEFAULT tenant on `RhemaERP`; both executions succeeded.
- Live counts after the second run: Activity 4, Contract 3, Department 7, Estate 3, Funding Source 3, Project 2.
- Live Finance dimension account-rule count remains **0**.
- No migration was created or applied and no accounting transaction was posted.
- API restarted without startup initialization and `/health/live` returned HTTP 200.

## Verification

- Focused readiness, Trial Balance, and Delta report component suites: **19 passed, 0 failed**.
- Base + Delta PDF builder: **1 passed, 0 failed**.
- Targeted ESLint for the changed pages and tests: **passed**.
- Repository-wide TypeScript check remains blocked by existing unrelated errors across Inventory, Procurement, Projects, report fixtures, and other baseline files. No error was reported in either changed readiness file.

## Implemented reporting decision

Base + Delta is an explicit Trial Balance reporting view. It does not silently reinterpret a Delta-only book selection as a combined report, and the accounting-book card link remains available as an administrative shortcut.

## Safe demonstration order

1. Complete zero-only Delta initialization and activation.
2. Enter and approve representative IFRS journals through the UI.
3. confirm the IFRS trial balance.
4. Enter and approve an explicit Delta Adjustment journal.
5. compare IFRS-only, Delta-only, and Base + Delta results.
