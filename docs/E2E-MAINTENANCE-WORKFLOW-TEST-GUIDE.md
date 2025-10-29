# End-to-End Maintenance Workflow Test Guide

## 🎯 Overview

This guide provides step-by-step instructions for testing the complete maintenance workflow from Job Card creation through completion. All master data has been seeded with predictable GUIDs for easy reference.

**Test Flow:**  
Draft Job Card → Submit → Approval (2 levels) → Work Order Creation → Execution → Quality Check → Completion

---

## 📋 Prerequisites

### Run E2E Seeder

The seeder will run automatically when you start the application in Development mode. Just run:

```powershell
cd .\src\ErpSystem.Api
dotnet run
```

The seeder will create all necessary test data on startup. Watch the console for the seeding summary.

---

## 🔑 Seeded Data Reference

### Tenant
- **Default Tenant ID:** `00000000-0000-0000-0000-000000000001`

### HR Employees (for roles and assignments)

| Role | ID | Email | Use For |
|------|----|----|---------|
| Fleet Manager | `EMP00000-0000-0000-0000-000000000001` | john.fleet@company.com | Job Card Requester |
| Maintenance Supervisor | `EMP00000-0000-0000-0000-000000000002` | sarah.supervisor@company.com | Level 1 Approver |
| Maintenance Manager | `EMP00000-0000-0000-0000-000000000003` | michael.manager@company.com | Level 2 Approver |
| Senior Technician | `EMP00000-0000-0000-0000-000000000004` | david.senior@company.com | Work Execution |
| Junior Technician | `EMP00000-0000-0000-0000-000000000005` | emily.junior@company.com | Assistant |
| Quality Inspector | `EMP00000-0000-0000-0000-000000000006` | robert.inspector@company.com | Quality Check |
| Service Advisor | `EMP00000-0000-0000-0000-000000000007` | linda.advisor@company.com | Discharge |

### Assets

| Asset | ID | Asset Number |
|-------|----|----|
| Delivery Truck - Isuzu NPR 75 | `AST00000-0000-0000-0000-000000000001` | TRK-001 |

### Inventory Parts

| Part | ID | Code |
|------|----|----|
| Engine Oil 5W-30 (5L) | `PART0000-0000-0000-0000-000000000001` | OIL-5W30-5L |
| Oil Filter | `PART0000-0000-0000-0000-000000000002` | FILTER-OIL-001 |
| Air Filter | `PART0000-0000-0000-0000-000000000003` | FILTER-AIR-001 |
| Brake Fluid DOT 4 | `PART0000-0000-0000-0000-000000000004` | FLUID-BRAKE-DOT4 |
| Spark Plug | `PART0000-0000-0000-0000-000000000005` | SPARK-PLUG-001 |

### Master Data

| Type | ID | Name |
|------|----|----|
| Maintenance Type | `MT000000-0000-0000-0000-000000000001` | Preventive Maintenance |
| Work Order Type | `WOT00000-0000-0000-0000-000000000001` | Preventive Maintenance |
| Priority Level (High) | `PR000000-0000-0000-0000-000000000001` | High |
| Priority Level (Medium) | `PR000000-0000-0000-0000-000000000002` | Medium |
| Priority Level (Low) | `PR000000-0000-0000-0000-000000000003` | Low |
| Asset Category | `CAT00000-0000-0000-0000-000000000001` | Vehicles |
| Asset Type | `AT000000-0000-0000-0000-000000000001` | Delivery Truck |

---

## 🧪 Test Workflow

### Phase 1: Create Job Card (Draft)

#### Login as Fleet Manager
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "email": "john.fleet@company.com",
  "password": "Password123!"
}
```

**Save the `token` from response**

#### Create Job Card
```http
POST https://localhost:7001/api/maintenance/job-cards
Authorization: Bearer {token}
Content-Type: application/json

