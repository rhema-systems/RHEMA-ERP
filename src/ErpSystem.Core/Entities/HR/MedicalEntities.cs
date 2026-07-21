using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Medical;

/// <summary>
/// Healthcare facility/hospital details
/// </summary>
public class HealthcareFacility : TenantEntity
{
    [Required]
    [MaxLength(300)]
    public string FacilityName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortName { get; set; }

    [Required]
    [MaxLength(50)]
    public string FacilityCode { get; set; } = string.Empty;

    [Required]
    public HealthFacilityType FacilityType { get; set; }

    [MaxLength(100)]
    public string? LicenseNumber { get; set; }

    public DateTime? LicenseExpiryDate { get; set; }

    // Contact Information
    [Required]
    [MaxLength(500)]
    public string PhysicalAddress { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(70)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [MaxLength(50)]
    public string? PrimaryPhone { get; set; }

    [MaxLength(50)]
    public string? EmergencyPhone { get; set; }

    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(255)]
    [Url]
    public string? Website { get; set; }

    // Facility Capabilities
    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool HasEmergencyServices { get; set; }

    public bool Has24HourService { get; set; }

    public bool HasAmbulanceService { get; set; }

    public bool HasLaboratory { get; set; }

    public bool HasPharmacy { get; set; }

    // Accreditation
    [MaxLength(200)]
    public string? AccreditationBody { get; set; }

    [MaxLength(100)]
    public string? AccreditationNumber { get; set; }

    public DateTime? AccreditationDate { get; set; }

    public DateTime? AccreditationExpiryDate { get; set; }

    // Insurance Acceptance (network providers via MedicalInsuranceProviderFacility)
    public bool AcceptsNHIS { get; set; }
	
    [MaxLength(100)]
    public string? NHISAccreditationNumber { get; set; }

    // Financial
    public Guid? BankId { get; set; }
    
    [ForeignKey(nameof(BankId))]
    public virtual EmployeeBank? Bank { get; set; }

    public Guid? BranchId { get; set; }

