using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The staff directory, as an employee browses it (area 25 slice 13a).
/// </summary>
/// <remarks>
/// <para><b>Why a service of its own.</b> <c>EmployeeService</c> already has a paged search and
/// it is open to any internal caller, but it cannot answer this question: its
/// <c>EmployeeSearchDto</c> filters on <c>DepartmentId</c>/<c>SectionId</c> — the axis area 25
/// slice 12c measured as being retired (SectionId is set on 0 of 8,131 employees, DepartmentId
/// on 7,440) — and has no <c>OrganizationUnitId</c> filter at all, which is the axis that is
/// actually maintained (8,107 of 8,131). A directory that cannot browse by organisation unit is
/// not a directory.</para>
///
/// <para><b>The caller names themselves.</b> Every method takes the caller's employee id from
/// the controller rather than reading it here, because <c>ICurrentUserProvider</c> carries the
/// tenant but not the employee link — the same reason every other portal service in this area
/// takes it as a parameter. It is used only to mark the caller's own row, never to widen a read.</para>
///
/// <para><b>Everyone on strength, and nobody else.</b> Every read here is limited to active,
/// non-deleted, non-terminated employees of the caller's tenant. A leaver disappears from the
/// directory the moment they are terminated, which is the whole point of looking someone up.</para>
/// </remarks>
public interface IStaffDirectoryService
{
    /// <summary>
    /// Search and browse. <paramref name="organizationUnitId"/> includes every unit BENEATH the
    /// one named, because units on this tenant hold their people in child units — slice 12c
    /// measured "Finance Department" with 0 direct members and 7,716 in its subtree, so a
    /// direct-membership browse would answer "nobody works in Finance".
    /// </summary>
    Task<PagedResult<StaffDirectoryEntryDto>> SearchAsync(
        Guid callerEmployeeId,
        string? search,
        Guid? organizationUnitId,
        Guid? locationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>One person's card, or null when they are not on strength in this tenant.</summary>
    Task<StaffDirectoryProfileDto?> GetProfileAsync(
        Guid callerEmployeeId, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>The caller's manager and direct reports.</summary>
    Task<MyTeamDto> GetMyTeamAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
