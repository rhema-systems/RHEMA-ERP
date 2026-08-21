using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Award Type Services

public interface IAwardTypeService
{
    Task<AwardTypeDto?> GetByIdAsync(Guid id);
    Task<AwardTypeDto?> GetByCodeAsync(Guid tenantId, string code);
    Task<AwardTypeDto?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<AwardTypeSummaryDto>> GetAllAsync(Guid tenantId);
    Task<PagedResult<AwardTypeSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, AwardCategory? category = null);
    Task<IEnumerable<AwardTypeSummaryDto>> GetActiveByCategoryAsync(Guid tenantId, AwardCategory category);
    Task<IEnumerable<AwardTypeSummaryDto>> GetActiveByFrequencyAsync(Guid tenantId, AwardFrequency frequency);
    Task<IEnumerable<AwardTypeSummaryDto>> GetWithLevelsAsync(Guid tenantId);
    Task<bool> CanDeleteAsync(Guid id);
    Task<bool> IsInUseAsync(Guid id);
    Task<AwardTypeDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardTypeDto dto);
    Task<AwardTypeDto> UpdateAsync(Guid id, Guid userId, UpdateAwardTypeDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAwardLevelService
{
    Task<AwardLevelDto?> GetByIdAsync(Guid id);
    Task<AwardLevelDto?> GetByCodeAsync(Guid tenantId, string code);
    Task<IEnumerable<AwardLevelDto>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardLevelDto>> GetActiveByAwardTypeIdAsync(Guid awardTypeId);
    Task<AwardLevelDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardLevelDto dto);
    Task<AwardLevelDto> UpdateAsync(Guid id, Guid userId, UpdateAwardLevelDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAwardTypeTargetService
{
    Task<AwardTypeTargetDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AwardTypeTargetDto>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardTypeTargetDto>> GetByScopeAsync(Guid awardTypeId, AwardScope scope);
    Task<bool> IsEmployeeEligibleAsync(Guid awardTypeId, Guid employeeId);
    Task<AwardTypeTargetDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardTypeTargetDto dto);
    Task<AwardTypeTargetDto> UpdateAsync(Guid id, Guid userId, UpdateAwardTypeTargetDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAwardBudgetService
{
    Task<AwardBudgetDto?> GetByIdAsync(Guid id);
    Task<AwardBudgetDto?> GetByYearAsync(Guid awardTypeId, int year);
    Task<AwardBudgetDto?> GetByBudgetCodeAsync(Guid tenantId, string budgetCode);
    Task<IEnumerable<AwardBudgetDto>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardBudgetDto>> GetByYearRangeAsync(Guid tenantId, int startYear, int endYear);
    Task<decimal> GetAvailableBudgetAsync(Guid awardTypeId, int year);
    Task<AwardBudgetDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardBudgetDto dto);
    Task<AwardBudgetDto> UpdateAsync(Guid id, Guid userId, UpdateAwardBudgetDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Employee Award Services

public interface IEmployeeAwardService
{
    Task<EmployeeAwardDto?> GetByIdAsync(Guid id);
    Task<EmployeeAwardDto?> GetByAwardNumberAsync(Guid tenantId, string awardNumber);
    Task<EmployeeAwardDetailDto?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetAllAsync(Guid tenantId);
    Task<PagedResult<EmployeeAwardSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? year = null, AwardStatus? status = null);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetByAwardLevelIdAsync(Guid awardLevelId);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingPresentationsAsync(Guid tenantId);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingPaymentsAsync(Guid tenantId);
    Task<IEnumerable<EmployeeAwardSummaryDto>> GetPendingLeaveProcessingAsync(Guid tenantId);
    Task<EmployeeAwardDto> CreateAsync(Guid tenantId, Guid userId, CreateEmployeeAwardDto dto);
    Task<EmployeeAwardDto> CreateFromNominationAsync(Guid nominationId, Guid userId, CreateEmployeeAwardFromNominationDto dto);
    Task<EmployeeAwardDto> UpdateAsync(Guid id, Guid userId, UpdateEmployeeAwardDto dto);
    Task DeleteAsync(Guid id);
    Task SchedulePresentationAsync(Guid userId, ScheduleAwardPresentationDto dto);
    Task RecordPresentationAsync(Guid id, Guid userId, RecordAwardPresentationDto dto);
    Task ProcessPaymentAsync(Guid userId, ProcessAwardPaymentDto dto);
    Task ProcessLeaveAsync(Guid id, Guid userId);
}

public interface IAwardAttachmentService
{
    Task<AwardAttachmentDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AwardAttachmentDto>> GetByAwardIdAsync(Guid awardId);
    Task<IEnumerable<AwardAttachmentDto>> GetByTypeAsync(Guid awardId, AwardAttachmentType type);
    Task<AwardAttachmentDto> CreateAsync(Guid tenantId, Guid awardId, Guid userId, CreateAwardAttachmentDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Award Nomination Services

public interface IAwardNominationService
{
    Task<AwardNominationDto?> GetByIdAsync(Guid id);
    Task<AwardNominationDto?> GetByNominationNumberAsync(Guid tenantId, string nominationNumber);
    Task<AwardNominationDetailDto?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<AwardNominationSummaryDto>> GetAllAsync(Guid tenantId);
    Task<PagedResult<AwardNominationSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? year = null, AwardNominationStatus? status = null);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByNomineeIdAsync(Guid nomineeId);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByNominatedByIdAsync(Guid nominatedById);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByYearAsync(Guid tenantId, int year);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByYearAndPeriodAsync(Guid tenantId, int year, int? quarter = null, int? month = null);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByStatusAsync(Guid tenantId, AwardNominationStatus status);
    Task<IEnumerable<AwardNominationSummaryDto>> GetByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<AwardNominationSummaryDto>> GetRequiringCommitteeReviewAsync(Guid tenantId);
    Task<IEnumerable<AwardNominationSummaryDto>> GetApprovedWithoutAwardAsync(Guid tenantId);
    Task<AwardNominationDto> CreateAsync(Guid tenantId, Guid nominatedById, Guid userId, CreateAwardNominationDto dto);
    Task<AwardNominationDto> UpdateAsync(Guid id, Guid userId, UpdateAwardNominationDto dto);
    Task DeleteAsync(Guid id);
    Task<AwardNominationDto> SubmitAsync(Guid id, Guid userId);
    Task<AwardNominationDto> AssignToCommitteeAsync(Guid id, Guid committeeId, Guid userId);
    Task<AwardNominationDto> SetOutcomeAsync(Guid id, Guid userId, SetNominationOutcomeDto dto);
}

public interface ITeamAwardNomineeService
{
    Task<IEnumerable<TeamAwardNomineeDto>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<TeamAwardNomineeDto>> GetByEmployeeIdAsync(Guid employeeId);
    Task<TeamAwardNomineeDto> AddAsync(Guid nominationId, Guid employeeId, Guid userId, CreateTeamAwardNomineeDto dto);
    Task UpdateAsync(Guid id, Guid userId, UpdateTeamAwardNomineeDto dto);
    Task RemoveAsync(Guid id);
}

public interface IAwardNomineeContributionService
{
    Task<IEnumerable<AwardNomineeContributionDto>> GetByNominationIdAsync(Guid nominationId);
    Task<AwardNomineeContributionDto> AddAsync(Guid nominationId, Guid userId, CreateAwardNomineeContributionDto dto);
    Task UpdateAsync(Guid id, Guid userId, UpdateAwardNomineeContributionDto dto);
    Task RemoveAsync(Guid id);
}

public interface IAwardNominationAttachmentService
{
    Task<IEnumerable<AwardNominationAttachmentDto>> GetByNominationIdAsync(Guid nominationId);
    Task<AwardNominationAttachmentDto> AddAsync(Guid nominationId, Guid uploadedById, Guid userId, CreateAwardNominationAttachmentDto dto);
    Task UpdateAsync(Guid id, Guid userId, UpdateAwardNominationAttachmentDto dto);
    Task RemoveAsync(Guid id);
}

public interface IAwardCommitteeService
{
    Task<AwardCommitteeDto?> GetByIdAsync(Guid id);
    Task<AwardCommitteeDto?> GetWithMembersAsync(Guid id);
    Task<AwardCommitteeDto?> GetActiveForDateAsync(Guid tenantId, DateTime date);
    Task<IEnumerable<AwardCommitteeDto>> GetAllAsync(Guid tenantId);
    Task<IEnumerable<AwardCommitteeDto>> GetActiveCommitteesAsync(Guid tenantId);
    Task<bool> HasQuorumAsync(Guid committeeId);
    Task<AwardCommitteeDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardCommitteeDto dto);
    Task<AwardCommitteeDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCommitteeDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAwardCommitteeMemberService
{
    Task<AwardCommitteeMemberDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AwardCommitteeMemberDto>> GetByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<AwardCommitteeMemberDto>> GetActiveByCommitteeIdAsync(Guid committeeId);
    Task<IEnumerable<AwardCommitteeMemberDto>> GetByEmployeeIdAsync(Guid employeeId);
    Task<bool> IsActiveMemberAsync(Guid committeeId, Guid employeeId);
    Task<AwardCommitteeMemberDto> AddAsync(Guid committeeId, Guid userId, CreateAwardCommitteeMemberDto dto);
    Task<AwardCommitteeMemberDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCommitteeMemberDto dto);
    Task RemoveAsync(Guid id);
    Task DeactivateAsync(Guid id, Guid userId, DateTime? endDate = null);
}

public interface IAwardCommitteeReviewService
{
    Task<AwardCommitteeReviewDto?> GetByIdAsync(Guid id);
    Task<AwardCommitteeReviewDto?> GetReviewAsync(Guid nominationId, Guid reviewerId);
    Task<IEnumerable<AwardCommitteeReviewDto>> GetByNominationIdAsync(Guid nominationId);
    Task<IEnumerable<AwardCommitteeReviewDto>> GetByReviewerIdAsync(Guid reviewerId);
    Task<IEnumerable<AwardCommitteeReviewDto>> GetPendingReviewsAsync(Guid reviewerId);
    Task<int> GetApprovalCountAsync(Guid nominationId);
    Task<int> GetRejectionCountAsync(Guid nominationId);
    Task<AwardCommitteeReviewDto> SubmitReviewAsync(Guid nominationId, Guid reviewerId, Guid userId, SubmitCommitteeReviewDto dto);
    Task<AwardCommitteeReviewDto> UpdateReviewAsync(Guid id, Guid userId, UpdateCommitteeReviewDto dto);
}

#endregion

#region Long Service Award Services

public interface ILongServiceAwardService
{
    Task<LongServiceAwardDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<LongServiceAwardSummaryDto>> GetAllAsync(Guid tenantId);
    Task<PagedResult<LongServiceAwardSummaryDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, string? searchTerm = null, int? yearsOfService = null);
    Task<IEnumerable<LongServiceAwardSummaryDto>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<LongServiceAwardSummaryDto>> GetUpcomingMilestonesAsync(Guid tenantId, int daysAhead = 90);
    Task<IEnumerable<LongServiceAwardSummaryDto>> GetPendingProcessingAsync(Guid tenantId);
    Task<LongServiceAwardDto> CreateAsync(Guid tenantId, Guid userId, CreateLongServiceAwardDto dto);
    Task<LongServiceAwardDto> UpdateAsync(Guid id, Guid userId, UpdateLongServiceAwardDto dto);
    Task DeleteAsync(Guid id);
    Task ProcessAsync(Guid userId, ProcessLongServiceAwardDto dto);
}

#endregion




#region Award Cycle & Eligibility Services

public interface IAwardCycleService
{
    Task<IEnumerable<AwardCycleSummaryDto>> GetByAwardTypeIdAsync(Guid awardTypeId);
    Task<IEnumerable<AwardCycleSummaryDto>> GetOpenForNominationAsync();
    Task<IEnumerable<AwardCycleSummaryDto>> GetOpenForVotingAsync();
    Task<AwardCycleDto?> GetByIdAsync(Guid id);
    Task<AwardCycleDto> CreateAsync(Guid tenantId, Guid userId, CreateAwardCycleDto dto);
    Task<AwardCycleDto> UpdateAsync(Guid id, Guid userId, UpdateAwardCycleDto dto);
    Task<AwardCycleDto> PublishAsync(Guid id, Guid userId);
    Task<AwardCycleDto> CancelAsync(Guid id, Guid userId, string reason);
    Task DeleteAsync(Guid id);
}

public interface IAwardEligibilityService
{
    Task<AwardEligibilityResultDto> EvaluateAsync(Guid awardTypeId, DateTime? asOf);
    Task<AwardEligibilityVerdictDto> EvaluateEmployeeAsync(Guid awardTypeId, Guid employeeId, DateTime? asOf);
}

#endregion
