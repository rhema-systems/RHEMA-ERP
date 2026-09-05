using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The staff policy library, and the acknowledgements it collects (area 25 slice 12d, D7).
/// </summary>
/// <remarks>
/// Audience membership is resolved at read time by the shared <c>IHrAudienceResolver</c>, the
/// same as announcements — so a policy applies to whoever is in scope NOW, and the outstanding
/// roster is computed rather than pre-seeded. See <c>HrPolicyAcknowledgement</c> for why.
/// </remarks>
public interface IHrPolicyService
{
    // ── The employee's side ───────────────────────────────────────────────────

    /// <summary>Live policies that apply to this employee, with their own outcome on each.</summary>
    Task<IEnumerable<MyPolicyDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Only the ones still waiting on them — the dashboard and inbox nudge.</summary>
    Task<IEnumerable<MyPolicyDto>> GetMyOutstandingAsync(
        Guid employeeId, CancellationToken cancellationToken = default);

    Task<MyPolicyDto?> GetMineByIdAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs it. The submitted declaration must match the policy's current wording, so nobody
    /// signs text that changed between the page loading and the click.
    /// </summary>
    Task<MyPolicyDto> AcknowledgeAsync(
        Guid id, Guid employeeId, AcknowledgePolicyDto dto, string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Refuses it, with a reason. A real outcome, recorded as such.</summary>
    Task<MyPolicyDto> DeclineAsync(
        Guid id, Guid employeeId, DeclinePolicyDto dto, string? ipAddress,
        CancellationToken cancellationToken = default);

    // ── The HR desk ───────────────────────────────────────────────────────────

    Task<IEnumerable<HrPolicyDto>> GetAllAsync(
        HrPolicyStatus? status, CancellationToken cancellationToken = default);

    Task<HrPolicyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<HrPolicyDto> CreateAsync(SaveHrPolicyDto dto, CancellationToken cancellationToken = default);

    /// <summary>Edits a draft. A published policy is superseded, never rewritten.</summary>
    Task<HrPolicyDto> UpdateAsync(
        Guid id, SaveHrPolicyDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes it. Refuses a policy with no document, an acknowledgement with no declaration,
    /// or an audience that reaches nobody.
    /// </summary>
    Task<HrPolicyDto> PublishAsync(
        Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Withdraws it. Acknowledgements survive — they are evidence.</summary>
    Task<HrPolicyDto> ArchiveAsync(
        Guid id, Guid archiverEmployeeId, CancellationToken cancellationToken = default);

    Task DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Who has and has not acknowledged it — computed, never stored. The four counts always
    /// cover the whole audience; the rows are filtered and paged, because a tenant-wide policy
    /// has thousands of them and the screen's first question is "who is outstanding?".
    /// </summary>
    Task<PolicyComplianceDto?> GetComplianceAsync(
        Guid id, PolicyComplianceFilter filter = PolicyComplianceFilter.All,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task AttachDocumentAsync(
        Guid id, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default);
}
