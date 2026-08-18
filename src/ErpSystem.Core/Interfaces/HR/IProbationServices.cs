using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

public interface IProbationService
{
    // Queries
    Task<ProbationPeriodDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// An employee <b>whole</b> probation history, newest first.
    /// </summary>
    /// <remarks>
    /// This returned a single record until slice 1, while the endpoint above it declared
    /// <c>IEnumerable&lt;ProbationPeriodSummaryDto&gt;</c> - so the response was one object where
    /// its own signature promised an array, and any client that mapped over it threw. An employee
    /// may hold several probation periods (rehire, or a contract renewal carrying a probation
    /// clause), which is what the entity comment says and what the repository already returned.
    /// </remarks>
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>One page of the probation register.</summary>
    Task<PagedResult<ProbationPeriodSummaryDto>> GetPagedAsync(
        int page = 1, int pageSize = 25, ProbationStatus? status = null, Guid? employeeId = null,
        string? search = null, CancellationToken cancellationToken = default);
    Task<ProbationPeriodDetailDto> GetWithReviewsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetByStatusAsync(ProbationStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetActiveProbationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationPeriodSummaryDto>> GetEndingWithinAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// The probation length that applies to this employee, and where it came from (FR-HR-031).
    /// </summary>
    /// <remarks>
    /// This is what a create form should call before it renders: the length is a property of the
    /// employee's staff category, not something a user should be inventing.
    /// </remarks>
    Task<ProbationPolicyDto> GetPolicyForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    // CRUD
    Task<ProbationPeriodDto> CreateAsync(CreateProbationPeriodDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>
    /// Extends the probation period, recording a <c>ProbationExtension</c> audit row.
    /// </summary>
    /// <remarks>
    /// ⚠ This replaces the old <c>ExtendAsync</c>, which the endpoint used to call. That method
    /// moved the end date, incremented the counter, wrote <b>no audit row at all</b>, and
    /// overwrote <c>OutcomeNotes</c> with the extension reason - the same field confirm and
    /// terminate use to record their outcome. So the entity built to be the extension audit trail
    /// could only ever be empty, and <c>ExtensionCount</c> disagreed with it by construction.
    /// </remarks>
    Task<ProbationExtensionDto> ExtendAsync(Guid probationId, CreateProbationExtensionDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Confirms the probation and records the confirmation on the <b>employee</b> record.
    /// </summary>
    /// <remarks>
    /// ⚠ Every route that confirms a probation must come through here. Area 5's
    /// <c>ConfirmProbationHandler</c> used to flip the status directly, so a probation confirmed
    /// from an appraisal recommendation left the employee sitting at
    /// <c>StaffStatus.Probation</c> with no <c>ConfirmationDate</c> — the same record, confirmed
    /// two different ways, ending in two different states.
    /// </remarks>
    Task<bool> ConfirmAsync(Guid probationId, Guid confirmedByUserId, string? notes = null, CancellationToken cancellationToken = default);
    Task<bool> TerminateAsync(TerminateProbationPeriodDto dto, Guid terminatedByUserId, CancellationToken cancellationToken = default);

    // Extension audit trail
    /// <summary>
    /// Extends the probation period, records a ProbationExtension audit entry,
    /// and increments ProbationPeriod.ExtensionCount — all in one transaction.
    /// </summary>
    Task<ProbationExtensionDto> RecordExtensionAsync(CreateProbationExtensionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Every extension applied to a probation period, newest first.</summary>
    Task<IEnumerable<ProbationExtensionDto>> GetExtensionsAsync(Guid probationId, CancellationToken cancellationToken = default);

    // Reviews
    Task<ProbationReviewDto> AddReviewAsync(CreateProbationReviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetReviewsAsync(Guid probationId, CancellationToken cancellationToken = default);
    Task<ProbationReviewDto> UpdateReviewAsync(UpdateProbationReviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reviewer records what they actually found: ratings, strengths, areas for improvement,
    /// comments and a recommendation.
    /// </summary>
    /// <remarks>
    /// ⚠ Until slice 2 there was <b>no writer for any of this</b>. The review entity carries 15
    /// substantive fields; <c>AddReviewAsync</c> wrote the schedule and <c>UpdateReviewAsync</c>
    /// wrote two of them, silently discarding the rest of the payload behind a 200. A review could
    /// only ever be scheduled and then marked complete while empty. The DTO for this method
    /// (<c>SubmitProbationReviewDto</c>) already existed, unused and unreferenced.
    /// </remarks>
    Task<ProbationReviewDto> SubmitReviewAsync(Guid reviewId, SubmitProbationReviewDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The employee records that they have seen the review, and may add their own response.
    /// </summary>
    /// <remarks>
    /// ⚠ Only the employee the probation is <b>about</b> may call this - not HR, and not the
    /// reviewer. Acknowledgement is testimony, not administration: the same rule area 8 applies to
    /// accepting a movement, and the hole area 9 found in acknowledge-with-no-actor.
    /// </remarks>
    Task<ProbationReviewDto> AcknowledgeReviewAsync(Guid reviewId, AcknowledgeProbationReviewDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>HR sign-off on a completed review. The approver comes from the token.</summary>
    Task<ProbationReviewDto> HrApproveReviewAsync(Guid reviewId, ApproveProbationReviewDto dto, Guid actorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a review conducted. Refuses a review that carries no assessment.
    /// </summary>
    /// <remarks>
    /// ⚠ This used to set the status and nothing else - not even <c>ActualDate</c> - so an empty
    /// review could be completed and would then read as a conducted one for the rest of its life.
    /// </remarks>
    Task<bool> CompleteReviewAsync(Guid reviewId, Guid completedByUserId, CancellationToken cancellationToken = default);

    /// <summary>The reviews of the caller own probation, so they can see what they are asked to sign.</summary>
    Task<IEnumerable<ProbationReviewDto>> GetMyReviewsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetReviewsByStatusAsync(ProbationReviewStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetOverdueReviewsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbationReviewDto>> GetReviewsByReviewerAsync(Guid reviewerEmployeeId, CancellationToken cancellationToken = default);
}
