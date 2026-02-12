using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetTransferService : IAssetTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssetTransferService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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
            .Include(a => a.MaintenanceAsset)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        var transfer = new AssetTransfer
        {
            TenantId = TenantId,
            FixedAssetId = dto.FixedAssetId,
            TransferDate = dto.TransferDate,
            TransferType = dto.TransferType,
            Status = AssetTransferStatus.PendingApproval,
            FromLocation = asset.MaintenanceAsset?.Location,
            FromCustodianId = asset.MaintenanceAsset?.EmployeeId,
            ToLocation = dto.ToLocation,
            ToCustodianId = dto.ToCustodianId,
            Reason = dto.Reason,
            TransferCost = dto.TransferCost,
            RequestedById = requestedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            ReferenceNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}"
        };

        _context.AssetTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(transfer.Id) ?? throw new InvalidOperationException("Failed to request transfer.");
    }

    public async Task<AssetTransferDto> ApproveTransferAsync(Guid transferId, Guid approvedById, ApproveAssetTransferDto dto)
    {
        var transfer = await _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .ThenInclude(a => a.MaintenanceAsset)
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId)
            ?? throw new KeyNotFoundException("Transfer request not found.");

        if (transfer.Status != AssetTransferStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending transfers can be approved.");
        }

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
            .ThenInclude(a => a.MaintenanceAsset)
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId)
            ?? throw new KeyNotFoundException("Transfer request not found.");

        if (transfer.Status != AssetTransferStatus.Approved)
        {
            throw new InvalidOperationException("Only approved transfers can be completed.");
        }

        if (transfer.FixedAsset.MaintenanceAsset != null)
        {
            transfer.FixedAsset.MaintenanceAsset.Location = transfer.ToLocation;
            transfer.FixedAsset.MaintenanceAsset.EmployeeId = transfer.ToCustodianId;
            transfer.FixedAsset.MaintenanceAsset.UpdatedAt = DateTime.UtcNow;
            transfer.FixedAsset.MaintenanceAsset.UpdatedBy = UserName;
        }

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
