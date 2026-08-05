using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// HEALTHCARE FACILITY REPOSITORY
// ============================================================================

#region Healthcare Facility Repository

public class HealthcareFacilityRepository : GenericRepository<HealthcareFacility>, IHealthcareFacilityRepository
{
    public HealthcareFacilityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<HealthcareFacility?> GetByFacilityCodeAsync(string facilityCode)
    {
        return await _dbSet
            .Include(f => f.Country)
            .FirstOrDefaultAsync(f => f.FacilityCode == facilityCode && !f.IsDeleted);
    }

    public async Task<IEnumerable<HealthcareFacility>> GetByFacilityTypeAsync(HealthFacilityType facilityType)
    {
        return await _dbSet
            .Include(f => f.Country)
            .Where(f => f.FacilityType == facilityType && !f.IsDeleted)
            .OrderBy(f => f.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<HealthcareFacility>> GetActiveFacilitiesAsync()
    {
        return await _dbSet
            .Include(f => f.Country)
            .Where(f => f.IsActive && !f.IsDeleted)
            .OrderBy(f => f.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<HealthcareFacility>> GetFacilitiesWithEmergencyServicesAsync()
    {
        return await _dbSet
            .Include(f => f.Country)
            .Where(f => f.HasEmergencyServices && !f.IsDeleted)
            .OrderBy(f => f.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<HealthcareFacility>> GetFacilitiesAcceptingNHISAsync()
    {
        return await _dbSet
            .Include(f => f.Country)
            .Where(f => f.AcceptsNHIS && !f.IsDeleted)
            .OrderBy(f => f.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<HealthcareFacility>> GetFacilitiesByCountryAsync(Guid countryId)
    {
        return await _dbSet
            .Include(f => f.Country)
            .Where(f => f.CountryId == countryId && !f.IsDeleted)
            .OrderBy(f => f.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<HealthcareFacility>> SearchFacilitiesAsync(string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _dbSet
            .Include(f => f.Country)
            .Where(f => !f.IsDeleted
                     && (f.FacilityName.ToLower().Contains(term)
                         || f.FacilityCode.ToLower().Contains(term)
                         || (f.City ?? string.Empty).ToLower().Contains(term)))
            .OrderBy(f => f.FacilityName)
            .ToListAsync();
    }

    public async Task<HealthcareFacility?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(f => f.Country)
            .Include(f => f.Physicians)
            .Include(f => f.Services)
            .Include(f => f.ProviderFacilities).ThenInclude(pf => pf.MedicalInsuranceProvider)
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
    }
}

#endregion

// ============================================================================
// PHYSICIAN REPOSITORY
// ============================================================================

#region Physician Repository

public class PhysicianRepository : GenericRepository<Physician>, IPhysicianRepository
{
    public PhysicianRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Physician>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(p => p.Facility)
            .Where(p => p.FacilityId == facilityId && !p.IsDeleted)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Physician>> GetActivePhysiciansAsync()
    {
        return await _dbSet
            .Include(p => p.Facility)
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Physician>> GetVerifiedPhysiciansAsync()
    {
        return await _dbSet
            .Include(p => p.Facility)
            .Where(p => p.IsVerified && !p.IsDeleted)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Physician>> GetBySpecializationAsync(string specialization)
    {
        var term = specialization.ToLower();
        return await _dbSet
            .Include(p => p.Facility)
            .Where(p => !p.IsDeleted
                     && p.Specialization != null
                     && p.Specialization.ToLower().Contains(term))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Physician>> SearchPhysiciansAsync(string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _dbSet
            .Include(p => p.Facility)
            .Where(p => !p.IsDeleted
                     && ((p.FirstName + " " + p.LastName).ToLower().Contains(term)
                         || (p.Email ?? string.Empty).ToLower().Contains(term)
                         || (p.MedicalLicenseNumber ?? string.Empty).ToLower().Contains(term)))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<Physician?> GetWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Facility)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// FACILITY SERVICE REPOSITORY
// ============================================================================

#region Facility Service Repository

public class FacilityServiceRepository : GenericRepository<FacilityService>, IFacilityServiceRepository
{
    public FacilityServiceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<FacilityService>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(s => s.Facility)
            .Where(s => s.FacilityId == facilityId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<FacilityService>> GetByServiceTypeAsync(MedicalServiceType serviceType)
    {
        return await _dbSet
            .Include(s => s.Facility)
            .Where(s => s.ServiceType == serviceType && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<FacilityService>> GetActiveServicesAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(s => s.Facility)
            .Where(s => s.FacilityId == facilityId && s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<FacilityService>> GetEmergencyServicesAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(s => s.Facility)
            .Where(s => s.FacilityId == facilityId && s.IsEmergencyService && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<FacilityService>> GetRequiringPreAuthorizationAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(s => s.Facility)
            .Where(s => s.FacilityId == facilityId && s.RequiresPreAuthorization && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PROVIDER REPOSITORY
// ============================================================================

#region Medical Insurance Provider Repository

public class MedicalInsuranceProviderRepository : GenericRepository<MedicalInsuranceProvider>, IMedicalInsuranceProviderRepository
{
    public MedicalInsuranceProviderRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalInsuranceProvider?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Include(p => p.Country)
            .FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted);
    }

    public async Task<IEnumerable<MedicalInsuranceProvider>> GetByProviderTypeAsync(MedicalInsuranceProviderType providerType)
    {
        return await _dbSet
            .Include(p => p.Country)
            .Where(p => p.ProviderType == providerType && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProvider>> GetActiveProvidersAsync()
    {
        return await _dbSet
            .Include(p => p.Country)
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProvider>> SearchProvidersAsync(string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _dbSet
            .Include(p => p.Country)
            .Where(p => !p.IsDeleted
                     && (p.Name.ToLower().Contains(term)
                         || p.Code.ToLower().Contains(term)))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<MedicalInsuranceProvider?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Country)
            .Include(p => p.Plans)
            .Include(p => p.ProviderFacilities).ThenInclude(pf => pf.Facility)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PLAN REPOSITORY
// ============================================================================

#region Medical Insurance Plan Repository

public class MedicalInsurancePlanRepository : GenericRepository<MedicalInsurancePlan>, IMedicalInsurancePlanRepository
{
    public MedicalInsurancePlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalInsurancePlan?> GetByCodeAsync(Guid providerId, string code)
    {
        return await _dbSet
            .Include(p => p.MedicalInsuranceProvider)
            .FirstOrDefaultAsync(p => p.MedicalInsuranceProviderId == providerId
                                   && p.Code == code
                                   && !p.IsDeleted);
    }

    public async Task<IEnumerable<MedicalInsurancePlan>> GetByProviderIdAsync(Guid providerId)
    {
        return await _dbSet
            .Include(p => p.MedicalInsuranceProvider)
            .Where(p => p.MedicalInsuranceProviderId == providerId && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePlan>> GetByPlanTypeAsync(MedicalInsurancePlanType planType)
    {
        return await _dbSet
            .Include(p => p.MedicalInsuranceProvider)
            .Where(p => p.PlanType == planType && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePlan>> GetActivePlansAsync(Guid providerId)
    {
        return await _dbSet
            .Include(p => p.MedicalInsuranceProvider)
            .Where(p => p.MedicalInsuranceProviderId == providerId && p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePlan>> GetPlansCoveringDependentsAsync(Guid providerId)
    {
        return await _dbSet
            .Include(p => p.MedicalInsuranceProvider)
            .Where(p => p.MedicalInsuranceProviderId == providerId && p.CoversDependents && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// EMPLOYEE MEDICAL INSURANCE POLICY REPOSITORY
// ============================================================================

#region Employee Medical Insurance Policy Repository

public class EmployeeMedicalInsurancePolicyRepository : GenericRepository<EmployeeMedicalInsurancePolicy>, IEmployeeMedicalInsurancePolicyRepository
{
    public EmployeeMedicalInsurancePolicyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<EmployeeMedicalInsurancePolicy?> GetByPolicyNumberAsync(string policyNumber)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Include(p => p.BenefitTier)
            .FirstOrDefaultAsync(p => p.PolicyNumber == policyNumber && !p.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Include(p => p.BenefitTier)
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByProviderIdAsync(Guid providerId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Where(p => p.ProviderId == providerId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Where(p => p.PlanId == planId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByBenefitTierIdAsync(Guid benefitTierId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Include(p => p.BenefitTier)
            .Where(p => p.BenefitTierId == benefitTierId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByStatusAsync(MedicalInsurancePolicyStatus status)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<EmployeeMedicalInsurancePolicy?> GetActivePolicyAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Include(p => p.BenefitTier)
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId
                                   && p.IsActive
                                   && p.Status == MedicalInsurancePolicyStatus.Active
                                   && !p.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetExpiringPoliciesAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Where(p => !p.IsDeleted
                     && p.IsActive
                     && p.EndDate != null
                     && p.EndDate <= cutoff)
            .OrderBy(p => p.EndDate)
            .ToListAsync();
    }

    public async Task<EmployeeMedicalInsurancePolicy?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .Include(p => p.BenefitTier)
            .Include(p => p.Dependents).ThenInclude(d => d.Dependent)
            .Include(p => p.InsuranceClaims)
            .Include(p => p.PremiumRecords)
            .Include(p => p.ExpenseClaims)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE POLICY DEPENDENT REPOSITORY
// ============================================================================

#region Medical Insurance Policy Dependent Repository

public class MedicalInsurancePolicyDependentRepository : GenericRepository<MedicalInsurancePolicyDependent>, IMedicalInsurancePolicyDependentRepository
{
    public MedicalInsurancePolicyDependentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalInsurancePolicyDependent>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Include(d => d.Policy)
            .Include(d => d.Dependent)
            .Where(d => d.PolicyId == policyId && !d.IsDeleted)
            .OrderBy(d => d.CoverageStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePolicyDependent>> GetByDependentIdAsync(Guid dependentId)
    {
        return await _dbSet
            .Include(d => d.Policy).ThenInclude(p => p.Employee)
            .Include(d => d.Dependent)
            .Where(d => d.DependentId == dependentId && !d.IsDeleted)
            .OrderByDescending(d => d.CoverageStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePolicyDependent>> GetActiveDependentsAsync(Guid policyId)
    {
        return await _dbSet
            .Include(d => d.Policy)
            .Include(d => d.Dependent)
            .Where(d => d.PolicyId == policyId && d.IsActive && !d.IsDeleted)
            .OrderBy(d => d.CoverageStartDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE CLAIM REPOSITORY
// ============================================================================

#region Medical Insurance Claim Repository

public class MedicalInsuranceClaimRepository : GenericRepository<MedicalInsuranceClaim>, IMedicalInsuranceClaimRepository
{
    public MedicalInsuranceClaimRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalInsuranceClaim?> GetByInsuranceClaimNumberAsync(string insuranceClaimNumber)
    {
        return await _dbSet
            .Include(c => c.Policy).ThenInclude(p => p.Employee)
            .Include(c => c.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(c => c.MedicalExpenseClaim)
            .FirstOrDefaultAsync(c => c.InsuranceClaimNumber == insuranceClaimNumber && !c.IsDeleted);
    }

    public async Task<IEnumerable<MedicalInsuranceClaim>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Include(c => c.Policy)
            .Include(c => c.MedicalExpenseClaim)
            .Where(c => c.PolicyId == policyId && !c.IsDeleted)
            .OrderByDescending(c => c.SubmissionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceClaim>> GetByMedicalExpenseClaimIdAsync(Guid medicalExpenseClaimId)
    {
        return await _dbSet
            .Include(c => c.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(c => c.MedicalExpenseClaim)
            .Where(c => c.MedicalExpenseClaimId == medicalExpenseClaimId && !c.IsDeleted)
            .OrderByDescending(c => c.SubmissionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceClaim>> GetByStatusAsync(MedicalInsuranceClaimStatus status)
    {
        return await _dbSet
            .Include(c => c.Policy).ThenInclude(p => p.Employee)
            .Include(c => c.MedicalExpenseClaim)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderByDescending(c => c.SubmissionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceClaim>> GetPendingClaimsAsync()
    {
        return await _dbSet
            .Include(c => c.Policy).ThenInclude(p => p.Employee)
            .Include(c => c.MedicalExpenseClaim)
            .Where(c => !c.IsDeleted
                     && (c.Status == MedicalInsuranceClaimStatus.Submitted
                         || c.Status == MedicalInsuranceClaimStatus.UnderReview))
            .OrderByDescending(c => c.SubmissionDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PROVIDER FACILITY REPOSITORY
// ============================================================================

#region Medical Insurance Provider Facility Repository

public class MedicalInsuranceProviderFacilityRepository : GenericRepository<MedicalInsuranceProviderFacility>, IMedicalInsuranceProviderFacilityRepository
{
    public MedicalInsuranceProviderFacilityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalInsuranceProviderFacility>> GetByProviderIdAsync(Guid providerId)
    {
        return await _dbSet
            .Include(pf => pf.MedicalInsuranceProvider)
            .Include(pf => pf.Facility)
            .Where(pf => pf.ProviderId == providerId && !pf.IsDeleted)
            .OrderBy(pf => pf.Facility.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderFacility>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(pf => pf.MedicalInsuranceProvider)
            .Include(pf => pf.Facility)
            .Where(pf => pf.FacilityId == facilityId && !pf.IsDeleted)
            .OrderBy(pf => pf.MedicalInsuranceProvider.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderFacility>> GetActiveNetworkFacilitiesAsync(Guid providerId)
    {
        return await _dbSet
            .Include(pf => pf.MedicalInsuranceProvider)
            .Include(pf => pf.Facility)
            .Where(pf => pf.ProviderId == providerId && pf.IsActive && !pf.IsDeleted)
            .OrderBy(pf => pf.Facility.FacilityName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderFacility>> GetPreferredProvidersAsync(Guid providerId)
    {
        return await _dbSet
            .Include(pf => pf.MedicalInsuranceProvider)
            .Include(pf => pf.Facility)
            .Where(pf => pf.ProviderId == providerId && pf.IsPreferredProvider && pf.IsActive && !pf.IsDeleted)
            .OrderBy(pf => pf.Facility.FacilityName)
            .ToListAsync();
    }

    public async Task<bool> IsFacilityInNetworkAsync(Guid providerId, Guid facilityId)
    {
        return await _dbSet
            .AnyAsync(pf => pf.ProviderId == providerId
                         && pf.FacilityId == facilityId
                         && pf.IsActive
                         && !pf.IsDeleted);
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PROVIDER DOCUMENT REPOSITORY
// ============================================================================

#region Medical Insurance Provider Document Repository

public class MedicalInsuranceProviderDocumentRepository : GenericRepository<MedicalInsuranceProviderDocument>, IMedicalInsuranceProviderDocumentRepository
{
    public MedicalInsuranceProviderDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalInsuranceProviderDocument>> GetByProviderIdAsync(Guid providerId)
    {
        return await _dbSet
            .Include(d => d.MedicalInsuranceProvider)
            .Where(d => d.ProviderId == providerId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderDocument>> GetByDocumentTypeAsync(Guid providerId, MedicalInsuranceProviderDocumentType documentType)
    {
        return await _dbSet
            .Include(d => d.MedicalInsuranceProvider)
            .Where(d => d.ProviderId == providerId && d.DocumentType == documentType && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderDocument>> GetActiveDocumentsAsync(Guid providerId)
    {
        return await _dbSet
            .Include(d => d.MedicalInsuranceProvider)
            .Where(d => d.ProviderId == providerId && d.IsActive && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderDocument>> GetExpiringDocumentsAsync(Guid providerId, int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(d => d.MedicalInsuranceProvider)
            .Where(d => d.ProviderId == providerId
                     && d.IsActive
                     && !d.IsDeleted
                     && d.ExpiryDate != null
                     && d.ExpiryDate <= cutoff)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PREMIUM RECORD REPOSITORY
// ============================================================================

#region Medical Insurance Premium Record Repository

public class MedicalInsurancePremiumRecordRepository : GenericRepository<MedicalInsurancePremiumRecord>, IMedicalInsurancePremiumRecordRepository
{
    public MedicalInsurancePremiumRecordRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByProviderIdAsync(Guid providerId)
    {
        return await _dbSet
            .Include(r => r.MedicalInsuranceProvider)
            .Include(r => r.MedicalInsurancePlan)
            .Include(r => r.Policy).ThenInclude(p => p!.Employee)
            .Where(r => r.ProviderId == providerId && !r.IsDeleted)
            .OrderByDescending(r => r.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(r => r.MedicalInsuranceProvider)
            .Include(r => r.MedicalInsurancePlan)
            .Include(r => r.Policy).ThenInclude(p => p!.Employee)
            .Where(r => r.PlanId == planId && !r.IsDeleted)
            .OrderByDescending(r => r.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Include(r => r.MedicalInsuranceProvider)
            .Include(r => r.MedicalInsurancePlan)
            .Include(r => r.Policy).ThenInclude(p => p!.Employee)
            .Where(r => r.PolicyId == policyId && !r.IsDeleted)
            .OrderByDescending(r => r.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByStatusAsync(MedicalInsurancePremiumPaymentStatus status)
    {
        return await _dbSet
            .Include(r => r.MedicalInsuranceProvider)
            .Include(r => r.MedicalInsurancePlan)
            .Include(r => r.Policy).ThenInclude(p => p!.Employee)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByBillingPeriodAsync(DateOnly periodStart, DateOnly periodEnd)
    {
        return await _dbSet
            .Include(r => r.MedicalInsuranceProvider)
            .Include(r => r.MedicalInsurancePlan)
            .Include(r => r.Policy).ThenInclude(p => p!.Employee)
            .Where(r => !r.IsDeleted
                     && r.BillingPeriodStart <= periodEnd
                     && r.BillingPeriodEnd >= periodStart)
            .OrderBy(r => r.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecord>> GetOverduePremiumsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(r => r.MedicalInsuranceProvider)
            .Include(r => r.MedicalInsurancePlan)
            .Include(r => r.Policy).ThenInclude(p => p!.Employee)
            .Where(r => !r.IsDeleted
                     && r.Status == MedicalInsurancePremiumPaymentStatus.Pending
                     && r.DueDate != null
                     && r.DueDate < now)
            .OrderBy(r => r.DueDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL BENEFIT SCHEME REPOSITORY
// ============================================================================

#region Medical Benefit Scheme Repository

public class MedicalBenefitSchemeRepository : GenericRepository<MedicalBenefitScheme>, IMedicalBenefitSchemeRepository
{
    public MedicalBenefitSchemeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalBenefitScheme?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.Code == code && !s.IsDeleted);
    }

    public async Task<IEnumerable<MedicalBenefitScheme>> GetActiveSchemesAsync()
    {
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<MedicalBenefitScheme?> GetWithTiersAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.Tiers).ThenInclude(t => t.StaffLevel)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }
}

#endregion

// ============================================================================
// MEDICAL BENEFIT TIER REPOSITORY
// ============================================================================

#region Medical Benefit Tier Repository

public class MedicalBenefitTierRepository : GenericRepository<MedicalBenefitTier>, IMedicalBenefitTierRepository
{
    public MedicalBenefitTierRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalBenefitTier>> GetBySchemeIdAsync(Guid schemeId)
    {
        return await _dbSet
            .Include(t => t.Scheme)
            .Include(t => t.StaffLevel)
            .Where(t => t.SchemeId == schemeId && !t.IsDeleted)
            .OrderBy(t => t.TierName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalBenefitTier>> GetByStaffLevelIdAsync(Guid staffLevelId)
    {
        return await _dbSet
            .Include(t => t.Scheme)
            .Include(t => t.StaffLevel)
            .Where(t => t.StaffLevelId == staffLevelId && !t.IsDeleted)
            .OrderBy(t => t.TierName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalBenefitTier>> GetActiveTiersAsync(Guid schemeId)
    {
        return await _dbSet
            .Include(t => t.Scheme)
            .Include(t => t.StaffLevel)
            .Where(t => t.SchemeId == schemeId && t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.TierName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// EMPLOYEE HEALTH PROFILE REPOSITORY
// ============================================================================

#region Employee Health Profile Repository

public class EmployeeHealthProfileRepository : GenericRepository<EmployeeHealthProfile>, IEmployeeHealthProfileRepository
{
    public EmployeeHealthProfileRepository(ApplicationDbContext context) : base(context) { }

    public async Task<EmployeeHealthProfile?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.PreferredFacility)
            .Include(p => p.PreferredPhysician)
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && !p.IsDeleted);
    }

    public async Task<EmployeeHealthProfile?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.PreferredFacility)
            .Include(p => p.PreferredPhysician)
            .Include(p => p.Conditions)
            .Include(p => p.Allergies)
            .Include(p => p.MedicalExams).ThenInclude(e => e.Facility)
            .Include(p => p.MedicalExams).ThenInclude(e => e.Physician)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// EMPLOYEE HEALTH CONDITION REPOSITORY
// ============================================================================

#region Employee Health Condition Repository

public class EmployeeHealthConditionRepository : GenericRepository<EmployeeHealthCondition>, IEmployeeHealthConditionRepository
{
    public EmployeeHealthConditionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeHealthCondition>> GetByHealthProfileIdAsync(Guid healthProfileId)
    {
        return await _dbSet
            .Include(c => c.HealthProfile).ThenInclude(p => p.Employee)
            .Where(c => c.HealthProfileId == healthProfileId && !c.IsDeleted)
            .OrderByDescending(c => c.DiagnosedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeHealthCondition>> GetActiveConditionsAsync(Guid healthProfileId)
    {
        return await _dbSet
            .Include(c => c.HealthProfile)
            .Where(c => c.HealthProfileId == healthProfileId
                     && !c.IsDeleted
                     && c.Status != HealthConditionStatus.Resolved)
            .OrderByDescending(c => c.DiagnosedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeHealthCondition>> GetByICDCodeAsync(string icdCode)
    {
        return await _dbSet
            .Include(c => c.HealthProfile).ThenInclude(p => p.Employee)
            .Where(c => !c.IsDeleted && c.ICDCode == icdCode)
            .OrderByDescending(c => c.DiagnosedDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// EMPLOYEE ALLERGY REPOSITORY
// ============================================================================

#region Employee Allergy Repository

public class EmployeeAllergyRepository : GenericRepository<EmployeeAllergy>, IEmployeeAllergyRepository
{
    public EmployeeAllergyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeAllergy>> GetByHealthProfileIdAsync(Guid healthProfileId)
    {
        return await _dbSet
            .Include(a => a.HealthProfile).ThenInclude(p => p.Employee)
            .Where(a => a.HealthProfileId == healthProfileId && !a.IsDeleted)
            .OrderBy(a => a.Allergen)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeAllergy>> GetActiveAllergiesAsync(Guid healthProfileId)
    {
        return await _dbSet
            .Include(a => a.HealthProfile)
            .Where(a => a.HealthProfileId == healthProfileId && a.IsActive && !a.IsDeleted)
            .OrderBy(a => a.Allergen)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// EMPLOYEE MEDICAL EXAM REPOSITORY
// ============================================================================

#region Employee Medical Exam Repository

public class EmployeeMedicalExamRepository : GenericRepository<EmployeeMedicalExam>, IEmployeeMedicalExamRepository
{
    public EmployeeMedicalExamRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeMedicalExam>> GetByHealthProfileIdAsync(Guid healthProfileId)
    {
        return await _dbSet
            .Include(e => e.HealthProfile).ThenInclude(p => p.Employee)
            .Include(e => e.Facility)
            .Include(e => e.Physician)
            .Where(e => e.HealthProfileId == healthProfileId && !e.IsDeleted)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalExam>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(e => e.HealthProfile).ThenInclude(p => p.Employee)
            .Include(e => e.Facility)
            .Include(e => e.Physician)
            .Where(e => e.FacilityId == facilityId && !e.IsDeleted)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalExam>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        return await _dbSet
            .Include(e => e.HealthProfile).ThenInclude(p => p.Employee)
            .Include(e => e.Facility)
            .Include(e => e.Physician)
            .Where(e => !e.IsDeleted && e.ExamDate >= startDate && e.ExamDate <= endDate)
            .OrderByDescending(e => e.ExamDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeMedicalExam>> GetDueForExamAsync(int daysAhead = 30)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(e => e.HealthProfile).ThenInclude(p => p.Employee)
            .Include(e => e.Facility)
            .Include(e => e.Physician)
            .Where(e => !e.IsDeleted
                     && e.NextExamDueDate != null
                     && e.NextExamDueDate <= cutoff
                     && e.NextExamDueDate >= today)
            .OrderBy(e => e.NextExamDueDate)
            .ToListAsync();
    }

    public async Task<EmployeeMedicalExam?> GetWithDocumentsAsync(Guid id)
    {
        return await _dbSet
            .Include(e => e.HealthProfile).ThenInclude(p => p.Employee)
            .Include(e => e.Facility)
            .Include(e => e.Physician)
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }
}

#endregion

// ============================================================================
// EMPLOYEE MEDICAL EXAM DOCUMENT REPOSITORY
// ============================================================================

#region Employee Medical Exam Document Repository

public class EmployeeMedicalExamDocumentRepository : GenericRepository<EmployeeMedicalExamDocument>, IEmployeeMedicalExamDocumentRepository
{
    public EmployeeMedicalExamDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeMedicalExamDocument>> GetByExamIdAsync(Guid examId)
    {
        return await _dbSet
            .Include(d => d.Exam).ThenInclude(e => e.HealthProfile).ThenInclude(p => p.Employee)
            .Where(d => d.ExamId == examId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL CLAIM PRE-AUTHORIZATION REPOSITORY
// ============================================================================

#region Medical Claim Pre-Authorization Repository

public class MedicalClaimPreAuthorizationRepository : GenericRepository<MedicalClaimPreAuthorization>, IMedicalClaimPreAuthorizationRepository
{
    public MedicalClaimPreAuthorizationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalClaimPreAuthorization?> GetByAuthorizationNumberAsync(string authorizationNumber)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Dependent)
            .Include(a => a.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Include(a => a.Approver)
            .FirstOrDefaultAsync(a => a.AuthorizationNumber == authorizationNumber && !a.IsDeleted);
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorization>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorization>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Policy)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => a.PolicyId == policyId && !a.IsDeleted)
            .OrderByDescending(a => a.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorization>> GetByStatusAsync(ClaimPreAuthorizationStatus status)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(a => a.Facility)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderByDescending(a => a.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorization>> GetPendingApprovalsAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => !a.IsDeleted
                     && (a.Status == ClaimPreAuthorizationStatus.PendingApproval
                         || a.Status == ClaimPreAuthorizationStatus.Requested))
            .OrderBy(a => a.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorization>> GetExpiringAuthorizationsAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Policy).ThenInclude(p => p.MedicalInsuranceProvider)
            .Include(a => a.Facility)
            .Where(a => !a.IsDeleted
                     && a.Status == ClaimPreAuthorizationStatus.Approved
                     && a.ExpiryDate != null
                     && a.ExpiryDate <= cutoff)
            .OrderBy(a => a.ExpiryDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL REFERRAL REPOSITORY
// ============================================================================

#region Medical Referral Repository

public class MedicalReferralRepository : GenericRepository<MedicalReferral>, IMedicalReferralRepository
{
    public MedicalReferralRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalReferral?> GetByReferralNumberAsync(string referralNumber)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Dependent)
            .Include(r => r.ReferringFacility)
            .Include(r => r.ReferringPhysician)
            .Include(r => r.ReferredToFacility)
            .Include(r => r.ReferredToPhysician)
            .FirstOrDefaultAsync(r => r.ReferralNumber == referralNumber && !r.IsDeleted);
    }

    public async Task<IEnumerable<MedicalReferral>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ReferringFacility)
            .Include(r => r.ReferredToFacility)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalReferral>> GetByStatusAsync(MedicalReferralStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ReferringFacility)
            .Include(r => r.ReferredToFacility)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.ReferralDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalReferral>> GetPendingReferralsAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ReferringFacility)
            .Include(r => r.ReferredToFacility)
            .Where(r => !r.IsDeleted
                     && (r.Status == MedicalReferralStatus.Pending
                         || r.Status == MedicalReferralStatus.Issued
                         || r.Status == MedicalReferralStatus.Accepted))
            .OrderBy(r => r.ReferralDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalReferral>> GetExpiringReferralsAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ReferringFacility)
            .Include(r => r.ReferredToFacility)
            .Where(r => !r.IsDeleted
                     && r.ExpiryDate != null
                     && r.ExpiryDate <= cutoff
                     && r.Status != MedicalReferralStatus.Completed
                     && r.Status != MedicalReferralStatus.Cancelled)
            .OrderBy(r => r.ExpiryDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL APPOINTMENT REPOSITORY
// ============================================================================

#region Medical Appointment Repository

public class MedicalAppointmentRepository : GenericRepository<MedicalAppointment>, IMedicalAppointmentRepository
{
    public MedicalAppointmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalAppointment?> GetByAppointmentNumberAsync(string appointmentNumber)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Dependent)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .FirstOrDefaultAsync(a => a.AppointmentNumber == appointmentNumber && !a.IsDeleted);
    }

    public async Task<IEnumerable<MedicalAppointment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.AppointmentDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalAppointment>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => a.FacilityId == facilityId && !a.IsDeleted)
            .OrderByDescending(a => a.AppointmentDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalAppointment>> GetByPhysicianIdAsync(Guid physicianId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => a.PhysicianId == physicianId && !a.IsDeleted)
            .OrderByDescending(a => a.AppointmentDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalAppointment>> GetByStatusAsync(MedicalAppointmentStatus status)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderByDescending(a => a.AppointmentDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalAppointment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => !a.IsDeleted
                     && a.AppointmentDateTime >= startDate
                     && a.AppointmentDateTime <= endDate)
            .OrderBy(a => a.AppointmentDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalAppointment>> GetUpcomingAppointmentsAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Include(a => a.Physician)
            .Where(a => !a.IsDeleted
                     && a.AppointmentDateTime >= now
                     && a.AppointmentDateTime <= cutoff
                     && a.Status != MedicalAppointmentStatus.Cancelled
                     && a.Status != MedicalAppointmentStatus.Completed
                     && a.Status != MedicalAppointmentStatus.NoShow)
            .OrderBy(a => a.AppointmentDateTime)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// NHIS CLAIM REPOSITORY
// ============================================================================

#region NHIS Claim Repository

public class NHISClaimRepository : GenericRepository<NHISClaim>, INHISClaimRepository
{
    public NHISClaimRepository(ApplicationDbContext context) : base(context) { }

    public async Task<NHISClaim?> GetByClaimNumberAsync(string claimNumber)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Dependent)
            .Include(c => c.Facility)
            .Include(c => c.Physician)
            .Include(c => c.LinkedMedicalClaim)
            .FirstOrDefaultAsync(c => c.ClaimNumber == claimNumber && !c.IsDeleted);
    }

    public async Task<IEnumerable<NHISClaim>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Include(c => c.Physician)
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<NHISClaim>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Include(c => c.Physician)
            .Where(c => c.FacilityId == facilityId && !c.IsDeleted)
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<NHISClaim>> GetByStatusAsync(NHISClaimStatus status)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<NHISClaim>> GetByBatchNumberAsync(string batchNumber)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => c.BatchNumber == batchNumber && !c.IsDeleted)
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<NHISClaim>> GetByLinkedMedicalClaimIdAsync(Guid medicalExpenseClaimId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Include(c => c.LinkedMedicalClaim)
            .Where(c => c.LinkedMedicalClaimId == medicalExpenseClaimId && !c.IsDeleted)
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<NHISClaim>> GetPendingClaimsAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => !c.IsDeleted
                     && (c.Status == NHISClaimStatus.Draft
                         || c.Status == NHISClaimStatus.Submitted
                         || c.Status == NHISClaimStatus.UnderReview))
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync();
    }

    public async Task<NHISClaim?> GetWithDocumentsAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Dependent)
            .Include(c => c.Facility)
            .Include(c => c.Physician)
            .Include(c => c.LinkedMedicalClaim)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }
}

#endregion

// ============================================================================
// NHIS CLAIM DOCUMENT REPOSITORY
// ============================================================================

#region NHIS Claim Document Repository

public class NHISClaimDocumentRepository : GenericRepository<NHISClaimDocument>, INHISClaimDocumentRepository
{
    public NHISClaimDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<NHISClaimDocument>> GetByNHISClaimIdAsync(Guid nhisClaimId)
    {
        return await _dbSet
            .Include(d => d.NHISClaim).ThenInclude(c => c.Employee)
            .Where(d => d.NHISClaimId == nhisClaimId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL EXPENSE CLAIM REPOSITORY
// ============================================================================

#region Medical Expense Claim Repository

public class MedicalExpenseClaimRepository : GenericRepository<MedicalExpenseClaim>, IMedicalExpenseClaimRepository
{
    public MedicalExpenseClaimRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MedicalExpenseClaim?> GetByClaimNumberAsync(string claimNumber)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Dependent)
            .Include(c => c.Facility)
            .Include(c => c.Physician)
            .Include(c => c.InsurancePolicy).ThenInclude(p => p!.MedicalInsuranceProvider)
            .FirstOrDefaultAsync(c => c.ClaimNumber == claimNumber && !c.IsDeleted);
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Include(c => c.InsurancePolicy)
            .Where(c => c.EmployeeId == employeeId && !c.IsDeleted)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetByDependentIdAsync(Guid dependentId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Dependent)
            .Include(c => c.Facility)
            .Where(c => c.DependentId == dependentId && !c.IsDeleted)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetByFacilityIdAsync(Guid facilityId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => c.FacilityId == facilityId && !c.IsDeleted)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetByStatusAsync(ClaimStatus status)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => c.Status == status && !c.IsDeleted)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetByExpenseTypeAsync(MedicalExpenseType expenseType)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => c.ExpenseType == expenseType && !c.IsDeleted)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => !c.IsDeleted && c.ClaimDate >= startDate && c.ClaimDate <= endDate)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetPendingClaimsAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => !c.IsDeleted
                     && (c.Status == ClaimStatus.Pending
                         || c.Status == ClaimStatus.Submitted
                         || c.Status == ClaimStatus.SupervisorReview
                         || c.Status == ClaimStatus.HrReview
                         || c.Status == ClaimStatus.FinanceReview
                         || c.Status == ClaimStatus.AdditionalInfoRequired))
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetFlaggedForReviewAsync()
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Where(c => c.IsFlaggedForReview && !c.IsDeleted)
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaim>> GetPendingApprovalsByApproverAsync(Guid approverId)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Facility)
            .Include(c => c.Approvals)
            .Where(c => !c.IsDeleted
                     && c.Approvals.Any(a => !a.IsDeleted
                                          && a.ApproverId == approverId
                                          && a.Status == MedicalExpenseApprovalStatus.Pending))
            .OrderByDescending(c => c.ClaimDate)
            .ToListAsync();
    }

    public async Task<MedicalExpenseClaim?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Employee)
            .Include(c => c.Dependent)
            .Include(c => c.Facility)
            .Include(c => c.Physician)
            .Include(c => c.PreAuthorization)
            .Include(c => c.Referral)
            .Include(c => c.InsurancePolicy).ThenInclude(p => p!.MedicalInsuranceProvider)
            .Include(c => c.InsurancePolicy).ThenInclude(p => p!.MedicalInsurancePlan)
            .Include(c => c.Items)
            .Include(c => c.Approvals).ThenInclude(a => a.Approver)
            .Include(c => c.Documents)
            .Include(c => c.Notes).ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }
}

#endregion

// ============================================================================
// MEDICAL EXPENSE APPROVAL REPOSITORY
// ============================================================================

#region Medical Expense Approval Repository

public class MedicalExpenseApprovalRepository : GenericRepository<MedicalExpenseApproval>, IMedicalExpenseApprovalRepository
{
    public MedicalExpenseApprovalRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalExpenseApproval>> GetByClaimIdAsync(Guid claimId)
    {
        return await _dbSet
            .Include(a => a.Claim).ThenInclude(c => c.Employee)
            .Include(a => a.Approver)
            .Where(a => a.ClaimId == claimId && !a.IsDeleted)
            .OrderByDescending(a => a.ActionDate ?? a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseApproval>> GetByApproverIdAsync(Guid approverId)
    {
        return await _dbSet
            .Include(a => a.Claim).ThenInclude(c => c.Employee)
            .Include(a => a.Approver)
            .Where(a => a.ApproverId == approverId && !a.IsDeleted)
            .OrderByDescending(a => a.ActionDate ?? a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseApproval>> GetPendingApprovalsByApproverAsync(Guid approverId)
    {
        return await _dbSet
            .Include(a => a.Claim).ThenInclude(c => c.Employee)
            .Include(a => a.Approver)
            .Where(a => a.ApproverId == approverId
                     && a.Status == MedicalExpenseApprovalStatus.Pending
                     && !a.IsDeleted)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<MedicalExpenseApproval?> GetLatestApprovalAsync(Guid claimId)
    {
        return await _dbSet
            .Include(a => a.Claim)
            .Include(a => a.Approver)
            .Where(a => a.ClaimId == claimId && !a.IsDeleted)
            .OrderByDescending(a => a.ActionDate ?? a.CreatedAt)
            .FirstOrDefaultAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL EXPENSE ITEM REPOSITORY
// ============================================================================

#region Medical Expense Item Repository

public class MedicalExpenseItemRepository : GenericRepository<MedicalExpenseItem>, IMedicalExpenseItemRepository
{
    public MedicalExpenseItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalExpenseItem>> GetByClaimIdAsync(Guid claimId)
    {
        return await _dbSet
            .Include(i => i.Claim).ThenInclude(c => c.Employee)
            .Where(i => i.ClaimId == claimId && !i.IsDeleted)
            .OrderBy(i => i.Description)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseItem>> GetByItemTypeAsync(Guid claimId, MedicalItemType itemType)
    {
        return await _dbSet
            .Include(i => i.Claim)
            .Where(i => i.ClaimId == claimId && i.ItemType == itemType && !i.IsDeleted)
            .OrderBy(i => i.Description)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL EXPENSE DOCUMENT REPOSITORY
// ============================================================================

#region Medical Expense Document Repository

public class MedicalExpenseDocumentRepository : GenericRepository<MedicalExpenseDocument>, IMedicalExpenseDocumentRepository
{
    public MedicalExpenseDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalExpenseDocument>> GetByClaimIdAsync(Guid claimId)
    {
        return await _dbSet
            .Include(d => d.Claim).ThenInclude(c => c.Employee)
            .Where(d => d.ClaimId == claimId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseDocument>> GetByDocumentTypeAsync(Guid claimId, MedicalDocumentType documentType)
    {
        return await _dbSet
            .Include(d => d.Claim)
            .Where(d => d.ClaimId == claimId && d.Type == documentType && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// MEDICAL EXPENSE CLAIM NOTE REPOSITORY
// ============================================================================

#region Medical Expense Claim Note Repository

public class MedicalExpenseClaimNoteRepository : GenericRepository<MedicalExpenseClaimNote>, IMedicalExpenseClaimNoteRepository
{
    public MedicalExpenseClaimNoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MedicalExpenseClaimNote>> GetByClaimIdAsync(Guid claimId)
    {
        return await _dbSet
            .Include(n => n.Claim).ThenInclude(c => c.Employee)
            .Include(n => n.Author)
            .Where(n => n.ClaimId == claimId && !n.IsDeleted)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaimNote>> GetInternalNotesAsync(Guid claimId)
    {
        return await _dbSet
            .Include(n => n.Author)
            .Where(n => n.ClaimId == claimId && n.IsInternal && !n.IsDeleted)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MedicalExpenseClaimNote>> GetByAuthorIdAsync(Guid authorId)
    {
        return await _dbSet
            .Include(n => n.Claim).ThenInclude(c => c.Employee)
            .Include(n => n.Author)
            .Where(n => n.AuthorId == authorId && !n.IsDeleted)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }
}

#endregion
