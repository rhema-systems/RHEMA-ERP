using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

// ============================================================================
// BUSINESS PARTNER SERVICE
// ============================================================================

/// <summary>
/// Service interface for Business Partner (Unified Supplier/Contractor) management
/// </summary>
public interface IBusinessPartnerService
{
    // Basic CRUD
    Task<BusinessPartnerDetailDto?> GetByIdAsync(Guid id);
    Task<BusinessPartnerDto?> GetByCodeAsync(string partnerCode);
    Task<BusinessPartnerDetailDto?> GetByUserIdAsync(Guid userId);
    Task<BusinessPartnerDetailDto> CreateAsync(CreateBusinessPartnerDto dto);
    Task<BusinessPartnerDetailDto> UpdateAsync(Guid id, UpdateBusinessPartnerDto dto);
    Task DeleteAsync(Guid id);

    // Queries
    Task<PagedResult<BusinessPartnerDto>> GetPartnersAsync(
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

    Task<IEnumerable<BusinessPartnerDto>> GetActivePartnersAsync(string? partnerType = null);
    Task<IEnumerable<BusinessPartnerDto>> GetPreferredPartnersAsync(string? partnerType = null);
    Task<IEnumerable<BusinessPartnerDto>> GetBlacklistedPartnersAsync();
    Task<IEnumerable<BusinessPartnerDto>> GetPartnersByCategoryAsync(Guid categoryId);
    Task<IEnumerable<BusinessPartnerDto>> GetPartnersBySpecializationAsync(Guid specializationId);
    Task<IEnumerable<BusinessPartnerDto>> GetPartnersRequiringApprovalAsync();
    Task<IEnumerable<BusinessPartnerDto>> GetPartnersWithExpiringLicensesAsync(int daysAhead = 30);

    // Business Logic
    Task<bool> IsPartnerCodeUniqueAsync(string partnerCode, Guid? excludeId = null);
    Task UpdateStatusAsync(Guid partnerId, string status);
    Task SubmitPartnerForApprovalAsync(Guid partnerId, Guid submittedById);
    Task ApprovePartnerAsync(Guid partnerId, Guid approvedById, string? comments = null);
    Task RejectPartnerAsync(Guid partnerId, Guid rejectedById, string reason);
    Task UpdatePerformanceRatingAsync(Guid partnerId, decimal rating);
    Task AddToBlacklistAsync(Guid partnerId, string reason, DateTime? expiryDate = null);
    Task RemoveFromBlacklistAsync(Guid partnerId);
    Task<string> GeneratePartnerCodeAsync(string partnerType);

    // Contacts
    Task<IEnumerable<BusinessPartnerContactDto>> GetContactsAsync(Guid partnerId);
    Task<BusinessPartnerContactDto> AddContactAsync(Guid partnerId, CreateBusinessPartnerContactDto dto);
    Task<BusinessPartnerContactDto> UpdateContactAsync(Guid partnerId, Guid contactId, CreateBusinessPartnerContactDto dto);
    Task DeleteContactAsync(Guid partnerId, Guid contactId);
    Task SetPrimaryContactAsync(Guid partnerId, Guid contactId);

    // Licenses
    Task<IEnumerable<BusinessPartnerLicenseDto>> GetLicensesAsync(Guid partnerId);
    Task<BusinessPartnerLicenseDto> AddLicenseAsync(Guid partnerId, CreateBusinessPartnerLicenseDto dto);
    Task<BusinessPartnerLicenseDto> UpdateLicenseAsync(Guid partnerId, Guid licenseId, CreateBusinessPartnerLicenseDto dto);
    Task DeleteLicenseAsync(Guid partnerId, Guid licenseId);
    Task<bool> HasValidLicenseAsync(Guid partnerId, Guid licenseTypeId);

    // Documents
    Task<IEnumerable<BusinessPartnerDocumentDto>> GetDocumentsAsync(Guid partnerId);
    Task<BusinessPartnerDocumentDto> UploadDocumentAsync(Guid partnerId, CreateBusinessPartnerDocumentDto dto);
    Task DeleteDocumentAsync(Guid partnerId, Guid documentId);
    Task VerifyDocumentAsync(Guid partnerId, Guid documentId, Guid verifiedById);

    // Financials
    Task<IEnumerable<BusinessPartnerFinancialDto>> GetFinancialsAsync(Guid partnerId);
    Task<BusinessPartnerFinancialDto> AddFinancialAsync(Guid partnerId, CreateBusinessPartnerFinancialDto dto);
    Task<BusinessPartnerFinancialDto> UpdateFinancialAsync(Guid partnerId, Guid financialId, CreateBusinessPartnerFinancialDto dto);
    Task DeleteFinancialAsync(Guid partnerId, Guid financialId);
}

// ============================================================================
// PARTNER CATEGORY SERVICE
// ============================================================================

/// <summary>
/// Service interface for Partner Categories
/// </summary>
public interface IPartnerCategoryService
{
    Task<PartnerCategoryDto?> GetByIdAsync(Guid id);
    Task<PartnerCategoryDto?> GetByCodeAsync(string categoryCode);
    Task<PartnerCategoryDto> CreateAsync(CreatePartnerCategoryDto dto);
    Task<PartnerCategoryDto> UpdateAsync(Guid id, UpdatePartnerCategoryDto dto);
    Task DeleteAsync(Guid id);

