# UAT AR Schema Alignment and Stale Demo Fixed Asset Reset Summary

This is technical smoke-test evidence only. It is not accountant approval, production migration evidence, or production sign-off evidence.

## Scope

- Target database: `RhemaERP_UAT_DryRun`
- Target tenant: `Default Tenant`
- Tenant ID: `00000000-0000-0000-0000-000000000001`
- Synthetic sign-off rerun: `SIGNOFF-20260710010353-7bd1d05b`
- Book classification: `IFRS`
- Production was not touched.

## AR Schema Decision

The current Finance AR model is `ApplicationDbContext.Invoices` mapped to table `Invoices` and `InvoiceLineItem` mapped to table `InvoiceLineItem`.

`ContractorInvoice` is a legacy maintenance/project table in the cloned UAT database. It was not substituted for the current AR invoice model, and no GL-only balance was used to fake AR subledger evidence.

Fix applied: migration `20260710100000_EnsureCurrentArInvoiceTables` creates the current empty AR source tables only when they are missing:

- `Invoices`
- `InvoiceLineItem`

The migration is schema alignment for UAT/current-model compatibility. It does not migrate legacy `ContractorInvoice` rows and does not create fake AR cutover evidence.

The `CustomerId1` shadow-column issue remains fixed by ignoring the legacy `Customer.Payments` navigation; `CustomerPayment` maps through `CustomerId`.

## Fixed Asset Demo Data Treatment

The following stale demo fixed assets were soft-deleted in `RhemaERP_UAT_DryRun` only for the synthetic GL-only smoke-test scope:

| Asset code | Asset ID | Asset name | Posted event candidates | Posted journal candidates | Treatment |
| --- | --- | --- | ---: | ---: | --- |
| `FA-2024-BLDG-001` | `e09e906d-6d03-4685-ac98-6eb91ade421d` | Headquarters Building | 0 | 0 | Soft-deleted as UAT smoke-test demo data |
| `FA-2024-EQP-001` | `23e99b72-9af9-492c-baca-653777236142` | Industrial Generator | 0 | 0 | Soft-deleted as UAT smoke-test demo data |
| `FA-2024-EQP-002` | `de297d7f-c7ad-4927-b627-1e43ec9586a7` | Server Rack System | 0 | 0 | Soft-deleted as UAT smoke-test demo data |
| `FA-2024-VEH-001` | `ba1f441e-2025-41ca-82f7-1bb9213cc9c2` | Delivery Truck - Toyota Hilux | 0 | 0 | Soft-deleted as UAT smoke-test demo data |

Reason: each row had no source document, capitalization date, journal reference, posting-event reference, posted fixed-asset event, or posted fixed-asset journal. No repair was run because there was no unique posted `FinancePostingEvent` to repair from.

No posted GL was mutated.

## Evidence Files

- `synthetic-smoke-result.json`
- `synthetic-smoke-summary.md`
- `posting-backreference-diagnostics.json`
- `posting-backreference-diagnostics.csv`
- `stale-fixed-asset-reset.csv`
- `uat-ar-schema-report.json`

## Verification

- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore --verbosity minimal -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false` passed.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore --verbosity minimal -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false` passed.
- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false` passed.
- `dotnet build outputs/migration-signoff-dryrun/synthetic-cutover-smoke-test/SyntheticCutoverSmokeRunner.csproj --no-restore --verbosity minimal -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false` passed.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter FullyQualifiedName~ControlledOpeningBalancePostingTests --logger "console;verbosity=minimal"` passed `28/28`.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter "Batch~FinanceGoLive" --logger "console;verbosity=minimal"` passed `398/398`.
- `dotnet run --project outputs/migration-signoff-dryrun/synthetic-cutover-smoke-test/SyntheticCutoverSmokeRunner.csproj --no-build` reran the synthetic smoke test against `RhemaERP_UAT_DryRun`.

## Rerun Result

- Status: `PassedWithAcceptedLimitations`
- Blocking findings: `0`
- Warnings: `1`
- Warning: pending high-risk workflow instances exist and must be reviewed before production sign-off.
- Back-reference diagnostics examined `0` unresolved rows after UAT demo reset.
- `Invoices` and `InvoiceLineItem` are present in the UAT schema report.
- `FIN-LIM-0048` was marked not applicable only for this synthetic GL-only smoke test.

## Recommendation

The UAT technical smoke path is clean enough to proceed to real representative cutover input loading, provided accounting/product supplies approved cutover inputs.

`FIN-LIM-0017` remains open until representative dry-run/accountant-reviewed evidence is produced and accepted. `FIN-LIM-0048` remains globally open and must block real sign-off if AP/AR/fixed-asset source-level openings are required but not loaded through proper source-document/import paths.
