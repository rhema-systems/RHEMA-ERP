using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Shared source-bound AP-006 evidence validator used both when an exception is
/// finally approved and whenever the invoice matcher later consumes it.
/// </summary>
internal static class VendorInvoiceMatchExceptionEvidenceValidator
{
    public static async Task RevalidateAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        VendorInvoice invoice,
        IEnumerable<VendorInvoiceMatchExceptionEvidence> evidence,
        CancellationToken cancellationToken)
    {
        var activeEvidence = evidence.Where(item => !item.IsDeleted).ToList();
        if (activeEvidence.Count == 0)
            throw Conflict("The approved match exception no longer retains active evidence.");

        foreach (var row in activeEvidence)
        {
            if (row.ReferenceKind ==
                VendorInvoiceMatchExceptionEvidenceKind.WorkflowEvidenceDocument)
            {
                var current = await (
                    from document in unitOfWork.Repository<WorkflowEvidenceDocument>()
                        .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
                        .AsNoTracking()
                    join step in unitOfWork.Repository<WorkflowStepInstance>()
                        .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
                        .AsNoTracking()
                        on document.StepInstanceId equals step.Id
                    join workflow in unitOfWork.Repository<WorkflowInstance>()
                        .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
                        .AsNoTracking()
                        on step.WorkflowInstanceId equals workflow.Id
                    where document.Id == row.WorkflowEvidenceDocumentId &&
                          (workflow.EntityId == invoice.Id ||
                           workflow.EntityId == invoice.PurchaseOrderId)
                    select document).SingleOrDefaultAsync(cancellationToken);
                if (current == null || !current.IsCurrent ||
                    current.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
                    current.MalwareScanStatus != WorkflowMalwareScanStatus.Clean ||
                    !string.Equals(current.Sha256, row.EvidenceHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw Conflict(
                        "Linked workflow evidence is missing, stale, unverified, unsafe, or changed.");
                continue;
            }

            var dms = await (
                from version in unitOfWork.Repository<CentralDocumentVersion>()
                    .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                                          item.DocumentRecord.TenantId == tenantId &&
                                          !item.DocumentRecord.IsDeleted)
                    .AsNoTracking()
                join upload in unitOfWork.Repository<FileUploadRecord>()
                    .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
                    .AsNoTracking()
                    on version.FileUploadRecordId equals upload.Id
                where version.FileUploadRecordId == row.FileUploadRecordId &&
                      (version.DocumentRecord.SourceRecordId == invoice.Id ||
                       version.DocumentRecord.SourceRecordId == invoice.PurchaseOrderId)
                select new
                {
                    UploadId = upload.Id,
                    version.DocumentRecordId,
                    VersionId = version.Id,
                    upload.FilePath,
                    upload.FileSize,
                    upload.ScannedAtUtc,
                    upload.VirusScanStatus
                }).SingleOrDefaultAsync(cancellationToken);
            var currentHash = dms == null ? null : VendorInvoiceMatchExceptionRules.Hash(new
            {
                Id = dms.UploadId,
                dms.DocumentRecordId,
                dms.VersionId,
                dms.FilePath,
                dms.FileSize,
                dms.ScannedAtUtc
            });
            if (dms == null || dms.VirusScanStatus != FileVirusScanStatus.Clean ||
                !string.Equals(currentHash, row.EvidenceHash,
                    StringComparison.OrdinalIgnoreCase))
                throw Conflict(
                    "Linked central-DMS evidence is missing, unsafe, relinked, or changed.");
        }
    }

    private static VendorInvoiceMatchExceptionControlException Conflict(string message) =>
        new("AP_MATCH_EXCEPTION_EVIDENCE_STALE", message, 409);
}
