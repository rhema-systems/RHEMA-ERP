using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

#region Probation Confirming Authority

/// <summary>One rule in the confirming-authority map (FR-HR-032, decision D-2).</summary>
public class ProbationConfirmingAuthorityDto
{
    public Guid Id { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }
    public Guid AuthorityEmployeeId { get; set; }
    public string AuthorityEmployeeName { get; set; } = string.Empty;
    public string AuthorityEmployeeNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// How narrow this rule is: 3 = unit + level, 2 = unit, 1 = level, 0 = tenant default.
    /// Resolution takes the highest that matches.
    /// </summary>
    public int Specificity { get; set; }

    /// <summary>Plain-language scope, e.g. "Estates — Senior Staff" or "All units, all levels".</summary>
    public string Scope { get; set; } = string.Empty;
}

public class CreateProbationConfirmingAuthorityDto
{
    public Guid? OrganizationUnitId { get; set; }
    public Guid? StaffLevelId { get; set; }

    [Required]
    public Guid AuthorityEmployeeId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateProbationConfirmingAuthorityDto
{
    [Required]
    public Guid AuthorityEmployeeId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>Who confirms this employee's probation, and which rule decided it.</summary>
public class ResolvedConfirmingAuthorityDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Null when no rule matches — the honest answer, not a silent fallback to HR.</summary>
    public Guid? AuthorityEmployeeId { get; set; }
    public string? AuthorityEmployeeName { get; set; }

    public Guid? MatchedRuleId { get; set; }
    public string? MatchedScope { get; set; }
    public int? MatchedSpecificity { get; set; }

    public bool IsResolved => AuthorityEmployeeId.HasValue;

    /// <summary>
    /// What to do when it is not resolved. Present so a screen shows guidance rather than a blank.
    /// </summary>
    public string? UnresolvedReason { get; set; }
}

#endregion
