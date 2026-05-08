using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

// ============================================================================
// LICENSE TYPE REPOSITORY
// ============================================================================

public class LicenseTypeRepository : GenericRepository<LicenseType>, ILicenseTypeRepository
{
    public LicenseTypeRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<LicenseType?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(lt => lt.Id == id && !lt.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<LicenseType?> GetByCodeAsync(string licenseCode)
    {
        return await _dbSet
            .Where(lt => lt.LicenseCode == licenseCode && !lt.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<LicenseType> CreateAsync(LicenseType licenseType)
    {
        return await AddAsync(licenseType);
    }

    public new async Task<LicenseType> UpdateAsync(LicenseType licenseType)
    {
        await base.UpdateAsync(licenseType);
        return licenseType;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var licenseType = await GetByIdAsync(id);
        if (licenseType != null)
        {
            await DeleteAsync(licenseType);
        }
    }

    public async Task<IEnumerable<LicenseType>> GetAllLicenseTypesAsync()
    {
        return await _dbSet
            .Where(lt => !lt.IsDeleted)
            .OrderBy(lt => lt.LicenseName)
            .ToListAsync();
    }

    public async Task<IEnumerable<LicenseType>> GetActiveLicenseTypesAsync()
    {
        return await _dbSet
            .Where(lt => lt.IsActive && !lt.IsDeleted)
            .OrderBy(lt => lt.LicenseName)
            .ToListAsync();
    }

    public async Task<IEnumerable<LicenseType>> GetMandatoryLicenseTypesAsync(string applicableTo)
    {
        return await _dbSet
            .Where(lt => lt.IsMandatory && lt.IsActive && !lt.IsDeleted)
            .OrderBy(lt => lt.LicenseName)
            .ToListAsync();
    }

    public async Task<IEnumerable<LicenseType>> GetLicenseTypesByApplicabilityAsync(string applicableTo)
    {
        return await _dbSet
            .Where(lt => lt.IsActive && !lt.IsDeleted)
            .OrderBy(lt => lt.LicenseName)
            .ToListAsync();
    }

    public async Task<bool> IsLicenseCodeUniqueAsync(string licenseCode, Guid? excludeId = null)
    {
        var query = _dbSet.Where(lt => lt.LicenseCode == licenseCode && !lt.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(lt => lt.Id != excludeId.Value);
        }

        return !await query.AnyAsync();
    }
}

// ============================================================================
// BUSINESS PARTNER LICENSE REPOSITORY
// ============================================================================

public class BusinessPartnerLicenseRepository : GenericRepository<BusinessPartnerLicense>, IBusinessPartnerLicenseRepository
{
    public BusinessPartnerLicenseRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<BusinessPartnerLicense?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(l => l.Id == id && !l.IsDeleted)
            .Include(l => l.LicenseType)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerLicense> CreateAsync(BusinessPartnerLicense license)
    {
        return await AddAsync(license);
    }

    public new async Task<BusinessPartnerLicense> UpdateAsync(BusinessPartnerLicense license)
    {
        await base.UpdateAsync(license);
        return license;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var license = await GetByIdAsync(id);
        if (license != null)
        {
            await DeleteAsync(license);
        }
    }

    public async Task<IEnumerable<BusinessPartnerLicense>> GetLicensesByPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(l => l.BusinessPartnerId == businessPartnerId && !l.IsDeleted)
            .Include(l => l.LicenseType)
            .OrderBy(l => l.LicenseType!.LicenseName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerLicense>> GetActiveLicensesByPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(l => l.BusinessPartnerId == businessPartnerId && l.Status == "Active" && !l.IsDeleted)
            .Include(l => l.LicenseType)
            .OrderBy(l => l.LicenseType!.LicenseName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerLicense>> GetExpiringLicensesAsync(int daysAhead = 30)
    {
        var expiryDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value <= expiryDate && l.Status == "Active" && !l.IsDeleted)
            .Include(l => l.LicenseType)
            .Include(l => l.BusinessPartner)
            .OrderBy(l => l.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerLicense>> GetExpiredLicensesAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(l => l.ExpiryDate.HasValue && l.ExpiryDate.Value < now && l.Status == "Active" && !l.IsDeleted)
            .Include(l => l.LicenseType)
            .Include(l => l.BusinessPartner)
            .OrderBy(l => l.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerLicense>> GetLicensesByTypeAsync(Guid licenseTypeId)
    {
        return await _dbSet
            .Where(l => l.LicenseTypeId == licenseTypeId && !l.IsDeleted)
            .Include(l => l.BusinessPartner)
            .OrderBy(l => l.BusinessPartner!.PartnerName)
            .ToListAsync();
    }

    public async Task<BusinessPartnerLicense?> GetPartnerLicenseByTypeAsync(Guid businessPartnerId, Guid licenseTypeId)
    {
        return await _dbSet
            .Where(l => l.BusinessPartnerId == businessPartnerId && l.LicenseTypeId == licenseTypeId && !l.IsDeleted)
            .Include(l => l.LicenseType)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasValidLicenseAsync(Guid businessPartnerId, Guid licenseTypeId)
    {
        var now = DateTime.UtcNow;
        return await _dbSet.AnyAsync(l =>
            l.BusinessPartnerId == businessPartnerId &&
            l.LicenseTypeId == licenseTypeId &&
            l.Status == "Active" &&
            (!l.ExpiryDate.HasValue || l.ExpiryDate.Value > now) &&
            !l.IsDeleted);
    }

    public async Task UpdateLicenseStatusAsync(Guid licenseId, string status)
    {
        var license = await GetByIdAsync(licenseId);
        if (license != null)
        {
            license.Status = status;
            license.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

// ============================================================================
// BUSINESS PARTNER CONTACT REPOSITORY
// ============================================================================

public class BusinessPartnerContactRepository : GenericRepository<BusinessPartnerContact>, IBusinessPartnerContactRepository
{
    public BusinessPartnerContactRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<BusinessPartnerContact?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessPartnerContact> CreateAsync(BusinessPartnerContact contact)
    {
        return await AddAsync(contact);
    }

    public new async Task<BusinessPartnerContact> UpdateAsync(BusinessPartnerContact contact)
    {
        await base.UpdateAsync(contact);
        return contact;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var contact = await GetByIdAsync(id);
        if (contact != null)
        {
            await DeleteAsync(contact);
        }
    }

    public async Task<IEnumerable<BusinessPartnerContact>> GetContactsByPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.ContactName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BusinessPartnerContact>> GetActiveContactsByPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.ContactName)
            .ToListAsync();
    }

    public async Task<BusinessPartnerContact?> GetPrimaryContactAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && c.IsPrimary && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BusinessPartnerContact>> GetContactsByDepartmentAsync(Guid businessPartnerId, string department)
    {
        return await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && c.Department == department && !c.IsDeleted)
            .OrderBy(c => c.ContactName)
            .ToListAsync();
    }

    public async Task SetPrimaryContactAsync(Guid businessPartnerId, Guid contactId)
    {
        // First, unset all primary contacts for this partner
        var contacts = await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && c.IsPrimary && !c.IsDeleted)
            .ToListAsync();

        foreach (var contact in contacts)
        {
            contact.IsPrimary = false;
            contact.UpdatedAt = DateTime.UtcNow;
        }

        // Then set the new primary contact
        var newPrimary = await GetByIdAsync(contactId);
        if (newPrimary != null && newPrimary.BusinessPartnerId == businessPartnerId)
        {
            newPrimary.IsPrimary = true;
            newPrimary.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
