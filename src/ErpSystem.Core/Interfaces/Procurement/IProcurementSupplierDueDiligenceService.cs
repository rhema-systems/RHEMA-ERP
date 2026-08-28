using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierDueDiligenceService
{
    Task<ProcurementSupplierDueDiligenceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligencePageDto> SearchAsync(ProcurementSupplierDueDiligenceSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceCurrentStateDto> GetCurrentStateAsync(Guid businessPartnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierDueDiligenceWorkflowOptionDto>> GetWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierDueDiligenceSupplierOptionDto>> GetSupplierOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> CreateAsync(CreateProcurementSupplierDueDiligenceRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> UpdateAsync(Guid id, UpdateProcurementSupplierDueDiligenceRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> SubmitAsync(Guid id, ProcurementSupplierDueDiligenceLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> ApproveAsync(Guid id, ProcurementSupplierDueDiligenceLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> SupersedeStaleAsync(Guid id, ProcurementSupplierDueDiligenceLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierDueDiligenceDto> RejectAsync(Guid id, ProcurementSupplierDueDiligenceLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<int> ProcessExpiryAsync(Guid? tenantId = null, DateTime? atUtc = null, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierDueDiligenceNotFoundException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierDueDiligenceValidationException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierDueDiligenceConflictException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierDueDiligenceAuthorizationException(string message) :
    Exception(message);
