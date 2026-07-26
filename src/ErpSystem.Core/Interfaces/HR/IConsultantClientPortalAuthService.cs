using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IConsultantClientPortalAuthService
{
    Task<ConsultantClientPortalAuthResultDto> RegisterAsync(
        ConsultantClientPortalRegisterDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ConsultantClientPortalAuthResultDto> LoginAsync(
        ConsultantClientPortalLoginDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    // Token flows resolve tenant from the globally-unique token; no X-Tenant-Id header (emailed link).
    Task VerifyEmailAsync(string token, CancellationToken ct = default);

    Task RequestPasswordResetAsync(string email, Guid tenantId, CancellationToken ct = default);

    Task ResetPasswordAsync(ConsultantClientPortalResetPasswordDto dto, CancellationToken ct = default);

    Task CompleteAccountSetupAsync(
        ConsultantClientPortalCompleteSetupDto dto,
        CancellationToken ct = default);

    Task ChangePasswordAsync(
        Guid accountId,
        ConsultantClientPortalChangePasswordDto dto,
        Guid tenantId,
        CancellationToken ct = default);

    Task<ConsultantClientPortalAccountSummaryDto> InvitePortalAccountAsync(
        Guid consultantClientId,
        ConsultantClientPortalInviteDto dto,
        Guid tenantId,
        Guid invitedByEmployeeId,
        CancellationToken ct = default);

    Task ResendPortalInviteAsync(
        Guid consultantClientId,
        string email,
        Guid tenantId,
        CancellationToken ct = default);

    Task<IEnumerable<ConsultantClientPortalAccountSummaryDto>> GetPortalAccountsForClientAsync(
        Guid consultantClientId,
        Guid tenantId,
        CancellationToken ct = default);
}
