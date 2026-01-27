namespace ErpSystem.Core.Enums;

/// <summary>
/// Options for preventing concurrent logins
/// </summary>
public enum PreventConcurrentLogin
{
    /// <summary>
    /// Allow users to log in from multiple devices/sessions simultaneously
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// New login will logout all other active sessions
    /// If you have logged in anywhere, this new login will log out all previous sessions
    /// </summary>
    LogoutFromAllDevices = 1,

    /// <summary>
    /// Prevent subsequent logins if user already has an active session
    /// If you have already logged in somewhere, you can't have subsequent login anywhere until you logout
    /// </summary>
    PreventSubsequentLogins = 2
}

/// <summary>
/// Severity levels for security threats
/// </summary>
public enum ThreatSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Status of threat detection
/// </summary>
public enum ThreatStatus
{
    New = 0,
    InProgress = 1,
    Resolved = 2,
    Dismissed = 3,
    Blocked = 4
}

/// <summary>
/// Types of security policies
/// </summary>
public enum SecurityPolicyType
{
    PasswordPolicy = 1,
    AccessPolicy = 2,
    SessionPolicy = 3,
    DataProtectionPolicy = 4,
    AuditPolicy = 5,
    CompliancePolicy = 6
}

/// <summary>
/// Severity levels for security policy violations
/// </summary>
public enum SecurityPolicyViolationSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
