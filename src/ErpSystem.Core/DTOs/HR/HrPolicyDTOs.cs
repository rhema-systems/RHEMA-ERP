using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// POLICY LIBRARY + ACKNOWLEDGEMENTS — area 25 slice 12d (decision D7)
// ============================================================================

public sealed class HrPolicyAudienceDto
{
    public Guid? Id { get; set; }

    [Required]
    public HrAudienceTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }
    public bool IsExclusion { get; set; }
    public string? TargetName { get; set; }
}

public class SaveHrPolicyDto
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Summary { get; set; }

    public HrPolicyCategory Category { get; set; } = HrPolicyCategory.General;

    [MaxLength(30)]
    public string? VersionLabel { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? ReviewOn { get; set; }

    public bool RequiresAcknowledgement { get; set; }

    /// <summary>Required when <see cref="RequiresAcknowledgement"/> is true — an employee cannot
    /// agree to nothing.</summary>
    [MaxLength(4000)]
    public string? AcknowledgementText { get; set; }

    public int? AcknowledgementDueDays { get; set; }

    /// <summary>The version this replaces, when it replaces one.</summary>
    public Guid? SupersedesPolicyId { get; set; }

    public List<HrPolicyAudienceDto> Audiences { get; set; } = [];
}

public sealed class HrPolicyDto
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }

    public HrPolicyCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    public string? VersionLabel { get; set; }

    public HrPolicyStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public bool IsLive { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? ReviewOn { get; set; }

    public bool RequiresAcknowledgement { get; set; }
    public string? AcknowledgementText { get; set; }
    public int? AcknowledgementDueDays { get; set; }

    public Guid? SupersedesPolicyId { get; set; }
    public string? SupersedesTitle { get; set; }

    public DateTime? PublishedAt { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedByName { get; set; }

    public bool HasDocument { get; set; }
    public string? FileName { get; set; }

    public IEnumerable<HrPolicyAudienceDto> Audiences { get; set; } = [];

    /// <summary>How many employees it applies to, resolved from the rules. Desk-side only.</summary>
    public int? AudienceCount { get; set; }

    /// <summary>How many of them have signed. Null when the policy needs no acknowledgement.</summary>
    public int? SignedCount { get; set; }

    /// <summary>How many have refused, which is a number somebody should look at.</summary>
    public int? DeclinedCount { get; set; }
}

/// <summary>The employee's view of a policy that applies to them.</summary>
public sealed class MyPolicyDto
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }

    public HrPolicyCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    public string? VersionLabel { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? PublishedAt { get; set; }

    public bool HasDocument { get; set; }
    public string? FileName { get; set; }

    public bool RequiresAcknowledgement { get; set; }
    public string? AcknowledgementText { get; set; }

    /// <summary>Null while they have neither signed nor declined — the outstanding case.</summary>
    public HrPolicyAcknowledgementOutcome? MyOutcome { get; set; }
    public string? MyOutcomeName => MyOutcome?.ToString();

    public DateTime? MySignedAt { get; set; }
    public DateTime? MyDeclinedAt { get; set; }
    public string? MyDeclineReason { get; set; }

    /// <summary>When it is due, if the policy states a window. Past means overdue.</summary>
    public DateTime? AcknowledgementDueBy { get; set; }

    /// <summary>True when this policy is still waiting on the caller.</summary>
    public bool IsOutstandingForMe { get; set; }
}

public sealed class AcknowledgePolicyDto
{
    /// <summary>
    /// Must match the policy's current declaration. The client echoes back what it displayed, so
    /// somebody cannot sign a wording that changed between the page loading and the click.
    /// </summary>
    [Required]
    public string AcknowledgementText { get; set; } = string.Empty;
}

public sealed class DeclinePolicyDto
{
    /// <summary>Required. A refusal without a reason gives nobody anything to act on.</summary>
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>One line of the compliance roster.</summary>
public sealed class PolicyComplianceRowDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public string? PositionTitle { get; set; }

    /// <summary>Null means outstanding — they have neither signed nor refused.</summary>
    public HrPolicyAcknowledgementOutcome? Outcome { get; set; }
    public string? OutcomeName => Outcome?.ToString();

    public DateTime? SignedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }
    public string? DeclineReason { get; set; }
}

/// <summary>Which slice of the roster to return. Omitted means everyone in the audience.</summary>
public enum PolicyComplianceFilter
{
    All = 0,
    Outstanding = 1,
    Signed = 2,
    Declined = 3
}

/// <summary>
/// Who has and has not acknowledged a policy — the report nothing in the codebase had before.
/// </summary>
/// <remarks>
/// <para>Computed, never stored: the audience is resolved at request time and left-joined to the
/// acknowledgements, so somebody who joined after publication appears as outstanding and a
/// leaver drops off, without anybody re-publishing anything.</para>
///
/// <para><b>The counts are always complete; the ROWS are a page.</b> Measured on the live tenant,
/// a policy addressed to everybody has an audience of ~7,900, and returning every row made a
/// 2.3 MB response taking 1.2 seconds — for a screen whose first question is almost always
/// "who is outstanding?". So the four totals are computed over the whole audience and the rows
/// are filtered and paged, which also means employee names are only fetched for the page.</para>
/// </remarks>
public sealed class PolicyComplianceDto
{
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? VersionLabel { get; set; }

    // ── Always over the WHOLE audience, regardless of the filter or page ─────
    public int AudienceCount { get; set; }
    public int SignedCount { get; set; }
    public int DeclinedCount { get; set; }
    public int OutstandingCount { get; set; }

    /// <summary>Signed as a percentage of the audience, 0 when it applies to nobody.</summary>
    public decimal CompliancePercent { get; set; }

    // ── The page ─────────────────────────────────────────────────────────────
    public PolicyComplianceFilter Filter { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>How many rows match the filter — what the paging runs over.</summary>
    public int TotalRows { get; set; }

    public IEnumerable<PolicyComplianceRowDto> Rows { get; set; } = [];
}
