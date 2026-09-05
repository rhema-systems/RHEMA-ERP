# Local DEFAULT UAT reset

`Invoke-DefaultTenantUatReset.ps1` is a one-off maintenance tool for the reset explicitly authorized on 2026-09-05. It is not a production migration, generic tenant eraser or application-startup task.

## Scope and safeguards

- Exact host `RHEMA-MICHAEL`, SQL instance `RHEMA-MICHAEL\SQL2017`, database `RhemaERP`, DEFAULT tenant GUID only. Credentials are read from the existing local development secret store, never embedded or logged.
- Explicit procurement/supplier/stock roots plus `PRJ-DEMO-*` projects and their linked QS transactions. Current maintained configuration, customers, non-demo projects, other tenants, users and payroll are preserved.
- A dependency closure uses real primary/foreign keys and reviewed ownership/polymorphic links. Unselected dependents stop the operation. The maintenance asset is retained and only detached from its deleted demo project. Unrelated WorkOrder inventory allocations are preserved.
- Preview computes a SHA-256 fingerprint of the exact target rows. Rehearse/Apply require that same fingerprint and a stopped local API. A change in the data stops the operation.
- A serializable SQL transaction suspends only affected triggers and internal reset FK edges, clears the selected rows and restores the original controls. Cross-boundary dependents are never swept automatically.
- Scoped GL cache movements are subtracted according to the existing Finance posting convention. Only UAT inventory item caches are cleared; other stock is preserved. Account-currency links were absent at inspection; their presence stops the reset for separate reconciliation.
- Full row-content hashes and counts verify preserved data. Rehearse rolls back; Apply commits only after the same checks pass.
- Physical DMS files, numbering configuration and unrelated audit history are not erased. No new backup is made because the user already took one.

## Operation

1. Preview and inspect the locally generated manifest and boundary list.
2. Set the local `StartupInitialization:SeedDevelopmentData` option to `false` so restart cannot recreate project demos. Keep ordinary baseline configuration initialization intact.
3. Stop only the verified local API process, then produce a fresh preview while writers are stopped.
4. Run `-Mode Rehearse -ExpectedFingerprint <reviewed fingerprint>` and inspect its rolled-back verification report.
5. Run `-Mode Apply` with that same fingerprint only after rehearsal succeeds.
6. Restart the existing API build, verify health, repeat SQL counts and inspect the empty UAT business registers in the browser.

Reports belong under the ignored `local-artifacts/uat-reset-20260905/` folder. They contain counts, hashes and record identifiers, not authentication secrets. The old UAT tracker is retained as historical evidence; the new campaign is tracked in `docs/PROCUREMENT_INVENTORY_STORES_FRESH_UAT_TRACKER.md`.

## Completed execution

The authorized reset committed on **2026-09-05 at 00:54:55 UTC**, deleting **2,883 rows from 151 tables** after successful rollback rehearsal and preservation checks. Post-restart SQL and visible browser checks are recorded in [the fresh UAT tracker](../../docs/PROCUREMENT_INVENTORY_STORES_FRESH_UAT_TRACKER.md#reset-evidence). Do not rerun Apply against newly created business data: this script belongs to the completed, specifically authorized reset, not to routine UAT setup. Recovery of the removed records requires the operator's existing backup.
