# Phase 2 & 3 Implementation Guide
## UOM and Price List Integration - Service & Frontend Layer

This document provides complete implementation instructions for Phase 2 (Service & API Layer) and Phase 3 (Frontend Integration).

## Phase 2: Service & API Layer Implementation

### Step 1: Create API Controller for Pricing

Create new file: `src/ErpSystem.API/Controllers/PricingController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services.Pricing;

namespace ErpSystem.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PricingController : ControllerBase
{
    private readonly PriceListLookupService _priceListLookupService;

    public PricingController(PriceListLookupService priceListLookupService)
    {
        _priceListLookupService = priceListLookupService;
    }

    [HttpGet("supplier/{supplierId}/item/{itemId}/price")]
    public async Task<IActionResult> GetSupplierItemPrice(
        Guid supplierId,
        Guid itemId,
        [FromQuery] decimal quantity = 1)
    {
        var price = await _priceListLookupService.GetSupplierPriceAsync(itemId, supplierId, quantity);
        if (price == null)
        {
            return NotFound(new { message = "No price found for this item from the supplier" });
        }
        return Ok(price);
    }

    [HttpGet("supplier/{supplierId}/item/{itemId}/prices")]
    public async Task<IActionResult> GetAllSupplierItemPrices(Guid supplierId, Guid itemId)
    {
        var prices = await _priceListLookupService.GetAllSupplierPricesAsync(itemId, supplierId);
        return Ok(prices);
    }

    [HttpGet("supplier/{supplierId}/item/{itemId}/history")]
    public async Task<IActionResult> GetPriceHistory(
        Guid supplierId,
        Guid itemId,
        [FromQuery] int months = 12)
    {
        var history = await _priceListLookupService.GetPriceHistoryAsync(itemId, supplierId, months);
        return Ok(history);
    }
}
```

### Step 2: Add UOM Endpoint to InventoryController

Add to existing `src/ErpSystem.API/Controllers/InventoryController.cs`:

```csharp
[HttpGet("items/{itemId}/units")]
public async Task<IActionResult> GetItemUnitsOfMeasure(Guid itemId)
{
    var units = await _itemUnitOfMeasureRepository.GetByItemAsync(itemId);
    
    var result = units.Select(u => new
    {
        id = u.Id,
        unitOfMeasureId = u.UnitOfMeasureId,
        unitCode = u.UnitOfMeasure.Code,
        unitName = u.UnitOfMeasure.Name,
        conversionFactor = u.ConversionFactor,
        isBaseUnit = u.IsBaseUnit,
        isPurchaseUnit = u.IsPurchaseUnit,
        isSalesUnit = u.IsSalesUnit,
        barcode = u.Barcode
    });
    
    return Ok(result);
}
```

### Step 3: Register PriceListLookupService in DI

Add to `src/ErpSystem.API/Program.cs` or `Startup.cs`:

```csharp
// Add in the service registration section
builder.Services.AddScoped<PriceListLookupService>();
```

## Phase 3: Frontend Integration

### Step 1: Create Pricing Service

Create new file: `frontend/src/services/pricingService.ts`

```typescript
import api from './api';

export interface PriceListLookupResult {
  priceListLineId: string;
  priceListId: string;
  priceListCode: string;
  priceListName: string;
  basePrice: number;
  netPrice: number;
  unitOfMeasure: string;
  minQuantity: number;
  maxQuantity?: number;
  effectiveFrom: string;
  effectiveTo?: string;
  currency: string;
  discountPercent?: number;
  lastPriceUpdate?: string;
  finalPrice: number;
}

export interface PriceHistoryResult {
  date: string;
  unitPrice: number;
  unitOfMeasure: string;
  priceListName: string;
  currency: string;
}

class PricingService {
  async getSupplierItemPrice(
    supplierId: string,
    itemId: string,
    quantity: number = 1
  ): Promise<PriceListLookupResult | null> {
    try {
      const response = await api.get<PriceListLookupResult>(
        `/pricing/supplier/${supplierId}/item/${itemId}/price`,
        { params: { quantity } }
      );
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  }

  async getAllSupplierItemPrices(
    supplierId: string,
    itemId: string
  ): Promise<PriceListLookupResult[]> {
    const response = await api.get<PriceListLookupResult[]>(
      `/pricing/supplier/${supplierId}/item/${itemId}/prices`
    );
    return response.data;
  }

  async getPriceHistory(
    supplierId: string,
    itemId: string,
    months: number = 12
  ): Promise<PriceHistoryResult[]> {
    const response = await api.get<PriceHistoryResult[]>(
      `/pricing/supplier/${supplierId}/item/${itemId}/history`,
      { params: { months } }
    );
    return response.data;
  }
}

export default new PricingService();
```

