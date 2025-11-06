# Vehicle Tracking Implementation for Expenses and Schedules

## Overview
Implemented VehicleId tracking for MaintenanceExpense to enable vehicle availability management. When a vehicle is assigned to a schedule or expense, it should be unavailable for other assignments to prevent double-booking.

## Implementation Date
January 6, 2025

## Changes Made

### 1. Backend Entity Changes

#### MaintenanceExpense Entity (ResourceManagementEntities.cs)
**Lines 493-508**: Added VehicleId field and navigation property
```csharp
// Vehicle tracking
public Guid? VehicleId { get; set; }

// Navigation properties
public virtual MaintenanceAsset? Vehicle { get; set; }
```

### 2. Backend DTO Changes

#### MaintenanceExpenseDto (MaintenanceDTOs.cs)
**Lines 4087-4089**: Added vehicle fields
```csharp
// Vehicle tracking
public Guid? VehicleId { get; set; }
public string? VehicleName { get; set; }
```

#### CreateMaintenanceExpenseDto (MaintenanceDTOs.cs)
**Line 4144**: Added VehicleId field
```csharp
public Guid? VehicleId { get; set; }
```

#### UpdateMaintenanceExpenseDto (MaintenanceDTOs.cs)
**Line 4183**: Added VehicleId field
```csharp
public Guid? VehicleId { get; set; }
```

### 3. Backend Service Changes

#### MaintenanceExpenseService.cs

**CreateExpenseAsync (Line 52)**: Map VehicleId from DTO
```csharp
VehicleId = createDto.VehicleId,
```

**UpdateExpenseAsync (Lines 100-101)**: Update VehicleId if provided
```csharp
if (updateDto.VehicleId.HasValue)
    expense.VehicleId = updateDto.VehicleId;
```

**MapToDto (Lines 299-300)**: Map VehicleId and VehicleName to DTO
```csharp
VehicleId = expense.VehicleId,
VehicleName = expense.Vehicle?.Name,
```

### 4. Backend Repository Changes

#### MaintenanceExpenseRepository.cs

**GetByWorkOrderIdAsync (Line 20)**: Include Vehicle navigation property
```csharp
.Include(e => e.Vehicle)
```

**GetByIdWithDetailsAsync (Line 71)**: Include Vehicle navigation property
```csharp
.Include(e => e.Vehicle)
```

### 5. Database Migration

**Migration**: `20251106125345_AddVehicleIdToMaintenanceExpense`
- Added `VehicleId` column (nullable Guid) to `MaintenanceExpenses` table
- Added foreign key constraint to `MaintenanceAssets` table
- Migration successfully applied to database

### 6. Frontend Changes

#### work-orders/page.tsx

**Create Expense Mapping (Line 3044)**: Send vehicleId to backend
```typescript
vehicleId: expense.vehicleUsed || null, // Send vehicleId to backend
```

**Update Expense Mapping (Line 3073)**: Send vehicleId to backend
```typescript
vehicleId: expense.vehicleUsed || null, // Send vehicleId to backend
```

## Field Mapping Summary

| Frontend Field | Backend DTO Field | Backend Entity Field | Notes |
|---|---|---|---|
| `vehicleUsed` | `vehicleId` | `VehicleId` | Vehicle (Asset) ID |
| `vendor` | `vendorName` | `VendorName` | Vendor/supplier name |
| `receiptNumber` | `referenceNumber` | `ReferenceNumber` | Receipt/invoice number |
| `notes` | `location` | `Location` | Location/notes field |

## Vehicle Availability Logic (To Be Implemented)

### Requirements
When a work order is approved and started:
1. Any vehicle assigned to an expense or schedule should become unavailable
2. The vehicle's status in `MaintenanceAssets` table should be updated to prevent:
   - Being selected for maintenance
   - Being assigned to another expense
   - Being assigned to another schedule

### Recommended Implementation Approach

