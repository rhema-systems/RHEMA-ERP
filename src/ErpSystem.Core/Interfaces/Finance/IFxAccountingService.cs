using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFxAccountingService
{
    Task<IReadOnlyList<FxRealizedSettlement>> PostRealizedFxForApPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FxRealizedSettlement>> PostRealizedFxForArReceiptAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FxRealizedSettlement>> PostRealizedFxForArReceiptAsync(
        Guid customerPaymentId,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default);

    Task<FxRevaluationBatch> RunUnrealizedRevaluationAsync(
        RevaluationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FxRevaluationBatch> ReverseRevaluationBatchAsync(
        Guid batchId,
        DateTime reversalDate,
        string reason,
        CancellationToken cancellationToken = default);
}
