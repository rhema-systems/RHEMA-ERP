using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using OfficeOpenXml;
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
        asset.Status.Should().Be(FixedAssetStatus.Active);
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

    private static MemoryStream CreateWorkbook(params (string AssetCode, string Name, string Location, string CategoryCode)[] rows)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var stream = new MemoryStream();

        using (var package = new ExcelPackage(stream))
        {
            var worksheet = package.Workbook.Worksheets.Add("Assets");
            var headers = new[]
            {
                "Asset Code*", "Name*", "Location", "Category Code*", "Purchase Date*",
                "Purchase Price*", "Installation Cost", "Tax Amount", "Useful Life (Months)*",
                "Residual Value", "Serial Number", "Status"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cells[1, column + 1].Value = headers[column];
            }

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var rowNumber = index + 2;
                worksheet.Cells[rowNumber, 1].Value = row.AssetCode;
                worksheet.Cells[rowNumber, 2].Value = row.Name;
                worksheet.Cells[rowNumber, 3].Value = row.Location;
                worksheet.Cells[rowNumber, 4].Value = row.CategoryCode;
                worksheet.Cells[rowNumber, 5].Value = new DateTime(2024, 1, 15);
                worksheet.Cells[rowNumber, 6].Value = 1500m;
                worksheet.Cells[rowNumber, 7].Value = 50m;
                worksheet.Cells[rowNumber, 8].Value = 195m;
                worksheet.Cells[rowNumber, 9].Value = 36;
                worksheet.Cells[rowNumber, 10].Value = 100m;
                worksheet.Cells[rowNumber, 11].Value = $"SN-{row.AssetCode}";
                worksheet.Cells[rowNumber, 12].Value = "Active";
            }

            package.Save();
        }

        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