{
  "title": "Q4 2025 Preventive Maintenance - TRK-001",
  "description": "Quarterly preventive maintenance including oil change, filters, and general inspection",
  "problemDescription": "Scheduled maintenance due at 85,000 km",
  "assetId": "AST00000-0000-0000-0000-000000000001",
  "maintenanceTypeId": "MT000000-0000-0000-0000-000000000001",
  "priority": "Medium",
  "priorityLevelId": "PR000000-0000-0000-0000-000000000002",
  "estimatedHours": 4,
  "estimatedCost": 500.00,
  "status": "Draft",
  "requiresApproval": true,
  "customFields": {
    "currentMileage": 85000,
    "lastServiceDate": "2025-07-29"
  }
}
```

**Save the Job Card ID from response** (let's call it `{jobCardId}`)

---

### Phase 2: Submit for Approval

#### Submit Job Card
```http
PUT https://localhost:7001/api/maintenance/job-cards/{jobCardId}/submit
Authorization: Bearer {token}
Content-Type: application/json

{
  "submissionNotes": "All documentation complete. Vehicle ready for maintenance.",
  "requestedCompletionDate": "2025-11-15T00:00:00Z"
}
```

**Expected Result:**
- Status changes to `Submitted`
- ApprovalStatus becomes `Pending`
- Approval workflow created with 2 levels

#### Verify Approval Steps
```http
GET https://localhost:7001/api/maintenance/job-cards/{jobCardId}/approval-steps
Authorization: Bearer {token}
```

---

### Phase 3: Level 1 Approval (Supervisor)

#### Login as Supervisor
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "email": "sarah.supervisor@company.com",
  "password": "Password123!"
}
```

#### View Pending Approvals
```http
GET https://localhost:7001/api/maintenance/job-cards/pending-approvals
Authorization: Bearer {supervisor-token}
```

#### Approve Level 1
```http
POST https://localhost:7001/api/maintenance/job-cards/{jobCardId}/approve
Authorization: Bearer {supervisor-token}
Content-Type: application/json

{
  "approvalStepId": "{level-1-step-id}",
  "comments": "Reviewed and approved. Maintenance schedule is appropriate.",
  "approved": true
}
```

---

### Phase 4: Level 2 Approval (Manager)

#### Login as Manager
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "email": "michael.manager@company.com",
  "password": "Password123!"
}
```

#### Approve Level 2 (Final)
```http
POST https://localhost:7001/api/maintenance/job-cards/{jobCardId}/approve
Authorization: Bearer {manager-token}
Content-Type: application/json

{
  "approvalStepId": "{level-2-step-id}",
  "comments": "Final approval granted. Budget allocated. Proceed with maintenance.",
  "approved": true
}
```

**Expected Result:**
- Job Card status → `Approved`
- ApprovalStatus → `Approved`
- Ready to generate Work Order

---

### Phase 5: Create Work Order

#### Login as Maintenance Coordinator/Supervisor
(Use supervisor token from Phase 3)

#### Create Work Order from Job Card
```http
POST https://localhost:7001/api/maintenance/work-orders
Authorization: Bearer {supervisor-token}
Content-Type: application/json

{
  "jobCardId": "{jobCardId}",
  "title": "PM Service - Isuzu TRK-001 @ 85,000 km",
  "description": "Complete quarterly preventive maintenance per job card requirements",
  "assetId": "AST00000-0000-0000-0000-000000000001",
  "maintenanceTypeId": "MT000000-0000-0000-0000-000000000001",
  "workOrderTypeId": "WOT00000-0000-0000-0000-000000000001",
  "priorityLevelId": "PR000000-0000-0000-0000-000000000002",
  "assignedTechnicianId": "EMP00000-0000-0000-0000-000000000004",
  "plannedStartDate": "2025-11-10T08:00:00Z",
  "plannedCompletionDate": "2025-11-10T17:00:00Z",
  "estimatedHours": 4,
  "estimatedCost": 500.00,
  "status": "Open"
}
```

**Save the Work Order ID** (let's call it `{workOrderId}`)

#### Add Tasks to Work Order
```http
POST https://localhost:7001/api/maintenance/work-orders/{workOrderId}/tasks/bulk
Authorization: Bearer {supervisor-token}
Content-Type: application/json

