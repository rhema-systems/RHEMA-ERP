using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Repositories.Procurement;

// ============================================================================
// BUSINESS PARTNER REPOSITORY
// ============================================================================

public class BusinessPartnerRepository : GenericRepository<BusinessPartner>, IBusinessPartnerRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<BusinessPartnerRepository> _logger;

    public BusinessPartnerRepository(
        ApplicationDbContext context,
        ICurrentUserProvider currentUserProvider,
        ILogger<BusinessPartnerRepository> logger) : base(context)
    {
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public override async Task<BusinessPartner?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            query = query.Where(bp =>
                bp.TenantId == tenantId &&
                (bp.UserId == userId || _context.BusinessPartnerUsers
                    .IgnoreQueryFilters()
                    .Any(link =>
                        link.TenantId == tenantId &&
                        link.BusinessPartnerId == bp.Id &&
                        link.UserId == userId &&
                        link.IsActive &&
                        !link.IsDeleted)));
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetByCodeAsync(string partnerCode)
    {
        return await _dbSet
            .Where(bp => bp.PartnerCode == partnerCode && !bp.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetByUserIdAsync(Guid userId)
    {
        if (userId == Guid.Empty || _currentUserProvider.TenantId == Guid.Empty)
        {
            return null;
        }

        // External access supports both the legacy primary-user link and the governed
        // multi-user relationship. IgnoreQueryFilters is retained because external
        // identities are resolved before some normal tenant-scoped repository calls,
        // but the tenant boundary is restored explicitly on both sides of the join.
        var tenantId = _currentUserProvider.TenantId;
        return await _dbSet
            .IgnoreQueryFilters()
            .Include(bp => bp.User)
            .Include(bp => bp.BankAccounts)
            .Where(bp =>
                bp.TenantId == tenantId &&
                !bp.IsDeleted &&
                (bp.UserId == userId || _context.BusinessPartnerUsers
                    .IgnoreQueryFilters()
                    .Any(link =>
                        link.TenantId == tenantId &&
                        link.BusinessPartnerId == bp.Id &&
                        link.UserId == userId &&
                        link.IsActive &&
                        !link.IsDeleted)))
            .OrderByDescending(bp => bp.UserId == userId)
            .ThenBy(bp => bp.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner> CreateAsync(BusinessPartner partner)
    {
        var created = await AddAsync(partner);
        await _context.SaveChangesAsync();
        return created;
    }

    public new async Task<BusinessPartner> UpdateAsync(BusinessPartner partner)
    {
        await base.UpdateAsync(partner);
        await _context.SaveChangesAsync();
        return partner;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var partner = await GetByIdAsync(id);
        if (partner != null)
        {
            await base.DeleteAsync(partner);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<BusinessPartner>> GetPartnersAsync(
        int page,
        int pageSize,
        string? search = null,
        string? partnerType = null,
        string? status = null,
        string? approvalStatus = null,
        bool? isPreferred = null,
        bool? isBlacklisted = null,
        List<Guid>? categoryIds = null,
        List<Guid>? specializationIds = null)
    {
        var query = _dbSet.Where(bp => !bp.IsDeleted);

        // External users can only see their own business partner
        var isExternalUser = _currentUserProvider.IsExternalUser;
        var currentUserId = _currentUserProvider.UserId;
        var authProvider = _currentUserProvider.AuthenticationProvider;

        _logger.LogInformation("GetPartnersAsync - IsExternalUser: {IsExternalUser}, UserId: {UserId}, AuthProvider: {AuthProvider}",
            isExternalUser, currentUserId, authProvider);

        if (isExternalUser)
        {
            _logger.LogInformation("Filtering business partners for external user {UserId}", currentUserId);
            query = query.Where(bp => bp.UserId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Keep the predicate server-translatable: the StringComparison
            // overload of Contains is not supported by the SQL Server provider.
            var searchLower = search.Trim().ToLowerInvariant();
            query = query.Where(bp =>
                bp.PartnerName.ToLower().Contains(searchLower) ||
                bp.PartnerCode.ToLower().Contains(searchLower) ||
                (bp.PrimaryEmail != null && bp.PrimaryEmail.ToLower().Contains(searchLower)) ||
                (bp.BusinessRegistrationNumber != null && bp.BusinessRegistrationNumber.ToLower().Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            query = query.Where(bp => bp.PartnerType == partnerType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(bp => bp.RegistrationStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(approvalStatus))
        {
            query = query.Where(bp => bp.ApprovalStatus == approvalStatus);
        }

        if (isPreferred.HasValue)
        {
            query = query.Where(bp => bp.IsPreferred == isPreferred.Value);
        }

        if (isBlacklisted.HasValue)
        {
            query = query.Where(bp => bp.IsBlacklisted == isBlacklisted.Value);
        }

        if (categoryIds != null && categoryIds.Any())
        {
            query = query.Where(bp => bp.Categories.Any(c => categoryIds.Contains(c.CategoryId)));
        }

        if (specializationIds != null && specializationIds.Any())
        {
            query = query.Where(bp => bp.Specializations.Any(s => specializationIds.Contains(s.SpecializationId)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(bp => bp.PartnerName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<BusinessPartner>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<BusinessPartner>> GetActivePartnersAsync(string? partnerType = null)
    {
        // "Active partners" are those that are usable in day-to-day transactions (e.g. PO creation):
        // - Operational status must be Active (or legacy Approved)
        // - Approval must be Approved
        // - Not blacklisted
        var query = _dbSet.Where(bp =>
            !bp.IsDeleted &&
            bp.IsActive &&
            !bp.IsBlacklisted &&
            bp.ApprovalStatus == BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus &&
            (bp.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
             bp.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus));
        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            query = query.Where(bp => bp.PartnerType == partnerType);
        }

        return await query.OrderBy(bp => bp.PartnerName).ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetPreferredPartnersAsync(string? partnerType = null)
    {
        var query = _dbSet.Where(bp =>
            bp.IsPreferred &&
            !bp.IsDeleted &&
            bp.IsActive &&
            !bp.IsBlacklisted &&
            bp.ApprovalStatus == BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus &&
            (bp.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
             bp.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus));
        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            query = query.Where(bp => bp.PartnerType == partnerType);
        }

        return await query.OrderBy(bp => bp.PartnerName).ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetBlacklistedPartnersAsync()
    {
        return await _dbSet
            .Where(bp => bp.IsBlacklisted && !bp.IsDeleted)
            .OrderBy(bp => bp.PartnerName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetPartnersByCategoryAsync(Guid categoryId)
    {
        return await _dbSet
            .Where(bp => bp.Categories.Any(c => c.CategoryId == categoryId) && !bp.IsDeleted)
            .OrderBy(bp => bp.PartnerName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetPartnersBySpecializationAsync(Guid specializationId)
    {
        return await _dbSet
            .Where(bp => bp.Specializations.Any(s => s.SpecializationId == specializationId) && !bp.IsDeleted)
            .OrderBy(bp => bp.PartnerName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetPartnersRequiringApprovalAsync()
    {
        return await _dbSet
            .Where(bp => bp.ApprovalStatus == "Pending" && !bp.IsDeleted)
            .OrderBy(bp => bp.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetPartnersWithExpiringLicensesAsync(int daysAhead = 30)
    {
        var expiryDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(bp => bp.Licenses.Any(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value <= expiryDate && l.Status == "Active") && !bp.IsDeleted)
            .Include(bp => bp.Licenses)
            .OrderBy(bp => bp.PartnerName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartner>> GetPartnersWithExpiringBlacklistAsync(int daysAhead = 30)
    {
        var expiryDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(bp => bp.IsBlacklisted && bp.BlacklistExpiryDate.HasValue && bp.BlacklistExpiryDate.Value <= expiryDate && !bp.IsDeleted)
            .OrderBy(bp => bp.BlacklistExpiryDate)
            .ToListAsync();
    }

    public async Task<bool> IsPartnerCodeUniqueAsync(string partnerCode, Guid? excludeId = null)
    {
        var query = _dbSet.Where(bp => bp.PartnerCode == partnerCode && !bp.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(bp => bp.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> HasActiveContractsAsync(Guid partnerId)
    {
        // TODO: Implement when Contract module is available
        return await Task.FromResult(false);
    }

    public async Task<bool> HasActivePurchaseOrdersAsync(Guid partnerId)
    {
        return await _context.PurchaseOrders
            .AnyAsync(po => po.BusinessPartnerId == partnerId && po.Status != "Cancelled" && po.Status != "Completed" && !po.IsDeleted);
    }

    public async Task UpdateStatusAsync(Guid partnerId, string status)
    {
        var partner = await GetByIdAsync(partnerId);
        if (partner != null)
        {
            partner.RegistrationStatus = status;
            // Keep the legacy boolean aligned with the operational status to avoid inconsistencies.
            if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                partner.IsActive = true;
            }
            else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                partner.IsActive = false;
            }
            partner.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateApprovalStatusAsync(Guid partnerId, string approvalStatus, Guid approvedById)
    {
        var partner = await GetByIdAsync(partnerId);
        if (partner != null)
        {
            partner.ApprovalStatus = approvalStatus;
            partner.ApprovedById = approvedById;
            partner.ApprovedDate = DateTime.UtcNow;
            if (BusinessPartnerLifecyclePolicy.IsApproved(approvalStatus))
            {
                // Once approved, ensure the partner is operationally usable unless explicitly deactivated later.
                if (partner.RegistrationStatus == "PendingApproval" || string.IsNullOrWhiteSpace(partner.RegistrationStatus))
                {
                    partner.RegistrationStatus =
                        BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus;
                }

                partner.IsActive =
                    BusinessPartnerLifecyclePolicy.IsOperationalRegistration(
                        partner.RegistrationStatus);
            }
            partner.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdatePerformanceRatingAsync(Guid partnerId, decimal rating)
    {
        var partner = await GetByIdAsync(partnerId);
        if (partner != null)
        {
            partner.PerformanceRating = rating;
            partner.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddToBlacklistAsync(Guid partnerId, string reason, DateTime? expiryDate = null)
    {
        var partner = await GetByIdAsync(partnerId);
        if (partner != null)
        {
            partner.IsBlacklisted = true;
            partner.BlacklistReason = reason;
            partner.BlacklistDate = DateTime.UtcNow;
            partner.BlacklistExpiryDate = expiryDate;
            partner.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveFromBlacklistAsync(Guid partnerId)
    {
        var partner = await GetByIdAsync(partnerId);
        if (partner != null)
        {
            partner.IsBlacklisted = false;
            partner.BlacklistReason = null;
            partner.BlacklistDate = null;
            partner.BlacklistExpiryDate = null;
            partner.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<string> GeneratePartnerCodeAsync(string partnerType)
    {
        // Determine prefix based on partner type
        var prefix = partnerType switch
        {
            "Contractor" => "CON",
            "Customer" => "CUS",
            "Both" => "BTH",
            _ => "SUP" // Default to Supplier
        };
        var year = DateTime.UtcNow.Year.ToString().Substring(2);

        var lastPartner = await _dbSet
            .Where(bp => bp.PartnerCode.StartsWith(prefix + year) && !bp.IsDeleted)
            .OrderByDescending(bp => bp.PartnerCode)
            .FirstOrDefaultAsync();

        int nextSequence = 1;
        if (lastPartner != null)
        {
            var lastSequence = lastPartner.PartnerCode.Substring(5);
            if (int.TryParse(lastSequence, out int seq))
            {
                nextSequence = seq + 1;
            }
        }

        return $"{prefix}{year}{nextSequence:D4}";
    }

    public async Task<BusinessPartner?> GetWithCategoriesAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(bp => bp.UserId == _currentUserProvider.UserId);
        }

        return await query
            .Include(bp => bp.Categories)
                .ThenInclude(c => c.Category)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithSpecializationsAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(bp => bp.UserId == _currentUserProvider.UserId);
        }

        return await query
            .Include(bp => bp.Specializations)
                .ThenInclude(s => s.Specialization)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithLicensesAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(bp => bp.UserId == _currentUserProvider.UserId);
        }

        return await query
            .Include(bp => bp.Licenses)
                .ThenInclude(l => l.LicenseType)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithContactsAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(bp => bp.UserId == _currentUserProvider.UserId);
        }

        return await query
            .Include(bp => bp.Contacts)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithDocumentsAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(bp => bp.UserId == _currentUserProvider.UserId);
        }

        return await query
            .Include(bp => bp.Documents)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithFinancialsAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(bp => bp.UserId == _currentUserProvider.UserId);
        }

        return await query
            .Include(bp => bp.Financials)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithBankAccountsAsync(Guid id)
    {
        var accessiblePartner = await GetByIdAsync(id);
        if (accessiblePartner == null)
        {
            return null;
        }

        return await _dbSet
            .Where(bp => bp.Id == accessiblePartner.Id && !bp.IsDeleted)
            .Include(bp => bp.BankAccounts)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartner?> GetWithAllRelatedDataAsync(Guid id)
    {
        var query = _dbSet.Where(bp => bp.Id == id && !bp.IsDeleted);

        // External users can only see their own business partner
        if (_currentUserProvider.IsExternalUser)
        {
            var tenantId = _currentUserProvider.TenantId;
            var userId = _currentUserProvider.UserId;
            query = query.Where(bp =>
                bp.TenantId == tenantId &&
                (bp.UserId == userId || _context.BusinessPartnerUsers
                    .IgnoreQueryFilters()
                    .Any(link =>
                        link.TenantId == tenantId &&
                        link.BusinessPartnerId == bp.Id &&
                        link.UserId == userId &&
                        link.IsActive &&
                        !link.IsDeleted)));
        }

        return await query
            .Include(bp => bp.Categories)
                .ThenInclude(c => c.Category)
            .Include(bp => bp.Specializations)
                .ThenInclude(s => s.Specialization)
            .Include(bp => bp.Licenses)
                .ThenInclude(l => l.LicenseType)
            .Include(bp => bp.Contacts)
            .Include(bp => bp.BankAccounts)
            .Include(bp => bp.Documents)
            .Include(bp => bp.Financials)
            // Canonical role rows coexist with the legacy PartnerType projection while Procurement
            // consumers migrate. Finance profile screens must receive the real multi-role state.
            .Include(bp => bp.Roles)
            .Include(bp => bp.ApprovedBy)
            .FirstOrDefaultAsync();
    }
}

// ============================================================================
// PARTNER CATEGORY REPOSITORY
// ============================================================================

public class PartnerCategoryRepository : GenericRepository<PartnerCategory>, IPartnerCategoryRepository
{
    public PartnerCategoryRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<PartnerCategory?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<PartnerCategory?> GetByCodeAsync(string categoryCode)
    {
        return await _dbSet
            .Where(c => c.CategoryCode == categoryCode && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<PartnerCategory> CreateAsync(PartnerCategory category)
    {
        return await AddAsync(category);
    }

    public new async Task<PartnerCategory> UpdateAsync(PartnerCategory category)
    {
        await base.UpdateAsync(category);
        return category;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var category = await GetByIdAsync(id);
        if (category != null)
        {
            await DeleteAsync(category);
        }
    }

    public async Task<IEnumerable<PartnerCategory>> GetAllCategoriesAsync(string? categoryType = null)
    {
        var query = _dbSet.Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(categoryType))
        {
            query = query.Where(c => c.CategoryType == categoryType);
        }

        return await query
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<IEnumerable<PartnerCategory>> GetActiveCategoriesAsync(string? categoryType = null)
    {
        var query = _dbSet.Where(c => c.IsActive && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(categoryType))
        {
            query = query.Where(c => c.CategoryType == categoryType);
        }

        return await query
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<IEnumerable<PartnerCategory>> GetRootCategoriesAsync(string? categoryType = null)
    {
        var query = _dbSet.Where(c => c.ParentCategoryId == null && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(categoryType))
        {
            query = query.Where(c => c.CategoryType == categoryType);
        }

        return await query
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<IEnumerable<PartnerCategory>> GetSubCategoriesAsync(Guid parentCategoryId)
    {
        return await _dbSet
            .Where(c => c.ParentCategoryId == parentCategoryId && !c.IsDeleted)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
    }

    public async Task<PartnerCategory?> GetWithSubCategoriesAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCategoryCodeUniqueAsync(string categoryCode, Guid? excludeId = null)
    {
        var query = _dbSet.Where(c => c.CategoryCode == categoryCode && !c.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> HasSubCategoriesAsync(Guid categoryId)
    {
        return await _dbSet.AnyAsync(c => c.ParentCategoryId == categoryId && !c.IsDeleted);
    }

    public async Task<bool> HasPartnersAsync(Guid categoryId)
    {
        return await _context.BusinessPartnerCategories
            .AnyAsync(bpc => bpc.CategoryId == categoryId);
    }
}

// ============================================================================
// CONTRACTOR SPECIALIZATION REPOSITORY
// ============================================================================

public class ContractorSpecializationRepository : GenericRepository<ContractorSpecialization>, IContractorSpecializationRepository
{
    public ContractorSpecializationRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<ContractorSpecialization?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(s => s.Id == id && !s.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<ContractorSpecialization?> GetByCodeAsync(string specializationCode)
    {
        return await _dbSet
            .Where(s => s.SpecializationCode == specializationCode && !s.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<ContractorSpecialization> CreateAsync(ContractorSpecialization specialization)
    {
        return await AddAsync(specialization);
    }

    public new async Task<ContractorSpecialization> UpdateAsync(ContractorSpecialization specialization)
    {
        await base.UpdateAsync(specialization);
        return specialization;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var specialization = await GetByIdAsync(id);
        if (specialization != null)
        {
            await DeleteAsync(specialization);
        }
    }

    public async Task<IEnumerable<ContractorSpecialization>> GetAllSpecializationsAsync()
    {
        return await _dbSet
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.SpecializationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContractorSpecialization>> GetActiveSpecializationsAsync()
    {
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.SpecializationName)
            .ToListAsync();
    }

    public async Task<bool> IsSpecializationCodeUniqueAsync(string specializationCode, Guid? excludeId = null)
    {
        var query = _dbSet.Where(s => s.SpecializationCode == specializationCode && !s.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }

    public async Task<bool> HasContractorsAsync(Guid specializationId)
    {
        return await _context.BusinessPartnerSpecializations
            .AnyAsync(bps => bps.SpecializationId == specializationId);
    }
}
