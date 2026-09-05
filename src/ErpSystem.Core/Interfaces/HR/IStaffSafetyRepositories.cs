using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE repositories — Reference Catalog (A) & Incident Management (B).
// Only methods beyond the generic repository are declared here. Pure leaf
// attachments (documents, witnesses, body parts, ...) are managed through their
// aggregate root and the generic repository, so they get no dedicated interface.
// Hazard/Inspection, Permit/PPE/Equipment, Contractor/Training, Environment/Health
// and Emergency/Governance repositories live in the sibling ISafety*Repositories.cs.
// ============================================================================

// ============================================================================
// A. REFERENCE / LOOKUP CATALOG
// ============================================================================

public interface ISheIncidentTypeRepository : IGenericRepository<SheIncidentType>
{
    Task<SheIncidentType?> GetByCodeAsync(string code);
    Task<IEnumerable<SheIncidentType>> GetActiveAsync();
    Task<IEnumerable<SheIncidentType>> GetByCategoryAsync(SheIncidentCategory category);
    Task<IEnumerable<SheIncidentType>> GetReportableAsync();
    /// <summary>Returns the incident type with its default corrective-action templates loaded.</summary>
    Task<SheIncidentType?> GetWithDefaultActionsAsync(Guid id);
}

public interface ISheInjuryTypeRepository : IGenericRepository<SheInjuryType>
{
    Task<SheInjuryType?> GetByCodeAsync(string code);
    Task<IEnumerable<SheInjuryType>> GetActiveAsync();
}

public interface ISheBodyPartRepository : IGenericRepository<SheBodyPart>
{
    Task<SheBodyPart?> GetByCodeAsync(string code);
    Task<IEnumerable<SheBodyPart>> GetActiveAsync();
}

public interface ISheCorrectiveActionTemplateRepository : IGenericRepository<SheCorrectiveActionTemplate>
{
    Task<SheCorrectiveActionTemplate?> GetByCodeAsync(string code);
    Task<IEnumerable<SheCorrectiveActionTemplate>> GetActiveAsync();
    Task<IEnumerable<SheCorrectiveActionTemplate>> GetByCategoryAsync(SheCorrectiveActionCategory category);
}

public interface ISheRegulatoryBodyRepository : IGenericRepository<SheRegulatoryBody>
{
    Task<IEnumerable<SheRegulatoryBody>> GetActiveAsync();
    Task<IEnumerable<SheRegulatoryBody>> GetByDomainAsync(SheRegulatoryDomain domain);
}

// ============================================================================
// B. INCIDENT MANAGEMENT & INVESTIGATION
// ============================================================================

public interface ISafetyIncidentRepository : IGenericRepository<SafetyIncident>
{
    /// <summary>Returns the incident matching the unique number, with classification navigations loaded.</summary>
    Task<SafetyIncident?> GetByIncidentNumberAsync(string incidentNumber);

    /// <summary>Returns a fully-loaded incident with all child collections and navigations.</summary>
    Task<SafetyIncident?> GetWithFullDetailsAsync(Guid id);

    Task<IEnumerable<SafetyIncident>> GetByStatusAsync(SheIncidentStatus status);
    Task<IEnumerable<SafetyIncident>> GetBySeverityAsync(SheIncidentSeverity severity);
    Task<IEnumerable<SafetyIncident>> GetByCategoryAsync(SheIncidentCategory category);
    Task<IEnumerable<SafetyIncident>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<SafetyIncident>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<SafetyIncident>> GetByOrganizationUnitAsync(Guid organizationUnitId);

    /// <summary>Returns open incidents flagged as requiring investigation.</summary>
    Task<IEnumerable<SafetyIncident>> GetRequiringInvestigationAsync();

    /// <summary>Returns incidents that are not yet closed.</summary>
    Task<IEnumerable<SafetyIncident>> GetOpenAsync();

    /// <summary>Returns incidents recorded as lost-time injuries.</summary>
    Task<IEnumerable<SafetyIncident>> GetLostTimeInjuriesAsync();

    /// <summary>Returns incidents in which the given employee is recorded as an involved person.</summary>
    Task<IEnumerable<SafetyIncident>> GetByInvolvedEmployeeAsync(Guid employeeId);

    /// <summary>Returns incidents the employee reported or was recorded as an involved person in (for self-service).</summary>
    Task<IEnumerable<SafetyIncident>> GetForEmployeeAsync(Guid employeeId);

    /// <summary>Returns authority-reportable incidents that have not yet been notified.</summary>
    Task<IEnumerable<SafetyIncident>> GetReportableNotYetNotifiedAsync();

    /// <summary>Returns a page of incidents (optionally filtered by status) plus the total count.</summary>
    Task<(IEnumerable<SafetyIncident> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, SheIncidentStatus? status = null);
}

public interface ISafetyIncidentCorrectiveActionRepository : IGenericRepository<SafetyIncidentCorrectiveAction>
{
    Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetByIncidentIdAsync(Guid incidentId);
    Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetByResponsiblePersonAsync(Guid employeeId);
    Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetByStatusAsync(SheCorrectiveActionStatus status);

    /// <summary>Returns actions past their due date that are not completed, verified or cancelled.</summary>
    Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetOverdueAsync();

    /// <summary>Returns completed actions whose effectiveness has not yet been verified.</summary>
    Task<IEnumerable<SafetyIncidentCorrectiveAction>> GetPendingVerificationAsync();
}
