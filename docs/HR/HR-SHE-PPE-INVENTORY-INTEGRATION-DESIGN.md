# SHE PPE ↔ Inventory (Stores) — integration touch point

> **Status: PROPOSED 2026-09-06 — awaiting two decisions (§11). Nothing below is built.**
> Supersedes the one-paragraph note at `HR-SHE-INTEGRATION-AND-BOUNDARIES.md` §3.6 once decided;
> that section will be rewritten to point here. Integration-map rows 14 and 15 are the same finding.

## 1. What the requirement says

| Source | Line | Obligation |
|---|---|---|
| `Safety Spec Docs\frd-v03.txt` ch. 9 interface table | 3969–3971 | **Stores & Inventory** ↔ SHE: "PPE stock levels, issues and reorder alerts (FR-SHE-241)" |
| same, FR-SHE-241 | 2798 | "manage PPE inventory: stock levels, issue records, expiry dates and automated reorder alerts" |
| same, FR-SHE-242 | 2806 | employee PPE record: employee, department, PPE type, issue date, expiry date, inspection status |
| SRS SHE §18 | 95–97 | Inventory ↔ SHE: "PPE stock and issues" |
| `docs/TDC_INVENTORY_STORES_ARCHITECTURE_REQUIREMENTS.md` | — | **silent** on SHE, PPE, HR and employees; no FR-INV ids exist |

So the obligation is asserted from the SHE side only. The gap analysis marked FR-SHE-241 "Built"
against SHE's own `PpeInventory` table, which is how a second stock system came to exist.

## 2. What exists today

**SHE** (`StaffSafetyEntities.cs` §F, `SafetyPermitPpeEquipmentServices.cs:370–700`,
`PpeManagementController.cs`, route `api/safety/ppe`):

- `PpeType` — the catalogue (code, category, standard, lifespan, serial/expiry flags). **Policy. Stays.**
- `JobRolePpeRequirement` — who must hold what, replacement cycle. **Policy. Stays.** Feeds the KPI
  `PpeComplianceRate` (`SheKpiComputationService.cs:349–397`).
- `PpeInventory` — type × brand × model × size with `QuantityInStock`, `ReorderLevel`, free-text
  `Supplier`, `UnitCost`, `StorageLocation`. **A parallel stock ledger. This is what goes.**
- `PpeIssuance` — employee, type, qty, size, serial, expiry, return. **The FR-SHE-242 record. Stays,
  gains a stock reference.** Today it links `PpeTypeId` only, and `IssueAsync` never decrements
  anything (`:613–626`). Stock only ever goes up (`RestockAsync :564`).
- Other readers of `PpeInventory`: dashboard `PpeBelowReorder` (`SheDashboardService.cs:104`),
  the weekly `PpeStockLow` reminder (`SheReminderService.cs:717–739`), the seeder
  (`SheDataSeeder.cs:698–750`), harness slices 1/6/13, and three screens.
- HR's `JobPpeRequirement` (job description PPE grid) links `PpeType` — unaffected.

