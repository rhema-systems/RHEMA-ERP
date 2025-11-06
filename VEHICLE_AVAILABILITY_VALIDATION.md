# Vehicle Availability Validation Implementation

## Date
January 6, 2025

## Overview
Added validation logic to prevent work orders from starting if assigned vehicles are not available (not in Active status). This prevents conflicts when vehicles are already InUse, in Maintenance, or have any other non-Active status.

## Business Requirement
**Problem**: Until a work order starts, vehicles remain available (Active status) and can be assigned to multiple work orders. When trying to start a work order, it should check if all assigned vehicles are actually available.

**Solution**: Before allowing a work order to start, validate that all assigned vehicles have `Active` status. If any vehicle is unavailable (InUse, Maintenance, etc.), prevent the work order from starting and provide clear error messages telling the user which vehicles are unavailable and that they need to assign different vehicles.

## Implementation Details

### 1. New Validation Method: `ValidateVehicleAvailabilityAsync`

**Location**: `WorkOrderService.cs` (Lines 1982-2053)

**Purpose**: Checks if all vehicles assigned to schedules and expenses for a work order are available (Active status)

**Logic**:
1. Get all schedules and expenses for the work order
2. Extract all vehicle IDs from `AssignedVehicleId` (schedules) and `VehicleId` (expenses)
3. Remove duplicates
4. For each vehicle:
   - Check if it exists
   - Check if status is `Active`
   - If not Active, add to unavailable list with reason (e.g., "Ford F-150 (VEH-001) is currently InUse")
5. Return tuple: `(bool IsValid, List<string> UnavailableVehicles)`

**Return Values**:
- `IsValid = true, UnavailableVehicles = []` → All vehicles available, can start work order
- `IsValid = false, UnavailableVehicles = [...]` → Some vehicles unavailable, cannot start

**Key Code**:
```csharp
private async Task<(bool IsValid, List<string> UnavailableVehicles)> ValidateVehicleAvailabilityAsync(Guid workOrderId)
{
    // Get schedules and expenses
    var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrderId);
    var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrderId);

    // Collect vehicle IDs
    var vehicleIds = new List<Guid>();
    vehicleIds.AddRange(schedules.Where(s => s.AssignedVehicleId.HasValue)
        .Select(s => s.AssignedVehicleId!.Value));
    vehicleIds.AddRange(expenses.Where(e => e.VehicleId.HasValue)
        .Select(e => e.VehicleId!.Value));
    vehicleIds = vehicleIds.Distinct().ToList();

    if (!vehicleIds.Any())
        return (true, new List<string>()); // No vehicles, validation passes

    // Check availability
    var unavailableVehicles = new List<string>();
    foreach (var vehicleId in vehicleIds)
    {
        var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
        if (vehicle == null)
        {
            unavailableVehicles.Add($"Vehicle ID {vehicleId} not found");
        }
        else if (vehicle.Status != AssetStatus.Active)
        {
            unavailableVehicles.Add($"{vehicle.Name} ({vehicle.AssetNumber}) is currently {vehicle.Status}");
        }
    }

    return (!unavailableVehicles.Any(), unavailableVehicles);
}
```

### 2. Integration with StartWorkOrderAsync (Full Version)

**Location**: Lines 100-116

**Changes**:
- Added validation call before any work order operations
- If validation fails, set `result.Success = false` and add warning messages
- Return early without starting the work order

**Code**:
```csharp
// Validate vehicle availability before starting
var (vehiclesAvailable, unavailableVehicles) = await ValidateVehicleAvailabilityAsync(workOrderId);
if (!vehiclesAvailable)
{
    result.Success = false;
    result.WarningMessages.Add("Cannot start work order: Some vehicles are not available.");
    foreach (var vehicle in unavailableVehicles)
    {
        result.WarningMessages.Add($"  - {vehicle}");
    }
    result.WarningMessages.Add("Please assign different vehicles before starting this work order.");
    
    _logger.LogWarning("Work order {WorkOrderId} cannot start due to unavailable vehicles: {Vehicles}",
        workOrderId, string.Join(", ", unavailableVehicles));
    
    return result; // Early return, don't start work order
}
```

