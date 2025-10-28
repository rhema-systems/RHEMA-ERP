# Work Order Task Tracking Guide

## Where to Find Work Order Task Progress

### Location
When you open a **Work Order** in the Work Order Management page, you'll find the task list in an **expandable "Tasks" accordion section**.

### Path to View Tasks
1. Navigate to **Work Order Management** page
2. Click on any work order to view details
3. Look for the **"Tasks (X)"** accordion (where X is the number of tasks)
4. Click to expand and see all tasks

### Task Information Displayed

Each task shows:
- ✅ **Task Name** - The name of the maintenance task
- 📝 **Description** - Details about what needs to be done
- 👤 **Assigned Technician** - The technician responsible for this task
- ⏱️ **Hours** - Actual hours vs. Estimated hours (e.g., "2.5h / 4h")
- 🎯 **Status** - Current status with color coding:
  - ✅ **Completed** (Green) - Task is finished
  - 🔄 **InProgress** (Orange) - Task is being worked on
  - ⏸️ **Pending** (Grey) - Task hasn't started yet
  - ❌ **Skipped** - Task was skipped (if applicable)

### How Tasks are Created

Tasks are automatically created when a work order is generated from templates:

1. **MaintenanceTaskTemplates** - System-wide default tasks for each maintenance type
2. **AssetTypeTaskTemplates** - Tasks specific to the asset type (e.g., Vehicle, Equipment)
3. **AssetTaskTemplates** - Tasks specific to individual assets (highest priority)

### Technician Assignment

- Technicians are assigned to tasks from the **task templates**
- When a work order is created, the `AssignedTechnicianId` from the template is copied to the work order task
- This ensures each task knows which technician should work on it

### Backend API

The task data comes from:
- **Endpoint**: `GET /api/maintenance/work-orders/{id}`
- **Response**: Returns `WorkOrderDto` which includes a `Tasks` collection
- **Task Properties**:
  ```json
  {
    "id": "guid",
    "taskName": "string",
    "description": "string",
    "status": "Pending|InProgress|Completed|Skipped",
    "estimatedHours": 2.5,
    "actualHours": 1.0,
    "assignedTechnicianId": "guid",
    "assignedTechnician": {
      "id": "guid",
      "fullName": "John Smith",
      "employeeNumber": "EMP001"
    },
    "completedAt": "2024-10-22T10:30:00Z",
    "isRequired": true
  }
  ```

### Frontend Component

The work order task display is implemented in:
- **Component**: `WorkOrderManagement.tsx`
- **Section**: Lines 1005-1048 (Tasks Accordion)
- **Service**: `workOrderService.getWorkOrderById(id)` fetches tasks from API

### Workflow Progress

The work order page also shows a **workflow stepper** that visualizes the overall maintenance workflow:
1. Work Order Created
2. Asset Admitted
3. Maintenance In Progress ← Tasks are executed here
4. Work Completed
5. Quality Check
6. Asset Discharged
7. Certificate Issued

### Task Completion

To mark a task as complete:
1. Update the task status to "Completed"
2. Record actual hours worked
3. The completion percentage of the work order automatically updates based on completed tasks
4. Once all required tasks are done, the work order can be marked as completed

### Technician View

Technicians can:
- See all tasks assigned to them
- View task details and requirements
- Update task progress
- Mark tasks as complete
- Record actual hours spent

### Recent Updates

**2024-10-22**: 
- Added `AssignedTechnicianId` field to all task template entities
- Updated task creation to copy technician assignments from templates
- Seeded existing task templates with technician IDs
- Fixed frontend to fetch real tasks from API instead of generating mock data

## Troubleshooting

### Tasks Not Showing
- Verify the work order was created from templates
- Check that task templates exist for the maintenance type
- Ensure the API endpoint is returning tasks in the response

### No Technician Assigned
- Check if the task template has an `AssignedTechnicianId`
- Verify technicians exist in the Technicians table
- Run the seed script to assign technicians to existing templates

### Empty Task List
- The work order may have been created before tasks were implemented
- Check the database `WorkOrderTasks` table for the specific `WorkOrderId`
- Create work orders from scratch to get tasks from templates
