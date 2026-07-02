using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Globalization;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class FixedAssetService : IFixedAssetService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public FixedAssetService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
    private string UserName => _currentUser.UserName ?? "system";

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
            .Where(a => a.TenantId == TenantId && a.Id == id)
            .FirstOrDefaultAsync();

        return asset == null ? null : MapToDto(asset);
    }

    public async Task<IEnumerable<FixedAssetDto>> GetAllAsync()
    {
        var assets = await _context.FixedAssets
            .Include(a => a.Category)
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

        var categoryExists = await _context.FixedAssetCategories
            .AnyAsync(c => c.TenantId == TenantId && c.Id == dto.FixedAssetCategoryId);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Fixed asset category not found.");
        }

        var acquisitionCost = dto.AcquisitionCost
            ?? (dto.PurchasePrice + dto.InstallationCost + dto.TaxAmount);

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

        _context.FixedAssets.Add(asset);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(asset.Id) ?? throw new InvalidOperationException("Failed to create fixed asset.");
    }

    public async Task<FixedAssetDto> UpdateAsync(Guid id, UpdateFixedAssetDto dto)
    {
        var asset = await _context.FixedAssets
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

        var categoryExists = await _context.FixedAssetCategories
            .AnyAsync(c => c.TenantId == TenantId && c.Id == dto.FixedAssetCategoryId);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Fixed asset category not found.");
        }

        var acquisitionCost = dto.AcquisitionCost
            ?? (dto.PurchasePrice + dto.InstallationCost + dto.TaxAmount);

        asset.AssetCode = assetCode;
        asset.Name = assetName;
        asset.Description = NormalizeOptionalText(dto.Description);
        asset.Location = NormalizeOptionalText(dto.Location);
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
        asset.MaintenanceAssetId = dto.MaintenanceAssetId;
        asset.SerialNumber = dto.SerialNumber;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        if (asset.Status == ErpSystem.Core.Enums.FixedAssetStatus.Draft)
        {
            asset.NetBookValue = acquisitionCost;
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(asset.Id) ?? throw new InvalidOperationException("Failed to update fixed asset.");
    }

    public async Task DeleteAsync(Guid id)
    {
        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

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
            FixedAssetCategoryId = asset.FixedAssetCategoryId,
            FixedAssetCategoryName = asset.Category?.Name,
            PurchaseDate = asset.PurchaseDate,
            PlacedInServiceDate = asset.PlacedInServiceDate,
            PurchasePrice = asset.PurchasePrice,
            InstallationCost = asset.InstallationCost,
            TaxAmount = asset.TaxAmount,
            AcquisitionCost = asset.AcquisitionCost,
            NetBookValue = asset.NetBookValue,
            DepreciationMethod = asset.DepreciationMethod,
            DepreciationConvention = asset.DepreciationConvention,
            UsefulLifeMonths = asset.UsefulLifeMonths,
            ResidualValue = asset.ResidualValue,
            Status = asset.Status,
            DisposalDate = asset.DisposalDate,
            MaintenanceAssetId = asset.MaintenanceAssetId,
            SerialNumber = asset.SerialNumber,
            CreatedAt = asset.CreatedAt,
            CreatedBy = asset.CreatedBy,
            UpdatedAt = asset.UpdatedAt,
            UpdatedBy = asset.UpdatedBy
        };
    }

    public async Task<BulkImportResultDto> ImportAssetsFromExcelAsync(Stream fileStream, string fileName, bool dryRun = false)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        var result = new BulkImportResultDto { IsDryRun = dryRun };
        var rowsToImport = new List<(int RowNumber, BulkAssetImportRowDto Data, Guid CategoryId)>();

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet?.Dimension == null)
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

        var rowCount = worksheet.Dimension?.Rows ?? 0;

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
        var purchasePriceColumn = FindColumn(headerMap, "Purchase Price", "Cost", "Asset Cost");
        var installationCostColumn = FindColumn(headerMap, "Installation Cost", "Installation");
        var taxAmountColumn = FindColumn(headerMap, "Tax Amount", "Tax", "VAT");
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

        // Load existing asset codes for duplicate check
        var existingCodesList = await _context.FixedAssets
            .Where(a => a.TenantId == TenantId)
            .Select(a => a.AssetCode)
            .ToListAsync();
        var existingCodes = new HashSet<string>(
            existingCodesList.Select(NormalizeRequiredText),
            StringComparer.OrdinalIgnoreCase);
        var importCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                PurchasePrice = ParseDecimal(worksheet, row, purchasePriceColumn),
                InstallationCost = ParseDecimal(worksheet, row, installationCostColumn),
                TaxAmount = ParseDecimal(worksheet, row, taxAmountColumn),
                UsefulLifeMonths = ParseInt(worksheet, row, usefulLifeColumn),
                ResidualValue = ParseDecimal(worksheet, row, residualValueColumn),
                SerialNumber = GetCellText(worksheet, row, serialNumberColumn),
                RawStatus = rawStatus,
                Status = parsedStatus
            };

            var rowErrors = ValidateRow(row, rowData, categories, existingCodes, importCodes);
            
            if (rowErrors.Any())
            {
                result.Errors.AddRange(rowErrors);
            }
            else
            {
                rowsToImport.Add((row, rowData, categories[rowData.CategoryCode]));
            }
        }

        UpdateErrorCount(result);
        result.SuccessCount = rowsToImport.Count;
        result.SuccessfulAssetCodes.AddRange(rowsToImport.Select(r => r.Data.AssetCode));

        if (dryRun || result.Errors.Any())
        {
            if (result.Errors.Any())
            {
                result.SuccessCount = dryRun ? rowsToImport.Count : 0;
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
                foreach (var (rowNumber, data, categoryId) in rowsToImport)
                {
                    var acquisitionCost = (data.PurchasePrice ?? 0) + (data.InstallationCost ?? 0) + (data.TaxAmount ?? 0);

                    var asset = new FixedAsset
                    {
                        TenantId = TenantId,
                        AssetCode = data.AssetCode,
                        Name = data.Name,
                        Description = data.Description,
                        Location = data.Location,
                        FixedAssetCategoryId = categoryId,
                        PurchaseDate = data.PurchaseDate!.Value,
                        PlacedInServiceDate = data.PlacedInServiceDate ?? data.PurchaseDate,
                        PurchasePrice = data.PurchasePrice ?? 0,
                        InstallationCost = data.InstallationCost ?? 0,
                        TaxAmount = data.TaxAmount ?? 0,
                        AcquisitionCost = acquisitionCost,
                        NetBookValue = acquisitionCost,
                        DepreciationMethod = ErpSystem.Core.Enums.DepreciationMethod.StraightLine,
                        DepreciationConvention = ErpSystem.Core.Enums.DepreciationConvention.FullMonth,
                        UsefulLifeMonths = data.UsefulLifeMonths ?? 36,
                        ResidualValue = data.ResidualValue ?? 0,
                        SerialNumber = data.SerialNumber,
                        Status = data.Status ?? ErpSystem.Core.Enums.FixedAssetStatus.Draft,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };

                    _context.FixedAssets.Add(asset);
                    result.SuccessfulAssetCodes.Add(data.AssetCode);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                result.SuccessCount = rowsToImport.Count;
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
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Assets");

        // Headers
        var headers = new[]
        {
            "Asset Code*", "Name*", "Description", "Location", "Category Code*",
            "Purchase Date*", "Placed In Service Date", "Purchase Price*", "Installation Cost",
            "Tax Amount", "Useful Life (Months)*", "Residual Value", "Serial Number", "Status"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
        }

        // Sample data row
        worksheet.Cells[2, 1].Value = "FA-2024-001";
        worksheet.Cells[2, 2].Value = "Dell Laptop";
        worksheet.Cells[2, 3].Value = "Core i7, 16GB RAM";
        worksheet.Cells[2, 4].Value = "Head Office - IT Room";
        worksheet.Cells[2, 5].Value = "COMP-HW";
        worksheet.Cells[2, 6].Value = new DateTime(2024, 1, 15);
        worksheet.Cells[2, 7].Value = new DateTime(2024, 1, 15);
        worksheet.Cells[2, 8].Value = 1500.00;
        worksheet.Cells[2, 9].Value = 50.00;
        worksheet.Cells[2, 10].Value = 195.00;
        worksheet.Cells[2, 11].Value = 36;
        worksheet.Cells[2, 12].Value = 100.00;
        worksheet.Cells[2, 13].Value = "SN123456";
        worksheet.Cells[2, 14].Value = "Draft";

        worksheet.Cells[2, 6, 2, 7].Style.Numberformat.Format = "yyyy-mm-dd";
        worksheet.Cells[2, 8, 2, 12].Style.Numberformat.Format = "#,##0.00";
        worksheet.View.FreezePanes(2, 1);

        var instructions = package.Workbook.Worksheets.Add("Instructions");
        instructions.Cells[1, 1].Value = "Fixed Asset Import Instructions";
        instructions.Cells[1, 1].Style.Font.Bold = true;
        instructions.Cells[3, 1].Value = "Required fields";
        instructions.Cells[3, 2].Value = "Asset Code, Name, Category Code, Purchase Date, Purchase Price, Useful Life (Months)";
        instructions.Cells[4, 1].Value = "Location";
        instructions.Cells[4, 2].Value = "Optional finance-owned asset location. This does not depend on the Maintenance module.";
        instructions.Cells[5, 1].Value = "Status";
        instructions.Cells[5, 2].Value = "Optional. Supported values include Draft, Active, Fully Depreciated, Disposed, Held for Sale, Written Off, Under Construction, On Hold.";
        instructions.Cells[6, 1].Value = "Dates";
        instructions.Cells[6, 2].Value = "Use yyyy-mm-dd, or a valid Excel date cell.";
        instructions.Cells[7, 1].Value = "Category Code";
        instructions.Cells[7, 2].Value = "Use an existing fixed asset category code from the Categories sheet.";
        instructions.Cells.AutoFitColumns();

        var categoriesSheet = package.Workbook.Worksheets.Add("Categories");
        categoriesSheet.Cells[1, 1].Value = "Category Code";
        categoriesSheet.Cells[1, 2].Value = "Category Name";
        categoriesSheet.Cells[1, 1, 1, 2].Style.Font.Bold = true;

        var categories = await _context.FixedAssetCategories
            .Where(c => c.TenantId == TenantId)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Code, c.Name })
            .ToListAsync();

        for (int i = 0; i < categories.Count; i++)
        {
            categoriesSheet.Cells[i + 2, 1].Value = categories[i].Code;
            categoriesSheet.Cells[i + 2, 2].Value = categories[i].Name;
        }

        categoriesSheet.Cells.AutoFitColumns();

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    private List<BulkImportErrorDto> ValidateRow(
        int rowNumber,
        BulkAssetImportRowDto data,
        Dictionary<string, Guid> categories,
        HashSet<string> existingCodes,
        HashSet<string> importCodes)
    {
        var errors = new List<BulkImportErrorDto>();

        if (string.IsNullOrWhiteSpace(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Required" });
        else if (data.AssetCode.Length > 50)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Cannot exceed 50 characters" });
        else if (existingCodes.Contains(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Duplicate asset code" });
        else if (!importCodes.Add(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Duplicate asset code in this file" });

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

        if (!string.IsNullOrWhiteSpace(data.SerialNumber) && data.SerialNumber.Length > 100)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Serial Number", Error = "Cannot exceed 100 characters" });

        if (!string.IsNullOrWhiteSpace(data.RawStatus) && !data.Status.HasValue)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Status", Error = "Invalid fixed asset status" });

        return errors;
    }

    private static DateTime? ParseDate(ExcelWorksheet worksheet, int row, int column)
    {
        if (column <= 0) return null;

        var cell = worksheet.Cells[row, column];
        if (cell.Value is DateTime dateValue)
            return dateValue.Date;

        if (TryConvertToDouble(cell.Value, out var serial))
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

    private static decimal? ParseDecimal(ExcelWorksheet worksheet, int row, int column)
    {
        if (column <= 0) return null;

        var cell = worksheet.Cells[row, column];
        if (cell.Value is decimal decimalValue) return decimalValue;
        if (cell.Value is double doubleValue) return Convert.ToDecimal(doubleValue);
        if (cell.Value is int intValue) return intValue;
        if (cell.Value is long longValue) return longValue;

        var value = GetCellText(worksheet, row, column);
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var number)
            || decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static int? ParseInt(ExcelWorksheet worksheet, int row, int column)
    {
        var decimalValue = ParseDecimal(worksheet, row, column);
        if (decimalValue.HasValue)
            return decimal.ToInt32(decimal.Truncate(decimalValue.Value));

        var value = GetCellText(worksheet, row, column);
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var number) ? number : null;
    }

    private static Dictionary<string, int> BuildHeaderMap(ExcelWorksheet worksheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var columnCount = worksheet.Dimension?.Columns ?? 0;

        for (var column = 1; column <= columnCount; column++)
        {
            var header = NormalizeHeader(worksheet.Cells[1, column].Text);
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

    private static string? GetCellText(ExcelWorksheet worksheet, int row, int column)
    {
        if (column <= 0) return null;

        var cell = worksheet.Cells[row, column];
        var text = cell.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) && cell.Value != null)
        {
            text = Convert.ToString(cell.Value, CultureInfo.InvariantCulture)?.Trim();
        }

        return NormalizeOptionalText(text);
    }

    private static bool IsRowEmpty(ExcelWorksheet worksheet, int row)
    {
        var columnCount = worksheet.Dimension?.Columns ?? 0;
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

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double doubleValue:
                result = doubleValue;
                return true;
            case decimal decimalValue:
                result = Convert.ToDouble(decimalValue);
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            default:
                result = 0;
                return false;
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

    // ========== Lifecycle Management ==========

    public async Task<FixedAssetDto> ActivateAsync(Guid id, DateTime? placedInServiceDate)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        if (asset.Status != FixedAssetStatus.Draft)
            throw new InvalidOperationException($"Cannot activate an asset in '{asset.Status}' status. Only Draft assets can be activated.");

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
        return MapToDto(asset);
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
