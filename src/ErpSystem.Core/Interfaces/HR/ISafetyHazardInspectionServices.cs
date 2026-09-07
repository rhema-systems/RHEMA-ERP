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

    // ── Builder (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §4) ──
    /// <summary>Validates the structure and freezes it; retires the previous version if it is still published.</summary>
    Task<SheInspectionChecklistDto> PublishAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistDto> RetireAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>Deep-clones a published/retired template into Draft Version+1 under the same number.</summary>
    Task<SheInspectionChecklistDto> CreateNewVersionAsync(Guid id, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistFieldDto> AddFieldAsync(CreateSheInspectionChecklistFieldDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistFieldDto> UpdateFieldAsync(UpdateSheInspectionChecklistFieldDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFieldAsync(Guid fieldId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistSectionDto> AddSectionAsync(CreateSheInspectionChecklistSectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistSectionDto> UpdateSectionAsync(UpdateSheInspectionChecklistSectionDto dto, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>Deletes the section and its items.</summary>
    Task<bool> DeleteSectionAsync(Guid sectionId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistOutcomeDto> AddOutcomeAsync(CreateSheInspectionChecklistOutcomeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistOutcomeDto> UpdateOutcomeAsync(UpdateSheInspectionChecklistOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteOutcomeAsync(Guid outcomeId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistSignatoryDto> AddSignatoryAsync(CreateSheInspectionChecklistSignatoryDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistSignatoryDto> UpdateSignatoryAsync(UpdateSheInspectionChecklistSignatoryDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSignatoryAsync(Guid signatoryId, CancellationToken cancellationToken = default);
    /// <summary>Replace-set reorders: the body is every child id in its new order; a missing or foreign id is refused.</summary>
    Task<SheInspectionChecklistDto> ReorderFieldsAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistDto> ReorderSectionsAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistDto> ReorderSectionItemsAsync(Guid sectionId, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistDto> ReorderOutcomesAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInspectionChecklistDto> ReorderSignatoriesAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default);
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

    // ── Checklist run (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §4) ──
    /// <summary>Materialises a published template's items onto an inspection that has none yet.</summary>
    Task<SafetyInspectionDto> ApplyChecklistAsync(Guid inspectionId, ApplySafetyInspectionChecklistDto dto, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>Bulk answer: each item's status (validated against its section kind) and remarks.</summary>
    Task<SafetyInspectionDto> SaveResponsesAsync(Guid inspectionId, IReadOnlyList<SafetyInspectionResponseDto> responses, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>Replace-set of the header field values, type-checked per field.</summary>
    Task<SafetyInspectionDto> SaveFieldValuesAsync(Guid inspectionId, IReadOnlyList<SafetyInspectionFieldValueWriteDto> values, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>The live score over the current answers, without persisting.</summary>
    Task<SafetyInspectionScoreDto> GetScoreAsync(Guid inspectionId, CancellationToken cancellationToken = default);
    /// <summary>Gate + persist: every item assessed, required fields filled, outcome confirmed; totals written; status advanced.</summary>
    Task<SafetyInspectionDto> CompleteAsync(Guid inspectionId, CompleteSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyInspectionSignatureDto> AddSignatureAsync(Guid inspectionId, CreateSafetyInspectionSignatureDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSignatureAsync(Guid signatureId, CancellationToken cancellationToken = default);
}
