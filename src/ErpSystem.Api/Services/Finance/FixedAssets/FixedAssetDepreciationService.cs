using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class FixedAssetDepreciationService : IFixedAssetDepreciationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IJournalEntryService _journalEntryService;

    public FixedAssetDepreciationService(
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

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> RunDepreciationAsync(
        RunDepreciationDto dto,
        CancellationToken cancellationToken = default)
    {
        var fiscalPeriod = await _context.FiscalPeriods
            .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == dto.FiscalPeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal period not found.");

        if (fiscalPeriod.IsLocked || !fiscalPeriod.IsOpen)
        {
            throw new InvalidOperationException("Fiscal period is locked or not open.");
        }

        var assetsQuery = _context.FixedAssets
            .Include(a => a.Category)
            .Where(a => a.TenantId == TenantId);

        if (dto.FixedAssetId.HasValue)
        {
            assetsQuery = assetsQuery.Where(a => a.Id == dto.FixedAssetId.Value);
        }

        var assets = await assetsQuery.ToListAsync(cancellationToken);
        if (assets.Count == 0) return Array.Empty<AssetDepreciationScheduleDto>();

        var existingSchedules = await _context.AssetDepreciationSchedules
            .Where(s => s.TenantId == TenantId && s.FiscalPeriodId == fiscalPeriod.Id)
            .Select(s => s.FixedAssetId)
            .ToListAsync(cancellationToken);

        var existingSet = existingSchedules.ToHashSet();
        var results = new List<AssetDepreciationScheduleDto>();

        foreach (var asset in assets)
        {
            if (existingSet.Contains(asset.Id)) continue;
            if (asset.PlacedInServiceDate == null || asset.PlacedInServiceDate > fiscalPeriod.EndDate) continue;
            if (asset.Status == FixedAssetStatus.Disposed || asset.Status == FixedAssetStatus.WrittenOff)
            {
                continue;
            }

            var accumulated = await _context.AssetDepreciationSchedules
                .Where(s => s.TenantId == TenantId && s.FixedAssetId == asset.Id)
                .SumAsync(s => s.DepreciationAmount, cancellationToken);

            var depreciableBase = asset.AcquisitionCost - asset.ResidualValue;
            var remaining = Math.Max(depreciableBase - accumulated, 0);
            if (remaining <= 0)
            {
                asset.Status = FixedAssetStatus.FullyDepreciated;
                continue;
            }

            var depreciationAmount = CalculateDepreciation(asset, remaining);
            if (depreciationAmount <= 0) continue;

            if (depreciationAmount > remaining)
            {
                depreciationAmount = remaining;
            }

            var newAccumulated = accumulated + depreciationAmount;
            var netBookValue = asset.AcquisitionCost - newAccumulated;

            if (netBookValue < asset.ResidualValue)
            {
                netBookValue = asset.ResidualValue;
                depreciationAmount = asset.AcquisitionCost - asset.ResidualValue - accumulated;
            }

            var schedule = new AssetDepreciationSchedule
            {
                TenantId = TenantId,
                FixedAssetId = asset.Id,
                FiscalPeriodId = fiscalPeriod.Id,
                DepreciationAmount = depreciationAmount,
                AccumulatedDepreciation = newAccumulated,
                NetBookValue = netBookValue,
                IsPosted = false,
                IsProjected = false,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.PostToGl)
            {
                var journalEntryId = await PostDepreciationToGlAsync(asset, fiscalPeriod, depreciationAmount, dto.PostingDate, cancellationToken);
                schedule.IsPosted = true;
                schedule.PostedDate = DateTime.UtcNow;
                schedule.JournalEntryId = journalEntryId;
            }

            _context.AssetDepreciationSchedules.Add(schedule);

            asset.NetBookValue = netBookValue;
            asset.Status = netBookValue <= asset.ResidualValue ? FixedAssetStatus.FullyDepreciated : FixedAssetStatus.Active;
            asset.UpdatedAt = DateTime.UtcNow;

            _context.AssetTransactions.Add(new AssetTransaction
            {
                TenantId = TenantId,
                FixedAssetId = asset.Id,
                TransactionDate = dto.PostingDate ?? fiscalPeriod.EndDate,
                TransactionType = "Depreciation",
                Description = $"Depreciation for {fiscalPeriod.PeriodCode}",
                Amount = depreciationAmount,
                ResultingBookValue = netBookValue,
                RelatedEntityId = schedule.JournalEntryId,
                PerformedByUserId = UserId ?? Guid.Empty,
                CreatedAt = DateTime.UtcNow
            });

            results.Add(MapSchedule(schedule));
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (!dto.FixedAssetId.HasValue && dto.PostToGl)
        {
            fiscalPeriod.DepreciationComplete = true;
            fiscalPeriod.DepreciationCompletedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return results;
    }

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> GetSchedulesForAssetAsync(
        Guid fixedAssetId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await _context.AssetDepreciationSchedules
            .Where(s => s.TenantId == TenantId && s.FixedAssetId == fixedAssetId)
            .OrderBy(s => s.FiscalPeriodId)
            .Select(s => new AssetDepreciationScheduleDto
            {
                Id = s.Id,
                FixedAssetId = s.FixedAssetId,
                FiscalPeriodId = s.FiscalPeriodId,
                DepreciationAmount = s.DepreciationAmount,
                AccumulatedDepreciation = s.AccumulatedDepreciation,
                NetBookValue = s.NetBookValue,
                IsPosted = s.IsPosted,
                PostedDate = s.PostedDate,
                JournalEntryId = s.JournalEntryId,
                IsProjected = s.IsProjected
            })
            .ToListAsync(cancellationToken);

        return schedules;
    }

    public async Task<IReadOnlyList<AssetDepreciationScheduleDto>> GetSchedulesForPeriodAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        var schedules = await _context.AssetDepreciationSchedules
            .Where(s => s.TenantId == TenantId && s.FiscalPeriodId == fiscalPeriodId)
            .OrderBy(s => s.FixedAssetId)
            .Select(s => new AssetDepreciationScheduleDto
            {
                Id = s.Id,
                FixedAssetId = s.FixedAssetId,
                FiscalPeriodId = s.FiscalPeriodId,
                DepreciationAmount = s.DepreciationAmount,
                AccumulatedDepreciation = s.AccumulatedDepreciation,
                NetBookValue = s.NetBookValue,
                IsPosted = s.IsPosted,
                PostedDate = s.PostedDate,
                JournalEntryId = s.JournalEntryId,
                IsProjected = s.IsProjected
            })
            .ToListAsync(cancellationToken);

        return schedules;
    }

    private static decimal CalculateDepreciation(FixedAsset asset, decimal remaining)
    {
        if (asset.UsefulLifeMonths <= 0) return 0;

        switch (asset.DepreciationMethod)
        {
            case DepreciationMethod.StraightLine:
                return remaining / asset.UsefulLifeMonths;
            case DepreciationMethod.DecliningBalance:
                return asset.NetBookValue * (1m / asset.UsefulLifeMonths);
            case DepreciationMethod.DoubleDecliningBalance:
                return asset.NetBookValue * (2m / asset.UsefulLifeMonths);
            default:
                throw new InvalidOperationException("Depreciation method not supported in MVP.");
        }
    }

    private async Task<Guid> PostDepreciationToGlAsync(
        FixedAsset asset,
        FiscalPeriod period,
        decimal amount,
        DateTime? postingDate,
        CancellationToken cancellationToken)
    {
        if (asset.Category == null)
        {
            asset.Category = await _context.FixedAssetCategories
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == asset.FixedAssetCategoryId, cancellationToken)
                ?? throw new InvalidOperationException("Fixed asset category not found.");
        }

        var journalNumber = await _journalEntryService.GenerateJournalEntryNumberAsync(cancellationToken);
        var reference = $"DEP-{period.PeriodCode}-{asset.AssetCode}";

        var entry = new CreateJournalEntryDto
        {
            JournalNumber = journalNumber,
            TransactionDate = postingDate ?? period.EndDate,
            Description = $"Depreciation for {asset.AssetCode} - {period.PeriodCode}",
            Reference = reference,
            SourceModule = "FixedAssets",
            FiscalPeriodId = period.Id,
            Transactions = new List<CreateAccountTransactionDto>
            {
                new()
                {
                    AccountId = asset.Category.DepreciationExpenseAccountId,
                    Amount = amount,
                    TransactionType = "Debit",
                    Description = "Depreciation Expense",
                    Reference = reference,
                    LineNumber = 1
                },
                new()
                {
                    AccountId = asset.Category.AccumulatedDepreciationAccountId,
                    Amount = amount,
                    TransactionType = "Credit",
                    Description = "Accumulated Depreciation",
                    Reference = reference,
                    LineNumber = 2
                }
            }
        };

        var created = await _journalEntryService.CreateJournalEntryAsync(entry, cancellationToken);
        var posted = await _journalEntryService.PostJournalEntryAsync(created.Id, cancellationToken);
        return posted.Id;
    }

    private static AssetDepreciationScheduleDto MapSchedule(AssetDepreciationSchedule schedule)
    {
        return new AssetDepreciationScheduleDto
        {
            Id = schedule.Id,
            FixedAssetId = schedule.FixedAssetId,
            FiscalPeriodId = schedule.FiscalPeriodId,
            DepreciationAmount = schedule.DepreciationAmount,
            AccumulatedDepreciation = schedule.AccumulatedDepreciation,
            NetBookValue = schedule.NetBookValue,
            IsPosted = schedule.IsPosted,
            PostedDate = schedule.PostedDate,
            JournalEntryId = schedule.JournalEntryId,
            IsProjected = schedule.IsProjected
        };
    }
}
