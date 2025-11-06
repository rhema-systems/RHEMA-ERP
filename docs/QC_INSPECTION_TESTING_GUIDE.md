# Quality Control Inspection - Complete Testing Guide

## Implementation Summary

### ✅ Completed Features:

1. **Backend API Endpoints**
   - `POST /api/maintenance/quality-control/update-item-result` - Save Pass/Fail for checklist items
   - `POST /api/maintenance/quality-control/complete-inspection` - Complete inspection with score calculation
   - `POST /api/maintenance/quality-control/reject-for-rework` - Reject work order and create rework record
   - `GET /api/maintenance/quality-control/certificate/{qualityCheckId}` - Generate PDF certificate

2. **Frontend Inspection Execution UI**
   - Load inspection data with work order details
   - Display checklist items one-by-one with navigation
   - Pass/Fail/N/A buttons with auto-save
   - Progress tracking (X/Y items completed, pass/fail counts)
   - General notes textarea
   - Complete/Reject dialogs
   - Auto-redirect after completion

3. **Certificate Generation (PDF)**
   - Professional PDF certificate with QuestPDF
   - Work order information section
   - Inspection details with score
   - Complete checklist results table
   - Pass/Fail badge
   - Signature section
   - Auto-download after successful completion

4. **Rejection & Rework Workflow**
   - Rejection dialog with reason and corrective actions
   - Severity selection (Low/Medium/High/Critical)
   - Creates WorkOrderRework record
   - Changes work order status to "Rework"
   - Updates quality check to "Fail"

---

## Testing Steps

### Prerequisites
1. Backend running on `http://localhost:5000`
2. Frontend running on `http://localhost:3000`
3. User logged in with QC inspector permissions
4. At least one work order with status "PendingQC"

### Test Scenario 1: Complete Inspection (Pass)

**Steps:**
1. Navigate to **Quality Control Dashboard** (`/maintenance/quality-control`)
2. Verify pending inspection appears in "Pending QC Inspections" card
3. Click **"Start"** button on the pending work order
4. Should navigate to inspection page showing:
   - Work order information (asset, location, priority, checklist)
   - Progress bar (0/X items)
   - Current checklist item with description
   - Pass/Fail/N/A buttons

5. **Mark all items as Pass:**
   - Click "Pass" for each item
   - Verify toast notification: "Saved - Item marked as Pass"
   - Verify progress updates automatically
   - Use "Next" button to advance through items

6. **Complete Inspection:**
   - After all items marked, "Complete Inspection" button should be enabled
   - Add general notes (optional)
   - Click "Complete Inspection"
   - Confirm in dialog
   - **Expected**: 
     - Score calculated (100% if all passed)
     - Overall result: "Pass"
     - Work order status updated to "Completed"
     - Certificate automatically downloaded as PDF
     - Redirected to QC dashboard

7. **Verify Certificate:**
   - Open downloaded PDF
   - Check contains: work order details, inspection results, checklist items, Pass badge
   - Verify all items show "Pass" in green

### Test Scenario 2: Complete Inspection (Fail)

**Steps:**
1. Start a new inspection
2. **Mark some items as Fail:**
   - Mark first 2-3 items as "Pass"
   - Mark remaining items as "Fail"
   - Verify progress shows pass/fail counts

3. **Complete Inspection:**
   - Click "Complete Inspection"
   - Confirm in dialog
   - **Expected**:
     - Score calculated (e.g., 30% if 3/10 passed)
     - Overall result: "Fail"
     - Work order status updated to "QCFailed"
     - Certificate downloaded showing Fail badge
     - Redirected to dashboard

4. **Verify Database:**
   ```sql
   SELECT Id, OverallResult, Score, Status 
   FROM WorkOrderQualityChecks 
   WHERE WorkOrderId = '<your-work-order-id>'
   
   SELECT Id, Status 
   FROM WorkOrders 
   WHERE Id = '<your-work-order-id>'
   ```
   - QualityCheck: OverallResult = "Fail", Score < MinimumPassingScore
   - WorkOrder: Status = "QCFailed"

### Test Scenario 3: Reject for Rework

