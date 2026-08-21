using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Award Type Repositories

public interface IAwardTypeRepository : IGenericRepository<AwardType>
{
    Task<IEnumerable<AwardType>> GetByTenantAsync(Guid tenantId);
    Task<AwardType?> GetByCodeAsync(Guid tenantId, string code);
    Task<AwardType?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<AwardType>> GetActiveByCategoryAsync(Guid tenantId, AwardCategory category);
    Task<IEnumerable<AwardType>> GetActiveByFrequencyAsync(Guid tenantId, AwardFrequency frequency);
    Task<IEnumerable<AwardType>> GetWithLevelsAsync(Guid tenantId);
    Task<int> GetAwardCountByTypeAsync(Guid awardTypeId);
    Task<bool> HasActiveNominationsAsync(Guid awardTypeId);
    Task<bool> IsInUseAsync(Guid awardTypeId);
}

public interface IAwardLevelRepository : IGenericRepository<AwardLevel>
{
    Task<IEnumerable<AwardLevel>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardLevel>> GetActiveByAwardTypeIdAsync(Guid awardTypeId);
    Task<AwardLevel?> GetByCodeAsync(Guid tenantId, string code);
    Task<AwardLevel?> GetByRankAsync(Guid awardTypeId, int rank);
}

/// <summary>
/// Resolves what an award-eligibility target actually points at.
/// </summary>
/// <remarks>
/// <c>AwardTypeTarget.TargetId</c> is polymorphic — an organisation unit, a position, a staff level
/// or an employee, according to <c>TargetType</c> — so there is no navigation to load and
/// <c>AwardTypeTargetDto.TargetName</c> had no writer anywhere in the solution. It was declared and
/// always null, which made the eligibility list unreadable: "Employee: (blank)" for every row.
/// Batched by kind on purpose — one query per target type, not one per row.
/// </remarks>
public interface IAwardTargetNameResolver
{
    Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IEnumerable<AwardTypeTarget> targets);
}

public interface IAwardTypeTargetRepository : IGenericRepository<AwardTypeTarget>
{
    Task<IEnumerable<AwardTypeTarget>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardTypeTarget>> GetByScopeAsync(Guid awardTypeId, AwardScope scope);
    Task<bool> IsEmployeeEligibleAsync(Guid awardTypeId, Guid employeeId);
}

/// <summary>
/// Answers "who qualifies for this award, and why not" against the full set of criteria.
/// </summary>
/// <remarks>
/// <para><b>Why this replaces the old check.</b> <c>IAwardTypeTargetRepository.IsEmployeeEligibleAsync</c>
/// evaluated the award's <i>targets</i> — the unit/position/level scoping — and nothing else. The
/// award type's own eligibility vocabulary was never consulted: <c>MinServiceYears</c>,
/// <c>MaxServiceYears</c>, <c>MinAge</c>, <c>MaxAge</c>, <c>MaxAwardsPerEmployee</c> and
/// <c>MaxAwardsPerPeriod</c> appeared only in mappers, mapped in and mapped out and applied by
/// nothing. Those six fields are exactly the "eligibility criteria" TDC's note has HR set up before
/// anyone nominates, so the feature the note describes could not work.</para>
///
/// <para><b>Why it returns reasons.</b> TDC's note has management "set the criteria and then it
/// will qualify some employees". An HR officer configuring that needs to see why somebody they
/// expected is absent — a list of names alone makes a mis-set rule indistinguishable from a correct
/// one.</para>
/// </remarks>
public interface IAwardEligibilityEvaluator
{
    /// <summary>Everyone who qualifies, with the reasons anyone excluded did not.</summary>
    Task<AwardEligibilityResult> EvaluateAsync(Guid awardTypeId, Guid tenantId, DateTime asOf);

    /// <summary>One employee's standing against the same criteria.</summary>
    Task<AwardEligibilityVerdict> EvaluateEmployeeAsync(Guid awardTypeId, Guid employeeId, Guid tenantId, DateTime asOf);
}

/// <summary>The qualified set, plus everyone considered and rejected and why.</summary>
public sealed class AwardEligibilityResult
{
    public Guid AwardTypeId { get; init; }
    public DateTime AsOf { get; init; }
    public int ConsideredCount { get; init; }
    public List<AwardEligibilityVerdict> Eligible { get; init; } = new();
    public List<AwardEligibilityVerdict> Ineligible { get; init; } = new();
}

