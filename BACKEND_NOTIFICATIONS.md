# Backend Notification System - Implementation Guide

## Overview

The backend unified notification system provides a single consolidated interface for all notification delivery across the ERP system, replacing separate domain-specific implementations with a unified approach.

---

## Architecture

### Location
- **Main Service:** `ErpSystem.Api.Services.UnifiedNotificationService.cs`
- **Interface:** `ErpSystem.Core.Interfaces.INotificationService`
- **Entity:** `ErpSystem.Core.Entities.Notification.cs`
- **Repository:** `ErpSystem.Data.Repositories.NotificationRepository.cs`
- **DTOs:** `ErpSystem.Core.DTOs.Notifications.NotificationDtos.cs`
- **SignalR Hub:** `ErpSystem.Api.Hubs.DashboardHub.cs`

### Core Components

1. **INotificationService** - Unified interface for all notification operations
2. **UnifiedNotificationService** - Main implementation (~195+ methods)
3. **Notification Entity** - Generic entity for all notification types
4. **NotificationRepository** - Data access layer
5. **SignalR Hub** - Real-time delivery via WebSocket

---

## Data Model

### Notification Entity

**File:** `ErpSystem.Core.Entities.Notification.cs`

```csharp
public class Notification : BaseEntity
{
    public string NotificationType { get; set; }      // Email, SMS, Push, InApp
    public string Title { get; set; }
    public string Message { get; set; }
    public string Priority { get; set; }             // Low, Normal, High, Critical
    public string Status { get; set; }               // Pending, Sent, Failed, Expired, Cancelled
    public string RecipientId { get; set; }          // User receiving the notification
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public string LastError { get; set; }
    
    // Entity Linking (for smart navigation)
    public string EntityType { get; set; }           // JobCard, WorkOrder, Invoice, Customer, etc.
    public string EntityId { get; set; }             // Specific entity instance ID
    
    // Delivery Information
    public string DeliveryMethods { get; set; }      // Comma-separated: Email, SMS, Push, InApp
    public string EmailAddress { get; set; }
    public string PhoneNumber { get; set; }
    public string ActionUrl { get; set; }
    
    // Additional Data
    public string AdditionalData { get; set; }       // JSON for extra metadata
    
    // Multi-tenancy
    public string TenantId { get; set; }
    
    public Notification() { }
}
```

### CreateNotificationDto

**File:** `ErpSystem.Core.DTOs.Notifications.NotificationDtos.cs`

```csharp
public class CreateNotificationDto
{
    [Required]
    public string RecipientId { get; set; }
    
    [Required]
    public string Type { get; set; }  // Email, SMS, Push, InApp
    
    [Required]
    public string Title { get; set; }
    
    [Required]
    public string Message { get; set; }
    
    [Required]
    public string Priority { get; set; }  // Low, Normal, High, Critical
    
    [Required]
    public string EntityType { get; set; }  // JobCard, WorkOrder, Invoice, etc.
    
    [Required]
    public string EntityId { get; set; }    // Specific entity ID
    
    public string ActionUrl { get; set; }
    
    public Dictionary<string, object> Metadata { get; set; }
}
```

---

## Key Services

### INotificationService Interface

**Location:** `ErpSystem.Core.Interfaces.INotificationService`

```csharp
public interface INotificationService
{
    // CRUD Operations
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto);
    Task<NotificationDto> GetNotificationAsync(string id);
    Task<PagedResult<NotificationDto>> GetNotificationsAsync(PaginationParams paginationParams);
    Task<NotificationDto> UpdateNotificationAsync(string id, UpdateNotificationDto dto);
    Task DeleteNotificationAsync(string id);
    
    // Filtering
    Task<PagedResult<NotificationDto>> GetNotificationsByStatusAsync(string status, PaginationParams paginationParams);
    Task<PagedResult<NotificationDto>> GetNotificationsByPriorityAsync(string priority, PaginationParams paginationParams);
    Task<PagedResult<NotificationDto>> GetNotificationsByEntityAsync(string entityType, string entityId, PaginationParams paginationParams);
    
    // Read Status
    Task MarkAsReadAsync(string id);
    Task MarkAllAsReadAsync(string recipientId);
    Task<int> GetUnreadCountAsync(string recipientId);
    
    // Delivery Methods
    Task SendEmailAsync(string recipientId, string title, string message, bool isHtml = true);
    Task SendSmsAsync(string recipientId, string message);
    Task SendPushAsync(string recipientId, string title, string message);
    Task SendInAppAsync(string recipientId, string title, string message);
    
    // Direct Notifications
    Task SendEmailDirectAsync(string emailAddress, string title, string message, bool isHtml = true);
    Task SendSmsDirectAsync(string phoneNumber, string message);
    
    // Real-time
    Task BroadcastAsync(NotificationDto notification);
    Task BroadcastToUserAsync(string recipientId, NotificationDto notification);
    Task BroadcastToGroupAsync(string groupName, NotificationDto notification);
    
    // Statistics & Analytics
    Task<NotificationStatsDto> GetStatisticsAsync(string recipientId, DateRange dateRange);
    Task<List<NotificationAnalyticsDto>> GetAnalyticsAsync(DateRange dateRange);
    
    // Preferences
    Task<NotificationPreferencesDto> GetPreferencesAsync(string recipientId);
    Task UpdatePreferencesAsync(string recipientId, NotificationPreferencesDto preferences);
    
    // Background Processing
    Task ProcessPendingNotificationsAsync();
    Task ProcessExpiredNotificationsAsync();
    Task RetryFailedNotificationsAsync(int maxAttempts = 5);
    
    // Admin Operations
    Task<List<NotificationDto>> GetPendingNotificationsAsync(PaginationParams paginationParams);
    Task<List<NotificationDto>> GetDeadLettersAsync(PaginationParams paginationParams);
    Task RetryDeadLetterAsync(string id);
    Task RetryAllDeadLettersAsync();
    Task DeleteExpiredNotificationsAsync();
}
```