**Inventory** (owned by Michael Marmah's team; last touched 2026-08-30):

- Item master `InventoryItem` (`Entities/Inventory/InventoryEntities.cs:15–173`): code, name,
  category, UoM, `ItemType {StockItem, Service, NonStock, FixedAsset}`, six tracking bools,
  `CurrentStock/AvailableStock/AllocatedStock`, `ReorderLevel`, `AverageCost`, brand, manufacturer.
  **No PPE/uniform/tool attribute anywhere in the module.**
- Three stock stores: item roll-up, `WarehouseQuantity` (item × warehouse), `InventoryLocation`
  (item × bin). Two movement ledgers (`StockMovement` operational, `InventoryMovement` valuation).
- **Two stock-out paths:**
  1. *Governed*: `IInventoryRequisitionService` create → submit → workflow approve → `IssueAsync`.
     Requires an approved requisition with department + (project | cost centre), a `RowVersion`,
     an idempotency key, a `ReceiverUserId` that is an **active internal user account**, and
     four-way segregation: requester ≠ approver, issuer ∉ {requester, approver, receiver}
     (`InventoryRequisitionService.cs:517–537`). Writes voucher, movement, finance posting.
  2. *Lightweight*: `IInventoryManagementService.AllocateForWorkOrderAsync` +
     `ConsumeAllocatedInventoryAsync` (`InventoryManagementService.cs:189–390`). Picks the best
     **picking bin** for the item, checks availability, decrements item + bin, writes a
     `StockMovement` ("Allocation" then "Consumption"). Joins an ambient transaction when one is
     open (`!_unitOfWork.HasActiveTransaction`). **This is what Maintenance uses**
     (`MaintenanceInventoryService.cs:131, 236`) behind its own facade `IMaintenanceInventoryService`.
- No path issues a consumable to an **employee**. The only person-level recipient is the voucher's
  `ReceiverUserId` (a user, not an employee); employee identity appears only on fixed-asset issues.
- No seeder creates a warehouse for a fresh tenant except one demo warehouse + one item with no
  bin rows (`DatabaseSeedingService.cs:3158–3197`). The Inventory UAT script assumes the tenant
  set these up by hand.

## 3. The touch point — decision

**Ownership**

| Concern | Owner | Where it lives |
|---|---|---|
| What a PPE item *is* (code, name, brand, UoM, cost, supplier, tracking) | **Inventory** | `InventoryItem` |
| How many are on the shelf, where, at what value; receipts, transfers, counts, reorder level | **Inventory** | item / bin / warehouse tables, `StockMovement` |
| Which stock items count as a given kind of PPE | SHE | `PpeStockItem` (new, replaces `PpeInventory`) — a link row, no quantities |
| PPE standards, lifespan, serial/expiry rules | SHE | `PpeType` (unchanged) |
| Who must hold what, replacement cycle | SHE | `JobRolePpeRequirement`, HR `JobPpeRequirement` (unchanged) |
| Who holds what, since when, condition, expiry, return | SHE | `PpeIssuance` (+ stock reference) |
| The stock-out when SHE hands an item to an employee | **Inventory, triggered by SHE** | consumption movement referencing the issuance number |
| Reorder alert | Inventory's numbers, SHE's notification | `PpeStockLow` sweep reads Inventory's reorder state |

**The seam**: one SHE-owned facade, `ISheInventoryBridge` (Core/Interfaces/HR, registered beside
the SHE services), wrapping `IInventoryManagementService` + `IInventoryItemRepository` +
`IStockMovementRepository`. Nothing in SHE references an Inventory type outside that facade and
the `PpeStockItem.InventoryItem` navigation. When Inventory ships a proper employee-issue API
(§9), only the facade changes.

**Why the lightweight path, not the governed requisition:** a per-employee PPE hand-out cannot
meet the governed path's shape. It needs three distinct user accounts per issuance plus a fourth
for the receiver; the receiver must have a login (most field staff, the people who need PPE, do
not); each issuance needs its own workflow approval and a cost object. That path is right for
Stores replenishing a store, not for a safety officer handing out twenty helmets at an induction.
The lightweight path is the one the platform already blesses for a sibling module's consumption.

**Why not keep a SHE float** (Stores issues in bulk to SHE, SHE re-counts): it keeps two ledgers,
which is the defect being closed. If TDC physically keeps a PPE cupboard, it is a **warehouse or
bin in Inventory**, fed by Inventory transfers; SHE consumes from it and never counts.

## 4. Data model

**`PpeStockItem`** (new table; `PpeInventory` dropped — its only rows are seed data)

| Column | Notes |
|---|---|
| `PpeTypeId` FK | the kind |
| `InventoryItemId` FK → `InventoryItem` | real FK + nav, the `CompanyAsset.FixedAssetId` pattern the integration map calls "done right" |
| `Size` (20) | display/selection hint when one item = one size; else read the item name |
| `IsActive` | retire a link without deleting history |
| unique (`TenantId`, `InventoryItemId`) | one PPE kind per stock item |

No quantity, cost, supplier, brand, model, storage or restock columns. The DTO projects them from
the item at read time: `itemCode`, `itemName`, `brand`, `unitOfMeasure`, `currentStock`,
`availableStock`, `reorderLevel`, `isBelowReorderLevel` (= Inventory's `CurrentStock <= ReorderLevel`),
`averageCost`, `warehouses[] {name, onHand}` from `WarehouseQuantity`.

**`PpeIssuance`** gains

| Column | Notes |
|---|---|
| `IssuanceNumber` (30) | `PPE-YYYY-D4`, tenant-unique; the SHE numbering idiom (numeric max incl. soft-deleted). Becomes the `ReferenceNumber` on Inventory's movement rows — the trace from ledger to holder |
| `PpeStockItemId` FK? | which stock item was handed out (null on pre-migration rows) |
| `InventoryAllocationId` Guid? | the handle Inventory returned; bare Guid like `CompanyAsset.MaintenanceAssetId` |
| `InventoryItemId` Guid? | denormalised so history survives a retired link |

`PpeTypeId` stays (the KPI and the requirement matrix are keyed on type).

## 5. Behaviour

- **Link** (`POST api/safety/ppe/stock-items`, `SheAdminPolicy`): pick a `PpeType` and an
  Inventory item. Refuses inactive items (`ItemStatus != Active`), `ItemType` Service/NonStock,
  and an item already linked (422). Reads use `SheReadPolicy`.
- **On-hand** (`GET stock-items`, `GET stock-items/by-type/{id}`, `GET stock-items/below-reorder`):
  read-through, never cached in SHE.
- **Issue** (`POST issuances`, `SheWritePolicy`), inside one SHE-opened transaction:
  1. guard employee, issuer, stock item (active, tenant), quantity ≥ 1;
  2. mint `IssuanceNumber`;
  3. `bridge.ConsumeAsync(inventoryItemId, qty, referenceNumber, referenceId: issuance.Id, userId)`
     = allocate then consume, both joining the open transaction;
  4. save the issuance with the allocation id; commit.
  Failure modes, all 422 with the message: no active picking bin holds the item; insufficient
  available stock (Inventory's own message, quoted); item retired. A failure rolls back the whole
  thing — no issuance without a movement, no movement without an issuance.
- **Return**: unchanged SHE record (condition, returned-to). **Does not restock** in v1: used PPE
  is not re-issued, and Inventory has no "return a consumed quantity" operation on this path
  (`ReleaseAllocationAsync` only releases *unconsumed* allocation). Serialised items in good
  condition are the case to revisit when Inventory offers a return-to-stock seam (§9).
- **Restock endpoint and card: removed.** Stock arrives through Inventory receipts and transfers;
  the SHE stock screen deep-links to the item in Inventory.
- **Reorder alert**: `SweepPpeAsync` iterates active `PpeStockItem`s and fires the existing weekly
  `PpeStockLow` notification when the linked item is at or below **Inventory's** reorder level.
  Same topic, same dedupe key shape, same audience (Safety Officer / SHE Manager). Inventory's own
  replenishment alerts continue to reach Stores; the two audiences differ, so both stay.
- **Dashboard** `PpeBelowReorder` and **KPI** `PpeComplianceRate`: same numbers, new source /
  unchanged respectively.
- **`issuances/mine`**: unchanged, gains item code and name.

## 6. Frontend

- `hr/safety/ppe/page.tsx` → "PPE stock (from Stores)": link register with live on-hand, reorder
  badge from Inventory, per-warehouse breakdown, link dialog, deep link to the Inventory item.
  RestockCard deleted.
- New reusable `InventoryItemPicker` (`components/inventory/`) calling
  `GET /InventoryItems/search?searchTerm=` server-side. No such component exists today; every
  existing picker pulls the whole catalogue and filters in the browser.
- Issuance dialog: PPE type → stock item (shows size/brand/available), quantity capped at
  available, 422 text surfaced verbatim.
- `me/safety/ppe`: shows item code and issuance number.
- `types/hr/safety-ppe.ts`, `services/hr/safety-ppe.service.ts`: `PpeInventory*` → `PpeStockItem*`,
  restock removed, issuance gains the new fields.

## 7. Seeding and harness

- `SheDataSeeder.SeedPpeAsync` becomes: ensure category `PPE`, warehouse `CENTRAL-STORE` with one
  picking location, five `InventoryItem`s with `InventoryLocation` + `WarehouseQuantity` rows
  (guarded create-if-missing, tenant-scoped), then five `PpeStockItem` links. Two items seeded at or
  below reorder so the alert, dashboard and below-reorder read stay demonstrable. The demo-pack
  `verify-tables` gate gains `PpeStockItems` and loses `PpeInventories`.
- Harness: rewrite `run-slice6.mjs` §2–3; assert on-hand equals the Inventory item's
  `availableStock`; issue decrements item **and** bin quantity; a `StockMovement` row exists with
  `referenceNumber == issuanceNumber`; over-issue is 422 and leaves stock untouched; `restock` is
  404; `run-slice13.mjs` PpeStockLow assertion re-pointed; `run-slice1.mjs` dashboard count still
  non-zero. Run slices 1, 6, 13, 14 as regression.

## 8. Authorization, tenancy, transaction

- SHE issuers hold `HR.She.Write`; the in-process bridge bypasses Inventory's HTTP layer, and the
  lightweight path itself checks nothing. This is the intended model: safety officers are PPE
  issuers. Record it in `HR-SHE-INTEGRATION-AND-BOUNDARIES.md` §2.
- Allocation and movement rows take `TenantId` from `ICurrentUserProvider`, which the SHE service
  already validates (`RequireCurrentTenant`). No stamping gap.
- `IUnitOfWork` is scoped and shared; the SHE service opens the transaction, both Inventory calls
  join it (`ownsTransaction == false`), the SHE commit closes it.

## 9. Known limitations and asks for the Inventory team (record in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`)

1. The allocate/consume path updates the item roll-up and the bin but **not `WarehouseQuantity`
   or `InventoryBalance`**, and writes no valuation `InventoryMovement`. Per-warehouse reports and
   valuation will not see PPE consumption. Maintenance has the same exposure today.
2. It hard-codes `ReferenceType.WO` and `AllocationType = "WorkOrder"`; PPE movements will be
   labelled as work-order consumption in the ledger. Ask: honour `AllocateInventoryDto.ReferenceType`
   and add `ReferenceType.PpeIssuance` (or a generic `Custody`).
3. No employee-recipient consumable issue exists. Ask: a single-actor "issue to employee" that
   writes a voucher with `ReceiverEmployeeId`, under a movement reason `PPE_ISSUE`, without a
   requisition. The bridge would switch to it.
4. No return-to-stock of a consumed quantity on this path.
5. No PPE attribute on `InventoryItem` / `InventoryCategory`. We link by row instead of by
   attribute, so nothing is needed, but a `PPE` category convention would make the picker cleaner.
6. Finance: PPE consumption cost is now carried on Inventory's movement (`AverageCost`), so the
   HR↔Finance backlog row 61 (`PpeInventory.UnitCost`) closes; expensing the consumption is
   Inventory's posting concern, not HR's.

## 10. The HR side

- `JobPpeRequirement` (job descriptions) and `JobRolePpeRequirement` stay on `PpeType`. No change.
- Induction PPE is a normal SHE issuance. No change.
- **Separation clearance** (`SeparationClearanceTemplate.SourcesFromAssetRegister`): the same
  shape can expand one line per outstanding PPE issuance. Optional follow-on, half a slice.
- **HR Assets** (uniforms, phones, tools below the capitalisation threshold — area 16 decision D1):
  drawn from Stores through the *same facade* if TDC wants it, with `CompanyAsset.InventoryItemId`
  and an `AssetSource.Stores`. Integration-map rows 12 and 15. **Not in this change** unless
  decision 2 says so.

## 11. Open decisions

1. **Consumption model** — per-employee issuance consumes Inventory stock directly (recommended,
   §3), or SHE keeps a float fed by Stores vouchers. Everything above assumes the first.
2. **Scope of "parts of HR"** — SHE PPE only (recommended for this slice), or also HR Assets
   fulfilment from Stores (a second slice of similar size, on area 16's tables).

Assumptions taken without asking: `PpeInventory` is dropped, not kept in parallel; returns do not
restock in v1; the SHE seeder may create Inventory master data for the DEFAULT/UAT tenant when
none exists; `IssuanceNumber` format `PPE-YYYY-D4`.

## 12. Slices

| # | Work | Size |
|---|---|---|
| A | Entities, migration (user scaffolds; guarded SQL; no metadata listing — see `docs/LOCAL-FAST-EF-BUILD.md`), bridge, service, controller, DTOs, mappers, reminder + dashboard re-point | 1 day |
| B | Seeder + harness rewrite (slices 1/6/13 green) | ½ day |
| C | Frontend: picker, stock screen, issuance dialog, self-service, types/services | 1 day |
| D | Docs: boundaries §3.6 + §2, integration map rows 14/15, cross-module backlog §9 asks, finish plan row, memory | ¼ day |
