---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: ready
integration_decision: include
candidate_branch: codex/fin-uat-tax-provisioning-20261004
candidate_head: b8c899f363ab0f0fe0458268d9edc1455dc3601f
base_commit: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: focused-tests-passed
integration_commit: pending
pull_request: pending
---

# Finance UAT workstream: Standard tax control-account provisioning and readiness

## Objective and scope

Provision missing canonical sales, purchase, and withholding tax control-account mappings without overwriting tenant choices, and explain and enforce applicability-specific requirements in the tax editor.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Worktree: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP\.w\fin-uat-tax-provisioning-20261004`
- Branch: `codex/fin-uat-tax-provisioning-20261004`
- Exact base commit: `6b59a3e841c61f5c0e295f9cce2571fc0d664d8c`
- Candidate implementation commit: `b8c899f363ab0f0fe0458268d9edc1455dc3601f`
- Working tree status before this ledger commit: clean.

## Implementation record

- Added an idempotent `FinanceTaxAccountProvisioningSeeder` that resolves canonical controls by account code and verifies active control-account type readiness.
- Missing mappings are provisioned as follows: output and purchase-withholding liabilities to `2200`; WHT suffered receivables to `1130`; recoverable purchase VAT and levies to `1140`.
- Existing non-null tenant mappings are preserved.
- Both the normal Finance seed and explicit `seed-finance-baseline` remediation path invoke the provisioning step.
- The tax editor now marks the account side required by applicability, tax category, and recoverability, provides guidance, and prevents saving an incomplete required mapping.
- Implementation commit: `b8c899f363ab0f0fe0458268d9edc1455dc3601f` (`fix(finance): provision standard tax accounts`).

## Migration record

None. No schema migration or database mutation was performed.

## Verification evidence

- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter FullyQualifiedName~FinanceDemoPrerequisiteSeederTests`: passed 7, failed 0, skipped 0.
- `npm test -- tax-account-requirements.test.ts`: passed 3 tests in 1 file.
- `npm exec eslint -- src/components/finance/tax/TaxFormDialog.tsx src/lib/finance/tax-account-requirements.ts src/lib/finance/tax-account-requirements.test.ts`: exit 0.
- `git diff --check`: exit 0 before commit; line-ending conversion warnings only.

## Known failures and risks

- Browser UAT against a remediated existing tenant remains part of consolidated-cycle verification.
- The explicit provisioning command must be run under separate database-mutation authorization to repair an existing environment; it was not run here.

## Remaining work

Integrate this candidate into the consolidated Finance UAT branch and perform combined browser UAT.

## Authorization boundaries

The user authorized implementation and local commits. Push, PR creation, migration application, deployment, database mutation, worktree removal, and branch deletion remain unauthorized.

## Integration outcome

Pending consolidated integration.
