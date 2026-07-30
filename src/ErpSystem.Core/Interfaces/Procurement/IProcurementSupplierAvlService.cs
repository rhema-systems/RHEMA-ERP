using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierAvlService
{
    Task<ProcurementSupplierAvlSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlPageDto> SearchAsync(ProcurementSupplierAvlSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlCurrentStateDto> GetCurrentStateAsync(Guid businessPartnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierAvlWorkflowOptionDto>> GetWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierAvlSupplierOptionDto>> GetSupplierOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> CreateAsync(CreateProcurementSupplierAvlRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> UpdateAsync(Guid id, UpdateProcurementSupplierAvlRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> AddEntryAsync(Guid id, AddProcurementSupplierAvlEntryRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> RemoveEntryAsync(Guid id, Guid entryId, string rowVersion, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> SubmitAsync(Guid id, ProcurementSupplierAvlLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> ApproveAsync(Guid id, ProcurementSupplierAvlLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> RejectAsync(Guid id, ProcurementSupplierAvlLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> PublishAsync(Guid id, ProcurementSupplierAvlLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> SuspendEntryAsync(Guid id, Guid entryId, ProcurementSupplierAvlEntryLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierAvlDto> ReinstateEntryAsync(Guid id, Guid entryId, ProcurementSupplierAvlEntryLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<int> ProcessExpiryAsync(Guid? tenantId = null, DateTime? atUtc = null, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierAvlNotFoundException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierAvlValidationException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierAvlConflictException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierAvlAuthorizationException(string message) :
    Exception(message);
