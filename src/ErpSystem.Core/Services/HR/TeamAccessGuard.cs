using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who may see and change what a team is working on.
/// </summary>
/// <remarks>
/// <para><b>Why this is its own class.</b> Slices F1 and F2 both need exactly these five questions,
/// and the alternative is two copies of an authorisation rule. Round 2 lane D2 measured what that
/// costs: four private copies of one address-snapshot rule had already drifted, two of them writing
/// a column the other two silently dropped, and nobody knew until they were lifted into one place.
/// An authorisation rule is the last thing to keep two of.</para>
///
/// <para><b>The vertical gate cannot answer any of this.</b> A controller policy can ask "may this
/// caller write HR records at all"; it cannot ask "is this caller anything to do with THIS team",
/// because that needs the record. Without the second question any HR-writing user could edit any
/// team's charter and any authenticated employee could tick any team's checklist.</para>
///
/// <para><b>Three capacities.</b> HR (or an admin role) acts on any team; the team's lead or deputy
/// acts on their own; an ordinary member reads everything and may move, tick and attach only on a
/// task assigned to them.</para>
/// </remarks>
public interface ITeamAccessGuard
{
    /// <summary>Whether the caller holds HR or an admin role — entitled to any team.</summary>
    bool IsHrDesk();

    /// <summary>The caller's own employee record, or null where their login is not linked to one.</summary>
    Guid? CallerEmployeeId();

    /// <summary>The caller's ACTIVE membership of this team, if any.</summary>
    Task<TeamMember?> CallerMembershipAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>HR, or any member. Throws <see cref="UnauthorizedAccessException"/> otherwise.</summary>
    Task RequireReadAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>HR, or the team's lead or deputy. Throws otherwise.</summary>
    Task RequireWriteAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>
    /// Resolves a member id against THIS team, so an owner, assignee or chair is always somebody
    /// actually on it.
    /// </summary>
    Task<Guid?> ResolveMemberAsync(Guid teamId, Guid? memberId, string what, CancellationToken ct = default);

    /// <summary>The team, or an <see cref="ArgumentException"/> the filter turns into a 404.</summary>
    Task<Team> RequireTeamAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>Member id to display name, for the read projections. One query, not one per row.</summary>
    Task<Dictionary<Guid, string>> MemberNamesAsync(IEnumerable<Guid> memberIds, CancellationToken ct = default);

    Guid GetTenantId();
}

/// <inheritdoc />
public sealed class TeamAccessGuard : ITeamAccessGuard
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public TeamAccessGuard(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Every read below scopes to the authenticated tenant explicitly.
    public Guid GetTenantId()
    {
        if (_currentUser.TenantId is not Guid id || id == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return id;
    }

    /// <remarks>
    /// Both HR spellings are checked. The seeded role is renamed from "HR User" to "HR" on startup,
    /// but a tenant that has not run that migration still holds the old name — the same helper the
    /// assets area carries, for the same reason.
    /// </remarks>
    public bool IsHrDesk() =>
        _currentUser.IsInRole(Constants.Roles.Hr)
        || _currentUser.IsInRole(Constants.Roles.LegacyHrUser)
        || _currentUser.IsInRole(Constants.Roles.SuperAdmin)
        || _currentUser.IsInRole(Constants.Roles.TenantAdmin);

    /// <remarks>
    /// ⚠ Null is never a match. An unlinked login — <c>admin</c> is one — is nobody's member, so it
    /// fails every membership check and passes only on the HR branch.
    /// </remarks>
    public Guid? CallerEmployeeId() =>
        _currentUser.EmployeeId is { } id && id != Guid.Empty ? id : null;

    public async Task<TeamMember?> CallerMembershipAsync(Guid teamId, CancellationToken ct = default)
    {
        if (CallerEmployeeId() is not Guid employeeId) return null;
        var tenantId = GetTenantId();

        return await _unitOfWork.Repository<TeamMember>().GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.TenantId == tenantId && !m.IsDeleted
                  && m.TeamId == teamId && m.EmployeeId == employeeId && m.IsActive, ct);
    }

    public async Task RequireReadAsync(Guid teamId, CancellationToken ct = default)
    {
        if (IsHrDesk()) return;
        if (await CallerMembershipAsync(teamId, ct) is not null) return;

        // ⚠ A refusal, not an empty list. An empty list would say "this team has nothing", which is
        // a different and untrue statement.
        throw new UnauthorizedAccessException(
            "You are not a member of this team, so you cannot see what it is working on.");
    }

    /// <remarks>
    /// ⚠ <c>Coordinator</c> and <c>Secretary</c> are deliberately NOT here. They are real roles on a
    /// committee, but keeping the minute book is not the same authority as rewriting the terms of
    /// reference, and the plan named lead and deputy.
    /// </remarks>
    public async Task RequireWriteAsync(Guid teamId, CancellationToken ct = default)
    {
        if (IsHrDesk()) return;

        var membership = await CallerMembershipAsync(teamId, ct);
        if (membership is { Role: TeamMemberRole.TeamLead or TeamMemberRole.DeputyLead }) return;

        throw new UnauthorizedAccessException(
            "Only HR or the team's lead or deputy can change what the team is working on.");
    }

    public async Task<Team> RequireTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        return await _unitOfWork.Repository<Team>().GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId && t.TenantId == tenantId && !t.IsDeleted, ct)
            ?? throw new ArgumentException($"Team '{teamId}' was not found.");
    }

    /// <remarks>
    /// ⚠ Scoped to the TEAM, not merely to the tenant. Without the team check a caller could name a
    /// member of a different team entirely — the id would resolve, the name would render, and the
    /// assignment would be meaningless.
    /// </remarks>
    public async Task<Guid?> ResolveMemberAsync(
        Guid teamId, Guid? memberId, string what, CancellationToken ct = default)
    {
        if (memberId is not Guid id) return null;
        var tenantId = GetTenantId();

        var member = await _unitOfWork.Repository<TeamMember>().GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId && !m.IsDeleted, ct)
            ?? throw new ArgumentException($"Team member '{id}' was not found.");

        if (member.TeamId != teamId)
            throw new InvalidOperationException(
                $"That person is not a member of this team, so they cannot be its {what}.");

        if (!member.IsActive)
            throw new InvalidOperationException($"That member has left the team and cannot be its {what}.");

        return id;
    }

    public async Task<Dictionary<Guid, string>> MemberNamesAsync(
        IEnumerable<Guid> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        var tenantId = GetTenantId();
        var rows = await _unitOfWork.Repository<TeamMember>().GetQueryable().AsNoTracking()
            .Where(m => m.TenantId == tenantId && ids.Contains(m.Id))
            .Select(m => new { m.Id, m.Employee.FirstName, m.Employee.LastName })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => $"{r.FirstName} {r.LastName}".Trim());
    }
}
