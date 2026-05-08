using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

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

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

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
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    public int ProbationPeriodDays { get; set; } = 90;

    public DateOnly? ConfirmationDate { get; set; }

    public DateOnly? RetirementDate { get; set; }

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    public Guid? DivisionId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    public Guid? SectionId { get; set; }

    public Guid? UnitId { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public StaffStatus StaffStatus { get; set; } = StaffStatus.Active;

    public Guid? LocationLevelId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? StationId { get; set; }

    public bool IsExpatriate { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    [MaxLength(50)]
    public string? SocialSecurityNumber { get; set; }

    [MaxLength(50)]
    public string? TINNumber { get; set; }

    public BloodType? BloodType { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? ShiftId { get; set; }

    public Guid? ManagerId { get; set; }

    // Additional HR Fields
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Salary { get; set; }

    public bool PayTax { get; set; }

    public bool SSFund { get; set; }

    public bool GrossUp { get; set; }

    public bool Tier2Only { get; set; }

    public bool Overtime { get; set; }

    [MaxLength(50)]
    public string? BadgeNumber { get; set; }

    public DateTime? LastPromotionDate { get; set; }

    public DateTime? LastReviewDate { get; set; }

    public DateTime? NextReviewDate { get; set; }

    public DateTime? TerminationDate { get; set; }

    [MaxLength(50)]
    public string? TerminationReason { get; set; }

    [MaxLength(1000)]
    public string? TerminationNotes { get; set; }

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
        !IsDeleted &&
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
    public virtual Division? Division { get; set; }
    public virtual Department Department { get; set; } = null!;
    public virtual Section? Section { get; set; }
    public virtual Unit? Unit { get; set; }
    public virtual EmployeePosition Position { get; set; } = null!;
    public virtual Country? Country { get; set; }
    public virtual Shift? Shift { get; set; }
    public virtual WorkStation? Station { get; set; }
    public virtual Employee? Manager { get; set; }
    public virtual LocationLevel? LocationLevel { get; set; }
    public virtual Location? Location { get; set; }
    public virtual OrganizationLevel? OrganizationLevel { get; set; }
    public virtual OrganizationUnit? OrganizationUnit { get; set; }
    
    [NotMapped]
    public virtual EmployeeContractDetail? CurrentTerms { get; set; }

    // Related Collections
    public virtual ICollection<EmployeeContact> Contacts { get; set; } = new List<EmployeeContact>();
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
    public virtual ICollection<ExpatriateAssignment> ExpatriateAssignments { get; set; } = new List<ExpatriateAssignment>();
    public virtual ICollection<EmployeePositionHistory> PositionHistories { get; set; } = new List<EmployeePositionHistory>();
    public virtual ICollection<EmployeeSalaryAssignment> SalaryAssignments { get; set; } = new List<EmployeeSalaryAssignment>();
    public virtual ICollection<EmployeeGuarantor> Guarantors { get; set; } = new List<EmployeeGuarantor>();
    public virtual ICollection<EmployeeReferee> Referees { get; set; } = new List<EmployeeReferee>();
    public virtual ICollection<EmployeeBankDetail> BankDetails { get; set; } = new List<EmployeeBankDetail>();
    
    // Leave Management
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public virtual ICollection<LeavePlan> LeavePlans { get; set; } = new List<LeavePlan>();

    // Performance Management
    public virtual ICollection<PerformanceAppraisal> PerformanceAppraisals { get; set; } = new List<PerformanceAppraisal>();


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

    public Guid? DivisionId { get; set; }

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
    public virtual Division? Division { get; set; }
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
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}

/// <summary>
/// Represents an organizational unit (smallest subdivision)
/// </summary>
public class Unit : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid SectionId { get; set; }

    public Guid? UnitHeadId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Section Section { get; set; } = null!;

    public virtual Employee? UnitHead { get; set; }

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

    public Guid? SectionId { get; set; }

    public Guid? UnitId { get; set; }

    /// <summary>
    /// Organization level this position belongs to
    /// </summary>
    public Guid? OrganizationLevelId { get; set; }

    /// <summary>
    /// Organization unit this position belongs to
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    public Guid? StaffLevelId { get; set; }

    // Position hierarchy
    public Guid? ReportsToPositionId { get; set; }

    public int Level { get; set; } = 1; // Organizational level (1 = entry level, higher = senior)

    public int? MinimumExperienceYears { get; set; }

    public int? MinimumAge { get; set; }

    public int? MaximumAge { get; set; }

    // Capacity planning
    public int ExpectedHeadcount { get; set; } = 1;

    public Guid? SalaryGradeId { get; set; }

    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxSalary { get; set; }

    public bool RequiresCertification { get; set; } = false;

    public bool RequiresGuarantor { get; set; }

    public int? NumberOfGuarantors { get; set; }

    public bool RequiresLicense { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Responsibilities { get; set; }

    [MaxLength(2000)]
    public string? Requirements { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(ReportsToPositionId))]
    public virtual EmployeePosition? ReportsToPosition { get; set; }

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel? StaffLevel { get; set; }

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade? SalaryGrade { get; set; }

    public virtual Department Department { get; set; } = null!;
    public virtual Section? Section { get; set; }
    public virtual Unit? Unit { get; set; } 
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public virtual ICollection<EmployeePositionHistory> PositionHistories { get; set; } = new List<EmployeePositionHistory>();
    public virtual ICollection<PositionSkillRequirement> SkillRequirements { get; set; } = new List<PositionSkillRequirement>();
    public virtual List<EmployeePositionBenefit> PositionBenefits { get; set; } = new List<EmployeePositionBenefit>();
}

public class BenefitPolicy : TenantEntity
{
    public BenefitPolicyType PolicyType { get; set; } = BenefitPolicyType.Medical;

    [Required]
    [MaxLength(150)]
    public string PolicyName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PolicyCode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public BenefitRecipient Recipient { get; set; } = BenefitRecipient.Staff;

    public int? MaxDependents { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EmployeeContribution { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EmployerContribution { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CoverageLimit { get; set; }

    public BenefitLimitPeriod LimitPeriod { get; set; } = BenefitLimitPeriod.Annual;

    public DateTime EffectiveFrom { get; set; }
    
    public DateTime? EffectiveTo { get; set; }
    
    public bool IsMandatory { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual List<EmployeePositionBenefit> PositionBenefits { get; set; } = new List<EmployeePositionBenefit>();

    public virtual List<BenefitPolicyRelation> BenefitPolicyRelations { get; set; } = new List<BenefitPolicyRelation>();
}

public class BenefitPolicyRelation : TenantEntity
{
    public Guid BenefitPolicyId { get; set; }
    
    [ForeignKey(nameof(BenefitPolicyId))]
    public BenefitPolicy BenefitPolicy { get; set; } = null!;

    public BenefitRelationType RelationType { get; set; }

    public int? MinAge { get; set; }
    
    public int? MaxAge { get; set; }

    public bool IsActive { get; set; } = true;
}

public class EmployeePositionBenefit : TenantEntity
{
    public Guid PositionId { get; set; }

    public Guid PolicyId { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PositionAmount { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [ForeignKey(nameof(PolicyId))]
    public virtual BenefitPolicy BenefitPolicy { get; set; } = null!;
}

/// <summary>
/// Represents work stations or locations
/// </summary>
public class WorkStation : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public StationType StationType { get; set; } = StationType.Branch;

    public Guid? DepartmentId { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    // Contact person - Foreign key to Employee
    public Guid? ContactPersonId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual Employee? ContactPerson { get; set; }
    public virtual Department? Department { get; set; }
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

/// <summary>
/// Represents an organizational division (highest level after company)
/// </summary>
public class Division : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? DivisionHeadId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Employee? DivisionHead { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class StaffLevel : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public int Rank { get; set; }  = 1;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual List<EmployeePosition> EmployeePositions { get; set; } = new List<EmployeePosition>();
}

public class EmployeeContractType : TenantEntity
{
    public string? Code { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Duration { get; set; }
}

#endregion

#region Employee Details and Relations

/// <summary>
/// A residential or postal address record for an employee.
/// An employee may have multiple contact records (e.g. home + mailing).
/// </summary>
public class EmployeeContact : TenantEntity
{
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
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
    public virtual Country? Country { get; set; }
 
    public bool IsPrimary { get; set; }
}

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
    public string? MiddleName { get; set; }

    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Relationship { get; set; } = string.Empty;

    public EmergencyContactType ContactType { get; set; } = EmergencyContactType.EmergencyContact;

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

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public bool IsPrimary { get; set; } = false;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }
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
    public string? MiddleName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    public DependentRelationship Relationship { get; set; }

    [MaxLength(100)]
    public string? RelationshipDescription { get; set; } // used when Relationship = Other

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

    public bool IsStudentDependent { get; set; } = false;

    public bool IsEmergencyContact { get; set; }

    public bool IsEligibleForBenefits { get; set; }

    public bool IsDeceased { get; set; }

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;

    public virtual List<EmployeeDependentBenefit> EmployeeDependentBenefits { get; set; } = new List<EmployeeDependentBenefit>();
}

public class EmployeeDependentBenefit : TenantEntity
{
    public Guid EmployeeDependentId { get; set; }

    public Guid PolicyId { get; set; }

    public DateOnly EnrolledDate { get; set; }

    public DateOnly? CoverageStartDate { get; set; }
    
    public DateOnly? CoverageEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BenefitAmountUsed { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeDependentId))]
    public virtual EmployeeDependent EmployeeDependent { get; set; } = null!;
    
    [ForeignKey(nameof(PolicyId))]
    public virtual BenefitPolicy BenefitPolicy { get; set; } = null!;
}

/// <summary>
/// Represents employee qualifications and education
/// </summary>
public class EmployeeQualification : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Reference to master Qualification if selected from dropdown
    /// </summary>
    public Guid? QualificationId { get; set; }

    /// <summary>
    /// Custom qualification name if not selected from dropdown
    /// </summary>
    [MaxLength(200)]
    public string? CustomQualificationName { get; set; }

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FieldOfStudy { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? CompletionDate { get; set; }

    [MaxLength(50)]
    public string? Grade { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsVerified { get; set; } = false;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(QualificationId))]
    public virtual Qualification? Qualification { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }
}

/// <summary>
/// Represents employee identification documents
/// </summary>
public class EmployeeIdentificationCard : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid IdentificationTypeId { get; set; }

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

    public DateTime? VerifiedDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(IdentificationTypeId))]
    public virtual IdentificationType IdentificationType { get; set; } = null!;
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

    [MaxLength(200)]
    public string? CompanyAddress { get; set; }

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

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Salary { get; set; } = 0;

    [MaxLength(10)]
    public string CurrencyCode { get; set; } = "GHS";

    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;

    /// <summary>
    /// How this contract is taxed
    /// </summary>
    public TaxTreatmentType TaxTreatmentType { get; set; } = TaxTreatmentType.PAYE;

    /// <summary>
    /// Applicable withholding tax rate (if applicable)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? WithholdingTaxRate { get; set; }

    /// <summary>
    /// Whether SSNIT / pension contributions apply
    /// </summary>
    public bool IsPensionApplicable { get; set; } = true;

    /// <summary>
    /// Whether this contract is tax-exempt
    /// </summary>
    public bool IsTaxExempt { get; set; } = false;

    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Null for permanent employment; populated for fixed-term/contract.
    /// </summary>
    public DateOnly? ContractEndDate { get; set; }

    public int WorkingHoursPerWeek { get; set; } = 40;

    public int AnnualLeaveEntitlementDays { get; set; } = 20;

    public int VacationDaysPerYear { get; set; } = 15;

    public int SickDaysPerYear { get; set; } = 10;

    public int? ProbationPeriodDays { get; set; }

    public DateOnly? ConfirmationDate { get; set; }

    public WorkSchedule WorkSchedule { get; set; } = WorkSchedule.FullTime;

    [MaxLength(1000)]
    public string? Terms { get; set; }

    [MaxLength(2000)]
    public string? SpecialConditions { get; set; }

    /// <summary>
    /// Whether this is the employee's currently active terms record.
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? ContractPath { get; set; } // Path to contract document

    public ContractStatus ContractStatus { get; set; } = ContractStatus.Active;

    public DateOnly? TerminationDate { get; set; }

    [MaxLength(1000)]
    public string? TerminationReason { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;
}

public class ExpatriateAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid HomeCountryId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RelocationAllowance { get; set; }

    public DateOnly? RelocationDate { get; set; }

    public bool FamilyAccompanying { get; set; }

    [MaxLength(1000)]
    public string? AssignmentObjective { get; set; } // e.g., Project, Training

    [MaxLength(100)]
    public string? VisaType { get; set; }

    public DateOnly? VisaExpiryDate { get; set; }

    [MaxLength(100)]
    public string? WorkPermitNumber { get; set; }

    public DateOnly? WorkPermitExpiryDate { get; set; }

    // Navigation Properties
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(HomeCountryId))]
    public virtual Country Country { get; set; } = null!;
}

