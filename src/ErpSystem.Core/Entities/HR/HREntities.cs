using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Entities.HR;

#region Core Employee Management

/// <summary>
/// Represents an employee in the organization
/// </summary>
public class Employee : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [MaxLength(50)]
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

    [MaxLength(50)]
    public string? Religion { get; set; }

    public bool IsFullTime { get; set; } = true;

    public DateOnly? DateEmployed { get; set; }

    // Contact Information
    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TelephoneNumber { get; set; }

    [MaxLength(50)]
    public string? BusinessNumber { get; set; }

    [MaxLength(50)]
    public string? MobileNumber { get; set; }

    [MaxLength(20)]
    public string? Extension { get; set; }

    // Employment Details
    public ContractType ContractType { get; set; } = ContractType.Permanent;

    public int ProbationPeriodDays { get; set; } = 90;

    public DateOnly? ConfirmationDate { get; set; }

    public DateOnly? RetirementDate { get; set; }

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? SectionId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public StaffStatus StaffStatus { get; set; } = StaffStatus.Active;

    public Guid? StationId { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    public BloodType? BloodType { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? ShiftId { get; set; }

    public Guid? ManagerId { get; set; }

    // Additional HR Fields
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Salary { get; set; }

    [MaxLength(50)]
    public string? BadgeNumber { get; set; }

    public DateTime? LastPromotionDate { get; set; }

    public DateTime? LastReviewDate { get; set; }

    public DateTime? NextReviewDate { get; set; }

    // Maintenance-specific properties (for employees who are technicians)
    /// <summary>
    /// Primary technical specialization (for maintenance technicians)
    /// </summary>
    [MaxLength(100)]
    public string? Specialization { get; set; }

    /// <summary>
    /// Certification level for technical work (Level 1, Level 2, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? CertificationLevel { get; set; }

    /// <summary>
    /// Experience level (Junior, Intermediate, Senior, Expert)
    /// </summary>
    [MaxLength(50)]
    public string? ExperienceLevel { get; set; }

    /// <summary>
    /// Current workload percentage for maintenance technicians (0-100)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal CurrentWorkload { get; set; } = 0;

    /// <summary>
    /// Maximum workload capacity percentage for maintenance technicians
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal MaxWorkload { get; set; } = 100;

    /// <summary>
    /// Last synchronization date with maintenance systems
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    /// <summary>
    /// Additional notes about the employee (maintenance-specific or general)
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Computed Properties
    [NotMapped]
    public string FullName => string.IsNullOrEmpty(MiddleName) 
        ? $"{FirstName} {LastName}" 
        : $"{FirstName} {MiddleName} {LastName}";

    [NotMapped]
    public string DisplayName => $"{FullName} ({EmployeeNumber})";

    [NotMapped]
    public int? YearsOfService => DateEmployed.HasValue 
        ? DateTime.Today.Year - DateEmployed.Value.Year 
        : null;

    [NotMapped]
    public bool IsOnProbation => StaffStatus == StaffStatus.Probation;

    [NotMapped]
    public bool CanBeAssignedToMaintenance => IsActive && 
        (StaffStatus == StaffStatus.Active || StaffStatus == StaffStatus.Probation) &&
        Department?.DepartmentType == DepartmentType.Maintenance;

    // Alias properties for service compatibility
    /// <summary>
    /// Alias for EmployeeNumber (for TechnicianService compatibility)
    /// </summary>
    [NotMapped]
    public string EmployeeId
    {
        get => EmployeeNumber;
        set => EmployeeNumber = value;
    }

    /// <summary>
    /// Alias for EmailAddress (for TechnicianService compatibility)
    /// </summary>
    [NotMapped]
    public string? Email
    {
        get => EmailAddress;
        set => EmailAddress = value ?? string.Empty;
    }

    /// <summary>
    /// Alias for TelephoneNumber/MobileNumber (for TechnicianService compatibility)
    /// </summary>
    [NotMapped]
    public string? Phone
    {
        get => MobileNumber ?? TelephoneNumber;
        set => MobileNumber = value;
    }

    // Navigation Properties
    public virtual Department Department { get; set; } = null!;
    public virtual Section? Section { get; set; }
    public virtual EmployeePosition Position { get; set; } = null!;
    public virtual Country? Country { get; set; }
    public virtual Shift? Shift { get; set; }
    public virtual WorkStation? Station { get; set; }
    public virtual Employee? Manager { get; set; }

    // Related Collections
    public virtual ICollection<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new List<EmployeeEmergencyContact>();
    public virtual ICollection<EmployeeDependent> Dependents { get; set; } = new List<EmployeeDependent>();
    public virtual ICollection<EmployeeQualification> Qualifications { get; set; } = new List<EmployeeQualification>();
    public virtual ICollection<EmployeeIdentificationCard> IdentificationCards { get; set; } = new List<EmployeeIdentificationCard>();
    public virtual ICollection<EmployeeWorkHistory> WorkHistories { get; set; } = new List<EmployeeWorkHistory>();
    public virtual ICollection<EmployeeContractDetail> ContractDetails { get; set; } = new List<EmployeeContractDetail>();
    public virtual ICollection<EmployeeSkill> Skills { get; set; } = new List<EmployeeSkill>();
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public virtual ICollection<EmployeeBiometric> Biometrics { get; set; } = new List<EmployeeBiometric>();
    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public virtual ICollection<EmployeeShiftPreference> ShiftPreferences { get; set; } = new List<EmployeeShiftPreference>();

    // Manager/Employee relationship
    public virtual ICollection<Employee> DirectReports { get; set; } = new List<Employee>(); // Employees who report to this manager

    // Maintenance-specific navigation properties
    public virtual ICollection<MaintenanceAsset> AssignedAssets { get; set; } = new List<MaintenanceAsset>(); // Assets primarily assigned to this technician
    public virtual ICollection<WorkOrder> AssignedWorkOrders { get; set; } = new List<WorkOrder>(); // Work orders assigned to this technician
    public virtual ICollection<TechnicianSchedule> TechnicianSchedules { get; set; } = new List<TechnicianSchedule>();
    public virtual ICollection<TechnicianAvailability> TechnicianAvailabilities { get; set; } = new List<TechnicianAvailability>();
}

#endregion

#region Organizational Structure

/// <summary>
/// Represents a department in the organization
/// </summary>
public class Department : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DepartmentType DepartmentType { get; set; } = DepartmentType.Operations;

    public Guid? ParentDepartmentId { get; set; }

    public Guid? DepartmentHeadId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Budget { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(7)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    // Navigation Properties
    public virtual Department? ParentDepartment { get; set; }
    public virtual Employee? DepartmentHead { get; set; }
    public virtual ICollection<Department> SubDepartments { get; set; } = new List<Department>();
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public virtual ICollection<EmployeePosition> Positions { get; set; } = new List<EmployeePosition>();
}

/// <summary>
/// Represents a section within a department
/// </summary>
public class Section : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? SectionHeadId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual Department Department { get; set; } = null!;
    public virtual Employee? SectionHead { get; set; }
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

/// <summary>
/// Represents job positions/roles in the organization
/// </summary>
public class EmployeePosition : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public int Level { get; set; } = 1; // Organizational level (1 = entry level, higher = senior)

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxSalary { get; set; }

    public bool RequiresCertification { get; set; } = false;

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Responsibilities { get; set; }

    [MaxLength(2000)]
    public string? Requirements { get; set; }

    // Navigation Properties
    public virtual Department Department { get; set; } = null!;
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public virtual ICollection<PositionSkillRequirement> SkillRequirements { get; set; } = new List<PositionSkillRequirement>();
}

