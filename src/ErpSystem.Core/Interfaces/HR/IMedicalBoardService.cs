using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Medical boards — a panel, the cases before it, its sittings and who attended them, and each case's
/// finding (residue plan G4; round 5, lanes K and K-II-a).
/// </summary>
/// <remarks>
/// <para><b>Lives in Medical because a ruling on fitness is a clinical record.</b> The SHE↔Medical
/// ownership boundary settles which module owns what: Medical keeps health profiles, examinations,
/// facilities and physicians, and everything else bridges by reference.</para>
///
/// <para>⚠ <b>Leave and separation READ a case; neither writes one.</b> There is deliberately no
/// method here for "satisfy a leave request" or "retire this employee". A board records what a panel
/// decided; what anybody does about it belongs to the module that acts.</para>
///
/// <para>⚠ <b>Two ratchets.</b> The board: Requested → Convened → Concluded (by itself, when its last
/// open case closes with one decided), cancellable or dissolvable until then. Each case: Listed →
/// Concluded (at a sitting, by the members present) or Withdrawn. Nothing un-concludes: a finding
/// that needs revisiting is a new case, which is also how it works on paper.</para>
/// </remarks>
public interface IMedicalBoardService
{
    Task<PagedResult<MedicalBoardDto>> GetBoardsAsync(
        MedicalBoardFilterDto filter, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<MedicalBoardDto?> GetBoardAsync(Guid id, CancellationToken ct = default);

    /// <summary>Asks for a board, with its first case. It has no members yet.</summary>
    Task<MedicalBoardDto> RequestBoardAsync(RequestMedicalBoardDto dto, CancellationToken ct = default);

    /// <summary>
    /// Another employee before the board (lane K-II-a). ⚠ Refused for somebody already before it, or
    /// sitting on it, and once the board has reported or been stopped.
    /// </summary>
    Task<MedicalBoardCaseDto> AddCaseAsync(Guid boardId, AddMedicalBoardCaseDto dto, CancellationToken ct = default);

    /// <summary>
    /// A case decided, at a sitting whose attendance holds the quorum of deciding members. ⚠ The only
    /// state leave and separation act on, and it cannot be undone. The board reports when its last open
    /// case closes.
    /// </summary>
    Task<MedicalBoardDto> ConcludeCaseAsync(Guid boardId, Guid caseId, ConcludeMedicalBoardCaseDto dto, CancellationToken ct = default);

    /// <summary>A case taken off the board without a finding. A reason is required.</summary>
    Task<MedicalBoardDto> WithdrawCaseAsync(Guid boardId, Guid caseId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Appoints somebody. ⚠ Refused once the board has reported — its membership is part of what its
    /// findings mean — and for anybody who is a case before it.
    /// </summary>
    Task<MedicalBoardMemberDto> AddMemberAsync(Guid boardId, AddMedicalBoardMemberDto dto, CancellationToken ct = default);

    Task<bool> RemoveMemberAsync(Guid boardId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Moves a board from Requested to Convened. ⚠ Refused without members, or without a case open:
    /// a board is its panel, and a panel with nobody before it has nothing to sit on.
    /// </summary>
    Task<MedicalBoardDto> ConveneAsync(Guid boardId, CancellationToken ct = default);

    /// <summary>Records a sitting and who was present (lane K-II-a).</summary>
    Task<MedicalBoardSittingDto> RecordSittingAsync(Guid boardId, RecordMedicalBoardSittingDto dto, CancellationToken ct = default);

    /// <summary>
    /// Replaces who was present at a sitting. ⚠ Refused once a case has been decided at it — its
    /// attendance is then the panel that decided.
    /// </summary>
    Task<MedicalBoardSittingDto> SetSittingAttendanceAsync(
        Guid boardId, Guid sittingId, SetMedicalBoardSittingAttendanceDto dto, CancellationToken ct = default);

    /// <summary>
    /// Stops a board that has not reported. ⚠ One status, two words (round 5, lane K5): a board
    /// still only Requested is a <b>cancelled request</b>; a Convened one is <b>dissolved</b>, and its
    /// members and sittings stay on the record. Its open cases are withdrawn with the reason.
    /// </summary>
    Task<MedicalBoardDto> CancelAsync(Guid boardId, string reason, CancellationToken ct = default);

    // ── Incapacity and compensation (round 5, lane K-II-b; PNDCL 187) ────────────────────────

    /// <summary>
    /// Records the incapacity assessment of a case, replacing the last, and works out the indicative
    /// figure. ⚠ Refused on a withdrawn case, a stopped board, and once the labour officer's amount is
    /// recorded against the assessment.
    /// </summary>
    Task<MedicalBoardDto> AssessIncapacityAsync(Guid boardId, Guid caseId, AssessIncapacityDto dto, CancellationToken ct = default);

    /// <summary>Records the labour officer's notified amount (s.35) and any agreement (s.15).</summary>
    Task<MedicalBoardDto> RecordCompensationAsync(Guid boardId, Guid caseId, RecordCompensationDto dto, CancellationToken ct = default);

    /// <summary>
    /// Names the SHE incident an injury-on-duty case rests on, or clears it with null. ⚠ The incident
    /// must name the employee among the people involved.
    /// </summary>
    Task<MedicalBoardDto> LinkSafetyIncidentAsync(Guid boardId, Guid caseId, Guid? incidentId, CancellationToken ct = default);

    /// <summary>The tenant's compensation schedule.</summary>
    Task<IReadOnlyList<IncapacityScheduleItemDto>> GetScheduleAsync(bool includeInactive, CancellationToken ct = default);

    /// <summary>
    /// Adds PNDCL 187's First and Third Schedule rows the tenant does not already have (matched on
    /// schedule and injury). ⚠ Edited rows are left alone, so a second load changes nothing.
    /// </summary>
    Task<IReadOnlyList<IncapacityScheduleItemDto>> LoadDefaultScheduleAsync(CancellationToken ct = default);

    Task<IncapacityScheduleItemDto> AddScheduleItemAsync(SaveIncapacityScheduleItemDto dto, CancellationToken ct = default);

    Task<IncapacityScheduleItemDto> UpdateScheduleItemAsync(Guid itemId, SaveIncapacityScheduleItemDto dto, CancellationToken ct = default);

    // ── Documents (round 5, lane K4) ─────────────────────────────────────────────────────────

    Task<IReadOnlyList<MedicalBoardDocumentDto>> GetDocumentsAsync(Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Records a paper the upload gate has already scanned and stored — about the board, or one of its
    /// cases (lane K-II-a). Allowed at any status: the signed report usually arrives afterwards.
    /// </summary>
    Task<MedicalBoardDocumentDto> AddDocumentAsync(
        Guid boardId, Guid? caseId, Guid uploadedById, string fileName, long? fileSize, string? description,
        Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken ct = default);

    /// <summary>Checks a case is this board's before anything is uploaded against it.</summary>
    Task EnsureCaseOnBoardAsync(Guid boardId, Guid caseId, CancellationToken ct = default);

    /// <summary>The document, if it is this board's and this tenant's — for the gated download.</summary>
    Task<MedicalBoardDocument?> GetDocumentForDownloadAsync(Guid boardId, Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// ⚠ Refused once the board has reported or been stopped: its papers are then part of the
    /// record a finding rests on, like its members and sittings.
    /// </summary>
    Task<bool> RemoveDocumentAsync(Guid boardId, Guid documentId, CancellationToken ct = default);
}
