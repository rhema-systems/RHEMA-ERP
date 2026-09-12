using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The teams register: working groups and who is in them.
/// </summary>
/// <remarks>
/// Built in slice 4b. Before it, <c>Team</c>, <c>TeamMember</c> and <c>TeamMemberHistory</c> were
/// three fully-configured entities with three real tables, zero rows, and exactly one consumer in
/// the whole repository — <c>OrganogramService</c>, which projected a table nothing could write.
/// </remarks>
public interface ITeamService
{
    Task<TeamDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The team with its full roster, current and former.</summary>
    Task<TeamDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<TeamDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<IEnumerable<TeamSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<TeamDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null, CancellationToken cancellationToken = default);

    Task<TeamDto> CreateAsync(CreateTeamDto createDto, CancellationToken cancellationToken = default);

    Task<TeamDto> UpdateAsync(UpdateTeamDto updateDto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // ── membership ────────────────────────────────────────────────────────────

    /// <summary>The roster. Former members are included so the record of who was on a team survives.</summary>
    Task<IEnumerable<TeamMemberDto>> GetMembersAsync(
        Guid teamId, bool currentOnly = false, CancellationToken cancellationToken = default);

    Task<TeamMemberDto> AddMemberAsync(
        Guid teamId, AddTeamMemberDto dto, CancellationToken cancellationToken = default);

    Task<TeamMemberDto> UpdateMemberAsync(
        Guid teamId, Guid memberId, UpdateTeamMemberDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a membership. Does <b>not</b> delete the row — the roster keeps who was on the team and
    /// when they left, which is the whole reason the record carries a <c>LeaveDate</c>.
    /// </summary>
    Task<bool> RemoveMemberAsync(
        Guid teamId, Guid memberId, RemoveTeamMemberDto dto, CancellationToken cancellationToken = default);

    /// <summary>Every team an employee belongs to. The read the employee profile needs.</summary>
    Task<IEnumerable<TeamMemberDto>> GetMembershipsForEmployeeAsync(
        Guid employeeId, bool currentOnly = true, CancellationToken cancellationToken = default);

    /// <summary>Role changes on a team, newest first.</summary>
    Task<IEnumerable<TeamMemberHistoryDto>> GetMemberHistoryAsync(
        Guid teamId, CancellationToken cancellationToken = default);
}
