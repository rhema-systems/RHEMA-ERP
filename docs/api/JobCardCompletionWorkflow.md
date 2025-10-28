# Job Card Completion Workflow API

This document describes the complete workflow for completing a job card, from marking it complete through quality check, acceptance, and certificate generation.

## Workflow Overview

The job card completion follows a strict sequential workflow:

1. **Complete** → Job card is marked as completed
2. **Quality Check** → Completed work is quality checked
3. **Acceptance** → Customer/user accepts the completed work
4. **Certificate Generation** → Certificate is generated and job card is closed

## API Endpoints

### 1. Complete Job Card

Marks a job card as completed with all completion details.

**Endpoint:** `POST /api/maintenance/job-cards/{id}/complete`

**Status Requirement:** Job card must be in progress (not already completed or closed)

**Request Body:**
```json
{
  "completionNotes": "All maintenance tasks completed successfully",
  "assetConditionOnCompletion": "Good",
  "mileageReadingOnCompletion": 125500.5,
  "hoursReadingOnCompletion": 3500.25,
  "fuelLevelOnCompletion": 75.5,
  "workCompletedSummary": "Replaced oil filter, performed engine diagnostics, replaced air filter",
  "remainingIssues": "Minor paint scratch on door",
  "warrantyDays": 90,
  "warrantyTerms": "90-day warranty on parts and labor",
  "requiresFollowUp": true,
  "followUpDate": "2025-12-01T10:00:00Z",
  "followUpInstructions": "Check engine performance after 1000 miles"
}
```

**Response:** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobCardNumber": "JC-2025-0001",
  "title": "Routine Maintenance",
  "jobCardStatus": "Completed",
  "completedDate": "2025-10-22T12:00:00Z",
  "completionNotes": "All maintenance tasks completed successfully",
  ...
}
```

**Status Change:** Job card status changes to **"Completed"**

---

### 2. Perform Quality Check

Performs quality check on a completed job card.

**Endpoint:** `POST /api/maintenance/job-cards/{id}/quality-check`

**Status Requirement:** Job card must have status **"Completed"**

**Request Body:**
```json
{
  "qualityCheckPassed": true,
  "qualityCheckNotes": "All work meets quality standards. Engine runs smoothly, no leaks detected."
}
```

**Response:** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobCardNumber": "JC-2025-0001",
  "jobCardStatus": "Quality Checked",
  "qualityCheckPassed": true,
  "qualityCheckDate": "2025-10-22T12:30:00Z",
  "qualityCheckNotes": "All work meets quality standards...",
  ...
}
```

**Status Change:** 
- If `qualityCheckPassed = true`: Status changes to **"Quality Checked"**
- If `qualityCheckPassed = false`: Status changes to **"Failed QC"**

---

### 3. Record Acceptance

Records customer/user acceptance of the completed work.

**Endpoint:** `POST /api/maintenance/job-cards/{id}/acceptance`

**Status Requirement:** Job card must have status **"Quality Checked"**

**Request Body:**
```json
{
  "accepted": true,
  "acceptanceNotes": "Vehicle is running perfectly. Very satisfied with the service."
}
```

