using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The teams register and its membership.
/// </summary>
/// <remarks>
/// <para>
/// Slice 4b. The three entities were already modelled and migrated; what did not exist was any way
/// to write them. Everything here is new application layer over an untouched schema, with one
/// exception: <c>IX_Team_Tenant_Code</c> gained a soft-delete filter, because a unique index over a
/// soft-deleting store is wrong until it is filtered (slice 0's D-9 and D-10, third occurrence).
/// </para>
/// <para>
/// Tenant scoping is explicit throughout. The ApplicationDbContext is registered without a tenant,
/// so its global query filter and TenantId auto-stamp are both inert
/// (see the RHEMA convention comment in every sibling service).
/// </para>
/// </remarks>
public class TeamService : ITeamService
{
    private readonly IGenericRepository<Team> _teams;
    private readonly IGenericRepository<TeamMember> _members;
    private readonly IGenericRepository<TeamMemberHistory> _history;
    private readonly IGenericRepository<Employee> _employees;
    private readonly IGenericRepository<OrganizationUnit> _units;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<TeamService> _logger;

    public TeamService(
        IGenericRepository<Team> teams,
        IGenericRepository<TeamMember> members,
        IGenericRepository<TeamMemberHistory> history,
        IGenericRepository<Employee> employees,
        IGenericRepository<OrganizationUnit> units,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<TeamService> logger)
    {
        _teams = teams;
        _members = members;
        _history = history;
        _employees = employees;
        _units = units;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Every read of a team loads the same navigations, because every map reads them.</summary>
    private IQueryable<Team> TeamsWithNames(Guid tenantId) => _teams.GetQueryable()
        .Where(t => t.TenantId == tenantId)
        .Include(t => t.OrganizationUnit)
        .Include(t => t.TeamLead)
        .Include(t => t.ParentTeam)
        .Include(t => t.Location)
        .Include(t => t.Shift);

    private IQueryable<TeamMember> MembersWithNames(Guid tenantId) => _members.GetQueryable()
        .Where(m => m.TenantId == tenantId)
        .Include(m => m.Team)
        .Include(m => m.Employee).ThenInclude(e => e.Position);

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<TeamDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await TeamsWithNames(tenantId).FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new ArgumentException($"Team with ID '{id}' not found.");

        var counts = await CountsForAsync(tenantId, new[] { id }, cancellationToken);
        return entity.ToDto(Today, counts.members.GetValueOrDefault(id), counts.children.GetValueOrDefault(id));
    }

    public async Task<TeamDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await TeamsWithNames(tenantId).FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new ArgumentException($"Team with ID '{id}' not found.");

