using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class FixedAssetService : IFixedAssetService
    {
        private static readonly JsonSerializerOptions CapitalizationSnapshotJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IAccountingBookService? _accountingBookService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IWorkflowService? _workflowService;
        private readonly IFinanceReversalPolicyService? _financeReversalPolicyService;
        private readonly IFixedAssetDimensionService? _fixedAssetDimensions;

        private static readonly FinancePostingProducerContext DirectCapitalizationProducer =
            new(FinanceDimensionRouteId.FinanceFixedAssetCapitalization);
        private static readonly FinancePostingProducerContext CapitalizationReversalProducer =
            new(FinanceDimensionRouteId.FinanceFixedAssetCapitalizationReversal);

        public FixedAssetService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IAccountingBookService? accountingBookService = null,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowService? workflowService = null,
        IFinanceReversalPolicyService? financeReversalPolicyService = null,
        IFixedAssetDimensionService? fixedAssetDimensions = null)
    {
        _context = context;
        _currentUser = currentUser;
        _accountingBookService = accountingBookService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _workflowService = workflowService;
        _financeReversalPolicyService = financeReversalPolicyService;
        _fixedAssetDimensions = fixedAssetDimensions;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserGuid => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    private async Task<bool> AssetCodeExistsAsync(string assetCode, Guid? excludingAssetId = null)
    {
        var normalizedCode = assetCode.ToUpperInvariant();
        return await _context.FixedAssets.AnyAsync(a =>
            a.TenantId == TenantId &&
            (!excludingAssetId.HasValue || a.Id != excludingAssetId.Value) &&
            a.AssetCode.ToUpper() == normalizedCode);
    }

    public async Task<FixedAssetDto?> GetByIdAsync(Guid id)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(v => v.AccountingBook)
            .Where(a => a.TenantId == TenantId && a.Id == id)
            .FirstOrDefaultAsync();

        if (asset == null)
            return null;
        var result = MapToDto(asset);
        var snapshot = TryReadCapitalizationApprovalSnapshot(asset);
        if (_fixedAssetDimensions is not null && snapshot is not null
            && !string.Equals(snapshot.SourceDocumentType, "ProcurementFixedAssetCapitalization", StringComparison.Ordinal))
        {
            var sourceDocumentId = DirectCapitalizationDocumentId(asset);
            result.FinanceDimensions = await _fixedAssetDimensions.GetAsync(
                DirectCapitalizationProducer,
                sourceDocumentId,
                snapshot.CapitalizationDate,
                BuildDirectCapitalizationDimensionLines(
                    asset,
                    sourceDocumentId,
                    snapshot.DebitAccountId,
                    snapshot.CreditAccountId,
                    snapshot.TransactionAmount,
                    snapshot.TransactionCurrencyCode,
                    snapshot.FunctionalCurrencyCode,
                    snapshot.ExchangeRate,
                    snapshot.ExchangeRateId,
                    snapshot.ExchangeRateDate,
                    snapshot.Reference));
        }
        return result;
    }

    public async Task<IEnumerable<FixedAssetDto>> GetAllAsync()
    {
        var assets = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(v => v.AccountingBook)
            .Where(a => a.TenantId == TenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return assets.Select(MapToDto).ToList();
    }

    public async Task<FixedAssetDto> CreateAsync(CreateFixedAssetDto dto)
    {
        ValidateDepreciationConfiguration(
            dto.DepreciationMethod,
            dto.UsefulLifeMonths,
            dto.ResidualValue,
            dto.DiminishingBalanceRatePercent,
            dto.LifetimeProductionCapacity);
        var assetCode = NormalizeRequiredText(dto.AssetCode);
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            throw new InvalidOperationException("Asset code is required.");
        }

        if (await AssetCodeExistsAsync(assetCode))
        {
            throw new InvalidOperationException($"Asset code '{assetCode}' already exists.");
        }

        var assetName = NormalizeRequiredText(dto.Name);
        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new InvalidOperationException("Asset name is required.");
        }

        await ResolveAssetCategoryAsync(dto.FixedAssetCategoryId);

        var acquisitionCost = dto.AcquisitionCost
            ?? (dto.PurchasePrice + dto.InstallationCost + dto.TaxAmount);

        var books = await GetActivePostingBooksAsync();
        var defaultBook = GetDefaultBook(books);

        var asset = new FixedAsset
        {
            TenantId = TenantId,
            AssetCode = assetCode,
            Name = assetName,
            Description = NormalizeOptionalText(dto.Description),
            Location = NormalizeOptionalText(dto.Location),
            FixedAssetCategoryId = dto.FixedAssetCategoryId,
            PurchaseDate = dto.PurchaseDate,
            PlacedInServiceDate = dto.PlacedInServiceDate,
            PurchasePrice = dto.PurchasePrice,
            InstallationCost = dto.InstallationCost,
            TaxAmount = dto.TaxAmount,
            AcquisitionCost = acquisitionCost,
            NetBookValue = acquisitionCost,
            DepreciationMethod = dto.DepreciationMethod,
            DepreciationConvention = dto.DepreciationConvention,
            UsefulLifeMonths = dto.UsefulLifeMonths,
            ResidualValue = dto.ResidualValue,
            DiminishingBalanceRatePercent = RoundRate(dto.DiminishingBalanceRatePercent),
            LifetimeProductionCapacity = dto.LifetimeProductionCapacity,
            AccumulatedProductionUnits = 0m,
            MaintenanceAssetId = dto.MaintenanceAssetId,
            SerialNumber = dto.SerialNumber,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        foreach (var book in books)
        {
            asset.BookValues.Add(BuildBookValue(
                asset,
                book,
                acquisitionCost,
                accumulatedDepreciation: 0,
                netBookValue: acquisitionCost,
                dto.ResidualValue,
                dto.UsefulLifeMonths,
                remainingUsefulLifeMonths: dto.UsefulLifeMonths,
                dto.DepreciationMethod,
                dto.DepreciationConvention,
                dto.PlacedInServiceDate,
                openingAsOfDate: null,
                openingYtdDepreciation: 0,
                openingSource: "Acquisition"));
        }

        asset.AcquisitionCost = asset.BookValues
            .First(value => value.AccountingBookId == defaultBook.Id)
            .AcquisitionCost;
        asset.NetBookValue = asset.BookValues
            .First(value => value.AccountingBookId == defaultBook.Id)
            .NetBookValue;

        _context.FixedAssets.Add(asset);
        await _context.SaveChangesAsync();
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetCreated,
            asset,
            afterValues: new { asset.AssetCode, asset.Name, asset.FixedAssetCategoryId, asset.Status },
            comment: "Fixed asset draft/register item created.");

        return await GetByIdAsync(asset.Id) ?? throw new InvalidOperationException("Failed to create fixed asset.");
    }

    public async Task<FixedAssetDto> UpdateAsync(Guid id, UpdateFixedAssetDto dto)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.BookValues)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status == FixedAssetStatus.PendingApproval)
            throw new InvalidOperationException("A fixed asset pending capitalization approval cannot be edited. Reject or withdraw the request first.");
        if (asset.Status == FixedAssetStatus.Acquired &&
            asset.CapitalizationApprovalApprovedAt.HasValue &&
            !asset.CapitalizationApprovalInvalidatedAt.HasValue)
            throw new InvalidOperationException("An approved direct capitalization cannot be edited before posting. Invalidate and resubmit the approval evidence instead.");

        var resetsRejectedApproval = asset.Status == FixedAssetStatus.Rejected;
        var resetsReversedApproval = asset.Status == FixedAssetStatus.Draft &&
            asset.CapitalizationReversalPostingEventId.HasValue &&
            !string.IsNullOrWhiteSpace(asset.CapitalizationApprovalSnapshotHash);

        var assetCode = NormalizeRequiredText(dto.AssetCode);
        if (string.IsNullOrWhiteSpace(assetCode))
        {
            throw new InvalidOperationException("Asset code is required.");
        }

        if (await AssetCodeExistsAsync(assetCode, id))
        {
            throw new InvalidOperationException($"Asset code '{assetCode}' already exists.");
        }

        var assetName = NormalizeRequiredText(dto.Name);
        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new InvalidOperationException("Asset name is required.");
        }

        await ResolveAssetCategoryAsync(dto.FixedAssetCategoryId);

        var acquisitionCost = dto.AcquisitionCost
            ?? (dto.PurchasePrice + dto.InstallationCost + dto.TaxAmount);
        var isCapitalized = IsCapitalized(asset);
        ValidateDepreciationConfiguration(
            dto.DepreciationMethod,
            dto.UsefulLifeMonths,
            dto.ResidualValue,
            dto.DiminishingBalanceRatePercent,
            dto.LifetimeProductionCapacity);
        if (isCapitalized)
        {
            ValidateCapitalizedAssetUpdate(asset, dto, acquisitionCost);
        }

        asset.AssetCode = assetCode;
        asset.Name = assetName;
        asset.Description = NormalizeOptionalText(dto.Description);
        asset.Location = NormalizeOptionalText(dto.Location);
        if (!isCapitalized)
        {
            asset.FixedAssetCategoryId = dto.FixedAssetCategoryId;
            asset.PurchaseDate = dto.PurchaseDate;
            asset.PlacedInServiceDate = dto.PlacedInServiceDate;
            asset.PurchasePrice = dto.PurchasePrice;
            asset.InstallationCost = dto.InstallationCost;
            asset.TaxAmount = dto.TaxAmount;
            asset.AcquisitionCost = acquisitionCost;
            asset.DepreciationMethod = dto.DepreciationMethod;
            asset.DepreciationConvention = dto.DepreciationConvention;
            asset.UsefulLifeMonths = dto.UsefulLifeMonths;
            asset.ResidualValue = dto.ResidualValue;
            asset.DiminishingBalanceRatePercent = RoundRate(dto.DiminishingBalanceRatePercent);
            asset.LifetimeProductionCapacity = dto.LifetimeProductionCapacity;
            asset.AccumulatedProductionUnits = 0m;
            asset.Status = resetsRejectedApproval || resetsReversedApproval
                ? FixedAssetStatus.Draft
                : dto.Status;
            asset.DisposalDate = dto.DisposalDate;
        }
        asset.MaintenanceAssetId = dto.MaintenanceAssetId;
        asset.SerialNumber = dto.SerialNumber;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        if (resetsRejectedApproval || resetsReversedApproval)
            ClearCapitalizationApproval(asset);

        if (!isCapitalized && asset.Status == ErpSystem.Core.Enums.FixedAssetStatus.Draft)
        {
            var books = await GetActivePostingBooksAsync();
            foreach (var book in books)
            {
                var bookValue = asset.BookValues.FirstOrDefault(value => value.AccountingBookId == book.Id);
                if (bookValue == null)
                {
                    asset.BookValues.Add(BuildBookValue(
                        asset,
                        book,
                        acquisitionCost,
                        accumulatedDepreciation: 0,
                        netBookValue: acquisitionCost,
                        dto.ResidualValue,
                        dto.UsefulLifeMonths,
                        remainingUsefulLifeMonths: dto.UsefulLifeMonths,
                        dto.DepreciationMethod,
                        dto.DepreciationConvention,
                        dto.PlacedInServiceDate,
                        openingAsOfDate: null,
                        openingYtdDepreciation: 0,
                        openingSource: "Acquisition"));
                    continue;
                }

                bookValue.AcquisitionCost = acquisitionCost;
                bookValue.AccumulatedDepreciation = 0;
                bookValue.NetBookValue = acquisitionCost;
                bookValue.ResidualValue = dto.ResidualValue;
                bookValue.UsefulLifeMonths = dto.UsefulLifeMonths;
                bookValue.RemainingUsefulLifeMonths = dto.UsefulLifeMonths;
                bookValue.DepreciationMethod = dto.DepreciationMethod;
                bookValue.DepreciationConvention = dto.DepreciationConvention;
                bookValue.DiminishingBalanceRatePercent = RoundRate(dto.DiminishingBalanceRatePercent);
                bookValue.LifetimeProductionCapacity = dto.LifetimeProductionCapacity;
                bookValue.AccumulatedProductionUnits = 0m;
                bookValue.PlacedInServiceDate = dto.PlacedInServiceDate;
                bookValue.UpdatedAt = DateTime.UtcNow;
                bookValue.UpdatedBy = UserName;
            }

            asset.NetBookValue = acquisitionCost;
        }

        await _context.SaveChangesAsync();
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetUpdated,
            asset,
            afterValues: new { asset.AssetCode, asset.Name, asset.Status },
            comment: isCapitalized
                ? "Fixed asset metadata updated; capitalized accounting fields were preserved."
                : "Fixed asset updated.");

        return await GetByIdAsync(asset.Id) ?? throw new InvalidOperationException("Failed to update fixed asset.");
    }

    public async Task DeleteAsync(Guid id)
    {
        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (IsCapitalized(asset))
        {
            throw new InvalidOperationException("Capitalized fixed assets cannot be deleted. Use a controlled reversal or disposal workflow.");
        }
        if (asset.Status == FixedAssetStatus.PendingApproval ||
            (asset.Status == FixedAssetStatus.Acquired && asset.CapitalizationApprovalApprovedAt.HasValue))
        {
            throw new InvalidOperationException("Fixed assets with pending or approved capitalization evidence cannot be deleted.");
        }

        _context.FixedAssets.Remove(asset);
        await _context.SaveChangesAsync();
    }

    private static FixedAssetDto MapToDto(FixedAsset asset)
    {
        return new FixedAssetDto
        {
            Id = asset.Id,
            AssetCode = asset.AssetCode,
            Name = asset.Name,
            Description = asset.Description,
            Location = asset.Location,
            CurrentCustodianId = asset.CurrentCustodianId,
            CurrentCustodianName = asset.CurrentCustodian != null ? $"{asset.CurrentCustodian.FirstName} {asset.CurrentCustodian.LastName}" : null,
            CurrentSegmentString = asset.CurrentSegmentString,
            CurrentSegmentLookupValueId = asset.CurrentSegmentLookupValueId,
            FixedAssetCategoryId = asset.FixedAssetCategoryId,
            FixedAssetCategoryName = asset.Category?.Name,
            PurchaseDate = asset.PurchaseDate,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            PurchasePrice = asset.PurchasePrice,
            InstallationCost = asset.InstallationCost,
            TaxAmount = asset.TaxAmount,
            CapitalizationDate = asset.CapitalizationDate,
            AcquisitionCost = asset.AcquisitionCost,
            NetBookValue = asset.NetBookValue,
            DepreciationMethod = asset.DepreciationMethod,
            DepreciationConvention = asset.DepreciationConvention,
            UsefulLifeMonths = asset.UsefulLifeMonths,
            ResidualValue = asset.ResidualValue,
            DiminishingBalanceRatePercent = asset.DiminishingBalanceRatePercent,
            LifetimeProductionCapacity = asset.LifetimeProductionCapacity,
            AccumulatedProductionUnits = asset.AccumulatedProductionUnits,
            Status = asset.Status,
            DisposalDate = asset.DisposalDate,
            FunctionalCurrencyCode = asset.FunctionalCurrencyCode,
            TransactionCurrencyCode = asset.TransactionCurrencyCode,
            ExchangeRate = asset.ExchangeRate,
            ExchangeRateId = asset.ExchangeRateId,
            ExchangeRateDate = asset.ExchangeRateDate,
            SourceDocumentType = asset.SourceDocumentType,
            SourceDocumentId = asset.SourceDocumentId,
            SourceDocumentLineId = asset.SourceDocumentLineId,
            JournalEntryId = asset.JournalEntryId,
            PostingEventId = asset.PostingEventId,
            CapitalizedAt = asset.CapitalizedAt,
            CapitalizationApprovalSnapshot = TryReadCapitalizationApprovalSnapshot(asset),
            CapitalizationApprovalSnapshotHash = asset.CapitalizationApprovalSnapshotHash,
            CapitalizationApprovalWorkflowInstanceId = asset.CapitalizationApprovalWorkflowInstanceId,
            CapitalizationApprovalSubmittedByUserId = asset.CapitalizationApprovalSubmittedByUserId,
            CapitalizationApprovalSubmittedAt = asset.CapitalizationApprovalSubmittedAt,
            CapitalizationApprovalApprovedByUserId = asset.CapitalizationApprovalApprovedByUserId,
            CapitalizationApprovalApprovedAt = asset.CapitalizationApprovalApprovedAt,
            CapitalizationApprovalInvalidatedAt = asset.CapitalizationApprovalInvalidatedAt,
            CapitalizationApprovalInvalidationReason = asset.CapitalizationApprovalInvalidationReason,
            CapitalizationReversalJournalEntryId = asset.CapitalizationReversalJournalEntryId,
            CapitalizationReversalPostingEventId = asset.CapitalizationReversalPostingEventId,
            CapitalizationReversedAt = asset.CapitalizationReversedAt,
            CapitalizationReversalReason = asset.CapitalizationReversalReason,
            MaintenanceAssetId = asset.MaintenanceAssetId,
            SerialNumber = asset.SerialNumber,
            CreatedAt = asset.CreatedAt,
            CreatedBy = asset.CreatedBy,
            UpdatedAt = asset.UpdatedAt,
            UpdatedBy = asset.UpdatedBy,
            BookValues = asset.BookValues
                .Where(value => !value.IsDeleted)
                .OrderBy(value => value.AccountingBook?.SortOrder ?? int.MaxValue)
                .ThenBy(value => value.BookClassification)
                .Select(MapBookValueToDto)
                .ToList()
        };
    }

    private static FixedAssetBookValueDto MapBookValueToDto(FixedAssetBookValue value)
    {
        return new FixedAssetBookValueDto
        {
            Id = value.Id,
            FixedAssetId = value.FixedAssetId,
            AccountingBookId = value.AccountingBookId,
            BookClassification = value.BookClassification,
            AccountingBookName = value.AccountingBook?.Name,
            AcquisitionCost = value.AcquisitionCost,
            AccumulatedDepreciation = value.AccumulatedDepreciation,
            NetBookValue = value.NetBookValue,
            ResidualValue = value.ResidualValue,
            UsefulLifeMonths = value.UsefulLifeMonths,
            RemainingUsefulLifeMonths = value.RemainingUsefulLifeMonths,
            DepreciationMethod = value.DepreciationMethod,
            DepreciationConvention = value.DepreciationConvention,
            DiminishingBalanceRatePercent = value.DiminishingBalanceRatePercent,
            LifetimeProductionCapacity = value.LifetimeProductionCapacity,
            AccumulatedProductionUnits = value.AccumulatedProductionUnits,
            PlacedInServiceDate = value.PlacedInServiceDate,
            OpeningAsOfDate = value.OpeningAsOfDate,
            OpeningYtdDepreciation = value.OpeningYtdDepreciation,
            LastDepreciationDate = value.LastDepreciationDate,
            OpeningPostedToGl = value.OpeningPostedToGl,
            OpeningPostedDate = value.OpeningPostedDate,
            OpeningSource = value.OpeningSource,
            CapitalizationDate = value.CapitalizationDate,
            CapitalizationJournalEntryId = value.CapitalizationJournalEntryId,
            CapitalizationPostingEventId = value.CapitalizationPostingEventId,
            CapitalizationReversalJournalEntryId = value.CapitalizationReversalJournalEntryId,
            CapitalizationReversalPostingEventId = value.CapitalizationReversalPostingEventId,
            CapitalizationReversedAt = value.CapitalizationReversedAt,
            SourceDocumentType = value.SourceDocumentType,
            SourceDocumentId = value.SourceDocumentId,
            SourceDocumentLineId = value.SourceDocumentLineId
        };
    }

    private async Task<List<AccountingBook>> GetActivePostingBooksAsync(bool persistFallback = true)
    {
        if (_accountingBookService != null)
        {
            await _accountingBookService.EnsureTenantDefaultsAsync();
        }

        var books = await _context.AccountingBooks
            .Where(book => book.TenantId == TenantId && !book.IsDeleted && book.IsActive && book.AllowsPosting)
            .OrderBy(book => book.SortOrder)
            .ThenBy(book => book.Name)
            .ToListAsync();

        if (books.Count > 0)
        {
            return books;
        }

        var fallbackBook = new AccountingBook
        {
            TenantId = TenantId,
            Code = "IFRS",
            Name = "IFRS",
            Description = "Primary corporate reporting book for IFRS financial statements.",
            Purpose = "Primary",
            IsActive = true,
            IsDefault = true,
            AllowsPosting = true,
            IsSystemDefined = true,
            SortOrder = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        if (persistFallback)
        {
            _context.AccountingBooks.Add(fallbackBook);
            await _context.SaveChangesAsync();
            // Do not clear the shared tracker here. This helper runs inside asset acquisition and
            // capitalization workflows; clearing it detaches the caller's FixedAsset immediately
            // before register/book values are updated, producing a successful response without a
            // persisted register change. The saved fallback book can safely remain tracked for the
            // remainder of the unit of work.
        }

        return new List<AccountingBook> { fallbackBook };
    }

    private static AccountingBook GetDefaultBook(IReadOnlyList<AccountingBook> books)
    {
        return books.FirstOrDefault(book => book.IsDefault) ?? books.First();
    }

    private static string NormalizeBookCode(string? value)
    {
        return NormalizeRequiredText(value).ToUpperInvariant();
    }

    private static FixedAssetBookValue BuildBookValue(
        FixedAsset asset,
        AccountingBook book,
        decimal acquisitionCost,
        decimal accumulatedDepreciation,
        decimal netBookValue,
        decimal residualValue,
        int usefulLifeMonths,
        int? remainingUsefulLifeMonths,
        DepreciationMethod depreciationMethod,
        DepreciationConvention depreciationConvention,
        DateTime? placedInServiceDate,
        DateTime? openingAsOfDate,
        decimal openingYtdDepreciation,
        string openingSource)
    {
        return new FixedAssetBookValue
        {
            TenantId = asset.TenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = book.Id,
            BookClassification = book.Code,
            AcquisitionCost = acquisitionCost,
            AccumulatedDepreciation = accumulatedDepreciation,
            NetBookValue = netBookValue,
            ResidualValue = residualValue,
            UsefulLifeMonths = usefulLifeMonths,
            RemainingUsefulLifeMonths = remainingUsefulLifeMonths,
            DepreciationMethod = depreciationMethod,
            DepreciationConvention = depreciationConvention,
            DiminishingBalanceRatePercent = asset.DiminishingBalanceRatePercent,
            LifetimeProductionCapacity = asset.LifetimeProductionCapacity,
            AccumulatedProductionUnits = asset.AccumulatedProductionUnits,
            PlacedInServiceDate = placedInServiceDate,
            OpeningAsOfDate = openingAsOfDate,
            OpeningYtdDepreciation = openingYtdDepreciation,
            OpeningSource = openingSource,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = asset.CreatedBy
        };
    }

    public async Task<BulkImportResultDto> ImportAssetsFromExcelAsync(Stream fileStream, string fileName, bool dryRun = false)
    {
        var result = new BulkImportResultDto { IsDryRun = dryRun };
        var rowsToImport = new List<ValidatedAssetImportRow>();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet?.LastCellUsed() == null)
        {
            result.Errors.Add(new BulkImportErrorDto
            {
                RowNumber = 0,
                Field = "File",
                Error = "Excel file is empty or has no worksheets."
            });
            UpdateErrorCount(result);
            return result;
        }

        var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (rowCount < 2)
        {
            result.Errors.Add(new BulkImportErrorDto
            {
                RowNumber = 0,
                Field = "File",
                Error = "Excel file is empty or has no data rows."
            });
            UpdateErrorCount(result);
            return result;
        }

        var headerMap = BuildHeaderMap(worksheet);
        var assetCodeColumn = FindColumn(headerMap, "Asset Code", "Asset Number", "Asset No", "Code");
        var nameColumn = FindColumn(headerMap, "Name", "Asset Name");
        var descriptionColumn = FindColumn(headerMap, "Description", "Asset Description");
        var locationColumn = FindColumn(headerMap, "Location", "Asset Location", "Current Location");
        var categoryColumn = FindColumn(headerMap, "Category Code", "Category");
        var purchaseDateColumn = FindColumn(headerMap, "Purchase Date", "Acquisition Date", "Acquired Date");
        var placedInServiceColumn = FindColumn(headerMap, "Placed In Service Date", "Placed In Service", "Service Date");
        var bookCodeColumn = FindColumn(headerMap, "Book Code", "Accounting Book", "Book Classification", "Book");
        var purchasePriceColumn = FindColumn(headerMap, "Purchase Price", "Cost", "Asset Cost");
        var installationCostColumn = FindColumn(headerMap, "Installation Cost", "Installation");
        var taxAmountColumn = FindColumn(headerMap, "Tax Amount", "Tax", "VAT");
        var accumulatedDepreciationColumn = FindColumn(headerMap, "Accumulated Depreciation", "Depreciation Reserve", "Opening Accumulated Depreciation", "Accum Depreciation");
        var netBookValueColumn = FindColumn(headerMap, "Net Book Value", "NBV", "Opening NBV");
        var openingAsOfDateColumn = FindColumn(headerMap, "Opening As Of Date", "Opening Date", "Depreciation As Of Date");
        var openingYtdDepreciationColumn = FindColumn(headerMap, "YTD Depreciation", "Opening YTD Depreciation", "Year To Date Depreciation");
        var remainingUsefulLifeColumn = FindColumn(headerMap, "Remaining Useful Life (Months)", "Remaining Useful Life Months", "Remaining Life Months");
        var usefulLifeColumn = FindColumn(headerMap, "Useful Life (Months)", "Useful Life Months", "Useful Life");
        var residualValueColumn = FindColumn(headerMap, "Residual Value", "Salvage Value");
        var serialNumberColumn = FindColumn(headerMap, "Serial Number", "Serial No", "Serial");
        var statusColumn = FindColumn(headerMap, "Status", "Asset Status");

        AddMissingHeaderError(result, assetCodeColumn, "Asset Code");
        AddMissingHeaderError(result, nameColumn, "Name");
        AddMissingHeaderError(result, categoryColumn, "Category Code");
        AddMissingHeaderError(result, purchaseDateColumn, "Purchase Date");
        AddMissingHeaderError(result, purchasePriceColumn, "Purchase Price");
        AddMissingHeaderError(result, usefulLifeColumn, "Useful Life (Months)");

        if (result.Errors.Any())
        {
            UpdateErrorCount(result);
            return result;
        }

        var categoryRows = await _context.FixedAssetCategories
            .Where(c => c.TenantId == TenantId)
            .Select(c => new { c.Code, c.Name, c.Id })
            .ToListAsync();

        var categories = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categoryRows)
        {
            AddLookupValue(categories, category.Code, category.Id);
            AddLookupValue(categories, category.Name, category.Id);
        }

        var activeBooks = await GetActivePostingBooksAsync(persistFallback: !dryRun);
        var booksByCode = activeBooks
            .GroupBy(book => NormalizeBookCode(book.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        // Load existing asset codes for duplicate check
        var existingCodesList = await _context.FixedAssets
            .Where(a => a.TenantId == TenantId)
            .Select(a => a.AssetCode)
            .ToListAsync();
        var existingCodes = new HashSet<string>(
            existingCodesList.Select(NormalizeRequiredText),
            StringComparer.OrdinalIgnoreCase);
        var importBookKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Parse and validate each row
        for (int row = 2; row <= rowCount; row++)
        {
            if (IsRowEmpty(worksheet, row))
            {
                continue;
            }

            result.TotalRows++;

            var rawStatus = GetCellText(worksheet, row, statusColumn);
            var parsedStatus = ParseStatus(rawStatus);
            var rowData = new BulkAssetImportRowDto
            {
                AssetCode = NormalizeRequiredText(GetCellText(worksheet, row, assetCodeColumn)),
                Name = NormalizeRequiredText(GetCellText(worksheet, row, nameColumn)),
                Description = GetCellText(worksheet, row, descriptionColumn),
                Location = GetCellText(worksheet, row, locationColumn),
                CategoryCode = NormalizeRequiredText(GetCellText(worksheet, row, categoryColumn)),
                PurchaseDate = ParseDate(worksheet, row, purchaseDateColumn),
                PlacedInServiceDate = ParseDate(worksheet, row, placedInServiceColumn),
                BookCode = NormalizeOptionalText(GetCellText(worksheet, row, bookCodeColumn)),
                PurchasePrice = ParseDecimal(worksheet, row, purchasePriceColumn),
                InstallationCost = ParseDecimal(worksheet, row, installationCostColumn),
                TaxAmount = ParseDecimal(worksheet, row, taxAmountColumn),
                AccumulatedDepreciation = ParseDecimal(worksheet, row, accumulatedDepreciationColumn),
                NetBookValue = ParseDecimal(worksheet, row, netBookValueColumn),
                OpeningAsOfDate = ParseDate(worksheet, row, openingAsOfDateColumn),
                OpeningYtdDepreciation = ParseDecimal(worksheet, row, openingYtdDepreciationColumn),
                RemainingUsefulLifeMonths = ParseInt(worksheet, row, remainingUsefulLifeColumn),
                UsefulLifeMonths = ParseInt(worksheet, row, usefulLifeColumn),
                ResidualValue = ParseDecimal(worksheet, row, residualValueColumn),
                SerialNumber = GetCellText(worksheet, row, serialNumberColumn),
                RawStatus = rawStatus,
                Status = parsedStatus
            };

            var targetBooks = ResolveTargetBooks(rowData.BookCode, activeBooks, booksByCode);
            var rowErrors = ValidateRow(row, rowData, categories, existingCodes, importBookKeys, targetBooks);
            
            if (rowErrors.Any())
            {
                result.Errors.AddRange(rowErrors);
            }
            else
            {
                rowsToImport.Add(new ValidatedAssetImportRow(row, rowData, categories[rowData.CategoryCode], targetBooks));
            }
        }

        result.Errors.AddRange(ValidateRepeatedAssetMasterRows(rowsToImport));

        UpdateErrorCount(result);
        result.SuccessCount = rowsToImport
            .Select(r => r.Data.AssetCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        result.SuccessfulAssetCodes.AddRange(rowsToImport
            .Select(r => r.Data.AssetCode)
            .Distinct(StringComparer.OrdinalIgnoreCase));

        if (dryRun || result.Errors.Any())
        {
            if (result.Errors.Any())
            {
                result.SuccessCount = dryRun
                    ? rowsToImport.Select(r => r.Data.AssetCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    : 0;
                if (!dryRun)
                {
                    result.SuccessfulAssetCodes.Clear();
                }
            }

            return result;
        }

        // Import valid rows. All validation errors are returned above before any data is saved.
        if (rowsToImport.Any())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                result.SuccessfulAssetCodes.Clear();
                var defaultBook = GetDefaultBook(activeBooks);
                foreach (var assetGroup in rowsToImport.GroupBy(row => row.Data.AssetCode, StringComparer.OrdinalIgnoreCase))
                {
                    var firstRow = assetGroup.First();
                    var summaryRow = assetGroup.FirstOrDefault(row =>
                        row.TargetBooks.Any(book => book.Id == defaultBook.Id)) ?? firstRow;
                    var summaryValues = CalculateOpeningValues(summaryRow.Data);
                    var data = firstRow.Data;
                    var summaryData = summaryRow.Data;

                    var asset = new FixedAsset
                    {
                        TenantId = TenantId,
                        AssetCode = data.AssetCode,
                        Name = data.Name,
                        Description = data.Description,
                        Location = data.Location,
                        FixedAssetCategoryId = firstRow.CategoryId,
                        PurchaseDate = data.PurchaseDate!.Value,
                        PlacedInServiceDate = data.PlacedInServiceDate ?? data.PurchaseDate,
                        PurchasePrice = summaryData.PurchasePrice ?? 0,
                        InstallationCost = summaryData.InstallationCost ?? 0,
                        TaxAmount = summaryData.TaxAmount ?? 0,
                        AcquisitionCost = summaryValues.AcquisitionCost,
                        NetBookValue = summaryValues.NetBookValue,
                        DepreciationMethod = ErpSystem.Core.Enums.DepreciationMethod.StraightLine,
                        DepreciationConvention = ErpSystem.Core.Enums.DepreciationConvention.FullMonth,
                        UsefulLifeMonths = summaryData.UsefulLifeMonths ?? 36,
                        ResidualValue = summaryData.ResidualValue ?? 0,
                        SerialNumber = data.SerialNumber,
                        Status = data.Status ?? ErpSystem.Core.Enums.FixedAssetStatus.Draft,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };

                    foreach (var importRow in assetGroup)
                    {
                        var openingValues = CalculateOpeningValues(importRow.Data);
                        foreach (var book in importRow.TargetBooks)
                        {
                            var performedByUserId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty;

                            asset.BookValues.Add(BuildBookValue(
                                asset,
                                book,
                                openingValues.AcquisitionCost,
                                openingValues.AccumulatedDepreciation,
                                openingValues.NetBookValue,
                                openingValues.ResidualValue,
                                openingValues.UsefulLifeMonths,
                                openingValues.RemainingUsefulLifeMonths,
                                asset.DepreciationMethod,
                                asset.DepreciationConvention,
                                asset.PlacedInServiceDate,
                                importRow.Data.OpeningAsOfDate,
                                importRow.Data.OpeningYtdDepreciation ?? 0,
                                openingValues.AccumulatedDepreciation > 0 ? "OpeningImport" : "Acquisition"));

                            _context.AssetTransactions.Add(new AssetTransaction
                            {
                                TenantId = TenantId,
                                FixedAssetId = asset.Id,
                                AccountingBookId = book.Id,
                                BookClassification = book.Code,
                                TransactionDate = importRow.Data.OpeningAsOfDate ?? asset.PlacedInServiceDate ?? asset.PurchaseDate,
                                TransactionType = "Opening Acquisition",
                                Description = $"Opening acquisition cost imported for {book.Code}",
                                Amount = openingValues.AcquisitionCost,
                                ResultingBookValue = openingValues.AcquisitionCost,
                                PerformedByUserId = performedByUserId,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = UserName
                            });

                            if (openingValues.AccumulatedDepreciation > 0)
                            {
                                _context.AssetTransactions.Add(new AssetTransaction
                                {
                                    TenantId = TenantId,
                                    FixedAssetId = asset.Id,
                                    AccountingBookId = book.Id,
                                    BookClassification = book.Code,
                                    TransactionDate = importRow.Data.OpeningAsOfDate ?? asset.PlacedInServiceDate ?? asset.PurchaseDate,
                                    TransactionType = "Opening Accumulated Depreciation",
                                    Description = $"Opening accumulated depreciation imported for {book.Code}",
                                    Amount = openingValues.AccumulatedDepreciation,
                                    ResultingBookValue = openingValues.NetBookValue,
                                    PerformedByUserId = performedByUserId,
                                    CreatedAt = DateTime.UtcNow,
                                    CreatedBy = UserName
                                });
                            }
                        }
                    }

                    _context.FixedAssets.Add(asset);
                    result.SuccessfulAssetCodes.Add(data.AssetCode);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                result.SuccessCount = result.SuccessfulAssetCodes.Count;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result.Errors.Add(new BulkImportErrorDto
                {
                    RowNumber = 0,
                    Field = "Database",
                    Error = $"Failed to save assets: {ex.Message}"
                });
                UpdateErrorCount(result);
                result.SuccessCount = 0;
                result.SuccessfulAssetCodes.Clear();
            }
        }

        UpdateErrorCount(result);
        return result;
    }

    public async Task<byte[]> GenerateImportTemplateAsync()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Assets");

        // Headers
        var headers = new[]
        {
            "Asset Code*", "Name*", "Description", "Location", "Category Code*",
            "Purchase Date*", "Placed In Service Date", "Book Code", "Purchase Price*", "Installation Cost",
            "Tax Amount", "Accumulated Depreciation", "Net Book Value", "Opening As Of Date",
            "YTD Depreciation", "Remaining Useful Life (Months)", "Useful Life (Months)*",
            "Residual Value", "Serial Number", "Status"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        // Sample data row
        worksheet.Cell(2, 1).Value = "FA-2024-001";
        worksheet.Cell(2, 2).Value = "Dell Laptop";
        worksheet.Cell(2, 3).Value = "Core i7, 16GB RAM";
        worksheet.Cell(2, 4).Value = "Head Office - IT Room";
        worksheet.Cell(2, 5).Value = "COMP-HW";
        worksheet.Cell(2, 6).Value = new DateTime(2024, 1, 15);
        worksheet.Cell(2, 7).Value = new DateTime(2024, 1, 15);
        worksheet.Cell(2, 8).Value = "IFRS";
        worksheet.Cell(2, 9).Value = 1500.00;
        worksheet.Cell(2, 10).Value = 50.00;
        worksheet.Cell(2, 11).Value = 195.00;
        worksheet.Cell(2, 12).Value = 250.00;
        worksheet.Cell(2, 13).Value = 1495.00;
        worksheet.Cell(2, 14).Value = DateTime.UtcNow.Date;
        worksheet.Cell(2, 15).Value = 40.00;
        worksheet.Cell(2, 16).Value = 30;
        worksheet.Cell(2, 17).Value = 36;
        worksheet.Cell(2, 18).Value = 100.00;
        worksheet.Cell(2, 19).Value = "SN123456";
        worksheet.Cell(2, 20).Value = "Draft";

        worksheet.Range(2, 6, 2, 7).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Cell(2, 14).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Range(2, 9, 2, 15).Style.NumberFormat.Format = "#,##0.00";
        worksheet.SheetView.FreezeRows(1);

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Fixed Asset Import Instructions";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(3, 1).Value = "Required fields";
        instructions.Cell(3, 2).Value = "Asset Code, Name, Category Code, Purchase Date, Purchase Price, Useful Life (Months)";
        instructions.Cell(4, 1).Value = "Location";
        instructions.Cell(4, 2).Value = "Optional finance-owned asset location. This does not depend on the Maintenance module.";
        instructions.Cell(5, 1).Value = "Status";
        instructions.Cell(5, 2).Value = "Optional. Supported values include Draft, Pending Approval, Rejected, Active, Fully Depreciated, Disposed, Held for Sale, Written Off, Under Construction, On Hold.";
        instructions.Cell(6, 1).Value = "Dates";
        instructions.Cell(6, 2).Value = "Use yyyy-mm-dd, or a valid Excel date cell.";
        instructions.Cell(7, 1).Value = "Category Code";
        instructions.Cell(7, 2).Value = "Use an existing fixed asset category code from the Categories sheet.";
        instructions.Cell(8, 1).Value = "Book Code";
        instructions.Cell(8, 2).Value = "Optional. Leave blank or use ALL_ACTIVE_BOOKS to import the same values to all active books; use IFRS, LOCAL_STATUTORY, or MANAGEMENT for book-specific rows.";
        instructions.Cell(9, 1).Value = "Opening values";
        instructions.Cell(9, 2).Value = "Accumulated Depreciation and Net Book Value are optional, but if both are supplied NBV must equal acquisition cost less accumulated depreciation.";
        instructions.Cell(10, 1).Value = "Repeated asset codes";
        instructions.Cell(10, 2).Value = "Allowed only for book-specific rows. Master data must match across rows for the same asset code.";
        instructions.ColumnsUsed().AdjustToContents();

        var categoriesSheet = workbook.Worksheets.Add("Categories");
        categoriesSheet.Cell(1, 1).Value = "Category Code";
        categoriesSheet.Cell(1, 2).Value = "Category Name";
        categoriesSheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

        var categories = await _context.FixedAssetCategories
            .Where(c => c.TenantId == TenantId)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Code, c.Name })
            .ToListAsync();

        for (int i = 0; i < categories.Count; i++)
        {
            categoriesSheet.Cell(i + 2, 1).Value = categories[i].Code;
            categoriesSheet.Cell(i + 2, 2).Value = categories[i].Name;
        }

        categoriesSheet.ColumnsUsed().AdjustToContents();

        var booksSheet = workbook.Worksheets.Add("Accounting Books");
        booksSheet.Cell(1, 1).Value = "Book Code";
        booksSheet.Cell(1, 2).Value = "Book Name";
        booksSheet.Cell(1, 3).Value = "Default";
        booksSheet.Range(1, 1, 1, 3).Style.Font.Bold = true;

        var books = await GetActivePostingBooksAsync();
        for (int i = 0; i < books.Count; i++)
        {
            booksSheet.Cell(i + 2, 1).Value = books[i].Code;
            booksSheet.Cell(i + 2, 2).Value = books[i].Name;
            booksSheet.Cell(i + 2, 3).Value = books[i].IsDefault ? "Yes" : "No";
        }

        booksSheet.ColumnsUsed().AdjustToContents();

        // Auto-fit columns
        worksheet.ColumnsUsed().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record ValidatedAssetImportRow(
        int RowNumber,
        BulkAssetImportRowDto Data,
        Guid CategoryId,
        IReadOnlyList<AccountingBook> TargetBooks);

    private readonly record struct OpeningImportValues(
        decimal AcquisitionCost,
        decimal AccumulatedDepreciation,
        decimal NetBookValue,
        decimal ResidualValue,
        int UsefulLifeMonths,
        int? RemainingUsefulLifeMonths);

    private static IReadOnlyList<AccountingBook> ResolveTargetBooks(
        string? bookCode,
        IReadOnlyList<AccountingBook> activeBooks,
        IReadOnlyDictionary<string, AccountingBook> booksByCode)
    {
        var normalizedBookCode = NormalizeBookCode(bookCode);
        if (string.IsNullOrWhiteSpace(normalizedBookCode) || normalizedBookCode == "ALL_ACTIVE_BOOKS")
        {
            return activeBooks;
        }

        return booksByCode.TryGetValue(normalizedBookCode, out var book)
            ? new List<AccountingBook> { book }
            : Array.Empty<AccountingBook>();
    }

    private static OpeningImportValues CalculateOpeningValues(BulkAssetImportRowDto data)
    {
        var acquisitionCost = (data.PurchasePrice ?? 0) + (data.InstallationCost ?? 0) + (data.TaxAmount ?? 0);
        var accumulatedDepreciation = data.AccumulatedDepreciation
            ?? (data.NetBookValue.HasValue ? acquisitionCost - data.NetBookValue.Value : 0);
        var netBookValue = data.NetBookValue ?? acquisitionCost - accumulatedDepreciation;
        var usefulLifeMonths = data.UsefulLifeMonths ?? 36;

        return new OpeningImportValues(
            acquisitionCost,
            accumulatedDepreciation,
            netBookValue,
            data.ResidualValue ?? 0,
            usefulLifeMonths,
            data.RemainingUsefulLifeMonths ?? usefulLifeMonths);
    }

    private static List<BulkImportErrorDto> ValidateRepeatedAssetMasterRows(IEnumerable<ValidatedAssetImportRow> rows)
    {
        var errors = new List<BulkImportErrorDto>();
        foreach (var group in rows.GroupBy(row => row.Data.AssetCode, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var firstSignature = BuildMasterSignature(first.Data);
            foreach (var row in group.Skip(1))
            {
                if (BuildMasterSignature(row.Data) == firstSignature)
                {
                    continue;
                }

                errors.Add(new BulkImportErrorDto
                {
                    RowNumber = row.RowNumber,
                    AssetCode = row.Data.AssetCode,
                    Field = "Asset Code",
                    Error = "Rows for the same asset code must share master data; book valuation and opening value columns may differ by book."
                });
            }
        }

        return errors;
    }

    private static string BuildMasterSignature(BulkAssetImportRowDto data)
    {
        return string.Join("|",
            NormalizeRequiredText(data.AssetCode).ToUpperInvariant(),
            NormalizeRequiredText(data.Name).ToUpperInvariant(),
            NormalizeRequiredText(data.Description).ToUpperInvariant(),
            NormalizeRequiredText(data.Location).ToUpperInvariant(),
            NormalizeRequiredText(data.CategoryCode).ToUpperInvariant(),
            data.PurchaseDate?.Date.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            data.PlacedInServiceDate?.Date.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            NormalizeRequiredText(data.SerialNumber).ToUpperInvariant(),
            data.Status?.ToString() ?? string.Empty);
    }

    private List<BulkImportErrorDto> ValidateRow(
        int rowNumber,
        BulkAssetImportRowDto data,
        Dictionary<string, Guid> categories,
        HashSet<string> existingCodes,
        HashSet<string> importBookKeys,
        IReadOnlyList<AccountingBook> targetBooks)
    {
        var errors = new List<BulkImportErrorDto>();

        if (string.IsNullOrWhiteSpace(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Required" });
        else if (data.AssetCode.Length > 50)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Cannot exceed 50 characters" });
        else if (existingCodes.Contains(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Duplicate asset code" });

        if (targetBooks.Count == 0)
        {
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Book Code", Error = "Accounting book not found" });
        }
        else if (!string.IsNullOrWhiteSpace(data.AssetCode))
        {
            foreach (var book in targetBooks)
            {
                var importKey = $"{NormalizeRequiredText(data.AssetCode).ToUpperInvariant()}|{NormalizeBookCode(book.Code)}";
                if (!importBookKeys.Add(importKey))
                {
                    errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Book Code", Error = $"Duplicate asset/book combination for {book.Code}" });
                }
            }
        }

        if (string.IsNullOrWhiteSpace(data.Name))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Name", Error = "Required" });
        else if (data.Name.Length > 200)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Name", Error = "Cannot exceed 200 characters" });

        if (!string.IsNullOrWhiteSpace(data.Description) && data.Description.Length > 1000)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Description", Error = "Cannot exceed 1000 characters" });

        if (!string.IsNullOrWhiteSpace(data.Location) && data.Location.Length > 500)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Location", Error = "Cannot exceed 500 characters" });

        if (string.IsNullOrWhiteSpace(data.CategoryCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Category Code", Error = "Required" });
        else if (!categories.ContainsKey(data.CategoryCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Category Code", Error = "Category not found" });

        if (!data.PurchaseDate.HasValue)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Purchase Date", Error = "Required or invalid date format" });
        else if (data.PurchaseDate.Value.Date > DateTime.UtcNow.Date)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Purchase Date", Error = "Cannot be in the future" });

        if (data.PlacedInServiceDate.HasValue && data.PlacedInServiceDate.Value.Date > DateTime.UtcNow.Date)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Placed In Service Date", Error = "Cannot be in the future" });

        if (data.PlacedInServiceDate.HasValue && data.PurchaseDate.HasValue && data.PlacedInServiceDate.Value.Date < data.PurchaseDate.Value.Date)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Placed In Service Date", Error = "Cannot be before purchase date" });

        if (!data.PurchasePrice.HasValue || data.PurchasePrice.Value <= 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Purchase Price", Error = "Required and must be greater than 0" });

        if (data.InstallationCost.HasValue && data.InstallationCost.Value < 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Installation Cost", Error = "Cannot be negative" });

        if (data.TaxAmount.HasValue && data.TaxAmount.Value < 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Tax Amount", Error = "Cannot be negative" });

        if (!data.UsefulLifeMonths.HasValue || data.UsefulLifeMonths.Value <= 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Useful Life", Error = "Required and must be greater than 0" });

        if (data.ResidualValue.HasValue && data.ResidualValue.Value < 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Residual Value", Error = "Cannot be negative" });

        if (data.AccumulatedDepreciation.HasValue && data.AccumulatedDepreciation.Value < 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Accumulated Depreciation", Error = "Cannot be negative" });

        if (data.NetBookValue.HasValue && data.NetBookValue.Value < 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Net Book Value", Error = "Cannot be negative" });

        if (data.OpeningYtdDepreciation.HasValue && data.OpeningYtdDepreciation.Value < 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "YTD Depreciation", Error = "Cannot be negative" });

        if (data.RemainingUsefulLifeMonths.HasValue && data.RemainingUsefulLifeMonths.Value <= 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Remaining Useful Life", Error = "Must be greater than 0" });

        if (data.UsefulLifeMonths.HasValue && data.RemainingUsefulLifeMonths.HasValue && data.RemainingUsefulLifeMonths.Value > data.UsefulLifeMonths.Value)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Remaining Useful Life", Error = "Cannot exceed useful life" });

        if (data.PurchasePrice.HasValue)
        {
            var openingValues = CalculateOpeningValues(data);

            if (openingValues.AccumulatedDepreciation > openingValues.AcquisitionCost)
                errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Accumulated Depreciation", Error = "Cannot exceed acquisition cost" });

            if (openingValues.NetBookValue > openingValues.AcquisitionCost)
                errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Net Book Value", Error = "Cannot exceed acquisition cost" });

            if (openingValues.ResidualValue > openingValues.AcquisitionCost)
                errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Residual Value", Error = "Cannot exceed acquisition cost" });

            if (data.AccumulatedDepreciation.HasValue && data.NetBookValue.HasValue)
            {
                var expectedNbv = openingValues.AcquisitionCost - data.AccumulatedDepreciation.Value;
                if (Math.Abs(expectedNbv - data.NetBookValue.Value) > 0.01m)
                {
                    errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Net Book Value", Error = "Must equal acquisition cost less accumulated depreciation" });
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(data.SerialNumber) && data.SerialNumber.Length > 100)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Serial Number", Error = "Cannot exceed 100 characters" });

        if (!string.IsNullOrWhiteSpace(data.RawStatus) && !data.Status.HasValue)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Status", Error = "Invalid fixed asset status" });

        return errors;
    }

    private static DateTime? ParseDate(IXLWorksheet worksheet, int row, int column)
    {
        if (column <= 0) return null;

        var cell = worksheet.Cell(row, column);
        if (cell.TryGetValue<DateTime>(out var dateValue))
            return dateValue.Date;

        if (cell.TryGetValue<double>(out var serial))
        {
            try
            {
                return DateTime.FromOADate(serial).Date;
            }
            catch
            {
                return null;
            }
        }

        var value = GetCellText(worksheet, row, column);
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out serial))
        {
            try
            {
                return DateTime.FromOADate(serial).Date;
            }
            catch
            {
                return null;
            }
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var date) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date))
        {
            return date.Date;
        }

        return null;
    }

    private static decimal? ParseDecimal(IXLWorksheet worksheet, int row, int column)
    {
        if (column <= 0) return null;

        var cell = worksheet.Cell(row, column);
        if (cell.TryGetValue<decimal>(out var decimalValue)) return decimalValue;

        var value = GetCellText(worksheet, row, column);
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number)
            || decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static int? ParseInt(IXLWorksheet worksheet, int row, int column)
    {
        var decimalValue = ParseDecimal(worksheet, row, column);
        if (decimalValue.HasValue)
            return decimal.ToInt32(decimal.Truncate(decimalValue.Value));

        var value = GetCellText(worksheet, row, column);
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var number) ? number : null;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var columnCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var column = 1; column <= columnCount; column++)
        {
            var header = NormalizeHeader(worksheet.Cell(1, column).GetFormattedString());
            if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
            {
                map[header] = column;
            }
        }

        return map;
    }

    private static int FindColumn(Dictionary<string, int> headerMap, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (headerMap.TryGetValue(NormalizeHeader(alias), out var column))
            {
                return column;
            }
        }

        return 0;
    }

    private static void AddMissingHeaderError(BulkImportResultDto result, int column, string field)
    {
        if (column <= 0)
        {
            result.Errors.Add(new BulkImportErrorDto
            {
                RowNumber = 1,
                Field = field,
                Error = $"Missing required column '{field}'."
            });
        }
    }

    private static string NormalizeHeader(string? value)
    {
        var text = NormalizeRequiredText(value).Replace("*", string.Empty, StringComparison.Ordinal);
        return new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string NormalizeRequiredText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? GetCellText(IXLWorksheet worksheet, int row, int column)
    {
        if (column <= 0) return null;

        var text = worksheet.Cell(row, column).GetFormattedString().Trim();

        return NormalizeOptionalText(text);
    }

    private static bool IsRowEmpty(IXLWorksheet worksheet, int row)
    {
        var columnCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= columnCount; column++)
        {
            if (!string.IsNullOrWhiteSpace(GetCellText(worksheet, row, column)))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddLookupValue(Dictionary<string, Guid> lookup, string? key, Guid value)
    {
        var normalizedKey = NormalizeOptionalText(key);
        if (!string.IsNullOrWhiteSpace(normalizedKey) && !lookup.ContainsKey(normalizedKey))
        {
            lookup[normalizedKey] = value;
        }
    }

    private static FixedAssetStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (int.TryParse(value, out var numericValue) && Enum.IsDefined(typeof(FixedAssetStatus), numericValue))
        {
            return (FixedAssetStatus)numericValue;
        }

        var normalizedValue = NormalizeHeader(value);
        foreach (var status in Enum.GetValues<FixedAssetStatus>())
        {
            if (NormalizeHeader(status.ToString()) == normalizedValue)
            {
                return status;
            }
        }

        return null;
    }

    private static void UpdateErrorCount(BulkImportResultDto result)
    {
        result.ErrorCount = result.Errors
            .Where(e => e.RowNumber > 0)
            .Select(e => e.RowNumber)
            .Distinct()
            .Count();

        if (result.ErrorCount == 0 && result.Errors.Any())
        {
            result.ErrorCount = result.Errors.Count;
        }
    }

    public async Task<FixedAssetDto> SubmitCapitalizationForApprovalAsync(
        Guid id,
        SubmitFixedAssetCapitalizationDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (IsCapitalized(asset))
        {
            return MapToDto(asset);
        }

        if (asset.Status == FixedAssetStatus.PendingApproval)
        {
            return MapToDto(asset);
        }

        if (asset.Status == FixedAssetStatus.Rejected)
        {
            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FinancePostingBlockedAfterRejection,
                asset,
                reason: "Direct fixed asset capitalization was rejected by workflow.",
                comment: dto.Comments);
            throw new InvalidOperationException("Rejected fixed asset capitalization requests cannot be submitted again without updating the asset.");
        }

        var snapshot = await BuildCapitalizationApprovalSnapshotAsync(asset, dto, cancellationToken);
        var snapshotJson = JsonSerializer.Serialize(snapshot, CapitalizationSnapshotJsonOptions);
        var snapshotHash = HashCapitalizationEvidence(snapshotJson);

        if (string.Equals(snapshot.SourceDocumentType, "FixedAsset", StringComparison.Ordinal)
            && _fixedAssetDimensions is not null)
        {
            var sourceDocumentId = DirectCapitalizationDocumentId(asset);
            var dimensionLines = BuildDirectCapitalizationDimensionLines(
                asset, sourceDocumentId, snapshot.DebitAccountId, snapshot.CreditAccountId,
                snapshot.TransactionAmount, snapshot.TransactionCurrencyCode,
                snapshot.FunctionalCurrencyCode, snapshot.ExchangeRate,
                snapshot.ExchangeRateId, snapshot.ExchangeRateDate, snapshot.Reference);
            await _fixedAssetDimensions!.SynchronizeAsync(
                DirectCapitalizationProducer,
                sourceDocumentId,
                snapshot.CapitalizationDate,
                dimensionLines,
                dto.FinanceDimensions,
                inheritedAssetJournalBySourceLine: null,
                "Fixed asset capitalization submitted for approval.",
                cancellationToken);
            await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                DirectCapitalizationProducer,
                sourceDocumentId,
                snapshot.CapitalizationDate,
                dimensionLines,
                cancellationToken);
        }

        if (_workflowService == null)
        {
            ApplyCapitalizationApprovalSubmission(asset, snapshot, snapshotJson, snapshotHash, workflowInstanceId: null);
            asset.Status = FixedAssetStatus.Acquired;
            asset.CapitalizationApprovalApprovedByUserId = CurrentUserGuid == Guid.Empty ? null : CurrentUserGuid;
            asset.CapitalizationApprovalApprovedAt = DateTime.UtcNow;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            return MapToDto(asset);
        }

        asset.Status = FixedAssetStatus.PendingApproval;
        ApplyCapitalizationApprovalSubmission(asset, snapshot, snapshotJson, snapshotHash, workflowInstanceId: null);
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);

        var workflowResult = await _workflowService.StartApprovalWorkflowAsync("FixedAsset", asset.Id);
        if (!workflowResult.Success)
        {
            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FinanceWorkflowApprovalFailed,
                asset,
                afterValues: new { workflowResult.Status, workflowResult.Message },
                reason: workflowResult.Message,
                comment: dto.Comments);
            asset.Status = FixedAssetStatus.Draft;
            asset.CapitalizationApprovalInvalidatedAt = DateTime.UtcNow;
            asset.CapitalizationApprovalInvalidationReason = workflowResult.Message
                ?? "Fixed asset capitalization workflow could not be started.";
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(workflowResult.Message ?? "Fixed asset capitalization workflow could not be started.");
        }

        asset.CapitalizationApprovalWorkflowInstanceId = workflowResult.WorkflowInstanceId;
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FinanceWorkflowSubmitted,
            asset,
            afterValues: new
            {
                asset.Status,
                workflowResult.WorkflowInstanceId,
                Action = "DirectCapitalization",
                SnapshotHash = snapshotHash,
                snapshot.CapitalizationDate,
                snapshot.TransactionAmount,
                snapshot.FunctionalCurrencyCode,
                snapshot.TransactionCurrencyCode,
                snapshot.DebitAccountId,
                snapshot.CreditAccountId,
                snapshot.ExchangeRateId
            },
            comment: dto.Comments ?? "Fixed asset direct capitalization submitted for workflow approval.");

        return MapToDto(asset);
    }

    public async Task<FixedAssetDto> CapitalizeAsync(Guid id, CapitalizeFixedAssetDto dto)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for fixed asset capitalization.");
        }

        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (IsCapitalized(asset))
        {
            return MapToDto(asset);
        }

        dto = await ResolveApprovedDirectCapitalizationInstructionAsync(asset, dto, CancellationToken.None);

        try
        {
            var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
            var assetAccount = await ResolveFixedAssetPostingAccountAsync(
                category.AssetAccountId,
                "fixed asset cost account",
                AccountType.Asset);
            var creditAccountId = dto.CreditAccountId ?? category.AucAccountId
                ?? throw new InvalidOperationException("Direct fixed asset capitalization requires a category AUC/CIP clearing account or an explicit credit account.");
            await ResolveFixedAssetPostingAccountAsync(
                creditAccountId,
                "fixed asset capitalization credit account",
                AccountType.Asset,
                AccountType.Liability,
                AccountType.Equity);

            var settings = await GetFinanceSettingsAsync();
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var transactionCurrency = NormalizeCurrency(dto.TransactionCurrencyCode ?? asset.TransactionCurrencyCode, functionalCurrency);
            var exchangeRate = NormalizeExchangeRate(dto.ExchangeRate ?? asset.ExchangeRate ?? 1m);
            var transactionAmount = RoundMoney(dto.Amount ?? asset.AcquisitionCost);
            if (transactionAmount <= 0m)
            {
                throw new InvalidOperationException("Fixed asset capitalization amount must be greater than zero.");
            }

            // The posting engine intentionally treats a source/action pair as immutable and
            // idempotent. After an approved reversal a corrected capitalization therefore uses a
            // new cycle document id; otherwise the original, already-reversed event would be
            // returned as a duplicate and the corrected cost would never reach the ledger.
            var isCorrectedCapitalization = asset.CapitalizationReversalPostingEventId.HasValue;
            var postingSourceDocumentType = DirectCapitalizationProducer.Definition.DocumentType;
            var postingSourceDocumentId = DirectCapitalizationDocumentId(asset);

            var postingLines = BuildDirectCapitalizationDimensionLines(
                asset,
                postingSourceDocumentId,
                assetAccount.Id,
                creditAccountId,
                transactionAmount,
                transactionCurrency,
                functionalCurrency,
                exchangeRate,
                dto.ExchangeRateId ?? asset.ExchangeRateId,
                dto.ExchangeRateDate ?? asset.ExchangeRateDate ?? dto.CapitalizationDate.Date,
                dto.Reference ?? asset.AssetCode);
            if (_fixedAssetDimensions is not null)
            {
                var existingDimensionAssignments = await _context.FinanceSourceDimensionAssignments
                    .AsNoTracking().AnyAsync(item => item.TenantId == TenantId
                        && item.RouteId == DirectCapitalizationProducer.RouteId
                        && item.SourceDocumentId == postingSourceDocumentId && !item.IsDeleted);
                if (!existingDimensionAssignments)
                    await _fixedAssetDimensions.SynchronizeAsync(
                        DirectCapitalizationProducer,
                        postingSourceDocumentId,
                        dto.CapitalizationDate.Date,
                        postingLines,
                        dto.FinanceDimensions,
                        inheritedAssetJournalBySourceLine: null,
                        "Fixed asset capitalization prepared for posting.");
                await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                    DirectCapitalizationProducer,
                    postingSourceDocumentId,
                    dto.CapitalizationDate.Date,
                    postingLines);
            }

            var request = new FinancePostingRequestDto
            {
                SourceModule = DirectCapitalizationProducer.Definition.PostingSourceModule,
                OriginModuleCode = FinanceModuleLockCatalog.Finance,
                SourceDocumentType = postingSourceDocumentType,
                SourceDocumentId = postingSourceDocumentId,
                SourceDocumentTenantId = asset.TenantId,
                PostingAction = "Capitalize",
                SourceDocumentReference = dto.Reference ?? asset.AssetCode,
                Description = $"Fixed asset capitalization - {asset.AssetCode} - {asset.Name}",
                PostingDate = dto.CapitalizationDate.Date,
                JournalType = "Fixed Asset Capitalization",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = isCorrectedCapitalization
                    ? $"FA:FixedAsset:{asset.TenantId:N}:{asset.Id:N}:Capitalize:{postingSourceDocumentId:N}"
                    : $"FA:FixedAsset:{asset.TenantId:N}:{asset.Id:N}:Capitalize",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines
            };

            var postingResult = await _financePostingEngine.PostAsync(request, DirectCapitalizationProducer);

            // The central posting engine clears the DbContext tracker when it recovers from a
            // concurrent/idempotent insert race. That recovery is correct for the ledger, but it
            // means the asset loaded before PostAsync may now be detached. Rehydrate it before
            // applying register changes so callers never receive a successful DTO while the
            // persisted fixed-asset register remains unchanged (FR-GL-010 / FIN-LIM-0030).
            if (_context.Entry(asset).State == EntityState.Detached)
            {
                asset = await _context.FixedAssets
                    .Include(item => item.BookValues)
                    .SingleAsync(item =>
                        item.TenantId == TenantId && item.Id == id && !item.IsDeleted);
            }

            var functionalCost = RoundMoney(postingResult.TotalDebitAmount);
            await ApplyCapitalizationAsync(
                asset,
                category,
                dto.CapitalizationDate.Date,
                "FixedAsset",
                asset.Id,
                dto.SourceDocumentLineId,
                postingResult.JournalEntryId,
                postingResult.PostingEventId,
                functionalCurrency,
                transactionCurrency,
                string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : exchangeRate,
                dto.ExchangeRateId ?? asset.ExchangeRateId,
                dto.ExchangeRateDate ?? asset.ExchangeRateDate ?? dto.CapitalizationDate.Date,
                functionalCost,
                transactionAmount,
                "Direct capitalization");
            asset.Status = FixedAssetStatus.Capitalized;

            await _context.SaveChangesAsync();

            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetCapitalizationConfigurationUsed,
                asset,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                afterValues: new { category.AssetAccountId, CreditAccountId = creditAccountId },
                comment: "Fixed asset capitalization category/account configuration used.");
            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetCapitalized,
                asset,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                afterValues: new { asset.AcquisitionCost, asset.Status, asset.CapitalizationDate },
                comment: dto.Reason);

            return MapToDto(asset);
        }
        catch (Exception ex)
        {
            await RecordFixedAssetAuditAsync(
                ex.Message.Contains("period is not open", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetCapitalizationBlockedClosedPeriod
                    : FinanceAuditEvents.FixedAssetCapitalizationFailed,
                asset,
                afterValues: new { error = ex.Message },
                reason: ex.Message,
                comment: dto.Reason);
            throw;
        }
    }

    public async Task<FixedAssetDto> CapitalizeFromProcurementAsync(
        Guid id,
        ProcurementFixedAssetPostingInstructionDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (_financePostingEngine == null)
            throw new InvalidOperationException("Central finance posting engine is not configured for Procurement fixed asset capitalization.");

        var asset = await _context.FixedAssets
            .Include(value => value.Category)
            .Include(value => value.BookValues)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        // A retry after the posting engine committed but before the handoff row was updated must
        // return the same asset. A different source, however, is an attempt to capitalize cost a
        // second time and is rejected explicitly.
        if (IsCapitalized(asset))
        {
            if (string.Equals(asset.SourceDocumentType, "ProcurementFixedAssetCapitalization", StringComparison.OrdinalIgnoreCase) &&
                asset.SourceDocumentId == dto.CapitalizationId &&
                asset.SourceDocumentLineId == dto.PurchaseOrderItemId)
                return MapToDto(asset);
            throw new InvalidOperationException($"Fixed asset '{asset.AssetCode}' is already capitalized by another source document.");
        }

        var handoff = await _context.Set<ProcurementFixedAssetCapitalization>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == dto.CapitalizationId &&
                value.FixedAssetId == asset.Id && value.PurchaseOrderItemId == dto.PurchaseOrderItemId &&
                !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The Finance-owned Procurement capitalization handoff was not found or does not match this asset.");
        if (handoff.Status == ProcurementFixedAssetCapitalizationStatus.Reversed)
            throw new InvalidOperationException("A reversed Procurement capitalization handoff cannot be posted again.");
        if (dto.FunctionalAmount <= 0m || RoundMoney(dto.FunctionalAmount) != RoundMoney(handoff.FunctionalAmount))
            throw new InvalidOperationException("The capitalization amount does not match the reserved Procurement receipt carrying value.");

        var approvedSnapshot = await ResolveApprovedSourceCapitalizationSnapshotAsync(
            asset,
            dto.CapitalizationId,
            dto.PurchaseOrderItemId,
            dto.Reason,
            cancellationToken);
        if (approvedSnapshot != null)
        {
            dto = new ProcurementFixedAssetPostingInstructionDto
            {
                CapitalizationId = dto.CapitalizationId,
                PurchaseOrderItemId = dto.PurchaseOrderItemId,
                CapitalizationDate = approvedSnapshot.CapitalizationDate,
                InventoryControlAccountId = approvedSnapshot.CreditAccountId,
                FunctionalAmount = approvedSnapshot.TransactionAmount,
                FunctionalCurrencyCode = approvedSnapshot.FunctionalCurrencyCode,
                SourceReference = approvedSnapshot.Reference,
                Reason = approvedSnapshot.Reason
            };
        }
        var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
        var assetAccount = await ResolveFixedAssetPostingAccountAsync(
            category.AssetAccountId,
            "fixed asset cost account",
            AccountType.Asset);
        await ResolveProcurementInventoryControlAccountAsync(dto.InventoryControlAccountId, cancellationToken);

        var functionalCurrency = NormalizeCurrency(dto.FunctionalCurrencyCode, "GHS");
        var amount = RoundMoney(dto.FunctionalAmount);
        var request = new FinancePostingRequestDto
        {
            SourceModule = "FA",
            // Period locks use the canonical short code, while journal inquiry retains the
            // human-readable Procurement origin through the catalog definition.
            OriginModuleCode = FinanceModuleLockCatalog.Procurement,
            SourceDocumentType = "ProcurementFixedAssetCapitalization",
            SourceDocumentId = dto.CapitalizationId,
            SourceDocumentTenantId = asset.TenantId,
            PostingAction = "CapitalizeAcceptedAsset",
            SourceDocumentReference = dto.SourceReference,
            Description = $"Capitalize accepted procured asset {asset.AssetCode} - {asset.Name}",
            PostingDate = dto.CapitalizationDate.Date,
            JournalType = "Fixed Asset Capitalization",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"FA:ProcurementFixedAssetCapitalization:{asset.TenantId:N}:{dto.CapitalizationId:N}:Post:v1",
            ReturnExistingOnDuplicate = true,
            Lines =
            [
                BuildCapitalizationPostingLine(
                    assetAccount.Id,
                    $"Capitalize accepted procured asset {asset.AssetCode}",
                    amount,
                    0m,
                    functionalCurrency,
                    functionalCurrency,
                    1m,
                    null,
                    dto.CapitalizationDate.Date,
                    dto.SourceReference,
                    1,
                    $"FixedAssetId={asset.Id:N};PurchaseOrderItemId={dto.PurchaseOrderItemId:N}",
                    "FA-Procurement-Capitalization"),
                BuildCapitalizationPostingLine(
                    dto.InventoryControlAccountId,
                    $"Release accepted inventory carrying value for {asset.AssetCode}",
                    0m,
                    amount,
                    functionalCurrency,
                    functionalCurrency,
                    1m,
                    null,
                    dto.CapitalizationDate.Date,
                    dto.SourceReference,
                    2,
                    $"FixedAssetId={asset.Id:N};PurchaseOrderItemId={dto.PurchaseOrderItemId:N}",
                    "FA-Procurement-Inventory-Clearing")
            ]
        };

        try
        {
            var posting = await _financePostingEngine.PostAsync(request, cancellationToken);
            // The posting engine may clear the tracker during an idempotent race recovery. Reload
            // before applying the register evidence, matching the direct-capitalization safeguard.
            if (_context.Entry(asset).State == EntityState.Detached)
            {
                asset = await _context.FixedAssets.Include(value => value.BookValues)
                    .SingleAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken);
            }

            await ApplyCapitalizationAsync(
                asset,
                category,
                dto.CapitalizationDate.Date,
                "ProcurementFixedAssetCapitalization",
                dto.CapitalizationId,
                dto.PurchaseOrderItemId,
                posting.JournalEntryId,
                posting.PostingEventId,
                functionalCurrency,
                functionalCurrency,
                null,
                null,
                dto.CapitalizationDate.Date,
                amount,
                handoff.SourceTransactionAmount,
                "Procurement accepted-receipt capitalization");
            await _context.SaveChangesAsync(cancellationToken);

            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetCapitalized,
                asset,
                postingEventId: posting.PostingEventId,
                journalEntryId: posting.JournalEntryId,
                afterValues: new
                {
                    Contract = "FIN-INT-007",
                    dto.CapitalizationId,
                    dto.PurchaseOrderItemId,
                    asset.AcquisitionCost,
                    asset.Status
                },
                comment: dto.Reason);
            return MapToDto(asset);
        }
        catch (Exception exception)
        {
            await RecordFixedAssetAuditAsync(
                exception.Message.Contains("period is not open", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetCapitalizationBlockedClosedPeriod
                    : FinanceAuditEvents.FixedAssetCapitalizationFailed,
                asset,
                afterValues: new { Contract = "FIN-INT-007", error = exception.Message },
                reason: exception.Message,
                comment: dto.Reason);
            throw;
        }
    }

    public async Task<FixedAssetCapitalizationReversalDto> RequestCapitalizationReversalAsync(
        Guid id,
        RequestFixedAssetCapitalizationReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (_financeReversalPolicyService == null)
            throw new InvalidOperationException("Finance reversal policy is not configured for fixed assets.");
        if (CurrentUserGuid == Guid.Empty)
            throw new InvalidOperationException("A resolved user identity is required to request a capitalization reversal.");

        var asset = await LoadAssetForCapitalizationReversalAsync(id, cancellationToken);
        EnsureDirectCapitalizationCanBeReversed(asset);
        await EnsureNoDownstreamAssetAccountingAsync(asset, cancellationToken);

        var pendingExists = await _context.FixedAssetCapitalizationReversals.AnyAsync(item =>
            item.TenantId == TenantId &&
            item.FixedAssetId == asset.Id &&
            item.Status != FixedAssetCapitalizationReversalStatuses.Rejected &&
            item.Status != FixedAssetCapitalizationReversalStatuses.Posted &&
            !item.IsDeleted,
            cancellationToken);
        if (pendingExists)
            throw new InvalidOperationException("This asset already has a capitalization reversal awaiting review or posting.");

        var policy = await _financeReversalPolicyService.ResolveAsync(
            asset.CapitalizationDate ?? asset.PurchaseDate,
            dto.Reason,
            dto.ReversalDate,
            cancellationToken);
        var impactAssessment = dto.ImpactAssessment?.Trim() ?? string.Empty;
        if (impactAssessment.Length < 20)
            throw new ArgumentException("The capitalization reversal impact assessment must contain at least 20 characters.", nameof(dto));

        var request = new FixedAssetCapitalizationReversal
        {
            TenantId = TenantId,
            FixedAssetId = asset.Id,
            OriginalPostingEventId = asset.PostingEventId!.Value,
            OriginalJournalEntryId = asset.JournalEntryId!.Value,
            Status = FixedAssetCapitalizationReversalStatuses.PendingApproval,
            Reason = policy.Reason,
            ImpactAssessment = impactAssessment,
            RequestedReversalDate = policy.ReversalDate,
            RequestedByUserId = CurrentUserGuid,
            RequestedByUserName = UserName,
            RequestedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = CurrentUserGuid
        };
        _context.FixedAssetCapitalizationReversals.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetCapitalizationReversalRequested,
            asset,
            beforeValues: new { asset.Status, asset.AcquisitionCost, asset.NetBookValue, asset.PostingEventId },
            afterValues: MapCapitalizationReversalToDto(request, asset),
            reason: policy.Reason,
            comment: impactAssessment);

        return MapCapitalizationReversalToDto(request, asset);
    }

    public async Task<FixedAssetCapitalizationReversalDto> ReviewCapitalizationReversalAsync(
        Guid id,
        Guid requestId,
        ReviewFixedAssetCapitalizationReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (CurrentUserGuid == Guid.Empty)
            throw new InvalidOperationException("A resolved user identity is required to review a capitalization reversal.");

        var request = await _context.FixedAssetCapitalizationReversals
            .Include(item => item.FixedAsset)
                .ThenInclude(asset => asset.BookValues)
            .SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == requestId && item.FixedAssetId == id && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset capitalization reversal request was not found.");
        if (request.Status != FixedAssetCapitalizationReversalStatuses.PendingApproval)
            throw new InvalidOperationException("Only a capitalization reversal awaiting approval can be reviewed.");
        if (request.RequestedByUserId == CurrentUserGuid)
            throw new InvalidOperationException("The reversal requester cannot review the same request.");

        var reviewComment = dto.ReviewComment?.Trim() ?? string.Empty;
        if (reviewComment.Length < 20)
            throw new ArgumentException("The capitalization reversal review comment must contain at least 20 characters.", nameof(dto));

        // Approval is a fresh control decision, not a rubber stamp of the maker's earlier view.
        // Re-running the lifecycle checks prevents approval after depreciation, valuation or
        // disposal activity has made a simple capitalization reversal unsafe.
        if (dto.Approved)
            await EnsureNoDownstreamAssetAccountingAsync(request.FixedAsset, cancellationToken);

        request.Status = dto.Approved
            ? FixedAssetCapitalizationReversalStatuses.Approved
            : FixedAssetCapitalizationReversalStatuses.Rejected;
        request.ReviewedByUserId = CurrentUserGuid;
        request.ReviewedByUserName = UserName;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComment = reviewComment;
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = UserName;
        request.LastModifiedById = CurrentUserGuid;
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFixedAssetAuditAsync(
            dto.Approved
                ? FinanceAuditEvents.FixedAssetCapitalizationReversalApproved
                : FinanceAuditEvents.FixedAssetCapitalizationReversalRejected,
            request.FixedAsset,
            beforeValues: new { Status = FixedAssetCapitalizationReversalStatuses.PendingApproval },
            afterValues: new { request.Status, request.ReviewedByUserName, request.ReviewedAt },
            reason: request.Reason,
            comment: reviewComment);
        return MapCapitalizationReversalToDto(request, request.FixedAsset);
    }

    public async Task<FixedAssetCapitalizationReversalDto> PostCapitalizationReversalAsync(
        Guid id,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (_financePostingEngine == null || _financeReversalPolicyService == null)
            throw new InvalidOperationException("Finance posting and reversal policy services are required for capitalization reversal.");

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var request = await _context.FixedAssetCapitalizationReversals
                    .Include(item => item.FixedAsset)
                        .ThenInclude(asset => asset.BookValues)
                    .SingleOrDefaultAsync(item =>
                        item.TenantId == TenantId && item.Id == requestId && item.FixedAssetId == id && !item.IsDeleted,
                        cancellationToken)
                    ?? throw new KeyNotFoundException("Fixed asset capitalization reversal request was not found.");
                if (request.Status == FixedAssetCapitalizationReversalStatuses.Posted)
                    return MapCapitalizationReversalToDto(request, request.FixedAsset);
                if (request.Status != FixedAssetCapitalizationReversalStatuses.Approved)
                    throw new InvalidOperationException("Only an independently approved capitalization reversal can be posted.");

                EnsureDirectCapitalizationCanBeReversed(request.FixedAsset);
                await EnsureNoDownstreamAssetAccountingAsync(request.FixedAsset, cancellationToken);
                var policy = await _financeReversalPolicyService.ResolveAsync(
                    request.FixedAsset.CapitalizationDate ?? request.FixedAsset.PurchaseDate,
                    request.Reason,
                    request.RequestedReversalDate,
                    cancellationToken);
                var plan = await _financePostingEngine.GetReversalPlanAsync(
                    request.OriginalPostingEventId,
                    policy.Reason,
                    policy.ReversalDate,
                    cancellationToken);
                var reversalLines = plan.ReversalLines.ToList();
                if (_fixedAssetDimensions is not null)
                    await _fixedAssetDimensions.RegisterHistoricalReversalAsync(
                        CapitalizationReversalProducer,
                        request.Id,
                        plan.OriginalJournalEntryId,
                        reversalLines,
                        cancellationToken);
                var posting = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                {
                    SourceModule = CapitalizationReversalProducer.Definition.PostingSourceModule,
                    OriginModuleCode = FinanceModuleLockCatalog.Finance,
                    SourceDocumentType = CapitalizationReversalProducer.Definition.DocumentType,
                    SourceDocumentId = request.Id,
                    SourceDocumentTenantId = request.TenantId,
                    PostingAction = "Reverse",
                    SourceDocumentReference = request.FixedAsset.AssetCode,
                    Description = $"Reverse fixed asset capitalization - {request.FixedAsset.AssetCode}",
                    PostingDate = plan.ReversalDate,
                    JournalType = "Fixed Asset Capitalization Reversal",
                    BookClassification = "IFRS",
                    FunctionalCurrencyCode = request.FixedAsset.FunctionalCurrencyCode,
                    ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                    ReversalReason = policy.Reason,
                    // ReversalType is a compact, indexed journal classification (20 characters
                    // maximum); the full business explanation remains in ReversalReason and the
                    // maker-checker request's impact assessment.
                    ReversalType = "FA Capitalization",
                    IdempotencyKey = $"FA:FixedAsset:{request.TenantId:N}:{request.FixedAssetId:N}:CapitalizationReverse:{request.Id:N}",
                    ReturnExistingOnDuplicate = true,
                    Lines = reversalLines
                }, CapitalizationReversalProducer, cancellationToken);

                // The posting engine may clear the shared DbContext tracker while resolving an
                // idempotency race (for example, two operators posting the approved request at
                // nearly the same time). Rehydrate the maker-checker request and its asset before
                // changing the register; otherwise the journal could post successfully while the
                // detached asset/request changes are never persisted. If the competing caller has
                // already completed the request, return that posted evidence instead of creating a
                // second negative register movement.
                if (_context.Entry(request).State == EntityState.Detached ||
                    _context.Entry(request.FixedAsset).State == EntityState.Detached)
                {
                    request = await _context.FixedAssetCapitalizationReversals
                        .Include(item => item.FixedAsset)
                            .ThenInclude(asset => asset.BookValues)
                        .SingleAsync(item =>
                            item.TenantId == TenantId &&
                            item.Id == requestId &&
                            item.FixedAssetId == id &&
                            !item.IsDeleted,
                            cancellationToken);

                    if (request.Status == FixedAssetCapitalizationReversalStatuses.Posted)
                    {
                        if (transaction != null)
                            await transaction.CommitAsync(cancellationToken);
                        return MapCapitalizationReversalToDto(request, request.FixedAsset);
                    }
                }

                ApplyCapitalizationReversal(
                    request.FixedAsset,
                    posting.JournalEntryId,
                    posting.PostingEventId,
                    posting.PostingDate,
                    policy.Reason,
                    request);
                await MarkProcurementCapitalizationReversedAsync(
                    request.FixedAsset,
                    posting.JournalEntryId,
                    posting.PostingEventId,
                    posting.PostingDate,
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                // The audit event is written before committing the surrounding transaction so a
                // successful journal can never exist without its maker-checker/register evidence.
                await RecordFixedAssetAuditAsync(
                    FinanceAuditEvents.FixedAssetCapitalizationReversed,
                    request.FixedAsset,
                    postingEventId: posting.PostingEventId,
                    journalEntryId: posting.JournalEntryId,
                    beforeValues: new { request.OriginalPostingEventId, request.OriginalJournalEntryId },
                    afterValues: new { request.Status, posting.PostingEventId, posting.JournalEntryId, posting.PostingDate },
                    reason: policy.Reason,
                    comment: request.ImpactAssessment);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return MapCapitalizationReversalToDto(request, request.FixedAsset);
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<FixedAssetCapitalizationReversalDto>> GetCapitalizationReversalsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var asset = await _context.FixedAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset not found.");
        var requests = await _context.FixedAssetCapitalizationReversals
            .AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.FixedAssetId == id && !item.IsDeleted)
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync(cancellationToken);
        return requests.Select(item => MapCapitalizationReversalToDto(item, asset)).ToList();
    }

    public async Task RecordApInvoiceCapitalizationAsync(
        Guid vendorInvoiceId,
        Guid journalEntryId,
        Guid postingEventId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _context.VendorInvoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(
                i => i.TenantId == TenantId && i.Id == vendorInvoiceId && !i.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Vendor invoice was not found for fixed asset capitalization.");

        var assetLines = invoice.LineItems
            .Where(line => IsFixedAssetLine(line) && !line.IsDeleted)
            .OrderBy(line => line.CreatedAt)
            .ThenBy(line => line.Id)
            .ToList();
        if (assetLines.Count == 0)
        {
            return;
        }

        var postingEvent = await _context.FinancePostingEvents
            .FirstOrDefaultAsync(
                e => e.TenantId == TenantId &&
                    e.Id == postingEventId &&
                    e.SourceDocumentType == "VendorInvoice" &&
                    e.SourceDocumentId == vendorInvoiceId &&
                    e.PostingStatus == "Posted",
                cancellationToken)
            ?? throw new InvalidOperationException("AP invoice posting event was not found for fixed asset capitalization.");

        var journal = await _context.JournalEntries
            .Include(j => j.Transactions)
            .FirstOrDefaultAsync(
                j => j.TenantId == TenantId &&
                    j.Id == journalEntryId &&
                    j.Id == postingEvent.JournalEntryId &&
                    j.PostingStatus == "Posted",
                cancellationToken)
            ?? throw new InvalidOperationException("AP invoice posted journal was not found for fixed asset capitalization.");

        foreach (var line in assetLines)
        {
            if (!line.FixedAssetId.HasValue)
            {
                throw new InvalidOperationException($"AP fixed asset line '{line.Description}' must reference a fixed asset.");
            }

            if (line.CapitalizationPostingEventId.HasValue && line.CapitalizationPostingEventId.Value != postingEventId)
            {
                throw new InvalidOperationException("AP invoice line is already capitalized by another posting event.");
            }

            var asset = await _context.FixedAssets
                .Include(a => a.Category)
                .Include(a => a.BookValues)
                .FirstOrDefaultAsync(
                    a => a.TenantId == TenantId &&
                        a.Id == line.FixedAssetId.Value &&
                        !a.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("AP invoice fixed asset was not found for this tenant.");

            if (asset.PostingEventId.HasValue && asset.PostingEventId.Value != postingEventId)
            {
                throw new InvalidOperationException("Fixed asset is already capitalized by another posting event.");
            }

            var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
            await ResolveFixedAssetPostingAccountAsync(category.AssetAccountId, "fixed asset cost account", AccountType.Asset);

            var assetJournalLines = journal.Transactions
                .Where(t => t.TenantId == TenantId &&
                    t.AccountId == category.AssetAccountId &&
                    t.DebitAmount > 0m &&
                    ReferencesApAssetLine(t.Notes, line.Id, asset.Id))
                .ToList();

            if (assetJournalLines.Count == 0)
            {
                throw new InvalidOperationException("Posted AP invoice journal does not contain a tagged fixed asset cost line.");
            }

            var functionalCost = RoundMoney(assetJournalLines.Sum(t => t.DebitAmount - t.CreditAmount));
            if (functionalCost <= 0m)
            {
                throw new InvalidOperationException("Posted AP invoice fixed asset cost must be greater than zero.");
            }

            var firstLine = assetJournalLines.OrderBy(t => t.LineNumber).First();
            var transactionCost = RoundMoney(assetJournalLines.Sum(t =>
                t.TransactionDebitAmount.GetValueOrDefault(t.ForeignCurrencyAmount ?? t.DebitAmount)));

            await ApplyCapitalizationAsync(
                asset,
                category,
                invoice.InvoiceDate.Date,
                "VendorInvoice",
                invoice.Id,
                line.Id,
                journalEntryId,
                postingEventId,
                firstLine.FunctionalCurrencyCode,
                NormalizeCurrency(firstLine.TransactionCurrency, firstLine.FunctionalCurrencyCode),
                firstLine.ExchangeRate,
                firstLine.ExchangeRateId,
                firstLine.ExchangeRateDate,
                functionalCost,
                transactionCost,
                "AP invoice capitalization");

            line.CapitalizationJournalEntryId = journalEntryId;
            line.CapitalizationPostingEventId = postingEventId;
            line.CapitalizedAt = DateTime.UtcNow;
            _context.Entry(asset).State = EntityState.Modified;
            _context.Entry(line).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();

        foreach (var line in assetLines.Where(l => l.FixedAssetId.HasValue))
        {
            var asset = await _context.FixedAssets
                .FirstAsync(a => a.TenantId == TenantId && a.Id == line.FixedAssetId!.Value, cancellationToken);

            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetAcquired,
                asset,
                postingEventId: postingEventId,
                journalEntryId: journalEntryId,
                afterValues: new
                {
                    invoice.InvoiceNumber,
                    VendorInvoiceLineId = line.Id,
                    asset.AcquisitionCost,
                    asset.Status
                },
                comment: "Fixed asset acquired from AP invoice line.");
            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetCapitalized,
                asset,
                postingEventId: postingEventId,
                journalEntryId: journalEntryId,
                afterValues: new { asset.AcquisitionCost, asset.CapitalizationDate },
                comment: "Fixed asset capitalized from posted AP invoice.");
        }

        _context.ChangeTracker.Clear();
    }

    public async Task ValidateApInvoiceCapitalizationReversalAsync(
        Guid vendorInvoiceId,
        CancellationToken cancellationToken = default)
    {
        var assetIds = await _context.Set<VendorInvoiceLineItem>()
            .AsNoTracking()
            .Where(line =>
                line.TenantId == TenantId &&
                line.VendorInvoiceId == vendorInvoiceId &&
                line.FixedAssetId.HasValue &&
                line.CapitalizationPostingEventId.HasValue &&
                !line.IsDeleted)
            .Select(line => line.FixedAssetId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var assetId in assetIds)
        {
            var asset = await LoadAssetForCapitalizationReversalAsync(assetId, cancellationToken);
            if (!string.Equals(asset.SourceDocumentType, "VendorInvoice", StringComparison.OrdinalIgnoreCase) ||
                asset.SourceDocumentId != vendorInvoiceId)
            {
                throw new InvalidOperationException("An AP fixed asset line is not linked to the invoice capitalization being reversed.");
            }

            await EnsureNoDownstreamAssetAccountingAsync(asset, cancellationToken);
        }
    }

    public async Task RecordApInvoiceCapitalizationReversalAsync(
        Guid vendorInvoiceId,
        Guid reversalJournalEntryId,
        Guid reversalPostingEventId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var lines = await _context.Set<VendorInvoiceLineItem>()
            .Where(line =>
                line.TenantId == TenantId &&
                line.VendorInvoiceId == vendorInvoiceId &&
                line.FixedAssetId.HasValue &&
                line.CapitalizationPostingEventId.HasValue &&
                !line.IsDeleted)
            .OrderBy(line => line.Id)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0)
            return;

        foreach (var line in lines)
        {
            if (line.CapitalizationReversalPostingEventId.HasValue)
            {
                if (line.CapitalizationReversalPostingEventId == reversalPostingEventId)
                    continue;
                throw new InvalidOperationException("An AP fixed asset line is already linked to a different capitalization reversal.");
            }

            var asset = await LoadAssetForCapitalizationReversalAsync(line.FixedAssetId!.Value, cancellationToken);
            await EnsureNoDownstreamAssetAccountingAsync(asset, cancellationToken);
            ApplyCapitalizationReversal(
                asset,
                reversalJournalEntryId,
                reversalPostingEventId,
                DateTime.UtcNow.Date,
                reason.Trim(),
                request: null);

            line.CapitalizationReversalJournalEntryId = reversalJournalEntryId;
            line.CapitalizationReversalPostingEventId = reversalPostingEventId;
            line.CapitalizationReversedAt = DateTime.UtcNow;
            line.UpdatedAt = DateTime.UtcNow;
            line.UpdatedBy = UserName;

            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetCapitalizationReversed,
                asset,
                postingEventId: reversalPostingEventId,
                journalEntryId: reversalJournalEntryId,
                beforeValues: new
                {
                    VendorInvoiceId = vendorInvoiceId,
                    VendorInvoiceLineId = line.Id,
                    OriginalPostingEventId = line.CapitalizationPostingEventId,
                    OriginalJournalEntryId = line.CapitalizationJournalEntryId
                },
                afterValues: new { reversalPostingEventId, reversalJournalEntryId, asset.Status, asset.NetBookValue },
                reason: reason,
                comment: "AP invoice reversal removed the related cost from the fixed asset register without posting a duplicate asset journal.");
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FixedAssetDto> RegisterInventoryIssueAssetAsync(
        RegisterInventoryIssueFixedAssetDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.IssueVoucherId == Guid.Empty || dto.IssueVoucherLineId == Guid.Empty ||
            dto.FixedAssetCategoryId == Guid.Empty || dto.CustodianEmployeeId == Guid.Empty ||
            dto.PostingEventId == Guid.Empty || dto.JournalEntryId == Guid.Empty)
            throw new InvalidOperationException("Complete issue, category, custodian and Finance lineage is required to register an inventory-issued asset.");

        var existing = await _context.FixedAssets
            .Include(asset => asset.Category)
            .Include(asset => asset.BookValues)
            .SingleOrDefaultAsync(asset =>
                asset.TenantId == TenantId &&
                asset.SourceDocumentType == "InventoryIssueVoucher" &&
                asset.SourceDocumentLineId == dto.IssueVoucherLineId &&
                !asset.IsDeleted,
                cancellationToken);
        if (existing != null)
        {
            if (existing.SourceDocumentId != dto.IssueVoucherId ||
                existing.PostingEventId != dto.PostingEventId ||
                existing.JournalEntryId != dto.JournalEntryId ||
                existing.FixedAssetCategoryId != dto.FixedAssetCategoryId ||
                existing.CurrentCustodianId != dto.CustodianEmployeeId)
                throw new InvalidOperationException("The inventory issue line is already linked to a different fixed-asset registration.");
            return MapToDto(existing);
        }

        var serialNumber = NormalizeRequiredText(dto.SerialNumber);
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new InvalidOperationException("A serial number is required before a fixed-asset item can be issued into custody.");
        if (await _context.FixedAssets.AnyAsync(asset =>
                asset.TenantId == TenantId && asset.SerialNumber == serialNumber && !asset.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException($"Serial number '{serialNumber}' is already registered to a fixed asset.");

        var category = await ResolveAssetCategoryAsync(dto.FixedAssetCategoryId);
        ValidateDepreciationConfiguration(
            category.DefaultMethod,
            category.DefaultUsefulLifeMonths,
            residualValue: 0m,
            category.DefaultDiminishingBalanceRatePercent,
            category.DefaultLifetimeProductionCapacity);

        var custodian = await _context.Employees.AsNoTracking().SingleOrDefaultAsync(employee =>
            employee.TenantId == TenantId && employee.Id == dto.CustodianEmployeeId &&
            employee.IsActive && employee.EndDate == null && !employee.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The selected receiver is not an active employee of this tenant and cannot hold a fixed asset.");

        var (postingEvent, journal, functionalCost) = await ValidateInventoryAssetJournalAsync(
            "InventoryIssueVoucher",
            dto.IssueVoucherId,
            dto.IssueVoucherLineId,
            dto.PostingEventId,
            dto.JournalEntryId,
            category.AssetAccountId,
            cancellationToken);

        var asset = new FixedAsset
        {
            TenantId = TenantId,
            AssetCode = await GenerateAssetCodeAsync(category.Id),
            Name = NormalizeRequiredText(dto.ItemName),
            Description = NormalizeOptionalText(dto.Description),
            Location = NormalizeOptionalText(dto.Location),
            CurrentCustodianId = custodian.Id,
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = dto.IssueDate.Date,
            PlacedInServiceDate = dto.IssueDate.Date,
            DepreciationMethod = category.DefaultMethod,
            DepreciationConvention = DepreciationConvention.FullMonth,
            UsefulLifeMonths = category.DefaultUsefulLifeMonths,
            ResidualValue = RoundMoney(functionalCost * category.DefaultResidualValuePercent / 100m),
            DiminishingBalanceRatePercent = RoundRate(category.DefaultDiminishingBalanceRatePercent),
            LifetimeProductionCapacity = category.DefaultLifetimeProductionCapacity,
            AccumulatedProductionUnits = 0m,
            SerialNumber = serialNumber,
            Status = FixedAssetStatus.Capitalized,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = CurrentUserGuid == Guid.Empty ? null : CurrentUserGuid
        };

        _context.FixedAssets.Add(asset);
        await ApplyCapitalizationAsync(
            asset,
            category,
            dto.IssueDate.Date,
            "InventoryIssueVoucher",
            dto.IssueVoucherId,
            dto.IssueVoucherLineId,
            journal.Id,
            postingEvent.Id,
            postingEvent.FunctionalCurrencyCode,
            postingEvent.FunctionalCurrencyCode,
            exchangeRate: null,
            exchangeRateId: null,
            exchangeRateDate: dto.IssueDate.Date,
            functionalCost,
            functionalCost,
            "Inventory issue capitalization and custody registration");
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetAcquired,
            asset,
            postingEvent.Id,
            journal.Id,
            afterValues: new
            {
                dto.IssueVoucherId,
                dto.IssueVoucherLineId,
                dto.IssueVoucherNumber,
                dto.ItemCode,
                CustodianEmployeeId = custodian.Id,
                asset.SerialNumber,
                asset.AcquisitionCost
            },
            comment: "Fixed asset registered from a governed inventory issue.");
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetCapitalized,
            asset,
            postingEvent.Id,
            journal.Id,
            afterValues: new { asset.Status, asset.CapitalizationDate, asset.CurrentCustodianId },
            comment: "Inventory-issued fixed asset capitalized without a duplicate Finance journal.");

        return MapToDto(asset);
    }

    public async Task ReverseInventoryIssueAssetAsync(
        Guid fixedAssetId,
        ReverseInventoryIssueFixedAssetDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var asset = await LoadAssetForCapitalizationReversalAsync(fixedAssetId, cancellationToken);
        EnsureInventoryIssueAsset(asset);

        if (asset.CapitalizationReversalPostingEventId.HasValue)
        {
            if (asset.CapitalizationReversalPostingEventId == dto.PostingEventId &&
                asset.CapitalizationReversalJournalEntryId == dto.JournalEntryId)
                return;
            throw new InvalidOperationException("The inventory-issued asset is already reversed by a different return posting.");
        }

        await ValidateInventoryAssetJournalAsync(
            "InventoryReturnVoucher",
            dto.ReturnVoucherId,
            dto.ReturnVoucherLineId,
            dto.PostingEventId,
            dto.JournalEntryId,
            asset.Category.AssetAccountId,
            cancellationToken,
            expectCredit: true);
        await EnsureNoDownstreamAssetAccountingAsync(asset, cancellationToken);

        ApplyCapitalizationReversal(
            asset,
            dto.JournalEntryId,
            dto.PostingEventId,
            dto.ReturnDate.Date,
            NormalizeRequiredText(dto.Reason),
            request: null);
        asset.CurrentCustodianId = null;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetCapitalizationReversed,
            asset,
            dto.PostingEventId,
            dto.JournalEntryId,
            afterValues: new { dto.ReturnVoucherId, dto.ReturnVoucherLineId, asset.Status, asset.CurrentCustodianId },
            reason: dto.Reason,
            comment: "Governed inventory return removed the asset from custody and compensated its register value.");
    }

    public async Task ReinstateInventoryIssueAssetAsync(
        Guid fixedAssetId,
        ReinstateInventoryIssueFixedAssetDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var asset = await _context.FixedAssets
            .Include(value => value.Category)
            .Include(value => value.BookValues)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == fixedAssetId && !value.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("The fixed asset was not found for this tenant.");
        EnsureInventoryIssueAsset(asset);

        if (!asset.CapitalizationReversalPostingEventId.HasValue || asset.Status != FixedAssetStatus.Draft)
        {
            if (asset.PostingEventId == dto.PostingEventId && asset.JournalEntryId == dto.JournalEntryId)
                return;
            throw new InvalidOperationException("Only a returned inventory-issued asset can be reinstated.");
        }

        var issueLineId = asset.SourceDocumentLineId!.Value;
        var (returnPostingEvent, _, functionalCost) = await ValidateInventoryAssetJournalAsync(
            "InventoryReturnVoucher",
            dto.ReturnVoucherId,
            dto.ReturnVoucherLineId,
            dto.PostingEventId,
            dto.JournalEntryId,
            asset.Category.AssetAccountId,
            cancellationToken);
        var originalCapitalization = await _context.AssetTransactions.AsNoTracking()
            .Where(transaction => transaction.TenantId == TenantId && transaction.FixedAssetId == asset.Id &&
                transaction.TransactionType == "Capitalization" && transaction.Amount > 0m && !transaction.IsDeleted)
            .OrderBy(transaction => transaction.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The original inventory-issue capitalization could not be reconstructed.");
        if (Math.Abs(originalCapitalization.Amount - functionalCost) > 0.01m)
            throw new InvalidOperationException("The return reversal value does not reconcile to the original fixed-asset capitalization.");

        var receiverEmployeeId = await (
            from line in _context.InventoryIssueVoucherLines.AsNoTracking()
            join voucher in _context.InventoryIssueVouchers.AsNoTracking()
                on line.InventoryIssueVoucherId equals voucher.Id
            join user in _context.Users.AsNoTracking()
                on voucher.ReceiverUserId equals user.Id
            where line.TenantId == TenantId && line.Id == issueLineId &&
                  voucher.TenantId == TenantId && user.TenantId == TenantId &&
                  user.EmployeeId.HasValue && !line.IsDeleted && !voucher.IsDeleted
            select user.EmployeeId!.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (receiverEmployeeId == Guid.Empty || !await _context.Employees.AsNoTracking().AnyAsync(employee =>
                employee.TenantId == TenantId && employee.Id == receiverEmployeeId && employee.IsActive &&
                employee.EndDate == null && !employee.IsDeleted,
                cancellationToken))
            throw new InvalidOperationException("The original active employee custodian could not be restored.");

        await ApplyCapitalizationAsync(
            asset,
            asset.Category,
            dto.ReversalDate.Date,
            "InventoryIssueVoucher",
            asset.SourceDocumentId!.Value,
            issueLineId,
            dto.JournalEntryId,
            dto.PostingEventId,
            returnPostingEvent.FunctionalCurrencyCode,
            returnPostingEvent.FunctionalCurrencyCode,
            exchangeRate: null,
            exchangeRateId: null,
            exchangeRateDate: dto.ReversalDate.Date,
            originalCapitalization.Amount,
            originalCapitalization.Amount,
            "Reinstatement after inventory return reversal");
        asset.CurrentCustodianId = receiverEmployeeId;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetCapitalized,
            asset,
            dto.PostingEventId,
            dto.JournalEntryId,
            afterValues: new { dto.ReturnVoucherId, dto.ReturnVoucherLineId, asset.Status, asset.CurrentCustodianId },
            reason: dto.Reason,
            comment: "Inventory-issued fixed asset reinstated after a governed return reversal.");
    }

    private async Task<(FinancePostingEvent PostingEvent, JournalEntry Journal, decimal Amount)>
        ValidateInventoryAssetJournalAsync(
            string sourceDocumentType,
            Guid sourceDocumentId,
            Guid sourceLineId,
            Guid postingEventId,
            Guid journalEntryId,
            Guid assetAccountId,
            CancellationToken cancellationToken,
            bool expectCredit = false)
    {
        var postingEvent = await _context.FinancePostingEvents.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == postingEventId &&
            value.SourceDocumentType == sourceDocumentType && value.SourceDocumentId == sourceDocumentId &&
            value.JournalEntryId == journalEntryId && value.PostingStatus == "Posted" && !value.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The posted Finance event does not match the inventory source document.");
        var journal = await _context.JournalEntries.AsNoTracking()
            .Include(value => value.Transactions)
            .SingleOrDefaultAsync(value => value.TenantId == TenantId && value.Id == journalEntryId &&
                value.SourceDocumentType == sourceDocumentType && value.SourceDocumentId == sourceDocumentId &&
                value.PostingStatus == "Posted" && value.IsBalanced && !value.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("The balanced posted Finance journal does not match the inventory source document.");

        var lineToken = sourceLineId.ToString("N");
        var taggedLines = journal.Transactions.Where(value =>
            value.TenantId == TenantId && value.AccountId == assetAccountId && !value.IsDeleted &&
            (value.Notes?.Contains(lineToken, StringComparison.OrdinalIgnoreCase) == true ||
             value.TransactionTag?.Contains(lineToken, StringComparison.OrdinalIgnoreCase) == true));
        var amount = RoundMoney(taggedLines.Sum(value => expectCredit ? value.CreditAmount : value.DebitAmount));
        if (amount <= 0m)
            throw new InvalidOperationException("The Finance journal does not contain the tagged fixed-asset value for this inventory line.");
        return (postingEvent, journal, amount);
    }

    private static void EnsureInventoryIssueAsset(FixedAsset asset)
    {
        if (!string.Equals(asset.SourceDocumentType, "InventoryIssueVoucher", StringComparison.OrdinalIgnoreCase) ||
            !asset.SourceDocumentId.HasValue || !asset.SourceDocumentLineId.HasValue ||
            !asset.PostingEventId.HasValue || !asset.JournalEntryId.HasValue)
            throw new InvalidOperationException("The fixed asset is not governed by inventory-issue lineage.");
    }

    private async Task ApplyCapitalizationAsync(
        FixedAsset asset,
        FixedAssetCategory category,
        DateTime capitalizationDate,
        string sourceDocumentType,
        Guid sourceDocumentId,
        Guid? sourceDocumentLineId,
        Guid journalEntryId,
        Guid postingEventId,
        string functionalCurrency,
        string transactionCurrency,
        decimal? exchangeRate,
        Guid? exchangeRateId,
        DateTime? exchangeRateDate,
        decimal functionalCost,
        decimal transactionCost,
        string transactionDescription)
    {
        if (asset.TenantId != TenantId || category.TenantId != TenantId)
        {
            await RecordFixedAssetAuditAsync(
                FinanceAuditEvents.FixedAssetCrossTenantRejected,
                asset,
                afterValues: new { category.Id },
                reason: "Fixed asset/category tenant mismatch.",
                comment: "Cross-tenant fixed asset capitalization rejected.");
            throw new InvalidOperationException("Fixed asset category belongs to another tenant.");
        }

        await EnsureBookValuesAsync(asset);

        asset.CapitalizationDate = capitalizationDate.Date;
        asset.PurchaseDate = asset.PurchaseDate == default ? capitalizationDate.Date : asset.PurchaseDate;
        asset.PurchasePrice = functionalCost;
        asset.TaxAmount = 0m;
        asset.InstallationCost = 0m;
        asset.AcquisitionCost = functionalCost;
        asset.NetBookValue = functionalCost;
        asset.FunctionalCurrencyCode = NormalizeCurrency(functionalCurrency, "GHS");
        asset.TransactionCurrencyCode = NormalizeCurrency(transactionCurrency, asset.FunctionalCurrencyCode);
        asset.ExchangeRate = exchangeRate;
        asset.ExchangeRateId = exchangeRateId;
        asset.ExchangeRateDate = exchangeRateDate?.Date;
        asset.SourceDocumentType = sourceDocumentType;
        asset.SourceDocumentId = sourceDocumentId;
        asset.SourceDocumentLineId = sourceDocumentLineId;
        asset.JournalEntryId = journalEntryId;
        asset.PostingEventId = postingEventId;
        asset.CapitalizedAt = DateTime.UtcNow;
        // A corrected capitalization starts a new current cycle. The prior cycle remains fully
        // evidenced by its reversal request, posting event and immutable AssetTransaction rows.
        asset.CapitalizationReversalJournalEntryId = null;
        asset.CapitalizationReversalPostingEventId = null;
        asset.CapitalizationReversedAt = null;
        asset.CapitalizationReversalReason = null;
        asset.Status = asset.Status == FixedAssetStatus.Active
            ? FixedAssetStatus.Active
            : FixedAssetStatus.Capitalized;

        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        foreach (var bookValue in asset.BookValues.Where(value => !value.IsDeleted))
        {
            bookValue.AcquisitionCost = functionalCost;
            bookValue.AccumulatedDepreciation = 0m;
            bookValue.NetBookValue = functionalCost;
            bookValue.ResidualValue = asset.ResidualValue;
            bookValue.UsefulLifeMonths = asset.UsefulLifeMonths;
            bookValue.RemainingUsefulLifeMonths = asset.UsefulLifeMonths;
            bookValue.DepreciationMethod = asset.DepreciationMethod;
            bookValue.DepreciationConvention = asset.DepreciationConvention;
            bookValue.DiminishingBalanceRatePercent = asset.DiminishingBalanceRatePercent;
            bookValue.LifetimeProductionCapacity = asset.LifetimeProductionCapacity;
            bookValue.AccumulatedProductionUnits = asset.AccumulatedProductionUnits;
            bookValue.PlacedInServiceDate = asset.PlacedInServiceDate;
            bookValue.CapitalizationDate = capitalizationDate.Date;
            bookValue.CapitalizationJournalEntryId = journalEntryId;
            bookValue.CapitalizationPostingEventId = postingEventId;
            bookValue.CapitalizationReversalJournalEntryId = null;
            bookValue.CapitalizationReversalPostingEventId = null;
            bookValue.CapitalizationReversedAt = null;
            bookValue.SourceDocumentType = sourceDocumentType;
            bookValue.SourceDocumentId = sourceDocumentId;
            bookValue.SourceDocumentLineId = sourceDocumentLineId;
            bookValue.UpdatedAt = DateTime.UtcNow;
            bookValue.UpdatedBy = UserName;

            var transactionExists = await _context.AssetTransactions.AnyAsync(t =>
                t.TenantId == TenantId &&
                t.FixedAssetId == asset.Id &&
                t.AccountingBookId == bookValue.AccountingBookId &&
                t.TransactionType == "Capitalization" &&
                t.RelatedEntityId == postingEventId);
            if (!transactionExists)
            {
                _context.AssetTransactions.Add(new AssetTransaction
                {
                    TenantId = TenantId,
                    FixedAssetId = asset.Id,
                    AccountingBookId = bookValue.AccountingBookId,
                    BookClassification = bookValue.BookClassification,
                    TransactionDate = capitalizationDate.Date,
                    TransactionType = "Capitalization",
                    Description = transactionDescription,
                    Amount = functionalCost,
                    ResultingBookValue = functionalCost,
                    RelatedEntityId = postingEventId,
                    PerformedByUserId = CurrentUserGuid,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                });
            }
        }
    }

    private async Task EnsureBookValuesAsync(FixedAsset asset)
    {
        var books = await GetActivePostingBooksAsync();
        foreach (var book in books)
        {
            if (asset.BookValues.Any(value => value.AccountingBookId == book.Id && !value.IsDeleted))
            {
                continue;
            }

            var bookValue = BuildBookValue(
                asset,
                book,
                0m,
                accumulatedDepreciation: 0m,
                netBookValue: 0m,
                asset.ResidualValue,
                asset.UsefulLifeMonths,
                remainingUsefulLifeMonths: asset.UsefulLifeMonths,
                asset.DepreciationMethod,
                asset.DepreciationConvention,
                asset.PlacedInServiceDate,
                openingAsOfDate: null,
                openingYtdDepreciation: 0m,
                openingSource: "Capitalization");
            asset.BookValues.Add(bookValue);
            _context.FixedAssetBookValues.Add(bookValue);
        }
    }

    private async Task<FixedAssetCategory> ResolveAssetCategoryAsync(Guid categoryId)
    {
        var category = await _context.FixedAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == categoryId && !c.IsDeleted);
        if (category == null)
        {
            throw new InvalidOperationException("Fixed asset category not found for this tenant.");
        }

        await ResolveFixedAssetPostingAccountAsync(category.AssetAccountId, "fixed asset cost account", AccountType.Asset);
        return category;
    }

    private async Task<Account> ResolveFixedAssetPostingAccountAsync(Guid accountId, string role, params AccountType[] allowedTypes)
    {
        if (accountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Fixed asset {role} is required.");
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == accountId && !a.IsDeleted);
        if (account == null)
        {
            throw new InvalidOperationException($"Fixed asset {role} was not found for this tenant.");
        }

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Fixed asset {role} account '{account.AccountNumber}' is not active.");
        }

        if (!account.AllowDirectPosting)
        {
            throw new InvalidOperationException($"Fixed asset {role} account '{account.AccountNumber}' does not allow posting.");
        }

        if (allowedTypes.Length > 0 && !allowedTypes.Contains(account.AccountType))
        {
            throw new InvalidOperationException($"Fixed asset {role} account '{account.AccountNumber}' has an invalid account type.");
        }

        return account;
    }

    private async Task ResolveProcurementInventoryControlAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == accountId && !value.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The configured Inventory Control Account was not found for this tenant.");
        if (account.Status != AccountStatus.Active || account.AccountType != AccountType.Asset)
            throw new InvalidOperationException("The configured Inventory Control Account must be an active asset account.");

        // Control accounts normally disallow manual direct posting. FIN-INT-007 is a system-owned
        // reclassification through the central engine, so requiring AllowDirectPosting here would
        // incorrectly weaken the control-account policy just to support the integration.
    }

    private async Task<FinanceSettings> GetFinanceSettingsAsync()
    {
        return await _context.FinanceSettings
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
    }

    private static FinancePostingLineDto BuildCapitalizationPostingLine(
        Guid accountId,
        string description,
        decimal debitTransactionAmount,
        decimal creditTransactionAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate,
        Guid? exchangeRateId,
        DateTime? exchangeRateDate,
        string reference,
        int lineNumber,
        string notes,
        string transactionTag)
    {
        var isForeign = !string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = ToFunctionalAmount(debitTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate),
            CreditAmount = ToFunctionalAmount(creditTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate),
            TransactionCurrency = transactionCurrency,
            TransactionDebitAmount = debitTransactionAmount > 0m ? debitTransactionAmount : 0m,
            TransactionCreditAmount = creditTransactionAmount > 0m ? creditTransactionAmount : 0m,
            ForeignCurrencyAmount = isForeign
                ? debitTransactionAmount > 0m ? debitTransactionAmount : creditTransactionAmount
                : null,
            ExchangeRateId = isForeign ? exchangeRateId : null,
            ExchangeRate = isForeign ? exchangeRate : null,
            ExchangeRateSource = isForeign ? "Fixed asset capitalization exchange-rate snapshot" : null,
            ExchangeRateDate = isForeign ? exchangeRateDate?.Date : null,
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            Notes = notes,
            TransactionTag = transactionTag
        };
    }

    private static Guid DirectCapitalizationDocumentId(FixedAsset asset) =>
        asset.CapitalizationReversalPostingEventId.HasValue
            ? FinanceSourceLineIdentity.Create(
                asset.Id,
                "CAPITALIZATION-CYCLE",
                asset.CapitalizationReversalPostingEventId.Value)
            : asset.Id;

    private static List<FinancePostingLineDto> BuildDirectCapitalizationDimensionLines(
        FixedAsset asset,
        Guid sourceDocumentId,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal transactionAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate,
        Guid? exchangeRateId,
        DateTime? exchangeRateDate,
        string reference)
    {
        var debit = BuildCapitalizationPostingLine(
            debitAccountId,
            $"Capitalize fixed asset {asset.AssetCode}",
            transactionAmount,
            0m,
            transactionCurrency,
            functionalCurrency,
            exchangeRate,
            exchangeRateId,
            exchangeRateDate,
            reference,
            1,
            $"FixedAssetId={asset.Id:N}",
            "FA-Capitalization");
        debit.SourceDocumentLineId = FinanceSourceLineIdentity.Create(
            sourceDocumentId, "ASSET-COST", asset.Id);

        var credit = BuildCapitalizationPostingLine(
            creditAccountId,
            $"Clear capitalization source for fixed asset {asset.AssetCode}",
            0m,
            transactionAmount,
            transactionCurrency,
            functionalCurrency,
            exchangeRate,
            exchangeRateId,
            exchangeRateDate,
            reference,
            2,
            $"FixedAssetId={asset.Id:N}",
            "FA-Capitalization-Clearing");
        credit.SourceDocumentLineId = FinanceSourceLineIdentity.Create(
            sourceDocumentId, "CAPITALIZATION-CLEARING", asset.Id);
        return [debit, credit];
    }

    private void EnsureFixedAssetDimensionsConfigured()
    {
        if (_fixedAssetDimensions is null)
            throw new InvalidOperationException(
                "Finance fixed-asset dimensions are not configured for this posting route.");
    }

    private static bool IsFixedAssetLine(VendorInvoiceLineItem line)
        => string.Equals(line.LineItemType, "FixedAsset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(line.LineItemType, "Fixed Asset", StringComparison.OrdinalIgnoreCase)
            || line.FixedAssetId.HasValue;

    private static bool ReferencesApAssetLine(string? notes, Guid lineId, Guid assetId)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        return notes.Contains($"VendorInvoiceLineId={lineId:N}", StringComparison.OrdinalIgnoreCase)
            || notes.Contains($"FixedAssetId={assetId:N}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCapitalized(FixedAsset asset)
        => !asset.CapitalizationReversalPostingEventId.HasValue &&
            (asset.PostingEventId.HasValue
             || asset.JournalEntryId.HasValue
             || asset.Status is FixedAssetStatus.Capitalized or FixedAssetStatus.Active);

    private async Task<FixedAsset> LoadAssetForCapitalizationReversalAsync(
        Guid assetId,
        CancellationToken cancellationToken)
        => await _context.FixedAssets
            .Include(asset => asset.Category)
            .Include(asset => asset.BookValues)
            .SingleOrDefaultAsync(asset =>
                asset.TenantId == TenantId && asset.Id == assetId && !asset.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

    private static void EnsureDirectCapitalizationCanBeReversed(FixedAsset asset)
    {
        // Keep these validations separate so an operator or integrating module receives an
        // actionable explanation. FR-GL-008 requires the original event and journal to be linked;
        // silently treating incomplete lineage as an ordinary status problem would make a data or
        // integration defect unnecessarily difficult to diagnose.
        if (asset.CapitalizationReversalPostingEventId.HasValue)
            throw new InvalidOperationException("The current fixed asset capitalization has already been reversed.");
        if (!IsCapitalized(asset))
            throw new InvalidOperationException("Only a currently posted fixed asset capitalization can be reversed.");
        if (!asset.PostingEventId.HasValue || !asset.JournalEntryId.HasValue)
            throw new InvalidOperationException("The fixed asset capitalization is missing its original posting event or journal lineage and cannot be reversed safely.");
        if (!string.Equals(asset.SourceDocumentType, "FixedAsset", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(asset.SourceDocumentType, "ProcurementFixedAssetCapitalization", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "This capitalization belongs to a source document. Reverse that source document so its shared journal and the asset register remain synchronized.");
        }
    }

    private async Task MarkProcurementCapitalizationReversedAsync(
        FixedAsset asset,
        Guid reversalJournalEntryId,
        Guid reversalPostingEventId,
        DateTime reversedAt,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(asset.SourceDocumentType, "ProcurementFixedAssetCapitalization", StringComparison.OrdinalIgnoreCase) ||
            !asset.SourceDocumentId.HasValue)
            return;

        var handoff = await _context.Set<ProcurementFixedAssetCapitalization>().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == asset.SourceDocumentId.Value &&
            value.FixedAssetId == asset.Id && !value.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The Procurement capitalization handoff was not found for the approved reversal.");
        handoff.Status = ProcurementFixedAssetCapitalizationStatus.Reversed;
        handoff.ReversalJournalEntryId = reversalJournalEntryId;
        handoff.ReversalPostingEventId = reversalPostingEventId;
        handoff.ReversedAt = reversedAt;
        handoff.UpdatedAt = DateTime.UtcNow;
        handoff.UpdatedBy = UserName;
    }

    private async Task EnsureNoDownstreamAssetAccountingAsync(
        FixedAsset asset,
        CancellationToken cancellationToken)
    {
        if (asset.Status != FixedAssetStatus.Capitalized)
        {
            throw new InvalidOperationException(
                "Capitalization reversal is allowed only before activation, depreciation, valuation, transfer, disposal, or another asset lifecycle action.");
        }

        var hasPostedDepreciation = await _context.AssetDepreciationSchedules.AnyAsync(item =>
            item.TenantId == TenantId && item.FixedAssetId == asset.Id && item.IsPosted && !item.IsDeleted,
            cancellationToken);
        var hasPostedValuation = await _context.AssetValuations.AnyAsync(item =>
            item.TenantId == TenantId && item.FixedAssetId == asset.Id && item.IsPostedToGL && !item.IsDeleted,
            cancellationToken);
        var hasPostedTransfer = await _context.AssetTransfers.AnyAsync(item =>
            item.TenantId == TenantId && item.FixedAssetId == asset.Id && item.PostingEventId.HasValue && !item.IsDeleted,
            cancellationToken);
        var hasDisposal = await _context.AssetDisposals.AnyAsync(item =>
            item.TenantId == TenantId && item.FixedAssetId == asset.Id &&
            item.Status != AssetDisposalStatus.Rejected && item.Status != AssetDisposalStatus.Cancelled && !item.IsDeleted,
            cancellationToken);
        var hasOtherValueMovement = await _context.AssetTransactions.AnyAsync(item =>
            item.TenantId == TenantId && item.FixedAssetId == asset.Id &&
            item.TransactionType != "Capitalization" &&
            item.TransactionType != "CapitalizationReversal" &&
            !item.IsDeleted,
            cancellationToken);

        if (hasPostedDepreciation || hasPostedValuation || hasPostedTransfer || hasDisposal || hasOtherValueMovement)
        {
            throw new InvalidOperationException(
                "Capitalization cannot be reversed after downstream asset accounting exists. Reverse the later lifecycle entries in order before reversing capitalization.");
        }
    }

    private void ApplyCapitalizationReversal(
        FixedAsset asset,
        Guid reversalJournalEntryId,
        Guid reversalPostingEventId,
        DateTime reversalDate,
        string reason,
        FixedAssetCapitalizationReversal? request)
    {
        var reversedAt = DateTime.UtcNow;
        asset.PurchasePrice = 0m;
        asset.InstallationCost = 0m;
        asset.TaxAmount = 0m;
        asset.AcquisitionCost = 0m;
        asset.NetBookValue = 0m;
        asset.CapitalizationDate = null;
        asset.Status = FixedAssetStatus.Draft;
        asset.CapitalizationReversalJournalEntryId = reversalJournalEntryId;
        asset.CapitalizationReversalPostingEventId = reversalPostingEventId;
        asset.CapitalizationReversedAt = reversedAt;
        asset.CapitalizationReversalReason = reason;
        asset.UpdatedAt = reversedAt;
        asset.UpdatedBy = UserName;

        foreach (var bookValue in asset.BookValues.Where(value => !value.IsDeleted))
        {
            var amountReversed = bookValue.AcquisitionCost;
            bookValue.AcquisitionCost = 0m;
            bookValue.AccumulatedDepreciation = 0m;
            bookValue.NetBookValue = 0m;
            bookValue.CapitalizationDate = null;
            bookValue.CapitalizationReversalJournalEntryId = reversalJournalEntryId;
            bookValue.CapitalizationReversalPostingEventId = reversalPostingEventId;
            bookValue.CapitalizationReversedAt = reversedAt;
            bookValue.UpdatedAt = reversedAt;
            bookValue.UpdatedBy = UserName;

            // The negative register movement mirrors the compensating GL journal while preserving
            // the original positive capitalization transaction. Reports can therefore reconstruct
            // both gross activity and the zero current carrying value without destructive edits.
            _context.AssetTransactions.Add(new AssetTransaction
            {
                TenantId = TenantId,
                FixedAssetId = asset.Id,
                AccountingBookId = bookValue.AccountingBookId,
                BookClassification = bookValue.BookClassification,
                TransactionDate = reversalDate.Date,
                TransactionType = "CapitalizationReversal",
                Description = reason,
                Amount = -amountReversed,
                ResultingBookValue = 0m,
                RelatedEntityId = reversalPostingEventId,
                PerformedByUserId = CurrentUserGuid,
                CreatedAt = reversedAt,
                CreatedBy = UserName
            });
        }

        if (request != null)
        {
            request.Status = FixedAssetCapitalizationReversalStatuses.Posted;
            request.ReversalJournalEntryId = reversalJournalEntryId;
            request.ReversalPostingEventId = reversalPostingEventId;
            request.PostedAt = reversedAt;
            request.FailureReason = null;
            request.UpdatedAt = reversedAt;
            request.UpdatedBy = UserName;
            request.LastModifiedById = CurrentUserGuid == Guid.Empty ? null : CurrentUserGuid;
        }
    }

    private static FixedAssetCapitalizationReversalDto MapCapitalizationReversalToDto(
        FixedAssetCapitalizationReversal request,
        FixedAsset asset) => new()
    {
        Id = request.Id,
        FixedAssetId = request.FixedAssetId,
        AssetCode = asset.AssetCode,
        AssetName = asset.Name,
        OriginalPostingEventId = request.OriginalPostingEventId,
        OriginalJournalEntryId = request.OriginalJournalEntryId,
        ReversalPostingEventId = request.ReversalPostingEventId,
        ReversalJournalEntryId = request.ReversalJournalEntryId,
        Status = request.Status,
        Reason = request.Reason,
        ImpactAssessment = request.ImpactAssessment,
        RequestedReversalDate = request.RequestedReversalDate,
        RequestedByUserId = request.RequestedByUserId,
        RequestedByUserName = request.RequestedByUserName,
        RequestedAt = request.RequestedAt,
        ReviewedByUserId = request.ReviewedByUserId,
        ReviewedByUserName = request.ReviewedByUserName,
        ReviewedAt = request.ReviewedAt,
        ReviewComment = request.ReviewComment,
        PostedAt = request.PostedAt,
        FailureReason = request.FailureReason
    };

    private async Task<CapitalizeFixedAssetDto> ResolveApprovedDirectCapitalizationInstructionAsync(
        FixedAsset asset,
        CapitalizeFixedAssetDto requested,
        CancellationToken cancellationToken)
    {
        if (_workflowService == null)
        {
            return requested;
        }

        if (asset.Status != FixedAssetStatus.Acquired)
        {
            var message = asset.Status switch
            {
                FixedAssetStatus.PendingApproval => "Direct fixed asset capitalization is pending workflow approval.",
                FixedAssetStatus.Rejected => "Direct fixed asset capitalization was rejected and cannot be posted.",
                _ => "Direct fixed asset capitalization must be submitted and approved before posting."
            };

            await RecordFixedAssetAuditAsync(
                asset.Status == FixedAssetStatus.Rejected
                    ? FinanceAuditEvents.FinancePostingBlockedAfterRejection
                    : FinanceAuditEvents.FinancePostingBlockedPendingApproval,
                asset,
                afterValues: new { asset.Status },
                reason: message,
                comment: requested.Reason);
            throw new InvalidOperationException(message);
        }

        var snapshot = ReadVerifiedCapitalizationApprovalSnapshot(asset);
        if (!string.Equals(snapshot.SourceDocumentType, "FixedAsset", StringComparison.Ordinal) ||
            snapshot.SourceDocumentId != asset.Id)
        {
            throw new InvalidOperationException(
                "This approval snapshot belongs to a source-owned capitalization. Post it through the owning Finance adapter.");
        }
        if (!asset.CapitalizationApprovalApprovedAt.HasValue ||
            !asset.CapitalizationApprovalApprovedByUserId.HasValue ||
            asset.CapitalizationApprovalInvalidatedAt.HasValue)
        {
            throw new InvalidOperationException(
                "Direct fixed asset capitalization does not have a valid immutable approval snapshot.");
        }

        var currentEvidenceHash = BuildAssetCapitalizationEvidenceHash(asset);
        if (!string.Equals(currentEvidenceHash, snapshot.AssetEvidenceHash, StringComparison.Ordinal))
        {
            await InvalidateCapitalizationApprovalAsync(
                asset,
                "The asset's accounting evidence changed after capitalization approval.",
                cancellationToken);
        }

        var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
        if (category.AssetAccountId != snapshot.DebitAccountId ||
            (snapshot.UsesCategoryAucAccount && category.AucAccountId != snapshot.CreditAccountId))
        {
            await InvalidateCapitalizationApprovalAsync(
                asset,
                "The fixed-asset category posting-account configuration changed after capitalization approval.",
                cancellationToken);
        }

        // Posting deliberately reconstructs the instruction from checker-approved evidence. The
        // caller can provide an operational comment, but cannot replace the date, amount, account,
        // currency, rate or reference after approval.
        return new CapitalizeFixedAssetDto
        {
            CapitalizationDate = snapshot.CapitalizationDate,
            CreditAccountId = snapshot.CreditAccountId,
            Reference = snapshot.Reference,
            Reason = snapshot.Reason,
            Amount = snapshot.TransactionAmount,
            TransactionCurrencyCode = snapshot.TransactionCurrencyCode,
            ExchangeRate = snapshot.ExchangeRate,
            ExchangeRateId = snapshot.ExchangeRateId,
            ExchangeRateDate = snapshot.ExchangeRateDate
        };
    }

    private async Task<FixedAssetCapitalizationApprovalSnapshotDto?> ResolveApprovedSourceCapitalizationSnapshotAsync(
        FixedAsset asset,
        Guid capitalizationId,
        Guid purchaseOrderItemId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (_workflowService == null)
            return null;

        if (asset.Status == FixedAssetStatus.Acquired)
        {
            var snapshot = ReadVerifiedCapitalizationApprovalSnapshot(asset);
            if (!asset.CapitalizationApprovalApprovedAt.HasValue ||
                !asset.CapitalizationApprovalApprovedByUserId.HasValue ||
                asset.CapitalizationApprovalInvalidatedAt.HasValue)
                throw new InvalidOperationException("The source-owned fixed asset does not have a valid immutable approval snapshot.");
            if (!string.Equals(snapshot.SourceDocumentType, "ProcurementFixedAssetCapitalization", StringComparison.Ordinal) ||
                snapshot.SourceDocumentId != capitalizationId || snapshot.SourceDocumentLineId != purchaseOrderItemId)
                throw new InvalidOperationException("The approved capitalization evidence does not match this Procurement handoff.");
            if (!string.Equals(BuildAssetCapitalizationEvidenceHash(asset), snapshot.AssetEvidenceHash, StringComparison.Ordinal))
                await InvalidateCapitalizationApprovalAsync(
                    asset,
                    "The asset's accounting evidence changed after Procurement capitalization approval.",
                    cancellationToken);

            var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
            var settings = await GetFinanceSettingsAsync();
            if (category.AssetAccountId != snapshot.DebitAccountId ||
                settings.ControlAccountInventoryId != snapshot.CreditAccountId)
                await InvalidateCapitalizationApprovalAsync(
                    asset,
                    "The asset or inventory-control account configuration changed after Procurement capitalization approval.",
                    cancellationToken);
            return snapshot;
        }

        var message = asset.Status switch
        {
            FixedAssetStatus.PendingApproval => "Fixed asset capitalization is pending workflow approval.",
            FixedAssetStatus.Rejected => "Fixed asset capitalization was rejected and cannot be posted.",
            _ => "Fixed asset capitalization must be submitted and approved before posting."
        };
        await RecordFixedAssetAuditAsync(
            asset.Status == FixedAssetStatus.Rejected
                ? FinanceAuditEvents.FinancePostingBlockedAfterRejection
                : FinanceAuditEvents.FinancePostingBlockedPendingApproval,
            asset,
            afterValues: new { asset.Status, SourceOwnedCapitalization = true },
            reason: message,
            comment: reason);
        throw new InvalidOperationException(message);
    }

    private static FixedAssetCapitalizationApprovalSnapshotDto? TryReadCapitalizationApprovalSnapshot(FixedAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.CapitalizationApprovalSnapshotJson) ||
            string.IsNullOrWhiteSpace(asset.CapitalizationApprovalSnapshotHash))
            return null;

        try
        {
            return ReadVerifiedCapitalizationApprovalSnapshot(asset);
        }
        catch (InvalidOperationException)
        {
            // The API still returns the asset so operators can investigate corrupt historical
            // evidence. Posting and approval remain fail-closed through the strict reader.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<FixedAssetCapitalizationApprovalSnapshotDto> BuildCapitalizationApprovalSnapshotAsync(
        FixedAsset asset,
        SubmitFixedAssetCapitalizationDto dto,
        CancellationToken cancellationToken)
    {
        var procurementHandoff = await _context.Set<ProcurementFixedAssetCapitalization>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.FixedAssetId == asset.Id &&
                value.Status == ProcurementFixedAssetCapitalizationStatus.Draft && !value.IsDeleted,
                cancellationToken);
        if (procurementHandoff != null)
            return await BuildProcurementCapitalizationApprovalSnapshotAsync(
                asset,
                procurementHandoff,
                dto,
                cancellationToken);

        var capitalizationDate = dto.CapitalizationDate.Date;
        if (capitalizationDate == default)
            throw new InvalidOperationException("Capitalization date is required.");

        var reason = NormalizeRequiredText(dto.Reason);
        if (reason.Length < 5)
            throw new InvalidOperationException("A substantive capitalization reason is required.");
        if (reason.Length > 1000)
            throw new InvalidOperationException("Capitalization reason cannot exceed 1000 characters.");

        var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
        var debitAccount = await ResolveFixedAssetPostingAccountAsync(
            category.AssetAccountId,
            "fixed asset cost account",
            AccountType.Asset);
        var usesCategoryAuc = !dto.CreditAccountId.HasValue;
        var creditAccountId = dto.CreditAccountId ?? category.AucAccountId
            ?? throw new InvalidOperationException(
                "Direct fixed asset capitalization requires a category AUC/CIP clearing account or an explicit credit account.");
        await ResolveFixedAssetPostingAccountAsync(
            creditAccountId,
            "fixed asset capitalization credit account",
            AccountType.Asset,
            AccountType.Liability,
            AccountType.Equity);

        var settings = await GetFinanceSettingsAsync();
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var transactionCurrency = NormalizeCurrency(
            dto.TransactionCurrencyCode ?? asset.TransactionCurrencyCode,
            functionalCurrency);
        if (transactionCurrency.Length != 3)
            throw new InvalidOperationException("Transaction currency must be a three-character ISO currency code.");

        var transactionAmount = RoundMoney(dto.Amount ?? asset.AcquisitionCost);
        if (transactionAmount <= 0m)
            throw new InvalidOperationException("Fixed asset capitalization amount must be greater than zero.");

        var exchangeRateDate = (dto.ExchangeRateDate ?? capitalizationDate).Date;
        decimal exchangeRate;
        Guid? exchangeRateId;
        if (string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            exchangeRate = 1m;
            exchangeRateId = null;
            exchangeRateDate = capitalizationDate;
        }
        else
        {
            exchangeRate = dto.ExchangeRate
                ?? throw new InvalidOperationException("Foreign-currency capitalization requires an approved exchange-rate value.");
            if (exchangeRate <= 0m)
                throw new InvalidOperationException("Foreign-currency capitalization exchange rate must be greater than zero.");
            exchangeRateId = dto.ExchangeRateId
                ?? throw new InvalidOperationException("Foreign-currency capitalization requires an approved exchange-rate record ID.");

            var rate = await _context.ExchangeRates.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == exchangeRateId.Value && !value.IsDeleted,
                cancellationToken)
                ?? throw new InvalidOperationException("The selected exchange rate was not found for this tenant.");
            if (!rate.IsActive || rate.ApprovalStatus is not (RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved))
                throw new InvalidOperationException("The selected exchange rate must be active and approved.");
            if (!string.Equals(rate.BaseCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rate.TargetCurrencyCode, transactionCurrency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected exchange rate currency pair does not match the capitalization currencies.");
            if (rate.EffectiveDate.Date > exchangeRateDate ||
                (rate.EndDate.HasValue && rate.EndDate.Value.Date < exchangeRateDate))
                throw new InvalidOperationException("The selected exchange rate is not effective for the capitalization rate date.");
            if (decimal.Round(rate.Rate, 6) != decimal.Round(exchangeRate, 6))
                throw new InvalidOperationException("The supplied exchange-rate value does not match the selected approved record.");
        }

        var reference = NormalizeRequiredText(dto.Reference);
        if (string.IsNullOrWhiteSpace(reference))
            reference = asset.AssetCode;
        if (reference.Length > 100)
            throw new InvalidOperationException("Capitalization reference cannot exceed 100 characters.");

        return new FixedAssetCapitalizationApprovalSnapshotDto
        {
            Version = 1,
            FixedAssetId = asset.Id,
            AssetCode = asset.AssetCode,
            FixedAssetCategoryId = asset.FixedAssetCategoryId,
            DebitAccountId = debitAccount.Id,
            CreditAccountId = creditAccountId,
            UsesCategoryAucAccount = usesCategoryAuc,
            CapitalizationDate = capitalizationDate,
            TransactionAmount = transactionAmount,
            FunctionalCurrencyCode = functionalCurrency,
            TransactionCurrencyCode = transactionCurrency,
            ExchangeRate = decimal.Round(exchangeRate, 6),
            ExchangeRateId = exchangeRateId,
            ExchangeRateDate = exchangeRateDate,
            Reference = reference,
            Reason = reason,
            SourceDocumentType = "FixedAsset",
            SourceDocumentId = asset.Id,
            SourceDocumentLineId = null,
            AssetEvidenceHash = BuildAssetCapitalizationEvidenceHash(asset)
        };
    }

    private async Task<FixedAssetCapitalizationApprovalSnapshotDto> BuildProcurementCapitalizationApprovalSnapshotAsync(
        FixedAsset asset,
        ProcurementFixedAssetCapitalization handoff,
        SubmitFixedAssetCapitalizationDto dto,
        CancellationToken cancellationToken)
    {
        var category = await ResolveAssetCategoryAsync(asset.FixedAssetCategoryId);
        var debitAccount = await ResolveFixedAssetPostingAccountAsync(
            category.AssetAccountId,
            "fixed asset cost account",
            AccountType.Asset);
        var settings = await GetFinanceSettingsAsync();
        var inventoryControlAccountId = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
        await ResolveProcurementInventoryControlAccountAsync(inventoryControlAccountId, cancellationToken);

        var reason = NormalizeRequiredText(dto.Reason);
        if (string.IsNullOrWhiteSpace(reason))
            reason = NormalizeRequiredText(dto.Comments);
        if (string.IsNullOrWhiteSpace(reason))
            reason = $"Capitalize accepted Procurement supply {handoff.AcceptedSupplyReference}.";
        if (reason.Length > 1000)
            throw new InvalidOperationException("Capitalization reason cannot exceed 1000 characters.");

        return new FixedAssetCapitalizationApprovalSnapshotDto
        {
            Version = 1,
            FixedAssetId = asset.Id,
            AssetCode = asset.AssetCode,
            FixedAssetCategoryId = asset.FixedAssetCategoryId,
            DebitAccountId = debitAccount.Id,
            CreditAccountId = inventoryControlAccountId,
            UsesCategoryAucAccount = false,
            CapitalizationDate = handoff.CapitalizationDate.Date,
            TransactionAmount = RoundMoney(handoff.FunctionalAmount),
            FunctionalCurrencyCode = NormalizeCurrency(handoff.FunctionalCurrencyCode, "GHS"),
            TransactionCurrencyCode = NormalizeCurrency(handoff.FunctionalCurrencyCode, "GHS"),
            ExchangeRate = 1m,
            ExchangeRateId = null,
            ExchangeRateDate = handoff.CapitalizationDate.Date,
            Reference = handoff.AcceptedSupplyReference,
            Reason = reason,
            SourceDocumentType = "ProcurementFixedAssetCapitalization",
            SourceDocumentId = handoff.Id,
            SourceDocumentLineId = handoff.PurchaseOrderItemId,
            AssetEvidenceHash = BuildAssetCapitalizationEvidenceHash(asset)
        };
    }

    private void ApplyCapitalizationApprovalSubmission(
        FixedAsset asset,
        FixedAssetCapitalizationApprovalSnapshotDto snapshot,
        string snapshotJson,
        string snapshotHash,
        Guid? workflowInstanceId)
    {
        var now = DateTime.UtcNow;
        asset.CapitalizationApprovalSnapshotJson = snapshotJson;
        asset.CapitalizationApprovalSnapshotHash = snapshotHash;
        asset.CapitalizationApprovalExchangeRateId = snapshot.ExchangeRateId;
        asset.CapitalizationApprovalWorkflowInstanceId = workflowInstanceId;
        asset.CapitalizationApprovalSubmittedByUserId = CurrentUserGuid == Guid.Empty ? null : CurrentUserGuid;
        asset.CapitalizationApprovalSubmittedAt = now;
        asset.CapitalizationApprovalApprovedByUserId = null;
        asset.CapitalizationApprovalApprovedAt = null;
        asset.CapitalizationApprovalInvalidatedAt = null;
        asset.CapitalizationApprovalInvalidationReason = null;
    }

    private static void ClearCapitalizationApproval(FixedAsset asset)
    {
        asset.CapitalizationApprovalSnapshotJson = null;
        asset.CapitalizationApprovalSnapshotHash = null;
        asset.CapitalizationApprovalExchangeRateId = null;
        asset.CapitalizationApprovalWorkflowInstanceId = null;
        asset.CapitalizationApprovalSubmittedByUserId = null;
        asset.CapitalizationApprovalSubmittedAt = null;
        asset.CapitalizationApprovalApprovedByUserId = null;
        asset.CapitalizationApprovalApprovedAt = null;
        asset.CapitalizationApprovalInvalidatedAt = null;
        asset.CapitalizationApprovalInvalidationReason = null;
    }

    private static FixedAssetCapitalizationApprovalSnapshotDto ReadVerifiedCapitalizationApprovalSnapshot(FixedAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.CapitalizationApprovalSnapshotJson) ||
            string.IsNullOrWhiteSpace(asset.CapitalizationApprovalSnapshotHash))
            throw new InvalidOperationException("Direct fixed asset capitalization is missing its immutable approval snapshot.");

        var currentHash = HashCapitalizationEvidence(asset.CapitalizationApprovalSnapshotJson);
        if (!FixedTimeHashEquals(currentHash, asset.CapitalizationApprovalSnapshotHash))
            throw new InvalidOperationException("Direct fixed asset capitalization approval evidence failed its integrity check.");

        var snapshot = JsonSerializer.Deserialize<FixedAssetCapitalizationApprovalSnapshotDto>(
            asset.CapitalizationApprovalSnapshotJson,
            CapitalizationSnapshotJsonOptions)
            ?? throw new InvalidOperationException("Direct fixed asset capitalization approval evidence is invalid.");
        if (snapshot.Version != 1 || snapshot.FixedAssetId != asset.Id)
            throw new InvalidOperationException("Direct fixed asset capitalization approval evidence does not match this asset.");
        return snapshot;
    }

    private static string BuildAssetCapitalizationEvidenceHash(FixedAsset asset)
        => HashCapitalizationEvidence(JsonSerializer.Serialize(new
        {
            asset.Id,
            asset.AssetCode,
            asset.FixedAssetCategoryId,
            PurchaseDate = asset.PurchaseDate.Date,
            PurchasePrice = RoundMoney(asset.PurchasePrice),
            InstallationCost = RoundMoney(asset.InstallationCost),
            TaxAmount = RoundMoney(asset.TaxAmount),
            AcquisitionCost = RoundMoney(asset.AcquisitionCost),
            asset.TransactionCurrencyCode,
            asset.ExchangeRate,
            asset.ExchangeRateId,
            ExchangeRateDate = asset.ExchangeRateDate?.Date,
            asset.SourceDocumentType,
            asset.SourceDocumentId,
            asset.SourceDocumentLineId
        }, CapitalizationSnapshotJsonOptions));

    private async Task InvalidateCapitalizationApprovalAsync(
        FixedAsset asset,
        string reason,
        CancellationToken cancellationToken)
    {
        asset.Status = FixedAssetStatus.Draft;
        asset.CapitalizationApprovalInvalidatedAt = DateTime.UtcNow;
        asset.CapitalizationApprovalInvalidationReason = reason;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FinancePostingBlockedPendingApproval,
            asset,
            afterValues: new
            {
                asset.Status,
                asset.CapitalizationApprovalSnapshotHash,
                asset.CapitalizationApprovalInvalidatedAt
            },
            reason: reason,
            comment: "The stale capitalization approval was retired; submit current evidence for a new decision.");
        throw new InvalidOperationException($"{reason} The stale approval was retired; submit the current capitalization evidence again.");
    }

    private static string HashCapitalizationEvidence(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeHashEquals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasOpeningImportBasis(FixedAsset asset)
        => asset.BookValues.Any(value =>
            !value.IsDeleted &&
            (value.OpeningPostedToGl ||
             value.OpeningSource.Contains("Opening", StringComparison.OrdinalIgnoreCase)));

    private static void ValidateCapitalizedAssetUpdate(
        FixedAsset asset,
        UpdateFixedAssetDto dto,
        decimal requestedAcquisitionCost)
    {
        if (asset.FixedAssetCategoryId != dto.FixedAssetCategoryId ||
            asset.PurchaseDate.Date != dto.PurchaseDate.Date ||
            RoundMoney(asset.PurchasePrice) != RoundMoney(dto.PurchasePrice) ||
            RoundMoney(asset.InstallationCost) != RoundMoney(dto.InstallationCost) ||
            RoundMoney(asset.TaxAmount) != RoundMoney(dto.TaxAmount) ||
            RoundMoney(asset.AcquisitionCost) != RoundMoney(requestedAcquisitionCost))
        {
            throw new InvalidOperationException("Capitalized fixed asset accounting fields cannot be edited destructively.");
        }

        if (asset.DepreciationMethod != dto.DepreciationMethod ||
            asset.DepreciationConvention != dto.DepreciationConvention ||
            asset.UsefulLifeMonths != dto.UsefulLifeMonths ||
            RoundMoney(asset.ResidualValue) != RoundMoney(dto.ResidualValue) ||
            RoundRate(asset.DiminishingBalanceRatePercent) != RoundRate(dto.DiminishingBalanceRatePercent) ||
            RoundUnits(asset.LifetimeProductionCapacity) != RoundUnits(dto.LifetimeProductionCapacity) ||
            asset.PlacedInServiceDate?.Date != dto.PlacedInServiceDate?.Date)
        {
            throw new InvalidOperationException("Capitalized fixed asset depreciation assumptions cannot be edited through the normal update path.");
        }

        if (dto.Status is FixedAssetStatus.Draft or FixedAssetStatus.Acquired)
        {
            throw new InvalidOperationException("Capitalized fixed asset status cannot be moved back to a pre-capitalization state.");
        }
    }

    private static void ValidateDepreciationConfiguration(
        DepreciationMethod method,
        int usefulLifeMonths,
        decimal residualValue,
        decimal diminishingBalanceRatePercent,
        decimal lifetimeProductionCapacity)
    {
        if (usefulLifeMonths <= 0)
        {
            throw new InvalidOperationException("Fixed asset useful life must be greater than zero.");
        }

        if (residualValue < 0m)
        {
            throw new InvalidOperationException("Fixed asset residual value cannot be negative.");
        }

        // These rejections are intentional accounting guardrails. The enum retains historical
        // values for compatibility, but TDC's approved catalogue excludes methods without an
        // evidenced IAS 16 consumption pattern.
        if (method is DepreciationMethod.SumOfYearsDigits or DepreciationMethod.None)
        {
            throw new InvalidOperationException("TDC supports straight-line, diminishing-balance, double-declining, and units-of-production depreciation only.");
        }

        if (method == DepreciationMethod.DecliningBalance &&
            (diminishingBalanceRatePercent <= 0m || diminishingBalanceRatePercent > 100m))
        {
            throw new InvalidOperationException("Diminishing-balance depreciation requires an annual rate greater than 0% and no more than 100%.");
        }

        if (method == DepreciationMethod.DoubleDecliningBalance &&
            (diminishingBalanceRatePercent < 0m || diminishingBalanceRatePercent > 100m))
        {
            throw new InvalidOperationException("A double-declining override rate must be between 0% and 100%; zero uses 200% divided by useful life in years.");
        }

        if (method == DepreciationMethod.UnitsOfProduction && lifetimeProductionCapacity <= 0m)
        {
            throw new InvalidOperationException("Units-of-production depreciation requires a positive lifetime production capacity.");
        }
    }

    private static decimal RoundRate(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal RoundUnits(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private async Task RecordFixedAssetAuditAsync(
        string eventType,
        FixedAsset asset,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        object? beforeValues = null,
        object? afterValues = null,
        string? reason = null,
        string? comment = null)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = asset.TenantId,
            SourceModule = "FA",
            SourceDocumentType = "FixedAsset",
            SourceDocumentId = asset.Id,
            JournalEntryId = journalEntryId ?? asset.JournalEntryId,
            PostingEventId = postingEventId ?? asset.PostingEventId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Resource = "Finance.FixedAsset",
            ResourceId = asset.Id.ToString()
        });
    }

    private static decimal ToFunctionalAmount(
        decimal transactionAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate)
    {
        if (transactionAmount == 0m)
        {
            return 0m;
        }

        return string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            ? RoundMoney(transactionAmount)
            : RoundMoney(transactionAmount * NormalizeExchangeRate(exchangeRate));
    }

    private static decimal NormalizeExchangeRate(decimal exchangeRate)
        => exchangeRate <= 0m ? 1m : exchangeRate;

    private static string NormalizeCurrency(string? currencyCode, string defaultValue)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? defaultValue.Trim().ToUpperInvariant()
            : currencyCode.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    // ========== Lifecycle Management ==========

    public async Task<FixedAssetDto> ActivateAsync(Guid id, DateTime? placedInServiceDate)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status is not (FixedAssetStatus.Capitalized or FixedAssetStatus.Acquired))
            throw new InvalidOperationException($"Cannot activate an asset in '{asset.Status}' status. Only acquired or capitalized assets can be activated.");

        if (!asset.PostingEventId.HasValue && !asset.JournalEntryId.HasValue && !HasOpeningImportBasis(asset))
        {
            throw new InvalidOperationException("Fixed asset cannot be activated before capitalization or approved opening/import setup.");
        }

        asset.Status = FixedAssetStatus.Active;
        asset.PlacedInServiceDate = placedInServiceDate ?? DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = id,
            TransactionDate = DateTime.UtcNow,
            TransactionType = "Activation",
            Description = $"Asset activated and placed in service on {asset.PlacedInServiceDate:yyyy-MM-dd}",
            Amount = 0,
            ResultingBookValue = asset.NetBookValue,
            PerformedByUserId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FixedAssetActivated,
            asset,
            afterValues: new { asset.Status, asset.PlacedInServiceDate },
            comment: "Fixed asset activated after capitalization/opening validation.");

        return await GetByIdAsync(asset.Id) ?? MapToDto(asset);
    }

    public async Task<FixedAssetDto> PutOnHoldAsync(Guid id, string reason)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status != FixedAssetStatus.Active)
            throw new InvalidOperationException($"Cannot put on hold an asset in '{asset.Status}' status. Only Active assets can be held.");

        asset.Status = FixedAssetStatus.OnHold;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = id,
            TransactionDate = DateTime.UtcNow,
            TransactionType = "Hold",
            Description = $"Asset put on hold: {reason}",
            Amount = 0,
            ResultingBookValue = asset.NetBookValue,
            PerformedByUserId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return MapToDto(asset);
    }

    public async Task<FixedAssetDto> ResumeAsync(Guid id)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status != FixedAssetStatus.OnHold)
            throw new InvalidOperationException($"Cannot resume an asset in '{asset.Status}' status. Only OnHold assets can be resumed.");

        asset.Status = FixedAssetStatus.Active;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = id,
            TransactionDate = DateTime.UtcNow,
            TransactionType = "Resume",
            Description = "Asset resumed from hold — depreciation resumes",
            Amount = 0,
            ResultingBookValue = asset.NetBookValue,
            PerformedByUserId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : Guid.Empty,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return MapToDto(asset);
    }

    // ========== Dashboard ==========

    public async Task<FixedAssetDashboardDto> GetDashboardAsync()
    {
        var assets = await _context.FixedAssets
            .Where(a => a.TenantId == TenantId)
            .Include(a => a.Category)
            .ToListAsync();

        var dashboard = new FixedAssetDashboardDto
        {
            TotalAssets = assets.Count,
            ActiveAssets = assets.Count(a => a.Status == FixedAssetStatus.Active),
            DisposedAssets = assets.Count(a => a.Status == FixedAssetStatus.Disposed),
            OnHoldAssets = assets.Count(a => a.Status == FixedAssetStatus.OnHold),
            DraftAssets = assets.Count(a => a.Status == FixedAssetStatus.Draft),
            TotalAcquisitionCost = assets.Sum(a => a.AcquisitionCost),
            TotalNetBookValue = assets.Sum(a => a.NetBookValue),
            TotalAccumulatedDepreciation = assets.Sum(a => a.AcquisitionCost - a.NetBookValue),
        };

        // Category breakdown
        dashboard.CategoryBreakdown = assets
            .GroupBy(a => new { a.FixedAssetCategoryId, CategoryName = a.Category?.Name ?? "Uncategorized" })
            .Select(g => new AssetCategorySummaryDto
            {
                CategoryId = g.Key.FixedAssetCategoryId,
                CategoryName = g.Key.CategoryName,
                AssetCount = g.Count(),
                TotalCost = g.Sum(a => a.AcquisitionCost),
                TotalNbv = g.Sum(a => a.NetBookValue)
            })
            .OrderByDescending(c => c.TotalCost)
            .ToList();

        // Recent activity (last 10 transactions)
        dashboard.RecentActivity = await _context.AssetTransactions
            .Where(t => t.TenantId == TenantId)
            .Include(t => t.FixedAsset)
            .OrderByDescending(t => t.TransactionDate)
            .Take(10)
            .Select(t => new AssetActivityItemDto
            {
                Date = t.TransactionDate,
                Type = t.TransactionType,
                Description = t.Description ?? "",
                AssetCode = t.FixedAsset != null ? t.FixedAsset.AssetCode : null,
                Amount = t.Amount
            })
            .ToListAsync();

        // Pending actions
        dashboard.PendingTransfers = await _context.AssetTransfers
            .CountAsync(t => t.TenantId == TenantId && t.Status == AssetTransferStatus.PendingApproval);
        dashboard.PendingDisposals = await _context.AssetDisposals
            .CountAsync(d => d.TenantId == TenantId && d.Status == AssetDisposalStatus.PendingApproval);
        dashboard.PendingVerifications = await _context.AssetVerificationSessions
            .CountAsync(v => v.TenantId == TenantId && v.Status == VerificationSessionStatus.InProgress);

        return dashboard;
    }

    // ========== Asset Code Generation ==========

    public async Task<string> GenerateAssetCodeAsync(Guid categoryId)
    {
        var category = await _context.FixedAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == categoryId)
            ?? throw new KeyNotFoundException("Category not found.");

        // Generate: FA-{CategoryCodePrefix}-{Year}-{Sequence}
        var prefix = category.Code?.Length >= 3 ? category.Code[..3].ToUpper() : (category.Code ?? "GEN").ToUpper();
        var year = DateTime.UtcNow.Year;
        var pattern = $"FA-{prefix}-{year}-";

        // Find highest existing sequence
        var existingCodes = await _context.FixedAssets
            .Where(a => a.TenantId == TenantId && a.AssetCode != null && a.AssetCode.StartsWith(pattern))
            .Select(a => a.AssetCode)
            .ToListAsync();

        var maxSequence = existingCodes
            .Select(code =>
            {
                var parts = code?.Split('-');
                if (parts != null && parts.Length >= 4 && int.TryParse(parts[^1], out var seq))
                    return seq;
                return 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{pattern}{(maxSequence + 1):D4}";
    }
    }
}
