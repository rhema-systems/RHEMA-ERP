using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Globalization;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class FixedAssetService : IFixedAssetService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IAccountingBookService? _accountingBookService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IWorkflowService? _workflowService;

        public FixedAssetService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IAccountingBookService? accountingBookService = null,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowService? workflowService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _accountingBookService = accountingBookService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _workflowService = workflowService;
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

        return asset == null ? null : MapToDto(asset);
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
            asset.Status = dto.Status;
            asset.DisposalDate = dto.DisposalDate;
        }
        asset.MaintenanceAssetId = dto.MaintenanceAssetId;
        asset.SerialNumber = dto.SerialNumber;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

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
            _context.ChangeTracker.Clear();
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
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
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
                comment: comments);
            throw new InvalidOperationException("Rejected fixed asset capitalization requests cannot be submitted again without updating the asset.");
        }

        if (_workflowService == null)
        {
            asset.Status = FixedAssetStatus.Acquired;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            return MapToDto(asset);
        }

        asset.Status = FixedAssetStatus.PendingApproval;
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
                comment: comments);
            throw new InvalidOperationException(workflowResult.Message ?? "Fixed asset capitalization workflow could not be started.");
        }

        await RecordFixedAssetAuditAsync(
            FinanceAuditEvents.FinanceWorkflowSubmitted,
            asset,
            afterValues: new
            {
                asset.Status,
                workflowResult.WorkflowInstanceId,
                Action = "DirectCapitalization"
            },
            comment: comments ?? "Fixed asset direct capitalization submitted for workflow approval.");

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

        await EnsureDirectCapitalizationApprovedAsync(asset, dto.Reason);

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

            var request = new FinancePostingRequestDto
            {
                SourceModule = "FA",
                SourceDocumentType = "FixedAsset",
                SourceDocumentId = asset.Id,
                SourceDocumentTenantId = asset.TenantId,
                PostingAction = "Capitalize",
                SourceDocumentReference = dto.Reference ?? asset.AssetCode,
                Description = $"Fixed asset capitalization - {asset.AssetCode} - {asset.Name}",
                PostingDate = dto.CapitalizationDate.Date,
                JournalType = "Fixed Asset Capitalization",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"FA:FixedAsset:{asset.TenantId:N}:{asset.Id:N}:Capitalize",
                ReturnExistingOnDuplicate = true,
                Lines = new[]
                {
                    BuildCapitalizationPostingLine(
                        assetAccount.Id,
                        $"Capitalize fixed asset {asset.AssetCode}",
                        transactionAmount,
                        0m,
                        transactionCurrency,
                        functionalCurrency,
                        exchangeRate,
                        dto.ExchangeRateId ?? asset.ExchangeRateId,
                        dto.ExchangeRateDate ?? asset.ExchangeRateDate ?? dto.CapitalizationDate.Date,
                        dto.Reference ?? asset.AssetCode,
                        1,
                        $"FixedAssetId={asset.Id:N}",
                        "FA-Capitalization"),
                    BuildCapitalizationPostingLine(
                        creditAccountId,
                        $"Clear capitalization source for fixed asset {asset.AssetCode}",
                        0m,
                        transactionAmount,
                        transactionCurrency,
                        functionalCurrency,
                        exchangeRate,
                        dto.ExchangeRateId ?? asset.ExchangeRateId,
                        dto.ExchangeRateDate ?? asset.ExchangeRateDate ?? dto.CapitalizationDate.Date,
                        dto.Reference ?? asset.AssetCode,
                        2,
                        $"FixedAssetId={asset.Id:N}",
                        "FA-Capitalization-Clearing")
                }
            };

            var postingResult = await _financePostingEngine.PostAsync(request);
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
            bookValue.PlacedInServiceDate = asset.PlacedInServiceDate;
            bookValue.CapitalizationDate = capitalizationDate.Date;
            bookValue.CapitalizationJournalEntryId = journalEntryId;
            bookValue.CapitalizationPostingEventId = postingEventId;
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
        => asset.PostingEventId.HasValue
            || asset.JournalEntryId.HasValue
            || asset.Status is FixedAssetStatus.Capitalized or FixedAssetStatus.Active;

    private async Task EnsureDirectCapitalizationApprovedAsync(FixedAsset asset, string? reason)
    {
        if (_workflowService == null)
        {
            return;
        }

        if (asset.Status == FixedAssetStatus.Acquired)
        {
            return;
        }

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
            comment: reason);

        throw new InvalidOperationException(message);
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
            asset.PlacedInServiceDate?.Date != dto.PlacedInServiceDate?.Date)
        {
            throw new InvalidOperationException("Capitalized fixed asset depreciation assumptions cannot be edited through the normal update path.");
        }

        if (dto.Status is FixedAssetStatus.Draft or FixedAssetStatus.Acquired)
        {
            throw new InvalidOperationException("Capitalized fixed asset status cannot be moved back to a pre-capitalization state.");
        }
    }

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