### 3. Integration with StartWorkOrderAsync (Simple Version)

**Location**: Lines 1045-1057

**Changes**:
- Added validation call before any work order operations
- If validation fails, throw `InvalidOperationException` with detailed message
- Prevents status change and blocks work order start

**Code**:
```csharp
// Validate vehicle availability before starting
var (vehiclesAvailable, unavailableVehicles) = await ValidateVehicleAvailabilityAsync(id);
if (!vehiclesAvailable)
{
    var errorMessage = $"Cannot start work order: Some vehicles are not available. " +
        string.Join(", ", unavailableVehicles) + ". " +
        "Please assign different vehicles before starting this work order.";
    
    _logger.LogWarning("Work order {WorkOrderId} cannot start due to unavailable vehicles: {Vehicles}",
        id, string.Join(", ", unavailableVehicles));
    
    throw new InvalidOperationException(errorMessage);
}
```

## User Experience

### Scenario 1: All Vehicles Available ✅
```
User clicks "Start Work Order"
  ↓
Validation checks 2 vehicles: Ford F-150, Toyota Hilux
  ↓
Both vehicles status = Active
  ↓
Validation passes ✅
  ↓
Work order starts → Vehicles status = InUse
  ↓
Success message: "Work order started successfully"
```

### Scenario 2: Vehicle Already InUse ❌
```
User clicks "Start Work Order"
  ↓
Validation checks 2 vehicles: Ford F-150, Toyota Hilux
  ↓
Ford F-150 status = Active ✅
Toyota Hilux status = InUse ❌ (being used by Work Order WO-12345)
  ↓
Validation fails ❌
  ↓
Error message:
  "Cannot start work order: Some vehicles are not available.
   - Toyota Hilux (VEH-2024-0002) is currently InUse
   Please assign different vehicles before starting this work order."
  ↓
Work order does NOT start
User must assign a different vehicle to replace Toyota Hilux
```

### Scenario 3: Vehicle in Maintenance ❌
```
User clicks "Start Work Order"
  ↓
Validation checks 1 vehicle: Ford F-150
  ↓
Ford F-150 status = Maintenance ❌ (being repaired)
  ↓
Validation fails ❌
  ↓
Error message:
  "Cannot start work order: Some vehicles are not available.
   - Ford F-150 (VEH-2024-0001) is currently Maintenance
   Please assign different vehicles before starting this work order."
  ↓
Work order does NOT start
User must wait for repair to complete or assign a different vehicle
```

## Error Message Format

**Structure**:
```
Cannot start work order: Some vehicles are not available.
  - {VehicleName} ({AssetNumber}) is currently {Status}
  - {VehicleName} ({AssetNumber}) is currently {Status}
Please assign different vehicles before starting this work order.
```

**Example**:
```
Cannot start work order: Some vehicles are not available.
  - Ford F-150 (VEH-2024-0001) is currently InUse
  - Toyota Hilux (VEH-2024-0002) is currently Maintenance
Please assign different vehicles before starting this work order.
```

## Logging

### Success Case
```
INFO: All 2 vehicle(s) are available for work order {WorkOrderId}
```

### Failure Case (Individual Vehicles)
```
WARN: Vehicle {VehicleId} ({VehicleName}) is not available for work order {WorkOrderId}. Current status: InUse
WARN: Vehicle {VehicleId} ({VehicleName}) is not available for work order {WorkOrderId}. Current status: Maintenance
```

### Failure Case (Summary)
```
WARN: 2 vehicle(s) are unavailable for work order {WorkOrderId}
WARN: Work order {WorkOrderId} cannot start due to unavailable vehicles: Ford F-150 (VEH-2024-0001) is currently InUse, Toyota Hilux (VEH-2024-0002) is currently Maintenance
```

## Status Checks

The validation checks if vehicle status equals `AssetStatus.Active` (value = 0). All other statuses will fail validation:

| Status | Value | Validation Result | Meaning |
|---|---|---|---|
| **Active** | 0 | ✅ **PASS** | Available for use |
| Inactive | 1 | ❌ FAIL | Not in use |
| Maintenance | 2 | ❌ FAIL | Being repaired |
| OutOfService | 3 | ❌ FAIL | Out of service |
| Retired | 4 | ❌ FAIL | No longer in fleet |
| Disposed | 5 | ❌ FAIL | Scrapped/sold |
| **InUse** | 6 | ❌ **FAIL** | **Being used by another work order** |

