using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE repositories — Hazard Register & Risk Assessment (C) and Inspections (D).
// ============================================================================

// ============================================================================
// C. HAZARD REGISTER & RISK ASSESSMENT
// ============================================================================

public interface ISheHazardRepository : IGenericRepository<SheHazard>
{
    Task<SheHazard?> GetByCodeAsync(string code);
    /// <summary>Returns the hazard with its controls and corrective actions loaded.</summary>
    Task<SheHazard?> GetWithControlsAsync(Guid id);
    /// <summary>All hazards with the list navigations (location, owner) loaded — the register read.</summary>
    Task<IEnumerable<SheHazard>> GetAllListAsync();
    Task<IEnumerable<SheHazard>> GetActiveAsync();
    Task<IEnumerable<SheHazard>> GetByStatusAsync(SheHazardStatus status);
    Task<IEnumerable<SheHazard>> GetByCategoryAsync(SheHazardCategory category);
    Task<IEnumerable<SheHazard>> GetByResidualRiskLevelAsync(SheHazardRiskLevel level);
    Task<IEnumerable<SheHazard>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<SheHazard>> GetByOwnerAsync(Guid ownerId);

    /// <summary>Returns active hazards whose residual risk score is at or above the threshold.</summary>
    Task<IEnumerable<SheHazard>> GetHighResidualRiskAsync(int minimumScore);

    /// <summary>Returns active hazards whose review due date falls within the specified number of days.</summary>
    Task<IEnumerable<SheHazard>> GetDueForReviewAsync(int daysAhead = 30);
}

public interface ISheRiskAssessmentRepository : IGenericRepository<SheRiskAssessment>
{
    Task<SheRiskAssessment?> GetByNumberAsync(string assessmentNumber);
    Task<SheRiskAssessment?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<SheRiskAssessment>> GetAllSummaryAsync();
    Task<IEnumerable<SheRiskAssessment>> GetByStatusAsync(SheRiskAssessmentStatus status);
    Task<IEnumerable<SheRiskAssessment>> GetByTypeAsync(SheRiskAssessmentType type);
    Task<IEnumerable<SheRiskAssessment>> GetByPreparerAsync(Guid preparedById);

    /// <summary>Returns approved/active assessments whose validity ends within the specified number of days.</summary>
    Task<IEnumerable<SheRiskAssessment>> GetExpiringAsync(int daysAhead = 30);

    /// <summary>Returns assessments whose next review date falls within the specified number of days.</summary>
    Task<IEnumerable<SheRiskAssessment>> GetDueForReviewAsync(int daysAhead = 30);

    /// <summary>Returns approved/active assessments (with their acknowledgements loaded) for employee sign-off.</summary>
    Task<IEnumerable<SheRiskAssessment>> GetActiveForAcknowledgementAsync();

    Task<string> GetNextAssessmentNumberAsync();
}

// ============================================================================
// D. SAFETY INSPECTIONS & AUDITS
// ============================================================================

public interface ISheInspectionChecklistRepository : IGenericRepository<SheInspectionChecklist>
{
    Task<SheInspectionChecklist?> GetByNumberAsync(string checklistNumber);
    /// <summary>Returns the checklist with its items loaded, ordered by item order.</summary>
    Task<SheInspectionChecklist?> GetWithItemsAsync(Guid id);
    Task<IEnumerable<SheInspectionChecklist>> GetActiveAsync();
    Task<IEnumerable<SheInspectionChecklist>> GetByTypeAsync(SheInspectionType type);
}

public interface ISafetyInspectionRepository : IGenericRepository<SafetyInspection>
{
    Task<SafetyInspection?> GetByNumberAsync(string inspectionNumber);
    Task<SafetyInspection?> GetWithFullDetailsAsync(Guid id);
    Task<IEnumerable<SafetyInspection>> GetAllSummaryAsync();
    Task<IEnumerable<SafetyInspection>> GetByStatusAsync(SheInspectionStatus status);
    Task<IEnumerable<SafetyInspection>> GetByTypeAsync(SheInspectionType type);
    Task<IEnumerable<SafetyInspection>> GetByCategoryAsync(SheInspectionCategory category);
    Task<IEnumerable<SafetyInspection>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<SafetyInspection>> GetByInspectorAsync(Guid inspectorId);
    Task<IEnumerable<SafetyInspection>> GetByLocationAsync(Guid locationId);

    /// <summary>Returns inspections whose next-due date falls within the specified number of days.</summary>
    Task<IEnumerable<SafetyInspection>> GetDueAsync(int daysAhead = 30);

    /// <summary>Returns inspections that are still open and have unresolved items.</summary>
    Task<IEnumerable<SafetyInspection>> GetOpenWithFindingsAsync();

    Task<string> GetNextInspectionNumberAsync();
}
