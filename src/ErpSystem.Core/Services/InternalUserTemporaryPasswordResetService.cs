using System.Text.RegularExpressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

public sealed record InternalUserTemporaryPasswordResetCommand(
    Guid TargetUserId,
    Guid TenantId,
    Guid ActorUserId,
    string ActorUserName,
    string NewPassword,
    string Reason,
    string IpAddress,
    string UserAgent);

public sealed record InternalUserTemporaryPasswordResetResult(
    bool Succeeded,
    string? Code,
    string? Detail,
    DateTime? TemporaryPasswordExpiresAtUtc,
    IReadOnlyCollection<string> Errors)
{
    public static InternalUserTemporaryPasswordResetResult Success(DateTime expiresAtUtc) =>
        new(true, null, null, expiresAtUtc, Array.Empty<string>());

    public static InternalUserTemporaryPasswordResetResult Failure(
        string code,
        string detail,
        IEnumerable<string>? errors = null) =>
        new(false, code, detail, null,
            errors?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToArray()
            ?? Array.Empty<string>());
}

public interface IInternalUserTemporaryPasswordResetService
{
    Task<InternalUserTemporaryPasswordResetResult> ResetAsync(
        InternalUserTemporaryPasswordResetCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the governed administrative reset of local, internal user credentials.
/// Supplier/external credentials remain on their dedicated portal lifecycle.
/// </summary>
public sealed class InternalUserTemporaryPasswordResetService :
    IInternalUserTemporaryPasswordResetService
{
    internal const int TemporaryPasswordLifetimeHours = 24;
    internal const int MaximumPasswordLength = 128;
    internal const int MaximumReasonLength = 500;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantService _userTenantService;
    private readonly IBusinessPartnerUserRepository _businessPartnerUsers;
    private readonly ISettingsService _settingsService;
    private readonly IUserSessionService _userSessionService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<InternalUserTemporaryPasswordResetService> _logger;

    public InternalUserTemporaryPasswordResetService(
        UserManager<ApplicationUser> userManager,
        IUserTenantService userTenantService,
        IBusinessPartnerUserRepository businessPartnerUsers,
        ISettingsService settingsService,
        IUserSessionService userSessionService,
        IAuditLogService auditLogService,
        ILogger<InternalUserTemporaryPasswordResetService> logger)
    {
        _userManager = userManager;
        _userTenantService = userTenantService;
        _businessPartnerUsers = businessPartnerUsers;
        _settingsService = settingsService;
        _userSessionService = userSessionService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<InternalUserTemporaryPasswordResetResult> ResetAsync(
        InternalUserTemporaryPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command.TargetUserId == Guid.Empty || command.TenantId == Guid.Empty ||
            command.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(command.ActorUserName))
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "INTERNAL_USER_RESET_CONTEXT_INVALID",
                "A valid tenant, target user, and administrator context are required.");
        }
        if (command.TargetUserId == command.ActorUserId)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "INTERNAL_USER_SELF_RESET_PROHIBITED",
                "Use the authenticated change-password flow to change your own password.");
        }

        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5 || reason.Length > MaximumReasonLength)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "INTERNAL_USER_RESET_REASON_INVALID",
                $"Reason must contain 5 to {MaximumReasonLength} characters.");
        }
        if (string.IsNullOrEmpty(command.NewPassword) ||
            command.NewPassword.Length > MaximumPasswordLength)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "TEMPORARY_PASSWORD_POLICY_FAILED",
                $"Temporary password is required and must not exceed {MaximumPasswordLength} characters.");
        }

        var target = await _userManager.FindByIdAsync(command.TargetUserId.ToString());
        if (target is null)
            return NotFound();

        var hasTenantAccess = target.TenantId == command.TenantId ||
                              await _userTenantService.HasActiveAccessAsync(
                                  target.Id, command.TenantId);
        if (!hasTenantAccess)
            return NotFound();
        if (!target.IsActive)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "INTERNAL_USER_INACTIVE",
                "Temporary passwords cannot be issued to an inactive user.");
        }
        if (target.AuthenticationProvider != AuthenticationProvider.Local)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "INTERNAL_USER_LOCAL_CREDENTIAL_REQUIRED",
                "This account is managed by an external identity provider.");
        }

        var roles = await _userManager.GetRolesAsync(target);
        var hasExternalRole = roles.Any(role => string.Equals(
            role, Constants.Roles.ExternalUser, StringComparison.OrdinalIgnoreCase));
        var businessPartnerUser = await _businessPartnerUsers.GetByUserIdAsync(target.Id);
        if (hasExternalRole || businessPartnerUser is not null)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "INTERNAL_USER_EXTERNAL_RESET_PROHIBITED",
                "Supplier and external user credentials must be managed through their dedicated portal flow.");
        }

        var policyErrors = await ValidatePasswordAsync(
            target, command.TenantId, command.NewPassword);
        if (policyErrors.Count > 0)
        {
            return InternalUserTemporaryPasswordResetResult.Failure(
                "TEMPORARY_PASSWORD_POLICY_FAILED",
                "Temporary password does not meet the configured password policy.",
                policyErrors);
        }

        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.AddHours(TemporaryPasswordLifetimeHours);
        var previousMustChangePassword = target.MustChangePassword;
        var previousExpiry = target.TemporaryPasswordExpiresAtUtc;
        var previousPasswordChangedAt = target.PasswordChangedAtUtc;
        var previousUpdatedAt = target.UpdatedAt;
        var previousUpdatedBy = target.UpdatedBy;

        target.MustChangePassword = true;
        target.TemporaryPasswordExpiresAtUtc = expiresAtUtc;
        target.PasswordChangedAtUtc = nowUtc;
        target.UpdatedAt = nowUtc;
        target.UpdatedBy = command.ActorUserName.Trim();

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(target);
        var reset = await _userManager.ResetPasswordAsync(
            target, resetToken, command.NewPassword);
        if (!reset.Succeeded)
        {
            target.MustChangePassword = previousMustChangePassword;
            target.TemporaryPasswordExpiresAtUtc = previousExpiry;
            target.PasswordChangedAtUtc = previousPasswordChangedAt;
            target.UpdatedAt = previousUpdatedAt;
            target.UpdatedBy = previousUpdatedBy;
            return InternalUserTemporaryPasswordResetResult.Failure(
                "TEMPORARY_PASSWORD_RESET_FAILED",
                "The temporary password could not be applied.",
                reset.Errors.Select(error => error.Description));
        }

        await _userSessionService.TerminateAllUserSessionsAsync(
            target.Id,
            excludeSessionId: null,
            reason: "Administrative temporary-password reset");

        await _auditLogService.LogUserActionAsync(
            command.ActorUserId,
            command.ActorUserName.Trim(),
            "ResetInternalUserTemporaryPassword",
            "User",
            target.Id.ToString(),
            oldValues: null,
            newValues: new
            {
                TargetUserId = target.Id,
                target.MustChangePassword,
                target.TemporaryPasswordExpiresAtUtc,
                Reason = reason,
                SessionsInvalidated = true
            },
            ipAddress: Normalize(command.IpAddress, 45, "Unknown"),
            userAgent: Normalize(command.UserAgent, 500, "Unknown"));

        _logger.LogInformation(
            "Administrator {ActorUserId} issued a temporary credential for internal user {TargetUserId} in tenant {TenantId}; active sessions were invalidated",
            command.ActorUserId, target.Id, command.TenantId);

        return InternalUserTemporaryPasswordResetResult.Success(expiresAtUtc);
    }

    private async Task<List<string>> ValidatePasswordAsync(
        ApplicationUser target,
        Guid tenantId,
        string password)
    {
        var errors = new List<string>();
        var security = await _settingsService.GetSecuritySettingsAsync(tenantId);
        if (security is not null)
        {
            if (password.Length < security.PasswordMinLength)
                errors.Add($"Password must be at least {security.PasswordMinLength} characters long.");
            if (security.PasswordRequireUppercase && !Regex.IsMatch(password, "[A-Z]"))
                errors.Add("Password must contain at least one uppercase letter.");
            if (security.PasswordRequireLowercase && !Regex.IsMatch(password, "[a-z]"))
                errors.Add("Password must contain at least one lowercase letter.");
            if (security.PasswordRequireDigits && !Regex.IsMatch(password, "[0-9]"))
                errors.Add("Password must contain at least one digit.");
            if (security.PasswordRequireSpecialChars && !Regex.IsMatch(
                    password, "[^a-zA-Z0-9]"))
                errors.Add("Password must contain at least one special character.");
        }

        foreach (var validator in _userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(_userManager, target, password);
            if (!validation.Succeeded)
                errors.AddRange(validation.Errors.Select(error => error.Description));
        }

        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    private static InternalUserTemporaryPasswordResetResult NotFound() =>
        InternalUserTemporaryPasswordResetResult.Failure(
            "INTERNAL_USER_NOT_FOUND",
            "The internal user was not found in the current tenant.");

    private static string Normalize(string? value, int maxLength, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
