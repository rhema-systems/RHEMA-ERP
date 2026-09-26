using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    public interface IAccountingBookService
    {
        Task<IReadOnlyList<AccountingBookDto>> GetBooksAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> GetBookAsync(Guid id, CancellationToken cancellationToken = default);

        Task<DeltaBookCombinedReportDto> GetDeltaCombinedReportAsync(Guid id, DateTime asOfDate, CancellationToken cancellationToken = default);

        Task<DeltaBookCombinedReportDto> GetDeltaCombinedReportAsync(IReadOnlyCollection<Guid> ids, DateTime asOfDate, CancellationToken cancellationToken = default);

        Task<DeltaBookLedgerInquiryDto> GetDeltaLedgerAsync(Guid id, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> CreateAsync(CreateAccountingBookDto request, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> UpdateAsync(Guid id, UpdateAccountingBookDto request, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> RequestTransitionAsync(Guid id, RequestAccountingBookTransitionDto request, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> ApproveTransitionAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> RejectTransitionAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default);

        Task<AccountingBookDto> RequestPrimaryReplacementAsync(Guid id, RequestPrimaryAccountingBookReplacementDto request, CancellationToken cancellationToken = default);
        Task<AccountingBookDto> ApprovePrimaryReplacementAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default);
        Task<AccountingBookDto> RejectPrimaryReplacementAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default);
        Task<AccountingBookDto> RequestPrimaryReplacementReversalAsync(Guid id, RequestPrimaryAccountingBookReversalDto request, CancellationToken cancellationToken = default);
        Task<AccountingBookDto> ApprovePrimaryReplacementReversalAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default);
        Task<AccountingBookDto> RejectPrimaryReplacementReversalAsync(Guid id, DecideAccountingBookTransitionDto request, CancellationToken cancellationToken = default);

        Task EnsureTenantDefaultsAsync(CancellationToken cancellationToken = default);

        Task SyncAccountMappingsAsync(Account account, IReadOnlyCollection<AccountAccountingBookUpdateDto> requestedMappings, CancellationToken cancellationToken = default);
    }
}
