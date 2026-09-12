using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SUCCESSION PLAN
// ============================================================================

#region Succession Plan

public interface ISuccessionPlanRepository : IGenericRepository<SuccessionPlan>
{
    /// <summary>Returns the plan matching the unique plan number, with position and incumbent loaded.</summary>
    Task<SuccessionPlan?> GetByPlanNumberAsync(string planNumber);

    /// <summary>Returns the single active-version plan for a position, fully loaded with all child collections.</summary>
    Task<SuccessionPlan?> GetActiveVersionForPositionAsync(Guid positionId);

    /// <summary>Returns all version records for a position, ordered newest-first.</summary>
    Task<IEnumerable<SuccessionPlan>> GetAllVersionsForPositionAsync(Guid positionId);

    /// <summary>Returns plans filtered by status, with lightweight navigation properties for list views.</summary>
    Task<IEnumerable<SuccessionPlan>> GetByStatusAsync(SuccessionPlanStatus status);

    /// <summary>Returns all active-version plans for a given year.</summary>
    Task<IEnumerable<SuccessionPlan>> GetByYearAsync(int planYear);

    /// <summary>Returns active-version plans by year and status.</summary>
    Task<IEnumerable<SuccessionPlan>> GetByYearAndStatusAsync(int planYear, SuccessionPlanStatus status);

    /// <summary>Returns active-version plans for positions with the given criticality rating.</summary>
    Task<IEnumerable<SuccessionPlan>> GetByCriticalityAsync(PositionCriticality criticality);

    /// <summary>Returns active-version plans at or above the specified risk level.</summary>
    Task<IEnumerable<SuccessionPlan>> GetByRiskLevelAsync(SuccessionRisk riskLevel);

    /// <summary>Returns active-version plans whose next review date falls within the specified number of days.</summary>
    Task<IEnumerable<SuccessionPlan>> GetDueForReviewAsync(int daysAhead = 30);

    /// <summary>Returns active-version plans that have no ready-now successor identified.</summary>
    Task<IEnumerable<SuccessionPlan>> GetWithNoReadyNowSuccessorAsync();

    /// <summary>Returns active-version plans that have zero identified successors.</summary>
    Task<IEnumerable<SuccessionPlan>> GetWithNoSuccessorsAsync();

    /// <summary>Returns active-version plans where the given employee is the current incumbent.</summary>
    Task<IEnumerable<SuccessionPlan>> GetByIncumbentAsync(Guid incumbentEmployeeId);

    /// <summary>
    /// Returns a fully-loaded plan including candidates, competency requirements,
    /// actions, documents, and plan history.
    /// </summary>
    Task<SuccessionPlan?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns the next version number to use for a given position (max existing + 1).</summary>
    Task<int> GetNextVersionNumberAsync(Guid positionId);

    /// <summary>Returns plans whose anticipated vacancy date falls within the specified number of days.</summary>
    Task<IEnumerable<SuccessionPlan>> GetWithImpendingVacancyAsync(int daysAhead = 90);
}

#endregion

// ============================================================================
// SUCCESSION COMPETENCY REQUIREMENT
// ============================================================================

#region Succession Competency Requirement

public interface ISuccessionCompetencyRequirementRepository : IGenericRepository<SuccessionCompetencyRequirement>
{
    /// <summary>Returns all competency requirements for a succession plan, with competency details loaded.</summary>
    Task<IEnumerable<SuccessionCompetencyRequirement>> GetByPlanIdAsync(Guid planId);

    /// <summary>
    /// One requirement with its <c>Competency</c> loaded. The plain <c>GetByIdAsync</c> loads no
    /// navigation, so a writer that mapped its result returned a row whose competency code, name,
    /// category and scale maximum were all blank — the panel would show an empty name on the row
    /// it had just created and the correct one after a refetch.
    /// </summary>
    Task<SuccessionCompetencyRequirement?> GetByIdWithCompetencyAsync(Guid id);

    /// <summary>
    /// Finds the row for this plan/competency pair <b>including soft-deleted ones</b>, so a
    /// re-add can revive it. <c>IX_SuccessionCompetencyReq_Tenant_Plan_Competency</c> is unique
    /// and unfiltered, so a soft-deleted row occupies the slot as firmly as a live one.
    /// </summary>
    Task<SuccessionCompetencyRequirement?> GetIncludingDeletedAsync(Guid planId, Guid competencyId, Guid tenantId);

