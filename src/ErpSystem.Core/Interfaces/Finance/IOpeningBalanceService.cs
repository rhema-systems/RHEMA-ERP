using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IOpeningBalanceService
{
    Task<OpeningBalanceBatchDto> CreateBatchAsync(
        CreateOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> CreateFixedAssetBatchAsync(
        CreateFixedAssetOpeningBalanceBatchDto dto,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> CreateBankAccountOpeningBatchAsync(
        CreateBankAccountOpeningBalanceDto dto,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> CreateResidualGlEquityOpeningBatchAsync(
        CreateResidualGlEquityOpeningBalanceDto dto,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> CreateSupplierAdvanceBatchAsync(CreateSupplierAdvanceOpeningBalanceDto dto, CancellationToken cancellationToken = default);
    Task<OpeningBalanceBatchDto> CreateCustomerAdvanceBatchAsync(CreateCustomerAdvanceOpeningBalanceDto dto, CancellationToken cancellationToken = default);
    Task<OpeningBalanceBatchDto> CreateApWithholdingBatchAsync(CreateApWithholdingOpeningBalanceDto dto, CancellationToken cancellationToken = default);
    Task<OpeningBalanceBatchDto> CreateArWithholdingBatchAsync(CreateArWithholdingOpeningBalanceDto dto, CancellationToken cancellationToken = default);
    Task<SpecializedOpeningBalanceOptionsDto> GetSpecializedOptionsAsync(CancellationToken cancellationToken = default);
    Task<GovernedOpeningBalanceOptionsDto> GetGovernedOptionsAsync(
        GovernedOpeningBalanceOptionsRequestDto dto,
        CancellationToken cancellationToken = default);

    Task<SubledgerOpeningBalanceReadinessDto> GetSubledgerReadinessAsync(
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto?> GetBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpeningBalanceBatchDto>> GetBatchesAsync(
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceBatchDto> UpdateBatchAsync(
        Guid batchId,
        UpdateOpeningBalanceBatchDto dto,
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

    Task<OpeningBalanceBatchReversalDto> RequestReversalAsync(Guid batchId, RequestOpeningBalanceBatchReversalDto dto, CancellationToken cancellationToken = default);
    Task<OpeningBalanceBatchReversalDto> ReviewReversalAsync(Guid batchId, Guid requestId, ReviewOpeningBalanceBatchReversalDto dto, CancellationToken cancellationToken = default);
    Task<OpeningBalanceBatchReversalDto> PostReversalAsync(Guid batchId, Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpeningBalanceBatchReversalDto>> GetReversalsAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpeningBalanceDiagnosticDto>> GetDiagnosticsAsync(
        CancellationToken cancellationToken = default);
}
