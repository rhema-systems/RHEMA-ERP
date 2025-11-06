# Quality Control Certificate Preview Feature

## Overview
The QC inspection now includes a certificate preview feature that allows inspectors to view the generated certificate before downloading it.

## Implementation Details

### Frontend Changes
**File**: `frontend/src/app/maintenance/quality-control/inspect/[workOrderId]/page.tsx`

#### New State Variables
```typescript
const [showCertificateDialog, setShowCertificateDialog] = useState(false);
const [certificateBlobUrl, setCertificateBlobUrl] = useState<string | null>(null);
const [completionResult, setCompletionResult] = useState<{ overallResult: string; score: number } | null>(null);
```

#### New Functions

1. **previewCertificate()** - Loads certificate as blob and displays preview
   - Fetches certificate PDF from backend API
   - Creates blob URL for iframe display
   - Opens certificate preview dialog

2. **downloadCertificate()** - Downloads the previewed certificate
   - Uses the existing blob URL
   - Creates download link with proper filename format
   - Triggers browser download

3. **closeCertificatePreview()** - Cleans up and redirects
   - Revokes blob URL to prevent memory leaks
   - Closes preview dialog
   - Redirects to QC dashboard

#### Updated handleComplete() Flow
1. Completes inspection via API
2. Stores completion result (score and overall result)
3. Shows toast notification
4. **If inspection passed**: Shows certificate preview
5. **If inspection failed**: Redirects to dashboard

#### Certificate Preview Dialog
- **Size**: Full-screen modal (max-w-4xl, h-90vh)
- **Content**: 
  - Title: "Quality Inspection Certificate"
  - Score and result display
  - PDF embedded in iframe
- **Actions**:
  - **Close**: Returns to QC dashboard
  - **Download Certificate**: Downloads PDF with proper filename

## User Experience Flow

### Passing Inspection
1. Inspector completes all checklist items
2. Clicks "Complete Inspection"
3. Confirms completion
4. System calculates score and determines Pass/Fail
5. **NEW**: Certificate preview dialog opens automatically
6. Inspector can view certificate in browser
7. Inspector can download certificate or close
8. Redirect to QC dashboard

### Failing Inspection
1. Same steps 1-4 above
2. No certificate preview (only passing inspections get certificates)
3. Direct redirect to QC dashboard

## Technical Features

### Memory Management
- Blob URLs are properly cleaned up using `URL.revokeObjectURL()`
- Prevents memory leaks from certificate previews

### Backend Integration
- Uses existing certificate generation endpoint: `GET /api/maintenance/quality-control/certificate/{qualityCheckId}`
- Response type: `blob` (PDF binary data)
- No backend changes required

### UI Components
- Uses shadcn/ui Dialog component
- Responsive design with proper sizing
- Iframe for PDF display (browser-native PDF viewer)

## Browser Compatibility
- Works in all modern browsers with native PDF support
- Chrome, Edge, Firefox, Safari all support PDF iframe display
- Fallback: Users can still download and open in external viewer

## File Naming Convention
Certificate filename format: `QC-Certificate-{inspectionId}.pdf`

Example: `QC-Certificate-3fa85f64-5717-4562-b3fc-2c963f66afa6.pdf`

## Future Enhancements
Potential improvements:
1. Add print button to certificate preview
2. Email certificate directly from preview
3. Add certificate to work order history automatically
4. Custom PDF viewer with zoom/rotate controls
5. Allow preview before completion (draft mode)
