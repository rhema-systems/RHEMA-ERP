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
