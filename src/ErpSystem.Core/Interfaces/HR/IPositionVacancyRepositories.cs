using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

public interface IPositionVacancyRepository : IGenericRepository<PositionVacancy>
{
    /// <summary>Returns a vacancy with position, org unit, vacated-by employee, and requisition loaded.</summary>
    Task<PositionVacancy?> GetWithDetailsAsync(Guid id);

    /// <summary>Filtered list of vacancies (with lightweight navs) for the vacancies grid.</summary>
    Task<IEnumerable<PositionVacancy>> GetVacanciesAsync(
        PositionVacancyStatus? status = null,
        Guid? organizationUnitId = null,
        VacancyReason? reason = null,
        VacancyClassification? classification = null,
        bool includeClosed = false);

    /// <summary>
    /// Establishment view of every active position — expected vs filled headcount, plus any open
    /// tracking vacancy. Powers the "all positions, filled and vacant" grid.
    /// </summary>
    Task<IEnumerable<PositionEstablishmentDto>> GetEstablishmentOverviewAsync(
        Guid? organizationUnitId = null, bool onlyVacant = false);

    /// <summary>Count of active occupants (not Terminated/Retired/Inactive) currently on a position.</summary>
    Task<int> CountActiveOnPositionAsync(Guid tenantId, Guid positionId);

    /// <summary>Summary counts for the dashboard cards.</summary>
    Task<PositionVacancyStatsDto> GetStatsAsync();
}
