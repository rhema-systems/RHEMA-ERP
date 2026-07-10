using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public sealed class WorkflowSignatureSubmissionStore : IWorkflowSignatureSubmissionStore
{
    private readonly ApplicationDbContext _db;
    public WorkflowSignatureSubmissionStore(ApplicationDbContext db) => _db = db;

    public async Task<WorkflowSignatureSubmissionDto?> GetPendingAsync(Guid approvalId, Guid signerUserId,
        CancellationToken cancellationToken = default)
    {
        var json = await _db.WorkflowSignatureEvidence.AsNoTracking()
            .Where(item => item.ApprovalId == approvalId && item.SignerUserId == signerUserId &&
                !item.IsCommitted && !item.IsDeleted)
            .Select(item => item.SubmissionJson).FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<WorkflowSignatureSubmissionDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task CommitAsync(Guid approvalId, Guid signerUserId, CancellationToken cancellationToken = default)
    {
        var signature = await _db.WorkflowSignatureEvidence.FirstOrDefaultAsync(item =>
            item.ApprovalId == approvalId && item.SignerUserId == signerUserId && !item.IsDeleted, cancellationToken);
        if (signature == null) return;
        signature.IsCommitted = true;
        signature.CommittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
