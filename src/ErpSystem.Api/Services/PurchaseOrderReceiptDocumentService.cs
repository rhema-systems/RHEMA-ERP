using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

public sealed class PurchaseOrderReceiptDocumentService : IProcurementReceiptDocumentService
{
    private const string EventType = "ProcurementReceiptDocument";
    private const string ReadPermission = "procurement.inventory.read";
    private const string ManagePermission = "procurement.inventory.receive";
    private const string IssuePermission = "procurement.purchase-order.approve";
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementConfigurationService _configuration;
    private readonly IDocumentNumberingService _numbering;
    private readonly IFileStorageService _storage;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;
    private readonly ILogger<PurchaseOrderReceiptDocumentService> _logger;

    public PurchaseOrderReceiptDocumentService(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementConfigurationService configuration,
        IDocumentNumberingService numbering,
        IFileStorageService storage,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications,
        ILogger<PurchaseOrderReceiptDocumentService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _configuration = configuration;
        _numbering = numbering;
        _storage = storage;
        _controlEvents = controlEvents;
        _notifications = notifications;
        _logger = logger;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public async Task<ProcurementReceiptDocumentOverviewDto> EnsureAsync(
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction is not null)
            return await EnsureCoreAsync(receiptId, correlationId, cancellationToken);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            return await EnsureCoreAsync(receiptId, correlationId, cancellationToken);
        });
    }

    private async Task<ProcurementReceiptDocumentOverviewDto> EnsureCoreAsync(
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var correlation = Correlation(correlationId);
        var receipt = await LoadReceiptAsync(receiptId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, receipt, correlation, cancellationToken);
        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            var existing = await DocumentQuery(true)
                .Where(item => item.PurchaseOrderReceiptId == receipt.Id)
                .ToListAsync(cancellationToken);
            var governance = existing.Count == 0
                ? await LoadGovernanceAsync(cancellationToken)
                : await LoadStoredGovernanceAsync(existing, cancellationToken);
            var grn = await _db.GoodsReceiptNotes
                .SingleOrDefaultAsync(item => item.TenantId == _currentUser.TenantId &&
                                              item.PurchaseOrderReceiptId == receipt.Id && !item.IsDeleted,
                    cancellationToken);

            foreach (var kind in RequiredKinds(governance.Value.DocumentType))
            {
                if (existing.Any(item => item.DocumentKind == kind)) continue;
                var configKind = kind == ProcurementReceiptDocumentKind.Mrn
                    ? ProcurementReceiptDocumentType.Mrn
                    : ProcurementReceiptDocumentType.Grn;
                var format = governance.Value.ResolveNumberFormat(configKind);
                var templateCode = governance.Value.ResolveTemplateReference(configKind);
                await EnsureTemplateAsync(templateCode, kind, cancellationToken);
                var id = Guid.NewGuid();
                var number = await _numbering.GenerateConfiguredAsync(
                    DocumentNumberingModules.Procurement,
                    kind == ProcurementReceiptDocumentKind.Grn ? "GoodsReceiptNote" : "MaterialReceiptNote",
                    kind == ProcurementReceiptDocumentKind.Grn ? "Goods receipt note" : "Material receipt note",
                    format,
                    ResetPolicy(format),
                    _currentUser.TenantId,
                    receipt.ReceiptDate,
                    nameof(ProcurementReceiptDocument),
                    id,
                    cancellationToken);
                var snapshot = SourceSnapshot(receipt, kind, number, governance);
                var now = DateTime.UtcNow;
                var document = new ProcurementReceiptDocument
                {
                    Id = id,
                    TenantId = _currentUser.TenantId,
                    PurchaseOrderReceiptId = receipt.Id,
                    GoodsReceiptNoteId = grn?.Id,
                    DocumentKind = kind,
                    DocumentNumber = number,
                    TemplateCode = templateCode,
                    Status = governance.Value.SignatureRequirements.Count == 0
                        ? ProcurementReceiptDocumentStatus.Draft
                        : ProcurementReceiptDocumentStatus.PendingSignatures,
                    ConfigurationProfileId = governance.Profile.Id,
                    ConfigurationProfileVersion = governance.Profile.Version,
                    ConfigurationDecisionId = governance.Decision.Id,
                    DecisionKeysJson = Serialize(DecisionKeys),
                    DecisionSnapshotJson = governance.Decision.Value.GetRawText(),
                    SourceSnapshotJson = snapshot,
                    SourceIntegrityHash = Hash(snapshot),
                    CorrelationId = correlation,
                    PreparedByUserId = _currentUser.UserId,
                    PreparedByName = ActorName,
                    PreparedAtUtc = now,
                    CreatedAt = now,
                    CreatedBy = ActorName,
                    CreatedById = _currentUser.UserId
                };
                AddAction(document, "Created", string.Empty, document.Status.ToString(), correlation,
                    "Generated from the effective DEC-013 receipt-document configuration.", governance.Decision.Value.GetRawText());
                _db.ProcurementReceiptDocuments.Add(document);
                existing.Add(document);
            }

            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            await RecordEventAsync(receipt, "Ensured", ProcurementControlEventResult.Succeeded,
                "Required GRN/MRN register entries exist.", correlation,
                new { governance.Profile.Id, governance.Profile.Version, governance.Value.DocumentType }, cancellationToken);
            return await BuildOverviewAsync(receipt, existing, governance.Value, cancellationToken,
                governance.Profile.Id, governance.Profile.Version);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            throw Conflict("RCV_DOCUMENT_CONCURRENT_CREATE", "The receipt-document register was created concurrently. Reload the receipt and retry.");
        }
    }

    public async Task<ProcurementReceiptDocumentOverviewDto> GetOverviewAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await LoadReceiptAsync(receiptId, false, cancellationToken);
        await EnsureCapabilityAsync(ReadPermission, receipt, Correlation(null), cancellationToken);
        var documents = await DocumentQuery(false)
            .Where(item => item.PurchaseOrderReceiptId == receipt.Id)
            .ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            var governance = await LoadGovernanceAsync(cancellationToken);
            return await BuildOverviewAsync(receipt, documents, governance.Value, cancellationToken,
                governance.Profile.Id, governance.Profile.Version);
        }

        return await BuildOverviewAsync(receipt, documents, DeserializeDecision(documents[0].DecisionSnapshotJson),
            cancellationToken, documents[0].ConfigurationProfileId, documents[0].ConfigurationProfileVersion);
    }

    public async Task<ProcurementReceiptDocumentDto> SignAsync(
        Guid documentId,
        SignProcurementReceiptDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = Correlation(correlationId);
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, document.PurchaseOrderReceipt, correlation, cancellationToken);
        EnsureRowVersion(document.RowVersion, request.RowVersion);
        if (document.Status is ProcurementReceiptDocumentStatus.Issued or ProcurementReceiptDocumentStatus.Cancelled)
            throw Conflict("RCV_DOCUMENT_NOT_SIGNABLE", "Issued or cancelled receipt documents cannot be signed.");
        var config = DeserializeDecision(document.DecisionSnapshotJson);
        var role = config.SignatureRequirements.FirstOrDefault(item =>
            string.Equals(item.Trim(), request.RequiredRole.Trim(), StringComparison.OrdinalIgnoreCase));
        if (role is null)
            throw Validation("RCV_DOCUMENT_SIGNATURE_ROLE_INVALID", "The selected signature role is not required by DEC-013.");
        if (!IsAdministrator() && !_currentUser.Roles.Any(item => string.Equals(item, role, StringComparison.OrdinalIgnoreCase)))
            throw new ProcurementReceiptDocumentAuthorizationException($"The current user is not assigned the required {role} role.");
        if (document.Signatures.Any(item => string.Equals(item.RequiredRole, role, StringComparison.OrdinalIgnoreCase)))
            throw Conflict("RCV_DOCUMENT_ALREADY_SIGNED", $"The {role} signature has already been recorded.");
        if (document.Signatures.Any(item => item.SignedByUserId == _currentUser.UserId))
            throw Conflict("RCV_DOCUMENT_SIGNATORY_SOD", "One person cannot satisfy more than one configured signatory role on the same receipt document.");

        var now = DateTime.UtcNow;
        var signaturePayload = Serialize(new { document.Id, role, userId = _currentUser.UserId, now, request.Comment });
        document.Signatures.Add(new ProcurementReceiptDocumentSignature
        {
            TenantId = _currentUser.TenantId,
            ReceiptDocumentId = document.Id,
            RequiredRole = role,
            SignedByUserId = _currentUser.UserId,
            SignedByName = ActorName,
            SignedAtUtc = now,
            Comment = Trim(request.Comment, 1000),
            IntegrityHash = Hash(signaturePayload),
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        });
        AddAction(document, "Signed", document.Status.ToString(),
            ProcurementReceiptDocumentStatus.PendingSignatures.ToString(), correlation,
            $"{role} signature recorded.", signaturePayload);
        document.Status = ProcurementReceiptDocumentStatus.PendingSignatures;
        document.UpdatedAt = now;
        document.UpdatedBy = ActorName;
        document.LastModifiedById = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(document.PurchaseOrderReceipt, "Signed", ProcurementControlEventResult.Succeeded,
            $"{role} signature recorded for {document.DocumentNumber}.", correlation,
            new { document.Id, document.DocumentNumber, role }, cancellationToken);
        return Map(document, config);
    }

    public async Task<ProcurementReceiptDocumentDto> IssueAsync(
        Guid documentId,
        IssueProcurementReceiptDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = Correlation(correlationId);
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        await EnsureCapabilityAsync(IssuePermission, document.PurchaseOrderReceipt, correlation, cancellationToken);
        if (document.Status == ProcurementReceiptDocumentStatus.Issued) return Map(document, DeserializeDecision(document.DecisionSnapshotJson));
        EnsureRowVersion(document.RowVersion, request.RowVersion);
        if (document.Status == ProcurementReceiptDocumentStatus.Cancelled)
            throw Conflict("RCV_DOCUMENT_CANCELLED", "A cancelled receipt document cannot be issued.");
        var config = DeserializeDecision(document.DecisionSnapshotJson);
        var missing = ProcurementReceiptDocumentRules.MissingRequirements(
            config.SignatureRequirements,
            document.Signatures.Select(signature => signature.RequiredRole));
        if (missing.Count > 0)
            throw Validation("RCV_DOCUMENT_SIGNATURES_INCOMPLETE", $"Required signatures are missing: {string.Join(", ", missing)}.");
        var inspection = await LatestInspectionAsync(document.PurchaseOrderReceiptId, cancellationToken);
        if (!ProcurementReceiptDocumentRules.IsInspectionApproved(inspection?.Status))
            throw Validation("RCV_DOCUMENT_INSPECTION_NOT_APPROVED", "The governed receipt inspection must be approved or closed before issue.");
        var evidence = await EvidenceKeysAsync(inspection!.Id, cancellationToken);
        var missingEvidence = ProcurementReceiptDocumentRules.MissingRequirements(config.EvidenceRequirements, evidence);
        if (missingEvidence.Count > 0)
            throw Validation("RCV_DOCUMENT_EVIDENCE_INCOMPLETE", $"Required DEC-013 receipt evidence is missing: {string.Join(", ", missingEvidence)}.");
        if (ProcurementReceiptDocumentRules.RequiresPriorGrnIssue(document.DocumentKind, config.CoexistenceRule) &&
            !await _db.ProcurementReceiptDocuments.AsNoTracking().AnyAsync(item =>
                item.TenantId == _currentUser.TenantId &&
                item.PurchaseOrderReceiptId == document.PurchaseOrderReceiptId &&
                item.DocumentKind == ProcurementReceiptDocumentKind.Grn &&
                item.Status == ProcurementReceiptDocumentStatus.Issued &&
                !item.IsDeleted,
                cancellationToken))
            throw Validation("RCV_DOCUMENT_SEQUENCE_INVALID", "DEC-013 requires the GRN to be issued before the MRN.");

        var template = await EnsureTemplateAsync(document.TemplateCode, document.DocumentKind, cancellationToken);
        var pdf = BuildPdf(document, template, inspection);
        var fileName = $"{SafeFileName(document.DocumentNumber)}.pdf";
        await using var stream = new MemoryStream(pdf);
        var upload = await _storage.UploadFileAsync(new FileUploadRequest
        {
            FileStream = stream,
            FileName = fileName,
            ContentType = "application/pdf",
            FileSize = pdf.LongLength,
            Category = "central-dms-generated",
            TenantId = _currentUser.TenantId.ToString(),
            Metadata =
            {
                ["TemplateCode"] = template.TemplateCode,
                ["SourceModule"] = "Procurement",
                ["SourceRecord"] = document.DocumentNumber
            }
        });
        if (!upload.Success)
            throw Validation("RCV_DOCUMENT_STORAGE_FAILED", upload.ErrorMessage ?? "The PDF could not be saved in the central document repository.");

        try
        {
            var now = DateTime.UtcNow;
            var uploadRecord = new FileUploadRecord
            {
                TenantId = _currentUser.TenantId,
                Category = "central-dms-generated",
                FilePath = upload.FilePath,
                StoredFileName = upload.FileName,
                OriginalFileName = upload.OriginalFileName,
                ContentType = upload.ContentType,
                FileSize = upload.FileSize,
                StorageProvider = upload.StorageProvider,
                UploadedByUserId = _currentUser.UserId,
                VirusScanStatus = FileVirusScanStatus.Skipped,
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            var record = new CentralDocumentRecord
            {
                TenantId = _currentUser.TenantId,
                DocumentReference = $"RCV-{document.Id:N}",
                Title = $"{KindLabel(document.DocumentKind)} {document.DocumentNumber}",
                SourceModule = "Procurement",
                SourceLabel = "Purchase receipt",
                SourceEntityType = nameof(ProcurementReceiptDocument),
                SourceRecordReference = document.PurchaseOrderReceipt.ReceiptNumber,
                SourceRecordId = document.Id,
                MetadataTemplateCode = template.MetadataTemplateCode,
                RepositoryStatus = "Linked",
                RepositoryPath = upload.FilePath,
                CurrentVersion = "v1.0",
                VersionStatus = "Published",
                AnnotationStatus = "PDF preview ready",
                AccessProfile = template.AccessProfile,
                LifecycleStatus = "Active",
                PublishedAt = now,
                PublishedById = _currentUser.UserId,
                EffectiveDate = now,
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            var version = new CentralDocumentVersion
            {
                TenantId = _currentUser.TenantId,
                DocumentRecordId = record.Id,
                VersionNumber = "v1.0",
                Status = "Published",
                RepositoryPath = upload.FilePath,
                RenditionPath = upload.FilePath,
                FileName = upload.OriginalFileName,
                ContentType = "application/pdf",
                FileSize = upload.FileSize,
                FileUploadRecordId = uploadRecord.Id,
                CreatedByUserId = _currentUser.UserId,
                PublishedAt = now,
                PublishedById = _currentUser.UserId,
                ChangeSummary = Trim(request.Comment, 1000),
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            _db.FileUploadRecords.Add(uploadRecord);
            _db.CentralDocumentRecords.Add(record);
            _db.CentralDocumentVersions.Add(version);
            var before = document.Status.ToString();
            document.Status = ProcurementReceiptDocumentStatus.Issued;
            document.IssuedAtUtc = now;
            document.IssuedByUserId = _currentUser.UserId;
            document.IssuedByName = ActorName;
            document.CentralDocumentRecordId = record.Id;
            document.CentralDocumentVersionId = version.Id;
            document.UpdatedAt = now;
            document.UpdatedBy = ActorName;
            document.LastModifiedById = _currentUser.UserId;
            AddAction(document, "Issued", before, document.Status.ToString(), correlation,
                Trim(request.Comment, 1000), Serialize(new
                {
                    CentralDocumentRecordId = record.Id,
                    CentralDocumentVersionId = version.Id,
                    upload.FilePath
                }));
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteFileAsync(upload.FilePath);
            throw;
        }

        await ReconcileDocumentAsync(document, config, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(document.PurchaseOrderReceipt, "Issued", ProcurementControlEventResult.Succeeded,
            $"{KindLabel(document.DocumentKind)} {document.DocumentNumber} issued to the central DMS.", correlation,
            new { document.Id, document.CentralDocumentRecordId, document.CentralDocumentVersionId }, cancellationToken);
        await PublishAsync(document, cancellationToken);
        return Map(document, config);
    }

    public async Task<ProcurementReceiptDocumentDto> CancelAsync(
        Guid documentId,
        CancelProcurementReceiptDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = Correlation(correlationId);
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        await EnsureCapabilityAsync(IssuePermission, document.PurchaseOrderReceipt, correlation, cancellationToken);
        if (document.Status == ProcurementReceiptDocumentStatus.Cancelled) return Map(document, DeserializeDecision(document.DecisionSnapshotJson));
        EnsureRowVersion(document.RowVersion, request.RowVersion);
        var now = DateTime.UtcNow;
        var before = document.Status.ToString();
        document.Status = ProcurementReceiptDocumentStatus.Cancelled;
        document.ReconciliationStatus = ProcurementReceiptDocumentReconciliationStatus.Cancelled;
        document.ReconciliationMessage = "Cancelled with immutable DMS and action history retained.";
        document.ReconciledAtUtc = now;
        document.CancelledAtUtc = now;
        document.CancelledByUserId = _currentUser.UserId;
        document.CancelledByName = ActorName;
        document.CancellationReason = request.Reason.Trim();
        document.UpdatedAt = now;
        document.UpdatedBy = ActorName;
        document.LastModifiedById = _currentUser.UserId;
        AddAction(document, "Cancelled", before, document.Status.ToString(), correlation,
            request.Reason.Trim(), Serialize(new { document.CentralDocumentRecordId, document.CentralDocumentVersionId }));
        if (document.CentralDocumentRecordId.HasValue)
        {
            var dmsRecord = await _db.CentralDocumentRecords.SingleOrDefaultAsync(item =>
                item.TenantId == _currentUser.TenantId && item.Id == document.CentralDocumentRecordId && !item.IsDeleted,
                cancellationToken);
            if (dmsRecord is not null)
            {
                dmsRecord.LifecycleStatus = "Cancelled";
                dmsRecord.RetentionStatus = "Retained";
                dmsRecord.UpdatedAt = now;
                dmsRecord.UpdatedBy = ActorName;
                dmsRecord.LastModifiedById = _currentUser.UserId;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(document.PurchaseOrderReceipt, "Cancelled", ProcurementControlEventResult.Succeeded,
            request.Reason.Trim(), correlation, new { document.Id, document.DocumentNumber }, cancellationToken);
        return Map(document, DeserializeDecision(document.DecisionSnapshotJson));
    }

    public async Task<ProcurementReceiptDocumentOverviewDto> ReconcileAsync(
        Guid receiptId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = Correlation(correlationId);
        var receipt = await LoadReceiptAsync(receiptId, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, receipt, correlation, cancellationToken);
        var documents = await DocumentQuery(true).Where(item => item.PurchaseOrderReceiptId == receiptId)
            .ToListAsync(cancellationToken);
        if (documents.Count == 0)
            throw Validation("RCV_DOCUMENT_REGISTER_MISSING", "No DEC-013 receipt-document register exists for this receipt.");
        var config = DeserializeDecision(documents[0].DecisionSnapshotJson);
        foreach (var document in documents) await ReconcileDocumentAsync(document, config, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(receipt, "Reconciled", documents.All(item =>
                item.ReconciliationStatus is ProcurementReceiptDocumentReconciliationStatus.Reconciled or ProcurementReceiptDocumentReconciliationStatus.Cancelled)
                ? ProcurementControlEventResult.Succeeded : ProcurementControlEventResult.Warning,
            "GRN/MRN register reconciled against receipt, inspection, signatory and DMS state.", correlation,
            documents.Select(item => new { item.Id, item.DocumentKind, item.Status, item.ReconciliationStatus }), cancellationToken);
        return await BuildOverviewAsync(receipt, documents, config, cancellationToken,
            documents[0].ConfigurationProfileId, documents[0].ConfigurationProfileVersion);
    }

    public async Task<ProcurementReceiptDocumentFileDto> DownloadAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        await EnsureCapabilityAsync(ReadPermission, document.PurchaseOrderReceipt, Correlation(null), cancellationToken);
        if (document.Status != ProcurementReceiptDocumentStatus.Issued || !document.CentralDocumentVersionId.HasValue)
            throw Conflict("RCV_DOCUMENT_NOT_ISSUED", "Only an issued receipt document can be downloaded.");
        var version = await _db.CentralDocumentVersions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == _currentUser.TenantId && item.Id == document.CentralDocumentVersionId && !item.IsDeleted,
            cancellationToken) ?? throw new ProcurementReceiptDocumentNotFoundException("The central DMS version was not found.");
        if (string.IsNullOrWhiteSpace(version.RepositoryPath))
            throw Conflict("RCV_DOCUMENT_FILE_MISSING", "The central DMS version has no repository file.");
        await using var stream = await _storage.DownloadFileAsync(version.RepositoryPath, version.FileUploadRecordId ?? version.Id);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return new ProcurementReceiptDocumentFileDto
        {
            Content = buffer.ToArray(),
            ContentType = version.ContentType ?? "application/pdf",
            FileName = version.FileName ?? $"{SafeFileName(document.DocumentNumber)}.pdf"
        };
    }

    public async Task<byte[]> GenerateGrnAsync(Guid receiptId)
    {
        var overview = await GetOverviewAsync(receiptId);
        var grn = overview.Documents.FirstOrDefault(item => item.DocumentKind == ProcurementReceiptDocumentKind.Grn)
                  ?? throw new ProcurementReceiptDocumentNotFoundException("A GRN is not configured for this receipt.");
        return (await DownloadAsync(grn.Id)).Content;
    }

    public async Task<ProcurementReceiptDocumentFileDto> DownloadGrnAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await LoadReceiptAsync(
            receiptId, false, cancellationToken);
        var registeredGrn = await DocumentQuery(false)
            .Where(item =>
                item.PurchaseOrderReceiptId == receipt.Id &&
                item.DocumentKind == ProcurementReceiptDocumentKind.Grn)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (registeredGrn is not null)
            return await DownloadAsync(registeredGrn.Id, cancellationToken);

        await EnsureCapabilityAsync(
            ReadPermission, receipt, Correlation(null), cancellationToken);

        if (receipt.PurchaseOrder.ProcurementSourceType !=
            ProcurementPurchaseOrderSourceType.HistoricalMigration)
        {
            throw new ProcurementReceiptDocumentNotFoundException(
                "A GRN is not configured for this receipt.");
        }

        var locationIds = receipt.Items
            .Where(item => item.LocationId.HasValue &&
                           item.LocationId.Value != Guid.Empty)
            .Select(item => item.LocationId!.Value)
            .Distinct()
            .ToList();
        var locations = locationIds.Count == 0
            ? new Dictionary<Guid, WarehouseLocation>()
            : await _db.WarehouseLocations
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == _currentUser.TenantId &&
                    locationIds.Contains(item.Id) &&
                    !item.IsDeleted)
                .Include(item => item.Warehouse)
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        return new ProcurementReceiptDocumentFileDto
        {
            Content = BuildHistoricalGrnPdf(receipt, locations),
            ContentType = "application/pdf",
            FileName = $"GRN-{SafeFileName(receipt.ReceiptNumber)}.pdf"
        };
    }

    private async Task<ProcurementReceiptDocumentOverviewDto> BuildOverviewAsync(
        PurchaseOrderReceipt receipt,
        IReadOnlyList<ProcurementReceiptDocument> documents,
        ProcurementReceiptDocumentDecisionValueDto config,
        CancellationToken cancellationToken,
        Guid configurationProfileId,
        int configurationProfileVersion)
    {
        var inspection = await LatestInspectionAsync(receipt.Id, cancellationToken);
        var required = RequiredKinds(config.DocumentType).ToList();
        var evidence = inspection is null
            ? Array.Empty<string>()
            : await EvidenceKeysAsync(inspection.Id, cancellationToken);
        var missingEvidence = ProcurementReceiptDocumentRules.MissingRequirements(config.EvidenceRequirements, evidence);
        var checks = new List<ProcurementReceiptDocumentCheckDto>
        {
            Check("SOURCE_PO", "Source purchase order", receipt.PurchaseOrderId != Guid.Empty && !string.IsNullOrWhiteSpace(receipt.PurchaseOrder.OrderNumber), "A tenant-valid purchase order is linked."),
            Check("INSPECTION", "Inspection/acceptance", inspection is not null && inspection.Status is ProcurementReceiptInspectionStatus.Approved or ProcurementReceiptInspectionStatus.Closed, inspection is null ? "No governed inspection exists." : $"Inspection is {inspection.Status}."),
            Check("DOCUMENT_SET", "Configured GRN/MRN set", required.All(kind => documents.Any(item => item.DocumentKind == kind)), $"DEC-013 requires {string.Join(" and ", required.Select(KindLabel))}."),
            Check("UNIQUE_NUMBERS", "Unique document numbers", documents.Select(item => item.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() == documents.Count, "Every register entry must have a unique number."),
            Check("EVIDENCE", "DEC-013 evidence", missingEvidence.Count == 0, missingEvidence.Count == 0 ? "All configured evidence is linked to the governed inspection." : $"Missing: {string.Join(", ", missingEvidence)}."),
            Check("DMS", "Central DMS", documents.Where(item => item.Status == ProcurementReceiptDocumentStatus.Issued).All(item => item.CentralDocumentRecordId.HasValue && item.CentralDocumentVersionId.HasValue), "Every issued document must have a central DMS record and version.")
        };
        return new ProcurementReceiptDocumentOverviewDto
        {
            PurchaseOrderReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            PurchaseOrderNumber = receipt.PurchaseOrder.OrderNumber,
            SupplierName = receipt.PurchaseOrder.BusinessPartner?.PartnerName ?? string.Empty,
            ReceiptStatus = receipt.Status,
            InspectionStatus = inspection?.Status.ToString() ?? "Missing",
            ConfigurationProfileId = configurationProfileId,
            ConfigurationProfileVersion = configurationProfileVersion,
            ConfiguredDocumentType = config.DocumentType,
            CoexistenceRule = config.CoexistenceRule,
            DecisionKeys = DecisionKeys,
            RequiredEvidence = config.EvidenceRequirements.Select(value => value.Trim()).Where(value => value.Length > 0).ToList(),
            AvailableEvidence = evidence,
            Checks = checks,
            Documents = documents.OrderBy(item => item.DocumentKind).Select(item => Map(item, config)).ToList()
        };
    }

    private ProcurementReceiptDocumentDto Map(ProcurementReceiptDocument item, ProcurementReceiptDocumentDecisionValueDto config) => new()
    {
        Id = item.Id,
        DocumentKind = item.DocumentKind,
        DocumentNumber = item.DocumentNumber,
        TemplateCode = item.TemplateCode,
        Status = item.Status,
        ReconciliationStatus = item.ReconciliationStatus,
        ReconciliationMessage = item.ReconciliationMessage,
        ReconciledAtUtc = item.ReconciledAtUtc,
        PreparedByName = item.PreparedByName,
        PreparedAtUtc = item.PreparedAtUtc,
        IssuedByName = item.IssuedByName,
        IssuedAtUtc = item.IssuedAtUtc,
        CancelledByName = item.CancelledByName,
        CancelledAtUtc = item.CancelledAtUtc,
        CancellationReason = item.CancellationReason,
        CentralDocumentRecordId = item.CentralDocumentRecordId,
        CentralDocumentVersionId = item.CentralDocumentVersionId,
        PdfUrl = item.Status == ProcurementReceiptDocumentStatus.Issued ? $"/api/ProcurementReceiptDocuments/{item.Id}/download" : null,
        SourceIntegrityHash = item.SourceIntegrityHash,
        RequiredSignatures = config.SignatureRequirements.Select(value => value.Trim()).Where(value => value.Length > 0).ToList(),
        Signatures = item.Signatures.OrderBy(value => value.SignedAtUtc).Select(value => new ProcurementReceiptDocumentSignatureDto
        {
            Id = value.Id, RequiredRole = value.RequiredRole, SignedByUserId = value.SignedByUserId,
            SignedByName = value.SignedByName, SignedAtUtc = value.SignedAtUtc, Comment = value.Comment,
            IntegrityHash = value.IntegrityHash
        }).ToList(),
        Actions = item.Actions.OrderByDescending(value => value.OccurredAtUtc).Select(value => new ProcurementReceiptDocumentActionDto
        {
            Id = value.Id, Action = value.Action, FromStatus = value.FromStatus, ToStatus = value.ToStatus,
            ActorName = value.ActorName, OccurredAtUtc = value.OccurredAtUtc, Reason = value.Reason,
            IntegrityHash = value.IntegrityHash
        }).ToList(),
        AllowedActions = AllowedActions(item, config),
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private static IReadOnlyList<string> AllowedActions(ProcurementReceiptDocument document, ProcurementReceiptDocumentDecisionValueDto config)
    {
        if (document.Status == ProcurementReceiptDocumentStatus.Cancelled) return Array.Empty<string>();
        if (document.Status == ProcurementReceiptDocumentStatus.Issued) return ["download", "cancel", "reconcile"];
        var result = new List<string> { "reconcile", "cancel" };
        if (config.SignatureRequirements.Any(role => !document.Signatures.Any(signature => string.Equals(signature.RequiredRole, role, StringComparison.OrdinalIgnoreCase)))) result.Add("sign");
        result.Add("issue");
        return result;
    }

    private async Task ReconcileDocumentAsync(ProcurementReceiptDocument document, ProcurementReceiptDocumentDecisionValueDto config, CancellationToken cancellationToken)
    {
        if (document.Status == ProcurementReceiptDocumentStatus.Cancelled)
        {
            document.ReconciliationStatus = ProcurementReceiptDocumentReconciliationStatus.Cancelled;
            document.ReconciliationMessage = "Cancelled with immutable DMS and action history retained.";
            document.ReconciledAtUtc = DateTime.UtcNow;
            return;
        }
        var inspection = await LatestInspectionAsync(document.PurchaseOrderReceiptId, cancellationToken);
        var missing = ProcurementReceiptDocumentRules.MissingRequirements(
            config.SignatureRequirements,
            document.Signatures.Select(signature => signature.RequiredRole));
        var evidence = inspection is null
            ? Array.Empty<string>()
            : await EvidenceKeysAsync(inspection.Id, cancellationToken);
        var missingEvidence = ProcurementReceiptDocumentRules.MissingRequirements(config.EvidenceRequirements, evidence);
        var errors = new List<string>();
        if (!ProcurementReceiptDocumentRules.IsInspectionApproved(inspection?.Status)) errors.Add("inspection not approved/closed");
        if (missing.Count > 0) errors.Add($"missing signatures: {string.Join(", ", missing)}");
        if (missingEvidence.Count > 0) errors.Add($"missing evidence: {string.Join(", ", missingEvidence)}");
        if (ProcurementReceiptDocumentRules.RequiresPriorGrnIssue(document.DocumentKind, config.CoexistenceRule) &&
            !await _db.ProcurementReceiptDocuments.AsNoTracking().AnyAsync(item =>
                item.TenantId == _currentUser.TenantId && item.PurchaseOrderReceiptId == document.PurchaseOrderReceiptId &&
                item.DocumentKind == ProcurementReceiptDocumentKind.Grn && item.Status == ProcurementReceiptDocumentStatus.Issued && !item.IsDeleted,
                cancellationToken)) errors.Add("GRN must be issued before MRN");
        if (document.Status != ProcurementReceiptDocumentStatus.Issued)
        {
            document.ReconciliationStatus = ProcurementReceiptDocumentReconciliationStatus.Pending;
            document.ReconciliationMessage = errors.Count == 0 ? "Ready for issue; final DMS reconciliation is pending." : string.Join("; ", errors);
            document.ReconciledAtUtc = DateTime.UtcNow;
            return;
        }
        if (document.Status == ProcurementReceiptDocumentStatus.Issued && (!document.CentralDocumentRecordId.HasValue || !document.CentralDocumentVersionId.HasValue)) errors.Add("issued document missing DMS lineage");
        document.ReconciliationStatus = errors.Count == 0
            ? ProcurementReceiptDocumentReconciliationStatus.Reconciled
            : ProcurementReceiptDocumentReconciliationStatus.Exception;
        document.ReconciliationMessage = errors.Count == 0 ? "Receipt, inspection, signatories, number, template and DMS lineage reconcile." : string.Join("; ", errors);
        document.ReconciledAtUtc = DateTime.UtcNow;
    }

    private async Task<ProcurementReceiptDocument> LoadDocumentAsync(Guid id, CancellationToken cancellationToken) =>
        await DocumentQuery(true).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new ProcurementReceiptDocumentNotFoundException("The receipt document was not found in the current tenant.");

    private IQueryable<ProcurementReceiptDocument> DocumentQuery(bool tracked)
    {
        var query = _db.ProcurementReceiptDocuments
            .Where(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.PurchaseOrderReceipt).ThenInclude(item => item.PurchaseOrder).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.PurchaseOrderReceipt).ThenInclude(item => item.Items).ThenInclude(item => item.PurchaseOrderItem).ThenInclude(item => item.InventoryItem)
            .Include(item => item.Signatures).Include(item => item.Actions);
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<PurchaseOrderReceipt> LoadReceiptAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        IQueryable<PurchaseOrderReceipt> query = _db.PurchaseOrderReceipts
            .Where(item => item.TenantId == _currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .Include(item => item.PurchaseOrder).ThenInclude(item => item.BusinessPartner)
            .Include(item => item.ReceivedBy).Include(item => item.InspectedBy)
            .Include(item => item.Items).ThenInclude(item => item.PurchaseOrderItem)
                .ThenInclude(item => item.InventoryItem)
            .Include(item => item.Items).ThenInclude(item => item.PurchaseOrderItem)
                .ThenInclude(item => item.Warehouse);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
               ?? throw new ProcurementReceiptDocumentNotFoundException("The receipt was not found in the current tenant.");
    }

    private Task<ProcurementReceiptInspectionCase?> LatestInspectionAsync(Guid receiptId, CancellationToken cancellationToken) =>
        _db.ProcurementReceiptInspectionCases.AsNoTracking()
            .Where(item => item.TenantId == _currentUser.TenantId && item.PurchaseOrderReceiptId == receiptId && !item.IsDeleted && item.Status != ProcurementReceiptInspectionStatus.Cancelled)
            .OrderByDescending(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);

    private async Task<string[]> EvidenceKeysAsync(Guid inspectionCaseId, CancellationToken cancellationToken) =>
        await _db.ProcurementReceiptInspectionEvidence.AsNoTracking()
            .Where(item => item.TenantId == _currentUser.TenantId && item.InspectionCaseId == inspectionCaseId && !item.IsDeleted)
            .Select(item => item.RequirementKey)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    private async Task<Governance> LoadGovernanceAsync(CancellationToken cancellationToken)
    {
        var profile = await _configuration.GetEffectiveProfileAsync("TDC-PROCUREMENT", DateTime.UtcNow, cancellationToken)
                      ?? throw Validation("RCV_CONFIGURATION_MISSING", "No effective Published TDC procurement configuration profile exists.");
        if (profile.Decisions.Count != 14 || profile.Decisions.Any(item => !item.IsComplete))
            throw Validation("RCV_CONFIGURATION_INCOMPLETE", "The effective configuration must contain fourteen complete approved decisions.");
        var decision = profile.Decisions.SingleOrDefault(item => item.DecisionKey == "DEC-013")
                       ?? throw Validation("RCV_DEC013_MISSING", "DEC-013 is missing from the effective configuration.");
        return new Governance(profile, decision, DeserializeDecision(decision.Value.GetRawText()));
    }

    private async Task<Governance> LoadStoredGovernanceAsync(
        IReadOnlyList<ProcurementReceiptDocument> documents,
        CancellationToken cancellationToken)
    {
        var source = documents[0];
        if (documents.Any(item => item.ConfigurationProfileId != source.ConfigurationProfileId ||
                                  item.ConfigurationProfileVersion != source.ConfigurationProfileVersion ||
                                  item.ConfigurationDecisionId != source.ConfigurationDecisionId ||
                                  item.DecisionSnapshotJson != source.DecisionSnapshotJson))
            throw Conflict("RCV_DOCUMENT_CONFIGURATION_MIXED", "Receipt-document entries do not share one immutable DEC-013 snapshot.");
        var profile = await _configuration.GetProfileAsync(source.ConfigurationProfileId, cancellationToken);
        if (profile.Version != source.ConfigurationProfileVersion)
            throw Validation("RCV_DOCUMENT_CONFIGURATION_INVALID", "The stored configuration-profile version no longer matches its receipt-document snapshot.");
        var decision = profile.Decisions.SingleOrDefault(item => item.Id == source.ConfigurationDecisionId && item.DecisionKey == "DEC-013")
                       ?? throw Validation("RCV_DOCUMENT_CONFIGURATION_INVALID", "The stored DEC-013 lineage no longer resolves.");
        return new Governance(profile, decision, DeserializeDecision(source.DecisionSnapshotJson));
    }

    private async Task<CentralDocumentGenerationTemplate> EnsureTemplateAsync(string code, ProcurementReceiptDocumentKind kind, CancellationToken cancellationToken)
    {
        var template = await _db.CentralDocumentGenerationTemplates.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == _currentUser.TenantId && item.TemplateCode == code && item.IsActive && !item.IsDeleted,
            cancellationToken);
        if (template is null)
            throw Validation("RCV_DOCUMENT_TEMPLATE_MISSING", $"Active central DMS template {code} was not found for this tenant.");
        var expected = kind == ProcurementReceiptDocumentKind.Grn ? "GoodsReceiptNote" : "MaterialReceiptNote";
        if (!string.Equals(template.Module, "Procurement", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(template.DocumentType, expected, StringComparison.OrdinalIgnoreCase))
            throw Validation("RCV_DOCUMENT_TEMPLATE_INVALID", $"Template {code} is not an active Procurement {expected} template.");
        return template;
    }

    private async Task EnsureCapabilityAsync(string permission, PurchaseOrderReceipt receipt, string correlation, CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementReceiptDocumentAuthorizationException("External users cannot administer internal receipt documents.");
        if (IsAdministrator()) return;
        var warehouseId = await ResolveWarehouseIdAsync(
            receipt, cancellationToken);
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = EventType,
            SourceReference = receipt.ReceiptNumber,
            WarehouseId = warehouseId
        }, correlation, cancellationToken);
        if (!decision.Allowed) throw new ProcurementReceiptDocumentAuthorizationException(decision.Message);
    }

    private async Task<Guid?> ResolveWarehouseIdAsync(
        PurchaseOrderReceipt receipt,
        CancellationToken cancellationToken)
    {
        var warehouseId = receipt.PurchaseOrder.DeliveryWarehouseId ??
                          receipt.Items
                              .Select(item => item.PurchaseOrderItem.WarehouseId)
                              .FirstOrDefault(item => item.HasValue &&
                                                      item != Guid.Empty);
        if (warehouseId.HasValue)
            return warehouseId;

        var locationIds = receipt.Items
            .Select(item => item.LocationId)
            .Where(item => item.HasValue && item.Value != Guid.Empty)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        if (locationIds.Count == 0)
            return null;

        return await _db.WarehouseLocations
            .AsNoTracking()
            .Where(item =>
                item.TenantId == _currentUser.TenantId &&
                locationIds.Contains(item.Id) &&
                !item.IsDeleted)
            .Select(item => (Guid?)item.InventoryWarehouseId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementReceiptDocumentAuthorizationException("An authenticated tenant user is required.");
    }

    private async Task RecordEventAsync(PurchaseOrderReceipt receipt, string action, ProcurementControlEventResult result, string reason, string correlation, object values, CancellationToken cancellationToken)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("receipt-document", _currentUser.TenantId, receipt.Id, action, correlation),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "RCV-005/TDC-0509",
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = nameof(PurchaseOrderReceipt),
            SourceId = receipt.Id,
            SourceReference = receipt.ReceiptNumber,
            Reason = reason,
            ResultValues = values,
            CorrelationId = correlation,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task PublishAsync(ProcurementReceiptDocument document, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new NotificationTopicEvent
            {
                TenantId = _currentUser.TenantId,
                TopicKey = "procurement.receipt-document.issued",
                NotificationType = "ReceiptDocumentIssued",
                EntityType = nameof(ProcurementReceiptDocument),
                EntityId = document.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["documentNumber"] = document.DocumentNumber,
                    ["documentKind"] = document.DocumentKind.ToString(),
                    ["receiptNumber"] = document.PurchaseOrderReceipt.ReceiptNumber
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Receipt document {DocumentId} issued but its notification could not be published", document.Id);
        }
    }

    private static byte[] BuildPdf(ProcurementReceiptDocument document, CentralDocumentGenerationTemplate template, ProcurementReceiptInspectionCase inspection)
    {
        var receipt = document.PurchaseOrderReceipt;
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(column =>
            {
                column.Item().Text(KindLabel(document.DocumentKind).ToUpperInvariant()).Bold().FontSize(18);
                column.Item().Text(document.DocumentNumber).FontSize(12);
                column.Item().Text($"Template: {template.TemplateCode} | PO: {receipt.PurchaseOrder.OrderNumber}");
                column.Item().PaddingTop(8).LineHorizontal(1);
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(template.Body)) column.Item().Text(template.Body).Italic();
                column.Item().PaddingTop(10).Text($"Supplier: {receipt.PurchaseOrder.BusinessPartner?.PartnerName ?? "N/A"}");
                column.Item().Text($"Receipt: {receipt.ReceiptNumber} dated {receipt.ReceiptDate:dd-MMM-yyyy}");
                column.Item().Text($"Inspection/acceptance: {inspection.Status}");
                column.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(4); columns.RelativeColumn(); columns.RelativeColumn(); });
                    table.Header(header =>
                    {
                        foreach (var value in new[] { "Item", "Description", "Received", "Accepted" })
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(value).Bold();
                    });
                    foreach (var line in receipt.Items.OrderBy(item => item.Id))
                    {
                        table.Cell().Padding(5).Text(line.PurchaseOrderItem.InventoryItem?.ItemCode ?? "-");
                        table.Cell().Padding(5).Text(line.PurchaseOrderItem.InventoryItem?.Name ?? line.PurchaseOrderItem.ItemDescription ?? "-");
                        table.Cell().Padding(5).Text(line.ReceivedQuantity.ToString("N2"));
                        table.Cell().Padding(5).Text(line.AcceptedQuantity.ToString("N2"));
                    }
                });
                column.Item().PaddingTop(18).Text("Signatories").Bold();
                foreach (var signature in document.Signatures.OrderBy(item => item.SignedAtUtc))
                    column.Item().Text($"{signature.RequiredRole}: {signature.SignedByName} at {signature.SignedAtUtc:u}");
                column.Item().PaddingTop(15).Text($"Integrity: {document.SourceIntegrityHash}").FontSize(7);
            });
            page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
        })).GeneratePdf();
    }

    private static byte[] BuildHistoricalGrnPdf(
        PurchaseOrderReceipt receipt,
        IReadOnlyDictionary<Guid, WarehouseLocation> locations) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(column =>
            {
                column.Item().Text("GOODS RECEIVED NOTE (HISTORICAL)")
                    .Bold().FontSize(18);
                column.Item().Text(
                    $"GRN: {receipt.ReceiptNumber} | PO: {receipt.PurchaseOrder.OrderNumber}");
                column.Item().Text(
                    $"Receipt date: {receipt.ReceiptDate:dd-MMM-yyyy} | Status: {receipt.Status}");
                column.Item().PaddingTop(8).LineHorizontal(1);
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Item().Text(
                    $"Supplier: {receipt.PurchaseOrder.BusinessPartner?.PartnerName ?? "N/A"}");
                if (!string.IsNullOrWhiteSpace(receipt.DeliveryNote))
                    column.Item().Text($"Delivery note: {receipt.DeliveryNote}");
                if (!string.IsNullOrWhiteSpace(receipt.Notes))
                    column.Item().PaddingTop(6).Text($"Notes: {receipt.Notes}");

                column.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        foreach (var value in new[]
                                 { "Item", "Description", "UOM", "Received", "Location" })
                        {
                            header.Cell().Background(Colors.Grey.Lighten3)
                                .Padding(5).Text(value).Bold();
                        }
                    });
                    foreach (var line in receipt.Items.OrderBy(item => item.Id))
                    {
                        var inventoryItem = line.PurchaseOrderItem.InventoryItem;
                        var location = line.LocationId.HasValue &&
                                       locations.TryGetValue(
                                           line.LocationId.Value,
                                           out var resolved)
                            ? $"{resolved.Warehouse?.Code} / {resolved.LocationCode}"
                                .Trim(' ', '/')
                            : line.PurchaseOrderItem.Warehouse?.Code ?? "-";
                        table.Cell().Padding(5)
                            .Text(inventoryItem?.ItemCode ?? "-");
                        table.Cell().Padding(5).Text(
                            inventoryItem?.Name ??
                            line.PurchaseOrderItem.ItemDescription ?? "-");
                        table.Cell().Padding(5).Text(
                            line.UnitOfMeasure ??
                            line.PurchaseOrderItem.UnitOfMeasure ?? "-");
                        table.Cell().Padding(5).AlignRight()
                            .Text(line.ReceivedQuantity.ToString("N2"));
                        table.Cell().Padding(5).Text(location);
                    }
                });
                column.Item().PaddingTop(16)
                    .Text("Generated from a pre-governance historical receipt. New receipts must use the controlled DEC-013 document register.")
                    .Italic().FontSize(8);
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();

    private static string SourceSnapshot(PurchaseOrderReceipt receipt, ProcurementReceiptDocumentKind kind, string number, Governance governance) => Serialize(new
    {
        schemaVersion = "tdc.receipt-document.v1",
        receipt = new { receipt.Id, receipt.ReceiptNumber, receipt.ReceiptDate, receipt.PurchaseOrderId, receipt.ReceiptSourceIntegrityHash },
        purchaseOrder = new { receipt.PurchaseOrder.Id, receipt.PurchaseOrder.OrderNumber, receipt.PurchaseOrder.BusinessPartnerId },
        document = new { kind, number },
        configuration = new { governance.Profile.Id, governance.Profile.Version, DecisionId = governance.Decision.Id },
        lines = receipt.Items.OrderBy(item => item.Id).Select(item => new { item.Id, item.PurchaseOrderItemId, item.ReceivedQuantity, item.AcceptedQuantity, item.RejectedQuantity, item.ReceiptLineIntegrityHash })
    });

    private static ProcurementReceiptDocumentDecisionValueDto DeserializeDecision(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProcurementReceiptDocumentDecisionValueDto>(json, JsonOptions)
                   ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Validation("RCV_DEC013_INVALID", "The effective DEC-013 receipt-document value is invalid.");
        }
    }

    private static IEnumerable<ProcurementReceiptDocumentKind> RequiredKinds(ProcurementReceiptDocumentType type)
    {
        try { return ProcurementReceiptDocumentRules.RequiredKinds(type); }
        catch (ArgumentOutOfRangeException) { throw Validation("RCV_DEC013_DOCUMENT_TYPE_INVALID", "DEC-013 has an unsupported receipt-document type."); }
    }

    private void AddAction(ProcurementReceiptDocument document, string action, string from, string to, string correlation, string? reason, string details)
    {
        var now = DateTime.UtcNow;
        var hash = Hash(Serialize(new { document.Id, action, from, to, correlation, reason, details, now }));
        document.Actions.Add(new ProcurementReceiptDocumentAction
        {
            TenantId = document.TenantId,
            ReceiptDocumentId = document.Id,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = _currentUser.UserId,
            ActorName = ActorName,
            OccurredAtUtc = now,
            CorrelationId = correlation,
            Reason = Trim(reason, 1000),
            DetailsJson = Serialize(new { value = details }),
            IntegrityHash = hash,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId
        });
    }

    private static ProcurementReceiptDocumentCheckDto Check(string code, string label, bool passed, string message) => new() { Code = code, Label = label, Passed = passed, Message = message };
    private static string KindLabel(ProcurementReceiptDocumentKind kind) => kind == ProcurementReceiptDocumentKind.Grn ? "Goods Receipt Note (GRN)" : "Material Receipt Note (MRN)";
    private static string ResetPolicy(string format) => format.Contains("{MM}", StringComparison.OrdinalIgnoreCase) ? "Monthly" : format.Contains("{YYYY}", StringComparison.OrdinalIgnoreCase) || format.Contains("{YY}", StringComparison.OrdinalIgnoreCase) ? "Yearly" : "Never";
    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string SafeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        try { if (!current.SequenceEqual(Convert.FromBase64String(supplied))) throw Conflict("RCV_DOCUMENT_ROW_VERSION_CONFLICT", "The receipt document changed. Reload and retry."); }
        catch (FormatException) { throw Validation("RCV_DOCUMENT_ROW_VERSION_INVALID", "The receipt-document row version is invalid."); }
    }
    private static ProcurementReceiptDocumentValidationException Validation(string code, string message) => new(code, message);
    private static ProcurementReceiptDocumentConflictException Conflict(string code, string message) => new(code, message);
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
    private sealed record Governance(ProcurementConfigurationProfileDto Profile, ProcurementConfigurationDecisionDto Decision, ProcurementReceiptDocumentDecisionValueDto Value);
}