### Step 2: Update Inventory Management Service

Add to `frontend/src/services/inventoryManagementService.ts`:

```typescript
export interface ItemUnitOfMeasureDto {
  id: string;
  unitOfMeasureId: string;
  unitCode: string;
  unitName: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  isPurchaseUnit: boolean;
  isSalesUnit: boolean;
  barcode?: string;
}

// Add to InventoryManagementService class:
async getItemUnitsOfMeasure(itemId: string): Promise<ItemUnitOfMeasureDto[]> {
  const response = await api.get<ItemUnitOfMeasureDto[]>(
    `/inventory/items/${itemId}/units`
  );
  return response.data;
}
```

### Step 3: Update PO Creation Page

Modify `frontend/src/app/procurement/purchase-orders/new/page.tsx`:

#### 3.1: Add State for UOM and Pricing

```typescript
// Add after existing state declarations (around line 100)
const [availableUOMs, setAvailableUOMs] = useState<ItemUnitOfMeasureDto[]>([]);
const [selectedUOM, setSelectedUOM] = useState<ItemUnitOfMeasureDto | null>(null);
const [suggestedPrice, setSuggestedPrice] = useState<PriceListLookupResult | null>(null);
const [showPriceHistory, setShowPriceHistory] = useState(false);
const [priceHistory, setPriceHistory] = useState<PriceHistoryResult[]>([]);
```

#### 3.2: Update Item Form Data Interface

```typescript
// Update POItemFormData interface (around line 61)
interface POItemFormData extends CreatePurchaseOrderItemDto {
  tempId: string;
  itemCode?: string;
  itemName?: string;
  unitOfMeasure: string;
  itemUnitOfMeasureId?: string;
  priceListLineId?: string;
}
```

#### 3.3: Update handleInventoryItemSelect Function

```typescript
// Replace existing handleInventoryItemSelect function (around line 251)
const handleInventoryItemSelect = async (itemId: string) => {
  const item = inventoryItems.find(i => i.id === itemId);
  if (item) {
    setSelectedInventoryItem(item);
    
    // Load available UOMs
    try {
      const uoms = await inventoryManagementService.getItemUnitsOfMeasure(itemId);
      setAvailableUOMs(uoms);
      
      // Set default to purchase UOM or base UOM
      const purchaseUOM = uoms.find(u => u.isPurchaseUnit) || uoms.find(u => u.isBaseUnit);
      setSelectedUOM(purchaseUOM || null);
      
      // Update form data with item details
      setItemFormData(prev => ({
        ...prev,
        inventoryItemId: item.id,
        itemCode: item.itemCode,
        itemName: item.name,
        itemDescription: item.description || item.name,
        unitOfMeasure: purchaseUOM?.unitCode || item.unitOfMeasure || 'EA',
        itemUnitOfMeasureId: purchaseUOM?.id
      }));
      
      // Load price if supplier is selected
      if (selectedSupplierId) {
        await loadSupplierPrice(itemId, prev.orderedQuantity);
      }
    } catch (error) {
      console.error('Error loading UOMs:', error);
      toast.error('Failed to load unit of measures');
    }
  }
};
```

#### 3.4: Add Price Loading Function

