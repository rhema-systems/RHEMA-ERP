using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetTransferService : IAssetTransferService
{
    private const string SourceModule = "FixedAssets";
    private const string SourceDocumentType = "FixedAssetTransfer";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IWorkflowService _workflowService;
    private readonly IFinanceAuditService? _financeAuditService;

    public AssetTransferService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        IWorkflowService workflowService,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _workflowService = workflowService;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public async Task<AssetTransferDto?> GetByIdAsync(Guid id)
    {
        var transfer = await BuildTransferQuery()
            .Where(t => t.TenantId == TenantId && t.Id == id)
            .FirstOrDefaultAsync();

        return transfer == null ? null : MapToDto(transfer);
    }

    public async Task<IEnumerable<AssetTransferDto>> GetAllAsync()
    {
        var transfers = await BuildTransferQuery()
            .Where(t => t.TenantId == TenantId)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<AssetTransferDto>> GetByAssetIdAsync(Guid assetId)
    {
        var transfers = await BuildTransferQuery()
            .Where(t => t.TenantId == TenantId && t.FixedAssetId == assetId)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<AssetTransferDto> RequestTransferAsync(RequestAssetTransferDto dto, Guid requestedById)
    {
        var idempotencyKey = NormalizeIdempotencyKey(dto.IdempotencyKey);
        if (idempotencyKey != null)
        {
            var existing = await BuildTransferQuery()
                .Where(t => t.TenantId == TenantId && t.IdempotencyKey == idempotencyKey)
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                return MapToDto(existing);
            }
        }

        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.CurrentCustodian)
            .Include(a => a.CurrentSegmentLookupValue)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        await ValidateTransferRequestAsync(asset, dto);

        var targetSegment = await ResolveSegmentAsync(dto.ToSegmentLookupValueId, dto.ToSegmentString, dto.TransferDate, asset.Id);
        var workflowResult = new WorkflowExecutionResult { Success = false };
        var transfer = new AssetTransfer
        {
            TenantId = TenantId,
            FixedAssetId = dto.FixedAssetId,
            TransferDate = dto.TransferDate.Date,
            AccountingDate = dto.AccountingDate?.Date,
            FiscalPeriodId = null,
            TransferType = dto.TransferType,
            Status = AssetTransferStatus.PendingApproval,
            FromLocation = asset.Location,
            FromCustodianId = asset.CurrentCustodianId,
            FromSegmentString = asset.CurrentSegmentString,
            FromSegmentLookupValueId = asset.CurrentSegmentLookupValueId,
            ToLocation = dto.ToLocation.Trim(),
            ToCustodianId = dto.ToCustodianId,
            ToSegmentString = targetSegment.segmentString,
            ToSegmentLookupValueId = targetSegment.segmentLookupValueId,
            Reason = dto.Reason,
            TransferCost = dto.TransferCost,
            RequestedById = requestedById == Guid.Empty ? null : requestedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            IdempotencyKey = idempotencyKey,
            ReferenceNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.AssetTransfer,
                TenantId,
                dto.TransferDate,
                nameof(AssetTransfer))
        };

        _context.AssetTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        await RecordTransferAuditAsync(
            FinanceAuditEvents.FixedAssetTransferRequested,
            transfer,
            afterValues: new
            {
                transfer.FixedAssetId,
                transfer.TransferDate,
                transfer.TransferType,
                transfer.Status,
                transfer.FromLocation,
                transfer.ToLocation,
                transfer.FromCustodianId,
                transfer.ToCustodianId,
                transfer.FromSegmentString,
                transfer.ToSegmentString
            },
            comment: transfer.Reason);

        workflowResult = await _workflowService.StartApprovalWorkflowAsync("AssetTransfer", transfer.Id);
        if (!workflowResult.Success)
        {
            transfer.Status = AssetTransferStatus.Rejected;
            transfer.Comments = workflowResult.Message ?? "Unable to start asset transfer approval workflow.";
            transfer.FailedAt = DateTime.UtcNow;
            transfer.FailureReason = transfer.Comments;
            transfer.UpdatedAt = DateTime.UtcNow;
            transfer.UpdatedBy = UserName;
            await _context.SaveChangesAsync();

            await RecordTransferAuditAsync(
                FinanceAuditEvents.FixedAssetTransferPostingFailed,
                transfer,
                afterValues: new { transfer.Status, transfer.FailureReason },
                comment: transfer.Comments);

            throw new InvalidOperationException(transfer.Comments);
        }

        transfer.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
        await _context.SaveChangesAsync();

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to request transfer.");
    }

    public async Task<AssetTransferDto> ApproveTransferAsync(Guid transferId, Guid approvedById, ApproveAssetTransferDto dto)
    {
        var transfer = await LoadTransferForMutationAsync(transferId);

        if (transfer.Status == AssetTransferStatus.Completed)
        {
            return MapToDto(transfer);
        }

        if (transfer.Status != AssetTransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending transfers can be approved.");
        }

        if (CurrentUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Unable to resolve the current approver.");
        }

        if (!await _workflowService.CanUserApproveAsync("AssetTransfer", transferId, CurrentUserId))
        {
            throw new InvalidOperationException("This asset transfer is assigned to another workflow approver.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("AssetTransfer", transferId, CurrentUserId, "Approve", dto.Comments);
        if (!workflowResult.Success)
        {
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process asset transfer approval.");
        }

        if (workflowResult.Status != WorkflowInstanceStatus.Completed)
        {
            return MapToDto(transfer);
        }

        transfer.Status = AssetTransferStatus.Approved;
        transfer.ApprovedById = approvedById == Guid.Empty ? null : approvedById;
        transfer.ApprovedAt = DateTime.UtcNow;
        transfer.Comments = dto.Comments;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        await RecordTransferAuditAsync(
            FinanceAuditEvents.FixedAssetTransferApproved,
            transfer,
            afterValues: new { transfer.Status, transfer.ApprovedById, transfer.ApprovedAt },
            comment: dto.Comments);

        return await CompleteTransferAsync(transfer.Id);
    }

    public async Task<AssetTransferDto> CompleteTransferAsync(Guid transferId)
    {
        var transfer = await LoadTransferForMutationAsync(transferId);

        if (transfer.Status == AssetTransferStatus.Completed)
        {
            return MapToDto(transfer);
        }

        if (transfer.Status != AssetTransferStatus.Approved)
        {
            throw new InvalidOperationException("Only approved transfers can be completed.");
        }

        var asset = transfer.FixedAsset;
        ValidateTransferCanComplete(transfer, asset);
        var beforeValues = new
        {
            asset.Location,
            asset.CurrentCustodianId,
            asset.CurrentSegmentString,
            asset.CurrentSegmentLookupValueId,
            asset.AcquisitionCost,
            asset.NetBookValue
        };

        asset.Location = transfer.ToLocation;
        asset.CurrentCustodianId = transfer.ToCustodianId;
        asset.CurrentSegmentString = NormalizeSegmentString(transfer.ToSegmentString);
        asset.CurrentSegmentLookupValueId = transfer.ToSegmentLookupValueId;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        transfer.Status = AssetTransferStatus.Completed;
        transfer.CompletedAt = DateTime.UtcNow;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = UserName;

        var assetTransaction = new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = transfer.FixedAssetId,
            TransactionDate = transfer.TransferDate,
            TransactionType = "Transfer",
            Description = BuildTransferDescription(transfer),
            Amount = 0m,
            ResultingBookValue = asset.NetBookValue,
            RelatedEntityId = transfer.Id,
            PerformedByUserId = CurrentUserId == Guid.Empty ? Guid.Empty : CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.AssetTransactions.Add(assetTransaction);
        await _context.SaveChangesAsync();

        await RecordTransferAuditAsync(
            FinanceAuditEvents.FixedAssetTransferred,
            transfer,
            beforeValues,
            afterValues: new
            {
                asset.Location,
                asset.CurrentCustodianId,
                asset.CurrentSegmentString,
                asset.CurrentSegmentLookupValueId,
                asset.AcquisitionCost,
                asset.NetBookValue,
                transfer.Status,
                transfer.CompletedAt
            },
            comment: transfer.Comments ?? transfer.Reason);

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to complete transfer.");
    }

    public async Task<AssetTransferDto> RejectTransferAsync(Guid transferId, Guid rejectedById, string comments)
    {
        var transfer = await LoadTransferForMutationAsync(transferId);

        if (transfer.Status != AssetTransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending transfers can be rejected.");
        }

        if (CurrentUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Unable to resolve the current approver.");
        }

        if (!await _workflowService.CanUserApproveAsync("AssetTransfer", transferId, CurrentUserId))
        {
            throw new InvalidOperationException("This asset transfer is assigned to another workflow approver.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("AssetTransfer", transferId, CurrentUserId, "Reject", comments);
        if (!workflowResult.Success)
        {
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process asset transfer rejection.");
        }

        if (workflowResult.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
        {
            return MapToDto(transfer);
        }

        transfer.Status = AssetTransferStatus.Rejected;
        transfer.ApprovedById = rejectedById == Guid.Empty ? null : rejectedById;
        transfer.Comments = comments;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = UserName;

        await _context.SaveChangesAsync();

        await RecordTransferAuditAsync(
            FinanceAuditEvents.FixedAssetTransferRejected,
            transfer,
            afterValues: new { transfer.Status, transfer.ApprovedById },
            comment: comments);

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to reject transfer.");
    }

    private IQueryable<AssetTransfer> BuildTransferQuery()
        => _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .Include(t => t.FromCustodian)
            .Include(t => t.ToCustodian)
            .Include(t => t.RequestedBy)
            .Include(t => t.ApprovedBy)
            .Include(t => t.FromSegmentLookupValue)
            .Include(t => t.ToSegmentLookupValue);

    private async Task<AssetTransfer> LoadTransferForMutationAsync(Guid transferId)
        => await BuildTransferQuery()
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId)
            ?? throw new KeyNotFoundException("Transfer request not found.");

    private async Task ValidateTransferRequestAsync(FixedAsset asset, RequestAssetTransferDto dto)
    {
        if (asset.TenantId != TenantId || asset.Category?.TenantId != TenantId)
        {
            await RecordBlockedTransferAuditAsync(dto.FixedAssetId, "Fixed asset or category belongs to another tenant.");
            throw new InvalidOperationException("Fixed asset or category belongs to another tenant.");
        }

        if (dto.TransferType == AssetTransferType.GlReclassification)
        {
            await RecordBlockedTransferAuditAsync(asset.Id, "Fixed asset GL reclassification transfers are not supported in this batch.");
            throw new InvalidOperationException("Fixed asset GL reclassification transfers are not supported in this batch.");
        }

        if (!IsTransferable(asset))
        {
            throw new InvalidOperationException("Fixed asset must be capitalized and in a transferable status before transfer.");
        }

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff or FixedAssetStatus.HeldForSale)
        {
            throw new InvalidOperationException("Disposed, written-off, or held-for-sale assets cannot be transferred.");
        }

        if (asset.CapitalizationDate.HasValue && dto.TransferDate.Date < asset.CapitalizationDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset transfer date cannot be before capitalization date.");
        }

        if (asset.PlacedInServiceDate.HasValue && dto.TransferDate.Date < asset.PlacedInServiceDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset transfer date cannot be before placed-in-service date.");
        }

        if (string.IsNullOrWhiteSpace(dto.ToLocation) && dto.ToCustodianId == null && string.IsNullOrWhiteSpace(dto.ToSegmentString) && dto.ToSegmentLookupValueId == null)
        {
            throw new InvalidOperationException("Fixed asset transfer requires a target location, custodian, or segment.");
        }

        if (dto.ToCustodianId.HasValue)
        {
            var sameTenantCustodian = await _context.Employees.AnyAsync(e =>
                e.TenantId == TenantId &&
                e.Id == dto.ToCustodianId.Value &&
                !e.IsDeleted);
            if (!sameTenantCustodian)
            {
                await RecordBlockedTransferAuditAsync(asset.Id, "Fixed asset transfer custodian belongs to another tenant or does not exist.");
                throw new InvalidOperationException("Fixed asset transfer custodian belongs to another tenant or does not exist.");
            }
        }

        if (dto.TransferType == AssetTransferType.SegmentMovement && string.IsNullOrWhiteSpace(dto.ToSegmentString) && !dto.ToSegmentLookupValueId.HasValue)
        {
            throw new InvalidOperationException("Segment movement transfer requires a target segment.");
        }
    }

    private async Task<(string? segmentString, Guid? segmentLookupValueId)> ResolveSegmentAsync(
        Guid? segmentLookupValueId,
        string? segmentString,
        DateTime transferDate,
        Guid assetId)
    {
        var normalized = NormalizeSegmentString(segmentString);
        if (!segmentLookupValueId.HasValue)
        {
            return (normalized, null);
        }

        var lookup = await _context.SegmentLookupValues
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.TenantId == TenantId && v.Id == segmentLookupValueId.Value && !v.IsDeleted);
        if (lookup == null)
        {
            var exception = await BuildBlockedTransferExceptionAsync(assetId, "Fixed asset transfer segment belongs to another tenant or does not exist.");
            throw exception;
        }

        if (!lookup.IsActive || lookup.EffectiveDate.Date > transferDate.Date || (lookup.ExpiryDate.HasValue && lookup.ExpiryDate.Value.Date < transferDate.Date))
        {
            var exception = await BuildBlockedTransferExceptionAsync(assetId, "Fixed asset transfer segment is not active for the transfer date.");
            throw exception;
        }

        return (normalized ?? lookup.SegmentValue, lookup.Id);
    }

    private static void ValidateTransferCanComplete(AssetTransfer transfer, FixedAsset asset)
    {
        if (transfer.TenantId != asset.TenantId)
        {
            throw new InvalidOperationException("Fixed asset transfer tenant does not match the asset tenant.");
        }

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff or FixedAssetStatus.HeldForSale)
        {
            throw new InvalidOperationException("Disposed, written-off, or held-for-sale assets cannot be transferred.");
        }

        if (asset.CapitalizationDate.HasValue && transfer.TransferDate.Date < asset.CapitalizationDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset transfer date cannot be before capitalization date.");
        }

        if (asset.PlacedInServiceDate.HasValue && transfer.TransferDate.Date < asset.PlacedInServiceDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset transfer date cannot be before placed-in-service date.");
        }
    }

    private static bool IsTransferable(FixedAsset asset)
        => asset.CapitalizedAt.HasValue &&
           asset.Status is FixedAssetStatus.Active or FixedAssetStatus.Capitalized or FixedAssetStatus.FullyDepreciated;

    private static string? NormalizeIdempotencyKey(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeSegmentString(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string BuildTransferDescription(AssetTransfer transfer)
    {
        var location = $"from {transfer.FromLocation ?? "(none)"} to {transfer.ToLocation}";
        var segment = transfer.FromSegmentString == transfer.ToSegmentString
            ? null
            : $" segment {transfer.FromSegmentString ?? "(none)"} to {transfer.ToSegmentString ?? "(none)"}";
        return $"Transfer {location}{segment}. Reason: {transfer.Reason}";
    }

    private async Task RecordBlockedTransferAuditAsync(Guid assetId, string reason)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.FixedAssetTransferBlockedInvalidTenantDimensionAccount,
            TenantId = TenantId,
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = assetId,
            Resource = "Finance.FixedAssetTransfer",
            ResourceId = assetId.ToString(),
            Reason = reason,
            Context = new { assetId, reason }
        });
    }

    private async Task<InvalidOperationException> BuildBlockedTransferExceptionAsync(Guid assetId, string reason)
    {
        await RecordBlockedTransferAuditAsync(assetId, reason);
        return new InvalidOperationException(reason);
    }

    private async Task RecordTransferAuditAsync(
        string eventType,
        AssetTransfer transfer,
        object? beforeValues = null,
        object? afterValues = null,
        string? comment = null)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = transfer.Id,
            JournalEntryId = transfer.JournalEntryId,
            PostingEventId = transfer.PostingEventId,
            WorkflowInstanceId = transfer.WorkflowInstanceId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Comment = comment,
            Resource = "Finance.FixedAssetTransfer",
            ResourceId = transfer.Id.ToString(),
            Context = new
            {
                transfer.FixedAssetId,
                transfer.TransferType,
                transfer.Status,
                transfer.ReferenceNumber
            }
        });
    }

    private static AssetTransferDto MapToDto(AssetTransfer t)
    {
        return new AssetTransferDto
        {
            Id = t.Id,
            FixedAssetId = t.FixedAssetId,
            FixedAssetName = t.FixedAsset?.Name,
            AssetCode = t.FixedAsset?.AssetCode,
            TransferDate = t.TransferDate,
            AccountingDate = t.AccountingDate,
            FiscalPeriodId = t.FiscalPeriodId,
            TransferType = t.TransferType,
            Status = t.Status,
            FromLocation = t.FromLocation,
            FromCustodianId = t.FromCustodianId,
            FromCustodianName = t.FromCustodian != null ? $"{t.FromCustodian.FirstName} {t.FromCustodian.LastName}" : null,
            FromSegmentString = t.FromSegmentString,
            FromSegmentLookupValueId = t.FromSegmentLookupValueId,
            ToLocation = t.ToLocation,
            ToCustodianId = t.ToCustodianId,
            ToCustodianName = t.ToCustodian != null ? $"{t.ToCustodian.FirstName} {t.ToCustodian.LastName}" : null,
            ToSegmentString = t.ToSegmentString,
            ToSegmentLookupValueId = t.ToSegmentLookupValueId,
            Reason = t.Reason,
            TransferCost = t.TransferCost,
            RequestedById = t.RequestedById,
            RequestedByName = t.RequestedBy != null ? $"{t.RequestedBy.FirstName} {t.RequestedBy.LastName}" : null,
            ApprovedById = t.ApprovedById,
            ApprovedByName = t.ApprovedBy != null ? $"{t.ApprovedBy.FirstName} {t.ApprovedBy.LastName}" : null,
            ApprovedAt = t.ApprovedAt,
            CompletedAt = t.CompletedAt,
            PostedAt = t.PostedAt,
            FailedAt = t.FailedAt,
            Comments = t.Comments,
            FailureReason = t.FailureReason,
            ReferenceNumber = t.ReferenceNumber,
            IdempotencyKey = t.IdempotencyKey,
            WorkflowInstanceId = t.WorkflowInstanceId,
            JournalEntryId = t.JournalEntryId,
            PostingEventId = t.PostingEventId,
            CreatedAt = t.CreatedAt
        };
    }
}
