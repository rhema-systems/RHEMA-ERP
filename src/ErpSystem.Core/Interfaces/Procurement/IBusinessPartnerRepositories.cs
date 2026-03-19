using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

// ============================================================================
// BUSINESS PARTNER REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner (Unified Supplier/Contractor) management
/// </summary>
public interface IBusinessPartnerRepository : IGenericRepository<BusinessPartner>
{
    // Basic CRUD
    new Task<BusinessPartner?> GetByIdAsync(Guid id);
    Task<BusinessPartner?> GetByCodeAsync(string partnerCode);
    Task<BusinessPartner?> GetByUserIdAsync(Guid userId);
    Task<BusinessPartner> CreateAsync(BusinessPartner partner);
    new Task<BusinessPartner> UpdateAsync(BusinessPartner partner);
    new Task DeleteAsync(Guid id);

    // Queries
    Task<PagedResult<BusinessPartner>> GetPartnersAsync(
        int page,
        int pageSize,
        string? search = null,
        string? partnerType = null,
        string? status = null,
        string? approvalStatus = null,
        bool? isPreferred = null,
        bool? isBlacklisted = null,
        List<Guid>? categoryIds = null,
        List<Guid>? specializationIds = null);

    Task<IEnumerable<BusinessPartner>> GetActivePartnersAsync(string? partnerType = null);
    Task<IEnumerable<BusinessPartner>> GetPreferredPartnersAsync(string? partnerType = null);
    Task<IEnumerable<BusinessPartner>> GetBlacklistedPartnersAsync();
    Task<IEnumerable<BusinessPartner>> GetPartnersByCategoryAsync(Guid categoryId);
    Task<IEnumerable<BusinessPartner>> GetPartnersBySpecializationAsync(Guid specializationId);
    Task<IEnumerable<BusinessPartner>> GetPartnersRequiringApprovalAsync();
    Task<IEnumerable<BusinessPartner>> GetPartnersWithExpiringLicensesAsync(int daysAhead = 30);
    Task<IEnumerable<BusinessPartner>> GetPartnersWithExpiringBlacklistAsync(int daysAhead = 30);

    // Business Logic
    Task<bool> IsPartnerCodeUniqueAsync(string partnerCode, Guid? excludeId = null);
    Task<bool> HasActiveContractsAsync(Guid partnerId);
    Task<bool> HasActivePurchaseOrdersAsync(Guid partnerId);
    Task UpdateStatusAsync(Guid partnerId, string status);
    Task UpdateApprovalStatusAsync(Guid partnerId, string approvalStatus, Guid approvedById);
    Task UpdatePerformanceRatingAsync(Guid partnerId, decimal rating);
    Task AddToBlacklistAsync(Guid partnerId, string reason, DateTime? expiryDate = null);
    Task RemoveFromBlacklistAsync(Guid partnerId);
    Task<string> GeneratePartnerCodeAsync(string partnerType);

    // Related Data
    Task<BusinessPartner?> GetWithCategoriesAsync(Guid id);
    Task<BusinessPartner?> GetWithSpecializationsAsync(Guid id);
    Task<BusinessPartner?> GetWithLicensesAsync(Guid id);
    Task<BusinessPartner?> GetWithContactsAsync(Guid id);
    Task<BusinessPartner?> GetWithDocumentsAsync(Guid id);
    Task<BusinessPartner?> GetWithFinancialsAsync(Guid id);
    Task<BusinessPartner?> GetWithAllRelatedDataAsync(Guid id);
}

// ============================================================================
// PARTNER CATEGORY REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Partner Categories (hierarchical)
/// </summary>
public interface IPartnerCategoryRepository : IGenericRepository<PartnerCategory>
{
    new Task<PartnerCategory?> GetByIdAsync(Guid id);
    Task<PartnerCategory?> GetByCodeAsync(string categoryCode);
    Task<PartnerCategory> CreateAsync(PartnerCategory category);
    new Task<PartnerCategory> UpdateAsync(PartnerCategory category);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<PartnerCategory>> GetAllCategoriesAsync(string? categoryType = null);
    Task<IEnumerable<PartnerCategory>> GetActiveCategoriesAsync(string? categoryType = null);
    Task<IEnumerable<PartnerCategory>> GetRootCategoriesAsync(string? categoryType = null);
    Task<IEnumerable<PartnerCategory>> GetSubCategoriesAsync(Guid parentCategoryId);
    Task<PartnerCategory?> GetWithSubCategoriesAsync(Guid id);
    Task<bool> IsCategoryCodeUniqueAsync(string categoryCode, Guid? excludeId = null);
    Task<bool> HasSubCategoriesAsync(Guid categoryId);
    Task<bool> HasPartnersAsync(Guid categoryId);
}

