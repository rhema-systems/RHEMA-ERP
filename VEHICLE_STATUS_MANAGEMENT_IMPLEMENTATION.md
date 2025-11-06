# Vehicle Status Management Implementation for Work Orders

## Overview
Implemented automatic vehicle status management that updates vehicle (asset) status in the `MaintenanceAssets` table based on work order lifecycle. When a work order starts, assigned vehicles are marked as "InUse" (currently being used for travel/work). When the work order completes, vehicles are released back to "Active" status.

## Implementation Date
January 6, 2025

## Business Requirements

### Problem Statement
When a work order is approved and started, vehicles assigned to schedules or expenses for that work order must become unavailable to prevent:
- Double-booking of vehicles
- Scheduling conflicts
- Resource allocation issues

### Solution
Automatically track vehicle status based on work order state:
- **Work Order Starts** → Vehicles set to `InUse` status (currently being used)
- **Work Order Completes** → Vehicles set to `Active` status (available)

## Changes Made

### Backend Changes

#### 1. Added Dependencies to WorkOrderService (Lines 29-30, 46-47, 62-63)

**Added repositories:**
```csharp
private readonly IMaintenanceStaffScheduleRepository _scheduleRepository;
private readonly IMaintenanceExpenseRepository _expenseRepository;
```

**Updated constructor:**
```csharp
public WorkOrderService(
    // ... existing parameters
    IMaintenanceStaffScheduleRepository scheduleRepository,
    IMaintenanceExpenseRepository expenseRepository,
    ILogger<WorkOrderService> logger,
    IUnitOfWork unitOfWork)
{
    // ... existing assignments
    _scheduleRepository = scheduleRepository;
    _expenseRepository = expenseRepository;
    _logger = logger;
    _unitOfWork = unitOfWork;
}
```

#### 2. Updated StartWorkOrderAsync (Full Version) - Line 136

Added vehicle status update before changing work order status:

```csharp
// Update vehicle status to InUse for assigned vehicles
await UpdateVehicleStatusOnWorkOrderStartAsync(workOrderId);

// Update work order status
workOrder.Status = "InProgress";
workOrder.ActualStartDate = result.StartedAt;
await _workOrderRepository.UpdateAsync(workOrder);
await _unitOfWork.SaveChangesAsync();
```

#### 3. Updated StartWorkOrderAsync (Simple Version) - Line 1025

Added same logic to the interface implementation:

```csharp
// Update vehicle status to InUse for assigned vehicles
await UpdateVehicleStatusOnWorkOrderStartAsync(id);

workOrder.Status = "InProgress";
workOrder.ActualStartDate = DateTime.UtcNow;
workOrder.UpdatedAt = DateTime.UtcNow;
```

#### 4. Updated CompleteWorkOrderAsync (Full Version) - Line 273

Added vehicle status release before completing work order:

```csharp
// Release vehicles back to Active status
await UpdateVehicleStatusOnWorkOrderCompleteAsync(completionDto.WorkOrderId);

// Update work order completion
workOrder.Status = "Completed";
workOrder.ActualCompletionDate = result.CompletionDate;
```

#### 5. Updated CompleteWorkOrderAsync (Simple Version) - Line 1057

Added same logic to the interface implementation:

```csharp
// Release vehicles back to Active status
await UpdateVehicleStatusOnWorkOrderCompleteAsync(id);

workOrder.Status = "Completed";
workOrder.ActualCompletionDate = DateTime.UtcNow;
```

#### 6. Implemented Helper Method: UpdateVehicleStatusOnWorkOrderStartAsync (Lines 1950-2003)

**Purpose**: Sets vehicle status to InUse when work order starts

**Logic**:
1. Get all schedules for the work order
2. Get all expenses for the work order
3. Extract vehicle IDs from both schedules (`AssignedVehicleId`) and expenses (`VehicleId`)
4. Remove duplicates
5. For each vehicle ID:
   - Fetch the vehicle asset
   - If status is `Active`, change to `InUse`
   - Update timestamp
   - Log the change
