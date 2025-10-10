using Microsoft.AspNetCore.SignalR;
using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.Interfaces;
using ErpSystem.Api.Hubs;

namespace ErpSystem.Api.Services
{
    public class HubNotificationService : IHubNotificationService
    {
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly ILogger<HubNotificationService> _logger;

        public HubNotificationService(
            IHubContext<DashboardHub> hubContext,
            ILogger<HubNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task BroadcastDashboardUpdateAsync(string tenantId, DashboardDataDto data)
        {
            try
            {
                await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("DashboardUpdate", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting dashboard update to tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task BroadcastNotificationAsync(string userId, NotificationDto notification)
        {
            try
            {
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("NewNotification", notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification to user {UserId}", userId);
                throw;
            }
        }

        public async Task BroadcastUserSessionUpdateAsync(string tenantId, UserSessionUpdateDto update)
        {
            try
            {
                await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("UserSessionUpdate", update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting user session update to tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task SendNotificationToAllAsync(string message, string? type = null)
        {
            try
            {
                var notification = new
                {
                    Message = message,
                    Type = type ?? "Info",
                    Timestamp = DateTime.UtcNow
                };
                await _hubContext.Clients.All.SendAsync("GlobalNotification", notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to all users: {Message}", message);
                throw;
            }
        }
    }
}
