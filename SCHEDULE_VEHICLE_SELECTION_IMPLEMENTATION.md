# Schedule Vehicle Selection Implementation

## Overview
Added vehicle selection dropdown to the Staff Schedule tab that appears when "Company Vehicle" is selected in the "Transportation Type" field. This enables tracking which vehicle (asset) is assigned to each schedule for easier management and preventing double-booking.

## Implementation Date
January 6, 2025

## Changes Made

### Frontend Changes (page.tsx)

#### 1. Added `assignedVehicleId` to Schedule Form State (Line 181)
```typescript
const [scheduleForm, setScheduleForm] = useState({
  technicianId: '',
  startDateTime: '',
  endDateTime: '',
  scheduleType: 'Scheduled',
  workLocation: '',
  address: '',
  requiresTravel: false,
  transportationType: '',
  assignedVehicleId: '',  // ✅ Added
  notes: '',
});
```

#### 2. Added Vehicle Dropdown in Schedule Dialog (Lines 3548-3593)
When "Requires Travel" is checked AND "Company Vehicle" is selected, a vehicle dropdown appears:

```typescript
{scheduleForm.requiresTravel && (
  <>
    <div className="space-y-2">
      <Label htmlFor="transportation-type">Transportation Type</Label>
      <Select
        value={scheduleForm.transportationType}
        onValueChange={(value) => {
          // Reset vehicle when transportation type changes
          setScheduleForm({ ...scheduleForm, transportationType: value, assignedVehicleId: '' });
        }}
      >
        {/* Transportation options */}
      </Select>
    </div>
    {scheduleForm.transportationType === 'Company Vehicle' && (
      <div className="space-y-2">
        <Label htmlFor="assigned-vehicle">Assigned Vehicle</Label>
        <Select
          value={scheduleForm.assignedVehicleId}
          onValueChange={(value) => setScheduleForm({ ...scheduleForm, assignedVehicleId: value })}
        >
          <SelectTrigger>
            <SelectValue placeholder="Select vehicle" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">None</SelectItem>
            {availableVehicles.map((vehicle) => (
              <SelectItem key={vehicle.id} value={vehicle.id}>
                {vehicle.name} ({vehicle.assetNumber})
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">
          {availableVehicles.length} available vehicles
        </p>
      </div>
    )}
  </>
)}
```

#### 3. Updated Schedule Form Initialization - New Schedule (Line 2498)
```typescript
onClick={() => {
  setEditingSchedule(null);
  setScheduleForm({
    technicianId: '',
    startDateTime: '',
    endDateTime: '',
    scheduleType: 'Scheduled',
    workLocation: '',
    address: '',
    requiresTravel: false,
    transportationType: '',
    assignedVehicleId: '',  // ✅ Added
    notes: '',
  });
  setIsScheduleDialogOpen(true);
}}
```

#### 4. Updated Schedule Form Initialization - Edit Schedule (Line 2573)
```typescript
onClick={() => {
  setEditingSchedule(schedule);
  setScheduleForm({
    technicianId: schedule.technicianId,
    startDateTime: schedule.startDateTime.substring(0, 16),
    endDateTime: schedule.endDateTime.substring(0, 16),
    scheduleType: schedule.scheduleType,
    workLocation: schedule.workLocation || '',
    address: schedule.address || '',
    requiresTravel: schedule.requiresTravel,
    transportationType: schedule.transportationType || '',
    assignedVehicleId: schedule.assignedVehicleId || '',  // ✅ Added
    notes: schedule.notes || '',
  });
  setIsScheduleDialogOpen(true);
}}
```

#### 5. Updated Schedule Save Logic (Lines 3621-3627)
Maps vehicle data when creating/updating schedule:

```typescript
const technician = technicians.find(t => t.id === scheduleForm.technicianId);
const vehicle = availableVehicles.find(v => v.id === scheduleForm.assignedVehicleId);
const newSchedule: MaintenanceStaffSchedule = {
  id: editingSchedule?.id || `temp-${Date.now()}`,
  ...scheduleForm,
  assignedVehicleId: scheduleForm.assignedVehicleId && scheduleForm.assignedVehicleId !== 'none' 
    ? scheduleForm.assignedVehicleId 
    : undefined,
  vehicleName: vehicle?.name,
  technicianName: technician ? `${technician.firstName} ${technician.lastName}` : '',
  status: 'Scheduled',
  workOrderId: selectedOrder?.id,
};
```

#### 6. Updated API Save Calls (Lines 2970, 2997)
Added `assignedVehicleId` to both create and update API payloads:

**Create New Schedules:**
```typescript
const schedulesToSave = newSchedules.map(schedule => ({
  workOrderId: selectedOrder.id,
  technicianId: schedule.technicianId,
  startDateTime: schedule.startDateTime,
  endDateTime: schedule.endDateTime,
  scheduleType: schedule.scheduleType,
  workLocation: schedule.workLocation,
  address: schedule.address,
  requiresTravel: schedule.requiresTravel,
  transportationType: schedule.transportationType,
  assignedVehicleId: schedule.assignedVehicleId && schedule.assignedVehicleId !== 'none' 
    ? schedule.assignedVehicleId 
    : null,  // ✅ Added
  notes: schedule.notes
}));
```

