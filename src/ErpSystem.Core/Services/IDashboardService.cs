using ErpSystem.Core.DTOs.Dashboard;

namespace ErpSystem.Core.Services
{
    public interface IDashboardService
    {
        Task<DashboardDataDto> GetDashboardDataAsync(string tenantId);
        Task<DashboardDataDto> GetDashboardDataAsync(string tenantId, bool isAdmin);
        Task BroadcastDashboardUpdateAsync(string tenantId, DashboardDataDto data);
        Task BroadcastNotificationAsync(string userId, NotificationDto notification);
        Task BroadcastUserSessionUpdateAsync(string tenantId, UserSessionUpdateDto update);
    }
}