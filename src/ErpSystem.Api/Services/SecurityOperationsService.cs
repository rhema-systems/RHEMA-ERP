using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public interface ISecurityOperationsService
{
    Task<SecurityOperationsOverviewDto> GetOverviewAsync(
        string? range,
        DateTime? startUtc,
        DateTime? endUtc,
        CancellationToken cancellationToken);

    Task<SecurityEventPageDto> GetEventsAsync(
        SecurityEventQueryDto query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Set-based, tenant-scoped security operations queries. This service only reports
/// persisted facts or documented derivations and never synthesizes threat events.
/// </summary>
public sealed class SecurityOperationsService : ISecurityOperationsService
{
    private static readonly string[] PrivilegedRoleNames =
    {
        Constants.Roles.SuperAdmin,
        Constants.Roles.TenantAdmin,
        Constants.Roles.Manager,
        "SecurityAdmin",
        "Administrator"
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SecurityOperationsService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SecurityOperationsOverviewDto> GetOverviewAsync(
        string? range,
        DateTime? startUtc,
        DateTime? endUtc,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var now = DateTime.UtcNow;
        var nowOffset = DateTimeOffset.UtcNow;
        var selectedRange = ResolveRange(range, startUtc, endUtc, now);

        var settings = await _db.Set<Security>()
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == tenantId, cancellationToken);
        var idleThresholdMinutes = settings?.SessionTimeoutMinutes ?? 30;
        var staleBefore = now.AddMinutes(-idleThresholdMinutes);

        var tenantUsers = await _db.Users
            .AsNoTracking()
            .Where(user =>
                user.TenantId == tenantId ||
                user.UserTenants.Any(link =>
                    link.TenantId == tenantId &&
                    !link.IsDeleted &&
                    link.Status == UserTenantStatus.Active &&
                    (!link.ExpiresAt.HasValue || link.ExpiresAt > now)))
            .Select(user => new UserSecurityProjection
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                TwoFactorEnabled = user.TwoFactorEnabled,
                LockoutEnd = user.LockoutEnd,
                LastLoginDate = user.LastLoginDate,
                CreatedAt = user.CreatedAt,
                Roles = user.UserRoles.Select(link => link.Role.Name ?? string.Empty).ToList()
            })
            .ToListAsync(cancellationToken);

        var activeUsers = tenantUsers.Where(user => user.IsActive).ToList();
        var privilegedUsers = tenantUsers
            .Where(user => user.Roles.Any(IsPrivilegedRole))
            .ToList();

        var activeSessions = await _db.Set<UserSession>()
            .AsNoTracking()
            .Where(session => session.TenantId == tenantId && session.IsActive)
            .Select(session => new
            {
                session.LastActivityTime,
                UserIsActive = session.User.IsActive
            })
            .ToListAsync(cancellationToken);

        var securityLogs = _db.Set<SecurityLog>()
            .AsNoTracking()
            .Where(log =>
                log.TenantId == tenantId &&
                log.Timestamp >= selectedRange.StartUtc &&
                log.Timestamp <= selectedRange.EndUtc);

        var successfulLogins = await securityLogs.CountAsync(
            log => log.Action == nameof(SecurityAction.LoginSuccess) && log.Success,
            cancellationToken);
        var failedLogins = await securityLogs.CountAsync(
            log => log.Action == nameof(SecurityAction.LoginFailure) && !log.Success,
            cancellationToken);
        var passwordChanges = await securityLogs.CountAsync(
            log => log.Action == nameof(SecurityAction.PasswordChangeSuccess) && log.Success,
            cancellationToken);
        var securityEventCount = await securityLogs.CountAsync(cancellationToken);

        var auditLogs = _db.Set<AuditLog>()
            .AsNoTracking()
            .Where(log =>
                log.TenantId == tenantId &&
                log.Timestamp >= selectedRange.StartUtc &&
                log.Timestamp <= selectedRange.EndUtc);
        var auditEventCount = await auditLogs.CountAsync(cancellationToken);

        var openAlertsQuery = _db.Set<SecurityAlert>()
            .AsNoTracking()
            .Where(alert => alert.TenantId == tenantId && !alert.Dismissed);
        var openThreatsQuery = _db.Set<ThreatDetection>()
            .AsNoTracking()
            .Where(threat =>
                threat.TenantId == tenantId &&
                (threat.Status == ThreatStatus.New || threat.Status == ThreatStatus.InProgress));

        var openAlertCount = await openAlertsQuery.CountAsync(cancellationToken);
        var openThreatCount = await openThreatsQuery.CountAsync(cancellationToken);
        var criticalSignalCount =
            await openAlertsQuery.CountAsync(alert => alert.Type == "critical" || alert.Severity >= 9, cancellationToken) +
            await openThreatsQuery.CountAsync(threat => threat.Severity == ThreatSeverity.Critical, cancellationToken);
        var highSignalCount =
            await openAlertsQuery.CountAsync(
                alert => alert.Type != "critical" && alert.Severity >= 7 && alert.Severity < 9,
                cancellationToken) +
            await openThreatsQuery.CountAsync(threat => threat.Severity == ThreatSeverity.High, cancellationToken);
        var mediumSignalCount =
            await openAlertsQuery.CountAsync(
                alert => alert.Type != "critical" && alert.Severity >= 4 && alert.Severity < 7,
                cancellationToken) +
            await openThreatsQuery.CountAsync(threat => threat.Severity == ThreatSeverity.Medium, cancellationToken);

        var openAlerts = await openAlertsQuery
            .OrderByDescending(alert => alert.Timestamp)
            .Take(25)
            .ToListAsync(cancellationToken);
        var openThreats = await openThreatsQuery
            .OrderByDescending(threat => threat.DetectedAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        var alertItems = openAlerts
            .Select(MapAlert)
            .Concat(openThreats.Select(MapThreat))
            .OrderByDescending(item => item.TimestampUtc)
            .Take(10)
            .ToList();

        var recentSecurityEvents = await securityLogs
            .OrderByDescending(log => log.Timestamp)
            .Take(30)
            .Select(log => new SecurityTimelineEventDto
            {
                Id = log.Id.ToString(),
                TimestampUtc = log.Timestamp,
                Source = "SecurityLog",
                EventType = log.Action,
                Title = log.Action,
                UserName = log.Username,
                IpAddress = log.IpAddress,
                Success = log.Success,
                Evidence = log.FailureReason ?? (log.Success ? "Recorded successful security event." : "Recorded unsuccessful security event.")
            })
            .ToListAsync(cancellationToken);

        foreach (var item in recentSecurityEvents)
        {
            item.Severity = ClassifySecurityEvent(item.EventType, item.Success == true);
        }

        var recentAuditEvents = await auditLogs
            .Where(log =>
                log.Resource.Contains("Security") ||
                log.Resource.Contains("Session") ||
                log.Resource.Contains("User") ||
                log.Resource.Contains("Role") ||
                log.Resource.Contains("Permission") ||
                log.Resource.Contains("Retention") ||
                log.Action.Contains("Password") ||
                log.Action.Contains("MFA") ||
                log.Action.Contains("TwoFactor"))
            .OrderByDescending(log => log.Timestamp)
            .Take(30)
            .Select(log => new SecurityTimelineEventDto
            {
                Id = log.Id.ToString(),
                TimestampUtc = log.Timestamp,
                Source = "AuditLog",
                EventType = log.Action,
                Title = log.Action + " " + log.Resource,
                UserName = log.Username,
                IpAddress = log.IpAddress,
                Success = true,
                Evidence = "Persisted administrative audit event."
            })
            .ToListAsync(cancellationToken);

        foreach (var item in recentAuditEvents)
        {
            item.Severity = ClassifyAuditEvent(item.EventType, item.Title);
        }

        var authenticationTrend = selectedRange.Key == "24h"
            ? await BuildHourlyAuthenticationTrendAsync(securityLogs, cancellationToken)
            : await BuildDailyAuthenticationTrendAsync(securityLogs, cancellationToken);

        var retentionPolicy = await _db.Set<DataRetentionPolicy>()
            .AsNoTracking()
            .FirstOrDefaultAsync(policy => policy.TenantId == tenantId && !policy.IsDeleted, cancellationToken);
        var lastRetentionRun = await _db.Set<DataRetentionJobRun>()
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && !run.IsDeleted)
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lockedAccounts = tenantUsers.Count(user => user.LockoutEnd.HasValue && user.LockoutEnd.Value > nowOffset);
        var staleSessions = activeSessions.Count(session => session.LastActivityTime < staleBefore);
        var disabledUserSessions = activeSessions.Count(session => !session.UserIsActive);
        var highRiskSignals = criticalSignalCount + highSignalCount;

        var evidence = new SecurityHealthEvidenceDto
        {
            EligibleUsers = activeUsers.Count,
            MfaEnabledUsers = activeUsers.Count(user => user.TwoFactorEnabled),
            PrivilegedUsers = privilegedUsers.Count(user => user.IsActive),
            PrivilegedUsersWithMfa = privilegedUsers.Count(user => user.IsActive && user.TwoFactorEnabled),
            ActiveSessions = activeSessions.Count,
            StaleSessions = staleSessions,
            DisabledUsersWithActiveSessions = disabledUserSessions,
            HasAuditEvents = auditEventCount > 0,
            HasSecurityEvents = securityEventCount > 0,
            SuccessfulLogins = successfulLogins,
            FailedLogins = failedLogins,
            LockedAccounts = lockedAccounts,
            UnresolvedHighRiskSignals = highRiskSignals,
            HasPersistedSecuritySettings = settings != null,
            PasswordPolicyConfigured = settings?.PasswordMinLength >= 8 &&
                                       settings.PasswordRequireLowercase &&
                                       settings.PasswordRequireUppercase &&
                                       settings.PasswordRequireDigits,
            LockoutConfigured = settings?.MaxFailedLoginAttempts is > 0 and <= 10 && settings.AccountLockoutMinutes > 0,
            LoginRateLimitConfigured = settings?.RateLimitLoginMaxAttempts is > 0 and <= 10 && settings.RateLimitLoginWindowMinutes > 0,
            SessionTimeoutConfigured = settings?.SessionTimeoutMinutes is >= 5 and <= 240,
            RetentionConfigured = retentionPolicy?.Enabled == true
        };

        var attempts = successfulLogins + failedLogins;
        return new SecurityOperationsOverviewDto
        {
            Range = selectedRange,
            Snapshot = new SecuritySnapshotDto
            {
                EligibleUsers = activeUsers.Count,
                ActiveUsers = activeUsers.Count,
                MfaEnabledUsers = evidence.MfaEnabledUsers,
                MfaAdoptionPercent = Percent(evidence.MfaEnabledUsers, activeUsers.Count),
                PrivilegedUsers = evidence.PrivilegedUsers,
                PrivilegedUsersWithoutMfa = evidence.PrivilegedUsers - evidence.PrivilegedUsersWithMfa,
                LockedAccounts = lockedAccounts,
                ActiveSessions = activeSessions.Count,
                StaleSessions = staleSessions,
                DisabledUsersWithActiveSessions = disabledUserSessions,
                SessionIdleThresholdMinutes = idleThresholdMinutes
            },
            Activity = new SecurityActivitySummaryDto
            {
                SuccessfulLogins = successfulLogins,
                FailedLogins = failedLogins,
                LoginFailureRatePercent = Percent(failedLogins, attempts),
                PasswordChanges = passwordChanges,
                SecurityEvents = securityEventCount,
                AuditEvents = auditEventCount
            },
            Health = SecurityHealthScoring.Calculate(evidence),
            Alerts = new SecurityAlertSummaryDto
            {
                OpenTotal = openAlertCount + openThreatCount,
                Critical = criticalSignalCount,
                High = highSignalCount,
                Medium = mediumSignalCount,
                Items = alertItems
            },
            RecentEvents = recentSecurityEvents
                .Concat(recentAuditEvents)
                .OrderByDescending(item => item.TimestampUtc)
                .Take(15)
                .ToList(),
            PrivilegedUsers = privilegedUsers
                .OrderByDescending(user => PrivilegeRank(user.Roles))
                .ThenBy(user => user.UserName)
                .Take(20)
                .Select(user => MapPrivilegedUser(user, nowOffset))
                .ToList(),
            AuthenticationTrend = authenticationTrend,
            Configuration = MapConfiguration(settings),
            Retention = new SecurityRetentionSummaryDto
            {
                Configured = retentionPolicy != null,
                Enabled = retentionPolicy?.Enabled == true,
                AuditLogRetentionDays = retentionPolicy?.AuditLogRetentionDays,
                SecurityLogRetentionDays = retentionPolicy?.SecurityLogRetentionDays,
                LastRunStartedAtUtc = lastRetentionRun?.StartedAtUtc,
                LastRunCompletedAtUtc = lastRetentionRun?.CompletedAtUtc,
                LastRunSucceeded = lastRetentionRun?.Success
            },
            GeneratedAtUtc = now
        };
    }

    public async Task<SecurityEventPageDto> GetEventsAsync(
        SecurityEventQueryDto query,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var endUtc = NormalizeUtc(query.EndUtc ?? DateTime.UtcNow);
        var startUtc = NormalizeUtc(query.StartUtc ?? endUtc.AddDays(-7));
        if (endUtc <= startUtc || endUtc - startUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentException("Security event range must be positive and no longer than 366 days.");
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = _db.Set<SecurityLog>()
            .AsNoTracking()
            .Where(log => log.TenantId == tenantId && log.Timestamp >= startUtc && log.Timestamp <= endUtc);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            source = source.Where(log => log.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(query.User))
        {
            var user = query.User.Trim();
            source = source.Where(log => log.Username != null && log.Username.Contains(user));
        }

        if (!string.IsNullOrWhiteSpace(query.IpAddress))
        {
            var ip = query.IpAddress.Trim();
            source = source.Where(log => log.IpAddress.Contains(ip));
        }

        if (query.Success.HasValue)
        {
            source = source.Where(log => log.Success == query.Success.Value);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(log => log.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new SecurityTimelineEventDto
            {
                Id = log.Id.ToString(),
                TimestampUtc = log.Timestamp,
                Source = "SecurityLog",
                EventType = log.Action,
                Title = log.Action,
                UserName = log.Username,
                IpAddress = log.IpAddress,
                Success = log.Success,
                Evidence = log.FailureReason ?? (log.Success ? "Recorded successful security event." : "Recorded unsuccessful security event.")
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.Severity = ClassifySecurityEvent(item.EventType, item.Success == true);
        }

        return new SecurityEventPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling((decimal)total / pageSize)
        };
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for security operations.");
        }

        return tenantId.Value;
    }

    private static SecurityTimeRangeDto ResolveRange(
        string? range,
        DateTime? startUtc,
        DateTime? endUtc,
        DateTime now)
    {
        var key = string.IsNullOrWhiteSpace(range) ? "24h" : range.Trim().ToLowerInvariant();
        var end = NormalizeUtc(endUtc ?? now);
        var start = key switch
        {
            "24h" => end.AddHours(-24),
            "7d" => end.AddDays(-7),
            "30d" => end.AddDays(-30),
            "custom" when startUtc.HasValue => NormalizeUtc(startUtc.Value),
            _ => throw new ArgumentException("Range must be 24h, 7d, 30d, or custom with a startUtc value.")
        };

        if (end <= start || end - start > TimeSpan.FromDays(366))
        {
            throw new ArgumentException("Security range must be positive and no longer than 366 days.");
        }

        return new SecurityTimeRangeDto { Key = key, StartUtc = start, EndUtc = end };
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static async Task<List<AuthenticationTrendPointDto>> BuildHourlyAuthenticationTrendAsync(
        IQueryable<SecurityLog> securityLogs,
        CancellationToken cancellationToken)
    {
        var rows = await securityLogs
            .Where(log =>
                log.Action == nameof(SecurityAction.LoginSuccess) ||
                log.Action == nameof(SecurityAction.LoginFailure))
            .GroupBy(log => new
            {
                log.Timestamp.Year,
                log.Timestamp.Month,
                log.Timestamp.Day,
                log.Timestamp.Hour
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                Successful = group.Count(log => log.Action == nameof(SecurityAction.LoginSuccess) && log.Success),
                Failed = group.Count(log => log.Action == nameof(SecurityAction.LoginFailure) && !log.Success)
            })
            .OrderBy(row => row.Year)
            .ThenBy(row => row.Month)
            .ThenBy(row => row.Day)
            .ThenBy(row => row.Hour)
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            var periodStart = new DateTime(row.Year, row.Month, row.Day, row.Hour, 0, 0, DateTimeKind.Utc);
            return new AuthenticationTrendPointDto
            {
                PeriodStartUtc = periodStart,
                Label = periodStart.ToString("HH:mm"),
                Successful = row.Successful,
                Failed = row.Failed
            };
        }).ToList();
    }

    private static async Task<List<AuthenticationTrendPointDto>> BuildDailyAuthenticationTrendAsync(
        IQueryable<SecurityLog> securityLogs,
        CancellationToken cancellationToken)
    {
        var rows = await securityLogs
            .Where(log =>
                log.Action == nameof(SecurityAction.LoginSuccess) ||
                log.Action == nameof(SecurityAction.LoginFailure))
            .GroupBy(log => log.Timestamp.Date)
            .Select(group => new
            {
                Date = group.Key,
                Successful = group.Count(log => log.Action == nameof(SecurityAction.LoginSuccess) && log.Success),
                Failed = group.Count(log => log.Action == nameof(SecurityAction.LoginFailure) && !log.Success)
            })
            .OrderBy(row => row.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new AuthenticationTrendPointDto
        {
            PeriodStartUtc = DateTime.SpecifyKind(row.Date, DateTimeKind.Utc),
            Label = row.Date.ToString("MMM d"),
            Successful = row.Successful,
            Failed = row.Failed
        }).ToList();
    }

    private static decimal Percent(int numerator, int denominator) =>
        denominator == 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 1);

    private static bool IsPrivilegedRole(string role) =>
        PrivilegedRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static int PrivilegeRank(IEnumerable<string> roles)
    {
        if (roles.Any(role => role.Equals(Constants.Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))) return 3;
        if (roles.Any(role => role.Equals(Constants.Roles.TenantAdmin, StringComparison.OrdinalIgnoreCase))) return 2;
        return 1;
    }

    private static PrivilegedAccessUserDto MapPrivilegedUser(UserSecurityProjection user, DateTimeOffset now)
    {
        var findings = new List<string>();
        if (!user.TwoFactorEnabled) findings.Add("MFA is not enabled.");
        if (!user.IsActive) findings.Add("Privileged account is inactive.");
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > now) findings.Add("Account is locked.");
        if (!user.LastLoginDate.HasValue) findings.Add("No recorded successful login.");

        return new PrivilegedAccessUserDto
        {
            UserId = user.Id.ToString(),
            DisplayName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            UserName = user.UserName,
            Email = user.Email,
            Roles = user.Roles.OrderBy(role => role).ToList(),
            PrivilegeLevel = PrivilegeRank(user.Roles) switch { 3 => "System", 2 => "Tenant", _ => "Elevated" },
            IsActive = user.IsActive,
            MfaEnabled = user.TwoFactorEnabled,
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
            LastLoginUtc = user.LastLoginDate,
            CreatedAtUtc = user.CreatedAt,
            Findings = findings
        };
    }

    private static SecurityConfigurationSummaryDto MapConfiguration(Security? settings)
    {
        if (settings == null)
        {
            return new SecurityConfigurationSummaryDto();
        }

        var provider = settings.CaptchaProvider?.Trim().ToLowerInvariant();
        var captchaConfigured = provider == "hcaptcha"
            ? !string.IsNullOrWhiteSpace(settings.HCaptchaSiteKey) && !string.IsNullOrWhiteSpace(settings.HCaptchaSecretKey)
            : !string.IsNullOrWhiteSpace(settings.RecaptchaSiteKey) && !string.IsNullOrWhiteSpace(settings.RecaptchaSecretKey);

        return new SecurityConfigurationSummaryDto
        {
            Persisted = true,
            PasswordMinimumLength = settings.PasswordMinLength,
            FailedAttemptThreshold = settings.MaxFailedLoginAttempts,
            LockoutMinutes = settings.AccountLockoutMinutes,
            SessionTimeoutMinutes = settings.SessionTimeoutMinutes,
            AccessTokenLifetimeMinutes = settings.JwtTokenLifetimeMinutes,
            ConcurrentLoginPolicy = settings.PreventConcurrentLogin.ToString(),
            LoginRateLimitingConfigured = settings.RateLimitLoginMaxAttempts > 0 && settings.RateLimitLoginWindowMinutes > 0,
            CaptchaEnabled = settings.CaptchaEnabled,
            CaptchaConfigured = captchaConfigured
        };
    }

    private static SecurityAlertItemDto MapAlert(SecurityAlert alert)
    {
        var severity = alert.Type.Equals("critical", StringComparison.OrdinalIgnoreCase) || alert.Severity >= 9
            ? "critical"
            : alert.Severity >= 7 ? "high" : alert.Severity >= 4 ? "medium" : "low";
        return new SecurityAlertItemDto
        {
            Id = alert.Id.ToString(),
            Source = "SecurityAlert",
            Severity = severity,
            Status = "new",
            Title = alert.Title,
            Description = alert.Message,
            TimestampUtc = alert.Timestamp,
            UserName = alert.AffectedUser,
            IpAddress = alert.IpAddress
        };
    }

    private static SecurityAlertItemDto MapThreat(ThreatDetection threat) => new()
    {
        Id = threat.Id.ToString(),
        Source = "ThreatDetection",
        Severity = threat.Severity.ToString().ToLowerInvariant(),
        Status = threat.Status.ToString().ToLowerInvariant(),
        Title = threat.Title,
        Description = threat.Description,
        TimestampUtc = threat.DetectedAt,
        UserName = threat.AffectedUserName,
        IpAddress = threat.IpAddress
    };

    private static string ClassifySecurityEvent(string eventType, bool success)
    {
        if (eventType.Equals(nameof(SecurityAction.BruteForceAttempt), StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals(nameof(SecurityAction.SuspiciousActivity), StringComparison.OrdinalIgnoreCase)) return "high";
        if (eventType.Equals(nameof(SecurityAction.AccountLocked), StringComparison.OrdinalIgnoreCase)) return "medium";
        if (!success || eventType.Equals(nameof(SecurityAction.TwoFactorDisabled), StringComparison.OrdinalIgnoreCase)) return "medium";
        return "informational";
    }

    private static string ClassifyAuditEvent(string eventType, string title)
    {
        if (title.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Permission", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
            eventType.Contains("Terminate", StringComparison.OrdinalIgnoreCase)) return "medium";
        return "informational";
    }

    private sealed class UserSecurityProjection
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
