# Warehouse-Based Inventory Implementation Summary

## ✅ Completed

### 1. Database Layer
- ✅ Created `WarehouseQuantity` entity with warehouse-level stock tracking
- ✅ Applied migration `CreateWarehouseQuantitiesTable`
- ✅ Seeded data: 20 inventory items (8 consumables, 12 tools) in Main Warehouse
- ✅ Created `IWarehouseQuantityRepository` and `WarehouseQuantityRepository` 
- ✅ Registered repository in DI container

### 2. API Endpoints
- ✅ `GET /api/InventoryItems/warehouses` - Get all active warehouses
- ✅ `GET /api/InventoryItems/by-warehouse/{warehouseId}?itemType=1` - Get consumables by warehouse
- ✅ `GET /api/InventoryItems/by-warehouse/{warehouseId}?itemType=4` - Get tools by warehouse

### 3. DTOs Updated
- ✅ Added `WarehouseId` (required) to `CreateWorkOrderPartDto`
- ✅ Added `WarehouseId` and `WarehouseName` to `WorkOrderPartDto`

### 4. Service Layer - Partial
- ✅ Refactored `WorkOrderPartService` constructor to use `IUnitOfWork`
- ✅ Updated `AddPartAsync` to use Unit of Work pattern with transactions
- ✅ Updated `AddPartAsync` to check warehouse-level stock via `WarehouseQuantityRepository`
- ✅ Updated `AddPartAsync` to allocate from warehouse quantities

## ⚠️ CRITICAL ISSUE - REMAINING WORK

### WorkOrderPartService - Incomplete Methods

The following methods in `WorkOrderPartService.cs` **still reference old repositories** that were removed and will cause **compilation errors**:

```csharp
// Lines 148-177: UpdatePartAsync
// Lines 180-194: DeletePartAsync  
// Lines 196-200: GetPartsByWorkOrderAsync
// Lines 202-233: UpdatePartStatusAsync
// Lines 235-239: GetTotalPartsCostAsync
// Lines 248-290: UpdateInventoryConsumptionAsync - References _allocationRepository, _inventoryItemRepository
// Lines 292-326: ReleaseAllocationAsync - References _allocationRepository, _inventoryItemRepository
```

### How to Fix

Replace all repository references with Unit of Work pattern:

```csharp
// OLD (will not compile):
var part = await _partRepository.GetByIdAsync(id);
await _allocationRepository.UpdateAsync(allocation);
await _inventoryItemRepository.UpdateAsync(inventoryItem);

// NEW (correct):
var partRepo = _unitOfWork.Repository<WorkOrderPart>();
var part = await partRepo.GetByIdAsync(id);

var allocationRepo = _unitOfWork.Repository<InventoryAllocation>();
await allocationRepo.UpdateAsync(allocation);

// For consumption/release, update WarehouseQuantity instead of InventoryItem:
var warehouseQuantity = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(
    part.WarehouseId, part.InventoryItemId);
warehouseQuantity.CurrentStock -= quantityConsumed;
warehouseQuantity.AllocatedStock -= quantityConsumed;
await _warehouseQuantityRepository.UpdateAsync(warehouseQuantity);
```

###  WorkOrderPart Entity Missing WarehouseId

The `WorkOrderPart` entity needs a `WarehouseId` field added:

**File**: `src/ErpSystem.Core/Entities/Maintenance/MaintenanceEntities.cs`

Add to WorkOrderPart class:
```csharp
public Guid WarehouseId { get; set; }
public virtual Warehouse? Warehouse { get; set; }
```

Then create migration:
```bash
dotnet ef migrations add AddWarehouseIdToWorkOrderPart
dotnet ef database update
```

## 🎯 Frontend Updates Needed

### Step 1: Update workOrderPartService.ts

**File**: `frontend/src/services/workOrderPartService.ts`

