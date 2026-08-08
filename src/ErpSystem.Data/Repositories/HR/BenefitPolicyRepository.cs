using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// EF Core repository implementation for <see cref="BenefitPolicy"/>.
/// Data access only; tenant scoping is enforced by the DbContext query filters when available.
/// </summary>
public class BenefitPolicyRepository : GenericRepository<BenefitPolicy>, IBenefitPolicyRepository
{
    public BenefitPolicyRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<BenefitPolicy?> GetByIdWithRelationsAsync(Guid id)
    {
        return await _context.Set<BenefitPolicy>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.BenefitPolicyRelations)
            .Include(p => p.PositionBenefits)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicy>> GetAllActiveAsync()
    {
        return await _context.Set<BenefitPolicy>()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicy>> GetByTypeAsync(BenefitPolicyType policyType)
    {
        return await _context.Set<BenefitPolicy>()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.PolicyType == policyType)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> PolicyCodeExistsAsync(string policyCode, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(policyCode))
        {
            return false;
        }

        var normalizedUpper = policyCode.Trim().ToUpperInvariant();

        var query = _context.Set<BenefitPolicy>()
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Where(p => (p.PolicyCode ?? string.Empty).Trim().ToUpperInvariant() == normalizedUpper);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BenefitPolicy>> GetEffectivePoliciesAsync(DateTime asOfDate)
    {
        return await _context.Set<BenefitPolicy>()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Where(p => p.EffectiveFrom <= asOfDate)
            .Where(p => p.EffectiveTo == null || p.EffectiveTo >= asOfDate)
            .OrderBy(p => p.PolicyType)
            .ThenBy(p => p.PolicyName)
            .ToListAsync();
    }
}
