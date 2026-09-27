using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

#region Business Partner Management (Unified Supplier/Contractor/Customer System)

/// <summary>
/// Unified entity for managing suppliers, contractors, and customers (debtors)
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
    public string PartnerType { get; set; } = "Supplier"; // Supplier, Contractor, Both, Customer

    // Legal & Registration
    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(100)]
    public string? BusinessRegistrationNumber { get; set; }

    [MaxLength(100)]
    public string? TaxIdentificationNumber { get; set; }

    [MaxLength(100)]
    public string? VATNumber { get; set; }

    public bool IsVatWithholdingAgent { get; set; }

    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;

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
    /// <summary>
    /// Currency code for the business partner (e.g., USD, EUR, GBP).
    /// NOTE: This is available for all partner types (Suppliers, Contractors, Customers).
    /// It is used as the default currency for Finance Purchase Orders, Invoices, and Billing.
    /// </summary>
    [MaxLength(50)]
    public string? Currency { get; set; }

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

    // Controlled supplier ownership and compliance master data.
    [Column(TypeName = "nvarchar(max)")]
    public string? BeneficialOwnershipJson { get; set; }

    public DateTime? OwnershipVerifiedAtUtc { get; set; }

    [MaxLength(50)]
    public string? ComplianceStatus { get; set; }

    public DateTime? ComplianceReviewDateUtc { get; set; }
    public DateTime? ComplianceValidUntilUtc { get; set; }

    [MaxLength(2000)]
    public string? ComplianceNotes { get; set; }

    [NotMapped]
    public List<Guid> CategoryIds { get; set; } = new();

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

    // Customer-Specific Fields (for Debtors/Sales)
    /// <summary>
    /// Customer account number for sales/AR purposes
    /// </summary>
    [MaxLength(50)]
    public string? CustomerAccountNumber { get; set; }

    /// <summary>
    /// Customer type classification (e.g., Retail, Wholesale, Corporate, Government)
    /// </summary>
    [MaxLength(50)]
    public string? CustomerType { get; set; }

    /// <summary>
    /// Credit limit for the customer
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Current outstanding balance
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OutstandingBalance { get; set; }

    /// <summary>
    /// Legacy payment terms descriptor
    /// </summary>
    [MaxLength(50)]
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Foreign Key to standardized Payment Term configuration
    /// </summary>
    public Guid? PaymentTermId { get; set; }
    public virtual PaymentTerm? PaymentTerm { get; set; }

    // GL Defaults
    public bool SubjectToWithholdingDeduction { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal WithholdingTaxRate { get; set; }
    public Guid? DefaultWithholdingTaxId { get; set; }

    public Guid? DefaultTaxGroupId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }

    [MaxLength(20)]
    public string CashAccountSource { get; set; } = "Chequebook";

    public Guid? DefaultCashAccountId { get; set; }
    public Guid? DefaultTermsDiscountsAvailableAccountId { get; set; }
    public Guid? DefaultTermsDiscountsTakenAccountId { get; set; }
    public Guid? DefaultFinanceChargesAccountId { get; set; }
    public Guid? DefaultTradeDiscountAccountId { get; set; }
    public Guid? DefaultMiscellaneousAccountId { get; set; }
    public Guid? DefaultFreightAccountId { get; set; }
    public Guid? DefaultTaxAccountId { get; set; }
    public Guid? DefaultWriteoffAccountId { get; set; }
    public Guid? DefaultAccruedPurchasesAccountId { get; set; }
    public Guid? DefaultPurchasePriceVarianceAccountId { get; set; }

    public Guid? DefaultApAccountId { get; set; }
    public virtual Account? DefaultApAccount { get; set; }

    public Guid? DefaultArAccountId { get; set; }
    public virtual Account? DefaultArAccount { get; set; }

    // Customer posting defaults remain independent of the supplier defaults above.
    public Guid? CustomerSalesAccountId { get; set; }
    public Guid? CustomerCostOfSalesAccountId { get; set; }
    public Guid? CustomerInventoryAccountId { get; set; }
    public Guid? CustomerTermsDiscountsTakenAccountId { get; set; }
    public Guid? CustomerSalesReturnsAccountId { get; set; }
    public Guid? CustomerFinanceChargesAccountId { get; set; }
    public Guid? CustomerWriteoffAccountId { get; set; }
    public Guid? CustomerOverpaymentWriteoffAccountId { get; set; }

    public Guid? DefaultExpenseAccountId { get; set; }
    public virtual Account? DefaultExpenseAccount { get; set; }



    /// <summary>
    /// Default discount percentage for the customer
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? DefaultDiscount { get; set; }

    /// <summary>
    /// Price list/tier assigned to the customer
    /// </summary>
    [MaxLength(50)]
    public string? PriceList { get; set; }

    /// <summary>
    /// Sales representative assigned to this customer
    /// </summary>
    public Guid? SalesRepresentativeId { get; set; }

    /// <summary>
    /// Sales territory/region for the customer
    /// </summary>
    [MaxLength(100)]
    public string? SalesTerritory { get; set; }

    /// <summary>
    /// Tax exemption status
    /// </summary>
    public bool IsTaxExempt { get; set; } = false;

    /// <summary>
    /// Tax exemption certificate number
    /// </summary>
    [MaxLength(100)]
    public string? TaxExemptionNumber { get; set; }

    /// <summary>
    /// Tax exemption expiry date
    /// </summary>
    public DateTime? TaxExemptionExpiry { get; set; }

    /// <summary>
    /// Shipping method preference
    /// </summary>
    [MaxLength(100)]
    public string? PreferredShippingMethod { get; set; }

    /// <summary>
    /// Delivery instructions
    /// </summary>
    [MaxLength(500)]
    public string? DeliveryInstructions { get; set; }

    /// <summary>
    /// Customer since date
    /// </summary>
    public DateTime? CustomerSince { get; set; }

    /// <summary>
    /// Last purchase date
    /// </summary>
    public DateTime? LastPurchaseDate { get; set; }

    /// <summary>
    /// Total lifetime purchases
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalLifetimePurchases { get; set; }

    /// <summary>
    /// Average order value
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AverageOrderValue { get; set; }

    /// <summary>
    /// Customer loyalty tier (e.g., Bronze, Silver, Gold, Platinum)
    /// </summary>
    [MaxLength(50)]
    public string? LoyaltyTier { get; set; }

    /// <summary>
    /// Loyalty points balance
    /// </summary>
    public int? LoyaltyPoints { get; set; }

    /// <summary>
    /// Whether the customer is on credit hold
    /// </summary>
    public bool IsOnCreditHold { get; set; } = false;

    /// <summary>
    /// Reason for credit hold
    /// </summary>
    [MaxLength(500)]
    public string? CreditHoldReason { get; set; }

    /// <summary>
    /// Date when credit hold was applied
    /// </summary>
    public DateTime? CreditHoldDate { get; set; }

    // Metadata
    public string? Notes { get; set; }

    // Parent Business Partner (for hierarchy/categorization)
    /// <summary>
    /// Optional parent business partner for creating hierarchies or groupings
    /// </summary>
    public Guid? ParentId { get; set; }

    // User Account Link (for external portal access)
    /// <summary>
    /// Links this business partner to its primary external-portal user.
    /// Registration and approval actors remain in their dedicated audit fields.
    /// </summary>
    public Guid? UserId { get; set; }

    // Navigation Properties
    public virtual BusinessPartner? Parent { get; set; }
    public virtual ICollection<BusinessPartner> Children { get; set; } = new List<BusinessPartner>();
    public virtual ApplicationUser? User { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? SalesRepresentative { get; set; }
    public virtual ICollection<BusinessPartnerCategory> Categories { get; set; } = new List<BusinessPartnerCategory>();
    public virtual ICollection<BusinessPartnerSpecialization> Specializations { get; set; } = new List<BusinessPartnerSpecialization>();
    public virtual ICollection<BusinessPartnerLicense> Licenses { get; set; } = new List<BusinessPartnerLicense>();
    public virtual ICollection<BusinessPartnerContact> Contacts { get; set; } = new List<BusinessPartnerContact>();
    public virtual ICollection<BusinessPartnerBankAccount> BankAccounts { get; set; } = new List<BusinessPartnerBankAccount>();
    public virtual ICollection<BusinessPartnerDocument> Documents { get; set; } = new List<BusinessPartnerDocument>();
    public virtual ICollection<BusinessPartnerFinancial> Financials { get; set; } = new List<BusinessPartnerFinancial>();

    /// <summary>
    /// Canonical multi-role capabilities. PartnerType remains only as transitional source data
    /// until the approved reset removes the legacy single-choice representation.
    /// </summary>
    public virtual ICollection<BusinessPartnerRole> Roles { get; set; } = new List<BusinessPartnerRole>();
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
/// Tenant-scoped bank accounts owned by a business partner.
/// The singular banking fields on <see cref="BusinessPartner"/> remain available
/// as a compatibility summary of the primary account.
/// </summary>
public class BusinessPartnerBankAccount : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [MaxLength(200)]
    public string? BankName { get; set; }

    [MaxLength(200)]
    public string? BranchName { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    [MaxLength(100)]
    public string? AccountNumber { get; set; }

    [MaxLength(50)]
    public string? SwiftCode { get; set; }

    [MaxLength(100)]
    public string? Iban { get; set; }

    [MaxLength(50)]
    public string? Currency { get; set; }

    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

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

    [MaxLength(100)]
    public string? ApplicantEmail { get; set; }

    [MaxLength(50)]
    public string? ApplicantPhone { get; set; }

    [Required]
    [MaxLength(20)]
    public string PartnerType { get; set; } = "Supplier"; // Supplier, Contractor, Both

    public ProcurementSupplierRegistrationCategory? RegistrationCategory { get; set; }

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
    public virtual ProcurementSupplierRegistrationEvidencePackBinding? EvidencePackBinding { get; set; }
    public virtual ProcurementSupplierOnboardingToken? OnboardingToken { get; set; }
    public virtual ProcurementSupplierApplicantAccess? ApplicantAccess { get; set; }
}

/// <summary>
/// Documents uploaded during registration process
/// </summary>
public class BusinessPartnerRegistrationDocument : TenantEntity
{
    [Required]
    public Guid RegistrationId { get; set; }

    public Guid? FileUploadRecordId { get; set; }

    public Guid? CentralDocumentRecordId { get; set; }

    public Guid? CentralDocumentVersionId { get; set; }

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

    [MaxLength(50)]
    public string? EvidenceRequirementCode { get; set; }

    [MaxLength(100)]
    public string? ClassificationCode { get; set; }

    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    [MaxLength(64)]
    public string? ChecksumSha256 { get; set; }

    public bool IsVerified { get; set; } = false;
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedDate { get; set; }

    [MaxLength(1000)]
    public string? VerificationNotes { get; set; }

    public bool IsRejected { get; set; } = false;
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedDate { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Navigation Properties
    public virtual BusinessPartnerRegistration Registration { get; set; } = null!;
    public virtual FileUploadRecord? FileUploadRecord { get; set; }
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

#region Business Partner User Management

/// <summary>
/// Links multiple users to a business partner for multi-user management
/// Allows business partners to have admin users who can create and manage sub-users
/// </summary>
public class BusinessPartnerUser : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "User"; // Admin, User, Viewer

    public bool IsActive { get; set; } = true;

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public Guid? GrantedById { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser? GrantedBy { get; set; }
}

#endregion

#region Tender Assignment Management

/// <summary>
/// Manages tender assignments to business partner users
/// Allows admins to control which users can work on which tenders
/// </summary>
public class TenderAssignment : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    /// <summary>
    /// Specific user assigned to this tender (null if assignment type is AllUsers)
    /// </summary>
    public Guid? AssignedToUserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AssignmentType { get; set; } = "Self"; // AllUsers, Self, SelectedUsers

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid AssignedById { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? AssignedToUser { get; set; }
    public virtual ApplicationUser AssignedBy { get; set; } = null!;
}

#endregion