// ============================================================================
// CONTRACTOR SPECIALIZATION REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Contractor Specializations
/// </summary>
public interface IContractorSpecializationRepository : IGenericRepository<ContractorSpecialization>
{
    new Task<ContractorSpecialization?> GetByIdAsync(Guid id);
    Task<ContractorSpecialization?> GetByCodeAsync(string specializationCode);
    Task<ContractorSpecialization> CreateAsync(ContractorSpecialization specialization);
    new Task<ContractorSpecialization> UpdateAsync(ContractorSpecialization specialization);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<ContractorSpecialization>> GetAllSpecializationsAsync();
    Task<IEnumerable<ContractorSpecialization>> GetActiveSpecializationsAsync();
    Task<bool> IsSpecializationCodeUniqueAsync(string specializationCode, Guid? excludeId = null);
    Task<bool> HasContractorsAsync(Guid specializationId);
}

// ============================================================================
// LICENSE TYPE REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for License Types
/// </summary>
public interface ILicenseTypeRepository : IGenericRepository<LicenseType>
{
    new Task<LicenseType?> GetByIdAsync(Guid id);
    Task<LicenseType?> GetByCodeAsync(string licenseCode);
    Task<LicenseType> CreateAsync(LicenseType licenseType);
    new Task<LicenseType> UpdateAsync(LicenseType licenseType);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<LicenseType>> GetAllLicenseTypesAsync();
    Task<IEnumerable<LicenseType>> GetActiveLicenseTypesAsync();
    Task<IEnumerable<LicenseType>> GetMandatoryLicenseTypesAsync(string applicableTo);
    Task<IEnumerable<LicenseType>> GetLicenseTypesByApplicabilityAsync(string applicableTo);
    Task<bool> IsLicenseCodeUniqueAsync(string licenseCode, Guid? excludeId = null);
}

// ============================================================================
// BUSINESS PARTNER LICENSE REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Licenses
/// </summary>
public interface IBusinessPartnerLicenseRepository : IGenericRepository<BusinessPartnerLicense>
{
    new Task<BusinessPartnerLicense?> GetByIdAsync(Guid id);
    Task<BusinessPartnerLicense> CreateAsync(BusinessPartnerLicense license);
    new Task<BusinessPartnerLicense> UpdateAsync(BusinessPartnerLicense license);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<BusinessPartnerLicense>> GetLicensesByPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerLicense>> GetActiveLicensesByPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerLicense>> GetExpiringLicensesAsync(int daysAhead = 30);
    Task<IEnumerable<BusinessPartnerLicense>> GetExpiredLicensesAsync();
    Task<IEnumerable<BusinessPartnerLicense>> GetLicensesByTypeAsync(Guid licenseTypeId);
    Task<BusinessPartnerLicense?> GetPartnerLicenseByTypeAsync(Guid businessPartnerId, Guid licenseTypeId);
    Task<bool> HasValidLicenseAsync(Guid businessPartnerId, Guid licenseTypeId);
    Task UpdateLicenseStatusAsync(Guid licenseId, string status);
}

// ============================================================================
// BUSINESS PARTNER CONTACT REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Contacts
/// </summary>
public interface IBusinessPartnerContactRepository : IGenericRepository<BusinessPartnerContact>
{
    new Task<BusinessPartnerContact?> GetByIdAsync(Guid id);
    Task<BusinessPartnerContact> CreateAsync(BusinessPartnerContact contact);
    new Task<BusinessPartnerContact> UpdateAsync(BusinessPartnerContact contact);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<BusinessPartnerContact>> GetContactsByPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerContact>> GetActiveContactsByPartnerAsync(Guid businessPartnerId);
    Task<BusinessPartnerContact?> GetPrimaryContactAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerContact>> GetContactsByDepartmentAsync(Guid businessPartnerId, string department);
    Task SetPrimaryContactAsync(Guid businessPartnerId, Guid contactId);
}

// ============================================================================
// BUSINESS PARTNER DOCUMENT REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Documents
/// </summary>
public interface IBusinessPartnerDocumentRepository : IGenericRepository<BusinessPartnerDocument>
{
    new Task<BusinessPartnerDocument?> GetByIdAsync(Guid id);
    Task<BusinessPartnerDocument> CreateAsync(BusinessPartnerDocument document);
    new Task<BusinessPartnerDocument> UpdateAsync(BusinessPartnerDocument document);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<BusinessPartnerDocument>> GetDocumentsByPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerDocument>> GetDocumentsByTypeAsync(Guid businessPartnerId, string documentType);
    Task<IEnumerable<BusinessPartnerDocument>> GetUnverifiedDocumentsAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerDocument>> GetExpiringDocumentsAsync(int daysAhead = 30);
    Task<IEnumerable<BusinessPartnerDocument>> GetExpiredDocumentsAsync();
    Task VerifyDocumentAsync(Guid documentId, Guid verifiedById);
}

