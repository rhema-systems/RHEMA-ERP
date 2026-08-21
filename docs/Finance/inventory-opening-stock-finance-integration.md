# Inventory Opening Stock and Finance Integration

## Review ownership

This change deliberately crosses the Inventory and Finance boundary and requires review by the Procurement/Inventory owner before merge.

- Inventory owns warehouses, locations, items, quantities, unit costs, tracking data, source schedules, adjustment lifecycle, approval evidence, and inventory database invariants.
- Finance consumes only an approved, immutable `INITIAL_STOCK` adjustment and derives the configured Inventory Control and Migration Clearing accounts.
- Finance must not create or repair Inventory masters, change quantities or costs, or bypass Inventory approval controls.
- Ordinary stock adjustments retain their established expense/recovery posting route. The opening-stock route is reserved for the dedicated endpoint and cannot be selected through ordinary create or update operations.

## Governed contract

Opening stock is created through `POST /api/inventory/adjustments/opening-stock`. The server, rather than the browser, forces `INITIAL_STOCK` and requires:

- one tenant-owned warehouse and exact tenant-owned location per line;
- active tenant-owned stock items;
- positive explicit quantity and unit cost from the approved cutover schedule;
- one explicit accounting book and opening date;
- a nonblank source-schedule reference;
- deterministic idempotency and payload-integrity evidence;
- independent approval before Finance may post the adjustment.

The schema adds `StockAdjustments.BookClassification` because the accounting book is part of the immutable source evidence. The migration also hardens the existing Inventory SQL triggers so opening-stock schedule costs are accepted only for controlled `INITIAL_STOCK` creation, while ordinary adjustment costing and lifecycle rules remain unchanged.

## Accounting result

For an approved positive opening adjustment, Finance posts:

- Debit: configured Inventory Control account;
- Credit: configured Migration Clearing account.

The Finance adapter rejects missing approval identity/date, missing payload or integrity hashes, missing Finance mappings, and any zero-value line. It creates no Inventory master or operational record.

## Supplier-return boundary

Supplier returns are a separate lifecycle. This PR does not transfer ownership of receipt, inspection, return authorization, warehouse movement, or supplier-return evidence to Finance. Finance integrations must consume canonical accepted Inventory/Procurement evidence and must not duplicate or mutate that lifecycle. Any supplier-return posting or settlement change should be reviewed in its own PR with the Procurement/Inventory owner.

## Deliberate exclusions

- No free-form Finance journal may substitute for item/location/quantity/cost evidence.
- No Inventory master is seeded by the opening-stock endpoint.
- No ordinary adjustment is reclassified as `INITIAL_STOCK`.
- No foreign-currency Inventory opening is asserted by this contract; Finance posts in the configured functional currency.
- The FINANCE-DEMO fixture and global Procurement identity-catalog deployment are operational UAT tooling and should be reviewed separately from this production contract.

## Reviewer checklist

- Confirm Inventory terminology, endpoint placement, DTOs, entity field, workflow expectations, and trigger invariants.
- Confirm the ordinary adjustment create/update/post and reversal paths are unchanged.
- Confirm maker/checker controls and exact warehouse/location access remain enforced.
- Confirm repeated requests converge on one logical opening adjustment and conflicting payloads fail closed.
- Confirm Finance receives only approved immutable evidence and posts Inventory Control against Migration Clearing.
- Confirm the migration upgrade and downgrade scripts preserve the pre-existing ordinary Inventory trigger contract.
