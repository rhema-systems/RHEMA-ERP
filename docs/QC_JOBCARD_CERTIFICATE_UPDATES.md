# QC JobCard Number and Certificate View Updates

## Overview
Enhanced the QC inspection system to display JobCard numbers and improved certificate download UX.

## Changes Implemented

### 1. Backend Changes

#### CompletedInspectionDto Enhancement
**File**: `src/ErpSystem.Api/Controllers/Maintenance/QualityControlController.cs`

Added `JobCardNumber` property to DTO:
```csharp
public class CompletedInspectionDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkOrderId { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string JobCardNumber { get; set; } = string.Empty; // NEW
    public string AssetName { get; set; } = string.Empty;
    public string ChecklistName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string OverallResult { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string? Notes { get; set; }
}
```

#### GetCompletedInspections Endpoint Update
Added JobCard loading:
```csharp
var workOrder = await _context.WorkOrders
    .Include(wo => wo.Asset)
    .Include(wo => wo.JobCard) // Added this
    .FirstOrDefaultAsync(wo => wo.Id == qc.WorkOrderId);

// Mapped to DTO
JobCardNumber = workOrder?.JobCard?.JobCardNumber ?? "N/A"
```

#### Certificate Service Update
**File**: `src/ErpSystem.Api/Services/QualityCertificateService.cs`

Added JobCard to work order query:
```csharp
var workOrder = await _context.WorkOrders
    .Include(wo => wo.Asset)
    .Include(wo => wo.WorkOrderType)
    .Include(wo => wo.MaintenanceType)
    .Include(wo => wo.PriorityLevel)
    .Include(wo => wo.JobCard) // Added this
    .FirstOrDefaultAsync(wo => wo.Id == qualityCheck.WorkOrderId);
```

Added JobCard to certificate:
```csharp
AddRow(table, "Work Order Number:", workOrder.WorkOrderNumber);
AddRow(table, "Job Card Number:", workOrder.JobCard?.JobCardNumber ?? "N/A"); // Added this
AddRow(table, "Title:", workOrder.Title);
```

### 2. Frontend Changes

#### Inspection Details Dialog
**File**: `frontend/src/app/maintenance/quality-control/page.tsx`

**Updated Layout**:
- Row 1: Work Order | Job Card Number
- Row 2: Asset | Checklist  
- Row 3: Inspection Date | (empty)
- Row 4: Result | Score
- Row 5: Inspector Notes (if present)

**Before**:
```typescript
Work Order    | Asset
Checklist     | Inspection Date
Result        | Score
```

**After**:
```typescript
Work Order         | Job Card Number
Asset              | Checklist
Inspection Date    | 
Result             | Score
```

#### Button Text Updates
Changed all certificate download buttons from "Certificate" or "Download Certificate" to **"View Certificate"**:

1. **In completed inspections table**:
   - Button text: `View Certificate`
   - Icon: FileText

2. **In inspection details dialog footer**:
   - Button text: `View Certificate`
   - Icon: FileText

### 3. Certificate PDF Enhancement

**Work Order Information Section** now includes:
```
Work Order Number:    WO-2025-001
Job Card Number:      JC-2025-123    ← NEW
Title:                Pump Maintenance
Asset:                Main Water Pump
Location:             Building A
Work Order Type:      Preventive
Maintenance Type:     Scheduled
Priority:             High
Completed Date:       2025-11-04
```

## User Experience

### Viewing Completed Inspection
1. Navigate to Quality Control Dashboard
2. Scroll to "Completed Inspections" section
3. Click eye icon to view details
4. **See JobCard Number** prominently displayed
5. Click "View Certificate" to download PDF
6. Certificate includes JobCard Number

### Certificate Download Flow
1. Click "View Certificate" button (table or dialog)
2. PDF downloads with authentication
3. Opens in browser or downloads
4. Certificate shows JobCard Number in Work Order section

## Benefits

### For Technicians
- ✅ Clear JobCard reference for tracking
- ✅ Link between work orders and job cards visible
- ✅ Easy certificate access with clear button labels

### For Quality Managers
- ✅ JobCard traceability in inspections
- ✅ Complete work order context
- ✅ Audit trail with JobCard references

### For Auditors
- ✅ JobCard numbers in inspection records
- ✅ Complete documentation chain
- ✅ Certificate includes JobCard for verification

## Technical Implementation

### Data Flow
```
WorkOrder
  ├─ JobCard
  │   └─ JobCardNumber
  ├─ Asset
  └─ QualityCheck
      └─ Certificate PDF
          └─ Shows JobCardNumber
```

### Backend Query Optimization
- Single query loads WorkOrder with JobCard
- Eager loading with `.Include(wo => wo.JobCard)`
- Null-safe access: `workOrder?.JobCard?.JobCardNumber ?? "N/A"`

### Frontend Display
- JobCard shown in dialog grid layout
- Falls back to "N/A" if no JobCard exists
- Consistent formatting across UI

## Testing Checklist

- [x] Backend builds successfully
- [ ] JobCard number appears in completed inspections API response
- [ ] JobCard number displays in inspection details dialog
- [ ] Certificate PDF includes JobCard number
- [ ] "View Certificate" button text displays correctly
- [ ] Certificate download works with authentication
- [ ] Null JobCard handles gracefully (shows "N/A")

## Notes
- JobCard is optional, so "N/A" shown when not present
- All certificate buttons now say "View Certificate" for consistency
- JobCard included in both UI and PDF certificate