[
  {
    "taskName": "Engine Oil Change",
    "description": "Drain old oil and replace with 5L of 5W-30 synthetic oil",
    "sequence": 1,
    "estimatedHours": 0.5,
    "isRequired": true
  },
  {
    "taskName": "Oil Filter Replacement",
    "description": "Remove old oil filter and install new one",
    "sequence": 2,
    "estimatedHours": 0.25,
    "isRequired": true
  },
  {
    "taskName": "Air Filter Inspection",
    "description": "Inspect and replace air filter if necessary",
    "sequence": 3,
    "estimatedHours": 0.25,
    "isRequired": true
  },
  {
    "taskName": "Brake System Inspection",
    "description": "Check brake pads, rotors, and fluid levels",
    "sequence": 4,
    "estimatedHours": 1,
    "isRequired": true
  },
  {
    "taskName": "Tire Rotation",
    "description": "Rotate tires and check pressure",
    "sequence": 5,
    "estimatedHours": 0.75,
    "isRequired": true
  },
  {
    "taskName": "General Visual Inspection",
    "description": "Complete visual inspection of all systems",
    "sequence": 6,
    "estimatedHours": 1.25,
    "isRequired": true
  }
]
```

#### Add Required Parts
```http
POST https://localhost:7001/api/maintenance/work-orders/{workOrderId}/parts/bulk
Authorization: Bearer {supervisor-token}
Content-Type: application/json

[
  {
    "partId": "PART0000-0000-0000-0000-000000000001",
    "quantityRequired": 5,
    "status": "Requested"
  },
  {
    "partId": "PART0000-0000-0000-0000-000000000002",
    "quantityRequired": 1,
    "status": "Requested"
  },
  {
    "partId": "PART0000-0000-0000-0000-000000000003",
    "quantityRequired": 1,
    "status": "Requested"
  }
]
```

---

### Phase 6: Execute Work Order (Technician)

#### Login as Senior Technician
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "email": "david.senior@company.com",
  "password": "Password123!"
}
```

#### View Assigned Work Orders
```http
GET https://localhost:7001/api/maintenance/work-orders/assigned-to-me
Authorization: Bearer {technician-token}
```

#### Start Work Order
```http
PUT https://localhost:7001/api/maintenance/work-orders/{workOrderId}/start
Authorization: Bearer {technician-token}
Content-Type: application/json

{
  "actualStartDate": "2025-11-10T08:15:00Z",
  "notes": "Starting preventive maintenance. Vehicle admitted to workshop bay 3."
}
```

#### Start Time Entry
```http
POST https://localhost:7001/api/maintenance/work-orders/{workOrderId}/time-entries
Authorization: Bearer {technician-token}
Content-Type: application/json

{
  "startTime": "2025-11-10T08:15:00Z",
  "activityType": "Normal",
  "description": "Preventive maintenance work"
}
```

