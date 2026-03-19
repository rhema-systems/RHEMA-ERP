using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

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
            AssetCode = dto.AssetCode,
            Name = dto.Name,
            Description = dto.Description,
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

        var categoryExists = await _context.FixedAssetCategories
            .AnyAsync(c => c.TenantId == TenantId && c.Id == dto.FixedAssetCategoryId);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Fixed asset category not found.");
        }

        var acquisitionCost = dto.AcquisitionCost
            ?? (dto.PurchasePrice + dto.InstallationCost + dto.TaxAmount);

        asset.AssetCode = dto.AssetCode;
        asset.Name = dto.Name;
        asset.Description = dto.Description;
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

    public async Task<BulkImportResultDto> ImportAssetsFromExcelAsync(Stream fileStream, string fileName)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        var result = new BulkImportResultDto();
        var rowsToImport = new List<(int RowNumber, BulkAssetImportRowDto Data, Guid CategoryId)>();

        using var package = new ExcelPackage(fileStream);
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension?.Rows ?? 0;

        if (rowCount < 2)
        {
            result.Errors.Add(new BulkImportErrorDto
            {
                RowNumber = 0,
                Field = "File",
                Error = "Excel file is empty or has no data rows."
            });
            return result;
        }

        // Load all categories once for validation
        var categories = await _context.FixedAssetCategories
            .Where(c => c.TenantId == TenantId)
            .ToDictionaryAsync(c => c.Code, c => c.Id);

        // Load existing asset codes for duplicate check
        var existingCodesList = await _context.FixedAssets
            .Where(a => a.TenantId == TenantId)
            .Select(a => a.AssetCode)
            .ToListAsync();
        var existingCodes = new HashSet<string>(existingCodesList);

        result.TotalRows = rowCount - 1; // Exclude header

        // Parse and validate each row
        for (int row = 2; row <= rowCount; row++)
        {
            var rowData = new BulkAssetImportRowDto
            {
                AssetCode = worksheet.Cells[row, 1].Text?.Trim() ?? string.Empty,
                Name = worksheet.Cells[row, 2].Text?.Trim() ?? string.Empty,
                Description = worksheet.Cells[row, 3].Text?.Trim(),
                CategoryCode = worksheet.Cells[row, 4].Text?.Trim() ?? string.Empty,
                PurchaseDate = ParseDate(worksheet.Cells[row, 5].Text),
                PurchasePrice = ParseDecimal(worksheet.Cells[row, 6].Text),
                InstallationCost = ParseDecimal(worksheet.Cells[row, 7].Text),
                TaxAmount = ParseDecimal(worksheet.Cells[row, 8].Text),
                UsefulLifeMonths = ParseInt(worksheet.Cells[row, 9].Text),
                ResidualValue = ParseDecimal(worksheet.Cells[row, 10].Text),
                SerialNumber = worksheet.Cells[row, 11].Text?.Trim()
            };

            var rowErrors = ValidateRow(row, rowData, categories, existingCodes);
            
            if (rowErrors.Any())
            {
                result.Errors.AddRange(rowErrors);
                result.ErrorCount++;
            }
            else
            {
                rowsToImport.Add((row, rowData, categories[rowData.CategoryCode]));
                existingCodes.Add(rowData.AssetCode); // Prevent duplicates within the same file
            }
        }

        // Import valid rows
        if (rowsToImport.Any())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var (rowNumber, data, categoryId) in rowsToImport)
                {
                    var acquisitionCost = (data.PurchasePrice ?? 0) + (data.InstallationCost ?? 0) + (data.TaxAmount ?? 0);

                    var asset = new FixedAsset
                    {
                        TenantId = TenantId,
                        AssetCode = data.AssetCode,
                        Name = data.Name,
                        Description = data.Description,
                        FixedAssetCategoryId = categoryId,
                        PurchaseDate = data.PurchaseDate!.Value,
                        PlacedInServiceDate = data.PurchaseDate,
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
                        Status = ErpSystem.Core.Enums.FixedAssetStatus.Draft,
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
                result.ErrorCount = result.TotalRows;
                result.SuccessCount = 0;
                result.SuccessfulAssetCodes.Clear();
            }
        }

        return result;
    }

    public async Task<byte[]> GenerateImportTemplateAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Asset Import Template");

        // Headers
        var headers = new[]
        {
            "Asset Code*", "Name*", "Description", "Category Code*",
            "Purchase Date*", "Purchase Price*", "Installation Cost",
            "Tax Amount", "Useful Life (Months)*", "Residual Value", "Serial Number"
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
        worksheet.Cells[2, 4].Value = "COMP-HW";
        worksheet.Cells[2, 5].Value = "2024-01-15";
        worksheet.Cells[2, 6].Value = 1500.00;
        worksheet.Cells[2, 7].Value = 50.00;
        worksheet.Cells[2, 8].Value = 195.00;
        worksheet.Cells[2, 9].Value = 36;
        worksheet.Cells[2, 10].Value = 100.00;
        worksheet.Cells[2, 11].Value = "SN123456";

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();

        return await Task.FromResult(package.GetAsByteArray());
    }

    private List<BulkImportErrorDto> ValidateRow(
        int rowNumber,
        BulkAssetImportRowDto data,
        Dictionary<string, Guid> categories,
        HashSet<string> existingCodes)
    {
        var errors = new List<BulkImportErrorDto>();

        if (string.IsNullOrWhiteSpace(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Required" });
        else if (existingCodes.Contains(data.AssetCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Asset Code", Error = "Duplicate asset code" });

        if (string.IsNullOrWhiteSpace(data.Name))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Name", Error = "Required" });

        if (string.IsNullOrWhiteSpace(data.CategoryCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Category Code", Error = "Required" });
        else if (!categories.ContainsKey(data.CategoryCode))
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Category Code", Error = "Category not found" });

        if (!data.PurchaseDate.HasValue)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Purchase Date", Error = "Required or invalid date format" });
        else if (data.PurchaseDate.Value > DateTime.UtcNow)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Purchase Date", Error = "Cannot be in the future" });

        if (!data.PurchasePrice.HasValue || data.PurchasePrice.Value <= 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Purchase Price", Error = "Required and must be greater than 0" });

        if (!data.UsefulLifeMonths.HasValue || data.UsefulLifeMonths.Value <= 0)
            errors.Add(new BulkImportErrorDto { RowNumber = rowNumber, AssetCode = data.AssetCode, Field = "Useful Life", Error = "Required and must be greater than 0" });

        return errors;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var date) ? date : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, out var number) ? number : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var number) ? number : null;
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
