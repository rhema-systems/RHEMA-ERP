using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE services — Emergency (M), Regulatory (N), Signage (O), KPI (P),
// Committee & Meetings (Q) and Return-to-Work (R).
// ============================================================================

// ============================================================================
// M. EMERGENCY SERVICE
// ============================================================================

public interface ISheEmergencyService
{
    // Plans
    Task<EmergencyPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmergencyPlanDto?> GetPlanByNumberAsync(string planNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyPlanSummaryDto>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyPlanSummaryDto>> GetPlansByTypeAsync(SheEmergencyType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyPlanSummaryDto>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyPlanSummaryDto>> GetPlansDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<EmergencyPlanDto> CreatePlanAsync(CreateEmergencyPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<EmergencyPlanDto> UpdatePlanAsync(UpdateEmergencyPlanDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default);

    // Assembly points
    Task<SheAssemblyPointDto> AddAssemblyPointAsync(CreateSheAssemblyPointDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheAssemblyPointDto> UpdateAssemblyPointAsync(UpdateSheAssemblyPointDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAssemblyPointAsync(Guid assemblyPointId, CancellationToken cancellationToken = default);

    // Emergency contacts
    Task<EmergencyContactDto> AddContactAsync(CreateEmergencyContactDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<EmergencyContactDto> UpdateContactAsync(UpdateEmergencyContactDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default);

    // Drills
    Task<IEnumerable<EmergencyDrillDto>> GetDrillsForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyDrillDto>> GetUpcomingDrillsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<EmergencyDrillDto> AddDrillAsync(CreateEmergencyDrillDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<EmergencyDrillDto> UpdateDrillAsync(UpdateEmergencyDrillDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDrillAsync(Guid drillId, CancellationToken cancellationToken = default);

    // Response team
    Task<EmergencyResponseTeamDto> AddTeamMemberAsync(CreateEmergencyResponseTeamDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<EmergencyResponseTeamDto> UpdateTeamMemberAsync(UpdateEmergencyResponseTeamDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTeamMemberAsync(Guid teamMemberId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyResponseTeamDto>> GetExpiringTeamCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmergencyResponseTeamDto>> GetTeamMembershipsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

// ============================================================================
// N. REGULATORY COMPLIANCE SERVICE
// ============================================================================

public interface ISheRegulatoryComplianceService
{
    Task<SheRegulatoryObligationDto> GetObligationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheRegulatoryObligationDto?> GetObligationByCodeAsync(string obligationCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetAllObligationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByDomainAsync(SheRegulatoryDomain domain, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByStatusAsync(SheComplianceStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetObligationsDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheRegulatoryObligationSummaryDto>> GetNonCompliantObligationsAsync(CancellationToken cancellationToken = default);

    Task<SheRegulatoryObligationDto> CreateObligationAsync(CreateSheRegulatoryObligationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheRegulatoryObligationDto> UpdateObligationAsync(UpdateSheRegulatoryObligationDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteObligationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheRegulatoryComplianceEvidenceDto> AddEvidenceAsync(CreateSheRegulatoryComplianceEvidenceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken = default);
}

// ============================================================================
// O. SAFETY SIGNAGE SERVICE
// ============================================================================

public interface ISafetySignageService
{
    Task<SafetySignDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SafetySignDto?> GetByCodeAsync(string signCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetySignDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetySignDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetySignDto>> GetByTypeAsync(SheSafetySignType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetySignDto>> GetByStatusAsync(SheSafetySignStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetySignDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetySignDto>> GetDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    Task<SafetySignDto> CreateAsync(CreateSafetySignDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetySignDto> UpdateAsync(UpdateSafetySignDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

// ============================================================================
// P. SHE PERFORMANCE (KPI) SERVICE
// ============================================================================

public interface IShePerformanceService
{
    Task<ShePerformanceSnapshotDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShePerformanceSnapshotDto?> GetByNumberAsync(string snapshotNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePerformanceSnapshotSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShePerformanceSnapshotSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<ShePerformanceSnapshotDto?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<ShePerformanceSnapshotDto> CreateAsync(CreateShePerformanceSnapshotDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Corrects the reported figures. Refused once the snapshot has been reviewed.</summary>
    Task<ShePerformanceSnapshotDto> UpdateAsync(UpdateShePerformanceSnapshotDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ReviewAsync(ReviewShePerformanceSnapshotDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

// ============================================================================
// Q. SAFETY COMMITTEE SERVICE  (committees, members, meetings)
// ============================================================================

public interface ISafetyCommitteeService
{
    // Committees
    Task<SafetyCommitteeDto> GetCommitteeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyCommitteeDto>> GetActiveCommitteesAsync(CancellationToken cancellationToken = default);
    Task<SafetyCommitteeDto> CreateCommitteeAsync(CreateSafetyCommitteeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyCommitteeDto> UpdateCommitteeAsync(UpdateSafetyCommitteeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCommitteeAsync(Guid id, CancellationToken cancellationToken = default);

    // Members
    Task<IEnumerable<SafetyCommitteeMemberDto>> GetMembersAsync(Guid committeeId, CancellationToken cancellationToken = default);
    Task<SafetyCommitteeMemberDto> AddMemberAsync(CreateSafetyCommitteeMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyCommitteeMemberDto> UpdateMemberAsync(UpdateSafetyCommitteeMemberDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    // Meetings
    Task<SafetyMeetingDto> GetMeetingAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyMeetingSummaryDto>> GetMeetingsByCommitteeAsync(Guid committeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyMeetingSummaryDto>> GetMeetingsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<SafetyMeetingDto> CreateMeetingAsync(CreateSafetyMeetingDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyMeetingDto> UpdateMeetingAsync(UpdateSafetyMeetingDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteMeetingAsync(Guid id, CancellationToken cancellationToken = default);

    // Attendees
    Task<SafetyMeetingAttendeeDto> AddAttendeeAsync(CreateSafetyMeetingAttendeeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveAttendeeAsync(Guid attendeeId, CancellationToken cancellationToken = default);

    // Action items
    Task<IEnumerable<SafetyMeetingActionItemDto>> GetOpenActionItemsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyMeetingActionItemDto>> GetOverdueActionItemsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SafetyMeetingActionItemDto>> GetActionItemsByAssigneeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<SafetyMeetingActionItemDto> AddActionItemAsync(CreateSafetyMeetingActionItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SafetyMeetingActionItemDto> UpdateActionItemAsync(UpdateSafetyMeetingActionItemDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteActionItemAsync(Guid actionItemId, CancellationToken cancellationToken = default);

    // Documents
    Task<SafetyMeetingDocumentDto> AddMeetingDocumentAsync(CreateSafetyMeetingDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteMeetingDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

// ============================================================================
// R. RETURN-TO-WORK SERVICE
// ============================================================================

public interface ISheReturnToWorkService
{
    Task<SheReturnToWorkPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheReturnToWorkPlanDto?> GetByNumberAsync(string planNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByStatusAsync(SheReturnToWorkStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetByIncidentAsync(Guid safetyIncidentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheReturnToWorkPlanSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<SheReturnToWorkPlanDto> CreateAsync(CreateSheReturnToWorkPlanDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheReturnToWorkPlanDto> UpdateAsync(UpdateSheReturnToWorkPlanDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheReturnToWorkPhaseDto> AddPhaseAsync(CreateSheReturnToWorkPhaseDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheReturnToWorkPhaseDto> UpdatePhaseAsync(UpdateSheReturnToWorkPhaseDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeletePhaseAsync(Guid phaseId, CancellationToken cancellationToken = default);

    Task<IEnumerable<SheReturnToWorkReviewDto>> GetReviewsAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<SheReturnToWorkReviewDto> AddReviewAsync(CreateSheReturnToWorkReviewDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
