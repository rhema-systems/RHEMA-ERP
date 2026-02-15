# Purchasing Module Migration Progress

**Date:** 2026-01-26  
**Status:** Phase 0 - Backend Migration (In Progress)

---

## ✅ Completed

### Phase 0.1: Entity Updates
- ✅ Updated `PurchaseOrder.SupplierId` → `PurchaseOrder.BusinessPartnerId`
- ✅ Updated `PurchaseOrder.Supplier` navigation → `PurchaseOrder.BusinessPartner`
- ✅ Updated `PurchaseOrder.SupplierOrderNumber` → `PurchaseOrder.BusinessPartnerOrderNumber`
- ✅ Updated `PurchaseRequisition.PreferredSupplierId` → `PurchaseRequisition.PreferredBusinessPartnerId`
- ✅ Updated `PurchaseRequisitionItem.PreferredSupplierId` → `PurchaseRequisitionItem.PreferredBusinessPartnerId`
- ✅ Updated `PurchaseRequisitionItem.PreferredSupplier` navigation → `PurchaseRequisitionItem.PreferredBusinessPartner`
- ✅ Updated `PurchaseOrderItem.SupplierItemCode` → `PurchaseOrderItem.BusinessPartnerItemCode`

### Phase 0.6: Repository Updates (Partial)
- ✅ Updated `PurchaseOrderRepositories.cs` - All `.Include(po => po.Supplier)` → `.Include(po => po.BusinessPartner)`
- ✅ Updated `PurchaseOrderRepositories.cs` - All `SupplierId` references → `BusinessPartnerId`
- ✅ Updated `PurchaseRequisitionRepositories.cs` - All `.Include(pri => pri.PreferredSupplier)` → `.Include(pri => pri.PreferredBusinessPartner)`
- ✅ Updated `PurchaseRequisitionRepositories.cs` - All `PreferredSupplierId` references → `PreferredBusinessPartnerId`
- ✅ Updated `SupplierRepositories.cs` - Purchase order references to use `BusinessPartnerId`
- ✅ Updated `BusinessPartnerRepositories.cs` - Purchase order references to use `BusinessPartnerId`
- ✅ Updated `SupplierReportingService.cs` - All `SupplierId` references → `BusinessPartnerId`
- ✅ Updated `SupplierPerformanceService.cs` - All `SupplierId` references → `BusinessPartnerId`
- ✅ Updated `ProcurementPlanService.cs` - Purchase order creation to use `BusinessPartnerId`

---

## 🔄 In Progress

### Phase 0.5: Controller Updates (Remaining)
**Files to Update:**
1. `src/ErpSystem.Api/Controllers/Procurement/PurchaseOrdersController.cs`
2. `src/ErpSystem.Api/Controllers/Procurement/PurchaseRequisitionsController.cs`

**Required Changes:**

#### PurchaseOrdersController.cs
- Line 71: `SupplierId` → `BusinessPartnerId`
- Line 72: `Supplier?.Name` → `BusinessPartner?.PartnerName`
- Line 120: `SupplierId` → `BusinessPartnerId`
- Line 121: `Supplier?.Name` → `BusinessPartner?.PartnerName`
- Line 141: `SupplierOrderNumber` → `BusinessPartnerOrderNumber`
- Line 145-147: All `Supplier` references → `BusinessPartner`
- Line 153: `SupplierItemCode` → `BusinessPartnerItemCode`
- Line 183: `Supplier?.Name` → `BusinessPartner?.PartnerName`
- Line 243: `SupplierId` → `BusinessPartnerId`
- Line 273: `SupplierItemCode` → `BusinessPartnerItemCode`
- Line 518: `Supplier?.Name` → `BusinessPartner?.PartnerName`
- Line 544-545: `SupplierId` and `Supplier` references
- Line 578-579: `SupplierId` and `Supplier` references
- Line 615-616: `SupplierId` and `Supplier` references
- Line 636: `SupplierOrderNumber` → `BusinessPartnerOrderNumber`
- Line 640-642: All `Supplier` references → `BusinessPartner`
- Line 648: `SupplierItemCode` → `BusinessPartnerItemCode`
- Line 678: `Supplier?.Name` → `BusinessPartner?.PartnerName`

#### PurchaseRequisitionsController.cs
- Line 128: `PreferredSupplierId` → `PreferredBusinessPartnerId`
- Line 129: `PreferredSupplier?.Name` → `PreferredBusinessPartner?.PartnerName`
- Line 203: `PreferredSupplierId` → `PreferredBusinessPartnerId`
- Line 521: `PreferredSupplierId` → `PreferredBusinessPartnerId`
- Line 615: `PreferredSupplierId` → `PreferredBusinessPartnerId`
- Line 616: `PreferredSupplier?.Name` → `PreferredBusinessPartner?.PartnerName`

---

## 📋 Remaining Tasks

### Phase 0.3: Database Migration
- [ ] Create migration after all code changes are complete
- [ ] Review migration for data migration needs
- [ ] Test migration on development database

### Phase 0.4: DTO Updates
**Files to Update:**
- `src/ErpSystem.Core/DTOs/Procurement/PurchaseOrderDTOs.cs`
- `src/ErpSystem.Core/DTOs/Procurement/PurchaseRequisitionDTOs.cs`

**Changes Needed:**
- Replace all `SupplierId` → `BusinessPartnerId`
- Replace all `SupplierName` → `BusinessPartnerName`
- Replace all `SupplierCode` → `BusinessPartnerCode`
- Replace all `SupplierItemCode` → `BusinessPartnerItemCode`
- Replace all `SupplierOrderNumber` → `BusinessPartnerOrderNumber`
- Replace all `PreferredSupplierId` → `PreferredBusinessPartnerId`
- Replace all `PreferredSupplierName` → `PreferredBusinessPartnerName`

---

## Next Steps

1. ✅ Complete controller updates (PurchaseOrdersController, PurchaseRequisitionsController)
2. ✅ Update DTOs
3. ✅ Create and test database migration
4. ✅ Test build
5. ✅ Proceed to Phase 1: Frontend Service Creation

---

## Notes

- The migration is straightforward since we're replacing one foreign key with another
- BusinessPartner entity already exists and is fully functional
- No data loss expected - just relationship changes
- All Supplier functionality is available in BusinessPartner (and more)

---

**Last Updated:** 2026-01-26 13:32  
**Updated By:** Kilo Code
