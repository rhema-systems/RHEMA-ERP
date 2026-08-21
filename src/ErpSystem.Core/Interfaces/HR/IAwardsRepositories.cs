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
    Task<IEnumerable<AwardNominationReview>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<AwardNominationReview>> GetByReviewerIdAsync(Guid reviewerId);
    Task<IEnumerable<AwardNominationReview>> GetPendingReviewsAsync(Guid reviewerId);
    Task<AwardNominationReview?> GetReviewAsync(Guid nominationId, Guid reviewerId);
    Task<int> GetApprovalCountAsync(Guid nominationId);
    Task<int> GetRejectionCountAsync(Guid nominationId);
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