**Steps:**
1. Start a new inspection
2. Mark some items (doesn't matter which)
3. **Click "Reject for Rework"** button
4. Fill rejection dialog:
   - **Rejection Reason**: "Poor workmanship - welds not to specification"
   - **Corrective Actions**: "Re-weld joints 3, 5, and 7. Perform dye penetrant test."
   - **Severity**: Select "High"
5. Click "Reject" button
6. **Expected**:
   - WorkOrderRework record created
   - Work order status changed to "Rework"
   - Quality check OverallResult = "Fail"
   - Toast: "Work Order Rejected - Rework has been created"
   - Redirected to dashboard

7. **Verify Database:**
   ```sql
   SELECT * FROM WorkOrderRework 
   WHERE WorkOrderId = '<your-work-order-id>'
   ORDER BY IdentifiedDate DESC
   
   SELECT Status FROM WorkOrders 
   WHERE Id = '<your-work-order-id>'
   ```
   - Rework record exists with rejection reason and corrective actions
   - WorkOrder.Status = "Rework"

### Test Scenario 4: In-Progress Inspection (Resume)

**Steps:**
1. Start inspection
2. Mark 5 items as Pass
3. **Close browser/navigate away WITHOUT completing**
4. **Resume inspection:**
   - Go back to QC dashboard
   - Work order should show in "Active Inspections" (not Pending)
   - Click "Continue" on the active inspection
5. **Expected**:
   - Previous results preserved (5 items still marked Pass)
   - Progress shows correct state
   - Can continue marking remaining items

---

## API Testing (Postman/Swagger)

### 1. Update Item Result
```http
POST /api/maintenance/quality-control/update-item-result
Authorization: Bearer <token>
Content-Type: application/json

{
  "qualityCheckId": "<guid>",
  "itemId": "item-0",
  "result": "Pass",
  "notes": "Item inspection completed successfully"
}
```

### 2. Complete Inspection
```http
POST /api/maintenance/quality-control/complete-inspection
Authorization: Bearer <token>
Content-Type: application/json

{
  "qualityCheckId": "<guid>",
  "notes": "All items inspected. Work meets quality standards."
}
```
**Response:**
```json
{
  "overallResult": "Pass",
  "score": 100,
  "message": "Inspection completed successfully"
}
```

### 3. Reject for Rework
```http
POST /api/maintenance/quality-control/reject-for-rework
Authorization: Bearer <token>
Content-Type: application/json

{
  "qualityCheckId": "<guid>",
  "rejectionReason": "Quality standards not met",
  "correctiveActions": "Rework required on items 3-5",
  "severity": "High"
}
```

### 4. Generate Certificate
```http
GET /api/maintenance/quality-control/certificate/<qualityCheckId>
Authorization: Bearer <token>
```
**Response:** PDF file download

---

## Database Validation Queries

### Check Inspection Status
```sql
SELECT 
    qc.Id AS QualityCheckId,
    wo.WorkOrderNumber,
    qc.OverallResult,
    qc.Score,
    qc.InspectionDate,
    wo.Status AS WorkOrderStatus,
    cl.Name AS ChecklistName
FROM WorkOrderQualityChecks qc
JOIN WorkOrders wo ON qc.WorkOrderId = wo.Id
JOIN QualityControlChecklists cl ON qc.ChecklistId = cl.Id
WHERE wo.TenantId = '00000000-0000-0000-0000-000000000001'
ORDER BY qc.InspectionDate DESC
```

### Check Rework Records
```sql
SELECT 
    r.Id,
    wo.WorkOrderNumber,
    r.ReworkReason,
    r.Description,
    r.Severity,
    r.Status,
    r.IdentifiedDate
FROM WorkOrderRework r
JOIN WorkOrders wo ON r.WorkOrderId = wo.Id
WHERE wo.TenantId = '00000000-0000-0000-0000-000000000001'
ORDER BY r.IdentifiedDate DESC
```

### Check Checklist Results (JSON)
```sql
SELECT 
    wo.WorkOrderNumber,
    qc.CheckResults,
    qc.Score,
    qc.OverallResult
FROM WorkOrderQualityChecks qc
JOIN WorkOrders wo ON qc.WorkOrderId = wo.Id
WHERE qc.Id = '<qualityCheckId>'
```

---

## Expected Results Summary

| Action | WorkOrder Status | QualityCheck OverallResult | Rework Created | Certificate |
|--------|------------------|----------------------------|----------------|-------------|
| Complete (Pass) | Completed | Pass | No | Yes (Pass badge) |
| Complete (Fail) | QCFailed | Fail | No | Yes (Fail badge) |
| Reject for Rework | Rework | Fail | Yes | No |

---

## Troubleshooting

### Issue: Certificate not downloading
- **Check**: Browser console for errors
- **Check**: Backend logs for PDF generation errors
- **Solution**: Ensure QuestPDF license is set correctly (Community license)

### Issue: Items not saving
- **Check**: Browser Network tab - verify 200 OK response from `update-item-result` endpoint
- **Check**: Backend logs for errors
- **Check**: TenantId matches between frontend and backend

### Issue: Work order not appearing in Pending Inspections
- **Check**: Work order status is "PendingQC"
- **Check**: WorkOrderQualityCheck record exists with OverallResult = "Pending"
- **Check**: TenantId matches
- **SQL Query**:
  ```sql
  SELECT * FROM WorkOrderQualityChecks 
  WHERE OverallResult = 'Pending' 
  AND TenantId = '00000000-0000-0000-0000-000000000001'
  ```

### Issue: Old inspection page still showing
- **Solution**: Hard refresh browser (Ctrl+Shift+R)
- **Solution**: Clear browser cache
- **Check**: Verify `page.tsx` in `inspect/[workOrderId]/` folder is the new simplified version

---

## File Locations

### Backend:
- `src/ErpSystem.Api/Controllers/Maintenance/QualityControlController.cs` - Main QC endpoints
- `src/ErpSystem.Api/Services/QualityCertificateService.cs` - PDF certificate generation
- `src/ErpSystem.Core/Entities/Maintenance/QualityControlEntities.cs` - Data models
- `src/ErpSystem.Core/Entities/Maintenance/QualityControlEntities.cs` - WorkOrderRework entity

### Frontend:
- `frontend/src/app/maintenance/quality-control/inspect/[workOrderId]/page.tsx` - Inspection execution page
- `frontend/src/app/maintenance/quality-control/page.tsx` - QC dashboard
- `frontend/src/services/qualityControlService.ts` - API service

---

## Success Criteria ✅

- [ ] Can start inspection from dashboard
- [ ] Can mark items as Pass/Fail/N/A
- [ ] Progress updates automatically
- [ ] Complete button enabled after all items marked
- [ ] Score calculated correctly (pass count / total count * 100)
- [ ] Work order status updates correctly
- [ ] Certificate downloads automatically on Pass
- [ ] Certificate contains all inspection data
- [ ] Reject dialog accepts reason and corrective actions
- [ ] Rework record created with correct data
- [ ] Work order status changes to "Rework" on rejection
- [ ] Can resume in-progress inspection after browser close

---

**Implementation Date:** 2025-11-04  
**Version:** 1.0  
**Status:** Complete and Ready for Testing
