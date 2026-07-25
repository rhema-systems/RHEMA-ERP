# Final Migration Sign-Off Scenario Pack PR Summary

## Scope

Implemented backend final migration/sign-off scenario pack support for tenant-level dry-run and UAT evidence generation.

This batch does not execute final production migration and does not mark production go-live approved.

## Files Changed

- `src/ErpSystem.Core/DTOs/Finance/MigrationSignOffDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IMigrationSignOffService.cs`
- `src/ErpSystem.Api/Services/Finance/Migration/MigrationSignOffService.cs`
- `src/ErpSystem.Api/Controllers/Finance/MigrationSignOffController.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/ControlledOpeningBalancePostingTests.cs`
- `docs/final-migration-signoff-scenario-pack.md`
- `docs/final-migration-sign-off-preparation-checklist.md`
- `docs/opening-balance-verification-migration-tooling-signoff-readiness.md`
- `docs/sql/final-migration-signoff-diagnostics.sql`
- `docs/finance-go-live-limitations-register.md`

## Migrations

No schema migration was added.

The sign-off pack is an auditable DTO/report output. A persistent production sign-off run table remains unnecessary until actual production sign-off execution is modeled.

## Behavior

`IMigrationSignOffService.RunFinalMigrationSignOffAsync` now produces:

- run identity and status;
- tenant-scoped checks;
- evidence export manifest;
- limitation acceptance matrix;
- `FIN-LIM-0048` cutover data-shape evaluation;
- audit events.

`IMigrationSignOffService.ReviewSignOffRunAsync` records review decisions without marking production go-live approved.

## Checks

Implemented checks for:

- posted GL trial balance balance;
- opening-balance posting status and references;
- duplicate opening-balance posting events;
- posting back-reference diagnostics;
- bank snapshot variance;
- AP/AR settlement read-model diagnostics;
- AP/AR control account variance;
- fixed asset missing references and disposed nonzero NBV;
- tax snapshot/configuration diagnostics and active current COVID levy;
- pending workflow instances;
- direct posting bypass evidence.

## FIN-LIM-0048

The run request declares the cutover data shape.

`FIN-LIM-0048` is not applicable only when the tenant cutover has no AP/AR/fixed-asset source-level opening balances requiring aging/register proof.

`FIN-LIM-0048` blocks sign-off when source-level openings are required but unsupported.

## Tests

Added focused tests:

- `FinalSignOffCleanFixture_ShouldPassWithAcceptedLimitations`
- `FinalSignOffUnbalancedTrialBalance_ShouldFail`
- `FinalSignOffMissingOpeningBalanceReferences_ShouldFail`
- `FinalSignOffBankSnapshotVariance_ShouldFailUnlessAccepted`
- `FinalSignOffFinLim0048_ShouldBlockWhenSubledgerOpeningsAreRequired`
- `FinalSignOffUnacceptedGoLiveLimitation_ShouldFail`
- `FinalSignOffBackReferenceCandidate_ShouldAppearInDiagnostics`
- `FinalSignOffActiveCurrentCovidLevy_ShouldFail`
- `FinalSignOff_ShouldIgnoreCrossTenantData`
- `FinalSignOffReview_ShouldEmitAuditEvent`

## Limitations

- `FIN-LIM-0017` is materially narrowed for tenant-level dry-run readiness but remains open until production migration execution and accountant sign-off evidence are completed.
- `FIN-LIM-0048` remains open and blocks final sign-off when required source-level AP/AR/fixed-asset opening balances are present and unsupported.
