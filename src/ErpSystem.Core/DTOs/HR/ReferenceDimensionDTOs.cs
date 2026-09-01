using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ═════════════════════════════════════════════════════════════════════════════
//  Lane 3b — reference data that should be a dimension.
//
//  Three lookups that were previously free text, an enum doing the wrong job, or
//  a format compiled into a repository.
// ═════════════════════════════════════════════════════════════════════════════

#region QualificationLevel

/// <summary>A rung on the academic / professional ladder.</summary>
public class QualificationLevelDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }

    /// <summary>Ascending. Higher is more advanced; this is what makes the ladder comparable.</summary>
    public int Rank { get; set; }

    public bool IsActive { get; set; }

    /// <summary>How many qualifications sit on this rung — so a screen can warn before retiring it.</summary>
    public int QualificationCount { get; set; }
}

public class CreateQualificationLevelDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Ascending order. Higher means more advanced.
    /// </summary>
    /// <remarks>
    /// ⚠ Ties are allowed on purpose — a Higher National Diploma and a Bachelor's degree may be
    /// treated as equivalent, and forcing a strict order would make the system assert a ranking the
    /// organisation does not hold.
    /// </remarks>
    [Range(0, 1000)]
    public int Rank { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateQualificationLevelDto : CreateQualificationLevelDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion

#region CertifyingBody

/// <summary>An organisation that certifies a skill.</summary>
public class CertifyingBodyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Abbreviation { get; set; }
    public string? Description { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCertifyingBodyDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short form — "ICAG", "CIPS". What people actually write.</summary>
    [MaxLength(50)]
    public string? Abbreviation { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? CountryId { get; set; }

    [MaxLength(255)]
    public string? Website { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateCertifyingBodyDto : CreateCertifyingBodyDto
{
    [Required]
    public Guid Id { get; set; }
}

#endregion

#region StaffNumberFormat

/// <summary>How one register of employees is numbered.</summary>
public class StaffNumberFormatDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;

    public EmploymentType? AppliesToEmploymentType { get; set; }

    /// <summary>Resolved name of the register, or "All other staff" for the default rule.</summary>
    public string AppliesToName { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;
    public string Separator { get; set; } = string.Empty;
    public bool IncludeYear { get; set; }
    public int YearDigits { get; set; }
    public int SequenceDigits { get; set; }
    public string Suffix { get; set; } = string.Empty;
    public bool AutoGenerate { get; set; }
    public string SequenceKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>
    /// What this rule produces for counter 1 in the current year.
    /// </summary>
    /// <remarks>
    /// ⚠ Returned by the server rather than recomposed in TypeScript, so the preview a user sees is
    /// produced by the same code that will issue the number. A format nobody can preview is a
    /// format that gets discovered in production; a preview built by a second implementation is a
    /// format that gets discovered to be wrong.
    /// </remarks>
    public string Example { get; set; } = string.Empty;
}

public class CreateStaffNumberFormatDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Which register this numbers. Null is the tenant's default rule.</summary>
    public EmploymentType? AppliesToEmploymentType { get; set; }

    [MaxLength(10)]
    public string Prefix { get; set; } = "";

    [MaxLength(3)]
    public string Separator { get; set; } = "";

    /// <summary>Prints the year, and therefore resets the counter each year. One decision, not two.</summary>
    public bool IncludeYear { get; set; }

    [Range(2, 4)]
    public int YearDigits { get; set; } = 4;

    [Range(1, 12)]
    public int SequenceDigits { get; set; } = 4;

    [MaxLength(10)]
    public string Suffix { get; set; } = "";

    /// <summary>
    /// Whether the system issues numbers for this register, or HR types them.
    /// </summary>
    /// <remarks>
    /// There is no global auto/manual switch: the ABSENCE of a rule means manual. This flag decides
    /// only what happens for a register that HAS a rule.
    /// </remarks>
    public bool AutoGenerate { get; set; } = true;

    [Required]
    [MaxLength(30)]
    public string SequenceKey { get; set; } = "EMP";

    public bool IsActive { get; set; } = true;
}

public class UpdateStaffNumberFormatDto : CreateStaffNumberFormatDto
{
    [Required]
    public Guid Id { get; set; }
}

/// <summary>A preview request, so a settings screen can show output before the rule is saved.</summary>
public class PreviewStaffNumberFormatDto
{
    [MaxLength(10)]
    public string Prefix { get; set; } = "";

    [MaxLength(3)]
    public string Separator { get; set; } = "";

    public bool IncludeYear { get; set; }

    [Range(2, 4)]
    public int YearDigits { get; set; } = 4;

    [Range(1, 12)]
    public int SequenceDigits { get; set; } = 4;

    [MaxLength(10)]
    public string Suffix { get; set; } = "";
}

#endregion