/// <summary>One employee's standing, and the criteria they failed if any.</summary>
public sealed class AwardEligibilityVerdict
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }
    public bool IsEligible { get; init; }

    /// <summary>Empty when eligible. Each entry names one criterion and the value that failed it.</summary>
    public List<string> Reasons { get; init; } = new();
}

/// <summary>
/// Award cycles, queried by tenant rather than loaded whole.
/// </summary>
/// <remarks>
/// The open-for-nomination and open-for-voting lists are read on every visit to the awards landing
/// page, so they must not begin by loading every cycle that has ever run. The window comparison
/// itself stays in the service — it depends on <c>Status</c> as well as the clock, and expressing
/// that in the predicate would put the rule in two places.
/// </remarks>
public interface IAwardCycleRepository : IGenericRepository<AwardCycle>
{
    Task<IEnumerable<AwardCycle>> GetByTenantAsync(Guid tenantId);
    Task<IEnumerable<AwardCycle>> GetByAwardTypeIdAsync(Guid tenantId, Guid awardTypeId);

    /// <summary>Published cycles only — the candidates for an "is it open right now" test.</summary>
    Task<IEnumerable<AwardCycle>> GetPublishedAsync(Guid tenantId);

    Task<bool> CodeExistsAsync(Guid tenantId, string cycleCode);
}

public interface IAwardBudgetRepository : IGenericRepository<AwardBudget>
{
    Task<IEnumerable<AwardBudget>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<AwardBudget?> GetByYearAsync(Guid awardTypeId, int year);
    Task<AwardBudget?> GetByBudgetCodeAsync(Guid tenantId, string budgetCode);
    Task<decimal> GetAvailableBudgetAsync(Guid awardTypeId, int year);
    Task<IEnumerable<AwardBudget>> GetByYearRangeAsync(Guid tenantId, int startYear, int endYear);
}

#endregion

#region Employee Award Repositories

public interface IEmployeeAwardRepository : IGenericRepository<EmployeeAward>
{
    Task<IEnumerable<EmployeeAward>> GetByTenantAsync(Guid tenantId);
    Task<EmployeeAward?> GetWithDetailsAsync(Guid id);
    Task<EmployeeAward?> GetByAwardNumberAsync(Guid tenantId, string awardNumber);
    Task<IEnumerable<EmployeeAward>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<EmployeeAward>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<EmployeeAward>> GetByAwardLevelIdAsync(Guid awardLevelId);
    Task<IEnumerable<EmployeeAward>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<EmployeeAward>> GetByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<EmployeeAward>> GetPendingPresentationsAsync(Guid tenantId);
    Task<IEnumerable<EmployeeAward>> GetPendingPaymentsAsync(Guid tenantId);
    Task<IEnumerable<EmployeeAward>> GetPendingLeaveProcessingAsync(Guid tenantId);
    Task<int> GetEmployeeAwardCountAsync(Guid employeeId, Guid awardTypeId);
    Task<bool> HasReceivedAwardAsync(Guid employeeId, Guid awardTypeId, int year);
}

public interface IAwardAttachmentRepository : IGenericRepository<AwardAttachment>
{
    Task<IEnumerable<AwardAttachment>> GetByAwardIdAsync(Guid awardId);
    Task<IEnumerable<AwardAttachment>> GetByTypeAsync(Guid awardId, AwardAttachmentType type);
    Task DeleteByAwardIdAsync(Guid awardId);
}

public interface ITeamAwardRecipientRepository : IGenericRepository<TeamAwardRecipient>
{
    Task<IEnumerable<TeamAwardRecipient>> GetByAwardIdAsync(Guid awardId);
    Task<IEnumerable<TeamAwardRecipient>> GetByEmployeeIdAsync(Guid employeeId);
    Task DeleteByAwardIdAsync(Guid awardId);
}

#endregion

#region Award Nomination Repositories