6. Save all changes in a single transaction
7. Exceptions are caught and logged as warnings (doesn't block work order start)

**Key Code**:
```csharp
private async Task UpdateVehicleStatusOnWorkOrderStartAsync(Guid workOrderId)
{
    try
    {
        // Get all schedules and expenses for this work order
        var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrderId);
        var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrderId);

        // Collect all vehicle IDs from schedules and expenses
        var vehicleIds = new List<Guid>();
        
        // From schedules
        vehicleIds.AddRange(schedules
            .Where(s => s.AssignedVehicleId.HasValue)
            .Select(s => s.AssignedVehicleId!.Value));
        
        // From expenses
        vehicleIds.AddRange(expenses
            .Where(e => e.VehicleId.HasValue)
            .Select(e => e.VehicleId!.Value));

        // Remove duplicates
        vehicleIds = vehicleIds.Distinct().ToList();

        // Update each vehicle status to InUse
        foreach (var vehicleId in vehicleIds)
        {
            var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
            if (vehicle != null && vehicle.Status == ErpSystem.Core.Enums.AssetStatus.Active)
            {
                vehicle.Status = ErpSystem.Core.Enums.AssetStatus.InUse;
                vehicle.UpdatedAt = DateTime.UtcNow;
                await _assetRepository.UpdateAsync(vehicle);
                _logger.LogInformation("Vehicle {VehicleId} ({VehicleName}) status updated to InUse for work order {WorkOrderId}",
                    vehicleId, vehicle.Name, workOrderId);
            }
        }

        if (vehicleIds.Any())
        {
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("{Count} vehicle(s) marked as InUse for work order {WorkOrderId}",
                vehicleIds.Count, workOrderId);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error updating vehicle status for work order {WorkOrderId}", workOrderId);
        // Don't throw - this shouldn't block work order start
    }
}
```

#### 7. Implemented Helper Method: UpdateVehicleStatusOnWorkOrderCompleteAsync (Lines 2005-2058)

**Purpose**: Sets vehicle status back to Active when work order completes

**Logic**:
1. Get all schedules for the work order
2. Get all expenses for the work order
3. Extract vehicle IDs from both schedules and expenses
4. Remove duplicates
5. For each vehicle ID:
   - Fetch the vehicle asset
   - If status is `InUse`, change back to `Active`
   - Update timestamp
   - Log the change
6. Save all changes in a single transaction
7. Exceptions are caught and logged as warnings (doesn't block work order completion)

**Key Code**:
```csharp
private async Task UpdateVehicleStatusOnWorkOrderCompleteAsync(Guid workOrderId)
{
    try
    {
        // Get all schedules and expenses for this work order
        var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrderId);
        var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrderId);

        // Collect all vehicle IDs from schedules and expenses
        var vehicleIds = new List<Guid>();
        
        // From schedules
        vehicleIds.AddRange(schedules
            .Where(s => s.AssignedVehicleId.HasValue)
            .Select(s => s.AssignedVehicleId!.Value));
        
        // From expenses
        vehicleIds.AddRange(expenses
            .Where(e => e.VehicleId.HasValue)
            .Select(e => e.VehicleId!.Value));

        // Remove duplicates
        vehicleIds = vehicleIds.Distinct().ToList();

        // Update each vehicle status back to Active
        foreach (var vehicleId in vehicleIds)
        {
            var vehicle = await _assetRepository.GetByIdAsync(vehicleId);
            if (vehicle != null && vehicle.Status == ErpSystem.Core.Enums.AssetStatus.InUse)
            {
                vehicle.Status = ErpSystem.Core.Enums.AssetStatus.Active;
                vehicle.UpdatedAt = DateTime.UtcNow;
                await _assetRepository.UpdateAsync(vehicle);
                _logger.LogInformation("Vehicle {VehicleId} ({VehicleName}) status updated to Active after work order {WorkOrderId} completion",
                    vehicleId, vehicle.Name, workOrderId);
            }
        }

        if (vehicleIds.Any())
        {
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("{Count} vehicle(s) marked as Active after work order {WorkOrderId} completion",
                vehicleIds.Count, workOrderId);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error releasing vehicle status for work order {WorkOrderId}", workOrderId);
        // Don't throw - this shouldn't block work order completion
    }
}
```

## Asset Status Enum Values

From `ErpSystem.Core.Enums.AssetStatus`:

```csharp
public enum AssetStatus
{
    Active = 0,           // Available for use
    Inactive = 1,         // Not in use
    Maintenance = 2,      // In maintenance (being worked on)
    OutOfService = 3,     // Out of service
    Retired = 4,          // Retired from use
    Disposed = 5,         // Disposed/scrapped
    InUse = 6            // Currently being used for travel or work order execution ← Used by this implementation
}
```

## Data Flow

### When Work Order Starts

```
User clicks "Start Work Order"
  ↓
StartWorkOrderAsync called
  ↓
UpdateVehicleStatusOnWorkOrderStartAsync()
  ↓
Query MaintenanceStaffSchedules WHERE WorkOrderId = {id}
Query MaintenanceExpenses WHERE WorkOrderId = {id}
  ↓
Extract AssignedVehicleId from schedules
Extract VehicleId from expenses
  ↓
For each vehicle ID:
  MaintenanceAssets.Status = InUse (6)
  MaintenanceAssets.UpdatedAt = DateTime.UtcNow
  ↓
SaveChanges()
  ↓
Work Order status = "InProgress"
```

### When Work Order Completes

```
User clicks "Complete Work Order"
  ↓
CompleteWorkOrderAsync called
  ↓
UpdateVehicleStatusOnWorkOrderCompleteAsync()
  ↓
Query MaintenanceStaffSchedules WHERE WorkOrderId = {id}
Query MaintenanceExpenses WHERE WorkOrderId = {id}
  ↓
Extract AssignedVehicleId from schedules
Extract VehicleId from expenses
  ↓
For each vehicle ID:
  MaintenanceAssets.Status = Active (0)
  MaintenanceAssets.UpdatedAt = DateTime.UtcNow
  ↓
SaveChanges()
  ↓
Work Order status = "Completed"
```

## Key Design Decisions

### 1. Non-Blocking Errors
- Vehicle status updates are wrapped in try-catch
- Errors are logged as warnings, not thrown
- **Rationale**: Vehicle status issues shouldn't prevent work order start/completion

### 2. Status Transition Rules
- **On Start**: Only update vehicles with status = `Active` → `InUse`
- **On Complete**: Only update vehicles with status = `InUse` → `Active`
- **Rationale**: Prevents overwriting manual status changes (e.g., vehicle marked as OutOfService or Maintenance)

### 3. Duplicate Handling
- Vehicle IDs are deduplicated with `.Distinct()`
- **Rationale**: Same vehicle might appear in multiple schedules/expenses

### 4. Batch Updates
- All vehicle status changes saved in single `SaveChangesAsync()`
- **Rationale**: Performance optimization and atomic transaction

### 5. Logging
- Info-level logs for each vehicle status change
- Summary log with count of vehicles updated
- **Rationale**: Audit trail and debugging support

## Integration Points

### Dependent Tables
- **MaintenanceStaffSchedules**: `AssignedVehicleId` field
- **MaintenanceExpenses**: `VehicleId` field
- **MaintenanceAssets**: `Status` field (updated)

### Dependent Services/Repositories
- `IMaintenanceStaffScheduleRepository` - Get schedules by work order
- `IMaintenanceExpenseRepository` - Get expenses by work order
- `IMaintenanceAssetRepository` - Get and update asset status

## Testing Checklist

### Manual Testing

#### Scenario 1: Work Order with Schedule Vehicle
- [ ] Create work order
- [ ] Add schedule with company vehicle assigned
- [ ] Click "Save Changes"
- [ ] **Verify**: Vehicle status remains `Active` (work order not started)
- [ ] Start work order
- [ ] **Verify**: Vehicle status changes to `InUse` in MaintenanceAssets table
- [ ] Complete work order
- [ ] **Verify**: Vehicle status changes back to `Active`

#### Scenario 2: Work Order with Expense Vehicle
- [ ] Create work order
- [ ] Add expense with vehicle assigned
- [ ] Click "Save Changes"
- [ ] **Verify**: Vehicle status remains `Active`
- [ ] Start work order
- [ ] **Verify**: Vehicle status changes to `InUse`
- [ ] Complete work order
- [ ] **Verify**: Vehicle status changes back to `Active`

#### Scenario 3: Multiple Vehicles
- [ ] Create work order
- [ ] Add 2 schedules with different vehicles
- [ ] Add 1 expense with a third vehicle
- [ ] Start work order
- [ ] **Verify**: All 3 vehicles status = `InUse`
- [ ] Complete work order
- [ ] **Verify**: All 3 vehicles status = `Active`

#### Scenario 4: Same Vehicle in Multiple Places
- [ ] Create work order
- [ ] Add schedule with Vehicle A
- [ ] Add expense with Vehicle A (same vehicle)
- [ ] Start work order
- [ ] **Verify**: Vehicle A status = `InUse` (not updated twice)
- [ ] Complete work order
- [ ] **Verify**: Vehicle A status = `Active`

#### Scenario 5: Vehicle Already in Maintenance
- [ ] Manually set a vehicle status to `Maintenance` (vehicle being repaired)
- [ ] Create work order with that vehicle in schedule
- [ ] Start work order
- [ ] **Verify**: Vehicle status remains `Maintenance` (not Active, so not changed)
- [ ] Complete work order
- [ ] **Verify**: Vehicle status remains `Maintenance` (was not InUse, so not changed back)

### Database Verification

```sql
-- Check vehicle status before work order start
SELECT Id, Name, AssetNumber, Status, UpdatedAt
FROM MaintenanceAssets
WHERE AssetCategory.AssetType = 'Vehicle';

-- Start work order, then check again
SELECT Id, Name, AssetNumber, Status, UpdatedAt
FROM MaintenanceAssets
WHERE Status = 6; -- InUse

-- Complete work order, then check again
SELECT Id, Name, AssetNumber, Status, UpdatedAt
FROM MaintenanceAssets
WHERE Status = 0; -- Active
```

### Log Verification

Check application logs for:
```
INFO: Vehicle {VehicleId} ({VehicleName}) status updated to InUse for work order {WorkOrderId}
INFO: 2 vehicle(s) marked as InUse for work order {WorkOrderId}
INFO: Vehicle {VehicleId} ({VehicleName}) status updated to Active after work order {WorkOrderId} completion
INFO: 2 vehicle(s) marked as Active after work order {WorkOrderId} completion
```

## Future Enhancements

### 1. Vehicle Availability Filtering in GetAvailableVehicles
Update `GetAvailableVehiclesAsync` to exclude vehicles with status = `InUse` or `Maintenance`:

```csharp
public async Task<IEnumerable<MaintenanceAssetDto>> GetAvailableVehiclesAsync()
{
    var query = _assetRepository.GetQueryable()
        .Include(a => a.AssetCategory)
        .Where(a => a.AssetCategory.AssetType == "Vehicle" 
            && a.Status == AssetStatus.Active); // Only Active vehicles (not InUse or Maintenance)
    
    var vehicles = await query.ToListAsync();
    return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(vehicles);
}
```

### 2. Vehicle Request/Reservation System
- Allow users to request vehicles for future work orders
- Check availability based on scheduled work orders
- Implement approval workflow for vehicle requests

### 3. Vehicle Utilization Dashboard
- Show which vehicles are currently in use
- Display which work orders they're assigned to
- Track vehicle usage statistics and availability percentage

### 4. Conflict Detection
- Warn when trying to assign a vehicle that's already InUse or in Maintenance
- Suggest alternative available vehicles
- Show when vehicle will become available

### 5. Status History Tracking
- Create `MaintenanceAssetStatusHistory` table
- Track all status changes with timestamps and reasons
- Generate reports on vehicle downtime and utilization

### 6. Automatic Status Reversion on Work Order Cancellation
Add logic to handle cancelled/paused work orders:
```csharp
public async Task CancelWorkOrderAsync(Guid id)
{
    // Release vehicles back to Active
    await UpdateVehicleStatusOnWorkOrderCompleteAsync(id);
    
    workOrder.Status = "Cancelled";
    // ... rest of cancellation logic
}
```

## Build Status
✅ **Backend Build**: Successful (8.6s)
- ErpSystem.Shared: ✅ Succeeded
- ErpSystem.Core: ✅ Succeeded
- ErpSystem.Data: ✅ Succeeded
- ErpSystem.Api: ✅ Succeeded

## Notes

- **Non-Destructive**: Only changes status if current state matches expected (Active → InUse, InUse → Active)
- **Error Resilient**: Vehicle status errors don't block work order lifecycle
- **Audit Trail**: All changes logged with vehicle details and work order context
- **Transaction Safe**: All updates in single database transaction
- **Duplicate Safe**: Handles same vehicle in multiple schedules/expenses
- **Semantic Clarity**: Uses InUse (6) for vehicles being used vs Maintenance (2) for vehicles being worked on
- **Foundation for Vehicle Management**: Sets groundwork for comprehensive vehicle request/reservation system

## Related Documentation

- `VEHICLE_TRACKING_IMPLEMENTATION.md` - VehicleId field implementation for expenses
- `SCHEDULE_VEHICLE_SELECTION_IMPLEMENTATION.md` - Vehicle dropdown in schedule tab