    Task<IEnumerable<PartnerCategoryDto>> GetAllCategoriesAsync(string? categoryType = null);
    Task<IEnumerable<PartnerCategoryDto>> GetActiveCategoriesAsync(string? categoryType = null);
    Task<IEnumerable<PartnerCategoryDto>> GetRootCategoriesAsync(string? categoryType = null);
    Task<IEnumerable<PartnerCategoryDto>> GetSubCategoriesAsync(Guid parentCategoryId);
    Task<PartnerCategoryDto?> GetWithSubCategoriesAsync(Guid id);
    Task<bool> IsCategoryCodeUniqueAsync(string categoryCode, Guid? excludeId = null);
}

// ============================================================================
// CONTRACTOR SPECIALIZATION SERVICE
// ============================================================================

/// <summary>
/// Service interface for Contractor Specializations
/// </summary>
public interface IContractorSpecializationService
{
    Task<ContractorSpecializationDto?> GetByIdAsync(Guid id);
    Task<ContractorSpecializationDto?> GetByCodeAsync(string specializationCode);
    Task<ContractorSpecializationDto> CreateAsync(CreateContractorSpecializationDto dto);
    Task<ContractorSpecializationDto> UpdateAsync(Guid id, UpdateContractorSpecializationDto dto);
    Task DeleteAsync(Guid id);

    Task<IEnumerable<ContractorSpecializationDto>> GetAllSpecializationsAsync();
    Task<IEnumerable<ContractorSpecializationDto>> GetActiveSpecializationsAsync();
    Task<bool> IsSpecializationCodeUniqueAsync(string specializationCode, Guid? excludeId = null);
}

// ============================================================================
// LICENSE TYPE SERVICE
// ============================================================================

/// <summary>
/// Service interface for License Types
/// </summary>
public interface ILicenseTypeService
{
    Task<LicenseTypeDto?> GetByIdAsync(Guid id);
    Task<LicenseTypeDto?> GetByCodeAsync(string licenseCode);
    Task<LicenseTypeDto> CreateAsync(CreateLicenseTypeDto dto);
    Task<LicenseTypeDto> UpdateAsync(Guid id, UpdateLicenseTypeDto dto);
    Task DeleteAsync(Guid id);

