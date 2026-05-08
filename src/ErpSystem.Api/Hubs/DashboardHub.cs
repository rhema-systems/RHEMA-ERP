using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ErpSystem.Api.Hubs
{
    [Authorize]
    public class DashboardHub : Hub
    {
        private readonly ILogger<DashboardHub> _logger;

        public DashboardHub(ILogger<DashboardHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
                var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value;

                _logger.LogDebug("SignalR connection attempt - ConnectionId: {ConnectionId}, UserId: {UserId}, TenantId: {TenantId}, UserName: {UserName}",
                    Context.ConnectionId, userId, tenantId, userName);

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(tenantId))
                {
                    // Add user to tenant-specific group
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");

                    // Add user to user-specific group for personal notifications
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

                    _logger.LogDebug("User {UserId} ({UserName}) from tenant {TenantId} successfully connected to dashboard hub with ConnectionId {ConnectionId}",
                        userId, userName, tenantId, Context.ConnectionId);
                }
                else
                {
                    _logger.LogWarning("SignalR connection missing required claims - UserId: {UserId}, TenantId: {TenantId}, ConnectionId: {ConnectionId}",
                        userId, tenantId, Context.ConnectionId);
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync for ConnectionId {ConnectionId}", Context.ConnectionId);
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
                var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value;

                if (exception != null)
                {
                    _logger.LogWarning(exception, "SignalR disconnection with exception - ConnectionId: {ConnectionId}, UserId: {UserId}, Exception: {ExceptionMessage}",
                        Context.ConnectionId, userId, exception.Message);
                }
                else
                {
                    _logger.LogDebug("SignalR clean disconnection - ConnectionId: {ConnectionId}, UserId: {UserId} ({UserName})",
                        Context.ConnectionId, userId, userName);
                }

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(tenantId))
                {
                    // Remove user from groups
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");

                    _logger.LogDebug("User {UserId} ({UserName}) from tenant {TenantId} removed from SignalR groups", userId, userName, tenantId);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync for ConnectionId {ConnectionId}", Context.ConnectionId);
                // Don't rethrow here as it's in disconnect handling
            }
        }

        // Client can join specific groups for more targeted updates
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("Connection {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("Connection {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
        }
    }
}
