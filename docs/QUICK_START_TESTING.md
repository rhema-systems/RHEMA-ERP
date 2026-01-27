# Quick Start: Testing Auto Work Order Generation

## 🎯 Direct Answer to Your Questions

### ❓ Will work orders be generated automatically?

**YES**, work orders will be auto-generated **IF**:
1. ✅ Your schedule has `AutoGenerateWorkOrders = true` (checked in UI)
2. ✅ Your schedule `IsActive = true` (checked in UI)
3. ✅ The trigger conditions are met (e.g., NextDueDate reached)
4. ✅ The background service is running (runs every 30 minutes)

### ❓ Will notifications be triggered?

**PARTIALLY IMPLEMENTED** - The notification system exists but needs integration:
- ✅ Notification service (`MaintenanceNotificationService`) is registered
- ❌ NOT currently called by the background trigger evaluation service
- ❌ Requires additional integration work

---

## 🚀 How to Test Work Order Auto-Generation (3 Methods)

### Method 1: Wait for Background Service (Automatic - Every 30 minutes)

The background service runs automatically every 30 minutes. It will:
1. Check all active schedules
2. Evaluate trigger conditions
3. Generate work orders for schedules that meet criteria

**When does it run?**
- First run: 5 minutes after API startup
- Subsequent runs: Every 30 minutes after that

**Check API logs for**:
```
[Information] Maintenance Trigger Evaluation Background Service is starting
[Information] Starting scheduled trigger evaluation
[Information] Trigger evaluation completed. Generated {Count} work orders
```

### Method 2: Manual Trigger via API (Immediate Testing) ⭐ RECOMMENDED

I just created a test endpoint for you! Use this to trigger evaluation immediately:

**Using Swagger:**
1. Open: `http://localhost:5000/swagger`
2. Find: `MaintenanceTriggerTest` section
3. Use endpoint: **POST** `/api/maintenance/trigger-test/evaluate-all`
4. Click "Try it out" → "Execute"

**Response will show:**
```json
{
  "message": "Evaluation completed. Generated 2 work orders.",
  "workOrdersGenerated": 2,
  "timestamp": "2025-01-07T13:05:37Z"
}
```

**Using PowerShell:**
```powershell
# Replace YOUR_AUTH_TOKEN with your actual JWT token
$token = "YOUR_AUTH_TOKEN"
$headers = @{ "Authorization" = "Bearer $token" }

Invoke-RestMethod -Uri "http://localhost:5000/api/maintenance/trigger-test/evaluate-all" -Method Post -Headers $headers
```

### Method 3: Restart the API

When the API restarts, the background service starts and will run its first evaluation after 5 minutes.

---

## ✅ Verify Work Orders Were Generated

### Check in UI:
1. Navigate to: `http://localhost:3000/maintenance/work-orders`
2. Look for new work orders with titles like:
   - "[Schedule Name] - Scheduled Maintenance"
   - Status: "Pending" or "Open"
3. Check if the work order has:
   - ✅ `MaintenanceScheduleId` populated
   - ✅ Due date set appropriately
   - ✅ Asset linked

### Check in Database (SQL):
```sql
-- View recently generated work orders
SELECT TOP 10 
    wo.WorkOrderNumber,
    wo.Title,
    wo.Status,
    wo.DueDate,
    s.Name AS ScheduleName,
    wo.CreatedAt
FROM WorkOrders wo
LEFT JOIN MaintenanceSchedules s ON wo.MaintenanceScheduleId = s.Id
WHERE wo.MaintenanceScheduleId IS NOT NULL
ORDER BY wo.CreatedAt DESC;

-- Check if your schedule has been processed
SELECT 
    Name,
    NextDueDate,
    LastGeneratedDate,
    LastProcessedDate,
    AutoGenerateWorkOrders,
    IsActive
FROM MaintenanceSchedules
WHERE CAST(NextDueDate AS DATE) <= CAST(GETDATE() AS DATE)
AND IsActive = 1;
```

---

## 🔍 Troubleshooting: Why No Work Order?

### Check 1: Verify Schedule Configuration

Run this SQL to see your schedule details:
```sql
SELECT 
    Id,
    Name,
    NextDueDate,
    DATEDIFF(day, GETDATE(), NextDueDate) AS DaysUntilDue,
    PrimaryTriggerType,
    IsActive,
    AutoGenerateWorkOrders,
    LastGeneratedDate,
    LastProcessedDate
FROM MaintenanceSchedules
WHERE Name LIKE '%YOUR_SCHEDULE_NAME%';
```

**Required values:**
- `IsActive` = 1 ✅
- `AutoGenerateWorkOrders` = 1 ✅
- `NextDueDate` <= Today ✅
- `LastGeneratedDate` = NULL or old date

### Check 2: Verify Background Service is Running

**Check API startup logs:**
Look for this message when API starts:
```
[Information] Maintenance Trigger Evaluation Background Service is starting
```

