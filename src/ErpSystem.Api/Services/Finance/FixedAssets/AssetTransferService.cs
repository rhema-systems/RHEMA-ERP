using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetTransferService : IAssetTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IWorkflowService _workflowService;

    public AssetTransferService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        IWorkflowService workflowService)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _workflowService = workflowService;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public async Task<AssetTransferDto?> GetByIdAsync(Guid id)
    {
        var transfer = await _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .Include(t => t.FromCustodian)
            .Include(t => t.ToCustodian)
            .Include(t => t.RequestedBy)
            .Include(t => t.ApprovedBy)
            .Where(t => t.TenantId == TenantId && t.Id == id)
            .FirstOrDefaultAsync();

        return transfer == null ? null : MapToDto(transfer);
    }

    public async Task<IEnumerable<AssetTransferDto>> GetAllAsync()
    {
        var transfers = await _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .Include(t => t.FromCustodian)
            .Include(t => t.ToCustodian)
            .Where(t => t.TenantId == TenantId)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<AssetTransferDto>> GetByAssetIdAsync(Guid assetId)
    {
        var transfers = await _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .Include(t => t.FromCustodian)
            .Include(t => t.ToCustodian)
            .Where(t => t.TenantId == TenantId && t.FixedAssetId == assetId)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

        return transfers.Select(MapToDto).ToList();
    }

    public async Task<AssetTransferDto> RequestTransferAsync(RequestAssetTransferDto dto, Guid requestedById)
    {
        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        var transfer = new AssetTransfer
        {
            TenantId = TenantId,
            FixedAssetId = dto.FixedAssetId,
            TransferDate = dto.TransferDate,
            TransferType = dto.TransferType,
            Status = AssetTransferStatus.PendingApproval,
            FromLocation = asset.Location,
            ToLocation = dto.ToLocation,
            ToCustodianId = dto.ToCustodianId,
            Reason = dto.Reason,
            TransferCost = dto.TransferCost,
            RequestedById = requestedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            ReferenceNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.AssetTransfer,
                TenantId,
                dto.TransferDate,
                nameof(AssetTransfer))
        };

        _context.AssetTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        var workflowResult = await _workflowService.StartApprovalWorkflowAsync("AssetTransfer", transfer.Id);
        if (!workflowResult.Success)
        {
            transfer.Status = AssetTransferStatus.Rejected;
            transfer.Comments = workflowResult.Message ?? "Unable to start asset transfer approval workflow.";
            transfer.UpdatedAt = DateTime.UtcNow;
            transfer.UpdatedBy = UserName;
            await _context.SaveChangesAsync();

            throw new InvalidOperationException(transfer.Comments);
        }

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to request transfer.");
    }

    public async Task<AssetTransferDto> ApproveTransferAsync(Guid transferId, Guid approvedById, ApproveAssetTransferDto dto)
    {
        var transfer = await _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId)
            ?? throw new KeyNotFoundException("Transfer request not found.");

        if (transfer.Status != AssetTransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending transfers can be approved.");
        }

        if (CurrentUserId == Guid.Empty)
            throw new InvalidOperationException("Unable to resolve the current approver.");

        if (!await _workflowService.CanUserApproveAsync("AssetTransfer", transferId, CurrentUserId))
            throw new InvalidOperationException("This asset transfer is assigned to another workflow approver.");

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("AssetTransfer", transferId, CurrentUserId, "Approve", dto.Comments);
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process asset transfer approval.");

        if (workflowResult.Status != WorkflowInstanceStatus.Completed)
            return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to retrieve transfer.");

        transfer.Status = AssetTransferStatus.Approved;
        transfer.ApprovedById = approvedById;
        transfer.ApprovedAt = DateTime.UtcNow;
        transfer.Comments = dto.Comments;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = UserName;

        await _context.SaveChangesAsync();

        return await CompleteTransferAsync(transfer.Id);
    }

    public async Task<AssetTransferDto> CompleteTransferAsync(Guid transferId)
    {
        var transfer = await _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId)
            ?? throw new KeyNotFoundException("Transfer request not found.");

        if (transfer.Status != AssetTransferStatus.Approved)
        {
            throw new InvalidOperationException("Only approved transfers can be completed.");
        }

        transfer.FixedAsset.Location = transfer.ToLocation;
        transfer.FixedAsset.UpdatedAt = DateTime.UtcNow;
        transfer.FixedAsset.UpdatedBy = UserName;
        transfer.Status = AssetTransferStatus.Completed;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = UserName;

        var assetTransaction = new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = transfer.FixedAssetId,
            TransactionDate = DateTime.UtcNow,
            TransactionType = "Transfer",
            Description = $"Transfer from {transfer.FromLocation} to {transfer.ToLocation}. Reason: {transfer.Reason}",
            Amount = 0,
            ResultingBookValue = transfer.FixedAsset.NetBookValue,
            RelatedEntityId = transfer.Id,
            PerformedByUserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.AssetTransactions.Add(assetTransaction);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to complete transfer.");
    }

    public async Task<AssetTransferDto> RejectTransferAsync(Guid transferId, Guid rejectedById, string comments)
    {
        var transfer = await _context.AssetTransfers
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId)
            ?? throw new KeyNotFoundException("Transfer request not found.");

        if (transfer.Status != AssetTransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending transfers can be rejected.");
        }

        if (CurrentUserId == Guid.Empty)
            throw new InvalidOperationException("Unable to resolve the current approver.");

        if (!await _workflowService.CanUserApproveAsync("AssetTransfer", transferId, CurrentUserId))
            throw new InvalidOperationException("This asset transfer is assigned to another workflow approver.");

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("AssetTransfer", transferId, CurrentUserId, "Reject", comments);
        if (!workflowResult.Success)
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process asset transfer rejection.");

        if (workflowResult.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to retrieve transfer.");

        transfer.Status = AssetTransferStatus.Rejected;
        transfer.Comments = comments;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = UserName;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to reject transfer.");
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
            TransferType = t.TransferType,
            Status = t.Status,
            FromLocation = t.FromLocation,
            FromCustodianId = t.FromCustodianId,
            FromCustodianName = t.FromCustodian != null ? $"{t.FromCustodian.FirstName} {t.FromCustodian.LastName}" : null,
            ToLocation = t.ToLocation,
            ToCustodianId = t.ToCustodianId,
            ToCustodianName = t.ToCustodian != null ? $"{t.ToCustodian.FirstName} {t.ToCustodian.LastName}" : null,
            Reason = t.Reason,
            TransferCost = t.TransferCost,
            RequestedById = t.RequestedById,
            RequestedByName = t.RequestedBy != null ? $"{t.RequestedBy.FirstName} {t.RequestedBy.LastName}" : null,
            ApprovedById = t.ApprovedById,
            ApprovedByName = t.ApprovedBy != null ? $"{t.ApprovedBy.FirstName} {t.ApprovedBy.LastName}" : null,
            ApprovedAt = t.ApprovedAt,
            Comments = t.Comments,
            ReferenceNumber = t.ReferenceNumber,
            CreatedAt = t.CreatedAt
        };
    }
}