    /// <summary>Returns all plans that require the specified competency, useful for competency impact analysis.</summary>
    Task<IEnumerable<SuccessionCompetencyRequirement>> GetByCompetencyIdAsync(Guid competencyId);
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE
// ============================================================================

#region Succession Candidate

public interface ISuccessionCandidateRepository : IGenericRepository<SuccessionCandidate>
{
    /// <summary>Returns all candidates for a plan, ordered by rank, with employee details loaded.</summary>
    Task<IEnumerable<SuccessionCandidate>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns all plans in which the given employee appears as a candidate.</summary>
    Task<IEnumerable<SuccessionCandidate>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns candidates for a plan filtered by readiness level.</summary>
    Task<IEnumerable<SuccessionCandidate>> GetByReadinessAsync(Guid planId, ReadinessLevel readiness);

    /// <summary>Returns candidates who are marked ReadyNow and are not emergency-only.</summary>
    Task<IEnumerable<SuccessionCandidate>> GetReadyNowCandidatesForPlanAsync(Guid planId);

    /// <summary>Returns candidates flagged as emergency-only for a plan.</summary>
    Task<IEnumerable<SuccessionCandidate>> GetEmergencyCandidatesForPlanAsync(Guid planId);

    /// <summary>Returns the selected candidate for a plan, or null if none is selected yet.</summary>
    Task<SuccessionCandidate?> GetSelectedCandidateForPlanAsync(Guid planId);

    /// <summary>Returns a fully-loaded candidate including competency gaps and development activities.</summary>
    Task<SuccessionCandidate?> GetWithFullDetailsAsync(Guid id);

    /// <summary>
    /// Returns candidates for a plan whose retention risk is at or above the specified threshold.
    /// </summary>
    Task<IEnumerable<SuccessionCandidate>> GetByRetentionRiskAsync(Guid planId, RetentionRisk minimumRisk);
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE GAP
// ============================================================================

#region Succession Candidate Gap

public interface ISuccessionCandidateGapRepository : IGenericRepository<SuccessionCandidateGap>
{
    /// <summary>Returns all competency gaps for a candidate, with competency details loaded.</summary>
    Task<IEnumerable<SuccessionCandidateGap>> GetByCandidateIdAsync(Guid candidateId);

    /// <summary>Returns gaps for a candidate that have not yet been addressed.</summary>
    Task<IEnumerable<SuccessionCandidateGap>> GetUnaddressedGapsAsync(Guid candidateId);

    /// <summary>Returns all candidate gaps relating to a specific competency, for cross-candidate analysis.</summary>
    Task<IEnumerable<SuccessionCandidateGap>> GetByCompetencyIdAsync(Guid competencyId);
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT ACTIVITY
// ============================================================================

#region Succession Development Activity

public interface ISuccessionDevelopmentActivityRepository : IGenericRepository<SuccessionDevelopmentActivity>
{
    /// <summary>Returns all development activities for a succession candidate, with milestones loaded.</summary>
    Task<IEnumerable<SuccessionDevelopmentActivity>> GetByCandidateIdAsync(Guid candidateId);

    /// <summary>Returns all development activities for a talent pool member, with milestones loaded.</summary>
    Task<IEnumerable<SuccessionDevelopmentActivity>> GetByTalentPoolMemberIdAsync(Guid memberId);

    /// <summary>Returns activities filtered by status.</summary>
    Task<IEnumerable<SuccessionDevelopmentActivity>> GetByStatusAsync(DevelopmentActivityStatus status);

    /// <summary>
    /// Returns in-progress activities whose planned end date has passed
    /// but whose status is not yet Completed or Cancelled.
    /// </summary>
    Task<IEnumerable<SuccessionDevelopmentActivity>> GetOverdueActivitiesAsync();

    /// <summary>Returns a fully-loaded activity including milestones and addressed competency gaps.</summary>
    Task<SuccessionDevelopmentActivity?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT MILESTONE
// ============================================================================

#region Succession Development Milestone

public interface ISuccessionDevelopmentMilestoneRepository : IGenericRepository<SuccessionDevelopmentMilestone>
{
    /// <summary>Returns all milestones for a development activity, ordered by target date.</summary>
    Task<IEnumerable<SuccessionDevelopmentMilestone>> GetByActivityIdAsync(Guid activityId);

