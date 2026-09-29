# Finance Layout Approval and Classification Role Cardinality

## Objective

1. Route financial-statement layout publication through independent Finance approval and expose submitted versions in the Finance Approval Workbench.
2. Treat account-classification system roles as repeatable semantic families while exact posting accounts remain governed by module configuration.

## Boundaries

- Base: `cbb0021f5`
- Branch: `codex/finance-layout-approval-role-cardinality`
- Worktree: `C:\Users\Akwas\Documents\DEV_WORK\RHEMA-ERP-layout-approval-role-cardinality`
- The running manual-test API and database must not be stopped, restarted, migrated, or otherwise mutated.
- The migration is authored and tested only; application requires separate user authorization.
- Other modules remain read-only except for inspection of existing exact-account contracts.

## Acceptance criteria

- Draft layout versions can be submitted only after successful validation.
- Submitted versions are immutable and appear in a dedicated Finance Approval Workbench queue.
- The submitter cannot approve or reject their own version, at both API and UI layers.
- Approval publishes the exact submitted revision; rejection returns it to Draft with a required reason.
- Direct publication cannot bypass submission and independent approval.
- Classification system roles can repeat within a tenant/accounting book without weakening type, hierarchy, posting-leaf, or tenant validation.
- Database uniqueness is removed through a forward migration and current model/index metadata is aligned.
- Fixed-asset category tests prove distinct role-aligned classifications/accounts remain valid.

## Status

`VERIFIED — READY FOR INTEGRATION`

## Verification ledger

- API build: succeeded with zero errors (`dotnet build ...ErpSystem.Api.csproj`).
- Focused backend regression tests: statement-layout maker/checker, submitted preview isolation,
  repeatable classification-role families, migration operations, and category-specific Fixed Asset
  posting accounts passed (6 focused cases across the targeted runs).
- Frontend statement-layout service tests: 6/6 passed.
- Changed frontend files: targeted ESLint passed with no findings.
- EF migration/model check: no pending model changes. The pre-existing BudgetRevisionLine
  dimension-set relationship was also corrected in the model snapshot to match its already-authored
  migration; no duplicate database operation was added.
- Full frontend type-check remains blocked by unrelated pre-existing errors in Civil Engineering,
  HR Medical, external portal, Estate, Projects, and Report Builder files; no diagnostic points to a
  file changed by this work.
- The broad AccountingBookClassificationAuthorityTests run exposed nine pre-existing manifest/
  provisioning fixture failures. Every classification-cardinality test changed or introduced here
  passed when isolated.
- `git diff --check`: passed (line-ending conversion notices only).