public interface IAwardNominationRepository : IGenericRepository<AwardNomination>
{
    Task<IEnumerable<AwardNomination>> GetByTenantAsync(Guid tenantId);
    Task<AwardNomination?> GetWithDetailsAsync(Guid id);
    Task<AwardNomination?> GetByNominationNumberAsync(Guid tenantId, string nominationNumber);
    Task<IEnumerable<AwardNomination>> GetByNomineeIdAsync(Guid nomineeId);

    /// <summary>
    /// Live nominations assigned to any of the given committees.
    /// </summary>
    /// <remarks>
    /// Used to answer "what do I still have to score", which is a question about nominations rather
    /// than about reviews. Loading every nomination in the tenant and filtering in memory is the
    /// shape this area has been removing since slice 3.
    /// </remarks>
    Task<IEnumerable<AwardNomination>> GetForCommitteesAsync(Guid tenantId, IEnumerable<Guid> committeeIds);

    /// <summary>
    /// How many live nominations each cycle holds, in one query. Counting them by loading every
    /// nomination in the tenant and grouping in memory is the shape that turns a cycle register
    /// into a full table scan per page.
    /// </summary>
    Task<Dictionary<Guid, int>> GetCountsByCycleAsync(Guid tenantId);
    Task<IEnumerable<AwardNomination>> GetByNominatedByIdAsync(Guid nominatedById);
    Task<IEnumerable<AwardNomination>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardNomination>> GetByYearAsync(Guid tenantId, int year);
    Task<IEnumerable<AwardNomination>> GetByYearAndPeriodAsync(Guid tenantId, int year, int? quarter = null, int? month = null);
    Task<IEnumerable<AwardNomination>> GetByStatusAsync(Guid tenantId, AwardNominationStatus status);
    Task<IEnumerable<AwardNomination>> GetByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<AwardNomination>> GetRequiringCommitteeReviewAsync(Guid tenantId);
    Task<IEnumerable<AwardNomination>> GetApprovedWithoutAwardAsync(Guid tenantId);
    Task<bool> HasNominationInPeriodAsync(Guid employeeId, Guid awardTypeId, int year, int? quarter = null, int? month = null);
}

public interface ITeamAwardNomineeRepository : IGenericRepository<TeamAwardNominee>
{
    Task<IEnumerable<TeamAwardNominee>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<TeamAwardNominee>> GetByEmployeeIdAsync(Guid employeeId);
    Task DeleteByNominationIdAsync(Guid nominationId);
}

public interface IAwardNomineeContributionRepository : IGenericRepository<AwardNomineeContribution>
{
    Task<IEnumerable<AwardNomineeContribution>> GetByNominationIdAsync(Guid nominationId);
    Task DeleteByNominationIdAsync(Guid nominationId);
}

public interface IAwardNominationAttachmentRepository : IGenericRepository<AwardNominationAttachment>
{
    Task<IEnumerable<AwardNominationAttachment>> GetByNominationIdAsync(Guid nominationId);
    Task DeleteByNominationIdAsync(Guid nominationId);
}

public interface IAwardCommitteeRepository : IGenericRepository<AwardCommittee>
{
    Task<IEnumerable<AwardCommittee>> GetByTenantAsync(Guid tenantId);
    Task<IEnumerable<AwardCommittee>> GetActiveCommitteesAsync(Guid tenantId);
    Task<AwardCommittee?> GetWithMembersAsync(Guid id);
    Task<AwardCommittee?> GetActiveForDateAsync(Guid tenantId, DateTime date);
    Task<bool> HasQuorumAsync(Guid committeeId);
}

public interface IAwardCommitteeMemberRepository : IGenericRepository<AwardCommitteeMember>
{
    Task<IEnumerable<AwardCommitteeMember>> GetByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<AwardCommitteeMember>> GetActiveByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<AwardCommitteeMember>> GetByEmployeeIdAsync(Guid employeeId);
    Task<bool> IsActiveMemberAsync(Guid committeeId, Guid employeeId);
    Task<int> GetActiveCountAsync(Guid committeeId);
}

