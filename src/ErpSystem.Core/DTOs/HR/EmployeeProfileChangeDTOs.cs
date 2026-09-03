using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// MY PROFILE + PERSONAL-DATA CHANGE REQUESTS — area 25 slice 12 (decision D6)
// ============================================================================

/// <summary>
/// The employee's own profile, as the portal shows it.
/// </summary>
/// <remarks>
/// <b>A deliberate projection, not <c>EmployeeFullProfileDto</c>.</b> That DTO carries
/// <c>Salary</c>, <c>SalaryAssignments</c>, HR's private <c>Notes</c> and
/// <c>TerminationNotes</c> — fields written ABOUT an employee rather than fields belonging to
/// them. Handing an employee the desk's own read would leak HR commentary about them, so this
/// projection lists what it shows and nothing else (the module's self-service law: an explicit
/// projection, never a reused desk DTO).
/// <para>Each field carries how it may be changed, so one read tells the screen whether to
/// render an input, a "request a change" link, or plain text — the client never hard-codes the
/// risk classification.</para>
/// </remarks>
public sealed class MyProfileDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;

    // ── Identity (change request) ─────────────────────────────────────────
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public string? Religion { get; set; }
    public BloodType? BloodType { get; set; }
    public string? PicturePath { get; set; }

    // ── Contact: the direct-edit set, plus the address block (change request) ──
    public string? EmailAddress { get; set; }
    public string? MobileNumber { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Extension { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? DigitalAddress { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }

    // ── Statutory (change request) ────────────────────────────────────────
    public string? SocialSecurityNumber { get; set; }
    public string? TINNumber { get; set; }
    public string? TaxNumber { get; set; }

    // ── Employment: read-only, always. HR and payroll own every one of these. ──
    public string? PositionTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? SectionName { get; set; }
    public string? OrganizationUnitName { get; set; }
    public string? LocationName { get; set; }
    public string? ManagerName { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public StaffStatus StaffStatus { get; set; }
    public DateOnly? DateEmployed { get; set; }
    public DateOnly? ConfirmationDate { get; set; }

    /// <summary>Null when no hire date is recorded — "unknown" and "none" are different facts,
    /// and a zero here would tell the employee they joined today.</summary>
    public int? YearsOfService { get; set; }

    // ── The employee's own people and accounts ────────────────────────────
    public IEnumerable<EmployeeEmergencyContactDto> EmergencyContacts { get; set; } = [];
    public IEnumerable<EmployeeDependentDto> Dependents { get; set; } = [];
    public IEnumerable<EmployeeBankDetailDto> BankDetails { get; set; } = [];

    /// <summary>Change requests of theirs still waiting on HR — so the screen can say
    /// "a change to this field is already pending" instead of inviting a duplicate.</summary>
    public IEnumerable<EmployeeProfileField> FieldsWithPendingRequests { get; set; } = [];
}

/// <summary>
/// The fields an employee edits directly, with no approval.
/// </summary>
/// <remarks>
/// Every field here is a contact detail the employee is the sole authority on and which
/// carries no payment or identity consequence. Null means "leave alone"; sending an empty
/// string clears the field (the two are deliberately different — a person genuinely may have
/// no desk extension any more).
/// </remarks>
public sealed class UpdateMyContactDetailsDto
{
    [MaxLength(50)]
    public string? MobileNumber { get; set; }

    [MaxLength(50)]
    public string? TelephoneNumber { get; set; }

    [MaxLength(50)]
    public string? BusinessNumber { get; set; }

    [MaxLength(20)]
    public string? Extension { get; set; }

    [MaxLength(50)]
    public string? Religion { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }
}

/// <summary>One field the employee wants changed.</summary>
public sealed class ProfileChangeItemInputDto
{
    [Required]
    public EmployeeProfileField Field { get; set; }

    /// <summary>The requested value. Parsed according to the field; an unparseable value is
    /// refused at filing time rather than silently defaulted.</summary>
    [Required]
    [MaxLength(500)]
    public string NewValue { get; set; } = string.Empty;
}

/// <summary>A request to change identity-, address-, statutory- or payment-bearing data.</summary>
public sealed class CreateProfileChangeRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Required when any bank field is included: which of the employee's accounts is
    /// being corrected. It must be one of theirs.</summary>
    public Guid? BankDetailId { get; set; }

    [Required]
    [MinLength(1)]
    public List<ProfileChangeItemInputDto> Items { get; set; } = [];
}

/// <summary>HR's answer. A rejection must carry a comment — the employee reads it back.</summary>
public sealed class ReviewProfileChangeRequestDto
{
    [MaxLength(1000)]
    public string? Comments { get; set; }
}

/// <summary>
/// One field's before/after within a request.
/// </summary>
/// <remarks>
/// ⚠ <b>These values are NOT masked, deliberately</b> — including a bank account number, which
/// the bank-details list redacts. The two contexts differ: that list is a roster somebody
/// scrolls, whereas this is the substance of a request seen only by the employee who filed it
/// and the HR officer approving it, who has to read the new number against the bank letter to
/// approve at all. Masking here would not harden anything; it would break the approval.
/// </remarks>
public sealed class ProfileChangeItemDto
{
    public Guid Id { get; set; }
    public EmployeeProfileField Field { get; set; }
    public string FieldName => Field.ToString();

    /// <summary>A human label for the field — the screen never has to keep its own map.</summary>
    public string FieldLabel { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public string? AppliedValue { get; set; }
}

public sealed class ProfileChangeRequestDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public ProfileChangeRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime SubmittedAt { get; set; }
    public string Reason { get; set; } = string.Empty;

    public Guid? BankDetailId { get; set; }
    /// <summary>The account being corrected, masked to its last four digits — enough to tell
    /// two accounts apart without reprinting the number on every queue row.</summary>
    public string? BankAccountMasked { get; set; }

    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComments { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public bool HasEvidence { get; set; }
    public string? EvidenceFileName { get; set; }

    public IEnumerable<ProfileChangeItemDto> Items { get; set; } = [];
}
