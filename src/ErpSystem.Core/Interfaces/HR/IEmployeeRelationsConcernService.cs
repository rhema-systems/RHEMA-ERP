using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Anonymous / whistleblower intake — area 9c slice 6, decisions D-2 and D-9.
/// </summary>
/// <remarks>
/// <para><b>⚠ The one store in this module that must not know who wrote it.</b> Nothing here takes
/// an employee id from the reporter's side, and nothing sets <c>CreatedBy</c> or <c>CreatedById</c>
/// on a concern or on a reporter's message. That is sufficient only because
/// <c>ApplicationDbContext.UpdateAuditableEntities</c> stamps timestamps, the tenant and the
/// soft-delete flag and nothing else — <b>if anything ever starts stamping an actor centrally, this
/// guarantee breaks silently.</b></para>
///
/// <para><b>Anonymous means UNATTRIBUTED, not UNAUTHENTICATED (D-9).</b> The report endpoint
/// requires a valid internal token and simply never records who called. A truly public surface
/// would be the application's second anonymous endpoint and needs its own security review. The
/// honest limit, so nobody over-promises to staff: request logs and the reverse proxy still see the
/// caller.</para>
///
/// <para><b>Only the reporter is anonymous.</b> HR triaging, replying, closing or converting is
/// recorded against them by name — the desk is accountable.</para>
/// </remarks>
public interface IEmployeeRelationsConcernService
{
    /// <summary>
    /// Reports a concern. Returns the retrieval code — <b>once</b>, and never again.
    /// </summary>
    /// <remarks>
    /// Takes no actor argument at all, which is the point: there is no parameter through which a
    /// caller's identity could reach the row even by mistake.
    /// </remarks>
    Task<ConcernReceiptDto> ReportAsync(ReportConcernDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reporter's own view, unlocked by their code. ⚠ Refuses with the same message whether the
    /// number or the code is wrong, so the endpoint cannot be used to confirm a concern exists.
    /// </summary>
    Task<EmployeeRelationsConcernDto> TrackAsync(TrackConcernDto dto, CancellationToken cancellationToken = default);

    /// <summary>The reporter adds to their own thread, still nameless.</summary>
    Task<EmployeeRelationsConcernDto> AddReporterUpdateAsync(AddConcernUpdateDto dto, CancellationToken cancellationToken = default);

    // ── HR's side, all attributed ─────────────────────────────────────────────

    /// <summary>The triage queue. HR only.</summary>
    Task<IEnumerable<EmployeeRelationsConcernDto>> GetAllAsync(ConcernStatus? status = null, CancellationToken cancellationToken = default);

    Task<EmployeeRelationsConcernDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeRelationsConcernDto> TriageAsync(Guid id, TriageConcernDto dto, Guid triagedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>HR replies on the thread. Attributed, unlike the reporter's messages.</summary>
    Task<EmployeeRelationsConcernDto> ReplyAsync(Guid id, ReplyToConcernDto dto, Guid authorEmployeeId, CancellationToken cancellationToken = default);

    Task<EmployeeRelationsConcernDto> CloseAsync(Guid id, CloseConcernDto dto, Guid closedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a concern into a named employee-relations case. ⚠ Never a grievance — see
    /// <see cref="ConvertConcernDto"/>.
    /// </summary>
    Task<EmployeeRelationsConcernDto> ConvertAsync(Guid id, ConvertConcernDto dto, Guid convertedByEmployeeId, CancellationToken cancellationToken = default);
}