**Check evaluation logs:**
```
[Information] Starting scheduled trigger evaluation
[Information] Starting maintenance schedule evaluation
[Information] Schedule evaluation completed. Generated {Count} work orders
```

If you don't see these, the background service might not be running.

### Check 3: Use Manual Trigger to See Errors

Call the test endpoint to see immediate results and error messages:
```
POST /api/maintenance/trigger-test/evaluate-all
```

Check the response for errors or check API logs for detailed error messages.

### Check 4: Common Issues

**Issue: Schedule already generated a work order**
- Check `LastGeneratedDate` in the schedule
- The service might not generate multiple work orders for the same due date
- Solution: Complete existing work order or manually reset `LastGeneratedDate` to NULL

**Issue: NextDueDate not actually passed**
- The service uses UTC time comparison
- Check: `DateTime.UtcNow >= schedule.NextDueDate`
- Solution: Ensure NextDueDate is set to past or current UTC time

**Issue: Permission/tenant issues**
- The background service runs in system context
- Might need proper tenant context setup
- Check API logs for "Error evaluating schedule" messages

---

## 📊 Monitoring the Background Service

### Get Service Information:
```
GET /api/maintenance/trigger-test/service-info
```

Response:
```json
{
  "backgroundServiceRunning": true,
  "evaluationInterval": "30 minutes",
  "initialDelay": "5 minutes after startup",
  "message": "Background service evaluates all active schedules automatically every 30 minutes",
  "manualTrigger": "Use POST /api/maintenance/trigger-test/evaluate-all to trigger immediately"
}
```

### Change Evaluation Interval (Optional)

Edit: `src/ErpSystem.Api/Services/Maintenance/MaintenanceTriggerEvaluationBackgroundService.cs`

Line 15:
```csharp
private readonly TimeSpan _evaluationInterval = TimeSpan.FromMinutes(30); // Change to 5 for faster testing
```

---

## 🔔 About Notifications

### Current Status:
The `MaintenanceNotificationService` is implemented but **NOT integrated** with the auto-generation process.

### What's Implemented:
- ✅ `SendMaintenanceDueNotificationsAsync()` - Checks upcoming and overdue schedules
- ✅ `SendScheduleReminderAsync()` - Sends reminders for specific schedules
- ✅ `SendOverdueMaintenanceAlertAsync()` - Sends alerts for overdue maintenance
- ✅ `SendWorkOrderAssignmentNotificationAsync()` - Notifies when work order assigned

### What's Missing:
The notification service needs to be called by:
1. **Background service** - Should call notification service during evaluation
2. **Work order creation** - Should send notifications when work orders are auto-generated

### To Enable Notifications:

You need to modify `MaintenanceTriggerEvaluationBackgroundService.cs` to inject and call the notification service.

**Would you like me to implement notification integration now?**

---

## 📝 Quick Test Checklist

- [ ] API is running (`http://localhost:5000`)
- [ ] Schedule created with NextDueDate = today or past
- [ ] Schedule has `AutoGenerateWorkOrders` = true
- [ ] Schedule has `IsActive` = true
- [ ] Open Swagger: `http://localhost:5000/swagger`
- [ ] Find `MaintenanceTriggerTest` → POST `/evaluate-all`
- [ ] Click "Try it out" → "Execute"
- [ ] Check response for `workOrdersGenerated` > 0
- [ ] Navigate to Work Orders page to see generated work order
- [ ] Check schedule's `LastGeneratedDate` updated

---

## 🎓 Understanding the System

### How Auto-Generation Works:

```
1. Background Service Runs (every 30 min)
   ↓
2. Gets all active schedules with AutoGenerateWorkOrders = true
   ↓
3. For each schedule, evaluates trigger:
   - Time-based: Is NextDueDate <= Now?
   - Usage-based: Has mileage/hours threshold been reached?
   - Condition-based: Are condition criteria met?
   - Combined: Checks AND/OR logic
   ↓
4. If trigger met:
   - Creates work order
   - Updates schedule.NextDueDate
   - Sets schedule.LastGeneratedDate
   - Creates history record
   ↓
5. Logs results
```

### Time-Based Trigger Logic:
```csharp
if (DateTime.UtcNow >= schedule.NextDueDate)
{
    // Generate work order
    // Update NextDueDate by adding frequency interval
    NextDueDate = NextDueDate + FrequencyValue (Days/Weeks/Months)
}
```

---

## 🚀 Next Steps

1. **Test immediately**: Use the manual trigger endpoint
2. **Check work orders**: Verify generation in UI and database
3. **Review logs**: Check API logs for evaluation messages
4. **Enable notifications** (optional): Let me know if you want me to integrate notifications
5. **Test usage-based triggers**: Add usage records to test usage-based schedules

---

## Need Help?

**If work orders aren't generating:**
1. Use the manual trigger endpoint first
2. Check API logs for errors
3. Verify schedule configuration with SQL queries
4. Ensure background service started successfully

**If you want notifications:**
Let me know and I'll integrate the notification service with the background trigger evaluation.

Happy testing! 🎉
