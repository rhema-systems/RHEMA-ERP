using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 1: CORE TRAVEL REQUEST SERVICE
// ============================================================================

#region Staff Travel Request Service

public interface IStaffTravelRequestService
{
    // Queries
    Task<StaffTravelRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffTravelRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<StaffTravelRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByStatusAsync(StaffTravelRequestStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByDateRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetUpcomingTripsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestSummaryDto>> GetChildRequestsAsync(Guid parentRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The approved policy a trip would be checked against — the traveller's own unit, the two
    /// countries, the departure date — for the request form, before anything is saved (T-16).
    /// </summary>
    Task<StaffTravelPolicyPreviewDto> GetPolicyPreviewAsync(Guid employeeId, DateOnly departure, Guid? originCountryId, Guid? destinationCountryId, CancellationToken cancellationToken = default);

    // Dashboard
    Task<StaffTravelDashboardDto> GetDashboardAsync(int upcomingDays = 30, CancellationToken cancellationToken = default);

    // CRUD
    Task<StaffTravelRequestDto> CreateAsync(CreateStaffTravelRequestDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelRequestDto> UpdateAsync(UpdateStaffTravelRequestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>
    /// Sends a Draft or returned request for approval, once everything that must hold does (lane 1);
    /// returns where it now is, the policy it was checked against and any warnings.
    /// </summary>
    Task<StaffTravelSubmitResultDto> SubmitAsync(SubmitStaffTravelRequestDto submitDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the stage the request is on (lane 2, D-7): the line-manager stage is the traveller's line
    /// authority's — the travel desk's only when no line authority can sign in — and HR's stage sets the
    /// approved budget.
    /// </summary>
    /// <param name="callerIsTravelDesk">Whether the caller holds <c>HR.Travel.Write</c> — evaluated by the
    /// API's policy pipeline, never taken from the request body.</param>
    Task<bool> ApproveAsync(ApproveStaffTravelRequestDto approveDto, bool callerIsTravelDesk, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(Guid requestId, Guid rejectedByUserId, string? reason, bool callerIsTravelDesk, CancellationToken cancellationToken = default);
    /// <summary>
    /// Cancels a request. <c>CancelledById</c> on the DTO is the <b>employee</b> who cancelled (an
    /// Employee FK on the entity); <paramref name="cancelledByUserId"/> is the platform user, for
    /// the audit trail. Different identifiers — the DTO field used to serve both.
    /// </summary>
    Task<bool> CancelAsync(CancelStaffTravelRequestDto cancelDto, Guid cancelledByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lane 6 (D-35): refuses — naming why — when a leg's driver's own request is live and could not be cancelled (under
    /// way, advance cash out, a committed booking). Nothing when there is none. Called before Fleet's cancel saves (G3).
    /// </summary>
    Task RequireDriverRequestCancellableAsync(Guid? driverRequestId, string tripNumber, CancellationToken cancellationToken = default);

    /// <summary>Lane 6 (D-35): cancels a leg's driver's own request, if it is live — with the leg.</summary>
    Task CancelDriverRequestAsync(
        Guid? driverRequestId, string reason, Guid cancelledById, Guid cancelledByUserId, CancellationToken cancellationToken = default);
    Task<bool> MarkCompletedAsync(Guid requestId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>An approver sends a submitted request back to its requester, with what to change (D-6).</summary>
    Task<bool> ReturnForRevisionAsync(Guid requestId, string reason, bool callerIsTravelDesk, CancellationToken cancellationToken = default);

    // The approver's door (lane 2, D-7, finding O-1)
    /// <summary>
    /// Whether the caller may open the request without the travel read permission: they are the
    /// traveller's line authority, or it is waiting for their decision now.
    /// </summary>
    Task<bool> CanOpenAsApproverAsync(Guid requestId, bool callerIsTravelDesk, CancellationToken cancellationToken = default);

    /// <summary>What the caller may decide on the request, at which stage, and as whom.</summary>
    Task<StaffTravelViewerActionsDto> GetViewerActionsAsync(Guid requestId, bool callerIsTravelDesk, CancellationToken cancellationToken = default);
    /// <summary>
    /// The active destination alerts over the trip's dates — its country, and its city or country-wide — most severe
    /// first (lane 5, T-45). Shown as a warning to whoever approves or books the trip; whether one should block is TDC's
    /// question.
    /// </summary>
    Task<IEnumerable<StaffTravelAlertSummaryDto>> GetDestinationAlertsAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>The submitted requests waiting for the caller's decision — asked of the engine, then of the line rule.</summary>
    Task<PagedResult<StaffTravelApprovalQueueItemDto>> GetMyPendingApprovalsAsync(int pageNumber, int pageSize, bool callerIsTravelDesk, CancellationToken cancellationToken = default);

    /// <summary>A change to an approved trip: back to the requester, then approved again (D-9).</summary>
    Task<bool> RequestChangeAsync(Guid requestId, string reason, CancellationToken cancellationToken = default);

    /// <summary>The traveller or whoever raised it withdraws a submitted request, back to Draft.</summary>
    Task<bool> RecallAsync(Guid requestId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Closes a completed trip once every claim and advance on it is finished (D-6).</summary>
    Task<bool> CloseAsync(Guid requestId, CancellationToken cancellationToken = default);

    // Comment operations
    /// <summary>
    /// Adds a comment. <paramref name="authorEmployeeId"/> is the caller's employee record and
    /// overrides any <c>AuthorId</c> on the payload — authorship is identity, not an input.
    /// </summary>
    Task<StaffTravelRequestCommentDto> AddCommentAsync(CreateStaffTravelRequestCommentDto createDto, Guid tenantId, Guid createdByUserId, Guid authorEmployeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestCommentDto>> GetCommentsAsync(Guid requestId, CancellationToken cancellationToken = default);
    /// <summary>Only the comment's author, or a travel administrator (<paramref name="callerIsTravelAdmin"/>), may edit it.</summary>
    Task<StaffTravelRequestCommentDto> UpdateCommentAsync(UpdateStaffTravelRequestCommentDto updateDto, Guid updatedByUserId, bool callerIsTravelAdmin, CancellationToken cancellationToken = default);
    /// <summary>Only the comment's author, or a travel administrator (<paramref name="callerIsTravelAdmin"/>), may delete it.</summary>
    Task<bool> DeleteCommentAsync(Guid commentId, bool callerIsTravelAdmin, CancellationToken cancellationToken = default);

    // Attachment operations
    /// <summary>
    /// Adds an attachment. <paramref name="uploaderEmployeeId"/> is the caller's employee record and
    /// overrides any <c>UploadedById</c> on the payload.
    /// </summary>
    Task<StaffTravelRequestAttachmentDto> AddAttachmentAsync(CreateStaffTravelRequestAttachmentDto createDto, Guid tenantId, Guid createdByUserId, Guid uploaderEmployeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRequestAttachmentDto>> GetAttachmentsAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    // Group travel operations
    Task<StaffGroupTravelDto> CreateGroupTravelAsync(CreateStaffGroupTravelDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffGroupTravelDto> GetGroupTravelByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffGroupTravelSummaryDto>> GetAllGroupTravelsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffGroupTravelSummaryDto>> GetGroupTravelsByStatusAsync(GroupTravelStatus status, CancellationToken cancellationToken = default);
    Task<StaffGroupTravelDto> UpdateGroupTravelAsync(UpdateStaffGroupTravelDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteGroupTravelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a draft travel request per selected employee, linked to the group, and returns the refreshed group.</summary>
    /// <summary>
    /// Adds participants to a group trip, raising a request for each.
    /// <paramref name="initiatorEmployeeId"/> becomes each request's <c>InitiatedById</c>, which is
    /// an Employee FK — it previously received the platform user id.
    /// </summary>
    Task<StaffGroupTravelDto> AddGroupParticipantsAsync(AddGroupTravelParticipantsDto dto, Guid tenantId, Guid createdByUserId, Guid initiatorEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Unlinks a participant's request from the group (the request itself is retained).</summary>
    Task<bool> RemoveGroupParticipantAsync(Guid groupTravelId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Puts an existing draft (or returned) request on the group, aligned to its destination and dates.</summary>
    Task<StaffGroupTravelDto> LinkGroupParticipantAsync(Guid groupTravelId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Opens a group to travellers (from Planning, or reopens a closed one).</summary>
    Task<StaffGroupTravelDto> OpenGroupTravelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Closes a group to new travellers.</summary>
    Task<StaffGroupTravelDto> CloseGroupTravelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Calls a group off once none of its travellers has a trip still going ahead.</summary>
    Task<StaffGroupTravelDto> CancelGroupTravelAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