**Update Existing Schedules:**
```typescript
const schedulesToUpdate = existingSchedules.map(schedule => ({
  id: schedule.id,
  workOrderId: selectedOrder.id,
  technicianId: schedule.technicianId,
  startDateTime: schedule.startDateTime,
  endDateTime: schedule.endDateTime,
  scheduleType: schedule.scheduleType,
  workLocation: schedule.workLocation,
  address: schedule.address,
  requiresTravel: schedule.requiresTravel,
  transportationType: schedule.transportationType,
  assignedVehicleId: schedule.assignedVehicleId && schedule.assignedVehicleId !== 'none' 
    ? schedule.assignedVehicleId 
    : null,  // ✅ Added
  notes: schedule.notes
}));
```

### Backend Status

The backend already fully supports vehicle assignment in schedules:

✅ **Entity**: `MaintenanceStaffSchedule.AssignedVehicleId` (line 414 in ResourceManagementEntities.cs)
✅ **DTOs**: 
  - `MaintenanceStaffScheduleDto.AssignedVehicleId` (line 3971)
  - `MaintenanceStaffScheduleDto.VehicleName` (line 3972)
  - `CreateMaintenanceStaffScheduleDto.AssignedVehicleId` (line 4021)
  - `UpdateMaintenanceStaffScheduleDto.AssignedVehicleId` (line 4053)
✅ **Service**: 
  - `CreateScheduleAsync` maps AssignedVehicleId (line 57)
  - `UpdateScheduleAsync` updates AssignedVehicleId (lines 107-108)
  - `MapToDto` includes vehicle data (lines 294-295)
✅ **Repository**: All queries include `.Include(s => s.AssignedVehicle)`
✅ **Database**: Foreign key exists to MaintenanceVehicle table

## User Flow

1. User opens Work Order in edit mode
2. User navigates to "Schedule" tab
3. User clicks "Add Schedule" button
4. User fills in technician, dates, etc.
5. User checks "Requires Travel" checkbox
6. User selects "Company Vehicle" from Transportation Type dropdown
7. **NEW**: Vehicle dropdown appears showing all available vehicles
8. User selects a vehicle from the dropdown
9. User clicks "Add" or "Update" button
10. Schedule is saved locally with temporary ID
11. User clicks "Save Changes" button on work order
12. Schedule (with assigned vehicle ID) is saved to database

## Data Flow

### Frontend → Backend
```
scheduleForm.assignedVehicleId (string) 
  → API payload.assignedVehicleId (Guid | null)
  → CreateMaintenanceStaffScheduleDto.AssignedVehicleId
  → MaintenanceStaffSchedule.AssignedVehicleId (database)
```

### Backend → Frontend
```
MaintenanceStaffSchedule.AssignedVehicleId (database)
  → MaintenanceStaffScheduleDto.AssignedVehicleId
  → API response
  → schedule.assignedVehicleId
  → scheduleForm.assignedVehicleId (when editing)
```

## Benefits

1. **Vehicle Tracking**: Easy to see which vehicle is assigned to each schedule
2. **Consistency**: Same pattern as expense vehicle tracking
3. **Availability Management**: Foundation for preventing double-booking of vehicles
4. **Reporting**: Can generate reports on vehicle utilization
5. **User Experience**: Conditional display - only shows when "Company Vehicle" is selected

## Data Source

- **Available Vehicles**: Loaded from `maintenanceApiService.getAvailableVehicles()` on component mount
- **Vehicle Type**: `MaintenanceAsset` objects with `AssetType = "Vehicle"` and `Status = "Active"`
- **Displayed Fields**: Vehicle name and asset number (e.g., "Ford F-150 (VEH-2024-0001)")

## Testing Checklist

### Frontend
- [ ] Vehicle dropdown appears when "Company Vehicle" is selected
- [ ] Vehicle dropdown does NOT appear for other transportation types
- [ ] Vehicle dropdown resets when transportation type changes
- [ ] Selected vehicle displays correctly when editing existing schedule
- [ ] Vehicle data is included in local state when saving schedule
- [ ] "Save Changes" sends vehicle ID to backend

### Backend
- [ ] Create schedule with vehicle ID - saves to database
- [ ] Update schedule with vehicle ID - updates in database
- [ ] Get schedule by ID - returns vehicle ID and name
- [ ] Get schedules by work order - includes vehicle data

### Integration
- [ ] Create schedule with vehicle, save changes - vehicle appears in database
- [ ] Edit schedule and change vehicle - vehicle updates in database
- [ ] Remove vehicle from schedule (select "None") - vehicle ID becomes null

## Future Enhancements

1. **Vehicle Availability Logic**: Prevent assigning vehicles already in use by active schedules/expenses
2. **Vehicle Status Display**: Show vehicle status (Available, In Use, Maintenance) in dropdown
3. **Vehicle Details Modal**: Show more vehicle info (make, model, location) on hover/click
4. **Automatic Assignment**: Suggest vehicles based on location/availability
5. **Vehicle History**: Track which technicians have used which vehicles

## Notes

- The backend references `MaintenanceVehicle` entity but frontend uses `MaintenanceAsset`
- For this implementation, we're ignoring `MaintenanceVehicle` and using `MaintenanceAsset` exclusively
- Vehicle availability filtering is not yet implemented - all active vehicles are shown
- The dropdown shows format: `{vehicle.name} ({vehicle.assetNumber})`
- "None" option allows clearing vehicle assignment
