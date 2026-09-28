using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
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
