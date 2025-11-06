# InUse Status Addition - Summary

## Date
January 6, 2025

## Change Summary
Added new `InUse` status to `AssetStatus` enum to distinguish between vehicles being used for travel/work orders versus vehicles being worked on (maintenance).

## Rationale
Using "Maintenance" status for vehicles being used for travel was semantically incorrect and confusing:
- **Maintenance** should mean: Vehicle is being repaired/serviced (unavailable because it's broken)
- **InUse** should mean: Vehicle is currently being used for travel/work order execution (unavailable because it's in use)

## Changes Made

### 1. Added InUse to AssetStatus Enum
**File**: `src/ErpSystem.Core/Enums/MaintenanceEnums.cs`

```csharp
public enum AssetStatus
{
    Active = 0,           // Available for use
    Inactive = 1,         // Not in use
    Maintenance = 2,      // In maintenance (being worked on)
    OutOfService = 3,     // Out of service
    Retired = 4,          // Retired from use
    Disposed = 5,         // Disposed/scrapped
    InUse = 6            // ✅ NEW: Currently being used for travel or work order execution
}
```

### 2. Updated WorkOrderService
**File**: `src/ErpSystem.Core/Services/Maintenance/WorkOrderService.cs`

**Changed from:**
- `AssetStatus.Maintenance` (when work order starts)
- Check for `AssetStatus.Maintenance` (when work order completes)

**Changed to:**
- `AssetStatus.InUse` (when work order starts)
- Check for `AssetStatus.InUse` (when work order completes)

**Lines Modified:**
- Line 1951: Method documentation
- Line 1983: Set status to `InUse`
- Line 1986: Log message references "InUse"
- Line 1994: Log message references "InUse"
- Line 2036: Check for `InUse` status

### 3. Updated Documentation
**File**: `VEHICLE_STATUS_MANAGEMENT_IMPLEMENTATION.md`

Updated all references from "Maintenance" to "InUse" throughout the document.

## Status Transition Logic

### When Work Order Starts
```
IF vehicle.Status == Active
THEN vehicle.Status = InUse
```

### When Work Order Completes
```
IF vehicle.Status == InUse
THEN vehicle.Status = Active
```

## Benefits

1. **Semantic Clarity**: Clear distinction between vehicles being used vs vehicles being repaired
2. **Better Reporting**: Can now accurately report on vehicle utilization vs maintenance downtime
3. **Improved UI**: Status displays will be more meaningful to users
4. **Correct Availability Logic**: Can filter vehicles correctly:
   - Exclude `InUse` vehicles from assignment dropdowns (being used)
   - Exclude `Maintenance` vehicles from assignment dropdowns (being repaired)
   - Show only `Active` vehicles as available

## Database Impact
- **No migration needed** - This is an enum value change only
- Existing data with status = 2 (Maintenance) remains unchanged
- New work orders will use status = 6 (InUse) for vehicles

## Status Display Mapping

| Status Value | Enum Name | Display Text | Meaning |
|---|---|---|---|
| 0 | Active | Active | Available for use |
| 1 | Inactive | Inactive | Not currently in service |
| 2 | Maintenance | In Maintenance | Being repaired/serviced |
| 3 | OutOfService | Out of Service | Permanently unavailable |
| 4 | Retired | Retired | No longer in fleet |
| 5 | Disposed | Disposed | Scrapped/sold |
| 6 | **InUse** | **In Use** | **Currently being used** |

## Build Status
✅ Backend build: Successful (86.1s)

## Testing Notes

When testing, verify:
1. ✅ Vehicle status changes to `InUse` (6) when work order starts
2. ✅ Vehicle status changes to `Active` (0) when work order completes
3. ✅ Vehicles manually set to `Maintenance` (2) are not affected by work order start
4. ✅ Vehicles manually set to `Maintenance` (2) are not changed back when work order completes
5. ✅ Log messages reference "InUse" not "Maintenance"

## SQL Verification

```sql
-- Check vehicles currently in use
SELECT Id, Name, AssetNumber, Status, UpdatedAt
FROM MaintenanceAssets
WHERE Status = 6; -- InUse

-- Check vehicles in maintenance (being repaired)
SELECT Id, Name, AssetNumber, Status, UpdatedAt
FROM MaintenanceAssets
WHERE Status = 2; -- Maintenance

-- Show all vehicle statuses
SELECT 
    Status,
    CASE Status
        WHEN 0 THEN 'Active'
        WHEN 1 THEN 'Inactive'
        WHEN 2 THEN 'Maintenance'
        WHEN 3 THEN 'OutOfService'
        WHEN 4 THEN 'Retired'
        WHEN 5 THEN 'Disposed'
        WHEN 6 THEN 'InUse'
    END AS StatusName,
    COUNT(*) AS Count
FROM MaintenanceAssets
WHERE AssetCategoryId IN (SELECT Id FROM AssetCategories WHERE AssetType = 'Vehicle')
GROUP BY Status;
```

## Related Files
- `src/ErpSystem.Core/Enums/MaintenanceEnums.cs` - Enum definition
- `src/ErpSystem.Core/Services/Maintenance/WorkOrderService.cs` - Status update logic
- `VEHICLE_STATUS_MANAGEMENT_IMPLEMENTATION.md` - Full implementation documentation
