using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FixedAssetDepreciationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly FixedAssetDepreciationService _sut;

    public FixedAssetDepreciationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fixed-asset-depreciation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new ApplicationDbContext(options, _tenantId);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        currentUser.SetupGet(x => x.UserId).Returns("22222222-2222-2222-2222-222222222222");
        currentUser.SetupGet(x => x.UserName).Returns("test_user");

        var journalEntryService = new Mock<IJournalEntryService>();
        _sut = new FixedAssetDepreciationService(_dbContext, currentUser.Object, journalEntryService.Object);
    }

    [Fact]
    public async Task RunDepreciationAsync_WhenRequestedBookIsFullyDepreciatedButAnotherBookIsNot_KeepsAssetActive()
    {
        var ifrsBook = SeedBook("IFRS", isDefault: true, sortOrder: 10);
        var localBook = SeedBook("LOCAL_STATUTORY", isDefault: false, sortOrder: 20);
        var category = SeedCategory();
        var period = SeedOpenFiscalPeriod();
        var capitalizationPostingEventId = Guid.NewGuid();
        var capitalizationJournalEntryId = Guid.NewGuid();

        var asset = new FixedAsset
        {
            TenantId = _tenantId,
            AssetCode = "FA-DEP-001",
            Name = "Generator",
            FixedAssetCategoryId = category.Id,
            PurchaseDate = new DateTime(2024, 1, 1),
            PlacedInServiceDate = new DateTime(2024, 1, 1),
            PurchasePrice = 1000m,
            AcquisitionCost = 1000m,
            NetBookValue = 100m,
            UsefulLifeMonths = 12,
            ResidualValue = 100m,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            Status = FixedAssetStatus.Active,
            CapitalizationDate = new DateTime(2024, 1, 1),
            CapitalizedAt = new DateTime(2024, 1, 1),
            PostingEventId = capitalizationPostingEventId,
            JournalEntryId = capitalizationJournalEntryId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test_user"
        };

        asset.BookValues.Add(new FixedAssetBookValue
        {
            TenantId = _tenantId,
            FixedAsset = asset,
            AccountingBookId = ifrsBook.Id,
            AccountingBook = ifrsBook,
            BookClassification = ifrsBook.Code,
            AcquisitionCost = 1000m,
            AccumulatedDepreciation = 900m,
            NetBookValue = 100m,
            ResidualValue = 100m,
            UsefulLifeMonths = 12,
            RemainingUsefulLifeMonths = 1,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            PlacedInServiceDate = new DateTime(2024, 1, 1),
            CapitalizationDate = new DateTime(2024, 1, 1),
            CapitalizationPostingEventId = capitalizationPostingEventId,
            CapitalizationJournalEntryId = capitalizationJournalEntryId,
            OpeningSource = "Test"
        });

        asset.BookValues.Add(new FixedAssetBookValue
        {
            TenantId = _tenantId,
            FixedAsset = asset,
            AccountingBookId = localBook.Id,
            AccountingBook = localBook,
            BookClassification = localBook.Code,
            AcquisitionCost = 1000m,
            AccumulatedDepreciation = 0m,
            NetBookValue = 1000m,
            ResidualValue = 100m,
            UsefulLifeMonths = 12,
            RemainingUsefulLifeMonths = 12,
            DepreciationMethod = DepreciationMethod.StraightLine,
            DepreciationConvention = DepreciationConvention.FullMonth,
            PlacedInServiceDate = new DateTime(2024, 1, 1),
            CapitalizationDate = new DateTime(2024, 1, 1),
            CapitalizationPostingEventId = capitalizationPostingEventId,
            CapitalizationJournalEntryId = capitalizationJournalEntryId,
            OpeningSource = "Test"
        });

        _dbContext.FixedAssets.Add(asset);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.RunDepreciationAsync(new RunDepreciationDto
        {
            FiscalPeriodId = period.Id,
            FixedAssetId = asset.Id,
            BookClassification = "IFRS",
            PostToGl = false
        });

        result.Should().BeEmpty();

        var savedAsset = await _dbContext.FixedAssets
            .Include(a => a.BookValues)
            .SingleAsync(a => a.Id == asset.Id);

        savedAsset.Status.Should().Be(FixedAssetStatus.Active);
        savedAsset.BookValues.Single(v => v.BookClassification == "IFRS").NetBookValue.Should().Be(100m);
        savedAsset.BookValues.Single(v => v.BookClassification == "LOCAL_STATUTORY").NetBookValue.Should().Be(1000m);
    }

    private FixedAssetCategory SeedCategory()
    {
        var assetAccount = SeedAccount("1600", AccountType.Asset);
        var accumulatedDepreciationAccount = SeedAccount("1699", AccountType.Asset);
        var depreciationExpenseAccount = SeedAccount("6700", AccountType.Expense);
        var category = new FixedAssetCategory
        {
            TenantId = _tenantId,
            Code = "PLANT",
            Name = "Plant",
            AssetAccountId = assetAccount.Id,
            AccumulatedDepreciationAccountId = accumulatedDepreciationAccount.Id,
            DepreciationExpenseAccountId = depreciationExpenseAccount.Id
        };

        _dbContext.FixedAssetCategories.Add(category);
        _dbContext.SaveChanges();
        return category;
    }

    private Account SeedAccount(string accountNumber, AccountType accountType)
    {
        var account = new Account
        {
            TenantId = _tenantId,
            AccountCode = $"{accountNumber}-LEG",
            AccountNumber = $"{accountNumber}-LEG",
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = true
        };

        _dbContext.Accounts.Add(account);
        return account;
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

    private FiscalPeriod SeedOpenFiscalPeriod()
    {
        var period = new FiscalPeriod
        {
            TenantId = _tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test_user"
        };

        _dbContext.FiscalPeriods.Add(period);
        _dbContext.SaveChanges();
        return period;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