#### Option 1: Update Asset Status on Work Order Start
```csharp
public async Task<WorkOrderDto> UpdateWorkOrderStatusAsync(Guid id, string status, string? notes = null)
{
    // Existing code...
    
    if (status == "InProgress")
    {
        // Get all expenses and schedules with vehicles
        var expenses = await _expenseRepository.GetByWorkOrderIdAsync(id);
        var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(id);
        
        var vehicleIds = expenses.Where(e => e.VehicleId.HasValue)
            .Select(e => e.VehicleId!.Value)
            .Union(schedules.Where(s => s.AssignedVehicleId.HasValue)
                .Select(s => s.AssignedVehicleId!.Value))
            .Distinct();
        
        foreach (var vehicleId in vehicleIds)
        {
            var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
            if (vehicle != null)
            {
                vehicle.Status = AssetStatus.InUse; // Mark as in use
                await _assetRepository.UpdateAsync(vehicle);
            }
        }
        
        await _unitOfWork.SaveChangesAsync();
    }
}
```

#### Option 2: Create VehicleAllocation Table
Create a dedicated table to track vehicle assignments:
```csharp
public class VehicleAllocation : TenantEntity
{
    public Guid VehicleId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? ScheduleId { get; set; }
    public Guid? ExpenseId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Allocated"; // Allocated, InUse, Returned
    
    public virtual MaintenanceAsset Vehicle { get; set; }
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual MaintenanceStaffSchedule? Schedule { get; set; }
    public virtual MaintenanceExpense? Expense { get; set; }
}
```

#### Option 3: Check Vehicle Availability in GetAvailableVehicles
Update the existing `GetAvailableVehiclesAsync` method to exclude vehicles currently in use:
```csharp
public async Task<IEnumerable<MaintenanceAssetDto>> GetAvailableVehiclesAsync()
{
    var query = _assetRepository.GetQueryable()
        .Include(a => a.AssetCategory)
        .Where(a => a.AssetCategory.AssetType == "Vehicle" 
            && a.Status == AssetStatus.Active
            && !_context.MaintenanceExpenses
                .Include(e => e.WorkOrder)
                .Where(e => e.VehicleId == a.Id 
                    && (e.WorkOrder.Status == "InProgress" || e.WorkOrder.Status == "Approved"))
                .Any()
            && !_context.MaintenanceStaffSchedules
                .Include(s => s.WorkOrder)
                .Where(s => s.AssignedVehicleId == a.Id
                    && (s.WorkOrder.Status == "InProgress" || s.WorkOrder.Status == "Approved")
                    && s.Status != "Completed")
                .Any()
        );
    
    var vehicles = await query.ToListAsync();
    return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(vehicles);
}
```

## Build Status
- ✅ Backend compiles successfully (4.1s)
- ✅ Frontend compiles successfully (54s)
- ✅ Database migration applied successfully

## Testing Checklist

### Backend
- [ ] Create expense with vehicleId - should save to database
- [ ] Update expense with vehicleId - should update in database
- [ ] Get expense by ID - should include VehicleId and VehicleName
- [ ] Get expenses by work order - should include vehicle data

### Frontend
- [ ] Select vehicle from dropdown - should store vehicleId
- [ ] Save expense with vehicle - should send vehicleId to backend
- [ ] Load expense with vehicle - should display vehicle name
- [ ] Update expense with different vehicle - should update vehicleId

### Vehicle Availability
- [ ] Vehicle assigned to approved/in-progress work order - should not appear in available vehicles list
- [ ] Vehicle assigned to completed work order - should appear in available vehicles list
- [ ] Vehicle assigned to draft work order - should appear in available vehicles list
- [ ] Same vehicle cannot be assigned to multiple active work orders

## Next Steps

1. Implement vehicle availability checking logic (choose one of the three options above)
2. Add similar vehicle tracking to MaintenanceStaffSchedule if not already present
3. Create service method to release vehicles when work order is completed
4. Add validation to prevent double-booking of vehicles
5. Consider adding vehicle usage reports/analytics

## Notes

- The `vehicleUsed` field in the frontend stores the vehicle (asset) ID as a string
- The backend `VehicleId` field references the `MaintenanceAssets` table
- Vehicle availability logic is not yet implemented - vehicles can currently be double-booked
- Consider adding a check in the expense/schedule creation to validate vehicle availability
