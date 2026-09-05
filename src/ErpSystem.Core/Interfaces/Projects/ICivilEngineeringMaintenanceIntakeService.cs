using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Interfaces.Projects;

/// <summary>Governed Civil overlay; Maintenance, Estate, Helpdesk, DMS and Workflow retain ownership.</summary>
public interface ICivilEngineeringMaintenanceIntakeService
{
    Task<CivilEngineeringMaintenanceIntakeLookupsDto> GetLookupsAsync(CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeDto>> ListAsync(CancellationToken token = default);
    Task<CivilEngineeringMaintenanceIntakeDto> CreateAsync(CreateCivilEngineeringMaintenanceIntakeRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceIntakeRevisionDto>> GetHistoryAsync(Guid intakeId, CancellationToken token = default);
}

public interface ICivilEngineeringMaintenanceAssessmentService
{
    Task<CivilEngineeringMaintenanceAssessmentLookupsDto> GetLookupsAsync(CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceAssessmentDto>> ListAsync(CancellationToken token = default);
    Task<CivilEngineeringMaintenanceAssessmentDto> StartAsync(Guid intakeId, StartCivilEngineeringMaintenanceAssessmentRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringMaintenanceAssessmentDto> TransitionAsync(Guid assessmentId, CivilEngineeringMaintenanceAssessmentTransitionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceAssessmentRevisionDto>> GetHistoryAsync(Guid assessmentId, CancellationToken token = default);
}

/// <summary>Cross-owner Civil costing linkage; it never creates QS, budget, requisition or contract records.</summary>
public interface ICivilEngineeringMaintenanceCostingHandoffService
{
    Task<CivilEngineeringMaintenanceCostingHandoffLookupsDto> GetLookupsAsync(CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffDto>> ListAsync(CancellationToken token = default);
    Task<CivilEngineeringMaintenanceCostingHandoffDto> CreateAsync(CreateCivilEngineeringMaintenanceCostingHandoffRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringMaintenanceCostingHandoffDto> ActAsync(Guid handoffId, CivilEngineeringMaintenanceCostingHandoffActionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceCostingHandoffRevisionDto>> GetHistoryAsync(Guid handoffId, CancellationToken token = default);
}

/// <summary>Links an awarded Civil scope to the authoritative Maintenance job-card/work-order lifecycle.</summary>
public interface ICivilEngineeringMaintenanceExecutionLinkService
{
    Task<CivilEngineeringMaintenanceExecutionLookupsDto> GetLookupsAsync(CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceExecutionLinkDto>> ListAsync(CancellationToken token = default);
    Task<CivilEngineeringMaintenanceExecutionLinkDto> CreateAsync(CreateCivilEngineeringMaintenanceExecutionLinkRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringMaintenanceExecutionLinkDto> RefreshAsync(Guid executionLinkId, RefreshCivilEngineeringMaintenanceExecutionLinkRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceExecutionLinkRevisionDto>> GetHistoryAsync(Guid executionLinkId, CancellationToken token = default);
}

/// <summary>
/// Civil completion review and directions only. Maintenance inspection/work lifecycle and Finance payment posting remain owner-controlled.
/// </summary>
public interface ICivilEngineeringMaintenanceCompletionControlService
{
    Task<CivilEngineeringMaintenanceCompletionLookupsDto> GetLookupsAsync(CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceCompletionControlDto>> ListAsync(CancellationToken token = default);
    Task<CivilEngineeringMaintenanceCompletionControlDto> CreateAsync(CreateCivilEngineeringMaintenanceCompletionControlRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringMaintenanceCompletionControlDto> ProcessAsync(Guid completionControlId, ProcessCivilEngineeringMaintenanceCompletionControlRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMaintenanceCompletionRevisionDto>> GetHistoryAsync(Guid completionControlId, CancellationToken token = default);
}

public interface ICivilEngineeringComplaintResolutionService
{
    Task<IReadOnlyList<CivilEngineeringComplaintResolutionDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CivilEngineeringComplaintResolutionTimelineEntryDto>> GetTimelineAsync(Guid helpdeskTicketId, CancellationToken cancellationToken = default);
}

/// <summary>Building Inspectorate register overlay. Workflow handoff, SCE review and HOD decision are separate Civil stages.</summary>
public interface ICivilEngineeringDevelopmentApprovalFileService
{
    Task<CivilEngineeringDevelopmentApprovalLookupsDto> GetLookupsAsync(CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalFileDto>> ListAsync(CancellationToken token = default);
    Task<CivilEngineeringDevelopmentApprovalFileDto> CreateAsync(CreateCivilEngineeringDevelopmentApprovalFileRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDevelopmentApprovalFileDto> RecordSiteInspectionAsync(Guid fileId, RecordCivilEngineeringSiteInspectionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalFileRevisionDto>> GetHistoryAsync(Guid fileId, CancellationToken token = default);
}

public interface ICivilEngineeringDevelopmentApprovalFileHandoffService
{
    Task<CivilEngineeringDevelopmentApprovalHandoffLookupsDto> GetLookupsAsync(Guid fileId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDevelopmentApprovalHandoffDto>> ListAsync(Guid fileId, CancellationToken token = default);
    Task<CivilEngineeringDevelopmentApprovalHandoffDto> CreateAsync(Guid fileId, CreateCivilEngineeringDevelopmentApprovalHandoffRequest request, string correlationId, CancellationToken token = default);
}

public interface ICivilEngineeringPermittingEngineeringReviewService
{
    Task<CivilEngineeringPermittingEngineeringReviewLookupsDto> GetLookupsAsync(Guid fileId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringPermittingEngineeringReviewDto>> ListAsync(Guid fileId, CancellationToken token = default);
    Task<CivilEngineeringPermittingEngineeringReviewDto> SubmitAsync(Guid fileId, SubmitCivilEngineeringPermittingEngineeringReviewRequest request, string correlationId, CancellationToken token = default);
}

public interface ICivilEngineeringPermittingHodDecisionService
{
    Task<IReadOnlyList<CivilEngineeringPermittingHodDecisionQueueItemDto>> ListPendingAsync(CancellationToken token = default);
    Task<CivilEngineeringPermittingHodDecisionQueueItemDto> DecideAsync(Guid engineeringReviewId, DecideCivilEngineeringPermittingReviewRequest request, string correlationId, CancellationToken token = default);
}

/// <summary>Governed Civil overlay over the authoritative Projects work-item owner.</summary>
public interface ICivilEngineeringDirectTaskService
{
    Task<CivilEngineeringDirectTaskLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDirectTaskDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringDirectTaskDto> CreateAsync(Guid projectId, CreateCivilEngineeringDirectTaskRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDirectTaskDto>> EscalateUrgentAsync(Guid projectId, EscalateCivilEngineeringUrgentTasksRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringDirectTaskFeedbackLookupsDto> GetFeedbackLookupsAsync(Guid projectId, Guid taskId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringDirectTaskFeedbackDto>> GetFeedbackAsync(Guid projectId, Guid taskId, CancellationToken token = default);
    Task<CivilEngineeringDirectTaskDto> ProcessFeedbackAsync(Guid projectId, Guid taskId, ProcessCivilEngineeringDirectTaskFeedbackRequest request, string correlationId, CancellationToken token = default);
}

/// <summary>
/// Historical-data staging for Civil Engineering. Owner modules retain all posting and
/// lifecycle ownership; this boundary validates, reconciles and signs off only.
/// </summary>
public interface ICivilEngineeringMigrationService
{
    Task<CivilEngineeringMigrationLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMigrationBatchDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<CivilEngineeringMigrationBatchDto> StageAsync(Guid projectId, StageCivilEngineeringMigrationBatchRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringMigrationBatchDto> ReconcileAsync(Guid projectId, Guid batchId, ReconcileCivilEngineeringMigrationBatchRequest request, string correlationId, CancellationToken token = default);
    Task<CivilEngineeringMigrationBatchDto> SignOffAsync(Guid projectId, Guid batchId, SignOffCivilEngineeringMigrationBatchRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<CivilEngineeringMigrationRevisionDto>> GetHistoryAsync(Guid projectId, Guid batchId, CancellationToken token = default);
}

public sealed class CivilEngineeringMaintenanceIntakeNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringMaintenanceIntakeValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceIntakeConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceAssessmentNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringMaintenanceAssessmentValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceAssessmentConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceCostingHandoffNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringMaintenanceCostingHandoffValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceCostingHandoffConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceExecutionLinkNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringMaintenanceExecutionLinkValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceExecutionLinkConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceCompletionControlNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringMaintenanceCompletionControlValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMaintenanceCompletionControlConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDevelopmentApprovalFileNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringDevelopmentApprovalFileValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDevelopmentApprovalFileConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDevelopmentApprovalFileHandoffNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringDevelopmentApprovalFileHandoffValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDevelopmentApprovalFileHandoffConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringPermittingEngineeringReviewNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringPermittingEngineeringReviewValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringPermittingEngineeringReviewConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringPermittingHodDecisionNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringPermittingHodDecisionValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringPermittingHodDecisionConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDirectTaskNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringDirectTaskValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringDirectTaskConflictException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMigrationNotFoundException(string message) : KeyNotFoundException(message);
public sealed class CivilEngineeringMigrationValidationException(string message) : InvalidOperationException(message);
public sealed class CivilEngineeringMigrationConflictException(string message) : InvalidOperationException(message);
