# Maintenance Module API Reference

## Overview

The Maintenance API provides comprehensive endpoints for managing assets, work orders, preventive maintenance, technician resources, and safety compliance. All endpoints require authentication and support multi-tenant operations.

## 🔐 Authentication

All API endpoints require JWT Bearer token authentication:

```http
Authorization: Bearer <your-jwt-token>
```

## 🌐 Base URL

```
https://your-domain.com/api/maintenance/
```

## 📋 API Categories

### Core Asset Management
- [**Assets**](#assets-api) - Asset lifecycle management
- [**Asset Categories**](#asset-categories-api) - Asset categorization
- [**Asset Types**](#asset-types-api) - Asset type configuration

### Work Order Management  
- [**Work Orders**](#work-orders-api) - Work order operations
- [**Work Order Types**](#work-order-types-api) - Work order classification
- [**Emergency Maintenance**](#emergency-maintenance-api) - Emergency work orders

### Preventive Maintenance
- [**Maintenance Schedules**](#maintenance-schedules-api) - Preventive maintenance scheduling
- [**Maintenance Types**](#maintenance-types-api) - Maintenance classification

### Resource Management
- [**Technicians**](#technicians-api) - Technician management
- [**Technical Skills**](#technical-skills-api) - Skill management
- [**Resource Allocation**](#resource-allocation-api) - Resource planning

### Safety & Compliance
- [**Safety Protocols**](#safety-protocols-api) - Safety protocol management

### Analytics & Reporting
- [**Dashboard**](#dashboard-api) - Analytics dashboard data
- [**History**](#history-api) - Historical data and reports

### Mobile API
- [**Mobile Work Orders**](#mobile-api) - Mobile-optimized endpoints

### System Configuration
- [**Priority Levels**](#priority-levels-api) - Priority configuration
- [**Attachments**](#attachments-api) - File management

---

## 📱 Assets API

### Get All Assets
```http
GET /api/maintenance/assets
```

**Query Parameters:**
- `page` (int, optional): Page number (default: 1)
- `pageSize` (int, optional): Page size (default: 20)
- `searchTerm` (string, optional): Search term for filtering
- `categoryId` (guid, optional): Filter by category
- `status` (string, optional): Filter by status

**Response:**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Production Line A",
      "assetNumber": "PL-001",
      "description": "Main production line",
      "status": "Active",
      "criticality": "High",
      "location": "Factory Floor 1",
      "manufacturer": "ACME Corp",
      "model": "PL-2000",
      "serialNumber": "SN123456",
      "purchaseDate": "2023-01-15T00:00:00Z",
      "purchasePrice": 250000.00,
      "currentValue": 200000.00,
      "category": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "name": "Production Equipment",
        "code": "PROD"
      }
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

### Get Asset by ID
```http
GET /api/maintenance/assets/{id}
```

### Create Asset
```http
POST /api/maintenance/assets
```

**Request Body:**
```json
{
  "name": "New Production Line",
  "assetNumber": "PL-002",
  "description": "Secondary production line",
  "assetCategoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "assetTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "manufacturer": "ACME Corp",
  "model": "PL-3000",
  "serialNumber": "SN789012",
  "location": "Factory Floor 2",
  "building": "Building A",
  "floor": "Floor 1",
  "room": "Room 101",
  "status": "Active",
  "criticality": "High",
  "purchaseDate": "2024-01-15T00:00:00Z",
  "purchasePrice": 300000.00,
  "employeeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Update Asset
```http
PUT /api/maintenance/assets/{id}
```

### Delete Asset
```http
DELETE /api/maintenance/assets/{id}
```

### Get Asset Hierarchy
```http
GET /api/maintenance/assets/{id}/hierarchy
```

### Update Asset Metrics
```http
PUT /api/maintenance/assets/{id}/operating-hours
PUT /api/maintenance/assets/{id}/mileage
```

---

## 📋 Work Orders API

### Get All Work Orders
```http
GET /api/maintenance/workorders
```

**Query Parameters:**
- `page` (int, optional): Page number
- `pageSize` (int, optional): Page size  
- `status` (string, optional): Filter by status
- `priority` (string, optional): Filter by priority
- `assetId` (guid, optional): Filter by asset
- `technicianId` (guid, optional): Filter by technician
- `startDate` (datetime, optional): Filter by start date
- `endDate` (datetime, optional): Filter by end date

**Response:**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "workOrderNumber": "WO-2024-001",
      "title": "Monthly Inspection - Production Line A",
      "description": "Regular monthly maintenance inspection",
      "status": "In Progress",
      "priority": "Medium",
      "scheduledStartDate": "2024-10-15T09:00:00Z",
      "scheduledEndDate": "2024-10-15T17:00:00Z",
      "actualStartDate": "2024-10-15T09:15:00Z",
      "estimatedHours": 8.0,
      "actualHours": 6.5,
      "asset": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "name": "Production Line A",
        "assetNumber": "PL-001"
      },
      "assignedTechnician": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "name": "John Smith",
        "employeeNumber": "EMP-001"
      }
    }
  ],
  "totalCount": 75,
  "page": 1,
  "pageSize": 20
}
```

### Create Work Order
```http
POST /api/maintenance/workorders
```

**Request Body:**
```json
{
  "title": "Emergency Repair - Conveyor Belt",
  "description": "Conveyor belt motor failure - urgent repair needed",
  "assetId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "workOrderTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "priority": "Critical",
  "scheduledStartDate": "2024-10-15T09:00:00Z",
  "scheduledEndDate": "2024-10-15T17:00:00Z",
  "estimatedHours": 6.0,
  "estimatedCost": 1500.00,
  "assignedTechnicianId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "instructions": "Replace motor and test operation",
  "safetyNotes": "Lock out tag out procedure required",
  "requiredSkills": ["Electrical", "Mechanical"],
  "requiredTools": ["Multimeter", "Torque Wrench"],
  "requiredParts": [
    {
      "partNumber": "MOTOR-001",
      "quantity": 1,
      "description": "Conveyor Motor 5HP"
    }
  ]
}
```

### Update Work Order Status
```http
PUT /api/maintenance/workorders/{id}/status
```

**Request Body:**
```json
{
  "status": "In Progress",
  "notes": "Started work on motor replacement"
}
```

### Assign Work Order
```http
PUT /api/maintenance/workorders/{id}/assign
```

### Start Work Order
```http
POST /api/maintenance/workorders/{id}/start
```

### Complete Work Order
```http
POST /api/maintenance/workorders/{id}/complete
```

**Request Body:**
```json
{
  "actualHours": 5.5,
  "actualCost": 1350.00,
  "completionNotes": "Motor replaced successfully, system tested and operational",
  "partsUsed": [
    {
      "partNumber": "MOTOR-001",
      "quantityUsed": 1,
      "cost": 850.00
    }
  ],
  "laborHours": [
    {
      "technicianId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "hours": 5.5,
      "hourlyRate": 75.00
    }
  ]
}
```

---

## 📅 Maintenance Schedules API

### Get All Schedules
```http
GET /api/maintenance/schedules
```

### Create Schedule
```http
POST /api/maintenance/schedules
```

**Request Body:**
```json
{
  "name": "Monthly Equipment Inspection",
  "code": "MI-001",
  "description": "Regular monthly inspection of production equipment",
  "assetId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "maintenanceTypeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "frequency": "Monthly",
  "frequencyValue": 1,
  "frequencyUnit": "Month",
  "nextDueDate": "2024-11-15T00:00:00Z",
  "priority": "Medium",
  "estimatedHours": 4.0,
  "estimatedCost": 300.00,
  "assignedTechnicianId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "instructions": "Inspect all mechanical components and lubricate as needed",
  "safetyNotes": "Ensure equipment is shut down before inspection",
  "autoGenerateWorkOrders": true,
  "advanceNotificationDays": 7,
  "isActive": true
}
```

### Generate Work Order from Schedule
```http
POST /api/maintenance/schedules/{id}/generate-workorder
```

---

## 👨‍🔧 Technicians API

### Get All Technicians
```http
GET /api/maintenance/technicians
```

**Query Parameters:**
- `page` (int): Page number
- `pageSize` (int): Page size
- `searchTerm` (string): Search by name or employee number
- `skillId` (guid): Filter by skill
- `isActive` (bool): Filter by active status

### Get Technician Availability
```http
GET /api/maintenance/technicians/{id}/availability
```

**Query Parameters:**
- `date` (datetime): Date to check availability
- `startTime` (datetime): Start time
- `endTime` (datetime): End time

### Get Technician Workload
```http
GET /api/maintenance/technicians/{id}/workload
```

**Query Parameters:**
- `startDate` (datetime): Period start date
- `endDate` (datetime): Period end date

---

## 🛡️ Safety Protocols API

### Get All Protocols
```http
GET /api/maintenance/safety-protocols
```

### Create Protocol
```http
POST /api/maintenance/safety-protocols
```

**Request Body:**
```json
{
  "title": "Lockout/Tagout Procedure",
  "code": "LOTO-001",
  "description": "Standard lockout/tagout procedure for electrical equipment",
  "category": "Electrical Safety",
  "version": "1.0",
  "effectiveDate": "2024-01-01T00:00:00Z",
  "reviewDate": "2024-12-31T00:00:00Z",
  "isMandatory": true,
  "riskLevel": "High",
  "regulatoryStandard": "OSHA 1910.147",
  "applicableAssetTypes": ["Electrical Equipment"],
  "requiredTraining": true,
  "trainingIntervalMonths": 12,
  "steps": [
    {
      "stepNumber": 1,
      "description": "Notify affected employees of shutdown",
      "isCritical": true
    },
    {
      "stepNumber": 2,
      "description": "Shut down equipment using normal procedures",
      "isCritical": true
    }
  ]
}
```

### Record Protocol Adherence
```http
POST /api/maintenance/safety-protocols/{id}/adherence
```

### Record Protocol Violation
```http
POST /api/maintenance/safety-protocols/{id}/violations
```

---

## 📊 Dashboard API

### Get Dashboard Data
```http
GET /api/maintenance/dashboard
```

**Response:**
```json
{
  "summary": {
    "totalAssets": 150,
    "activeWorkOrders": 25,
    "overdueWorkOrders": 3,
    "availableTechnicians": 12,
    "criticalAssets": 8
  },
  "workOrdersByStatus": {
    "pending": 10,
    "inProgress": 15,
    "completed": 45,
    "cancelled": 2
  },
  "maintenanceMetrics": {
    "averageCompletionTime": 4.5,
    "scheduledComplianceRate": 95.2,
    "totalMaintenanceCost": 125000.00
  },
  "upcomingMaintenance": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "assetName": "Production Line A",
      "maintenanceType": "Monthly Inspection",
      "dueDate": "2024-10-20T00:00:00Z",
      "priority": "Medium"
    }
  ]
}
```

### Get KPI Metrics
```http
GET /api/maintenance/dashboard/kpis
```

**Query Parameters:**
- `startDate` (datetime): Period start date
- `endDate` (datetime): Period end date

---

## 📱 Mobile API

### Get Assigned Work Orders (Mobile)
```http
GET /api/mobile/maintenance/workorders/assigned
```

### Start Work Order (Mobile)
```http
POST /api/mobile/maintenance/workorders/{id}/start
```

### Update Work Order Progress (Mobile)
```http
PUT /api/mobile/maintenance/workorders/{id}/progress
```

**Request Body:**
```json
{
  "progressPercentage": 75,
  "statusNotes": "Motor replacement 75% complete",
  "hoursSpent": 4.5,
  "attachments": [
    {
      "fileName": "progress_photo.jpg",
      "fileData": "base64-encoded-image-data",
      "description": "Progress photo showing motor installation"
    }
  ]
}
```

---

## 🔧 Common Response Formats

### Success Response
```json
{
  "success": true,
  "data": { /* response data */ },
  "message": "Operation completed successfully"
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed",
    "details": [
      {
        "field": "name",
        "message": "Name is required"
      }
    ]
  }
}
```

### Paged Response
```json
{
  "data": [ /* array of items */ ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## 📋 Standard HTTP Status Codes

- **200 OK** - Request successful
- **201 Created** - Resource created successfully
- **204 No Content** - Request successful, no content returned
- **400 Bad Request** - Invalid request data
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Resource not found
- **409 Conflict** - Resource conflict (e.g., duplicate)
- **422 Unprocessable Entity** - Validation errors
- **500 Internal Server Error** - Server error

## 🔐 Security & Rate Limiting

### Authentication
- All endpoints require valid JWT Bearer token
- Tokens must include appropriate maintenance module permissions

### Rate Limiting
- Standard endpoints: 1000 requests per hour per user
- Bulk operations: 100 requests per hour per user
- Mobile endpoints: 2000 requests per hour per user

### Tenant Isolation
- All data is automatically filtered by tenant
- Cross-tenant data access is not possible
- Tenant context is derived from JWT token

---

*Last Updated: October 2024*