        var members = await LoadMembersAsync(tenantId, id, currentOnly: false, cancellationToken);
        var counts = await CountsForAsync(tenantId, new[] { id }, cancellationToken);
        return entity.ToDetailDto(Today, members, counts.children.GetValueOrDefault(id));
    }

    public async Task<IEnumerable<TeamDto>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = TeamsWithNames(tenantId);
        if (!includeInactive) query = query.Where(t => t.IsActive);

        var entities = await query
            .OrderBy(t => t.Sequence)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await CountsForAsync(tenantId, entities.Select(t => t.Id), cancellationToken);
        var today = Today;
        return entities
            .Select(t => t.ToDto(today, counts.members.GetValueOrDefault(t.Id), counts.children.GetValueOrDefault(t.Id)))
            .ToList();
    }

    public async Task<IEnumerable<TeamSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _teams.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await CountsForAsync(tenantId, entities.Select(t => t.Id), cancellationToken);
        return entities.Select(t => t.ToSummaryDto(counts.members.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<PagedResult<TeamDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var query = TeamsWithNames(tenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t =>
                EF.Functions.Like(t.Name, $"%{term}%") ||
                EF.Functions.Like(t.Code, $"%{term}%") ||
                (t.ProjectCode != null && EF.Functions.Like(t.ProjectCode, $"%{term}%")));
        }

        // Counted from the SAME tenant-scoped, search-filtered query the page is taken from. Slice
        // 3's D-15 was a paged read that counted the whole table across every tenant because the
        // count was taken off the bare queryable; it is one line to get wrong and invisible in test.
        var total = await query.CountAsync(cancellationToken);

        var entities = await query
            .OrderBy(t => t.Sequence)
            .ThenBy(t => t.Name)
            .ThenBy(t => t.Id) // a tiebreaker, so a page boundary cannot shuffle between calls
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var counts = await CountsForAsync(tenantId, entities.Select(t => t.Id), cancellationToken);
        var today = Today;

        return new PagedResult<TeamDto>
        {
            Items = entities
                .Select(t => t.ToDto(today, counts.members.GetValueOrDefault(t.Id), counts.children.GetValueOrDefault(t.Id)))
                .ToList(),
            TotalCount = total,
            Page = pageNumber,
            PageSize = pageSize,
        };
    }

    // ── writes ────────────────────────────────────────────────────────────────

    public async Task<TeamDto> CreateAsync(CreateTeamDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await ValidateAsync(tenantId, dto.Code, dto.EffectiveFrom, dto.EffectiveTo, dto.ParentTeamId,
            dto.OrganizationUnitId, dto.TeamLeadId, selfId: null, cancellationToken);

        var entity = new Team { TenantId = tenantId };
        entity.ApplyCreate(dto);

        // EffectiveFrom is a required DateOnly with no sensible zero: default(DateOnly) is
        // 0001-01-01, which would silently save as a real date a century before the company existed.
        if (entity.EffectiveFrom == default) entity.EffectiveFrom = Today;

        // ⚠ Nothing stamps the author automatically — see AuditStampExtensions. Slice 11's audit
        // found every team row carrying a blank CreatedBy, against 7 of 7 on the external-associate
        // register next door. Slice 3 had already fixed this shape on the unit change log.
        entity.StampCreated(_currentUser);
        await _teams.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team {Code} created for tenant {TenantId}", entity.Code, tenantId);

        // Re-read rather than mapping what we just wrote: every *Name on the DTO comes off a
        // navigation, and on a freshly constructed entity those navigations are all null. This is
        // slice 1's company-profile lesson and D-6's stale-nav-on-write shape in one.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TeamDto> UpdateAsync(UpdateTeamDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _teams.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == dto.Id && t.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Team with ID '{dto.Id}' not found.");

        await ValidateAsync(tenantId, dto.Code, dto.EffectiveFrom, dto.EffectiveTo, dto.ParentTeamId,
            dto.OrganizationUnitId, dto.TeamLeadId, selfId: dto.Id, cancellationToken);

        entity.ApplyUpdate(dto);
        if (entity.EffectiveFrom == default) entity.EffectiveFrom = Today;

        entity.StampUpdated(_currentUser);
        await _teams.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _teams.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Team with ID '{id}' not found.");

        var childCount = await _teams.GetQueryable()
            .CountAsync(t => t.ParentTeamId == id && t.TenantId == tenantId, cancellationToken);
        if (childCount > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' has {childCount} sub-team(s). Reassign or dissolve them first.");

        var today = Today;
        var current = await _members.GetQueryable()
            .Where(m => m.TeamId == id && m.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var stillOn = current.Count(m => TeamMappingExtensions.IsCurrentMembership(m, today));
        if (stillOn > 0)
            throw new InvalidOperationException(
                $"'{entity.Name}' still has {stillOn} member(s). Remove them before dissolving the team.");

        await _teams.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team {Code} deleted for tenant {TenantId}", entity.Code, tenantId);
        return true;
    }

    // ── membership ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TeamMemberDto>> GetMembersAsync(
        Guid teamId, bool currentOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireTeamAsync(tenantId, teamId, cancellationToken);
        return await LoadMembersAsync(tenantId, teamId, currentOnly, cancellationToken);
    }

    public async Task<TeamMemberDto> AddMemberAsync(
        Guid teamId, AddTeamMemberDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var team = await RequireTeamAsync(tenantId, teamId, cancellationToken);
        var today = Today;

        var employee = await _employees.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{dto.EmployeeId}' not found.");

        // Same predicate the rest of HR uses for "is this person still with us"
        // (EmployeeService.cs:2385). Putting a leaver on a team would put them back on the
        // organogram through the teams dimension, out the side of the slice-4 D-27 fix.
        if (!employee.IsActive || employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} has left the organisation and cannot be added to a team.");

        var existing = await _members.GetQueryable()
            .Where(m => m.TeamId == teamId && m.EmployeeId == dto.EmployeeId && m.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // ⚠ Read through the loaded list, not through a filtered COUNT. IX_TeamMember_Team_Employee
        // is deliberately NOT unique — a person may leave a team and rejoin it later, which is two
        // legitimate rows — so this rule is the service's to hold, and it must ask "is one of them
        // CURRENT", not "does one exist".
        if (existing.Any(m => TeamMappingExtensions.IsCurrentMembership(m, today)))
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} is already on {team.Name}.");

        if (team.MaxMembers.HasValue)
        {
            var live = await CountCurrentMembersAsync(tenantId, teamId, today, cancellationToken);
            if (live >= team.MaxMembers.Value)
                throw new InvalidOperationException(
                    $"{team.Name} is capped at {team.MaxMembers.Value} member(s) and already has {live}.");
        }

        var join = dto.JoinDate == default ? today : dto.JoinDate;
        if (dto.LeaveDate.HasValue && dto.LeaveDate.Value < join)
            throw new InvalidOperationException("A leaving date cannot fall before the joining date.");

        var member = new TeamMember
        {
            TenantId = tenantId, // explicit: the DbContext auto-stamp is inert here (D-1's lesson)
            TeamId = teamId,
            EmployeeId = dto.EmployeeId,
            Role = dto.Role,
            AllocationPercent = dto.AllocationPercent,
            IsPrimary = dto.IsPrimary,
            JoinDate = join,
            LeaveDate = dto.LeaveDate,
            IsActive = true,
            Notes = dto.Notes,
        };

        member.StampCreated(_currentUser);
        await _members.AddAsync(member);
        if (dto.IsPrimary) await ClearOtherPrimariesAsync(tenantId, dto.EmployeeId, member.Id, cancellationToken);

        await RecordRoleChangeAsync(tenantId, teamId, dto.EmployeeId, dto.Role, dto.Role, join,
            $"Joined {team.Name}", cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadMemberAsync(tenantId, member.Id, cancellationToken);
    }

    public async Task<TeamMemberDto> UpdateMemberAsync(
        Guid teamId, Guid memberId, UpdateTeamMemberDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireTeamAsync(tenantId, teamId, cancellationToken);

        var member = await _members.GetQueryable()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == teamId && m.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Team membership '{memberId}' not found on this team.");

        var join = dto.JoinDate == default ? member.JoinDate : dto.JoinDate;
        if (dto.LeaveDate.HasValue && dto.LeaveDate.Value < join)
            throw new InvalidOperationException("A leaving date cannot fall before the joining date.");

        var previousRole = member.Role;

        member.Role = dto.Role;
        member.AllocationPercent = dto.AllocationPercent;
        member.IsPrimary = dto.IsPrimary;
        member.JoinDate = join;
        member.LeaveDate = dto.LeaveDate;
        member.IsActive = dto.IsActive;
        member.Notes = dto.Notes;

        member.StampUpdated(_currentUser);
        await _members.UpdateAsync(member);
        if (dto.IsPrimary) await ClearOtherPrimariesAsync(tenantId, member.EmployeeId, member.Id, cancellationToken);

        // Only a genuine role move is worth a history row. Slice 3 settled this shape on the unit
        // audit trail: a log that records a rename records nothing anyone will ever read.
        if (previousRole != dto.Role)
        {
            await RecordRoleChangeAsync(tenantId, teamId, member.EmployeeId, previousRole, dto.Role,
                Today, dto.ChangeReason, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoadMemberAsync(tenantId, member.Id, cancellationToken);
    }

    public async Task<bool> RemoveMemberAsync(
        Guid teamId, Guid memberId, RemoveTeamMemberDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var team = await RequireTeamAsync(tenantId, teamId, cancellationToken);

        var member = await _members.GetQueryable()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == teamId && m.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Team membership '{memberId}' not found on this team.");

        var leave = dto.LeaveDate ?? Today;
        if (leave < member.JoinDate)
            throw new InvalidOperationException("A leaving date cannot fall before the joining date.");

        // Ended, not deleted. A team's value is partly the record of who was on it, and a soft
        // delete would hide that from the roster while leaving the row to trip over later.
        member.LeaveDate = leave;
        member.IsActive = false;
        member.StampUpdated(_currentUser);
        await _members.UpdateAsync(member);

        await RecordRoleChangeAsync(tenantId, teamId, member.EmployeeId, member.Role, member.Role,
            member.JoinDate, dto.Reason ?? $"Left {team.Name}", cancellationToken, effectiveTo: leave);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<TeamMemberDto>> GetMembershipsForEmployeeAsync(
        Guid employeeId, bool currentOnly = true, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = Today;

        var rows = await MembersWithNames(tenantId)
            .Where(m => m.EmployeeId == employeeId)
            .OrderByDescending(m => m.IsPrimary)
            .ThenByDescending(m => m.JoinDate)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var mapped = rows.Select(m => m.ToDto(today));
        return (currentOnly ? mapped.Where(m => m.IsCurrent) : mapped).ToList();
    }

    public async Task<IEnumerable<TeamMemberHistoryDto>> GetMemberHistoryAsync(
        Guid teamId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await RequireTeamAsync(tenantId, teamId, cancellationToken);

        var rows = await _history.GetQueryable()
            .Where(h => h.TeamId == teamId && h.TenantId == tenantId)
            .Include(h => h.Team)
            .Include(h => h.Employee)
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenByDescending(h => h.CreatedAt) // same-day changes cannot shuffle between calls
            .ToListAsync(cancellationToken);

        return rows.Select(h => h.ToDto()).ToList();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Team> RequireTeamAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken)
        => await _teams.GetQueryable()
               .FirstOrDefaultAsync(t => t.Id == teamId && t.TenantId == tenantId, cancellationToken)
           ?? throw new ArgumentException($"Team with ID '{teamId}' not found.");

    private async Task<List<TeamMemberDto>> LoadMembersAsync(
        Guid tenantId, Guid teamId, bool currentOnly, CancellationToken cancellationToken)
    {
        var today = Today;
        var rows = await MembersWithNames(tenantId)
            .Where(m => m.TeamId == teamId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinDate)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var mapped = rows.Select(m => m.ToDto(today));
        return (currentOnly ? mapped.Where(m => m.IsCurrent) : mapped).ToList();
    }

    private async Task<TeamMemberDto> LoadMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken)
    {
        var row = await MembersWithNames(tenantId).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new ArgumentException($"Team membership '{memberId}' not found.");
        return row.ToDto(Today);
    }

    private async Task<int> CountCurrentMembersAsync(
        Guid tenantId, Guid teamId, DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await _members.GetQueryable()
            .Where(m => m.TeamId == teamId && m.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return rows.Count(m => TeamMappingExtensions.IsCurrentMembership(m, today));
    }

    /// <summary>
    /// Member and sub-team counts for a set of teams, in two queries rather than two per team.
    /// </summary>
    private async Task<(Dictionary<Guid, int> members, Dictionary<Guid, int> children)> CountsForAsync(
        Guid tenantId, IEnumerable<Guid> teamIds, CancellationToken cancellationToken)
    {
        var ids = teamIds.Distinct().ToList();
        if (ids.Count == 0) return (new Dictionary<Guid, int>(), new Dictionary<Guid, int>());

        var today = Today;

        // Materialised before counting because `IsCurrentMembership` is the shared definition of a
        // live membership and lives in C#. Restating it as an EF-translatable predicate here is
        // exactly how the roster and the count end up disagreeing.
        var memberRows = await _members.GetQueryable()
            .Where(m => m.TenantId == tenantId && ids.Contains(m.TeamId))
            .Select(m => new { m.TeamId, m.IsActive, m.IsDeleted, m.LeaveDate })
            .ToListAsync(cancellationToken);

        var members = memberRows
            .Where(m => m.IsActive && !m.IsDeleted && (!m.LeaveDate.HasValue || m.LeaveDate.Value >= today))
            .GroupBy(m => m.TeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        var children = (await _teams.GetQueryable()
                .Where(t => t.TenantId == tenantId && t.ParentTeamId != null && ids.Contains(t.ParentTeamId.Value))
                .Select(t => t.ParentTeamId!.Value)
                .ToListAsync(cancellationToken))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return (members, children);
    }

    /// <summary>At most one primary team per employee — the flag means nothing if several are set.</summary>
    private async Task ClearOtherPrimariesAsync(
        Guid tenantId, Guid employeeId, Guid keepMemberId, CancellationToken cancellationToken)
    {
        var others = await _members.GetQueryable()
            .Where(m => m.TenantId == tenantId && m.EmployeeId == employeeId
                        && m.IsPrimary && m.Id != keepMemberId)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.IsPrimary = false;
            await _members.UpdateAsync(other);
        }
    }

    private async Task RecordRoleChangeAsync(
        Guid tenantId, Guid teamId, Guid employeeId, TeamMemberRole from, TeamMemberRole to,
        DateOnly effectiveFrom, string? reason, CancellationToken cancellationToken,
        DateOnly? effectiveTo = null)
    {
        // TenantId stamped here and nowhere else, for the same reason slice 3 pulled the unit-history
        // row construction into one shared method: three call sites building the row inline is three
        // chances for one of them to forget, and an unstamped TenantEntity takes an FK 547.
        await _history.AddAsync(new TeamMemberHistory
        {
            TenantId = tenantId,
            TeamId = teamId,
            EmployeeId = employeeId,
            PreviousRole = from,
            NewRole = to,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            ChangeReason = reason,
        }.StampCreated(_currentUser));
    }

    private async Task ValidateAsync(
        Guid tenantId, string code, DateOnly effectiveFrom, DateOnly? effectiveTo,
        Guid? parentTeamId, Guid? organizationUnitId, Guid? teamLeadId, Guid? selfId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("A team code is required.");

        var trimmed = code.Trim();
        var clash = await _teams.GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && t.Code == trimmed && (selfId == null || t.Id != selfId),
                cancellationToken);
        if (clash)
            throw new InvalidOperationException($"Another team already uses the code '{trimmed}'.");

        if (effectiveTo.HasValue && effectiveFrom != default && effectiveTo.Value < effectiveFrom)
            throw new InvalidOperationException("A team cannot end before it starts.");

        if (parentTeamId.HasValue)
        {
            if (parentTeamId == selfId)
                throw new InvalidOperationException("A team cannot be its own parent.");

            var parent = await _teams.GetQueryable()
                .FirstOrDefaultAsync(t => t.Id == parentTeamId.Value && t.TenantId == tenantId, cancellationToken)
                ?? throw new ArgumentException($"Parent team '{parentTeamId}' not found.");

            if (selfId.HasValue) await RejectCycleAsync(tenantId, selfId.Value, parent, cancellationToken);
        }

        if (organizationUnitId.HasValue)
        {
            // Checked here rather than left to the FK, because an FK violation surfaces as an
            // opaque 500 while this surfaces as a sentence the form can render. Slice 3's D-18 was
            // 42 business rules arriving as "An error occurred while…" for exactly this reason.
            var unitExists = await _units.GetQueryable()
                .AnyAsync(u => u.Id == organizationUnitId.Value && u.TenantId == tenantId, cancellationToken);
            if (!unitExists)
                throw new ArgumentException($"Organization unit '{organizationUnitId}' not found.");
        }

        if (teamLeadId.HasValue)
        {
            var lead = await _employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == teamLeadId.Value && e.TenantId == tenantId, cancellationToken)
                ?? throw new ArgumentException($"Team lead '{teamLeadId}' not found.");

            if (!lead.IsActive || lead.StaffStatus == StaffStatus.Terminated)
                throw new InvalidOperationException(
                    $"{lead.FirstName} {lead.LastName} has left the organisation and cannot lead a team.");
        }
    }

    /// <summary>
    /// Walks up from the proposed parent and refuses if it reaches the team being edited.
    /// </summary>
    /// <remarks>
    /// Without this, a two-team loop is one PUT away, and the organogram's teams dimension would
    /// then hand the client a group of nodes reachable from no root — which is precisely the case
    /// slice 4 had to add <c>DetachCycles</c> for. Refusing the write is better than rendering
    /// around it.
    /// </remarks>
    private async Task RejectCycleAsync(Guid tenantId, Guid selfId, Team parent, CancellationToken cancellationToken)
    {
        var seen = new HashSet<Guid> { parent.Id };
        var cursor = parent;

        while (cursor.ParentTeamId.HasValue)
        {
            if (cursor.ParentTeamId.Value == selfId)
                throw new InvalidOperationException(
                    "That would put the team beneath one of its own sub-teams.");

            if (!seen.Add(cursor.ParentTeamId.Value)) break; // pre-existing loop; not this write's fault

            var next = await _teams.GetQueryable()
                .FirstOrDefaultAsync(t => t.Id == cursor.ParentTeamId.Value && t.TenantId == tenantId, cancellationToken);
            if (next is null) break;
            cursor = next;
        }
    }
}
