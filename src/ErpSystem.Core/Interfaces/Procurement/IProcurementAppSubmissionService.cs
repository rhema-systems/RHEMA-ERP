using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementAppSubmissionService
{
    Task<ProcurementAppSubmissionSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementAppSubmissionPlanOptionDto>> GetPublishedPlanOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionPageDto> SearchAsync(ProcurementAppSubmissionSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionDto> RecordExportAsync(RecordProcurementAppExportRequest request, string causationId, CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionDto> SubmitAsync(Guid id, SubmitProcurementAppRequest request, string causationId, CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionDto> AcknowledgeAsync(Guid id, AcknowledgeProcurementAppRequest request, string causationId, CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionDto> RejectAsync(Guid id, RejectProcurementAppRequest request, string causationId, CancellationToken cancellationToken = default);
    Task<ProcurementAppSubmissionDto> ResubmitAsync(Guid id, ResubmitProcurementAppRequest request, string causationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementAppSubmissionNotFoundException(string message) : InvalidOperationException(message);
public sealed class ProcurementAppSubmissionConflictException(string message) : InvalidOperationException(message);
public sealed class ProcurementAppSubmissionAuthorizationException(string message) : UnauthorizedAccessException(message);
public sealed class ProcurementAppSubmissionValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
