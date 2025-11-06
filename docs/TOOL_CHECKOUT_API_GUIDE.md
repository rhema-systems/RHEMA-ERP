# Tool Checkout API - Quick Reference Guide

## Overview
The Tool Checkout API provides complete management of tool inventory, checkout/return operations, and usage tracking for maintenance operations.

## Base URL
```
/api/maintenance/tools
```

## Authentication
All endpoints require JWT Bearer token authentication.

---

## Tool Management Endpoints

### 1. Get Available Tools
**Endpoint**: `GET /api/maintenance/tools/available`

**Description**: Returns all tools with status "Available"

**Response**:
```json
[
  {
    "id": "guid",
    "toolCode": "TL-001",
    "name": "Torque Wrench",
    "description": "Digital torque wrench 0-100 Nm",
    "category": "Hand Tools",
    "status": "Available",
    "currentLocation": "Tool Room A",
    "homeLocation": "Tool Room A",
    "requiresCertification": false,
    "requiresTraining": true,
    "dailyRentalRate": 25.00,
    "lastUsedDate": "2025-11-01T10:30:00Z",
    "totalUsageDays": 45
  }
]
```

---

### 2. Get All Tools
**Endpoint**: `GET /api/maintenance/tools/all`

**Description**: Returns all active tools regardless of status

**Response**: Same as Get Available Tools

---

### 3. Get Tool By ID
**Endpoint**: `GET /api/maintenance/tools/{toolId}`

**Parameters**:
- `toolId` (path) - Tool GUID

**Response**: Single tool object

---

### 4. Check Tool Availability
**Endpoint**: `GET /api/maintenance/tools/{toolId}/availability`

**Query Parameters** (optional):
- `startDate` - Check availability from this date
- `endDate` - Check availability until this date

**Response**:
```json
{
  "toolId": "guid",
  "toolName": "Torque Wrench",
  "toolCode": "TL-001",
  "isAvailable": true,
  "currentStatus": "Available",
  "availableFrom": null,
  "upcomingCheckouts": []
}
```

---

### 5. Get Tool Checkout History
**Endpoint**: `GET /api/maintenance/tools/{toolId}/history`

**Query Parameters**:
- `limit` (optional, default: 50) - Number of recent checkouts to return

**Response**:
```json
{
  "toolId": "guid",
  "toolName": "Torque Wrench",
  "toolCode": "TL-001",
  "totalCheckouts": 127,
  "totalUsageDays": 245,
  "totalRentalCost": 6125.00,
  "recentCheckouts": [
    {
      "id": "guid",
      "toolId": "guid",
      "toolCode": "TL-001",
      "toolName": "Torque Wrench",
      "checkedOutById": "guid",
      "checkedOutByName": "John Smith",
      "checkoutDate": "2025-11-01T08:00:00Z",
      "expectedReturnDate": "2025-11-03T17:00:00Z",
      "actualReturnDate": "2025-11-03T16:30:00Z",
      "status": "Returned",
      "daysOut": 2,
      "isOverdue": false,
      "workOrderId": "guid",
      "workOrderNumber": "WO-2025-001",
      "conditionOnCheckout": "Good",
      "checkoutNotes": "For pump maintenance"
    }
  ]
}
```

---

## Checkout Operations

### 6. Checkout Tool
**Endpoint**: `POST /api/maintenance/tools/checkout`

**Query Parameters**:
- `toolId` (required) - Tool GUID
- `employeeId` (required) - Employee GUID

**Request Body**:
```json
{
  "workOrderId": "guid",  // optional
  "jobCardId": "guid",     // optional
  "expectedReturnDate": "2025-11-10T17:00:00Z",  // optional
  "checkoutNotes": "For pump maintenance work",   // optional
  "conditionOnCheckout": "Good"  // Good, Fair, Damaged
}
```

**Response**:
```json
{
  "checkoutId": "guid",
  "toolId": "guid",
  "toolName": "Torque Wrench",
  "toolCode": "TL-001",
  "checkoutDate": "2025-11-04T13:00:00Z",
  "expectedReturnDate": "2025-11-10T17:00:00Z",
  "checkedOutBy": "employee-guid",
  "status": "CheckedOut"
}
```

**Error Responses**:
- `404` - Tool not found
- `400` - Tool not available / Tool already checked out

---

### 7. Return Tool
**Endpoint**: `POST /api/maintenance/tools/return/{checkoutId}`

**Parameters**:
- `checkoutId` (path) - Checkout record GUID

**Request Body**:
```json
{
  "conditionOnReturn": "Good",  // Good, Fair, Damaged
  "returnNotes": "Tool in good condition",  // optional
  "damageReported": false,
  "damageDescription": null,  // required if damageReported = true
  "damageCost": null          // optional
}
```

**Response**:
```json
{
  "checkoutId": "guid",
  "returnDate": "2025-11-10T16:30:00Z",
  "daysCheckedOut": 6,
  "isOverdue": false,
  "overdueDays": null,
  "damageReported": false
}
```

**Note**: If tool is returned damaged or `damageReported=true`, tool status automatically changes to "Maintenance"

---

### 8. Report Tool Damage
**Endpoint**: `POST /api/maintenance/tools/checkouts/{checkoutId}/damage`

**Parameters**:
- `checkoutId` (path) - Checkout record GUID

**Request Body**:
```json
{
  "damageDescription": "Handle cracked during use",
  "estimatedCost": 150.00,  // optional
  "requiresRepair": true
}
```

