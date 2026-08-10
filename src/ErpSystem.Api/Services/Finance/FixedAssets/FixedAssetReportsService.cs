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
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(value => value.AccountingBook)
            .ToListAsync();

        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));
        var items = assets.Select(a =>
        {
            var bookValue = SelectBookValue(a, requestedBook);
            var cost = RoundMoney(bookValue?.AcquisitionCost ?? a.AcquisitionCost);
            var accumulatedDepreciation = RoundMoney(bookValue?.AccumulatedDepreciation ?? a.AcquisitionCost - a.NetBookValue);
            var assetCostGl = RoundMoney(glLines
                .Where(line => line.AccountId == a.Category.AssetAccountId && IsAssetRelated(line, a))
                .Sum(line => line.DebitAmount - line.CreditAmount));
            var accumulatedDepreciationGl = RoundMoney(glLines
                .Where(line => line.AccountId == a.Category.AccumulatedDepreciationAccountId && IsAssetRelated(line, a))
                .Sum(line => line.CreditAmount - line.DebitAmount));

            return new FixedAssetRegisterItemDto
            {
                Id = a.Id,
                BookValueId = bookValue?.Id,
                CategoryId = a.FixedAssetCategoryId,
                AssetCode = a.AssetCode,
                Name = a.Name,
                CategoryName = a.Category?.Name ?? "Unknown",
                AcquisitionDate = a.PurchaseDate,
                CapitalizationDate = bookValue?.CapitalizationDate ?? a.CapitalizationDate,
                Cost = cost,
                AccumulatedDepreciation = accumulatedDepreciation,
                NetBookValue = RoundMoney(bookValue?.NetBookValue ?? a.NetBookValue),
                BookClassification = bookValue?.BookClassification ?? requestedBook ?? "IFRS",
                Status = a.Status,
                SerialNumber = a.SerialNumber,
                Location = a.Location,
                CurrentSegmentString = a.CurrentSegmentString,
                SourceDocumentType = bookValue?.SourceDocumentType ?? a.SourceDocumentType,
                SourceDocumentId = bookValue?.SourceDocumentId ?? a.SourceDocumentId,
                SourceDocumentLineId = bookValue?.SourceDocumentLineId ?? a.SourceDocumentLineId,
                JournalEntryId = bookValue?.CapitalizationJournalEntryId ?? a.JournalEntryId,
                PostingEventId = bookValue?.CapitalizationPostingEventId ?? a.PostingEventId,
                PostedGlCostMovement = assetCostGl,
                PostedGlAccumulatedDepreciationMovement = accumulatedDepreciationGl,
                ReconciliationVariance = RoundMoney(assetCostGl - cost),
                HasPostedGlReference = (bookValue?.CapitalizationJournalEntryId ?? a.JournalEntryId).HasValue
                    && (bookValue?.CapitalizationPostingEventId ?? a.PostingEventId).HasValue
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
            .Where(a => DateInRange(a.CapitalizationDate ?? a.CapitalizedAt ?? a.PurchaseDate, query))
            .ToList();

        var glLines = await LoadPostedFixedAssetGlLinesAsync(query);
        var items = assets.Select(a =>
        {
            var bookValue = SelectBookValue(a, requestedBook);
            var cost = RoundMoney(bookValue?.AcquisitionCost ?? a.AcquisitionCost);
            var postedCost = RoundMoney(glLines
                .Where(line =>
                    line.AccountId == a.Category.AssetAccountId &&
                    IsAssetRelated(line, a) &&
                    string.Equals(line.TransactionTag, "FA-Capitalization", StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.DebitAmount - line.CreditAmount));
            var sourceDocumentType = bookValue?.SourceDocumentType ?? a.SourceDocumentType;

            return new FixedAssetAdditionReportItemDto
            {
                AssetId = a.Id,
                AssetCode = a.AssetCode,
                Name = a.Name,
                CategoryId = a.FixedAssetCategoryId,
                CategoryName = a.Category?.Name ?? "Unknown",
                CapitalizationDate = bookValue?.CapitalizationDate ?? a.CapitalizationDate,
                SourceDocumentType = sourceDocumentType,
                SourceDocumentId = bookValue?.SourceDocumentId ?? a.SourceDocumentId,
                SourceDocumentLineId = bookValue?.SourceDocumentLineId ?? a.SourceDocumentLineId,
                JournalEntryId = bookValue?.CapitalizationJournalEntryId ?? a.JournalEntryId,
                PostingEventId = bookValue?.CapitalizationPostingEventId ?? a.PostingEventId,
                FunctionalCurrencyCode = a.FunctionalCurrencyCode,
                TransactionCurrencyCode = a.TransactionCurrencyCode,
                CapitalizedCost = cost,
                PostedGlCost = postedCost,
                Variance = RoundMoney(postedCost - cost),
                IsApSourced = string.Equals(sourceDocumentType, SourceDocumentTypeVendorInvoice, StringComparison.OrdinalIgnoreCase),
                IsDirectCapitalization = string.Equals(sourceDocumentType, SourceDocumentTypeFixedAsset, StringComparison.OrdinalIgnoreCase),
                MissingPostingReference = !(bookValue?.CapitalizationJournalEntryId ?? a.JournalEntryId).HasValue
                    || !(bookValue?.CapitalizationPostingEventId ?? a.PostingEventId).HasValue
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
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .ToListAsync();
        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));

        var items = assets.Select(a =>
        {
            var bookValue = SelectBookValue(a, requestedBook);
            var subledgerAccumulated = RoundMoney(bookValue?.AccumulatedDepreciation ?? a.AcquisitionCost - a.NetBookValue);
            var postedGlAccumulated = RoundMoney(glLines
                .Where(line => line.AccountId == a.Category.AccumulatedDepreciationAccountId && IsAssetRelated(line, a))
                .Sum(line => line.CreditAmount - line.DebitAmount));

            return new FixedAssetAccumulatedDepreciationReportItemDto
            {
                FixedAssetId = a.Id,
                AssetCode = a.AssetCode,
                AssetName = a.Name,
                BookClassification = bookValue?.BookClassification ?? requestedBook ?? "IFRS",
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
                SaleProceeds = d.SaleProceeds,
                DisposalCost = d.DisposalCost,
                NetProceeds = d.NetProceeds,
                CostAtDisposal = d.CostAtDisposal,
                AccumulatedDepreciationAtDisposal = d.AccumulatedDepreciationAtDisposal,
                AccumulatedImpairmentAtDisposal = d.AccumulatedImpairmentAtDisposal,
                RevaluationSurplusAtDisposal = d.RevaluationSurplusAtDisposal,
                RevaluationSurplusTransferAmount = d.RevaluationSurplusTransferAmount,
                NetBookValue = d.NetBookValueAtDisposal,
                GainLoss = d.GainOrLoss,
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
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .ToListAsync();
        var schedules = await BuildDepreciationQuery(query).Where(s => s.IsPosted && !s.IsDeleted).ToListAsync();
        var valuations = await BuildValuationQuery(query).Where(v => v.IsPostedToGL && !v.IsDeleted).ToListAsync();
        var disposals = await BuildDisposalQuery(query).Where(d => d.Status == AssetDisposalStatus.Completed).ToListAsync();

        var rows = assets
            .GroupBy(a => new
            {
                a.FixedAssetCategoryId,
                CategoryName = a.Category?.Name ?? "Unknown",
                Book = SelectBookValue(a, requestedBook)?.BookClassification ?? requestedBook ?? "IFRS"
            })
            .Select(group =>
            {
                var assetIds = group.Select(a => a.Id).ToHashSet();
                var openingCost = RoundMoney(group.Sum(a =>
                {
                    var book = SelectBookValue(a, requestedBook);
                    var capDate = book?.CapitalizationDate ?? a.CapitalizationDate ?? a.PurchaseDate;
                    return query.FromDate.HasValue && capDate.Date < query.FromDate.Value.Date
                        ? book?.AcquisitionCost ?? a.AcquisitionCost
                        : 0m;
                }));
                var additions = RoundMoney(group.Sum(a =>
                {
                    var book = SelectBookValue(a, requestedBook);
                    var capDate = book?.CapitalizationDate ?? a.CapitalizationDate ?? a.PurchaseDate;
                    return DateInRange(capDate, query)
                        ? book?.AcquisitionCost ?? a.AcquisitionCost
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
                    AssetCount = group.Count(),
                    OpeningCost = openingCost,
                    Additions = additions,
                    RevaluationIncrease = RoundMoney(groupValuations.Sum(v => v.RevaluationSurplus)),
                    RevaluationDecrease = RoundMoney(groupValuations.Sum(v => v.RevaluationDeficit)),
                    ImpairmentAdditions = RoundMoney(groupValuations.Sum(v => v.ImpairmentLoss)),
                    DepreciationCharge = RoundMoney(groupSchedules.Sum(s => s.DepreciationAmount)),
                    AccumulatedDepreciationMovement = RoundMoney(groupSchedules.Sum(s => s.DepreciationAmount)),
                    Disposals = RoundMoney(groupDisposals.Sum(d => d.CostAtDisposal)),
                    AccumulatedDepreciationCleared = RoundMoney(groupDisposals.Sum(d => d.AccumulatedDepreciationAtDisposal)),
                    AccumulatedImpairmentCleared = RoundMoney(groupDisposals.Sum(d => d.AccumulatedImpairmentAtDisposal))
                };

                row.ClosingCost = RoundMoney(row.OpeningCost + row.Additions + row.RevaluationIncrease - row.RevaluationDecrease - row.Disposals);
                row.ClosingAccumulatedDepreciation = RoundMoney(row.AccumulatedDepreciationMovement - row.AccumulatedDepreciationCleared);
                row.ClosingAccumulatedImpairment = RoundMoney(row.ImpairmentAdditions - row.AccumulatedImpairmentCleared);
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
            Additions = rows.Sum(r => r.Additions),
            RevaluationIncrease = rows.Sum(r => r.RevaluationIncrease),
            RevaluationDecrease = rows.Sum(r => r.RevaluationDecrease),
            ImpairmentAdditions = rows.Sum(r => r.ImpairmentAdditions),
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
        var assets = await BuildAssetQuery(query)
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .ToListAsync();
        var categories = assets
            .Select(a => a.Category)
            .Where(c => c != null)
            .DistinctBy(c => c!.Id)
            .Select(c => c!)
            .ToList();
        var schedules = await BuildDepreciationQuery(query).Where(s => s.IsPosted && !s.IsDeleted).ToListAsync();
        var valuations = await BuildValuationQuery(query).Where(v => v.IsPostedToGL && !v.IsDeleted).ToListAsync();
        var disposals = await BuildDisposalQuery(query).Where(d => d.Status == AssetDisposalStatus.Completed).ToListAsync();
        var glLines = await LoadPostedFixedAssetGlLinesAsync(BuildAsOfQuery(query));
        var accounts = await _context.Accounts
            .Where(a => a.TenantId == TenantId)
            .ToDictionaryAsync(a => a.Id);
        var diagnostics = new List<FixedAssetReportingDiagnosticDto>();
        var rows = new List<FixedAssetGlReconciliationRowDto>();

        foreach (var category in categories)
        {
            AddReconciliationRow(rows, diagnostics, "Asset Cost/Carrying", category.AssetAccountId, accounts, glLines,
                SubledgerAssetCarryingBalance(assets, valuations, disposals, category.Id),
                assets.Count(a => a.FixedAssetCategoryId == category.Id),
                MissingCapitalizationReferences(assets, category.Id),
                SubledgerWithoutGlCapitalizations(assets, glLines, category.Id),
                BalanceConvention.DebitMinusCredit);

            AddReconciliationRow(rows, diagnostics, "Accumulated Depreciation", category.AccumulatedDepreciationAccountId, accounts, glLines,
                RoundMoney(assets.Where(a => a.FixedAssetCategoryId == category.Id)
                    .SelectMany(a => a.BookValues.Where(b => !b.IsDeleted))
                    .Sum(b => b.AccumulatedDepreciation)),
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
                    RoundMoney(valuations.Where(v => v.ValuationType == ValuationType.Impairment && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(v => v.ImpairmentLoss)
                        - disposals.Where(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => d.AccumulatedImpairmentAtDisposal)),
                    valuations.Count(v => v.ValuationType == ValuationType.Impairment && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id)),
                    valuations.Count(v => v.ValuationType == ValuationType.Impairment && assets.Any(a => a.Id == v.FixedAssetId && a.FixedAssetCategoryId == category.Id) && (!v.JournalEntryId.HasValue || !v.PostingEventId.HasValue)),
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

            if (category.DisposalProceedsClearingAccountId.HasValue)
            {
                AddReconciliationRow(rows, diagnostics, "Disposal Proceeds Clearing", category.DisposalProceedsClearingAccountId.Value, accounts, glLines,
                    RoundMoney(disposals.Where(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id)).Sum(d => d.NetProceeds)),
                    disposals.Count(d => assets.Any(a => a.Id == d.FixedAssetId && a.FixedAssetCategoryId == category.Id) && d.NetProceeds > 0m),
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
            AsOfDate = query.ToDate,
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
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(reportType);

        if (string.Equals(reportType, "AssetRegister", StringComparison.OrdinalIgnoreCase))
        {
            var data = await GetAssetRegisterAsync(query);
            worksheet.Cell(1, 1).Value = "Asset Code";
            worksheet.Cell(1, 2).Value = "Name";
            worksheet.Cell(1, 3).Value = "Category";
            worksheet.Cell(1, 4).Value = "Location";
            worksheet.Cell(1, 5).Value = "Acquisition Date";
            worksheet.Cell(1, 6).Value = "Cost";
            worksheet.Cell(1, 7).Value = "Acc. Depreciation";
            worksheet.Cell(1, 8).Value = "NBV";
            worksheet.Cell(1, 9).Value = "Status";

            var range = worksheet.Range(1, 1, 1, 9);
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.LightGray;

            for (var i = 0; i < data.Items.Count; i++)
            {
                var item = data.Items[i];
                worksheet.Cell(i + 2, 1).Value = item.AssetCode;
                worksheet.Cell(i + 2, 2).Value = item.Name;
                worksheet.Cell(i + 2, 3).Value = item.CategoryName;
                worksheet.Cell(i + 2, 4).Value = item.Location;
                worksheet.Cell(i + 2, 5).Value = item.AcquisitionDate;
                worksheet.Cell(i + 2, 5).Style.DateFormat.Format = "yyyy-mm-dd";
                worksheet.Cell(i + 2, 6).Value = item.Cost;
                worksheet.Cell(i + 2, 7).Value = item.AccumulatedDepreciation;
                worksheet.Cell(i + 2, 8).Value = item.NetBookValue;
                worksheet.Cell(i + 2, 9).Value = item.Status.ToString();
            }

            worksheet.ColumnsUsed().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        throw new NotSupportedException($"Fixed asset Excel export type '{reportType}' is not supported by this legacy endpoint. Use the central finance report export service for fixed asset roll-forward and GL reconciliation exports.");
    }

    public async Task<byte[]> ExportToPdfAsync(string reportType, FixedAssetReportQueryDto query)
    {
        if (string.Equals(reportType, "AssetRegister", StringComparison.OrdinalIgnoreCase))
        {
            var data = await GetAssetRegisterAsync(query);
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Text("FIXED ASSET REGISTER").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Code");
                            header.Cell().Element(CellStyle).Text("Name");
                            header.Cell().Element(CellStyle).Text("Category");
                            header.Cell().Element(CellStyle).Text("Location");
                            header.Cell().Element(CellStyle).Text("Cost");
                            header.Cell().Element(CellStyle).Text("Depr.");
                            header.Cell().Element(CellStyle).Text("NBV");
                            header.Cell().Element(CellStyle).Text("Status");

                            static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Element(ValueStyle).Text(item.AssetCode);
                            table.Cell().Element(ValueStyle).Text(item.Name);
                            table.Cell().Element(ValueStyle).Text(item.CategoryName);
                            table.Cell().Element(ValueStyle).Text(item.Location ?? string.Empty);
                            table.Cell().Element(ValueStyle).Text(item.Cost.ToString("N2"));
                            table.Cell().Element(ValueStyle).Text(item.AccumulatedDepreciation.ToString("N2"));
                            table.Cell().Element(ValueStyle).Text(item.NetBookValue.ToString("N2"));
                            table.Cell().Element(ValueStyle).Text(item.Status.ToString());

                            static IContainer ValueStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();
        }

        throw new NotSupportedException($"Fixed asset PDF export type '{reportType}' is not supported by this legacy endpoint. Use CSV from the central finance report export service for fixed asset roll-forward and GL reconciliation exports.");
    }

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
        var dbQuery = _context.AssetDepreciationSchedules
            // Reversed schedules remain immutable source evidence, but current depreciation and
            // reconciliation reports must follow the net accounting position after correction.
            .Where(s => s.TenantId == TenantId && !s.IsDeleted && !s.IsReversed);

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
        var dbQuery = _context.AssetValuations
            .Where(v => v.TenantId == TenantId);

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
        var dbQuery = _context.AssetTransfers
            .Where(t => t.TenantId == TenantId);

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

        return dbQuery;
    }

    private IQueryable<AssetDisposal> BuildDisposalQuery(FixedAssetReportQueryDto query)
    {
        var dbQuery = _context.AssetDisposals
            .Where(d => d.TenantId == TenantId);

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
            dbQuery = dbQuery.Where(t => t.BookClassification == book);

        return await dbQuery.ToListAsync();
    }

    private static FixedAssetReportQueryDto BuildAsOfQuery(FixedAssetReportQueryDto query)
        => new()
        {
            ToDate = query.ToDate,
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

    private async Task ValidateFiltersAsync(FixedAssetReportQueryDto query)
    {
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
        => transaction.SourceDocumentType == SourceDocumentTypeDepreciationRun &&
           (transaction.SourceDocumentId == schedule.FixedAssetDepreciationRunId ||
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

    private static bool NotesContainId(string? notes, string key, Guid id)
        => !string.IsNullOrWhiteSpace(notes) &&
           notes.Contains($"{key}={id:N}", StringComparison.OrdinalIgnoreCase);

    private static decimal SubledgerAssetCarryingBalance(
        IReadOnlyCollection<FixedAsset> assets,
        IReadOnlyCollection<AssetValuation> valuations,
        IReadOnlyCollection<AssetDisposal> disposals,
        Guid categoryId)
    {
        var assetIds = assets.Where(a => a.FixedAssetCategoryId == categoryId).Select(a => a.Id).ToHashSet();
        var capitalizedCost = assets
            .Where(a => a.FixedAssetCategoryId == categoryId && a.Status is not FixedAssetStatus.Draft)
            .SelectMany(a => a.BookValues.Where(b => !b.IsDeleted))
            .Sum(b => b.AcquisitionCost);
        var revaluationAdjustment = valuations
            .Where(v => assetIds.Contains(v.FixedAssetId))
            .Sum(v => v.RevaluationSurplus - v.RevaluationDeficit);
        var disposedCarryingCost = disposals
            .Where(d => assetIds.Contains(d.FixedAssetId))
            .Sum(d => d.CostAtDisposal);

        return RoundMoney(capitalizedCost + revaluationAdjustment - disposedCarryingCost);
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
