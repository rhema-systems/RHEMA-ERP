using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class FixedAssetReportsService : IFixedAssetReportsService
{
    private const string SourceModuleDirectFa = "FA";
    private const string SourceModuleFixedAssets = "FixedAssets";
    private const string SourceDocumentTypeFixedAsset = "FixedAsset";
    private const string SourceDocumentTypeVendorInvoice = "VendorInvoice";
    private const string SourceDocumentTypeDepreciationRun = "FixedAssetDepreciationRun";
    private const string SourceDocumentTypeValuation = "FixedAssetValuation";
    private const string SourceDocumentTypeDisposal = "FixedAssetDisposal";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;

    public FixedAssetReportsService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;

        QuestPDF.Settings.License = LicenseType.Community;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<FixedAssetRegisterDto> GetAssetRegisterAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var requestedBook = NormalizeBookClassification(query.BookClassification);
        var historyQuery = BuildHistoryScopeQuery(query);
        var cutoff = ReportCutoff(query);
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(value => value.AccountingBook)
            .ToListAsync();

        var allSchedules = await BuildDepreciationQuery(historyQuery)
            .Where(schedule => schedule.IsPosted && !schedule.IsDeleted)
            .ToListAsync();
        var allValuations = await BuildValuationQuery(historyQuery)
            .Where(valuation => valuation.IsPostedToGL && !valuation.IsDeleted)
            .ToListAsync();
        var allDisposals = await BuildDisposalQuery(historyQuery)
            .Where(disposal => disposal.Status == AssetDisposalStatus.Completed)
            .ToListAsync();
        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));
        // This is an accounting-book register, not the operational asset-master catalogue.
        // A master without a value for the requested book must not contribute plausible but
        // unposted cost/NBV to Finance reports or reconciliation totals.
        var items = assets
            .Select(asset => new { Asset = asset, BookValue = SelectBookValue(asset, requestedBook) })
            .Where(item =>
                item.BookValue != null &&
                CapitalizationDate(item.Asset, item.BookValue) <= cutoff)
            .Select(item =>
            {
                var a = item.Asset;
                var bookValue = item.BookValue!;
                var cost = InitialAcquisitionCost(a.Id, bookValue, allDisposals);
                var carryingCost = CarryingCostAt(a, bookValue, cutoff, allValuations, allDisposals);
                var accumulatedDepreciation = AccumulatedDepreciationAt(
                    a,
                    bookValue,
                    cutoff,
                    allSchedules,
                    allDisposals);
                var accumulatedImpairment = AccumulatedImpairmentAt(
                    a.Id,
                    cutoff,
                    allValuations,
                    allDisposals);
                var assetCostGl = RoundMoney(glLines
                    .Where(line => line.AccountId == a.Category.AssetAccountId && IsAssetRelated(line, a))
                    .Sum(line => line.DebitAmount - line.CreditAmount));
                var accumulatedDepreciationGl = RoundMoney(glLines
                    .Where(line => line.AccountId == a.Category.AccumulatedDepreciationAccountId && IsAssetRelated(line, a))
                    .Sum(line => line.CreditAmount - line.DebitAmount));

                return new FixedAssetRegisterItemDto
                {
                    Id = a.Id,
                    BookValueId = bookValue.Id,
                    CategoryId = a.FixedAssetCategoryId,
                    AssetCode = a.AssetCode,
                    Name = a.Name,
                    CategoryName = a.Category?.Name ?? "Unknown",
                    AcquisitionDate = a.PurchaseDate,
                    CapitalizationDate = bookValue.CapitalizationDate ?? a.CapitalizationDate,
                    Cost = cost,
                    AccumulatedDepreciation = accumulatedDepreciation,
                    NetBookValue = RoundMoney(carryingCost - accumulatedDepreciation - accumulatedImpairment),
                    BookClassification = bookValue.BookClassification,
                    Status = StatusAt(a, cutoff, allDisposals),
                    SerialNumber = a.SerialNumber,
                    Location = a.Location,
                    CurrentSegmentString = a.CurrentSegmentString,
                    SourceDocumentType = bookValue.SourceDocumentType ?? a.SourceDocumentType,
                    SourceDocumentId = bookValue.SourceDocumentId ?? a.SourceDocumentId,
                    SourceDocumentLineId = bookValue.SourceDocumentLineId ?? a.SourceDocumentLineId,
                    JournalEntryId = bookValue.CapitalizationJournalEntryId ?? a.JournalEntryId,
                    PostingEventId = bookValue.CapitalizationPostingEventId ?? a.PostingEventId,
                    PostedGlCostMovement = assetCostGl,
                    PostedGlAccumulatedDepreciationMovement = accumulatedDepreciationGl,
                    ReconciliationVariance = RoundMoney(assetCostGl - carryingCost),
                    HasPostedGlReference = (bookValue.CapitalizationJournalEntryId ?? a.JournalEntryId).HasValue
                        && (bookValue.CapitalizationPostingEventId ?? a.PostingEventId).HasValue
                };
            }).ToList();

        await RecordReportAuditAsync(
            FinanceAuditEvents.FixedAssetRegisterReportGenerated,
            "FixedAssetRegister",
            new { ItemCount = items.Count, TotalCost = items.Sum(i => i.Cost), requestedBook });

        return new FixedAssetRegisterDto
        {
            Items = items,
            TotalCost = items.Sum(i => i.Cost),
            TotalAccumulatedDepreciation = items.Sum(i => i.AccumulatedDepreciation),
            TotalNetBookValue = items.Sum(i => i.NetBookValue)
        };
    }

    public async Task<FixedAssetAdditionsReportDto> GetAdditionsReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var requestedBook = NormalizeBookClassification(query.BookClassification);
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .Where(a => a.CapitalizationDate.HasValue || a.PostingEventId.HasValue)
            .ToListAsync();

        assets = assets
            .Where(a =>
                SelectBookValue(a, requestedBook) != null &&
                DateInRange(
                    SelectBookValue(a, requestedBook)!.CapitalizationDate ??
                    a.CapitalizationDate ??
                    a.CapitalizedAt ??
                    a.PurchaseDate,
                    query))
            .ToList();

        var glLines = await LoadPostedFixedAssetGlLinesAsync(query);
        var items = assets.Select(a =>
        {
            var bookValue = SelectBookValue(a, requestedBook)!;
            var cost = RoundMoney(bookValue.AcquisitionCost);
            var postedCost = RoundMoney(glLines
                .Where(line =>
                    line.AccountId == a.Category.AssetAccountId &&
                    IsAssetRelated(line, a) &&
                    string.Equals(line.TransactionTag, "FA-Capitalization", StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.DebitAmount - line.CreditAmount));
            var sourceDocumentType = bookValue.SourceDocumentType ?? a.SourceDocumentType;

            return new FixedAssetAdditionReportItemDto
            {
                AssetId = a.Id,
                AssetCode = a.AssetCode,
                Name = a.Name,
                CategoryId = a.FixedAssetCategoryId,
                CategoryName = a.Category?.Name ?? "Unknown",
                CapitalizationDate = bookValue.CapitalizationDate ?? a.CapitalizationDate,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = bookValue.SourceDocumentId ?? a.SourceDocumentId,
                SourceDocumentLineId = bookValue.SourceDocumentLineId ?? a.SourceDocumentLineId,
                JournalEntryId = bookValue.CapitalizationJournalEntryId ?? a.JournalEntryId,
                PostingEventId = bookValue.CapitalizationPostingEventId ?? a.PostingEventId,
                FunctionalCurrencyCode = a.FunctionalCurrencyCode,
                TransactionCurrencyCode = a.TransactionCurrencyCode,
                CapitalizedCost = cost,
                PostedGlCost = postedCost,
                Variance = RoundMoney(postedCost - cost),
                IsApSourced = string.Equals(sourceDocumentType, SourceDocumentTypeVendorInvoice, StringComparison.OrdinalIgnoreCase),
                IsDirectCapitalization = string.Equals(sourceDocumentType, SourceDocumentTypeFixedAsset, StringComparison.OrdinalIgnoreCase),
                MissingPostingReference = !(bookValue.CapitalizationJournalEntryId ?? a.JournalEntryId).HasValue
                    || !(bookValue.CapitalizationPostingEventId ?? a.PostingEventId).HasValue
            };
        }).ToList();

        return new FixedAssetAdditionsReportDto
        {
            Items = items,
            TotalCapitalizedCost = items.Sum(i => i.CapitalizedCost),
            TotalPostedGlCost = items.Sum(i => i.PostedGlCost),
            TotalVariance = RoundMoney(items.Sum(i => i.Variance))
        };
    }

    public async Task<FixedAssetDepreciationReportDto> GetDepreciationReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var schedules = await BuildDepreciationQuery(query)
            .Include(s => s.FixedAsset)
            .Include(s => s.DepreciationRun)
            .Where(s => s.IsPosted && !s.IsDeleted)
            .ToListAsync();

        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));
        var items = schedules.Select(s =>
        {
            var scheduleLines = glLines
                .Where(line => IsDepreciationLineRelated(line, s))
                .ToList();
            var postedExpense = RoundMoney(scheduleLines
                .Where(line => string.Equals(line.TransactionTag, "FA-Depreciation", StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.DebitAmount - line.CreditAmount));
            var postedAccumulatedDepreciation = RoundMoney(scheduleLines
                .Where(line => string.Equals(line.TransactionTag, "FA-AccumulatedDepreciation", StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.CreditAmount - line.DebitAmount));

            return new FixedAssetDepreciationReportItemDto
            {
                ScheduleId = s.Id,
                FixedAssetId = s.FixedAssetId,
                AssetCode = s.FixedAsset?.AssetCode ?? string.Empty,
                AssetName = s.FixedAsset?.Name ?? string.Empty,
                DepreciationRunId = s.FixedAssetDepreciationRunId,
                FiscalPeriodId = s.FiscalPeriodId,
                BookClassification = s.BookClassification,
                PostingDate = s.PostingDate,
                DepreciationAmount = s.DepreciationAmount,
                AccumulatedDepreciation = s.AccumulatedDepreciation,
                NetBookValueBefore = s.NetBookValueBefore,
                NetBookValue = s.NetBookValue,
                JournalEntryId = s.JournalEntryId,
                PostingEventId = s.PostingEventId,
                PostedExpenseDebit = postedExpense,
                PostedAccumulatedDepreciationCredit = postedAccumulatedDepreciation,
                Variance = RoundMoney(postedExpense - s.DepreciationAmount),
                HasPostedGlReference = s.JournalEntryId.HasValue && s.PostingEventId.HasValue
            };
        }).ToList();

        return new FixedAssetDepreciationReportDto
        {
            Items = items,
            TotalDepreciation = items.Sum(i => i.DepreciationAmount),
            TotalPostedExpense = items.Sum(i => i.PostedExpenseDebit),
            TotalPostedAccumulatedDepreciation = items.Sum(i => i.PostedAccumulatedDepreciationCredit),
            TotalVariance = RoundMoney(items.Sum(i => i.Variance))
        };
    }

    public async Task<FixedAssetAccumulatedDepreciationReportDto> GetAccumulatedDepreciationReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var requestedBook = NormalizeBookClassification(query.BookClassification);
        var historyQuery = BuildHistoryScopeQuery(query);
        var cutoff = ReportCutoff(query);
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .ToListAsync();
        assets = assets
            .Where(asset => SelectBookValue(asset, requestedBook) != null)
            .ToList();
        var allSchedules = await BuildDepreciationQuery(historyQuery)
            .Where(schedule => schedule.IsPosted && !schedule.IsDeleted)
            .ToListAsync();
        var allDisposals = await BuildDisposalQuery(historyQuery)
            .Where(disposal => disposal.Status == AssetDisposalStatus.Completed)
            .ToListAsync();
        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));

        var items = assets.Select(a =>
        {
            var bookValue = SelectBookValue(a, requestedBook)!;
            var subledgerAccumulated = AccumulatedDepreciationAt(
                a,
                bookValue,
                cutoff,
                allSchedules,
                allDisposals);
            var postedGlAccumulated = RoundMoney(glLines
                .Where(line => line.AccountId == a.Category.AccumulatedDepreciationAccountId && IsAssetRelated(line, a))
                .Sum(line => line.CreditAmount - line.DebitAmount));

            return new FixedAssetAccumulatedDepreciationReportItemDto
            {
                FixedAssetId = a.Id,
                AssetCode = a.AssetCode,
                AssetName = a.Name,
                BookClassification = bookValue.BookClassification,
                SubledgerAccumulatedDepreciation = subledgerAccumulated,
                PostedGlAccumulatedDepreciation = postedGlAccumulated,
                Variance = RoundMoney(postedGlAccumulated - subledgerAccumulated)
            };
        }).ToList();

        return new FixedAssetAccumulatedDepreciationReportDto
        {
            Items = items,
            TotalSubledgerAccumulatedDepreciation = items.Sum(i => i.SubledgerAccumulatedDepreciation),
            TotalPostedGlAccumulatedDepreciation = items.Sum(i => i.PostedGlAccumulatedDepreciation),
            TotalVariance = RoundMoney(items.Sum(i => i.Variance))
        };
    }

    public async Task<FixedAssetValuationMovementReportDto> GetValuationMovementReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var valuations = await BuildValuationQuery(query)
            .Include(v => v.FixedAsset)
            .Where(v => v.IsPostedToGL && !v.IsDeleted)
            .ToListAsync();
        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));

        var items = valuations.Select(v =>
        {
            var postedMovement = RoundMoney(glLines
                .Where(line => IsValuationLineRelated(line, v))
                .Sum(line => Math.Abs(line.DebitAmount - line.CreditAmount)) / 2m);

            return new FixedAssetValuationMovementReportItemDto
            {
                ValuationId = v.Id,
                FixedAssetId = v.FixedAssetId,
                AssetCode = v.FixedAsset?.AssetCode ?? string.Empty,
                AssetName = v.FixedAsset?.Name ?? string.Empty,
                ValuationType = v.ValuationType,
                ValuationDate = v.ValuationDate,
                AccountingDate = v.AccountingDate,
                BookClassification = v.BookClassification,
                CarryingAmountBefore = v.CarryingAmountBefore,
                CarryingAmountAfter = v.CarryingAmountAfter,
                RevaluationSurplus = v.RevaluationSurplus,
                RevaluationDeficit = v.RevaluationDeficit,
                ImpairmentLoss = v.ImpairmentLoss,
                ImpairmentReversal = v.ImpairmentReversal,
                JournalEntryId = v.JournalEntryId,
                PostingEventId = v.PostingEventId,
                PostedGlMovement = postedMovement,
                HasPostedGlReference = v.JournalEntryId.HasValue && v.PostingEventId.HasValue
            };
        }).ToList();

        return new FixedAssetValuationMovementReportDto
        {
            Items = items,
            TotalRevaluationIncrease = items.Sum(i => i.RevaluationSurplus),
            TotalRevaluationDecrease = items.Sum(i => i.RevaluationDeficit),
            TotalImpairmentLoss = items.Sum(i => i.ImpairmentLoss),
            TotalImpairmentReversal = items.Sum(i => i.ImpairmentReversal),
            TotalPostedGlMovement = items.Sum(i => i.PostedGlMovement)
        };
    }

    public async Task<List<AssetDisposalReportDto>> GetDisposalReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var disposals = await BuildDisposalQuery(query)
            .Include(d => d.FixedAsset)
                .ThenInclude(a => a.Category)
                    .ThenInclude(c => c.GainOnDisposalAccount)
            .Where(d => d.Status == AssetDisposalStatus.Completed)
            .ToListAsync();

        return disposals.Select(d =>
        {
            var gainAccount = d.FixedAsset?.Category?.GainOnDisposalAccount;
            var warning = BuildDisposalGainPresentationWarning(d, gainAccount);

            return new AssetDisposalReportDto
            {
                Id = d.Id,
                FixedAssetId = d.FixedAssetId,
                AssetCode = d.FixedAsset?.AssetCode ?? "N/A",
                Name = d.FixedAsset?.Name ?? "Unknown",
                DisposalDate = d.DisposalDate,
                DisposalType = d.DisposalType,
                DisposalScope = d.DisposalScope,
                DisposedPortionPercent = d.DisposedPortionPercent,
                ComponentReference = d.ComponentReference,
                AllocationEvidenceReference = d.AllocationEvidenceReference,
                SaleProceeds = d.SaleProceeds,
                DisposalCost = d.DisposalCost,
                NetProceeds = d.NetProceeds,
                ProceedsCurrencyCode = d.ProceedsCurrencyCode,
                ProceedsFunctionalAmount = d.ProceedsFunctionalAmount,
                ProceedsExchangeRateId = d.ProceedsExchangeRateId,
                ProceedsExchangeRateValue = d.ProceedsExchangeRateValue,
                ProceedsExchangeRateSource = d.ProceedsExchangeRateSource,
                ProceedsExchangeRateDate = d.ProceedsExchangeRateDate,
                CostAtDisposal = d.CostAtDisposal,
                AccumulatedDepreciationAtDisposal = d.AccumulatedDepreciationAtDisposal,
                AccumulatedImpairmentAtDisposal = d.AccumulatedImpairmentAtDisposal,
                RevaluationSurplusAtDisposal = d.RevaluationSurplusAtDisposal,
                RevaluationSurplusTransferAmount = d.RevaluationSurplusTransferAmount,
                NetBookValue = d.NetBookValueAtDisposal,
                GainLoss = d.GainOrLoss,
                RemainingNetBookValue = d.RemainingNetBookValueAfterDisposal,
                BuyerName = d.BuyerName,
                JournalEntryId = d.JournalEntryId,
                PostingEventId = d.PostingEventId,
                HasPostedGlReference = d.JournalEntryId.HasValue && d.PostingEventId.HasValue,
                GainLossPresentation = d.GainOrLoss > 0m
                    ? "Disposal gain - non-operating presentation"
                    : d.GainOrLoss < 0m
                        ? "Disposal loss"
                        : "No gain/loss",
                PresentationWarning = warning
            };
        }).ToList();
    }

    public async Task<List<AssetTransferReportDto>> GetTransferReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var transfers = await BuildTransferQuery(query)
            .Include(t => t.FixedAsset)
            .Where(t => t.Status == AssetTransferStatus.Completed)
            .ToListAsync();

        return transfers.Select(t => new AssetTransferReportDto
        {
            Id = t.Id,
            FixedAssetId = t.FixedAssetId,
            AssetCode = t.FixedAsset?.AssetCode ?? "N/A",
            Name = t.FixedAsset?.Name ?? "Unknown",
            TransferDate = t.TransferDate,
            TransferType = t.TransferType,
            Status = t.Status,
            FromLocation = t.FromLocation,
            ToLocation = t.ToLocation,
            FromDepartment = null,
            ToDepartment = null,
            FromSegmentString = t.FromSegmentString,
            ToSegmentString = t.ToSegmentString,
            Reason = t.Reason,
            JournalEntryId = t.JournalEntryId,
            PostingEventId = t.PostingEventId,
            HasGlImpact = t.JournalEntryId.HasValue || t.PostingEventId.HasValue,
            HasPostedGlReference = t.JournalEntryId.HasValue && t.PostingEventId.HasValue
        }).ToList();
    }

    public async Task<FixedAssetRollForwardReportDto> GetRollForwardReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var requestedBook = NormalizeBookClassification(query.BookClassification);
        var historyQuery = BuildHistoryScopeQuery(query);
        var periodEnd = ReportCutoff(query);
        var periodStart = query.FromDate?.Date ?? DateTime.MinValue.Date;
        var openingCutoff = query.FromDate.HasValue
            ? query.FromDate.Value.Date.AddTicks(-1)
            : DateTime.MinValue;
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .ToListAsync();
        assets = assets
            .Where(asset =>
                SelectBookValue(asset, requestedBook) != null &&
                CapitalizationDate(asset, SelectBookValue(asset, requestedBook)!) <= periodEnd)
            .ToList();
        var allSchedules = await BuildDepreciationQuery(historyQuery)
            .Where(schedule => schedule.IsPosted && !schedule.IsDeleted)
            .ToListAsync();
        var allValuations = await BuildValuationQuery(historyQuery)
            .Where(valuation => valuation.IsPostedToGL && !valuation.IsDeleted)
            .ToListAsync();
        var allDisposals = await BuildDisposalQuery(historyQuery)
            .Where(disposal => disposal.Status == AssetDisposalStatus.Completed)
            .ToListAsync();
        var schedules = allSchedules.Where(schedule => DateInRange(ScheduleDate(schedule), query)).ToList();
        var valuations = allValuations.Where(valuation => DateInRange(ValuationDate(valuation), query)).ToList();
        var disposals = allDisposals.Where(disposal => DateInRange(DisposalDate(disposal), query)).ToList();

        var rows = assets
            .GroupBy(a => new
            {
                a.FixedAssetCategoryId,
                CategoryName = a.Category?.Name ?? "Unknown",
                Book = SelectBookValue(a, requestedBook)!.BookClassification
            })
            .Select(group =>
            {
                var assetIds = group.Select(a => a.Id).ToHashSet();
                var openingCost = RoundMoney(group.Sum(asset => CarryingCostAt(
                    asset,
                    SelectBookValue(asset, requestedBook)!,
                    openingCutoff,
                    allValuations,
                    allDisposals)));
                var openingAccumulatedDepreciation = RoundMoney(group.Sum(asset => AccumulatedDepreciationAt(
                    asset,
                    SelectBookValue(asset, requestedBook)!,
                    openingCutoff,
                    allSchedules,
                    allDisposals)));
                var openingAccumulatedImpairment = RoundMoney(group.Sum(asset => AccumulatedImpairmentAt(
                    asset.Id,
                    openingCutoff,
                    allValuations,
                    allDisposals)));
                var additions = RoundMoney(group.Sum(a =>
                {
                    var book = SelectBookValue(a, requestedBook)!;
                    var capDate = CapitalizationDate(a, book);
                    return capDate >= periodStart && capDate <= periodEnd
                        ? InitialAcquisitionCost(a.Id, book, allDisposals)
                        : 0m;
                }));
                var groupValuations = valuations.Where(v => assetIds.Contains(v.FixedAssetId)).ToList();
                var groupSchedules = schedules.Where(s => assetIds.Contains(s.FixedAssetId)).ToList();
                var groupDisposals = disposals.Where(d => assetIds.Contains(d.FixedAssetId)).ToList();

                var row = new FixedAssetRollForwardRowDto
                {
                    CategoryId = group.Key.FixedAssetCategoryId,
                    CategoryName = group.Key.CategoryName,
                    BookClassification = group.Key.Book,
                    AssetCount = group.Count(asset => IsOnRegisterAt(asset, periodEnd, allDisposals)),
                    OpeningCost = openingCost,
                    OpeningAccumulatedDepreciation = openingAccumulatedDepreciation,
                    OpeningAccumulatedImpairment = openingAccumulatedImpairment,
                    Additions = additions,
                    RevaluationIncrease = RoundMoney(groupValuations.Sum(v => v.RevaluationSurplus)),
                    RevaluationDecrease = RoundMoney(groupValuations.Sum(v => v.RevaluationDeficit)),
                    ImpairmentAdditions = RoundMoney(groupValuations.Sum(v => v.ImpairmentLoss)),
                    ImpairmentReversals = RoundMoney(groupValuations.Sum(v => v.ImpairmentReversal)),
                    DepreciationCharge = RoundMoney(groupSchedules.Sum(s => s.DepreciationAmount)),
                    AccumulatedDepreciationMovement = RoundMoney(groupSchedules.Sum(s => s.DepreciationAmount)),
                    Disposals = RoundMoney(groupDisposals.Sum(d => d.CostAtDisposal)),
                    AccumulatedDepreciationCleared = RoundMoney(groupDisposals.Sum(d => d.AccumulatedDepreciationAtDisposal)),
                    AccumulatedImpairmentCleared = RoundMoney(groupDisposals.Sum(d => d.AccumulatedImpairmentAtDisposal))
                };

                row.ClosingCost = RoundMoney(row.OpeningCost + row.Additions + row.RevaluationIncrease - row.RevaluationDecrease - row.Disposals);
                row.ClosingAccumulatedDepreciation = RoundMoney(row.OpeningAccumulatedDepreciation + row.AccumulatedDepreciationMovement - row.AccumulatedDepreciationCleared);
                row.ClosingAccumulatedImpairment = RoundMoney(row.OpeningAccumulatedImpairment + row.ImpairmentAdditions - row.ImpairmentReversals - row.AccumulatedImpairmentCleared);
                row.ClosingNetBookValue = RoundMoney(row.ClosingCost - row.ClosingAccumulatedDepreciation - row.ClosingAccumulatedImpairment);
                return row;
            })
            .ToList();

        var totals = new FixedAssetRollForwardRowDto
        {
            CategoryName = "Total",
            BookClassification = requestedBook ?? "ALL",
            AssetCount = rows.Sum(r => r.AssetCount),
            OpeningCost = rows.Sum(r => r.OpeningCost),
            OpeningAccumulatedDepreciation = rows.Sum(r => r.OpeningAccumulatedDepreciation),
            OpeningAccumulatedImpairment = rows.Sum(r => r.OpeningAccumulatedImpairment),
            Additions = rows.Sum(r => r.Additions),
            RevaluationIncrease = rows.Sum(r => r.RevaluationIncrease),
            RevaluationDecrease = rows.Sum(r => r.RevaluationDecrease),
            ImpairmentAdditions = rows.Sum(r => r.ImpairmentAdditions),
            ImpairmentReversals = rows.Sum(r => r.ImpairmentReversals),
            DepreciationCharge = rows.Sum(r => r.DepreciationCharge),
            AccumulatedDepreciationMovement = rows.Sum(r => r.AccumulatedDepreciationMovement),
            Disposals = rows.Sum(r => r.Disposals),
            AccumulatedDepreciationCleared = rows.Sum(r => r.AccumulatedDepreciationCleared),
            AccumulatedImpairmentCleared = rows.Sum(r => r.AccumulatedImpairmentCleared),
            ClosingCost = rows.Sum(r => r.ClosingCost),
            ClosingAccumulatedDepreciation = rows.Sum(r => r.ClosingAccumulatedDepreciation),
            ClosingAccumulatedImpairment = rows.Sum(r => r.ClosingAccumulatedImpairment),
            ClosingNetBookValue = rows.Sum(r => r.ClosingNetBookValue)
        };

        await RecordReportAuditAsync(
            FinanceAuditEvents.FixedAssetRollForwardReportGenerated,
            "FixedAssetRollForward",
            new { RowCount = rows.Count, totals.ClosingNetBookValue });

        return new FixedAssetRollForwardReportDto
        {
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            Rows = rows,
            Totals = totals
        };
    }

    public async Task<FixedAssetGlReconciliationReportDto> GetGlReconciliationReportAsync(FixedAssetReportQueryDto query)
    {
        await ValidateFiltersAsync(query);
        var requestedBook = NormalizeBookClassification(query.BookClassification);
        var historyQuery = BuildHistoryScopeQuery(query);
        var cutoff = ReportCutoff(query);
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .ToListAsync();
        assets = assets
            .Where(asset =>
                SelectBookValue(asset, requestedBook) != null &&
                CapitalizationDate(asset, SelectBookValue(asset, requestedBook)!) <= cutoff)
            .ToList();
        var categories = assets
            .Select(a => a.Category)
            .Where(c => c != null)
            .DistinctBy(c => c!.Id)
            .Select(c => c!)
            .ToList();
        var allSchedules = await BuildDepreciationQuery(historyQuery)
            .Where(schedule => schedule.IsPosted && !schedule.IsDeleted)
            .ToListAsync();
        var allValuations = await BuildValuationQuery(historyQuery)
            .Where(valuation => valuation.IsPostedToGL && !valuation.IsDeleted)
            .ToListAsync();
        var allDisposals = await BuildDisposalQuery(historyQuery)
            .Where(disposal => disposal.Status == AssetDisposalStatus.Completed)
            .ToListAsync();
        var schedules = allSchedules.Where(schedule => ScheduleDate(schedule) <= cutoff).ToList();
        var valuations = allValuations.Where(valuation => ValuationDate(valuation) <= cutoff).ToList();
        var disposals = allDisposals.Where(disposal => DisposalDate(disposal) <= cutoff).ToList();
        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));
        var accounts = await _context.Accounts
            .Where(a => a.TenantId == TenantId)
            .ToDictionaryAsync(a => a.Id);
        var diagnostics = new List<FixedAssetReportingDiagnosticDto>();
        var rows = new List<FixedAssetGlReconciliationRowDto>();

        foreach (var category in categories)
        {
            AddReconciliationRow(rows, diagnostics, "Asset Cost/Carrying", category.AssetAccountId, accounts, glLines,
                SubledgerAssetCarryingBalance(
                    assets,
                    allValuations,
                    allDisposals,
                    category.Id,
                    requestedBook,
                    cutoff),
                assets.Count(a => a.FixedAssetCategoryId == category.Id),
                MissingCapitalizationReferences(assets, category.Id),
                SubledgerWithoutGlCapitalizations(assets, glLines, category.Id),
                BalanceConvention.DebitMinusCredit);

            AddReconciliationRow(rows, diagnostics, "Accumulated Depreciation", category.AccumulatedDepreciationAccountId, accounts, glLines,
                RoundMoney(assets
                    .Where(asset => asset.FixedAssetCategoryId == category.Id)
                    .Sum(asset => AccumulatedDepreciationAt(
                        asset,
                        SelectBookValue(asset, requestedBook)!,
                        cutoff,
                        allSchedules,
                        allDisposals))),
                schedules.Count(s => assets.Any(a => a.Id == s.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                schedules.Count(s => assets.Any(a => a.Id == s.FixedAssetId && a.FixedAssetCategoryId == category.Id) && (!s.JournalEntryId.HasValue || !s.PostingEventId.HasValue)),
                schedules.Count(s => assets.Any(a => a.Id == s.FixedAssetId && a.FixedAssetCategoryId == category.Id) && !HasMatchingDepreciationGlLine(glLines, s)),
                BalanceConvention.CreditMinusDebit);

            AddReconciliationRow(rows, diagnostics, "Depreciation Expense", category.DepreciationExpenseAccountId, accounts, glLines,
                RoundMoney(schedules.Where(s => assets.Any(a => a.Id == s.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(s => s.DepreciationAmount)),
                schedules.Count(s => assets.Any(a => a.Id == s.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                0,
                schedules.Count(s => assets.Any(a => a.Id == s.FixedAssetId && a.FixedAssetCategoryId == category.Id) && !HasMatchingDepreciationGlLine(glLines, s)),
                BalanceConvention.DebitMinusCredit);

            if (category.AccumulatedImpairmentAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Accumulated Impairment", category.AccumulatedImpairmentAccountId.Value, accounts, glLines,
                    RoundMoney(valuations.Where(v => assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(v => v.ImpairmentLoss - v.ImpairmentReversal)
                        - disposals.Where(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => d.AccumulatedImpairmentAtDisposal)),
                    valuations.Count(v => (v.ValuationType == ValuationType.Impairment || v.ValuationType == ValuationType.ImpairmentReversal) && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    valuations.Count(v => (v.ValuationType == ValuationType.Impairment || v.ValuationType == ValuationType.ImpairmentReversal) && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id) && (!v.JournalEntryId.HasValue || !v.PostingEventId.HasValue)),
                    0,
                    BalanceConvention.CreditMinusDebit);
            }

            if (category.RevaluationSurplusAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Revaluation Surplus", category.RevaluationSurplusAccountId.Value, accounts, glLines,
                    // Completed disposal transfers reduce the asset-specific reserve directly in
                    // equity. Subtract the retained transfer evidence so subledger reconciliation
                    // follows the same balance as the posted revaluation-surplus account.
                    RoundMoney(valuations.Where(v => assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(v => v.RevaluationSurplus - v.RevaluationSurplusApplied)
                        - disposals.Where(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => d.RevaluationSurplusTransferAmount)),
                    valuations.Count(v => v.ValuationType == ValuationType.Revaluation && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    0,
                    0,
                    BalanceConvention.CreditMinusDebit);
            }

            if (category.RevaluationLossAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Revaluation Loss", category.RevaluationLossAccountId.Value, accounts, glLines,
                    RoundMoney(valuations.Where(v => assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(v => v.RevaluationLossRecognized)),
                    valuations.Count(v => v.RevaluationLossRecognized > 0m && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    0,
                    0,
                    BalanceConvention.DebitMinusCredit);
            }

            if (category.ImpairmentLossAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Impairment Loss", category.ImpairmentLossAccountId.Value, accounts, glLines,
                    RoundMoney(valuations.Where(v => v.ValuationType == ValuationType.Impairment && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(v => v.ImpairmentLoss)),
                    valuations.Count(v => v.ValuationType == ValuationType.Impairment && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    0,
                    0,
                    BalanceConvention.DebitMinusCredit);
            }

            if (category.ImpairmentReversalAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Impairment Reversal", category.ImpairmentReversalAccountId.Value, accounts, glLines,
                    RoundMoney(valuations.Where(v => v.ValuationType == ValuationType.ImpairmentReversal && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(v => v.ImpairmentReversal)),
                    valuations.Count(v => v.ValuationType == ValuationType.ImpairmentReversal && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    0,
                    0,
                    BalanceConvention.CreditMinusDebit);
            }

            if (category.DisposalProceedsClearingAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Disposal Proceeds Clearing", category.DisposalProceedsClearingAccountId.Value, accounts, glLines,
                    // GL reconciliation must aggregate functional values; adding USD and GHS native
                    // amounts would produce a plausible-looking but meaningless control total.
                    RoundMoney(disposals.Where(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => d.ProceedsFunctionalAmount)),
                    disposals.Count(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id) && d.ProceedsFunctionalAmount > 0m),
                    0,
                    0,
                    BalanceConvention.DebitMinusCredit);
            }

            if (category.GainOnDisposalAccountId.HasValue)
            {
                var presentationWarning = BuildDisposalGainPresentationWarning(
                    disposals.FirstOrDefault(d => d.GainOrLoss > 0m && assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    accounts.GetValueOrDefault(category.GainOnDisposalAccountId.Value));
                AddReconciliationRow(rows, diagnostics, "Disposal Gain", category.GainOnDisposalAccountId.Value, accounts, glLines,
                    RoundMoney(disposals.Where(d => d.GainOrLoss > 0m && assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => d.GainOrLoss)),
                    disposals.Count(d => d.GainOrLoss > 0m && assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    0,
                    0,
                    BalanceConvention.CreditMinusDebit,
                    presentationWarning);
            }

            if (category.LossOnDisposalAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Disposal Loss", category.LossOnDisposalAccountId.Value, accounts, glLines,
                    RoundMoney(disposals.Where(d => d.GainOrLoss < 0m && assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => Math.Abs(d.GainOrLoss))),
                    disposals.Count(d => d.GainOrLoss < 0m && assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    0,
                    0,
                    BalanceConvention.DebitMinusCredit);
            }
        }

        AddSourceReferenceDiagnostics(diagnostics, assets, schedules, valuations, disposals, glLines);

        if (diagnostics.Any(d => d.Severity == "Critical" || d.Code.Contains("Variance", StringComparison.OrdinalIgnoreCase)))
        {
            await RecordReportAuditAsync(
                FinanceAuditEvents.FixedAssetVarianceDiagnosticGenerated,
                "FixedAssetVarianceDiagnostics",
                new { DiagnosticCount = diagnostics.Count });
        }

        await RecordReportAuditAsync(
            FinanceAuditEvents.FixedAssetGlReconciliationReportGenerated,
            "FixedAssetGlReconciliation",
            new { RowCount = rows.Count, DiagnosticCount = diagnostics.Count });

        return new FixedAssetGlReconciliationReportDto
        {
            AsOfDate = ReportCutoff(query),
            Rows = rows,
            Diagnostics = diagnostics,
            TotalGlBalance = rows.Sum(r => r.GlBalance),
            TotalSubledgerBalance = rows.Sum(r => r.SubledgerBalance),
            TotalVariance = RoundMoney(rows.Sum(r => r.Variance))
        };
    }

    public async Task<VerificationSummaryDto> GetVerificationSummaryAsync(Guid sessionId)
    {
        var session = await _context.AssetVerificationSessions
            .Include(s => s.Items)
            .ThenInclude(i => i.FixedAsset)
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        var conditionSummary = session.Items
            .Where(i => i.IsVerified)
            .GroupBy(i => i.Condition)
            .Select(g => new AssetConditionSummaryDto
            {
                Condition = g.Key,
                Count = g.Count(),
                Percentage = session.Items.Count > 0 ? (decimal)g.Count() / session.Items.Count * 100 : 0
            }).ToList();

        return new VerificationSummaryDto
        {
            SessionId = session.Id,
            SessionName = session.SessionName,
            StartDate = session.ScheduledDate,
            EndDate = session.CompletionDate,
            TotalItems = session.Items.Count,
            VerifiedItems = session.Items.Count(i => i.IsVerified),
            MissingItems = session.Items.Count(i => i.Condition == AssetCondition.Missing),
            ConditionSummary = conditionSummary,
            DetailedItems = session.Items.Select(i => new AssetVerificationItemDto
            {
                Id = i.Id,
                FixedAssetId = i.FixedAssetId,
                AssetCode = i.FixedAsset?.AssetCode,
                FixedAssetName = i.FixedAsset?.Name,
                IsVerified = i.IsVerified,
                Condition = i.Condition,
                VerificationDate = i.VerificationDate,
                CurrentLocation = i.CurrentLocation ?? i.FixedAsset?.Location,
                Notes = i.Notes
            }).ToList()
        };
    }

    public async Task<byte[]> ExportToExcelAsync(string reportType, FixedAssetReportQueryDto query)
    {
        var export = await BuildExportTableAsync(reportType, query);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(export.WorksheetName);

        for (var column = 0; column < export.Headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = export.Headers[column];
        }

        var header = worksheet.Range(1, 1, 1, export.Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        header.Style.Alignment.WrapText = true;

        for (var row = 0; row < export.Rows.Count; row++)
        {
            for (var column = 0; column < export.Headers.Length; column++)
            {
                SetExcelCellValue(worksheet.Cell(row + 2, column + 1), export.Rows[row][column]);
            }
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        worksheet.ColumnsUsed().AdjustToContents(8, 45);
        await RecordReportAuditAsync(
            FinanceAuditEvents.FixedAssetReportExported,
            export.WorksheetName,
            new { Format = "Excel", RowCount = export.Rows.Count, ReportType = reportType });

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportToPdfAsync(string reportType, FixedAssetReportQueryDto query)
    {
        var export = await BuildExportTableAsync(reportType, query);
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(export.Headers.Length > 9 ? PageSizes.A3.Landscape() : PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(export.Headers.Length > 9 ? 6 : 8));
                page.Header().Text(export.Title.ToUpperInvariant()).FontSize(18).Bold().FontColor(Colors.Blue.Darken2);

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in export.Headers)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var heading in export.Headers)
                        {
                            header.Cell().Element(HeaderCellStyle).Text(heading);
                        }
                    });

                    foreach (var row in export.Rows)
                    {
                        foreach (var value in row)
                        {
                            table.Cell().Element(ValueCellStyle).Text(FormatExportValue(value));
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();

        await RecordReportAuditAsync(
            FinanceAuditEvents.FixedAssetReportExported,
            export.WorksheetName,
            new { Format = "PDF", RowCount = export.Rows.Count, ReportType = reportType });
        return bytes;

        static IContainer HeaderCellStyle(IContainer container)
            => container.DefaultTextStyle(style => style.Bold()).Padding(3).Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Black);

        static IContainer ValueCellStyle(IContainer container)
            => container.Padding(3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
    }

    private async Task<FixedAssetExportTable> BuildExportTableAsync(
        string reportType,
        FixedAssetReportQueryDto query)
    {
        var normalized = (reportType ?? string.Empty)
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        switch (normalized)
        {
            case "ASSETREGISTER":
            case "FIXEDASSETREGISTER":
            {
                var report = await GetAssetRegisterAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Register",
                    "Asset Register",
                    ["Asset Code", "Name", "Category", "Book", "Location", "Capitalization Date", "Cost", "Accumulated Depreciation", "NBV", "Status", "GL Variance"],
                    report.Items.Select(item => new object?[]
                    {
                        item.AssetCode, item.Name, item.CategoryName, item.BookClassification, item.Location,
                        item.CapitalizationDate, item.Cost, item.AccumulatedDepreciation, item.NetBookValue,
                        item.Status, item.ReconciliationVariance
                    }).ToList());
            }
            case "ADDITIONSREPORT":
            case "ADDITIONS":
            {
                var report = await GetAdditionsReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Additions",
                    "Additions",
                    ["Asset Code", "Name", "Category", "Capitalization Date", "Source", "Currency", "Capitalized Cost", "Posted GL Cost", "Variance", "Missing Reference"],
                    report.Items.Select(item => new object?[]
                    {
                        item.AssetCode, item.Name, item.CategoryName, item.CapitalizationDate,
                        item.SourceDocumentType, item.FunctionalCurrencyCode, item.CapitalizedCost,
                        item.PostedGlCost, item.Variance, item.MissingPostingReference
                    }).ToList());
            }
            case "DEPRECIATIONREPORT":
            case "DEPRECIATION":
            {
                var report = await GetDepreciationReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Depreciation",
                    "Depreciation",
                    ["Asset Code", "Asset", "Book", "Posting Date", "Depreciation", "Accumulated Depreciation", "NBV", "Posted Expense", "Posted Reserve", "Variance"],
                    report.Items.Select(item => new object?[]
                    {
                        item.AssetCode, item.AssetName, item.BookClassification, item.PostingDate,
                        item.DepreciationAmount, item.AccumulatedDepreciation, item.NetBookValue,
                        item.PostedExpenseDebit, item.PostedAccumulatedDepreciationCredit, item.Variance
                    }).ToList());
            }
            case "ACCUMULATEDDEPRECIATIONREPORT":
            case "ACCUMULATEDDEPRECIATION":
            {
                var report = await GetAccumulatedDepreciationReportAsync(query);
                return new FixedAssetExportTable(
                    "Accumulated Depreciation Reconciliation",
                    "Accumulated Depreciation",
                    ["Asset Code", "Asset", "Book", "Subledger Reserve", "Posted GL Reserve", "Variance"],
                    report.Items.Select(item => new object?[]
                    {
                        item.AssetCode, item.AssetName, item.BookClassification,
                        item.SubledgerAccumulatedDepreciation, item.PostedGlAccumulatedDepreciation,
                        item.Variance
                    }).ToList());
            }
            case "VALUATIONREPORT":
            case "VALUATIONSREPORT":
            case "VALUATIONS":
            {
                var report = await GetValuationMovementReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Valuation Movements",
                    "Valuations",
                    ["Asset Code", "Asset", "Type", "Accounting Date", "Book", "Before", "After", "Revaluation Increase", "Revaluation Decrease", "Impairment", "Impairment Reversal", "Posted GL Movement"],
                    report.Items.Select(item => new object?[]
                    {
                        item.AssetCode, item.AssetName, item.ValuationType, item.AccountingDate,
                        item.BookClassification, item.CarryingAmountBefore, item.CarryingAmountAfter,
                        item.RevaluationSurplus, item.RevaluationDeficit, item.ImpairmentLoss,
                        item.ImpairmentReversal, item.PostedGlMovement
                    }).ToList());
            }
            case "DISPOSALREPORT":
            case "DISPOSALS":
            {
                var report = await GetDisposalReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Disposals",
                    "Disposals",
                    ["Asset Code", "Asset", "Date", "Type", "Scope", "Functional Proceeds", "Cost Derecognized", "NBV", "Gain/Loss", "Remaining NBV", "Posted Reference"],
                    report.Select(item => new object?[]
                    {
                        item.AssetCode, item.Name, item.DisposalDate, item.DisposalType,
                        item.DisposalScope, item.ProceedsFunctionalAmount, item.CostAtDisposal,
                        item.NetBookValue, item.GainLoss, item.RemainingNetBookValue,
                        item.HasPostedGlReference
                    }).ToList());
            }
            case "TRANSFERREPORT":
            case "TRANSFERS":
            {
                var report = await GetTransferReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Transfers",
                    "Transfers",
                    ["Asset Code", "Asset", "Date", "Type", "From Location", "To Location", "From Segment", "To Segment", "Reason", "GL Impact", "Posted Reference"],
                    report.Select(item => new object?[]
                    {
                        item.AssetCode, item.Name, item.TransferDate, item.TransferType,
                        item.FromLocation, item.ToLocation, item.FromSegmentString,
                        item.ToSegmentString, item.Reason, item.HasGlImpact,
                        item.HasPostedGlReference
                    }).ToList());
            }
            case "ROLLFORWARDREPORT":
            case "ROLLFORWARD":
            {
                var report = await GetRollForwardReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset Roll-Forward",
                    "Roll Forward",
                    ["Category", "Book", "Assets", "Opening Cost", "Opening Depreciation", "Opening Impairment", "Additions", "Revaluation Increase", "Revaluation Decrease", "Depreciation", "Impairment", "Impairment Reversal", "Disposals", "Closing Cost", "Closing Depreciation", "Closing Impairment", "Closing NBV"],
                    report.Rows.Select(item => new object?[]
                    {
                        item.CategoryName, item.BookClassification, item.AssetCount, item.OpeningCost,
                        item.OpeningAccumulatedDepreciation, item.OpeningAccumulatedImpairment,
                        item.Additions, item.RevaluationIncrease, item.RevaluationDecrease,
                        item.DepreciationCharge, item.ImpairmentAdditions, item.ImpairmentReversals,
                        item.Disposals, item.ClosingCost, item.ClosingAccumulatedDepreciation,
                        item.ClosingAccumulatedImpairment, item.ClosingNetBookValue
                    }).ToList());
            }
            case "GLRECONCILIATIONREPORT":
            case "GLRECONCILIATION":
            {
                var report = await GetGlReconciliationReportAsync(query);
                return new FixedAssetExportTable(
                    "Fixed Asset to GL Reconciliation",
                    "GL Reconciliation",
                    ["Area", "Account", "Account Name", "GL Balance", "Subledger Balance", "Variance", "Source Documents", "Missing References", "Subledger Without GL", "GL Without Source", "Warning"],
                    report.Rows.Select(item => new object?[]
                    {
                        item.Area, item.AccountNumber, item.AccountName, item.GlBalance,
                        item.SubledgerBalance, item.Variance, item.SourceDocumentCount,
                        item.MissingPostingReferenceCount, item.SubledgerWithoutGlCount,
                        item.GlWithoutSourceReferenceCount, item.PresentationWarning
                    }).ToList());
            }
            default:
                throw new ArgumentException($"Unsupported fixed asset report type '{reportType}'.", nameof(reportType));
        }
    }

    private static void SetExcelCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case DateTime date:
                cell.Value = date;
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;
            case decimal number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00";
                break;
            case int number:
                cell.Value = number;
                break;
            case bool boolean:
                cell.Value = boolean;
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static string FormatExportValue(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd"),
            decimal number => number.ToString("N2"),
            bool boolean => boolean ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty
        };

    private sealed record FixedAssetExportTable(
        string Title,
        string WorksheetName,
        string[] Headers,
        List<object?[]> Rows);

    private IQueryable<FixedAsset> BuildAssetQuery(FixedAssetReportQueryDto query)
    {
        var dbQuery = _context.FixedAssets
            .Where(a => a.TenantId == TenantId && !a.IsDeleted);

        if (query.AssetId.HasValue)
            dbQuery = dbQuery.Where(a => a.Id == query.AssetId.Value);
        if (query.CategoryId.HasValue)
            dbQuery = dbQuery.Where(a => a.FixedAssetCategoryId == query.CategoryId.Value);
        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(a => a.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            dbQuery = dbQuery.Where(a =>
                a.Name.Contains(query.SearchTerm) ||
                a.AssetCode.Contains(query.SearchTerm) ||
                (a.Location != null && a.Location.Contains(query.SearchTerm)));
        if (!string.IsNullOrWhiteSpace(query.Location))
            dbQuery = dbQuery.Where(a => a.Location == query.Location);
        if (!string.IsNullOrWhiteSpace(query.SegmentString))
            dbQuery = dbQuery.Where(a => a.CurrentSegmentString == query.SegmentString);
        if (query.AccountId.HasValue)
        {
            dbQuery = dbQuery.Where(a =>
                a.Category.AssetAccountId == query.AccountId.Value ||
                a.Category.AccumulatedDepreciationAccountId == query.AccountId.Value ||
                a.Category.DepreciationExpenseAccountId == query.AccountId.Value ||
                a.Category.GainOnDisposalAccountId == query.AccountId.Value ||
                a.Category.LossOnDisposalAccountId == query.AccountId.Value ||
                a.Category.DisposalProceedsClearingAccountId == query.AccountId.Value ||
                a.Category.RevaluationSurplusAccountId == query.AccountId.Value ||
                a.Category.RevaluationLossAccountId == query.AccountId.Value ||
                a.Category.ImpairmentLossAccountId == query.AccountId.Value ||
                a.Category.AccumulatedImpairmentAccountId == query.AccountId.Value);
        }

        return dbQuery;
    }

    private IQueryable<AssetDepreciationSchedule> BuildDepreciationQuery(FixedAssetReportQueryDto query)
    {
        var filteredAssetIds = BuildAssetQuery(query).Select(asset => asset.Id);
        var dbQuery = _context.AssetDepreciationSchedules
            // Reversed schedules remain immutable source evidence, but current depreciation and
            // reconciliation reports must follow the net accounting position after correction.
            .Where(s =>
                s.TenantId == TenantId &&
                !s.IsDeleted &&
                !s.IsReversed &&
                filteredAssetIds.Contains(s.FixedAssetId));

        if (query.AssetId.HasValue)
            dbQuery = dbQuery.Where(s => s.FixedAssetId == query.AssetId.Value);
        if (query.FiscalPeriodId.HasValue)
            dbQuery = dbQuery.Where(s => s.FiscalPeriodId == query.FiscalPeriodId.Value);
        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(s => (s.PostingDate ?? s.PostedDate ?? DateTime.MinValue).Date >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(s => (s.PostingDate ?? s.PostedDate ?? DateTime.MaxValue).Date <= query.ToDate.Value.Date);
        var book = NormalizeBookClassification(query.BookClassification);
        if (!string.IsNullOrWhiteSpace(book))
            dbQuery = dbQuery.Where(s => s.BookClassification == book);

        return dbQuery;
    }

    private IQueryable<AssetValuation> BuildValuationQuery(FixedAssetReportQueryDto query)
    {
        var filteredAssetIds = BuildAssetQuery(query).Select(asset => asset.Id);
        var dbQuery = _context.AssetValuations
            .Where(v =>
                v.TenantId == TenantId &&
                !v.IsDeleted &&
                !v.IsCorrected &&
                filteredAssetIds.Contains(v.FixedAssetId));

        if (query.AssetId.HasValue)
            dbQuery = dbQuery.Where(v => v.FixedAssetId == query.AssetId.Value);
        if (query.FiscalPeriodId.HasValue)
            dbQuery = dbQuery.Where(v => v.FiscalPeriodId == query.FiscalPeriodId.Value);
        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(v => (v.AccountingDate == default ? v.ValuationDate : v.AccountingDate).Date >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(v => (v.AccountingDate == default ? v.ValuationDate : v.AccountingDate).Date <= query.ToDate.Value.Date);
        var book = NormalizeBookClassification(query.BookClassification);
        if (!string.IsNullOrWhiteSpace(book))
            dbQuery = dbQuery.Where(v => v.BookClassification == book);

        return dbQuery;
    }

    private IQueryable<AssetTransfer> BuildTransferQuery(FixedAssetReportQueryDto query)
    {
        var filteredAssetIds = BuildAssetQuery(query).Select(asset => asset.Id);
        var dbQuery = _context.AssetTransfers
            .Where(t =>
                t.TenantId == TenantId &&
                !t.IsDeleted &&
                filteredAssetIds.Contains(t.FixedAssetId));

        if (query.AssetId.HasValue)
            dbQuery = dbQuery.Where(t => t.FixedAssetId == query.AssetId.Value);
        if (query.FiscalPeriodId.HasValue)
            dbQuery = dbQuery.Where(t => t.FiscalPeriodId == query.FiscalPeriodId.Value);
        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransferDate.Date >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransferDate.Date <= query.ToDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(query.Location))
            dbQuery = dbQuery.Where(t => t.ToLocation == query.Location || t.FromLocation == query.Location);
        if (!string.IsNullOrWhiteSpace(query.SegmentString))
            dbQuery = dbQuery.Where(t => t.ToSegmentString == query.SegmentString || t.FromSegmentString == query.SegmentString);
        var book = NormalizeBookClassification(query.BookClassification);
        if (!string.IsNullOrWhiteSpace(book))
            dbQuery = dbQuery.Where(t => t.BookClassification == book);

        return dbQuery;
    }

    private IQueryable<AssetDisposal> BuildDisposalQuery(FixedAssetReportQueryDto query)
    {
        var filteredAssetIds = BuildAssetQuery(query).Select(asset => asset.Id);
        var dbQuery = _context.AssetDisposals
            .Where(d =>
                d.TenantId == TenantId &&
                !d.IsDeleted &&
                filteredAssetIds.Contains(d.FixedAssetId));

        if (query.AssetId.HasValue)
            dbQuery = dbQuery.Where(d => d.FixedAssetId == query.AssetId.Value);
        if (query.FiscalPeriodId.HasValue)
            dbQuery = dbQuery.Where(d => d.FiscalPeriodId == query.FiscalPeriodId.Value);
        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(d => d.DisposalDate.Date >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(d => d.DisposalDate.Date <= query.ToDate.Value.Date);
        var book = NormalizeBookClassification(query.BookClassification);
        if (!string.IsNullOrWhiteSpace(book))
            dbQuery = dbQuery.Where(d => d.BookClassification == book);

        return dbQuery;
    }

    private async Task<List<AccountTransaction>> LoadPostedFixedAssetGlLinesAsync(FixedAssetReportQueryDto query)
    {
        var dbQuery = _context.AccountTransactions
            .Include(t => t.Account)
            .Include(t => t.JournalEntry)
            .Where(t =>
                t.TenantId == TenantId &&
                !t.IsDeleted &&
                t.PostingStatus == "Posted" &&
                t.JournalEntry != null &&
                !t.JournalEntry.IsDeleted &&
                t.JournalEntry.PostingStatus == "Posted" &&
                (t.SourceModule == SourceModuleDirectFa ||
                    t.SourceModule == SourceModuleFixedAssets ||
                    (t.Notes != null && t.Notes.Contains("FixedAssetId="))));

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransactionDate.Date >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransactionDate.Date <= query.ToDate.Value.Date);
        if (query.FiscalPeriodId.HasValue)
            dbQuery = dbQuery.Where(t => t.FiscalPeriodId == query.FiscalPeriodId.Value);
        if (query.AccountId.HasValue)
            dbQuery = dbQuery.Where(t => t.AccountId == query.AccountId.Value);
        var book = NormalizeBookClassification(query.BookClassification);
        if (!string.IsNullOrWhiteSpace(book))
        {
            var accountingBookId = await _context.AccountingBooks
                .Where(candidate =>
                    candidate.TenantId == TenantId &&
                    !candidate.IsDeleted &&
                    candidate.Code == book)
                .Select(candidate => (Guid?)candidate.Id)
                .SingleAsync();
            dbQuery = dbQuery.Where(t =>
                t.BookClassification == book &&
                t.AccountingBookId == accountingBookId.Value);
        }

        return await dbQuery.ToListAsync();
    }

    private static FixedAssetReportQueryDto BuildAsOfQuery(FixedAssetReportQueryDto query)
        => new()
        {
            ToDate = query.ToDate ?? DateTime.UtcNow.Date,
            AssetId = query.AssetId,
            CategoryId = query.CategoryId,
            AccountId = query.AccountId,
            FiscalPeriodId = query.FiscalPeriodId,
            Status = query.Status,
            SearchTerm = query.SearchTerm,
            BookClassification = query.BookClassification,
            Location = query.Location,
            SegmentString = query.SegmentString
        };

    private static FixedAssetReportQueryDto BuildHistoryScopeQuery(FixedAssetReportQueryDto query)
        => new()
        {
            AssetId = query.AssetId,
            CategoryId = query.CategoryId,
            AccountId = query.AccountId,
            Status = query.Status,
            SearchTerm = query.SearchTerm,
            BookClassification = query.BookClassification,
            Location = query.Location,
            SegmentString = query.SegmentString
        };

    private async Task ValidateFiltersAsync(FixedAssetReportQueryDto query)
    {
        if (query.FromDate.HasValue && query.ToDate.HasValue &&
            query.FromDate.Value.Date > query.ToDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset report start date cannot be after the end date.");
        }

        if (query.AssetId.HasValue &&
            !await _context.FixedAssets.AnyAsync(a => a.TenantId == TenantId && a.Id == query.AssetId.Value && !a.IsDeleted))
        {
            throw new InvalidOperationException("Fixed asset filter does not belong to the current tenant.");
        }

        if (query.CategoryId.HasValue &&
            !await _context.FixedAssetCategories.AnyAsync(c => c.TenantId == TenantId && c.Id == query.CategoryId.Value && !c.IsDeleted))
        {
            throw new InvalidOperationException("Fixed asset category filter does not belong to the current tenant.");
        }

        if (query.AccountId.HasValue &&
            !await _context.Accounts.AnyAsync(a => a.TenantId == TenantId && a.Id == query.AccountId.Value && !a.IsDeleted))
        {
            throw new InvalidOperationException("Fixed asset report account filter does not belong to the current tenant.");
        }

        if (query.FiscalPeriodId.HasValue &&
            !await _context.FiscalPeriods.AnyAsync(period =>
                period.TenantId == TenantId &&
                period.Id == query.FiscalPeriodId.Value &&
                !period.IsDeleted))
        {
            throw new InvalidOperationException("Fixed asset report fiscal period does not belong to the current tenant.");
        }

        var book = NormalizeBookClassification(query.BookClassification);
        if (!string.IsNullOrWhiteSpace(book) &&
            !await _context.AccountingBooks.AnyAsync(candidate =>
                candidate.TenantId == TenantId &&
                !candidate.IsDeleted &&
                candidate.IsActive &&
                candidate.Code == book))
        {
            throw new InvalidOperationException("Fixed asset report accounting book does not belong to the current tenant or is inactive.");
        }
    }

    private static FixedAssetBookValue? SelectBookValue(FixedAsset asset, string? requestedBook)
    {
        var values = asset.BookValues
            .Where(value => !value.IsDeleted)
            .ToList();

        if (!string.IsNullOrWhiteSpace(requestedBook))
        {
            return values.FirstOrDefault(value =>
                NormalizeBookClassification(value.BookClassification) == requestedBook);
        }

        return values
            .OrderBy(value => value.AccountingBook?.IsDefault == true ? 0 : 1)
            .ThenBy(value => value.AccountingBook?.SortOrder ?? int.MaxValue)
            .FirstOrDefault();
    }

    private static bool IsAssetRelated(AccountTransaction transaction, FixedAsset asset)
    {
        if (transaction.SourceDocumentId == asset.Id)
        {
            return true;
        }

        if (asset.SourceDocumentId.HasValue && transaction.SourceDocumentId == asset.SourceDocumentId.Value)
        {
            return NotesContainId(transaction.Notes, "FixedAssetId", asset.Id)
                || transaction.JournalEntryId == asset.JournalEntryId;
        }

        return NotesContainId(transaction.Notes, "FixedAssetId", asset.Id)
            || transaction.JournalEntryId == asset.JournalEntryId;
    }

    private static bool IsDepreciationLineRelated(AccountTransaction transaction, AssetDepreciationSchedule schedule)
        => (transaction.SourceDocumentType == SourceDocumentTypeDepreciationRun ||
            transaction.SourceDocumentType == SourceDocumentTypeDisposal) &&
           (transaction.SourceDocumentId == schedule.FixedAssetDepreciationRunId ||
            transaction.SourceDocumentId == schedule.AssetDisposalId ||
            NotesContainId(transaction.Notes, "ScheduleId", schedule.Id) ||
            NotesContainId(transaction.Notes, "FixedAssetId", schedule.FixedAssetId));

    private static bool IsValuationLineRelated(AccountTransaction transaction, AssetValuation valuation)
        => transaction.SourceDocumentType == SourceDocumentTypeValuation &&
           (transaction.SourceDocumentId == valuation.Id ||
            NotesContainId(transaction.Notes, "ValuationId", valuation.Id) ||
            NotesContainId(transaction.Notes, "FixedAssetId", valuation.FixedAssetId));

    private static bool HasMatchingDepreciationGlLine(IEnumerable<AccountTransaction> glLines, AssetDepreciationSchedule schedule)
        => glLines.Any(line => IsDepreciationLineRelated(line, schedule));

    private static bool DateInRange(DateTime date, FixedAssetReportQueryDto query)
    {
        var value = date.Date;
        return (!query.FromDate.HasValue || value >= query.FromDate.Value.Date)
            && (!query.ToDate.HasValue || value <= query.ToDate.Value.Date);
    }

    private static DateTime ReportCutoff(FixedAssetReportQueryDto query)
        => (query.ToDate ?? DateTime.UtcNow.Date).Date.AddDays(1).AddTicks(-1);

    private static DateTime CapitalizationDate(FixedAsset asset, FixedAssetBookValue bookValue)
        => (bookValue.CapitalizationDate ?? asset.CapitalizationDate ?? asset.CapitalizedAt ?? asset.PurchaseDate).Date;

    private static DateTime ScheduleDate(AssetDepreciationSchedule schedule)
        => (schedule.PostingDate ?? schedule.PostedDate ?? schedule.CreatedAt).Date;

    private static DateTime ValuationDate(AssetValuation valuation)
        => (valuation.AccountingDate == default ? valuation.ValuationDate : valuation.AccountingDate).Date;

    private static DateTime DisposalDate(AssetDisposal disposal)
        => (disposal.AccountingDate ?? disposal.DisposalDate).Date;

    private static decimal InitialAcquisitionCost(
        Guid assetId,
        FixedAssetBookValue bookValue,
        IReadOnlyCollection<AssetDisposal> allDisposals)
        => RoundMoney(bookValue.AcquisitionCost + allDisposals
            .Where(disposal =>
                disposal.FixedAssetId == assetId &&
                disposal.DisposalScope != AssetDisposalScope.WholeAsset)
            .Sum(disposal => disposal.CostAtDisposal));

    private static decimal CarryingCostAt(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        DateTime cutoff,
        IReadOnlyCollection<AssetValuation> allValuations,
        IReadOnlyCollection<AssetDisposal> allDisposals)
    {
        if (CapitalizationDate(asset, bookValue) > cutoff)
        {
            return 0m;
        }

        var initialCost = InitialAcquisitionCost(asset.Id, bookValue, allDisposals);
        var revaluationMovement = allValuations
            .Where(valuation => valuation.FixedAssetId == asset.Id && ValuationDate(valuation) <= cutoff)
            .Sum(valuation => valuation.RevaluationSurplus - valuation.RevaluationDeficit);
        var disposedCost = allDisposals
            .Where(disposal => disposal.FixedAssetId == asset.Id && DisposalDate(disposal) <= cutoff)
            .Sum(disposal => disposal.CostAtDisposal);

        return RoundMoney(initialCost + revaluationMovement - disposedCost);
    }

    private static decimal AccumulatedDepreciationAt(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        DateTime cutoff,
        IReadOnlyCollection<AssetDepreciationSchedule> allSchedules,
        IReadOnlyCollection<AssetDisposal> allDisposals)
    {
        if (CapitalizationDate(asset, bookValue) > cutoff)
        {
            return 0m;
        }

        // Book values are current-state snapshots. Reverse all immutable movements to recover the
        // opening reserve, then replay only the movements effective by the requested cutoff.
        var lifetimeDepreciation = allSchedules
            .Where(schedule => schedule.FixedAssetId == asset.Id)
            .Sum(schedule => schedule.DepreciationAmount);
        var lifetimeCleared = allDisposals
            .Where(disposal => disposal.FixedAssetId == asset.Id)
            .Sum(disposal => disposal.AccumulatedDepreciationAtDisposal);
        var openingReserve = bookValue.AccumulatedDepreciation + lifetimeCleared - lifetimeDepreciation;
        var depreciationToDate = allSchedules
            .Where(schedule => schedule.FixedAssetId == asset.Id && ScheduleDate(schedule) <= cutoff)
            .Sum(schedule => schedule.DepreciationAmount);
        var clearedToDate = allDisposals
            .Where(disposal => disposal.FixedAssetId == asset.Id && DisposalDate(disposal) <= cutoff)
            .Sum(disposal => disposal.AccumulatedDepreciationAtDisposal);

        return RoundMoney(openingReserve + depreciationToDate - clearedToDate);
    }

    private static decimal AccumulatedImpairmentAt(
        Guid assetId,
        DateTime cutoff,
        IReadOnlyCollection<AssetValuation> allValuations,
        IReadOnlyCollection<AssetDisposal> allDisposals)
    {
        var movement = allValuations
            .Where(valuation => valuation.FixedAssetId == assetId && ValuationDate(valuation) <= cutoff)
            .Sum(valuation => valuation.ImpairmentLoss - valuation.ImpairmentReversal);
        var cleared = allDisposals
            .Where(disposal => disposal.FixedAssetId == assetId && DisposalDate(disposal) <= cutoff)
            .Sum(disposal => disposal.AccumulatedImpairmentAtDisposal);

        return RoundMoney(movement - cleared);
    }

    private static bool IsOnRegisterAt(
        FixedAsset asset,
        DateTime cutoff,
        IReadOnlyCollection<AssetDisposal> allDisposals)
        => !allDisposals.Any(disposal =>
            disposal.FixedAssetId == asset.Id &&
            disposal.DisposalScope == AssetDisposalScope.WholeAsset &&
            DisposalDate(disposal) <= cutoff);

    private static FixedAssetStatus StatusAt(
        FixedAsset asset,
        DateTime cutoff,
        IReadOnlyCollection<AssetDisposal> allDisposals)
    {
        if (!IsOnRegisterAt(asset, cutoff, allDisposals))
        {
            return asset.Status is FixedAssetStatus.WrittenOff
                ? FixedAssetStatus.WrittenOff
                : FixedAssetStatus.Disposed;
        }

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff)
        {
            return asset.PlacedInServiceDate.HasValue && asset.PlacedInServiceDate.Value.Date <= cutoff
                ? FixedAssetStatus.Active
                : FixedAssetStatus.Capitalized;
        }

        return asset.Status;
    }

    private static bool NotesContainId(string? notes, string key, Guid id)
        => !string.IsNullOrWhiteSpace(notes) &&
           notes.Contains($"{key}={id:N}", StringComparison.OrdinalIgnoreCase);

    private static decimal SubledgerAssetCarryingBalance(
        IReadOnlyCollection<FixedAsset> assets,
        IReadOnlyCollection<AssetValuation> valuations,
        IReadOnlyCollection<AssetDisposal> disposals,
        Guid categoryId,
        string? requestedBook,
        DateTime cutoff)
    {
        return RoundMoney(assets
            .Where(asset => asset.FixedAssetCategoryId == categoryId)
            .Sum(asset => CarryingCostAt(
                asset,
                SelectBookValue(asset, requestedBook)!,
                cutoff,
                valuations,
                disposals)));
    }

    private static int MissingCapitalizationReferences(IReadOnlyCollection<FixedAsset> assets, Guid categoryId)
        => assets.Count(a =>
            a.FixedAssetCategoryId == categoryId &&
            a.Status is not FixedAssetStatus.Draft &&
            (!a.JournalEntryId.HasValue || !a.PostingEventId.HasValue));

    private static int SubledgerWithoutGlCapitalizations(
        IReadOnlyCollection<FixedAsset> assets,
        IReadOnlyCollection<AccountTransaction> glLines,
        Guid categoryId)
        => assets.Count(a =>
            a.FixedAssetCategoryId == categoryId &&
            a.Status is not FixedAssetStatus.Draft &&
            !glLines.Any(line => line.AccountId == a.Category.AssetAccountId && IsAssetRelated(line, a)));

    private static void AddReconciliationRow(
        List<FixedAssetGlReconciliationRowDto> rows,
        List<FixedAssetReportingDiagnosticDto> diagnostics,
        string area,
        Guid accountId,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyCollection<AccountTransaction> glLines,
        decimal subledgerBalance,
        int sourceDocumentCount,
        int missingPostingReferenceCount,
        int subledgerWithoutGlCount,
        BalanceConvention convention,
        string? presentationWarning = null)
    {
        if (!accounts.TryGetValue(accountId, out var account))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-MISSING-ACCOUNT",
                Severity = "Critical",
                Message = $"{area} account mapping does not resolve to a same-tenant account.",
                AccountId = accountId
            });
            return;
        }

        var accountLines = glLines.Where(line => line.AccountId == accountId).ToList();
        var glBalance = convention == BalanceConvention.CreditMinusDebit
            ? RoundMoney(accountLines.Sum(line => line.CreditAmount - line.DebitAmount))
            : RoundMoney(accountLines.Sum(line => line.DebitAmount - line.CreditAmount));
        var glWithoutSource = accountLines.Count(line => string.IsNullOrWhiteSpace(line.SourceDocumentType) || !IsFixedAssetSourceLine(line));
        var variance = RoundMoney(glBalance - subledgerBalance);

        rows.Add(new FixedAssetGlReconciliationRowDto
        {
            Area = area,
            AccountId = accountId,
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            AccountType = account.AccountType,
            GlBalance = glBalance,
            SubledgerBalance = subledgerBalance,
            Variance = variance,
            SourceDocumentCount = sourceDocumentCount,
            MissingPostingReferenceCount = missingPostingReferenceCount,
            GlWithoutSourceReferenceCount = glWithoutSource,
            SubledgerWithoutGlCount = subledgerWithoutGlCount,
            PresentationWarning = presentationWarning
        });

        if (Math.Abs(variance) >= 0.01m)
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-GL-VARIANCE",
                Severity = "Critical",
                Message = $"{area} subledger does not reconcile to posted GL.",
                AccountId = accountId
            });
        }

        if (missingPostingReferenceCount > 0)
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-MISSING-POSTING-REFERENCE",
                Severity = "Critical",
                Message = $"{area} has fixed asset subledger records missing journal or posting-event references.",
                AccountId = accountId
            });
        }

        if (subledgerWithoutGlCount > 0)
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-SUBLEDGER-WITHOUT-GL",
                Severity = "Critical",
                Message = $"{area} has fixed asset subledger records without matching posted GL movement.",
                AccountId = accountId
            });
        }

        if (glWithoutSource > 0)
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-GL-WITHOUT-ASSET-SOURCE",
                Severity = "Warning",
                Message = $"{area} has posted GL movement without fixed asset source references.",
                AccountId = accountId
            });
        }

        if (!string.IsNullOrWhiteSpace(presentationWarning))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-DISPOSAL-GAIN-PRESENTATION",
                Severity = "Warning",
                Message = presentationWarning,
                AccountId = accountId
            });
        }
    }

    private static bool IsFixedAssetSourceLine(AccountTransaction line)
        => string.Equals(line.SourceModule, SourceModuleDirectFa, StringComparison.OrdinalIgnoreCase)
            || string.Equals(line.SourceModule, SourceModuleFixedAssets, StringComparison.OrdinalIgnoreCase)
            || NotesContainFixedAssetReference(line.Notes);

    private static bool NotesContainFixedAssetReference(string? notes)
        => !string.IsNullOrWhiteSpace(notes)
            && notes.Contains("FixedAssetId=", StringComparison.OrdinalIgnoreCase);

    private static void AddSourceReferenceDiagnostics(
        List<FixedAssetReportingDiagnosticDto> diagnostics,
        IReadOnlyCollection<FixedAsset> assets,
        IReadOnlyCollection<AssetDepreciationSchedule> schedules,
        IReadOnlyCollection<AssetValuation> valuations,
        IReadOnlyCollection<AssetDisposal> disposals,
        IReadOnlyCollection<AccountTransaction> glLines)
    {
        foreach (var asset in assets.Where(a =>
            a.Status is FixedAssetStatus.Capitalized or FixedAssetStatus.Active or FixedAssetStatus.FullyDepreciated &&
            (!a.JournalEntryId.HasValue || !a.PostingEventId.HasValue)))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-CAPITALIZED-MISSING-REFERENCE",
                Severity = "Critical",
                Message = "Capitalized fixed asset is missing journal or posting-event reference.",
                FixedAssetId = asset.Id,
                SourceDocumentType = asset.SourceDocumentType,
                SourceDocumentId = asset.SourceDocumentId
            });
        }

        foreach (var schedule in schedules.Where(s => !s.JournalEntryId.HasValue || !s.PostingEventId.HasValue))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-DEPRECIATION-MISSING-REFERENCE",
                Severity = "Critical",
                Message = "Posted depreciation schedule is missing journal or posting-event reference.",
                FixedAssetId = schedule.FixedAssetId,
                SourceDocumentType = SourceDocumentTypeDepreciationRun,
                SourceDocumentId = schedule.FixedAssetDepreciationRunId
            });
        }

        foreach (var valuation in valuations.Where(v => !v.JournalEntryId.HasValue || !v.PostingEventId.HasValue))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-VALUATION-MISSING-REFERENCE",
                Severity = "Critical",
                Message = "Posted valuation is missing journal or posting-event reference.",
                FixedAssetId = valuation.FixedAssetId,
                SourceDocumentType = SourceDocumentTypeValuation,
                SourceDocumentId = valuation.Id
            });
        }

        foreach (var disposal in disposals.Where(d => !d.JournalEntryId.HasValue || !d.PostingEventId.HasValue))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-DISPOSAL-MISSING-REFERENCE",
                Severity = "Critical",
                Message = "Completed disposal is missing journal or posting-event reference.",
                FixedAssetId = disposal.FixedAssetId,
                SourceDocumentType = SourceDocumentTypeDisposal,
                SourceDocumentId = disposal.Id
            });
        }

        foreach (var line in glLines.Where(line => !IsFixedAssetSourceLine(line)))
        {
            diagnostics.Add(new FixedAssetReportingDiagnosticDto
            {
                Code = "FA-REPORT-GL-WITHOUT-ASSET-SOURCE",
                Severity = "Warning",
                Message = "Posted fixed asset account line is missing fixed asset source references.",
                SourceDocumentType = line.SourceDocumentType,
                SourceDocumentId = line.SourceDocumentId,
                AccountId = line.AccountId
            });
        }
    }

    private static string? BuildDisposalGainPresentationWarning(AssetDisposal? disposal, Account? gainAccount)
    {
        if (disposal == null || disposal.GainOrLoss <= 0m || gainAccount == null)
        {
            return null;
        }

        if (gainAccount.AccountType == AccountType.Expense)
        {
            return "Disposal gain is credited to an expense-class presentation account as a contra gain workaround; fixed asset reports classify it as non-operating disposal gain, not ordinary operating expense.";
        }

        if (gainAccount.AccountType == AccountType.Revenue)
        {
            return "Disposal gain account is revenue-class; fixed asset reports classify the movement as non-operating disposal gain unless financial statement mapping intentionally presents it as revenue.";
        }

        return null;
    }

    private async Task RecordReportAuditAsync(string eventType, string reportName, object afterValues)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = SourceModuleFixedAssets,
            SourceDocumentType = reportName,
            AfterValues = afterValues,
            Resource = "Finance.FixedAssetReports",
            ResourceId = reportName,
            Comment = $"{reportName} generated."
        });
    }

    private static string? NormalizeBookClassification(string? bookClassification)
    {
        var normalized = bookClassification?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToUpperInvariant();
    }

    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private enum BalanceConvention
    {
        DebitMinusCredit,
        CreditMinusDebit
    }
}
