---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/fin-uat-recurring-journal-20261004
candidate_head: 1e1df507c826e6b41afd419f37b0f76d9f19106f
base_commit: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: focused-tests-pass
integration_commit: 641cfcf4d
pull_request: pending-creation
---

# Finance UAT workstream: Recurring journal setup usability and book governance

## Objective and scope

Fix recurring-journal creation so that active posting books load and the tenant default/BASE book is selected, time zones use a searchable validated IANA picker, the UI exposes the supported recurrence choices, and posting accounts are searchable without weakening posting eligibility.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Worktree: `.w/fin-uat-recurring-journal-20261004`
- Branch: `codex/fin-uat-recurring-journal-20261004`
- Exact base: `6b59a3e841c61f5c0e295f9cce2571fc0d664d8c`
- Implementation commit: `1e1df507c826e6b41afd419f37b0f76d9f19106f`

## Implementation record

- Removed obsolete IFRS/LOCAL_STATUTORY/MANAGEMENT book whitelists from UI and API.
- The UI now lists every active book that allows posting and prefers `IsDefault`, then `BASE`, then the primary full book.
- The API resolves the selected code within the current tenant and rejects missing, inactive, deleted, or non-posting books.
- Replaced free-text time zone entry with a searchable IANA catalog picker.
- Added daily, weekly, semi-monthly, monthly, month-end, quarterly, and annual schedules with interval, weekday, and day-of-month controls serialized to the existing recurrence contract.
- Replaced line account dropdowns with a searchable account combobox while retaining the existing direct-posting and non-control filters.
- Added focused schedule serialization and book-validation regression tests, including explicit test-project discovery registration.

## Migration record

No schema or data migration is required.

## Verification evidence

- Frontend focused tests: 8 passed (3 schedule serialization and 5 existing multiline tests).
- Changed-file ESLint: passed.
- API build: passed; repository-baseline warnings only.
- Backend focused test `RecurringJournalDefinitionValidationTests`: 1 passed.
- Full frontend type-check still reports pre-existing repository errors; none reference the changed recurring-journal files.
- `git diff --check`: passed.

## Known failures and risks

- Browser UAT remains required after integration because the current runtime checkout does not contain candidate commits.
- Cross-platform .NET time-zone acceptance should be rechecked in the deployment environment; the UI emits IANA identifiers and server validation uses `TimeZoneInfo`.

## Remaining work

- Integrate this candidate through the Finance UAT consolidation protocol when authorized.
- Run browser UAT for create/edit schedule behavior after the consolidated candidate is running.

## Authorization boundaries

The user authorized implementation and commits. No push, PR creation, deployment, migration application, worktree removal, or branch deletion is authorized.

## Integration outcome

Ready for the consolidated Finance UAT candidate; not yet integrated.
