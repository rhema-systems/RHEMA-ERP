# Job Card Completion Workflow - Quick Reference

## Endpoints

### 1. Complete Job Card
```http
POST /api/maintenance/job-cards/{id}/complete
Content-Type: application/json
Authorization: Bearer {token}

{
  "completionNotes": "Required completion notes",
  "warrantyDays": 90
}
```

### 2. Quality Check
```http
POST /api/maintenance/job-cards/{id}/quality-check
Content-Type: application/json
Authorization: Bearer {token}

{
  "qualityCheckPassed": true,
  "qualityCheckNotes": "Required QC notes"
}
```

### 3. Record Acceptance
```http
POST /api/maintenance/job-cards/{id}/acceptance
Content-Type: application/json
Authorization: Bearer {token}

{
  "accepted": true,
  "acceptanceNotes": "Optional notes"
}
```

### 4. Generate Certificate
```http
POST /api/maintenance/job-cards/{id}/certificates
Content-Type: application/json
Authorization: Bearer {token}

{
  "certificateType": "Maintenance Completion",
  "validUntil": "2026-01-22T00:00:00Z",
  "description": "Optional description"
}
```

### 5. Get Certificates
```http
GET /api/maintenance/job-cards/{id}/certificates
Authorization: Bearer {token}
```

### 6. Get Certificate by ID
```http
GET /api/maintenance/job-cards/certificates/{certificateId}
Authorization: Bearer {token}
```

## Workflow Sequence

```
Draft/In Progress
    ↓ [Complete]
Completed
    ↓ [Quality Check - Pass]
Quality Checked
    ↓ [Accept]
Accepted
    ↓ [Generate Certificate]
Closed ✓
```

## Status Requirements

| Action | Required Status |
|--------|----------------|
| Complete | Not Completed/Closed |
| Quality Check | Completed |
| Accept | Quality Checked |
| Generate Certificate | Accepted |

## Error Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created (certificate) |
| 400 | Invalid workflow state |
| 401 | Not authenticated |
| 404 | Job card not found |
| 500 | Server error |

## Key Features

✅ Sequential workflow enforcement  
✅ Automatic status transitions  
✅ Complete audit trail via comments  
✅ Employee tracking  
✅ Certificate numbering (CERT-YYYY-####)  
✅ Warranty expiration calculation  
✅ Follow-up management  

## Security

- JWT token required
- User must have employee record
- All actions tied to current employee
- Tenant isolation enforced

## Files Modified

- `JobCardService.cs` - Service implementation
- `IJobCardRepository.cs` - Repository interface
- `JobCardRepository.cs` - Repository implementation
- `JobCardController.cs` - API endpoints
- `SimpleJobCardService.cs` - Stub implementations

## Database

- Migration: `20251022114125_AddJobCardAdmissionCompletionAndCertificates`
- Status: ✅ Applied
- Tables: JobCard (extended), JobCardCertificate

## Documentation

- Full API docs: `docs/api/JobCardCompletionWorkflow.md`
- Implementation summary: `docs/IMPLEMENTATION_SUMMARY.md`
- This quick reference: `docs/QUICK_REFERENCE.md`

## Testing Checklist

- [ ] Create job card
- [ ] Complete job card
- [ ] Quality check (pass)
- [ ] Record acceptance
- [ ] Generate certificate
- [ ] Verify status = "Closed"
- [ ] Check comments created
- [ ] Retrieve certificates
- [ ] Test error scenarios

## Common Issues

**"Job card must be completed before quality check"**
→ Ensure status is "Completed" before quality check

**"User not authenticated"**
→ Check JWT token is valid and not expired

**"Current user does not have an associated employee record"**
→ Link user account to employee record

**"Job card must be accepted before generating certificate"**
→ Follow workflow: Complete → QC → Accept → Certificate
