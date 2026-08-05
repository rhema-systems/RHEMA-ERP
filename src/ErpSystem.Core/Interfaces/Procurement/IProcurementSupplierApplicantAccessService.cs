using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierApplicantAccessService
{
    Task<SupplierApplicantTokenIssueDto> CreateVerifiedApplicationAsync(
        VerifyAndIssueSupplierApplicantTokenRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantSessionDto> StartSessionAsync(
        StartSupplierApplicantSessionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantTokenDeliveryDto> DeliverApplicationTokenAsync(
        Guid tokenId,
        string plaintextToken,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantSessionDto> ValidateSessionAsync(
        Guid sessionReference,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantPortalDto> GetPortalAsync(
        Guid sessionReference,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantPortalDto> UpdateApplicationAsync(
        Guid sessionReference,
        UpdateSupplierApplicantApplicationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantPortalDto> SubmitAsync(
        Guid sessionReference,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task CloseForTerminalRegistrationAsync(
        Guid registrationId,
        string terminalStatus,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task ProvisionApprovedSupplierAsync(
        Guid registrationId,
        Guid businessPartnerId,
        Guid approvedById,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task RetryApprovedSupplierActivationAsync(
        Guid registrationId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task ResendTemporaryCredentialAsync(
        Guid registrationId,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task CompleteCredentialActivationAsync(
        Guid approvedUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<SupplierApplicantAccessSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierApplicantAccessListItemDto>> GetHistoryAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierApplicantAccessException(
    string code,
    string message,
    int statusCode = 409) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