public interface IAwardNominationReviewRepository : IGenericRepository<AwardNominationReview>
{
    /// <summary>
    /// Average score and reviewer count per nomination for a cycle, computed in the database.
    /// </summary>
    /// <remarks>
    /// Replaces <c>GetApprovalCountAsync</c> and <c>GetRejectionCountAsync</c>, which counted
    /// approvals and rejections of a <c>bool? Approved</c> that no longer exists. Those two encoded
    /// the wrong model: TDC decides the winner on the highest average score, and a count of
    /// approvals cannot rank two nominations everybody approved.
    /// </remarks>
    Task<Dictionary<Guid, (double Average, int Reviewers)>> GetScoreSummaryByCycleAsync(Guid cycleId);

    Task<IEnumerable<AwardNominationReview>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<AwardNominationReview>> GetByReviewerIdAsync(Guid reviewerId);
    Task<AwardNominationReview?> GetReviewAsync(Guid nominationId, Guid reviewerId);
    Task<bool> HasReviewedAsync(Guid nominationId, Guid reviewerId);
}

#endregion

#region Long Service Award Repositories

public interface ILongServiceAwardRepository : IGenericRepository<LongServiceAward>
{
    Task<IEnumerable<LongServiceAward>> GetByTenantAsync(Guid tenantId);
    Task<LongServiceAward?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<LongServiceAward>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<LongServiceAward>> GetByYearsOfServiceAsync(Guid tenantId, int yearsOfService);
    Task<IEnumerable<LongServiceAward>> GetUpcomingMilestonesAsync(Guid tenantId, int daysAhead = 90);
    Task<IEnumerable<LongServiceAward>> GetPendingProcessingAsync(Guid tenantId);
    Task<LongServiceAward?> GetByEmployeeAndYearsAsync(Guid employeeId, int yearsOfService);
}

#endregion


#region Award Voting Repositories

public interface IAwardVoteRepository : IGenericRepository<AwardVote>
{
    /// <summary>This voter's ballot in this cycle, or null. There can only ever be one.</summary>
    Task<AwardVote?> GetByVoterAsync(Guid cycleId, Guid voterId);

    Task<IEnumerable<AwardVote>> GetByCycleAsync(Guid cycleId);

    /// <summary>Votes per nomination for a cycle, counted in the database rather than in memory.</summary>
    Task<Dictionary<Guid, int>> GetCountsByNominationAsync(Guid cycleId);
}

/// <summary>
/// Answers whether an employee may vote in an award.
/// </summary>
/// <remarks>
/// TDC's note: <i>"a section of the employees or all of them can vote on the nominees"</i>. The
/// electorate is scoped with the same targets as eligibility, distinguished by
/// <c>AwardTargetPurpose.Electorate</c> — an award with no electorate targets is voted on by
/// everyone, which is the "or all of them" half of the sentence and the sensible default.
/// </remarks>
public interface IAwardElectorateEvaluator
{
    Task<bool> CanVoteAsync(Guid awardTypeId, Guid employeeId, Guid tenantId, DateTime asOf);
}

#endregion


#region Award Performance Triggers

/// <summary>Finds who an award should put forward automatically, from performance records.</summary>
public interface IAwardPerformanceTriggerEvaluator
{
    Task<AwardPerformanceTriggerResult> EvaluateAsync(AwardType awardType, Guid tenantId);
}

/// <summary>
/// Who the triggers matched, and how much evidence there was to match against.
/// </summary>
/// <remarks>
/// The <c>Examined</c> counts exist so a caller can tell "almost nobody qualified" from "almost
/// nobody has been appraised". Measured 2026-08-21, the live store holds 4,328 appraisals of which
/// 18 carry a score, so the second explanation is currently the true one and a screen that reported
/// only "0 candidates" would be blaming the rule for the data.
/// </remarks>
public sealed class AwardPerformanceTriggerResult
{
    public decimal? MinPerformanceScore { get; init; }
    public int? MinGoalsAchieved { get; init; }

    /// <summary>Appraisals carrying a score that were considered.</summary>
    public int AppraisalsExamined { get; set; }

    /// <summary>Completed goals that were considered.</summary>
    public int GoalsExamined { get; set; }

    public List<Guid> EmployeeIds { get; set; } = new();

    /// <summary>True when the award has no trigger configured, so nothing can be generated.</summary>
    public bool NoTriggerConfigured => MinPerformanceScore == null && MinGoalsAchieved == null;
}

#endregion
