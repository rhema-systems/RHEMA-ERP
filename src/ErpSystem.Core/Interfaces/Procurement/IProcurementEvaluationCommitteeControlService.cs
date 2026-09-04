using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementEvaluationCommitteeControlService
{
    Task<ProcurementEvaluationCommitteeReadinessDto> GetReadinessAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationCommitteeOptionsDto> GetOptionsAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationCommitteeDto> GetAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationCommitteeDto> BindAsync(
        BindProcurementEvaluationCommitteeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationCommitteeDto> ActivateAsync(
        Guid committeeControlId,
        ActivateProcurementEvaluationCommitteeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationCommitteeDto> RetireDraftAsync(
        Guid committeeControlId,
        RetireProcurementEvaluationCommitteeDraftRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationAppointmentDto> RespondToAppointmentAsync(
        Guid appointmentId,
        RespondProcurementEvaluationAppointmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationAppointmentDto> SubmitConflictDeclarationAsync(
        Guid appointmentId,
        SubmitProcurementEvaluationConflictDeclarationRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationMeetingDto> CreateMeetingAsync(
        Guid committeeControlId,
        CreateProcurementEvaluationMeetingRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationAttendanceDto> SignAttendanceAsync(
        Guid meetingId,
        SignProcurementEvaluationAttendanceRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationMeetingDto> ConfirmQuorumAsync(
        Guid meetingId,
        ConfirmProcurementEvaluationQuorumRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationScorerEligibilityDto> EnsureScorerEligibleAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        ProcurementEvaluationPhase phase,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationScorerEligibilityDto> EnsureScoreSubjectEligibleAsync(
        ProcurementEvaluationSourceType sourceType,
        Guid sourceId,
        ProcurementEvaluationPhase phase,
        string scoreSubjectType,
        Guid scoreSubjectId,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationScoreSheetDto> LockScoreSheetAsync(
        LockProcurementEvaluationScoreSheetRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationScoreRecallDto> RequestScoreRecallAsync(
        Guid scoreSheetId,
        RequestProcurementEvaluationScoreRecallRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
    Task<ProcurementEvaluationScoreRecallDto> DecideScoreRecallAsync(
        Guid recallId,
        DecideProcurementEvaluationScoreRecallRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcurementEvaluationCommitteeNotFoundException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementEvaluationCommitteeConflictException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementEvaluationCommitteeValidationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementEvaluationCommitteeAuthorizationException(string message)
    : UnauthorizedAccessException(message);