/// <summary>
/// Tracks employee position/career progression history
/// </summary>
public class EmployeePositionHistory : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid LocationLevelId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid PositionId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public PositionChangeReason ChangeReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsCurrent => EndDate == null || EndDate > DateTime.Today;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(LocationLevelId))]
    public virtual LocationLevel LocationLevel { get; set; } = null!;

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel OrganizationLevel { get; set; } = null!;

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;
}

public class EmployeeSalaryAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    
    public Guid GradeId { get; set; }
    
    public Guid? LevelId { get; set; }
    
    public Guid? NotchId { get; set; }
    
    public DateTime EffectiveDate { get; set; }
    
    public DateTime? EffectiveTo { get; set; }
    
    public string AssignmentReason { get; set; } = string.Empty; // Promotion, Annual Review, etc.
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
    
    [ForeignKey(nameof(GradeId))]
    public virtual SalaryGrade Grade { get; set; } = null!;
    
    [ForeignKey(nameof(LevelId))]
    public virtual SalaryLevel? Level { get; set; }
    
    [ForeignKey(nameof(NotchId))]
    public virtual SalaryNotch? Notch { get; set; }
}

/// <summary>
/// Represents referees provided by an employee/applicant
/// </summary>
public class EmployeeReferee : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Type of referee (Professional, Academic, Personal)
    /// </summary>
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

    public bool IsContacted { get; set; }

    public DateTime? ContactedDate { get; set; }

    [MaxLength(1000)]
    public string? ReferenceNotes { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents guarantors for employees (if required)
/// </summary>
public class EmployeeGuarantor : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Whether this is the primary guarantor (if multiple are required)
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Relationship of guarantor to employee
    /// </summary>
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

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonthlyIncome { get; set; }

    [MaxLength(50)]
    public string? NationalIdType { get; set; } // e.g. Ghana Card, Passport

    [MaxLength(100)]
    public string? NationalIdNumber { get; set; }

    public DateOnly? NationalIdExpiryDate { get; set; }

    public bool HasSignedGuarantorForm { get; set; } = false;

    public DateOnly? DateFormSigned { get; set; }

    [MaxLength(500)]
    public string? GuarantorFormPath { get; set; }

    public bool IsVerified { get; set; } = false;

    public DateTime? VerificationDate { get; set; }

    public Guid? VerifiedByEmployeeId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? LastContactDate { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(VerifiedByEmployeeId))]
    public virtual Employee? VerifiedByEmployee { get; set; }
    
    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [NotMapped]
    public string FullName =>
        string.IsNullOrWhiteSpace(MiddleName)
            ? $"{FirstName} {LastName}"
            : $"{FirstName} {MiddleName} {LastName}";
}

