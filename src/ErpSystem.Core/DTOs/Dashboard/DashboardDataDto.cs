namespace ErpSystem.Core.DTOs.Dashboard
{
    public class DashboardDataDto
    {
        public DashboardMetricsDto Metrics { get; set; } = new();
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<NotificationDto> Notifications { get; set; } = new();
        public List<OnlineUserDto> OnlineUsers { get; set; } = new();
        public SystemStatusDto SystemStatus { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class DashboardMetricsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int OnlineUsers { get; set; }
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public decimal SystemUptime { get; set; }
        public Dictionary<string, int> UsersByRole { get; set; } = new();
        public List<ChartDataPoint> UserLoginTrend { get; set; } = new();
        public List<ChartDataPoint> SessionActivity { get; set; } = new();
    }

    public class RecentActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? AdditionalData { get; set; }
    }

    public class NotificationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "info"; // info, warning, error, success
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        // Optional linking to a domain entity (enables real-time UI refresh on detail screens)
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
    }

    public class OnlineUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime LastActivity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Location { get; set; }
    }

    public class SystemStatusDto
    {
        public bool IsHealthy { get; set; } = true;
        public string Status { get; set; } = "Operational";
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public int DatabaseConnections { get; set; }
        public List<ServiceStatusDto> Services { get; set; } = new();
        public DateTime LastCheck { get; set; } = DateTime.UtcNow;
    }

    public class ServiceStatusDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public long ResponseTime { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class UserSessionUpdateDto
    {
        public string Type { get; set; } = string.Empty; // "login", "logout", "activity"
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}
