# Finance UAT Settings and Calendar Hardening — 2026-10-01

## Scope

- Restore truthful governed account-segment lookup behavior for Legal Entity / Company.
- Move manual-journal reversal timing choice into the reversal transaction UI and API contract.
- Rename user-facing Finance labels requested during UAT.
- Consolidate the settings-facing fiscal calendar entry and harden fiscal-year sequencing and period-type consistency.
- Separate Tax Configuration overview metrics from configuration actions.

## Isolation

- Branch: `codex/finance-uat-settings-calendar-hardening`
- Worktree: `C:\Users\Akwas\Documents\DEV_WORK\RHEMA-ERP-finance-uat-settings-calendar-hardening`
- Base commit: `fc4adb57cb47b7c95ca8dcdfd88732fcac189110`
- Remote push: not authorized for this package.

## Work Items

| ID | Area | Status | Validation |
|---|---|---|---|
| UAT-FIN-01 | Legal-entity lookup contract | Complete | API projection fixed; governed UI tests pass |
| UAT-FIN-02 | Per-transaction journal reversal timing | Complete | API/UI tests pass; independent review approved |
| UAT-FIN-03 | System Accounts and Period-end wording | Complete | Targeted UI tests pass |
| UAT-FIN-04 | Fiscal navigation consolidation and invariants | Complete | Service, concurrency, migration, and UI tests pass |
| UAT-FIN-05 | Tax overview/configuration demarcation | Complete | Changed-file lint passes |

## Decisions

- Keep persisted enum/database identifiers such as `MonthEnd` for compatibility; change user-facing wording to `Period-end`.
- Keep operational fiscal-year close access separate from settings configuration, while removing the duplicate settings catalogue entry.
- Enforce fiscal continuity and established period type in the API; the UI mirrors the contract but is not the control boundary.
- Do not apply migrations or mutate tenant data in this package.

## Evidence

- Backend focused accounting-control suite: 16/16 passed. This covers contiguous year creation, stable period type, mixed-type rejection, soft-deleted outliers, the relational tenant/year uniqueness invariant, original-period journal reversal, closed-period fall-forward, invalid requested dates, and audit policy/effective-date evidence.
- Frontend affected contract suite: 21/21 passed across governed account lookup, journal reversal payload, close templates, and settings navigation.
- Changed-file frontend ESLint: passed with no findings.
- Git diff check: passed; only repository line-ending conversion notices were emitted.
- Reversible migration generated: 20261001231335_AddFiscalYearTenantYearInvariant; not applied to any database.
- Full frontend TypeScript compilation remains blocked by unrelated pre-existing errors elsewhere in the repository; no newly introduced changed-file lint or targeted-test failure remains.
- Independent accounting review: approved on second pass after stale-date reversal fallback, fiscal concurrency, and mixed-period findings were corrected.