/// <summary>
/// Represents a bank (financial institution) used as a reference for employee bank details.
/// </summary>
public class EmployeeBank : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short bank identifier, e.g. "GCB", "ABSA". Unique per tenant.</summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>International SWIFT / BIC code, if applicable.</summary>
    [MaxLength(20)]
    public string? SwiftCode { get; set; }

    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<EmployeeBankBranch> Branches { get; set; } = new List<EmployeeBankBranch>();
}

/// <summary>
/// Represents a branch of a <see cref="Bank"/>.
/// </summary>
public class EmployeeBankBranch : TenantEntity
{
    [Required]
    public Guid BankId { get; set; }

    [ForeignKey(nameof(BankId))]
    public virtual EmployeeBank Bank { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Branch sort code or internal identifier. Unique per bank per tenant.</summary>
    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Bank account details for payroll disbursement.
/// An employee may have multiple accounts (e.g. split salary).
/// </summary>
public class EmployeeBankDetail : TenantEntity
{
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    // ── Structured reference (optional, links to Bank / BankBranch catalogue) ──

    public Guid? BankId { get; set; }

    [ForeignKey(nameof(BankId))]
    public virtual EmployeeBank? Bank { get; set; }

    public Guid? BranchId { get; set; }

    [ForeignKey(nameof(BranchId))]
    public virtual EmployeeBankBranch? Branch { get; set; }

    // ── Free-text fallback (used when Bank/Branch entities are not selected) ──
 
    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;
 
    [MaxLength(100)]
    public string BranchName { get; set; } = string.Empty;
 
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;
 
    [MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;
 
    [MaxLength(50)]
    public string? MobileMoneyNumber { get; set; }
 
    public EmployeeBankAccountType AccountType { get; set; }
 
    /// <summary>
    /// If split payroll, the percentage of net pay directed to this account.
    /// All active accounts for an employee must sum to 100.
    /// </summary>
    public decimal AllocationPercentage { get; set; } = 100;
 
    public bool IsPrimary { get; set; }
 
    public bool IsActive { get; set; } = true;
 
    public bool IsVerified { get; set; }
 
    public DateTime? VerifiedDate { get; set; }
 
    public Guid? VerifiedById { get; set; }
 
    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }
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

    public bool IsCertified { get; set; }

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

/// <summary>
/// Master list of standard qualifications
/// </summary>
public class Qualification : TenantEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortCode { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public QualificationType Type { get; set; }

    [MaxLength(200)]
    public string? IssuingAuthority { get; set; }

    public bool IsActive { get; set; } = true;
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

/// <summary>
/// Represents types of identification documents
/// </summary>
public class IdentificationType : TenantEntity
{
    /// <summary>
    /// Display name (e.g. Ghana Card, Passport, Driver’s License)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short code (e.g. GH_CARD, PASSPORT)
    /// </summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>
    /// Description of the identification type
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Issuing authority name (e.g. National Identification Authority)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string IssuingAuthorityName { get; set; } = string.Empty;

    /// <summary>
    /// Country that issues this ID
    /// </summary>
    public Guid? IssuingCountryId { get; set; }

    /// <summary>
    /// Whether this ID has an expiry date
    /// </summary>
    public bool HasExpiryDate { get; set; } = true;

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(IssuingCountryId))]
    public virtual Country? IssuingCountry { get; set; }

    public virtual ICollection<EmployeeIdentificationCard> EmployeeIdentificationCards { get; set; } = new List<EmployeeIdentificationCard>();
}

#endregion

#region External Associates

public class ExternalAssociate : TenantEntity
{
    [MaxLength(50)]
    public string AssociateNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string MiddleName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Title { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    public bool HasFixedModule { get; set; }

    public int? ModuleId { get; set; }

    public string? Role { get; set; }

    [MaxLength(500)]
    public string PicturePath { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime DateAdded { get; set; }
}

#endregion
