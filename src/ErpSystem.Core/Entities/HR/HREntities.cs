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
public class Employee : TenantEntity, IDisabilityTypeConsumer
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

    /// <summary>
    /// How the employee describes their gender, where <see cref="Enums.Gender.Other"/> is chosen.
    /// </summary>
    /// <remarks>
    /// The enum offers <c>Other</c> and then had nowhere to say what "other" means, which makes the
    /// option a dead end for the person choosing it. Free text on purpose: an enumeration of the
    /// answers would be the same problem one level down.
    /// </remarks>
    [MaxLength(100)]
    public string? GenderDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }

    [MaxLength(50)]
    public string? Religion { get; set; }

    /// <summary>Home town or place of origin.</summary>
    [MaxLength(150)]
    public string? Hometown { get; set; }

    /// <summary>
    /// Whether the employee has a disability, and what it is.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Deliberately DUPLICATED with <see cref="EmployeeDependent"/>, not moved from it.</b>
    /// The demo feedback recorded these as sitting "on EmployeeDependent, not Employee" — read as a
    /// misplacement. It is not one: a dependant's disability and an employee's own are different
    /// facts about different people, and both are needed. The dependant's fields stay exactly as
    /// they are; these are new. Decided by the user, 2026-09-01.</para>
    /// <para>Same names and same length as the dependant's pair, so a reader moving between the two
    /// is not asked to learn a second vocabulary for one idea.</para>
    /// </remarks>
    public bool HasDisability { get; set; }

    /// <summary>Which kind, from the tenant's catalogue (round 3, lane P2). Notes stay in the description.</summary>
    public Guid? DisabilityTypeId { get; set; }

    [ForeignKey(nameof(DisabilityTypeId))]
    public virtual DisabilityType? DisabilityType { get; set; }

    [MaxLength(500)]
    public string? DisabilityDescription { get; set; }

    public bool IsFullTime { get; set; } = true;

    public DateOnly? DateEmployed { get; set; }

    // Contact Information
    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// ⚠ A DISPLAY SNAPSHOT since 2026-09-03, not the source of truth. When
    /// <see cref="GeoAreaId"/> is set the service overwrites this with the resolved town or
    /// district name. Kept because reports, integrations and ported rows read it, and because an
    /// employee whose address predates the geography tree still has to say something.
    /// </summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// ⚠ A DISPLAY SNAPSHOT since 2026-09-03 — see <see cref="City"/>. Overwritten with the
    /// resolved region name when <see cref="GeoAreaId"/> is set.
    /// </summary>
    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>
    /// Where this employee lives, as one reference to the administrative geography tree.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ One FK, not one per tier.</b> It points at the <i>lowest</i> tier known — a
    /// community if that is what was chosen, a district if not — and the ancestors come from
    /// <c>GeoArea.Path</c>. That is what lets Ghana's scheme gain a fifth tier, or a second country
    /// arrive with a different depth, without a migration on this table. A
    /// <c>RegionId</c>/<c>DistrictId</c>/<c>TownId</c> trio would have to be migrated the day
    /// either happened.</para>
    ///
    /// <para>Nullable and expected to stay null on plenty of rows: the register predates the
    /// geography tree, and <see cref="State"/> / <see cref="City"/> carry whatever those rows
    /// already said. See docs/GEOGRAPHY-REFERENCE-DESIGN.md.</para>
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    /// <summary>
    /// Optional since 2026-09-03. A register never has an address for every driver, carpenter or
    /// bill distributor, and a required column only taught data loads to invent one. Null means
    /// "none"; the service normalises blank to null so the filtered unique index
    /// (<c>IX_Employee_Tenant_EmailAddress</c>, <c>WHERE EmailAddress IS NOT NULL</c>) is never
    /// asked to compare two empty strings.
    /// </summary>
    [MaxLength(200)]
    [ErpSystem.Core.Validation.OptionalEmailAddress]
    public string? EmailAddress { get; set; }

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

    /// <summary>
    /// Where <see cref="ProbationPeriodDays"/> came from — the post, the company policy, or a
    /// length supplied for this person.
    /// </summary>
    /// <remarks>
    /// ⚠ Null on rows created before lane D1 (2026-09-09). See <see cref="Core.Enums.ProbationSource"/>
    /// for why it is not defaulted.
    /// </remarks>
    public ProbationSource? ProbationSource { get; set; }

    /// <summary>
    /// The date probation was passed. <b>An outcome, not a term.</b>
    /// </summary>
    /// <remarks>
    /// ⚠ Written by <c>ProbationService.MarkEmployeeConfirmed</c> — and, for staff confirmed
    /// before this system existed, by the IMPORT door only. The ordinary employee update refuses
    /// it (lane D1): until then <c>UpdateEmployeeDto.ConfirmationDate</c> was applied straight onto
    /// this column by <c>EmployeeMappingExtensions.Apply</c> with no guard at all, so anyone who
    /// could edit an employee could confirm them — bypassing the authority, the letter and the
    /// probation record. The contract's own copy had been guarded since lane 3d; the header, which
    /// is the one the guard READS, had not.
    /// </remarks>
    public DateOnly? ConfirmationDate { get; set; }

    public DateOnly? RetirementDate { get; set; }

    public DateOnly? EndDate { get; set; } // Populated on exit

    /// <summary>
    /// ⚠ <b>LEGACY, and a caller-supplied path.</b> It is on the create AND update DTOs and mapped
    /// straight onto the entity, while no photo upload endpoint has ever existed — so the only way
    /// to "set" an employee photo has been for a caller to type a location. That is the sink area 16
    /// replaced wholesale and D-10, D-14 and D-39 each removed again after shipping; this is its
    /// seventh instance and the first on the module's most-used entity.
    /// <para>Kept because it holds ported values. Use <see cref="PhotoDocumentRecordId"/> and its
    /// siblings for anything new; the download helper prefers the DMS ids and only falls back to
    /// this string. It should be retired once the ported images are migrated through the gate.</para>
    /// </summary>
    [MaxLength(500)]
    public string? PicturePath { get; set; }

    // ── The photograph, through the controlled gate ───────────────────────────
    public Guid? PhotoFileUploadRecordId { get; set; }
    public Guid? PhotoDocumentRecordId { get; set; }
    public Guid? PhotoDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? PhotoFileName { get; set; }

    [MaxLength(150)]
    public string? PhotoMimeType { get; set; }

    public long? PhotoFileSizeBytes { get; set; }

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

    // ── Payroll membership ──────────────────────────────────────────────────────────────────
    // Not every employee is paid through the payroll run: consultants are paid on invoice, interns
    // and national service personnel on an allowance, secondees by their parent body. This is HR's
    // statement of WHICH; membership of an actual run stays payroll's decision on its own profile
    // (PayrollEmployeeProfile.PayrollActive). When false, Salary, the five switches above and the
    // grade/notch assignment are not captured — see EmployeeService.

    /// <summary>Whether this person is paid through the payroll run.</summary>
    public bool IsOnPayroll { get; set; } = true;

    /// <summary>Why not, when <see cref="IsOnPayroll"/> is false. Null while on payroll.</summary>
    public OffPayrollReason? OffPayrollReason { get; set; }

    /// <summary>Free text qualifying the reason (who pays, under what arrangement).</summary>
    [MaxLength(500)]
    public string? OffPayrollNote { get; set; }

    // ── Pay basis ───────────────────────────────────────────────────────────────────────────
    // A different question from membership. On payroll says the run pays them; this says how the
    // figure it pays is arrived at — read off the scale, or agreed for the person.

    /// <summary>Scale or negotiated. See <see cref="Core.Enums.PayBasis"/>.</summary>
    /// <remarks>
    /// Defaults to the scale, which is what every existing row was implicitly: a placement on a
    /// grade was the only pay basis HR could record before lane E1.
    /// </remarks>
    public PayBasis PayBasis { get; set; } = PayBasis.SalaryScale;

    /// <summary>
    /// Why the pay is negotiated — "contract engagement, rate per agreement of 2026-07-01". Required
    /// when <see cref="PayBasis"/> is <c>Negotiated</c>; cleared on return to the scale.
    /// </summary>
    [MaxLength(500)]
    public string? PayBasisNote { get; set; }

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

    /// <summary>
    /// Completed years of service, counting only anniversaries that have actually come round.
    /// </summary>
    /// <remarks>
    /// Corrected in area 14 slice 3b. This previously read
    /// <c>DateTime.Today.Year - DateEmployed.Value.Year</c>, which reports a completed year on
    /// 1 January for someone whose anniversary falls in December — overstating service by up to a
    /// year for anyone whose start date has not yet come round. It fell on exactly the rules that
    /// turn on a threshold, and it disagreed with <c>HrPolicyCalculations.Age</c>, which had always
    /// done the check correctly. Both now share one implementation.
    /// </remarks>
    [NotMapped]
    public int? YearsOfService => ErpSystem.Core.Services.HR.HrPolicyCalculations.CompletedYears(DateEmployed);

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

    /// <summary>The administrative area this employee lives in. See <see cref="GeoAreaId"/>.</summary>
    public virtual ErpSystem.Core.Entities.Reference.GeoArea? GeoArea { get; set; }

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

    /// <summary>The benefit groups this policy belongs to (round 2, lane C3 — register row P-3).</summary>
    public virtual ICollection<BenefitGroupMember> GroupMemberships { get; set; } = new List<BenefitGroupMember>();
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

    /// <summary>When an approved manpower budget last set <see cref="ExpectedHeadcount"/>.</summary>
    /// <remarks>
    /// <para>⚠ This column exists to answer one question FR-HR-136 cannot be enforced without:
    /// <b>is this position's establishment an authorised number, or is it still the column
    /// default?</b> Measured on the live tenant, <b>132 of 146 positions carry
    /// <c>ExpectedHeadcount = 1</c></b> — the default, never touched — while one of them holds over
    /// a thousand people. A rule that refuses a vacancy "outside the establishment" would therefore
    /// refuse almost everything, which is why area 8 had to downgrade FR-HR-173 to advisory and why
    /// area 6's establishment classification is decorative.</para>
    ///
    /// <para>With this, the rule can have teeth exactly where it is meaningful: null means nobody
    /// has ever authorised a headcount for this post, so it is not establishment-constrained — the
    /// same shape as the requisition budget check's "no approved budget line" branch. Set means the
    /// number came from a manpower budget that went the whole way up FR-HR-135's chain, and it is
    /// then worth refusing to exceed.</para>
    ///
    /// <para>Written by <c>ManpowerBudgetService</c> on approval (decision D-2) and by the
    /// establishment admin endpoint for posts no budget covers.</para>
    /// </remarks>
    public DateTime? EstablishmentApprovedOn { get; set; }

    /// <summary>The manpower budget that authorised the current establishment, when one did.</summary>
    /// <remarks>
    /// Null when the establishment was set directly by HR rather than derived from a budget — both
    /// are legitimate, and telling them apart is what lets a screen say <i>where the number came
    /// from</i> rather than just showing it.
    /// </remarks>
    public Guid? EstablishmentSourceBudgetId { get; set; }

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

    /// <summary>
    /// How much surety the post requires, where <see cref="RequiresGuarantor"/> is set.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b><c>RequiresGuarantor</c> was a bare bool</b> — the post could demand a guarantor
    /// and never say for how much, so "is this cashier properly guaranteed?" was a question the
    /// system could pose and not answer. The same shape as <c>FamilyAccompanying</c>, and the same
    /// fix: give the flag something behind it.</para>
    ///
    /// <para>Per POSITION rather than per employee or per tenant, because the exposure is the
    /// post's: a cashier handling daily takings needs a larger surety than a clerk, and it should
    /// not have to be re-argued for each person appointed to the seat.</para>
    ///
    /// <para>Null with <c>RequiresGuarantor</c> true means "a guarantor is required, no amount is
    /// specified" — a legitimate state, and the compliance read reports the guarantor as present
    /// or absent without judging the sum.</para>
    /// </remarks>
    public decimal? RequiredGuarantorAmount { get; set; }

    /// <summary>
    /// The currency that requirement is stated in, validated against Finance's currency master.
    /// </summary>
    /// <remarks>
    /// Null means the tenant's configured HR default. ⚠ Comparing a surety in one currency against
    /// a requirement in another needs a rate and a date; see <c>EmployeeDocumentService</c>'s
    /// guarantor compliance for what it does and — more importantly — what it refuses to guess.
    /// </remarks>
    [MaxLength(3)]
    public string? RequiredGuarantorCurrencyCode { get; set; }

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

    /// <summary>
    /// What the post must hold (round 2, lane C2 — register row P-2). <see cref="RequiresCertification"/>
    /// and <see cref="RequiresLicense"/> stay as the switches; these rows are what they mean.
    /// </summary>
    public virtual ICollection<PositionCertificationRequirement> CertificationRequirements { get; set; } = new List<PositionCertificationRequirement>();
    public virtual List<EmployeePositionBenefit> PositionBenefits { get; set; } = new List<EmployeePositionBenefit>();
    public virtual ICollection<PositionOvertimePolicy> OvertimePolicies { get; set; } = new List<PositionOvertimePolicy>();

    // ── Named sets (round 2, lane C3 — register rows P-3, S-3; plan § 6.4) ──────────────────
    // Attachments, not rows: what the post actually requires is the UNION of these sets' members
    // with the individual collections above, which is what IPositionEffectiveSets computes. Every
    // consumer of the individual rows reads the union instead — a set whose members nothing acts
    // on is the dead path this lane exists to avoid.
    public virtual ICollection<EmployeePositionBenefitGroup> BenefitGroups { get; set; } = new List<EmployeePositionBenefitGroup>();
    public virtual ICollection<PositionSkillSet> SkillSets { get; set; } = new List<PositionSkillSet>();
    public virtual ICollection<PositionCertificationSet> CertificationSets { get; set; } = new List<PositionCertificationSet>();
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

    /// <summary>How long this kind of engagement normally runs, in MONTHS. Zero means open-ended.</summary>
    /// <remarks>
    /// It is what gives <see cref="EmployeeContractDetail.ContractEndDate"/> a default: pick
    /// "Fixed Term" (12) on a contract starting 1 March and the end date offered is 28 February.
    /// </remarks>
    public int Duration { get; set; }

    /// <summary>
    /// Retire a kind of engagement without deleting the contracts written against it.
    /// </summary>
    /// <remarks>
    /// ⚠ Added in lane D1. The table was seeded with seven TDC rows in 2026 and had no DTO, no
    /// service, no controller and no screen — it could be read only by opening the database. It is
    /// now the contract-kind picker, so it needs the retire-without-delete every other HR lookup has.
    /// </remarks>
    public bool IsActive { get; set; } = true;
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

    /// <summary>
    /// Where this address sits on the shared administrative-geography tree (round 2, lane D2 —
    /// register row E-3).
    /// </summary>
    /// <remarks>
    /// ⚠ When this is set, <see cref="City"/> and <see cref="Region"/> above become DISPLAY
    /// SNAPSHOTS written from the tree — the <c>Employee.City</c>/<c>State</c> convention. A null
    /// area leaves both exactly as they were; most rows predate the tree and the free text is the
    /// only address they have.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    [ForeignKey(nameof(GeoAreaId))]
    public virtual ErpSystem.Core.Entities.Reference.GeoArea? GeoArea { get; set; }
 
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Represents emergency contacts for employees
/// </summary>
public class EmployeeEmergencyContact : TenantEntity, IRelationshipTypeConsumer
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

    /// <summary>
    /// How this person is tied to the employee, as words.
    /// </summary>
    /// <remarks>
    /// ⚠ Widened from 50 to 100 in round 2 lane D2, to match the guarantor's column and the
    /// catalogue's <c>RelationshipType.Name</c>. It had been the narrowest of the four columns
    /// spelling out the same idea, while its DTO carried no length at all and the screen's schema
    /// allowed 100 — so a 51-character relationship reached SQL Server and failed as a truncation
    /// 500 rather than a refusal.
    /// </remarks>
    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>
    /// The tie, from the tenant's relationship catalogue (round 2, lane D2 — register rows E-11a,
    /// E-11b).
    /// </summary>
    /// <remarks>
    /// ⚠ The free-text <c>Relationship</c> above is kept and MIRRORED from this row's name
    /// whenever the id is set, so rows written before the catalogue keep their wording and every
    /// consumer that reads the string keeps working. Nullable: a tie nobody has catalogued may
    /// still be typed.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

    [ForeignKey(nameof(RelationshipTypeId))]
    public virtual RelationshipType? RelationshipTypeRef { get; set; }

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

    /// <summary>⚠ A DISPLAY SNAPSHOT when <see cref="GeoAreaId"/> is set — see that field.</summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>⚠ A DISPLAY SNAPSHOT when <see cref="GeoAreaId"/> is set — see that field.</summary>
    [MaxLength(100)]
    public string? Region { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>
    /// Where this address sits on the shared administrative-geography tree (round 2, lane D2 —
    /// register row E-3).
    /// </summary>
    /// <remarks>
    /// ⚠ When this is set, <see cref="City"/> and <see cref="Region"/> become DISPLAY SNAPSHOTS
    /// written from the tree — the <c>Employee.City</c>/<c>State</c> convention. A null area
    /// leaves both exactly as they were.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    [ForeignKey(nameof(GeoAreaId))]
    public virtual ErpSystem.Core.Entities.Reference.GeoArea? GeoArea { get; set; }

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
public class EmployeeDependent : TenantEntity, IDisabilityTypeConsumer
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

    /// <summary>How the dependant describes their gender, where Gender is Other.</summary>
    [MaxLength(100)]
    public string? GenderDescription { get; set; }

    public bool HasDisability { get; set; }

    /// <summary>Which kind, from the tenant's catalogue (round 3, lane P2). Notes stay in the description.</summary>
    public Guid? DisabilityTypeId { get; set; }

    [ForeignKey(nameof(DisabilityTypeId))]
    public virtual DisabilityType? DisabilityType { get; set; }

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

    /// <summary>⚠ Legacy caller-supplied path, as on <see cref="Employee"/>. See the note there.</summary>
    [MaxLength(500)]
    public string? PicturePath { get; set; }

    // The photograph, through the controlled gate.
    public Guid? PhotoFileUploadRecordId { get; set; }
    public Guid? PhotoDocumentRecordId { get; set; }
    public Guid? PhotoDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? PhotoFileName { get; set; }

    [MaxLength(150)]
    public string? PhotoMimeType { get; set; }

    public long? PhotoFileSizeBytes { get; set; }

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

    /// <summary>
    /// The employer's street address, as one line.
    /// </summary>
    /// <remarks>
    /// ⚠ 200, and the screen's schema said 300 until round 2 lane D2 (finding X-5) — so a 201-to-300
    /// character address passed the form, was refused by the create with a 400 the user could not
    /// have predicted, and reached SQL Server as a truncation 500 on the update, whose DTO carried
    /// no length at all. The country, area and city below are where the structured part now lives.
    /// </remarks>
    [MaxLength(200)]
    public string? CompanyAddress { get; set; }

    /// <summary>
    /// The country the employer is in. Added in round 2 lane D2 (register row E-6): the work
    /// history was one free-text line with no country, city or geography at all.
    /// </summary>
    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    /// <summary>⚠ A DISPLAY SNAPSHOT when <see cref="GeoAreaId"/> is set — see that field.</summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>⚠ A DISPLAY SNAPSHOT when <see cref="GeoAreaId"/> is set — see that field.</summary>
    [MaxLength(100)]
    public string? Region { get; set; }

    /// <summary>
    /// Where this address sits on the shared administrative-geography tree (round 2, lane D2 —
    /// register row E-3).
    /// </summary>
    /// <remarks>
    /// ⚠ When this is set, <see cref="City"/> and <see cref="Region"/> become DISPLAY SNAPSHOTS
    /// written from the tree — the <c>Employee.City</c>/<c>State</c> convention. A null area
    /// leaves both exactly as they were.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    [ForeignKey(nameof(GeoAreaId))]
    public virtual ErpSystem.Core.Entities.Reference.GeoArea? GeoArea { get; set; }

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

    /// <summary>
    /// Which kind of engagement this is, from the tenant's own vocabulary.
    /// </summary>
    /// <remarks>
    /// ⚠ A DIFFERENT axis from <see cref="EmploymentType"/>, which is the system's fixed enum.
    /// This is TDC's list — Permanent, Contract, Fixed Term, National Service, Internship, Casual,
    /// Consultancy — the one the appointment letter picks from, and it carries the
    /// <see cref="EmployeeContractType.Duration"/> that gives <see cref="ContractEndDate"/> a default.
    /// Nullable: rows written before lane D1, and tenants that keep no such list, name no kind.
    /// </remarks>
    public Guid? ContractTypeId { get; set; }

    [ForeignKey(nameof(ContractTypeId))]
    public virtual EmployeeContractType? ContractType { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>
    /// When the engagement ACTUALLY ended.
    /// </summary>
    /// <remarks>
    /// ⚠ Not the same date as <see cref="ContractEndDate"/>, which is when it was SCHEDULED to end.
    /// A two-year contract run to term has both, equal; one terminated in month seven has an
    /// EndDate of month seven and a ContractEndDate still in month twenty-four, and the gap between
    /// them is the fact a separation or a renewal report is asking about. This column is written by
    /// the terminate and separate paths (<c>EndDate ??= terminationDate</c>) and by the supersede
    /// path, which closes an open row the day before its successor takes effect.
    /// </remarks>
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

    /// <summary>
    /// The day these terms take effect — the one the contract history is ordered and closed on.
    /// </summary>
    /// <remarks>
    /// ⚠ Every row added through the manual tab before lane D1 carried <c>0001-01-01</c>:
    /// <c>AddContractAsync</c> never set it and the DTO did not expose it. It now defaults to
    /// <see cref="StartDate"/> when a caller says nothing, and superseding a current row uses it to
    /// date the closure.
    /// </remarks>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// When the engagement is SCHEDULED to end. Null for permanent employment; populated for
    /// fixed-term and contract appointments.
    /// </summary>
    /// <remarks>
    /// See <see cref="EndDate"/> for the distinction. Defaulted from
    /// <see cref="EmployeeContractType.Duration"/> when a kind is named and no date is given.
    /// </remarks>
    public DateOnly? ContractEndDate { get; set; }

    public int WorkingHoursPerWeek { get; set; } = 40;

    /// <summary>
    /// The annual leave this contract grants, as the contract states it.
    /// </summary>
    /// <remarks>
    /// ⚠ Written by NOTHING and read by NOTHING until lane D1 — a column with a default of 20 that
    /// no caller could reach, sitting beside <see cref="VacationDaysPerYear"/> (default 15), which
    /// the DTOs did expose. Two numbers for one entitlement, disagreeing by five days out of the
    /// box. This is the one the appointment letter quotes and the one the leave module's
    /// entitlement should be reconciled against; <see cref="VacationDaysPerYear"/> stays for the
    /// rows that hold it.
    /// </remarks>
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
    /// Whether these are the terms in force today. At most one row per employee carries it.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Until lane D1 this was a default nobody maintained: <c>AddContractAsync</c> left it
    /// at <c>true</c> on every row it inserted, nothing closed the row it superseded, and the two
    /// termination paths cleared <c>IsActive</c> without clearing this — so
    /// <see cref="Employee.CurrentTerms"/>, which reads it, returned whichever contract EF happened
    /// to materialise first, terminated ones included.</para>
    ///
    /// <para>It is now maintained by one helper, and it is the flag the "current contract" read and
    /// the header write-through both key off. <see cref="IsActive"/> and
    /// <see cref="ContractStatus"/> remain the ROW's own lifecycle: a superseded contract is not
    /// current, and it is also no longer active — but an inactive row that was never superseded
    /// (a draft, a backfilled historical term) is not current either.</para>
    /// </remarks>
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

    /// <summary>
    /// Whether family came with them — and, when true, <see cref="FamilyMembers"/> says who.
    /// </summary>
    /// <remarks>
    /// ⚠ This was a bare bool with nothing behind it: the record could say a family had accompanied
    /// the assignee and never who they were, so nobody could tell how many permits were owed or
    /// whose were expiring. The flag stays — it is what a form asks first — and the collection is
    /// what makes it answerable.
    /// </remarks>
    public bool FamilyAccompanying { get; set; }

    [MaxLength(1000)]
    public string? AssignmentObjective { get; set; } // e.g., Project, Training

    [MaxLength(100)]
    public string? VisaType { get; set; }

    /// <summary>
    /// When the visa was ISSUED, as opposed to when it runs out.
    /// </summary>
    /// <remarks>
    /// ⚠ Every permit on this record carried an expiry and no issue date, so it could never answer
    /// "how long was this granted for" — the question asked when a renewal is refused or shortened.
    /// </remarks>
    public DateOnly? VisaIssueDate { get; set; }

    public DateOnly? VisaExpiryDate { get; set; }

    [MaxLength(100)]
    public string? WorkPermitNumber { get; set; }

    public DateOnly? WorkPermitIssueDate { get; set; }

    public DateOnly? WorkPermitExpiryDate { get; set; }

    /// <summary>
    /// The residence permit — a separate instrument from the work permit.
    /// </summary>
    /// <remarks>
    /// ⚠ These are issued by different authorities on different clocks: the work permit says you
    /// may be employed, the residence permit says you may live here. Recording only one and calling
    /// it "the permit" is how somebody ends up lawfully employed and unlawfully resident, with a
    /// record that cannot show it.
    /// </remarks>
    [MaxLength(100)]
    public string? ResidentPermitNumber { get; set; }

    public DateOnly? ResidentPermitIssueDate { get; set; }

    public DateOnly? ResidentPermitExpiryDate { get; set; }

    public virtual ICollection<ExpatriateFamilyMember> FamilyMembers { get; set; }
        = new List<ExpatriateFamilyMember>();

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

    /// <summary>
    /// The last day these terms were in force. Null while they still are.
    /// </summary>
    /// <remarks>
    /// ⚠ Written ONLY for a placement that actually took effect. A placement withdrawn before its
    /// start date was never in force, so it has no window at all — see <see cref="WithdrawnAt"/>.
    /// </remarks>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// When this placement was withdrawn — as opposed to superseded by a later one, or simply run
    /// to its end. Null on a placement that stands, and on one replaced in the ordinary way.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ This column exists because one date interval was carrying two different facts,
    /// and the collision had teeth.</b> "When were these terms in force" is the window; "was this
    /// row withdrawn" is a separate act. Closing used to express the second by clamping the first,
    /// and the clamp could not go earlier than the row's own start date without producing a window
    /// that ends before it begins. So a placement withdrawn on or before its start date was closed
    /// to <c>EffectiveTo = EffectiveDate</c> — a one-day window.</para>
    ///
    /// <para>For a placement starting TODAY that was a cosmetic lag: it read as in force until
    /// midnight. For a FUTURE-dated one — a promotion booked ahead, then withdrawn when the person
    /// moved to negotiated pay or came off payroll — it was a live defect: the window
    /// <c>[1 Oct, 1 Oct]</c> matches the as-of predicate again ON 1 October, weeks after the
    /// withdrawal, and <c>EmolumentService</c> would read that notch as basic pay for the day.
    /// Benefit enrolment computes contributions from that figure.</para>
    ///
    /// <para>Every as-of read must exclude withdrawn rows. Kept rather than deleted, and kept
    /// distinct from <c>IsDeleted</c> — "entered in error" and "withdrawn because the pay basis
    /// changed" are different statements, and the second is worth showing on the tab.</para>
    /// </remarks>
    public DateTime? WithdrawnAt { get; set; }

    /// <summary>Why it was withdrawn — "taken off payroll", "pay basis changed to negotiated".</summary>
    [MaxLength(500)]
    public string? WithdrawnReason { get; set; }

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
public class EmployeeReferee : TenantEntity, IRelationshipTypeConsumer
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

    /// <summary>
    /// The tie, from the tenant's relationship catalogue (round 2, lane D2 — register rows E-11a,
    /// E-11b).
    /// </summary>
    /// <remarks>
    /// ⚠ The free-text <c>Relationship</c> above is kept and MIRRORED from this row's name
    /// whenever the id is set, so rows written before the catalogue keep their wording and every
    /// consumer that reads the string keeps working. Nullable: a tie nobody has catalogued may
    /// still be typed.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

    [ForeignKey(nameof(RelationshipTypeId))]
    public virtual RelationshipType? RelationshipTypeRef { get; set; }

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

    // ── The written reference, through the controlled gate ────────────────────
    // A referee could be recorded, phoned and noted, and the letter they actually wrote had nowhere
    // to live. Three ids, never a path.
    public Guid? LetterFileUploadRecordId { get; set; }
    public Guid? LetterDocumentRecordId { get; set; }
    public Guid? LetterDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? LetterFileName { get; set; }

    [MaxLength(150)]
    public string? LetterMimeType { get; set; }

    public long? LetterFileSizeBytes { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Represents guarantors for employees (if required)
/// </summary>
public class EmployeeGuarantor : TenantEntity, IRelationshipTypeConsumer
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

    /// <summary>
    /// The tie, from the tenant's relationship catalogue (round 2, lane D2 — register rows E-11a,
    /// E-11b).
    /// </summary>
    /// <remarks>
    /// ⚠ The free-text <c>Relationship</c> above is kept and MIRRORED from this row's name
    /// whenever the id is set, so rows written before the catalogue keep their wording and every
    /// consumer that reads the string keeps working. Nullable: a tie nobody has catalogued may
    /// still be typed.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

    [ForeignKey(nameof(RelationshipTypeId))]
    public virtual RelationshipType? RelationshipTypeRef { get; set; }

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

    /// <summary>How the guarantor describes their gender, where Gender is Other.</summary>
    [MaxLength(100)]
    public string? GenderDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    /// <summary>⚠ A DISPLAY SNAPSHOT when <see cref="GeoAreaId"/> is set — see that field.</summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>⚠ A DISPLAY SNAPSHOT when <see cref="GeoAreaId"/> is set — see that field.</summary>
    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>
    /// Where this address sits on the shared administrative-geography tree (round 2, lane D2 —
    /// register row E-3).
    /// </summary>
    /// <remarks>
    /// ⚠ When this is set, <see cref="City"/> and <see cref="Region"/> become DISPLAY SNAPSHOTS
    /// written from the tree — the <c>Employee.City</c>/<c>State</c> convention. A null area
    /// leaves both exactly as they were.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    [ForeignKey(nameof(GeoAreaId))]
    public virtual ErpSystem.Core.Entities.Reference.GeoArea? GeoArea { get; set; }

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

    /// <summary>
    /// The kind of national ID, as free text — the ported column.
    /// </summary>
    /// <remarks>
    /// ⚠ Kept alongside <see cref="NationalIdTypeId"/>, not replaced by it (the lane-3b idiom for
    /// the certifying body): existing rows hold text nobody has mapped, and blanking it would lose
    /// what was typed. The screen offers the lookup; this column is shown only where the FK is
    /// null. Nothing new should write it.
    /// </remarks>
    [MaxLength(50)]
    public string? NationalIdType { get; set; } // e.g. Ghana Card, Passport

    /// <summary>
    /// The kind of national ID, from the tenant's identification-type catalogue (demo feedback
    /// round 2, E-13: "National ID type for the guarantor should be a dropdown populated with the
    /// set up identification document types").
    /// </summary>
    public Guid? NationalIdTypeId { get; set; }

    [ForeignKey(nameof(NationalIdTypeId))]
    public virtual IdentificationType? NationalIdTypeRef { get; set; }

    [MaxLength(100)]
    public string? NationalIdNumber { get; set; }

    public DateOnly? NationalIdExpiryDate { get; set; }

    /// <summary>
    /// What the guarantor stands surety FOR.
    /// </summary>
    /// <remarks>
    /// ⚠ Distinct from <c>MonthlyIncome</c>, which is what the guarantor EARNS — the row could say
    /// how solvent the person was and never what they had undertaken, which is the only figure that
    /// matters if the guarantee is ever called. Stated in the tenant's default currency
    /// (<c>CompanyHrPolicySettings.DefaultCurrencyCode</c>); a per-row currency would need the
    /// Finance rate bridge and no HR guarantee has ever been in anything but GHS.
    /// </remarks>
    public decimal? AmountGuaranteed { get; set; }

    /// <summary>
    /// The currency that amount is stated in — validated against FINANCE's currency master.
    /// </summary>
    /// <remarks>
    /// ⚠ A bare three-letter code that nothing checks is how travel ended up able to file a claim
    /// in "XYZ" and total it. <c>HrCurrencyBridge.RequireKnownCurrencyAsync</c> refuses a code
    /// Finance does not hold. Null means the tenant's configured HR default applies, so existing
    /// rows keep meaning what they meant.
    /// <para>No FK column: Finance's uniqueness is <c>(TenantId, Code)</c>, and a composite FK here
    /// would make a currency re-code a schema problem — the same call travel made.</para>
    /// </remarks>
    [MaxLength(3)]
    public string? AmountGuaranteedCurrencyCode { get; set; }

    // ── The guarantor's photograph, through the controlled gate ───────────────
    // ⚠ Three ids and never a path. Note `GuarantorFormPath` above: a caller-supplied file location
    // of exactly the kind D-10, D-14 and D-39 each had to remove. It is left alone here because it
    // holds legacy values and gating it is its own migration, but nothing NEW on this row may use
    // that shape — which is why the photograph gets its own gated columns rather than a second path.
    public Guid? PhotoFileUploadRecordId { get; set; }
    public Guid? PhotoDocumentRecordId { get; set; }
    public Guid? PhotoDocumentVersionId { get; set; }

    [MaxLength(255)]
    public string? PhotoFileName { get; set; }

    [MaxLength(150)]
    public string? PhotoMimeType { get; set; }

    public long? PhotoFileSizeBytes { get; set; }

    public bool HasSignedGuarantorForm { get; set; } = false;

    public DateOnly? DateFormSigned { get; set; }

    /// <summary>
    /// LEGACY caller-supplied location of the signed guarantor form.
    /// </summary>
    /// <remarks>
    /// ⚠ Read-only since demo feedback round 2 (lane A-6). The write DTOs no longer carry it — a
    /// path string on a JSON body is the sink D-10, D-14 and D-39 each had to remove — and the
    /// signed form now goes on the row as an <see cref="EmployeeGuarantorDocument"/> through the
    /// gate. The column stays so ported values are not lost; the detail read still returns it.
    /// </remarks>
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

    /// <summary>Documents pertaining to this guarantor — the signed form, an ID scan, a payslip.</summary>
    public virtual ICollection<EmployeeGuarantorDocument> Documents { get; set; } = new List<EmployeeGuarantorDocument>();

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

    /// <summary>
    /// The credential(s) that evidence this skill (round 2, lane C2 — register row S-1). A skill
    /// with <see cref="RequiresCertification"/> carries at least one.
    /// </summary>
    public virtual ICollection<SkillCertification> Certifications { get; set; } = new List<SkillCertification>();

    /// <summary>The skill sets this skill belongs to (round 2, lane C3 — register row S-3).</summary>
    public virtual ICollection<SkillSetMember> SetMemberships { get; set; } = new List<SkillSetMember>();
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

    /// <summary>
    /// Who certified the skill, as free text.
    /// </summary>
    /// <remarks>
    /// ⚠ KEPT alongside <see cref="CertifyingBodyId"/> rather than replaced. Two reasons: the
    /// existing rows are free text and dropping the column would discard them, and a genuinely
    /// one-off certifier does not deserve a catalogue row. The lookup is what makes the common
    /// case consistent; this stays for the tail.
    /// </remarks>
    [MaxLength(200)]
    public string? CertifyingBody { get; set; }

    /// <summary>The catalogued body that certified this skill, where there is one.</summary>
    public Guid? CertifyingBodyId { get; set; }

    [ForeignKey(nameof(CertifyingBodyId))]
    public virtual CertifyingBody? CertifyingBodyRef { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsVerified { get; set; } = false;

    /// <summary>
    /// The credential on the employee's certification tab that evidences this skill (round 2,
    /// lane C2). The per-skill certification columns above stay, and are shown when this is null —
    /// the lane-3b idiom for the free-text certifying body beside its FK.
    /// </summary>
    public Guid? EmployeeCertificationId { get; set; }

    [ForeignKey(nameof(EmployeeCertificationId))]
    public virtual EmployeeCertification? EmployeeCertification { get; set; }

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

    /// <summary>
    /// What KIND of qualification this is — Education, Certification, License, Membership.
    /// </summary>
    /// <remarks>
    /// ⚠ A category, NOT a rank. It cannot answer "is a Master's higher than a Diploma", which is
    /// what shortlisting and succession need; <see cref="QualificationLevelId"/> is the ladder.
    /// </remarks>
    public QualificationType Type { get; set; }

    /// <summary>Where this sits on the academic / professional ladder, when it sits on one.</summary>
    /// <remarks>
    /// Nullable because plenty of qualifications are unranked — a membership or a short course has
    /// a kind but no level, and forcing one would invent a comparison that does not exist.
    /// </remarks>
    public Guid? QualificationLevelId { get; set; }

    [ForeignKey(nameof(QualificationLevelId))]
    public virtual QualificationLevel? QualificationLevel { get; set; }

    [MaxLength(200)]
    public string? IssuingAuthority { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// An organisation that certifies a skill — an institute, board or awarding body.
/// </summary>
/// <remarks>
/// <para>Introduced because <c>EmployeeSkill.CertifyingBody</c> was free text, so "Institute of
/// Chartered Accountants", "ICAG" and "I.C.A.G." were three different certifiers as far as any
/// report was concerned.</para>
///
/// <para>⚠ The free-text column stays. A lookup that forces every one-off certifier into the
/// catalogue makes the catalogue worthless; this exists so the COMMON certifiers are consistent,
/// not so the rare ones are impossible.</para>
/// </remarks>
public class CertifyingBody : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short form, e.g. "ICAG", "CIPS". What people actually write.</summary>
    [MaxLength(50)]
    public string? Abbreviation { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Country the body is based in, where that distinguishes it.</summary>
    public Guid? CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    [MaxLength(255)]
    public string? Website { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<EmployeeSkill> EmployeeSkills { get; set; } = new List<EmployeeSkill>();

    /// <summary>The credentials this body issues (round 2, lane C2 — register row S-2).</summary>
    public virtual ICollection<Certification> Certifications { get; set; } = new List<Certification>();
}

/// <summary>
/// The academic / professional ladder a <see cref="Qualification"/> can sit on.
/// </summary>
/// <remarks>
/// <para>Separate from <see cref="QualificationType"/>, which is a CATEGORY (Education,
/// Certification, License…) and cannot be ordered. This is what makes "at least a Bachelor's"
/// answerable.</para>
///
/// <para><b><see cref="Rank"/> is the whole point.</b> A ladder whose rungs cannot be compared is
/// just a second category. Rank ascends — higher means more advanced — and is what a shortlisting
/// rule or a succession readiness check compares.</para>
/// </remarks>
public class QualificationLevel : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Ascending order. Higher is more advanced; ties are permitted for equivalents.</summary>
    public int Rank { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Qualification> Qualifications { get; set; } = new List<Qualification>();
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

    /// <summary>
    /// How many days before a card of this type expires the holder should be reminded.
    /// </summary>
    /// <remarks>
    /// Per TYPE, because the lead time is a property of the document, not of the person: a Ghana
    /// Card renewal is not a passport renewal. <c>null</c> means no reminder for this type, which
    /// is the correct reading for an ID that does not expire at all — see
    /// <see cref="HasExpiryDate"/>.
    ///
    /// ⚠ Read by <c>IdentificationExpiryReminderBackgroundService</c>. Before 2026-09-01 nothing
    /// swept <c>EmployeeIdentificationCard.ExpiryDate</c> at all, so this column and that sweep
    /// were added together — a lead time nothing acts on is a setting that only looks like a
    /// feature.
    /// </remarks>
    public int? ExpiryNotificationLeadDays { get; set; }

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

    /// <summary>
    /// Legacy caller-supplied file location for the associate's photograph.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Kept for ported values only; nothing new writes it.</b> A photograph is set through the
    /// controlled upload endpoint, which stores <see cref="PhotoFileUploadRecordId"/>. Non-nullable
    /// with an empty default, so "no photo" is an empty string here rather than null.
    /// </remarks>
    [MaxLength(500)]
    public string PicturePath { get; set; } = string.Empty;

    /// <summary>
    /// Scanned controlled upload holding the associate's photograph.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>One column, not the employee's six.</b> An associate's photo is an avatar, and the
    /// house precedent for an avatar is <c>JobCandidate.ProfilePhotoFileUploadRecordId</c>: it is
    /// deliberately NOT registered in the central DMS, because an avatar carries no retention value
    /// and one document record per photo is repository noise. An employee photograph is part of a
    /// personnel file and does get DMS ids; an external associate is a contact, not personnel.
    /// </remarks>
    public Guid? PhotoFileUploadRecordId { get; set; }

    public bool IsActive { get; set; }

    public DateTime DateAdded { get; set; }
}

#endregion
