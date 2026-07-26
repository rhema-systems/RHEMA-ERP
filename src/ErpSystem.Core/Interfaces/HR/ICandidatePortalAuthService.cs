using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Authentication service for the external candidate portal.
/// Handles registration, login, email verification and password management
/// for <see cref="ErpSystem.Core.Entities.HR.Recruitment.CandidatePortalAccount"/> accounts.
/// </summary>
public interface ICandidatePortalAuthService
{
    /// <summary>Register a new candidate portal account.</summary>
    Task<CandidatePortalAuthResultDto> RegisterAsync(
        CandidatePortalRegisterDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>Authenticate with email and password.</summary>
    Task<CandidatePortalAuthResultDto> LoginAsync(
        CandidatePortalLoginDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>Verify the email using the token sent during registration.</summary>
    // Tenant resolved from the globally-unique token; no X-Tenant-Id header required (emailed link).
    Task VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary>Initiate password reset — sends a reset link to the candidate's email.</summary>
    Task RequestPasswordResetAsync(string email, Guid tenantId, CancellationToken ct = default);

    /// <summary>Complete password reset using the token emailed to the candidate.</summary>
    Task ResetPasswordAsync(CandidatePortalResetPasswordDto dto, CancellationToken ct = default);

    /// <summary>Change password for an authenticated candidate.</summary>
    Task ChangePasswordAsync(Guid accountId, CandidatePortalChangePasswordDto dto, Guid tenantId, CancellationToken ct = default);
}