**Response:** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobCardNumber": "JC-2025-0001",
  "jobCardStatus": "Accepted",
  "acceptedDate": "2025-10-22T13:00:00Z",
  "customerAcceptance": true,
  "acceptanceNotes": "Vehicle is running perfectly...",
  ...
}
```

**Status Change:** Status changes to **"Accepted"**

**Note:** If `accepted = false`, the endpoint will return an error. Use a different workflow for rejections.

---

### 4. Generate Certificate

Generates a maintenance certificate for the completed and accepted job card.

**Endpoint:** `POST /api/maintenance/job-cards/{id}/certificates`

**Status Requirement:** Job card must have status **"Accepted"**

**Request Body:**
```json
{
  "certificateType": "Maintenance Completion",
  "validUntil": "2026-01-22T00:00:00Z",
  "description": "This certifies that routine maintenance was completed in accordance with manufacturer specifications.",
  "certificateData": {
    "inspectorName": "John Smith",
    "inspectorLicense": "MECH-12345",
    "complianceStandards": ["ISO 9001", "OEM Standards"]
  }
}
```

**Response:** `201 Created`
```json
{
  "id": "8c3b8e7a-4d2f-4e5a-b6c8-9d1e2f3a4b5c",
  "certificateNumber": "CERT-2025-0001",
  "jobCardId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobCardNumber": "JC-2025-0001",
  "assetId": "7d1e8f9a-2c3b-4d5e-a6f7-8b9c0d1e2f3a",
  "assetName": "Forklift FL-001",
  "certificateType": "Maintenance Completion",
  "issuedDate": "2025-10-22T13:30:00Z",
  "validUntil": "2026-01-22T00:00:00Z",
  "issuedBy": "Jane Doe",
  "description": "This certifies that routine maintenance...",
  "isActive": true,
  "createdAt": "2025-10-22T13:30:00Z"
}
```

**Status Change:** Job card status changes to **"Closed"**

---

### 5. Get Certificates for Job Card

Retrieves all certificates issued for a specific job card.

**Endpoint:** `GET /api/maintenance/job-cards/{id}/certificates`

**Response:** `200 OK`
```json
[
  {
    "id": "8c3b8e7a-4d2f-4e5a-b6c8-9d1e2f3a4b5c",
    "certificateNumber": "CERT-2025-0001",
    "jobCardId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "jobCardNumber": "JC-2025-0001",
    "assetId": "7d1e8f9a-2c3b-4d5e-a6f7-8b9c0d1e2f3a",
    "assetName": "Forklift FL-001",
    "certificateType": "Maintenance Completion",
    "issuedDate": "2025-10-22T13:30:00Z",
    "validUntil": "2026-01-22T00:00:00Z",
    "issuedBy": "Jane Doe",
    "description": "This certifies that routine maintenance...",
    "isActive": true,
    "createdAt": "2025-10-22T13:30:00Z"
  }
]
```

---

### 6. Get Certificate by ID

Retrieves a specific certificate by its ID.

**Endpoint:** `GET /api/maintenance/job-cards/certificates/{certificateId}`

**Response:** `200 OK`
```json
{
  "id": "8c3b8e7a-4d2f-4e5a-b6c8-9d1e2f3a4b5c",
  "certificateNumber": "CERT-2025-0001",
  "jobCardId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "jobCardNumber": "JC-2025-0001",
  "assetId": "7d1e8f9a-2c3b-4d5e-a6f7-8b9c0d1e2f3a",
  "assetName": "Forklift FL-001",
  "certificateType": "Maintenance Completion",
  "issuedDate": "2025-10-22T13:30:00Z",
  "validUntil": "2026-01-22T00:00:00Z",
  "issuedBy": "Jane Doe",
  "description": "This certifies that routine maintenance...",
  "isActive": true,
  "createdAt": "2025-10-22T13:30:00Z"
}
```

---

## Error Handling

### Common Error Responses

**404 Not Found**
```json
{
  "error": "Job card with ID {id} not found"
}
```

**400 Bad Request** - Invalid workflow state
```json
{
  "error": "Job card must be completed before quality check"
}
```

**401 Unauthorized** - User not authenticated
```json
{
  "error": "User not authenticated"
}
```

**500 Internal Server Error**
```json
{
  "error": "An error occurred while completing the job card"
}
```

---

## Workflow States

| Current Status | Allowed Action | Next Status |
|---------------|----------------|-------------|
| In Progress | Complete | Completed |
| Completed | Quality Check (Pass) | Quality Checked |
| Completed | Quality Check (Fail) | Failed QC |
| Quality Checked | Accept | Accepted |
| Accepted | Generate Certificate | Closed |

---

## Complete Workflow Example

Here's a complete example using cURL:

```bash
# 1. Complete the job card
curl -X POST "https://api.example.com/api/maintenance/job-cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/complete" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "completionNotes": "All tasks completed",
    "assetConditionOnCompletion": "Good",
    "warrantyDays": 90
  }'

# 2. Perform quality check
curl -X POST "https://api.example.com/api/maintenance/job-cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/quality-check" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "qualityCheckPassed": true,
    "qualityCheckNotes": "All quality standards met"
  }'

# 3. Record acceptance
curl -X POST "https://api.example.com/api/maintenance/job-cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/acceptance" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "accepted": true,
    "acceptanceNotes": "Satisfied with the service"
  }'

# 4. Generate certificate
curl -X POST "https://api.example.com/api/maintenance/job-cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/certificates" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "certificateType": "Maintenance Completion",
    "validUntil": "2026-01-22T00:00:00Z",
    "description": "Maintenance completed successfully"
  }'

# 5. Get certificates
curl -X GET "https://api.example.com/api/maintenance/job-cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/certificates" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Audit Trail

Each action automatically creates a comment on the job card for audit purposes:
- **Completion**: "Job card completed by {EmployeeId}. {CompletionNotes}"
- **Quality Check**: "Quality check {passed/failed} by employee {EmployeeId}. {QualityCheckNotes}"
- **Acceptance**: "Job card accepted by employee {EmployeeId}. {AcceptanceNotes}"
- **Certificate**: "Certificate {CertificateNumber} generated for job card. Type: {CertificateType}"

All comments are logged with timestamp, employee information, and details of the action.

---

## Security

All endpoints require:
- Valid JWT authentication token
- User must have an associated employee record
- User must belong to a valid tenant

The current user's employee ID is automatically captured for all operations (completion, quality check, acceptance, and certificate issuance).

---

## Notes

1. **Sequential Workflow**: The workflow must be followed in order. You cannot skip steps.
2. **Certificate Numbering**: Certificates are automatically numbered sequentially by year (CERT-YYYY-####).
3. **Job Card Closure**: Generating a certificate automatically closes the job card.
4. **Multiple Certificates**: Multiple certificates can be generated for a single job card if needed.
5. **Warranty Tracking**: Warranty expiration is automatically calculated based on warranty days.
6. **Follow-up Management**: Job cards can be flagged for follow-up with specific dates and instructions.
