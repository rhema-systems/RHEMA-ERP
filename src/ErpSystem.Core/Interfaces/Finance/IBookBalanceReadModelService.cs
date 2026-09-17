using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IBookBalanceReadModelService
{
    Task ApplyPostingAsync(Guid tenantId, Guid accountingBookId, string accountingBookCode,
        Guid fiscalPeriodId, string functionalCurrencyCode, IReadOnlyCollection<AccountTransaction> lines,
        DateTime postedAt, Guid? actorId,
        CancellationToken cancellationToken = default);

    Task<BookBalanceInquiryDto> GetAsync(Guid tenantId, Guid accountId, string accountingBookCode,
        Guid? fiscalPeriodId = null, CancellationToken cancellationToken = default);

    Task<BookBalanceReconciliationDto> ReconcileAsync(Guid tenantId,
        BookBalanceReconciliationRequestDto request, Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
