using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.Payroll;
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
    public string? EmailAddress { get; set; }
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
    /// <summary>Paid through the payroll run. False for invoice, allowance and secondee staff.</summary>
    public bool IsOnPayroll { get; set; } = true;
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

    /// <summary>
    /// Where the employee lives, as one reference to the geography tree — the lowest tier known.
    /// The edit form re-opens its cascade from this by asking for the area's ancestors.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

    public Guid? ShiftId { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? Religion { get; set; }

    /// <summary>How the employee describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }

    /// <summary>Home town or place of origin.</summary>
    public string? Hometown { get; set; }

    /// <summary>
    /// ⚠ The EMPLOYEE's own disability, added alongside — never replacing — the one on
    /// EmployeeDependent. A dependant's disability and an employee's are different facts about
    /// different people; the feedback read as a misplacement and was not one.
    /// </summary>
    public bool HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    /// <summary>Round 3, lane P2: the catalogue row, and its name for display.</summary>
    public Guid? DisabilityTypeId { get; set; }
    public string? DisabilityTypeName { get; set; }
    public string? Address { get; set; }

    /// <summary>⚠ A display snapshot resolved from <c>GeoAreaId</c> when one is set — see the entity.</summary>
    public string? City { get; set; }

    /// <summary>⚠ A display snapshot resolved from <c>GeoAreaId</c> when one is set — see the entity.</summary>
    public string? State { get; set; }

    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public string? CountryName { get; set; }

    // ⚠ No GeoAreaName / GeoAreaFullPath here, deliberately. Either would be null on every read
    // whose query did not Include the navigation — the always-null-field shape this module has
    // met repeatedly — and Include depth would have to be right in a dozen places. Lists print
    // State and City, which is exactly what the snapshot columns are kept for; the edit form
    // resolves the display chain from GeoAreaId through the geography service's ancestors
    // endpoint, which it has to call anyway to re-open its cascade.

    public string? TelephoneNumber { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Extension { get; set; }
    public int ProbationPeriodDays { get; set; }

    /// <summary>Where the probation term came from — the post, the policy, or this person.</summary>
    /// <remarks>Null on rows created before lane D1; the term is there, its provenance is not.</remarks>
    public ProbationSource? ProbationSource { get; set; }

    /// <summary>
    /// When probation is due to end, i.e. <c>DateEmployed + ProbationPeriodDays</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ EXPECTED, not agreed. It is arithmetic over the term, computed on read so it cannot go
    /// stale, and it is null when the employee has no <c>DateEmployed</c> or no probation. The
    /// date probation was actually passed is <see cref="ConfirmationDate"/>, and nothing but the
    /// probation confirm action writes that.
    /// </remarks>
    public DateOnly? ExpectedConfirmationDate { get; set; }

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
    // Payroll membership — IsOnPayroll itself is on the summary DTO.
    public OffPayrollReason? OffPayrollReason { get; set; }
    public string? OffPayrollNote { get; set; }

    /// <summary>How basic pay is arrived at: the scale, or an amount agreed for this person.</summary>
    /// <remarks>Written through <c>PUT api/hr/Employees/{id}/pay-basis</c> only — not through the create or the ordinary update.</remarks>
    public PayBasis PayBasis { get; set; }
    public string? PayBasisNote { get; set; }

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
    // ── The photograph, through the gate ──────────────────────────────────────
    // ⚠ Read-side only. There is no way to SET these from a DTO: the image arrives through the
    // upload endpoint and the gate fills them in. `PicturePath` above is the LEGACY caller-supplied
    // location and is being retired — prefer `hasPhoto` for whether an image exists.
    public bool HasPhoto { get; set; }
    public string? PhotoFileName { get; set; }
    public string? PhotoMimeType { get; set; }
    public long? PhotoFileSizeBytes { get; set; }

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

    /// <summary>How the employee describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }

    /// <summary>Home town or place of origin.</summary>
    public string? Hometown { get; set; }

    /// <summary>
    /// ⚠ The EMPLOYEE's own disability, added alongside — never replacing — the one on
    /// EmployeeDependent. A dependant's disability and an employee's are different facts about
    /// different people; the feedback read as a misplacement and was not one.
    /// </summary>
    public bool HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    /// <summary>Round 3, lane P2: one of the tenant's live disability types. Only meaningful with HasDisability.</summary>
    public Guid? DisabilityTypeId { get; set; }
    public bool IsFullTime { get; set; } = true;
    public DateOnly? DateEmployed { get; set; }

    // Contact Information
    public string? Address { get; set; }

    /// <summary>
    /// ⚠ Overwritten by the resolved town or district when <see cref="GeoAreaId"/> is supplied.
    /// Only what a caller sends with no area survives.
    /// </summary>
    public string? City { get; set; }

    /// <summary>⚠ Overwritten by the resolved region when <see cref="GeoAreaId"/> is supplied.</summary>
    public string? State { get; set; }

    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }

    /// <summary>
    /// Where the employee lives, as one reference to the geography tree — the lowest tier the
    /// caller knows. When set, the service resolves the region and town from it and writes them
    /// into <see cref="State"/> and <see cref="City"/>, so the two can never disagree.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

    /// <summary>Optional (2026-09-03). Format-checked and unique when supplied; blank means none.</summary>
    [ErpSystem.Core.Validation.OptionalEmailAddress]
    public string? EmailAddress { get; set; }

    public string? TelephoneNumber { get; set; }
    public string? BusinessNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? Extension { get; set; }

    // Employment Details
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    /// <summary>
    /// The probation term, in days. <b>Leave it null and the server derives it</b> from the
    /// position, falling back to the company policy default.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Nullable since lane D1, and that is the point of the change. It was <c>int</c> with
    /// a default of 90, so a caller that said nothing about probation was indistinguishable from
    /// one that asked for ninety days — and 90 is not TDC's number for anybody (junior posts run
    /// three months, senior and management six). The register's own positions carry the term, 123
    /// of 146 of them, and every create was quietly overwriting it with a form default.</para>
    ///
    /// <para>A value supplied against a position that states its own is REFUSED, not silently
    /// ignored: the caller is told which post it is and what the post says. Where the position is
    /// silent, a supplied value stands and is recorded as <c>ProbationSource.Override</c>.</para>
    /// </remarks>
    public int? ProbationPeriodDays { get; set; }

    /// <summary>
    /// The date probation was passed. <b>Accepted on the IMPORT path only.</b>
    /// </summary>
    /// <remarks>
    /// ⚠ For staff whose probation ended before this system existed and who arrive already
    /// confirmed. The ordinary create refuses it with a sentence — a new hire has not passed a
    /// probation that has not started, and confirmation is an outcome the probation record records,
    /// with a letter behind it.
    /// </remarks>
    public DateOnly? ConfirmationDate { get; set; }

    public DateOnly? RetirementDate { get; set; }

    /// <summary>
    /// Which kind of engagement the employee's first contract is, from the tenant's contract-type
    /// list. Optional — a tenant that keeps no such list names no kind.
    /// </summary>
    /// <remarks>
    /// The create opens the employee's first <c>EmployeeContractDetail</c> (E-7a), and the kind is
    /// part of the terms it records: it is what the appointment letter says, and its duration is
    /// what gives a fixed-term contract its end date.
    /// </remarks>
    public Guid? ContractTypeId { get; set; }

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
    // Payroll membership. Defaults to ON so every existing caller (imports, harness fixtures, the
    // hire path) keeps its meaning; a caller that says OFF must give a reason and must NOT send a
    // salary or any switch above — the service refuses rather than silently dropping them.
    public bool IsOnPayroll { get; set; } = true;
    public OffPayrollReason? OffPayrollReason { get; set; }
    public string? OffPayrollNote { get; set; }
    public string? BadgeNumber { get; set; }
    // ⚠ No PicturePath here, deliberately. It is the LEGACY caller-supplied file location and the
    // photo download still serves it, so accepting one on a write let a caller point a photo at
    // any file under the legacy roots. Photos are set through the gated upload endpoint.
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

    /// <summary>How the employee describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }

    /// <summary>Home town or place of origin.</summary>
    public string? Hometown { get; set; }

    /// <summary>
    /// ⚠ The EMPLOYEE's own disability, added alongside — never replacing — the one on
    /// EmployeeDependent. A dependant's disability and an employee's are different facts about
    /// different people; the feedback read as a misplacement and was not one.
    /// </summary>
    public bool HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    /// <summary>Round 3, lane P2: one of the tenant's live disability types. Only meaningful with HasDisability.</summary>
    public Guid? DisabilityTypeId { get; set; }
    public bool IsFullTime { get; set; }
    public DateOnly? DateEmployed { get; set; }

    // Contact Information
    public string? Address { get; set; }

    /// <summary>⚠ Overwritten by the resolved town or district when <see cref="GeoAreaId"/> is supplied.</summary>
    public string? City { get; set; }

    /// <summary>⚠ Overwritten by the resolved region when <see cref="GeoAreaId"/> is supplied.</summary>
    public string? State { get; set; }

    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }

    /// <summary>
    /// Where the employee lives. Supplying it also rewrites <see cref="State"/> and
    /// <see cref="City"/> from the tree.
    /// </summary>
    /// <remarks>
    /// ⚠ Following this DTO's convention, <c>null</c> means "not supplied" and leaves whatever the
    /// record already had — the same limitation <c>CountryId</c> has. **To remove an area, send
    /// <see cref="ClearGeoArea"/>**; a null on its own cannot mean both "leave it" and "clear it",
    /// and without the flag a user who emptied the region picker would watch the save do nothing.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    /// <summary>
    /// Removes the employee's area. Wins over <see cref="GeoAreaId"/> if both are sent. The
    /// snapshot columns are left as they are — the record still has to say where the person lives.
    /// </summary>
    public bool ClearGeoArea { get; set; }

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
    // Payroll membership. Omit to leave it alone; false requires a reason and clears the pay
    // figures; true clears the reason and (when no payroll profile exists yet) enrols the person.
    public bool? IsOnPayroll { get; set; }
    public OffPayrollReason? OffPayrollReason { get; set; }
    public string? OffPayrollNote { get; set; }
    public string? BadgeNumber { get; set; }
    // ⚠ No PicturePath here, deliberately. It is the LEGACY caller-supplied file location and the
    // photo download still serves it, so accepting one on a write let a caller point a photo at
    // any file under the legacy roots. Photos are set through the gated upload endpoint.
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
    /// <summary>True = paid through payroll only; false = off-payroll staff only.</summary>
    public bool? IsOnPayroll { get; set; }
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

    /// <summary>
    /// Where this address sits on the administrative-geography tree. Round 2, lane D2.
    /// </summary>
    /// <remarks>
    /// ⚠ When it is set, <c>City</c> and <c>Region</c> above are SNAPSHOTS the server wrote from
    /// the tree, not values a caller can decide. The form reads this to re-open the cascade.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }
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

    /// <summary>
    /// The area this address sits in. Supplying it rewrites <c>City</c> and <c>Region</c> from the
    /// tree, and fills in <c>CountryId</c> when none was stated.
    /// </summary>
    /// <remarks>
    /// ⚠ An area outside the stated country is REFUSED — the snapshots would otherwise contradict
    /// the country on the same row. Silence is not a contradiction: a record with an area and no
    /// country is given the area's country rather than being refused.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

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

    /// <summary>
    /// The area this address sits in. Supplying it rewrites <c>City</c> and <c>Region</c> from the
    /// tree, and fills in <c>CountryId</c> when none was stated.
    /// </summary>
    /// <remarks>
    /// ⚠ An area outside the stated country is REFUSED — the snapshots would otherwise contradict
    /// the country on the same row. Silence is not a contradiction: a record with an area and no
    /// country is given the area's country rather than being refused.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    /// <remarks>
    /// ⚠ On THIS DTO a null area CLEARS the link, because its address fields are already
    /// full-replace — <c>City</c>, <c>Region</c> and <c>AddressLine1</c> are all written straight
    /// from the payload. The guarantor and referee DTOs, whose fields mean "not supplied" when
    /// null, carry an explicit <c>ClearGeoArea</c> flag instead. The tab sends the whole form on
    /// every save either way.
    /// </remarks>
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
    public string? Region { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? GeoAreaId { get; set; }
    public string? DigitalAddress { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>The tie, from the relationship catalogue, where one was chosen (round 2, lane D2).</summary>
    /// <remarks>
    /// ⚠ <c>Relationship</c> above already carries the catalogue row's NAME — the service mirrors it
    /// on every save. This id is for re-opening the form's dropdown, not for display.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }
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

    /// <summary>
    /// The tie, as words. Ignored — and overwritten — when <see cref="RelationshipTypeId"/> names a
    /// catalogue row, so a caller that sends only the id still stores a readable relationship.
    /// </summary>
    /// <remarks>
    /// ⚠ Length stated since round 2 lane D2. It had none, while the column was 50 and the screen's
    /// schema allowed 100 — so a 51-character relationship reached SQL Server and failed as a
    /// truncation 500. The column is now 100 and this refuses anything longer with a 400.
    /// </remarks>
    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>The tie, from the tenant's relationship catalogue.</summary>
    /// <remarks>⚠ A next of kin accepts FAMILIAL and OTHER ties only; a professional one is refused.</remarks>
    public Guid? RelationshipTypeId { get; set; }

    public EmergencyContactType ContactType { get; set; }

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

    [MaxLength(100)]
    public string? Region { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>
    /// The area this address sits in. Supplying it rewrites <c>City</c> and <c>Region</c> from the
    /// tree, and fills in <c>CountryId</c> when none was stated; an area outside a stated country
    /// is refused.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
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

    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>
    /// The tie, from the catalogue. Null CLEARS the link, matching this DTO's other address
    /// fields, which are full-replace; the free-text <see cref="Relationship"/> then stands alone.
    /// </summary>
    public Guid? RelationshipTypeId { get; set; }

    public EmergencyContactType? ContactType { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(50)]
    public string? AlternatePhoneNumber { get; set; }

    [MaxLength(200)]
    public string? EmailAddress { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>The area. Null clears it — see <see cref="RelationshipTypeId"/>.</summary>
    public Guid? GeoAreaId { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public bool? IsPrimary { get; set; }
    public bool? IsActive { get; set; }

    [MaxLength(500)]
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


    /// <summary>How the dependant describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }
    public bool HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    /// <summary>Round 3, lane P2: the catalogue row, and its name for display.</summary>
    public Guid? DisabilityTypeId { get; set; }
    public string? DisabilityTypeName { get; set; }

    public string? GhanaCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? DigitalAddress { get; set; }
    public string? Occupation { get; set; }

    public bool IsEligibleForBenefits { get; set; }
    public bool IsDeceased { get; set; }

    public string? PicturePath { get; set; }
    public string? Notes { get; set; }
    // ── The photograph, through the gate ──────────────────────────────────────
    // ⚠ Read-side only. There is no way to SET these from a DTO: the image arrives through the
    // upload endpoint and the gate fills them in. `PicturePath` above is the LEGACY caller-supplied
    // location and is being retired — prefer `hasPhoto` for whether an image exists.
    public bool HasPhoto { get; set; }
    public string? PhotoFileName { get; set; }
    public string? PhotoMimeType { get; set; }
    public long? PhotoFileSizeBytes { get; set; }

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


    /// <summary>How the dependant describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }
    public bool HasDisability { get; set; }

    [MaxLength(500)]
    public string? DisabilityDescription { get; set; }
    /// <summary>Round 3, lane P2: one of the tenant's live disability types. Only meaningful with HasDisability.</summary>
    public Guid? DisabilityTypeId { get; set; }

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

    // ⚠ No PicturePath here, deliberately. It is the LEGACY caller-supplied file location and the
    // photo download still serves it, so accepting one on a write let a caller point a photo at
    // any file under the legacy roots. Photos are set through the gated upload endpoint.

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

    /// <summary>How the dependant describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }
    public bool? HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    /// <summary>Round 3, lane P2: one of the tenant's live disability types. Only meaningful with HasDisability.</summary>
    public Guid? DisabilityTypeId { get; set; }
    public string? GhanaCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? DigitalAddress { get; set; }
    public string? Occupation { get; set; }
    public bool? IsEligibleForBenefits { get; set; }
    public bool? IsDeceased { get; set; }
    // ⚠ No PicturePath here, deliberately. It is the LEGACY caller-supplied file location and the
    // photo download still serves it, so accepting one on a write let a caller point a photo at
    // any file under the legacy roots. Photos are set through the gated upload endpoint.
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

    /// <summary>
    /// Who certified the skill, as free text.
    /// </summary>
    /// <remarks>
    /// ⚠ Kept ALONGSIDE <see cref="CertifyingBodyId"/>, not replaced. Existing rows are free text
    /// and dropping the column would discard them, and a genuinely one-off certifier does not
    /// deserve a catalogue row. A screen should show the catalogued name where there is one and
    /// fall back to this.
    /// </remarks>
    public string? CertifyingBody { get; set; }

    /// <summary>The catalogued body that certified this skill, where there is one.</summary>
    public Guid? CertifyingBodyId { get; set; }

    /// <summary>Resolved name of that body — set, not declared and forgotten.</summary>
    public string? CertifyingBodyName { get; set; }

    public bool IsVerified { get; set; }
    public bool IsCertificationExpired { get; set; }
    public string? Notes { get; set; }

    /// <summary>The credential on the employee's certification tab that evidences this skill (round 2, lane C2).</summary>
    public Guid? EmployeeCertificationId { get; set; }
    public string? EmployeeCertificationName { get; set; }

    /// <summary>Whether the skill itself requires certification.</summary>
    public bool RequiresCertification { get; set; }

    /// <summary>
    /// False when the skill requires certification and nothing valid evidences it — a linked
    /// credential that is valid or expiring, or the legacy per-skill certification still in date.
    /// Recording is not gating: the row is allowed and flagged.
    /// </summary>
    public bool IsCompliant { get; set; }
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

    /// <summary>The catalogued certifier. Sits beside the free-text field rather than replacing it.</summary>
    public Guid? CertifyingBodyId { get; set; }

    /// <summary>Applied unconditionally, like the certifying body: null clears it.</summary>
    public Guid? EmployeeCertificationId { get; set; }

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

    /// <summary>The catalogued certifier. Sits beside the free-text field rather than replacing it.</summary>
    public Guid? CertifyingBodyId { get; set; }

    /// <summary>A credential the employee already holds that evidences this skill.</summary>
    public Guid? EmployeeCertificationId { get; set; }

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

    /// <summary>The tenant's own name for this kind of engagement, and its id.</summary>
    public Guid? ContractTypeId { get; set; }
    public string? ContractTypeName { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>The day these terms took effect. Ordering and supersession run on it.</summary>
    /// <remarks>
    /// ⚠ Reads <c>0001-01-01</c> on every row the manual tab added before lane D1 — the writer
    /// never set it. Rows created since carry the start date when nothing else was said.
    /// </remarks>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>When the engagement actually ended. Null while it is running.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>When the engagement is scheduled to end. Null for permanent employment.</summary>
    public DateOnly? ContractEndDate { get; set; }

    /// <summary>Whether these are the terms in force today. At most one per employee.</summary>
    public bool IsCurrent { get; set; }

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

    // Round 3, lane P3 (D-5): the three leave columns are gone; entitlement is the leave module's.

    /// <summary>The probation term, and the date it was passed.</summary>
    /// <remarks>
    /// ⚠ Both were SETTABLE on create and update and readable nowhere — measured 2026-09-01, the
    /// contract response carried neither. A probation term could be recorded and then never seen
    /// again, which is why nobody noticed it could also be rewritten after confirmation.
    /// Instrument 03 cannot see this class of gap: it scans Create/Update DTOs, not read ones.
    /// </remarks>
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }

    /// <summary>The currency the salary is expressed in. Defaults to GHS on the entity.</summary>
    /// <remarks>
    /// ⚠ A salary was exposed with NO currency at all, so every figure on every contract screen
    /// was implicitly GHS whether or not it was. Added 2026-09-01 (lane 3d).
    /// </remarks>
    public string CurrencyCode { get; set; } = "GHS";

    /// <summary>Full time, part time, shift, flexi, remote or hybrid.</summary>
    /// <remarks>
    /// ⚠ A different axis from <c>EmploymentType</c>, which says permanent vs contract. Both sit
    /// on the entity; only the latter was reachable.
    /// </remarks>
    public WorkArrangementType WorkSchedule { get; set; } = WorkArrangementType.FullTime;

    /// <summary>Conditions particular to this contract, longer-form than <c>Terms</c>.</summary>
    public string? SpecialConditions { get; set; }

    /// <summary>Internal remarks about the contract record itself.</summary>
    public string? Notes { get; set; }

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

    /// <summary>Which kind of engagement, from the tenant's contract-type list. Optional.</summary>
    /// <remarks>
    /// Naming one with no <see cref="ContractEndDate"/> supplied defaults the end date from the
    /// kind's duration; a kind whose duration is zero is open-ended and defaults nothing.
    /// </remarks>
    public Guid? ContractTypeId { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>The day these terms take effect. Defaults to <see cref="StartDate"/>.</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>When the engagement actually ended — normally left null on a new contract.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>When the engagement is scheduled to end. Null for permanent employment.</summary>
    public DateOnly? ContractEndDate { get; set; }

    public decimal Salary { get; set; }

    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;

    public TaxTreatmentType TaxTreatmentType { get; set; } = TaxTreatmentType.PAYE;

    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRate { get; set; }

    public bool IsPensionApplicable { get; set; } = true;
    public bool IsTaxExempt { get; set; } = false;

    public int WorkingHoursPerWeek { get; set; } = 40;

    // Round 3, lane P3 (D-5): no leave figures on a contract — they are the leave module's.
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }

    [MaxLength(10)]
    public string CurrencyCode { get; set; } = "GHS";

    public WorkArrangementType WorkSchedule { get; set; } = WorkArrangementType.FullTime;

    [MaxLength(2000)]
    public string? SpecialConditions { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

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

    /// <summary>The contract's reference. Correctable since lane D1.</summary>
    /// <remarks>
    /// ⚠ It had to become writable because the create now OPENS a contract and numbers it from the
    /// sequence — so a load carrying the organisation's own reference for that engagement has to be
    /// able to put it on the row that exists, rather than adding a second one to hold it.
    /// </remarks>
    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    public EmploymentType? EmploymentType { get; set; }

    /// <summary>Which kind of engagement, from the tenant's contract-type list.</summary>
    public Guid? ContractTypeId { get; set; }

    public DateOnly? StartDate { get; set; }

    /// <summary>The day these terms take effect.</summary>
    public DateOnly? EffectiveDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>When the engagement is scheduled to end. Null for permanent employment.</summary>
    /// <remarks>
    /// ⚠ Was on the entity and on no write DTO before lane D1, so the only rows that ever carried
    /// a scheduled end were the ones the hire-from-offer path wrote.
    /// </remarks>
    public DateOnly? ContractEndDate { get; set; }

    public decimal? Salary { get; set; }
    public PayFrequency? PayFrequency { get; set; }
    public TaxTreatmentType? TaxTreatmentType { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? WithholdingTaxRate { get; set; }

    public bool? IsPensionApplicable { get; set; }
    public bool? IsTaxExempt { get; set; }
    public int? WorkingHoursPerWeek { get; set; }

    // Round 3, lane P3 (D-5): no leave figures on a contract — they are the leave module's.
    public int? ProbationPeriodDays { get; set; }
    public DateOnly? ConfirmationDate { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public WorkArrangementType? WorkSchedule { get; set; }

    [MaxLength(2000)]
    public string? SpecialConditions { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
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

    /// <summary>Days before expiry that the holder is reminded. Null means no reminder.</summary>
    public int? ExpiryNotificationLeadDays { get; set; }
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

    /// <summary>
    /// Days before expiry that the holder is reminded. Null means this type raises no reminder.
    /// </summary>
    /// <remarks>
    /// Per TYPE, because the lead time belongs to the document: a Ghana Card renewal is not a
    /// passport renewal. Read by <c>IdentificationExpiryReminderBackgroundService</c>.
    /// </remarks>
    [Range(1, 365)]
    public int? ExpiryNotificationLeadDays { get; set; }

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

    /// <summary>Days before expiry that the holder is reminded. Null means no reminder.</summary>
    [Range(1, 365)]
    public int? ExpiryNotificationLeadDays { get; set; }

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

    // ── The employer's address, round 2 lane D2 (register row E-6) ──────────────────────────
    // ⚠ City and Region are SNAPSHOTS written from GeoAreaId, not values a caller decides.
    public Guid? CountryId { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public Guid? GeoAreaId { get; set; }

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

    /// <summary>
    /// The employer's street address, as one line.
    /// </summary>
    /// <remarks>
    /// ⚠ 200 — and the screen's schema said 300 until round 2 lane D2 (finding X-5), so a longer
    /// address passed the form and was refused here with a 400 the user could not have predicted.
    /// The structured part of the address is the country, area and city below.
    /// </remarks>
    [MaxLength(200)]
    public string? CompanyAddress { get; set; }

    /// <summary>The country the employer is in. Round 2, lane D2 (register row E-6).</summary>
    public Guid? CountryId { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    /// <summary>
    /// The area the employer sits in. Supplying it rewrites <c>City</c> and <c>Region</c> from the
    /// tree and fills in <c>CountryId</c>; an area outside a stated country is refused.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

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

/// <remarks>
/// ⚠ Every length here was ADDED in round 2 lane D2 (finding X-5). This DTO carried none, so where
/// the create refused an over-long value with a 400, the update let it through to SQL Server and
/// failed as a truncation 500 — the same payload, two different answers, neither of them the
/// screen's. The numbers match the create DTO and the columns exactly.
/// </remarks>
public class UpdateEmployeeWorkHistoryDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(200)]
    public string? CompanyAddress { get; set; }

    /// <summary>The country the employer is in. Round 2, lane D2 (register row E-6).</summary>
    public Guid? CountryId { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    /// <summary>
    /// The area. Null CLEARS the link, matching this DTO's other address fields, which are
    /// full-replace.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

    [MaxLength(100)]
    public string? JobTitle { get; set; }

    [MaxLength(1000)]
    public string? JobDescription { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? Salary { get; set; }

    [MaxLength(1000)]
    public string? ReasonForLeaving { get; set; }

    [MaxLength(200)]
    public string? SupervisorName { get; set; }

    [MaxLength(50)]
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
    public DateOnly? VisaIssueDate { get; set; }
    public DateOnly? VisaExpiryDate { get; set; }
    public string? WorkPermitNumber { get; set; }
    public DateOnly? WorkPermitIssueDate { get; set; }
    public DateOnly? WorkPermitExpiryDate { get; set; }

    /// <summary>The residence permit — a different instrument from the work permit.</summary>
    public string? ResidentPermitNumber { get; set; }
    public DateOnly? ResidentPermitIssueDate { get; set; }
    public DateOnly? ResidentPermitExpiryDate { get; set; }

    /// <summary>
    /// Who came with them. ⚠ On the DETAIL read only — the list read stays a summary, and a screen
    /// that needs the members must fetch the record rather than bind to a row.
    /// </summary>
    public List<ExpatriateFamilyMemberDto> FamilyMembers { get; set; } = new();
}

/// <summary>A family member accompanying an expatriate assignee.</summary>
public class ExpatriateFamilyMemberDto
{
    public Guid Id { get; set; }
    public Guid ExpatriateAssignmentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DependentRelationship Relationship { get; set; }
    public string? RelationshipDescription { get; set; }
    public Gender? Gender { get; set; }
    public string? GenderDescription { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiryDate { get; set; }
    public string? ResidentPermitNumber { get; set; }
    public DateOnly? ResidentPermitIssueDate { get; set; }
    public DateOnly? ResidentPermitExpiryDate { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string? Notes { get; set; }
}

public class CreateExpatriateFamilyMemberDto
{
    [Required]
    public Guid ExpatriateAssignmentId { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public DependentRelationship Relationship { get; set; }

    /// <summary>Used when Relationship is Other — the same pairing EmployeeDependent uses.</summary>
    [MaxLength(100)]
    public string? RelationshipDescription { get; set; }

    public Gender? Gender { get; set; }

    [MaxLength(100)]
    public string? GenderDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(100)]
    public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiryDate { get; set; }

    [MaxLength(100)]
    public string? ResidentPermitNumber { get; set; }
    public DateOnly? ResidentPermitIssueDate { get; set; }
    public DateOnly? ResidentPermitExpiryDate { get; set; }

    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateExpatriateFamilyMemberDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public DependentRelationship Relationship { get; set; }

    /// <summary>Used when Relationship is Other — the same pairing EmployeeDependent uses.</summary>
    [MaxLength(100)]
    public string? RelationshipDescription { get; set; }

    public Gender? Gender { get; set; }

    [MaxLength(100)]
    public string? GenderDescription { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(100)]
    public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiryDate { get; set; }

    [MaxLength(100)]
    public string? ResidentPermitNumber { get; set; }
    public DateOnly? ResidentPermitIssueDate { get; set; }
    public DateOnly? ResidentPermitExpiryDate { get; set; }

    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
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

    public DateOnly? VisaIssueDate { get; set; }
    public DateOnly? VisaExpiryDate { get; set; }

    [MaxLength(100)]
    public string? WorkPermitNumber { get; set; }

    public DateOnly? WorkPermitIssueDate { get; set; }
    public DateOnly? WorkPermitExpiryDate { get; set; }

    [MaxLength(100)]
    public string? ResidentPermitNumber { get; set; }
    public DateOnly? ResidentPermitIssueDate { get; set; }
    public DateOnly? ResidentPermitExpiryDate { get; set; }
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
    public DateOnly? VisaIssueDate { get; set; }
    public DateOnly? VisaExpiryDate { get; set; }
    public string? WorkPermitNumber { get; set; }
    public DateOnly? WorkPermitIssueDate { get; set; }
    public DateOnly? WorkPermitExpiryDate { get; set; }
    public string? ResidentPermitNumber { get; set; }
    public DateOnly? ResidentPermitIssueDate { get; set; }
    public DateOnly? ResidentPermitExpiryDate { get; set; }
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
/// <summary>
/// HR's payroll-membership statement for one employee, set beside payroll's own answer. The two
/// are different facts from different owners: HR says whether the person SHOULD be paid through
/// the run; payroll's profile says whether they ARE. This read puts both on one screen.
/// </summary>
/// <summary>
/// One payroll allowance/deduction component beside THIS employee's exception on it, if any
/// (round 3, lane X; decision D-3). The component's defaults are payroll's; the exception is the
/// per-person override payroll stores in <c>PayrollEmployeeComponents</c>. Read through HR's door;
/// written only through payroll's own bulk endpoint, one person's row at a time — the probe proved
/// that save touches only the lines it is sent.
/// </summary>
public class EmployeePayrollComponentRowDto
{
    public Guid PayrollComponentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollComponentType ComponentType { get; set; }
    public string ComponentTypeName => ComponentType.ToString();
    public PayrollCalculationType DefaultCalculationType { get; set; }
    public decimal DefaultAmount { get; set; }
    public decimal DefaultRate { get; set; }
    public bool DefaultTaxable { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    /// <summary>Payroll applies the component to everyone unless an exception says otherwise.</summary>
    public bool AppliesByDefault { get; set; }

    public bool HasException => ExceptionId.HasValue;
    public Guid? ExceptionId { get; set; }
    public PayrollCalculationType? CalculationType { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Rate { get; set; }
    public bool? Taxable { get; set; }
    public bool? Applicable { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>Every active payroll component with this employee's exception beside it (round 3, lane X).</summary>
public class EmployeePayrollComponentsDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    /// <summary>False when payroll has no profile yet — the rows still list the defaults, but nothing can be saved until it exists.</summary>
    public bool HasPayrollProfile { get; set; }
    public Guid? PayrollProfileId { get; set; }
    public List<EmployeePayrollComponentRowDto> Rows { get; set; } = new();
}

public class EmployeePayrollStatusDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public bool IsOnPayroll { get; set; }
    public OffPayrollReason? OffPayrollReason { get; set; }
    public string? OffPayrollNote { get; set; }

    /// <summary>Scale or negotiated — decides which figure <see cref="HrMonthlyBasicPay"/> is.</summary>
    public PayBasis PayBasis { get; set; }
    public string? PayBasisNote { get; set; }

    /// <summary>
    /// The HR-side basic pay. On the scale: the notch amount, else the level mid-point, else the flat
    /// figure on the record. Negotiated: payroll's active basis, else the flat figure.
    /// </summary>
    public decimal? HrMonthlyBasicPay { get; set; }

    /// <summary>Where <see cref="HrMonthlyBasicPay"/> came from, in words the tab can print beside it.</summary>
    public string? HrBasicPaySource { get; set; }

    public bool HasActiveSalaryAssignment { get; set; }

    /// <summary>Payroll's side, read from its employee profile. Null fields = no profile.</summary>
    public bool HasPayrollProfile { get; set; }
    public bool? PayrollActive { get; set; }
    public decimal? PayrollMonthlyBasicSalary { get; set; }
    public string? PayrollCurrencyCode { get; set; }

    /// <summary>One of the <see cref="PayrollReconciliationIssue"/> names, or null when consistent.</summary>
    public string? Issue { get; set; }
}

/// <summary>Where HR's statement and payroll's profile disagree, or where a statement has no basis.</summary>
public enum PayrollReconciliationIssue
{
    /// <summary>HR says on payroll; payroll has no profile for the person.</summary>
    AwaitingPayrollSetup = 1,
    /// <summary>HR says on payroll; payroll's profile is switched off.</summary>
    InactiveInPayroll = 2,
    /// <summary>HR says off payroll; payroll's profile is still active — the run will pay them.</summary>
    StillActiveInPayroll = 3,
    /// <summary>HR says on payroll but has neither a salary nor a graded notch — the run would skip them silently.</summary>
    NoPayBasis = 4,
    /// <summary>
    /// On the scale, placed on a notch, and payroll's active basis is a different amount. The run
    /// pays payroll's figure; HR's placement says another. Not raised for negotiated pay, where
    /// payroll's figure IS the basis.
    /// </summary>
    BasicPayMismatch = 5,
}

/// <summary>Body of <c>PUT api/hr/Employees/{id}/pay-basis</c>.</summary>
public class SetEmployeePayBasisDto
{
    public PayBasis PayBasis { get; set; }

    /// <summary>Required when negotiated: who agreed what, and when.</summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}

public class PayrollReconciliationRowDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public StaffStatus StaffStatus { get; set; }
    public bool IsOnPayroll { get; set; }
    public OffPayrollReason? OffPayrollReason { get; set; }
    public bool HasPayrollProfile { get; set; }
    public bool? PayrollActive { get; set; }
    public PayBasis PayBasis { get; set; }
    public decimal? HrMonthlyBasicPay { get; set; }
    /// <summary>Payroll's active basis, so a <see cref="PayrollReconciliationIssue.BasicPayMismatch"/> row shows both figures.</summary>
    public decimal? PayrollMonthlyBasicSalary { get; set; }
    public PayrollReconciliationIssue Issue { get; set; }
}

public class PayrollReconciliationDto
{
    public DateTime GeneratedAt { get; set; }
    public int OnPayrollCount { get; set; }
    public int OffPayrollCount { get; set; }
    public int AwaitingPayrollSetup { get; set; }
    public int InactiveInPayroll { get; set; }
    public int StillActiveInPayroll { get; set; }
    public int NoPayBasis { get; set; }
    public int BasicPayMismatch { get; set; }
    public List<PayrollReconciliationRowDto> Rows { get; set; } = new();
}

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

    /// <summary>In force TODAY: taken effect, not ended, not withdrawn.</summary>
    /// <remarks>
    /// ⚠ Was <c>EffectiveTo == null || EffectiveTo &gt;= today</c>, which never asked whether the
    /// placement had STARTED — so one dated next month read as active now — and knew nothing of
    /// withdrawal. Corrected in lane E1b.
    /// </remarks>
    public bool IsActive { get; set; }

    /// <summary>Takes effect in the future: dated forward, not withdrawn.</summary>
    public bool IsScheduled { get; set; }

    /// <summary>Withdrawn rather than superseded or run to its end. See the entity's remarks.</summary>
    public DateTime? WithdrawnAt { get; set; }
    public string? WithdrawnReason { get; set; }

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

    /// <summary>The tie, from the relationship catalogue, where one was chosen (round 2, lane D2).</summary>
    /// <remarks>
    /// ⚠ <c>Relationship</c> above already carries the row's NAME — the service mirrors it on every
    /// save. This id is for re-opening the form's dropdown, not for display.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// On the LIST projection, not only the detail: the tab renders a "Contacted" badge off the
    /// list row, and until round 2 the flag was absent from it, so the badge never showed.
    /// </summary>
    public bool IsContacted { get; set; }

    // ── The written reference ─────────────────────────────────────────────────
    // ⚠ Read-side only. There is no way to SET these from a DTO: the letter arrives through the
    // upload endpoint and the gate fills them in. `hasLetter` exists so a screen can show the
    // download affordance without having to reason about which of three ids means "present".
    //
    // On the LIST projection since demo feedback round 2 (lane A-4): the tab lists referees from
    // this shape and needs to show a paperclip per row without a detail read each.
    public bool HasLetter { get; set; }
    public string? LetterFileName { get; set; }
    public string? LetterMimeType { get; set; }
    public long? LetterFileSizeBytes { get; set; }
}

public class EmployeeRefereeDetailDto : EmployeeRefereeListDto
{
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

    /// <summary>
    /// The tie, as words. Overwritten when <see cref="RelationshipTypeId"/> names a catalogue row.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>The tie, from the tenant's relationship catalogue.</summary>
    /// <remarks>
    /// ⚠ Which values are accepted depends on <see cref="RefereeType"/>: a PERSONAL referee may be
    /// a relative or a family friend (familial, other); a PROFESSIONAL or ACADEMIC one may not
    /// (professional, other). The refusal names both the value's category and the kind of referee.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

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

    /// <summary>
    /// The tie, from the catalogue. Null means NOT SUPPLIED on this DTO, matching every other field
    /// on it; send <see cref="ClearRelationshipType"/> to unlink.
    /// </summary>
    public Guid? RelationshipTypeId { get; set; }

    /// <summary>
    /// Unlinks the catalogue row, leaving the free-text <see cref="Relationship"/> standing. Wins
    /// over <see cref="RelationshipTypeId"/> if both are sent — the <c>ClearNationalIdType</c> shape.
    /// </summary>
    public bool ClearRelationshipType { get; set; }
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

    // ── Round 2 (lane A-5/A-6): what the tab needs per ROW without a detail read each ────────
    /// <summary>Whether a photograph is on the row. Read-side only; it arrives through the gate.</summary>
    public bool HasPhoto { get; set; }
    /// <summary>How many documents pertain to this guarantor.</summary>
    public int DocumentCount { get; set; }
    /// <summary>The national ID kind from the catalogue, where one was chosen.</summary>
    public Guid? NationalIdTypeId { get; set; }
    public string? NationalIdTypeName { get; set; }

    /// <summary>The tie, from the relationship catalogue, where one was chosen (round 2, lane D2).</summary>
    /// <remarks>
    /// ⚠ <c>Relationship</c> above already carries the row's NAME — the service mirrors it on every
    /// save. This id is for re-opening the form's dropdown, not for display.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }
}

public class EmployeeGuarantorDetailDto : EmployeeGuarantorListDto
{
    public string? MiddleName { get; set; }
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;

    // ⚠ City and Region are SNAPSHOTS written from GeoAreaId, not values a caller decides.
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? GeoAreaId { get; set; }

    public string? JobTitle { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerAddress { get; set; }
    public string? EmployerPhone { get; set; }
    public decimal? MonthlyIncome { get; set; }

    /// <summary>⚠ What they stand surety FOR — distinct from MonthlyIncome, which is what they earn.</summary>
    public decimal? AmountGuaranteed { get; set; }

    /// <summary>The currency that amount is stated in. Null means HR's configured default.</summary>
    public string? AmountGuaranteedCurrencyCode { get; set; }

    /// <summary>How the guarantor describes their gender, where Gender is Other.</summary>
    public string? GenderDescription { get; set; }

    // The photograph. Read-side only; it arrives through the upload endpoint. `HasPhoto` itself
    // sits on the list projection since round 2.
    public string? PhotoFileName { get; set; }
    public string? PhotoMimeType { get; set; }
    public long? PhotoFileSizeBytes { get; set; }

    /// <summary>The free-text kind, for rows recorded before the catalogue link existed.</summary>
    public string? NationalIdType { get; set; }
    public string? NationalIdNumberMasked { get; set; }
    public DateOnly? NationalIdExpiryDate { get; set; }

    public bool HasSignedGuarantorForm { get; set; }
    public DateOnly? DateFormSigned { get; set; }
    /// <summary>LEGACY, read-only. The signed form is now a guarantor document through the gate.</summary>
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

    /// <summary>
    /// The tie, as words. Overwritten when <see cref="RelationshipTypeId"/> names a catalogue row.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>The tie, from the tenant's relationship catalogue.</summary>
    /// <remarks>
    /// ⚠ A guarantor accepts ALL THREE categories, unlike the referee and next-of-kin screens: an
    /// employer, a brother and a landlord can each stand surety, and refusing any of them would be
    /// inventing a rule the business does not have.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

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

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(50)]
    public string? DigitalAddress { get; set; }

    public Guid? CountryId { get; set; }

    /// <summary>
    /// The area this address sits in. Supplying it rewrites <c>City</c> and <c>Region</c> from the
    /// tree and fills in <c>CountryId</c>; an area outside a stated country is refused.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

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

    /// <summary>Free-text kind — accepted for rows whose kind is not in the catalogue; prefer the id.</summary>
    [MaxLength(50)]
    public string? NationalIdType { get; set; }

    /// <summary>The kind, from the tenant's identification-type catalogue. Refused if unknown or inactive.</summary>
    public Guid? NationalIdTypeId { get; set; }

    [MaxLength(100)]
    public string? NationalIdNumber { get; set; }

    public DateOnly? NationalIdExpiryDate { get; set; }

    public bool HasSignedGuarantorForm { get; set; }
    public DateOnly? DateFormSigned { get; set; }

    // ⚠ No GuarantorFormPath. Removed in round 2 (lane A-6): a caller-supplied file location on a
    // JSON body. The signed form is uploaded as a guarantor document through the gate.

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// What the guarantor undertakes to cover, in the tenant's default currency.
    /// </summary>
    /// <remarks>
    /// ⚠ The photograph is NOT here and must never be: it arrives through
    /// <c>POST api/hr/Employees/guarantors/{id}/photo</c> and the gate sets its identifiers. A
    /// file field on a JSON DTO is the sink D-10, D-14 and D-39 each had to remove.
    /// </remarks>
    public decimal? AmountGuaranteed { get; set; }

    /// <summary>
    /// The currency the surety is stated in — refused unless FINANCE holds it.
    /// </summary>
    /// <remarks>
    /// Null means the tenant's configured HR default, so existing rows keep meaning what they meant.
    /// </remarks>
    [MaxLength(3)]
    public string? AmountGuaranteedCurrencyCode { get; set; }

    /// <summary>How the guarantor describes their gender, where Gender is Other.</summary>
    [MaxLength(100)]
    public string? GenderDescription { get; set; }
}

public class UpdateEmployeeGuarantorDto
{
    [Required]
    public Guid Id { get; set; }

    public bool? IsPrimary { get; set; }
    public string? Relationship { get; set; }

    /// <summary>
    /// The tie, from the catalogue. Null means NOT SUPPLIED on this DTO; send
    /// <see cref="ClearRelationshipType"/> to unlink.
    /// </summary>
    public Guid? RelationshipTypeId { get; set; }

    /// <summary>Unlinks the catalogue row, leaving the free-text <see cref="Relationship"/> standing.</summary>
    public bool ClearRelationshipType { get; set; }

    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }

    /// <summary>
    /// The area. Null means NOT SUPPLIED on this DTO; send <see cref="ClearGeoArea"/> to unlink.
    /// </summary>
    public Guid? GeoAreaId { get; set; }

    /// <summary>
    /// Removes the guarantor's area. Wins over <see cref="GeoAreaId"/> if both are sent. The
    /// snapshot columns are left as they are — the record still has to say where they live.
    /// </summary>
    public bool ClearGeoArea { get; set; }

    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? JobTitle { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerAddress { get; set; }
    public string? EmployerPhone { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public string? NationalIdType { get; set; }
    /// <summary>The catalogue kind. Null means "not supplied"; send <see cref="ClearNationalIdType"/> to unlink.</summary>
    public Guid? NationalIdTypeId { get; set; }
    /// <summary>
    /// Nullable-means-not-supplied is the house convention, so emptying the picker has to say so
    /// explicitly or the save would succeed and change nothing — the `clearGeoArea` shape.
    /// </summary>
    public bool ClearNationalIdType { get; set; }
    public string? NationalIdNumber { get; set; }
    public DateOnly? NationalIdExpiryDate { get; set; }
    public bool? HasSignedGuarantorForm { get; set; }
    public DateOnly? DateFormSigned { get; set; }
    // ⚠ No GuarantorFormPath — see the create DTO.
    public bool? IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public Guid? VerifiedByEmployeeId { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastContactDate { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>
    /// What the guarantor undertakes to cover, in the tenant's default currency.
    /// </summary>
    /// <remarks>
    /// ⚠ The photograph is NOT here and must never be: it arrives through
    /// <c>POST api/hr/Employees/guarantors/{id}/photo</c> and the gate sets its identifiers. A
    /// file field on a JSON DTO is the sink D-10, D-14 and D-39 each had to remove.
    /// </remarks>
    public decimal? AmountGuaranteed { get; set; }

    /// <summary>
    /// The currency the surety is stated in — refused unless FINANCE holds it.
    /// </summary>
    /// <remarks>
    /// Null means the tenant's configured HR default, so existing rows keep meaning what they meant.
    /// </remarks>
    [MaxLength(3)]
    public string? AmountGuaranteedCurrencyCode { get; set; }

    /// <summary>How the guarantor describes their gender, where Gender is Other.</summary>
    [MaxLength(100)]
    public string? GenderDescription { get; set; }
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

    /// <summary>
    /// Unlinks the catalogue bank and branch so the typed names stand alone. Needed because a null
    /// id means "not supplied" on this DTO, the house convention.
    /// </summary>
    public bool ClearBankLink { get; set; }

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

    /// <summary>How much surety the post requires, where RequiresGuarantor is set.</summary>
    /// <remarks>⚠ Null with RequiresGuarantor true means "a guarantor, amount unspecified" — a
    /// legitimate state that the compliance read reports without judging the sum.</remarks>
    public decimal? RequiredGuarantorAmount { get; set; }

    /// <summary>Validated against Finance's currency master. Null means HR's default.</summary>
    [MaxLength(3)]
    public string? RequiredGuarantorCurrencyCode { get; set; }
    public bool RequiresLicense { get; set; }

    public bool IsActive { get; set; }

    public int EmployeeCount { get; set; }
    public List<PositionSkillRequirementDto> SkillRequirements { get; set; } = new();
    public List<EmployeePositionBenefitDto> PositionBenefits { get; set; } = new();

    /// <summary>What the post must hold (round 2, lane C2). Filled on the single read and the write responses.</summary>
    public List<PositionCertificationRequirementDto> CertificationRequirements { get; set; } = new();

    // ── Named sets (round 2, lane C3) ───────────────────────────────────────────────────────
    // What is ATTACHED. What the post actually requires is the union of these with the individual
    // collections above — the three effective reads, which is what every consumer now uses.
    public List<AttachedSetDto> BenefitGroups { get; set; } = new();
    public List<AttachedSetDto> SkillSets { get; set; } = new();
    public List<AttachedSetDto> CertificationSets { get; set; } = new();

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

    /// <summary>How much surety the post requires, where RequiresGuarantor is set.</summary>
    /// <remarks>⚠ Null with RequiresGuarantor true means "a guarantor, amount unspecified" — a
    /// legitimate state that the compliance read reports without judging the sum.</remarks>
    public decimal? RequiredGuarantorAmount { get; set; }

    /// <summary>Validated against Finance's currency master. Null means HR's default.</summary>
    [MaxLength(3)]
    public string? RequiredGuarantorCurrencyCode { get; set; }
    public bool RequiresLicense { get; set; } = false;

    public ICollection<CreatePositionSkillRequirementDto> SkillRequirements { get; set; } = new List<CreatePositionSkillRequirementDto>();
    public ICollection<CreateEmployeePositionBenefitDto> PositionBenefits { get; set; } = new List<CreateEmployeePositionBenefitDto>();

    /// <summary>
    /// The required credentials, as the whole set (round 2, lane C2). With RequiresCertification or
    /// RequiresLicense on, at least one — the switches say "some", these rows say which.
    /// </summary>
    public ICollection<CreatePositionCertificationRequirementDto> CertificationRequirements { get; set; } = new List<CreatePositionCertificationRequirementDto>();

    /// <summary>
    /// Named sets attached to this post (round 2, lane C3). ⚠ Sent as the COMPLETE set on every
    /// save, like the collections above: an omitted id is a detachment. Null means "leave as they
    /// are", which is how a caller that predates this lane keeps working.
    /// </summary>
    public ICollection<Guid>? BenefitGroupIds { get; set; }
    public ICollection<Guid>? SkillSetIds { get; set; }
    public ICollection<Guid>? CertificationSetIds { get; set; }


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

    /// <summary>How much surety the post requires, where RequiresGuarantor is set.</summary>
    /// <remarks>⚠ Null with RequiresGuarantor true means "a guarantor, amount unspecified" — a
    /// legitimate state that the compliance read reports without judging the sum.</remarks>
    public decimal? RequiredGuarantorAmount { get; set; }

    /// <summary>Validated against Finance's currency master. Null means HR's default.</summary>
    [MaxLength(3)]
    public string? RequiredGuarantorCurrencyCode { get; set; }
    public bool RequiresLicense { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public ICollection<CreatePositionSkillRequirementDto> SkillRequirements { get; set; } = new List<CreatePositionSkillRequirementDto>();
    public ICollection<CreateEmployeePositionBenefitDto> PositionBenefits { get; set; } = new List<CreateEmployeePositionBenefitDto>();

    /// <summary>
    /// The required credentials, as the whole set (round 2, lane C2). With RequiresCertification or
    /// RequiresLicense on, at least one — the switches say "some", these rows say which.
    /// </summary>
    public ICollection<CreatePositionCertificationRequirementDto> CertificationRequirements { get; set; } = new List<CreatePositionCertificationRequirementDto>();

    /// <summary>
    /// Named sets attached to this post (round 2, lane C3). ⚠ Sent as the COMPLETE set on every
    /// save, like the collections above: an omitted id is a detachment. Null means "leave as they
    /// are", which is how a caller that predates this lane keeps working.
    /// </summary>
    public ICollection<Guid>? BenefitGroupIds { get; set; }
    public ICollection<Guid>? SkillSetIds { get; set; }
    public ICollection<Guid>? CertificationSetIds { get; set; }


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
/// <summary>
/// A named set attached to a position — enough to name it and link to it, no members. Round 2,
/// lane C3.
/// </summary>
public class AttachedSetDto
{
    /// <summary>The attachment row's own id, so a screen can detach exactly this one.</summary>
    public Guid Id { get; set; }

    public Guid SetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
}

public class EmployeePositionBenefitDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? PositionAmount { get; set; }

    // ⚠ X-3 CLOSED (round 2, lane C3): there was an `IsActive` here with NO column behind it.
    // `EmployeePositionBenefit` has no such property; the mapper hard-coded `true` on every read,
    // so the field said "active" about rows that had no such state and about soft-deleted ones
    // alike. A screen that believed it would have shown a switch nothing could turn off. Removed
    // rather than backed with a column: the row's presence IS its activeness, and its absence is
    // the soft delete.
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

    /// <summary>The credentials that evidence this skill (round 2, lane C2). Filled on the single read.</summary>
    public List<SkillCertificationDto> Certifications { get; set; } = new();
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

    /// <summary>
    /// The accepted credentials, as the whole set. Null = the save did not carry them (leave as
    /// stored); empty = none. A skill that requires certification must name at least one.
    /// </summary>
    public ICollection<SkillCertificationInputDto>? Certifications { get; set; }
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

    /// <summary>Where this sits on the academic / professional ladder, when it sits on one.</summary>
    /// <remarks>
    /// ⚠ <see cref="Type"/> is a CATEGORY — Education, Certification, License, Membership — and
    /// cannot answer "is a Master's higher than a Diploma", which is what shortlisting and
    /// succession need. The level is the rank; the two are not substitutes.
    /// </remarks>
    public Guid? QualificationLevelId { get; set; }

    /// <summary>Resolved name of the rung, so a list does not need a second call to be readable.</summary>
    public string? QualificationLevelName { get; set; }

    /// <summary>The rung's rank, so a caller can order by ladder rather than by name.</summary>
    public int? QualificationLevelRank { get; set; }
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

    /// <summary>
    /// Which rung of the ladder this qualification sits on. Null means unranked.
    /// </summary>
    /// <remarks>
    /// Nullable on purpose: a membership or a short course has a kind but no level, and forcing one
    /// would invent a comparison the organisation does not actually make.
    /// </remarks>
    public Guid? QualificationLevelId { get; set; }

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
    public string? EmailAddress { get; set; }
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