/// <summary>
/// Represents work stations or locations
/// </summary>
public class WorkStation : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public Guid? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual Department? Department { get; set; }
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

#endregion

#region Employee Details and Relations

/// <summary>
/// Represents emergency contacts for employees
/// </summary>
public class EmployeeEmergencyContact : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastName { get; set; }

    [Required]
    [MaxLength(50)]
    public string Relationship { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? AlternatePhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? EmailAddress { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public bool IsPrimary { get; set; } = false;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents employee dependents
/// </summary>
public class EmployeeDependent : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastName { get; set; }

    [Required]
    [MaxLength(50)]
    public string Relationship { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public Gender? Gender { get; set; }

    [MaxLength(100)]
    public string? Occupation { get; set; }

    public bool IsStudentDependent { get; set; } = false;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents employee qualifications and education
/// </summary>
public class EmployeeQualification : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string QualificationName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FieldOfStudy { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? CompletionDate { get; set; }

    [MaxLength(50)]
    public string? Grade { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsVerified { get; set; } = false;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents employee identification documents
/// </summary>
public class EmployeeIdentificationCard : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty; // National ID, Passport, Driver's License, etc.

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(200)]
    public string? IssuingAuthority { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; } // Path to scanned document

    public bool IsVerified { get; set; } = false;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents employee work history
/// </summary>
public class EmployeeWorkHistory : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string JobTitle { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? JobDescription { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Salary { get; set; }

    [MaxLength(1000)]
    public string? ReasonForLeaving { get; set; }

    [MaxLength(200)]
    public string? SupervisorName { get; set; }

    [MaxLength(50)]
    public string? SupervisorPhone { get; set; }

    public bool CanContact { get; set; } = true;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents employee contract details
/// </summary>
public class EmployeeContractDetail : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ContractNumber { get; set; } = string.Empty;

    public ContractType ContractType { get; set; } = ContractType.Permanent;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Salary { get; set; } = 0;

    [MaxLength(50)]
    public string PayFrequency { get; set; } = "Monthly"; // Weekly, Bi-weekly, Monthly, etc.

    public int WorkingHoursPerWeek { get; set; } = 40;

    public int VacationDaysPerYear { get; set; } = 15;

    public int SickDaysPerYear { get; set; } = 10;

    [MaxLength(1000)]
    public string? Terms { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? ContractPath { get; set; } // Path to contract document

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

#endregion

#region Skills and Competencies

/// <summary>
/// Represents skills that can be assigned to employees
/// </summary>
public class Skill : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; } // Technical, Soft Skills, Certification, etc.

    public bool IsActive { get; set; } = true;

    public bool RequiresCertification { get; set; } = false;

    // Navigation Properties
    public virtual ICollection<EmployeeSkill> EmployeeSkills { get; set; } = new List<EmployeeSkill>();
    public virtual ICollection<PositionSkillRequirement> PositionRequirements { get; set; } = new List<PositionSkillRequirement>();
}

/// <summary>
/// Junction table for employee skills with proficiency levels
/// </summary>
public class EmployeeSkill : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    public SkillLevel SkillLevel { get; set; } = SkillLevel.Beginner;

    public DateOnly? AcquiredDate { get; set; }

    public DateOnly? CertificationDate { get; set; }

    public DateOnly? CertificationExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificationNumber { get; set; }

    [MaxLength(200)]
    public string? CertifyingBody { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsVerified { get; set; } = false;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}

/// <summary>
/// Represents skill requirements for specific positions
/// </summary>
public class PositionSkillRequirement : TenantEntity
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    public SkillLevel RequiredLevel { get; set; } = SkillLevel.Beginner;

    public bool IsRequired { get; set; } = true; // true = required, false = preferred

    public int Priority { get; set; } = 1; // Higher number = higher priority

    // Navigation Properties
    public virtual EmployeePosition Position { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}

#endregion

#region Support Entities

/// <summary>
/// Represents countries for employee records
/// </summary>
public class Country : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(3)]
    public string Code { get; set; } = string.Empty; // ISO 3166-1 alpha-3

    [MaxLength(2)]
    public string Alpha2Code { get; set; } = string.Empty; // ISO 3166-1 alpha-2

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

/// <summary>
/// Represents work shifts
/// </summary>
public class Shift : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(7)]
    public string? Color { get; set; }

    // Navigation Properties
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
}

/// <summary>
/// Represents attendance records
/// </summary>
public class AttendanceRecord : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public double? WorkedHours { get; set; }

    public double? OvertimeHours { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Present"; // Present, Absent, Late, Partial Day

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents biometric data for employees
/// </summary>
public class EmployeeBiometric : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [MaxLength(50)]
    public string BiometricType { get; set; } = string.Empty; // Fingerprint, Face, Iris, etc.

    [Required]
    [MaxLength(500)]
    public string BiometricData { get; set; } = string.Empty; // Encoded biometric data

    [MaxLength(100)]
    public string? DeviceId { get; set; }

    public DateTime EnrolledDate { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents shift assignments for employees
/// </summary>
public class ShiftAssignment : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ShiftId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
    public virtual Shift Shift { get; set; } = null!;
}

/// <summary>
/// Represents employee shift preferences
/// </summary>
public class EmployeeShiftPreference : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ShiftId { get; set; }

    public int PreferenceLevel { get; set; } = 1; // 1 = most preferred, higher = less preferred

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
    public virtual Shift Shift { get; set; } = null!;
}

#endregion
