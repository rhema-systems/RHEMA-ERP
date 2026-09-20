# Main UAT count migration parity

Read-only verification on 12 September 2026 found **four pending migrations** in local main UAT (`RhemaERP`). Rehearsal has all four. This note does not record a main migration application.

| Migration | Purpose |
| --- | --- |
| `20260909213000_LinkLandedCostsToSupplierInvoiceLines` | Nullable invoice-line link to landed-cost charges, unique active link and trusted FK. Does not create or post invoices. |
| `20260911210000_PhysicalCountReviewDecisions` | Count review/investigation lifecycle, correction and audit/freeze guards. Does not rewrite count quantities or approve/post counts. |
| `20260912003000_WarehouseDefaultLocations` | Default-bin metadata/index and supported default-location audit action. Does not relocate existing item balances or change stock. |
| `20260912013000_AlignStockAdjustmentLocationValuation` | Four-decimal adjustment unit costs and location-aware valuation validation. Does not change item costs, count quantities or post stock. |

## Verify without changing either database

From the regular repository, run PowerShell 7:

```powershell
./scripts/procurement/Test-LocalUatMigrationParity.ps1 -Database RhemaERP
./scripts/procurement/Test-LocalUatMigrationParity.ps1 -Database RhemaERP_PO_Rehearsal_20260909
```

The script contains only SELECT/metadata queries and has no apply option. It verifies the exact local server/database, compares all source migration IDs with migration history, and reports schema, enabled control guards, default bins and count/stock preservation baselines. It never prints credentials or connection strings.

Initial result: main 458 migration records, rehearsal 462; no unexpected pending migrations. Main has no invoice landed-cost link or default-bin flag, and adjustment `UnitCost` is still scale 2. Rehearsal has both fields, trusted/unique constraints, scale 4 and one valid `DEFAULT` bin for Project Demo Warehouse. All seven inspected count/adjustment guards are enabled in both databases.

## Safe main application sequence

1. Keep normal main UAT servers stopped. Run the read-only parity script and retain its baseline. Stop if the pending set contains anything beyond the reviewed four migrations.
2. Back up **only `RhemaERP`** on the verified local SQL instance using a new unique file in its configured backup directory. Use `BACKUP DATABASE [RhemaERP] ... WITH COPY_ONLY, CHECKSUM`, followed by `RESTORE VERIFYONLY ... WITH CHECKSUM`. Do not proceed unless both succeed.
3. For this exact reviewed rollout, use `Apply-MainUatCountMigrationParity.ps1` as described below. It pins all four migration source hashes, creates/verifies the backup, applies their exact operations in one transaction, records history only after the operations succeed, and verifies preserved data before committing. Alternatively, a verified matching updated API runtime can use the existing **`apply-migrations`** entry point after a separately verified backup. That entry point does not start HTTP servers, run startup seeders, activate workflows or approve/post transactions; it intentionally applies migrations regardless of the normal-host `SkipStartupInitialization` setting.
4. Run the read-only parity script again. Require no pending migrations, the expected invoice link/index/FK, a unique valid default per warehouse, adjustment unit-cost scale 4, and enabled control guards. Compare count/item, inventory balance, movement and adjustment baselines before any separate data repair or UAT transaction.
5. Start the updated main UAT application only after verification. Do not advance its counts to demonstrate the rehearsal result. Preserve main approval history and transaction state.

Do not use ordinary frontend startup as evidence of database migration success. Do not stamp migration history manually or apply rehearsal-only SQL to main. Any failure stops this sequence; inspect the precise failed migration before retrying.

## Guarded exact-main helper

The helper is read-only by default. Run the preview, review its pending list and preservation hashes, then supply that exact hash only when applying the authorized main rollout:

```powershell
./scripts/procurement/Apply-MainUatCountMigrationParity.ps1
./scripts/procurement/Apply-MainUatCountMigrationParity.ps1 -Apply -ExpectedPreviewHash '<reviewed PreviewHash>'
```

It uses existing integrated Windows authentication against only `RHEMA-MICHAEL\SQL2017` / `RhemaERP`. It does not stop or start applications. Ports 3000/5000 must be stopped for application; rehearsal ports are untouched. If SQL cannot report its default backup directory, supply the verified absolute directory through `-BackupDirectory`.

The helper uses the pinned migration source rather than assuming a previously built Data assembly matches the current source. For the review/valuation migrations it reads the exact `UpgradeSql`; for default locations it reads the exact first SQL block after executing the pinned EF column/index equivalents; the invoice link is the pinned column/index/FK equivalent. Separate batches ensure new columns exist before dependent SQL is compiled. All four operations and history entries commit together or roll back together.

It rejects any pending set other than the exact four, unreviewed historical migrations, changed migration source, changed preserved data/guards, invalid defaults, or missing trusted/unique constraints. The already-retained `20260402003233_InitialCreate` historical record is explicitly allowed and preserved unchanged. Decimal fingerprint values are canonicalized so widening adjustment precision does not appear as a business-value change. Inventory, counts, invoices, journal and workflow/approval tables are checked before commit. Default-bin metadata is the only intended data change.

The preview was followed by an authorized successful `-Apply` run on 12 September 2026. A separate read-only verification confirmed **462 applied migrations, none pending**, the expected constraints/precision/default metadata, and preserved operational fingerprints. See [physical-count end-to-end evidence](PHYSICAL_COUNT_E2E_20260912.md) for the subsequent setup and rehearsal status; migration parity alone is not end-to-end acceptance.

## Existing script boundaries

- `Apply-LocalPhysicalCountReview.ps1` supports the two exact local databases, but covers only the review migration and does not create a backup. Its default mode executes SQL inside a rolled-back transaction: it is **not** a read-only inspection command.
- `Apply-LocalStockAdjustmentValuation.ps1` is guarded to rehearsal only. With `-Apply` it creates and verifies a backup; without it SQL is still executed and rolled back.
- `Apply-RehearsalLandedCostInvoiceLink.sql` is explicitly rehearsal-only.
- `Repair-LocalPhysicalCountLegacyTotals.ps1` is a separate, hash-guarded data repair, **not a migration**. Do not combine it with schema application or claim migration parity repairs legacy totals.
- `Program.cs` → `apply-migrations` is the existing coherent migration-only path for the reviewed main pending set. No new broad seeding command is needed.

## Default-bin ownership

The migration selects the sole eligible ordinary active Bin, or creates a dedicated `DEFAULT` bin when there is no unambiguous eligible candidate. Special-purpose, consignment and non-Bin locations are not selected as defaults.

`WarehouseDefaultLocationService` owns subsequent assignments. New warehouse creation obtains a default; item-to-warehouse creation/assignment and count preparation call the shared service. For unlocated residual quantities it verifies warehouse/bin agreement, retains item/warehouse totals, and records default-bin assignment evidence. The migration itself only establishes metadata; it does not invent valuation balances, move already located stock or correct legacy item totals.