```typescript
// Add new function after handleInventoryItemSelect
const loadSupplierPrice = async (itemId: string, quantity: number) => {
  if (!selectedSupplierId) return;
  
  try {
    const price = await pricingService.getSupplierItemPrice(
      selectedSupplierId,
      itemId,
      quantity
    );
    
    if (price) {
      setSuggestedPrice(price);
      setItemFormData(prev => ({
        ...prev,
        unitPrice: price.netPrice,
        priceListLineId: price.priceListLineId,
        unitOfMeasure: price.unitOfMeasure
      }));
      toast.success(`Price loaded from ${price.priceListName}`);
    } else {
      setSuggestedPrice(null);
      // Use last purchase cost or standard cost
      const item = inventoryItems.find(i => i.id === itemId);
      if (item) {
        setItemFormData(prev => ({
          ...prev,
          unitPrice: item.lastPurchaseCost || item.standardCost || item.currentCost || 0
        }));
      }
    }
  } catch (error) {
    console.error('Error loading price:', error);
    // Don't show error toast, just use default price
  }
};
```

#### 3.5: Add Price History Loading

```typescript
// Add function to load price history
const loadPriceHistory = async (itemId: string) => {
  if (!selectedSupplierId) return;
  
  try {
    const history = await pricingService.getPriceHistory(selectedSupplierId, itemId);
    setPriceHistory(history);
    setShowPriceHistory(true);
  } catch (error) {
    console.error('Error loading price history:', error);
    toast.error('Failed to load price history');
  }
};
```

#### 3.6: Update Item Dialog UI

Add UOM selector and price display in the item dialog (around line 1086):

```typescript
{/* UOM Selection */}
<div className="space-y-2">
  <Label htmlFor="unitOfMeasure">Unit of Measure *</Label>
  <Select
    value={itemFormData.itemUnitOfMeasureId || ''}
    onValueChange={(value) => {
      const uom = availableUOMs.find(u => u.id === value);
      if (uom) {
        setSelectedUOM(uom);
        setItemFormData(prev => ({
          ...prev,
          unitOfMeasure: uom.unitCode,
          itemUnitOfMeasureId: uom.id
        }));
      }
    }}
  >
    <SelectTrigger>
      <SelectValue placeholder="Select unit of measure" />
    </SelectTrigger>
    <SelectContent>
      {availableUOMs.map(uom => (
        <SelectItem key={uom.id} value={uom.id}>
          {uom.unitCode} - {uom.unitName}
          {uom.conversionFactor !== 1 && ` (${uom.conversionFactor}x)`}
          {uom.isBaseUnit && ' [Base]'}
          {uom.isPurchaseUnit && ' [Purchase]'}
        </SelectItem>
      ))}
    </SelectContent>
  </Select>
  {selectedUOM && !selectedUOM.isBaseUnit && (
    <p className="text-xs text-muted-foreground">
      Conversion: 1 {selectedUOM.unitCode} = {selectedUOM.conversionFactor} base units
    </p>
  )}
</div>

{/* Price Display with Price List Info */}
<div className="space-y-2">
  <div className="flex items-center justify-between">
    <Label htmlFor="unitPrice" className="flex items-center gap-2">
      <DollarSign className="h-4 w-4" />
      Unit Price *
    </Label>
    {suggestedPrice && (
      <Badge variant="outline" className="text-xs">
        From: {suggestedPrice.priceListName}
      </Badge>
    )}
  </div>
  <Input
    id="unitPrice"
    type="number"
    min="0"
    step="0.01"
    value={itemFormData.unitPrice}
    onChange={(e) => {
      const newPrice = parseFloat(e.target.value) || 0;
      setItemFormData(prev => ({ ...prev, unitPrice: newPrice }));
      
      // Show variance warning if significantly different from suggested price
      if (suggestedPrice && Math.abs(newPrice - suggestedPrice.netPrice) / suggestedPrice.netPrice > 0.1) {
        toast.warning('Price differs significantly from price list');
      }
    }}
  />
  {suggestedPrice && (
    <div className="text-xs space-y-1">
      <p className="text-muted-foreground">
        Suggested: ${suggestedPrice.netPrice.toFixed(2)}
        {suggestedPrice.discountPercent && ` (${suggestedPrice.discountPercent}% discount)`}
      </p>
      <Button
        type="button"
        variant="link"
        size="sm"
        className="h-auto p-0 text-xs"
        onClick={() => selectedInventoryItem && loadPriceHistory(selectedInventoryItem.id)}
      >
        View Price History
      </Button>
    </div>
  )}
</div>
```

#### 3.7: Add Price History Dialog

Add after the main item dialog (around line 1178):

