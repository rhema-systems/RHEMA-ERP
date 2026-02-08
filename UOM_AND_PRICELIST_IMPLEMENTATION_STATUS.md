# Unit of Measure and Price List Implementation Status

## Date: 2026-01-27

## Overview
This document tracks the implementation of Unit of Measure (UOM) Schedule and Item Price List integration into the Purchase Order (PO) and Purchase Requisition (PR) creation process.

## Completed Tasks

### 1. Backend Entity Updates ✅
- **File**: `src/ErpSystem.Core/Entities/Procurement/ProcurementEntities.cs`
- **Changes**:
  - Added `UnitOfMeasure` field (string, max 20 chars, default "EA") to `PurchaseOrderItem`
  - Added `ItemUnitOfMeasureId` (Guid?, nullable) to link to specific UOM from item's UOM schedule
  - Added `PriceListLineId` (Guid?, nullable) to track which price list line was used
  - Added navigation property `ItemUnitOfMeasure` to `PurchaseOrderItem`

### 2. Database Migration ✅
- **File**: `database/migrations/Add_UOM_And_PriceList_To_PurchaseOrderItems.sql`
- **Changes**:
  - Adds `UnitOfMeasure` column (NVARCHAR(20), NOT NULL, DEFAULT 'EA')
  - Adds `ItemUnitOfMeasureId` column (UNIQUEIDENTIFIER, NULL)
  - Adds `PriceListLineId` column (UNIQUEIDENTIFIER, NULL)
  - Creates foreign key constraints for both new ID columns
  - Creates indexes for performance
  - Updates existing records to use inventory item's UOM where available

### 3. DTO Updates ✅
- **File**: `src/ErpSystem.Core/DTOs/Procurement/ProcurementDTOs.cs`
- **Changes to `PurchaseOrderItemDto`**:
  - Added `UnitOfMeasure` (string, default "EA")
  - Added `ItemUnitOfMeasureId` (Guid?, nullable)
  - Added `PriceListLineId` (Guid?, nullable)
  - Added `PriceListName` (string, nullable)
  - Added `BaseUnitConversionFactor` (decimal?, nullable) for UOM conversion display
  - Added `BaseUnitOfMeasure` (string, nullable) for reference

- **Changes to `CreatePurchaseOrderItemDto`**:
  - Added `UnitOfMeasure` (string, required, default "EA")
  - Added `ItemUnitOfMeasureId` (Guid?, nullable)
  - Added `PriceListLineId` (Guid?, nullable)

### 4. Price List Lookup Service ✅
- **File**: `src/ErpSystem.Core/Services/Pricing/PriceListLookupService.cs`
- **Features**:
  - `GetSupplierPriceAsync()` - Gets the best price for an item from supplier price lists
  - `GetAllSupplierPricesAsync()` - Gets all available prices for an item from a supplier
  - `GetPriceHistoryAsync()` - Gets price history for an item from a supplier
  - `PriceListLookupResult` class - Contains price lookup results with base price, net price, UOM, quantity tiers, etc.
  - `PriceHistoryResult` class - Contains historical price data

## Pending Tasks

### 5. Service Layer Integration ⏳
**Priority: HIGH**
- **Files to Update**:
  - `src/ErpSystem.Core/Services/Procurement/PurchaseOrderService.cs`
  - `src/ErpSystem.Core/Services/Procurement/PurchaseRequisitionService.cs`

- **Required Changes**:
  - Inject `PriceListLookupService` and UOM repositories
  - When creating PO items, lookup price from price lists if available
  - Validate UOM against item's available UOMs
  - Calculate base unit quantities for inventory tracking
  - Store price list line reference for audit trail

### 6. API Controller Updates ⏳
**Priority: HIGH**
- **Files to Update**:
  - `src/ErpSystem.API/Controllers/PurchaseOrderController.cs`
  - `src/ErpSystem.API/Controllers/PurchaseRequisitionController.cs`

- **Required Changes**:
  - Add endpoint to get available UOMs for an item: `GET /api/inventory/items/{id}/units`
  - Add endpoint to get supplier prices: `GET /api/pricing/supplier/{supplierId}/item/{itemId}/price`
  - Update existing create/update endpoints to handle new UOM and price list fields

### 7. Frontend Service Updates ⏳
**Priority: HIGH**
- **Files to Update**:
  - `frontend/src/services/inventoryManagementService.ts`
  - `frontend/src/services/purchasingService.ts`

- **Required Changes**:
  - Add `getItemUnitsOfMeasure(itemId)` method
  - Add `getSupplierItemPrice(supplierId, itemId, quantity)` method
  - Update `CreatePurchaseOrderItemDto` interface to include UOM fields
  - Add price history lookup method

### 8. Frontend PO Creation Page Updates ⏳
**Priority: HIGH**
- **File**: `frontend/src/app/procurement/purchase-orders/new/page.tsx`

- **Required Changes**:
  - Add UOM dropdown in item dialog (line ~1108)
  - Load available UOMs when inventory item is selected
  - Display UOM conversion factor if different from base UOM
  - Add price lookup when supplier + item + quantity is known
  - Display suggested price from price list with option to override
  - Show price history tooltip/modal
  - Update `itemFormData` state to include `unitOfMeasure` and `itemUnitOfMeasureId`
  - Update `handleInventoryItemSelect` to load UOMs and prices

### 9. Frontend Components to Create ⏳
**Priority: MEDIUM**
- **New Components Needed**:
  1. `UnitOfMeasureSelector.tsx` - Dropdown with UOM conversion display
  2. `PriceListPriceDisplay.tsx` - Shows suggested price with price list info
  3. `PriceHistoryTooltip.tsx` - Shows historical prices for the item

