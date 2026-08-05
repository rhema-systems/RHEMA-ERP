using System;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Projects the existing org-related hierarchies into a single, uniform flat-node shape
/// (<see cref="OrganogramNodeDto"/>) for the organogram visualisation. Read-only;
/// tenant scoping and soft-delete are applied by the DbContext global query filters.
/// </summary>
public interface IOrganogramService
{
    /// <summary>Company / unit hierarchy (OrganizationUnit.ParentUnitId).</summary>
    Task<OrganogramResponseDto> GetUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>Positions hierarchy (EmployeePosition.ReportsToPositionId), with vacancy/headcount.</summary>
    Task<OrganogramResponseDto> GetPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>People reporting lines (Employee.ManagerId), with photos.</summary>
    Task<OrganogramResponseDto> GetPeopleAsync(CancellationToken cancellationToken = default);

    /// <summary>Geographic hierarchy (Location.ParentLocationId) for a given location structure.</summary>
    Task<OrganogramResponseDto> GetLocationsAsync(Guid structureId, CancellationToken cancellationToken = default);

    /// <summary>Team hierarchy (Team.ParentTeamId), with team leads.</summary>
    Task<OrganogramResponseDto> GetTeamsAsync(CancellationToken cancellationToken = default);
}
