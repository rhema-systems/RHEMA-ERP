using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ErpSystem.Data.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubNotificationService _hubNotificationService;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            ApplicationDbContext context,
            IHubNotificationService hubNotificationService,
            ILogger<DashboardService> logger)
        {
            _context = context;
            _hubNotificationService = hubNotificationService;
            _logger = logger;
        }

        public async Task<DashboardDataDto> GetDashboardDataAsync(string tenantId)
        {
            // Default to non-admin for backward compatibility
            return await GetDashboardDataAsync(tenantId, isAdmin: false);
        }

        public async Task<DashboardDataDto> GetDashboardDataAsync(string tenantId, bool isAdmin)
        {
            try
            {
                var metrics = await GetDashboardMetricsAsync(tenantId, isAdmin);
                var recentActivities = await GetRecentActivitiesAsync(tenantId, isAdmin);
                var notifications = await GetNotificationsAsync(tenantId);
                var onlineUsers = await GetOnlineUsersAsync(tenantId, isAdmin);
                var systemStatus = await GetSystemStatusAsync();

                return new DashboardDataDto
                {
                    Metrics = metrics,
                    RecentActivities = recentActivities,
                    Notifications = notifications,
                    OnlineUsers = onlineUsers,
                    SystemStatus = systemStatus,
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard data for tenant {TenantId}, isAdmin: {IsAdmin}", tenantId, isAdmin);
                throw;
            }
        }

        public async Task BroadcastDashboardUpdateAsync(string tenantId, DashboardDataDto data)
        {
            try
            {
                await _hubNotificationService.BroadcastDashboardUpdateAsync(tenantId, data);
                _logger.LogInformation("Dashboard update broadcasted to tenant {TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting dashboard update to tenant {TenantId}", tenantId);
            }
        }

        public async Task BroadcastNotificationAsync(string userId, NotificationDto notification)
        {
            try
            {
                await _hubNotificationService.BroadcastNotificationAsync(userId, notification);
                _logger.LogInformation("Notification broadcasted to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification to user {UserId}", userId);
            }
        }

        public async Task BroadcastUserSessionUpdateAsync(string tenantId, UserSessionUpdateDto update)
        {
            try
            {
                await _hubNotificationService.BroadcastUserSessionUpdateAsync(tenantId, update);
                _logger.LogInformation("User session update broadcasted to tenant {TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting user session update to tenant {TenantId}", tenantId);
            }
        }

        private async Task<DashboardMetricsDto> GetDashboardMetricsAsync(string tenantId, bool isSuperAdmin = false)
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
            {
                throw new ArgumentException("Invalid tenant ID format", nameof(tenantId));
            }

            List<ApplicationUser> users;
            List<UserSession> activeSessions;
            int totalSessions;
            List<UserSession> recentLoginSessions;
            List<UserSession> recentActivitySessions;

            if (isSuperAdmin)
            {
                _logger.LogInformation("Loading dashboard metrics for SuperAdmin - showing data across ALL tenants");

                // SuperAdmin sees ALL data across ALL tenants
                users = await _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .ToListAsync();

                activeSessions = await _context.UserSessions
                    .Where(s => s.IsActive)
                    .ToListAsync();

                totalSessions = await _context.UserSessions.CountAsync();

                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                recentLoginSessions = await _context.UserSessions
                    .Where(s => s.LoginTime >= sevenDaysAgo)
                    .ToListAsync();

                var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);
                recentActivitySessions = await _context.UserSessions
                    .Where(s => s.LoginTime >= twentyFourHoursAgo)
                    .ToListAsync();
            }
            else
            {
                _logger.LogInformation("Loading dashboard metrics for tenant {TenantId} - showing tenant-specific data", tenantId);

                // TenantAdmin/Regular users see only their tenant data
                users = await _context.Users
                    .Where(u => u.TenantId == tenantGuid)
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .ToListAsync();

                activeSessions = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid && s.IsActive)
                    .ToListAsync();

                totalSessions = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid)
                    .CountAsync();

                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                recentLoginSessions = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid && s.LoginTime >= sevenDaysAgo)
                    .ToListAsync();

                var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);
                recentActivitySessions = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid && s.LoginTime >= twentyFourHoursAgo)
                    .ToListAsync();
            }

            var onlineUsers = activeSessions.Select(s => s.UserId).Distinct().Count();

            var loginTrend = recentLoginSessions
                .GroupBy(s => s.LoginTime.Date)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key.ToString("MMM dd"),
                    Value = g.Count(),
                    Timestamp = g.Key
                })
                .OrderBy(p => p.Timestamp)
                .ToList();

            var sessionActivity = recentActivitySessions
                .GroupBy(s => s.LoginTime.Hour)
                .Select(g => new ChartDataPoint
                {
                    Label = $"{g.Key:00}:00",
                    Value = g.Count(),
                    Timestamp = DateTime.Today.AddHours(g.Key)
                })
                .OrderBy(p => p.Timestamp)
                .ToList();

            return new DashboardMetricsDto
            {
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.IsActive),
                OnlineUsers = onlineUsers,
                TotalSessions = totalSessions,
                ActiveSessions = activeSessions.Count,
                // No application uptime monitor is configured for this legacy endpoint.
                SystemUptime = null,
                UsersByRole = GetUsersByRoleDistribution(users),
                UserLoginTrend = loginTrend,
                SessionActivity = sessionActivity
            };
        }

        private async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(string tenantId, bool isSuperAdmin = false)
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
            {
                throw new ArgumentException("Invalid tenant ID format", nameof(tenantId));
            }

            var activities = new List<RecentActivityDto>();

            if (isSuperAdmin)
            {
                _logger.LogInformation("Loading recent activities for SuperAdmin - showing data across ALL tenants");

                // SuperAdmin sees activities from ALL tenants
                var recentLogins = await _context.UserSessions
                    .OrderByDescending(s => s.LoginTime)
                    .Take(10)
                    .Include(s => s.User)
                    .Include(s => s.Tenant)
                    .Select(s => new RecentActivityDto
                    {
                        Id = s.SessionId,
                        Type = "login",
                        Description = $"{s.User.UserName} logged in ({s.Tenant.Name})",
                        UserId = s.UserId.ToString(),
                        UserName = s.User.UserName ?? "Unknown",
                        Timestamp = s.LoginTime
                    })
                    .ToListAsync();

                activities.AddRange(recentLogins);

                var recentLogouts = await _context.UserSessions
                    .Where(s => s.LogoutTime.HasValue)
                    .OrderByDescending(s => s.LogoutTime)
                    .Take(10)
                    .Include(s => s.User)
                    .Include(s => s.Tenant)
                    .Select(s => new RecentActivityDto
                    {
                        Id = s.SessionId,
                        Type = "logout",
                        Description = $"{s.User.UserName} logged out ({s.Tenant.Name})",
                        UserId = s.UserId.ToString(),
                        UserName = s.User.UserName ?? "Unknown",
                        Timestamp = s.LogoutTime!.Value
                    })
                    .ToListAsync();

                activities.AddRange(recentLogouts);
            }
            else
            {
                _logger.LogInformation("Loading recent activities for tenant {TenantId} - showing tenant-specific data", tenantId);

                // TenantAdmin/Regular users see only their tenant activities
                var recentLogins = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid)
                    .OrderByDescending(s => s.LoginTime)
                    .Take(10)
                    .Include(s => s.User)
                    .Select(s => new RecentActivityDto
                    {
                        Id = s.SessionId,
                        Type = "login",
                        Description = $"{s.User.UserName} logged in",
                        UserId = s.UserId.ToString(),
                        UserName = s.User.UserName ?? "Unknown",
                        Timestamp = s.LoginTime
                    })
                    .ToListAsync();

                activities.AddRange(recentLogins);

                var recentLogouts = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid && s.LogoutTime.HasValue)
                    .OrderByDescending(s => s.LogoutTime)
                    .Take(10)
                    .Include(s => s.User)
                    .Select(s => new RecentActivityDto
                    {
                        Id = s.SessionId,
                        Type = "logout",
                        Description = $"{s.User.UserName} logged out",
                        UserId = s.UserId.ToString(),
                        UserName = s.User.UserName ?? "Unknown",
                        Timestamp = s.LogoutTime!.Value
                    })
                    .ToListAsync();

                activities.AddRange(recentLogouts);
            }

            return activities.OrderByDescending(a => a.Timestamp).Take(20).ToList();
        }

        private static Task<List<NotificationDto>> GetNotificationsAsync(string tenantId)
        {
            // User-targeted notifications are served by NotificationsController. This legacy
            // tenant-only contract has no user identity, so returning an empty state is safer
            // than exposing another user's records or inventing a sample notification.
            return Task.FromResult(new List<NotificationDto>());
        }

        private async Task<List<OnlineUserDto>> GetOnlineUsersAsync(string tenantId, bool isSuperAdmin = false)
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid))
            {
                throw new ArgumentException("Invalid tenant ID format", nameof(tenantId));
            }

            List<UserSession> activeSessions;

            if (isSuperAdmin)
            {
                _logger.LogInformation("Getting online users for SuperAdmin - showing ALL users across ALL tenants");

                // SuperAdmin sees ALL online users across ALL tenants
                activeSessions = await _context.UserSessions
                    .Where(s => s.IsActive)
                    .Include(s => s.User)
                        .ThenInclude(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                    .Include(s => s.Tenant)
                    .GroupBy(s => s.UserId)
                    .Select(g => g.OrderByDescending(s => s.LastActivityTime).First())
                    .ToListAsync();
            }
            else
            {
                _logger.LogInformation("Getting online users for tenant {TenantId} - showing tenant-specific users only", tenantId);

                // TenantAdmin sees only online users from their own tenant
                activeSessions = await _context.UserSessions
                    .Where(s => s.TenantId == tenantGuid && s.IsActive)
                    .Include(s => s.User)
                        .ThenInclude(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                    .Include(s => s.Tenant)
                    .GroupBy(s => s.UserId)
                    .Select(g => g.OrderByDescending(s => s.LastActivityTime).First())
                    .ToListAsync();
            }

            // Convert to DTOs with proper role and tenant information
            var onlineUsers = activeSessions.Select(session => new OnlineUserDto
            {
                UserId = session.UserId.ToString(),
                UserName = session.User.UserName ?? "Unknown",
                Email = session.User.Email ?? "",
                Role = GetUserPrimaryRole(session.User),
                LastActivity = session.LastActivityTime,
                Status = "Online",
                Location = isSuperAdmin
                    ? $"{session.Tenant?.Name ?? "Unknown Tenant"} - {session.Location}"  // Include tenant name for SuperAdmin
                    : session.Location  // Just location for TenantAdmin
            }).ToList();

            _logger.LogInformation("Found {OnlineUserCount} online users (SuperAdmin: {IsSuperAdmin})", onlineUsers.Count, isSuperAdmin);
            return onlineUsers;
        }

        private async Task<SystemStatusDto> GetSystemStatusAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var databaseHealthy = false;
            string? databaseError = null;
            try
            {
                databaseHealthy = await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                databaseError = "Database readiness check failed.";
                _logger.LogWarning(ex, "Legacy dashboard database readiness check failed");
            }
            stopwatch.Stop();

            return new SystemStatusDto
            {
                IsHealthy = databaseHealthy,
                Status = databaseHealthy
                    ? "Database available; host telemetry unavailable"
                    : "Database unavailable; host telemetry unavailable",
                CpuUsage = null,
                MemoryUsage = null,
                DiskUsage = null,
                DatabaseConnections = null,
                Services =
                [
                    new ServiceStatusDto
                    {
                        Name = "Database",
                        IsHealthy = databaseHealthy,
                        Status = databaseHealthy ? "Connected" : "Unavailable",
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        ErrorMessage = databaseError
                    }
                ],
                LastCheck = DateTime.UtcNow
            };
        }

        private static Dictionary<string, int> GetUsersByRoleDistribution(List<ApplicationUser> users)
        {
            var roleDistribution = new Dictionary<string, int>();

            foreach (var user in users)
            {
                if (user.UserRoles?.Any() == true)
                {
                    // User has roles - count each role
                    foreach (var userRole in user.UserRoles)
                    {
                        var roleName = userRole.Role?.Name ?? "Unknown";
                        roleDistribution[roleName] = roleDistribution.GetValueOrDefault(roleName, 0) + 1;
                    }
                }
                else
                {
                    // User has no roles - count as "User"
                    roleDistribution["User"] = roleDistribution.GetValueOrDefault("User", 0) + 1;
                }
            }

            // Ensure at least one entry exists
            if (!roleDistribution.Any())
            {
                roleDistribution["User"] = users.Count;
            }

            return roleDistribution;
        }

        private static string GetUserPrimaryRole(ApplicationUser user)
        {
            if (user.UserRoles?.Any() != true)
            {
                return "User";
            }

            // Get the first role (you could implement priority logic here)
            var primaryRole = user.UserRoles.FirstOrDefault()?.Role?.Name;
            return primaryRole ?? "User";
        }
    }
}
