using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// FR-HR-181 — employee grievances and their escalation through Employee → Supervisor → HOD → HR →
/// GM Finance &amp; Administration → Managing Director → Board.
/// </summary>
/// <remarks>
/// Deliberately NOT on the workflow engine: the engine models approval, and a grievance is answered
/// rather than approved. The decision to escalate belongs to the griever, not to an approver.
/// </remarks>
public interface IStaffGrievanceService
{
    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Every grievance in the tenant. HR only — this is people's complaints about each other.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<StaffGrievanceSummaryDto>> GetByStatusAsync(GrievanceStatus status, CancellationToken cancellationToken = default);

    /// <summary>Grievances sitting unanswered at a given rung — HR's view of where the ladder is stuck.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetAwaitingResponseAsync(GrievanceEscalationLevel? level = null, CancellationToken cancellationToken = default);

    /// <summary>The caller's own grievances. Token-derived; there is no id-bearing equivalent.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetMineAsync(Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Grievances whose current step names the caller as the responder.</summary>
    Task<IEnumerable<StaffGrievanceSummaryDto>> GetAwaitingMyResponseAsync(Guid responderEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One grievance in full. Readable by the griever, by HR, and by anyone named on one of its
    /// steps — and nobody else. A grievance is usually ABOUT somebody, so the read surface is
    /// narrower than the rest of the module.
    /// </summary>
    Task<StaffGrievanceDto> GetByIdAsync(Guid id, Guid? callerEmployeeId, CancellationToken cancellationToken = default);

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <param name="grieverEmployeeId">The caller, from their token — never from the payload.</param>
    Task<StaffGrievanceDto> FileAsync(FileGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Names who should answer at the current rung. HR only.</summary>
    Task<StaffGrievanceDto> AssignCurrentStepAsync(Guid grievanceId, AssignGrievanceStepDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers at the current rung. Permitted to HR and to whoever the step names; refused to
    /// everyone else, including the griever — you cannot answer your own grievance.
    /// </summary>
    Task<StaffGrievanceDto> RespondAsync(Guid grievanceId, RespondToGrievanceDto dto, Guid responderEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the grievance up a rung. The GRIEVER'S act — refused to HR, because escalating is the
    /// employee saying the answer did not satisfy them, and nobody can say that for them.
    /// </summary>
    Task<StaffGrievanceDto> EscalateAsync(Guid grievanceId, EscalateGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>The griever withdraws. Also theirs alone.</summary>
    Task<StaffGrievanceDto> WithdrawAsync(Guid grievanceId, WithdrawGrievanceDto dto, Guid grieverEmployeeId, CancellationToken cancellationToken = default);
}
