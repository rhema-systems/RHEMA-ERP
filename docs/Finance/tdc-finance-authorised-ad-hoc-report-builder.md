# TDC Finance Authorised Ad Hoc Report Builder (FR-RP-012)

## Intent

FR-RP-012 requires authorised Finance users to create useful one-off analyses without giving a browser, or an ordinary user, direct SQL access. This implementation deliberately extends the shared Reports execution and export foundation instead of creating a second reporting engine.

The browser stores a declarative definition made of server-issued dataset, field, filter, aggregation and sort keys. The server owns every SQL expression, join and tenant predicate.

## User workflow

1. Open **Reports > Financial Reports > Ad hoc report builder**.
2. Select one curated Finance dataset: GL lines, AP invoices, AR invoices, fixed assets or chart of accounts.
3. Select up to 20 columns and optional calculations, filters and sorting.
4. Save the definition as private, or publish it to authorised Finance report users when the user has the separate share permission.
5. Run a governed preview or export through the existing shared Reports service to XLSX or PDF.

Private definitions are visible only to their owner. Finance-shared definitions may be run or exported by users with the corresponding Finance reporting permissions, but remain read-only to everyone except their owner.

## Security and control design

- `Finance.Reports.AdHoc.Build` controls entry to the builder and definition lifecycle.
- `Finance.Reports.AdHoc.Share` permits an owner to publish a definition to the Finance audience; it does not transfer ownership.
- Existing `Finance.Reports.Run` and `Finance.Reports.Export` permissions control execution and export.
- Every definition and query is tenant-scoped on the server. The compiler always injects the tenant predicate and never accepts a table, join, SQL fragment or expression from the client.
- Filter values are converted to typed `SqlParameter` values. Catalogue field aliases are the only dynamic identifiers.
- Definition row ceilings are limited to 5,000 rows and shared execution paging cannot exceed the saved ceiling.
- Create, update and delete emit dedicated Finance audit events. Execution and export retain the existing shared report evidence and usage history.
- Optimistic row-version concurrency prevents one editor from unknowingly overwriting a newer definition.

## Existing capability reused

The new provider implements `ISystemReportProvider`, so saved definitions appear within the established shared Reports catalogue and use `IReportsService` for execution and export. `DatabaseReportsService` now evaluates record-level provider access before listing or opening a report; this prevents private definition names from leaking through the shared catalogue.

The old generic administration report builder was not extended because it exposes database schema and constructs SQL in the browser. It remains a separate legacy capability and is not the FR-RP-012 security boundary.

## Accounting impact

This is a read-only reporting capability. It creates no journals and changes no accounting or operational transaction. GL datasets read posted lines; AP, AR and asset datasets read their existing authoritative Finance records.

## Migration and rollback

Migration `20260809093427_AddFinanceAdHocReportBuilder` creates only `FinanceAdHocReportDefinitions` and its indexes/foreign keys. It does not rewrite existing reports or accounting data.

Rollback removes saved ad hoc definitions and their linked shared Report rows through the migration `Down` path. It does not reverse or alter Finance postings.

## Verification and UAT

Automated release checks cover:

- exact curated dataset catalogue;
- hostile filter text remaining parameterised;
- mandatory tenant predicate;
- aggregation, grouping, sort and saved row ceiling;
- purpose-specific controller permission mapping;
- backend compilation, migration discovery and targeted frontend lint/type checks.

TDC UAT should create a private definition, confirm it is absent for a second user, publish another definition with the share permission, run it as an authorised colleague, export both formats and inspect the audit/report history.

## Honest boundary

The first production catalogue contains five high-value Finance datasets and can be extended server-side as TDC approves additional fields. Cross-module datasets, user-authored joins, unrestricted SQL and automated distribution are intentionally outside this Finance-only slice. Automated delivery is handled independently by FR-RP-010.
