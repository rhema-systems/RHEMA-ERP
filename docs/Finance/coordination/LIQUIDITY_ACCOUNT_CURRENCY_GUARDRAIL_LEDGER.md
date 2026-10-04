---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: ready
integration_decision: include
candidate_branch: codex/finance-budget-posting-evidence
candidate_head: e7128104501d04f2c9619e9c61c02be79a56d7dd
base_commit: d6ce7f432ed496394f6071572891979a96294a28
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: focused-passed-browser-uat-pending
integration_commit: pending
pull_request: pending
---

# Finance UAT workstream: Liquidity account currency guardrails

## Objective

Prevent liquidity-account masters from pairing a currency with a GL control account that cannot post that currency, and prevent Bank-type liquidity accounts from diverging from their linked bank master.

## Scope

- New Liquidity Account UI field dependency and ordering.
- Currency choices limited to the selected GL account's primary currency and active, currently effective currency links.
- Bank-type GL account and currency derived from the selected bank account master.
- API validation for GL/currency compatibility and bank-master mapping consistency.
- Focused frontend and banking service regression coverage.

## Branch and base

- Branch: `codex/finance-budget-posting-evidence`
- Worktree: primary checkout at `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Exact base commit: `d6ce7f432ed496394f6071572891979a96294a28`

## Completed changes

- Reordered the form so the GL control account precedes Currency.
- Added GL-account currency-link loading and eligibility filtering.
- Made Bank account master selection derive and lock the GL account and currency.
- Added server-side rejection for unsupported single-currency and multi-currency GL mappings.
- Added server-side rejection when Bank liquidity mapping differs from its bank master.
- Added focused frontend source tests and banking release-gate tests.

## Changed files

- `frontend/src/app/finance/cash/liquidity-accounts/new/page.tsx`
- `frontend/src/app/finance/cash/liquidity-accounts/new/page.test.ts`
- `src/ErpSystem.Api/Services/Finance/Cash/BankingSettlementService.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankingSettlementReleaseGateTests.cs`
- `docs/Finance/coordination/LIQUIDITY_ACCOUNT_CURRENCY_GUARDRAIL_LEDGER.md`

## Commits

- `e7128104501d04f2c9619e9c61c02be79a56d7dd` — `fix(finance): govern liquidity account currencies`

## Migrations and application status

- No schema or data migration required.
- Changes are committed locally and have not been pushed, integrated, or deployed.

## Verification

- `npm test -- --run src/app/finance/cash/liquidity-accounts/new/page.test.ts`: passed, 2/2 tests.
- `npx eslint src/app/finance/cash/liquidity-accounts/new/page.tsx src/app/finance/cash/liquidity-accounts/new/page.test.ts`: passed with no findings.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter FullyQualifiedName~BankingSettlementReleaseGateTests --no-restore`: passed, 14/14 tests. The run compiled Core, Data, API, and the test assembly successfully; existing compiler warnings remain.
- `npm run type-check`: failed on pre-existing errors in unrelated civil-engineering, inventory, procurement, reporting, and legacy test files. No diagnostic referenced either changed liquidity-account frontend file.

## Known failures

- Repository-wide frontend type-check already has unrelated failures outside this workstream; see task verification output if rerun.

## Remaining work

- Perform browser UAT after deployment or with a suitable local application environment.
- Include this workstream in the future unified PR for integration cycle `FIN-UAT-2026-10-04-A` when the user explicitly starts consolidation.

## Authorization boundaries

- The user authorized the local implementation commit and marked this workstream for inclusion in the future unified Finance UAT PR.
- No push, pull request creation, deployment, database migration, or external-state mutation is currently authorized.

## Integration outcome

- Ready local candidate. Browser UAT remains an integration verification item.
