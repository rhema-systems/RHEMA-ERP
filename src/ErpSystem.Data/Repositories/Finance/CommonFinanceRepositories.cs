using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Finance;

public class PaymentTermRepository : GenericRepository<PaymentTerm>, IPaymentTermRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public PaymentTermRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    private Guid TenantId => _currentUserProvider.TenantId;

    public async Task<PaymentTerm?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Where(pt => pt.Code == code && pt.TenantId == TenantId && !pt.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PaymentTerm>> GetActiveAsync()
    {
        return await _dbSet
            .Where(pt => pt.IsActive && pt.TenantId == TenantId && !pt.IsDeleted)
            .OrderBy(pt => pt.DisplayOrder)
            .ThenBy(pt => pt.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<PaymentTerm>> GetByApplicableToAsync(string applicableTo)
    {
        var normalizedApplicableTo = string.IsNullOrWhiteSpace(applicableTo)
            ? "All"
            : applicableTo.Trim();
        var applicableAliases = normalizedApplicableTo.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
            ? new[] { "Supplier", "Vendor", "All" }
            : normalizedApplicableTo.Equals("Customer", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Customer", "Client", "All" }
                : new[] { normalizedApplicableTo, "All" };
        var normalizedAliases = applicableAliases.Select(alias => alias.ToUpper()).ToArray();

        return await _dbSet
            .Where(pt => normalizedAliases.Contains(pt.ApplicableTo.ToUpper()) && pt.IsActive && pt.TenantId == TenantId && !pt.IsDeleted)
            .OrderBy(pt => pt.DisplayOrder)
            .ThenBy(pt => pt.Name)
            .ToListAsync();
    }

    public async Task<PaymentTerm?> GetDefaultAsync(string? applicableTo = null)
    {
        var query = _dbSet.Where(pt => pt.IsDefault && pt.IsActive && pt.TenantId == TenantId && !pt.IsDeleted);
        
        if (!string.IsNullOrEmpty(applicableTo))
        {
            query = query.Where(pt => pt.ApplicableTo == applicableTo || pt.ApplicableTo == "All");
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        var query = _dbSet.Where(pt => pt.Code == code && pt.TenantId == TenantId && !pt.IsDeleted);
        
        if (excludeId.HasValue)
        {
            query = query.Where(pt => pt.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public override async Task<PaymentTerm?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted);
    }

    public override async Task<IEnumerable<PaymentTerm>> GetAllAsync()
    {
        return await _dbSet.Where(e => e.TenantId == TenantId && !e.IsDeleted).ToListAsync();
    }
}

public class CurrencyRepository : GenericRepository<Currency>, ICurrencyRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public CurrencyRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    private Guid TenantId => _currentUserProvider.TenantId;

    public async Task<Currency?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Where(c => c.Code == code && c.TenantId == TenantId && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Currency>> GetActiveAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive && c.TenantId == TenantId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CurrencyName)
            .ToListAsync();
    }

    public async Task<Currency?> GetBaseCurrencyAsync()
    {
        return await _dbSet
            .Where(c => c.IsBaseCurrency && c.IsActive && c.TenantId == TenantId && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        var query = _dbSet.Where(c => c.Code == code && c.TenantId == TenantId && !c.IsDeleted);
        
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public override async Task<Currency?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted);
    }

    public override async Task<IEnumerable<Currency>> GetAllAsync()
    {
        return await _dbSet.Where(e => e.TenantId == TenantId && !e.IsDeleted).ToListAsync();
    }
}