    [ForeignKey(nameof(BranchId))]
    public virtual EmployeeBankBranch? Branch { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    // Contact Person
    [MaxLength(200)]
    public string? ContactPersonName { get; set; }

    [MaxLength(20)]
    public string? ContactPersonTitle { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? ContactPersonPhone { get; set; }

    [MaxLength(255)]
    [EmailAddress]
    public string? ContactPersonEmail { get; set; }

    // Operating Hours
    [MaxLength(100)]
    public string? OperatingHours { get; set; }

    [MaxLength(500)]
    public string? OperatingDays { get; set; }

    // Status
    public bool IsActive { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Relations
    public virtual ICollection<Physician> Physicians { get; set; } = new List<Physician>();
    public virtual ICollection<FacilityService> Services { get; set; } = new List<FacilityService>();
    public virtual ICollection<MedicalInsuranceProviderFacility> ProviderFacilities { get; set; } = new List<MedicalInsuranceProviderFacility>();
    public virtual ICollection<MedicalAppointment> Appointments { get; set; } = new List<MedicalAppointment>();
    public virtual ICollection<MedicalExpenseClaim> ExpenseClaims { get; set; } = new List<MedicalExpenseClaim>();
}

/// <summary>
/// Physician/Doctor details
/// </summary>
public class Physician : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? MiddleName { get; set; }

    [MaxLength(20)]
    public string? Title { get; set; }
	
    // Specialization
    [MaxLength(200)]
    public string? Specialization { get; set; }
	
    [MaxLength(100)]
    public string? MedicalLicenseNumber { get; set; }
 
    public DateTime? LicenseExpiryDate { get; set; }

    // Contact Information
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    // Primary Facility
    public Guid? FacilityId { get; set; }

    [ForeignKey(nameof(FacilityId))]
    [InverseProperty(nameof(HealthcareFacility.Physicians))]
    public virtual HealthcareFacility? Facility { get; set; }

    // Status
    public bool IsVerified { get; set; }

    public DateTime? VerificationDate { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
	
    // Relations
    public virtual ICollection<MedicalReferral> IssuedReferrals { get; set; } = new List<MedicalReferral>();
    public virtual ICollection<MedicalReferral> ReceivedReferrals { get; set; } = new List<MedicalReferral>();
    public virtual ICollection<MedicalExpenseClaim> ExpenseClaims { get; set; } = new List<MedicalExpenseClaim>();
    public virtual ICollection<MedicalAppointment> Appointments { get; set; } = new List<MedicalAppointment>();
}

/// <summary>
/// Services offered by a facility
/// </summary>
public class FacilityService : TenantEntity
{
    [Required]
    public Guid FacilityId { get; set; }

    [ForeignKey(nameof(FacilityId))]
    [InverseProperty(nameof(HealthcareFacility.Services))]
    public virtual HealthcareFacility Facility { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public MedicalServiceType ServiceType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedCost { get; set; }

    public bool RequiresAppointment { get; set; }

    public bool IsEmergencyService { get; set; }
	
	public bool RequiresPreAuthorization { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Insurance provider/company details
/// </summary>
public class MedicalInsuranceProvider : TenantEntity
{
    [Required]
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortName { get; set; }

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public MedicalInsuranceProviderType ProviderType { get; set; }

    // Licensing & Registration
    [Required]
    [MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;

    public DateTime? LicenseExpiryDate { get; set; }

    // Contact Information
    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [Required]
    [MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ClaimsHotline { get; set; }

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    [EmailAddress]
    public string? ClaimsEmail { get; set; }

    [MaxLength(255)]
    [Url]
    public string? Website { get; set; }

    public bool HasOnlinePortal { get; set; }

    [MaxLength(255)]
    [Url]
    public string? ClaimsPortalUrl { get; set; }

    // Claims Processing
    [Required]
    public int StandardProcessingDays { get; set; }

    public int? EmergencyProcessingDays { get; set; }

    [Required]
    public int ClaimSubmissionDeadlineDays { get; set; }

    [MaxLength(2000)]
    public string? ClaimSubmissionProcess { get; set; }

    public PaymentMethod? PreferredPaymentMethod { get; set; }

    // Financial Information
    public Guid? BankId { get; set; }

    [ForeignKey(nameof(BankId))]
    public virtual EmployeeBank? Bank { get; set; }

    public Guid? BranchId { get; set; }

    [ForeignKey(nameof(BranchId))]
    public virtual EmployeeBankBranch? Branch { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    // Status
    public bool IsActive { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Relations
    public virtual ICollection<MedicalInsurancePlan> Plans { get; set; } = new List<MedicalInsurancePlan>();
    public virtual ICollection<MedicalInsuranceProviderFacility> ProviderFacilities { get; set; } = new List<MedicalInsuranceProviderFacility>();
    public virtual ICollection<MedicalInsuranceProviderDocument> Documents { get; set; } = new List<MedicalInsuranceProviderDocument>();
    public virtual ICollection<EmployeeMedicalInsurancePolicy> EmployeePolicies { get; set; } = new List<EmployeeMedicalInsurancePolicy>();
	public virtual ICollection<MedicalInsurancePremiumRecord> PremiumRecords { get; set; } = new List<MedicalInsurancePremiumRecord>();
}

/// <summary>
/// Insurance plans offered by a provider
/// </summary>
public class MedicalInsurancePlan : TenantEntity
{
	[Required]
    public Guid MedicalInsuranceProviderId { get; set; }

    [ForeignKey(nameof(MedicalInsuranceProviderId))]
    [InverseProperty(nameof(MedicalInsuranceProvider.Plans))]
    public virtual MedicalInsuranceProvider MedicalInsuranceProvider { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public MedicalInsurancePlanType PlanType { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    // Coverage Limits
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AnnualLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? LifetimeLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? OutpatientLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? InpatientLimit { get; set; }
	
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DentalLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OpticalLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaternityLimit { get; set; }

	[Column(TypeName = "decimal(18,2)")]
    public decimal? MentalHealthLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PrescriptionLimit { get; set; }

    // Dependents
    public bool CoversDependents { get; set; }
    public int? MaxDependents { get; set; }
    public int? MaxChildAge { get; set; }
	
    // Premium
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonthlyPremium { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AnnualPremium { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EmployerContributionPercent { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EmployeeContributionPercent { get; set; }

    // Dates
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
	
    // Relations
    public virtual ICollection<EmployeeMedicalInsurancePolicy> EmployeePolicies { get; set; } = new List<EmployeeMedicalInsurancePolicy>();
    public virtual ICollection<MedicalInsurancePremiumRecord> PremiumRecords { get; set; } = new List<MedicalInsurancePremiumRecord>();
}

/// <summary>
/// Employee's insurance policy with a provider.
/// Limits are snapshotted at enrollment from plan and benefit tier (effective cap = min of applicable sources).
/// ProviderId is denormalized from the plan for validation and query performance.
/// Status reflects business lifecycle; IsActive controls visibility in pickers and soft operational use.
/// </summary>
public class EmployeeMedicalInsurancePolicy : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    public Guid ProviderId { get; set; }

    [ForeignKey(nameof(ProviderId))]
    public virtual MedicalInsuranceProvider MedicalInsuranceProvider { get; set; } = null!;

    [Required]
    public Guid PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
    public virtual MedicalInsurancePlan MedicalInsurancePlan { get; set; } = null!;
	
    // Optionally linked to a benefit scheme tier
    public Guid? BenefitTierId { get; set; }
 
    [ForeignKey(nameof(BenefitTierId))]
    [InverseProperty(nameof(MedicalBenefitTier.Policies))]
    public virtual MedicalBenefitTier? BenefitTier { get; set; }

    [MaxLength(100)]
    public string PolicyNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MembershipNumber { get; set; }

    // Coverage Period
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Null while coverage is open-ended (active employment). Set on renewal, cancellation, or supersession.
    /// </summary>
    public DateTime? EndDate { get; set; }

    // Coverage Details — snapshotted at enrollment; not retroactively updated when plan/tier masters change
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AnnualLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedAmount { get; set; }

    [NotMapped]
    public decimal RemainingLimit => AnnualLimit - UtilizedAmount;

    public bool CoversDependents { get; set; }

    [Required]
    public MedicalInsurancePolicyStatus Status { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CancellationDate { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Relations
    public virtual ICollection<MedicalInsurancePolicyDependent> Dependents { get; set; } = new List<MedicalInsurancePolicyDependent>();
    public virtual ICollection<MedicalExpenseClaim> ExpenseClaims { get; set; } = new List<MedicalExpenseClaim>();
    public virtual ICollection<MedicalInsuranceClaim> InsuranceClaims { get; set; } = new List<MedicalInsuranceClaim>();
	public virtual ICollection<MedicalInsurancePremiumRecord> PremiumRecords { get; set; } = new List<MedicalInsurancePremiumRecord>();
	public virtual ICollection<MedicalClaimPreAuthorization> PreAuthorizations { get; set; } = new List<MedicalClaimPreAuthorization>();
}

/// <summary>
/// Dependents covered under employee's insurance policy
/// </summary>
public class MedicalInsurancePolicyDependent : TenantEntity
{
	[Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    [InverseProperty(nameof(EmployeeMedicalInsurancePolicy.Dependents))]
    public virtual EmployeeMedicalInsurancePolicy Policy { get; set; } = null!;

    [Required]
    public Guid DependentId { get; set; }

    [ForeignKey(nameof(DependentId))]
    public virtual EmployeeDependent Dependent { get; set; } = null!;

    [MaxLength(100)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Required]
    public DateTime CoverageStartDate { get; set; }

    public DateTime? CoverageEndDate { get; set; }

    /// <summary>
    /// Snapshotted dependent limit at enrollment; UtilizedAmount tracks approved usage against this cap.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AnnualLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedAmount { get; set; }

    [NotMapped]
    public decimal RemainingLimit => AnnualLimit - UtilizedAmount;

    public bool IsActive { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Insurance claim submitted to a provider against an employee policy.
/// Always linked to a parent MedicalExpenseClaim (employer reimbursement aggregate).
/// </summary>
public class MedicalInsuranceClaim : TenantEntity
{
    [Required]
    public Guid PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    [InverseProperty(nameof(EmployeeMedicalInsurancePolicy.InsuranceClaims))]
    public virtual EmployeeMedicalInsurancePolicy Policy { get; set; } = null!;

    [Required]
    public Guid MedicalExpenseClaimId { get; set; }

    [ForeignKey(nameof(MedicalExpenseClaimId))]
    [InverseProperty(nameof(MedicalExpenseClaim.InsuranceClaims))]
    public virtual MedicalExpenseClaim MedicalExpenseClaim { get; set; } = null!;

    [MaxLength(100)]
    public string InsuranceClaimNumber { get; set; } = string.Empty;

    [Required]
    public DateTime SubmissionDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ClaimedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PaidAmount { get; set; }
	
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CoPayAmount { get; set; }

    [Required]
    public MedicalInsuranceClaimStatus Status { get; set; }

    public DateTime? ApprovalDate { get; set; }

    public DateTime? RejectionDate { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime? PaymentDate { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Healthcare facilities in an insurance provider's network
/// </summary>
public class MedicalInsuranceProviderFacility : TenantEntity
{
    [Required]
    public Guid ProviderId { get; set; }

    [ForeignKey(nameof(ProviderId))]
    [InverseProperty(nameof(MedicalInsuranceProvider.ProviderFacilities))]
    public virtual MedicalInsuranceProvider MedicalInsuranceProvider { get; set; } = null!;

    [Required]
    public Guid FacilityId { get; set; }

    [ForeignKey(nameof(FacilityId))]
    [InverseProperty(nameof(HealthcareFacility.ProviderFacilities))]
    public virtual HealthcareFacility Facility { get; set; } = null!;

    [Required]
    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }
	
	public bool IsPreferredProvider { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Documents attached to an insurance provider record
/// </summary>
public class MedicalInsuranceProviderDocument : TenantEntity
{
    [Required]
    public Guid ProviderId { get; set; }

    [ForeignKey(nameof(ProviderId))]
    [InverseProperty(nameof(MedicalInsuranceProvider.Documents))]
    public virtual MedicalInsuranceProvider MedicalInsuranceProvider { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public MedicalInsuranceProviderDocumentType DocumentType { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime UploadDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Tracks premium payments made to an insurance provider for employee policies
/// </summary>
public class MedicalInsurancePremiumRecord : TenantEntity
{
    [Required]
    public Guid ProviderId { get; set; }
 
    [ForeignKey(nameof(ProviderId))]
    public virtual MedicalInsuranceProvider MedicalInsuranceProvider { get; set; } = null!;
 
    [Required]
    public Guid PlanId { get; set; }
 
    [ForeignKey(nameof(PlanId))]
    public virtual MedicalInsurancePlan MedicalInsurancePlan { get; set; } = null!;
 
    // Optionally linked to a specific employee policy
    public Guid? PolicyId { get; set; }
 
    [ForeignKey(nameof(PolicyId))]
    public virtual EmployeeMedicalInsurancePolicy? Policy { get; set; }
 
    // Billing Period
    [Required]
    public DateOnly BillingPeriodStart { get; set; }
 
    [Required]
    public DateOnly BillingPeriodEnd { get; set; }
 
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPremiumAmount { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal EmployerContribution { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal EmployeeContribution { get; set; }
 
    public int CoveredLivesCount { get; set; }
 
    [Required]
    public MedicalInsurancePremiumPaymentStatus Status { get; set; }
 
    public DateTime? DueDate { get; set; }
 
    public DateTime? PaymentDate { get; set; }
 
    [MaxLength(100)]
    public string? PaymentReference { get; set; }
 
    public PaymentMethod? PaymentMethod { get; set; }
 
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Company-defined medical benefit schemes — governs who gets what level of coverage
/// </summary>
public class MedicalBenefitScheme : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
 
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
 
    [MaxLength(1000)]
    public string? Description { get; set; }
 
    [Required]
    public DateTime EffectiveDate { get; set; }
 
    public DateTime? ExpiryDate { get; set; }
 
    public bool IsActive { get; set; }
 
    [MaxLength(1000)]
    public string? Notes { get; set; }
 
    // Relations
    public virtual ICollection<MedicalBenefitTier> Tiers { get; set; } = new List<MedicalBenefitTier>();
}

/// <summary>
/// A tier within a benefit scheme — maps to a staff level and defines limits
/// </summary>
public class MedicalBenefitTier : TenantEntity
{
    [Required]
    public Guid SchemeId { get; set; }
 
    [ForeignKey(nameof(SchemeId))]
    [InverseProperty(nameof(MedicalBenefitScheme.Tiers))]
    public virtual MedicalBenefitScheme Scheme { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string TierName { get; set; } = string.Empty;
 
    // Link to staff level — FK type depends on Staff Level entity
    public Guid? StaffLevelId { get; set; }
	
	[ForeignKey(nameof(StaffLevelId))]
	public virtual StaffLevel? StaffLevel {get; set;}
 
    [MaxLength(200)]
    public string? TierDescription { get; set; }
 
    // Coverage Limits
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AnnualLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? InpatientLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OutpatientLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DentalLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OpticalLimit { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaternityLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MentalHealthLimit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PrescriptionLimit { get; set; }

    // Dependents
    public bool CoversDependents { get; set; }
 
    public int? MaxDependents { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DependentAnnualLimit { get; set; }
 
    public bool IsActive { get; set; }
 
    [MaxLength(1000)]
    public string? Notes { get; set; }
 
    // Relations
    public virtual ICollection<EmployeeMedicalInsurancePolicy> Policies { get; set; } = new List<EmployeeMedicalInsurancePolicy>();
}

/// <summary>
/// Persistent health profile for an employee — maintained across claims and exams.
/// One profile per employee per tenant; create before recording exams or clinical history.
/// </summary>
public class EmployeeHealthProfile : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;
 
    public double? HeightCm { get; set; }
 
    public double? WeightKg { get; set; }
 
    [NotMapped]
    public double? BMI => (HeightCm.HasValue && WeightKg.HasValue && HeightCm > 0)
        ? Math.Round(WeightKg.Value / Math.Pow(HeightCm.Value / 100.0, 2), 2)
        : null;
 
    public DisabilityStatus DisabilityStatus { get; set; } = DisabilityStatus.None;
 
    [MaxLength(500)]
    public string? DisabilityDescription { get; set; }
 
    // Emergency Medical Contact
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }
 
    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }
 
    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }
 
    // Preferred Facility
    public Guid? PreferredFacilityId { get; set; }
 
    [ForeignKey(nameof(PreferredFacilityId))]
    public virtual HealthcareFacility? PreferredFacility { get; set; }
 
    public Guid? PreferredPhysicianId { get; set; }
 
    [ForeignKey(nameof(PreferredPhysicianId))]
    public virtual Physician? PreferredPhysician { get; set; }
 
    public DateTime? LastUpdated { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
 
    // Relations
    public virtual ICollection<EmployeeHealthCondition> Conditions { get; set; } = new List<EmployeeHealthCondition>();
    public virtual ICollection<EmployeeAllergy> Allergies { get; set; } = new List<EmployeeAllergy>();
    public virtual ICollection<EmployeeMedicalExam> MedicalExams { get; set; } = new List<EmployeeMedicalExam>();
}

/// <summary>
/// Chronic or significant health conditions for an employee
/// </summary>
public class EmployeeHealthCondition : TenantEntity
{
    [Required]
    public Guid HealthProfileId { get; set; }
 
    [ForeignKey(nameof(HealthProfileId))]
    [InverseProperty(nameof(EmployeeHealthProfile.Conditions))]
    public virtual EmployeeHealthProfile HealthProfile { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string ConditionName { get; set; } = string.Empty;
 
    // ICD-10 code where applicable
    [MaxLength(20)]
    public string? ICDCode { get; set; }
 
    public HealthConditionSeverity Severity { get; set; }
 
    public HealthConditionStatus Status { get; set; }
 
    public DateOnly? DiagnosedDate { get; set; }
 
    public DateOnly? ResolvedDate { get; set; }
 
    [MaxLength(500)]
    public string? TreatmentSummary { get; set; }
 
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Known allergies for an employee
/// </summary>
public class EmployeeAllergy : TenantEntity
{
    [Required]
    public Guid HealthProfileId { get; set; }
 
    [ForeignKey(nameof(HealthProfileId))]
    [InverseProperty(nameof(EmployeeHealthProfile.Allergies))]
    public virtual EmployeeHealthProfile HealthProfile { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Allergen { get; set; } = string.Empty;
 
    public AllergyType AllergyType { get; set; }
 
    public AllergySeverity Severity { get; set; }
 
    [MaxLength(500)]
    public string? ReactionDescription { get; set; }
 
    [MaxLength(300)]
    public string? ManagementPlan { get; set; }
 
    public bool IsActive { get; set; } = true;
 
    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Annual or periodic medical examination results for an employee
/// </summary>
public class EmployeeMedicalExam : TenantEntity
{
    [Required]
    public Guid HealthProfileId { get; set; }
 
    [ForeignKey(nameof(HealthProfileId))]
    [InverseProperty(nameof(EmployeeHealthProfile.MedicalExams))]
    public virtual EmployeeHealthProfile HealthProfile { get; set; } = null!;

    [Required]
    public DateOnly ExamDate { get; set; }
 
    public Guid? FacilityId { get; set; }
 
    [ForeignKey(nameof(FacilityId))]
    public virtual HealthcareFacility? Facility { get; set; }
 
    public Guid? PhysicianId { get; set; }
 
    [ForeignKey(nameof(PhysicianId))]
    public virtual Physician? Physician { get; set; }
 
    // Vitals
    public double? HeightCm { get; set; }
 
    public double? WeightKg { get; set; }
 
    [MaxLength(20)]
    public string? BloodPressure { get; set; }
 
    public double? BMIRecorded { get; set; }
 
    [MaxLength(200)]
    public string? VisionResult { get; set; }
 
    [MaxLength(200)]
    public string? HearingResult { get; set; }
 
    // Outcome
    [Required]
    public MedicalExamResult Result { get; set; }
 
    [MaxLength(1000)]
    public string? Findings { get; set; }
 
    [MaxLength(1000)]
    public string? Recommendations { get; set; }
 
    [MaxLength(500)]
    public string? Restrictions { get; set; }
 
    public DateOnly? NextExamDueDate { get; set; }
 
    // Related cost (may be claimed)
    public Guid? LinkedClaimId { get; set; }
 
    [ForeignKey(nameof(LinkedClaimId))]
    public virtual MedicalExpenseClaim? LinkedClaim { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
 
    // Documents
    public virtual ICollection<EmployeeMedicalExamDocument> Documents { get; set; } = new List<EmployeeMedicalExamDocument>();
}

public class EmployeeMedicalExamDocument : TenantEntity
{
    [Required]
    public Guid ExamId { get; set; }
 
    [ForeignKey(nameof(ExamId))]
    [InverseProperty(nameof(EmployeeMedicalExam.Documents))]
    public virtual EmployeeMedicalExam Exam { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pre-authorization before a planned medical procedure.
/// ApprovedBy is internal HR/medical officer sign-off; insurer authorization is captured via AuthorizationNumber and related fields.
/// Resulting expense claims reference this record via MedicalExpenseClaim.PreAuthorizationId.
/// </summary>
public class MedicalClaimPreAuthorization : TenantEntity
{
    [MaxLength(50)]
    public string AuthorizationNumber { get; set; } = string.Empty;
 
    [Required]
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
	
    public Guid? DependentId { get; set; }

    [ForeignKey(nameof(DependentId))]
    public virtual EmployeeDependent? Dependent { get; set; }
 
    [Required]
    public Guid PolicyId { get; set; }
 
    [ForeignKey(nameof(PolicyId))]
    [InverseProperty(nameof(EmployeeMedicalInsurancePolicy.PreAuthorizations))]
    public virtual EmployeeMedicalInsurancePolicy Policy { get; set; } = null!;

    public Guid? FacilityId { get; set; }

    [ForeignKey(nameof(FacilityId))]
    public virtual HealthcareFacility? Facility { get; set; }

    public Guid? PhysicianId { get; set; }

    [ForeignKey(nameof(PhysicianId))]
    public virtual Physician? Physician { get; set; }

    [Required]
    public MedicalServiceType ServiceType { get; set; }
	
	public bool IsEmergency { get; set; }
 
	[Required]
    [MaxLength(500)]
    public string Diagnosis { get; set; } = string.Empty;
	
	[Required]
    [MaxLength(1000)]
    public string ProposedTreatment { get; set; } = string.Empty;
 
    public DateTime? PlannedServiceDate { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedCost { get; set; }
	
    public DateTime RequestDate { get; set; }
 
    public ClaimPreAuthorizationStatus Status { get; set; } = ClaimPreAuthorizationStatus.Draft;

    /// <summary>
    /// Internal approver (HR/medical officer). Insurer users are not Employee records.
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    [ForeignKey(nameof(ApprovedBy))]
    public virtual Employee? Approver { get; set; }
	
	[Column(TypeName = "decimal(18,2)")]
    public decimal? AuthorizedAmount { get; set; }
 
    public DateTime? AuthorizationDate { get; set; }
 
    public DateTime? ExpiryDate { get; set; }
 
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Physician referral — tracks when an employee is referred to a specialist or another facility.
/// Resulting expense claims reference this record via MedicalExpenseClaim.ReferralId.
/// </summary>
public class MedicalReferral : TenantEntity
{
    [MaxLength(50)]
    public string ReferralNumber { get; set; } = string.Empty;
 
    [Required]
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public bool IsForDependent { get; set; }
 
    public Guid? DependentId { get; set; }
 
    [ForeignKey(nameof(DependentId))]
    public virtual EmployeeDependent? Dependent { get; set; }
 
    // Referring
    public Guid? ReferringFacilityId { get; set; }
 
    [ForeignKey(nameof(ReferringFacilityId))]
    public virtual HealthcareFacility? ReferringFacility { get; set; }
 
    public Guid? ReferringPhysicianId { get; set; }
 
    [ForeignKey(nameof(ReferringPhysicianId))]
    [InverseProperty(nameof(Physician.IssuedReferrals))]
    public virtual Physician? ReferringPhysician { get; set; }
 
    // Referred To
    public Guid? ReferredToFacilityId { get; set; }
 
    [ForeignKey(nameof(ReferredToFacilityId))]
    public virtual HealthcareFacility? ReferredToFacility { get; set; }
 
    public Guid? ReferredToPhysicianId { get; set; }
 
    [ForeignKey(nameof(ReferredToPhysicianId))]
    [InverseProperty(nameof(Physician.ReceivedReferrals))]
    public virtual Physician? ReferredToPhysician { get; set; }
 
    [Required]
    public DateTime ReferralDate { get; set; }
 
    public DateTime? ExpiryDate { get; set; }
 
    [Required]
    public MedicalReferralPriority Priority { get; set; }
 
	[MaxLength(500)]
    public string? Diagnosis { get; set; }
 
    [Required]
    [MaxLength(1000)]
    public string ReasonForReferral { get; set; } = string.Empty;
 
    [Required]
    public MedicalReferralStatus Status { get; set; } = MedicalReferralStatus.Pending;
 
    public DateTime? CompletedDate { get; set; }
 
    [MaxLength(1000)]
    public string? OutcomeSummary { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Scheduled medical appointments for employees and their dependents
/// </summary>
public class MedicalAppointment : TenantEntity
{
    [MaxLength(50)]
    public string AppointmentNumber { get; set; } = string.Empty;
 
    [Required]
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public bool IsForDependent { get; set; }
 
    public Guid? DependentId { get; set; }
 
    [ForeignKey(nameof(DependentId))]
    public virtual EmployeeDependent? Dependent { get; set; }
 
    [Required]
    public Guid FacilityId { get; set; }
 
    [ForeignKey(nameof(FacilityId))]
    [InverseProperty(nameof(HealthcareFacility.Appointments))]
    public virtual HealthcareFacility Facility { get; set; } = null!;

    public Guid? PhysicianId { get; set; }

    [ForeignKey(nameof(PhysicianId))]
    [InverseProperty(nameof(Physician.Appointments))]
    public virtual Physician? Physician { get; set; }

    [Required]
    public DateTime AppointmentDateTime { get; set; }
 
    public int? DurationMinutes { get; set; }
 
    public MedicalServiceType ServiceType { get; set; }
 
    [Required]
    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;
 
    [Required]
    public MedicalAppointmentStatus Status { get; set; } = MedicalAppointmentStatus.Draft;
 
    public DateTime? CheckInTime { get; set; }
 
    public DateTime? CheckOutTime { get; set; }
 
    [MaxLength(1000)]
    public string? OutcomeSummary { get; set; }
 
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
 
    // May result in a referral or claim
    public Guid? LinkedReferralId { get; set; }
 
    [ForeignKey(nameof(LinkedReferralId))]
    public virtual MedicalReferral? LinkedReferral { get; set; }
 
    public Guid? LinkedClaimId { get; set; }
 
    [ForeignKey(nameof(LinkedClaimId))]
    public virtual MedicalExpenseClaim? LinkedClaim { get; set; }
 
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// NHIS-specific claim — standalone reimbursement track.
/// Link to MedicalExpenseClaim only when the employer reimburses co-pay or top-up (LinkedMedicalClaimId).
/// </summary>
public class NHISClaim : TenantEntity
{
    [MaxLength(50)]
    public string ClaimNumber { get; set; } = string.Empty;
 
    [Required]
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public bool IsForDependent { get; set; }
 
    public Guid? DependentId { get; set; }
 
    [ForeignKey(nameof(DependentId))]
    public virtual EmployeeDependent? Dependent { get; set; }
 
    [MaxLength(100)]
    public string NHISMembershipNumber { get; set; } = string.Empty;
 
    [Required]
    public Guid FacilityId { get; set; }
 
    [ForeignKey(nameof(FacilityId))]
    public virtual HealthcareFacility Facility { get; set; } = null!;
 
    public Guid? PhysicianId { get; set; }
 
    [ForeignKey(nameof(PhysicianId))]
    public virtual Physician? Physician { get; set; }
 
    [Required]
    public DateTime ServiceDate { get; set; }
 
    [Required]
    public MedicalServiceType ServiceType { get; set; }
 
    [Required]
    [MaxLength(1000)]
    public string ServiceDescription { get; set; } = string.Empty;
 
    [MaxLength(500)]
    public string? Diagnosis { get; set; }
 
    [MaxLength(20)]
    public string? ICDCode { get; set; }
 
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NHISCoveredAmount { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CoPayAmount { get; set; }
 
    // Batch submission info
    [MaxLength(100)]
    public string? BatchNumber { get; set; }
 
    public DateTime? SubmissionDate { get; set; }
 
    [Required]
    public NHISClaimStatus Status { get; set; } = NHISClaimStatus.Draft;
 
    public DateTime? ApprovalDate { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ApprovedAmount { get; set; }
 
    public DateTime? RejectionDate { get; set; }
 
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
 
    public DateTime? PaymentDate { get; set; }
 
    [MaxLength(100)]
    public string? PaymentReference { get; set; }
 
    // Optional link when employer reimburses co-pay or top-up via MedicalExpenseClaim
    public Guid? LinkedMedicalClaimId { get; set; }
 
    [ForeignKey(nameof(LinkedMedicalClaimId))]
    [InverseProperty(nameof(MedicalExpenseClaim.LinkedNHISClaims))]
    public virtual MedicalExpenseClaim? LinkedMedicalClaim { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
 
    public virtual ICollection<NHISClaimDocument> Documents { get; set; } = new List<NHISClaimDocument>();
}

public class NHISClaimDocument : TenantEntity
{
    [Required]
    public Guid NHISClaimId { get; set; }
 
    [ForeignKey(nameof(NHISClaimId))]
    [InverseProperty(nameof(NHISClaim.Documents))]
    public virtual NHISClaim NHISClaim { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Employee medical expense claim — employer reimbursement aggregate (HR/Finance workflow hub).
/// Insurance and NHIS submissions are child records; derive coverage from InsurancePolicyId and child collections.
/// </summary>
public class MedicalExpenseClaim : TenantEntity
{
    [MaxLength(50)]
    public string ClaimNumber { get; set; } = string.Empty;

    // Employee & Dependent
	[Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public bool IsForDependent { get; set; }

    public Guid? DependentId { get; set; }

    [ForeignKey(nameof(DependentId))]
    public virtual EmployeeDependent? Dependent { get; set; }

    // Claim Details
    [Required]
    public DateTime ClaimDate { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ServiceDate { get; set; }
	
	public DateTime? ServiceEndDate { get; set; }

    [Required]
    public MedicalExpenseType ExpenseType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    // Healthcare Provider
    public Guid FacilityId { get; set; }

    [ForeignKey(nameof(FacilityId))]
    [InverseProperty(nameof(HealthcareFacility.ExpenseClaims))]
    public virtual HealthcareFacility Facility { get; set; } = null!;

    public Guid? PhysicianId { get; set; }

    [ForeignKey(nameof(PhysicianId))]
    [InverseProperty(nameof(Physician.ExpenseClaims))]
    public virtual Physician? Physician { get; set; }

    // Diagnosis & Treatment
    [MaxLength(500)]
    public string? Diagnosis { get; set; }
	
    [MaxLength(20)]
    public string? ICDCode { get; set; }

    [MaxLength(1000)]
    public string? TreatmentReceived { get; set; }

    public bool IsEmergency { get; set; }
    public bool RequiredHospitalization { get; set; }
    public DateOnly? AdmissionStart { get; set; }
    public DateOnly? AdmissionEnd { get; set; }
	
    // Pre-Authorization & Referral Links
    public Guid? PreAuthorizationId { get; set; }
 
    [ForeignKey(nameof(PreAuthorizationId))]
    public virtual MedicalClaimPreAuthorization? PreAuthorization { get; set; }

    public Guid? ReferralId { get; set; }

    [ForeignKey(nameof(ReferralId))]
    public virtual MedicalReferral? Referral { get; set; }

    // Cost Details
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountRequested { get; set; }

    // Insurance — presence of InsurancePolicyId indicates private insurance involvement
    public Guid? InsurancePolicyId { get; set; }

    [ForeignKey(nameof(InsurancePolicyId))]
    [InverseProperty(nameof(EmployeeMedicalInsurancePolicy.ExpenseClaims))]
    public virtual EmployeeMedicalInsurancePolicy? InsurancePolicy { get; set; }

    // Leave Integration
    public Guid? LeaveRequestId { get; set; }
	
	[ForeignKey(nameof(LeaveRequestId))]
	public virtual LeaveRequest? LeaveRequest {get; set;}

    // Approval Workflow
    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

    // Payment
    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountApproved { get; set; }

    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }
	
    // Flag for review (fraud detection)
    public bool IsFlaggedForReview { get; set; }

    [MaxLength(500)]
    public string? FlagReason { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    // Relations
    public virtual ICollection<MedicalInsuranceClaim> InsuranceClaims { get; set; } = new List<MedicalInsuranceClaim>();
    public virtual ICollection<NHISClaim> LinkedNHISClaims { get; set; } = new List<NHISClaim>();
    public virtual ICollection<MedicalExpenseApproval> Approvals { get; set; } = new List<MedicalExpenseApproval>();
    public virtual ICollection<MedicalExpenseDocument> Documents { get; set; } = new List<MedicalExpenseDocument>();
    public virtual ICollection<MedicalExpenseItem> Items { get; set; } = new List<MedicalExpenseItem>();
	public virtual ICollection<MedicalExpenseClaimNote> Notes { get; set; } = new List<MedicalExpenseClaimNote>();
}

public class MedicalExpenseApproval : TenantEntity
{
    [Required]
    public Guid ClaimId { get; set; }

    [ForeignKey(nameof(ClaimId))]
    [InverseProperty(nameof(MedicalExpenseClaim.Approvals))]
    public virtual MedicalExpenseClaim Claim { get; set; } = null!;

    public Guid? ApproverId { get; set; }

    [ForeignKey(nameof(ApproverId))]
    public virtual Employee? Approver { get; set; }

    public MedicalExpenseApprovalStatus Status { get; set; } = MedicalExpenseApprovalStatus.Pending;
    
    public DateTime? ActionDate { get; set; }

    [MaxLength(500)]
    public string? Comments { get; set; }
}

public class MedicalExpenseItem : TenantEntity
{
	[Required]
    public Guid ClaimId { get; set; }

    [ForeignKey(nameof(ClaimId))]
    [InverseProperty(nameof(MedicalExpenseClaim.Items))]
    public virtual MedicalExpenseClaim Claim { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public MedicalItemType ItemType { get; set; }
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [NotMapped]
    public decimal TotalCost => Quantity * UnitCost;

    [MaxLength(500)]
    public string? Remarks { get; set; }
}

public class MedicalExpenseDocument : TenantEntity
{
	[Required]
    public Guid ClaimId { get; set; }

    [ForeignKey(nameof(ClaimId))]
    [InverseProperty(nameof(MedicalExpenseClaim.Documents))]
    public virtual MedicalExpenseClaim Claim { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public MedicalDocumentType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Auditable notes and correspondence on a medical expense claim
/// </summary>
public class MedicalExpenseClaimNote : TenantEntity
{
    [Required]
    public Guid ClaimId { get; set; }
 
    [ForeignKey(nameof(ClaimId))]
    [InverseProperty(nameof(MedicalExpenseClaim.Notes))]
    public virtual MedicalExpenseClaim Claim { get; set; } = null!;

    [Required]
    public Guid AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public virtual Employee Author { get; set; } = null!;

    [Required]
    public MedicalExpenseClaimNoteType NoteType { get; set; }
 
    [Required]
    [MaxLength(3000)]
    public string Content { get; set; } = string.Empty;
 
    /// <summary>
    /// Internal notes are visible only to HR/Finance, not the employee
    /// </summary>
    public bool IsInternal { get; set; }
 
    public DateTime NoteDate { get; set; } = DateTime.UtcNow;
}
