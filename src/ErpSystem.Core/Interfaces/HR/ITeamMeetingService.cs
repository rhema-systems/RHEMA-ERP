using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// A team's minute book and its periodic reviews — slice F2 of the teams sub-module.
/// </summary>
/// <remarks>
/// <para>Round 2, lane F2 (plan § 6.6). Split from <see cref="ITeamActivityService"/> rather than
/// bolted onto it: F1's service is already large, and meetings and reviews share none of its
/// objective-progress machinery. Both enforce the same authorisation, through the same helpers.</para>
///
/// <para><b>⚠ Authorisation is enforced HERE, not at the route.</b> Same reasoning as F1: a team's
/// lead is very often not an HR user, so the vertical gate is "any internal user" and the
/// horizontal question — <i>is this caller anything to do with THIS team, and in what capacity?</i>
/// — is answered against the record.</para>
/// </remarks>
public interface ITeamMeetingService
{
    // ── Meetings ──────────────────────────────────────────────────────────────

    Task<IEnumerable<TeamMeetingListDto>> GetMeetingsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamMeetingDetailDto?> GetMeetingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamMeetingDetailDto> CreateMeetingAsync(Guid teamId, CreateTeamMeetingDto dto, CancellationToken cancellationToken = default);

    /// <summary>⚠ The attendee list is a REPLACE SET — an invitee omitted is an invitee removed.</summary>
    Task<TeamMeetingDetailDto> UpdateMeetingAsync(Guid id, UpdateTeamMeetingDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the meeting happened, with its minutes and who came.
    /// </summary>
    /// <remarks>
    /// ⚠ Attendance here is NOT a replace set, unlike the invitee list. The register is marked in
    /// passes — the chair ticks the room, then adds an apology that arrived late — and replacing
    /// the whole set each save would wipe the earlier pass.
    /// </remarks>
    Task<TeamMeetingDetailDto> HoldMeetingAsync(Guid id, HoldTeamMeetingDto dto, CancellationToken cancellationToken = default);

    Task<TeamMeetingDetailDto> CancelMeetingAsync(Guid id, string reason, CancellationToken cancellationToken = default);

    Task<bool> DeleteMeetingAsync(Guid id, CancellationToken cancellationToken = default);

    Task AttachMinutesDocumentAsync(
        Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default);

    Task<HrStoredFileRef?> GetMinutesDocumentRefAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Decisions ─────────────────────────────────────────────────────────────

    Task<TeamMeetingDecisionDto> AddDecisionAsync(Guid meetingId, CreateTeamMeetingDecisionDto dto, CancellationToken cancellationToken = default);

    Task<TeamMeetingDecisionDto> UpdateDecisionAsync(Guid id, UpdateTeamMeetingDecisionDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteDecisionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a decision into a team task, and links the two.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>The reason this sub-module exists at all.</b> An action item that lives only in
    /// minutes is one nobody chases. The raised task takes its ASSIGNEE and DUE DATE from the
    /// decision, not from the caller — an action item that quietly acquired a different owner from
    /// the one the meeting named would be worse than no link.</para>
    ///
    /// <para>One task per decision; a second is refused.</para>
    /// </remarks>
    Task<TeamTaskDetailDto> RaiseTaskFromDecisionAsync(Guid decisionId, RaiseTaskFromDecisionDto dto, CancellationToken cancellationToken = default);

    // ── Reviews ───────────────────────────────────────────────────────────────

    Task<IEnumerable<TeamReviewListDto>> GetReviewsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamReviewDetailDto?> GetReviewAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamReviewDetailDto> CreateReviewAsync(Guid teamId, CreateTeamReviewDto dto, CancellationToken cancellationToken = default);

    /// <summary>Edits a DRAFT. A submitted review is immutable.</summary>
    Task<TeamReviewDetailDto> UpdateReviewAsync(Guid id, UpdateTeamReviewDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates the line for one objective, snapshotting its progress at this moment.
    /// </summary>
    Task<TeamReviewLineDto> UpsertReviewLineAsync(Guid reviewId, UpsertTeamReviewLineDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteReviewLineAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Submits it. After this the review is a record and cannot be edited.</summary>
    Task<TeamReviewDetailDto> SubmitReviewAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The lead acknowledges it — says it was read, and nothing about whether they agreed.
    /// </summary>
    Task<TeamReviewDetailDto> AcknowledgeReviewAsync(Guid id, AcknowledgeTeamReviewDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteReviewAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    /// <summary>
    /// One screen's worth of "how is this team doing", assembled in one read.
    /// </summary>
    /// <remarks>
    /// ⚠ Server-side rather than six client calls: the tiles have to agree with each other, and an
    /// overdue count that disagrees with the list under it is worse than no tile.
    /// </remarks>
    Task<TeamDashboardDto> GetDashboardAsync(Guid teamId, CancellationToken cancellationToken = default);
}
