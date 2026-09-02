using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned adapter for approved external source transactions. Producer modules consume this
/// interface but cannot supply or impersonate Finance's trusted route metadata.
/// </summary>
public interface IExternalFinancePostingAdapter
{
    Task<FinanceSourceDocumentDimensionDto> ValidateDimensionsAsync(
        FinanceExternalPostingEnvelopeDto envelope,
        CancellationToken cancellationToken = default);

    Task<FinancePostingResultDto> PostAsync(
        FinanceExternalPostingEnvelopeDto envelope,
        CancellationToken cancellationToken = default);
}
