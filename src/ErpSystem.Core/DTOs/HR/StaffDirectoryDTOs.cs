namespace ErpSystem.Core.DTOs.HR;

#region Staff Directory DTOs (area 25 slice 13a)

/// <summary>
/// One person as the staff directory shows them: who they are, what they do, where they sit,
/// and how to reach them at work.
/// </summary>
/// <remarks>
/// <para><b>This DTO is deliberately not <see cref="EmployeeDto"/>.</b> The lean employee summary
/// is already open to any internal caller (W3 slice 11 took that decision for the shared
/// <c>EmployeePicker</c>), and it would have served here without a line of new code — but it
/// carries gender, staff status, employment type, full/part-time, expatriate status, hire date
/// and years of service. None of those are directory data. A picker showing them to the handful
/// of desk users who open one is a different thing from a browsable directory putting them in
/// front of 8,000 colleagues, so the portal projects its own columns rather than reusing a DTO
/// that happens to be reachable.</para>
///
/// <para><b>Work contact only.</b> <c>BusinessNumber</c> and <c>Extension</c> are the office
/// numbers; <c>MobileNumber</c> is the personal one the employee maintains themselves
/// (slice 12a), and it is not projected here. Measured on DEFAULT 2026-08-27: 8,131/8,131 have
/// an email address, 0 have a business number, 0 have an extension and 14 have a mobile — so
/// the phone columns render empty on this tenant today and the directory is an email directory
/// until TDC loads office numbers. That is an honest empty state, not a missing feature.</para>
/// </remarks>
public class StaffDirectoryEntryDto
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }

    public string PositionTitle { get; set; } = string.Empty;
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }

    public string? EmailAddress { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Extension { get; set; }

    public string? PicturePath { get; set; }

    /// <summary>True on the caller's own row, so the list can mark it rather than hide it.</summary>
    public bool IsSelf { get; set; }
}

/// <summary>
/// One person's directory card: their entry, who they report to, and who reports to them.
/// </summary>
/// <remarks>
/// Everything here is org data — the reporting line and the unit path — and nothing on it is
/// personal. It is emphatically not <c>employees/{id}/details</c>, which carries date of birth,
/// home address, SSNIT/TIN, salary and bank details and is gated on <c>HR.Employee.Read</c>.
/// </remarks>
public class StaffDirectoryProfileDto : StaffDirectoryEntryDto
{
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerPositionTitle { get; set; }

    /// <summary>Root-first unit path, e.g. ["Tema Development Corporation", "Finance", "Treasury"].</summary>
    public List<string> UnitPath { get; set; } = new();

    /// <summary>
    /// The people who report to this person. Sparse by construction: 416 of 8,131 live employees
    /// carry a <c>ManagerId</c> on DEFAULT (5.1%), so most cards show none. The org-authority
    /// model that would fix that is a deferred module, not a directory bug.
    /// </summary>
    public List<StaffDirectoryEntryDto> DirectReports { get; set; } = new();
}

/// <summary>The caller's own team: their manager above, their direct reports below.</summary>
/// <remarks>
/// The manager arm is what makes this useful to the ~95% of staff who manage nobody — an empty
/// team page that still tells you who you report to has said something.
/// </remarks>
public class MyTeamDto
{
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerPositionTitle { get; set; }
    public string? ManagerEmailAddress { get; set; }

    public List<StaffDirectoryEntryDto> DirectReports { get; set; } = new();

    /// <summary>Convenience for the empty state — the client should not have to infer it.</summary>
    public bool ManagesAnyone => DirectReports.Count > 0;
}

#endregion
