# UAT Schema Alignment and Final Sign-Off Re-Run Preparation

## Context

- Source database cloned: RhemaERP
- UAT dry-run database: RhemaERP_UAT_DryRun
- SQL Server: RHEMA-AKWASI\EXPRESS22
- Tenant: Default Tenant
- Tenant ID: 00000000-0000-0000-0000-000000000001
- Candidate as-of date used for read-only diagnostics: 2026-06-30
- Candidate fiscal period found: June 2026
- Production migration executed: No
- Production go-live approved: No
- Repairs/rebuilds/postings run: No
- Posted GL mutated: No

## Database Target Preparation

The existing configured database was not migrated in place.

`RhemaERP_UAT_DryRun` was created as a backup/restore clone of `RhemaERP` and all EF migration work was directed at the cloned database only.

Backup file used:

- C:\tmp\RhemaERP_UAT_DryRun_20260709_1615.bak

## Migration Application

EF initially did not discover the July 2026 Finance migrations because the manual migration classes were missing EF migration metadata. Minimal metadata was added to those existing migration classes so EF can discover them.

The following migration execution-order issues were also fixed for SQL Server:

- `20260703143000_AddFixedAssetBookValues`
- `20260706143000_AddExplicitFinanceTaxTreatmentAndWithholdingFields`
- `20260708100000_AddFixedAssetRevaluationImpairmentFoundation`
- `20260708110000_AddFixedAssetTransferFoundation`
- `20260708120000_AddFixedAssetDisposalFoundation`

These fixes do not change the accounting model. They make existing migration bodies discoverable and executable against the older cloned schema.

Final migration in `__EFMigrationsHistory`:

- 20260709120000_AddOpeningBalancePostingFoundation

EF migration list after alignment shows no pending migrations through:

- 20260709120000_AddOpeningBalancePostingFoundation

## Required Sign-Off Table Presence

All required sign-off tables were present after migration:

- FinancePostingEvents: Present
- OpeningBalanceBatches: Present
- OpeningBalanceLines: Present
- SubledgerSettlementBalances: Present
- SubledgerSettlementApplications: Present
- FixedAssetBookValues: Present
- FxRealizedSettlements: Present
- FxRevaluationBatches: Present
- FixedAssetDepreciationRuns: Present
- AssetValuations: Present
- AssetTransfers: Present
- AssetDisposals: Present
- TaxCalculations: Present

## Build And Test Results

Passed:

- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`
- `dotnet build ErpSystem.sln --no-restore -v:q /clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false /p:RunAnalyzers=false /p:RunAnalyzersDuringBuild=false`
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter FullyQualifiedName~ControlledOpeningBalancePostingTests --logger "console;verbosity=minimal"`: 26/26 passed
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter "Batch~FinanceGoLive" --logger "console;verbosity=minimal"`: 396/396 passed

## Dry-Run Data Readiness Checks

Read-only diagnostics against `RhemaERP_UAT_DryRun` showed:

- Posted GL transaction count: 0
- Opening-balance batch count: 0
- Finance posting event count: 0
- AP settlement balance count: 0
- AR settlement balance count: 0
- Fixed asset book value count: 0
- Tax calculation count: 0
- Trial balance total debit: 0.00
- Trial balance total credit: 0.00
- Trial balance difference: 0.00
- Pending workflow instances: 3
- Active current COVID levy taxes: 0
- Unposted opening-balance batches: 0
- Posted opening balances missing references: 0

These results confirm schema readiness, but not sign-off readiness. The tenant does not yet contain the migrated accounting evidence needed for a meaningful go-live dry-run.

## Cutover Inputs Supplied Or Missing

Supplied:

- Tenant ID: 00000000-0000-0000-0000-000000000001
- UAT database target: RhemaERP_UAT_DryRun
- Candidate period from data: June 2026

Missing:

- Business-approved cutover date
- Business-approved fiscal period
- Opening trial balance file/data source
- Open AP invoice declaration
- Open AR invoice declaration
- Unapplied AP payments/supplier advances declaration
- Unapplied AR receipts/customer advances declaration
- WHT/VAT withholding certificate balance declaration
- Foreign-currency open AP/AR balance declaration
- Fixed asset opening register declaration
- Accumulated depreciation/impairment opening declaration
- Detailed cashbook history declaration
- Leadership/accounting limitation acceptance decisions

## FIN-LIM-0048 Decision

Decision: Undetermined / potentially blocking.

The cutover data shape was not supplied. `FIN-LIM-0048` cannot be marked not applicable for this tenant until accounting confirms that there are no source-level AP/AR/fixed-asset opening balances in scope.

If open AP invoices, open AR invoices, fixed asset opening registers, advances, withholding certificate balances, or foreign-currency open AP/AR balances are required and not loaded through proper source-document/import paths, sign-off must remain No-Go.

## Evidence Generated

Generated:

- Schema migration evidence
- Required table presence evidence
- Build/test evidence
- Read-only data readiness evidence
- Limitation register update for `FIN-LIM-0017` and `FIN-LIM-0048`

Not generated:

- Accountant-reviewable final evidence export pack
- Posted opening-balance evidence
- AP/AR settlement rebuild evidence
- AP/AR aging/control reconciliation exports
- Fixed asset reconciliation exports
- Ghana tax reconciliation exports
- Limitation acceptance matrix signed by leadership/accounting

## Repairs And Rebuilds

None.

No bank snapshot rebuild was run.
No posting back-reference repair was run.
No AP/AR settlement read-model rebuild was run.
No opening-balance posting was run.
No posted GL record was mutated.

## Final Recommendation

Final dry-run recommendation: No-Go.

Reason: schema alignment is now resolved for the UAT clone, but the tenant does not yet have cutover inputs or migrated accounting evidence loaded.

## Remaining Blockers

- `FIN-LIM-0017`: still open and go-live blocking until a representative tenant dry-run and final accountant evidence are completed.
- `FIN-LIM-0048`: still open and undetermined until the tenant cutover data shape is declared.

## Next Required Action

Load or provide the representative cutover inputs and source data for the UAT tenant, then rerun the final sign-off pack:

- approved cutover date and fiscal period
- balanced opening TB
- AP/AR source opening decisions and files, if applicable
- fixed asset opening register decisions and files, if applicable
- bank/cash cutover detail decision
- limitation acceptance/not-applicable decisions
