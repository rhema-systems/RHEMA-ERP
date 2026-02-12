using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetDisposalService : IAssetDisposalService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssetDisposalService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public async Task<AssetDisposalDto?> GetByIdAsync(Guid id)
    {
        var disposal = await _context.AssetDisposals
            .Include(d => d.FixedAsset)
            .Include(d => d.RequestedBy)
            .Include(d => d.ApprovedBy)
            .Where(d => d.TenantId == TenantId && d.Id == id)
            .FirstOrDefaultAsync();

        return disposal == null ? null : MapToDto(disposal);
    }

    public async Task<IEnumerable<AssetDisposalDto>> GetAllAsync()
    {
        var disposals = await _context.AssetDisposals
            .Include(d => d.FixedAsset)
            .Include(d => d.RequestedBy)
            .Include(d => d.ApprovedBy)
            .Where(d => d.TenantId == TenantId)
            .OrderByDescending(d => d.DisposalDate)
            .ToListAsync();

        return disposals.Select(MapToDto).ToList();
    }

    public async Task<AssetDisposalDto> RequestDisposalAsync(RequestAssetDisposalDto dto, Guid requestedById)
    {
        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status == FixedAssetStatus.Disposed)
        {
            throw new InvalidOperationException("Asset is already disposed.");
        }

        var disposal = new AssetDisposal
        {
            TenantId = TenantId,
            FixedAssetId = dto.FixedAssetId,
            DisposalDate = dto.DisposalDate,
            DisposalType = dto.DisposalType,
            Status = AssetDisposalStatus.PendingApproval,
            Reason = dto.Reason,
            SaleProceeds = dto.SaleProceeds,
            DisposalCost = dto.DisposalCost,
            NetBookValueAtDisposal = asset.NetBookValue,
            GainOrLoss = (dto.SaleProceeds - dto.DisposalCost) - asset.NetBookValue,
            BuyerName = dto.BuyerName,
            RequestedById = requestedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            ReferenceNumber = $"DSP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}"
        };

        _context.AssetDisposals.Add(disposal);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to request disposal.");
    }

    public async Task<AssetDisposalDto> ApproveDisposalAsync(Guid disposalId, Guid approvedById, ApproveAssetDisposalDto dto)
    {
        var disposal = await _context.AssetDisposals
            .Include(d => d.FixedAsset)
            .FirstOrDefaultAsync(d => d.TenantId == TenantId && d.Id == disposalId)
            ?? throw new KeyNotFoundException("Disposal request not found.");

        if (disposal.Status != AssetDisposalStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending disposals can be approved.");
        }

        disposal.Status = AssetDisposalStatus.Approved;
        disposal.ApprovedById = approvedById;
        disposal.ApprovedAt = DateTime.UtcNow;
        disposal.Comments = dto.Comments;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;

        // Finalize disposal on approval
        disposal.Status = AssetDisposalStatus.Completed;
        disposal.FixedAsset.Status = FixedAssetStatus.Disposed;
        disposal.FixedAsset.DisposalDate = disposal.DisposalDate;
        disposal.FixedAsset.UpdatedAt = DateTime.UtcNow;
        disposal.FixedAsset.UpdatedBy = UserName;

        var assetTransaction = new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = disposal.FixedAssetId,
            TransactionDate = DateTime.UtcNow,
            TransactionType = "Disposal",
            Description = $"Disposal ({disposal.DisposalType}) to {disposal.BuyerName}. Gain/Loss: {disposal.GainOrLoss}",
            Amount = disposal.SaleProceeds - disposal.DisposalCost,
            ResultingBookValue = 0,
            RelatedEntityId = disposal.Id,
            PerformedByUserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.AssetTransactions.Add(assetTransaction);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to approve disposal.");
    }

    public async Task<AssetDisposalDto> RejectDisposalAsync(Guid disposalId, Guid rejectedById, string comments)
    {
        var disposal = await _context.AssetDisposals
            .FirstOrDefaultAsync(d => d.TenantId == TenantId && d.Id == disposalId)
            ?? throw new KeyNotFoundException("Disposal request not found.");

        disposal.Status = AssetDisposalStatus.Rejected;
        disposal.Comments = comments;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to reject disposal.");
    }

    private static AssetDisposalDto MapToDto(AssetDisposal d)
    {
        return new AssetDisposalDto
        {
            Id = d.Id,
            FixedAssetId = d.FixedAssetId,
            FixedAssetName = d.FixedAsset?.Name,
            AssetCode = d.FixedAsset?.AssetCode,
            DisposalDate = d.DisposalDate,
            DisposalType = d.DisposalType,
            Status = d.Status,
            Reason = d.Reason,
            SaleProceeds = d.SaleProceeds,
            DisposalCost = d.DisposalCost,
            NetBookValueAtDisposal = d.NetBookValueAtDisposal,
            GainOrLoss = d.GainOrLoss,
            BuyerName = d.BuyerName,
            ReferenceNumber = d.ReferenceNumber,
            RequestedById = d.RequestedById,
            RequestedByName = d.RequestedBy != null ? $"{d.RequestedBy.FirstName} {d.RequestedBy.LastName}" : null,
            ApprovedById = d.ApprovedById,
            ApprovedByName = d.ApprovedBy != null ? $"{d.ApprovedBy.FirstName} {d.ApprovedBy.LastName}" : null,
            ApprovedAt = d.ApprovedAt,
            Comments = d.Comments,
            CreatedAt = d.CreatedAt
        };
    }
}
