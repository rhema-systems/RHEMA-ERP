using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities.Procurement;

#region Business Partner Management (Unified Supplier/Contractor System)

/// <summary>
/// Unified entity for managing both suppliers and contractors
/// </summary>
public class BusinessPartner : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PartnerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PartnerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PartnerType { get; set; } = "Supplier"; // Supplier, Contractor, Both

    // Legal & Registration
    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(100)]
    public string? BusinessRegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? TaxIdentificationNumber { get; set; }

    [MaxLength(100)]
    public string? VATNumber { get; set; }

    public DateTime? RegistrationDate { get; set; }
    public DateTime? IncorporationDate { get; set; }

    // Contact Information
    [MaxLength(100)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactTitle { get; set; }

    [MaxLength(100)]
    public string? PrimaryEmail { get; set; }

    [MaxLength(50)]
    public string? PrimaryPhone { get; set; }

    [MaxLength(50)]
    public string? SecondaryPhone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    // Physical Address
    [MaxLength(500)]
    public string? PhysicalAddress { get; set; }

    [MaxLength(100)]
    public string? PhysicalCity { get; set; }

    [MaxLength(100)]
    public string? PhysicalState { get; set; }

    [MaxLength(100)]
    public string? PhysicalCountry { get; set; }

    [MaxLength(20)]
    public string? PhysicalPostalCode { get; set; }

    // Mailing Address
    [MaxLength(500)]
    public string? MailingAddress { get; set; }

    [MaxLength(100)]
    public string? MailingCity { get; set; }

    [MaxLength(100)]
    public string? MailingState { get; set; }

    [MaxLength(100)]
    public string? MailingCountry { get; set; }

    [MaxLength(20)]
    public string? MailingPostalCode { get; set; }

    // Banking Information
    [MaxLength(200)]
    public string? BankName { get; set; }

    [MaxLength(100)]
    public string? BankAccountNumber { get; set; }

    [MaxLength(200)]
    public string? BankAccountName { get; set; }

    [MaxLength(200)]
    public string? BankBranch { get; set; }

    [MaxLength(50)]
    public string? BankSwiftCode { get; set; }

    [MaxLength(100)]
    public string? BankIBAN { get; set; }

    // Classification
    [MaxLength(100)]
    public string? IndustryClassification { get; set; }

    [MaxLength(50)]
    public string? CompanySize { get; set; } // Small, Medium, Large, Enterprise

    [MaxLength(200)]
    public string? GeographicCoverage { get; set; }

    // Status & Approval
    [Required]
    [MaxLength(50)]
    public string RegistrationStatus { get; set; } = "Pending"; // Pending, UnderReview, Approved, Rejected, Suspended, Blacklisted

    [MaxLength(50)]
    public string? ApprovalStatus { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Performance & Risk
    [Column(TypeName = "decimal(3,2)")]
    public decimal? PerformanceRating { get; set; } // 0.00 to 5.00

    [MaxLength(20)]
    public string? RiskLevel { get; set; } // Low, Medium, High, Critical

    public bool IsPreferred { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public bool IsBlacklisted { get; set; } = false;

    [MaxLength(1000)]
    public string? BlacklistReason { get; set; }

    public DateTime? BlacklistDate { get; set; }
    public DateTime? BlacklistExpiryDate { get; set; }

    // Financial Health
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AnnualTurnover { get; set; }

    [MaxLength(20)]
    public string? CreditRating { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? InsuranceCoverage { get; set; }

    // Metadata
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ICollection<BusinessPartnerCategory> Categories { get; set; } = new List<BusinessPartnerCategory>();
    public virtual ICollection<BusinessPartnerSpecialization> Specializations { get; set; } = new List<BusinessPartnerSpecialization>();
    public virtual ICollection<BusinessPartnerLicense> Licenses { get; set; } = new List<BusinessPartnerLicense>();
    public virtual ICollection<BusinessPartnerContact> Contacts { get; set; } = new List<BusinessPartnerContact>();
    public virtual ICollection<BusinessPartnerDocument> Documents { get; set; } = new List<BusinessPartnerDocument>();
    public virtual ICollection<BusinessPartnerFinancial> Financials { get; set; } = new List<BusinessPartnerFinancial>();
}

/// <summary>
/// Partner category configuration (hierarchical)
/// </summary>
public class PartnerCategory : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string CategoryCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CategoryType { get; set; } = "Supplier"; // Supplier, Contractor, Both

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual PartnerCategory? ParentCategory { get; set; }
    public virtual ICollection<PartnerCategory> SubCategories { get; set; } = new List<PartnerCategory>();
    public virtual ICollection<BusinessPartnerCategory> BusinessPartnerCategories { get; set; } = new List<BusinessPartnerCategory>();
}

/// <summary>
/// Many-to-many relationship between BusinessPartner and PartnerCategory
/// </summary>
public class BusinessPartnerCategory
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsPrimary { get; set; } = false;

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual PartnerCategory Category { get; set; } = null!;
}

/// <summary>
/// Contractor specialization types (e.g., Building, Road, Electrical, etc.)
/// </summary>
public class ContractorSpecialization : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string SpecializationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string SpecializationName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool RequiresLicense { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual ICollection<BusinessPartnerSpecialization> BusinessPartnerSpecializations { get; set; } = new List<BusinessPartnerSpecialization>();
}

/// <summary>
/// Many-to-many relationship between BusinessPartner and ContractorSpecialization
/// </summary>
public class BusinessPartnerSpecialization
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid SpecializationId { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool IsPrimary { get; set; } = false;

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ContractorSpecialization Specialization { get; set; } = null!;
}

/// <summary>
/// License types configuration
/// </summary>
public class LicenseType : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string LicenseCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string LicenseName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? IssuingAuthority { get; set; }

    public int? ValidityPeriodMonths { get; set; }
    public bool IsMandatory { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual ICollection<BusinessPartnerLicense> BusinessPartnerLicenses { get; set; } = new List<BusinessPartnerLicense>();
}

/// <summary>
/// Business partner licenses and certifications
/// </summary>
public class BusinessPartnerLicense : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public Guid LicenseTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? IssuingAuthority { get; set; }

    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Expired, Suspended, Revoked

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual LicenseType LicenseType { get; set; } = null!;
}

