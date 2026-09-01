using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 7: COMPLIANCE & SAFETY
// ============================================================================

#region Staff Travel Document Repository

public class StaffTravelDocumentRepository : GenericRepository<StaffTravelDocument>, IStaffTravelDocumentRepository
{
    public StaffTravelDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelDocument?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(d => d.Employee)
            .Include(d => d.IssuingCountry)
            .Include(d => d.VerifiedBy)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelDocument>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(d => d.Employee)
            .Include(d => d.IssuingCountry)
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelDocument>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(d => d.IssuingCountry)
            .Where(d => d.EmployeeId == employeeId && !d.IsDeleted)
            .OrderBy(d => d.DocumentType)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelDocument>> GetByTypeAsync(Guid employeeId, TravelDocumentType documentType)
    {
        return await _dbSet
            .Include(d => d.IssuingCountry)
            .Where(d => d.EmployeeId == employeeId && d.DocumentType == documentType && !d.IsDeleted)
            .OrderByDescending(d => d.IsPrimary)
            .ToListAsync();
    }

    public async Task<StaffTravelDocument?> GetPrimaryDocumentAsync(Guid employeeId, TravelDocumentType documentType)
    {
        return await _dbSet
            .Include(d => d.IssuingCountry)
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.DocumentType == documentType
                                   && d.IsPrimary && !d.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelDocument>> GetExpiringDocumentsAsync(int daysAhead = 90)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(d => d.Employee)
            .Where(d => !d.IsDeleted && d.ExpiryDate != null
                     && d.ExpiryDate >= today && d.ExpiryDate <= cutoff)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Visa Requirement Repository

public class StaffTravelVisaRequirementRepository : GenericRepository<StaffTravelVisaRequirement>, IStaffTravelVisaRequirementRepository
{
    public StaffTravelVisaRequirementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelVisaRequirement?> GetRequirementAsync(Guid passportCountryId, Guid destinationCountryId)
    {
        return await _dbSet
            .Include(r => r.PassportCountry)
            .Include(r => r.DestinationCountry)
            .FirstOrDefaultAsync(r => r.PassportCountryId == passportCountryId
                                   && r.DestinationCountryId == destinationCountryId && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelVisaRequirement>> GetByDestinationAsync(Guid destinationCountryId)
    {
        return await _dbSet
            .Include(r => r.PassportCountry)
            // ⚠ DestinationCountry was NOT included, while GetRequirementAsync beside it includes
            // both — so this list resolved the passport name and returned a null destination name
            // for every row, though the DTO declares it. Measured 2026-09-01 (lane 5b): the lookup
            // answered "Ghana"/"United Kingdom" and this list answered "Ghana"/null. The uneven
            // `.Include` shape.
            .Include(r => r.DestinationCountry)
            .Where(r => r.DestinationCountryId == destinationCountryId && !r.IsDeleted)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Visa Application Repository

public class StaffTravelVisaApplicationRepository : GenericRepository<StaffTravelVisaApplication>, IStaffTravelVisaApplicationRepository
{
    public StaffTravelVisaApplicationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelVisaApplication?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(v => v.Employee)
            .Include(v => v.DestinationCountry)
            .Include(v => v.Vendor)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == tenantId && !v.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelVisaApplication>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(v => v.Employee)
            .Include(v => v.DestinationCountry)
            .Where(v => !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelVisaApplication>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(v => v.DestinationCountry)
            .Include(v => v.Vendor)
            .Where(v => v.StaffTravelRequestId == requestId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelVisaApplication>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(v => v.DestinationCountry)
            .Where(v => v.EmployeeId == employeeId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelVisaApplication>> GetByStatusAsync(VisaApplicationStatus status)
    {
        return await _dbSet
            .Include(v => v.Employee)
            .Include(v => v.DestinationCountry)
            .Where(v => v.Status == status && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelVisaApplication>> GetExpiringVisasAsync(int daysAhead = 90)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(v => v.Employee)
            .Include(v => v.DestinationCountry)
            .Where(v => v.Status == VisaApplicationStatus.Approved && !v.IsDeleted
                     && v.ExpiryDate != null && v.ExpiryDate >= today && v.ExpiryDate <= cutoff)
            .OrderBy(v => v.ExpiryDate)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Risk Assessment Repository

public class StaffTravelRiskAssessmentRepository : GenericRepository<StaffTravelRiskAssessment>, IStaffTravelRiskAssessmentRepository
{
    public StaffTravelRiskAssessmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelRiskAssessment?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(a => a.AssessedBy)
            .Include(a => a.DestinationCountry)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelRiskAssessment>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(a => a.DestinationCountry)
            .Include(a => a.AssessedBy)
            .Where(a => a.StaffTravelRequestId == requestId && !a.IsDeleted)
            .OrderByDescending(a => a.AssessedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessment>> GetByCountryAsync(Guid countryId)
    {
        return await _dbSet
            .Where(a => a.DestinationCountryId == countryId && !a.IsDeleted)
            .OrderByDescending(a => a.AssessedAt)
            .ToListAsync();
    }

    public async Task<StaffTravelRiskAssessment?> GetCurrentForRequestAsync(Guid requestId)
    {
        return await _dbSet
            .Include(a => a.DestinationCountry)
            .Where(a => a.StaffTravelRequestId == requestId && !a.IsDeleted)
            .OrderByDescending(a => a.AssessedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessment>> GetRequiringAcknowledgementAsync()
    {
        return await _dbSet
            .Include(a => a.DestinationCountry)
            .Where(a => !a.EmployeeAcknowledged && !a.IsDeleted)
            .OrderByDescending(a => a.RiskLevel)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Alert Repository

public class StaffTravelAlertRepository : GenericRepository<StaffTravelAlert>, IStaffTravelAlertRepository
{
    public StaffTravelAlertRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelAlert?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(a => a.Country)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelAlert>> GetActiveAlertsAsync()
    {
        return await _dbSet
            .Include(a => a.Country)
            .Where(a => a.IsActive && !a.IsDeleted)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAlert>> GetByCountryAsync(Guid countryId)
    {
        return await _dbSet
            .Where(a => a.CountryId == countryId && !a.IsDeleted)
            .OrderByDescending(a => a.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAlert>> GetCurrentAlertsForCountryAsync(Guid countryId)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            // The country is projected onto the DTO this read now returns in full.
            .Include(a => a.Country)
            .Where(a => a.CountryId == countryId && a.IsActive && !a.IsDeleted
                     && a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo >= now))
            .OrderByDescending(a => a.Severity)
            .ToListAsync();
    }

    public async Task<StaffTravelAlert?> GetWithNotificationsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.Country)
            .Include(a => a.Notifications)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }
}

#endregion

#region Staff Travel Alert Notification Repository

public class StaffTravelAlertNotificationRepository : GenericRepository<StaffTravelAlertNotification>, IStaffTravelAlertNotificationRepository
{
    public StaffTravelAlertNotificationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelAlertNotification>> GetByAlertIdAsync(Guid alertId)
    {
        return await _dbSet
            .Include(n => n.Employee)
            .Where(n => n.TravelAlertId == alertId && !n.IsDeleted)
            .OrderByDescending(n => n.NotificationSentAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAlertNotification>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(n => n.TravelAlert)
            .Where(n => n.StaffTravelRequestId == requestId && !n.IsDeleted)
            .OrderByDescending(n => n.NotificationSentAt)
            .ToListAsync();
    }

    // ⚠ StaffTravelRequest is included on both of these because the DTO carries RequestNumber and
    // the traveller's own screen has to say WHICH trip an alert is about. TravelAlert alone left
    // that column blank.
    public async Task<IEnumerable<StaffTravelAlertNotification>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(n => n.TravelAlert)
            .Include(n => n.StaffTravelRequest)
            .Where(n => n.EmployeeId == employeeId && !n.IsDeleted)
            .OrderByDescending(n => n.NotificationSentAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelAlertNotification>> GetUnacknowledgedAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(n => n.TravelAlert)
            .Include(n => n.StaffTravelRequest)
            .Where(n => n.EmployeeId == employeeId && !n.IsAcknowledged && !n.IsDeleted)
            .OrderByDescending(n => n.NotificationSentAt)
            .ToListAsync();
    }

    /// <summary>
    /// One notification with every navigation its DTO projects — the alert, the trip and the
    /// employee.
    /// </summary>
    /// <remarks>
    /// The plain <c>GetByIdAsync</c> loads none of them, so <c>CreateAlertNotificationAsync</c>
    /// re-read with it and still returned a row whose <c>alertTitle</c>, <c>requestNumber</c> and
    /// <c>employeeName</c> were all blank — the desk saw three empty columns on the row it had
    /// just created and the right ones after a refetch.
    /// </remarks>
    public async Task<StaffTravelAlertNotification?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(n => n.TravelAlert)
            .Include(n => n.StaffTravelRequest)
            .Include(n => n.Employee)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }
}

#endregion

#region Staff Travel Insurance Policy Repository

public class StaffTravelInsurancePolicyRepository : GenericRepository<StaffTravelInsurancePolicy>, IStaffTravelInsurancePolicyRepository
{
    public StaffTravelInsurancePolicyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelInsurancePolicy?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(i => i.Vendor)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && !i.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelInsurancePolicy>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(i => i.Vendor)
            .Where(i => i.StaffTravelRequestId == requestId && !i.IsDeleted)
            .OrderByDescending(i => i.CoverageStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelInsurancePolicy>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Where(i => i.VendorId == vendorId && !i.IsDeleted)
            .OrderByDescending(i => i.CoverageStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelInsurancePolicy>> GetActiveByDateAsync(DateOnly onDate)
    {
        return await _dbSet
            .Include(i => i.Vendor)
            .Where(i => !i.IsDeleted && i.CoverageStart <= onDate && i.CoverageEnd >= onDate)
            .OrderBy(i => i.CoverageEnd)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Health Requirement Repository

public class StaffTravelHealthRequirementRepository : GenericRepository<StaffTravelHealthRequirement>, IStaffTravelHealthRequirementRepository
{
    public StaffTravelHealthRequirementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelHealthRequirement?> GetWithDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(h => h.Country)
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId && !h.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelHealthRequirement>> GetByCountryAsync(Guid countryId)
    {
        return await _dbSet
            .Where(h => h.CountryId == countryId && !h.IsDeleted)
            .OrderBy(h => h.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirement>> GetMandatoryByCountryAsync(Guid countryId)
    {
        return await _dbSet
            .Where(h => h.CountryId == countryId && h.IsMandatory && h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.RequirementName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirement>> GetActiveAsync()
    {
        return await _dbSet
            .Include(h => h.Country)
            .Where(h => h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.RequirementName)
            .ToListAsync();
    }
}

#endregion
