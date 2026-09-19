using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Finance-side adapter for FIN-INT-007 / FIN-LIM-0028. Procurement remains authoritative for
/// PO, receipt and inspection decisions; this service consumes its accepted-supply contract and
/// owns only the asset-register reservation and Inventory Control-to-Fixed Asset reclassification.
/// </summary>
public sealed class ProcurementFixedAssetCapitalizationAdapter : IProcurementFixedAssetCapitalizationAdapter
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcurementAcceptedSupplyService _acceptedSupply;
    private readonly IFixedAssetService _fixedAssets;

    public ProcurementFixedAssetCapitalizationAdapter(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcurementAcceptedSupplyService acceptedSupply,
        IFixedAssetService fixedAssets)
    {
        _context = context;
        _currentUser = currentUser;
        _acceptedSupply = acceptedSupply;
        _fixedAssets = fixedAssets;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<IReadOnlyList<ProcurementFixedAssetCandidateDto>> GetCandidatesAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAcceptedGoodsAsync(
            ProcurementAcceptedSupplyKind.GoodsReceiptInspection,
            purchaseOrderId,
            purchaseOrderId,
            cancellationToken);
        var acceptedLineIds = resolution.Lines.Select(line => line.PurchaseOrderItemId).ToList();
        var fixedAssetLineIds = await _context.PurchaseOrderItems.AsNoTracking()
            .Where(line => line.TenantId == TenantId &&
                line.PurchaseOrderId == purchaseOrderId &&
                acceptedLineIds.Contains(line.Id) &&
                line.InventoryItem != null &&
                line.InventoryItem.ItemType == ItemType.FixedAsset &&
                !line.IsDeleted &&
                !line.InventoryItem.IsDeleted)
            .Select(line => line.Id)
            .ToListAsync(cancellationToken);
        var fixedAssetLineIdSet = fixedAssetLineIds.ToHashSet();
        var results = new List<ProcurementFixedAssetCandidateDto>();
        foreach (var sourceLine in resolution.Lines
                     .Where(line => fixedAssetLineIdSet.Contains(line.PurchaseOrderItemId))
                     .OrderBy(line => line.PurchaseOrderItemId))
        {
            var candidate = await ResolveCandidateAsync(resolution, sourceLine.PurchaseOrderItemId, cancellationToken);
            // Inventory master data is the producer-owned declaration that a received good is an
            // asset candidate. Filtering before full evidence resolution keeps ordinary stock (or
            // non-stock PO lines) from blocking the Finance fixed-asset workspace.
            results.Add(ToCandidateDto(candidate));
        }

        return results;
    }

    public async Task<ProcurementFixedAssetCapitalizationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<ProcurementFixedAssetCapitalization>()
            .AsNoTracking()
            .Include(value => value.FixedAsset)
            .SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == id && !value.IsDeleted,
                cancellationToken);
        return entity == null ? null : Map(entity);
    }

    public async Task<ProcurementFixedAssetCapitalizationDto> CreateDraftAsync(
        CreateProcurementFixedAssetDraftDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var idempotencyKey = NormalizeRequired(dto.IdempotencyKey, "Idempotency key", 100);

        // A single accepted PO line can create several individually controlled assets. Run the
        // availability check, register-draft creation and source reservation in one serializable
        // transaction so two Finance users cannot reserve the same remaining receipt unit. The
        // execution strategy is required by SQL Server when transient retries wrap a transaction.
        // If an orchestrator already owns a transaction, retain that atomic boundary rather than
        // nesting an execution strategy that SQL Server correctly rejects.
        if (_context.Database.CurrentTransaction != null)
            return await CreateDraftCoreAsync(dto, idempotencyKey, cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var result = await CreateDraftCoreAsync(dto, idempotencyKey, cancellationToken);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<ProcurementFixedAssetCapitalizationDto> CreateDraftCoreAsync(
        CreateProcurementFixedAssetDraftDto dto,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Set<ProcurementFixedAssetCapitalization>()
            .AsNoTracking()
            .Include(value => value.FixedAsset)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId &&
                value.IdempotencyKey == idempotencyKey && !value.IsDeleted, cancellationToken);
        if (existing != null)
            return Map(existing);

        var resolution = await ResolveAcceptedGoodsAsync(
            dto.AcceptedSupplyKind,
            dto.AcceptedSupplySourceId,
            dto.PurchaseOrderId,
            cancellationToken);
        var candidate = await ResolveCandidateAsync(resolution, dto.PurchaseOrderItemId, cancellationToken);
        EnsureReadyForOneAsset(candidate);

        var asset = await _fixedAssets.CreateAsync(new CreateFixedAssetDto
        {
            AssetCode = dto.AssetCode,
            Name = dto.Name,
            Description = dto.Description,
            Location = dto.Location,
            FixedAssetCategoryId = dto.FixedAssetCategoryId,
            // The accepted/inspection decision date is the immutable acquisition evidence. The
            // operator may choose a later placed-in-service date through Finance policy.
            PurchaseDate = resolution.AcceptedAtUtc.Date,
            PlacedInServiceDate = dto.PlacedInServiceDate,
            PurchasePrice = candidate.FunctionalUnitCost,
            AcquisitionCost = candidate.FunctionalUnitCost,
            DepreciationMethod = dto.DepreciationMethod,
            DepreciationConvention = dto.DepreciationConvention,
            UsefulLifeMonths = dto.UsefulLifeMonths,
            ResidualValue = dto.ResidualValue,
            DiminishingBalanceRatePercent = dto.DiminishingBalanceRatePercent,
            LifetimeProductionCapacity = dto.LifetimeProductionCapacity,
            SerialNumber = dto.SerialNumber
        });

        var entity = new ProcurementFixedAssetCapitalization
        {
            TenantId = TenantId,
            FixedAssetId = asset.Id,
            AcceptedSupplyKind = resolution.Kind,
            AcceptedSupplySourceId = resolution.SourceId,
            AcceptedSupplyReference = resolution.SourceReference,
            PurchaseOrderId = dto.PurchaseOrderId,
            PurchaseOrderItemId = dto.PurchaseOrderItemId,
            InventoryItemId = candidate.InventoryItem.Id,
            // The fixed-asset register models individually controlled assets rather than bulk
            // stock quantities. A receipt of ten laptops therefore produces ten separately
            // identifiable drafts, each reserving one accepted unit.
            CapitalizedQuantity = 1m,
            SourceCurrencyCode = resolution.CurrencyCode,
            SourceTransactionAmount = Round(candidate.SourceLine.UnitPrice),
            FunctionalCurrencyCode = candidate.FunctionalCurrencyCode,
            FunctionalAmount = candidate.FunctionalUnitCost,
            SourceIntegrityHash = resolution.SourceIntegrityHash,
            SourceSnapshotJson = resolution.SnapshotJson,
            ReceiptPostingEvidenceJson = candidate.ReceiptPostingEvidenceJson,
            Status = ProcurementFixedAssetCapitalizationStatus.Draft,
            IdempotencyKey = idempotencyKey,
            CapitalizationDate = dto.PlacedInServiceDate?.Date ?? resolution.AcceptedAtUtc.Date,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };
        _context.Set<ProcurementFixedAssetCapitalization>().Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        entity.FixedAsset = await _context.FixedAssets.AsNoTracking()
            .SingleAsync(value => value.TenantId == TenantId && value.Id == asset.Id, cancellationToken);
        return Map(entity);
    }

    public async Task<ProcurementFixedAssetCapitalizationDto> PostAsync(
        Guid id,
        PostProcurementFixedAssetCapitalizationDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await _context.Set<ProcurementFixedAssetCapitalization>()
            .Include(value => value.FixedAsset)
            .SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == id && !value.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Procurement fixed-asset capitalization was not found.");
        if (entity.Status == ProcurementFixedAssetCapitalizationStatus.Posted)
            return Map(entity);
        if (entity.Status == ProcurementFixedAssetCapitalizationStatus.Reversed)
            throw new InvalidOperationException("A reversed Procurement capitalization cannot be posted again; create a corrected Finance draft.");

        var reason = NormalizeRequired(dto.Reason, "Capitalization reason", 1000);
        var current = await ResolveAcceptedGoodsAsync(
            entity.AcceptedSupplyKind,
            entity.AcceptedSupplySourceId,
            entity.PurchaseOrderId,
            cancellationToken);
        if (!string.Equals(current.SourceIntegrityHash, entity.SourceIntegrityHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Procurement accepted-supply evidence changed after this asset draft was prepared. Review the new receipt/inspection evidence and create a corrected draft.");
        }

        var settings = await _context.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        var inventoryControlAccountId = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");

        try
        {
            var asset = await _fixedAssets.CapitalizeFromProcurementAsync(
                entity.FixedAssetId,
                new ProcurementFixedAssetPostingInstructionDto
                {
                    CapitalizationId = entity.Id,
                    PurchaseOrderItemId = entity.PurchaseOrderItemId,
                    CapitalizationDate = dto.CapitalizationDate.Date,
                    InventoryControlAccountId = inventoryControlAccountId,
                    FunctionalAmount = entity.FunctionalAmount,
                    FunctionalCurrencyCode = entity.FunctionalCurrencyCode,
                    SourceReference = entity.AcceptedSupplyReference,
                    Reason = reason
                },
                cancellationToken);

            if (_context.Entry(entity).State == EntityState.Detached)
            {
                entity = await _context.Set<ProcurementFixedAssetCapitalization>()
                    .Include(value => value.FixedAsset)
                    .SingleAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken);
            }

            entity.Status = ProcurementFixedAssetCapitalizationStatus.Posted;
            entity.PostingEventId = asset.PostingEventId;
            entity.JournalEntryId = asset.JournalEntryId;
            entity.PostedAt = DateTime.UtcNow;
            entity.CapitalizationDate = asset.CapitalizationDate?.Date ?? entity.CapitalizationDate;
            entity.FailureReason = null;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }
        catch (Exception exception)
        {
            // A failed attempt does not release the accepted unit: doing so would allow another
            // draft to reserve the same receipt while this approved asset remains retryable.
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                entity = await _context.Set<ProcurementFixedAssetCapitalization>()
                    .SingleAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken);
            }
            entity.FailureReason = exception.Message.Length <= 2000
                ? exception.Message
                : exception.Message[..2000];
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<ProcurementAcceptedSupplyResolutionDto> ResolveAcceptedGoodsAsync(
        ProcurementAcceptedSupplyKind kind,
        Guid sourceId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        if (kind != ProcurementAcceptedSupplyKind.GoodsReceiptInspection)
            throw new InvalidOperationException("FIN-INT-007 currently accepts governed goods receipt/inspection evidence only.");
        var resolution = await _acceptedSupply.ResolveAsync(kind, sourceId, purchaseOrderId, null, cancellationToken);
        if (resolution.Category != ProcurementCategoryClass.Goods || resolution.PurchaseOrderId != purchaseOrderId)
            throw new InvalidOperationException("The accepted-supply evidence is not a Goods receipt for the selected purchase order.");
        return resolution;
    }

    private async Task<ResolvedCandidate> ResolveCandidateAsync(
        ProcurementAcceptedSupplyResolutionDto resolution,
        Guid purchaseOrderItemId,
        CancellationToken cancellationToken)
    {
        var sourceLine = resolution.Lines.SingleOrDefault(value => value.PurchaseOrderItemId == purchaseOrderItemId)
            ?? throw new InvalidOperationException("The selected purchase-order line has no accepted quantity in the governed evidence.");
        var orderLine = await _context.PurchaseOrderItems.AsNoTracking()
            .Include(value => value.InventoryItem)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == purchaseOrderItemId &&
                value.PurchaseOrderId == resolution.PurchaseOrderId && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The accepted purchase-order line was not found in this tenant.");
        var inventoryItem = orderLine.InventoryItem
            ?? throw new InvalidOperationException("A procured fixed asset must reference an Inventory item classified as Fixed Asset.");

        var blocked = new List<string>();
        if (inventoryItem.ItemType != ItemType.FixedAsset)
            blocked.Add("The Inventory item is not classified as Fixed Asset.");
        var duplicateItemLine = await _context.PurchaseOrderItems.AsNoTracking().AnyAsync(value =>
            value.TenantId == TenantId && value.PurchaseOrderId == orderLine.PurchaseOrderId &&
            value.Id != orderLine.Id && value.InventoryItemId == orderLine.InventoryItemId && !value.IsDeleted,
            cancellationToken);
        if (duplicateItemLine)
            blocked.Add("The purchase order repeats this Inventory item on multiple lines; line-specific valuation evidence is ambiguous.");

        var receiptLines = await _context.PurchaseOrderReceiptItems.AsNoTracking()
            .Include(value => value.Receipt)
            .Where(value => value.TenantId == TenantId && value.PurchaseOrderItemId == orderLine.Id &&
                value.AcceptedQuantity > 0m && !value.IsDeleted && !value.Receipt.IsDeleted)
            .OrderBy(value => value.Receipt.ReceiptDate).ThenBy(value => value.ReceiptId)
            .ToListAsync(cancellationToken);
        var receiptIds = receiptLines.Select(value => value.ReceiptId).Distinct().ToList();
        var movements = await _context.InventoryMovements.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ReferenceId.HasValue &&
                receiptIds.Contains(value.ReferenceId.Value) &&
                value.InventoryItemId == inventoryItem.Id && value.MovementType == InventoryMovementType.PurchaseReceipt &&
                value.IsPosted && !value.IsReversal && !value.IsDeleted)
            .OrderBy(value => value.PostingDate).ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        var events = await _context.FinancePostingEvents.AsNoTracking()
            .Where(value => value.TenantId == TenantId && receiptIds.Contains(value.SourceDocumentId) &&
                value.SourceDocumentType == "ProcurementPurchaseOrderReceipt" && value.PostingStatus == "Posted" && !value.IsDeleted)
            .OrderBy(value => value.PostedAt).ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        if (receiptIds.Count == 0)
            blocked.Add("No accepted receipt lines were found for the purchase-order line.");
        if (movements.Count == 0)
            blocked.Add("No posted inventory valuation movement supports the accepted fixed-asset item.");
        if (events.Select(value => value.SourceDocumentId).Distinct().Count() != receiptIds.Count)
            blocked.Add("Every accepted receipt must have a posted Finance inventory/GRV journal before capitalization.");

        var functionalTotal = Round(movements.Sum(value => value.TotalValue));
        if (functionalTotal <= 0m)
            blocked.Add("The posted inventory carrying value is not positive.");
        var allocated = await _context.Set<ProcurementFixedAssetCapitalization>().AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.PurchaseOrderItemId == orderLine.Id &&
                value.Status != ProcurementFixedAssetCapitalizationStatus.Reversed &&
                value.Status != ProcurementFixedAssetCapitalizationStatus.Failed && !value.IsDeleted)
            .SumAsync(value => (decimal?)value.CapitalizedQuantity, cancellationToken) ?? 0m;
        var available = Math.Max(0m, sourceLine.AcceptedQuantity - allocated);
        if (available < 1m)
            blocked.Add("All accepted units on this purchase-order line are already reserved or capitalized.");

        var settings = await _context.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && !value.IsDeleted, cancellationToken);
        var functionalCurrency = string.IsNullOrWhiteSpace(settings?.BaseCurrency)
            ? "GHS"
            : settings.BaseCurrency.Trim().ToUpperInvariant();
        var functionalUnitCost = sourceLine.AcceptedQuantity > 0m
            ? Round(functionalTotal / sourceLine.AcceptedQuantity)
            : 0m;
        var evidence = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.finance.procurement-asset-receipt-posting.v1",
            purchaseOrderItemId = orderLine.Id,
            inventoryItemId = inventoryItem.Id,
            receipts = receiptLines.Select(value => new
            {
                value.ReceiptId,
                value.Receipt.ReceiptNumber,
                value.AcceptedQuantity
            }),
            movements = movements.Select(value => new
            {
                value.Id,
                value.MovementNumber,
                value.Quantity,
                value.UnitCost,
                value.TotalValue
            }),
            postingEvents = events.Select(value => new
            {
                value.Id,
                value.JournalEntryId,
                value.SourceDocumentId
            }),
            functionalCurrency,
            functionalTotal
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new ResolvedCandidate(
            resolution,
            sourceLine,
            orderLine,
            inventoryItem,
            functionalCurrency,
            functionalUnitCost,
            allocated,
            available,
            evidence,
            blocked);
    }

    private static void EnsureReadyForOneAsset(ResolvedCandidate candidate)
    {
        if (candidate.BlockedReasons.Count > 0)
            throw new InvalidOperationException(string.Join(" ", candidate.BlockedReasons));
        if (candidate.AvailableQuantity < 1m)
            throw new InvalidOperationException("At least one unreserved accepted unit is required.");
    }

    private static ProcurementFixedAssetCandidateDto ToCandidateDto(ResolvedCandidate value) => new()
    {
        AcceptedSupplyKind = value.Resolution.Kind,
        AcceptedSupplySourceId = value.Resolution.SourceId,
        AcceptedSupplyReference = value.Resolution.SourceReference,
        PurchaseOrderId = value.Resolution.PurchaseOrderId!.Value,
        PurchaseOrderItemId = value.OrderLine.Id,
        InventoryItemId = value.InventoryItem.Id,
        ItemCode = value.InventoryItem.ItemCode,
        ItemDescription = value.OrderLine.ItemDescription ?? value.InventoryItem.Name,
        AcceptedQuantity = value.SourceLine.AcceptedQuantity,
        ReservedOrCapitalizedQuantity = value.AllocatedQuantity,
        AvailableQuantity = value.AvailableQuantity,
        SourceCurrencyCode = value.Resolution.CurrencyCode,
        SourceUnitPrice = value.SourceLine.UnitPrice,
        FunctionalCurrencyCode = value.FunctionalCurrencyCode,
        FunctionalUnitCost = value.FunctionalUnitCost,
        IsReady = value.BlockedReasons.Count == 0,
        BlockedReasons = value.BlockedReasons
    };

    private static ProcurementFixedAssetCapitalizationDto Map(ProcurementFixedAssetCapitalization value) => new()
    {
        Id = value.Id,
        FixedAssetId = value.FixedAssetId,
        AssetCode = value.FixedAsset?.AssetCode ?? string.Empty,
        AcceptedSupplyKind = value.AcceptedSupplyKind,
        AcceptedSupplySourceId = value.AcceptedSupplySourceId,
        AcceptedSupplyReference = value.AcceptedSupplyReference,
        PurchaseOrderId = value.PurchaseOrderId,
        PurchaseOrderItemId = value.PurchaseOrderItemId,
        InventoryItemId = value.InventoryItemId,
        CapitalizedQuantity = value.CapitalizedQuantity,
        SourceCurrencyCode = value.SourceCurrencyCode,
        SourceTransactionAmount = value.SourceTransactionAmount,
        FunctionalCurrencyCode = value.FunctionalCurrencyCode,
        FunctionalAmount = value.FunctionalAmount,
        Status = value.Status,
        CapitalizationDate = value.CapitalizationDate,
        PostingEventId = value.PostingEventId,
        JournalEntryId = value.JournalEntryId,
        PostedAt = value.PostedAt,
        ReversedAt = value.ReversedAt,
        FailureReason = value.FailureReason
    };

    private static string NormalizeRequired(string? value, string label, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException($"{label} is required.");
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"{label} cannot exceed {maximumLength} characters.");
        return normalized;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record ResolvedCandidate(
        ProcurementAcceptedSupplyResolutionDto Resolution,
        ProcurementAcceptedSupplyLineDto SourceLine,
        PurchaseOrderItem OrderLine,
        InventoryItem InventoryItem,
        string FunctionalCurrencyCode,
        decimal FunctionalUnitCost,
        decimal AllocatedQuantity,
        decimal AvailableQuantity,
        string ReceiptPostingEvidenceJson,
        IReadOnlyList<string> BlockedReasons);
}
