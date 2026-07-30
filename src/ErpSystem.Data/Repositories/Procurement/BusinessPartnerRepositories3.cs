using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Repositories.Procurement;

// ============================================================================
// BUSINESS PARTNER DOCUMENT REPOSITORY
// ============================================================================

public class BusinessPartnerDocumentRepository : GenericRepository<BusinessPartnerDocument>, IBusinessPartnerDocumentRepository
{
    public BusinessPartnerDocumentRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<BusinessPartnerDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(d => d.Id == id && !d.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerDocument> CreateAsync(BusinessPartnerDocument document)
    {
        return await AddAsync(document);
    }

    public new async Task<BusinessPartnerDocument> UpdateAsync(BusinessPartnerDocument document)
    {
        await base.UpdateAsync(document);
        return document;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var document = await GetByIdAsync(id);
        if (document != null)
        {
            await DeleteAsync(document);
        }
    }

    public async Task<IEnumerable<BusinessPartnerDocument>> GetDocumentsByPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(d => d.BusinessPartnerId == businessPartnerId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerDocument>> GetDocumentsByTypeAsync(Guid businessPartnerId, string documentType)
    {
        return await _dbSet
            .Where(d => d.BusinessPartnerId == businessPartnerId && d.DocumentType == documentType && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerDocument>> GetUnverifiedDocumentsAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(d => d.BusinessPartnerId == businessPartnerId && !d.IsVerified && !d.IsDeleted)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerDocument>> GetExpiringDocumentsAsync(int daysAhead = 30)
    {
        var expiryDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value <= expiryDate && !d.IsDeleted)
            .Include(d => d.BusinessPartner)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerDocument>> GetExpiredDocumentsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value < now && !d.IsDeleted)
            .Include(d => d.BusinessPartner)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync();
    }

    public async Task VerifyDocumentAsync(Guid documentId, Guid verifiedById)
    {
        var document = await GetByIdAsync(documentId);
        if (document != null)
        {
            document.IsVerified = true;
            document.VerifiedById = verifiedById;
            document.VerifiedDate = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            // Note: SaveChangesAsync should be called by the service layer using Unit of Work
        }
    }
}

// ============================================================================
// BUSINESS PARTNER FINANCIAL REPOSITORY
// ============================================================================

public class BusinessPartnerFinancialRepository : GenericRepository<BusinessPartnerFinancial>, IBusinessPartnerFinancialRepository
{
    public BusinessPartnerFinancialRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<BusinessPartnerFinancial?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(f => f.Id == id && !f.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerFinancial> CreateAsync(BusinessPartnerFinancial financial)
    {
        return await AddAsync(financial);
    }

    public new async Task<BusinessPartnerFinancial> UpdateAsync(BusinessPartnerFinancial financial)
    {
        await base.UpdateAsync(financial);
        return financial;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var financial = await GetByIdAsync(id);
        if (financial != null)
        {
            await DeleteAsync(financial);
        }
    }

    public async Task<IEnumerable<BusinessPartnerFinancial>> GetFinancialsByPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(f => f.BusinessPartnerId == businessPartnerId && !f.IsDeleted)
            .OrderByDescending(f => f.FiscalYear)
            .ToListAsync();
    }

    public async Task<BusinessPartnerFinancial?> GetFinancialByYearAsync(Guid businessPartnerId, int financialYear)
    {
        return await _dbSet
            .Where(f => f.BusinessPartnerId == businessPartnerId && f.FiscalYear == financialYear && !f.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BusinessPartnerFinancial>> GetFinancialsByYearRangeAsync(Guid businessPartnerId, int startYear, int endYear)
    {
        return await _dbSet
            .Where(f => f.BusinessPartnerId == businessPartnerId && f.FiscalYear >= startYear && f.FiscalYear <= endYear && !f.IsDeleted)
            .OrderByDescending(f => f.FiscalYear)
            .ToListAsync();
    }

    public async Task<BusinessPartnerFinancial?> GetLatestFinancialAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(f => f.BusinessPartnerId == businessPartnerId && !f.IsDeleted)
            .OrderByDescending(f => f.FiscalYear)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasFinancialRecordAsync(Guid businessPartnerId, int financialYear)
    {
        return await _dbSet.AnyAsync(f => f.BusinessPartnerId == businessPartnerId && f.FiscalYear == financialYear && !f.IsDeleted);
    }
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION REPOSITORY
// ============================================================================

public class BusinessPartnerRegistrationRepository : GenericRepository<BusinessPartnerRegistration>, IBusinessPartnerRegistrationRepository
{
    private readonly ILogger<BusinessPartnerRegistrationRepository> _logger;

    public BusinessPartnerRegistrationRepository(ApplicationDbContext context, ILogger<BusinessPartnerRegistrationRepository> logger) : base(context)
    {
        _logger = logger;
    }

    public override async Task<BusinessPartnerRegistration?> GetByIdAsync(Guid id)
    {
        // Use IgnoreQueryFilters to bypass tenant filtering for external registrations
        // External users may not have tenant context set
        return await _dbSet
            .IgnoreQueryFilters()
            .Where(r => r.Id == id && !r.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerRegistration?> GetByApplicationNumberAsync(string applicationNumber)
    {
        // Use IgnoreQueryFilters to bypass tenant filtering for external registrations
        return await _dbSet
            .IgnoreQueryFilters()
            .Where(r => r.RegistrationNumber == applicationNumber && !r.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public override async Task<IEnumerable<BusinessPartnerRegistration>> GetAllAsync()
    {
        // Override to ignore query filters for debugging
        _logger.LogInformation("GetAllAsync called - ignoring query filters");
        return await _dbSet
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted)
            .ToListAsync();
    }

    public async Task<BusinessPartnerRegistration> CreateAsync(BusinessPartnerRegistration registration)
    {
        return await AddAsync(registration);
    }

    public new async Task<BusinessPartnerRegistration> UpdateAsync(BusinessPartnerRegistration registration)
    {
        await base.UpdateAsync(registration);
        return registration;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var registration = await GetByIdAsync(id);
        if (registration != null)
        {
            await DeleteAsync(registration);
        }
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<BusinessPartnerRegistration>> GetRegistrationsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? partnerType = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        // Exclude Draft registrations from admin UI - they should only be visible to the user who created them
        var query = _dbSet.Where(r => !r.IsDeleted && r.Status != "Draft");

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(r =>
                r.ApplicantName.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                r.ApplicantEmail.ToLower().Contains(searchLower) ||
                r.RegistrationNumber.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            query = query.Where(r => r.PartnerType == partnerType);
        }

        if (startDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= endDate.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<BusinessPartnerRegistration>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<BusinessPartnerRegistration>> GetRegistrationsByStatusAsync(string status)
    {
        return await _dbSet
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerRegistration>> GetRegistrationsByUserAsync(Guid userId)
    {
        _logger.LogInformation("GetRegistrationsByUserAsync called for userId: {UserId}", userId);

        // First, check total count without filters to diagnose
        var totalInDb = await _dbSet.IgnoreQueryFilters().CountAsync(r => !r.IsDeleted);
        _logger.LogInformation("Total non-deleted registrations in DB (ignoring filters): {Count}", totalInDb);

        var matchingCreatedById = await _dbSet.IgnoreQueryFilters()
            .Where(r => r.CreatedById == userId && !r.IsDeleted)
            .ToListAsync();
        _logger.LogInformation("Registrations matching CreatedById={UserId} (ignoring filters): {Count}", userId, matchingCreatedById.Count);

        if (matchingCreatedById.Any())
        {
            foreach (var reg in matchingCreatedById)
            {
                _logger.LogInformation("Found registration: Id={Id}, TenantId={TenantId}, CreatedById={CreatedById}, Status={Status}",
                    reg.Id, reg.TenantId, reg.CreatedById, reg.Status);
            }
        }

        // Use IgnoreQueryFilters to bypass tenant filtering for external registrations
        // External users may not have tenant context properly set during registration
        var results = await _dbSet
            .IgnoreQueryFilters()
            .Where(r => r.CreatedById == userId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        _logger.LogInformation("Returning {Count} registrations for user {UserId}", results.Count, userId);
        return results;
    }

    public async Task<IEnumerable<BusinessPartnerRegistration>> GetPendingReviewRegistrationsAsync()
    {
        return await _dbSet
            .Where(r => r.Status == "Submitted" && !r.IsDeleted)
            .OrderBy(r => r.SubmittedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerRegistration>> GetApprovedRegistrationsAsync()
    {
        return await _dbSet
            .Where(r => r.Status == "Approved" && !r.IsDeleted)
            .OrderByDescending(r => r.ApprovedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerRegistration>> GetRejectedRegistrationsAsync()
    {
        return await _dbSet
            .Where(r => r.Status == "Rejected" && !r.IsDeleted)
            .OrderByDescending(r => r.ReviewedDate)
            .ToListAsync();
    }

    public async Task<BusinessPartnerRegistration?> GetWithDocumentsAsync(Guid id)
    {
        // Use IgnoreQueryFilters to bypass tenant filtering for external registrations
        // Note: IgnoreQueryFilters applies to the entire query including related entities
        return await _dbSet
            .IgnoreQueryFilters()
            .Where(r => r.Id == id && !r.IsDeleted)
            .Include(r => r.Documents.Where(d => !d.IsDeleted))
                .ThenInclude(d => d.FileUploadRecord)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerRegistration?> GetWithStatusHistoryAsync(Guid id)
    {
        // Use IgnoreQueryFilters to bypass tenant filtering for external registrations
        return await _dbSet
            .IgnoreQueryFilters()
            .Where(r => r.Id == id && !r.IsDeleted)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GenerateApplicationNumberAsync()
    {
        var year = DateTime.UtcNow.Year.ToString().Substring(2);
        var yearPrefix = $"APP{year}";

        var lastRegistration = await _dbSet
            .Where(r => r.RegistrationNumber.StartsWith(yearPrefix) && !r.IsDeleted)
            .OrderByDescending(r => r.RegistrationNumber)
            .FirstOrDefaultAsync();

        int nextSequence = 1;
        if (lastRegistration != null)
        {
            var lastSequence = lastRegistration.RegistrationNumber.Substring(5);
            if (int.TryParse(lastSequence, out int seq))
            {
                nextSequence = seq + 1;
            }
        }

        return $"{yearPrefix}{nextSequence:D4}";
    }

    public async Task UpdateStatusAsync(Guid registrationId, string status, Guid? changedById = null, string? notes = null)
    {
        var registration = await GetByIdAsync(registrationId);
        if (registration != null)
        {
            var oldStatus = registration.Status;
            registration.Status = status;
            registration.UpdatedAt = DateTime.UtcNow;

            if (status == "Submitted")
            {
                registration.SubmittedDate = DateTime.UtcNow;
            }
            else if (status == "Approved")
            {
                registration.ApprovedDate = DateTime.UtcNow;
                registration.ApprovedById = changedById;
            }
            else if (status == "Rejected" || status == "MoreInfoRequired")
            {
                registration.ReviewedDate = DateTime.UtcNow;
                registration.ReviewedById = changedById;
            }

            // Create status history entry
            if (changedById.HasValue)
            {
                var history = new BusinessPartnerRegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    FromStatus = oldStatus,
                    ToStatus = status,
                    ChangedById = changedById.Value,
                    ChangedAt = DateTime.UtcNow,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.BusinessPartnerRegistrationStatusHistories.AddAsync(history);
            }

            // Note: SaveChangesAsync should be called by the service layer using Unit of Work
        }
    }

    public async Task<bool> HasPendingRegistrationAsync(string email)
    {
        return await _dbSet.AnyAsync(r =>
            r.ApplicantEmail == email &&
            (r.Status == "Draft" || r.Status == "Submitted" || r.Status == "MoreInfoRequired") &&
            !r.IsDeleted);
    }
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION DOCUMENT REPOSITORY
// ============================================================================

public class BusinessPartnerRegistrationDocumentRepository : GenericRepository<BusinessPartnerRegistrationDocument>, IBusinessPartnerRegistrationDocumentRepository
{
    public BusinessPartnerRegistrationDocumentRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<BusinessPartnerRegistrationDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(d => d.FileUploadRecord)
            .Where(d => d.Id == id && !d.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerRegistrationDocument> CreateAsync(BusinessPartnerRegistrationDocument document)
    {
        return await AddAsync(document);
    }

    public new async Task<BusinessPartnerRegistrationDocument> UpdateAsync(BusinessPartnerRegistrationDocument document)
    {
        await base.UpdateAsync(document);
        return document;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var document = await GetByIdAsync(id);
        if (document != null)
        {
            await DeleteAsync(document);
        }
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetDocumentsByRegistrationAsync(Guid registrationId)
    {
        return await _dbSet
            .Include(d => d.FileUploadRecord)
            .Where(d => d.RegistrationId == registrationId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetByRegistrationIdAsync(Guid registrationId)
    {
        return await GetDocumentsByRegistrationAsync(registrationId);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetDocumentsByTypeAsync(Guid registrationId, string documentType)
    {
        return await _dbSet
            .Where(d => d.RegistrationId == registrationId && d.DocumentType == documentType && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetUnverifiedDocumentsAsync(Guid registrationId)
    {
        return await _dbSet
            .Where(d => d.RegistrationId == registrationId && !d.IsVerified && !d.IsDeleted)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task VerifyDocumentAsync(Guid documentId, Guid verifiedById)
    {
        var document = await GetByIdAsync(documentId);
        if (document != null)
        {
            document.IsVerified = true;
            document.VerifiedById = verifiedById;
            document.VerifiedDate = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            // Note: SaveChangesAsync should be called by the service layer using Unit of Work
        }
    }
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION STATUS HISTORY REPOSITORY
// ============================================================================

public class BusinessPartnerRegistrationStatusHistoryRepository : GenericRepository<BusinessPartnerRegistrationStatusHistory>, IBusinessPartnerRegistrationStatusHistoryRepository
{
    public BusinessPartnerRegistrationStatusHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<BusinessPartnerRegistrationStatusHistory> CreateAsync(BusinessPartnerRegistrationStatusHistory history)
    {
        return await AddAsync(history);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationStatusHistory>> GetHistoryByRegistrationAsync(Guid registrationId)
    {
        return await _dbSet
            .Where(h => h.RegistrationId == registrationId && !h.IsDeleted)
            .Include(h => h.ChangedBy)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }
}