    Task<IEnumerable<LicenseTypeDto>> GetAllLicenseTypesAsync();
    Task<IEnumerable<LicenseTypeDto>> GetActiveLicenseTypesAsync();
    Task<IEnumerable<LicenseTypeDto>> GetMandatoryLicenseTypesAsync(string applicableTo);
    Task<IEnumerable<LicenseTypeDto>> GetLicenseTypesByApplicabilityAsync(string applicableTo);
    Task<bool> IsLicenseCodeUniqueAsync(string licenseCode, Guid? excludeId = null);
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION SERVICE (External Portal)
// ============================================================================

/// <summary>
/// Service interface for Business Partner Registration (External Portal)
/// </summary>
public interface IBusinessPartnerRegistrationService
{
    // External User Operations (for portal)
    Task<BusinessPartnerRegistrationDetailDto?> GetByIdAsync(Guid id);
    Task<BusinessPartnerRegistrationDetailDto?> GetByApplicationNumberAsync(string applicationNumber);
    Task<BusinessPartnerRegistrationDetailDto> CreateAsync(CreateBusinessPartnerRegistrationDto dto, Guid userId);
    Task<BusinessPartnerRegistrationDetailDto> UpdateAsync(Guid id, UpdateBusinessPartnerRegistrationDto dto, Guid userId);
    Task DeleteAsync(Guid id, Guid userId);
    Task<IEnumerable<BusinessPartnerRegistrationDto>> GetMyRegistrationsAsync(Guid userId);
    Task SubmitForReviewAsync(Guid id, Guid userId);
    Task SubmitExternalApplicantForReviewAsync(Guid id, Guid applicantActorId);

    // Debug
    Task<IEnumerable<BusinessPartnerRegistrationDto>> GetAllRegistrationsForDebugAsync();

    // Internal Admin Operations
    Task<PagedResult<BusinessPartnerRegistrationDto>> GetRegistrationsAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        string? partnerType = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    Task<IEnumerable<BusinessPartnerRegistrationDto>> GetPendingReviewRegistrationsAsync();
    Task<IEnumerable<BusinessPartnerRegistrationDto>> GetApprovedRegistrationsAsync();
    Task<IEnumerable<BusinessPartnerRegistrationDto>> GetRejectedRegistrationsAsync();

    // Review & Approval
    Task ReviewRegistrationAsync(ReviewBusinessPartnerRegistrationDto dto, Guid reviewedById);
    Task ApproveRegistrationAsync(Guid id, Guid approvedById, string? notes = null);
    Task RejectRegistrationAsync(Guid id, Guid rejectedById, string reason);
    Task RequestMoreInfoAsync(Guid id, Guid requestedById, string notes);

    // Document Management
    Task<IEnumerable<BusinessPartnerRegistrationDocumentDto>> GetDocumentsAsync(Guid registrationId);
    Task<IEnumerable<BusinessPartnerRegistrationDocumentDto>> GetDocumentsForInternalReviewAsync(Guid registrationId, Guid userId);
    Task<BusinessPartnerRegistrationDocumentDto?> GetDocumentByIdAsync(Guid registrationId, Guid documentId);
    Task<BusinessPartnerRegistrationDocumentDto?> GetDocumentForInternalDownloadAsync(Guid registrationId, Guid documentId, Guid userId);
    Task<BusinessPartnerRegistrationDocumentDto> UploadDocumentAsync(Guid registrationId, CreateBusinessPartnerDocumentDto dto, Guid userId);
    Task DeleteDocumentAsync(Guid registrationId, Guid documentId, Guid userId);
    Task VerifyDocumentAsync(Guid registrationId, Guid documentId, Guid verifiedById);
    Task RejectDocumentAsync(Guid registrationId, Guid documentId, Guid rejectedById, string reason);
    Task RevertDocumentRejectionAsync(Guid registrationId, Guid documentId, Guid userId);
    Task TrackDocumentDownloadAsync(Guid registrationId, Guid documentId, Guid userId);

    // Status History
    Task<IEnumerable<BusinessPartnerRegistrationStatusHistoryDto>> GetStatusHistoryAsync(Guid registrationId);

