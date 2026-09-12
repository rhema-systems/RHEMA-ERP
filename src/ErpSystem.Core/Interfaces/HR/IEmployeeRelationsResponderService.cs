using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The employee-relations responder matrix — area 9c slice 5, and <b>FR-HR-084</b>: "model a
/// grievance hierarchy defining reporting lines."
/// </summary>
/// <remarks>
/// <para><b>Maintained, not derived.</b> Measured on the DEFAULT tenant 2026-08-27:
/// <c>Employees.ManagerId</c> is set for 486 of 8,353 (5.8%) and <c>OrganizationUnits.HeadEmployeeId</c>
/// for 2 of 48 — both worse than six weeks earlier. Deriving FR-HR-181's Supervisor and HOD rungs
/// from that resolves to nobody for 94% of staff. This is the alternative already recommended to TDC
/// (<c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c> §4): HR names who answers, on a screen.</para>
///
/// <para><b>Resolution order: unit row → tenant default → nobody.</b> "Nobody" is a supported
/// outcome. A case in a unit with no row is still filed and simply arrives unassigned, exactly as
/// every case did before this existed — see <see cref="ResolveAsync"/>.</para>
/// </remarks>
public interface IEmployeeRelationsResponderService
{
    /// <summary>Every row in the matrix, defaults first, then by unit, level and start date.</summary>
    Task<IEnumerable<EmployeeRelationsResponderDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The rows for one scope. Pass null for the tenant-wide defaults.</summary>
    Task<IEnumerable<EmployeeRelationsResponderDto>> GetForUnitAsync(Guid? organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the matrix resolves to for a unit and a rung, right now, without filing anything.
    /// </summary>
    /// <remarks>
    /// The admin screen's whole purpose is showing where this comes back unresolved, so an unmatched
    /// lookup returns a DTO saying so rather than throwing.
    /// </remarks>
    Task<ResponderResolutionDto> ResolveAsync(Guid? organizationUnitId, GrievanceEscalationLevel level, CancellationToken cancellationToken = default);

    /// <summary>Every rung's answer for one unit — the coverage row the admin screen renders.</summary>
    Task<ResponderCoverageDto> GetCoverageAsync(Guid? organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Names a responder. Refuses a window that overlaps an existing row for the same unit and rung:
    /// two people answering one rung for one unit on one day is not a policy, it is a bug that would
    /// resolve arbitrarily.
    /// </summary>
    Task<EmployeeRelationsResponderDto> CreateAsync(UpsertEmployeeRelationsResponderDto dto, CancellationToken cancellationToken = default);

    Task<EmployeeRelationsResponderDto> UpdateAsync(Guid id, UpsertEmployeeRelationsResponderDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