Add new methods:
```typescript
// Add to interface
export interface WarehouseDto {
  id: string;
  code: string;
  name: string;
  warehouseType: string;
  isActive: boolean;
}

export interface WarehouseInventoryDto {
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  itemType: number;
  description?: string;
  unitOfMeasure?: string;
  currentStock: number;
  availableStock: number;
  allocatedStock: number;
  unitCost: number;
  categoryName?: string;
}

// Add these methods to the service:
async getWarehouses(): Promise<WarehouseDto[]> {
  const response = await api.get('/api/InventoryItems/warehouses');
  return response.data;
}

async getInventoryByWarehouse(
  warehouseId: string, 
  itemType: number
): Promise<WarehouseInventoryDto[]> {
  const response = await api.get(
    `/api/InventoryItems/by-warehouse/${warehouseId}?itemType=${itemType}`
  );
  return response.data;
}

// Update CreateWorkOrderPartDto to include warehouseId
export interface CreateWorkOrderPartDto {
  workOrderId: string;
  inventoryItemId: string;
  warehouseId: string;  // NEW - REQUIRED
  quantityRequired: number;
  unitCost: number;
  warehouseLocationId?: string;
  serialNumber?: string;
  lotNumber?: string;
  notes?: string;
}
```

### Step 2: Update Work Orders Page - Consumables Tab

**File**: `frontend/src/app/maintenance/work-orders/page.tsx`

**Find the Consumables tab section** (around line 1900-2100) and update:

```typescript
// Add state variables at the top:
const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
const [selectedWarehouse, setSelectedWarehouse] = useState<string>('');
const [warehouseInventory, setWarehouseInventory] = useState<WarehouseInventoryDto[]>([]);

// Load warehouses on mount:
useEffect(() => {
  const loadWarehouses = async () => {
    try {
      const data = await workOrderPartService.getWarehouses();
      setWarehouses(data);
      if (data.length > 0) {
        setSelectedWarehouse(data[0].id);
      }
    } catch (error) {
      console.error('Error loading warehouses:', error);
    }
  };
  loadWarehouses();
}, []);

// Load inventory when warehouse selected:
useEffect(() => {
  const loadWarehouseInventory = async () => {
    if (!selectedWarehouse) return;
    
    try {
      const data = await workOrderPartService.getInventoryByWarehouse(
        selectedWarehouse,
        1 // ItemType.Consumable
      );
      setWarehouseInventory(data);
    } catch (error) {
      console.error('Error loading warehouse inventory:', error);
    }
  };
  loadWarehouseInventory();
}, [selectedWarehouse]);

// REPLACE the warehouse location dropdown with warehouse dropdown:
<div className="space-y-2">
  <Label htmlFor="warehouse">Warehouse *</Label>
  <Select
    value={selectedWarehouse}
    onValueChange={(value) => setSelectedWarehouse(value)}
  >
    <SelectTrigger>
      <SelectValue placeholder="Select warehouse" />
    </SelectTrigger>
    <SelectContent>
      {warehouses.map((wh) => (
        <SelectItem key={wh.id} value={wh.id}>
          {wh.name} ({wh.code})
        </SelectItem>
      ))}
    </SelectContent>
  </Select>
</div>

// REPLACE the consumables dropdown to use warehouseInventory:
<div className="space-y-2">
  <Label htmlFor="consumable">Consumable Item *</Label>
  <Select
    value={currentPart.inventoryItemId}
    onValueChange={(value) => {
      const item = warehouseInventory.find(i => i.inventoryItemId === value);
      setCurrentPart({
        ...currentPart,
        inventoryItemId: value,
        itemCode: item?.itemCode || '',
        itemName: item?.itemName || '',
        unitCost: item?.unitCost || 0,
        warehouseId: selectedWarehouse, // ADD THIS
      });
    }}
  >
    <SelectTrigger>
      <SelectValue placeholder="Select item" />
    </SelectTrigger>
    <SelectContent>
      {warehouseInventory.map((item) => (
        <SelectItem key={item.inventoryItemId} value={item.inventoryItemId}>
          {item.itemCode} - {item.itemName} 
          <span className="text-sm text-gray-500 ml-2">
            (Available: {item.availableStock} {item.unitOfMeasure})
          </span>
        </SelectItem>
      ))}
    </SelectContent>
  </Select>
</div>

// UPDATE the savePart function to include warehouseId:
const savePart = async () => {
  if (!selectedWarehouse) {
    toast.error('Please select a warehouse');
    return;
  }
  
  const partData: CreateWorkOrderPartDto = {
    workOrderId: workOrder.id,
    inventoryItemId: currentPart.inventoryItemId,
    warehouseId: selectedWarehouse, // ADD THIS
    quantityRequired: currentPart.quantityRequired,
    unitCost: currentPart.unitCost,
    notes: currentPart.notes,
  };
  
  await workOrderPartService.create(partData);
  // ... rest of save logic
};
```