**Response**:
```json
{
  "message": "Damage reported successfully"
}
```

**Note**: If `requiresRepair=true`, tool status automatically changes to "Maintenance"

---

## Query Operations

### 9. Get Active Checkouts
**Endpoint**: `GET /api/maintenance/tools/checkouts/active`

**Query Parameters** (optional):
- `employeeId` - Filter by specific employee

**Response**: Array of checkout objects (same structure as checkout history)

---

### 10. Get Overdue Checkouts
**Endpoint**: `GET /api/maintenance/tools/checkouts/overdue`

**Description**: Returns all checkouts where expected return date has passed and tool hasn't been returned

**Response**: Array of checkout objects with `isOverdue=true`

---

### 11. Get Employee Checkout History
**Endpoint**: `GET /api/maintenance/tools/employees/{employeeId}/checkouts`

**Parameters**:
- `employeeId` (path) - Employee GUID

**Query Parameters**:
- `limit` (optional, default: 50) - Number of recent checkouts

**Response**: Array of checkout objects

---

## Business Logic

### Tool Status Management
Tool status automatically changes based on operations:
- **Checkout**: `Available` → `InUse`
- **Return (Good/Fair)**: `InUse` → `Available`
- **Return (Damaged)**: `InUse` → `Maintenance`
- **Damage Report**: Current status → `Maintenance`

### Overdue Calculation
- Checkout is overdue when: `CurrentDate > ExpectedReturnDate` AND `ActualReturnDate = null`
- Overdue days calculated as: `CurrentDate - ExpectedReturnDate`

### Usage Tracking
- `TotalUsageDays` increments on tool return: `ActualReturnDate - CheckoutDate`
- `LastUsedDate` updates to `ActualReturnDate`

### Validation Rules
1. Tool must have status "Available" to be checked out
2. Tool cannot be checked out if already checked out
3. Only active tools can be checked out
4. Checkout must exist and be in "CheckedOut" status to return
5. Cannot return already returned tool

---

## Usage Examples

### Example 1: Simple Tool Checkout
```bash
# Get available tools
GET /api/maintenance/tools/available

# Checkout tool
POST /api/maintenance/tools/checkout?toolId={guid}&employeeId={guid}
{
  "conditionOnCheckout": "Good"
}

# Return tool after 3 days
POST /api/maintenance/tools/return/{checkoutId}
{
  "conditionOnReturn": "Good",
  "returnNotes": "Work completed successfully"
}
```

### Example 2: Work Order Tool Management
```bash
# Checkout tool for work order
POST /api/maintenance/tools/checkout?toolId={guid}&employeeId={guid}
{
  "workOrderId": "{work-order-guid}",
  "expectedReturnDate": "2025-11-15T17:00:00Z",
  "conditionOnCheckout": "Good",
  "checkoutNotes": "For WO-2025-045 - Compressor maintenance"
}

# Tool damaged during work
POST /api/maintenance/tools/checkouts/{checkoutId}/damage
{
  "damageDescription": "Calibration out of spec",
  "estimatedCost": 200.00,
  "requiresRepair": true
}

# Return damaged tool
POST /api/maintenance/tools/return/{checkoutId}
{
  "conditionOnReturn": "Damaged",
  "damageReported": true,
  "damageDescription": "Calibration out of spec - sent for repair",
  "damageCost": 200.00
}
```

### Example 3: Track Overdue Tools
```bash
# Get all overdue checkouts
GET /api/maintenance/tools/checkouts/overdue

# Check specific employee's active checkouts
GET /api/maintenance/tools/checkouts/active?employeeId={guid}

# View tool usage history
GET /api/maintenance/tools/{toolId}/history?limit=100
```

---

## Integration with Work Orders

Tools can be linked to work orders during checkout:
1. Set `workOrderId` in checkout request
2. Tool appears in work order's tool list
3. Tool checkout history shows work order number
4. Work order completion can trigger tool return reminder

## Integration with Job Cards

Tools can be linked to job cards:
1. Set `jobCardId` in checkout request
2. Useful for tracking tools across multiple work orders under same job card
3. Job card completion can verify all tools returned

---

## Testing the API

### Using Swagger
1. Navigate to `/swagger` endpoint
2. Authorize with JWT token
3. Expand "ToolCheckout" controller
4. Test endpoints with sample data

### Using Postman
1. Import collection from `/docs/postman/tool-checkout.json` (if available)
2. Set environment variable for Bearer token
3. Execute requests in sequence

---

## Common Issues & Solutions

### Issue: "Tool not available"
**Solution**: Check tool status - may be checked out or in maintenance

### Issue: "Tool already checked out"
**Solution**: Return the active checkout before creating new one

### Issue: "Checkout not found"
**Solution**: Verify checkoutId is correct and exists

### Issue: "Cannot return already returned tool"
**Solution**: Check if tool was already returned - view checkout history

---

## Performance Considerations

- Checkout queries are indexed on `ToolId`, `EmployeeId`, `Status`
- History queries limited to 50 results by default
- Active checkouts query is optimized with status filtering
- Overdue calculation uses database date comparison for efficiency

---

## Future Enhancements (Planned)

- [ ] Tool reservation system
- [ ] Automated overdue notifications
- [ ] Tool maintenance scheduling integration
- [ ] Barcode/QR code scanning support
- [ ] Mobile app checkout interface
- [ ] Tool usage analytics dashboard
- [ ] Predictive maintenance based on usage
