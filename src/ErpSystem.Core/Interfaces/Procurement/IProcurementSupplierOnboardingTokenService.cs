using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierOnboardingTokenService
{
    Task<ProcurementSupplierOnboardingTokenSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenPageDto> SearchAsync(ProcurementSupplierOnboardingTokenSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenDto?> GetForRegistrationAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierOnboardingPaymentMethodOptionDto>> GetPaymentMethodsAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenIssueResultDto> IssueAsync(IssueProcurementSupplierOnboardingTokenRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenIssueResultDto> IssueForVerifiedApplicantAsync(Guid tenantId, Guid registrationId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenDto> ValidateApplicantTokenAsync(Guid tenantId, string plaintextToken, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenIssueResultDto> ReissueAsync(Guid id, ReissueProcurementSupplierOnboardingTokenRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenIssueResultDto> RecordPaymentAsync(Guid id, RecordProcurementSupplierOnboardingPaymentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenIssueResultDto> ReconcilePaymentAsync(Guid tokenId, Guid paymentId, ReconcileProcurementSupplierOnboardingPaymentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenDto> RequestExemptionAsync(Guid id, RequestProcurementSupplierOnboardingExemptionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierOnboardingTokenIssueResultDto> DecideExemptionAsync(Guid tokenId, Guid exemptionId, DecideProcurementSupplierOnboardingExemptionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task ExpireForTerminalRegistrationAsync(Guid registrationId, string terminalStatus, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierOnboardingTokenNotFoundException(string code, string message) :
    InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierOnboardingTokenConflictException(string code, string message) :
    InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierOnboardingTokenValidationException(string code, string message) :
    InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierOnboardingTokenAuthorizationException(string message) :
    UnauthorizedAccessException(message);