    /// <summary>Returns milestones whose target date has passed but are not yet marked completed.</summary>
    Task<IEnumerable<SuccessionDevelopmentMilestone>> GetOverdueMilestonesAsync();
}

#endregion

// ============================================================================
// SUCCESSION ACTION
// ============================================================================

#region Succession Action

public interface ISuccessionActionRepository : IGenericRepository<SuccessionAction>
{
    /// <summary>Returns all actions for a succession plan, with responsible person loaded.</summary>
    Task<IEnumerable<SuccessionAction>> GetByPlanIdAsync(Guid planId);

    /// <summary>
    /// One action with every navigation the full DTO resolves — plan, candidate, responsible
    /// person, assigner and the action it depends on. The plain <c>GetByIdAsync</c> loads none of
    /// them, so a writer mapping its result returned blanks in five name fields.
    /// </summary>
    Task<SuccessionAction?> GetByIdWithDetailsAsync(Guid id);

    /// <summary>Returns candidate-specific actions for a given candidate.</summary>
    Task<IEnumerable<SuccessionAction>> GetByCandidateIdAsync(Guid candidateId);

    /// <summary>Returns actions filtered by status.</summary>
    Task<IEnumerable<SuccessionAction>> GetByStatusAsync(ActionStatus status);

    /// <summary>Returns actions filtered by priority.</summary>
    Task<IEnumerable<SuccessionAction>> GetByPriorityAsync(ActionPriority priority);

    /// <summary>Returns actions whose due date has passed but are not yet completed or cancelled.</summary>
    Task<IEnumerable<SuccessionAction>> GetOverdueActionsAsync();

    /// <summary>Returns actions assigned to the specified responsible person.</summary>
    Task<IEnumerable<SuccessionAction>> GetByResponsiblePersonAsync(Guid employeeId);
}

#endregion

// ============================================================================
// SUCCESSION PLAN HISTORY
// ============================================================================

#region Succession Plan History

public interface ISuccessionPlanHistoryRepository : IGenericRepository<SuccessionPlanHistory>
{
    /// <summary>Returns all history snapshots for a plan, ordered from newest to oldest.</summary>
    Task<IEnumerable<SuccessionPlanHistory>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns the most recent history snapshot for a plan.</summary>
    Task<SuccessionPlanHistory?> GetLatestSnapshotAsync(Guid planId);
}

#endregion

// ============================================================================
// SUCCESSION DOCUMENT
// ============================================================================

#region Succession Document

public interface ISuccessionDocumentRepository : IGenericRepository<SuccessionDocument>
{
    /// <summary>
    /// One document with its uploader resolved.
    /// </summary>
    /// <remarks>
    /// Exists for the write paths. Every collection read here includes <c>UploadedBy</c>, but a
    /// just-added entity has no navigation loaded, so mapping it straight after
    /// <c>SaveChangesAsync</c> produced a create response whose <c>uploadedByName</c> was blank
    /// while the list beside it showed the name. Slice 13 caught it.
    /// </remarks>
    Task<SuccessionDocument?> GetByIdWithUploaderAsync(Guid id);

    /// <summary>Returns all documents attached to a succession plan.</summary>
    Task<IEnumerable<SuccessionDocument>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns documents narrowed to a specific candidate within a plan.</summary>
    Task<IEnumerable<SuccessionDocument>> GetByCandidateIdAsync(Guid candidateId);

    /// <summary>Returns documents attached to a talent pool member.</summary>
    Task<IEnumerable<SuccessionDocument>> GetByTalentPoolMemberIdAsync(Guid memberId);

    /// <summary>Returns only confidential documents for a plan.</summary>
    Task<IEnumerable<SuccessionDocument>> GetConfidentialDocumentsAsync(Guid planId);

    /// <summary>Returns documents whose retention date has passed, for archival processing.</summary>
    Task<IEnumerable<SuccessionDocument>> GetExpiredRetentionDocumentsAsync();
}

#endregion

// ============================================================================
// TALENT POOL
// ============================================================================

#region Talent Pool

public interface ITalentPoolRepository : IGenericRepository<TalentPool>
{
    /// <summary>Returns talent pools filtered by pool type definition.</summary>
    Task<IEnumerable<TalentPool>> GetByPoolTypeAsync(Guid poolTypeId);

    /// <summary>Returns currently active talent pools (IsActive = true and within ValidFrom/ValidTo window).</summary>
    Task<IEnumerable<TalentPool>> GetActivePoolsAsync();

    /// <summary>Returns talent pools owned by the specified employee.</summary>
    Task<IEnumerable<TalentPool>> GetByOwnerAsync(Guid ownerEmployeeId);

