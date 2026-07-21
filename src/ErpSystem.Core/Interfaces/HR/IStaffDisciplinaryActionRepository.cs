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
    Task<StaffDisciplinaryAction?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns the unique case matching the given case number, with core navigations loaded.</summary>
    Task<StaffDisciplinaryAction?> GetByCaseNumberAsync(string caseNumber);

    /// <summary>Returns all cases for an employee, ordered most-recent incident first.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Returns cases filtered by status, with employee and offense navigations for list views.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByStatusAsync(DisciplinaryStatus status);

    /// <summary>Returns cases for a specific offense type, ordered by incident date descending.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByOffenseAsync(Guid offenseId);

    /// <summary>Returns cases at or above the specified severity level, ordered by severity then incident date.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetBySeverityAsync(StaffOffenseSeverity minimumSeverity);

    /// <summary>Returns open (not Closed or Dismissed) cases, ordered by incident date descending.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetOpenCasesAsync();

    /// <summary>Returns cases where an investigation is required but none has been opened yet.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetPendingInvestigationAsync();

    /// <summary>Returns cases where a hearing is required but none has been scheduled yet.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetPendingHearingAsync();

    /// <summary>Returns cases with status DecisionMade awaiting closure.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetPendingClosureAsync();

    /// <summary>Returns cases that have an active (non-expired) warning penalty.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveWarningAsync();

    /// <summary>Returns cases that have a suspension currently in effect (today is within the suspension window).</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveSuspensionAsync();

    /// <summary>Returns cases with an outstanding fine that has not been fully paid.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithOutstandingFineAsync();

    /// <summary>Returns cases with a termination record but no separation process initiated.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithPendingTerminationAsync();

    /// <summary>Returns cases with an active appeal (status is not DecisionMade or Dismissed).</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveAppealAsync();

    /// <summary>Returns cases that have at least one open (no completion date) legal review.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetWithActiveLegalReviewAsync();

    /// <summary>Returns cases whose incident date falls within the specified range, ordered by incident date.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByIncidentDateRangeAsync(DateTime from, DateTime to);

    /// <summary>Returns cases whose reported date falls within the specified range, ordered by reported date.</summary>
    Task<IEnumerable<StaffDisciplinaryAction>> GetByReportedDateRangeAsync(DateTime from, DateTime to);

    /// <summary>Returns whether a case number is already in use for the given tenant.</summary>
    Task<bool> CaseNumberExistsAsync(string caseNumber, Guid tenantId);

    /// <summary>Returns the count of open cases for an employee (for policy-enforcement checks).</summary>
    Task<int> GetOpenCaseCountForEmployeeAsync(Guid employeeId);
}

#endregion
