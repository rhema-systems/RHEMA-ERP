# Physical-count valuation and submission verification

## Verified outcome

On 12 September 2026, the saved rehearsal count **PC-20260911-0004** was submitted through the visible UI as `manager`. It now shows **Stores Approval**. No approval or stock posting was performed.

- Count ID: `cf9c91fc-95be-4ce2-b56e-ae27932a89a8`; database status: `PendingStoresApproval`.
- All nine saved lines and their total counted quantity of 1,683 were retained.
- Linked adjustment: **ADJ260001**, ID `44c7b4aa-21cd-4a21-8879-eb7174c209e0`, status `PendingApproval`.
- Workflow instance: `8fe05905-5ffd-4347-8e00-e1aa28b189fb`.
- Adjustment approval and posting timestamps remain empty.
- Before/after stock checks were unchanged: 5 stock movements, 14 journal entries, InventoryBalances quantity 77, InventoryLocations quantity 659, and item CurrentStock total 409. These are separate storage projections, not interchangeable totals.

## Cause and fix

The stock-adjustment service calculated unit costs for the exact warehouse/location, but `TR_StockAdjustmentItems_ControlledMutation` validated ordinary lines against the item-wide fallback cost. Valid location-valued lines therefore failed with `INV_ADJUSTMENT_LINE_INVALID`.

Migration `20260912013000_AlignStockAdjustmentLocationValuation` aligns that cost predicate with the service's existing valuation rules, including standard cost, location average, and negative FIFO layer consumption/fallback. Tenant, exact active location, opening-stock, quantity/value and immutable-line controls remain in place. The model and SQL unit-cost column now retain four decimals; service rounding explicitly matches SQL midpoint rounding.

The migration was applied **only to rehearsal** (`RhemaERP_PO_Rehearsal_20260909`) after a COPY_ONLY/CHECKSUM backup and successful RESTORE VERIFYONLY. Backup: `C:\Program Files\Microsoft SQL Server\MSSQL15.SQL2017\MSSQL\Backup\RhemaERP_Rehearsal_before_adjustment_valuation_20260912_004324.bak`.

Updated Core/Data assemblies were deployed to the isolated rehearsal API. Main UAT was not migrated as part of this repair.

## Approval setup

The user authorized publication of the existing **TDC Stock Adjustment Approval** definition, `936e790f-f14d-4e0d-9f77-071c0977d48d`. Rehearsal publication was performed through **Administration → Workflow → Definitions → Publish**, not direct SQL flag changes. Its configured independent approval role is **TDC_STORES_MANAGER**; `procurementapprover` is an active eligible user, separate from the counter.

The same existing version was also published in **main UAT (`RhemaERP`)**, through the authenticated central publish API as `manager`, at `2026-09-12T01:04:26.3942374Z`. The temporary loopback API had startup initialization and background services disabled and outbound requests blocked. It was stopped after verification. Main migration history, physical counts, inventory balances, adjustments, movements and workflow-instance fingerprints were unchanged. No role grants, count submissions, approvals or stock postings were made in main UAT.

Both copies now have this definition at version 1, `Published`, `IsActive=true`. The next rehearsal action belongs to the independent `procurementapprover`: open the same count and perform the Stores review. This task stopped before that decision.

The current stock-adjustment implementation still requires a workflow to start with an independent pending approval. Publishing a workflow resolves the configuration blocker but **does not implement optional approval when no workflow is active**. That behaviour remains a separate design/implementation gap; do not describe it as fixed or silently auto-approve stock adjustments.

## Where to see the PVC cost

**Reports → Inventory Reports → Balance Register → Project Demo Warehouse → Run report** shows **PVC Pipe 50mm / SKU-001 / LOC-001 → Average cost 1,918.85**. This was verified in the visible rehearsal UI.

The **1,907.09** value is the stored item-wide `InventoryItems.AverageCost`. It is not currently exposed as a separate per-item field in the UI. Do not direct users to the item's standard/current-cost fields as though they show that average.

## Verification and limits

- 19 focused C# valuation tests passed.
- SQL rollback guard checks: 10 passed; 3 skipped because the local fixture lacks a second tenant, another warehouse, and a standard-cost-method item. Standard-cost and tenant-isolation behaviour also have focused C# coverage.
- SQL guard checks retain rejection of forged costs, invalid locations/values, locked-line changes and invalid opening stock.
- Visible UI submission and persisted SQL state agree on the pending approval result.
- No downstream approval, finance posting, or stock-posting success is claimed by this submission test. Other valuation-method/legacy-layer posting paths were not changed by this repair.

## Main UAT migration status

Main UAT still needs these pending migrations with the matching updated application before this count flow can be claimed equivalent:

1. `20260909213000_LinkLandedCostsToSupplierInvoiceLines`
2. `20260911210000_PhysicalCountReviewDecisions`
3. `20260912003000_WarehouseDefaultLocations`
4. `20260912013000_AlignStockAdjustmentLocationValuation`

Workflow activation alone does not apply these migrations.
