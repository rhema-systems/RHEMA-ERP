using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE services — Reference Catalog (A) & Incident Management (B).
// Services are the entry point for the API controllers. Each aggregate root has
// a service that also manages its child collections; the five reference catalogs
// are grouped into a single reference-data service. Hazard/Inspection,
// Permit/PPE/Equipment, Contractor/Training, Environment/Health and
// Emergency/Governance services live in the sibling ISafety*Services.cs files.
// ============================================================================

// ============================================================================
// A. REFERENCE / LOOKUP CATALOG SERVICE
// ============================================================================

public interface ISheReferenceDataService
{
    // Incident types
    Task<IEnumerable<SheIncidentTypeDto>> GetIncidentTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SheIncidentTypeDto> GetIncidentTypeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheIncidentTypeDto>> GetReportableIncidentTypesAsync(CancellationToken cancellationToken = default);
    Task<SheIncidentTypeDto> CreateIncidentTypeAsync(CreateSheIncidentTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheIncidentTypeDto> UpdateIncidentTypeAsync(UpdateSheIncidentTypeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteIncidentTypeAsync(Guid id, CancellationToken cancellationToken = default);

    // Default corrective actions on an incident type — these are what auto-populate onto a new
    // incident of that type (FR-INC-004).
    Task<SheIncidentTypeCorrectiveActionDto> AddIncidentTypeCorrectiveActionAsync(CreateSheIncidentTypeCorrectiveActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheIncidentTypeCorrectiveActionDto> UpdateIncidentTypeCorrectiveActionAsync(UpdateSheIncidentTypeCorrectiveActionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveIncidentTypeCorrectiveActionAsync(Guid linkId, CancellationToken cancellationToken = default);

    // Injury types
    Task<IEnumerable<SheInjuryTypeDto>> GetInjuryTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SheInjuryTypeDto> CreateInjuryTypeAsync(CreateSheInjuryTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheInjuryTypeDto> UpdateInjuryTypeAsync(UpdateSheInjuryTypeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInjuryTypeAsync(Guid id, CancellationToken cancellationToken = default);

    // Body parts
    Task<IEnumerable<SheBodyPartDto>> GetBodyPartsAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SheBodyPartDto> CreateBodyPartAsync(CreateSheBodyPartDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheBodyPartDto> UpdateBodyPartAsync(UpdateSheBodyPartDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteBodyPartAsync(Guid id, CancellationToken cancellationToken = default);

    // Corrective action templates
    Task<IEnumerable<SheCorrectiveActionTemplateDto>> GetCorrectiveActionTemplatesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SheCorrectiveActionTemplateDto> CreateCorrectiveActionTemplateAsync(CreateSheCorrectiveActionTemplateDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheCorrectiveActionTemplateDto> UpdateCorrectiveActionTemplateAsync(UpdateSheCorrectiveActionTemplateDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCorrectiveActionTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    // Regulatory bodies
    Task<IEnumerable<SheRegulatoryBodyDto>> GetRegulatoryBodiesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SheRegulatoryBodyDto> CreateRegulatoryBodyAsync(CreateSheRegulatoryBodyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheRegulatoryBodyDto> UpdateRegulatoryBodyAsync(UpdateSheRegulatoryBodyDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRegulatoryBodyAsync(Guid id, CancellationToken cancellationToken = default);
}

// ============================================================================
// B. SAFETY INCIDENT SERVICE
// ============================================================================

public interface ISafetyIncidentService
{
    // Queries
    Task<SafetyIncidentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SafetyIncidentDto?> GetByNumberAsync(string incidentNumber, CancellationToken cancellationToken = default);
    Task<PagedResult<SafetyIncidentSummaryDto>> GetPagedAsync(int page, int pageSize, SheIncidentStatus? status = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetByStatusAsync(SheIncidentStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetBySeverityAsync(SheIncidentSeverity severity, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetByCategoryAsync(SheIncidentCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetByInvolvedEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetRequiringInvestigationAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetOpenAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetLostTimeInjuriesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentSummaryDto>> GetReportableNotYetNotifiedAsync(CancellationToken cancellationToken = default);

    // CRUD
    Task<SafetyIncidentDto> CreateAsync(CreateSafetyIncidentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyIncidentDto> UpdateAsync(UpdateSafetyIncidentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> AssignInvestigationAsync(AssignSafetyIncidentInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RecordInvestigationAsync(RecordSafetyIncidentInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> NotifyAuthorityAsync(NotifySafetyIncidentAuthorityDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> FileClaimAsync(FileSafetyIncidentClaimDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ReviewAsync(ReviewSafetyIncidentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CloseAsync(CloseSafetyIncidentDto dto, Guid userId, CancellationToken cancellationToken = default);

    // Involved persons (+ injured body parts)
    Task<SafetyIncidentInvolvedPersonDto> AddInvolvedPersonAsync(CreateSafetyIncidentInvolvedPersonDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyIncidentInvolvedPersonDto> UpdateInvolvedPersonAsync(UpdateSafetyIncidentInvolvedPersonDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInvolvedPersonAsync(Guid involvedPersonId, CancellationToken cancellationToken = default);
    Task<SafetyIncidentInjuredBodyPartDto> AddInjuredBodyPartAsync(CreateSafetyIncidentInjuredBodyPartDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInjuredBodyPartAsync(Guid injuredBodyPartId, CancellationToken cancellationToken = default);

    // Witnesses
    Task<SafetyIncidentWitnessDto> AddWitnessAsync(CreateSafetyIncidentWitnessDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyIncidentWitnessDto> UpdateWitnessAsync(UpdateSafetyIncidentWitnessDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteWitnessAsync(Guid witnessId, CancellationToken cancellationToken = default);

    // Investigation team
    Task<SafetyIncidentInvestigationTeamMemberDto> AddInvestigationTeamMemberAsync(CreateSafetyIncidentInvestigationTeamMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveInvestigationTeamMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    // Corrective actions
    Task<SafetyIncidentCorrectiveActionDto> AddCorrectiveActionAsync(CreateSafetyIncidentCorrectiveActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyIncidentCorrectiveActionDto> UpdateCorrectiveActionAsync(UpdateSafetyIncidentCorrectiveActionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyCorrectiveActionAsync(VerifySafetyIncidentCorrectiveActionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCorrectiveActionAsync(Guid correctiveActionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentCorrectiveActionDto>> GetCorrectiveActionsForIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentCorrectiveActionDto>> GetOverdueCorrectiveActionsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyIncidentCorrectiveActionDto>> GetCorrectiveActionsByResponsibleAsync(Guid employeeId, CancellationToken cancellationToken = default);

    // Follow-ups & documents
    Task<SafetyIncidentFollowUpDto> AddFollowUpAsync(CreateSafetyIncidentFollowUpDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyIncidentDocumentDto> AddDocumentAsync(CreateSafetyIncidentDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
