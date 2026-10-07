---
integration_cycle: FIN-UAT-2026-10-04-A
integration_status: integrated
integration_decision: include
candidate_branch: codex/fin-uat-inventory-bulk-20261004
candidate_head: 1500b62be6d9ebd766844bc6526276b0d56742d1
base_commit: 6b59a3e841c61f5c0e295f9cce2571fc0d664d8c
target_ref: origin/master
depends_on: none
migration_status: none
verification_status: focused-tests-pass
integration_commit: 641cfcf4d
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/352
---

# Finance UAT workstream: Scalable inventory opening capture

## Objective and scope

Make governed inventory opening practical for hundreds or thousands of item/location balances while retaining Inventory-owned master data, immutable evidence, server-derived posting accounts, atomic validation, and idempotent schedule preparation.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Worktree: `.w/fin-uat-inventory-bulk-20261004`
- Branch: `codex/fin-uat-inventory-bulk-20261004`
- Exact base: `6b59a3e841c61f5c0e295f9cce2571fc0d664d8c`
- Implementation commit: `1500b62be6d9ebd766844bc6526276b0d56742d1`

## Implementation record

- Added downloadable inventory-opening CSV template and CSV/XLSX import.
- Import resolves item and location codes only against the server-provided eligible catalog for the selected warehouse.
- Preflight validates required columns, positive quantity/cost, serial/lot/batch requirements, serial quantity, dates, duplicate identities, file readability, and a 10,000-row per governed-batch ceiling.
- Any preflight error preserves the current draft; valid rows are applied only when the complete file passes.
- Added row-level feedback with a downloadable complete error report, valid row count, paged preview, and derived total.
- Uses the import filename as the default source schedule reference when the operator has not supplied one, retaining a durable audit/idempotency identity.
- Renders at most 50 editable rows per page so a large import does not mount thousands of interactive cards.
- Replaced manual item and location dropdowns with searchable Finance pickers.
- Preserved the existing server endpoint and authority boundary: one atomic transaction, tenant and location scope checks, duplicate rejection, immutable evidence, server-derived posting, and source-schedule idempotency.

## Migration record

No schema or data migration is required. No staging tables were added; imported rows are preflighted in the browser and then submitted through the existing atomic governed opening-stock contract.

## Verification evidence

- Frontend focused tests: 11 passed (4 import tests and 7 governed-source regressions).
- Includes a 1,000-row import test with totals and no UI rendering dependency.
- Changed-file ESLint: passed.
- Full frontend type-check reports repository-baseline errors; zero errors reference the changed governed-opening or import files.
- Existing backend `StockAdjustmentsOpeningStockControllerTests`: 2 passed; repository-baseline compiler warnings only.
- `git diff --check`: passed.

## Known failures and risks

- Browser UAT with representative customer CSV and XLSX files remains required after integration.
- This candidate deliberately uses the existing atomic request rather than durable server-side import staging. The 10,000-row ceiling bounds request size; exceptionally large estates should split schedules by warehouse/source reference.
- Spreadsheet preflight is based on the options snapshot loaded for the actor. The server revalidates all authority and master data at preparation time to handle concurrent changes.

## Remaining work

- Integrate through the Finance UAT consolidation protocol when authorized.
- Run browser UAT with ordinary, serial-, lot-, and batch-tracked examples after the consolidated runtime is updated.

## Authorization boundaries

The user authorized implementation and commits. No push, PR creation, deployment, migration application, worktree removal, or branch deletion is authorized.

## Integration outcome

Ready for the consolidated Finance UAT candidate; not yet integrated.
