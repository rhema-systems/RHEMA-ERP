using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.DTOs.HR;

#region Employee DTOs

/// <summary>
/// Basic employee information for lists and searches
/// </summary>
public class EmployeeDto
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string? StaffLevelName { get; set; }

    // Preferred org assignment display (replaces Department/Section over time)
    public string? OrganizationLevelName { get; set; }
    public string? OrganizationUnitName { get; set; }
    public string? LocationLevelName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }

    public StaffStatus StaffStatus { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public bool IsActive { get; set; }
    public bool IsFullTime { get; set; }
    public bool IsExpatriate { get; set; }
    public DateOnly? DateEmployed { get; set; }
    public int? YearsOfService { get; set; }
    public bool CanBeAssignedToMaintenance { get; set; }
    public string? PicturePath { get; set; }
}

/// <summary>
/// Detailed employee information including all related data
/// </summary>
public class EmployeeDetailDto : EmployeeDto
{
    // Foreign keys / IDs (useful for edit forms)
    public Guid? DepartmentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid PositionId { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? ShiftId { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? Religion { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public string? CountryName { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Extension { get; set; }
    public int ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? RetirementDate { get; set; }
    public string? TaxNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public string? TINNumber { get; set; }
    public BloodType? BloodType { get; set; }
    public string? ShiftName { get; set; }
    public decimal? Salary { get; set; }
    // Payroll/tax switches (additive)
    public bool PayTax { get; set; }
    public bool SSFund { get; set; }
    public bool GrossUp { get; set; }
    public bool Tier2Only { get; set; }
    public bool Overtime { get; set; }
    public string? BadgeNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastPromotionDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? StationName { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? TerminationNotes { get; set; }

    // Convenience UI flag (computed in domain; projected here)
    public bool IsOnProbation { get; set; }

    // Related collections
    public List<EmployeeContactDto> Contacts { get; set; } = new();
    public List<EmployeeEmergencyContactDto> EmergencyContacts { get; set; } = new();
    public List<EmployeeDependentDto> Dependents { get; set; } = new();
    public List<EmployeeQualificationDto> Qualifications { get; set; } = new();
    public List<EmployeeSkillDto> Skills { get; set; } = new();
    public List<EmployeeContractDetailDto> ContractDetails { get; set; } = new();
}

/// <summary>
/// Full employee profile used by HR "360" screens. Composes the large aggregate.
/// </summary>
public class EmployeeFullProfileDto : EmployeeDetailDto
{
    public List<EmployeeIdentificationCardListDto> IdentificationCards { get; set; } = new();
    public List<EmployeeWorkHistoryListDto> WorkHistories { get; set; } = new();
    public List<ExpatriateAssignmentListDto> ExpatriateAssignments { get; set; } = new();
    public List<EmployeePositionHistoryListDto> PositionHistories { get; set; } = new();
    public List<EmployeeSalaryAssignmentListDto> SalaryAssignments { get; set; } = new();
    public List<EmployeeRefereeListDto> Referees { get; set; } = new();
    public List<EmployeeGuarantorListDto> Guarantors { get; set; } = new();
    public List<EmployeeDependentBenefitDto> DependentBenefits { get; set; } = new();
    public List<EmployeeBankDetailDto> BankDetails { get; set; } = new();
}

/// <summary>
/// DTO for creating new employees
/// </summary>
public class CreateEmployeeDto
{
    // Optional: the service auto-generates an employee number when this is blank.
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Title { get; set; }

    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? Religion { get; set; }
    public bool IsFullTime { get; set; } = true;
    public DateOnly? DateEmployed { get; set; }

    // Contact Information
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }

    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    public string? TelephoneNumber { get; set; }
    public string? BusinessNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? Extension { get; set; }

    // Employment Details
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public int ProbationPeriodDays { get; set; } = 90;
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? RetirementDate { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? SectionId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public Guid OrganizationUnitId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? ManagerId { get; set; }

    public StaffStatus StaffStatus { get; set; } = StaffStatus.Active;
    public string? TaxNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public string? TINNumber { get; set; }
    public BloodType? BloodType { get; set; }
    public Guid? ShiftId { get; set; }
    public decimal? Salary { get; set; }
    // Payroll/tax switches
    public bool PayTax { get; set; }
    public bool SSFund { get; set; }
    public bool GrossUp { get; set; }
    public bool Tier2Only { get; set; }
    public bool Overtime { get; set; }
    public string? BadgeNumber { get; set; }
    public string? PicturePath { get; set; }
    public string? Notes { get; set; }

    public bool IsExpatriate { get; set; }
}

/// <summary>
/// DTO for updating employee information
/// </summary>
public class UpdateEmployeeDto
{
    public string? EmployeeNumber { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? Religion { get; set; }
    public bool IsFullTime { get; set; }

    // Contact Information
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }
    public string? EmailAddress { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? BusinessNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? Extension { get; set; }

    // Employment Details
    public EmploymentType? EmploymentType { get; set; }
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? RetirementDate { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public StaffStatus? StaffStatus { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ManagerId { get; set; }
    public bool? IsExpatriate { get; set; }
    public string? TaxNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public string? TINNumber { get; set; }
    public BloodType? BloodType { get; set; }
    public Guid? ShiftId { get; set; }
    public decimal? Salary { get; set; }
    // Payroll/tax switches
    public bool? PayTax { get; set; }
    public bool? SSFund { get; set; }
    public bool? GrossUp { get; set; }
    public bool? Tier2Only { get; set; }
    public bool? Overtime { get; set; }
    public string? BadgeNumber { get; set; }
    public string? PicturePath { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastPromotionDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? TerminationNotes { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Employee search and filter criteria
/// </summary>
public class EmployeeSearchDto
{
    public string? SearchTerm { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public StaffStatus? StaffStatus { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFullTime { get; set; }
    public bool? MaintenanceTechniciansOnly { get; set; }
    public DateOnly? HiredAfter { get; set; }
    public DateOnly? HiredBefore { get; set; }
    public int? MinYearsOfService { get; set; }
    public int? MaxYearsOfService { get; set; }
}

public class TerminateEmployeeDto
{
    public DateTime TerminationDate { get; set; }
    public string TerminationReason { get; set; } = string.Empty;
    public string? TerminationNotes { get; set; }
}

#endregion

#region Employee Related DTOs

/// <summary>
/// A residential or postal address record for an employee.
/// </summary>
public class EmployeeContactDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public EmployeeContactType ContactType { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// DTO for creating an employee address/contact record.
/// </summary>
public class CreateEmployeeContactDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    public EmployeeContactType ContactType { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(30)]
    public string? DigitalAddress { get; set; }

    public Guid? CountryId { get; set; }

    public bool IsPrimary { get; set; }
}

/// <summary>
/// DTO for updating an employee address/contact record (patch-style).
/// </summary>
public class UpdateEmployeeContactDto
{
    [Required]
    public Guid Id { get; set; }

    public EmployeeContactType? ContactType { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(30)]
    public string? DigitalAddress { get; set; }

    public Guid? CountryId { get; set; }

    public bool? IsPrimary { get; set; }
}

/// <summary>
/// Employee emergency contact information
/// </summary>
public class EmployeeEmergencyContactDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public EmergencyContactType ContactType { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternatePhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? CountryId { get; set; }
    public string? DigitalAddress { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating emergency contacts
/// </summary>
public class CreateEmployeeEmergencyContactDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Relationship { get; set; } = string.Empty;

    public EmergencyContactType ContactType { get; set; }

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? AlternatePhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? CountryId { get; set; }
    public string? DigitalAddress { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating emergency contacts (additive; does not replace existing DTOs).
/// </summary>
public class UpdateEmployeeEmergencyContactDto
{
    [Required]
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public EmergencyContactType? ContactType { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AlternatePhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? CountryId { get; set; }
    public string? DigitalAddress { get; set; }
    public bool? IsPrimary { get; set; }
    public bool? IsActive { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee dependent information
/// </summary>
public class EmployeeDependentDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Occupation { get; set; }
    public bool IsEligibleForBenefits { get; set; }
    public int? Age { get; set; }
    public bool IsDeceased { get; set; }
}

/// <summary>
/// Preferred dependent read model (stable IDs + enums). Keeps legacy EmployeeDependentDto unchanged.
/// </summary>
public class EmployeeDependentReadDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;

    public DependentRelationship Relationship { get; set; }
    public string? RelationshipDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }

    public bool HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }

    public string? GhanaCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? DigitalAddress { get; set; }
    public string? Occupation { get; set; }

    public bool IsEligibleForBenefits { get; set; }
    public bool IsDeceased { get; set; }

    public string? PicturePath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Preferred dependent create model.
/// </summary>
public class EmployeeDependentCreateDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    public DependentRelationship Relationship { get; set; }

    [MaxLength(100)]
    public string? RelationshipDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }

    public bool HasDisability { get; set; }

    [MaxLength(500)]
    public string? DisabilityDescription { get; set; }

    [MaxLength(50)]
    public string? GhanaCardNumber { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    [MaxLength(100)]
    public string? Occupation { get; set; }

    public bool IsEligibleForBenefits { get; set; }
    public bool IsDeceased { get; set; }

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Preferred dependent update model.
/// </summary>
public class EmployeeDependentUpdateDto
{
    [Required]
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DependentRelationship? Relationship { get; set; }
    public string? RelationshipDescription { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public bool? HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    public string? GhanaCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? DigitalAddress { get; set; }
    public string? Occupation { get; set; }
    public bool? IsEligibleForBenefits { get; set; }
    public bool? IsDeceased { get; set; }
    public string? PicturePath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Dependent benefit read model.
/// </summary>
public class EmployeeDependentBenefitDto
{
    public Guid Id { get; set; }
    public Guid EmployeeDependentId { get; set; }
    public Guid PolicyId { get; set; }
    public string? PolicyName { get; set; }
    public DateOnly EnrolledDate { get; set; }
    public DateOnly? CoverageStartDate { get; set; }
    public DateOnly? CoverageEndDate { get; set; }
    public decimal BenefitAmountUsed { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmployeeDependentBenefitDto
{
    [Required]
    public Guid EmployeeDependentId { get; set; }

    [Required]
    public Guid PolicyId { get; set; }

    public DateOnly EnrolledDate { get; set; }
    public DateOnly? CoverageStartDate { get; set; }
    public DateOnly? CoverageEndDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal BenefitAmountUsed { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeDependentBenefitDto
{
    [Required]
    public Guid Id { get; set; }

    public DateOnly? CoverageStartDate { get; set; }
    public DateOnly? CoverageEndDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? BenefitAmountUsed { get; set; }

    public bool? IsActive { get; set; }
}

/// <summary>
/// DTO for creating dependents
/// </summary>
public class CreateEmployeeDependentDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Relationship { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? GhanaCardNumber { get; set; }
    public string? DigitalAddress { get; set; }
    public string? Occupation { get; set; }
    public bool IsStudentDependent { get; set; }
    public bool IsEmergencyContact { get; set; }
    public bool IsEligibleForBenefits { get; set; }
    public bool IsDeceased { get; set; }
    public string? PicturePath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee qualification information
/// </summary>
public class EmployeeQualificationDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? QualificationId { get; set; }
    public string QualificationName { get; set; } = string.Empty;
    public string? CustomQualificationName { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    public string? Grade { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? Description { get; set; }
    public bool IsVerified { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating qualifications
/// </summary>
public class CreateEmployeeQualificationDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Reference to master Qualification if selected from dropdown
    /// </summary>
    public Guid? QualificationId { get; set; }

    /// <summary>
    /// Custom qualification name if not selected from dropdown.
    /// This will be used if QualificationId is not provided.
    /// </summary>
    public string? CustomQualificationName { get; set; }

    [Required]
    public string Institution { get; set; } = string.Empty;

    public string? FieldOfStudy { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    public string? Grade { get; set; }
    public Guid? CountryId { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating qualifications.
/// </summary>
public class UpdateEmployeeQualificationDto
{
    [Required]
    public Guid Id { get; set; }

    public Guid? QualificationId { get; set; }
    public string? CustomQualificationName { get; set; }
    public string? Institution { get; set; }
    public string? FieldOfStudy { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    public string? Grade { get; set; }
    public Guid? CountryId { get; set; }
    public string? Description { get; set; }
    public bool? IsVerified { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee skill information with proficiency
/// </summary>
public class EmployeeSkillDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
    public SkillLevel SkillLevel { get; set; }
    public DateOnly? AcquiredDate { get; set; }
    public DateOnly? CertificationDate { get; set; }
    public DateOnly? CertificationExpiryDate { get; set; }
    public string? CertificationNumber { get; set; }
    public string? CertifyingBody { get; set; }
    public bool IsVerified { get; set; }
    public bool IsCertificationExpired { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating employee skills.
/// </summary>
public class UpdateEmployeeSkillDto
{
    [Required]
    public Guid Id { get; set; }

    public SkillLevel? SkillLevel { get; set; }
    public DateOnly? AcquiredDate { get; set; }
    public bool? IsCertified { get; set; }
    public DateOnly? CertificationDate { get; set; }
    public DateOnly? CertificationExpiryDate { get; set; }
    public string? CertificationNumber { get; set; }
    public string? CertifyingBody { get; set; }
    public string? Notes { get; set; }
    public bool? IsVerified { get; set; }
}

/// <summary>
/// DTO for creating employee skills
/// </summary>
public class CreateEmployeeSkillDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    public SkillLevel SkillLevel { get; set; } = SkillLevel.Beginner;
    public DateOnly? AcquiredDate { get; set; }
    public DateOnly? CertificationDate { get; set; }
    public DateOnly? CertificationExpiryDate { get; set; }
    public string? CertificationNumber { get; set; }
    public string? CertifyingBody { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee contract detail information
/// </summary>
public class EmployeeContractDetailDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public EmploymentType EmploymentType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Salary { get; set; }
    public string PayFrequency { get; set; } = string.Empty;
    public PayFrequency? PayFrequencyType { get; set; }

    public TaxTreatmentType? TaxTreatmentType { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRate { get; set; }

    public bool? IsPensionApplicable { get; set; }
    public bool? IsTaxExempt { get; set; }

    public ContractStatus? ContractStatus { get; set; }
    public int WorkingHoursPerWeek { get; set; }
    public int VacationDaysPerYear { get; set; }
    public int SickDaysPerYear { get; set; }
    public string? Terms { get; set; }
    public bool IsActive { get; set; }
    public string? ContractPath { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
}

/// <summary>
/// Employee contract detail create model.
/// </summary>
public class CreateEmployeeContractDetailDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ContractNumber { get; set; } = string.Empty;

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public decimal Salary { get; set; }

    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;

    public TaxTreatmentType TaxTreatmentType { get; set; } = TaxTreatmentType.PAYE;

    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRate { get; set; }

    public bool IsPensionApplicable { get; set; } = true;
    public bool IsTaxExempt { get; set; } = false;

    public int WorkingHoursPerWeek { get; set; } = 40;
    public int VacationDaysPerYear { get; set; } = 15;
    public int SickDaysPerYear { get; set; } = 10;
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }

    [MaxLength(1000)]
    public string? Terms { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? ContractPath { get; set; }

    public ContractStatus ContractStatus { get; set; } = ContractStatus.Active;

    public DateOnly? TerminationDate { get; set; }

    [MaxLength(1000)]
    public string? TerminationReason { get; set; }
}

/// <summary>
/// Employee contract detail update model.
/// </summary>
public class UpdateEmployeeContractDetailDto
{
    [Required]
    public Guid Id { get; set; }

    public EmploymentType? EmploymentType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? Salary { get; set; }
    public PayFrequency? PayFrequency { get; set; }
    public TaxTreatmentType? TaxTreatmentType { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRate { get; set; }

    public bool? IsPensionApplicable { get; set; }
    public bool? IsTaxExempt { get; set; }
    public int? WorkingHoursPerWeek { get; set; }
    public int? VacationDaysPerYear { get; set; }
    public int? SickDaysPerYear { get; set; }
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public string? Terms { get; set; }
    public bool? IsActive { get; set; }
    public string? ContractPath { get; set; }
    public ContractStatus? ContractStatus { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
}

#endregion

#region Employee Extended (Subresources)

/// <summary>
/// Identification type lookup for selectors.
/// </summary>
public class IdentificationTypeLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool HasExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Full IdentificationType details
/// </summary>
public class IdentificationTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string IssuingAuthorityName { get; set; } = string.Empty;
    public Guid? IssuingCountryId { get; set; }
    public string? IssuingCountryName { get; set; }
    public bool HasExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new IdentificationType
/// </summary>
public class CreateIdentificationTypeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(200)]
    public string IssuingAuthorityName { get; set; } = string.Empty;

    public Guid? IssuingCountryId { get; set; }

    public bool HasExpiryDate { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing IdentificationType
/// </summary>
public class UpdateIdentificationTypeDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(200)]
    public string IssuingAuthorityName { get; set; } = string.Empty;

    public Guid? IssuingCountryId { get; set; }

    public bool HasExpiryDate { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Employee identification card list projection.
/// </summary>
public class EmployeeIdentificationCardListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid IdentificationTypeId { get; set; }
    public string IdentificationTypeName { get; set; } = string.Empty;
    public string CardTypeName { get; set; } = string.Empty; // Actual property to populate
    public string CardNumber { get; set; } = string.Empty; // Actual card number
    public string? DocumentNumberMasked { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public bool IsVerified { get; set; }
    public string? DocumentPath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee identification card detail projection.
/// NOTE: Avoid exposing DocumentPath to untrusted clients.
/// </summary>
public class EmployeeIdentificationCardDetailDto : EmployeeIdentificationCardListDto
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? VerifiedDate { get; set; }
}

public class CreateEmployeeIdentificationCardDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid IdentificationTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public bool IsVerified { get; set; } = false;

    public DateTime? VerifiedDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeeIdentificationCardDto
{
    [Required]
    public Guid Id { get; set; }

    public Guid? IdentificationTypeId { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? DocumentPath { get; set; }
    public bool? IsVerified { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee work history list projection.
/// </summary>
public class EmployeeWorkHistoryListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

/// <summary>
/// Employee work history detail projection.
/// </summary>
public class EmployeeWorkHistoryDetailDto : EmployeeWorkHistoryListDto
{
    public string? CompanyAddress { get; set; }
    public string? JobDescription { get; set; }
    public decimal? Salary { get; set; }
    public string? ReasonForLeaving { get; set; }
    public string? SupervisorName { get; set; }
    public string? SupervisorPhone { get; set; }
    public bool CanContact { get; set; }
}

public class CreateEmployeeWorkHistoryDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CompanyAddress { get; set; }

    [Required]
    [MaxLength(100)]
    public string JobTitle { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? JobDescription { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public decimal? Salary { get; set; }

    [MaxLength(1000)]
    public string? ReasonForLeaving { get; set; }

    [MaxLength(200)]
    public string? SupervisorName { get; set; }

    [MaxLength(50)]
    public string? SupervisorPhone { get; set; }

    public bool CanContact { get; set; } = true;
}

public class UpdateEmployeeWorkHistoryDto
{
    [Required]
    public Guid Id { get; set; }

    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? JobTitle { get; set; }
    public string? JobDescription { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? Salary { get; set; }
    public string? ReasonForLeaving { get; set; }
    public string? SupervisorName { get; set; }
    public string? SupervisorPhone { get; set; }
    public bool? CanContact { get; set; }
}

/// <summary>
/// Expatriate assignment list projection.
/// </summary>
public class ExpatriateAssignmentListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid HomeCountryId { get; set; }
    public string? HomeCountryName { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool FamilyAccompanying { get; set; }
}

/// <summary>
/// Expatriate assignment detail projection.
/// </summary>
public class ExpatriateAssignmentDetailDto : ExpatriateAssignmentListDto
{
    public decimal? RelocationAllowance { get; set; }
    public DateOnly? RelocationDate { get; set; }
    public string? AssignmentObjective { get; set; }
    public string? VisaType { get; set; }
    public DateOnly? VisaExpiryDate { get; set; }
    public string? WorkPermitNumber { get; set; }
    public DateOnly? WorkPermitExpiryDate { get; set; }
}

public class CreateExpatriateAssignmentDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid HomeCountryId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public decimal? RelocationAllowance { get; set; }
    public DateOnly? RelocationDate { get; set; }
    public bool FamilyAccompanying { get; set; }

    [MaxLength(1000)]
    public string? AssignmentObjective { get; set; }

    [MaxLength(100)]
    public string? VisaType { get; set; }

    public DateOnly? VisaExpiryDate { get; set; }

    [MaxLength(100)]
    public string? WorkPermitNumber { get; set; }

    public DateOnly? WorkPermitExpiryDate { get; set; }
}

public class UpdateExpatriateAssignmentDto
{
    [Required]
    public Guid Id { get; set; }

    public Guid? HomeCountryId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? RelocationAllowance { get; set; }
    public DateOnly? RelocationDate { get; set; }
    public bool? FamilyAccompanying { get; set; }
    public string? AssignmentObjective { get; set; }
    public string? VisaType { get; set; }
    public DateOnly? VisaExpiryDate { get; set; }
    public string? WorkPermitNumber { get; set; }
    public DateOnly? WorkPermitExpiryDate { get; set; }
}

/// <summary>
/// Employee position history list projection.
/// </summary>
public class EmployeePositionHistoryListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PositionChangeReason ChangeReason { get; set; }
    public bool IsCurrent { get; set; }
}

public class EmployeePositionHistoryDetailDto : EmployeePositionHistoryListDto
{
    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? Notes { get; set; }
}

public class CreateEmployeePositionHistoryDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? LocationLevelId { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PositionChangeReason ChangeReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateEmployeePositionHistoryDto
{
    [Required]
    public Guid Id { get; set; }

    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PositionChangeReason? ChangeReason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee salary assignment list projection.
/// </summary>
public class EmployeeSalaryAssignmentListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid GradeId { get; set; }
    public string? GradeCode { get; set; }
    public string? GradeName { get; set; }
    public Guid? LevelId { get; set; }
    public string? LevelCode { get; set; }
    public Guid? NotchId { get; set; }
    public string? NotchNumber { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
    
    // Computed/projected amount from Grade/Level/Notch
    public decimal? Amount { get; set; }
}

public class EmployeeSalaryAssignmentDetailDto : EmployeeSalaryAssignmentListDto
{
    public string AssignmentReason { get; set; } = string.Empty;
}

public class CreateEmployeeSalaryAssignmentDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid GradeId { get; set; }

    public Guid? LevelId { get; set; }
    public Guid? NotchId { get; set; }

    public DateTime EffectiveDate { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [Required]
    public string AssignmentReason { get; set; } = string.Empty;
}

public class UpdateEmployeeSalaryAssignmentDto
{
    [Required]
    public Guid Id { get; set; }

    public Guid? GradeId { get; set; }
    public Guid? LevelId { get; set; }
    public Guid? NotchId { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? AssignmentReason { get; set; }
}

/// <summary>
/// Employee referee list projection.
/// </summary>
public class EmployeeRefereeListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public RefereeType RefereeType { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string? PositionOrTitle { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeeRefereeDetailDto : EmployeeRefereeListDto
{
    public bool IsContacted { get; set; }
    public DateTime? ContactedDate { get; set; }
    public string? ReferenceNotes { get; set; }
}

public class CreateEmployeeRefereeDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    public RefereeType RefereeType { get; set; } = RefereeType.Professional;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Organization { get; set; }

    [MaxLength(100)]
    public string? PositionOrTitle { get; set; }

    [Required]
    [MaxLength(200)]
    public string Relationship { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    [EmailAddress]
    public string? EmailAddress { get; set; }

    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeRefereeDto
{
    [Required]
    public Guid Id { get; set; }

    public RefereeType? RefereeType { get; set; }
    public string? FullName { get; set; }
    public string? Organization { get; set; }
    public string? PositionOrTitle { get; set; }
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public bool? IsContacted { get; set; }
    public DateTime? ContactedDate { get; set; }
    public string? ReferenceNotes { get; set; }
    public bool? IsPrimary { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Employee guarantor list projection (avoid PII-heavy fields).
/// </summary>
public class EmployeeGuarantorListDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public bool IsPrimary { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeeGuarantorDetailDto : EmployeeGuarantorListDto
{
    public string? MiddleName { get; set; }
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }

    public string? JobTitle { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerAddress { get; set; }
    public string? EmployerPhone { get; set; }
    public decimal? MonthlyIncome { get; set; }

    public string? NationalIdType { get; set; }
    public string? NationalIdNumberMasked { get; set; }
    public DateOnly? NationalIdExpiryDate { get; set; }

    public bool HasSignedGuarantorForm { get; set; }
    public DateOnly? DateFormSigned { get; set; }
    public string? GuarantorFormPath { get; set; }

    public DateTime? VerificationDate { get; set; }
    public Guid? VerifiedByEmployeeId { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastContactDate { get; set; }
}

public class CreateEmployeeGuarantorDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    public bool IsPrimary { get; set; }

    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Title { get; set; }

    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? EmailAddress { get; set; }

    [MaxLength(200)]
    public string? JobTitle { get; set; }

    [MaxLength(200)]
    public string? EmployerName { get; set; }

    [MaxLength(500)]
    public string? EmployerAddress { get; set; }

    [MaxLength(50)]
    public string? EmployerPhone { get; set; }

    public decimal? MonthlyIncome { get; set; }

    [MaxLength(50)]
    public string? NationalIdType { get; set; }

    [MaxLength(100)]
    public string? NationalIdNumber { get; set; }

    public DateOnly? NationalIdExpiryDate { get; set; }

    public bool HasSignedGuarantorForm { get; set; }
    public DateOnly? DateFormSigned { get; set; }

    [MaxLength(500)]
    public string? GuarantorFormPath { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeGuarantorDto
{
    [Required]
    public Guid Id { get; set; }

    public bool? IsPrimary { get; set; }
    public string? Relationship { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? JobTitle { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerAddress { get; set; }
    public string? EmployerPhone { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public string? NationalIdType { get; set; }
    public string? NationalIdNumber { get; set; }
    public DateOnly? NationalIdExpiryDate { get; set; }
    public bool? HasSignedGuarantorForm { get; set; }
    public DateOnly? DateFormSigned { get; set; }
    public string? GuarantorFormPath { get; set; }
    public bool? IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public Guid? VerifiedByEmployeeId { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastContactDate { get; set; }
    public bool? IsActive { get; set; }
}

// ─── Bank + Branch Reference DTOs ────────────────────────────────────────────

public class BankDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? SwiftCode { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public bool IsActive { get; set; }
    public int BranchCount { get; set; }
}

public class CreateBankDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? SwiftCode { get; set; }

    public Guid? CountryId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateBankDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(20)]
    public string? SwiftCode { get; set; }

    public Guid? CountryId { get; set; }

    public bool? IsActive { get; set; }
}

public class BankBranchDto
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}

public class CreateBankBranchDto
{
    [Required]
    public Guid BankId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateBankBranchDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public bool? IsActive { get; set; }
}

// ─── Bank Details ────────────────────────────────────────────────────────────

public class EmployeeBankDetailDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    // Structured references (populated when Bank/Branch entities are linked)
    public Guid? BankId { get; set; }
    public string? BankCode { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchCode { get; set; }
    // Free-text display values (from entity nav props when structured, or fallback strings otherwise)
    public string BankName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? MobileMoneyNumber { get; set; }
    public EmployeeBankAccountType AccountType { get; set; }
    public decimal AllocationPercentage { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public Guid? VerifiedById { get; set; }
}

public class CreateEmployeeBankDetailDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>Optional link to a Bank catalogue entry. When provided, BankName is derived from the entity.</summary>
    public Guid? BankId { get; set; }

    /// <summary>Optional link to a BankBranch catalogue entry. When provided, BranchName is derived from the entity.</summary>
    public Guid? BranchId { get; set; }

    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string BranchName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MobileMoneyNumber { get; set; }

    public EmployeeBankAccountType AccountType { get; set; }

    [Range(0.01, 100)]
    public decimal AllocationPercentage { get; set; } = 100;

    public bool IsPrimary { get; set; }
}

public class UpdateEmployeeBankDetailDto
{
    [Required]
    public Guid Id { get; set; }

    public Guid? BankId { get; set; }

    public Guid? BranchId { get; set; }

    [MaxLength(200)]
    public string? BankName { get; set; }

    [MaxLength(100)]
    public string? BranchName { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    [MaxLength(50)]
    public string? MobileMoneyNumber { get; set; }

    public EmployeeBankAccountType? AccountType { get; set; }

    [Range(0.01, 100)]
    public decimal? AllocationPercentage { get; set; }

    public bool? IsPrimary { get; set; }
    public bool? IsActive { get; set; }
}

#endregion

#region Organizational DTOs

/// <summary>
/// Department information
/// </summary>
public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepartmentType DepartmentType { get; set; }
    public Guid? ParentDepartmentId { get; set; }
    public string? ParentDepartmentName { get; set; }
    public Guid? DepartmentHeadId { get; set; }
    public string? DepartmentHeadName { get; set; }
    public decimal? Budget { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int EmployeeCount { get; set; }
    public int SectionCount { get; set; }
}

/// <summary>
/// DTO for creating departments
/// </summary>
public class CreateDepartmentDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepartmentType DepartmentType { get; set; } = DepartmentType.Operations;
    public Guid? ParentDepartmentId { get; set; }
    public Guid? DepartmentHeadId { get; set; }
    public decimal? Budget { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// Section information
/// </summary>
public class SectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public Guid? SectionHeadId { get; set; }
    public string? SectionHeadName { get; set; }
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
}

/// <summary>
/// DTO for creating sections
/// </summary>
public class CreateSectionDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? SectionHeadId { get; set; }
}

/// <summary>
/// Employee position information
/// </summary>
public class EmployeePositionDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid OrganizationLevelId { get; set; }
    public string OrganizationLevelName { get; set; } = string.Empty;

    public Guid OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;

    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }

    public Guid? ReportsToPositionId { get; set; }
    public string? ReportsToPositionTitle { get; set; }

    public int Level { get; set; }
    public int? MinimumExperienceYears { get; set; }
    public int? MinimumAge { get; set; }
    public int? MaximumAge { get; set; }

    public int ExpectedHeadcount { get; set; }

    public Guid? SalaryGradeId { get; set; }
    public string? SalaryGradeName { get; set; }

    public WorkMode WorkMode { get; set; }
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }

    public bool RequiresCertification { get; set; }
    public bool RequiresGuarantor { get; set; }
    public bool RequiresLicense { get; set; }

    public bool IsActive { get; set; }

    public int EmployeeCount { get; set; }
    public List<PositionSkillRequirementDto> SkillRequirements { get; set; } = new();
    public List<EmployeePositionBenefitDto> PositionBenefits { get; set; } = new();
}

/// <summary>
/// DTO for creating positions
/// </summary>
public class CreateEmployeePositionDto : IValidatableObject
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    [Required]
    public Guid OrganizationUnitId { get; set; }

    public Guid? StaffLevelId { get; set; }
    public Guid? ReportsToPositionId { get; set; }

    [Range(1, int.MaxValue)]
    public int Level { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int? MinimumExperienceYears { get; set; }

    [Range(0, 120)]
    public int? MinimumAge { get; set; }

    [Range(0, 120)]
    public int? MaximumAge { get; set; }

    [Range(1, int.MaxValue)]
    public int ExpectedHeadcount { get; set; } = 1;

    public Guid? SalaryGradeId { get; set; }

    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }

    public bool RequiresCertification { get; set; } = false;
    public bool RequiresGuarantor { get; set; } = false;
    public bool RequiresLicense { get; set; } = false;

    public ICollection<CreatePositionSkillRequirementDto> SkillRequirements { get; set; } = new List<CreatePositionSkillRequirementDto>();
    public ICollection<CreateEmployeePositionBenefitDto> PositionBenefits { get; set; } = new List<CreateEmployeePositionBenefitDto>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinimumAge.HasValue && MaximumAge.HasValue && MinimumAge.Value > MaximumAge.Value)
        {
            yield return new ValidationResult(
                "MinimumAge cannot be greater than MaximumAge.",
                new[] { nameof(MinimumAge), nameof(MaximumAge) });
        }
    }
}

/// <summary>
/// DTO for updating positions.
/// </summary>
public class UpdateEmployeePositionDto : IValidatableObject
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    [Required]
    public Guid OrganizationUnitId { get; set; }

    public Guid? StaffLevelId { get; set; }
    public Guid? ReportsToPositionId { get; set; }

    [Range(1, int.MaxValue)]
    public int Level { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int? MinimumExperienceYears { get; set; }

    [Range(0, 120)]
    public int? MinimumAge { get; set; }

    [Range(0, 120)]
    public int? MaximumAge { get; set; }

    [Range(1, int.MaxValue)]
    public int ExpectedHeadcount { get; set; } = 1;

    public Guid? SalaryGradeId { get; set; }

    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }
    public bool RequiresCertification { get; set; } = false;
    public bool RequiresGuarantor { get; set; } = false;
    public bool RequiresLicense { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public ICollection<CreatePositionSkillRequirementDto> SkillRequirements { get; set; } = new List<CreatePositionSkillRequirementDto>();
    public ICollection<CreateEmployeePositionBenefitDto> PositionBenefits { get; set; } = new List<CreateEmployeePositionBenefitDto>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinimumAge.HasValue && MaximumAge.HasValue && MinimumAge.Value > MaximumAge.Value)
        {
            yield return new ValidationResult(
                "MinimumAge cannot be greater than MaximumAge.",
                new[] { nameof(MinimumAge), nameof(MaximumAge) });
        }
    }
}

/// <summary>
/// Lightweight position DTO for dropdowns.
/// </summary>
public class EmployeePositionLookupDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid OrganizationUnitId { get; set; }
    public bool IsActive { get; set; }
    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }
    public Guid? ReportsToPositionId { get; set; }
}

/// <summary>
/// Employee position benefit assignment (read model).
/// </summary>
public class EmployeePositionBenefitDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? PositionAmount { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Employee position benefit assignment (create model).
/// </summary>
public class CreateEmployeePositionBenefitDto
{
    [Required]
    public Guid PolicyId { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? PositionAmount { get; set; }
}

#endregion

#region Skills DTOs

/// <summary>
/// Skill information
/// </summary>
public class SkillDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool RequiresCertification { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Minimal skill DTO for selectors.
/// </summary>
public class SkillLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating skills
/// </summary>
public class CreateSkillDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }
    public bool RequiresCertification { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Position skill requirement information
/// </summary>
public class PositionSkillRequirementDto
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public SkillLevel RequiredLevel { get; set; }
    public bool IsRequired { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// DTO for creating position skill requirements
/// </summary>
public class CreatePositionSkillRequirementDto
{
    [Required]
    public Guid SkillId { get; set; }

    public SkillLevel RequiredLevel { get; set; } = SkillLevel.Beginner;
    public bool IsRequired { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int Priority { get; set; } = 1;
}

#endregion

#region Qualification Catalogue DTOs

public class QualificationCatalogueDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortCode { get; set; }
    public string? Description { get; set; }
    public QualificationType Type { get; set; }
    public string? IssuingAuthority { get; set; }
    public bool IsActive { get; set; }
}

public class CreateQualificationCatalogueDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortCode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public QualificationType Type { get; set; }

    [MaxLength(200)]
    public string? IssuingAuthority { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

#region Support DTOs

/// <summary>
/// Country information
/// </summary>
public class CountryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Alpha2Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a country.
/// </summary>
public class CreateCountryDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty; // ISO 3166-1 alpha-3

    [MaxLength(2)]
    public string Alpha2Code { get; set; } = string.Empty; // ISO 3166-1 alpha-2

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating a country.
/// </summary>
public class UpdateCountryDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(2)]
    public string Alpha2Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

/// <summary>
/// Work shift information
/// </summary>
public class ShiftDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
    public int EmployeeCount { get; set; }
}

/// <summary>
/// Work station information
/// </summary>
public class WorkStationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
}

#endregion

#region Maintenance Integration DTOs

/// <summary>
/// DTO specifically for maintenance technician selection
/// </summary>
public class MaintenanceTechnicianDto
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAvailable { get; set; }
    public List<UserTechnicianSkillDto> Skills { get; set; } = new();
    public int CurrentWorkOrders { get; set; }
    public decimal WorkloadScore { get; set; }
    public string? BadgeNumber { get; set; }
    public string? ShiftName { get; set; }
}


/// <summary>
/// Technician availability and scheduling information
/// </summary>
public class TechnicianAvailabilityDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableUntil { get; set; }
    public string? UnavailabilityReason { get; set; }
    public int CurrentWorkOrders { get; set; }
    public decimal WorkloadPercentage { get; set; }
}

#endregion
