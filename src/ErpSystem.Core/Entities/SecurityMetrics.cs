using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

public class SecurityMetrics : TenantEntity
{
    [Required]
    public DateTime MetricDate { get; set; } = DateTime.UtcNow.Date;

    // Core Security Metrics
    [Range(0, 100)]
    public int TwoFactorAdoptionRate { get; set; } = 0;

    public int FailedLoginAttempts { get; set; } = 0;

    public int ActiveSessions { get; set; } = 0;

    public int SecurityIncidents { get; set; } = 0;

    [Range(0, 100)]
    public int PasswordCompliance { get; set; } = 0;

    public int AuditEventsToday { get; set; } = 0;

    // Additional Metrics
    public int TotalUsers { get; set; } = 0;
    public int UsersWithTwoFactorEnabled { get; set; } = 0;
    public int LockedAccounts { get; set; } = 0;
    public int PasswordExpiringSoon { get; set; } = 0;
    public int SuspiciousActivityCount { get; set; } = 0;

    // Trend data (percentage change from previous period)
    public int TwoFactorAdoptionTrend { get; set; } = 0;
    public int FailedLoginAttemptsTrend { get; set; } = 0;
    public int ActiveSessionsTrend { get; set; } = 0;
    public int SecurityIncidentsTrend { get; set; } = 0;
    public int PasswordComplianceTrend { get; set; } = 0;
    public int AuditEventsTrend { get; set; } = 0;

    // Ensure one metric per tenant per day
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
