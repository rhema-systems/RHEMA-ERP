using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSpecificationTemplateService
{
    Task<ProcurementSpecificationTemplateSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplatePageDto> SearchAsync(ProcurementSpecificationTemplateSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto?> GetEffectiveAsync(string templateCode, DateTime atUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSpecificationWorkflowOptionDto>> GetWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> CreateAsync(SaveProcurementSpecificationTemplateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> UpdateAsync(Guid id, SaveProcurementSpecificationTemplateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateValidationDto> ValidateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> SubmitAsync(Guid id, ProcurementSpecificationTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> PublishAsync(Guid id, ProcurementSpecificationTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> RejectAsync(Guid id, ProcurementSpecificationTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> CloneAsync(Guid id, CloneProcurementSpecificationTemplateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSpecificationTemplateDto> RetireAsync(Guid id, ProcurementSpecificationTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(Guid id, ProcurementSpecificationTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSpecificationTemplateNotFoundException(string message) : InvalidOperationException(message);
public sealed class ProcurementSpecificationTemplateConflictException(string message) : InvalidOperationException(message);
public sealed class ProcurementSpecificationTemplateAuthorizationException(string message) : UnauthorizedAccessException(message);
public sealed class ProcurementSpecificationTemplateValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
