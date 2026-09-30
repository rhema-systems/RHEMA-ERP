using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetCategoryRoleFamilyTests
{
    [Fact]
    public async Task MaintenanceFeed_ReturnsOnlyTenantAssetsFromOptedInCategories()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"fixed-asset-maintenance-feed-{Guid.NewGuid():N}")
                .Options);

        var eligibleCategory = CategoryEntity(tenantId, "VEH", requiresMaintenance: true);
        var excludedCategory = CategoryEntity(tenantId, "LAND", requiresMaintenance: false);
        var otherTenantCategory = CategoryEntity(otherTenantId, "OTHER", requiresMaintenance: true);
        db.FixedAssetCategories.AddRange(eligibleCategory, excludedCategory, otherTenantCategory);
        db.FixedAssets.AddRange(
            Asset(tenantId, eligibleCategory, "FA-001"),
            Asset(tenantId, excludedCategory, "FA-002"),
            Asset(otherTenantId, otherTenantCategory, "FA-003"));
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("maintenance.reader");
        var service = new FixedAssetCategoryService(db, currentUser.Object);

        var result = await service.GetMaintenanceEligibleAssetsAsync();

        result.Should().ContainSingle(item => item.AssetCode == "FA-001");
        result.Should().OnlyContain(item => item.FixedAssetCategoryCode == "VEH");
        new FixedAssetCategory().RequiresMaintenance.Should().BeFalse();
    }

    [Fact]
    public async Task CategoryUpdate_PersistsAndReturnsMaintenanceEligibility()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"fixed-asset-maintenance-update-{Guid.NewGuid():N}")
                .Options);
        var cost = AddAccount(db, tenantId, "1520", "Vehicle cost", AccountType.Asset);
        var accumulated = AddAccount(db, tenantId, "1592", "Vehicle accumulated depreciation", AccountType.Asset);
        var expense = AddAccount(db, tenantId, "7310", "Vehicle depreciation expense", AccountType.Expense);
        var category = CategoryEntity(tenantId, "VEH", requiresMaintenance: false);
        category.AssetAccountId = cost.Id;
        category.AccumulatedDepreciationAccountId = accumulated.Id;
        category.DepreciationExpenseAccountId = expense.Id;
        category.DefaultMethod = DepreciationMethod.StraightLine;
        category.DefaultUsefulLifeMonths = 60;
        db.FixedAssetCategories.Add(category);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("fixed.asset.accountant");
        var service = new FixedAssetCategoryService(db, currentUser.Object);

        var response = await service.UpdateAsync(category.Id, new UpdateFixedAssetCategoryDto
        {
            Code = category.Code,
            Name = category.Name,
            Description = "Maintenance-managed vehicle category",
            RequiresMaintenance = true,
            DefaultMethod = DepreciationMethod.StraightLine,
            DefaultUsefulLifeMonths = 60,
            DefaultResidualValuePercent = 10m,
            AssetAccountId = cost.Id,
            AccumulatedDepreciationAccountId = accumulated.Id,
            DepreciationExpenseAccountId = expense.Id
        });

        response.RequiresMaintenance.Should().BeTrue();
        response.DefaultResidualValuePercent.Should().Be(10m);
        db.ChangeTracker.Clear();
        var stored = await db.FixedAssetCategories.AsNoTracking()
            .SingleAsync(item => item.Id == category.Id);
        stored.RequiresMaintenance.Should().BeTrue();
        stored.DefaultResidualValuePercent.Should().Be(10m);
        var reloaded = await service.GetByIdAsync(category.Id);
        reloaded.Should().NotBeNull();
        reloaded!.RequiresMaintenance.Should().BeTrue();
        reloaded.DefaultResidualValuePercent.Should().Be(10m);
    }

    [Fact]
    public async Task Categories_SelectDistinctAccountsFromRepeatableFixedAssetRoleFamilies()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"fixed-asset-role-family-{Guid.NewGuid():N}")
                .Options);
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS",
            Purpose = "Primary", IsActive = true, IsDefault = true, AllowsPosting = true
        };
        db.AccountingBooks.Add(book);

        var buildingsCost = AddAccount(db, tenantId, "1510", "Buildings cost", AccountType.Asset);
        var vehiclesCost = AddAccount(db, tenantId, "1520", "Vehicles cost", AccountType.Asset);
        var buildingsAccumulated = AddAccount(db, tenantId, "1591", "Buildings accumulated depreciation", AccountType.Asset);
        var vehiclesAccumulated = AddAccount(db, tenantId, "1592", "Vehicles accumulated depreciation", AccountType.Asset);
        var depreciationExpense = AddAccount(db, tenantId, "7310", "Depreciation expense", AccountType.Expense);

        var buildingCostClass = AddClassification(db, tenantId, book.Id, "BUILDINGS_COST",
            AccountClassificationSystemRole.FixedAssetCost);
        var vehicleCostClass = AddClassification(db, tenantId, book.Id, "VEHICLES_COST",
            AccountClassificationSystemRole.FixedAssetCost);
        var buildingAccumulatedClass = AddClassification(db, tenantId, book.Id, "BUILDINGS_ACCUM_DEP",
            AccountClassificationSystemRole.AccumulatedDepreciation);
        var vehicleAccumulatedClass = AddClassification(db, tenantId, book.Id, "VEHICLES_ACCUM_DEP",
            AccountClassificationSystemRole.AccumulatedDepreciation);
        Map(db, tenantId, book.Id, buildingsCost.Id, buildingCostClass.Id);
        Map(db, tenantId, book.Id, vehiclesCost.Id, vehicleCostClass.Id);
        Map(db, tenantId, book.Id, buildingsAccumulated.Id, buildingAccumulatedClass.Id);
        Map(db, tenantId, book.Id, vehiclesAccumulated.Id, vehicleAccumulatedClass.Id);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("fixed.asset.accountant");
        var service = new FixedAssetCategoryService(db, currentUser.Object);

        var buildings = await service.CreateAsync(Category(
            "BUILDINGS", buildingsCost.Id, buildingsAccumulated.Id, depreciationExpense.Id));
        var vehicles = await service.CreateAsync(Category(
            "VEHICLES", vehiclesCost.Id, vehiclesAccumulated.Id, depreciationExpense.Id));

        buildings.AssetAccountId.Should().Be(buildingsCost.Id);
        buildings.AccumulatedDepreciationAccountId.Should().Be(buildingsAccumulated.Id);
        vehicles.AssetAccountId.Should().Be(vehiclesCost.Id);
        vehicles.AccumulatedDepreciationAccountId.Should().Be(vehiclesAccumulated.Id);
        (await db.AccountClassifications.CountAsync(item =>
            item.SystemRole == AccountClassificationSystemRole.FixedAssetCost)).Should().Be(2);
        (await db.AccountClassifications.CountAsync(item =>
            item.SystemRole == AccountClassificationSystemRole.AccumulatedDepreciation)).Should().Be(2);
    }

    private static CreateFixedAssetCategoryDto Category(
        string code,
        Guid costAccountId,
        Guid accumulatedDepreciationAccountId,
        Guid depreciationExpenseAccountId) => new()
    {
        Code = code,
        Name = code,
        DefaultMethod = DepreciationMethod.StraightLine,
        DefaultUsefulLifeMonths = 60,
        AssetAccountId = costAccountId,
        AccumulatedDepreciationAccountId = accumulatedDepreciationAccountId,
        DepreciationExpenseAccountId = depreciationExpenseAccountId
    };

    private static FixedAssetCategory CategoryEntity(Guid tenantId, string code, bool requiresMaintenance)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            RequiresMaintenance = requiresMaintenance,
            AssetAccountId = Guid.NewGuid(),
            AccumulatedDepreciationAccountId = Guid.NewGuid(),
            DepreciationExpenseAccountId = Guid.NewGuid()
        };

    private static FixedAsset Asset(Guid tenantId, FixedAssetCategory category, string assetCode)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetCode = assetCode,
            Name = assetCode,
            FixedAssetCategoryId = category.Id,
            Category = category,
            PurchaseDate = new DateTime(2026, 9, 1),
            PurchasePrice = 1000m,
            AcquisitionCost = 1000m,
            NetBookValue = 1000m,
            UsefulLifeMonths = 60,
            Status = FixedAssetStatus.Active
        };

    private static Account AddAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string number,
        string name,
        AccountType type)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = number,
            AccountNumber = number, AccountName = name, AccountType = type,
            CurrencyCode = "GHS", Status = AccountStatus.Active, AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private static AccountClassification AddClassification(
        ApplicationDbContext db,
        Guid tenantId,
        Guid bookId,
        string code,
        AccountClassificationSystemRole role)
    {
        var classification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = bookId,
            Code = code, Name = code, CoreAccountType = AccountType.Asset,
            SystemRole = role, Status = AccountClassificationStatus.Active,
            IsPostingClassification = true
        };
        db.AccountClassifications.Add(classification);
        return classification;
    }

    private static void Map(
        ApplicationDbContext db,
        Guid tenantId,
        Guid bookId,
        Guid accountId,
        Guid classificationId) => db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = bookId,
            AccountId = accountId, AccountClassificationId = classificationId,
            IsEnabled = true
        });
}
