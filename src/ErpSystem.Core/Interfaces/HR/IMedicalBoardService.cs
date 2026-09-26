using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Medical boards — convening a panel, recording its sittings, and its recommendation
/// (residue plan G4 / R-15b).
/// </summary>
/// <remarks>
/// <para><b>Lives in Medical because a ruling on fitness is a clinical record.</b> The SHE↔Medical
/// ownership boundary settles which module owns what: Medical keeps health profiles, examinations,
/// facilities and physicians, and everything else bridges by reference.</para>
///
/// <para>⚠ <b>Leave and separation READ a board; neither writes one.</b> There is deliberately no
/// method here for "satisfy a leave request" or "retire this employee". A board records what a panel
/// decided; what anybody does about it belongs to the module that acts.</para>
///
/// <para>⚠ <b>The lifecycle is a one-way ratchet</b>: Requested → Convened → Concluded, with Cancel
/// available until it concludes. There is no un-conclude. A board that needs to revisit its finding
/// is a new board, which is also how it works on paper — and it is what stops a leave request
/// having been approved on a recommendation that has since been edited away.</para>
/// </remarks>
public interface IMedicalBoardService
{
    Task<PagedResult<MedicalBoardDto>> GetBoardsAsync(
        MedicalBoardFilterDto filter, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<MedicalBoardDto?> GetBoardAsync(Guid id, CancellationToken ct = default);

    /// <summary>Asks for a board. It has no members and no finding yet.</summary>
    Task<MedicalBoardDto> RequestBoardAsync(RequestMedicalBoardDto dto, CancellationToken ct = default);

    /// <summary>
    /// Appoints somebody. ⚠ Refused once the board has concluded — its membership is part of what
    /// its recommendation means, and changing it afterwards would rewrite who decided.
    /// </summary>
    Task<MedicalBoardMemberDto> AddMemberAsync(Guid boardId, AddMedicalBoardMemberDto dto, CancellationToken ct = default);

    Task<bool> RemoveMemberAsync(Guid boardId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Moves a board from Requested to Convened. ⚠ Refused without members: a board is its panel,
    /// and one with nobody on it cannot sit.
    /// </summary>
    Task<MedicalBoardDto> ConveneAsync(Guid boardId, CancellationToken ct = default);

    Task<MedicalBoardSittingDto> RecordSittingAsync(Guid boardId, RecordMedicalBoardSittingDto dto, CancellationToken ct = default);

    /// <summary>
    /// The board reports. ⚠ This is the only status leave and separation act on, and it cannot be
    /// undone.
    /// </summary>
    Task<MedicalBoardDto> ConcludeAsync(Guid boardId, ConcludeMedicalBoardDto dto, CancellationToken ct = default);

    /// <summary>
    /// Stops a board that has not reported. ⚠ One status, two words (round 5, lane K5): a board
    /// still only Requested is a <b>cancelled request</b>; a Convened one is <b>dissolved</b>, and its
    /// members and sittings stay on the record. Either way a reason is required, and when and by
    /// whom are recorded.
    /// </summary>
    Task<MedicalBoardDto> CancelAsync(Guid boardId, string reason, CancellationToken ct = default);

    // ── Documents (round 5, lane K4) ─────────────────────────────────────────────────────────

    Task<IReadOnlyList<MedicalBoardDocumentDto>> GetDocumentsAsync(Guid boardId, CancellationToken ct = default);

    /// <summary>
    /// Records a paper the upload gate has already scanned and stored. Allowed at any status — the
    /// signed report usually arrives after the board has concluded.
    /// </summary>
    Task<MedicalBoardDocumentDto> AddDocumentAsync(
        Guid boardId, Guid uploadedById, string fileName, long? fileSize, string? description,
        Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken ct = default);

    /// <summary>The document, if it is this board's and this tenant's — for the gated download.</summary>
    Task<MedicalBoardDocument?> GetDocumentForDownloadAsync(Guid boardId, Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// ⚠ Refused once the board has reported or been stopped: its papers are then part of the
    /// record a finding rests on, like its members and sittings.
    /// </summary>
    Task<bool> RemoveDocumentAsync(Guid boardId, Guid documentId, CancellationToken ct = default);
}