### 10. Testing ⏳
**Priority: MEDIUM**
- **Test Scenarios**:
  1. Create PO with item that has multiple UOMs
  2. Verify UOM conversion calculations
  3. Create PO with item that has price list
  4. Verify price is auto-populated from price list
  5. Override price list price and verify it's saved
  6. Create PO with item that has no price list
  7. Verify manual price entry works
  8. Test quantity-based pricing tiers
  9. Test with different currencies
  10. Test PR to PO conversion with UOM preservation

## Implementation Priority

### Phase 1 (Immediate - Backend Foundation)
1. ✅ Entity updates
2. ✅ Database migration
3. ✅ DTO updates
4. ✅ Price List Lookup Service

### Phase 2 (Next - Service Integration)
5. ⏳ Service layer integration
6. ⏳ API controller updates

### Phase 3 (Frontend Integration)
7. ⏳ Frontend service updates
8. ⏳ Frontend PO creation page updates
9. ⏳ Frontend components

### Phase 4 (Validation & Testing)
10. ⏳ Comprehensive testing

## Database Migration Instructions

To apply the database migration:

```sql
-- Run this script on your database
-- File: database/migrations/Add_UOM_And_PriceList_To_PurchaseOrderItems.sql

-- The script will:
-- 1. Add new columns to PurchaseOrderItems table
-- 2. Create foreign key constraints
-- 3. Create indexes
-- 4. Update existing records with default UOM from inventory items
```

## API Endpoints to Add

### Get Item Units of Measure
```
GET /api/inventory/items/{itemId}/units
Response: [
  {
    "id": "guid",
    "unitOfMeasureId": "guid",
    "unitCode": "EA",
    "unitName": "Each",
    "conversionFactor": 1.0,
    "isBaseUnit": true,
    "isPurchaseUnit": true,
    "isSalesUnit": true
  }
]
```

### Get Supplier Item Price
```
GET /api/pricing/supplier/{supplierId}/item/{itemId}/price?quantity=10
Response: {
  "priceListLineId": "guid",
  "priceListName": "Supplier ABC - 2026",
  "basePrice": 100.00,
  "netPrice": 95.00,
  "unitOfMeasure": "EA",
  "minQuantity": 1,
  "maxQuantity": 100,
  "currency": "USD",
  "discountPercent": 5.0
}
```

### Get Price History
```
GET /api/pricing/supplier/{supplierId}/item/{itemId}/history?months=12
Response: [
  {
    "date": "2026-01-01",
    "unitPrice": 95.00,
    "unitOfMeasure": "EA",
    "priceListName": "Supplier ABC - 2026",
    "currency": "USD"
  }
]
```

## Frontend Implementation Example

### UOM Selector in Item Dialog
```typescript
// Add to item dialog state
const [availableUOMs, setAvailableUOMs] = useState<ItemUnitOfMeasureDto[]>([]);
const [selectedUOM, setSelectedUOM] = useState<ItemUnitOfMeasureDto | null>(null);

// Load UOMs when item is selected
const handleInventoryItemSelect = async (itemId: string) => {
  const item = inventoryItems.find(i => i.id === itemId);
  if (item) {
    // Load available UOMs
    const uoms = await inventoryManagementService.getItemUnitsOfMeasure(itemId);
    setAvailableUOMs(uoms);
    
    // Set default to purchase UOM or base UOM
    const purchaseUOM = uoms.find(u => u.isPurchaseUnit) || uoms.find(u => u.isBaseUnit);
    setSelectedUOM(purchaseUOM);
    
    // Load price if supplier is selected
    if (selectedSupplierId) {
      const price = await pricingService.getSupplierItemPrice(
        selectedSupplierId,
        itemId,
        itemFormData.orderedQuantity
      );
      if (price) {
        setItemFormData(prev => ({
          ...prev,
          unitPrice: price.netPrice,
          priceListLineId: price.priceListLineId,
          unitOfMeasure: price.unitOfMeasure
        }));
      }
    }
  }
};
```

## Notes

- The implementation follows the existing patterns in the codebase
- All new fields are nullable to maintain backward compatibility
- Default UOM is "EA" (Each) if not specified
- Price list integration is optional - manual price entry still works
- UOM conversion factors are stored for audit and display purposes
- The system supports multiple UOMs per item through the ItemUnitOfMeasure entity

## Next Steps

1. Run the database migration script
2. Implement service layer integration (Phase 2)
3. Add API endpoints for UOM and price lookup
4. Update frontend services
5. Implement frontend UI components
6. Test end-to-end workflow

## Questions/Decisions Needed

1. Should we enforce UOM selection from available UOMs only, or allow free-text entry?
   - **Recommendation**: Enforce selection for inventory items, allow free-text for non-inventory items

2. Should price list prices be mandatory or optional?
   - **Recommendation**: Optional - allow manual override

3. How should we handle UOM conversions when receiving goods?
   - **Recommendation**: Store both purchase UOM and base UOM quantities

4. Should we show price variance warnings if manual price differs significantly from price list?
   - **Recommendation**: Yes, show warning if variance > 10%

## References

- Entity Framework Core documentation for migrations
- Existing UOM implementation in `src/ErpSystem.Core/Entities/Inventory/InventoryEnhancedEntities.cs`
- Existing Price List implementation in `src/ErpSystem.Core/Entities/Pricing/PriceListEntities.cs`
- Purchase Order creation flow in `frontend/src/app/procurement/purchase-orders/new/page.tsx`
