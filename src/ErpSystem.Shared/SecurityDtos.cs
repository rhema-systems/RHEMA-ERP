using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Shared;

// Security Metrics DTOs
public class SecurityMetricsDto
{
    public int TwoFactorAdoptionRate { get; set; }
    public int FailedLoginAttempts { get; set; }
    public int ActiveSessions { get; set; }
    public int SecurityIncidents { get; set; }
    public int PasswordCompliance { get; set; }
    public int AuditEventsToday { get; set; }
    public string LastUpdated { get; set; } = string.Empty;

    public SecurityTrendsDto Trends { get; set; } = new();
}

public class SecurityTrendsDto
{
    public int TwoFactorAdoptionRate { get; set; }
    public int FailedLoginAttempts { get; set; }
    public int ActiveSessions { get; set; }
    public int SecurityIncidents { get; set; }
    public int PasswordCompliance { get; set; }
    public int AuditEventsToday { get; set; }
}

// Security Alert DTOs
public class SecurityAlertDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Dismissed { get; set; }
    public int Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? AffectedUser { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class CreateSecurityAlertRequest
{
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Range(1, 10)]
    public int Severity { get; set; } = 1;

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Source { get; set; } = string.Empty;

    public string? AffectedUser { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

// Audit Log DTOs
public class AuditLogEntryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Details { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

public class AuditLogFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public string? Result { get; set; }
    public string? Risk { get; set; }
    public string? IpAddress { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "timestamp";
    public string SortOrder { get; set; } = "desc";
}

public class AuditLogResponseDto
{
    public List<AuditLogEntryDto> Entries { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrevious { get; set; }
}

// Device Session DTOs (extending UserSession data)
public class DeviceSessionDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    public DeviceInfoDto DeviceInfo { get; set; } = new();
    public LocationInfoDto Location { get; set; } = new();
    public SessionInfoDto Session { get; set; } = new();
    public SecurityInfoDto Security { get; set; } = new();
}

public class DeviceInfoDto
{
    public string Type { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class LocationInfoDto
{
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public CoordinatesDto? Coordinates { get; set; }
}

public class CoordinatesDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class SessionInfoDto
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastActivity { get; set; }
    public bool IsActive { get; set; }
    public int Duration { get; set; } // in minutes
}

public class SecurityInfoDto
{
    public bool IsTrusted { get; set; }
    public int RiskScore { get; set; }
    public List<string> Flags { get; set; } = new();
    public bool TwoFactorEnabled { get; set; }
}

// Security Health Score DTOs
public class SecurityHealthScoreDto
{
    public int Overall { get; set; }
    public SecurityHealthCategoriesDto Categories { get; set; } = new();
    public List<SecurityRecommendationDto> Recommendations { get; set; } = new();
    public DateTime LastCalculated { get; set; }
}

public class SecurityHealthCategoriesDto
{
    public int PasswordPolicies { get; set; }
    public int TwoFactorAdoption { get; set; }
    public int SessionSecurity { get; set; }
    public int AccessControls { get; set; }
    public int AuditCompliance { get; set; }
}

public class SecurityRecommendationDto
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty; // 'low', 'medium', 'high', 'critical'
    public bool ActionRequired { get; set; }
}

// Threat Detection DTOs
public class ThreatDetectionDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // 'brute_force', 'anomalous_login', 'privilege_escalation', etc.
    public string Severity { get; set; } = string.Empty; // 'low', 'medium', 'high', 'critical'
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Target { get; set; }
    public List<ThreatIndicatorDto> Indicators { get; set; } = new();
    public string Status { get; set; } = string.Empty; // 'active', 'investigating', 'resolved', 'false_positive'
    public string? AssignedTo { get; set; }
    public string? Resolution { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ThreatIndicatorDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Risk { get; set; }
}

public class UpdateThreatStatusRequest
{
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Resolution { get; set; }
}

// Two-Factor Authentication DTOs
public class TwoFactorSettingsDto
{
    public bool IsEnabled { get; set; }
    public List<string> RecoveryCodes { get; set; } = new();
    public string? AuthenticatorKey { get; set; }
    public DateTime? EnabledAt { get; set; }
    public string? BackupCodesGeneratedAt { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}

public class TwoFactorSetupDto
{
    public string AuthenticatorKey { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public List<string> RecoveryCodes { get; set; } = new();
    public string Instructions { get; set; } = string.Empty;
}

public class EnableTwoFactorRequest
{
    // VerificationCode is optional for initial setup, required for enabling
    [StringLength(6, MinimumLength = 0)]
    public string? VerificationCode { get; set; }

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

public class DisableTwoFactorRequest
{
    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Reason { get; set; }
}

// Security Policy DTOs
public class SecurityPolicyDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PolicyType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

public class CreateSecurityPolicyRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PolicyType { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public Dictionary<string, object>? Configuration { get; set; }
}

public class UpdateSecurityPolicyRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public Dictionary<string, object>? Configuration { get; set; }
}

// Consolidated, tenant-scoped Security Operations DTOs.
public sealed class SecurityOperationsOverviewDto
{
    public SecurityTimeRangeDto Range { get; set; } = new();
    public SecuritySnapshotDto Snapshot { get; set; } = new();
    public SecurityActivitySummaryDto Activity { get; set; } = new();
    public SecurityOperationsHealthDto Health { get; set; } = new();
    public SecurityAlertSummaryDto Alerts { get; set; } = new();
    public List<SecurityTimelineEventDto> RecentEvents { get; set; } = new();
    public List<PrivilegedAccessUserDto> PrivilegedUsers { get; set; } = new();
    public List<AuthenticationTrendPointDto> AuthenticationTrend { get; set; } = new();
    public SecurityConfigurationSummaryDto Configuration { get; set; } = new();
    public SecurityRetentionSummaryDto Retention { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}

public sealed class SecurityTimeRangeDto
{
    public string Key { get; set; } = "24h";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}

public sealed class SecuritySnapshotDto
{
    public int EligibleUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int MfaEnabledUsers { get; set; }
    public decimal MfaAdoptionPercent { get; set; }
    public int PrivilegedUsers { get; set; }
    public int PrivilegedUsersWithoutMfa { get; set; }
    public int LockedAccounts { get; set; }
    public int ActiveSessions { get; set; }
    public int StaleSessions { get; set; }
    public int DisabledUsersWithActiveSessions { get; set; }
    public int SessionIdleThresholdMinutes { get; set; }
}

public sealed class SecurityActivitySummaryDto
{
    public int SuccessfulLogins { get; set; }
    public int FailedLogins { get; set; }
    public decimal LoginFailureRatePercent { get; set; }
    public int PasswordChanges { get; set; }
    public int SecurityEvents { get; set; }
    public int AuditEvents { get; set; }
}

public sealed class SecurityOperationsHealthDto
{
    public int Score { get; set; }
    public int MaximumScore { get; set; } = 100;
    public string ModelVersion { get; set; } = "2026.10";
    public List<SecurityHealthFactorDto> Factors { get; set; } = new();
}

public sealed class SecurityHealthFactorDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EarnedPoints { get; set; }
    public int MaximumPoints { get; set; }
    public string Status { get; set; } = "unavailable";
    public string Evidence { get; set; } = string.Empty;
}

public sealed class SecurityHealthEvidenceDto
{
    public int EligibleUsers { get; set; }
    public int MfaEnabledUsers { get; set; }
    public int PrivilegedUsers { get; set; }
    public int PrivilegedUsersWithMfa { get; set; }
    public int ActiveSessions { get; set; }
    public int StaleSessions { get; set; }
    public int DisabledUsersWithActiveSessions { get; set; }
    public bool HasAuditEvents { get; set; }
    public bool HasSecurityEvents { get; set; }
    public int SuccessfulLogins { get; set; }
    public int FailedLogins { get; set; }
    public int LockedAccounts { get; set; }
    public int UnresolvedHighRiskSignals { get; set; }
    public bool HasPersistedSecuritySettings { get; set; }
    public bool PasswordPolicyConfigured { get; set; }
    public bool LockoutConfigured { get; set; }
    public bool LoginRateLimitConfigured { get; set; }
    public bool SessionTimeoutConfigured { get; set; }
    public bool RetentionConfigured { get; set; }
}

public sealed class SecurityAlertSummaryDto
{
    public int OpenTotal { get; set; }
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public List<SecurityAlertItemDto> Items { get; set; } = new();
}

public sealed class SecurityAlertItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class SecurityTimelineEventDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public bool? Success { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public sealed class PrivilegedAccessUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string PrivilegeLevel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MfaEnabled { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<string> Findings { get; set; } = new();
}

public sealed class AuthenticationTrendPointDto
{
    public DateTime PeriodStartUtc { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Successful { get; set; }
    public int Failed { get; set; }
}

public sealed class SecurityConfigurationSummaryDto
{
    public bool Persisted { get; set; }
    public int? PasswordMinimumLength { get; set; }
    public int? FailedAttemptThreshold { get; set; }
    public int? LockoutMinutes { get; set; }
    public int? SessionTimeoutMinutes { get; set; }
    public int? AccessTokenLifetimeMinutes { get; set; }
    public string ConcurrentLoginPolicy { get; set; } = "Unavailable";
    public bool LoginRateLimitingConfigured { get; set; }
    public bool CaptchaEnabled { get; set; }
    public bool CaptchaConfigured { get; set; }
}

public sealed class SecurityRetentionSummaryDto
{
    public bool Configured { get; set; }
    public bool Enabled { get; set; }
    public int? AuditLogRetentionDays { get; set; }
    public int? SecurityLogRetentionDays { get; set; }
    public DateTime? LastRunStartedAtUtc { get; set; }
    public DateTime? LastRunCompletedAtUtc { get; set; }
    public bool? LastRunSucceeded { get; set; }
}

public sealed class SecurityEventQueryDto
{
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public string? Action { get; set; }
    public string? User { get; set; }
    public string? IpAddress { get; set; }
    public bool? Success { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class SecurityEventPageDto
{
    public List<SecurityTimelineEventDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

public static class SecurityHealthScoring
{
    public static SecurityOperationsHealthDto Calculate(SecurityHealthEvidenceDto evidence)
    {
        var factors = new List<SecurityHealthFactorDto>
        {
            PercentageFactor(
                "mfa-coverage",
                "MFA coverage",
                20,
                evidence.EligibleUsers == 0 ? 1m : (decimal)evidence.MfaEnabledUsers / evidence.EligibleUsers,
                $"{evidence.MfaEnabledUsers} of {evidence.EligibleUsers} eligible users have MFA enabled."),
            PercentageFactor(
                "privileged-mfa",
                "Privileged-account protection",
                20,
                evidence.PrivilegedUsers == 0 ? 1m : (decimal)evidence.PrivilegedUsersWithMfa / evidence.PrivilegedUsers,
                $"{evidence.PrivilegedUsersWithMfa} of {evidence.PrivilegedUsers} privileged users have MFA enabled."),
            SessionFactor(evidence),
            TelemetryFactor(evidence),
            AuthenticationFactor(evidence),
            ConfigurationFactor(evidence)
        };

        return new SecurityOperationsHealthDto
        {
            Score = factors.Sum(factor => factor.EarnedPoints),
            MaximumScore = factors.Sum(factor => factor.MaximumPoints),
            Factors = factors
        };
    }

    private static SecurityHealthFactorDto PercentageFactor(
        string key,
        string name,
        int maximum,
        decimal ratio,
        string evidence)
    {
        var earned = (int)Math.Round(maximum * Math.Clamp(ratio, 0m, 1m), MidpointRounding.AwayFromZero);
        return Factor(key, name, earned, maximum, evidence);
    }

    private static SecurityHealthFactorDto SessionFactor(SecurityHealthEvidenceDto evidence)
    {
        if (evidence.ActiveSessions == 0)
        {
            return Factor("session-hygiene", "Session hygiene", 15, 15, "No active sessions are currently in scope.");
        }

        var affected = Math.Min(evidence.ActiveSessions, evidence.StaleSessions + evidence.DisabledUsersWithActiveSessions);
        var earned = 15 - (int)Math.Round(15m * affected / evidence.ActiveSessions, MidpointRounding.AwayFromZero);
        return Factor(
            "session-hygiene",
            "Session hygiene",
            earned,
            15,
            $"{evidence.StaleSessions} stale sessions and {evidence.DisabledUsersWithActiveSessions} disabled-user sessions out of {evidence.ActiveSessions} active sessions.");
    }

    private static SecurityHealthFactorDto TelemetryFactor(SecurityHealthEvidenceDto evidence)
    {
        var earned = evidence.HasAuditEvents && evidence.HasSecurityEvents
            ? 15
            : evidence.HasAuditEvents || evidence.HasSecurityEvents ? 8 : 0;
        return Factor(
            "audit-telemetry",
            "Audit telemetry",
            earned,
            15,
            $"Audit events present: {evidence.HasAuditEvents}; security events present: {evidence.HasSecurityEvents}.");
    }

    private static SecurityHealthFactorDto AuthenticationFactor(SecurityHealthEvidenceDto evidence)
    {
        var attempts = evidence.SuccessfulLogins + evidence.FailedLogins;
        var failureRate = attempts == 0 ? 0m : (decimal)evidence.FailedLogins / attempts * 100m;
        var failureDeduction = failureRate switch
        {
            <= 5m => 0,
            <= 15m => 3,
            <= 30m => 6,
            _ => 9
        };
        var lockoutDeduction = evidence.LockedAccounts > 0 ? 3 : 0;
        var signalDeduction = evidence.UnresolvedHighRiskSignals > 0 ? 3 : 0;
        var earned = Math.Max(0, 15 - failureDeduction - lockoutDeduction - signalDeduction);
        return Factor(
            "authentication-anomalies",
            "Authentication anomalies",
            earned,
            15,
            $"Failure rate {failureRate:0.0}%, {evidence.LockedAccounts} locked accounts, {evidence.UnresolvedHighRiskSignals} unresolved high-risk signals.");
    }

    private static SecurityHealthFactorDto ConfigurationFactor(SecurityHealthEvidenceDto evidence)
    {
        var checks = new[]
        {
            evidence.PasswordPolicyConfigured,
            evidence.LockoutConfigured,
            evidence.LoginRateLimitConfigured,
            evidence.SessionTimeoutConfigured,
            evidence.RetentionConfigured
        };
        var earned = checks.Count(value => value) * 3;
        var prefix = evidence.HasPersistedSecuritySettings ? string.Empty : "No persisted security settings row. ";
        return Factor(
            "security-configuration",
            "Security configuration",
            earned,
            15,
            $"{prefix}{checks.Count(value => value)} of {checks.Length} evidence-backed configuration checks pass.");
    }

    private static SecurityHealthFactorDto Factor(
        string key,
        string name,
        int earned,
        int maximum,
        string evidence)
    {
        var status = earned == maximum ? "healthy" : earned >= maximum * 0.6m ? "attention" : "action-required";
        return new SecurityHealthFactorDto
        {
            Key = key,
            Name = name,
            EarnedPoints = Math.Clamp(earned, 0, maximum),
            MaximumPoints = maximum,
            Status = status,
            Evidence = evidence
        };
    }
}
