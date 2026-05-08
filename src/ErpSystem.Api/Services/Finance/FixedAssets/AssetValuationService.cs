using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetValuationService : IAssetValuationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IJournalEntryService _journalEntryService;

    public AssetValuationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IJournalEntryService journalEntryService)
    {
        _context = context;
        _currentUser = currentUser;
        _journalEntryService = journalEntryService;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
    private Guid? UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : null;

    public async Task<AssetValuationDto> CreateValuationAsync(
        CreateAssetValuationDto dto, Guid performedByUserId)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status != FixedAssetStatus.Active)
            throw new InvalidOperationException("Can only revalue Active assets.");

        var valuation = BuildValuation(asset, dto, performedByUserId);
        _context.Set<AssetValuation>().Add(valuation);

        // Update asset NBV
        asset.NetBookValue = valuation.CarryingAmountAfter;
        if (dto.RevisedUsefulLifeMonths.HasValue)
            asset.UsefulLifeMonths = dto.RevisedUsefulLifeMonths.Value;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUser.UserName ?? "system";

        // Log AssetTransaction
        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = asset.Id,
            TransactionDate = dto.ValuationDate,
            TransactionType = dto.ValuationType == ValuationType.Revaluation
                ? "Revaluation" : dto.ValuationType == ValuationType.ImpairmentReversal
                    ? "Impairment Reversal" : "Impairment",
            Description = $"{dto.ValuationType} — Fair value: {dto.FairValue:N2}, Carrying: {valuation.CarryingAmountBefore:N2} → {valuation.CarryingAmountAfter:N2}",
            Amount = valuation.CarryingAmountAfter - valuation.CarryingAmountBefore,
            ResultingBookValue = valuation.CarryingAmountAfter,
            RelatedEntityId = valuation.Id,
            PerformedByUserId = performedByUserId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return MapToDto(valuation, asset);
    }

    public async Task<BulkOperationResultDto<AssetValuationDto>> CreateBulkValuationAsync(
        CreateBulkAssetValuationDto dto, Guid performedByUserId)
    {
        var result = new BulkOperationResultDto<AssetValuationDto>
        {
            TotalCount = dto.FixedAssetIds.Count
        };

        var assets = await _context.FixedAssets
            .Include(a => a.Category)
            .Where(a => a.TenantId == TenantId
                && dto.FixedAssetIds.Contains(a.Id)
                && a.Status == FixedAssetStatus.Active)
            .ToListAsync();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var asset in assets)
            {
                // Calculate fair value from index percentage
                var fairValue = asset.NetBookValue * (1 + dto.IndexPercentage / 100m);

                var singleDto = new CreateAssetValuationDto
                {
                    FixedAssetId = asset.Id,
                    ValuationDate = dto.ValuationDate,
                    ValuationType = dto.ValuationType,
                    FairValue = Math.Round(fairValue, 2),
                    ValuerName = dto.ValuerName,
                    ValuationMethod = dto.ValuationMethod,
                    ValuationReportReference = dto.ValuationReportReference,
                    Reason = dto.Reason ?? $"Bulk revaluation — {dto.IndexPercentage}% index adjustment",
                    Notes = dto.Notes
                };

                try
                {
                    var valuation = BuildValuation(asset, singleDto, performedByUserId);
                    _context.Set<AssetValuation>().Add(valuation);

                    asset.NetBookValue = valuation.CarryingAmountAfter;
                    asset.UpdatedAt = DateTime.UtcNow;
                    asset.UpdatedBy = _currentUser.UserName ?? "system";

                    _context.AssetTransactions.Add(new AssetTransaction
                    {
                        TenantId = TenantId,
                        FixedAssetId = asset.Id,
                        TransactionDate = dto.ValuationDate,
                        TransactionType = dto.ValuationType == ValuationType.Revaluation
                            ? "Revaluation" : "Impairment",
                        Description = $"Bulk {dto.ValuationType} — {dto.IndexPercentage}% index, {valuation.CarryingAmountBefore:N2} → {valuation.CarryingAmountAfter:N2}",
                        Amount = valuation.CarryingAmountAfter - valuation.CarryingAmountBefore,
                        ResultingBookValue = valuation.CarryingAmountAfter,
                        RelatedEntityId = valuation.Id,
                        PerformedByUserId = performedByUserId,
                        CreatedAt = DateTime.UtcNow
                    });

                    result.SuccessfulItems.Add(MapToDto(valuation, asset));
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Asset {asset.AssetCode}: {ex.Message}");
                    result.FailureCount++;
                }
            }

            // Report assets that weren't found or weren't Active
            var processedIds = assets.Select(a => a.Id).ToHashSet();
            foreach (var missingId in dto.FixedAssetIds.Where(id => !processedIds.Contains(id)))
            {
                result.Errors.Add($"Asset {missingId}: Not found or not in Active status");
                result.FailureCount++;
            }

            await _context.SaveChangesAsync();
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

    public async Task<IEnumerable<AssetValuationDto>> GetValuationsByAssetAsync(Guid assetId)
    {
        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == assetId);

        var valuations = await _context.Set<AssetValuation>()
            .Where(v => v.TenantId == TenantId && v.FixedAssetId == assetId)
            .OrderByDescending(v => v.ValuationDate)
            .ToListAsync();

        return valuations.Select(v => MapToDto(v, asset)).ToList();
    }

    public async Task<AssetValuationDto> PostValuationToGLAsync(Guid valuationId)
    {
        var valuation = await _context.Set<AssetValuation>()
            .Include(v => v.FixedAsset)
                .ThenInclude(a => a.Category)
            .FirstOrDefaultAsync(v => v.TenantId == TenantId && v.Id == valuationId)
            ?? throw new KeyNotFoundException("Valuation not found.");

        if (valuation.IsPostedToGL)
            throw new InvalidOperationException("Valuation has already been posted to GL.");

        var asset = valuation.FixedAsset;
        var category = asset.Category;

        // Determine GL accounts based on valuation type
        var journalNumber = await _journalEntryService.GenerateJournalEntryNumberAsync();
        var reference = $"VAL-{asset.AssetCode}-{valuation.ValuationDate:yyyyMMdd}";

        var transactions = new List<CreateAccountTransactionDto>();

        switch (valuation.ValuationType)
        {
            case ValuationType.Revaluation:
                // Dr Asset Account (increase value)
                // Cr Revaluation Surplus (OCI)
                if (category.RevaluationSurplusAccountId == null)
                    throw new InvalidOperationException("Revaluation Surplus GL account not configured for this category.");

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = category.AssetAccountId,
                    Amount = valuation.RevaluationSurplus,
                    TransactionType = "Debit",
                    Description = "Revaluation — asset value increase",
                    Reference = reference,
                    LineNumber = 1
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = category.RevaluationSurplusAccountId.Value,
                    Amount = valuation.RevaluationSurplus,
                    TransactionType = "Credit",
                    Description = "Revaluation Surplus (OCI)",
                    Reference = reference,
                    LineNumber = 2
                });
                break;

            case ValuationType.Impairment:
                // Dr Impairment Loss (P&L) — use Depreciation Expense account or Loss on Disposal
                // Cr Asset Account (reduce value)
                var impairmentLossAccountId = category.LossOnDisposalAccountId
                    ?? category.DepreciationExpenseAccountId;

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = impairmentLossAccountId,
                    Amount = valuation.ImpairmentLoss,
                    TransactionType = "Debit",
                    Description = "Impairment Loss",
                    Reference = reference,
                    LineNumber = 1
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = category.AssetAccountId,
                    Amount = valuation.ImpairmentLoss,
                    TransactionType = "Credit",
                    Description = "Asset value reduction — impairment",
                    Reference = reference,
                    LineNumber = 2
                });
                break;

            case ValuationType.ImpairmentReversal:
                // Dr Asset Account (restore value)
                // Cr Impairment Reversal (P&L)
                var reversalAccountId = category.GainOnDisposalAccountId
                    ?? category.DepreciationExpenseAccountId;

                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = category.AssetAccountId,
                    Amount = valuation.ImpairmentReversal,
                    TransactionType = "Debit",
                    Description = "Impairment reversal — asset value restoration",
                    Reference = reference,
                    LineNumber = 1
                });
                transactions.Add(new CreateAccountTransactionDto
                {
                    AccountId = reversalAccountId,
                    Amount = valuation.ImpairmentReversal,
                    TransactionType = "Credit",
                    Description = "Impairment Reversal (P&L)",
                    Reference = reference,
                    LineNumber = 2
                });
                break;
        }

        var entry = new CreateJournalEntryDto
        {
            JournalNumber = journalNumber,
            TransactionDate = valuation.ValuationDate,
            Description = $"{valuation.ValuationType} for {asset.AssetCode} — {asset.Name}",
            Reference = reference,
            SourceModule = "FixedAssets",
            Transactions = transactions
        };

        var created = await _journalEntryService.CreateJournalEntryAsync(entry);
        var posted = await _journalEntryService.PostJournalEntryAsync(created.Id);

        valuation.IsPostedToGL = true;
        valuation.PostedDate = DateTime.UtcNow;
        valuation.JournalEntryId = posted.Id;

        await _context.SaveChangesAsync();
        return MapToDto(valuation, asset);
    }

    #region Private Helpers

    private AssetValuation BuildValuation(FixedAsset asset, CreateAssetValuationDto dto, Guid performedByUserId)
    {
        var carryingBefore = asset.NetBookValue;
        var carryingAfter = dto.FairValue;

        decimal surplus = 0, deficit = 0, impairmentLoss = 0, impairmentReversal = 0;

        switch (dto.ValuationType)
        {
            case ValuationType.Revaluation:
                if (dto.FairValue > carryingBefore)
                    surplus = dto.FairValue - carryingBefore;
                else
                    deficit = carryingBefore - dto.FairValue;
                break;

            case ValuationType.Impairment:
                if (dto.FairValue >= carryingBefore)
                    throw new InvalidOperationException("Fair value must be less than carrying amount for impairment.");
                impairmentLoss = carryingBefore - dto.FairValue;
                break;

            case ValuationType.ImpairmentReversal:
                if (dto.FairValue <= carryingBefore)
                    throw new InvalidOperationException("Fair value must be greater than carrying amount for impairment reversal.");
                impairmentReversal = dto.FairValue - carryingBefore;
                break;
        }

        return new AssetValuation
        {
            TenantId = TenantId,
            FixedAssetId = asset.Id,
            ValuationDate = dto.ValuationDate,
            ValuationType = dto.ValuationType,
            CarryingAmountBefore = carryingBefore,
            FairValue = dto.FairValue,
            CarryingAmountAfter = carryingAfter,
            RevaluationSurplus = surplus,
            RevaluationDeficit = deficit,
            ImpairmentLoss = impairmentLoss,
            ImpairmentReversal = impairmentReversal,
            RevisedUsefulLifeMonths = dto.RevisedUsefulLifeMonths,
            ValuerName = dto.ValuerName,
            ValuationMethod = dto.ValuationMethod,
            ValuationReportReference = dto.ValuationReportReference,
            Reason = dto.Reason,
            Notes = dto.Notes,
            PerformedByUserId = performedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static AssetValuationDto MapToDto(AssetValuation v, FixedAsset? asset)
    {
        return new AssetValuationDto
        {
            Id = v.Id,
            FixedAssetId = v.FixedAssetId,
            AssetCode = asset?.AssetCode,
            AssetName = asset?.Name,
            ValuationDate = v.ValuationDate,
            ValuationType = v.ValuationType,
            CarryingAmountBefore = v.CarryingAmountBefore,
            FairValue = v.FairValue,
            CarryingAmountAfter = v.CarryingAmountAfter,
            RevaluationSurplus = v.RevaluationSurplus,
            RevaluationDeficit = v.RevaluationDeficit,
            ImpairmentLoss = v.ImpairmentLoss,
            ImpairmentReversal = v.ImpairmentReversal,
            RevisedUsefulLifeMonths = v.RevisedUsefulLifeMonths,
            ValuerName = v.ValuerName,
            ValuationMethod = v.ValuationMethod,
            ValuationReportReference = v.ValuationReportReference,
            Reason = v.Reason,
            Notes = v.Notes,
            IsPostedToGL = v.IsPostedToGL,
            JournalEntryId = v.JournalEntryId,
            PostedDate = v.PostedDate,
            CreatedAt = v.CreatedAt
        };
    }

    #endregion
}
