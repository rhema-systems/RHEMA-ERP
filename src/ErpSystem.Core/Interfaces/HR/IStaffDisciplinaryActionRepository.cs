using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF DISCIPLINARY ACTION  (case header)
// ============================================================================

#region Staff Disciplinary Action

public interface IStaffDisciplinaryActionRepository : IGenericRepository<StaffDisciplinaryAction>
{
    /// <summary>
    /// Returns a case fully loaded with all sub-entities and collections for the detail view.
    /// </summary>
    Task<StaffDisciplinaryAction?> GetWithFullDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns the unique case matching the given case number, with core navigations loaded.</summary>
    Task<StaffDisciplinaryAction?> GetByCaseNumberAsync(Guid tenantId, string caseNumber);

    /// <summary>
    /// The register: one page of cases, counted and paged in the database, over the same include set
    /// as every other list read.
    /// </summary>
    /// <remarks>
    /// This lives here rather than in the service because the service used to page over a bare
    /// queryable with no includes, which is why the main case list rendered blank employee and
    /// offense names while the narrower quick views did not.
    /// </remarks>
    Task<(IEnumerable<StaffDisciplinaryAction> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId, int pageNumber, int pageSize);

    /// <summary>Returns all cases for an employee, ordered most-recent incident first.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns cases filtered by status, with employee and offense navigations for list views.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByStatusAsync(Guid tenantId, DisciplinaryStatus status);

    /// <summary>Returns cases for a specific offense type, ordered by incident date descending.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByOffenseAsync(Guid tenantId, Guid offenseId);

    /// <summary>Returns cases at or above the specified severity level, ordered by severity then incident date.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetBySeverityAsync(Guid tenantId, StaffOffenseSeverity minimumSeverity);

    /// <summary>Returns open (not Closed or Dismissed) cases, ordered by incident date descending.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetOpenCasesAsync(Guid tenantId);

    /// <summary>Returns cases where an investigation is required but none has been opened yet.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetPendingInvestigationAsync(Guid tenantId);

    /// <summary>Returns cases where a hearing is required but none has been scheduled yet.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetPendingHearingAsync(Guid tenantId);

    /// <summary>Returns cases with status DecisionMade awaiting closure.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetPendingClosureAsync(Guid tenantId);

    /// <summary>Returns cases that have an active (non-expired) warning penalty.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveWarningAsync(Guid tenantId);

    /// <summary>Returns cases that have a suspension currently in effect (today is within the suspension window).</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveSuspensionAsync(Guid tenantId);

    /// <summary>Returns cases with an outstanding fine that has not been fully paid.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithOutstandingFineAsync(Guid tenantId);

    /// <summary>Returns cases with a termination record but no separation process initiated.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithPendingTerminationAsync(Guid tenantId);

    /// <summary>Returns cases with an active appeal (status is not DecisionMade or Dismissed).</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveAppealAsync(Guid tenantId);

    /// <summary>Returns cases that have at least one open (no completion date) legal review.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveLegalReviewAsync(Guid tenantId);

    /// <summary>Returns cases whose incident date falls within the specified range, ordered by incident date.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByIncidentDateRangeAsync(Guid tenantId, DateTime from, DateTime to);

    /// <summary>Returns cases whose reported date falls within the specified range, ordered by reported date.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByReportedDateRangeAsync(Guid tenantId, DateTime from, DateTime to);

    /// <summary>Returns whether a case number is already in use for the given tenant.</summary>
    Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId);

    /// <summary>Returns the count of open cases for an employee (for policy-enforcement checks).</summary>
    Task<int> GetOpenCaseCountForEmployeeAsync(Guid tenantId, Guid employeeId);
}

#endregion
