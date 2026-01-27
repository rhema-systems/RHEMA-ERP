using ErpSystem.Core.DTOs.Dashboard;

namespace ErpSystem.Core.Interfaces
{
    public interface IHubNotificationService
    {
        Task BroadcastDashboardUpdateAsync(string tenantId, DashboardDataDto data);
        Task BroadcastNotificationAsync(string userId, NotificationDto notification);
        Task BroadcastUserSessionUpdateAsync(string tenantId, UserSessionUpdateDto update);
        Task SendNotificationToAllAsync(string message, string? type = null);

        // Enhanced maintenance notification support
        Task BroadcastMaintenanceNotificationAsync(string userId, string title, string message, string priority, string? actionUrl = null);
        Task BroadcastMaintenanceAlertToTenantAsync(string tenantId, string alertType, string message, Dictionary<string, object>? data = null);
        Task BroadcastCriticalMaintenanceAlertAsync(string message, string? assetId = null, string? workOrderId = null);
    }
}
