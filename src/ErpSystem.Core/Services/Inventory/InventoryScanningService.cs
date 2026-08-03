using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

public sealed class InventoryScanningService : IInventoryScanningService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IInventoryItemIdentifierService _identifiers;
    private readonly IProcurementAccessControlService _access;
    private readonly IGoodsReceiptNoteService _goodsReceipts;
    private readonly IInventoryRequisitionService _requisitions;
    private readonly IInventoryTransferService _transfers;
    private readonly IPhysicalCountService _counts;
    private readonly ILogger<InventoryScanningService> _logger;

    public InventoryScanningService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IInventoryItemIdentifierService identifiers,
        IProcurementAccessControlService access,
        IGoodsReceiptNoteService goodsReceipts,
        IInventoryRequisitionService requisitions,
        IInventoryTransferService transfers,
        IPhysicalCountService counts,
        ILogger<InventoryScanningService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _identifiers = identifiers;
        _access = access;
        _goodsReceipts = goodsReceipts;
        _requisitions = requisitions;
        _transfers = transfers;
        _counts = counts;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InventoryLabelProfileDto>> GetLabelProfilesAsync(CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return await Profiles.Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .AsNoTracking().OrderByDescending(item => item.IsDefault).ThenBy(item => item.Name)
            .Select(item => ToProfileDto(item)).ToListAsync(cancellationToken);
    }

    public async Task<InventoryLabelProfileDto> SaveLabelProfileAsync(
        Guid? id,
        SaveInventoryLabelProfileRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        await RequireLabelProfileMutationCapabilityAsync(correlationId, cancellationToken);
        var name = request.Name.Trim();
        var duplicate = await Profiles.AnyAsync(item => item.TenantId == TenantId && !item.IsDeleted &&
            item.Name == name && (!id.HasValue || item.Id != id.Value), cancellationToken);
        if (duplicate) throw new InventoryScanningException("INV_LABEL_NAME_DUPLICATE", "A label profile with this name already exists.");

        InventoryLabelProfile profile;
        object? before = null;
        if (id.HasValue)
        {
            profile = await Profiles.SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted, cancellationToken)
                ?? throw new InventoryScanningException("INV_LABEL_PROFILE_NOT_FOUND", "The label profile was not found in the current tenant.");
            if (string.IsNullOrWhiteSpace(request.RowVersion))
                throw new InventoryScanningException("INV_LABEL_ROW_VERSION_REQUIRED", "RowVersion is required when updating a label profile.");
            byte[] suppliedVersion;
            try { suppliedVersion = Convert.FromBase64String(request.RowVersion); }
            catch (FormatException) { throw new InventoryScanningException("INV_LABEL_ROW_VERSION_INVALID", "RowVersion is invalid."); }
            if (!profile.RowVersion.SequenceEqual(suppliedVersion))
                throw new InventoryScanningException("INV_LABEL_CONCURRENCY_CONFLICT", "The label profile changed after it was loaded. Refresh and retry.");
            before = new { profile.Name, profile.Symbology, profile.WidthMm, profile.HeightMm, profile.IsDefault, profile.IsActive };
        }
        else
        {
            profile = new InventoryLabelProfile { TenantId = TenantId, CreatedById = UserId };
            await _unitOfWork.Repository<InventoryLabelProfile>().AddAsync(profile);
        }

        if (request.IsDefault)
        {
            var currentDefaults = await Profiles.Where(item => item.TenantId == TenantId && item.Id != profile.Id && item.IsDefault && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var current in currentDefaults)
            {
                current.IsDefault = false;
                current.UpdatedAt = DateTime.UtcNow;
                current.LastModifiedById = UserId;
            }
        }

        profile.Name = name;
        profile.Description = Clean(request.Description);
        profile.Symbology = request.Symbology.Trim().ToUpperInvariant();
        profile.WidthMm = request.WidthMm;
        profile.HeightMm = request.HeightMm;
        profile.Dpi = request.Dpi;
        profile.IncludeItemCode = request.IncludeItemCode;
        profile.IncludeItemName = request.IncludeItemName;
        profile.IncludeUnit = request.IncludeUnit;
        profile.IncludeLot = request.IncludeLot;
        profile.IncludeSerial = request.IncludeSerial;
        profile.IncludeExpiry = request.IncludeExpiry;
        profile.IsDefault = request.IsDefault;
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.LastModifiedById = UserId;
        await AddAuditAsync(id.HasValue ? "InventoryLabelProfile.Updated" : "InventoryLabelProfile.Created", profile.Id, before,
            new { profile.Name, profile.Symbology, profile.WidthMm, profile.HeightMm, profile.IsDefault, profile.IsActive }, correlationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToProfileDto(profile);
    }

    public async Task DeleteLabelProfileAsync(Guid id, string correlationId, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        await RequireLabelProfileMutationCapabilityAsync(correlationId, cancellationToken);
        var profile = await Profiles.SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new InventoryScanningException("INV_LABEL_PROFILE_NOT_FOUND", "The label profile was not found in the current tenant.");
        if (profile.IsDefault) throw new InventoryScanningException("INV_LABEL_DEFAULT_DELETE_BLOCKED", "Select another default label profile before deleting this one.");
        profile.IsDeleted = true;
        profile.DeletedAt = DateTime.UtcNow;
        profile.DeletedBy = _currentUser.Username;
        await AddAuditAsync("InventoryLabelProfile.Deleted", profile.Id, new { profile.Name, profile.Symbology }, null, correlationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryLabelCandidateDto>> SearchLabelCandidatesAsync(
        string? query,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        await RequireLabelCandidateReadCapabilityAsync(cancellationToken);
        take = Math.Clamp(take, 1, 100);
        var term = Clean(query);
        var items = Items.Where(item => item.TenantId == TenantId && !item.IsDeleted);
        if (term is not null)
            items = items.Where(item => item.ItemCode.Contains(term) || item.Name.Contains(term) ||
                (item.Barcode != null && item.Barcode.Contains(term)) || (item.AlternateBarcode != null && item.AlternateBarcode.Contains(term)) ||
                (item.QRCode != null && item.QRCode.Contains(term)));
        var selected = await items.AsNoTracking().OrderBy(item => item.ItemCode).Take(take).ToListAsync(cancellationToken);
        var ids = selected.Select(item => item.Id).ToList();
        var units = await ItemUnits.Where(item => item.TenantId == TenantId && ids.Contains(item.InventoryItemId) && !item.IsDeleted && item.IsActive && item.Barcode != null)
            .AsNoTracking().Include(item => item.UnitOfMeasure).ToListAsync(cancellationToken);

        var result = new List<InventoryLabelCandidateDto>();
        foreach (var item in selected)
        {
            AddCandidate(result, item, item.Barcode, "PrimaryBarcode");
            AddCandidate(result, item, item.AlternateBarcode, "AlternateBarcode");
            AddCandidate(result, item, item.QRCode, "QRCode");
            result.AddRange(units.Where(unit => unit.InventoryItemId == item.Id).Select(unit => new InventoryLabelCandidateDto
            {
                InventoryItemId = item.Id, ItemCode = item.ItemCode, ItemName = item.Name,
                Identifier = unit.Barcode!, IdentifierKind = "UnitBarcode", UnitOfMeasureId = unit.UnitOfMeasureId, UnitCode = unit.UnitOfMeasure.Code
            }));
        }
        return result;
    }

    public async Task<InventoryLabelPrintEventDto> RecordLabelPrintAsync(
        RecordInventoryLabelPrintRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var profile = await Profiles.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == request.LabelProfileId && !item.IsDeleted && item.IsActive, cancellationToken)
            ?? throw new InventoryScanningException("INV_LABEL_PROFILE_NOT_FOUND", "The active label profile was not found in the current tenant.");
        var match = await _identifiers.ResolveAsync(TenantId, request.Identifier, cancellationToken)
            ?? throw new InventoryScanningException("INV_LABEL_IDENTIFIER_NOT_FOUND", "The label identifier is not assigned in the current tenant.");
        if (match.InventoryItemId != request.InventoryItemId || match.UnitOfMeasureId != request.UnitOfMeasureId)
            throw new InventoryScanningException("INV_LABEL_IDENTIFIER_MISMATCH", "The selected identifier does not match the requested item and unit.");

        var row = new InventoryLabelPrintEvent
        {
            TenantId = TenantId, LabelProfileId = profile.Id, InventoryItemId = request.InventoryItemId,
            UnitOfMeasureId = request.UnitOfMeasureId, Identifier = match.Identifier, IdentifierKind = match.IdentifierKind,
            LabelCount = request.LabelCount, LotNumber = Clean(request.LotNumber), SerialNumber = Clean(request.SerialNumber),
            ExpiryDate = request.ExpiryDate, PrinterName = Clean(request.PrinterName), PrintedById = UserId,
            PrintedAtUtc = DateTime.UtcNow, CorrelationId = NormalizeCorrelation(correlationId), CreatedById = UserId
        };
        await _unitOfWork.Repository<InventoryLabelPrintEvent>().AddAsync(row);
        await AddAuditAsync("InventoryLabel.Printed", row.Id, null,
            new { row.LabelProfileId, row.InventoryItemId, row.UnitOfMeasureId, row.Identifier, row.IdentifierKind, row.LabelCount, row.PrinterName }, correlationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapPrintAsync(row.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryLabelPrintEventDto>> GetRecentPrintsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 250);
        return await PrintEvents.IgnoreQueryFilters()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                item.LabelProfile.TenantId == TenantId && item.InventoryItem.TenantId == TenantId).AsNoTracking()
            .Include(item => item.LabelProfile).Include(item => item.InventoryItem)
            .OrderByDescending(item => item.PrintedAtUtc).Take(take).Select(item => ToPrintDto(item)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryScanDocumentSummaryDto>> GetDocumentsAsync(
        InventoryScanOperation operation,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 100);
        var allowed = new List<InventoryScanDocumentSummaryDto>(take);
        var offset = 0;
        var pageSize = Math.Max(take, 50);
        while (allowed.Count < take)
        {
            var candidates = await LoadDocumentSummariesAsync(operation, offset, pageSize, cancellationToken);
            foreach (var document in candidates)
            {
                if (await HasCapabilityAsync(operation, document.WarehouseId, document.DocumentReference, false, cancellationToken))
                    allowed.Add(document);
                if (allowed.Count == take) break;
            }
            offset += candidates.Count;
            if (candidates.Count < pageSize) break;
        }
        return allowed;
    }

    public async Task<InventoryScanDocumentContextDto> GetDocumentContextAsync(
        InventoryScanOperation operation,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var context = await LoadDocumentContextAsync(operation, documentId, cancellationToken);
        await RequireCapabilityAsync(operation, context.WarehouseId, context.DocumentReference, cancellationToken);
        foreach (var locationId in context.Lines.Select(item => item.LocationId).Distinct())
            await RequireCapabilityAsync(operation, context.WarehouseId, context.DocumentReference,
                cancellationToken, locationId, requireLocationScope: true);
        return context;
    }

    public async Task<InventoryScanBatchDto> SynchronizeAsync(
        SynchronizeInventoryScanBatchRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var deviceId = Required(request.DeviceId, "INV_SCAN_DEVICE_REQUIRED", "DeviceId is required.");
        var idempotencyKey = Required(request.IdempotencyKey, "INV_SCAN_IDEMPOTENCY_REQUIRED", "IdempotencyKey is required.");
        if (request.Lines.Count == 0) throw new InventoryScanningException("INV_SCAN_LINES_REQUIRED", "At least one scan line is required.");
        if (request.Lines.Select(item => item.ClientLineId).Distinct().Count() != request.Lines.Count)
            throw new InventoryScanningException("INV_SCAN_CLIENT_LINE_DUPLICATE", "ClientLineId values must be unique within a scan batch.");
        var payloadHash = HashPayload(request, deviceId, idempotencyKey);

        var existing = await Batches.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId &&
            item.DeviceId == deviceId && item.IdempotencyKey == idempotencyKey && !item.IsDeleted, cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingPayload(existing, payloadHash);
            return await MapBatchAsync(existing.Id, cancellationToken);
        }

        var context = await LoadDocumentContextAsync(request.Operation, request.DocumentId, cancellationToken);
        if (context.WarehouseId != request.WarehouseId)
            throw new InventoryScanningException("INV_SCAN_WAREHOUSE_MISMATCH", "The requested warehouse does not match the transaction warehouse.");
        await RequireCapabilityAsync(request.Operation, context.WarehouseId, context.DocumentReference, cancellationToken);
        var resolved = await ResolveLinesAsync(request, context, cancellationToken);
        foreach (var locationId in resolved.Select(item => item.LocationId).Distinct())
            await RequireCapabilityAsync(request.Operation, context.WarehouseId, context.DocumentReference,
                cancellationToken, locationId, requireLocationScope: true);
        ValidateQuantities(request, context, resolved);

        Guid batchId = Guid.Empty;
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"inventory-scan:{TenantId:N}:{deviceId}:{idempotencyKey}", cancellationToken);
                var replay = await Batches.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId &&
                    item.DeviceId == deviceId && item.IdempotencyKey == idempotencyKey && !item.IsDeleted, cancellationToken);
                if (replay is not null)
                {
                    EnsureMatchingPayload(replay, payloadHash);
                    batchId = replay.Id;
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return;
                }

                var now = DateTime.UtcNow;
                var batch = new InventoryScanBatch
                {
                    TenantId = TenantId, DeviceId = deviceId, IdempotencyKey = idempotencyKey, PayloadHash = payloadHash,
                    Operation = request.Operation, DocumentId = request.DocumentId, DocumentReference = context.DocumentReference,
                    WarehouseId = context.WarehouseId, ApplyTransaction = request.ApplyTransaction,
                    Status = InventoryScanBatchStatus.Captured, ActorUserId = UserId, CapturedAtUtc = now,
                    CorrelationId = NormalizeCorrelation(correlationId), CreatedById = UserId
                };
                await _unitOfWork.Repository<InventoryScanBatch>().AddAsync(batch);
                for (var index = 0; index < resolved.Count; index++)
                {
                    var line = resolved[index];
                    await _unitOfWork.Repository<InventoryScanLine>().AddAsync(new InventoryScanLine
                    {
                        TenantId = TenantId, ScanBatchId = batch.Id, ClientLineId = line.Input.ClientLineId, Sequence = index + 1,
                        RawIdentifier = line.Match.Identifier, IdentifierKind = line.Match.IdentifierKind,
                        InventoryItemId = line.Match.InventoryItemId, UnitOfMeasureId = line.Match.UnitOfMeasureId,
                        DocumentLineId = line.DocumentLine.DocumentLineId, ScannedQuantity = line.Input.Quantity,
                        ConversionToBase = line.Match.ConversionToBase, BaseQuantity = line.BaseQuantity,
                        LocationId = line.LocationId, LocationIdentifier = Clean(line.Input.LocationIdentifier),
                        LotNumber = Clean(line.Input.LotNumber), BatchNumber = Clean(line.Input.BatchNumber),
                        SerialNumber = Clean(line.Input.SerialNumber), ManufactureDate = Utc(line.Input.ManufactureDate),
                        ExpiryDate = Utc(line.Input.ExpiryDate), InventoryTrackingExceptionId = line.Input.InventoryTrackingExceptionId,
                        ScannedAtUtc = line.Input.ScannedAtUtc == default ? now : line.Input.ScannedAtUtc.ToUniversalTime(), CreatedById = UserId
                    });
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var movementIdsBeforeApply = request.ApplyTransaction
                    ? await MovementIdsForDocumentAsync(request.DocumentId, cancellationToken)
                    : new HashSet<Guid>();
                if (request.ApplyTransaction)
                {
                    await ApplyTransactionAsync(request.Operation, request.DocumentId, resolved,
                        $"scan:{batch.Id:N}", correlationId, cancellationToken);
                    batch.Status = InventoryScanBatchStatus.Applied;
                    batch.ProcessedAtUtc = DateTime.UtcNow;
                }

                var reconciliation = await ReconcileAsync(request.Operation, request.DocumentId, resolved,
                    movementIdsBeforeApply, request.ApplyTransaction, cancellationToken);
                batch.ReconciliationJson = JsonSerializer.Serialize(reconciliation);
                await AddAuditAsync(request.ApplyTransaction ? "InventoryScanBatch.Applied" : "InventoryScanBatch.Captured", batch.Id, null,
                    new { batch.DeviceId, batch.IdempotencyKey, batch.Operation, batch.DocumentId, batch.DocumentReference, batch.WarehouseId,
                        batch.ApplyTransaction, batch.Status, Lines = resolved.Count, reconciliation }, correlationId);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                batchId = batch.Id;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);

        _logger.LogInformation("Synchronized inventory scan batch {BatchId} for {Operation} document {DocumentId}", batchId, request.Operation, request.DocumentId);
        return await MapBatchAsync(batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryScanBatchDto>> GetRecentBatchesAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 250);
        var result = new List<InventoryScanBatchDto>(take);
        var skip = 0;
        var pageSize = Math.Max(50, take);
        while (result.Count < take)
        {
            var page = await BatchQuery()
                .OrderByDescending(item => item.CapturedAtUtc).ThenByDescending(item => item.Id)
                .Skip(skip).Take(pageSize).ToListAsync(cancellationToken);
            if (page.Count == 0) break;
            skip += page.Count;
            foreach (var batch in page)
            {
                if (!await CanReadBatchAsync(batch, auditDenied: false, cancellationToken)) continue;
                result.Add(ToBatchDto(batch));
                if (result.Count == take) break;
            }
        }
        return result;
    }

    public async Task<InventoryScanBatchDto> GetBatchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        return await MapBatchAsync(id, cancellationToken);
    }

    private async Task ApplyTransactionAsync(
        InventoryScanOperation operation,
        Guid documentId,
        IReadOnlyList<ResolvedLine> lines,
        string transactionIdempotencyKey,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var transactionLines = new List<InventoryTransactionScanLineDto>();
        foreach (var group in lines.GroupBy(line => line.DocumentLine.DocumentLineId))
        {
            var locations = group.Select(line => line.LocationId).Distinct().ToList();
            var lots = group.Select(line => Clean(line.Input.LotNumber)).Where(value => value is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var batches = group.Select(line => Clean(line.Input.BatchNumber)).Where(value => value is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var serials = group.Select(line => Clean(line.Input.SerialNumber)).Where(value => value is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var manufactureDates = group.Select(line => Utc(line.Input.ManufactureDate)).Where(value => value.HasValue).Distinct().ToList();
            var expiryDates = group.Select(line => Utc(line.Input.ExpiryDate)).Where(value => value.HasValue).Distinct().ToList();
            var exceptions = group.Select(line => line.Input.InventoryTrackingExceptionId).Where(value => value.HasValue).Distinct().ToList();
            var rowVersions = group.Select(line => Clean(line.Input.DocumentLineRowVersion))
                .Where(value => value is not null).Distinct(StringComparer.Ordinal).ToList();
            if (locations.Count > 1 || lots.Count > 1 || batches.Count > 1 || serials.Count > 1 ||
                manufactureDates.Count > 1 || expiryDates.Count > 1 || exceptions.Count > 1 || rowVersions.Count > 1)
                throw new InventoryScanningException("INV_SCAN_LINE_METADATA_AMBIGUOUS", "Scans for one transaction line must use one location, lot, batch, serial, date, and exception scope.");
            var first = group.First();
            transactionLines.Add(new InventoryTransactionScanLineDto
            {
                DocumentLineId = group.Key, InventoryItemId = first.Match.InventoryItemId,
                BaseQuantity = group.Sum(line => line.BaseQuantity), LocationId = locations.SingleOrDefault(),
                LotNumber = lots.SingleOrDefault(), BatchNumber = batches.SingleOrDefault(), SerialNumber = serials.SingleOrDefault(),
                ManufactureDate = manufactureDates.SingleOrDefault(), ExpiryDate = expiryDates.SingleOrDefault(),
                InventoryTrackingExceptionId = exceptions.SingleOrDefault(),
                DocumentLineRowVersion = rowVersions.SingleOrDefault()
            });
        }

        switch (operation)
        {
            case InventoryScanOperation.GoodsReceipt:
                await _goodsReceipts.ApplyScanMetadataAsync(documentId, transactionLines, UserId);
                await _goodsReceipts.PostToInventoryAsync(documentId, UserId);
                break;
            case InventoryScanOperation.RequisitionIssue:
                await _requisitions.IssueAsync(documentId, new IssueRequisitionDto
                {
                    IdempotencyKey = transactionIdempotencyKey,
                    CorrelationId = correlationId,
                    Notes = "Applied from authenticated mobile scan synchronization.",
                    Items = transactionLines.Select(line => new IssueRequisitionItemDto
                    {
                        ItemId = line.DocumentLineId, IssuedQuantity = line.BaseQuantity, LocationId = line.LocationId,
                        LotNumber = line.LotNumber, BatchNumber = line.BatchNumber, SerialNumber = line.SerialNumber,
                        ManufactureDate = line.ManufactureDate, ExpiryDate = line.ExpiryDate,
                        InventoryTrackingExceptionId = line.InventoryTrackingExceptionId
                    }).ToList()
                });
                break;
            case InventoryScanOperation.RequisitionReturn:
                var returnRowVersion = await _unitOfWork.Repository<InventoryRequisition>().GetQueryable(item =>
                        item.TenantId == TenantId && item.Id == documentId && !item.IsDeleted)
                    .Select(item => item.RowVersion)
                    .SingleAsync(cancellationToken);
                await _requisitions.ReturnAsync(documentId, new ReturnRequisitionDto
                {
                    ReasonCode = InventoryReturnReasonCodes.Unused,
                    Reason = "Unused stock returned through authenticated mobile scanning.",
                    Notes = "Applied from authenticated mobile scan synchronization.",
                    IdempotencyKey = transactionIdempotencyKey,
                    CorrelationId = correlationId,
                    RowVersion = Convert.ToBase64String(returnRowVersion),
                    Items = transactionLines.Select(line => new ReturnRequisitionItemDto
                    {
                        ItemId = line.DocumentLineId, ReturnedQuantity = line.BaseQuantity, LocationId = line.LocationId,
                        LotNumber = line.LotNumber, BatchNumber = line.BatchNumber, SerialNumber = line.SerialNumber,
                        ManufactureDate = line.ManufactureDate, ExpiryDate = line.ExpiryDate,
                        InventoryTrackingExceptionId = line.InventoryTrackingExceptionId
                    }).ToList()
                });
                break;
            case InventoryScanOperation.TransferShipment:
                await _transfers.ApplyScanMetadataAsync(documentId, operation, transactionLines, UserId);
                await _transfers.ShipAsync(documentId, UserId, null,
                    transactionLines.ToDictionary(item => item.DocumentLineId, item => item.BaseQuantity));
                break;
            case InventoryScanOperation.TransferReceipt:
                await _transfers.ApplyScanMetadataAsync(documentId, operation, transactionLines, UserId);
                await _transfers.ReceiveAsync(documentId, UserId, transactionLines.Select(item => new InventoryTransferItemDto
                {
                    Id = item.DocumentLineId, InventoryItemId = item.InventoryItemId, ReceivedQuantity = item.BaseQuantity,
                    DestinationLocationId = item.LocationId, LotNumber = item.LotNumber, BatchNumber = item.BatchNumber,
                    SerialNumber = item.SerialNumber, ManufactureDate = item.ManufactureDate, ExpiryDate = item.ExpiryDate,
                    InventoryTrackingExceptionId = item.InventoryTrackingExceptionId
                }).ToList());
                break;
            case InventoryScanOperation.PhysicalCount:
                foreach (var item in transactionLines.OrderBy(value => value.DocumentLineId))
                {
                    var rowVersion = Clean(item.DocumentLineRowVersion)
                        ?? throw new InventoryScanningException("INV_SCAN_COUNT_ROW_VERSION_REQUIRED",
                            "Each physical-count scan must retain the count-line row version captured with its document context.");
                    await _counts.RecordCountItemAsync(new RecordCountItemDto
                    {
                        PhysicalCountItemId = item.DocumentLineId,
                        CountedQuantity = item.BaseQuantity,
                        LotNumber = item.LotNumber,
                        SerialNumber = item.SerialNumber,
                        Notes = "Recorded from authenticated mobile scan synchronization.",
                        RowVersion = rowVersion,
                        IdempotencyKey = $"{transactionIdempotencyKey}:{item.DocumentLineId:N}"
                    }, UserId);
                }
                break;
            default:
                throw new InventoryScanningException("INV_SCAN_OPERATION_UNSUPPORTED", "The scan operation is not supported.");
        }
        _ = cancellationToken;
    }

    private async Task<List<ResolvedLine>> ResolveLinesAsync(
        SynchronizeInventoryScanBatchRequest request,
        InventoryScanDocumentContextDto context,
        CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedLine>(request.Lines.Count);
        var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in request.Lines.OrderBy(item => item.ScannedAtUtc).ThenBy(item => item.ClientLineId))
        {
            var match = await _identifiers.ResolveAsync(TenantId, input.RawIdentifier, cancellationToken)
                ?? throw new InventoryScanningException("INV_SCAN_IDENTIFIER_NOT_FOUND", $"Identifier '{input.RawIdentifier}' is not assigned in the current tenant.");
            var candidates = context.Lines.Where(line => line.InventoryItemId == match.InventoryItemId &&
                (!input.DocumentLineId.HasValue || line.DocumentLineId == input.DocumentLineId.Value)).ToList();
            if (candidates.Count == 0)
                throw new InventoryScanningException("INV_SCAN_ITEM_NOT_ON_DOCUMENT", $"Item '{match.ItemCode}' is not present on the selected transaction.");
            if (candidates.Count > 1)
                throw new InventoryScanningException("INV_SCAN_DOCUMENT_LINE_REQUIRED", $"Item '{match.ItemCode}' appears more than once; select the document line before scanning.");
            var documentLine = candidates[0];
            var locationId = input.LocationId;
            var locationIdentifier = Clean(input.LocationIdentifier);
            if (!locationId.HasValue && locationIdentifier is not null)
            {
                var normalizedLocation = locationIdentifier.ToUpperInvariant();
                var locationMatches = await Locations.Where(item => item.TenantId == TenantId && !item.IsDeleted && item.IsActive &&
                        (item.WarehouseId == context.WarehouseId ||
                         (item.IsConsignmentBin && item.ConsignmentWarehouseId == context.WarehouseId)) &&
                        (item.LocationBarcode == normalizedLocation || item.LocationCode == normalizedLocation))
                    .Select(item => item.Id).Take(2).ToListAsync(cancellationToken);
                if (locationMatches.Count == 0)
                    throw new InventoryScanningException("INV_SCAN_LOCATION_NOT_FOUND", $"Location identifier '{locationIdentifier}' is not assigned in the transaction warehouse.");
                if (locationMatches.Count > 1)
                    throw new InventoryScanningException("INV_SCAN_LOCATION_AMBIGUOUS", $"Location identifier '{locationIdentifier}' is ambiguous in the transaction warehouse.");
                locationId = locationMatches[0];
            }
            locationId ??= documentLine.LocationId;
            if (locationId.HasValue)
            {
                var valid = await Locations.AnyAsync(item => item.TenantId == TenantId && item.Id == locationId.Value &&
                    (item.WarehouseId == context.WarehouseId || (item.IsConsignmentBin && item.ConsignmentWarehouseId == context.WarehouseId)) &&
                    !item.IsDeleted && item.IsActive, cancellationToken);
                if (!valid) throw new InventoryScanningException("INV_SCAN_LOCATION_INVALID", "The scanned location is not active in the transaction warehouse.");
            }
            if ((request.Operation is InventoryScanOperation.TransferShipment or InventoryScanOperation.TransferReceipt) &&
                documentLine.LocationId.HasValue && locationId != documentLine.LocationId)
                throw new InventoryScanningException("INV_SCAN_TRANSFER_LOCATION_MISMATCH", "The scanned location does not match the approved transfer line.");
            var serial = Clean(input.SerialNumber);
            if (serial is not null && !seenSerials.Add(serial))
                throw new InventoryScanningException("INV_SCAN_SERIAL_DUPLICATE", $"Serial '{serial}' appears more than once in this scan batch.");
            resolved.Add(new ResolvedLine(input, match, documentLine, input.Quantity * match.ConversionToBase, locationId));
        }
        return resolved;
    }

    private static void ValidateQuantities(
        SynchronizeInventoryScanBatchRequest request,
        InventoryScanDocumentContextDto context,
        IReadOnlyList<ResolvedLine> lines)
    {
        foreach (var group in lines.GroupBy(item => item.DocumentLine.DocumentLineId))
        {
            var documentLine = group.First().DocumentLine;
            var scanned = group.Sum(item => item.BaseQuantity);
            if (request.Operation != InventoryScanOperation.PhysicalCount && scanned > documentLine.ExpectedQuantity)
                throw new InventoryScanningException("INV_SCAN_QUANTITY_EXCEEDED", $"Scanned quantity for '{documentLine.ItemCode}' exceeds the remaining transaction quantity.");
        }

        if (!request.ApplyTransaction) return;
        if (request.Operation is InventoryScanOperation.GoodsReceipt or InventoryScanOperation.TransferShipment or InventoryScanOperation.TransferReceipt)
        {
            foreach (var documentLine in context.Lines.Where(line => line.ExpectedQuantity > 0))
            {
                var scanned = lines.Where(item => item.DocumentLine.DocumentLineId == documentLine.DocumentLineId).Sum(item => item.BaseQuantity);
                if (scanned != documentLine.ExpectedQuantity)
                    throw new InventoryScanningException("INV_SCAN_DOCUMENT_INCOMPLETE", $"Scan every remaining quantity for '{documentLine.ItemCode}' before applying this transaction.");
            }
        }
    }

    private async Task<InventoryScanReconciliationDto> ReconcileAsync(
        InventoryScanOperation operation,
        Guid documentId,
        IReadOnlyList<ResolvedLine> lines,
        IReadOnlySet<Guid> movementIdsBeforeApply,
        bool applyTransaction,
        CancellationToken cancellationToken)
    {
        var movements = applyTransaction
            ? (await _unitOfWork.Repository<StockMovement>().GetQueryable(item =>
                    item.TenantId == TenantId && item.ReferenceId == documentId && !item.IsDeleted)
                .AsNoTracking().ToListAsync(cancellationToken))
                .Where(item => !movementIdsBeforeApply.Contains(item.Id)).ToList()
            : [];
        return new InventoryScanReconciliationDto
        {
            DocumentStatus = await LoadDocumentStatusAsync(operation, documentId, cancellationToken),
            ScannedLineCount = lines.Count,
            ScannedBaseQuantity = lines.Sum(item => item.BaseQuantity),
            StockMovementCount = movements.Count,
            NetStockMovementQuantity = movements.Sum(item => item.Quantity),
            AuditedEventCount = 1,
            ReconciledAtUtc = DateTime.UtcNow
        };
    }

    private async Task<HashSet<Guid>> MovementIdsForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        (await _unitOfWork.Repository<StockMovement>().GetQueryable(item =>
                item.TenantId == TenantId && item.ReferenceId == documentId && !item.IsDeleted)
            .AsNoTracking().Select(item => item.Id).ToListAsync(cancellationToken)).ToHashSet();

    private async Task<IReadOnlyList<InventoryScanDocumentSummaryDto>> LoadDocumentSummariesAsync(
        InventoryScanOperation operation,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            InventoryScanOperation.GoodsReceipt => await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(item =>
                    item.TenantId == TenantId && !item.IsDeleted && !item.StockUpdated && item.Status != GRNStatus.Cancelled)
                .AsNoTracking().Include(item => item.Warehouse).OrderByDescending(item => item.ReceiptDate).ThenByDescending(item => item.Id).Skip(skip).Take(take)
                .Select(item => Summary(item.Id, item.GRNNumber, item.Status.ToString(), item.WarehouseId, item.Warehouse.Name, item.ReceiptDate)).ToListAsync(cancellationToken),
            InventoryScanOperation.RequisitionIssue => await RequisitionSummaries(item => item.Status == RequisitionStatus.Approved || item.Status == RequisitionStatus.InProgress || item.Status == RequisitionStatus.PartiallyIssued, skip, take, cancellationToken),
            InventoryScanOperation.RequisitionReturn => await RequisitionSummaries(item => item.Status == RequisitionStatus.PartiallyIssued || item.Status == RequisitionStatus.Issued || item.Status == RequisitionStatus.Completed, skip, take, cancellationToken),
            InventoryScanOperation.TransferShipment => await TransferSummaries(item => item.Status == TransferStatus.Approved || item.Status == TransferStatus.InTransit, true, skip, take, cancellationToken),
            InventoryScanOperation.TransferReceipt => await TransferSummaries(item => item.Status == TransferStatus.InTransit, false, skip, take, cancellationToken),
            InventoryScanOperation.PhysicalCount => await _unitOfWork.Repository<PhysicalCount>().GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted && item.Status == "InProgress")
                .AsNoTracking().Include(item => item.Warehouse).OrderByDescending(item => item.CountDate).ThenByDescending(item => item.Id).Skip(skip).Take(take)
                .Select(item => Summary(item.Id, item.CountNumber, item.Status, item.WarehouseId, item.Warehouse.Name, item.CountDate)).ToListAsync(cancellationToken),
            _ => throw new InventoryScanningException("INV_SCAN_OPERATION_UNSUPPORTED", "The scan operation is not supported.")
        };
    }

    private async Task<List<InventoryScanDocumentSummaryDto>> RequisitionSummaries(
        System.Linq.Expressions.Expression<Func<InventoryRequisition, bool>> predicate,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<InventoryRequisition>().GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
            .Where(predicate).AsNoTracking().Include(item => item.Warehouse).OrderByDescending(item => item.RequestDate).ThenByDescending(item => item.Id).Skip(skip).Take(take)
            .Select(item => Summary(item.Id, item.RequisitionNumber, item.Status.ToString(), item.WarehouseId, item.Warehouse.Name, item.RequestDate)).ToListAsync(cancellationToken);

    private async Task<List<InventoryScanDocumentSummaryDto>> TransferSummaries(
        System.Linq.Expressions.Expression<Func<InventoryTransfer, bool>> predicate,
        bool source,
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await _unitOfWork.Repository<InventoryTransfer>().GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
            .Where(predicate).AsNoTracking().Include(item => item.SourceWarehouse).Include(item => item.DestinationWarehouse)
            .OrderByDescending(item => item.RequestDate).ThenByDescending(item => item.Id).Skip(skip).Take(take)
            .Select(item => Summary(item.Id, item.TransferNumber, item.Status.ToString(), source ? item.SourceWarehouseId : item.DestinationWarehouseId,
                source ? item.SourceWarehouse.Name : item.DestinationWarehouse.Name, item.RequestDate)).ToListAsync(cancellationToken);

    private async Task<InventoryScanDocumentContextDto> LoadDocumentContextAsync(
        InventoryScanOperation operation,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        InventoryScanDocumentContextDto? result;
        switch (operation)
        {
            case InventoryScanOperation.GoodsReceipt:
            {
                var document = await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId && !item.IsDeleted)
                    .AsNoTracking().Include(item => item.Warehouse).Include(item => item.Items).ThenInclude(item => item.InventoryItem)
                    .Include(item => item.Items).ThenInclude(item => item.StorageLocation).SingleOrDefaultAsync(cancellationToken);
                EnsureDocumentState(document is null || (!document.StockUpdated && document.Status != GRNStatus.Cancelled), operation, document?.Status.ToString());
                result = document is null ? null : Context(operation, document.Id, document.GRNNumber, document.Status.ToString(), document.WarehouseId,
                    document.Warehouse.Name, document.ReceiptDate, document.Items.Select(item => Line(item.Id, item.InventoryItemId, item.ItemCode, item.ItemName,
                        item.AcceptedQuantity > 0 ? item.AcceptedQuantity : item.ReceivedQuantity, 0, item.StorageLocationId, item.StorageLocation?.Name,
                        item.LotNumber, item.SerialNumber, item.BatchNumber, item.ManufactureDate, item.ExpiryDate)));
                break;
            }
            case InventoryScanOperation.RequisitionIssue:
            case InventoryScanOperation.RequisitionReturn:
            {
                var document = await _unitOfWork.Repository<InventoryRequisition>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId && !item.IsDeleted)
                    .AsNoTracking().Include(item => item.Warehouse).Include(item => item.Items).ThenInclude(item => item.InventoryItem)
                    .Include(item => item.Items).ThenInclude(item => item.Location).SingleOrDefaultAsync(cancellationToken);
                var eligible = document is null || (operation == InventoryScanOperation.RequisitionIssue
                    ? document.Status is RequisitionStatus.Approved or RequisitionStatus.InProgress or RequisitionStatus.PartiallyIssued
                    : document.Status is RequisitionStatus.PartiallyIssued or RequisitionStatus.Issued or RequisitionStatus.Completed);
                EnsureDocumentState(eligible, operation, document?.Status.ToString());
                result = document is null ? null : Context(operation, document.Id, document.RequisitionNumber, document.Status.ToString(), document.WarehouseId,
                    document.Warehouse.Name, document.RequestDate, document.Items.Select(item => Line(item.Id, item.InventoryItemId, item.ItemCode, item.ItemName,
                        operation == InventoryScanOperation.RequisitionIssue ? Math.Max(0, item.ApprovedQuantity - item.IssuedQuantity) : item.IssuedQuantity,
                        item.IssuedQuantity, item.LocationId ?? document.LocationId, item.Location?.Name,
                        item.LotNumber, item.SerialNumber, item.BatchNumber, item.ManufactureDate, item.ExpiryDate)));
                break;
            }
            case InventoryScanOperation.TransferShipment:
            case InventoryScanOperation.TransferReceipt:
            {
                var document = await _unitOfWork.Repository<InventoryTransfer>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId && !item.IsDeleted)
                    .AsNoTracking().Include(item => item.SourceWarehouse).Include(item => item.DestinationWarehouse)
                    .Include(item => item.Items).ThenInclude(item => item.InventoryItem)
                    .Include(item => item.Items).ThenInclude(item => item.SourceLocation)
                    .Include(item => item.Items).ThenInclude(item => item.DestinationLocation).SingleOrDefaultAsync(cancellationToken);
                var shipment = operation == InventoryScanOperation.TransferShipment;
                EnsureDocumentState(document is null || (shipment
                    ? document.Status == TransferStatus.Approved || document.Status == TransferStatus.InTransit
                    : document.Status == TransferStatus.InTransit), operation, document?.Status.ToString());
                result = document is null ? null : Context(operation, document.Id, document.TransferNumber, document.Status.ToString(),
                    shipment ? document.SourceWarehouseId : document.DestinationWarehouseId,
                    shipment ? document.SourceWarehouse.Name : document.DestinationWarehouse.Name, document.RequestDate,
                    document.Items.Select(item => Line(item.Id, item.InventoryItemId, item.ItemCode, item.ItemName,
                        shipment ? Math.Max(0, item.RequestedQuantity - item.ShippedQuantity) : Math.Max(0, item.ShippedQuantity - item.ReceivedQuantity - item.DamagedQuantity - item.ShortageQuantity),
                        shipment ? item.ShippedQuantity : item.ReceivedQuantity,
                        shipment ? item.SourceLocationId : item.DestinationLocationId,
                        shipment ? item.SourceLocation?.Name : item.DestinationLocation?.Name,
                        item.LotNumber, item.SerialNumber, item.BatchNumber, item.ManufactureDate, item.ExpiryDate)));
                break;
            }
            case InventoryScanOperation.PhysicalCount:
            {
                var document = await _unitOfWork.Repository<PhysicalCount>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId && !item.IsDeleted)
                    .AsNoTracking().Include(item => item.Warehouse).Include(item => item.Items).ThenInclude(item => item.InventoryItem)
                    .Include(item => item.Items).ThenInclude(item => item.Location).SingleOrDefaultAsync(cancellationToken);
                EnsureDocumentState(document is null || document.Status == "InProgress", operation, document?.Status);
                result = document is null ? null : Context(operation, document.Id, document.CountNumber, document.Status, document.WarehouseId,
                    document.Warehouse.Name, document.CountDate, document.Items.Select(item => Line(item.Id, item.InventoryItemId, item.ItemCode, item.ItemName,
                        item.SystemQuantity, item.IsCounted ? item.CountedQuantity : 0, item.LocationId ?? document.LocationId, item.Location?.Name,
                        item.LotNumber, item.SerialNumber, rowVersion: Convert.ToBase64String(item.RowVersion))));
                break;
            }
            default:
                throw new InventoryScanningException("INV_SCAN_OPERATION_UNSUPPORTED", "The scan operation is not supported.");
        }
        return result ?? throw new InventoryScanningException("INV_SCAN_DOCUMENT_NOT_FOUND", "The scan transaction was not found in the current tenant.");
    }

    private static void EnsureDocumentState(bool eligible, InventoryScanOperation operation, string? status)
    {
        if (!eligible)
            throw new InventoryScanningException(
                "INV_SCAN_DOCUMENT_STATE_INVALID",
                $"The {operation} transaction is not eligible for scanning in status '{status ?? "Unknown"}'.");
    }

    private async Task<string> LoadDocumentStatusAsync(InventoryScanOperation operation, Guid documentId, CancellationToken cancellationToken) => operation switch
    {
        InventoryScanOperation.GoodsReceipt => (await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId).Select(item => item.Status).SingleAsync(cancellationToken)).ToString(),
        InventoryScanOperation.RequisitionIssue or InventoryScanOperation.RequisitionReturn => (await _unitOfWork.Repository<InventoryRequisition>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId).Select(item => item.Status).SingleAsync(cancellationToken)).ToString(),
        InventoryScanOperation.TransferShipment or InventoryScanOperation.TransferReceipt => (await _unitOfWork.Repository<InventoryTransfer>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId).Select(item => item.Status).SingleAsync(cancellationToken)).ToString(),
        InventoryScanOperation.PhysicalCount => await _unitOfWork.Repository<PhysicalCount>().GetQueryable(item => item.TenantId == TenantId && item.Id == documentId).Select(item => item.Status).SingleAsync(cancellationToken),
        _ => throw new InventoryScanningException("INV_SCAN_OPERATION_UNSUPPORTED", "The scan operation is not supported.")
    };

    private async Task RequireCapabilityAsync(
        InventoryScanOperation operation,
        Guid warehouseId,
        string sourceReference,
        CancellationToken cancellationToken,
        Guid? locationId = null,
        bool requireLocationScope = false)
    {
        if (!await HasCapabilityAsync(operation, warehouseId, sourceReference, true, cancellationToken,
                locationId, requireLocationScope))
            throw new InventoryScanningAuthorizationException(
                "The current actor is not assigned to this warehouse, location, and scan duty.");
    }

    private async Task RequireLabelProfileMutationCapabilityAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        var warehouseIds = await _unitOfWork.Repository<Warehouse>().GetQueryable(item =>
                item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .AsNoTracking().Select(item => item.Id).ToListAsync(cancellationToken);
        var permissions = new[]
        {
            "procurement.inventory.receive",
            "procurement.inventory.issue",
            "procurement.inventory.transfer",
            "procurement.inventory.count"
        };
        foreach (var warehouseId in warehouseIds)
        {
            foreach (var permission in permissions)
            {
                var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    WarehouseId = warehouseId,
                    SourceType = "InventoryLabelProfile",
                    SourceReference = "tenant-label-profiles"
                }, NormalizeCorrelation(correlationId), cancellationToken);
                if (decision.Allowed) return;
            }
        }

        if (warehouseIds.Count > 0)
        {
            await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permissions[0],
                WarehouseId = warehouseIds[0],
                SourceType = "InventoryLabelProfile",
                SourceReference = "tenant-label-profiles"
            }, NormalizeCorrelation(correlationId), cancellationToken);
        }
        throw new InventoryScanningAuthorizationException(
            "An active receive, issue, transfer, or count responsibility assignment is required to change label profiles.");
    }

    private async Task RequireLabelCandidateReadCapabilityAsync(CancellationToken cancellationToken)
    {
        const string sourceReference = "label-candidate-search";
        var warehouseIds = await _unitOfWork.Repository<Warehouse>().GetQueryable(item =>
                item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .AsNoTracking().Select(item => item.Id).ToListAsync(cancellationToken);
        var permissions = new[]
        {
            "procurement.inventory.read",
            "procurement.inventory.receive",
            "procurement.inventory.issue",
            "procurement.inventory.transfer",
            "procurement.inventory.count"
        };
        foreach (var warehouseId in warehouseIds)
        {
            foreach (var permission in permissions)
            {
                var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = permission,
                    WarehouseId = warehouseId,
                    SourceType = "InventoryLabelCandidate",
                    SourceReference = sourceReference
                }, sourceReference, cancellationToken);
                if (decision.Allowed) return;
            }
        }

        try
        {
            var denial = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = "procurement.inventory.read",
                WarehouseId = warehouseIds.Count > 0 ? warehouseIds[0] : null,
                SourceType = "InventoryLabelCandidate",
                SourceReference = sourceReference
            }, sourceReference, cancellationToken);
            if (denial.Allowed) return;
        }
        catch (Exception exception) when (exception is ProcurementAccessAuthorizationException or ProcurementAccessValidationException)
        {
            // Map the shared authorization boundary to the scanning API's structured 403 contract.
        }
        throw new InventoryScanningAuthorizationException(
            "Inventory-read authority or an active receive, issue, transfer, or count responsibility is required to search label candidates.");
    }

    private async Task<bool> HasCapabilityAsync(
        InventoryScanOperation operation,
        Guid warehouseId,
        string sourceReference,
        bool auditDenied,
        CancellationToken cancellationToken,
        Guid? locationId = null,
        bool requireLocationScope = false)
    {
        var request = new ProcurementAccessCapabilityRequest
        {
            PermissionCode = PermissionFor(operation), WarehouseId = warehouseId,
            LocationId = locationId, RequireLocationScope = requireLocationScope,
            SourceType = "InventoryMobileScan", SourceReference = sourceReference
        };
        var decision = await _access.CheckCapabilityAsync(request, sourceReference, cancellationToken);
        if (auditDenied && !decision.Allowed)
            decision = await _access.EnforceCapabilityAsync(request, sourceReference, cancellationToken);
        return decision.Allowed;
    }

    private async Task<InventoryScanBatchDto> MapBatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var batch = await BatchQuery().Where(item => item.Id == id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InventoryScanningException("INV_SCAN_BATCH_NOT_FOUND", "The scan batch was not found in the current tenant.");
        if (!await CanReadBatchAsync(batch, auditDenied: true, cancellationToken))
            throw new InventoryScanningAuthorizationException(
                "The current actor is not assigned to the warehouse and location scope of this scan batch.");
        return ToBatchDto(batch);
    }

    private IQueryable<InventoryScanBatch> BatchQuery() =>
        Batches.Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .AsNoTracking().Include(item => item.Warehouse)
            .Include(item => item.Lines).ThenInclude(item => item.InventoryItem)
            .Include(item => item.Lines).ThenInclude(item => item.Location)
            .AsSplitQuery();

    private async Task<bool> CanReadBatchAsync(
        InventoryScanBatch batch,
        bool auditDenied,
        CancellationToken cancellationToken)
    {
        var locationIds = batch.Lines.Select(item => item.LocationId).Distinct().ToList();
        if (locationIds.Count == 0) locationIds.Add(null);
        foreach (var locationId in locationIds)
        {
            var requireLocation = locationId.HasValue;
            var operationRequest = BatchReadRequest(PermissionFor(batch.Operation), batch, locationId, requireLocation);
            var correlation = $"inventory-scan-read:{batch.Id:N}:{locationId?.ToString("N") ?? "warehouse"}";
            var operationDecision = await _access.CheckCapabilityAsync(operationRequest, correlation, cancellationToken);
            if (operationDecision.Allowed) continue;

            var readRequest = BatchReadRequest("procurement.inventory.read", batch, locationId, requireLocation);
            var readDecision = await _access.CheckCapabilityAsync(readRequest, correlation, cancellationToken);
            if (readDecision.Allowed) continue;
            if (auditDenied)
                await _access.EnforceCapabilityAsync(readRequest, correlation, cancellationToken);
            return false;
        }
        return true;
    }

    private static ProcurementAccessCapabilityRequest BatchReadRequest(
        string permission,
        InventoryScanBatch batch,
        Guid? locationId,
        bool requireLocationScope) => new()
    {
        PermissionCode = permission,
        WarehouseId = batch.WarehouseId,
        LocationId = locationId,
        RequireLocationScope = requireLocationScope,
        SourceType = "InventoryMobileScanBatch",
        SourceReference = batch.DocumentReference
    };

    private static InventoryScanBatchDto ToBatchDto(InventoryScanBatch batch) => new()
    {
        Id = batch.Id, DeviceId = batch.DeviceId, IdempotencyKey = batch.IdempotencyKey, Operation = batch.Operation,
        DocumentId = batch.DocumentId, DocumentReference = batch.DocumentReference, WarehouseId = batch.WarehouseId,
        WarehouseName = batch.Warehouse.Name, ApplyTransaction = batch.ApplyTransaction, Status = batch.Status,
        CapturedAtUtc = batch.CapturedAtUtc, ProcessedAtUtc = batch.ProcessedAtUtc, FailureCode = batch.FailureCode,
        FailureMessage = batch.FailureMessage, Reconciliation = string.IsNullOrWhiteSpace(batch.ReconciliationJson) ? null :
            JsonSerializer.Deserialize<InventoryScanReconciliationDto>(batch.ReconciliationJson),
        Lines = batch.Lines.OrderBy(item => item.Sequence).Select(item => new InventoryScanLineDto
        {
            ClientLineId = item.ClientLineId, RawIdentifier = item.RawIdentifier, IdentifierKind = item.IdentifierKind,
            InventoryItemId = item.InventoryItemId, ItemCode = item.InventoryItem.ItemCode, ItemName = item.InventoryItem.Name,
            DocumentLineId = item.DocumentLineId, ScannedQuantity = item.ScannedQuantity, ConversionToBase = item.ConversionToBase,
            BaseQuantity = item.BaseQuantity, LocationId = item.LocationId, LocationName = item.Location?.Name,
            LocationIdentifier = item.LocationIdentifier, LotNumber = item.LotNumber, BatchNumber = item.BatchNumber,
            SerialNumber = item.SerialNumber, ManufactureDate = item.ManufactureDate, ExpiryDate = item.ExpiryDate,
            InventoryTrackingExceptionId = item.InventoryTrackingExceptionId, ScannedAtUtc = item.ScannedAtUtc
        }).ToList()
    };

    private async Task<InventoryLabelPrintEventDto> MapPrintAsync(Guid id, CancellationToken cancellationToken) =>
        await PrintEvents.Where(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted).AsNoTracking()
            .Include(item => item.LabelProfile).Include(item => item.InventoryItem).Select(item => ToPrintDto(item)).SingleAsync(cancellationToken);

    private async Task AddAuditAsync(string action, Guid resourceId, object? before, object? after, string correlationId)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = string.IsNullOrWhiteSpace(_currentUser.Username) ? "Unknown" : _currentUser.Username,
            Action = action, Resource = "InventoryScanning", ResourceId = resourceId.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before), NewValues = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = "Service", UserAgent = $"Correlation:{NormalizeCorrelation(correlationId)}", Timestamp = DateTime.UtcNow
        });
    }

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || TenantId == Guid.Empty || UserId == Guid.Empty || _currentUser.IsExternalUser)
            throw new InventoryScanningAuthorizationException("An authenticated internal tenant user is required for inventory scanning.");
    }

    private Guid TenantId => _currentUser.TenantId;
    private Guid UserId => _currentUser.UserId;
    private IQueryable<InventoryLabelProfile> Profiles => _unitOfWork.Repository<InventoryLabelProfile>().GetQueryable();
    private IQueryable<InventoryLabelPrintEvent> PrintEvents => _unitOfWork.Repository<InventoryLabelPrintEvent>().GetQueryable();
    private IQueryable<InventoryScanBatch> Batches => _unitOfWork.Repository<InventoryScanBatch>().GetQueryable();
    private IQueryable<InventoryItem> Items => _unitOfWork.Repository<InventoryItem>().GetQueryable();
    private IQueryable<ItemUnitOfMeasure> ItemUnits => _unitOfWork.Repository<ItemUnitOfMeasure>().GetQueryable();
    private IQueryable<WarehouseLocation> Locations => _unitOfWork.Repository<WarehouseLocation>().GetQueryable();

    private static InventoryLabelProfileDto ToProfileDto(InventoryLabelProfile item) => new()
    {
        Id = item.Id, Name = item.Name, Description = item.Description, Symbology = item.Symbology,
        WidthMm = item.WidthMm, HeightMm = item.HeightMm, Dpi = item.Dpi,
        IncludeItemCode = item.IncludeItemCode, IncludeItemName = item.IncludeItemName, IncludeUnit = item.IncludeUnit,
        IncludeLot = item.IncludeLot, IncludeSerial = item.IncludeSerial, IncludeExpiry = item.IncludeExpiry,
        IsDefault = item.IsDefault, IsActive = item.IsActive, RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private static InventoryLabelPrintEventDto ToPrintDto(InventoryLabelPrintEvent item) => new()
    {
        Id = item.Id, LabelProfileId = item.LabelProfileId, LabelProfileName = item.LabelProfile.Name,
        InventoryItemId = item.InventoryItemId, ItemCode = item.InventoryItem.ItemCode, ItemName = item.InventoryItem.Name,
        Identifier = item.Identifier, IdentifierKind = item.IdentifierKind, LabelCount = item.LabelCount,
        LotNumber = item.LotNumber, SerialNumber = item.SerialNumber, ExpiryDate = item.ExpiryDate,
        PrinterName = item.PrinterName, PrintedAtUtc = item.PrintedAtUtc
    };

    private static InventoryScanDocumentSummaryDto Summary(Guid id, string reference, string status, Guid warehouseId, string warehouseName, DateTime date) => new()
    { DocumentId = id, DocumentReference = reference, Status = status, WarehouseId = warehouseId, WarehouseName = warehouseName, DocumentDate = date };

    private static InventoryScanDocumentContextDto Context(InventoryScanOperation operation, Guid id, string reference, string status,
        Guid warehouseId, string warehouseName, DateTime date, IEnumerable<InventoryScanDocumentLineDto> lines) => new()
    {
        Operation = operation, DocumentId = id, DocumentReference = reference, Status = status,
        WarehouseId = warehouseId, WarehouseName = warehouseName, DocumentDate = date, Lines = lines.ToList()
    };

    private static InventoryScanDocumentLineDto Line(Guid id, Guid inventoryItemId, string code, string name, decimal expected, decimal processed,
        Guid? locationId, string? locationName, string? lot, string? serial, string? batch = null,
        DateTime? manufactureDate = null, DateTime? expiryDate = null, string? rowVersion = null) => new()
    {
        DocumentLineId = id, InventoryItemId = inventoryItemId, ItemCode = code, ItemName = name,
        ExpectedQuantity = expected, ProcessedQuantity = processed, LocationId = locationId, LocationName = locationName,
        LotNumber = lot, BatchNumber = batch, SerialNumber = serial,
        ManufactureDate = manufactureDate, ExpiryDate = expiryDate, RowVersion = rowVersion
    };

    private static void AddCandidate(List<InventoryLabelCandidateDto> target, InventoryItem item, string? identifier, string kind)
    {
        if (!string.IsNullOrWhiteSpace(identifier)) target.Add(new InventoryLabelCandidateDto
        { InventoryItemId = item.Id, ItemCode = item.ItemCode, ItemName = item.Name, Identifier = identifier, IdentifierKind = kind });
    }

    private static string PermissionFor(InventoryScanOperation operation) => operation switch
    {
        InventoryScanOperation.GoodsReceipt => "procurement.inventory.receive",
        InventoryScanOperation.RequisitionIssue or InventoryScanOperation.RequisitionReturn => "procurement.inventory.issue",
        InventoryScanOperation.TransferShipment or InventoryScanOperation.TransferReceipt => "procurement.inventory.transfer",
        InventoryScanOperation.PhysicalCount => "procurement.inventory.count",
        _ => throw new InventoryScanningException("INV_SCAN_OPERATION_UNSUPPORTED", "The scan operation is not supported.")
    };

    private static string HashPayload(SynchronizeInventoryScanBatchRequest request, string deviceId, string idempotencyKey)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            DeviceId = deviceId, IdempotencyKey = idempotencyKey, request.Operation, request.DocumentId, request.WarehouseId,
            request.ApplyTransaction,
            Lines = request.Lines.OrderBy(item => item.ClientLineId).Select(item => new
            {
                item.ClientLineId, Identifier = item.RawIdentifier.Trim().ToUpperInvariant(), item.DocumentLineId, item.Quantity,
                DocumentLineRowVersion = Clean(item.DocumentLineRowVersion),
                item.LocationId, LocationIdentifier = Clean(item.LocationIdentifier), Lot = Clean(item.LotNumber),
                Batch = Clean(item.BatchNumber), Serial = Clean(item.SerialNumber), Manufacture = Utc(item.ManufactureDate),
                Expiry = Utc(item.ExpiryDate), item.InventoryTrackingExceptionId, item.ScannedAtUtc
            })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void EnsureMatchingPayload(InventoryScanBatch existing, string payloadHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(existing.PayloadHash), Convert.FromHexString(payloadHash)))
            throw new InventoryScanningException("INV_SCAN_IDEMPOTENCY_CONFLICT", "This device idempotency key was already used for a different scan payload.");
    }

    private static string Required(string? value, string code, string message) =>
        Clean(value) ?? throw new InventoryScanningException(code, message);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime? Utc(DateTime? value) => value.HasValue
        ? value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime()
        : null;
    private static string NormalizeCorrelation(string? value) => Clean(value) ?? Guid.NewGuid().ToString("N");

    private sealed record ResolvedLine(
        InventoryScanInputDto Input,
        InventoryIdentifierMatchDto Match,
        InventoryScanDocumentLineDto DocumentLine,
        decimal BaseQuantity,
        Guid? LocationId);
}
