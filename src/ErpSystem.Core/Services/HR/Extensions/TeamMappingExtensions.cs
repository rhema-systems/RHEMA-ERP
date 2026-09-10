using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Entity ↔ DTO mapping for teams and their membership.
/// </summary>
/// <remarks>
/// Every <c>*Name</c> field here is read off a navigation, so every caller must
/// <c>.Include()</c> what it maps. That is not a style preference: slice 3's D-16 was four name
/// fields hardcoded to <c>null</c> behind the comment "would need to load separately if needed",
/// on a log whose entire job was to say what changed from what to what. A name that is silently
/// blank is worse than one that is absent, because the screen renders an empty box for ever.
/// </remarks>
public static class TeamMappingExtensions
{
    #region Team

    public static TeamDto ToDto(this Team entity, DateOnly today, int memberCount = 0, int childTeamCount = 0)
    {
        return new TeamDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            TeamType = entity.TeamType,
            Status = entity.Status,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            TeamLeadId = entity.TeamLeadId,
            TeamLeadName = ComposeName(entity.TeamLead),
            ParentTeamId = entity.ParentTeamId,
            ParentTeamName = entity.ParentTeam?.Name,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            ShiftId = entity.ShiftId,
            // ⚠ `ShiftName`, not `Name` — ShiftDefinition does not have a `Name`. Written from the
            // entity rather than from the field's own label, which is the area-12 rule.
            ShiftName = entity.Shift?.ShiftName,
            CostCenterCode = entity.CostCenterCode,
            FinanceAccountId = entity.FinanceAccountId,
            ProjectCode = entity.ProjectCode,
            TeamEmail = entity.TeamEmail,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            MaxMembers = entity.MaxMembers,
            Sequence = entity.Sequence,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
            MemberCount = memberCount,
            ChildTeamCount = childTeamCount,
            HasLapsed = entity.EffectiveTo.HasValue && entity.EffectiveTo.Value < today,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
        };
    }

    public static TeamDetailDto ToDetailDto(
        this Team entity, DateOnly today, IEnumerable<TeamMemberDto> members, int childTeamCount = 0)
    {
        var list = members.ToList();
        var basic = entity.ToDto(today, list.Count(m => m.IsCurrent), childTeamCount);

        return new TeamDetailDto
        {
            Id = basic.Id,
            TenantId = basic.TenantId,
            Name = basic.Name,
            Code = basic.Code,
            Description = basic.Description,
            TeamType = basic.TeamType,
            Status = basic.Status,
            OrganizationUnitId = basic.OrganizationUnitId,
            OrganizationUnitName = basic.OrganizationUnitName,
            TeamLeadId = basic.TeamLeadId,
            TeamLeadName = basic.TeamLeadName,
            ParentTeamId = basic.ParentTeamId,
            ParentTeamName = basic.ParentTeamName,
            LocationId = basic.LocationId,
            LocationName = basic.LocationName,
            ShiftId = basic.ShiftId,
            ShiftName = basic.ShiftName,
            CostCenterCode = basic.CostCenterCode,
            ProjectCode = basic.ProjectCode,
            TeamEmail = basic.TeamEmail,
            EffectiveFrom = basic.EffectiveFrom,
            EffectiveTo = basic.EffectiveTo,
            MaxMembers = basic.MaxMembers,
            Sequence = basic.Sequence,
            IsActive = basic.IsActive,
            Notes = basic.Notes,
            MemberCount = basic.MemberCount,
            ChildTeamCount = basic.ChildTeamCount,
            HasLapsed = basic.HasLapsed,
            CreatedAt = basic.CreatedAt,
            CreatedBy = basic.CreatedBy,
            UpdatedAt = basic.UpdatedAt,
            UpdatedBy = basic.UpdatedBy,
            Members = list,
        };
    }

    public static TeamSummaryDto ToSummaryDto(this Team entity, int memberCount = 0)
    {
        return new TeamSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            TeamType = entity.TeamType,
            Status = entity.Status,
            MemberCount = memberCount,
            IsActive = entity.IsActive,
        };
    }

    public static void ApplyCreate(this Team entity, CreateTeamDto dto)
    {
        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim();
        entity.Description = dto.Description;
        entity.TeamType = dto.TeamType;
        entity.Status = dto.Status;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.TeamLeadId = dto.TeamLeadId;
        entity.ParentTeamId = dto.ParentTeamId;
        entity.LocationId = dto.LocationId;
        entity.ShiftId = dto.ShiftId;
        entity.CostCenterCode = dto.CostCenterCode;
        entity.ProjectCode = dto.ProjectCode;
        entity.TeamEmail = dto.TeamEmail;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.MaxMembers = dto.MaxMembers;
        entity.Sequence = dto.Sequence;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
    }

    /// <summary>
    /// Applies an edit. Every writable field is assigned — a field this method forgets is a field
    /// the endpoint accepts, answers 200 for, and silently discards, which is area 14's signature
    /// defect and slice 2's D-14. If a field is deliberately not editable it belongs off the DTO,
    /// not off this method.
    /// </summary>
    public static void ApplyUpdate(this Team entity, UpdateTeamDto dto)
    {
        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim();
        entity.Description = dto.Description;
        entity.TeamType = dto.TeamType;
        entity.Status = dto.Status;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.TeamLeadId = dto.TeamLeadId;
        entity.ParentTeamId = dto.ParentTeamId;
        entity.LocationId = dto.LocationId;
        entity.ShiftId = dto.ShiftId;
        entity.CostCenterCode = dto.CostCenterCode;
        entity.ProjectCode = dto.ProjectCode;
        entity.TeamEmail = dto.TeamEmail;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.MaxMembers = dto.MaxMembers;
        entity.Sequence = dto.Sequence;
        entity.IsActive = dto.IsActive;
        entity.Notes = dto.Notes;
    }

    #endregion

    #region Membership

    public static TeamMemberDto ToDto(this TeamMember entity, DateOnly today)
    {
        return new TeamMemberDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TeamId = entity.TeamId,
            TeamName = entity.Team?.Name,
            EmployeeId = entity.EmployeeId,
            EmployeeName = ComposeName(entity.Employee),
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            PositionTitle = entity.Employee?.Position?.Title,
            Role = entity.Role,
            AllocationPercent = entity.AllocationPercent,
            IsPrimary = entity.IsPrimary,
            JoinDate = entity.JoinDate,
            LeaveDate = entity.LeaveDate,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
            IsCurrent = IsCurrentMembership(entity, today),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
        };
    }

    /// <summary>
    /// The one definition of "on the team right now", so the roster, the member count and the
    /// organogram cannot disagree about who is in a team.
    /// </summary>
    public static bool IsCurrentMembership(TeamMember entity, DateOnly today)
        => entity.IsActive
           && !entity.IsDeleted
           && (!entity.LeaveDate.HasValue || entity.LeaveDate.Value >= today);

    public static TeamMemberHistoryDto ToDto(this TeamMemberHistory entity)
    {
        return new TeamMemberHistoryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TeamId = entity.TeamId,
            TeamName = entity.Team?.Name,
            EmployeeId = entity.EmployeeId,
            EmployeeName = ComposeName(entity.Employee),
            PreviousRole = entity.PreviousRole,
            NewRole = entity.NewRole,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            ChangeReason = entity.ChangeReason,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
        };
    }

    #endregion

    /// <summary>
    /// Composes an employee's display name in memory.
    /// </summary>
    /// <remarks>
    /// <c>Employee.FullName</c> is <c>[NotMapped]</c> and throws when EF tries to project it
    /// server-side, so it is only safe once the entity is materialised — the same trap slice 3 hit
    /// on the unit history log and area 14 hit in an <c>OrderBy</c>.
    /// </remarks>
    private static string? ComposeName(Employee? employee)
        => employee is null ? null : employee.FullName;
}
