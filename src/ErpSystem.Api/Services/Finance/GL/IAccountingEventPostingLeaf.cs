using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Internal-only authority used by the C6 aggregate transaction. Keeping this out of the public
/// posting interface prevents producers from turning the C1 parallel-book gate into a DTO flag.
/// </summary>
internal interface IAccountingEventPostingLeaf
{
    Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestV2Dto request,
        AccountingEventPostingAuthority authority,
        CancellationToken cancellationToken = default);

    Task<FinancePostingResultDto> ReverseAsync(
        Guid financePostingEventId,
        DateTime reversalDate,
        string reason,
        string idempotencyKey,
        AccountingEventPostingAuthority authority,
        CancellationToken cancellationToken = default);
}

internal readonly record struct AccountingEventPostingAuthority(
    Guid AccountingEventId,
    Guid AccountingBookSelectionEvidenceId,
    Guid AccountingBookId,
    string AuthorityFingerprint);
