# Site Selection for Purchase Order Line Items

## Overview
Implemented site (warehouse) location selection for each purchase order line item. Users must now select a site when adding items to a purchase order, and the site is displayed in the items list grid.

## Changes Made

### 1. Database Changes

#### Entity Updates
**File:** [`src/ErpSystem.Core/Entities/Procurement/ProcurementEntities.cs`](src/ErpSystem.Core/Entities/Procurement/ProcurementEntities.cs:357-414)
- Added `WarehouseId` property to `PurchaseOrderItem` entity (nullable Guid)
- Added `Warehouse` navigation property

#### Migrations
**Files:**
- [`src/ErpSystem.Data/Migrations/20260128_AddWarehouseIdToPurchaseOrderItems.cs`](src/ErpSystem.Data/Migrations/20260128_AddWarehouseIdToPurchaseOrderItems.cs) - EF Core migration
- [`database/migrations/Add_WarehouseId_To_PurchaseOrderItems.sql`](database/migrations/Add_WarehouseId_To_PurchaseOrderItems.sql) - SQL migration script

**Migration Details:**
- Adds `WarehouseId` column (nullable uniqueidentifier)
- Creates index `IX_PurchaseOrderItems_WarehouseId`
- Adds foreign key constraint to `Warehouses` table with `ON DELETE NO ACTION`

### 2. Backend Changes

#### DTOs
**File:** [`src/ErpSystem.Core/DTOs/Procurement/ProcurementDTOs.cs`](src/ErpSystem.Core/DTOs/Procurement/ProcurementDTOs.cs)

**PurchaseOrderItemDto** (lines 226-250):
- Added `WarehouseId` property (nullable Guid)
- Added `WarehouseName` property (nullable string)

**CreatePurchaseOrderItemDto** (lines 283-306):
- Added `WarehouseId` property (nullable Guid)

#### Repository
**File:** [`src/ErpSystem.Data/Repositories/Procurement/PurchaseOrderRepositories.cs`](src/ErpSystem.Data/Repositories/Procurement/PurchaseOrderRepositories.cs:318-334)
- Updated `GetPurchaseOrderByIdAsync` to include `Warehouse` navigation property:
  ```csharp
  .Include(po => po.Items)
      .ThenInclude(i => i.Warehouse)
  ```

#### Controller
**File:** [`src/ErpSystem.Api/Controllers/Procurement/PurchaseOrdersController.cs`](src/ErpSystem.Api/Controllers/Procurement/PurchaseOrdersController.cs)

**GetPurchaseOrder** (lines 175-193):
- Maps `WarehouseId` and `WarehouseName` when returning items

**CreatePurchaseOrder** (lines 297-318):
- Sets `WarehouseId` when creating new purchase order items

**UpdatePurchaseOrder** (lines 397-418):
- Sets `WarehouseId` when updating purchase order items

**GetPurchaseOrderDetailDto** (lines 877-895):
- Maps `WarehouseId` and `WarehouseName` when returning items

### 3. Frontend Changes

#### Service Interface
**File:** [`frontend/src/services/purchasingService.ts`](frontend/src/services/purchasingService.ts)

**PurchaseOrderItemDto** (lines 130-147):
- Added `warehouseId?: string`
- Added `warehouseName?: string`

**CreatePurchaseOrderItemDto** (lines 165-176):
- Added `warehouseId?: string`

#### New Purchase Order Page
**File:** [`frontend/src/app/procurement/purchase-orders/new/page.tsx`](frontend/src/app/procurement/purchase-orders/new/page.tsx)

**POItemFormData Interface** (lines 62-68):
- Added `warehouseId?: string`
- Added `warehouseName?: string`

**Items List Grid** (lines 967-1031):
- Added "Warehouse" column header (line 974)
- Added warehouse cell displaying `item.warehouseName || '-'` (line 1001)

**Item Dialog** (lines 1279-1314):
- Added warehouse dropdown (required field)
- Dropdown displays: `{warehouse.code} - {warehouse.name}`
- Stores both `warehouseId` and `warehouseName` on selection

**Validation** (lines 485-518):
- Added warehouse validation in `handleSaveItem`:
  ```typescript
  if (!itemFormData.warehouseId) {
    toast.error('Please select a warehouse');
    return;
  }
  ```

**Data Submission** (lines 563-574, 624-636):
- Includes `warehouseId` when creating/submitting purchase orders

#### Edit Purchase Order Page
**File:** [`frontend/src/app/procurement/purchase-orders/[id]/edit/page.tsx`](frontend/src/app/procurement/purchase-orders/[id]/edit/page.tsx)

**Import Statement** (line 53):
- Added `WarehouseDto` to imports

**POItemFormData Interface** (lines 57-63):
- Added `warehouseId?: string`
- Added `warehouseName?: string`

**State Management** (line 96):
- Added `warehouses` state: `const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);`

**Data Loading** (lines 180-204):
- Added warehouse loading to `useEffect`:
  ```typescript
  const [itemsData, suppliersData, warehousesData] = await Promise.all([
    inventoryManagementService.getInventoryItems({ isActive: true }),
    businessPartnerService.getActivePartners(),
    inventoryManagementService.getWarehouses(true)
  ]);
  setWarehouses(warehousesData || []);
  ```

**Load Existing Items** (lines 152-166):
- Loads `warehouseId` and `warehouseName` from existing items

**Items List Grid** (lines 678-734):
- Added "Warehouse" column header (line 683)
- Added warehouse cell displaying `item.warehouseName || '-'` (line 704)

**Item Dialog** (lines 957-993):
- Added warehouse dropdown (required field)
- Same implementation as new page

**Validation** (lines 325-351):
- Added warehouse validation in `handleSaveItem`

**Data Submission** (lines 409-420):
- Includes `warehouseId` when updating purchase orders

## Usage

### Creating a New Purchase Order
1. Select a supplier
2. Click "Add Item"
3. Search and select an inventory item
4. **Select a warehouse** (required)
5. Enter quantity, price, and other details
6. Click "Add Item" to add to grid
7. Warehouse name will be displayed in the "Warehouse" column

### Editing an Existing Purchase Order
1. Open a draft purchase order
2. Click "Edit" on an item or "Add Item"
3. **Select a warehouse** (required)
4. Update other details as needed
5. Save changes

### Validation
- Warehouse selection is **mandatory** for all purchase order line items
- Error message displayed if user tries to save without selecting a warehouse
- Warehouse dropdown shows all active warehouses in format: `{code} - {name}`

## Database Migration

To apply the database changes, run one of the following:

### Option 1: EF Core Migration
```bash
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api
```

### Option 2: SQL Script
Execute the SQL script:
```bash
sqlcmd -S your_server -d your_database -i database/migrations/Add_WarehouseId_To_PurchaseOrderItems.sql
```

## Technical Notes

- `WarehouseId` is nullable to maintain backward compatibility with existing purchase orders
- Foreign key constraint uses `ON DELETE NO ACTION` to prevent accidental warehouse deletion
- Warehouse name is loaded via navigation property for display purposes
- Frontend stores both `warehouseId` and `warehouseName` for efficient display without additional API calls
- Warehouse dropdown follows the same pattern as the maintenance work order parts tab

## Testing Checklist

- [ ] Create new purchase order with warehouse selection
- [ ] Verify warehouse appears in items list grid
- [ ] Edit existing purchase order and add item with warehouse
- [ ] Verify validation prevents saving without warehouse
- [ ] Verify warehouse name displays correctly in grid
- [ ] Test with multiple warehouses
- [ ] Verify existing purchase orders still load correctly (with null warehouse)
