using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorPaymentService
{
    private const int MaximumPaymentEvidenceBytes = 25 * 1024 * 1024;
    private readonly IControlledFileUploadService? _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService? _centralDocuments;

    private async Task<VendorPayment> LoadEvidencePaymentAsync(Guid id, FinanceAccessLevel access, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) throw new UnauthorizedAccessException("An authenticated payment user is required.");
        var payment = await _unitOfWork.Repository<VendorPayment>().GetQueryable(item =>
            item.TenantId == TenantId && item.Id == id && !item.IsDeleted).AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("The payment was not found.");
        await _financeAccessScopeService.EnsureBankAccountAccessAsync(
            await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, ct), access, ct);
        return payment;
    }

    private async Task EnsurePaymentEvidenceEditableAsync(VendorPayment payment)
    {
        if (payment.Status != VendorPaymentStatus.Draft || payment.PaymentBatchId.HasValue ||
            payment.WorkflowInstanceId.HasValue || payment.JournalEntryId.HasValue ||
            await IsPaymentApprovalRequiredAsync("VendorPayment", payment.Id))
            throw new InvalidOperationException("Payment attachments can be added here only to a direct draft with no active approval process. Refresh the payment to see its current route.");
    }

    public async Task<VendorPaymentEvidenceDocumentDto> UploadEvidenceAsync(
        Guid id, VendorPaymentEvidenceUploadDto request, CancellationToken cancellationToken = default)
    {
        var payment = await LoadEvidencePaymentAsync(id, FinanceAccessLevel.Operate, cancellationToken);
        if (_controlledFiles == null || _centralDocuments == null || _approvalPolicyResolver == null || _financeAuditService == null)
            throw new InvalidOperationException("Controlled document storage and payment policy/audit services must be configured.");
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Upload the payment attachment as a separate request before completing the payment.");
        if (request.ClientRequestId == Guid.Empty || request.Content.Length == 0 || request.Content.Length > MaximumPaymentEvidenceBytes ||
            string.IsNullOrWhiteSpace(request.RequirementKey) || request.RequirementKey.Length > 150 ||
            string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 255 || request.ContentType.Length > 200)
            throw new InvalidOperationException("Choose a file up to 25 MB and a valid payment document requirement.");
        var key = request.RequirementKey.Trim();
        var checksum = Convert.ToHexString(SHA256.HashData(request.Content));
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { PaymentId = id, RequirementKey = key, request.FileName, request.ContentType, request.ExpiryDate, Checksum = checksum }))));
        var previous = await _unitOfWork.Repository<VendorPaymentEvidenceLink>().GetQueryable(item =>
            item.TenantId == TenantId && item.ClientRequestId == request.ClientRequestId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (previous != null)
        {
            if (previous.VendorPaymentId != id || previous.IsDeleted || previous.RequestHash != requestHash)
                throw new InvalidOperationException("This upload request was already used for a different payment document.");
            var saved = await PaymentEvidenceQuery(id).SingleAsync(item => item.Id == previous.Id, cancellationToken);
            return MapPaymentEvidence(saved, (!saved.ExpiryDate.HasValue || saved.ExpiryDate.Value >= DateTime.UtcNow) &&
                await OpenValidatedPaymentEvidenceBytesAsync(saved, true, cancellationToken) != null);
        }
        await EnsurePaymentEvidenceEditableAsync(payment);
        var resolution = await _approvalPolicyResolver.ResolveAsync(new WorkflowApprovalPolicyContext(
            TenantId, "Vendor Payment", DateTime.UtcNow, Module: "Finance", Category: payment.PaymentMethod.ToString(),
            Amount: decimal.Round(payment.TotalAmount * payment.ExchangeRate, 2, MidpointRounding.AwayFromZero),
            CurrencyCode: (await _tenantSettingsService.GetBaseCurrencyAsync()).Trim().ToUpperInvariant()), cancellationToken);
        if (resolution?.ApprovalConfig.EvidenceRequirements.Any(item =>
                string.Equals(item.RequirementKey, key, StringComparison.OrdinalIgnoreCase)) != true)
            throw new InvalidOperationException("This document requirement is not part of the payment's current policy. Refresh the payment.");
        if (request.ExpiryDate.HasValue && request.ExpiryDate.Value <= DateTime.UtcNow)
            throw new InvalidOperationException("The payment document has already expired.");
        if (await _unitOfWork.Repository<VendorPaymentEvidenceLink>().GetQueryable(item => item.TenantId == TenantId &&
            item.VendorPaymentId == id && item.RequirementKey == key && item.ChecksumSha256 == checksum).AnyAsync(cancellationToken))
            throw new InvalidOperationException("This file is already attached to this payment requirement.");

        Guid? uploadedId = null;
        Guid? centralId = null;
        var evidenceId = Guid.NewGuid();
        var committed = false;
        try
        {
            var upload = await _controlledFiles.UploadAsync(new ControlledFileUploadRequest
            {
                TenantId = TenantId, ActorUserId = CurrentUserId, ActorName = UserName,
                Category = ControlledFileUploadCategories.DocumentManagement, FileName = request.FileName,
                ContentType = request.ContentType, FileSize = request.Content.LongLength,
                OpenReadStream = () => new MemoryStream(request.Content, writable: false)
            }, cancellationToken);
            uploadedId = upload.Record.Id;
            if (upload.Record.TenantId != TenantId || upload.Record.VirusScanStatus != FileVirusScanStatus.Clean ||
                !string.Equals(upload.ChecksumSha256, checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The document did not pass the clean-file and checksum checks.");
            var central = await _centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = CurrentUserId, ActorName = UserName,
                FileUploadRecordId = upload.Record.Id, SourceModule = "Finance", SourceLabel = "AP payment evidence",
                SourceEntityType = nameof(VendorPayment), SourceRecordId = id, SourceRecordReference = payment.PaymentNumber,
                Title = request.FileName, DocumentType = "VendorPaymentEvidence", AccessProfile = "Finance AP payment restricted",
                VersionStatus = "Submitted", RequirePublishedGovernance = false,
                Notes = "Payment attachment; no human verification is required when the internal approval process is inactive."
            }, cancellationToken);
            centralId = central.DocumentRecordId;
            var link = new VendorPaymentEvidenceLink
            {
                Id = evidenceId, TenantId = TenantId, VendorPaymentId = id, RequirementKey = key,
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                FileUploadRecordId = upload.Record.Id, CentralDocumentRecordId = central.DocumentRecordId,
                CentralDocumentVersionId = central.DocumentVersionId, FileName = request.FileName,
                ContentType = request.ContentType, FileSize = request.Content.LongLength,
                ChecksumSha256 = checksum, ExpiryDate = request.ExpiryDate,
                CreatedById = CurrentUserId, CreatedBy = UserName, CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync(ApSettlementLockKeys.Payment(TenantId, id), cancellationToken);
                    var current = await LoadEvidencePaymentAsync(id, FinanceAccessLevel.Operate, cancellationToken);
                    var committedLink = await _unitOfWork.Repository<VendorPaymentEvidenceLink>().GetQueryable(item =>
                        item.TenantId == TenantId && item.Id == evidenceId && item.RequestHash == requestHash)
                        .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                    if (committedLink != null)
                    {
                        await _unitOfWork.CommitAsync(cancellationToken);
                        return true;
                    }
                    await EnsurePaymentEvidenceEditableAsync(current);
                    link.FileUploadRecord = await _unitOfWork.Repository<FileUploadRecord>().GetQueryable(item =>
                        item.TenantId == TenantId && item.Id == link.FileUploadRecordId && !item.IsDeleted).AsNoTracking().SingleAsync(cancellationToken);
                    link.CentralDocumentRecord = await _unitOfWork.Repository<CentralDocumentRecord>().GetQueryable(item =>
                        item.TenantId == TenantId && item.Id == link.CentralDocumentRecordId && !item.IsDeleted).AsNoTracking().SingleAsync(cancellationToken);
                    link.CentralDocumentVersion = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable(item =>
                        item.TenantId == TenantId && item.Id == link.CentralDocumentVersionId && !item.IsDeleted).AsNoTracking().SingleAsync(cancellationToken);
                    // Physical content is checked again before capturing the durable link.
                    var validBytes = await OpenValidatedPaymentEvidenceBytesAsync(link, validateMetadata: true, cancellationToken);
                    if (validBytes == null) throw new InvalidOperationException("The document content no longer matches its upload.");
                    // Attach only the new link, never Add the already-saved central DMS graph.
                    link.FileUploadRecord = null!;
                    link.CentralDocumentRecord = null!;
                    link.CentralDocumentVersion = null!;
                    await _unitOfWork.Repository<VendorPaymentEvidenceLink>().AddAsync(link);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await RecordApPaymentAuditAsync("Finance.APPayment.EvidenceAttached", current, afterValues: new
                    { EvidenceId = link.Id, link.RequirementKey, link.CentralDocumentRecordId, link.CentralDocumentVersionId,
                        link.FileUploadRecordId, link.ChecksumSha256, link.ExpiryDate, Verification = "NotRequired" },
                        cancellationToken: cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return true;
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(CancellationToken.None);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
            committed = true;
            return MapPaymentEvidence(link, true);
        }
        finally
        {
            if (!committed && uploadedId.HasValue)
            {
                // An uncertain commit must never delete a file already linked to the payment.
                try
                {
                    var linked = await _unitOfWork.Repository<VendorPaymentEvidenceLink>().GetQueryable(item =>
                        item.TenantId == TenantId && item.Id == evidenceId).AsNoTracking().AnyAsync(CancellationToken.None);
                    if (!linked)
                    {
                        if (centralId.HasValue) await _centralDocuments.DeleteAsync(TenantId, centralId.Value, CurrentUserId, CancellationToken.None);
                        else await _controlledFiles.DeleteAsync(TenantId, uploadedId.Value, CurrentUserId, CancellationToken.None);
                    }
                }
                catch (Exception cleanupError) { _logger.LogWarning(cleanupError, "Payment evidence upload cleanup deferred for {PaymentId}", id); }
            }
        }
    }

    private IQueryable<VendorPaymentEvidenceLink> PaymentEvidenceQuery(Guid id) =>
        _unitOfWork.Repository<VendorPaymentEvidenceLink>().GetQueryable(item =>
            item.TenantId == TenantId && item.VendorPaymentId == id && !item.IsDeleted).AsNoTracking()
            .Include(item => item.FileUploadRecord).Include(item => item.CentralDocumentRecord).Include(item => item.CentralDocumentVersion);

    private bool HasValidPaymentEvidenceMetadata(VendorPaymentEvidenceLink link)
    {
        var record = link.CentralDocumentRecord;
        var version = link.CentralDocumentVersion;
        var upload = link.FileUploadRecord;
        return !link.IsDeleted && link.TenantId == TenantId && link.FileSize > 0 && link.FileSize <= MaximumPaymentEvidenceBytes &&
            link.ChecksumSha256.Length == 64 && link.ChecksumSha256.All(Uri.IsHexDigit) &&
            link.RequestHash.Length == 64 && link.RequestHash.All(Uri.IsHexDigit) && link.CreatedById.HasValue &&
            record != null && version != null && upload != null &&
            !record.IsDeleted && !version.IsDeleted && !upload.IsDeleted &&
            record.TenantId == TenantId && version.TenantId == TenantId && upload.TenantId == TenantId &&
            record.Id == link.CentralDocumentRecordId && version.Id == link.CentralDocumentVersionId && upload.Id == link.FileUploadRecordId &&
            version.DocumentRecordId == record.Id && version.FileUploadRecordId == upload.Id &&
            record.SourceModule == "Finance" && record.SourceEntityType == nameof(VendorPayment) && record.SourceRecordId == link.VendorPaymentId &&
            record.LifecycleStatus == "Active" && record.VersionStatus == "Submitted" && version.Status == "Submitted" &&
            record.CurrentVersion == version.VersionNumber && !string.IsNullOrWhiteSpace(record.CurrentVersion) &&
            upload.VirusScanStatus == FileVirusScanStatus.Clean && upload.ScannedAtUtc.HasValue && !upload.StorageDeletedAtUtc.HasValue &&
            version.RepositoryPath == upload.FilePath && !string.IsNullOrWhiteSpace(upload.FilePath) && upload.FileSize == link.FileSize &&
            version.FileSize == link.FileSize && upload.OriginalFileName == link.FileName && version.FileName == link.FileName &&
            upload.ContentType == link.ContentType && version.ContentType == link.ContentType &&
            upload.UploadedByUserId == link.CreatedById && version.CreatedByUserId == link.CreatedById &&
            upload.Category == ControlledFileUploadCategories.DocumentManagement;
    }

    private async Task<byte[]?> OpenValidatedPaymentEvidenceBytesAsync(VendorPaymentEvidenceLink link, bool validateMetadata, CancellationToken ct)
    {
        if (_centralDocuments == null || (validateMetadata && !HasValidPaymentEvidenceMetadata(link))) return null;
        try
        {
            await using var content = await _centralDocuments.OpenAsync(TenantId, link.CentralDocumentRecordId, link.CentralDocumentVersionId, ct);
            if (content == null || content.UploadRecord.Id != link.FileUploadRecordId || content.UploadRecord.TenantId != TenantId ||
                content.UploadRecord.VirusScanStatus != FileVirusScanStatus.Clean || content.FileSize != link.FileSize) return null;
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int length;
            while ((length = await content.Content.ReadAsync(buffer.AsMemory(), ct)) != 0)
            {
                if (bytes.Length + length > link.FileSize || bytes.Length + length > MaximumPaymentEvidenceBytes) return null;
                await bytes.WriteAsync(buffer.AsMemory(0, length), ct);
            }
            var result = bytes.ToArray();
            return result.LongLength == link.FileSize && string.Equals(Convert.ToHexString(SHA256.HashData(result)),
                link.ChecksumSha256, StringComparison.OrdinalIgnoreCase) ? result : null;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException or HttpRequestException)
        {
            _logger.LogWarning(error, "Payment document {EvidenceId} is unavailable or failed integrity validation", link.Id);
            return null;
        }
    }

    private async Task<(List<VendorPaymentEvidenceRequirementStatusDto> Requirements, List<VendorPaymentEvidenceDocumentDto> Documents)>
        GetPaymentEvidenceReadinessAsync(VendorPayment payment, WorkflowApprovalConfigDto control, CancellationToken ct)
    {
        var links = await PaymentEvidenceQuery(payment.Id).ToListAsync(ct);
        var documents = new List<VendorPaymentEvidenceDocumentDto>();
        var valid = new List<VendorPaymentEvidenceLink>();
        foreach (var link in links)
        {
            var usable = (!link.ExpiryDate.HasValue || link.ExpiryDate.Value > DateTime.UtcNow) &&
                (link.CentralDocumentRecord == null || !link.CentralDocumentRecord.ExpiryDate.HasValue || link.CentralDocumentRecord.ExpiryDate.Value > DateTime.UtcNow) &&
                await OpenValidatedPaymentEvidenceBytesAsync(link, true, ct) != null;
            documents.Add(MapPaymentEvidence(link, usable));
            if (usable) valid.Add(link);
        }
        var requirements = control.EvidenceRequirements.Where(item => !string.IsNullOrWhiteSpace(item.RequirementKey)).Select(requirement =>
        {
            var count = valid.Where(item => string.Equals(item.RequirementKey, requirement.RequirementKey, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.ChecksumSha256.ToUpperInvariant()).Distinct().Count();
            return new VendorPaymentEvidenceRequirementStatusDto
            {
                RequirementKey = requirement.RequirementKey, DocumentName = string.IsNullOrWhiteSpace(requirement.DocumentName)
                    ? requirement.RequirementKey : requirement.DocumentName, DocumentType = requirement.DocumentType,
                MinimumDocuments = Math.Max(1, requirement.MinimumDocuments), RequireVerification = false,
                CurrentDocumentCount = count, VerifiedDocumentCount = 0, IsSatisfied = count >= Math.Max(1, requirement.MinimumDocuments)
            };
        }).ToList();
        return (requirements, documents);
    }

    private async Task RequirePaymentEvidenceAsync(VendorPayment payment, WorkflowApprovalConfigDto control, CancellationToken ct)
    {
        if (!control.EvidenceRequirements.Any(item => !string.IsNullOrWhiteSpace(item.RequirementKey))) return;
        if (payment.PaymentBatchId.HasValue)
            throw new InvalidOperationException("This payment batch requires supporting documents: " +
                string.Join(", ", control.EvidenceRequirements.Where(item => !string.IsNullOrWhiteSpace(item.RequirementKey))
                    .Select(item => string.IsNullOrWhiteSpace(item.DocumentName) ? item.RequirementKey : item.DocumentName)) +
                ". Record individual draft payments and attach their required documents before completing them; batch document capture is not yet available.");
        var readiness = await GetPaymentEvidenceReadinessAsync(payment, control, ct);
        var missing = readiness.Requirements.Where(item => !item.IsSatisfied).Select(item =>
            $"{item.DocumentName}: {item.CurrentDocumentCount} of {item.MinimumDocuments} valid document(s)").ToList();
        if (missing.Count > 0) throw new InvalidOperationException("Attach the required payment documents before completing or posting: " + string.Join("; ", missing) + ".");
    }

    private static VendorPaymentEvidenceDocumentDto MapPaymentEvidence(VendorPaymentEvidenceLink link, bool usable) => new()
    {
        Id = link.Id, AttachmentId = link.FileUploadRecordId.ToString(), RequirementKey = link.RequirementKey,
        DocumentName = link.RequirementKey, FileName = link.FileName, EvidenceSource = "Payment",
        DownloadUrl = $"/api/ap/payments/{link.VendorPaymentId}/evidence/{link.Id}/content",
        VerificationStatus = "NotRequired", MalwareScanStatus = usable ? "Clean" : "Unavailable",
        UploadedAt = link.CreatedAt, UploadedById = link.CreatedById ?? Guid.Empty, Sha256 = link.ChecksumSha256
    };

    public async Task<CentralDocumentRepositoryContent?> OpenEvidenceAsync(Guid paymentId, Guid evidenceId, CancellationToken cancellationToken = default)
    {
        await LoadEvidencePaymentAsync(paymentId, FinanceAccessLevel.Read, cancellationToken);
        var link = await PaymentEvidenceQuery(paymentId).SingleOrDefaultAsync(item => item.Id == evidenceId, cancellationToken);
        if (link == null) return null;
        // Expired evidence remains downloadable for audit; it does not satisfy a payment policy.
        var bytes = await OpenValidatedPaymentEvidenceBytesAsync(link, true, cancellationToken);
        if (bytes == null) throw new InvalidOperationException("This payment attachment is unavailable or failed its integrity checks.");
        return new CentralDocumentRepositoryContent
        {
            Content = new MemoryStream(bytes, writable: false), FileName = link.FileName,
            ContentType = link.ContentType, FileSize = link.FileSize, UploadRecord = link.FileUploadRecord
        };
    }
}
