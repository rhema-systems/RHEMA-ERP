using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// What a team or committee is chartered to do, what it has undertaken, and who is doing it.
/// </summary>
/// <remarks>
/// <para>Round 2, lane F1 (plan § 1.4, § 6.6). Hangs off the existing <c>Team</c> record; see
/// <see cref="Entities.HR.TeamTermsOfReference"/> for why committees are not a separate entity.</para>
///
/// <para><b>⚠ Authorisation is enforced HERE, not only at the controller.</b> The routes carry a
/// vertical gate (HR write, or plain <c>[Authorize]</c> for the member-scoped ones); the horizontal
/// question — "is this caller anything to do with THIS team?" — can only be answered against the
/// record, so every method below asks it. A controller policy alone would let any HR-writing user
/// edit any team's tasks, and any authenticated employee tick any team's checklist.</para>
/// </remarks>
public interface ITeamActivityService
{
    // ── Terms of reference ────────────────────────────────────────────────────

    Task<IEnumerable<TeamTermsOfReferenceListDto>> GetTermsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamTermsOfReferenceDetailDto?> GetTermsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamTermsOfReferenceDetailDto> CreateTermsAsync(Guid teamId, CreateTeamTermsOfReferenceDto dto, CancellationToken cancellationToken = default);

    /// <summary>Edits a DRAFT. An approved version is immutable — take a new one instead.</summary>
    Task<TeamTermsOfReferenceDetailDto> UpdateTermsAsync(Guid id, UpdateTeamTermsOfReferenceDto dto, CancellationToken cancellationToken = default);

    Task<TeamTermsOfReferenceDetailDto> SubmitTermsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Approves it, and supersedes whatever approved version the team held before.</summary>
    Task<TeamTermsOfReferenceDetailDto> ApproveTermsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Declines it, sending it back to Draft with a REQUIRED reason (lane F3).</summary>
    Task<TeamTermsOfReferenceDetailDto> RejectTermsAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    /// <summary>The committee withdrawing its own submission before anyone has ruled (lane F3).</summary>
    Task<TeamTermsOfReferenceDetailDto> RecallTermsAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Clones an approved version to a new Draft at version + 1.</summary>
    Task<TeamTermsOfReferenceDetailDto> NewTermsVersionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteTermsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Records the signed charter after the upload gate has stored it.</summary>
    Task AttachTermsDocumentAsync(
        Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default);

    // ── Objectives ────────────────────────────────────────────────────────────

    Task<IEnumerable<TeamObjectiveListDto>> GetObjectivesAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<TeamObjectiveDetailDto?> GetObjectiveByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamObjectiveDetailDto> CreateObjectiveAsync(Guid teamId, CreateTeamObjectiveDto dto, CancellationToken cancellationToken = default);

    Task<TeamObjectiveDetailDto> UpdateObjectiveAsync(Guid id, UpdateTeamObjectiveDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an objective. Completing below 100 % requires an outcome summary; cancelling requires
    /// a reason.
    /// </summary>
    Task<TeamObjectiveDetailDto> ChangeObjectiveStatusAsync(
        Guid id, TeamObjectiveStatus status, TeamObjectiveStatusChangeDto body, CancellationToken cancellationToken = default);

    // ── Approval, on the workflow engine (lane F3) ────────────────────────────
    //
    // ⚠ These four are the ONLY way an objective reaches Active from Draft.
    // ChangeObjectiveStatusAsync refuses that transition by hand, or the approval would be
    // decorative — a lead who found it inconvenient could simply set the status themselves.

    Task<TeamObjectiveDetailDto> SubmitObjectiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamObjectiveDetailDto> ApproveObjectiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamObjectiveDetailDto> RejectObjectiveAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    Task<TeamObjectiveDetailDto> RecallObjectiveAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    Task<bool> DeleteObjectiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the team's ACTIVE objective weights sum to 100, and what they do sum to.
    /// </summary>
    /// <remarks>
    /// ⚠ Advisory, never a refusal — a team mid-planning has every right to a half-built set. The
    /// screen shows it; nothing blocks on it.
    /// </remarks>
    Task<(int Total, bool IsBalanced)> GetObjectiveWeightTotalAsync(Guid teamId, CancellationToken cancellationToken = default);

    // ── Tasks ─────────────────────────────────────────────────────────────────

    Task<IEnumerable<TeamTaskListDto>> GetTasksAsync(
        Guid teamId, Guid? objectiveId = null, bool mineOnly = false, CancellationToken cancellationToken = default);

    Task<TeamTaskDetailDto?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamTaskDetailDto> CreateTaskAsync(Guid teamId, CreateTeamTaskDto dto, CancellationToken cancellationToken = default);

    Task<TeamTaskDetailDto> UpdateTaskAsync(Guid id, UpdateTeamTaskDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a task. Blocking requires a reason; completing stamps the date.
    /// </summary>
    /// <remarks>
    /// ⚠ The one door an ordinary MEMBER may use on their own task, which is why it is separate
    /// from the update: progressing work you were assigned is not the same privilege as rewriting
    /// the task record.
    /// </remarks>
    Task<TeamTaskDetailDto> ChangeTaskStatusAsync(Guid id, TeamTaskStatusChangeDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Checklist ─────────────────────────────────────────────────────────────

    Task<TeamTaskChecklistItemDto> AddChecklistItemAsync(Guid taskId, CreateTeamTaskChecklistItemDto dto, CancellationToken cancellationToken = default);

    Task<TeamTaskChecklistItemDto> UpdateChecklistItemAsync(Guid id, UpdateTeamTaskChecklistItemDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteChecklistItemAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Attachments ───────────────────────────────────────────────────────────

    /// <summary>Records a task attachment after the upload gate has stored it.</summary>
    Task<TeamTaskAttachmentDto> AddTaskAttachmentAsync(
        Guid taskId, string? title, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default);

    Task<TeamTaskAttachmentDto?> GetTaskAttachmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteTaskAttachmentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The three DMS ids and the file's name behind a stored document, for the download gate.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately NOT on the read DTOs. A screen has no business knowing a document record id —
    /// it asks for the file by the row's own id and the gate resolves it. Exposing the ids on the
    /// DTO would put a DMS handle in every browser that renders the tab.
    /// </remarks>
    Task<HrStoredFileRef?> GetTermsDocumentRefAsync(Guid id, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="GetTermsDocumentRefAsync"/>
    Task<HrStoredFileRef?> GetTaskAttachmentRefAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the caller may READ this team's activity — HR, or a member of it.
    /// </summary>
    /// <remarks>Exposed so an upload controller can check entitlement before touching storage.</remarks>
    Task<bool> CanReadTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the download gate needs to serve a file: the three DMS identifiers plus what to call it.
/// </summary>
/// <remarks>
/// A record rather than a tuple because four call sites read it by name, and a five-part tuple read
/// positionally is how a mime type ends up in a file-name slot.
/// </remarks>
public sealed record HrStoredFileRef(
    Guid? DocumentRecordId,
    Guid? DocumentVersionId,
    Guid? FileUploadRecordId,
    string? FileName,
    string? MimeType);
