using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Staff announcements: HR writes and publishes, employees read (area 25 slice 12c, D7).
/// </summary>
/// <remarks>
/// Audience membership is evaluated at READ time, not frozen at publish. An employee who
/// transfers into Operations on Monday should see the notice Operations was sent on Friday, and
/// one who leaves should stop seeing it — freezing a recipient list would get both wrong.
/// </remarks>
public interface IHrAnnouncementService
{
    // ── The employee's side ───────────────────────────────────────────────────

    /// <summary>Live announcements aimed at this employee: pinned first, then newest.</summary>
    Task<IEnumerable<MyAnnouncementDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>The dashboard cut — the same list, capped.</summary>
    Task<IEnumerable<MyAnnouncementDto>> GetMineForDashboardAsync(
        Guid employeeId, int limit = 3, CancellationToken cancellationToken = default);

    /// <summary>One announcement, if it is live and aimed at this employee. Otherwise null.</summary>
    Task<MyAnnouncementDto?> GetMineByIdAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default);

    // ── The HR desk ───────────────────────────────────────────────────────────

    Task<IEnumerable<HrAnnouncementDto>> GetAllAsync(
        HrAnnouncementStatus? status, CancellationToken cancellationToken = default);

    Task<HrAnnouncementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<HrAnnouncementDto> CreateAsync(
        CreateHrAnnouncementDto dto, CancellationToken cancellationToken = default);

    /// <summary>Edits a draft. A published announcement's text is not rewritten in place —
    /// archive it and publish a correction, so what people were told stays findable.</summary>
    Task<HrAnnouncementDto> UpdateAsync(
        Guid id, UpdateHrAnnouncementDto dto, CancellationToken cancellationToken = default);

    /// <summary>Publishes it. Refuses an audience that reaches nobody.</summary>
    Task<HrAnnouncementDto> PublishAsync(
        Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default);

    Task<HrAnnouncementDto> ArchiveAsync(
        Guid id, Guid archiverEmployeeId, CancellationToken cancellationToken = default);

    Task DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many employees a rule set reaches, for the sender to see before publishing.
    /// </summary>
    Task<int> PreviewAudienceCountAsync(
        IEnumerable<HrAnnouncementAudienceDto> audiences, CancellationToken cancellationToken = default);

    /// <summary>Records an uploaded attachment against an announcement.</summary>
    Task AttachFileAsync(
        Guid id, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default);
}
