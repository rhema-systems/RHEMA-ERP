using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
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
    private readonly IJournalEntryService _journalEntryService;

    public AssetDisposalService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IJournalEntryService journalEntryService)
    {
        _context = context;
        _currentUser = currentUser;
        _journalEntryService = journalEntryService;
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

        await _context.SaveChangesAsync();

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to approve disposal.");
    }

    public async Task<AssetDisposalDto> CompleteDisposalAsync(Guid disposalId)
    {
        var disposal = await _context.AssetDisposals
            .Include(d => d.FixedAsset)
                .ThenInclude(a => a.Category)
            .FirstOrDefaultAsync(d => d.TenantId == TenantId && d.Id == disposalId)
            ?? throw new KeyNotFoundException("Disposal request not found.");

        if (disposal.Status != AssetDisposalStatus.Approved)
            throw new InvalidOperationException("Only approved disposals can be completed.");

        var asset = disposal.FixedAsset;
        var category = asset.Category;

        // == GL Journal Entry ==
        var journalNumber = await _journalEntryService.GenerateJournalEntryNumberAsync();
        var reference = $"DSP-{asset.AssetCode}-{disposal.DisposalDate:yyyyMMdd}";
        var netProceeds = disposal.SaleProceeds - disposal.DisposalCost;
        var gainOrLoss = netProceeds - asset.NetBookValue;
        var accumulatedDepr = asset.AcquisitionCost - asset.NetBookValue;

        var transactions = new List<CreateAccountTransactionDto>();
        int line = 1;

        // Dr Accumulated Depreciation (clear)
        if (accumulatedDepr > 0)
        {
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = category.AccumulatedDepreciationAccountId,
                Amount = accumulatedDepr,
                TransactionType = "Debit",
                Description = "Clear accumulated depreciation",
                Reference = reference,
                LineNumber = line++
            });
        }

        // Cr Asset Account (original cost)
        if (category.AssetAccountId != Guid.Empty)
        {
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = category.AssetAccountId,
                Amount = asset.AcquisitionCost,
                TransactionType = "Credit",
                Description = "Remove asset from books",
                Reference = reference,
                LineNumber = line++
            });
        }

        // Dr/Cr Gain or Loss
        if (gainOrLoss > 0 && category.GainOnDisposalAccountId.HasValue)
        {
            // Net Credit: Gain on Disposal
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = category.GainOnDisposalAccountId.Value,
                Amount = Math.Abs(gainOrLoss),
                TransactionType = "Credit",
                Description = "Gain on disposal",
                Reference = reference,
                LineNumber = line++
            });
        }
        else if (gainOrLoss < 0 && category.LossOnDisposalAccountId.HasValue)
        {
            // Net Debit: Loss on Disposal
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = category.LossOnDisposalAccountId.Value,
                Amount = Math.Abs(gainOrLoss),
                TransactionType = "Debit",
                Description = "Loss on disposal",
                Reference = reference,
                LineNumber = line++
            });
        }

        // Dr Cash/AR for net sale proceeds
        if (netProceeds > 0)
        {
            // Use the Asset Account as placeholder; ideally this would be a Cash/AR account
            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = category.AssetAccountId,
                Amount = netProceeds,
                TransactionType = "Debit",
                Description = "Sale proceeds receivable",
                Reference = reference,
                LineNumber = line++
            });
        }

        if (transactions.Count >= 2)
        {
            var entry = new CreateJournalEntryDto
            {
                JournalNumber = journalNumber,
                TransactionDate = disposal.DisposalDate,
                Description = $"Disposal of {asset.AssetCode} — {asset.Name} ({disposal.DisposalType})",
                Reference = reference,
                SourceModule = "FixedAssets",
                Transactions = transactions
            };

            var created = await _journalEntryService.CreateJournalEntryAsync(entry);
            await _journalEntryService.PostJournalEntryAsync(created.Id);
        }

        // == Finalize Disposal ==
        disposal.Status = AssetDisposalStatus.Completed;
        disposal.GainOrLoss = gainOrLoss;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;

        asset.Status = FixedAssetStatus.Disposed;
        asset.DisposalDate = disposal.DisposalDate;
        asset.NetBookValue = 0;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        // Log audit trail
        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = disposal.FixedAssetId,
            TransactionDate = disposal.DisposalDate,
            TransactionType = "Disposal",
            Description = $"Disposal ({disposal.DisposalType}) to {disposal.BuyerName}. Proceeds: {netProceeds:N2}, Gain/Loss: {gainOrLoss:N2}",
            Amount = netProceeds,
            ResultingBookValue = 0,
            RelatedEntityId = disposal.Id,
            PerformedByUserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        });

        await _context.SaveChangesAsync();

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to complete disposal.");
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

    public async Task<BulkOperationResultDto<AssetDisposalDto>> RequestBulkDisposalAsync(
        RequestBulkAssetDisposalDto dto, Guid requestedById)
    {
        var result = new BulkOperationResultDto<AssetDisposalDto>
        {
            TotalCount = dto.FixedAssetIds.Count
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var assetId in dto.FixedAssetIds)
            {
                try
                {
                    var singleDto = new RequestAssetDisposalDto
                    {
                        FixedAssetId = assetId,
                        DisposalDate = dto.DisposalDate,
                        DisposalType = dto.DisposalType,
                        Reason = dto.Reason,
                        BuyerName = dto.BuyerName,
                        SaleProceeds = dto.SaleProceeds,
                        DisposalCost = dto.DisposalCost
                    };

                    var disposal = await RequestDisposalAsync(singleDto, requestedById);
                    result.SuccessfulItems.Add(disposal);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Asset {assetId}: {ex.Message}");
                    result.FailureCount++;
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.Errors.Add($"Bulk operation failed: {ex.Message}");
            result.SuccessCount = 0;
            result.FailureCount = result.TotalCount;
            result.SuccessfulItems.Clear();
        }

        return result;
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