    // Conversion
    Task<BusinessPartnerDetailDto> ConvertToBusinessPartnerAsync(Guid registrationId, Guid approvedById);
}

// ============================================================================
// PARTNER VERIFICATION SERVICE
// ============================================================================

/// <summary>
/// Service interface for Partner Verification operations
/// </summary>
public interface IPartnerVerificationService
{
    Task<bool> VerifyPartnerAsync(Guid partnerId, Guid verifiedById);
    Task<bool> VerifyLicenseAsync(Guid partnerId, Guid licenseId, Guid verifiedById);
    Task<bool> VerifyDocumentAsync(Guid partnerId, Guid documentId, Guid verifiedById);
    Task<bool> VerifyFinancialAsync(Guid partnerId, Guid financialId, Guid verifiedById);
    Task<IEnumerable<BusinessPartnerDto>> GetUnverifiedPartnersAsync();
    Task<IEnumerable<BusinessPartnerLicenseDto>> GetUnverifiedLicensesAsync(Guid partnerId);
    Task<IEnumerable<BusinessPartnerDocumentDto>> GetUnverifiedDocumentsAsync(Guid partnerId);
}

// ============================================================================
// PARTNER PERFORMANCE SERVICE
// ============================================================================

/// <summary>
/// Service interface for Partner Performance tracking
/// </summary>
public interface IPartnerPerformanceService
{
    Task UpdatePerformanceRatingAsync(Guid partnerId, decimal rating);
    Task UpdateQualityScoreAsync(Guid partnerId, int score);
    Task UpdateDeliveryScoreAsync(Guid partnerId, int score);
    Task UpdateComplianceScoreAsync(Guid partnerId, int score);
    Task RecordPerformanceReviewAsync(Guid partnerId, Guid reviewedById);
    Task<BusinessPartnerDetailDto?> GetPerformanceMetricsAsync(Guid partnerId);
}

// ============================================================================
// PARTNER BLACKLIST SERVICE
// ============================================================================

/// <summary>
/// Service interface for Partner Blacklist management
/// </summary>
public interface IPartnerBlacklistService
{
    Task AddToBlacklistAsync(Guid partnerId, string reason, Guid addedById, DateTime? expiryDate = null);
    Task RemoveFromBlacklistAsync(Guid partnerId, Guid removedById);
    Task<IEnumerable<BusinessPartnerDto>> GetBlacklistedPartnersAsync();
    Task<IEnumerable<BusinessPartnerDto>> GetPartnersWithExpiringBlacklistAsync(int daysAhead = 30);
    Task<bool> IsBlacklistedAsync(Guid partnerId);
}

// ============================================================================
// BLACKLIST APPEAL SERVICE
// ============================================================================

/// <summary>
/// Service interface for Blacklist Appeal management
/// </summary>
public interface IBlacklistAppealService
{
    Task<BlacklistAppealDto?> GetByIdAsync(Guid id);
    Task<BlacklistAppealDto?> GetByAppealNumberAsync(string appealNumber);
    Task<IEnumerable<BlacklistAppealDto>> GetByBusinessPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<BlacklistAppealDto>> GetByStatusAsync(string status);
    Task<IEnumerable<BlacklistAppealDto>> GetPendingAppealsAsync();
    Task<BlacklistAppealDto> CreateAppealAsync(CreateBlacklistAppealDto createDto);
    Task<BlacklistAppealDto> ReviewAppealAsync(Guid appealId, ReviewBlacklistAppealDto reviewDto);
    Task<BlacklistAppealDto> ApproveAppealAsync(Guid appealId, ApproveBlacklistAppealDto approveDto);
    Task<BlacklistAppealDto> RejectAppealAsync(Guid appealId, RejectBlacklistAppealDto rejectDto);
    Task DeleteAppealAsync(Guid id);
}

/// <summary>
/// Service interface for Blacklist History management
/// </summary>
public interface IBlacklistHistoryService
{
    Task<IEnumerable<BlacklistHistoryDto>> GetByBusinessPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<BlacklistHistoryDto>> GetByActionAsync(string action);
    Task<IEnumerable<BlacklistHistoryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task RecordHistoryAsync(Guid businessPartnerId, string action, string? reason = null, Guid? relatedAppealId = null, string? notes = null);
}

