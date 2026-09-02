using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ClosedXML.Excel;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetServiceImportTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly FixedAssetService _sut;

    public FixedAssetServiceImportTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-import-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new ApplicationDbContext(options, _tenantId);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        currentUser.SetupGet(x => x.UserName).Returns("test_user");

        _sut = new FixedAssetService(_dbContext, currentUser.Object);
    }

    [Fact]
    public async Task ImportAssetsFromExcelAsync_DryRun_ValidatesLocationWithoutSavingAsset()
    {
        SeedCategory("COMP-HW");
        await using var stream = CreateWorkbook(
            ("FA-TEST-001", "Dell Laptop", "Head Office - IT Room", "COMP-HW"));

        var result = await _sut.ImportAssetsFromExcelAsync(stream, "assets.xlsx", dryRun: true);

        result.IsDryRun.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
        result.SuccessCount.Should().Be(1);
        result.SuccessfulAssetCodes.Should().ContainSingle("FA-TEST-001");
        (await _dbContext.FixedAssets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAssetsFromExcelAsync_ValidFile_SavesLocationOnFinanceAsset()
    {
        SeedCategory("COMP-HW");
        await using var stream = CreateWorkbook(
            ("FA-TEST-001", "Dell Laptop", "Head Office - IT Room", "COMP-HW"));

        var result = await _sut.ImportAssetsFromExcelAsync(stream, "assets.xlsx");

        result.IsDryRun.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.ErrorCount.Should().Be(0);
        result.SuccessCount.Should().Be(1);

        var asset = await _dbContext.FixedAssets.SingleAsync();
        asset.AssetCode.Should().Be("FA-TEST-001");
        asset.Location.Should().Be("Head Office - IT Room");
        asset.Status.Should().Be(FixedAssetStatus.Draft);
        asset.AcquisitionCost.Should().Be(1745m);
        asset.NetBookValue.Should().Be(1745m);
    }

    [Fact]
    public async Task ImportAssetsFromExcelAsync_WithDuplicateCode_DoesNotPartiallyImportValidRows()
    {
        var category = SeedCategory("COMP-HW");
        _dbContext.FixedAssets.Add(new FixedAsset
        {
            TenantId = _tenantId,
            AssetCode = "FA-EXISTING",
            Name = "Existing Asset",
            FixedAssetCategoryId = category.Id,
            PurchaseDate = new DateTime(2024, 1, 1),
            PurchasePrice = 1000m,
            AcquisitionCost = 1000m,
            NetBookValue = 1000m,
            UsefulLifeMonths = 36,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth
        });
        await _dbContext.SaveChangesAsync();

        await using var stream = CreateWorkbook(
            ("FA-EXISTING", "Duplicate Asset", "Warehouse", "COMP-HW"),
            ("FA-NEW-001", "Valid Asset", "Branch Office", "COMP-HW"));

        var result = await _sut.ImportAssetsFromExcelAsync(stream, "assets.xlsx");

        result.ErrorCount.Should().Be(1);
        result.SuccessCount.Should().Be(0);
        result.Errors.Should().Contain(e => e.Field == "Asset Code" && e.Error == "Duplicate asset code");
        (await _dbContext.FixedAssets.CountAsync()).Should().Be(1);
        (await _dbContext.FixedAssets.AnyAsync(a => a.AssetCode == "FA-NEW-001")).Should().BeFalse();
    }

    [Fact]
    public async Task ImportAssetsFromExcelAsync_WithNonDraftStatus_RejectsLifecycleBypass()
    {
        SeedCategory("COMP-HW");
        await using var stream = CreateOpeningWorkbook(new OpeningAssetRow
        {
            AssetCode = "FA-ACTIVE-001",
            Name = "Lifecycle Bypass",
            Location = "Head Office",
            CategoryCode = "COMP-HW",
            PurchasePrice = 1500m,
            Status = "Active"
        });

        var result = await _sut.ImportAssetsFromExcelAsync(stream, "assets.xlsx");

        result.SuccessCount.Should().Be(0);
        result.Errors.Should().ContainSingle(error =>
            error.Field == "Status"
            && error.Error.Contains("controlled Draft records"));
        (await _dbContext.FixedAssets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAssetsFromExcelAsync_WithOpeningAccumulatedDepreciation_CreatesBookValueAndAuditTransactions()
    {
        SeedCategory("COMP-HW");
        SeedBook("IFRS", isDefault: true, sortOrder: 10);

        await using var stream = CreateOpeningWorkbook(new OpeningAssetRow
        {
            AssetCode = "FA-OPEN-001",
            Name = "Server Rack",
            Location = "Data Center",
            CategoryCode = "COMP-HW",
            BookCode = "IFRS",
            PurchasePrice = 10000m,
            AccumulatedDepreciation = 4000m,
            NetBookValue = 6000m,
            OpeningAsOfDate = new DateTime(2026, 6, 30),
            OpeningYtdDepreciation = 500m,
            RemainingUsefulLifeMonths = 24,
            UsefulLifeMonths = 60,
            ResidualValue = 1000m
        });

        var result = await _sut.ImportAssetsFromExcelAsync(stream, "assets.xlsx");

        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);

        var asset = await _dbContext.FixedAssets
            .Include(a => a.BookValues)
            .SingleAsync(a => a.AssetCode == "FA-OPEN-001");

        asset.AcquisitionCost.Should().Be(10000m);
        asset.NetBookValue.Should().Be(6000m);

        var bookValue = asset.BookValues.Should().ContainSingle().Subject;
        bookValue.BookClassification.Should().Be("IFRS");
        bookValue.AcquisitionCost.Should().Be(10000m);
        bookValue.AccumulatedDepreciation.Should().Be(4000m);
        bookValue.NetBookValue.Should().Be(6000m);
        bookValue.OpeningAsOfDate.Should().Be(new DateTime(2026, 6, 30));
        bookValue.OpeningYtdDepreciation.Should().Be(500m);
        bookValue.RemainingUsefulLifeMonths.Should().Be(24);

        var transactions = await _dbContext.AssetTransactions
            .Where(t => t.FixedAssetId == asset.Id)
            .OrderBy(t => t.TransactionType)
            .ToListAsync();

        transactions.Should().HaveCount(2);
        transactions.Should().Contain(t =>
            t.TransactionType == "Opening Acquisition"
            && t.BookClassification == "IFRS"
            && t.Amount == 10000m
            && t.ResultingBookValue == 10000m);
        transactions.Should().Contain(t =>
            t.TransactionType == "Opening Accumulated Depreciation"
            && t.BookClassification == "IFRS"
            && t.Amount == 4000m
            && t.ResultingBookValue == 6000m);
    }

    [Fact]
    public async Task ImportAssetsFromExcelAsync_WithRepeatedRowsPerBook_CreatesSeparateBookValuesForOneAsset()
    {
        SeedCategory("COMP-HW");
        SeedBook("IFRS", isDefault: true, sortOrder: 10);
        SeedBook("LOCAL_STATUTORY", isDefault: false, sortOrder: 20);

        await using var stream = CreateOpeningWorkbook(
            new OpeningAssetRow
            {
                AssetCode = "FA-MULTI-001",
                Name = "Production Machine",
                Location = "Factory Floor",
                CategoryCode = "COMP-HW",
                BookCode = "IFRS",
                PurchasePrice = 10000m,
                AccumulatedDepreciation = 4000m,
                NetBookValue = 6000m,
                OpeningAsOfDate = new DateTime(2026, 6, 30),
                RemainingUsefulLifeMonths = 24,
                UsefulLifeMonths = 60,
                ResidualValue = 1000m
            },
            new OpeningAssetRow
            {
                AssetCode = "FA-MULTI-001",
                Name = "Production Machine",
                Location = "Factory Floor",
                CategoryCode = "COMP-HW",
                BookCode = "LOCAL_STATUTORY",
                PurchasePrice = 9500m,
                AccumulatedDepreciation = 2000m,
                NetBookValue = 7500m,
                OpeningAsOfDate = new DateTime(2026, 6, 30),
                RemainingUsefulLifeMonths = 30,
                UsefulLifeMonths = 48,
                ResidualValue = 500m
            });

        var result = await _sut.ImportAssetsFromExcelAsync(stream, "assets.xlsx");

        result.Errors.Should().BeEmpty();
        result.SuccessCount.Should().Be(1);
        (await _dbContext.FixedAssets.CountAsync()).Should().Be(1);

        var asset = await _dbContext.FixedAssets
            .Include(a => a.BookValues)
            .SingleAsync(a => a.AssetCode == "FA-MULTI-001");

        asset.AcquisitionCost.Should().Be(10000m);
        asset.NetBookValue.Should().Be(6000m);
        asset.UsefulLifeMonths.Should().Be(60);
        asset.ResidualValue.Should().Be(1000m);

        asset.BookValues.Should().HaveCount(2);
        var ifrs = asset.BookValues.Single(v => v.BookClassification == "IFRS");
        ifrs.AcquisitionCost.Should().Be(10000m);
        ifrs.AccumulatedDepreciation.Should().Be(4000m);
        ifrs.NetBookValue.Should().Be(6000m);
        ifrs.UsefulLifeMonths.Should().Be(60);
        ifrs.RemainingUsefulLifeMonths.Should().Be(24);

        var local = asset.BookValues.Single(v => v.BookClassification == "LOCAL_STATUTORY");
        local.AcquisitionCost.Should().Be(9500m);
        local.AccumulatedDepreciation.Should().Be(2000m);
        local.NetBookValue.Should().Be(7500m);
        local.UsefulLifeMonths.Should().Be(48);
        local.RemainingUsefulLifeMonths.Should().Be(30);
    }

    private FixedAssetCategory SeedCategory(string code)
    {
        var category = new FixedAssetCategory
        {
            TenantId = _tenantId,
            Code = code,
            Name = "Computer Hardware",
            AssetAccountId = Guid.NewGuid(),
            AccumulatedDepreciationAccountId = Guid.NewGuid(),
            DepreciationExpenseAccountId = Guid.NewGuid()
        };

        _dbContext.FixedAssetCategories.Add(category);
        _dbContext.SaveChanges();
        return category;
    }

    private AccountingBook SeedBook(string code, bool isDefault, int sortOrder)
    {
        var book = new AccountingBook
        {
            TenantId = _tenantId,
            Code = code,
            Name = code,
            Purpose = isDefault ? "Primary" : "Reporting",
            IsActive = true,
            IsDefault = isDefault,
            AllowsPosting = true,
            IsSystemDefined = true,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test_user"
        };

        _dbContext.AccountingBooks.Add(book);
        _dbContext.SaveChanges();
        return book;
    }

    private static MemoryStream CreateWorkbook(params (string AssetCode, string Name, string Location, string CategoryCode)[] rows)
    {
        var stream = new MemoryStream();

        using (var package = new XLWorkbook())
        {
            var worksheet = package.Worksheets.Add("Assets");
            var headers = new[]
            {
                "Asset Code*", "Name*", "Location", "Category Code*", "Purchase Date*",
                "Purchase Price*", "Installation Cost", "Tax Amount", "Useful Life (Months)*",
                "Residual Value", "Serial Number", "Status"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
            }

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var rowNumber = index + 2;
                worksheet.Cell(rowNumber, 1).Value = row.AssetCode;
                worksheet.Cell(rowNumber, 2).Value = row.Name;
                worksheet.Cell(rowNumber, 3).Value = row.Location;
                worksheet.Cell(rowNumber, 4).Value = row.CategoryCode;
                worksheet.Cell(rowNumber, 5).Value = new DateTime(2024, 1, 15);
                worksheet.Cell(rowNumber, 6).Value = 1500m;
                worksheet.Cell(rowNumber, 7).Value = 50m;
                worksheet.Cell(rowNumber, 8).Value = 195m;
                worksheet.Cell(rowNumber, 9).Value = 36;
                worksheet.Cell(rowNumber, 10).Value = 100m;
                worksheet.Cell(rowNumber, 11).Value = $"SN-{row.AssetCode}";
                worksheet.Cell(rowNumber, 12).Value = "Draft";
            }

            package.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateOpeningWorkbook(params OpeningAssetRow[] rows)
    {
        var stream = new MemoryStream();

        using (var package = new XLWorkbook())
        {
            var worksheet = package.Worksheets.Add("Assets");
            var headers = new[]
            {
                "Asset Code*", "Name*", "Location", "Category Code*", "Purchase Date*",
                "Placed In Service Date", "Book Code", "Purchase Price*", "Installation Cost",
                "Tax Amount", "Accumulated Depreciation", "Net Book Value", "Opening As Of Date",
                "YTD Depreciation", "Remaining Useful Life (Months)", "Useful Life (Months)*",
                "Residual Value", "Serial Number", "Status"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
            }

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var rowNumber = index + 2;
                worksheet.Cell(rowNumber, 1).Value = row.AssetCode;
                worksheet.Cell(rowNumber, 2).Value = row.Name;
                worksheet.Cell(rowNumber, 3).Value = row.Location;
                worksheet.Cell(rowNumber, 4).Value = row.CategoryCode;
                worksheet.Cell(rowNumber, 5).Value = row.PurchaseDate;
                worksheet.Cell(rowNumber, 6).Value = row.PlacedInServiceDate;
                SetCellValue(worksheet.Cell(rowNumber, 7), row.BookCode);
                worksheet.Cell(rowNumber, 8).Value = row.PurchasePrice;
                worksheet.Cell(rowNumber, 9).Value = row.InstallationCost;
                worksheet.Cell(rowNumber, 10).Value = row.TaxAmount;
                SetCellValue(worksheet.Cell(rowNumber, 11), row.AccumulatedDepreciation);
                SetCellValue(worksheet.Cell(rowNumber, 12), row.NetBookValue);
                SetCellValue(worksheet.Cell(rowNumber, 13), row.OpeningAsOfDate);
                SetCellValue(worksheet.Cell(rowNumber, 14), row.OpeningYtdDepreciation);
                SetCellValue(worksheet.Cell(rowNumber, 15), row.RemainingUsefulLifeMonths);
                worksheet.Cell(rowNumber, 16).Value = row.UsefulLifeMonths;
                worksheet.Cell(rowNumber, 17).Value = row.ResidualValue;
                worksheet.Cell(rowNumber, 18).Value = row.SerialNumber ?? $"SN-{row.AssetCode}";
                worksheet.Cell(rowNumber, 19).Value = row.Status;
            }

            package.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string text:
                cell.Value = text;
                return;
            case DateTime date:
                cell.Value = date;
                return;
            case decimal number:
                cell.Value = number;
                return;
            case int number:
                cell.Value = number;
                return;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                return;
        }
    }

    private sealed class OpeningAssetRow
    {
        public string AssetCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string CategoryCode { get; init; } = string.Empty;
        public DateTime PurchaseDate { get; init; } = new(2024, 1, 15);
        public DateTime PlacedInServiceDate { get; init; } = new(2024, 1, 15);
        public string? BookCode { get; init; }
        public decimal PurchasePrice { get; init; }
        public decimal InstallationCost { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal? AccumulatedDepreciation { get; init; }
        public decimal? NetBookValue { get; init; }
        public DateTime? OpeningAsOfDate { get; init; }
        public decimal? OpeningYtdDepreciation { get; init; }
        public int? RemainingUsefulLifeMonths { get; init; }
        public int UsefulLifeMonths { get; init; } = 36;
        public decimal ResidualValue { get; init; }
        public string? SerialNumber { get; init; }
        public string Status { get; init; } = "Draft";
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
