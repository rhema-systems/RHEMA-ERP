using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierRiskService
{
    Task<ProcurementSupplierRiskSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierRiskPageDto> SearchAsync(ProcurementSupplierRiskSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierRiskAssessmentDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierRiskCurrentStateDto> GetCurrentStateAsync(Guid businessPartnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierRiskSupplierOptionDto>> GetSupplierOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierRiskWorkflowOptionDto>> GetWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementSupplierRiskAssessmentDto> EvaluateAsync(EvaluateProcurementSupplierRiskRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierRiskAlertDto> EscalateAsync(Guid alertId, EscalateProcurementSupplierRiskAlertRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementSupplierRiskAlertDto> ResolveAsync(Guid alertId, ResolveProcurementSupplierRiskAlertRequest request, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierRiskNotFoundException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierRiskValidationException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierRiskConflictException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementSupplierRiskAuthorizationException(string message) :
    Exception(message);
