using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 6: POLICY & VENDOR
// ============================================================================

#region Staff Travel Policy Repository

public class StaffTravelPolicyRepository : GenericRepository<StaffTravelPolicy>, IStaffTravelPolicyRepository
{
    public StaffTravelPolicyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelPolicy?> GetWithRulesAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Rules)
            .Include(p => p.AppliesToLevelFrom)
            .Include(p => p.AppliesToLevelTo)
            .Include(p => p.AppliesToOrganizationUnit)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelPolicy>> GetCurrentVersionsAsync()
    {
        return await _dbSet
            .Where(p => p.IsCurrentVersion && !p.IsDeleted)
            .OrderBy(p => p.PolicyName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicy>> GetApplicablePoliciesAsync(Guid? staffLevelId, Guid? organizationUnitId, DateOnly onDate)
    {
        var candidates = await _dbSet
            .Where(p => p.IsCurrentVersion && !p.IsDeleted
                     && p.EffectiveFrom <= onDate
                     && (p.EffectiveTo == null || p.EffectiveTo >= onDate)
                     && (p.AppliesToOrganizationUnitId == null || p.AppliesToOrganizationUnitId == organizationUnitId))
            .ToListAsync();

        // Most-specific first (policies scoped to a unit / level rank above org-wide defaults).
        return candidates
            .OrderByDescending(p => p.AppliesToOrganizationUnitId != null ? 1 : 0)
            .ThenByDescending(p => p.AppliesToLevelFromId != null ? 1 : 0)
            .ThenByDescending(p => p.EffectiveFrom)
            .ToList();
    }
}

#endregion

#region Staff Travel Policy Rule Repository

public class StaffTravelPolicyRuleRepository : GenericRepository<StaffTravelPolicyRule>, IStaffTravelPolicyRuleRepository
{
    public StaffTravelPolicyRuleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelPolicyRule>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Where(r => r.PolicyId == policyId && !r.IsDeleted)
            .OrderBy(r => r.RuleCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicyRule>> GetActiveRulesAsync(Guid policyId)
    {
        return await _dbSet
            .Where(r => r.PolicyId == policyId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.RuleCode)
            .ToListAsync();
    }

    public async Task<StaffTravelPolicyRule?> GetByRuleCodeAsync(Guid policyId, string ruleCode)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.PolicyId == policyId && r.RuleCode == ruleCode && !r.IsDeleted);
    }
}

#endregion

#region Staff Travel Policy Exception Repository

public class StaffTravelPolicyExceptionRepository : GenericRepository<StaffTravelPolicyException>, IStaffTravelPolicyExceptionRepository
{
    public StaffTravelPolicyExceptionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelPolicyException>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(e => e.PolicyRule)
            .Include(e => e.ApprovedBy)
            .Where(e => e.StaffTravelRequestId == requestId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicyException>> GetByRuleIdAsync(Guid policyRuleId)
    {
        return await _dbSet
            .Where(e => e.PolicyRuleId == policyRuleId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelPolicyException>> GetPendingAsync()
    {
        return await _dbSet
            .Include(e => e.PolicyRule)
            .Where(e => e.Status == TravelPolicyExceptionStatus.Pending && !e.IsDeleted)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Vendor Repository

public class StaffTravelVendorRepository : GenericRepository<StaffTravelVendor>, IStaffTravelVendorRepository
{
    public StaffTravelVendorRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelVendor?> GetByVendorCodeAsync(string vendorCode)
    {
        return await _dbSet
            .Include(v => v.Country)
            .FirstOrDefaultAsync(v => v.VendorCode == vendorCode && !v.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelVendor>> GetByTypeAsync(TravelVendorType vendorType)
    {
        return await _dbSet
            .Where(v => v.VendorType == vendorType && !v.IsDeleted)
            .OrderBy(v => v.VendorName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelVendor>> GetPreferredVendorsAsync(TravelVendorType? vendorType = null)
    {
        return await _dbSet
            .Where(v => v.IsPreferred && v.IsActive && !v.IsDeleted
                     && (vendorType == null || v.VendorType == vendorType))
            .OrderBy(v => v.VendorName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelVendor>> GetActiveVendorsAsync()
    {
        return await _dbSet
            .Where(v => v.IsActive && !v.IsDeleted)
            .OrderBy(v => v.VendorName)
            .ToListAsync();
    }
}

#endregion
