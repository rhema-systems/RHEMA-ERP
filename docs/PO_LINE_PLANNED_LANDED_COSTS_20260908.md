# PO line-specific planned landed costs

## Customer walkthrough

1. Open a Draft PO and choose **Edit**.
2. On the relevant item row, open **… → Planned landed costs**.
3. Add the estimated freight, duty, insurance or other charges for that item. Enter the amount, currency and exchange rate. A cost supplier and reference are optional.
4. Choose **Save line costs**. This applies the charges to the current PO form, not yet to the database. **Cancel** discards dialog changes.
5. Repeat for another item if needed. The row action shows the number of costs assigned to that row.
6. Use **PO-wide planned landed costs** below the PO total only for shared charges. Do not enter the same charge here and on a line.
7. Choose **Save Changes** (or **Save as Draft** on a new PO). The PO and all planned costs save together. A failed save must leave the form available for correction.
8. Reopen the PO. Its planned-cost list identifies **Whole PO** or the exact target item line. Editing, reordering or using the same catalogue item on two rows must not move a charge to another row.
9. At receipt, copying the plan to the GRN keeps line charges on the matching PO line only. Partial receipts prorate line estimates by received/accepted versus ordered quantity. A charge for a line absent from the receipt is excluded.

Planned landed costs are estimates, not supplier prices. They do not increase the supplier's PO total. Existing receipt/landed-cost approval and posting controls remain in force; Finance posting rules are unchanged.

Removing a PO row uses the ERP confirmation dialog and explicitly removes its line-specific estimates from the form. PO-wide estimates remain. Saving an empty cost list clears estimates; omitting the plan in an API request preserves existing estimates.

## Deployment and acceptance

- Schema migration: `20260908220000_ScopePlannedLandedCostsToPurchaseOrderLines`.
- Nullable `PurchaseOrderItemId` is added to `PurchaseOrderLandedCostPlanItems` and `LandedCostItems`. Existing costs remain PO-wide.
- Backend and frontend both need deployment; source edits alone do not update the running production frontend.
- Required acceptance: mixed shared/line estimates, reload, item edit, removal and cancellation, failed validation, unchanged commercial total, and receipt isolation.
- Do not claim live UAT acceptance until these checks have been executed against the updated runtime.

## Validation recorded on 8 September 2026

- 31 frontend tests pass: line-dialog save/cancel/validation, payload scope, existing source/UOM and ad-hoc line regression tests.
- 19 Core tests pass: shared and targeted costs, foreign/deleted line rejection, empty replacement, validation before mutation, allocation isolation, partial receipts and exclusion of unreceived lines.
- 8 API/SQL-provider tests pass: stable line identity, PO rollback on invalid planned costs, retry after a transient failure, source guards, and migration/FK/downgrade checks. SQL tests used disposable databases, not the customer's PO records.
- Focused TypeScript check passes: `npx tsc -p tsconfig.po-landed-costs.json --pretty false`.
- Backend build passed. The full build reported existing repository warnings; a subsequent focused API compile is used after API-only cleanup.
- Runtime switching was not performed during migration application. No planned charges were added to PO-2026-0003. Updated-runtime browser save/reload acceptance remains pending.

## Local UAT migration applied on 8 September 2026

- Applied only `20260908220000_ScopePlannedLandedCostsToPurchaseOrderLines` to the verified local `RHEMA-MICHAEL\SQL2017` / `RhemaERP` database using `scripts/procurement/Apply-LocalUatPurchaseOrderLandedCostScope.ps1`.
- The scoped script applies the same additive operations as the EF migration in one transaction and records its migration-history entry. No unrelated migrations or business-record changes are executed.
- Verified both nullable `uniqueidentifier` columns, both indexes, and both enabled/trusted foreign keys to `PurchaseOrderItems.Id` with no cascading deletes. A second invocation verified the already-applied state without changes.
- Before/after counts are unchanged: 7 PO items, 0 planned-cost items, 0 actual landed-cost items.
- The database prerequisite is complete. A successful updated frontend build and updated backend runtime are still required before browser acceptance; this schema check is not an end-to-end UI test.