### UnifiedNotificationService Implementation

**Location:** `ErpSystem.Api.Services.UnifiedNotificationService.cs`

Implements 195+ methods covering:

1. **Basic CRUD** - Create, Read, Update, Delete notifications
2. **Filtering** - By status, priority, entity type, date range
3. **Delivery Methods** - Email, SMS, Push, In-App
4. **Real-time** - SignalR broadcasting
5. **Statistics** - Analytics and reporting
6. **Preferences** - User notification settings
7. **Background Jobs** - Pending/expired processing
8. **Admin Operations** - Dead letter handling, retries

---

## Integration with Other Modules

### Maintenance Module Example

**When Job Card is submitted:**

```csharp
// In MaintenanceService.cs
public async Task SubmitJobCardAsync(string jobCardId)
{
    var jobCard = await _unitOfWork.JobCards.GetByIdAsync(jobCardId);
    
    // Perform submission logic
    jobCard.Status = "Submitted";
    await _unitOfWork.SaveChangesAsync();
    
    // Send notification through unified service
    await _notificationService.CreateNotificationAsync(new CreateNotificationDto
    {
        RecipientId = jobCard.ManagerId,
        Type = "InApp",
        Title = "Job Card Submitted",
        Message = $"Job Card {jobCard.Number} submitted for approval",
        Priority = "High",
        EntityType = "JobCard",
        EntityId = jobCard.Id,
        ActionUrl = null  // Frontend will compute: /maintenance/job-cards/{id}
    });
}
```

### How It Works

1. **Module Event** - Maintenance action (submission, approval, etc.)
2. **Service Call** - Call `_notificationService.CreateNotificationAsync()`
3. **Entity Tracking** - Save with EntityType="JobCard", EntityId=specific-id
4. **Database** - Persisted to Notification table
5. **SignalR** - Real-time broadcast to recipient
6. **Frontend** - Automatically routed to correct page

---

## Database Schema

### Migration

**File:** `ErpSystem.Data.Migrations.20251107170106_CreateNotificationTable`

```sql
CREATE TABLE Notifications (
    Id NVARCHAR(MAX) PRIMARY KEY,
    NotificationType NVARCHAR(50) NOT NULL,
    Title NVARCHAR(255) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    Priority NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    RecipientId NVARCHAR(MAX) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    ReadAt DATETIME2 NULL,
    DismissedAt DATETIME2 NULL,
    ScheduledFor DATETIME2 NULL,
    SentAt DATETIME2 NULL,
    ExpiresAt DATETIME2 NULL,
    AttemptCount INT NOT NULL DEFAULT 0,
    LastError NVARCHAR(MAX) NULL,
    EntityType NVARCHAR(100) NULL,
    EntityId NVARCHAR(MAX) NULL,
    DeliveryMethods NVARCHAR(500) NULL,
    EmailAddress NVARCHAR(255) NULL,
    PhoneNumber NVARCHAR(20) NULL,
    ActionUrl NVARCHAR(MAX) NULL,
    AdditionalData NVARCHAR(MAX) NULL,
    TenantId NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Notifications_RecipientId ON RecipientId,
    INDEX IX_Notifications_Status ON Status,
    INDEX IX_Notifications_Priority ON Priority,
    INDEX IX_Notifications_EntityType ON EntityType,
    INDEX IX_Notifications_TenantId ON TenantId
);
```

