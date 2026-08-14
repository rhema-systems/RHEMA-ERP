using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public sealed class ProcurementReceiptSourceEvidenceService(
    ApplicationDbContext db,
    ICurrentUserProvider currentUser,
    IProcurementAccessControlService access,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments,
    IProcurementControlEventService controlEvents,
    ILogger<ProcurementReceiptSourceEvidenceService> logger) :
    IProcurementReceiptSourceEvidenceService
{
    private const string ReadPermission = "procurement.inventory.read";
    private const string ManagePermission = "procurement.inventory.receive";
    private const string FinanceReadPermission = "Finance.Read";
    private const string TemplateCode = "TDC-PROC-RECEIPT-SOURCE";
    private const string AccessProfile = "Procurement receipt and AP restricted";
    private const int MaximumFileBytes = 25_000_000;

    public async Task<ProcurementReceiptSourceEvidenceOverviewDto> GetOverviewAsync(
        Guid receiptId, CancellationToken cancellationToken = default)
    {
        EnsureInternalUser();
        var receipt = await RequiredReceiptAsync(receiptId, false, cancellationToken);
        await RequireReadAsync(receipt, cancellationToken);
        var values = await EvidenceQuery(false)
            .Where(item => item.PurchaseOrderReceiptId == receiptId && item.IsCurrent)
            .OrderBy(item => item.EvidenceKind)
            .ToListAsync(cancellationToken);
        var locked = await IsLockedAsync(receiptId, cancellationToken);
        var canUpload = !locked && await CanUseAsync(ManagePermission, receipt, cancellationToken);
        return new ProcurementReceiptSourceEvidenceOverviewDto
        {
            ReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            PurchaseOrderNumber = receipt.PurchaseOrder.OrderNumber,
            SupplierName = receipt.PurchaseOrder.BusinessPartner?.PartnerName ?? string.Empty,
            WaybillReady = values.Any(item => item.EvidenceKind == ProcurementReceiptSourceEvidenceKind.Waybill && IsClean(item)),
            InspectionEvidenceLocked = locked,
            CanUpload = canUpload,
            Evidence = values.Select(Map).ToList()
        };
    }

    public async Task<ProcurementReceiptSourceEvidenceDto> UploadAsync(
        Guid receiptId,
        ProcurementReceiptSourceEvidenceUploadCommand command,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Validate(command);
        EnsureInternalUser();
        var receipt = await RequiredReceiptAsync(receiptId, true, cancellationToken);
        await RequireCapabilityAsync(ManagePermission, receipt, cancellationToken);
        await EnsureMutableAsync(receiptId, cancellationToken);

        var reference = command.ReferenceNumber.Trim();
        var safeName = Path.GetFileName(command.FileName.Trim());
        var contentType = string.IsNullOrWhiteSpace(command.ContentType)
            ? "application/octet-stream"
            : command.ContentType.Trim();
        var checksum = Convert.ToHexString(SHA256.HashData(command.Content)).ToLowerInvariant();
        var requestHash = Hash(new
        {
            receiptId,
            command.EvidenceKind,
            ReferenceNumber = reference,
            DocumentDate = command.DocumentDate.Date,
            FileName = safeName,
            ContentType = contentType,
            ChecksumSha256 = checksum
        });

        var replay = await EvidenceQuery(false).SingleOrDefaultAsync(item =>
            item.ClientRequestId == command.ClientRequestId, cancellationToken);
        if (replay is not null)
        {
            if (replay.PurchaseOrderReceiptId != receiptId || !FixedEquals(replay.RequestHash, requestHash))
                throw Conflict("RCV_SOURCE_EVIDENCE_IDEMPOTENCY_CONFLICT",
                    "The client request identifier was already used for a different receipt-evidence payload.");
            return Map(replay);
        }

        var evidenceId = Guid.NewGuid();
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = currentUser.TenantId,
            ActorUserId = currentUser.UserId,
            ActorName = ActorName,
            Category = ControlledFileUploadCategories.ProcurementReceiptSourceEvidence,
            FileName = safeName,
            ContentType = contentType,
            FileSize = command.Content.LongLength,
            OpenReadStream = () => new MemoryStream(command.Content, writable: false)
        }, cancellationToken);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean ||
            !FixedEquals(upload.ChecksumSha256, checksum))
        {
            await controlledFiles.DeleteAsync(currentUser.TenantId, upload.Record.Id,
                currentUser.UserId, cancellationToken);
            throw Conflict("RCV_SOURCE_EVIDENCE_SCAN_FAILED",
                "The receipt evidence did not pass the central malware and integrity checks.");
        }

        CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = currentUser.TenantId,
                ActorUserId = currentUser.UserId,
                ActorName = ActorName,
                FileUploadRecordId = upload.Record.Id,
                SourceModule = "Procurement",
                SourceLabel = "Purchase receipt source evidence",
                SourceEntityType = nameof(ProcurementReceiptSourceEvidence),
                SourceRecordId = evidenceId,
                SourceRecordReference = receipt.ReceiptNumber,
                Title = safeName,
                DocumentType = "PurchaseReceiptSourceEvidence",
                MetadataTemplateCode = TemplateCode,
                AccessProfile = AccessProfile,
                VersionStatus = CentralDocumentEvidenceRules.PublishedVersionStatus,
                ChangeSummary = $"Clean-scanned {KindLabel(command.EvidenceKind)} retained for receipt {receipt.ReceiptNumber}.",
                RequirePublishedGovernance = true,
                MetadataValues =
                [
                    new("sourceReference", "Source reference", receipt.ReceiptNumber),
                    new("documentFamily", "Document family", KindLabel(command.EvidenceKind)),
                    new("classification", "Classification", command.EvidenceKind.ToString()),
                    new("sourceStatus", "Source status", receipt.Status),
                    new("uploadedBy", "Uploaded by", ActorName),
                    new("checksumSha256", "Checksum SHA-256", checksum)
                ]
            }, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await controlledFiles.DeleteAsync(currentUser.TenantId, upload.Record.Id,
                currentUser.UserId, cancellationToken);
            logger.LogWarning(exception,
                "Central DMS rejected receipt evidence for receipt {ReceiptId}", receiptId);
            throw Conflict("RCV_SOURCE_EVIDENCE_DMS_UNAVAILABLE",
                "The receipt evidence could not be registered under the current central document policy. Ask an administrator to verify the published receipt-document governance.");
        }
        catch
        {
            await controlledFiles.DeleteAsync(currentUser.TenantId, upload.Record.Id,
                currentUser.UserId, cancellationToken);
            throw;
        }

        var resultEvidenceId = evidenceId;
        var discardNewDocument = false;
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                resultEvidenceId = evidenceId;
                discardNewDocument = false;
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                var currentReceipt = await RequiredReceiptAsync(receiptId, true, cancellationToken);
                await EnsureMutableAsync(receiptId, cancellationToken);
                var concurrentReplay = await EvidenceQuery(true).SingleOrDefaultAsync(item =>
                    item.ClientRequestId == command.ClientRequestId, cancellationToken);
                if (concurrentReplay is not null)
                {
                    if (concurrentReplay.PurchaseOrderReceiptId != receiptId ||
                        !FixedEquals(concurrentReplay.RequestHash, requestHash))
                        throw Conflict("RCV_SOURCE_EVIDENCE_IDEMPOTENCY_CONFLICT",
                            "The client request identifier was already used for a different receipt-evidence payload.");
                    resultEvidenceId = concurrentReplay.Id;
                    discardNewDocument = true;
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                var previous = await EvidenceQuery(true).SingleOrDefaultAsync(item =>
                    item.PurchaseOrderReceiptId == receiptId &&
                    item.EvidenceKind == command.EvidenceKind && item.IsCurrent, cancellationToken);
                var now = DateTime.UtcNow;
                var evidence = new ProcurementReceiptSourceEvidence
                {
                    Id = evidenceId,
                    TenantId = currentUser.TenantId,
                    PurchaseOrderReceiptId = receiptId,
                    EvidenceKind = command.EvidenceKind,
                    ReferenceNumber = reference,
                    DocumentDate = command.DocumentDate.Date,
                    IsCurrent = true,
                    ClientRequestId = command.ClientRequestId,
                    RequestHash = requestHash,
                    OriginalFileName = upload.Record.OriginalFileName,
                    ContentType = contentType,
                    FileSize = command.Content.LongLength,
                    ChecksumSha256 = checksum,
                    FileUploadRecordId = document.FileUploadRecordId,
                    CentralDocumentRecordId = document.DocumentRecordId,
                    CentralDocumentVersionId = document.DocumentVersionId,
                    CreatedAt = now,
                    CreatedBy = ActorName,
                    CreatedById = currentUser.UserId
                };
                if (previous is not null)
                {
                    previous.IsCurrent = false;
                    previous.SupersededAtUtc = now;
                    previous.SupersededByEvidenceId = evidence.Id;
                    previous.UpdatedAt = now;
                    previous.UpdatedBy = ActorName;
                    previous.LastModifiedById = currentUser.UserId;
                    // Release the filtered unique current-kind slot before inserting
                    // the immutable replacement. Both saves remain inside the same
                    // serializable transaction and are rolled back together.
                    await db.SaveChangesAsync(cancellationToken);
                }
                db.ProcurementReceiptSourceEvidence.Add(evidence);
                await db.SaveChangesAsync(cancellationToken);
                await controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
                {
                    EventKey = ProcurementControlEventKey.Create("receipt-source-evidence", currentUser.TenantId,
                        receiptId, previous is null ? "Attached" : "Replaced", command.ClientRequestId.ToString("N")),
                    EventType = "ProcurementReceiptSourceEvidence",
                    Action = previous is null ? "Attached" : "Replaced",
                    Result = ProcurementControlEventResult.Succeeded,
                    RuleCode = "INV-REQ-FU-001",
                    DecisionKeys = [],
                    SourceType = nameof(PurchaseOrderReceipt),
                    SourceId = receiptId,
                    SourceReference = currentReceipt.ReceiptNumber,
                    Reason = $"Governed {KindLabel(command.EvidenceKind)} evidence captured through the central DMS.",
                    ResultValues = new
                    {
                        evidence.Id,
                        evidence.EvidenceKind,
                        evidence.ReferenceNumber,
                        evidence.DocumentDate,
                        evidence.ChecksumSha256,
                        evidence.CentralDocumentRecordId,
                        evidence.CentralDocumentVersionId,
                        SupersededEvidenceId = previous?.Id,
                        FinanceApOwnsInvoiceLifecycle = command.EvidenceKind == ProcurementReceiptSourceEvidenceKind.VatInvoiceCopy
                    },
                    CorrelationId = Correlation(correlationId),
                    OccurredAtUtc = now
                }, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await SafeDeleteDocumentAsync(document.DocumentRecordId, cancellationToken);
            throw Conflict("RCV_SOURCE_EVIDENCE_CONCURRENT_CHANGE",
                "Receipt evidence changed concurrently. Reload the receipt and retry with a new client request identifier.");
        }
        catch
        {
            await SafeDeleteDocumentAsync(document.DocumentRecordId, cancellationToken);
            throw;
        }

        if (discardNewDocument)
            await SafeDeleteDocumentAsync(document.DocumentRecordId, cancellationToken);
        db.ChangeTracker.Clear();
        return Map(await EvidenceQuery(false).SingleAsync(item => item.Id == resultEvidenceId, cancellationToken));
    }

    public async Task<CentralDocumentRepositoryContent> OpenAsync(
        Guid receiptId, Guid evidenceId, CancellationToken cancellationToken = default)
    {
        EnsureInternalUser();
        var receipt = await RequiredReceiptAsync(receiptId, false, cancellationToken);
        await RequireReadAsync(receipt, cancellationToken);
        var evidence = await EvidenceQuery(false).SingleOrDefaultAsync(item =>
            item.Id == evidenceId && item.PurchaseOrderReceiptId == receiptId, cancellationToken)
            ?? throw new ProcurementReceiptSourceEvidenceNotFoundException(
                "The receipt evidence was not found in the current tenant.");
        return await centralDocuments.OpenAsync(currentUser.TenantId,
                   evidence.CentralDocumentRecordId, evidence.CentralDocumentVersionId, cancellationToken)
               ?? throw Conflict("RCV_SOURCE_EVIDENCE_CONTENT_MISSING",
                   "The centrally governed receipt-evidence content is unavailable.");
    }

    public async Task EnsureWaybillReadyAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var waybill = await EvidenceQuery(false).SingleOrDefaultAsync(item =>
            item.PurchaseOrderReceiptId == receiptId && item.IsCurrent &&
            item.EvidenceKind == ProcurementReceiptSourceEvidenceKind.Waybill, cancellationToken);
        if (waybill is null)
            throw Validation("RCV_WAYBILL_REQUIRED",
                "Attach a typed, clean-scanned Waybill before submitting or approving the receipt inspection.");
        if (!IsClean(waybill))
            throw Conflict("RCV_WAYBILL_NOT_CLEAN",
                "The current Waybill is not backed by an active clean central-DMS version.");
    }

    private IQueryable<ProcurementReceiptSourceEvidence> EvidenceQuery(bool tracked)
    {
        var query = db.ProcurementReceiptSourceEvidence
            .Where(item => item.TenantId == currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.FileUploadRecord)
            .Include(item => item.CentralDocumentRecord)
            .Include(item => item.CentralDocumentVersion)
            .AsQueryable();
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<PurchaseOrderReceipt> RequiredReceiptAsync(Guid id, bool tracked, CancellationToken token)
    {
        EnsureAuthenticatedTenant();
        var query = db.PurchaseOrderReceipts
            .Where(item => item.TenantId == currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .Include(item => item.PurchaseOrder).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.Items).ThenInclude(item => item.PurchaseOrderItem)
            .AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(token)
               ?? throw new ProcurementReceiptSourceEvidenceNotFoundException(
                   "The purchase-order receipt was not found in the current tenant.");
    }

    private async Task EnsureMutableAsync(Guid receiptId, CancellationToken token)
    {
        if (await IsLockedAsync(receiptId, token))
            throw Conflict("RCV_SOURCE_EVIDENCE_LOCKED",
                "Receipt-source evidence is locked while the inspection is submitted, approved, or in resolution. Start the permitted inspection correction lifecycle instead of replacing evidence in place.");
    }

    private Task<bool> IsLockedAsync(Guid receiptId, CancellationToken token) =>
        db.ProcurementReceiptInspectionCases.AsNoTracking().AnyAsync(item =>
            item.TenantId == currentUser.TenantId && item.PurchaseOrderReceiptId == receiptId && !item.IsDeleted &&
            item.Status != ProcurementReceiptInspectionStatus.Draft &&
            item.Status != ProcurementReceiptInspectionStatus.Rejected &&
            item.Status != ProcurementReceiptInspectionStatus.Cancelled, token);

    private bool IsClean(ProcurementReceiptSourceEvidence item) =>
        item.FileUploadRecord.VirusScanStatus == FileVirusScanStatus.Clean &&
        !item.FileUploadRecord.IsDeleted && !item.CentralDocumentRecord.IsDeleted &&
        !item.CentralDocumentVersion.IsDeleted &&
        item.CentralDocumentVersion.FileUploadRecordId == item.FileUploadRecordId &&
        item.CentralDocumentVersion.DocumentRecordId == item.CentralDocumentRecordId &&
        item.CentralDocumentVersion.FileSize == item.FileSize &&
        item.FileUploadRecord.FileSize == item.FileSize &&
        string.Equals(item.FileUploadRecord.Category,
            ControlledFileUploadCategories.ProcurementReceiptSourceEvidence,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.CentralDocumentRecord.LifecycleStatus,
            CentralDocumentEvidenceRules.ActiveLifecycleStatus, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.CentralDocumentRecord.VersionStatus,
            CentralDocumentEvidenceRules.PublishedVersionStatus, StringComparison.OrdinalIgnoreCase) &&
        item.CentralDocumentRecord.CurrentVersion == item.CentralDocumentVersion.VersionNumber &&
        string.Equals(item.CentralDocumentVersion.Status,
            CentralDocumentEvidenceRules.PublishedVersionStatus, StringComparison.OrdinalIgnoreCase) &&
        item.CentralDocumentVersion.PublishedAt.HasValue &&
        item.ChecksumSha256.Length == 64;

    private async Task RequireReadAsync(PurchaseOrderReceipt receipt, CancellationToken token)
    {
        EnsureAuthenticatedTenant();
        if (currentUser.IsExternalUser)
            throw new ProcurementReceiptSourceEvidenceAuthorizationException(
                "External users cannot access internal receipt evidence.");
        if (IsAdministrator()) return;
        if (await CanUseAsync(ReadPermission, receipt, token) || await CanUseAsync(FinanceReadPermission, receipt, token)) return;
        throw new ProcurementReceiptSourceEvidenceAuthorizationException(
            "The current user is not permitted to view receipt evidence.");
    }

    private async Task RequireCapabilityAsync(string permission, PurchaseOrderReceipt receipt, CancellationToken token)
    {
        EnsureAuthenticatedTenant();
        if (currentUser.IsExternalUser)
            throw new ProcurementReceiptSourceEvidenceAuthorizationException(
                "External users cannot administer internal receipt evidence.");
        if (IsAdministrator()) return;
        var decision = await access.EnforceCapabilityAsync(Capability(permission, receipt),
            Guid.NewGuid().ToString("N"), token);
        if (!decision.Allowed) throw new ProcurementReceiptSourceEvidenceAuthorizationException(decision.Message);
    }

    private async Task<bool> CanUseAsync(string permission, PurchaseOrderReceipt receipt, CancellationToken token)
    {
        if (currentUser.IsExternalUser) return false;
        try
        {
            return (await access.CheckCapabilityAsync(Capability(permission, receipt),
                Guid.NewGuid().ToString("N"), token)).Allowed;
        }
        catch (ProcurementAccessAuthorizationException) { return false; }
        catch (ProcurementAccessValidationException) { return false; }
    }

    private ProcurementAccessCapabilityRequest Capability(string permission, PurchaseOrderReceipt receipt) => new()
    {
        PermissionCode = permission,
        WarehouseId = receipt.PurchaseOrder.DeliveryWarehouseId ?? receipt.Items
            .Select(item => item.PurchaseOrderItem.WarehouseId)
            .FirstOrDefault(item => item.HasValue && item.Value != Guid.Empty),
        SourceType = "ProcurementReceiptSourceEvidence",
        SourceReference = receipt.ReceiptNumber
    };

    private async Task SafeDeleteDocumentAsync(Guid documentRecordId, CancellationToken token)
    {
        try { await centralDocuments.DeleteAsync(currentUser.TenantId, documentRecordId, currentUser.UserId, token); }
        catch (Exception cleanupError) { logger.LogError(cleanupError, "Could not clean failed receipt-evidence DMS record {DocumentRecordId}", documentRecordId); }
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty || currentUser.TenantId == Guid.Empty)
            throw new ProcurementReceiptSourceEvidenceAuthorizationException(
                "An authenticated tenant user is required.");
    }

    private void EnsureInternalUser()
    {
        EnsureAuthenticatedTenant();
        if (currentUser.IsExternalUser)
            throw new ProcurementReceiptSourceEvidenceAuthorizationException(
                "External users cannot access internal receipt evidence.");
    }

    private static void Validate(ProcurementReceiptSourceEvidenceUploadCommand command)
    {
        if (!Enum.IsDefined(command.EvidenceKind))
            throw Validation("RCV_SOURCE_EVIDENCE_KIND_INVALID", "Select Waybill or VAT invoice copy.");
        if (command.ClientRequestId == Guid.Empty)
            throw Validation("RCV_SOURCE_EVIDENCE_REQUEST_ID_REQUIRED", "A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(command.ReferenceNumber) || command.ReferenceNumber.Trim().Length is < 2 or > 100)
            throw Validation("RCV_SOURCE_EVIDENCE_REFERENCE_INVALID", "A 2 to 100 character document reference is required.");
        if (command.DocumentDate == default || command.DocumentDate.Date > DateTime.UtcNow.Date.AddDays(1))
            throw Validation("RCV_SOURCE_EVIDENCE_DATE_INVALID", "Enter a valid document date that is not in the future.");
        if (command.Content.Length == 0 || command.Content.Length > MaximumFileBytes)
            throw Validation("RCV_SOURCE_EVIDENCE_FILE_INVALID", "Select a non-empty file no larger than 25 MB.");
        if (string.IsNullOrWhiteSpace(command.FileName))
            throw Validation("RCV_SOURCE_EVIDENCE_FILE_REQUIRED", "Select a receipt-evidence file.");
    }

    private static ProcurementReceiptSourceEvidenceDto Map(ProcurementReceiptSourceEvidence item) => new()
    {
        Id = item.Id,
        EvidenceKind = item.EvidenceKind,
        ReferenceNumber = item.ReferenceNumber,
        DocumentDate = item.DocumentDate,
        OriginalFileName = item.OriginalFileName,
        ContentType = item.ContentType,
        FileSize = item.FileSize,
        ChecksumSha256 = item.ChecksumSha256,
        CentralDocumentRecordId = item.CentralDocumentRecordId,
        CentralDocumentVersionId = item.CentralDocumentVersionId,
        UploadedAtUtc = item.CreatedAt,
        UploadedBy = item.CreatedBy ?? string.Empty,
        IsCurrent = item.IsCurrent
    };

    private bool IsAdministrator() => currentUser.HasRole("SuperAdmin") || currentUser.HasRole("TenantAdmin");
    private string ActorName => string.IsNullOrWhiteSpace(currentUser.FullName) ? currentUser.Username : currentUser.FullName;
    private static string KindLabel(ProcurementReceiptSourceEvidenceKind kind) =>
        kind == ProcurementReceiptSourceEvidenceKind.Waybill ? "Waybill" : "VAT invoice copy";
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    private static ProcurementReceiptSourceEvidenceValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementReceiptSourceEvidenceConflictException Conflict(string code, string message) => new(code, message);
}