/// <summary>
/// Multiple contacts per business partner
/// </summary>
public class BusinessPartnerContact : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactTitle { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Mobile { get; set; }

    public bool IsPrimary { get; set; } = false;

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
}

/// <summary>
/// Document management for business partners
/// </summary>
public class BusinessPartnerDocument : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty; // BusinessRegistration, TaxCertificate, Insurance, BankStatement, License, Certification, FinancialStatement, Other

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string DocumentPath { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    [MaxLength(100)]
    public string? MimeType { get; set; }

    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public bool IsVerified { get; set; } = false;
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedDate { get; set; }

    [MaxLength(1000)]
    public string? VerificationNotes { get; set; }

    public Guid? UploadedById { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? VerifiedBy { get; set; }
    public virtual ApplicationUser? UploadedBy { get; set; }
}

/// <summary>
/// Financial history for business partners
/// </summary>
public class BusinessPartnerFinancial : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public int FiscalYear { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AnnualRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? NetProfit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalAssets { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalLiabilities { get; set; }

    [MaxLength(20)]
    public string? CreditRating { get; set; }

    [MaxLength(500)]
    public string? FinancialStatementPath { get; set; }

    [MaxLength(200)]
    public string? AuditorName { get; set; }

    public DateTime? AuditDate { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
}

#endregion

#region External Registration Portal

/// <summary>
/// Tracks external business partner registration applications
/// </summary>
public class BusinessPartnerRegistration : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RegistrationNumber { get; set; } = string.Empty;

    // Applicant Information
    [Required]
    [MaxLength(200)]
    public string ApplicantName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ApplicantEmail { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ApplicantPhone { get; set; }

    [Required]
    [MaxLength(20)]
    public string PartnerType { get; set; } = "Supplier"; // Supplier, Contractor, Both

    // Status Tracking
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, UnderReview, Approved, Rejected, Cancelled

    public DateTime? SubmittedDate { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? ApprovedById { get; set; }

    // Registration Data (stored as JSON for flexibility during registration process)
    public string? RegistrationDataJson { get; set; }

    // Linked Business Partner (after approval)
    public Guid? BusinessPartnerId { get; set; }

    // Notes & Communication
    public string? ApplicantNotes { get; set; }
    public string? InternalNotes { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Navigation Properties
    public virtual ApplicationUser? ReviewedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual BusinessPartner? BusinessPartner { get; set; }
    public virtual ICollection<BusinessPartnerRegistrationDocument> Documents { get; set; } = new List<BusinessPartnerRegistrationDocument>();
    public virtual ICollection<BusinessPartnerRegistrationStatusHistory> StatusHistory { get; set; } = new List<BusinessPartnerRegistrationStatusHistory>();
}

/// <summary>
/// Documents uploaded during registration process
/// </summary>
public class BusinessPartnerRegistrationDocument : TenantEntity
{
    [Required]
    public Guid RegistrationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string DocumentPath { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    [MaxLength(100)]
    public string? MimeType { get; set; }

    public bool IsVerified { get; set; } = false;
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedDate { get; set; }

    [MaxLength(1000)]
    public string? VerificationNotes { get; set; }

    // Navigation Properties
    public virtual BusinessPartnerRegistration Registration { get; set; } = null!;
    public virtual ApplicationUser? VerifiedBy { get; set; }
}

/// <summary>
/// Status change history for registration applications
/// </summary>
public class BusinessPartnerRegistrationStatusHistory : BaseEntity
{
    public Guid RegistrationId { get; set; }

    [MaxLength(50)]
    public string? FromStatus { get; set; }

    [Required]
    [MaxLength(50)]
    public string ToStatus { get; set; } = string.Empty;

    public Guid? ChangedById { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartnerRegistration Registration { get; set; } = null!;
    public virtual ApplicationUser? ChangedBy { get; set; }
}

#endregion