### Step 3: Update Tools Tab

Similar changes for tools tab (around line 2200-2400):

```typescript
// Same warehouse dropdown as above

// Load tools inventory when warehouse selected:
useEffect(() => {
  const loadWarehouseTools = async () => {
    if (!selectedWarehouse) return;
    
    try {
      const data = await workOrderPartService.getInventoryByWarehouse(
        selectedWarehouse,
        4 // ItemType.Tool
      );
      setWarehouseTools(data);
    } catch (error) {
      console.error('Error loading warehouse tools:', error);
    }
  };
  loadWarehouseTools();
}, [selectedWarehouse]);

// UPDATE tools list to show available stock:
{warehouseTools.map((tool) => (
  <div key={tool.inventoryItemId} className="flex items-center space-x-3">
    <Checkbox
      id={`tool-${tool.inventoryItemId}`}
      checked={selectedTools.includes(tool.inventoryItemId)}
      onCheckedChange={(checked) => {
        if (checked) {
          setSelectedTools([...selectedTools, tool.inventoryItemId]);
        } else {
          setSelectedTools(selectedTools.filter(id => id !== tool.inventoryItemId));
        }
      }}
    />
    <Label htmlFor={`tool-${tool.inventoryItemId}`}>
      {tool.itemCode} - {tool.itemName}
      <span className="text-sm text-gray-500 ml-2">
        (Available: {tool.availableStock})
      </span>
    </Label>
  </div>
))}
```

## Testing Checklist

- [ ] Build backend: `dotnet build`
- [ ] Fix remaining `WorkOrderPartService` methods
- [ ] Add `WarehouseId` to `WorkOrderPart` entity + migration
- [ ] Test API endpoints with Postman/Swagger
- [ ] Build frontend: `npm run build`
- [ ] Test warehouse dropdown loads
- [ ] Test consumables load by warehouse
- [ ] Test tools load by warehouse
- [ ] Test part allocation with warehouseId
- [ ] Verify stock quantities update in WarehouseQuantities table

## Database Queries for Verification

```sql
-- Check warehouse quantities
SELECT i.ItemCode, i.Name, w.Name AS Warehouse, 
       wq.CurrentStock, wq.AvailableStock, wq.AllocatedStock
FROM WarehouseQuantities wq
JOIN InventoryItems i ON wq.InventoryItemId = i.Id
JOIN Warehouses w ON wq.WarehouseId = w.Id
WHERE i.ItemType IN (1, 4)
ORDER BY i.ItemType, i.ItemCode;

-- Check work order parts with warehouse
SELECT wo.WorkOrderNumber, i.ItemCode, i.Name, 
       wop.QuantityRequired, wop.QuantityUsed, wop.Status
FROM WorkOrderParts wop
JOIN WorkOrders wo ON wop.WorkOrderId = wo.Id
JOIN InventoryItems i ON wop.InventoryItemId = i.Id
ORDER BY wo.CreatedAt DESC;
```