## Flow Diagram

```
┌─────────────────────────────────┐
│ User clicks "Start Work Order"  │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│ ValidateVehicleAvailability()   │
│ - Get schedules & expenses      │
│ - Extract vehicle IDs           │
│ - Check each vehicle status     │
└────────────┬────────────────────┘
             │
             ├──────────────┬──────────────┐
             │              │              │
             ▼              ▼              ▼
      All Active     Some InUse    Some Maintenance
         ✅              ❌              ❌
             │              │              │
             ▼              ▼              ▼
     ┌───────────┐  ┌───────────┐ ┌───────────┐
     │   START   │  │  BLOCKED  │ │  BLOCKED  │
     │ Work Order│  │ Show Error│ │ Show Error│
     │           │  │ Message   │ │ Message   │
     │ Set Status│  │           │ │           │
     │  = InUse  │  │Return     │ │Return     │
     └───────────┘  └───────────┘ └───────────┘
```

## Benefits

1. **Prevents Conflicts**: Cannot start a work order if vehicles are already in use
2. **Clear Feedback**: User knows exactly which vehicles are unavailable and why
3. **Actionable Errors**: Message tells user what to do (assign different vehicles)
4. **Audit Trail**: All validation attempts are logged
5. **Data Integrity**: Ensures vehicle status is always consistent
6. **Business Logic**: Enforces rule that vehicles can only be used by one work order at a time

## Resolution Workflow

When a work order is blocked:

1. **User sees error** listing unavailable vehicles
2. **User opens work order** in edit mode
3. **User goes to Schedule tab** or **Expenses tab**
4. **User edits schedule/expense** with unavailable vehicle
5. **User selects different vehicle** from dropdown (only Active vehicles shown)
6. **User clicks "Save Changes"**
7. **User tries "Start Work Order"** again
8. **Validation passes** → Work order starts successfully

## Build Status
✅ **Backend Build**: Successful (80.1s)

## Testing Checklist

### Test Case 1: Start with All Vehicles Available
- [ ] Create work order with 2 schedules, each with different Active vehicle
- [ ] Click "Start Work Order"
- [ ] **Expected**: Work order starts, both vehicles status = InUse

### Test Case 2: Start with Vehicle Already InUse
- [ ] Start work order WO-001 with Vehicle A (Vehicle A status = InUse)
- [ ] Create work order WO-002 with Vehicle A
- [ ] Try to start WO-002
- [ ] **Expected**: Error message "Vehicle A is currently InUse", work order does NOT start

### Test Case 3: Start with Vehicle in Maintenance
- [ ] Manually set Vehicle B status to Maintenance
- [ ] Create work order with Vehicle B
- [ ] Try to start work order
- [ ] **Expected**: Error message "Vehicle B is currently Maintenance", work order does NOT start

### Test Case 4: Replace Vehicle and Retry
- [ ] Create work order with Vehicle C (InUse)
- [ ] Try to start → Blocked
- [ ] Edit work order, change Vehicle C to Vehicle D (Active)
- [ ] Save changes
- [ ] Try to start again
- [ ] **Expected**: Work order starts successfully

### Test Case 5: Multiple Unavailable Vehicles
- [ ] Create work order with Vehicle E (InUse) and Vehicle F (Maintenance)
- [ ] Try to start work order
- [ ] **Expected**: Error lists both vehicles with their specific statuses

### Test Case 6: No Vehicles Assigned
- [ ] Create work order without any vehicles
- [ ] Try to start work order
- [ ] **Expected**: Work order starts successfully (no vehicles to validate)

## Related Files
- `src/ErpSystem.Core/Services/Maintenance/WorkOrderService.cs` - Validation logic
- `src/ErpSystem.Core/Enums/MaintenanceEnums.cs` - AssetStatus enum
- `VEHICLE_STATUS_MANAGEMENT_IMPLEMENTATION.md` - Vehicle status management
- `INUSE_STATUS_ADDITION.md` - InUse status documentation
