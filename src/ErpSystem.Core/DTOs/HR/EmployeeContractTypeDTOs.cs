using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// A kind of engagement in the tenant's own vocabulary — the list an appointment letter picks from.
/// </summary>
/// <remarks>
/// <para>⚠ <b>Distinct from the <c>EmploymentType</c> enum.</b> The enum is the system's fixed set
/// (Permanent, Contract, FixedTerm, Internship, Casual, PartTime, Temporary, Consultant,
/// Freelance) and code branches on it — the probation rule is keyed to <c>Permanent</c>, the
/// staff-number register is chosen by it. This is the organisation's own naming, and it carries
/// the <see cref="Duration"/> that gives a fixed-term contract's end date a default.</para>
///
/// <para>The table was seeded with seven TDC rows and then had no DTO, no service, no controller
/// and no screen — surfaced in lane D1 (round-2 question Q-4). See
/// docs/HR/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 9.</para>
/// </remarks>
public class EmployeeContractTypeDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>How long this kind of engagement normally runs, in MONTHS. Zero means open-ended.</summary>
    public int Duration { get; set; }

    public bool IsActive { get; set; }

    /// <summary>How many contract rows name this kind — so a retire decision can see its reach.</summary>
    public int ContractCount { get; set; }
}

public class CreateEmployeeContractTypeDto
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Months. Zero means open-ended — a permanent appointment has no scheduled end.</summary>
    [Range(0, 600)]
    public int Duration { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeContractTypeDto
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 600)]
    public int Duration { get; set; }

    public bool IsActive { get; set; } = true;
}
