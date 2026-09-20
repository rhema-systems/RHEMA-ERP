using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Finance;

/// <summary>A Finance account as the adapter needs to see it: identity, type and whether it is live.</summary>
public sealed record HrFinanceAccountSnapshot(
    Guid Id,
    string AccountCode,
    string AccountNumber,
    string AccountName,
    AccountType AccountType,
    bool IsActive);

/// <summary>Finance's tenant-level facts an HR posting depends on.</summary>
/// <param name="FunctionalCurrencyCode">Finance's base currency; every HR journal is stated in it.</param>
/// <param name="AccountingBookCode">The one concrete book HR posts to, or null when it cannot be resolved.</param>
/// <param name="AccountingBookProblem">Why the book could not be resolved, when it could not.</param>
public sealed record HrFinanceTenantContext(
    string FunctionalCurrencyCode,
    string? AccountingBookCode,
    string? AccountingBookProblem);

/// <summary>
/// The adapter's data access, split out so the adapter's contract tests can drive it with mocks
/// and never need an EF provider. Reads Finance master data read-only; writes only HR's own tables.
/// </summary>
public interface IHrFinancePostingStore
{
    Task<HrFinanceTenantContext> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HrFinanceAccountMapping>> GetMappingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, HrFinanceAccountSnapshot>> GetAccountsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);
    Task<HrFinancePostingRule?> GetRuleAsync(Guid tenantId, string eventCode, CancellationToken cancellationToken = default);
    Task<HrFinancePostingRecord?> FindRecordAsync(Guid tenantId, string eventCode, Guid sourceDocumentId, CancellationToken cancellationToken = default);
    Task<HrFinancePostingRecord?> FindPostedRecordAsync(Guid tenantId, string sourceDocumentType, Guid sourceDocumentId, CancellationToken cancellationToken = default);
    Task AddRecordAsync(HrFinancePostingRecord record, CancellationToken cancellationToken = default);
    Task UpdateRecordAsync(HrFinancePostingRecord record, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IHrFinancePostingStore"/> over the shared unit of work. Every read is tenant-scoped
/// explicitly: the DbContext is registered without a tenant, so its filters are inert (RHEMA convention).
/// </summary>
public sealed class HrFinancePostingStore : IHrFinancePostingStore
{
    private readonly IUnitOfWork _unitOfWork;

    public HrFinancePostingStore(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<HrFinanceTenantContext> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await _unitOfWork.Repository<FinanceSettings>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var currency = settings?.BaseCurrency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency)) currency = "GHS";

        // Finance's own resolver: one concrete book, never a multi-book selector. Procurement's
        // tender-fee adapter resolves the same way, so HR and Procurement cannot disagree about
        // which book an external producer lands in.
        try
        {
            var book = FinanceAccountingBookCodeResolver.ResolveLegacySingleBook(
                settings?.SubledgerPostingMode, settings is not null);
            return new HrFinanceTenantContext(currency, book, null);
        }
        catch (ArgumentException ex)
        {
            return new HrFinanceTenantContext(currency, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<HrFinanceAccountMapping>> GetMappingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _unitOfWork.Repository<HrFinanceAccountMapping>()
            .GetQueryable(m => m.TenantId == tenantId && !m.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, HrFinanceAccountSnapshot>> GetAccountsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new Dictionary<Guid, HrFinanceAccountSnapshot>();
        var ids = accountIds.Distinct().ToList();
        var accounts = await _unitOfWork.Repository<Account>()
            .GetQueryable(a => a.TenantId == tenantId && ids.Contains(a.Id) && !a.IsDeleted)
            .AsNoTracking()
            .Select(a => new HrFinanceAccountSnapshot(
                a.Id, a.AccountCode, a.AccountNumber, a.AccountName, a.AccountType, a.Status == AccountStatus.Active))
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(a => a.Id);
    }

    public async Task<HrFinancePostingRule?> GetRuleAsync(Guid tenantId, string eventCode, CancellationToken cancellationToken = default)
        => await _unitOfWork.Repository<HrFinancePostingRule>()
            .GetQueryable(r => r.TenantId == tenantId && r.EventCode == eventCode && !r.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<HrFinancePostingRecord?> FindRecordAsync(Guid tenantId, string eventCode, Guid sourceDocumentId, CancellationToken cancellationToken = default)
        => await _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && r.EventCode == eventCode
                && r.SourceDocumentId == sourceDocumentId && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<HrFinancePostingRecord?> FindPostedRecordAsync(Guid tenantId, string sourceDocumentType, Guid sourceDocumentId, CancellationToken cancellationToken = default)
        => await _unitOfWork.Repository<HrFinancePostingRecord>()
            .GetQueryable(r => r.TenantId == tenantId && r.SourceDocumentType == sourceDocumentType
                && r.SourceDocumentId == sourceDocumentId && r.Status == HrFinancePostingStatus.Posted && !r.IsDeleted)
            .AsNoTracking()
            .OrderByDescending(r => r.PostedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddRecordAsync(HrFinancePostingRecord record, CancellationToken cancellationToken = default)
        => _unitOfWork.Repository<HrFinancePostingRecord>().AddAsync(record);

    public Task UpdateRecordAsync(HrFinancePostingRecord record, CancellationToken cancellationToken = default)
        => _unitOfWork.Repository<HrFinancePostingRecord>().UpdateAsync(record);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.SaveChangesAsync(cancellationToken);
}