    /// <summary>Returns a fully-loaded talent pool including all active members and their employee details.</summary>
    Task<TalentPool?> GetWithMembersAsync(Guid id);
}

#endregion

// ============================================================================
// TALENT POOL MEMBER
// ============================================================================

#region Talent Pool Member

public interface ITalentPoolMemberRepository : IGenericRepository<TalentPoolMember>
{
    /// <summary>Returns all active members of a talent pool, ordered by rank.</summary>
    Task<IEnumerable<TalentPoolMember>> GetByTalentPoolIdAsync(Guid poolId);

    /// <summary>Returns all pool memberships for an employee across all pools.</summary>
    Task<IEnumerable<TalentPoolMember>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns active members of a pool filtered by readiness level.</summary>
    Task<IEnumerable<TalentPoolMember>> GetByReadinessAsync(Guid poolId, ReadinessLevel readiness);

    /// <summary>
    /// Returns members whose next review date falls within the specified number of days,
    /// for proactive reviewer assignment.
    /// </summary>
    Task<IEnumerable<TalentPoolMember>> GetDueForReviewAsync(int daysAhead = 30);

    /// <summary>Returns a fully-loaded member including review ratings, development activities, and documents.</summary>
    Task<TalentPoolMember?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns the specific membership record for an employee in a given pool, or null if not a member.</summary>
    Task<TalentPoolMember?> GetMembershipAsync(Guid poolId, Guid employeeId);
}

#endregion

// ============================================================================
// TALENT REVIEW SESSION
// ============================================================================

#region Talent Review Session

public interface ITalentReviewSessionRepository : IGenericRepository<TalentReviewSession>
{
    /// <summary>Returns all review sessions for a given calendar year, ordered by session date.</summary>
    Task<IEnumerable<TalentReviewSession>> GetByYearAsync(int reviewYear);

    /// <summary>Returns sessions scoped to a specific organization unit.</summary>
    Task<IEnumerable<TalentReviewSession>> GetByOrganizationUnitAsync(Guid organizationUnitId);

    /// <summary>Returns sessions that have been finalized.</summary>
    Task<IEnumerable<TalentReviewSession>> GetFinalizedSessionsAsync();

    /// <summary>Returns sessions that are not yet finalized (still in progress).</summary>
    Task<IEnumerable<TalentReviewSession>> GetPendingSessionsAsync();

    /// <summary>Returns a fully-loaded session including all ratings with employee details.</summary>
    Task<TalentReviewSession?> GetWithRatingsAsync(Guid id);
}

#endregion

// ============================================================================
// TALENT REVIEW RATING
// ============================================================================

#region Talent Review Rating

public interface ITalentReviewRatingRepository : IGenericRepository<TalentReviewRating>
{
    /// <summary>Returns all ratings for a review session, with employee details loaded.</summary>
    Task<IEnumerable<TalentReviewRating>> GetBySessionIdAsync(Guid sessionId);

    /// <summary>Returns all ratings across all sessions for a specific employee, ordered newest-first.</summary>
    Task<IEnumerable<TalentReviewRating>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns all ratings linked to a specific talent pool membership.</summary>
    Task<IEnumerable<TalentReviewRating>> GetByTalentPoolMemberIdAsync(Guid memberId);

    /// <summary>Returns the most recent calibration-confirmed rating for an employee.</summary>
    Task<TalentReviewRating?> GetLatestConfirmedRatingForEmployeeAsync(Guid employeeId);

    /// <summary>
    /// Returns ratings in a session that match the given 9-box coordinates,
    /// enabling 9-box grid population and filtering.
    /// </summary>
    Task<IEnumerable<TalentReviewRating>> GetByNineBoxPositionAsync(Guid sessionId, PerformanceRating performance, PotentialRating potential);

    /// <summary>Returns ratings in a session where calibration has been confirmed.</summary>
    Task<IEnumerable<TalentReviewRating>> GetCalibratedRatingsAsync(Guid sessionId);

    /// <summary>Returns ratings in a session that are still awaiting calibration confirmation.</summary>
    Task<IEnumerable<TalentReviewRating>> GetPendingCalibrationAsync(Guid sessionId);

    /// <summary>
    /// Returns the rating for a specific employee in a specific session, or null if not rated.
    /// </summary>
    Task<TalentReviewRating?> GetBySessionAndEmployeeAsync(Guid sessionId, Guid employeeId);
}

#endregion