// ============================================================================
// BUSINESS PARTNER FINANCIAL REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Financial Records
/// </summary>
public interface IBusinessPartnerFinancialRepository : IGenericRepository<BusinessPartnerFinancial>
{
    new Task<BusinessPartnerFinancial?> GetByIdAsync(Guid id);
    Task<BusinessPartnerFinancial> CreateAsync(BusinessPartnerFinancial financial);
    new Task<BusinessPartnerFinancial> UpdateAsync(BusinessPartnerFinancial financial);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<BusinessPartnerFinancial>> GetFinancialsByPartnerAsync(Guid businessPartnerId);
    Task<BusinessPartnerFinancial?> GetFinancialByYearAsync(Guid businessPartnerId, int financialYear);
    Task<IEnumerable<BusinessPartnerFinancial>> GetFinancialsByYearRangeAsync(Guid businessPartnerId, int startYear, int endYear);
    Task<BusinessPartnerFinancial?> GetLatestFinancialAsync(Guid businessPartnerId);
    Task<bool> HasFinancialRecordAsync(Guid businessPartnerId, int financialYear);
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Registration (External Portal)
/// </summary>
public interface IBusinessPartnerRegistrationRepository : IGenericRepository<BusinessPartnerRegistration>
{
    new Task<BusinessPartnerRegistration?> GetByIdAsync(Guid id);
    Task<BusinessPartnerRegistration?> GetByApplicationNumberAsync(string applicationNumber);
    Task<BusinessPartnerRegistration> CreateAsync(BusinessPartnerRegistration registration);
    new Task<BusinessPartnerRegistration> UpdateAsync(BusinessPartnerRegistration registration);
    new Task DeleteAsync(Guid id);

    Task<PagedResult<BusinessPartnerRegistration>> GetRegistrationsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? partnerType = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    Task<IEnumerable<BusinessPartnerRegistration>> GetRegistrationsByStatusAsync(string status);
    Task<IEnumerable<BusinessPartnerRegistration>> GetRegistrationsByUserAsync(Guid userId);
    Task<IEnumerable<BusinessPartnerRegistration>> GetPendingReviewRegistrationsAsync();
    Task<IEnumerable<BusinessPartnerRegistration>> GetApprovedRegistrationsAsync();
    Task<IEnumerable<BusinessPartnerRegistration>> GetRejectedRegistrationsAsync();
    Task<BusinessPartnerRegistration?> GetWithDocumentsAsync(Guid id);
    Task<BusinessPartnerRegistration?> GetWithStatusHistoryAsync(Guid id);
    Task<string> GenerateApplicationNumberAsync();
    Task UpdateStatusAsync(Guid registrationId, string status, Guid? changedById = null, string? notes = null);
    Task<bool> HasPendingRegistrationAsync(string email);
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION DOCUMENT REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Registration Documents
/// </summary>
public interface IBusinessPartnerRegistrationDocumentRepository : IGenericRepository<BusinessPartnerRegistrationDocument>
{
    new Task<BusinessPartnerRegistrationDocument?> GetByIdAsync(Guid id);
    Task<BusinessPartnerRegistrationDocument> CreateAsync(BusinessPartnerRegistrationDocument document);
    new Task<BusinessPartnerRegistrationDocument> UpdateAsync(BusinessPartnerRegistrationDocument document);
    new Task DeleteAsync(Guid id);

    Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetDocumentsByRegistrationAsync(Guid registrationId);
    Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetByRegistrationIdAsync(Guid registrationId);
    Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetDocumentsByTypeAsync(Guid registrationId, string documentType);
    Task<IEnumerable<BusinessPartnerRegistrationDocument>> GetUnverifiedDocumentsAsync(Guid registrationId);
    Task VerifyDocumentAsync(Guid documentId, Guid verifiedById);
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION STATUS HISTORY REPOSITORY
// ============================================================================

/// <summary>
/// Repository interface for Business Partner Registration Status History
/// </summary>
public interface IBusinessPartnerRegistrationStatusHistoryRepository : IGenericRepository<BusinessPartnerRegistrationStatusHistory>
{
    Task<BusinessPartnerRegistrationStatusHistory> CreateAsync(BusinessPartnerRegistrationStatusHistory history);
    Task<IEnumerable<BusinessPartnerRegistrationStatusHistory>> GetHistoryByRegistrationAsync(Guid registrationId);
}

