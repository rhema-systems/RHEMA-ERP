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
    public string? CorporateEmployeeID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string? DivisionName { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public string? UnitName { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public StaffStatus StaffStatus { get; set; }
    public ContractType ContractType { get; set; }
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
    public BloodType? BloodType { get; set; }
    public string? ShiftName { get; set; }
    public decimal? Salary { get; set; }
    public string? BadgeNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastPromotionDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? StationName { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? TerminationNotes { get; set; }

    // Related collections
    public List<EmployeeEmergencyContactDto> EmergencyContacts { get; set; } = new();
    public List<EmployeeDependentDto> Dependents { get; set; } = new();
    public List<EmployeeQualificationDto> Qualifications { get; set; } = new();
    public List<EmployeeSkillDto> Skills { get; set; } = new();
    public List<EmployeeContractDetailDto> ContractDetails { get; set; } = new();
}

/// <summary>
/// DTO for creating new employees
/// </summary>
public class CreateEmployeeDto
{
    [Required]
    public string EmployeeNumber { get; set; } = string.Empty;

    public string? CorporateEmployeeID { get; set; }

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
    public ContractType ContractType { get; set; } = ContractType.Permanent;
    public int ProbationPeriodDays { get; set; } = 90;
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? RetirementDate { get; set; }

    public Guid? DivisionId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? SectionId { get; set; }

    public Guid? UnitId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public StaffStatus StaffStatus { get; set; } = StaffStatus.Active;
    public Guid? StationId { get; set; }
    public string? TaxNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public BloodType? BloodType { get; set; }
    public Guid? ShiftId { get; set; }
    public decimal? Salary { get; set; }
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
    public string? CorporateEmployeeID { get; set; }
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
    public ContractType? ContractType { get; set; }
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? RetirementDate { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public StaffStatus? StaffStatus { get; set; }
    public Guid? StationId { get; set; }
    public Guid? UnitId { get; set; }
    public bool? IsExpatriate { get; set; }
    public string? TaxNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public BloodType? BloodType { get; set; }
    public Guid? ShiftId { get; set; }
    public decimal? Salary { get; set; }
    public string? BadgeNumber { get; set; }
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
    public Guid? DivisionId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? UnitId { get; set; }
    public StaffStatus? StaffStatus { get; set; }
    public ContractType? ContractType { get; set; }
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
/// Employee emergency contact information
/// </summary>
public class EmployeeEmergencyContactDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public EmergencyContactType ContactType { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternatePhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public string? DigitalAddress { get; set; }
    public bool IsPrimary { get; set; }
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

    public string? LastName { get; set; }

    [Required]
    public string Relationship { get; set; } = string.Empty;

    public EmergencyContactType ContactType { get; set; }

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? AlternatePhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public string? DigitalAddress { get; set; }
    public bool IsPrimary { get; set; }
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
    public string? LastName { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Occupation { get; set; }
    public bool IsStudentDependent { get; set; }
    public bool IsEligibleForBenefits { get; set; }
    public int? Age { get; set; }
    public bool IsDeceased { get; set; }
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

    public string? LastName { get; set; }

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
    public string QualificationName { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    public string? Grade { get; set; }
    public string? Description { get; set; }
    public bool IsVerified { get; set; }
}

/// <summary>
/// DTO for creating qualifications
/// </summary>
public class CreateEmployeeQualificationDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public string QualificationName { get; set; } = string.Empty;

    [Required]
    public string Institution { get; set; } = string.Empty;

    public string? FieldOfStudy { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? CompletionDate { get; set; }
    public string? Grade { get; set; }
    public string? Description { get; set; }
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
    public ContractType ContractType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Salary { get; set; }
    public string PayFrequency { get; set; } = string.Empty;
    public int WorkingHoursPerWeek { get; set; }
    public int VacationDaysPerYear { get; set; }
    public int SickDaysPerYear { get; set; }
    public string? Terms { get; set; }
    public bool IsActive { get; set; }
    public string? ContractPath { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
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
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public bool RequiresCertification { get; set; }
    public bool IsActive { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
    public int EmployeeCount { get; set; }
    public List<PositionSkillRequirementDto> SkillRequirements { get; set; } = new();
}

/// <summary>
/// DTO for creating positions
/// </summary>
public class CreateEmployeePositionDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public int Level { get; set; } = 1;
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public bool RequiresCertification { get; set; }
    public string? Responsibilities { get; set; }
    public string? Requirements { get; set; }
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
    public bool IsActive { get; set; }
    public bool RequiresCertification { get; set; }
    public int EmployeeCount { get; set; }
    public int PositionRequirementCount { get; set; }
}

/// <summary>
/// DTO for creating skills
/// </summary>
public class CreateSkillDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool RequiresCertification { get; set; }
}

/// <summary>
/// Position skill requirement information
/// </summary>
public class PositionSkillRequirementDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string? SkillCategory { get; set; }
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
    public Guid PositionId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    public SkillLevel RequiredLevel { get; set; } = SkillLevel.Beginner;
    public bool IsRequired { get; set; } = true;
    public int Priority { get; set; } = 1;
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