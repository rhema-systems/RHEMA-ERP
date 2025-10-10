using ErpSystem.Core.DTOs.Dashboard;

namespace ErpSystem.Core.Interfaces
{
    public interface IHubNotificationService
    {
        Task BroadcastDashboardUpdateAsync(string tenantId, DashboardDataDto data);
        Task BroadcastNotificationAsync(string userId, NotificationDto notification);
        Task BroadcastUserSessionUpdateAsync(string tenantId, UserSessionUpdateDto update);
        Task SendNotificationToAllAsync(string message, string? type = null);
    }
}