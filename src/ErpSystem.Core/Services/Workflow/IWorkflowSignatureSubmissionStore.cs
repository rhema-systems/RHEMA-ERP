using ErpSystem.Core.DTOs.Workflow;

namespace ErpSystem.Core.Services.Workflow;

public interface IWorkflowSignatureSubmissionStore
{
    Task<WorkflowSignatureSubmissionDto?> GetPendingAsync(Guid approvalId, Guid signerUserId,
        CancellationToken cancellationToken = default);
    Task CommitAsync(Guid approvalId, Guid signerUserId, CancellationToken cancellationToken = default);
}