**Save the Time Entry ID** (let's call it `{timeEntryId}`)

#### Complete Tasks (one by one)

**Task 1: Oil Change**
```http
PUT https://localhost:7001/api/maintenance/work-orders/tasks/{task1Id}/complete
Authorization: Bearer {technician-token}
Content-Type: application/json

{
  "actualHours": 0.5,
  "completionDate": "2025-11-10T08:45:00Z",
  "notes": "Oil changed. Old oil was dark but no metal particles. Used 5L 5W-30 synthetic."
}
```

**Consume Parts**
```http
PUT https://localhost:7001/api/maintenance/work-orders/parts/{partId}/consume
Authorization: Bearer {technician-token}
Content-Type: application/json

{
  "quantityUsed": 5,
  "status": "Consumed"
}
```

*Repeat for other tasks...*

#### End Time Entry
```http
PUT https://localhost:7001/api/maintenance/work-orders/time-entries/{timeEntryId}/end
Authorization: Bearer {technician-token}
Content-Type: application/json

{
  "endTime": "2025-11-10T12:30:00Z"
}
```

#### Mark Work Order as Completed
```http
PUT https://localhost:7001/api/maintenance/work-orders/{workOrderId}/complete
Authorization: Bearer {technician-token}
Content-Type: application/json

{
  "actualCompletionDate": "2025-11-10T12:30:00Z",
  "actualHours": 4.25,
  "workPerformed": "Completed all scheduled preventive maintenance tasks:\n- Engine oil and filter changed\n- Air filter replaced\n- Brake system inspected - within spec\n- Tires rotated and pressures adjusted\n- General inspection completed - no issues found",
  "completionNotes": "Vehicle is in excellent condition. All systems operating normally. Next service recommended at 100,000 km."
}
```

---

### Phase 7: Quality Inspection

#### Login as Quality Inspector
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "email": "robert.inspector@company.com",
  "password": "Password123!"
}
```

#### View Work Orders Pending Inspection
```http
GET https://localhost:7001/api/maintenance/work-orders?status=Completed&qualityCheckPassed=null
Authorization: Bearer {inspector-token}
```

#### Perform Quality Check (via Frontend)
Navigate to: `https://localhost:3000/maintenance/quality-control/inspect/{workOrderId}`

Or via API:
```http
POST https://localhost:7001/api/maintenance/quality-control/inspections
Authorization: Bearer {inspector-token}
Content-Type: application/json

{
  "workOrderId": "{workOrderId}",
  "checklistId": "{quality-checklist-id}",
  "inspectorId": "EMP00000-0000-0000-0000-000000000006"
}
```

#### Complete Inspection
```http
PUT https://localhost:7001/api/maintenance/quality-control/inspections/{inspectionId}/complete
Authorization: Bearer {inspector-token}
Content-Type: application/json

{
  "overallResult": "Pass",
  "score": 95,
  "notes": "Quality inspection passed. All work completed to standard. No issues identified.",
  "checkResults": [
    {"itemId": "qc-001", "result": "Pass"},
    {"itemId": "qc-002", "result": "Pass"},
    {"itemId": "qc-003", "result": "Pass"},
    {"itemId": "qc-004", "result": "Pass"},
    {"itemId": "qc-005", "result": "Pass"},
    {"itemId": "qc-006", "result": "Pass"},
    {"itemId": "qc-007", "result": "Pass"},
    {"itemId": "qc-008", "result": "Pass"},
    {"itemId": "qc-009", "result": "Pass"},
    {"itemId": "qc-010", "result": "Pass"}
  ]
}
```

#### Update Work Order Quality Status
```http
PUT https://localhost:7001/api/maintenance/work-orders/{workOrderId}/quality-check
Authorization: Bearer {inspector-token}
Content-Type: application/json

{
  "qualityCheckPassed": true,
  "qualityScore": 95,
  "qualityCheckDate": "2025-11-10T13:00:00Z"
}
```

---

### Phase 8: Asset Discharge & Completion

#### Login as Service Advisor
```http
POST https://localhost:7001/api/auth/login
Content-Type: application/json

{
  "email": "linda.advisor@company.com",
  "password": "Password123!"
}
```

