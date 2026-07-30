using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Validation;

namespace ErpSystem.Core.DTOs.Procurement;

// ============================================================================
// BUSINESS PARTNER DTOs
// ============================================================================

/// <summary>
/// DTO for Business Partner list view
/// </summary>
public class BusinessPartnerDto
{
    public Guid Id { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = "Supplier"; // Supplier, Contractor, Both, Customer
    public string? TradingName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string Status { get; set; } = "Active";
    public string? ApprovalStatus { get; set; }
    public decimal? PerformanceRating { get; set; }
    public string? RiskLevel { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsActive { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? Currency { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> Specializations { get; set; } = new();
    
    // Customer-specific fields for list view
    public string? CustomerType { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public bool IsOnCreditHold { get; set; }
    public Guid? PaymentTermId { get; set; }
    
    // Parent Business Partner
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }

    // Workflow display helpers (optional)
    public string? CurrentWorkflowStepName { get; set; }
}

/// <summary>
/// DTO for detailed Business Partner view
/// </summary>
public class BusinessPartnerDetailDto : BusinessPartnerDto
{
    // User Account Link
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }

    // Legal & Registration Information
    public string? LegalEntityType { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? RegistrationCountry { get; set; }
    public new string? VatNumber { get; set; }

    // Contact Information
    public string? ContactPerson { get; set; }
    public string? ContactTitle { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactMobile { get; set; }
    public new string? AlternatePhone { get; set; }
    public string? Fax { get; set; }

    // Address Information
    public string? MailingAddress { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingPostalCode { get; set; }
    public string? MailingCountry { get; set; }
    public string? PhysicalState { get; set; }
    public string? PhysicalPostalCode { get; set; }

    // Banking Information
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? SwiftCode { get; set; }
    public string? Iban { get; set; }
    public string? Currency { get; set; }

    // Classification
    public string? IndustryType { get; set; }
    public string? CompanySize { get; set; }
    public int? NumberOfEmployees { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? YearsInBusiness { get; set; }
    public string? GeographicCoverage { get; set; }

    // Performance & Status
    public int? QualityScore { get; set; }
    public int? DeliveryScore { get; set; }
    public int? ComplianceScore { get; set; }
    public DateTime? LastPerformanceReview { get; set; }
    public DateTime? NextPerformanceReview { get; set; }
    public string? BlacklistReason { get; set; }
    public DateTime? BlacklistDate { get; set; }
    public DateTime? BlacklistExpiryDate { get; set; }
    public string? ComplianceStatus { get; set; }
    public DateTime? ComplianceReviewDateUtc { get; set; }
    public DateTime? ComplianceValidUntilUtc { get; set; }
    public string? ComplianceNotes { get; set; }

    // Contractor-Specific Fields
    public string? ContractorLicenseNumber { get; set; }
    public DateTime? ContractorLicenseExpiry { get; set; }
    public string? ContractorGrade { get; set; }
    public decimal? MaxProjectValue { get; set; }
    public int? MaxConcurrentProjects { get; set; }
    public int? TechnicalStaffCount { get; set; }
    public int? EquipmentCount { get; set; }
    public bool? HasQualityManagementSystem { get; set; }
    public bool? HasSafetyManagementSystem { get; set; }
    public string? InsuranceProvider { get; set; }
    public decimal? InsuranceCoverageAmount { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }

    // Customer-Specific Fields (for Debtors/Sales)
    public string? CustomerAccountNumber { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? DefaultDiscount { get; set; }
    public string? PriceList { get; set; }
    public Guid? SalesRepresentativeId { get; set; }
    public string? SalesRepresentativeName { get; set; }
    public string? SalesTerritory { get; set; }
    public bool IsTaxExempt { get; set; }
    public string? TaxExemptionNumber { get; set; }
    public DateTime? TaxExemptionExpiry { get; set; }
    public string? PreferredShippingMethod { get; set; }
    public string? DeliveryInstructions { get; set; }
    public DateTime? CustomerSince { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public decimal? TotalLifetimePurchases { get; set; }
    public decimal? AverageOrderValue { get; set; }
    public string? LoyaltyTier { get; set; }
    public int? LoyaltyPoints { get; set; }
    public string? CreditHoldReason { get; set; }
    public DateTime? CreditHoldDate { get; set; }

    // Approval Information
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Additional Information
    public string? Notes { get; set; }
    public string? Tags { get; set; }

    // Related Data
    public List<BusinessPartnerContactDto> Contacts { get; set; } = new();
    public List<BusinessPartnerLicenseDto> Licenses { get; set; } = new();
    public List<BusinessPartnerDocumentDto> Documents { get; set; } = new();
    public List<BusinessPartnerFinancialDto> FinancialRecords { get; set; } = new();
    public List<BusinessPartnerFinancialDto> FinancialInfo { get; set; } = new(); // Alias for FinancialRecords
}

/// <summary>
/// DTO for creating a new Business Partner
/// </summary>
public class CreateBusinessPartnerDto
{
    [Required]
    [MaxLength(200)]
    public string PartnerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PartnerType { get; set; } = "Supplier"; // Supplier, Contractor, Both, Customer

    [MaxLength(200)]
    public string? TradingName { get; set; }

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? TaxNumber { get; set; }

    // Validates email format only when not empty
    [OptionalEmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    // Validates phone format only when not empty
    [OptionalPhone]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? PhysicalAddress { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(50)]
    public string? Currency { get; set; }

    public List<Guid> CategoryIds { get; set; } = new();
    public List<Guid> SpecializationIds { get; set; } = new();

    // Customer-Specific Fields (for Debtors/Sales)
    [MaxLength(50)]
    public string? CustomerType { get; set; } // Retail, Wholesale, Corporate, Government

    public decimal? CreditLimit { get; set; }

    [MaxLength(50)]
    public string? PaymentTerms { get; set; } // Net 30, Net 60, COD, etc.

    public Guid? PaymentTermId { get; set; }


    public decimal? DefaultDiscount { get; set; }

    [MaxLength(50)]
    public string? PriceList { get; set; }

    public Guid? SalesRepresentativeId { get; set; }

    [MaxLength(100)]
    public string? SalesTerritory { get; set; }

    public bool IsTaxExempt { get; set; }

    [MaxLength(100)]
    public string? TaxExemptionNumber { get; set; }

    public DateTime? TaxExemptionExpiry { get; set; }

    [MaxLength(100)]
    public string? PreferredShippingMethod { get; set; }

    [MaxLength(500)]
    public string? DeliveryInstructions { get; set; }

    public DateTime? CustomerSince { get; set; }

    [MaxLength(50)]
    public string? LoyaltyTier { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Optional parent business partner ID for hierarchy/categorization
    /// </summary>
    public Guid? ParentId { get; set; }
}

/// <summary>
/// DTO for updating a Business Partner
/// </summary>
public class UpdateBusinessPartnerDto
{
    [Required]
    [MaxLength(200)]
    public string PartnerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TradingName { get; set; }

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? TaxNumber { get; set; }

    // Validates email format only when not empty
    [OptionalEmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    // Validates phone format only when not empty
    [OptionalPhone]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? PhysicalAddress { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(50)]
    public string? Currency { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Active";

    public bool IsPreferred { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<Guid> CategoryIds { get; set; } = new();
    public List<Guid> SpecializationIds { get; set; } = new();

    // Customer-Specific Fields (for Debtors/Sales)
    [MaxLength(50)]
    public string? CustomerType { get; set; } // Retail, Wholesale, Corporate, Government

    public decimal? CreditLimit { get; set; }

    [MaxLength(50)]
    public string? PaymentTerms { get; set; } // Net 30, Net 60, COD, etc.

    public Guid? PaymentTermId { get; set; }


    public decimal? DefaultDiscount { get; set; }

    [MaxLength(50)]
    public string? PriceList { get; set; }

    public Guid? SalesRepresentativeId { get; set; }

    [MaxLength(100)]
    public string? SalesTerritory { get; set; }

    public bool IsTaxExempt { get; set; }

    [MaxLength(100)]
    public string? TaxExemptionNumber { get; set; }

    public DateTime? TaxExemptionExpiry { get; set; }

    [MaxLength(100)]
    public string? PreferredShippingMethod { get; set; }

    [MaxLength(500)]
    public string? DeliveryInstructions { get; set; }

    [MaxLength(50)]
    public string? LoyaltyTier { get; set; }

    public bool IsOnCreditHold { get; set; }

    [MaxLength(500)]
    public string? CreditHoldReason { get; set; }

    public DateTime? CreditHoldDate { get; set; }
    
    /// <summary>
    /// Optional parent business partner ID for hierarchy/categorization
    /// </summary>
    public Guid? ParentId { get; set; }
}

// ============================================================================
// PARTNER CATEGORY DTOs
// ============================================================================

public class PartnerCategoryDto
{
    public Guid Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryType { get; set; } = "Supplier"; // Supplier, Contractor, Both
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public List<PartnerCategoryDto> SubCategories { get; set; } = new();
}

public class CreatePartnerCategoryDto
{
    [Required]
    [MaxLength(50)]
    public string CategoryCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(20)]
    public string CategoryType { get; set; } = "Supplier";

    public Guid? ParentCategoryId { get; set; }
    public int DisplayOrder { get; set; }
}

public class UpdatePartnerCategoryDto
{
    [Required]
    [MaxLength(200)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

// ============================================================================
// CONTRACTOR SPECIALIZATION DTOs
// ============================================================================

public class ContractorSpecializationDto
{
    public Guid Id { get; set; }
    public string SpecializationCode { get; set; } = string.Empty;
    public string SpecializationName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateContractorSpecializationDto
{
    [Required]
    [MaxLength(50)]
    public string SpecializationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string SpecializationName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }
}

public class UpdateContractorSpecializationDto
{
    [Required]
    [MaxLength(200)]
    public string SpecializationName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

// ============================================================================
// LICENSE TYPE DTOs
// ============================================================================

public class LicenseTypeDto
{
    public Guid Id { get; set; }
    public string LicenseCode { get; set; } = string.Empty;
    public string LicenseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ApplicableTo { get; set; } = "Both"; // Supplier, Contractor, Both
    public bool IsMandatory { get; set; }
    public int? ValidityPeriodMonths { get; set; }
    public bool RequiresRenewal { get; set; }
    public int? RenewalReminderDays { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLicenseTypeDto
{
    [Required]
    [MaxLength(50)]
    public string LicenseCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string LicenseName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(20)]
    public string ApplicableTo { get; set; } = "Both";

    public bool IsMandatory { get; set; }
    public int? ValidityPeriodMonths { get; set; }
    public bool RequiresRenewal { get; set; }
    public int? RenewalReminderDays { get; set; }
}

public class UpdateLicenseTypeDto
{
    [Required]
    [MaxLength(200)]
    public string LicenseName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsMandatory { get; set; }
    public int? ValidityPeriodMonths { get; set; }
    public bool RequiresRenewal { get; set; }
    public int? RenewalReminderDays { get; set; }
    public bool IsActive { get; set; }
}

// ============================================================================
// BUSINESS PARTNER LICENSE DTOs
// ============================================================================

public class BusinessPartnerLicenseDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid LicenseTypeId { get; set; }
    public string LicenseTypeName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string IssuingAuthority { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public bool IsExpired { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public string? FilePath { get; set; }
    public string? VerificationNotes { get; set; }
}

public class CreateBusinessPartnerLicenseDto
{
    [Required]
    public Guid LicenseTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    [Required]
    public DateTime IssueDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [Required]
    [MaxLength(200)]
    public string IssuingAuthority { get; set; } = string.Empty;
}

// ============================================================================
// BUSINESS PARTNER CONTACT DTOs
// ============================================================================

public class BusinessPartnerContactDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? ContactTitle { get; set; } // Alias for Title
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
}

public class CreateBusinessPartnerContactDto
{
    [Required]
    [MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Title { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Phone]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Phone]
    [MaxLength(50)]
    public string? Mobile { get; set; }

    public bool IsPrimary { get; set; }
}

// ============================================================================
// BUSINESS PARTNER DOCUMENT DTOs
// ============================================================================

public class BusinessPartnerDocumentDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? VerificationNotes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class CreateBusinessPartnerDocumentDto
{
    [Required]
    public Guid? FileUploadRecordId { get; set; }

    [Required]
    public Guid? CentralDocumentRecordId { get; set; }

    [Required]
    public Guid? CentralDocumentVersionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DocumentNumber { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DocumentPath { get; set; } // Alias for FilePath

    public long FileSize { get; set; }

    [MaxLength(100)]
    public string? MimeType { get; set; }

    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(50)]
    public string? EvidenceRequirementCode { get; set; }

    [MaxLength(100)]
    public string? ClassificationCode { get; set; }

    [MaxLength(64)]
    public string? ChecksumSha256 { get; set; }
}

// ============================================================================
// BUSINESS PARTNER FINANCIAL DTOs
// ============================================================================

public class BusinessPartnerFinancialDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public int FinancialYear { get; set; }
    public int FiscalYear { get; set; } // Alias for FinancialYear
    public decimal? Revenue { get; set; }
    public decimal? AnnualRevenue { get; set; } // Alias for Revenue
    public decimal? Profit { get; set; }
    public decimal? NetProfit { get; set; } // Alias for Profit
    public decimal? Assets { get; set; }
    public decimal? TotalAssets { get; set; } // Alias for Assets
    public decimal? Liabilities { get; set; }
    public decimal? TotalLiabilities { get; set; } // Alias for Liabilities
    public decimal? Equity { get; set; }
    public string? Currency { get; set; }
    public bool IsAudited { get; set; }
    public string? AuditorName { get; set; }
    public DateTime? AuditDate { get; set; }
    public string? CreditRating { get; set; }
    public string? FinancialStatementPath { get; set; }
}

public class CreateBusinessPartnerFinancialDto
{
    [Required]
    [Range(2000, 2100)]
    public int FinancialYear { get; set; }

    public decimal? Revenue { get; set; }
    public decimal? Profit { get; set; }
    public decimal? Assets { get; set; }
    public decimal? Liabilities { get; set; }
    public decimal? Equity { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    public bool IsAudited { get; set; }

    [MaxLength(200)]
    public string? AuditorName { get; set; }
}

// ============================================================================
// BUSINESS PARTNER REGISTRATION DTOs (External Portal)
// ============================================================================

public class BusinessPartnerRegistrationDto
{
    public Guid Id { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string PartnerType { get; set; } = "Supplier";
    public ProcurementSupplierRegistrationCategory? RegistrationCategory { get; set; }
    public string Status { get; set; } = "Draft";
    public string CompanyName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public int CompletionPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BusinessPartnerRegistrationDetailDto : BusinessPartnerRegistrationDto
{
    public string? RegistrationData { get; set; } // JSON data
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }

    // Parsed fields from RegistrationData JSON
    public string? TradingName { get; set; }
    public string? TaxNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? Website { get; set; }
    public string? IndustryType { get; set; }
    public int? YearsInBusiness { get; set; }
    public int? NumberOfEmployees { get; set; }
    public decimal? AnnualRevenue { get; set; }

    // Contact Information
    public string? AlternatePhone { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }

    // Primary Contact Person
    public string? ContactPersonName { get; set; }
    public string? ContactPersonTitle { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? ContactPersonPhone { get; set; }

    // Banking Information
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }

    public List<BusinessPartnerRegistrationDocumentDto> Documents { get; set; } = new();
    public List<BusinessPartnerRegistrationStatusHistoryDto> StatusHistory { get; set; } = new();
    public ProcurementSupplierEvidenceReadinessDto? EvidenceReadiness { get; set; }
}

public class CreateBusinessPartnerRegistrationDto
{
    [Required]
    [MaxLength(20)]
    public string PartnerType { get; set; } = "Supplier";

    public ProcurementSupplierRegistrationCategory? RegistrationCategory { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? TaxNumber { get; set; }

    [MaxLength(100)]
    public string? VatNumber { get; set; }

    // Email validation only if provided (not null or empty) - removed validation for draft saves
    [MaxLength(200)]
    public string? Email { get; set; }

    // Phone validation only if provided (not null or empty) - removed validation for draft saves
    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(500)]
    public string? PhysicalAddress { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? ContactPersonName { get; set; }

    public string? RegistrationData { get; set; } // JSON data

    // License Information (for contractors)
    public List<BusinessPartnerRegistrationLicenseDto>? Licenses { get; set; }
}

/// <summary>
/// License information for business partner registration
/// </summary>
public class BusinessPartnerRegistrationLicenseDto
{
    [Required]
    public string LicenseTypeId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    [Required]
    public string IssueDate { get; set; } = string.Empty;

    public string? ExpiryDate { get; set; }

    [Required]
    [MaxLength(200)]
    public string IssuingAuthority { get; set; } = string.Empty;
}

public class UpdateBusinessPartnerRegistrationDto
{
    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public ProcurementSupplierRegistrationCategory? RegistrationCategory { get; set; }

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    // Email validation only if provided (not null or empty) - removed validation for draft saves
    [MaxLength(200)]
    public string? Email { get; set; }

    // Phone validation only if provided (not null or empty) - removed validation for draft saves
    [MaxLength(50)]
    public string? Phone { get; set; }

    public string? RegistrationData { get; set; } // JSON data
    public int CompletionPercentage { get; set; }
}

public class SubmitBusinessPartnerRegistrationDto
{
    [Required]
    public Guid RegistrationId { get; set; }
}

public class ReviewBusinessPartnerRegistrationDto
{
    [Required]
    public Guid RegistrationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Approve, Reject, RequestMoreInfo

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? ReviewNotes { get; set; } // Alias for Notes

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

public class BusinessPartnerRegistrationDocumentDto
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    public FileVirusScanStatus? VirusScanStatus { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? DocumentPath { get; set; } // Alias for FilePath
    [JsonIgnore]
    public string? InternalStoragePath { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? EvidenceRequirementCode { get; set; }
    public string? ClassificationCode { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? ChecksumSha256 { get; set; }
    public bool IsVerified { get; set; }
    public bool IsRejected { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? RejectedDate { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class BusinessPartnerRegistrationStatusHistoryDto
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}

