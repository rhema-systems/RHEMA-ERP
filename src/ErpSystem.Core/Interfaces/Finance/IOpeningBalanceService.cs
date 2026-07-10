using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IOpeningBalanceService
{
    Task<OpeningBalanceBatchDto> CreateBatchAsync(
        CreateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto?> GetBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceValidationResultDto> ValidateBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> SubmitForApprovalAsync(
        Guid batchId,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> PostAsync(
        Guid batchId,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpeningBalanceDiagnosticDto>> GetDiagnosticsAsync(
        CancellationToken cancellationToken = default);
}
