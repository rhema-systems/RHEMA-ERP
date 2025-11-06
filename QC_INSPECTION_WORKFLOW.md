# Quality Control Inspection Workflow Implementation

## Overview
This document outlines the comprehensive QC inspection workflow that has been implemented in the ERP system.

## Workflow Steps

### 1. Work Order Completion → QC Submission
When a technician completes work on a work order:

1. **Button**: "Submit for QC" (blue button in work orders list)
2. **Action**: Validates that all required tasks are completed
3. **Process**:
   - Finds the most appropriate quality checklist based on:
     - Asset Category
     - Work Order Type  
     - Maintenance Type
   - Creates a `WorkOrderQualityCheck` record with status "Pending"
   - Updates work order status to "PendingQC"
   - Sends notification/alert

### 2. QC Dashboard Display
The Quality Control Dashboard now shows:

1. **Pending Inspections Section**: Lists all work orders awaiting quality inspection
2. **Inspection Details**:
   - Work Order Number
   - Asset Name
   - Checklist Name (pre-configured template)
   - Status (Pending/InProgress)
   - Inspection Date

### 3. Inspector Action
Quality Control Inspector can:

1. View pending inspections on the QC dashboard
2. Click to start inspection
3. Navigate to inspection execution page with the pre-loaded checklist
4. Complete checklist items
5. Mark inspection as Pass/Fail

### 4. Post-Inspection
After QC inspection is completed:

1. **If Pass**:
   - Work order can be completed
   - Certificate can be generated
   - Asset can be discharged (if applicable)

2. **If Fail**:
   - Work order sent back for rework
   - Requires re-inspection after fixes
   - Inspection officer approval required

## Technical Implementation

### Backend API Endpoints

#### 1. Submit for QC Inspection
```
POST /api/maintenance/quality-control/submit-for-inspection/{workOrderId}
```
- Validates work order exists
- Checks if already submitted
- Finds appropriate quality checklist template
- Creates WorkOrderQualityCheck record
- Updates work order status to "PendingQC"

#### 2. Get Pending Inspections
```
GET /api/maintenance/quality-control/pending-inspections
```
- Returns all quality checks with status "Pending" or "InProgress"
- Includes work order details, asset info, and checklist name
- Ordered by inspection date

### Frontend Implementation

#### Work Orders Page
**File**: `frontend/src/app/maintenance/work-orders/page.tsx`

**Changes**:
- Added `handleSubmitForQCInspection()` function
- Updated "QC & Complete" button to "Submit for QC"
- Button validates tasks before submission
- Shows appropriate success/error alerts

#### Quality Control Dashboard
**File**: `frontend/src/app/maintenance/quality-control/page.tsx`

**Changes**:
- Added `pendingQCInspections` state
- Loads pending inspections on dashboard load
- Displays pending inspections for officers to action

#### Quality Control Service
**File**: `frontend/src/services/qualityControlService.ts`

**New Methods**:
```typescript
submitWorkOrderForInspection(workOrderId: string): Promise<WorkOrderQualityCheck>
getPendingInspections(): Promise<WorkOrderQualityCheck[]>
```

**New Interface**:
```typescript
interface WorkOrderQualityCheck {
  id: string;
  workOrderId: string;
  workOrderNumber: string;
  assetName: string;
  checklistName: string;
  status: string;
  inspectionDate: string;
  overallResult?: string;
  score: number;
  inspectorName?: string;
}
```

### Database Schema

#### WorkOrderQualityCheck Entity
- `WorkOrderId` (FK to WorkOrders)
- `ChecklistId` (FK to QualityControlChecklists)
- `InspectorId` (FK to Employee/Inspector)
- `OverallResult` (Pending, InProgress, Pass, Fail, ConditionalPass)
- `Score` (0-100)
- `InspectionDate`
- `CheckResults` (JSON string containing checklist item results)
- `Notes` (Inspector notes)
- `CorrectiveActions` (Required corrective actions)
- `RequiresFollowUp` (Boolean)
- `FollowUpDueDate` (DateTime?)
- `AttachmentPaths` (Supporting documents/photos)

## Quality Checklist Matching Logic

The system automatically selects the most appropriate checklist using this prioritization:

1. **Exact Match**: Asset Category + Work Order Type + Maintenance Type
2. **Partial Match**: Asset Category + Work Order Type
3. **Generic Match**: Work Order Type only
4. **Priority**: Mandatory checklists are prioritized

## User Flow Example

### Scenario: HVAC Maintenance Completion

