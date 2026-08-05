using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.StaffAttendance;


namespace ErpSystem.Core.Entities.HR;

#region Core Employee Management

/// <summary>
/// Represents an employee in the organization
/// The authoritative HR record for a person employed by the organisation.
/// Created (or linked) when a JobHireRecord is confirmed and onboarding completes.
/// </summary>
public class Employee : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string EmployeeNumber { get; set; } = string.Empty;

    // RHEMA hrdev extra: secondary corporate identifier (e.g. group-wide staff ID).
    [MaxLength(50)]
    public string? CorporateEmployeeID { get; set; }

    /// <summary>
    /// Back-reference to the recruitment hire that created this employee.
    /// Null for employees entered directly (e.g. legacy data migrations).
    /// </summary>
    public Guid? HireRecordId { get; set; }
 
    [ForeignKey(nameof(HireRecordId))]
    public virtual JobHireRecord? JobHireRecord { get; set; }

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

    public DateOnly? ConfirmationDate { get; set; } // Date probation was passed

    public DateOnly? RetirementDate { get; set; }

    public DateOnly? EndDate { get; set; } // Populated on exit

    [MaxLength(500)]
    public string? PicturePath { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? SectionId { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    // RHEMA hrdev extras: additional org placement (Division / Unit / WorkStation).
    public Guid? DivisionId { get; set; }

    public Guid? UnitId { get; set; }

    public Guid? StationId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public StaffStatus StaffStatus { get; set; } = StaffStatus.Active;

    public Guid? LocationLevelId { get; set; }
    
    public Guid? LocationId { get; set; }

    public bool IsExpatriate { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    [MaxLength(50)]
    public string? SocialSecurityNumber { get; set; }

    [MaxLength(50)]
    public string? TINNumber { get; set; }

    public BloodType? BloodType { get; set; }

    public bool IsActive { get; set; } = true;

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

    public TerminationReason? TerminationReason { get; set; }

    [MaxLength(2000)]
    public string? TerminationNotes { get; set; }

    // Maintenance-specific properties (for employees who are technicians)

    public bool CanBeAssignedToMaintenance { get; set; }

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
    public virtual Department? Department { get; set; }
    public virtual Section? Section { get; set; }
    // RHEMA hrdev extras
    public virtual Division? Division { get; set; }
    public virtual Unit? Unit { get; set; }
    public virtual WorkStation? Station { get; set; }
    public virtual EmployeePosition Position { get; set; } = null!;
    public virtual Country? Country { get; set; }
    public virtual Employee? Manager { get; set; }
    public virtual LocationLevel? LocationLevel { get; set; }
    public virtual Location? Location { get; set; }
    public virtual OrganizationLevel? OrganizationLevel { get; set; }
    public virtual OrganizationUnit? OrganizationUnit { get; set; }
    // Related Collections
    public virtual ICollection<EmployeeContact> Contacts { get; set; } = new List<EmployeeContact>();
    public virtual ICollection<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new List<EmployeeEmergencyContact>();
    public virtual ICollection<EmployeeDependent> Dependents { get; set; } = new List<EmployeeDependent>();
    public virtual ICollection<EmployeeQualification> Qualifications { get; set; } = new List<EmployeeQualification>();
    public virtual ICollection<EmployeeIdentificationCard> IdentificationCards { get; set; } = new List<EmployeeIdentificationCard>();
    public virtual ICollection<EmployeeWorkHistory> WorkHistories { get; set; } = new List<EmployeeWorkHistory>();
    public virtual ICollection<EmployeeContractDetail> ContractDetails { get; set; } = new List<EmployeeContractDetail>();
    public virtual ICollection<ProbationPeriod> ProbationPeriods { get; set; } = new List<ProbationPeriod>();
    public virtual ICollection<OnboardingPlan> OnboardingPlans { get; set; } = new List<OnboardingPlan>();

    // CurrentTerms / Probation / OnboardingPlan are [NotMapped] accessors over the collections
    // above. As mapped reference navigations they had no inverse to pair with — the one-to-many
    // was already owned by ContractDetails / ProbationPeriod.Employee / OnboardingPlan.Employee —
    // so EF built a second relationship for each and put a shadow FK (EmployeeId1) on the
    // dependent, duplicating the column EmployeeId already carries.

    /// <summary>
    /// The currently active contract terms, i.e. the one <see cref="EmployeeContractDetail.IsCurrent"/>
    /// marks. In-memory only — read <see cref="ContractDetails"/> (Include it first) rather than
    /// projecting this in a LINQ-to-Entities query.
    /// </summary>
    [NotMapped]
    public EmployeeContractDetail? CurrentTerms =>
        ContractDetails.FirstOrDefault(c => c.IsCurrent);

    /// <summary>
    /// The probation period still running, if any. An employee may accumulate several over a
    /// career (re-hire, role change), so this deliberately picks the active one rather than
    /// assuming there is only ever one. In-memory only, as with <see cref="CurrentTerms"/>.
    /// </summary>
    [NotMapped]
    public ProbationPeriod? Probation =>
        ProbationPeriods.FirstOrDefault(p => p.Status == ProbationStatus.Active);

    /// <summary>
    /// The onboarding plan still in flight, if any; falls back to the most recently started plan
    /// once they have all closed out. In-memory only, as with <see cref="CurrentTerms"/>.
    /// </summary>
    [NotMapped]
    public OnboardingPlan? OnboardingPlan =>
        OnboardingPlans.FirstOrDefault(p => p.Status == OnboardingStatus.NotStarted
                                         || p.Status == OnboardingStatus.InProgress
                                         || p.Status == OnboardingStatus.Overdue)
        ?? OnboardingPlans.OrderByDescending(p => p.StartDate).FirstOrDefault();
    public virtual ICollection<EmployeeSkill> Skills { get; set; } = new List<EmployeeSkill>();
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

    // Team membership
    public virtual ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();
    public virtual ICollection<Team> LedTeams { get; set; } = new List<Team>();

    // Maintenance-specific navigation properties
    public virtual ICollection<MaintenanceAsset> AssignedAssets { get; set; } = new List<MaintenanceAsset>(); // Assets primarily assigned to this technician
    public virtual ICollection<WorkOrder> AssignedWorkOrders { get; set; } = new List<WorkOrder>(); // Work orders assigned to this technician
    public virtual ICollection<TechnicianSchedule> TechnicianSchedules { get; set; } = new List<TechnicianSchedule>();
    public virtual ICollection<TechnicianAvailability> TechnicianAvailabilities { get; set; } = new List<TechnicianAvailability>();

    // Attendance Management
    public virtual ICollection<StaffAttendanceRecord> AttendanceRecords { get; set; } = new List<StaffAttendanceRecord>();
    public virtual ICollection<StaffDailyAttendance> DailyAttendances { get; set; } = new List<StaffDailyAttendance>();
    public virtual ICollection<EmployeeBiometric> Biometrics { get; set; } = new List<EmployeeBiometric>();
    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public virtual ICollection<EmployeeWorkSchedule> WorkSchedules { get; set; } = new List<EmployeeWorkSchedule>();
    public virtual ICollection<StaffOvertimeRequest> OvertimeRequests { get; set; } = new List<StaffOvertimeRequest>();
    public virtual ICollection<RemoteWorkRequest> RemoteWorkRequests { get; set; } = new List<RemoteWorkRequest>();
    public virtual ICollection<EmployeeOvertimeOverride> OvertimeOverrides { get; set; } = new List<EmployeeOvertimeOverride>();
    public virtual ICollection<StaffMonthlyAttendanceSummary> MonthlySummaries { get; set; } = new List<StaffMonthlyAttendanceSummary>();
    public virtual ICollection<ConsultantTimesheet> ConsultantTimesheets { get; set; } = new List<ConsultantTimesheet>();
    public virtual ICollection<ClientEngagement> ConsultantEngagements { get; set; } = new List<ClientEngagement>();
}

#endregion

#region Organizational Structure

/// <summary>
/// Represents work stations or locations
/// </summary>
public class WorkStation : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public Guid CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    // Contact person - Foreign key to Employee
    public Guid? ContactPersonId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual Employee? ContactPerson { get; set; }
}

/// <summary>
/// Represents an organizational division (highest level after company)
/// </summary>
public class Division : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? DivisionHeadId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Employee? DivisionHead { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
}

/// <summary>
/// Represents a department in the organization
/// </summary>
public class Department : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    // RHEMA hrdev extras
    public DepartmentType DepartmentType { get; set; } = DepartmentType.Operations;

    public Guid? DivisionId { get; set; }

    public Guid? ParentDepartmentId { get; set; }

    public Guid? DepartmentHeadId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Budget { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;

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

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AccountCode { get; set; } = string.Empty;

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

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid SectionId { get; set; }

    public Guid? UnitHeadId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Section Section { get; set; } = null!;

    public virtual Employee? UnitHead { get; set; }
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

    // ===== Enterprise enhancement: classification, valuation, tax/statutory & payroll hand-off =====

    /// <summary>How the benefit is delivered (cash, in-kind, reimbursement, service, voucher).</summary>
    public BenefitDeliveryType DeliveryType { get; set; } = BenefitDeliveryType.Cash;

    /// <summary>ISO currency code the benefit's monetary values are expressed in.</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>How often the benefit's value is paid/assessed.</summary>
    public PayFrequency Frequency { get; set; } = PayFrequency.Monthly;

    /// <summary>How contribution amounts are calculated (fixed, % of basic/gross, grade table).</summary>
    public BenefitCalculationBasis CalculationBasis { get; set; } = BenefitCalculationBasis.FixedAmount;

    // --- Tax / Benefit-in-Kind ---

    /// <summary>Whether the benefit's assessed value is subject to income tax.</summary>
    public bool IsTaxable { get; set; } = true;

    /// <summary>Income-tax treatment of the assessed value.</summary>
    public BenefitTaxTreatment TaxTreatment { get; set; } = BenefitTaxTreatment.FullyTaxable;

    /// <summary>Taxable portion (0-100) when <see cref="TaxTreatment"/> is PartiallyTaxable.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? TaxablePercentage { get; set; }

    /// <summary>How the benefit's monetary / Benefit-in-Kind value is determined.</summary>
    public BenefitValuationMethod ValuationMethod { get; set; } = BenefitValuationMethod.FlatRate;

    /// <summary>Configured monetary value used by flat/actual-cost/market-value valuation methods.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FlatValue { get; set; }

    /// <summary>Rate used by percentage/statutory valuation methods (e.g. GRA 12.5%).</summary>
    [Column(TypeName = "decimal(9,4)")]
    public decimal? ValuationRate { get; set; }

    /// <summary>Optional periodic cap on the valued amount (e.g. GHS 600/month vehicle BIK cap).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ValuationCap { get; set; }

    /// <summary>Amount of the assessed value that is exempt from tax (applied before TaxablePercentage).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxExemptThreshold { get; set; }

    // --- Statutory (Ghana: SSNIT / social security) ---

    /// <summary>Whether the benefit's value counts toward pension/SSNIT contributions.</summary>
    public bool IsPensionable { get; set; }

    /// <summary>Whether the benefit increases gross pay for payroll roll-up.</summary>
    public bool AffectsGrossPay { get; set; } = true;

    /// <summary>Whether the benefit affects net pay (e.g. an employee-funded deduction).</summary>
    public bool AffectsNetPay { get; set; } = true;

    // --- Contribution model ---

    /// <summary>Who bears the cost of the benefit.</summary>
    public BenefitContributionResponsibility ContributionResponsibility { get; set; } = BenefitContributionResponsibility.EmployerPaysAll;

    /// <summary>Employer contribution as a percentage (used when CalculationBasis is percentage-based).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? EmployerContributionRate { get; set; }

    /// <summary>Employee contribution as a percentage (used when CalculationBasis is percentage-based).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? EmployeeContributionRate { get; set; }

    // --- Eligibility ---

    /// <summary>Minimum completed months of service before an employee is eligible.</summary>
    public int? MinServiceMonths { get; set; }

    /// <summary>Whether the benefit is available to employees still on probation.</summary>
    public bool AvailableDuringProbation { get; set; } = true;

    // --- Payroll hand-off ---

    /// <summary>
    /// Optional link to the <see cref="PayComponent"/> that payroll resolves for this benefit's
    /// monetary/notional portion, keeping payroll element logic in a single place.
    /// </summary>
    public Guid? PayComponentId { get; set; }

    [ForeignKey(nameof(PayComponentId))]
    public virtual PayComponent? PayComponent { get; set; }

    public virtual List<BenefitPolicyRelation> BenefitPolicyRelations { get; set; } = new List<BenefitPolicyRelation>();

    public virtual List<EmployeePositionBenefit> PositionBenefits { get; set; } = new List<EmployeePositionBenefit>();

    /// <summary>Per-grade / staff-level value &amp; eligibility table for this policy.</summary>
    public virtual List<BenefitGradeValue> GradeValues { get; set; } = new List<BenefitGradeValue>();

    /// <summary>Direct, in-force employee enrollments in this policy.</summary>
    public virtual List<EmployeeBenefitEnrollment> Enrollments { get; set; } = new List<EmployeeBenefitEnrollment>();
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

    /// <summary>
    /// Organization level this position belongs to
    /// </summary>
    [Required]
    public Guid OrganizationLevelId { get; set; }

    /// <summary>
    /// Organization unit this position belongs to
    /// </summary>
    [Required]
    public Guid OrganizationUnitId { get; set; }

    public Guid? StaffLevelId { get; set; }

    /// <summary>
    /// Position this role reports to
    /// </summary>
    public Guid? ReportsToPositionId { get; set; }

    /// <summary>
    /// Organizational level of the position (1 = entry, higher = senior)
    /// </summary>
    public int Level { get; set; } = 1;

    public int? MinimumExperienceYears { get; set; }

    public int? MinimumAge { get; set; }

    public int? MaximumAge { get; set; }

    // Capacity planning
    public int ExpectedHeadcount { get; set; } = 1;

    public Guid? SalaryGradeId { get; set; }

    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;

    /// <summary>
    /// Standard probation period for this position in months.
    /// Defaulted onto the job offer when an offer is issued for this position.
    /// </summary>
    public int? ProbationPeriodMonths { get; set; }

    /// <summary>
    /// Notice period in months that an employee in this position must give the company if they resign.
    /// Carried forward to the job offer as a printed contract term.
    /// </summary>
    public int? NoticePeriodMonths { get; set; }

    public bool RequiresCertification { get; set; } = false;

    public bool RequiresGuarantor { get; set; }

    public int? NumberOfGuarantors { get; set; }

    public bool RequiresLicense { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel OrganizationLevel { get; set; } = null!;

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit OrganizationUnit { get; set; } = null!;

    [ForeignKey(nameof(ReportsToPositionId))]
    public virtual EmployeePosition? ReportsToPosition { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel? StaffLevel { get; set; }

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade? SalaryGrade { get; set; }
    
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public virtual ICollection<EmployeePositionHistory> PositionHistories { get; set; } = new List<EmployeePositionHistory>();
    public virtual ICollection<PositionSkillRequirement> SkillRequirements { get; set; } = new List<PositionSkillRequirement>();
    public virtual List<EmployeePositionBenefit> PositionBenefits { get; set; } = new List<EmployeePositionBenefit>();
    public virtual ICollection<PositionOvertimePolicy> OvertimePolicies { get; set; } = new List<PositionOvertimePolicy>();
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
    [ForeignKey(nameof(EmployeeId))]
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
    public string LastName { get; set; } = string.Empty;

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

/// <summary>
/// Represents benefits assigned to employee dependents
/// </summary>
public class EmployeeDependentBenefit : TenantEntity
{
    public Guid EmployeeDependentId { get; set; }

    public Guid PolicyId { get; set; }

    /// <summary>
    /// Optional link to the covering employee's enrollment. When set, this dependent's coverage
    /// hangs off the parent <see cref="EmployeeBenefitEnrollment"/> (employee → dependents hierarchy);
    /// when null, the dependent benefit is a standalone record (legacy behaviour).
    /// </summary>
    public Guid? EnrollmentId { get; set; }

    public DateOnly EnrolledDate { get; set; }

    public DateOnly? CoverageStartDate { get; set; }

    public DateOnly? CoverageEndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BenefitAmountUsed { get; set; }

    [ForeignKey(nameof(EmployeeDependentId))]
    public virtual EmployeeDependent EmployeeDependent { get; set; } = null!;

    [ForeignKey(nameof(PolicyId))]
    public virtual BenefitPolicy BenefitPolicy { get; set; } = null!;

    [ForeignKey(nameof(EnrollmentId))]
    public virtual EmployeeBenefitEnrollment? Enrollment { get; set; }
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
    public string DocumentNumber { get; set; } = string.Empty;

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

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
/// Records terms of employment for an employee at a point in time.
/// The most recent active record is the current terms.
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

    public WorkArrangementType WorkSchedule { get; set; } = WorkArrangementType.FullTime;

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

/// <summary>
/// Represents expatriate assignments for employees
/// </summary>
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
    [ForeignKey(nameof(EmployeeId))]
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

    public Guid? LocationLevelId { get; set; }

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
    public virtual LocationLevel? LocationLevel { get; set; }

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

// ─── EmployeeBank Reference Entities ─────────────────────────────────────────────────

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
/// Represents a branch of a <see cref="EmployeeBank"/>.
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
/// EmployeeBank account details for payroll disbursement.
/// An employee may have multiple accounts (e.g. split salary).
/// </summary>
public class EmployeeBankDetail : TenantEntity
{
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    // ── Structured reference (optional, links to EmployeeBank / EmployeeBankBranch catalogue) ──

    public Guid? BankId { get; set; }

    [ForeignKey(nameof(BankId))]
    public virtual EmployeeBank? Bank { get; set; }

    public Guid? BranchId { get; set; }

    [ForeignKey(nameof(BranchId))]
    public virtual EmployeeBankBranch? Branch { get; set; }

    // ── Free-text fallback (used when EmployeeBank/Branch entities are not selected) ──
 
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
    public virtual ICollection<CompetencySkillIndicator> CompetencyIndicators { get; set; } = new List<CompetencySkillIndicator>();
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
/// Reusable competency definition. Single source of truth for all competency
/// references across the HR system.
/// </summary>
public class Competency : TenantEntity
{
    public string Code { get; set; } = string.Empty;           // e.g., LEAD-STRAT
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }       // Leadership, Technical, Behavioral, etc.
    public int ProficiencyScaleMax { get; set; } = 5;          // 1-5 or 1-10 scale
	public bool IsActive { get; set; } = true;

    public virtual ICollection<PositionCompetency> PositionCompetencies { get; set; } = new List<PositionCompetency>();
    public virtual ICollection<CompetencySkillIndicator> SkillIndicators { get; set; } = new List<CompetencySkillIndicator>();
    public virtual ICollection<EmployeeCompetency> EmployeeCompetencies { get; set; } = new List<EmployeeCompetency>();
}

/// <summary>
/// Declares that possession of a skill at a given level provides evidence
/// towards a competency. Many skills can contribute to one competency;
/// one skill can contribute to many competencies.
/// This is evidence mapping, not equivalence — a skill does not "equal"
/// a competency, it supports the case that the competency is present.
/// </summary>
public class CompetencySkillIndicator : TenantEntity
{
    public Guid CompetencyId { get; set; }
    public Competency Competency { get; set; } = null!;

    public Guid SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The minimum skill level that constitutes meaningful evidence
    /// towards this competency.
    /// </summary>
    public SkillLevel MinimumSkillLevelRequired { get; set; }

    /// <summary>
    /// Narrative explanation of why this skill is an indicator of the
    /// competency — useful for assessors reviewing gap reports.
    /// </summary>
    public string? Rationale { get; set; }
}

/// <summary>
/// Required proficiency level for a position definition.
/// </summary>
public class PositionCompetency : TenantEntity
{
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;
    
    public Guid CompetencyId { get; set; }
    
    [ForeignKey(nameof(CompetencyId))]
    public virtual Competency Competency { get; set; } = null!;

    public int RequiredProficiencyLevel { get; set; }   // e.g., 4 out of 5
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// An employee's current assessed competency level.
///
/// Authority source for <see cref="CurrentProficiencyLevel"/>. Always reflects
/// the most recent assessment. Every time this record is updated the previous
/// values must be written to <see cref="EmployeeCompetencyHistory"/> by the
/// application layer before the update is committed.
/// </summary>
public class EmployeeCompetency : TenantEntity
{
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid CompetencyId { get; set; }

    [ForeignKey(nameof(CompetencyId))]
    public virtual Competency Competency { get; set; } = null!;

    public int CurrentProficiencyLevel { get; set; }
    
    public DateTime AssessmentDate { get; set; }
    
	public Guid? AssessedById { get; set; }

    [ForeignKey(nameof(AssessedById))]
    public virtual Employee? AssessedBy { get; set; }
    
	public string? AssessmentMethod { get; set; }   // PerformanceReview, 360, Test, etc.
    
    public string? EvidenceNotes { get; set; }
	
	public virtual ICollection<EmployeeCompetencyHistory> History { get; set; } = new List<EmployeeCompetencyHistory>();
}

/// <summary>
/// Full audit trail of competency assessments for one employee–competency pair.
/// Written by the application layer every time <see cref="EmployeeCompetency"/>
/// is updated — never modified after creation.
/// </summary>
public class EmployeeCompetencyHistory : TenantEntity
{
    public Guid EmployeeCompetencyId { get; set; }
    
    [ForeignKey(nameof(EmployeeCompetencyId))]
    public virtual EmployeeCompetency EmployeeCompetency { get; set; } = null!;

    public int ProficiencyLevel { get; set; }
    
    public DateTime AssessmentDate { get; set; }

    public Guid? AssessedById { get; set; }
    
    [ForeignKey(nameof(AssessedById))]
    public virtual Employee? AssessedBy { get; set; }

    public string? AssessmentMethod { get; set; }
    
    public string? EvidenceNotes { get; set; }

    /// <summary>
    /// Reason for the change (e.g. post-training re-assessment, promotion review).
    /// </summary>
    public string? ChangeReason { get; set; }

    public DateTime RecordedAt    { get; set; }
    
    public Guid? RecordedById  { get; set; }
    
    [ForeignKey(nameof(RecordedById))]
    public virtual Employee? RecordedBy   { get; set; }
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
