# Quality Control Inspection History & Certificate Management

## Overview
Added comprehensive inspection history tracking with the ability to review completed inspections and re-download certificates.

## Features Implemented

### 1. Backend API Endpoint
**File**: `src/ErpSystem.Api/Controllers/Maintenance/QualityControlController.cs`

#### New Endpoint: Get Completed Inspections
```csharp
[HttpGet("completed-inspections")]
public async Task<ActionResult<List<CompletedInspectionDto>>> GetCompletedInspections()
```

**Features**:
- Returns last 50 completed inspections (Pass or Fail)
- Includes work order, asset, and checklist information
- Ordered by inspection date (most recent first)
- Tenant-isolated data

**Response DTO**:
```csharp
public class CompletedInspectionDto
{
    public string Id { get; set; }
    public string WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; }
    public string AssetName { get; set; }
    public string ChecklistName { get; set; }
    public DateTime InspectionDate { get; set; }
    public string OverallResult { get; set; } // "Pass" or "Fail"
    public decimal Score { get; set; }
    public string? Notes { get; set; }
}
```

### 2. Frontend Service
**File**: `frontend/src/services/qualityControlService.ts`

#### New Method
```typescript
async getCompletedInspections(): Promise<any[]>
```

Fetches completed inspections from the backend API.

### 3. Quality Control Dashboard
**File**: `frontend/src/app/maintenance/quality-control/page.tsx`

#### New "Completed Inspections" Section
Displays a comprehensive table with:

**Columns**:
1. Work Order Number
2. Asset Name
3. Checklist Name
4. Inspection Date
5. Result (Pass/Fail badge with icon)
6. Score (percentage with progress bar)
7. Actions (Certificate download + View details)

**Features**:
- **Pass Badge**: Green with checkmark icon
- **Fail Badge**: Red with X icon
- **Score Display**: Shows percentage with visual progress bar
- **Certificate Button**: Only visible for passed inspections
- **View Button**: Shows inspection details in alert dialog

#### Certificate Re-Download
Passed inspections show a "Certificate" button that:
1. Fetches PDF from backend endpoint
2. Downloads with proper filename format: `QC-Certificate-{inspectionId}.pdf`
3. Uses existing certificate generation endpoint (no duplication)

**Download Implementation**:
```typescript
<Button
  size="sm"
  variant="outline"
  onClick={() => {
    const link = document.createElement('a');
    link.href = `${API_URL}/maintenance/quality-control/certificate/${inspection.id}`;
    link.download = `QC-Certificate-${inspection.id}.pdf`;
    link.click();
  }}
>
  <FileText className="h-4 w-4 mr-1" />
  Certificate
</Button>
```

#### View Inspection Details
**Eye icon button** shows popup with:
- Work Order Number
- Asset Name
- Overall Result
- Score Percentage
- Inspector Notes

## User Flow

### Completing an Inspection
1. Inspector completes all checklist items
2. Clicks "Complete Inspection"
3. System calculates score and determines Pass/Fail
4. **If Pass**: Certificate preview opens automatically
5. Inspector can download certificate or close
6. Redirected to QC dashboard

### Reviewing Past Inspections
1. Navigate to Quality Control Dashboard
2. Scroll to "Completed Inspections" section
3. View table of recent inspections
4. Click "Certificate" button to re-download (Pass only)
5. Click eye icon to view inspection details

## Data Persistence
- All completed inspections stored in `WorkOrderQualityChecks` table
- Certificates generated on-demand from stored inspection data
- No PDF files stored - regenerated from database each time
- Inspection results include:
  - CheckResults JSON (all item-by-item results)
  - Overall score and result
  - Inspector notes
  - Timestamps

## Benefits

### For Inspectors
- ✅ Review past inspection decisions
- ✅ Re-download certificates for audits
- ✅ See historical pass/fail rates
- ✅ Access inspection notes

### For Quality Managers
- ✅ Monitor inspection trends
- ✅ Identify frequently failing assets
- ✅ Audit trail for compliance
- ✅ Easy certificate retrieval for customers

### For Auditors
- ✅ Complete inspection history
- ✅ Downloadable certificates
- ✅ Detailed item-level results
- ✅ Timestamp tracking

## Technical Details

### Performance Optimizations
- Limit to last 50 inspections (configurable)
- Async data loading
- Separate queries for work orders (avoids N+1 problem after fix)
- Indexed database queries

### Security
- Tenant isolation enforced
- Authentication required (via authToken)
- Authorization headers on all API calls

### Certificate Generation
- Uses existing `QualityCertificateService`
- Generated on-the-fly from database
- Consistent formatting with stamp design
- Date format: `dd-MMM-yy HH:mm:ss`

## Future Enhancements
Potential improvements:
1. **Advanced Filtering**:
   - Filter by date range
   - Filter by asset
   - Filter by result (Pass/Fail)
   - Filter by inspector

2. **Export Options**:
   - Export history to Excel
   - Bulk certificate download
   - Email certificates directly

3. **Analytics**:
   - Pass/fail rate charts
   - Average score trends
   - Inspector performance metrics
   - Asset quality trends

4. **Detailed View**:
   - Full inspection details dialog
   - Item-by-item results display
   - Photos/evidence viewing
   - Edit notes (with audit trail)

5. **Notifications**:
   - Certificate expiry reminders
   - Re-inspection due dates
   - Failed inspection alerts

## Testing
To test the completed inspections feature:
1. Complete an inspection (Pass or Fail)
2. Return to QC dashboard
3. Verify inspection appears in "Completed Inspections" table
4. Click "Certificate" button (Pass only) - should download PDF
5. Click eye icon - should show inspection details
6. Refresh page - data should persist