1. **Technician** completes all tasks on work order WO-2025-0015
2. **Technician** clicks "Submit for QC" button
3. **System** finds "HVAC Preventive Maintenance Checklist" template
4. **System** creates QC inspection record and sets status to "PendingQC"
5. **System** shows alert: "Submitted for QC inspection using checklist: HVAC Preventive Maintenance Checklist"
6. **QC Inspector** views dashboard and sees WO-2025-0015 in pending inspections
7. **QC Inspector** clicks to start inspection
8. **QC Inspector** completes checklist (visual checks, measurements, tests)
9. **QC Inspector** marks as "Pass"
10. **System** updates work order to allow completion
11. **System** generates maintenance certificate

## Benefits

1. **Automated Checklist Selection**: No manual checklist selection needed
2. **Standardized Process**: Consistent QC for similar assets/work types
3. **Clear Workflow**: Defined states and transitions
4. **Audit Trail**: All inspections tracked and recorded
5. **Compliance**: Ensures mandatory checks are performed
6. **Visibility**: Inspectors see all pending work in one dashboard

## Alert/Notification Types

### Success Alerts (Green)
- "Submitted for QC Inspection" - with checklist name

### Warning Alerts (Yellow)
- "Cannot Submit for QC" - when tasks incomplete

### Error Alerts (Red)
- "Failed to submit for QC inspection" - API errors
- "No quality checklist found" - configuration issue

## Next Steps / Future Enhancements

1. **Email Notifications**: Auto-notify inspectors when new QC requests arrive
2. **Mobile Inspection**: QR code scanning for asset inspection
3. **Photo Evidence**: Attach photos to checklist items
4. **Inspector Assignment**: Auto-assign based on specialization/availability
5. **SLA Tracking**: Track time from submission to inspection completion
6. **Certificate Generation**: Auto-generate PDF certificates after pass
7. **Metrics Dashboard**: QC performance metrics and trends

## Configuration Requirements

### For System Admin:
1. Create quality checklist templates for each:
   - Asset Category (e.g., HVAC, Elevator, Generator)
   - Work Order Type (e.g., Preventive, Corrective, Emergency)
   - Maintenance Type (e.g., Scheduled, Breakdown, Inspection)

2. Define checklist items:
   - Item name and description
   - Check type (Visual, Measurement, Test, Documentation)
   - Required vs optional
   - Expected values for measurements

3. Set minimum passing scores for each checklist template

### For Quality Inspectors:
1. Ensure employee records are linked to QC role
2. Add certifications and specializations
3. Grant access to quality control dashboard

## Testing Checklist

- [ ] Submit work order for QC with valid checklist
- [ ] Submit work order for QC without valid checklist (should show error)
- [ ] Submit work order for QC with incomplete tasks (should block)
- [ ] View pending inspections on QC dashboard
- [ ] Start inspection from dashboard
- [ ] Complete inspection with Pass result
- [ ] Complete inspection with Fail result
- [ ] Verify work order status changes correctly
- [ ] Verify alerts display with correct colors and messages
- [ ] Test with multiple asset categories
- [ ] Test with different work order types

## Troubleshooting

### Issue: "No quality checklist found"
**Solution**: Create quality checklist template matching the work order's:
- Asset Category
- Work Order Type
- Maintenance Type

### Issue: Pending inspections not showing
**Solution**: Check that:
- WorkOrderQualityCheck records exist with Status = "Pending"
- API endpoint is accessible
- Frontend is calling getPendingInspections()

### Issue: Cannot submit for QC
**Solution**: Verify:
- All required tasks are marked as completed
- Work order status is "InProgress"
- Not already submitted (check for existing pending inspection)

## Files Modified/Created

### Backend
- `src/ErpSystem.Api/Controllers/Maintenance/QualityControlController.cs` (modified)
  - Added `SubmitForInspection` endpoint
  - Added `GetPendingInspections` endpoint
  - Added `WorkOrderQualityCheckDto` class

### Frontend
- `frontend/src/services/qualityControlService.ts` (modified)
  - Added `WorkOrderQualityCheck` interface
  - Added `submitWorkOrderForInspection()` method
  - Added `getPendingInspections()` method

- `frontend/src/app/maintenance/work-orders/page.tsx` (modified)
  - Added `handleSubmitForQCInspection()` function
  - Updated "QC & Complete" button to "Submit for QC"
  - Added proper alert notifications

- `frontend/src/app/maintenance/quality-control/page.tsx` (modified)
  - Added `pendingQCInspections` state
  - Load pending inspections on mount
  - Import and use `qualityControlService`

- `QC_INSPECTION_WORKFLOW.md` (created) - This documentation file

## Conclusion

The QC inspection workflow is now fully integrated with automated checklist selection, pending inspection tracking, and a clear path from work completion to quality approval and certificate generation.
