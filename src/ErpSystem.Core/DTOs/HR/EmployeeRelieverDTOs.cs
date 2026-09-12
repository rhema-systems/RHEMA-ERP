using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// One pre-defined reliever: who covers for an employee, and in what order.
/// </summary>
/// <remarks>
/// ⚠ Made to inherit <see cref="BaseDto"/> by slice 11's content audit. It was the only DTO in
/// areas 19–23 that did not, so it carried no <c>createdAt</c>, <c>createdBy</c>, <c>updatedAt</c>
/// or <c>updatedBy</c> — and once slice 11 fixed the services to stamp the author, the reliever
/// roster was the one store stamping a value that no caller could ever see. A field written for
/// nobody is the same defect as a field never written; the audit only caught it because it
/// asserted the payload rather than the row.
/// </remarks>
public class EmployeeRelieverDto : BaseDto
{
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Whose cover this is.
    /// </summary>
    /// <remarks>
    /// Added in areas 19-23 slice 7. The DTO named only the reliever, which is enough for the
    /// employee's own roster tab and useless to anything that lists rosters across people — the
    /// row would say who is covering without saying for whom.
    /// </remarks>
    public string EmployeeName { get; set; } = string.Empty;

    public Guid RelieverEmployeeId { get; set; }
    public string RelieverName { get; set; } = string.Empty;
    public string? RelieverPositionName { get; set; }
    public string? RelieverOrganizationUnitName { get; set; }

    /// <summary>1 = primary, 2 = backup, and so on. Drives which leave slot it fills.</summary>
    public int Priority { get; set; }

    public bool IsActive { get; set; }
}

public class CreateEmployeeRelieverDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid RelieverEmployeeId { get; set; }

    /// <remarks>
    /// ⚠ Ordinal, so it starts at 1. Slice 7's probe created priority <c>0</c> and priority
    /// <c>-1</c> rows without complaint: nothing validated this, and the leave form reads the
    /// roster in priority order to fill the first and second reliever slots, so a zero or a
    /// negative silently jumps the queue.
    /// </remarks>
    [Range(1, 99, ErrorMessage = "Priority must be 1 or greater — 1 is the primary reliever.")]
    public int Priority { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeRelieverDto
{
    [Required]
    public Guid RelieverEmployeeId { get; set; }

    [Range(1, 99, ErrorMessage = "Priority must be 1 or greater — 1 is the primary reliever.")]
    public int Priority { get; set; }

    public bool IsActive { get; set; }
}
