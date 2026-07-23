using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementControlEventService
{
    Task<ProcurementControlEventDto> RecordAsync(ProcurementControlEventWriteRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementControlEventDto> RecordSystemAsync(Guid tenantId, string actorName,
        ProcurementControlEventWriteRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementControlEventSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementControlEventPageDto> SearchAsync(ProcurementControlEventSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementControlEventDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementControlEventDto>> GetCorrelationAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementControlEventIntegrityDto> VerifyIntegrityAsync(int take = 1000, CancellationToken cancellationToken = default);
}

public sealed class ProcurementControlEventAuthorizationException(string message) : Exception(message);
public sealed class ProcurementControlEventNotFoundException(string message) : Exception(message);
public sealed class ProcurementControlEventConflictException(string message) : Exception(message);
public sealed class ProcurementControlEventValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
