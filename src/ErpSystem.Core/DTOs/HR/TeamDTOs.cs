using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Team DTOs

/// <summary>
/// A working group — permanent, project, task force, cross-functional, shift or committee.
/// </summary>
/// <remarks>
/// Distinct from <see cref="OrganizationUnitDto"/>, which is the formal hierarchy an employee is
/// posted into. A team is who actually works together, which is why membership carries an
/// allocation percentage: in a matrix organisation a person's team is not their unit, and they may
/// be in several at once.
/// </remarks>
public class TeamDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public TeamType TeamType { get; set; }
    public TeamStatus Status { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    public Guid? TeamLeadId { get; set; }
    public string? TeamLeadName { get; set; }

    public Guid? ParentTeamId { get; set; }
    public string? ParentTeamName { get; set; }

    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }

    public Guid? ShiftId { get; set; }
    public string? ShiftName { get; set; }

    public string? CostCenterCode { get; set; }
    public string? ProjectCode { get; set; }
    public string? TeamEmail { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public int? MaxMembers { get; set; }
    public int Sequence { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Members whose membership is active and not yet ended. Always populated — the entity's
    /// <c>MemberCount</c> is <c>[NotMapped]</c> precisely so a service fills it, and a team list
    /// that cannot say how many people are in each team is a list of labels.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>Direct sub-teams. Lets a list show which teams are parents without a second call.</summary>
    public int ChildTeamCount { get; set; }

    /// <summary>True once <see cref="EffectiveTo"/> is in the past — the team has run its course.</summary>
    public bool HasLapsed { get; set; }
}

/// <summary>
/// A team with its membership loaded. The detail read.
/// </summary>
public class TeamDetailDto : TeamDto
{
    public List<TeamMemberDto> Members { get; set; } = new();
}

/// <summary>Lightweight shape for pickers and parent-team selectors.</summary>
public class TeamSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public TeamType TeamType { get; set; }
    public TeamStatus Status { get; set; }
    public int MemberCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTeamDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public TeamType TeamType { get; set; } = TeamType.Permanent;

    public TeamStatus Status { get; set; } = TeamStatus.Draft;

    public Guid? OrganizationUnitId { get; set; }
    public Guid? TeamLeadId { get; set; }
    public Guid? ParentTeamId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ShiftId { get; set; }

    [MaxLength(100)]
    public string? CostCenterCode { get; set; }

    [MaxLength(50)]
    public string? ProjectCode { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? TeamEmail { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Range(1, 10000)]
    public int? MaxMembers { get; set; }

    [Range(1, int.MaxValue)]
    public int Sequence { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateTeamDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public TeamType TeamType { get; set; }
    public TeamStatus Status { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? TeamLeadId { get; set; }
    public Guid? ParentTeamId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ShiftId { get; set; }

    [MaxLength(100)]
    public string? CostCenterCode { get; set; }

    [MaxLength(50)]
    public string? ProjectCode { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? TeamEmail { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    [Range(1, 10000)]
    public int? MaxMembers { get; set; }

    [Range(1, int.MaxValue)]
    public int Sequence { get; set; } = 1;

    public bool IsActive { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Team membership DTOs

public class TeamMemberDto : BaseDto
{
    public Guid TenantId { get; set; }

    public Guid TeamId { get; set; }
    public string? TeamName { get; set; }

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    /// <summary>The employee's substantive post, so a team roster reads without a second lookup.</summary>
    public string? PositionTitle { get; set; }

    public TeamMemberRole Role { get; set; }

    /// <summary>Percentage of the person's time on this team. Matters when they are on several.</summary>
    public decimal AllocationPercent { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly JoinDate { get; set; }

    /// <summary>Null while the person is still on the team.</summary>
    public DateOnly? LeaveDate { get; set; }

    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// True when the membership is live: active, not soft-deleted, and not yet ended. This is the
    /// definition <c>MemberCount</c> and the organogram both count on, stated once here rather than
    /// re-derived by each caller.
    /// </summary>
    public bool IsCurrent { get; set; }
}

public class AddTeamMemberDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

    [Range(0, 100)]
    public decimal AllocationPercent { get; set; } = 100m;

    public bool IsPrimary { get; set; }

    public DateOnly JoinDate { get; set; }

    public DateOnly? LeaveDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateTeamMemberDto
{
    public TeamMemberRole Role { get; set; }

    [Range(0, 100)]
    public decimal AllocationPercent { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly JoinDate { get; set; }

    public DateOnly? LeaveDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Why the role changed. Written to the membership history when — and only when — the role
    /// actually moves, on the same reasoning as slice 3's audit trail: a log that records every
    /// keystroke is one nobody reads.
    /// </summary>
    [MaxLength(500)]
    public string? ChangeReason { get; set; }
}

/// <summary>Ends a membership without deleting the record, so the roster keeps its history.</summary>
public class RemoveTeamMemberDto
{
    public DateOnly? LeaveDate { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class TeamMemberHistoryDto : BaseDto
{
    public Guid TenantId { get; set; }

    public Guid TeamId { get; set; }
    public string? TeamName { get; set; }

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public TeamMemberRole PreviousRole { get; set; }
    public TeamMemberRole NewRole { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public string? ChangeReason { get; set; }
}

#endregion