---

## Dependency Injection Registration

**File:** `ErpSystem.Api.Extensions.ServiceCollectionExtensions.cs` (Line 252)

```csharp
public static IServiceCollection AddNotificationServices(this IServiceCollection services)
{
    services.AddScoped<INotificationRepository, NotificationRepository>();
    services.AddScoped<INotificationService, UnifiedNotificationService>();
    
    // Background job service for processing pending notifications
    services.AddScoped<INotificationBackgroundService, NotificationBackgroundService>();
    
    return services;
}
```

**In Startup/Program.cs:**

```csharp
services.AddNotificationServices();
```

---

## API Endpoints

### REST Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/notifications` | Get paginated notifications |
| GET | `/api/notifications/{id}` | Get specific notification |
| POST | `/api/notifications` | Create notification |
| PUT | `/api/notifications/{id}` | Update notification |
| DELETE | `/api/notifications/{id}` | Delete notification |
| POST | `/api/notifications/{id}/mark-read` | Mark as read |
| POST | `/api/notifications/mark-all-read` | Mark all as read |
| GET | `/api/notifications/unread-count` | Get unread count |
| GET | `/api/notifications/preferences` | Get user preferences |
| PUT | `/api/notifications/preferences` | Update preferences |
| GET | `/api/notifications/admin/pending` | Get pending (admin only) |
| GET | `/api/notifications/admin/dead-letters` | Get failed (admin only) |

### SignalR Endpoints

**Hub:** `/api/hubs/dashboard`

**Events:**
- `NewNotification` - Real-time notification delivery
- `NotificationRead` - When notification marked as read
- `NotificationDeleted` - When notification deleted

---

## Best Practices

### 1. Always Provide EntityType and EntityId

```csharp
// ✅ Good - Frontend can navigate to specific entity
await _notificationService.CreateNotificationAsync(new CreateNotificationDto
{
    EntityType = "Invoice",
    EntityId = "inv-12345",
    // ...
});

// ❌ Bad - Frontend can't navigate
await _notificationService.CreateNotificationAsync(new CreateNotificationDto
{
    EntityType = "General",
    EntityId = null,
    // ...
});
```

### 2. Use Consistent Entity Type Naming

```csharp
// ✅ Good - PascalCase
EntityType = "JobCard"
EntityType = "WorkOrder"
EntityType = "Invoice"

// ❌ Bad - Inconsistent
EntityType = "job_card"
EntityType = "work-order"
EntityType = "INVOICE"
```

### 3. URL-Safe Entity IDs

```csharp
// ✅ Good - URL safe
EntityId = "jc-20251107-001"
EntityId = "6d4a8f9e-2b5c-4e1a-9c3d-7f2b8e1a4c5d"

// ❌ Bad - Not URL safe
EntityId = "Job Card with spaces"
EntityId = "jc?dangerous=true"
```

### 4. Set Appropriate Priority

```csharp
// ✅ Priority guidelines
Priority = "Critical"      // System errors, security issues
Priority = "High"          // Important business (approvals, rejections)
Priority = "Normal"        // Standard workflows (creation, updates)
Priority = "Low"           // Informational (reminders, completions)
```

### 5. Leave ActionUrl as Null

```csharp
// ✅ Good - Frontend computes it
ActionUrl = null

// ❌ Bad - Don't hardcode
ActionUrl = "/maintenance/job-cards/jc-123"
```

The frontend will compute the URL from EntityType and EntityId using the entity navigation router.

---

## Error Handling

### Exceptions

```csharp
public class NotificationException : ApplicationException
{
    public NotificationException(string message) : base(message) { }
}

public class InvalidNotificationStateException : NotificationException
{
    public InvalidNotificationStateException(string state) 
        : base($"Invalid notification state: {state}") { }
}

public class NotificationDeliveryException : NotificationException
{
    public NotificationDeliveryException(string channel, string reason)
        : base($"Failed to deliver via {channel}: {reason}") { }
}
```

### Usage

```csharp
try
{
    await _notificationService.CreateNotificationAsync(dto);
}
catch (NotificationException ex)
{
    _logger.LogError(ex, "Notification creation failed");
    // Handle appropriately
}
```

---

## Background Processing

### Pending Notifications

Notifications start with Status="Pending" and are processed by background job:

