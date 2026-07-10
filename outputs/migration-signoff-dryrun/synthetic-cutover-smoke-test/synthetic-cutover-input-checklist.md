# Synthetic UAT Cutover Smoke Test Pack

Target database: `RhemaERP_UAT_DryRun`

Target tenant: `Default Tenant`

Tenant ID: `00000000-0000-0000-0000-000000000001`

Cutover date: `2026-06-30`

Fiscal period: `June 2026`

Book classification: `IFRS`

This pack is synthetic/demo data for technical end-to-end validation only. It must not be used as accountant approval, production migration evidence, or production sign-off evidence.

`ALL_ACTIVE_BOOKS` is not used and must remain rejected.

## Scope

- GL-only balanced opening trial balance.
- No AP source-level openings.
- No AR source-level openings.
- No fixed asset source-level openings.
- No bank/cash source-level openings beyond posted GL smoke lines.

## FIN-LIM-0048

`FIN-LIM-0048` is marked not applicable only for this synthetic GL-only smoke test. It remains globally open until real tenant cutover data declares whether source-level AP/AR/fixed-asset openings are required.