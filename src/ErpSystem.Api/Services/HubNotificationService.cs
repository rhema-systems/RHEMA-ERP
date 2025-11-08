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

        public async Task BroadcastMaintenanceNotificationAsync(string userId, string title, string message, string priority, string? actionUrl = null)
        {
            try
            {
                var notification = new
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Message = message,
                    Priority = priority,
                    ActionUrl = actionUrl,
                    Type = "Maintenance",
                    Timestamp = DateTime.UtcNow,
                    IsRead = false
                };
                
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("MaintenanceNotification", notification);
                _logger.LogInformation("Broadcasted maintenance notification to user {UserId}: {Title}", userId, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting maintenance notification to user {UserId}", userId);
                throw;
            }
        }

        public async Task BroadcastMaintenanceAlertToTenantAsync(string tenantId, string alertType, string message, Dictionary<string, object>? data = null)
        {
            try
            {
                var alert = new
                {
                    Id = Guid.NewGuid().ToString(),
                    AlertType = alertType,
                    Message = message,
                    Data = data ?? new Dictionary<string, object>(),
                    Timestamp = DateTime.UtcNow
                };
                
                await _hubContext.Clients.Group($"Tenant_{tenantId}").SendAsync("MaintenanceAlert", alert);
                _logger.LogInformation("Broadcasted maintenance alert to tenant {TenantId}: {AlertType}", tenantId, alertType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting maintenance alert to tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task BroadcastCriticalMaintenanceAlertAsync(string message, string? assetId = null, string? workOrderId = null)
        {
            try
            {
                var alert = new
                {
                    Id = Guid.NewGuid().ToString(),
                    Message = message,
                    Priority = "Critical",
                    AssetId = assetId,
                    WorkOrderId = workOrderId,
                    Type = "CriticalMaintenanceAlert",
                    Timestamp = DateTime.UtcNow
                };
                
                await _hubContext.Clients.All.SendAsync("CriticalMaintenanceAlert", alert);
                _logger.LogWarning("Broadcasted critical maintenance alert to all users: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting critical maintenance alert: {Message}", message);
                throw;
            }
        }
    }
}