```csharp
// Typically runs every 5 minutes
public async Task ProcessPendingNotificationsAsync()
{
    var pendingNotifications = await _repository.GetPendingAsync();
    
    foreach (var notification in pendingNotifications)
    {
        try
        {
            await DeliverNotificationAsync(notification);
            notification.Status = "Sent";
            notification.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            notification.AttemptCount++;
            notification.LastError = ex.Message;
            
            if (notification.AttemptCount >= MaxRetries)
            {
                notification.Status = "Failed";  // Goes to dead letter queue
            }
        }
    }
    
    await _repository.SaveChangesAsync();
}
```

### Expired Notifications

Notifications with ExpiresAt in past are marked expired:

```csharp
public async Task ProcessExpiredNotificationsAsync()
{
    var expiredNotifications = await _repository.GetExpiredAsync();
    
    foreach (var notification in expiredNotifications)
    {
        notification.Status = "Expired";
    }
    
    await _repository.SaveChangesAsync();
}
```

---

## Performance Considerations

### Indexes

- **RecipientId** - For fetching user's notifications
- **Status** - For filtering by status
- **Priority** - For filtering by priority
- **EntityType** - For entity-specific queries
- **TenantId** - For multi-tenancy queries

### Pagination

Always paginate results:

```csharp
var params = new PaginationParams { PageNumber = 1, PageSize = 50 };
var result = await _notificationService.GetNotificationsAsync(params);
// result.Items - Current page
// result.TotalCount - Total records
// result.TotalPages - Total pages
```

### Cleanup

Implement periodic cleanup for old notifications:

```csharp
// Delete notifications older than 90 days
public async Task CleanupOldNotificationsAsync(int daysToKeep = 90)
{
    var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
    await _repository.DeleteOlderThanAsync(cutoffDate);
}
```

---

## Testing

### Unit Test Example

```csharp
[TestClass]
public class UnifiedNotificationServiceTests
{
    private Mock<INotificationRepository> _repositoryMock;
    private Mock<ISignalRHub> _signalRMock;
    private UnifiedNotificationService _service;
    
    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<INotificationRepository>();
        _signalRMock = new Mock<ISignalRHub>();
        _service = new UnifiedNotificationService(
            _repositoryMock.Object,
            _signalRMock.Object);
    }
    
    [TestMethod]
    public async Task CreateNotificationAsync_WithValidDto_ReturnsNotification()
    {
        // Arrange
        var dto = new CreateNotificationDto
        {
            RecipientId = "user-123",
            Type = "InApp",
            Title = "Test",
            Message = "Test message",
            Priority = "Normal",
            EntityType = "JobCard",
            EntityId = "jc-123"
        };
        
        // Act
        var result = await _service.CreateNotificationAsync(dto);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Pending", result.Status);
    }
}
```

---

## Debugging

### Common Issues

**1. Notifications not persisting**
- Check EntityType/EntityId are not null
- Verify TenantId is set correctly
- Check database migration applied

**2. SignalR not delivering**
- Verify hub connection established
- Check recipient is connected to hub
- Verify notification status is "Sent"

**3. Email not sending**
- Check SMTP settings configured
- Verify EmailAddress or RecipientId is valid
- Check email template exists

### Debugging Commands

```csharp
// Check pending notifications
var pending = await _notificationService.GetPendingNotificationsAsync(
    new PaginationParams { PageSize = 100 });

// Check unread count
var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

// Manually trigger processing
await _notificationService.ProcessPendingNotificationsAsync();

// Check notification details
var notification = await _notificationService.GetNotificationAsync(id);
```

---

## Migration Path

### From Domain-Specific to Unified

**Before:**
```csharp
await _jobCardNotificationService.SendApprovalRequiredAsync(jobCardId);
await _workOrderNotificationService.SendCompletedAsync(workOrderId);
```

**After:**
```csharp
await _notificationService.CreateNotificationAsync(new CreateNotificationDto
{
    RecipientId = managerId,
    Type = "InApp",
    Title = "Job Card Approval Required",
    Priority = "High",
    EntityType = "JobCard",
    EntityId = jobCardId
});

await _notificationService.CreateNotificationAsync(new CreateNotificationDto
{
    RecipientId = technicianId,
    Type = "InApp",
    Title = "Work Order Completed",
    Priority = "Normal",
    EntityType = "WorkOrder",
    EntityId = workOrderId
});
```

Benefits:
- Single interface for all notifications
- Consistent entity tracking
- Better analytics
- Easier to extend
- Simplified testing

---

## Summary

The backend notification system:

✅ Provides unified interface for all modules
✅ Tracks entities for smart navigation
✅ Handles multiple delivery methods
✅ Includes real-time SignalR integration
✅ Supports background processing
✅ Includes analytics and preferences
✅ Is fully tested and production-ready
✅ Scales to multiple modules

**Next Step:** Frontend consumes these notifications through REST API and SignalR, automatically handling entity navigation and UI display.
