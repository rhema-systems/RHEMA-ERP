# Warehouse default bins and compact count register

## Behaviour

- Administration → Inventory → Warehouses → Locations: use **Use as default bin** when adding a location, or **Default bin** in Edit Location. Only an active, ordinary storage bin can be the default. Every warehouse has one; a new warehouse receives a default bin automatically.
- Creating an inventory item with a default warehouse, or assigning an item to a warehouse, creates its default-bin assignment. Changing the warehouse default does not move stock already assigned to another bin.
- Count creation uses the warehouse default for genuinely unlocated stock, without adding to item or warehouse totals. Warehouse-wide counts include all eligible bins; location counts include only the selected bin. Existing located stock is never duplicated into the default.
- Old count snapshots remain immutable. Safe missing-location resolutions are recorded in append-only control history and pinned to the resolved bin. An ambiguous old aggregate covering several bins is rejected instead of being assigned a guessed location.
- Existing valuation records are relocated only when their quantities reconcile with the unlocated stock. The service does not invent valuation for old operational stock without a financial balance.
- The count register shows newest-created counts first and removes the extra Items / With Variance summary line. Hidden quantity cells show **xxx** in dialog and full-page grids.
- Walkthrough B20 is updated in both Markdown and HTML.

## Automated verification

- 38 physical-count workflow/scope tests passed.
- 19 default-location service/model tests passed, including unique default configuration, safe residual allocation, ownership checks and valuation reconciliation.
- 6 default-location API controller tests passed.
- 48 count/warehouse page tests and 6 shared-grid tests passed.
- Focused TypeScript checks and the production frontend build passed.
- Core, Data and API builds passed. Existing compiler warnings remain.
- Tested Core SHA256: `74D857DAE39961D58D65E204296B817991F8E2BF1891402259382692C51D9FB4`.
- Tested Data SHA256: `9F9A1E7CD567A086D8BD79322781E5375E9CABED4FE0477DF245334408E7FE7A`.

## Deployment verification

Deployment target is rehearsal only: `RhemaERP_PO_Rehearsal_20260909`, API 5002, frontend 3002. Main UAT is not migrated or started by this update.

Migration: `20260912003000_WarehouseDefaultLocations`. It adds `WarehouseLocations.IsDefault`, a filtered unique index, default-bin metadata for existing warehouses and the append-only count location-resolution action. It does not rewrite saved count quantities or stock totals.

A copy-only, checksum-verified rehearsal backup was created before migration: `C:/Program Files/Microsoft SQL Server/MSSQL15.SQL2017/MSSQL/Backup/RhemaERP_Rehearsal_before_default_bins_20260911_2307.bak`.

Before deployment, original count PC-20260910-0001 remained UnderReview: seven active lines, system quantity total 466, counted total 513, no stock adjustment. Warehouse stock total was 659 across nine records; 77 was already assigned to bins.

## Live rehearsal checks

- Applied the exact migration to rehearsal and verified its EF history entry, unique filtered default index and exactly one active default per warehouse. Project Demo Warehouse received **DEFAULT** because its existing LOC-001 is a Zone, not an ordinary Bin. No existing stock was moved by migration.
- Loaded the production frontend on 3002 and the verified API on 5002. Main UAT 3000/5000 remained stopped. Reauthenticated as the existing manager after API restart.
- Saved **PC-20260911-0003** through the visible UI, clearly labelled `REHEARSAL DEFAULT BIN CHECK 20260911 - Warehouse-wide verification draft only; do not start.` It remains Draft, with no StartedDate or StockAdjustmentId; it was not started, submitted, approved or posted.
- The saved Items tab shows nine rows: six formerly unlocated items in DEFAULT and the original three assignments in LOC-001. All hidden quantity cells display **xxx**. Read-only SQL confirms DEFAULT contains 582 and LOC-001 still contains 77; the warehouse total remains 659. No duplicated bin quantity.
- Original PC-20260910-0001 retains all seven active snapshots: system total 466, counted total 513 and its original null snapshot locations. No old quantities or audit history were rewritten.
- The refreshed register displays PC-20260911-0003, -0002, -0001, then PC-20260910-0001, followed by the September 6 counts. Each card has one metadata line; the former Items / With Variance summary is absent.
- In the live warehouse page, Project Demo Warehouse displays **Default bin: DEFAULT**. Edit Location shows Type **Bin**, Active enabled and **Default bin** enabled, with the message that existing stock assignments stay unchanged. No location settings were changed during this read-only UI check.
