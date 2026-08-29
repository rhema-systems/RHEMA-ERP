using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// MEDICAL MODULE DTOs
// ============================================================================

#region Healthcare Facility

public class HealthcareFacilityDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string FacilityCode { get; set; } = string.Empty;
    public HealthFacilityType FacilityType { get; set; }
    public string FacilityTypeName => FacilityType.ToString();
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }

    public string PhysicalAddress { get; set; } = string.Empty;
    public string? DigitalAddress { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    public string? Description { get; set; }
    public bool HasEmergencyServices { get; set; }
    public bool Has24HourService { get; set; }
    public bool HasAmbulanceService { get; set; }
    public bool HasLaboratory { get; set; }
    public bool HasPharmacy { get; set; }

    public string? AccreditationBody { get; set; }
    public string? AccreditationNumber { get; set; }
    public DateTime? AccreditationDate { get; set; }
    public DateTime? AccreditationExpiryDate { get; set; }
    public bool AcceptsNHIS { get; set; }
    public string? NHISAccreditationNumber { get; set; }

    public Guid? BankId { get; set; }
    public string? BankName { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }

    public string? ContactPersonName { get; set; }
    public string? ContactPersonTitle { get; set; }
    public string? ContactPersonPhone { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? OperatingHours { get; set; }
    public string? OperatingDays { get; set; }

    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class HealthcareFacilitySummaryDto
{
    public Guid Id { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string FacilityCode { get; set; } = string.Empty;
    public HealthFacilityType FacilityType { get; set; }
    public string FacilityTypeName => FacilityType.ToString();
    public string? City { get; set; }
    public string? PrimaryPhone { get; set; }
    public bool HasEmergencyServices { get; set; }
    public bool AcceptsNHIS { get; set; }
    public bool IsActive { get; set; }
}

public class HealthcareFacilityDetailDto : HealthcareFacilityDto
{
    public List<PhysicianSummaryDto> Physicians { get; set; } = new();
    public List<FacilityServiceDto> Services { get; set; } = new();
    public List<MedicalInsuranceProviderFacilitySummaryDto> ProviderNetworks { get; set; } = new();
}

public class CreateHealthcareFacilityDto : CreateDtoBase
{
    [Required][MaxLength(300)]
    public string FacilityName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? ShortName { get; set; }
    [MaxLength(50)]
    public string FacilityCode { get; set; } = string.Empty;
    [Required]
    public HealthFacilityType FacilityType { get; set; }
    [MaxLength(100)]
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    [Required][MaxLength(500)]
    public string PhysicalAddress { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? DigitalAddress { get; set; }
    [MaxLength(100)]
    public string? City { get; set; }
    [MaxLength(70)]
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    [MaxLength(50)]
    public string? PrimaryPhone { get; set; }
    [MaxLength(50)]
    public string? EmergencyPhone { get; set; }
    [MaxLength(255)][EmailAddress]
    public string? Email { get; set; }
    [MaxLength(255)][Url]
    public string? Website { get; set; }
    [MaxLength(1000)]
    public string? Description { get; set; }
    public bool HasEmergencyServices { get; set; }
    public bool Has24HourService { get; set; }
    public bool HasAmbulanceService { get; set; }
    public bool HasLaboratory { get; set; }
    public bool HasPharmacy { get; set; }
    [MaxLength(200)]
    public string? AccreditationBody { get; set; }
    [MaxLength(100)]
    public string? AccreditationNumber { get; set; }
    public DateTime? AccreditationDate { get; set; }
    public DateTime? AccreditationExpiryDate { get; set; }
    public bool AcceptsNHIS { get; set; }
    [MaxLength(100)]
    public string? NHISAccreditationNumber { get; set; }
    public Guid? BankId { get; set; }
    public Guid? BranchId { get; set; }
    [MaxLength(50)]
    public string? AccountNumber { get; set; }
    [MaxLength(200)]
    public string? AccountName { get; set; }
    [MaxLength(200)]
    public string? ContactPersonName { get; set; }
    [MaxLength(20)]
    public string? ContactPersonTitle { get; set; }
    [MaxLength(50)][Phone]
    public string? ContactPersonPhone { get; set; }
    [MaxLength(255)][EmailAddress]
    public string? ContactPersonEmail { get; set; }
    [MaxLength(100)]
    public string? OperatingHours { get; set; }
    [MaxLength(500)]
    public string? OperatingDays { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateHealthcareFacilityDto : UpdateDtoBase
{
    [Required][MaxLength(300)]
    public string FacilityName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? ShortName { get; set; }
    [MaxLength(50)]
    public string FacilityCode { get; set; } = string.Empty;
    [Required]
    public HealthFacilityType FacilityType { get; set; }
    [MaxLength(100)]
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    [Required][MaxLength(500)]
    public string PhysicalAddress { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? DigitalAddress { get; set; }
    [MaxLength(100)]
    public string? City { get; set; }
    [MaxLength(70)]
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    [MaxLength(50)]
    public string? PrimaryPhone { get; set; }
    [MaxLength(50)]
    public string? EmergencyPhone { get; set; }
    [MaxLength(255)][EmailAddress]
    public string? Email { get; set; }
    [MaxLength(255)][Url]
    public string? Website { get; set; }
    [MaxLength(1000)]
    public string? Description { get; set; }
    public bool HasEmergencyServices { get; set; }
    public bool Has24HourService { get; set; }
    public bool HasAmbulanceService { get; set; }
    public bool HasLaboratory { get; set; }
    public bool HasPharmacy { get; set; }
    [MaxLength(200)]
    public string? AccreditationBody { get; set; }
    [MaxLength(100)]
    public string? AccreditationNumber { get; set; }
    public DateTime? AccreditationDate { get; set; }
    public DateTime? AccreditationExpiryDate { get; set; }
    public bool AcceptsNHIS { get; set; }
    [MaxLength(100)]
    public string? NHISAccreditationNumber { get; set; }
    public Guid? BankId { get; set; }
    public Guid? BranchId { get; set; }
    [MaxLength(50)]
    public string? AccountNumber { get; set; }
    [MaxLength(200)]
    public string? AccountName { get; set; }
    [MaxLength(200)]
    public string? ContactPersonName { get; set; }
    [MaxLength(20)]
    public string? ContactPersonTitle { get; set; }
    [MaxLength(50)][Phone]
    public string? ContactPersonPhone { get; set; }
    [MaxLength(255)][EmailAddress]
    public string? ContactPersonEmail { get; set; }
    [MaxLength(100)]
    public string? OperatingHours { get; set; }
    [MaxLength(500)]
    public string? OperatingDays { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Physician

public class PhysicianDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? Title { get; set; }
    public string FullName => $"{Title} {FirstName} {MiddleName ?? ""} {LastName}".Trim();
    public string? Specialization { get; set; }
    public string? MedicalLicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class PhysicianSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Specialization { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FacilityName { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
}

public class CreatePhysicianDto : CreateDtoBase
{
    [Required][MaxLength(200)]
    public string FirstName { get; set; } = string.Empty;
    [Required][MaxLength(200)]
    public string LastName { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? MiddleName { get; set; }
    [MaxLength(20)]
    public string? Title { get; set; }
    [MaxLength(200)]
    public string? Specialization { get; set; }
    [MaxLength(100)]
    public string? MedicalLicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
    [MaxLength(255)][EmailAddress]
    public string? Email { get; set; }
    public Guid? FacilityId { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdatePhysicianDto : UpdateDtoBase
{
    [Required][MaxLength(200)]
    public string FirstName { get; set; } = string.Empty;
    [Required][MaxLength(200)]
    public string LastName { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? MiddleName { get; set; }
    [MaxLength(20)]
    public string? Title { get; set; }
    [MaxLength(200)]
    public string? Specialization { get; set; }
    [MaxLength(100)]
    public string? MedicalLicenseNumber { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
    [MaxLength(255)][EmailAddress]
    public string? Email { get; set; }
    public Guid? FacilityId { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class VerifyPhysicianDto
{
    [Required]
    public Guid PhysicianId { get; set; }
    public DateTime VerificationDate { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Facility Service

public class FacilityServiceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MedicalServiceType ServiceType { get; set; }
    public string ServiceTypeName => ServiceType.ToString();
    public decimal? EstimatedCost { get; set; }
    public bool RequiresAppointment { get; set; }
    public bool IsEmergencyService { get; set; }
    public bool RequiresPreAuthorization { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class CreateFacilityServiceDto : CreateDtoBase
{
    [Required]
    public Guid FacilityId { get; set; }
    [Required][MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [Required]
    public MedicalServiceType ServiceType { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }
    public bool RequiresAppointment { get; set; }
    public bool IsEmergencyService { get; set; }
    public bool RequiresPreAuthorization { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateFacilityServiceDto : UpdateDtoBase
{
    [Required][MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [Required]
    public MedicalServiceType ServiceType { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }
    public bool RequiresAppointment { get; set; }
    public bool IsEmergencyService { get; set; }
    public bool RequiresPreAuthorization { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Insurance Provider

public class MedicalInsuranceProviderDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string Code { get; set; } = string.Empty;
    public MedicalInsuranceProviderType ProviderType { get; set; }
    public string ProviderTypeName => ProviderType.ToString();
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime? LicenseExpiryDate { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string PrimaryPhone { get; set; } = string.Empty;
    public string? ClaimsHotline { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? ClaimsEmail { get; set; }
    public string? Website { get; set; }
    public bool HasOnlinePortal { get; set; }
    public string? ClaimsPortalUrl { get; set; }
    public int StandardProcessingDays { get; set; }
    public int? EmergencyProcessingDays { get; set; }
    public int ClaimSubmissionDeadlineDays { get; set; }
    public string? ClaimSubmissionProcess { get; set; }
    public PaymentMethod? PreferredPaymentMethod { get; set; }
    public string? PreferredPaymentMethodName => PreferredPaymentMethod?.ToString();
    public Guid? BankId { get; set; }
    public string? BankName { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class MedicalInsuranceProviderSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public MedicalInsuranceProviderType ProviderType { get; set; }
    public string ProviderTypeName => ProviderType.ToString();
    public string PrimaryPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int PlanCount { get; set; }
}

public class MedicalInsuranceProviderDetailDto : MedicalInsuranceProviderDto
{
    public List<MedicalInsurancePlanDto> Plans { get; set; } = new();
    public List<MedicalInsuranceProviderFacilityDto> NetworkFacilities { get; set; } = new();
    public List<MedicalInsuranceProviderDocumentDto> Documents { get; set; } = new();
}

public class CreateMedicalInsuranceProviderDto : CreateDtoBase
{
    [Required][MaxLength(300)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? ShortName { get; set; }
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [Required]
    public MedicalInsuranceProviderType ProviderType { get; set; }
    [Required][MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime? LicenseExpiryDate { get; set; }
    [Required][MaxLength(500)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? City { get; set; }
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    [Required][MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? ClaimsHotline { get; set; }
    [Required][MaxLength(255)][EmailAddress]
    public string Email { get; set; } = string.Empty;
    [MaxLength(255)][EmailAddress]
    public string? ClaimsEmail { get; set; }
    [MaxLength(255)][Url]
    public string? Website { get; set; }
    public bool HasOnlinePortal { get; set; }
    [MaxLength(255)][Url]
    public string? ClaimsPortalUrl { get; set; }
    [Required][Range(1, int.MaxValue)]
    public int StandardProcessingDays { get; set; }
    [Range(1, int.MaxValue)]
    public int? EmergencyProcessingDays { get; set; }
    [Required][Range(1, int.MaxValue)]
    public int ClaimSubmissionDeadlineDays { get; set; }
    [MaxLength(2000)]
    public string? ClaimSubmissionProcess { get; set; }
    public PaymentMethod? PreferredPaymentMethod { get; set; }
    public Guid? BankId { get; set; }
    public Guid? BranchId { get; set; }
    [MaxLength(50)]
    public string? AccountNumber { get; set; }
    [MaxLength(200)]
    public string? AccountName { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalInsuranceProviderDto : UpdateDtoBase
{
    [Required][MaxLength(300)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? ShortName { get; set; }
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [Required]
    public MedicalInsuranceProviderType ProviderType { get; set; }
    [Required][MaxLength(100)]
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime? LicenseExpiryDate { get; set; }
    [Required][MaxLength(500)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? City { get; set; }
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    public Guid? CountryId { get; set; }
    [Required][MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;
    [MaxLength(50)]
    public string? ClaimsHotline { get; set; }
    [Required][MaxLength(255)][EmailAddress]
    public string Email { get; set; } = string.Empty;
    [MaxLength(255)][EmailAddress]
    public string? ClaimsEmail { get; set; }
    [MaxLength(255)][Url]
    public string? Website { get; set; }
    public bool HasOnlinePortal { get; set; }
    [MaxLength(255)][Url]
    public string? ClaimsPortalUrl { get; set; }
    [Required][Range(1, int.MaxValue)]
    public int StandardProcessingDays { get; set; }
    [Range(1, int.MaxValue)]
    public int? EmergencyProcessingDays { get; set; }
    [Required][Range(1, int.MaxValue)]
    public int ClaimSubmissionDeadlineDays { get; set; }
    [MaxLength(2000)]
    public string? ClaimSubmissionProcess { get; set; }
    public PaymentMethod? PreferredPaymentMethod { get; set; }
    public Guid? BankId { get; set; }
    public Guid? BranchId { get; set; }
    [MaxLength(50)]
    public string? AccountNumber { get; set; }
    [MaxLength(200)]
    public string? AccountName { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Insurance Plan

public class MedicalInsurancePlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MedicalInsuranceProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public MedicalInsurancePlanType PlanType { get; set; }
    public string PlanTypeName => PlanType.ToString();
    public string? Description { get; set; }
    public decimal AnnualLimit { get; set; }
    public decimal? LifetimeLimit { get; set; }
    public decimal? OutpatientLimit { get; set; }
    public decimal? InpatientLimit { get; set; }
    public decimal? DentalLimit { get; set; }
    public decimal? OpticalLimit { get; set; }
    public decimal? MaternityLimit { get; set; }
    public decimal? MentalHealthLimit { get; set; }
    public decimal? PrescriptionLimit { get; set; }
    public bool CoversDependents { get; set; }
    public int? MaxDependents { get; set; }
    public int? MaxChildAge { get; set; }
    public decimal? MonthlyPremium { get; set; }
    public decimal? AnnualPremium { get; set; }
    public decimal? EmployerContributionPercent { get; set; }
    public decimal? EmployeeContributionPercent { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class MedicalInsurancePlanSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public MedicalInsurancePlanType PlanType { get; set; }
    public string PlanTypeName => PlanType.ToString();
    public decimal AnnualLimit { get; set; }
    public bool IsActive { get; set; }
}

public class CreateMedicalInsurancePlanDto : CreateDtoBase
{
    [Required]
    public Guid MedicalInsuranceProviderId { get; set; }
    [Required][MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [Required]
    public MedicalInsurancePlanType PlanType { get; set; }
    [MaxLength(1000)]
    public string? Description { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? LifetimeLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OutpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? InpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? DentalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OpticalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MaternityLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MentalHealthLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? PrescriptionLimit { get; set; }
    public bool CoversDependents { get; set; }
    [Range(0, int.MaxValue)]
    public int? MaxDependents { get; set; }
    [Range(0, int.MaxValue)]
    public int? MaxChildAge { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MonthlyPremium { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? AnnualPremium { get; set; }
    [Range(0, 100)]
    public decimal? EmployerContributionPercent { get; set; }
    [Range(0, 100)]
    public decimal? EmployeeContributionPercent { get; set; }
    [Required]
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalInsurancePlanDto : UpdateDtoBase
{
    [Required][MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [Required]
    public MedicalInsurancePlanType PlanType { get; set; }
    [MaxLength(1000)]
    public string? Description { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? LifetimeLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OutpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? InpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? DentalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OpticalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MaternityLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MentalHealthLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? PrescriptionLimit { get; set; }
    public bool CoversDependents { get; set; }
    [Range(0, int.MaxValue)]
    public int? MaxDependents { get; set; }
    [Range(0, int.MaxValue)]
    public int? MaxChildAge { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MonthlyPremium { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? AnnualPremium { get; set; }
    [Range(0, 100)]
    public decimal? EmployerContributionPercent { get; set; }
    [Range(0, 100)]
    public decimal? EmployeeContributionPercent { get; set; }
    [Required]
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Medical Insurance Policy

public class EmployeeMedicalInsurancePolicyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? BenefitTierId { get; set; }
    public string? BenefitTierName { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal AnnualLimit { get; set; }
    public decimal UtilizedAmount { get; set; }
    public decimal RemainingLimit => AnnualLimit - UtilizedAmount;
    public decimal UtilizationPercentage => AnnualLimit > 0 ? (UtilizedAmount / AnnualLimit) * 100 : 0;
    public bool CoversDependents { get; set; }
    public MedicalInsurancePolicyStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsActive { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? Notes { get; set; }
}

public class EmployeeMedicalInsurancePolicySummaryDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal RemainingLimit { get; set; }
    public MedicalInsurancePolicyStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsActive { get; set; }
}

public class EmployeeMedicalInsurancePolicyDetailDto : EmployeeMedicalInsurancePolicyDto
{
    public List<MedicalInsurancePolicyDependentDto> Dependents { get; set; } = new();
    public List<MedicalExpenseClaimSummaryDto> RecentClaims { get; set; } = new();
    public List<MedicalInsuranceClaimSummaryDto> InsuranceClaims { get; set; } = new();
    public List<MedicalInsurancePremiumRecordSummaryDto> PremiumRecords { get; set; } = new();
}

public class CreateEmployeeMedicalInsurancePolicyDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    [Required]
    public Guid ProviderId { get; set; }
    [Required]
    public Guid PlanId { get; set; }
    public Guid? BenefitTierId { get; set; }
    [MaxLength(100)]
    public string PolicyNumber { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? MembershipNumber { get; set; }
    [Required]
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    public bool CoversDependents { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeMedicalInsurancePolicyDto : UpdateDtoBase
{
    [MaxLength(100)]
    public string PolicyNumber { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? MembershipNumber { get; set; }
    public Guid? BenefitTierId { get; set; }
    [Required]
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    public bool CoversDependents { get; set; }
    public MedicalInsurancePolicyStatus Status { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CancelEmployeeMedicalInsurancePolicyDto
{
    [Required]
    public Guid PolicyId { get; set; }
    [Required][MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
    public DateTime CancellationDate { get; set; } = DateTime.UtcNow;
}

#endregion

#region Medical Insurance Policy Dependent

public class MedicalInsurancePolicyDependentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid DependentId { get; set; }
    public string DependentName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public DateTime CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
    public decimal AnnualLimit { get; set; }
    public decimal UtilizedAmount { get; set; }
    public decimal RemainingLimit => AnnualLimit - UtilizedAmount;
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class AddMedicalInsurancePolicyDependentDto : CreateDtoBase
{
    [Required]
    public Guid PolicyId { get; set; }
    [Required]
    public Guid DependentId { get; set; }
    [MaxLength(100)]
    public string MembershipNumber { get; set; } = string.Empty;
    [Required]
    public DateTime CoverageStartDate { get; set; }
    public DateTime? CoverageEndDate { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateMedicalInsurancePolicyDependentDto : UpdateDtoBase
{
    [MaxLength(100)]
    public string MembershipNumber { get; set; } = string.Empty;
    public DateTime? CoverageEndDate { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Insurance Claim

public class MedicalInsuranceClaimDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid MedicalExpenseClaimId { get; set; }
    public string MedicalClaimNumber { get; set; } = string.Empty;
    public string InsuranceClaimNumber { get; set; } = string.Empty;
    public DateTime SubmissionDate { get; set; }
    public decimal ClaimedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public decimal? CoPayAmount { get; set; }
    public MedicalInsuranceClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ApprovalDate { get; set; }
    public DateTime? RejectionDate { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}

public class MedicalInsuranceClaimSummaryDto
{
    public Guid Id { get; set; }
    public string InsuranceClaimNumber { get; set; } = string.Empty;
    public string MedicalClaimNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public DateTime SubmissionDate { get; set; }
    public decimal ClaimedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public MedicalInsuranceClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateMedicalInsuranceClaimDto : CreateDtoBase
{
    [Required]
    public Guid PolicyId { get; set; }
    [Required]
    public Guid MedicalExpenseClaimId { get; set; }
    [MaxLength(100)]
    public string InsuranceClaimNumber { get; set; } = string.Empty;
    [Required][Range(0, double.MaxValue)]
    public decimal ClaimedAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? CoPayAmount { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalInsuranceClaimStatusDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required]
    public MedicalInsuranceClaimStatus Status { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? ApprovedAmount { get; set; }
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class RecordMedicalInsuranceClaimPaymentDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal PaidAmount { get; set; }
    [Required][MaxLength(100)]
    public string PaymentReference { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Insurance Provider Facility

public class MedicalInsuranceProviderFacilityDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string? FacilityCity { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsPreferredProvider { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class MedicalInsuranceProviderFacilitySummaryDto
{
    public Guid Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public bool IsPreferredProvider { get; set; }
    public bool IsActive { get; set; }
}

public class AddMedicalInsuranceProviderFacilityDto : CreateDtoBase
{
    [Required]
    public Guid ProviderId { get; set; }
    [Required]
    public Guid FacilityId { get; set; }
    [Required]
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsPreferredProvider { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalInsuranceProviderFacilityDto : UpdateDtoBase
{
    public DateTime? ExpiryDate { get; set; }
    public bool IsPreferredProvider { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Insurance Provider Document

public class MedicalInsuranceProviderDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    /// <summary>⚠ Legacy path, empty on anything uploaded through the gate. Never a URL.</summary>
    public string FilePath { get; set; } = string.Empty;
    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public MedicalInsuranceProviderDocumentType DocumentType { get; set; }
    public string DocumentTypeName => DocumentType.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateMedicalInsuranceProviderDocumentDto : CreateDtoBase
{
    [Required]
    public Guid ProviderId { get; set; }
    [Required][MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Legacy storage path, no longer required and refused when an API caller supplies it — see
    /// the controller. Files arrive through <c>POST provider-documents/upload</c>, which puts them
    /// past the malware scanner into private storage and fills the three ids below instead.
    /// </summary>
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [Required]
    public MedicalInsuranceProviderDocumentType DocumentType { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

#endregion

#region Medical Insurance Premium Record

public class MedicalInsurancePremiumRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }
    public decimal TotalPremiumAmount { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal EmployeeContribution { get; set; }
    public int CoveredLivesCount { get; set; }
    public MedicalInsurancePremiumPaymentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? DueDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentMethodName => PaymentMethod?.ToString();
    public string? Notes { get; set; }
}

public class MedicalInsurancePremiumRecordSummaryDto
{
    public Guid Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }
    public decimal TotalPremiumAmount { get; set; }
    public MedicalInsurancePremiumPaymentStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateMedicalInsurancePremiumRecordDto : CreateDtoBase
{
    [Required]
    public Guid ProviderId { get; set; }
    [Required]
    public Guid PlanId { get; set; }
    public Guid? PolicyId { get; set; }
    [Required]
    public DateOnly BillingPeriodStart { get; set; }
    [Required]
    public DateOnly BillingPeriodEnd { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal TotalPremiumAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal EmployerContribution { get; set; }
    [Range(0, double.MaxValue)]
    public decimal EmployeeContribution { get; set; }
    public int CoveredLivesCount { get; set; }
    public DateTime? DueDate { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class RecordMedicalInsurancePremiumPaymentDto
{
    [Required]
    public Guid PremiumRecordId { get; set; }
    [Required]
    public PaymentMethod PaymentMethod { get; set; }
    [Required][MaxLength(100)]
    public string PaymentReference { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Benefit Scheme

public class MedicalBenefitSchemeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public List<MedicalBenefitTierDto> Tiers { get; set; } = new();
}

public class MedicalBenefitSchemeSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; }
    public int TierCount { get; set; }
}

public class MedicalBenefitSchemeDetailDto : MedicalBenefitSchemeDto
{
    public new List<MedicalBenefitTierDto> Tiers { get; set; } = new();
}

public class CreateMedicalBenefitSchemeDto : CreateDtoBase
{
    [Required][MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Description { get; set; }
    [Required]
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalBenefitSchemeDto : UpdateDtoBase
{
    [Required][MaxLength(200)]
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
}

#endregion

#region Medical Benefit Tier

public class MedicalBenefitTierDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid SchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string TierName { get; set; } = string.Empty;
    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }
    public string? TierDescription { get; set; }
    public decimal AnnualLimit { get; set; }
    public decimal? InpatientLimit { get; set; }
    public decimal? OutpatientLimit { get; set; }
    public decimal? DentalLimit { get; set; }
    public decimal? OpticalLimit { get; set; }
    public decimal? MaternityLimit { get; set; }
    public decimal? MentalHealthLimit { get; set; }
    public decimal? PrescriptionLimit { get; set; }
    public bool CoversDependents { get; set; }
    public int? MaxDependents { get; set; }
    public decimal? DependentAnnualLimit { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class MedicalBenefitTierSummaryDto
{
    public Guid Id { get; set; }
    public string TierName { get; set; } = string.Empty;
    public string? StaffLevelName { get; set; }
    public decimal AnnualLimit { get; set; }
    public bool IsActive { get; set; }
}

public class CreateMedicalBenefitTierDto : CreateDtoBase
{
    [Required]
    public Guid SchemeId { get; set; }
    [Required][MaxLength(200)]
    public string TierName { get; set; } = string.Empty;
    public Guid? StaffLevelId { get; set; }
    [MaxLength(200)]
    public string? TierDescription { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? InpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OutpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? DentalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OpticalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MaternityLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MentalHealthLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? PrescriptionLimit { get; set; }
    public bool CoversDependents { get; set; }
    [Range(0, int.MaxValue)]
    public int? MaxDependents { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? DependentAnnualLimit { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalBenefitTierDto : UpdateDtoBase
{
    [Required][MaxLength(200)]
    public string TierName { get; set; } = string.Empty;
    public Guid? StaffLevelId { get; set; }
    [MaxLength(200)]
    public string? TierDescription { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AnnualLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? InpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OutpatientLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? DentalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? OpticalLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MaternityLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? MentalHealthLimit { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? PrescriptionLimit { get; set; }
    public bool CoversDependents { get; set; }
    [Range(0, int.MaxValue)]
    public int? MaxDependents { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? DependentAnnualLimit { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Health Profile

public class EmployeeHealthProfileDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public BloodGroup BloodGroup { get; set; }
    public string BloodGroupName => BloodGroup.ToString();
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public double? BMI => (HeightCm.HasValue && WeightKg.HasValue && HeightCm > 0)
        ? Math.Round(WeightKg.Value / Math.Pow(HeightCm.Value / 100.0, 2), 2)
        : null;
    public DisabilityStatus DisabilityStatus { get; set; }
    public string DisabilityStatusName => DisabilityStatus.ToString();
    public string? DisabilityDescription { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public Guid? PreferredFacilityId { get; set; }
    public string? PreferredFacilityName { get; set; }
    public Guid? PreferredPhysicianId { get; set; }
    public string? PreferredPhysicianName { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? Notes { get; set; }
}

public class EmployeeHealthProfileDetailDto : EmployeeHealthProfileDto
{
    public List<EmployeeHealthConditionDto> Conditions { get; set; } = new();
    public List<EmployeeAllergyDto> Allergies { get; set; } = new();
    public List<EmployeeMedicalExamSummaryDto> MedicalExams { get; set; } = new();
}

public class CreateEmployeeHealthProfileDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public DisabilityStatus DisabilityStatus { get; set; } = DisabilityStatus.None;
    [MaxLength(500)]
    public string? DisabilityDescription { get; set; }
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }
    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }
    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }
    public Guid? PreferredFacilityId { get; set; }
    public Guid? PreferredPhysicianId { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeHealthProfileDto : UpdateDtoBase
{
    public BloodGroup BloodGroup { get; set; }
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public DisabilityStatus DisabilityStatus { get; set; }
    [MaxLength(500)]
    public string? DisabilityDescription { get; set; }
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }
    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }
    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }
    public Guid? PreferredFacilityId { get; set; }
    public Guid? PreferredPhysicianId { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Health Condition

public class EmployeeHealthConditionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid HealthProfileId { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    public string? ICDCode { get; set; }
    public HealthConditionSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public HealthConditionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly? DiagnosedDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public string? TreatmentSummary { get; set; }
    public string? Notes { get; set; }
}

public class CreateEmployeeHealthConditionDto : CreateDtoBase
{
    [Required]
    public Guid HealthProfileId { get; set; }
    [Required][MaxLength(300)]
    public string ConditionName { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? ICDCode { get; set; }
    [Required]
    public HealthConditionSeverity Severity { get; set; }
    [Required]
    public HealthConditionStatus Status { get; set; }
    public DateOnly? DiagnosedDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    [MaxLength(500)]
    public string? TreatmentSummary { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeHealthConditionDto : UpdateDtoBase
{
    [Required][MaxLength(300)]
    public string ConditionName { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? ICDCode { get; set; }
    [Required]
    public HealthConditionSeverity Severity { get; set; }
    [Required]
    public HealthConditionStatus Status { get; set; }
    public DateOnly? DiagnosedDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    [MaxLength(500)]
    public string? TreatmentSummary { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Allergy

public class EmployeeAllergyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid HealthProfileId { get; set; }
    public string Allergen { get; set; } = string.Empty;
    public AllergyType AllergyType { get; set; }
    public string AllergyTypeName => AllergyType.ToString();
    public AllergySeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public string? ReactionDescription { get; set; }
    public string? ManagementPlan { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class CreateEmployeeAllergyDto : CreateDtoBase
{
    [Required]
    public Guid HealthProfileId { get; set; }
    [Required][MaxLength(200)]
    public string Allergen { get; set; } = string.Empty;
    [Required]
    public AllergyType AllergyType { get; set; }
    [Required]
    public AllergySeverity Severity { get; set; }
    [MaxLength(500)]
    public string? ReactionDescription { get; set; }
    [MaxLength(300)]
    public string? ManagementPlan { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeAllergyDto : UpdateDtoBase
{
    [Required][MaxLength(200)]
    public string Allergen { get; set; } = string.Empty;
    [Required]
    public AllergyType AllergyType { get; set; }
    [Required]
    public AllergySeverity Severity { get; set; }
    [MaxLength(500)]
    public string? ReactionDescription { get; set; }
    [MaxLength(300)]
    public string? ManagementPlan { get; set; }
    public bool IsActive { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Medical Exam

public class EmployeeMedicalExamDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid HealthProfileId { get; set; }
    public DateOnly ExamDate { get; set; }
    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public Guid? PhysicianId { get; set; }
    public string? PhysicianName { get; set; }
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    public string? BloodPressure { get; set; }
    public double? BMIRecorded { get; set; }
    public string? VisionResult { get; set; }
    public string? HearingResult { get; set; }
    public MedicalExamResult Result { get; set; }
    public string ResultName => Result.ToString();
    public string? Findings { get; set; }
    public string? Recommendations { get; set; }
    public string? Restrictions { get; set; }
    public DateOnly? NextExamDueDate { get; set; }
    public Guid? LinkedClaimId { get; set; }
    public string? LinkedClaimNumber { get; set; }
    public string? Notes { get; set; }
    public List<EmployeeMedicalExamDocumentDto> Documents { get; set; } = new();
}

public class EmployeeMedicalExamSummaryDto
{
    public Guid Id { get; set; }
    public DateOnly ExamDate { get; set; }
    public string? FacilityName { get; set; }
    public MedicalExamResult Result { get; set; }
    public string ResultName => Result.ToString();
    public DateOnly? NextExamDueDate { get; set; }
}

public class EmployeeMedicalExamDetailDto : EmployeeMedicalExamDto
{
    public new List<EmployeeMedicalExamDocumentDto> Documents { get; set; } = new();
}

public class CreateEmployeeMedicalExamDto : CreateDtoBase
{
    [Required]
    public Guid HealthProfileId { get; set; }
    [Required]
    public DateOnly ExamDate { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    [MaxLength(20)]
    public string? BloodPressure { get; set; }
    public double? BMIRecorded { get; set; }
    [MaxLength(200)]
    public string? VisionResult { get; set; }
    [MaxLength(200)]
    public string? HearingResult { get; set; }
    [Required]
    public MedicalExamResult Result { get; set; }
    [MaxLength(1000)]
    public string? Findings { get; set; }
    [MaxLength(1000)]
    public string? Recommendations { get; set; }
    [MaxLength(500)]
    public string? Restrictions { get; set; }
    public DateOnly? NextExamDueDate { get; set; }
    public Guid? LinkedClaimId { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeMedicalExamDto : UpdateDtoBase
{
    [Required]
    public DateOnly ExamDate { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }
    [MaxLength(20)]
    public string? BloodPressure { get; set; }
    public double? BMIRecorded { get; set; }
    [MaxLength(200)]
    public string? VisionResult { get; set; }
    [MaxLength(200)]
    public string? HearingResult { get; set; }
    [Required]
    public MedicalExamResult Result { get; set; }
    [MaxLength(1000)]
    public string? Findings { get; set; }
    [MaxLength(1000)]
    public string? Recommendations { get; set; }
    [MaxLength(500)]
    public string? Restrictions { get; set; }
    public DateOnly? NextExamDueDate { get; set; }
    public Guid? LinkedClaimId { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Employee Medical Exam Document

public class EmployeeMedicalExamDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ExamId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class CreateEmployeeMedicalExamDocumentDto : CreateDtoBase
{
    [Required]
    public Guid ExamId { get; set; }
    [Required][MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Legacy storage path. Set only by the migration utility for pre-existing rows — the
    /// upload endpoint leaves it empty and uses <see cref="FileUploadRecordId"/>. This was
    /// previously accepted straight from the API caller, which let anyone point a medical
    /// document at arbitrary bytes on disk.
    /// </summary>
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

#endregion

#region Medical Claim Pre-Authorization

public class MedicalClaimPreAuthorizationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AuthorizationNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? DependentId { get; set; }
    public string? DependentName { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public Guid? PhysicianId { get; set; }
    public string? PhysicianName { get; set; }
    public MedicalServiceType ServiceType { get; set; }
    public string ServiceTypeName => ServiceType.ToString();
    public bool IsEmergency { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string ProposedTreatment { get; set; } = string.Empty;
    public DateTime? PlannedServiceDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public DateTime RequestDate { get; set; }
    public ClaimPreAuthorizationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public decimal? AuthorizedAmount { get; set; }
    public DateTime? AuthorizationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
}

public class MedicalClaimPreAuthorizationSummaryDto
{
    public Guid Id { get; set; }
    public string AuthorizationNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public MedicalServiceType ServiceType { get; set; }
    public string ServiceTypeName => ServiceType.ToString();
    public ClaimPreAuthorizationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? PlannedServiceDate { get; set; }
    public decimal? EstimatedCost { get; set; }
}

public class CreateMedicalClaimPreAuthorizationDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    public Guid? DependentId { get; set; }
    [Required]
    public Guid PolicyId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
    [Required]
    public MedicalServiceType ServiceType { get; set; }
    public bool IsEmergency { get; set; }
    [Required][MaxLength(500)]
    public string Diagnosis { get; set; } = string.Empty;
    [Required][MaxLength(1000)]
    public string ProposedTreatment { get; set; } = string.Empty;
    public DateTime? PlannedServiceDate { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalClaimPreAuthorizationDto : UpdateDtoBase
{
    public Guid? FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
    [Required]
    public MedicalServiceType ServiceType { get; set; }
    public bool IsEmergency { get; set; }
    [Required][MaxLength(500)]
    public string Diagnosis { get; set; } = string.Empty;
    [Required][MaxLength(1000)]
    public string ProposedTreatment { get; set; } = string.Empty;
    public DateTime? PlannedServiceDate { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ApproveMedicalClaimPreAuthorizationDto
{
    [Required]
    public Guid PreAuthorizationId { get; set; }
    [Required]
    public Guid ApprovedBy { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? AuthorizedAmount { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class RejectMedicalClaimPreAuthorizationDto
{
    [Required]
    public Guid PreAuthorizationId { get; set; }
    [Required][MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Referral

public class MedicalReferralDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ReferralNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    public string? DependentName { get; set; }
    public Guid? ReferringFacilityId { get; set; }
    public string? ReferringFacilityName { get; set; }
    public Guid? ReferringPhysicianId { get; set; }
    public string? ReferringPhysicianName { get; set; }
    public Guid? ReferredToFacilityId { get; set; }
    public string? ReferredToFacilityName { get; set; }
    public Guid? ReferredToPhysicianId { get; set; }
    public string? ReferredToPhysicianName { get; set; }
    public DateTime ReferralDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public MedicalReferralPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public string? Diagnosis { get; set; }
    public string ReasonForReferral { get; set; } = string.Empty;
    public MedicalReferralStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? CompletedDate { get; set; }
    public string? OutcomeSummary { get; set; }
    public string? Notes { get; set; }
}

public class MedicalReferralSummaryDto
{
    public Guid Id { get; set; }
    public string ReferralNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public MedicalReferralPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public MedicalReferralStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime ReferralDate { get; set; }
}

public class CreateMedicalReferralDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    public Guid? ReferringFacilityId { get; set; }
    public Guid? ReferringPhysicianId { get; set; }
    public Guid? ReferredToFacilityId { get; set; }
    public Guid? ReferredToPhysicianId { get; set; }
    [Required]
    public DateTime ReferralDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [Required]
    public MedicalReferralPriority Priority { get; set; }
    [MaxLength(500)]
    public string? Diagnosis { get; set; }
    [Required][MaxLength(1000)]
    public string ReasonForReferral { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalReferralDto : UpdateDtoBase
{
    public Guid? ReferredToFacilityId { get; set; }
    public Guid? ReferredToPhysicianId { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [Required]
    public MedicalReferralPriority Priority { get; set; }
    [MaxLength(500)]
    public string? Diagnosis { get; set; }
    [Required][MaxLength(1000)]
    public string ReasonForReferral { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalReferralStatusDto
{
    [Required]
    public Guid ReferralId { get; set; }
    [Required]
    public MedicalReferralStatus Status { get; set; }
    [MaxLength(1000)]
    public string? OutcomeSummary { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CompleteMedicalReferralDto
{
    [Required]
    public Guid ReferralId { get; set; }
    [MaxLength(1000)]
    public string? OutcomeSummary { get; set; }
    public DateTime CompletedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Appointment

public class MedicalAppointmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    public string? DependentName { get; set; }
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public Guid? PhysicianId { get; set; }
    public string? PhysicianName { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public MedicalServiceType ServiceType { get; set; }
    public string ServiceTypeName => ServiceType.ToString();
    public string Purpose { get; set; } = string.Empty;
    public MedicalAppointmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? OutcomeSummary { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? LinkedReferralId { get; set; }
    public string? LinkedReferralNumber { get; set; }
    public Guid? LinkedClaimId { get; set; }
    public string? LinkedClaimNumber { get; set; }
    public string? Notes { get; set; }
}

public class MedicalAppointmentSummaryDto
{
    public Guid Id { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public DateTime AppointmentDateTime { get; set; }
    public MedicalAppointmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateMedicalAppointmentDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    [Required]
    public Guid FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
    [Required]
    public DateTime AppointmentDateTime { get; set; }
    [Range(1, 480)]
    public int? DurationMinutes { get; set; }
    public MedicalServiceType ServiceType { get; set; }
    [Required][MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalAppointmentDto : UpdateDtoBase
{
    public Guid? PhysicianId { get; set; }
    [Required]
    public DateTime AppointmentDateTime { get; set; }
    [Range(1, 480)]
    public int? DurationMinutes { get; set; }
    public MedicalServiceType ServiceType { get; set; }
    [Required][MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateMedicalAppointmentStatusDto
{
    [Required]
    public Guid AppointmentId { get; set; }
    [Required]
    public MedicalAppointmentStatus Status { get; set; }
    [MaxLength(1000)]
    public string? OutcomeSummary { get; set; }
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
}

public class CancelMedicalAppointmentDto
{
    [Required]
    public Guid AppointmentId { get; set; }
    [Required][MaxLength(500)]
    public string CancellationReason { get; set; } = string.Empty;
}

public class CheckInMedicalAppointmentDto
{
    [Required]
    public Guid AppointmentId { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
}

public class CheckOutMedicalAppointmentDto
{
    [Required]
    public Guid AppointmentId { get; set; }
    public DateTime CheckOutTime { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)]
    public string? OutcomeSummary { get; set; }
}

#endregion

#region NHIS Claim

public class NHISClaimDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    public string? DependentName { get; set; }
    public string NHISMembershipNumber { get; set; } = string.Empty;
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public Guid? PhysicianId { get; set; }
    public string? PhysicianName { get; set; }
    public DateTime ServiceDate { get; set; }
    public MedicalServiceType ServiceType { get; set; }
    public string ServiceTypeName => ServiceType.ToString();
    public string ServiceDescription { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public string? ICDCode { get; set; }
    public decimal TotalCost { get; set; }
    public decimal? NHISCoveredAmount { get; set; }
    public decimal? CoPayAmount { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public NHISClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ApprovalDate { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public DateTime? RejectionDate { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public Guid? LinkedMedicalClaimId { get; set; }
    public string? LinkedMedicalClaimNumber { get; set; }
    public string? Notes { get; set; }
}

public class NHISClaimSummaryDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime ServiceDate { get; set; }
    public decimal TotalCost { get; set; }
    public NHISClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class NHISClaimDetailDto : NHISClaimDto
{
    public List<NHISClaimDocumentDto> Documents { get; set; } = new();
}

public class CreateNHISClaimDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    [MaxLength(100)]
    public string NHISMembershipNumber { get; set; } = string.Empty;
    [Required]
    public Guid FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
    [Required]
    public DateTime ServiceDate { get; set; }
    [Required]
    public MedicalServiceType ServiceType { get; set; }
    [Required][MaxLength(1000)]
    public string ServiceDescription { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Diagnosis { get; set; }
    [MaxLength(20)]
    public string? ICDCode { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal TotalCost { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? NHISCoveredAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? CoPayAmount { get; set; }
    public Guid? LinkedMedicalClaimId { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateNHISClaimDto : UpdateDtoBase
{
    [MaxLength(100)]
    public string NHISMembershipNumber { get; set; } = string.Empty;
    public Guid? PhysicianId { get; set; }
    [Required]
    public DateTime ServiceDate { get; set; }
    [Required]
    public MedicalServiceType ServiceType { get; set; }
    [Required][MaxLength(1000)]
    public string ServiceDescription { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Diagnosis { get; set; }
    [MaxLength(20)]
    public string? ICDCode { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal TotalCost { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? NHISCoveredAmount { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? CoPayAmount { get; set; }
    [MaxLength(100)]
    public string? BatchNumber { get; set; }
    public Guid? LinkedMedicalClaimId { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateNHISClaimStatusDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required]
    public NHISClaimStatus Status { get; set; }
    [Range(0, double.MaxValue)]
    public decimal? ApprovedAmount { get; set; }
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class SubmitNHISClaimDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [MaxLength(100)]
    public string? BatchNumber { get; set; }
    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;
}

public class RecordNHISClaimPaymentDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required][MaxLength(100)]
    public string PaymentReference { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region NHIS Claim Document

public class NHISClaimDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid NHISClaimId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class CreateNHISClaimDocumentDto : CreateDtoBase
{
    [Required]
    public Guid NHISClaimId { get; set; }
    [Required][MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    [Required][MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
}

#endregion

#region Medical Expense Claim

public class MedicalExpenseClaimDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    public string? DependentName { get; set; }
    public DateTime ClaimDate { get; set; }
    public DateTime ServiceDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public MedicalExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName => ExpenseType.ToString();
    public string Description { get; set; } = string.Empty;
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public Guid? PhysicianId { get; set; }
    public string? PhysicianName { get; set; }
    public string? Diagnosis { get; set; }
    public string? ICDCode { get; set; }
    public string? TreatmentReceived { get; set; }
    public bool IsEmergency { get; set; }
    public bool RequiredHospitalization { get; set; }
    public DateOnly? AdmissionStart { get; set; }
    public DateOnly? AdmissionEnd { get; set; }
    public Guid? PreAuthorizationId { get; set; }
    public string? PreAuthorizationNumber { get; set; }
    public Guid? ReferralId { get; set; }
    public string? ReferralNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountRequested { get; set; }
    public Guid? InsurancePolicyId { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public Guid? LeaveRequestId { get; set; }
    public string? LeaveRequestNumber { get; set; }
    public ClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal? AmountApproved { get; set; }
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentMethodName => PaymentMethod?.ToString();
    public bool IsFlaggedForReview { get; set; }
    public string? FlagReason { get; set; }
    public string? AdditionalNotes { get; set; }
}

public class MedicalExpenseClaimSummaryDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsForDependent { get; set; }
    public string? DependentName { get; set; }
    public DateTime ClaimDate { get; set; }
    public DateTime ServiceDate { get; set; }
    public MedicalExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName => ExpenseType.ToString();
    public string FacilityName { get; set; } = string.Empty;
    public decimal AmountRequested { get; set; }
    public decimal? AmountApproved { get; set; }
    public ClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsFlaggedForReview { get; set; }
}

public class MedicalExpenseClaimDetailDto : MedicalExpenseClaimDto
{
    public List<MedicalExpenseApprovalDto> Approvals { get; set; } = new();
    public List<MedicalExpenseDocumentDto> Documents { get; set; } = new();
    public List<MedicalExpenseItemDto> Items { get; set; } = new();
    public List<MedicalExpenseClaimNoteDto> Notes { get; set; } = new();
    public List<MedicalInsuranceClaimSummaryDto> InsuranceClaims { get; set; } = new();
    public List<NHISClaimSummaryDto> LinkedNHISClaims { get; set; } = new();
}

public class CreateMedicalExpenseClaimDto : CreateDtoBase
{
    /// <summary>
    /// The employee the claim is for. <b>Required</b> on the HR endpoint
    /// (<c>POST api/medical-expense-claims</c>), which files on an employee's behalf.
    /// <b>Ignored</b> on the employee self-service endpoint, which always files for the
    /// authenticated caller and never accepts a caller-supplied subject.
    /// </summary>
    public Guid? EmployeeId { get; set; }
    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    [Required]
    public DateTime ServiceDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    [Required]
    public MedicalExpenseType ExpenseType { get; set; }
    [Required][MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required]
    public Guid FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
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
    public Guid? PreAuthorizationId { get; set; }
    public Guid? ReferralId { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AmountRequested { get; set; }
    public Guid? InsurancePolicyId { get; set; }
    public Guid? LeaveRequestId { get; set; }
    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
    public List<CreateMedicalExpenseItemDto>? Items { get; set; }
}

public class UpdateMedicalExpenseClaimDto : UpdateDtoBase
{
    [Required]
    public DateTime ServiceDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    [Required]
    public MedicalExpenseType ExpenseType { get; set; }
    [Required][MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required]
    public Guid FacilityId { get; set; }
    public Guid? PhysicianId { get; set; }
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
    public Guid? PreAuthorizationId { get; set; }
    public Guid? ReferralId { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal AmountRequested { get; set; }
    public Guid? InsurancePolicyId { get; set; }
    public Guid? LeaveRequestId { get; set; }
    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}

public class ProcessMedicalExpenseClaimDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required]
    public MedicalExpenseApprovalStatus Status { get; set; }
    // ApproverId is derived server-side from the authenticated employee, not accepted from the client.
    [Range(0, double.MaxValue)]
    public decimal? AmountApproved { get; set; }
    [MaxLength(500)]
    public string? Comments { get; set; }
}

public class ProcessMedicalExpensePaymentDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required]
    public PaymentMethod PaymentMethod { get; set; }
    [Required][MaxLength(100)]
    public string PaymentReference { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
}

public class FlagMedicalExpenseClaimDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required][MaxLength(500)]
    public string FlagReason { get; set; } = string.Empty;
}

public class UnflagMedicalExpenseClaimDto
{
    [Required]
    public Guid ClaimId { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Medical Expense Approval

public class MedicalExpenseApprovalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ClaimId { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public MedicalExpenseApprovalStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ActionDate { get; set; }
    public string? Comments { get; set; }
}

#endregion

#region Medical Expense Item

public class MedicalExpenseItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ClaimId { get; set; }
    public string Description { get; set; } = string.Empty;
    public MedicalItemType ItemType { get; set; }
    public string ItemTypeName => ItemType.ToString();
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
    public string? Remarks { get; set; }
}

public class CreateMedicalExpenseItemDto : CreateDtoBase
{
    public Guid ClaimId { get; set; }
    [Required][MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    [Required]
    public MedicalItemType ItemType { get; set; }
    [Required][Range(1, int.MaxValue)]
    public int Quantity { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }
    [MaxLength(500)]
    public string? Remarks { get; set; }
}

public class UpdateMedicalExpenseItemDto : UpdateDtoBase
{
    public Guid ClaimId { get; set; }
    [Required][MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    [Required]
    public MedicalItemType ItemType { get; set; }
    [Required][Range(1, int.MaxValue)]
    public int Quantity { get; set; }
    [Required][Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }
    [MaxLength(500)]
    public string? Remarks { get; set; }
}

#endregion

#region Medical Expense Document

public class MedicalExpenseDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ClaimId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MedicalDocumentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Internal carrier for a claim document that has already been through the controlled-upload gate.
/// </summary>
/// <remarks>
/// <b>Not a request body.</b> Receipts arrive as multipart content on
/// <c>POST api/medical-expense-claims/{claimId}/documents</c>, which scans and stores the bytes and
/// then fills this in. <c>FilePath</c> is retained only for rows written before that gate existed and
/// is left empty on new ones; it was previously accepted from the caller, which made it a
/// path-injection sink.
/// </remarks>
public class CreateMedicalExpenseDocumentDto : CreateDtoBase
{
    [Required]
    public Guid ClaimId { get; set; }
    [Required][MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [Required]
    public MedicalDocumentType Type { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
}

#endregion

#region Medical Expense Claim Note

public class MedicalExpenseClaimNoteDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ClaimId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public MedicalExpenseClaimNoteType NoteType { get; set; }
    public string NoteTypeName => NoteType.ToString();
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime NoteDate { get; set; }
}

public class AddMedicalExpenseClaimNoteDto : CreateDtoBase
{
    [Required]
    public Guid ClaimId { get; set; }
    // AuthorId is derived server-side from the authenticated employee, not accepted from the client.
    [Required]
    public MedicalExpenseClaimNoteType NoteType { get; set; }
    [Required][MaxLength(3000)]
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

#endregion
