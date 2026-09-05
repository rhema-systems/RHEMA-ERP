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

## Separate LOCAL UAT evaluation correction (completed)

`Invoke-LocalUatEvaluationCorrection.ps1` and `local-uat-evaluation-correction-20260905.sql` implement the separately approved correction of **only TND-2026-0001**, not another reset. They must not be deployed, seeded or generalized into a production correction path.

- Exact local host/SQL/database/tenant and tender/bid IDs are guarded. The expected tender must be Closed, its only bid Opened and unscored, and its current template must match the reviewed QCBS mismatch.
- Preview defaults to read-only rollback. Rehearse and Apply require the reviewed SHA-256 state fingerprint. Rehearse performs the same changes and preservation checks but rolls back.
- A serializable transaction clones a non-default WeightedAverage template, retaining all five criteria and their scoring parameters. The shared original template remains unchanged. Only the target tender's template link and maintenance metadata change.
- Bid files and values, committee evidence, other tenders and original templates/criteria are protected by full-row content hashes. SQL triggers/constraints remain enabled. No records are deleted and no application build or migration is required.
- An append-only central audit records old/new values, authorization and `IsWorkflowApproval=false`. This is direct local maintenance, not an approval through the procurement workflow. An audit-guarded replay returns AlreadyApplied without writing duplicates.

The correction committed at **2026-09-05 21:48:32 UTC**, after fingerprint rejection and successful rollback rehearsal. Audit: `7c299fac-7af0-459a-8656-aa2f6c43c9d5`; new template: `dc2839e9-d639-4898-a674-15c0e1f917e7`. Evidence is recorded under EV-U10-007 onward in [the fresh UAT tracker](../../docs/PROCUREMENT_INVENTORY_STORES_FRESH_UAT_TRACKER.md#u10-configuration-consistency-correction--2026-09-05). No evaluation scores, evaluator assignments or award were created by this correction.
