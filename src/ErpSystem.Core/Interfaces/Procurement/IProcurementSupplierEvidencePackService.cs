using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierEvidencePackService
{
    Task<ProcurementSupplierEvidencePackSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackPageDto> SearchAsync(ProcurementSupplierEvidencePackSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierEvidencePackOptionDto>> GetWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierEvidencePackOptionDto>> GetConfigurationProfileOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> CreateAsync(SaveProcurementSupplierEvidencePackRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> UpdateAsync(Guid id, SaveProcurementSupplierEvidencePackRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> SubmitAsync(Guid id, ProcurementSupplierEvidencePackLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> PublishAsync(Guid id, ProcurementSupplierEvidencePackLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> RejectAsync(Guid id, ProcurementSupplierEvidencePackLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> CloneAsync(Guid id, CloneProcurementSupplierEvidencePackRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidencePackDto> RetireAsync(Guid id, ProcurementSupplierEvidencePackLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, ProcurementSupplierEvidencePackLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidenceReadinessDto> GetRegistrationReadinessAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierEvidenceReadinessDto> BindAndValidateRegistrationAsync(Guid registrationId, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierEvidencePackNotFoundException(string code, string message) :
    InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierEvidencePackConflictException(string code, string message) :
    InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierEvidencePackValidationException(string code, string message) :
    InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierEvidencePackAuthorizationException(string message) :
    UnauthorizedAccessException(message);
