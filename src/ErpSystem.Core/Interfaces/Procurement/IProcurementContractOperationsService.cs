using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementContractOperationsService
{
    Task<ProcurementContractOperationsPortfolioDto> SearchAsync(
        ProcurementContractOperationsSearchRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcurementContractOperationsDetailDto> GetAsync(
        Guid contractId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ProcessProcurementContractOperationsAlertsResult> ProcessAlertsAsync(
        ProcessProcurementContractOperationsAlertsRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementContractOperationsNotFoundException(
    string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementContractOperationsAuthorizationException(
    string message) : UnauthorizedAccessException(message);

public sealed class ProcurementContractOperationsValidationException(
    string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