#### Create Asset Discharge
```http
POST https://localhost:7001/api/maintenance/asset-discharge
Authorization: Bearer {advisor-token}
Content-Type: application/json

{
  "assetId": "AST00000-0000-0000-0000-000000000001",
  "jobCardId": "{jobCardId}",
  "dischargeDate": "2025-11-10T14:00:00Z",
  "finalCondition": "Excellent",
  "mileageOrHours": 85000,
  "qualityCheckPassed": true,
  "dischargeChecklist": {
    "engineOilLevel": "OK",
    "brakesOperational": "OK",
    "tiresCondition": "Good",
    "lightsWorking": "OK",
    "cleanedAndWashed": "Yes"
  },
  "dischargeNotes": "Vehicle ready for return. All maintenance completed successfully. Next service due at 100,000 km.",
  "acceptedByCustomer": true,
  "customerName": "John Fleet",
  "customerContactNumber": "+1234567001"
}
```

#### Complete Job Card
```http
PUT https://localhost:7001/api/maintenance/job-cards/{jobCardId}/complete
Authorization: Bearer {advisor-token}
Content-Type: application/json

{
  "completedDate": "2025-11-10T14:00:00Z",
  "actualHours": 4.25,
  "actualCost": 485.50,
  "completionNotes": "Preventive maintenance completed successfully. All tasks performed to specification. Customer satisfied with service.",
  "acceptedByCustomer": true,
  "customerAcceptanceDate": "2025-11-10T14:00:00Z"
}
```

---

## ✅ Verification

### Check Complete Workflow
```http
GET https://localhost:7001/api/maintenance/job-cards/{jobCardId}/history
Authorization: Bearer {token}
```

### View Work Order Details
```http
GET https://localhost:7001/api/maintenance/work-orders/{workOrderId}
Authorization: Bearer {token}
```

### View Asset Maintenance History
```http
GET https://localhost:7001/api/maintenance/assets/AST00000-0000-0000-0000-000000000001/maintenance-history
Authorization: Bearer {token}
```

---

## 🎭 Alternative Test Scenarios

### Scenario A: Rejection at Approval
```http
POST https://localhost:7001/api/maintenance/job-cards/{jobCardId}/reject
Authorization: Bearer {approver-token}
Content-Type: application/json

{
  "approvalStepId": "{step-id}",
  "rejectionReason": "Insufficient justification for maintenance at this time.",
  "comments": "Please provide more details on urgency and defer to next quarter if not critical."
}
```

### Scenario B: Quality Inspection Failure → Rework
```http
POST https://localhost:7001/api/maintenance/work-orders/{workOrderId}/rework
Authorization: Bearer {inspector-token}
Content-Type: application/json

{
  "reworkReason": "QualityIssue",
  "description": "Oil filter not properly tightened. Risk of leak detected.",
  "severity": "High",
  "assignedTechnicianId": "EMP00000-0000-0000-0000-000000000004"
}
```

---

## 📊 Expected Results Summary

| Phase | Job Card Status | Work Order Status | Key Action |
|-------|----------------|-------------------|------------|
| 1 | Draft | N/A | Creation |
| 2 | Submitted | N/A | Submit for approval |
| 3 | Submitted | N/A | Level 1 approval |
| 4 | Approved | N/A | Level 2 approval |
| 5 | Approved | Open | WO creation |
| 6 | InProgress | InProgress | Execution |
| 7 | InProgress | Completed | Quality check |
| 8 | Completed | Completed | Discharge |

---

## 🐛 Troubleshooting

### Issue: "Employee not found"
- Ensure seeder has run successfully
- Check database for employee records with expected IDs

### Issue: "Approval step not created"
- Verify `RequiresApproval` is true on Job Card
- Check maintenance type configuration

### Issue: "Parts not available"
- Verify inventory seeding completed
- Check `InventoryItems` table for part IDs

---

## 📝 Notes

- All timestamps should be in UTC
- Use the same tenant for all operations
- Save IDs from responses for subsequent API calls
- Check application logs for detailed seeding information
- Frontend URL: `https://localhost:3000` (adjust if different)
- API URL: `https://localhost:7001` (adjust if different)

---

**Test Duration:** ~2-3 hours for complete E2E workflow  
**Last Updated:** 2025-10-29