```typescript
{/* Price History Dialog */}
<Dialog open={showPriceHistory} onOpenChange={setShowPriceHistory}>
  <DialogContent className="max-w-2xl">
    <DialogHeader>
      <DialogTitle>Price History</DialogTitle>
      <DialogDescription>
        Historical pricing for this item from the selected supplier
      </DialogDescription>
    </DialogHeader>
    <div className="space-y-4">
      {priceHistory.length === 0 ? (
        <p className="text-center text-muted-foreground py-8">
          No price history available
        </p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Date</TableHead>
              <TableHead>Price List</TableHead>
              <TableHead>Unit Price</TableHead>
              <TableHead>UOM</TableHead>
              <TableHead>Currency</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {priceHistory.map((item, index) => (
              <TableRow key={index}>
                <TableCell>{format(new Date(item.date), 'MMM dd, yyyy')}</TableCell>
                <TableCell>{item.priceListName}</TableCell>
                <TableCell>${item.unitPrice.toFixed(2)}</TableCell>
                <TableCell>{item.unitOfMeasure}</TableCell>
                <TableCell>{item.currency}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  </DialogContent>
</Dialog>
```

### Step 4: Update Purchasing Service

Add to `frontend/src/services/purchasingService.ts`:

```typescript
// Update CreatePurchaseOrderItemDto interface
export interface CreatePurchaseOrderItemDto {
  inventoryItemId: string;
  supplierItemCode?: string;
  itemDescription?: string;
  orderedQuantity: number;
  unitOfMeasure: string;
  itemUnitOfMeasureId?: string;
  unitPrice: number;
  priceListLineId?: string;
  expectedDeliveryDate?: string;
  notes?: string;
}
```

## Testing Checklist

After implementation, test the following scenarios:

### Backend Testing
- [ ] GET `/api/inventory/items/{id}/units` returns UOMs
- [ ] GET `/api/pricing/supplier/{supplierId}/item/{itemId}/price` returns price
- [ ] Price lookup works with quantity tiers
- [ ] Price history endpoint returns data

### Frontend Testing
- [ ] UOM dropdown appears when item is selected
- [ ] UOM conversion factor is displayed
- [ ] Price is auto-populated from price list
- [ ] Manual price override works
- [ ] Price variance warning appears when price differs >10%
- [ ] Price history dialog displays correctly
- [ ] PO creation saves UOM and price list reference
- [ ] Created PO displays correct UOM and price list info

### Integration Testing
- [ ] Create PO with item having multiple UOMs
- [ ] Create PO with item having price list
- [ ] Create PO with item having no price list
- [ ] Override price list price
- [ ] Test with different quantity tiers
- [ ] Convert PR to PO preserving UOM

## Deployment Steps

1. **Backend**:
   ```bash
   # Migration already applied
   # Build and deploy API
   dotnet build src/ErpSystem.API
   ```

2. **Frontend**:
   ```bash
   cd frontend
   npm run build
   # Deploy to production
   ```

3. **Verify**:
   - Check API endpoints are accessible
   - Test PO creation flow
   - Verify database records have UOM and price list data

## Rollback Plan

If issues occur:

1. **Database**: Migration can be rolled back:
   ```bash
   dotnet ef database update PreviousMigrationName --project src/ErpSystem.Data --startup-project src/ErpSystem.API
   ```

2. **Code**: Revert commits for:
   - PricingController.cs
   - Frontend service files
   - PO creation page changes

## Support & Troubleshooting

### Common Issues

1. **UOMs not loading**: Check ItemUnitOfMeasure repository is registered in DI
2. **Prices not found**: Verify price lists are active and approved
3. **Conversion factors wrong**: Check UOM schedule configuration
4. **API 404 errors**: Verify controller routes and service registration

### Debug Tips

- Enable detailed logging in PriceListLookupService
- Check browser console for frontend errors
- Verify API responses in Network tab
- Test API endpoints directly with Postman/Swagger

## Next Steps

After Phase 2 & 3 are complete:
1. Implement Phase 4 (comprehensive testing)
2. Add unit tests for PriceListLookupService
3. Add integration tests for PO creation with UOM/pricing
4. Update user documentation
5. Train users on new features
