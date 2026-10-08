# AR Invoice Final Approval Authority Mismatch

## Objective

Diagnose and remediate the `SOURCE_BOOK_AUTHORITY_POSTING_MISMATCH` raised when the Financial Controller gives final approval to customer invoice `INV-202610-0005` in the deployed VPS application.

## Scope and authorization

- Repository implementation and verification are authorized.
- Deployment, VPS/database mutation, workflow mutation, push, and pull-request creation remain unauthorized.
- Remote investigation remains read-only.

## Repository state

- Worktree: `.w/latest-master-test-20261008`
- Branch: `codex/ar-invoice-authority-mismatch-fix`
- Exact base: `cec3306f49ae02d2ce6842936d456106e90c2e46` (`origin/master` on 2026-10-08)
- The primary checkout was already materially dirty and was not modified.
- Deployed API reported release commit `10036aff220647e37cb2c43318a5e95fd4127f69`; that object is unavailable in the local repository.

## Incident evidence

- Invoice id: `e51bf21f-9a32-49d6-86f2-6f114a9d6179`
- Invoice number: `INV-202610-0005`
- Business partner id: `43a92487-f256-476e-9035-b043b3a0b6fb`
- Amount/currency: GHS 520,000.00
- Approval id: `9fe166e3-ed5b-4caa-afed-8406c5487d79`
- Actor: `financial.controller`
- The same final approval failed at approximately 21:29, 21:30, and 21:31 on 2026-10-07.
- Trace: `0HNP4MDET9AGD:0000001C`
- Exact error: `SOURCE_BOOK_AUTHORITY_POSTING_MISMATCH: posted event/journal differs from frozen authority.`
- No invoice journal is visible because workflow completion and posting share a transaction that rolls back when authority binding fails.

## Root-cause finding

- Source-book binding requires the frozen authority, posting event, and journal to share the canonical top-level origin module.
- Finance posting sources such as `AR` resolve to canonical Finance origin `FIN`.
- Commit `311c34698` (`finance-harden-source-authority-callers`) changed AR invoice authority creation to use `FinanceModuleLockCatalog.Finance` for direct Finance posting and `ResolveOriginModuleCode(...)` for producer routes.
- Current `origin/master` contains that functional fix. The deployed release commit is not locally available and exhibits the pre-fix behavior.
- The prior final-approval regression did not configure a real source-book authority service and failed before exercising the production path, leaving this scenario without effective regression coverage.

## Changes

- Updated `ArInvoicePostingMigrationTests.PendingApprovalArInvoice_ShouldPostWhenFinalApprovalReleasesIt` to model the full submitted-authority lifecycle:
  - pending invoice and active workflow;
  - frozen submitted primary-book authority;
  - completed maker-checker approval evidence;
  - final AR posting;
  - event/journal binding to the authority;
  - canonical `FIN` origin assertions on authority and posting event.
- Added a focused workflow-approval evidence helper used only by this regression.

## Verification

- Baseline focused test failed because the old fixture omitted `IFinanceSourceBookAuthorityService`.
- Updated focused test passes:
  - `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PendingApprovalArInvoice_ShouldPostWhenFinalApprovalReleasesIt" --verbosity minimal`
  - Result: 1 passed, 0 failed.
- Expanded focused batch passes:
  - incident regression, `FinanceSourceBookAuthorityServiceTests`, and `FinanceSourceBookAuthorityCallerAdoptionTests`;
  - Result: 28 passed, 0 failed, 0 skipped.
- `git diff --check` passed; only the repository's Windows line-ending notice was emitted.
- Existing build warnings include known ImageSharp vulnerability advisories and unrelated compiler/analyzer warnings.

## Migrations and application status

- Migrations added/applied: none.
- VPS deployment/database changes: none.
- Push/PR: none.

## Remaining work

1. Commit the regression on the fix branch.
2. Deploy a build containing commit `311c34698` and this regression after explicit authorization.
3. Re-submit or otherwise re-freeze `INV-202610-0005` through the supported workflow if its persisted authority was created by the pre-fix build; do not manually edit authority or journal rows.
4. Retry final approval and confirm a single posted AR journal is created and bound.

## Known limitations

- The exact deployed source commit is unavailable locally, so the pre-fix implementation cannot be directly diffed against the reported release SHA.
- Browser-visible logs do not expose the individual frozen-versus-posted coordinate values.
