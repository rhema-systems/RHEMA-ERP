using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE services — Hazard Register & Risk Assessment (C) and Inspections (D).
// ============================================================================

// ============================================================================
// C. HAZARD SERVICE
// ============================================================================

public interface ISheHazardService
{
    Task<SheHazardDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheHazardDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetByStatusAsync(SheHazardStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetByCategoryAsync(SheHazardCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetByResidualRiskLevelAsync(SheHazardRiskLevel level, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetHighResidualRiskAsync(int minimumScore, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheHazardSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    Task<SheHazardDto> CreateAsync(CreateSheHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheHazardDto> UpdateAsync(UpdateSheHazardDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheHazardControlDto> AddControlAsync(CreateSheHazardControlDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheHazardControlDto> UpdateControlAsync(UpdateSheHazardControlDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteControlAsync(Guid controlId, CancellationToken cancellationToken = default);

    Task<SheHazardCorrectiveActionDto> AddCorrectiveActionAsync(CreateSheHazardCorrectiveActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCorrectiveActionAsync(Guid correctiveActionId, CancellationToken cancellationToken = default);
}

// ============================================================================
// C. RISK ASSESSMENT SERVICE
// ============================================================================

public interface ISheRiskAssessmentService
{
    Task<SheRiskAssessmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheRiskAssessmentDto?> GetByNumberAsync(string assessmentNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByStatusAsync(SheRiskAssessmentStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByTypeAsync(SheRiskAssessmentType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByPreparerAsync(Guid preparedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    Task<SheRiskAssessmentDto> CreateAsync(CreateSheRiskAssessmentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheRiskAssessmentDto> UpdateAsync(UpdateSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(ApproveSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<SheRiskAssessmentHazardDto> AddHazardAsync(CreateSheRiskAssessmentHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheRiskAssessmentHazardDto> UpdateHazardAsync(UpdateSheRiskAssessmentHazardDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteHazardAsync(Guid hazardLineId, CancellationToken cancellationToken = default);

    Task<SheRiskAssessmentAcknowledgementDto> AddAcknowledgementAsync(CreateSheRiskAssessmentAcknowledgementDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Active risk assessments with this employee's acknowledgement status (self-service sign-off list).</summary>
    Task<IEnumerable<MyRiskAcknowledgementDto>> GetForEmployeeAcknowledgementAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

// ============================================================================
// D. INSPECTION CHECKLIST SERVICE
// ============================================================================

public interface ISheInspectionChecklistService
{
    Task<SheInspectionChecklistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheInspectionChecklistDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheInspectionChecklistDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default);

    Task<SheInspectionChecklistDto> CreateAsync(CreateSheInspectionChecklistDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistDto> UpdateAsync(UpdateSheInspectionChecklistDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheInspectionChecklistItemDto> AddItemAsync(CreateSheInspectionChecklistItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistItemDto> UpdateItemAsync(UpdateSheInspectionChecklistItemDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default);
}

// ============================================================================
// D. SAFETY INSPECTION SERVICE
// ============================================================================

public interface ISafetyInspectionService
{
    Task<SafetyInspectionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SafetyInspectionDto?> GetByNumberAsync(string inspectionNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetByStatusAsync(SheInspectionStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetByCategoryAsync(SheInspectionCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetByInspectorAsync(Guid inspectorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetDueAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyInspectionSummaryDto>> GetOpenWithFindingsAsync(CancellationToken cancellationToken = default);

    Task<SafetyInspectionDto> CreateAsync(CreateSafetyInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyInspectionDto> UpdateAsync(UpdateSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CloseAsync(CloseSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<SafetyInspectionItemDto> AddItemAsync(CreateSafetyInspectionItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyInspectionItemDto> UpdateItemAsync(UpdateSafetyInspectionItemDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<SafetyInspectionHazardDto> AddHazardAsync(CreateSafetyInspectionHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyInspectionHazardDto> UpdateHazardAsync(UpdateSafetyInspectionHazardDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteHazardAsync(Guid inspectionHazardId, CancellationToken cancellationToken = default);

    Task<SafetyInspectionHazardActionDto> AddHazardActionAsync(CreateSafetyInspectionHazardActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyInspectionHazardActionDto> UpdateHazardActionAsync(UpdateSafetyInspectionHazardActionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteHazardActionAsync(Guid hazardActionId, CancellationToken cancellationToken = default);

    Task<SafetyInspectionDocumentDto> AddDocumentAsync(CreateSafetyInspectionDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
