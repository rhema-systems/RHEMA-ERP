using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SUCCESSION PLAN SERVICE
// ============================================================================

#region Succession Plan Service

public interface ISuccessionPlanService
{
    // Queries
    Task<SuccessionPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SuccessionPlanDto?> GetByPlanNumberAsync(string planNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SuccessionPlanSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<SuccessionPlanDto?> GetActiveVersionForPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetAllVersionsForPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetByStatusAsync(SuccessionPlanStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetByYearAsync(int planYear, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetByYearAndStatusAsync(int planYear, SuccessionPlanStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetByCriticalityAsync(PositionCriticality criticality, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetByRiskLevelAsync(SuccessionRisk riskLevel, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithNoReadyNowSuccessorAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithNoSuccessorsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetByIncumbentAsync(Guid incumbentEmployeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionPlanSummaryDto>> GetWithImpendingVacancyAsync(int daysAhead = 90, CancellationToken cancellationToken = default);

    // CRUD
    Task<SuccessionPlanDto> CreateAsync(CreateSuccessionPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<SuccessionPlanDto> UpdateAsync(UpdateSuccessionPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> SubmitForReviewAsync(Guid planId, Guid submittedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ReviewAsync(ReviewSuccessionPlanDto reviewDto, Guid reviewedByEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(ApproveSuccessionPlanDto approveDto, Guid approvedByEmployeeId, CancellationToken cancellationToken = default);

    // Competency requirement operations
    Task<IEnumerable<CompetencyLookupDto>> GetAllActiveCompetenciesAsync(CancellationToken cancellationToken = default);
    Task<SuccessionCompetencyRequirementDto> AddCompetencyRequirementAsync(CreateSuccessionCompetencyRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCompetencyRequirementDto>> GetCompetencyRequirementsAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<SuccessionCompetencyRequirementDto> UpdateCompetencyRequirementAsync(UpdateSuccessionCompetencyRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCompetencyRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default);

    // Action operations
    Task<SuccessionActionDto> AddActionAsync(CreateSuccessionActionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionActionSummaryDto>> GetActionsForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<SuccessionActionDto> UpdateActionAsync(UpdateSuccessionActionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteActionAsync(Guid actionId, CancellationToken cancellationToken = default);

    // History (read-only — snapshots are created internally on status changes)
    Task<IEnumerable<SuccessionPlanHistorySummaryDto>> GetHistoryAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<SuccessionPlanHistoryDto?> GetLatestSnapshotAsync(Guid planId, CancellationToken cancellationToken = default);

    // Document operations
    Task<SuccessionDocumentDto> AddDocumentAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDocumentDto>> GetConfidentialDocumentsAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    // Dashboard
    Task<SuccessionDashboardDto> GetDashboardAsync(int? planYear = null, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE SERVICE
// ============================================================================

#region Succession Candidate Service

public interface ISuccessionCandidateService
{
    // Queries
    Task<SuccessionCandidateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByReadinessAsync(Guid planId, ReadinessLevel readiness, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateSummaryDto>> GetReadyNowCandidatesForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateSummaryDto>> GetEmergencyCandidatesForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<SuccessionCandidateDto?> GetSelectedCandidateForPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateSummaryDto>> GetByRetentionRiskAsync(Guid planId, RetentionRisk minimumRisk, CancellationToken cancellationToken = default);

    // CRUD
    Task<SuccessionCandidateDto> CreateAsync(CreateSuccessionCandidateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<SuccessionCandidateDto> UpdateAsync(UpdateSuccessionCandidateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> AssessAsync(AssessCandidateDto assessDto, Guid assessedByEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> SelectCandidateAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-updates the rank of each specified candidate in a single transaction.
    /// Accepts an ordered list of {Id, Rank} pairs.
    /// </summary>
    Task BulkUpdateRanksAsync(IEnumerable<CandidateRankUpdateDto> updates, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Competency gap operations
    Task<SuccessionCandidateGapDto> AddCompetencyGapAsync(CreateSuccessionCandidateGapDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateGapDto>> GetCompetencyGapsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionCandidateGapDto>> GetUnaddressedGapsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<SuccessionCandidateGapDto> UpdateCompetencyGapAsync(UpdateSuccessionCandidateGapDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCompetencyGapAsync(Guid gapId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-generates competency gaps for a candidate by mapping the target position's
    /// <c>PositionCompetency</c> requirements against the employee's own assessed
    /// <c>EmployeeCompetency</c> levels. Skips competencies that already have a gap row.
    /// Returns the created gaps.
    /// </summary>
    Task<IEnumerable<SuccessionCandidateGapDto>> GenerateGapsFromPositionAsync(Guid candidateId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    // Reviewer feedback operations
    Task<IEnumerable<SuccessionCandidateFeedbackDto>> GetFeedbackAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<SuccessionCandidateFeedbackDto> AddFeedbackAsync(Guid candidateId, CreateSuccessionCandidateFeedbackDto createDto, Guid tenantId, Guid reviewerEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFeedbackAsync(Guid feedbackId, CancellationToken cancellationToken = default);

    // Development activity operations
    Task<SuccessionDevelopmentActivityDto> AddDevelopmentActivityAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetDevelopmentActivitiesAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<SuccessionDevelopmentActivityDto> UpdateDevelopmentActivityAsync(UpdateSuccessionDevelopmentActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDevelopmentActivityAsync(Guid activityId, CancellationToken cancellationToken = default);

    // Document operations
    Task<SuccessionDocumentDto> AddDocumentAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT ACTIVITY SERVICE
// ============================================================================

#region Succession Development Activity Service

public interface ISuccessionDevelopmentActivityService
{
    Task<SuccessionDevelopmentActivityDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivityDto>> GetFullByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByTalentPoolMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivityDto>> GetFullByTalentPoolMemberIdAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetByStatusAsync(DevelopmentActivityStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetOverdueActivitiesAsync(CancellationToken cancellationToken = default);
    Task<SuccessionDevelopmentActivityDto> CreateAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<SuccessionDevelopmentActivityDto> UpdateAsync(UpdateSuccessionDevelopmentActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Milestone operations
    Task<SuccessionDevelopmentMilestoneDto> AddMilestoneAsync(CreateSuccessionDevelopmentMilestoneDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentMilestoneDto>> GetMilestonesAsync(Guid activityId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentMilestoneDto>> GetOverdueMilestonesAsync(CancellationToken cancellationToken = default);
    Task<SuccessionDevelopmentMilestoneDto> UpdateMilestoneAsync(UpdateSuccessionDevelopmentMilestoneDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteMilestoneAsync(Guid milestoneId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteMilestoneAsync(Guid milestoneId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TALENT POOL SERVICE
// ============================================================================

#region Talent Pool Service

public interface ITalentPoolService
{
    // Queries
    Task<TalentPoolDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<TalentPoolSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolSummaryDto>> GetByPoolTypeAsync(Guid poolTypeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolSummaryDto>> GetActivePoolsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolSummaryDto>> GetByOwnerAsync(Guid ownerEmployeeId, CancellationToken cancellationToken = default);
    Task<TalentPoolDto> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default);

    // CRUD
    Task<TalentPoolDto> CreateAsync(CreateTalentPoolDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<TalentPoolDto> UpdateAsync(UpdateTalentPoolDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Member operations
    Task<TalentPoolMemberDto> AddMemberAsync(CreateTalentPoolMemberDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersAsync(Guid poolId, CancellationToken cancellationToken = default);
    Task<TalentPoolMemberDto> GetMemberByIdAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<TalentPoolMemberDto> UpdateMemberAsync(UpdateTalentPoolMemberDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveMemberAsync(Guid memberId, string removalReason, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersByReadinessAsync(Guid poolId, ReadinessLevel readiness, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentPoolMemberSummaryDto>> GetMembersDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    // Member development activity operations
    Task<SuccessionDevelopmentActivityDto> AddDevelopmentActivityForMemberAsync(CreateSuccessionDevelopmentActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDevelopmentActivitySummaryDto>> GetDevelopmentActivitiesForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    // Member document operations
    Task<SuccessionDocumentDto> AddDocumentForMemberAsync(CreateSuccessionDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SuccessionDocumentDto>> GetDocumentsForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TALENT REVIEW SESSION SERVICE
// ============================================================================

#region Talent Review Session Service

public interface ITalentReviewSessionService
{
    // Queries
    Task<TalentReviewSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewSessionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<TalentReviewSessionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewSessionSummaryDto>> GetByYearAsync(int reviewYear, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewSessionSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewSessionSummaryDto>> GetFinalizedSessionsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewSessionSummaryDto>> GetPendingSessionsAsync(CancellationToken cancellationToken = default);
    Task<TalentReviewSessionDto> GetWithRatingsAsync(Guid id, CancellationToken cancellationToken = default);

    // CRUD
    Task<TalentReviewSessionDto> CreateAsync(CreateTalentReviewSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<TalentReviewSessionDto> UpdateAsync(UpdateTalentReviewSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> FinalizeAsync(FinalizeTalentReviewSessionDto finalizeDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Rating operations
    Task<TalentReviewRatingDto> AddRatingAsync(CreateTalentReviewRatingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewRatingSummaryDto>> GetRatingsForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewRatingSummaryDto>> GetRatingsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewRatingSummaryDto>> GetByNineBoxPositionAsync(Guid sessionId, PerformanceRating performance, PotentialRating potential, CancellationToken cancellationToken = default);
    Task<TalentReviewRatingDto?> GetRatingByIdAsync(Guid ratingId, CancellationToken cancellationToken = default);
    Task<TalentReviewRatingDto?> GetLatestConfirmedRatingForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewRatingSummaryDto>> GetCalibratedRatingsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TalentReviewRatingSummaryDto>> GetPendingCalibrationAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<TalentReviewRatingDto> UpdateRatingAsync(UpdateTalentReviewRatingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ConfirmCalibrationAsync(ConfirmCalibrationDto confirmDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteRatingAsync(Guid ratingId, CancellationToken cancellationToken = default);
}

#endregion
