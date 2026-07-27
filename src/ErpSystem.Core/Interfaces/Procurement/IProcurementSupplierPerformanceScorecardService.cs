using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierPerformanceScorecardService
{
    Task<ProcurementSupplierPerformanceSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default);
    Task<ProcurementSupplierPerformancePageDto> SearchAsync(
        ProcurementSupplierPerformanceSearchRequest request,
        CancellationToken cancellationToken = default);
    Task<ProcurementSupplierPerformanceScorecardDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<ProcurementSupplierPerformanceCurrentStateDto> GetCurrentStateAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementSupplierPerformanceSupplierOptionDto>> GetSupplierOptionsAsync(
        CancellationToken cancellationToken = default);
    Task<ProcurementSupplierPerformanceScorecardDto> CalculateAsync(
        CalculateProcurementSupplierPerformanceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementSupplierPerformanceNotFoundException :
    InvalidOperationException
{
    public ProcurementSupplierPerformanceNotFoundException(string code, string message) :
        base(message) => Code = code;
    public string Code { get; }
}

public sealed class ProcurementSupplierPerformanceValidationException :
    InvalidOperationException
{
    public ProcurementSupplierPerformanceValidationException(string code, string message) :
        base(message) => Code = code;
    public string Code { get; }
}

public sealed class ProcurementSupplierPerformanceConflictException :
    InvalidOperationException
{
    public ProcurementSupplierPerformanceConflictException(string code, string message) :
        base(message) => Code = code;
    public string Code { get; }
}

public sealed class ProcurementSupplierPerformanceAuthorizationException :
    UnauthorizedAccessException
{
    public ProcurementSupplierPerformanceAuthorizationException(string message) : base(message) { }
}
