using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSourcingCaseService
{
    Task<ProcurementSourcingCaseSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCasePageDto> SearchAsync(ProcurementSourcingCaseSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSourcingCaseSourceOptionDto>> GetSourceOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseReadinessDto> GetReadinessAsync(Guid requisitionId, ProcurementMethodType? method = null,
        string? overrideReason = null, CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseDto> CreateAsync(CreateProcurementSourcingCaseRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseDto> CloseAsync(Guid id, ProcurementSourcingCaseActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseDto> CancelAsync(Guid id, ProcurementSourcingCaseActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseEntryGateDto> EnforceSourceEntryAsync(Guid requisitionId, ProcurementMethodType? expectedMethod, string sourceType, string sourceReference, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSourcingCaseEntryGateDto> RevalidateSourceEntryAsync(Guid requisitionId, Guid sourcingReleaseId, Guid sourcingCaseId,
        ProcurementMethodType expectedMethod, string sourceType, Guid sourceEntityId, string sourceReference,
        string correlationId, CancellationToken cancellationToken = default);
    Task RegisterSourceRequestAsync(Guid sourcingCaseId, string sourceType, Guid sourceEntityId, string sourceEntityReference, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSourcingCaseNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSourcingCaseValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSourcingCaseConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSourcingCaseAuthorizationException(string message) : UnauthorizedAccessException(message);
