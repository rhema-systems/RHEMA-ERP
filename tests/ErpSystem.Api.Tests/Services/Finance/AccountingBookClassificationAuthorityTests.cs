using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookClassificationAuthorityTests
{
    [Fact]
    public async Task CreateAsync_DefaultsNewClassificationToExcludedRevaluation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var result = await service.CreateAsync(new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id,
            Code = "other_asset",
            Name = "Other asset",
            CoreAccountType = nameof(AccountType.Asset),
            IsPostingClassification = true,
            Status = nameof(AccountClassificationStatus.Active)
        });

        result.Code.Should().Be("OTHER_ASSET");
        result.DefaultRevaluationTreatment.Should().Be(nameof(RevaluationTreatment.Exclude));
    }

    [Fact]
    public async Task SyncAccountMappingsAsync_RequiresCompatibleActivePostingClassification()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var account = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = new AccountingBookService(db, CurrentUser(tenantId).Object);

        var missingClassification = () => service.SyncAccountMappingsAsync(account,
        [
            new AccountAccountingBookUpdateDto { AccountingBookId = book.Id, IsEnabled = true }
        ]);
        await missingClassification.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*compatible active posting classification*");

        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        await db.SaveChangesAsync();
        await service.SyncAccountMappingsAsync(account,
        [
            new AccountAccountingBookUpdateDto
            {
                AccountingBookId = book.Id,
                AccountClassificationId = classification.Id,
                IsEnabled = true
            }
        ]);

        (await db.AccountAccountingBooks.SingleAsync()).AccountClassificationId.Should().Be(classification.Id);
    }

    [Fact]
    public async Task UsedClassification_CodeAndCoreTypeCannotChange()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "CASH", AccountType.Asset);
        var account = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = book.Id,
            AccountClassificationId = classification.Id,
            IsEnabled = true
        });
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var action = () => service.UpdateAsync(classification.Id, new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id,
            Code = "RENAMED",
            Name = "Cash renamed",
            CoreAccountType = nameof(AccountType.Asset),
            DefaultRevaluationTreatment = nameof(RevaluationTreatment.Include),
            IsPostingClassification = true,
            Status = nameof(AccountClassificationStatus.Active),
            RowVersion = Convert.ToBase64String([1])
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*code and core account type are immutable*");
    }

    [Theory]
    [InlineData(nameof(AccountClassificationStatus.Draft), true)]
    [InlineData(nameof(AccountClassificationStatus.Retired), true)]
    [InlineData(nameof(AccountClassificationStatus.Active), false)]
    public async Task UsedByEnabledMapping_CannotBecomeUnavailableForPosting(
        string targetStatus,
        bool isPostingClassification)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        var account = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = book.Id,
            AccountClassificationId = classification.Id,
            IsEnabled = true
        });
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var action = () => service.UpdateAsync(classification.Id, new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id,
            Code = classification.Code,
            Name = classification.Name,
            CoreAccountType = nameof(AccountType.Expense),
            DefaultRevaluationTreatment = nameof(RevaluationTreatment.Exclude),
            IsPostingClassification = isPostingClassification,
            Status = targetStatus,
            RowVersion = Convert.ToBase64String([1])
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*enabled account-book assignments must remain active and posting-enabled*");
    }

    [Fact]
    public async Task RetireAsync_RejectsClassificationUsedByEnabledMapping_ButAllowsDisabledHistory()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "CASH", AccountType.Asset);
        var account = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var mapping = new AccountAccountingBook
        {
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = book.Id,
            AccountClassificationId = classification.Id,
            IsEnabled = true
        };
        db.AccountAccountingBooks.Add(mapping);
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);
        var request = new RetireAccountClassificationDto
        {
            Reason = "Superseded after reviewed remapping.",
            RowVersion = Convert.ToBase64String([1])
        };

        var enabledAction = () => service.RetireAsync(classification.Id, request);
        await enabledAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*enabled account-book assignments cannot be retired*");

        mapping.IsEnabled = false;
        await db.SaveChangesAsync();
        var retired = await service.RetireAsync(classification.Id, request);
        retired.Status.Should().Be(nameof(AccountClassificationStatus.Retired));
    }

    [Fact]
    public async Task SyncAccountMappingsAsync_RejectsStaleExistingMappingRowVersion()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        var account = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = book.Id,
            AccountClassificationId = classification.Id,
            IsEnabled = true,
            RowVersion = [1, 2, 3]
        });
        await db.SaveChangesAsync();
        var service = new AccountingBookService(db, CurrentUser(tenantId).Object);

        var action = () => service.SyncAccountMappingsAsync(account,
        [
            new AccountAccountingBookUpdateDto
            {
                AccountingBookId = book.Id,
                AccountClassificationId = classification.Id,
                IsEnabled = true,
                RowVersion = Convert.ToBase64String([9, 9, 9])
            }
        ]);

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>()
            .WithMessage("*changed after it was loaded*");
    }

    [Fact]
    public async Task SyncAccountMappingsAsync_RequiresRowVersionForExistingMapping()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        var account = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = book.Id,
            AccountClassificationId = classification.Id,
            IsEnabled = true,
            RowVersion = [1, 2, 3]
        });
        await db.SaveChangesAsync();
        var service = new AccountingBookService(db, CurrentUser(tenantId).Object);

        var action = () => service.SyncAccountMappingsAsync(account,
        [
            new AccountAccountingBookUpdateDto
            {
                AccountingBookId = book.Id,
                AccountClassificationId = classification.Id,
                IsEnabled = true
            }
        ]);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Row version is required for an existing account-book assignment.");
    }

    [Fact]
    public async Task ManifestSeeder_IsRepeatableAndMarksOnlyReviewedMonetaryLeavesIncluded()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedAccount(db, tenantId, "1000", AccountType.Asset);
        SeedAccount(db, tenantId, "6100", AccountType.Expense);
        await db.SaveChangesAsync();
        var seeder = new FinanceClassificationManifestSeeder(db, NullLogger.Instance);

        await seeder.SeedAsync(tenantId, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var firstClassifications = await db.AccountClassifications.CountAsync();
        var firstMappings = await db.AccountAccountingBooks.CountAsync();
        var cashAccountId = await db.Accounts.Where(item => item.AccountCode == "1000").Select(item => item.Id).SingleAsync();
        var reviewedMapping = await db.AccountAccountingBooks.Include(item => item.AccountingBook)
            .SingleAsync(item => item.AccountId == cashAccountId && item.AccountingBook.Code == "IFRS");
        reviewedMapping.AccountClassificationId = await db.AccountClassifications
            .Where(item => item.AccountingBookId == reviewedMapping.AccountingBookId && item.Code == "ASSET_OTHER")
            .Select(item => item.Id).SingleAsync();
        reviewedMapping.IsEnabled = false;
        reviewedMapping.UpdatedBy = "finance.admin";
        await db.SaveChangesAsync();
        await seeder.SeedAsync(tenantId, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        (await db.AccountClassifications.CountAsync()).Should().Be(firstClassifications);
        (await db.AccountAccountingBooks.CountAsync()).Should().Be(firstMappings);
        (await db.AccountClassifications.Where(item => item.Code == "CASH")
            .Select(item => item.DefaultRevaluationTreatment).Distinct().SingleAsync())
            .Should().Be(RevaluationTreatment.Include);
        (await db.AccountClassifications.Where(item => item.Code == "EXPENSE")
            .Select(item => item.DefaultRevaluationTreatment).Distinct().SingleAsync())
            .Should().Be(RevaluationTreatment.Exclude);
        reviewedMapping.AccountClassificationId.Should().Be(
            await db.AccountClassifications.Where(item => item.AccountingBookId == reviewedMapping.AccountingBookId && item.Code == "ASSET_OTHER")
                .Select(item => item.Id).SingleAsync());
        reviewedMapping.IsEnabled.Should().BeFalse();
        (await db.AccountClassifications.CountAsync(item => item.Code == "ASSETS" && !item.IsPostingClassification))
            .Should().Be(3);
        (await db.AccountClassifications.Where(item => item.Code == "CASH")
            .AllAsync(item => item.ParentClassificationId != null)).Should().BeTrue();
        FinanceClassificationManifestSeeder.ResolveReviewedClassificationCode("5000", AccountType.Expense)
            .Should().Be("COST_OF_SALES");
    }

    [Fact]
    public async Task Hierarchy_RejectsCrossTypeParentCyclesAndNonLeafAssignments()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var assetRoot = SeedClassification(db, tenantId, book.Id, "ASSETS", AccountType.Asset);
        assetRoot.IsPostingClassification = false;
        var child = SeedClassification(db, tenantId, book.Id, "CASH", AccountType.Asset);
        child.IsPostingClassification = false;
        child.ParentClassificationId = assetRoot.Id;
        var expenseRoot = SeedClassification(db, tenantId, book.Id, "EXPENSES", AccountType.Expense);
        expenseRoot.IsPostingClassification = false;
        var account = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var crossType = () => service.UpdateAsync(child.Id, Request(child, book.Id, parentId: expenseRoot.Id));
        await crossType.Should().ThrowAsync<InvalidOperationException>().WithMessage("*share accounting book and core account type*");

        var cycle = () => service.UpdateAsync(assetRoot.Id, Request(assetRoot, book.Id, parentId: child.Id));
        await cycle.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cycles are prohibited*");

        var mapping = new AccountingBookService(db, CurrentUser(tenantId).Object);
        var nonLeaf = () => mapping.SyncAccountMappingsAsync(account,
        [
            new AccountAccountingBookUpdateDto { AccountingBookId = book.Id, AccountClassificationId = assetRoot.Id, IsEnabled = true }
        ]);
        await nonLeaf.Should().ThrowAsync<InvalidOperationException>().WithMessage("*compatible active posting classification*");
    }

    [Fact]
    public async Task WhereUsed_ReportsEnabledAndHistoricalMappings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        var active = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var historical = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        db.AccountAccountingBooks.AddRange(
            new AccountAccountingBook { TenantId = tenantId, AccountId = active.Id, AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = true },
            new AccountAccountingBook { TenantId = tenantId, AccountId = historical.Id, AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = false });
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var usage = await service.GetWhereUsedAsync(classification.Id);

        usage.TotalMappings.Should().Be(2);
        usage.EnabledMappings.Should().Be(1);
        usage.Mappings.Should().Contain(item => item.AccountCode == "6100" && item.IsEnabled);
        usage.Mappings.Should().Contain(item => item.AccountCode == "6200" && !item.IsEnabled);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleClassificationRowVersion()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        classification.RowVersion = [1, 2, 3];
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var request = Request(classification, book.Id);
        request.RowVersion = Convert.ToBase64String([9, 9, 9]);
        var action = () => service.UpdateAsync(classification.Id, request);

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task CreateAsync_RejectsSystemRoleIncompatibleWithCoreAccountType()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var action = () => service.CreateAsync(new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id, Code = "BANK_REVENUE", Name = "Invalid bank revenue",
            CoreAccountType = nameof(AccountType.Revenue), SystemRole = nameof(AccountClassificationSystemRole.Bank),
            IsPostingClassification = true, Status = nameof(AccountClassificationStatus.Active)
        });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*incompatible with core account type*");
    }

    [Fact]
    public async Task CreateUpdateAndRetire_RecordGovernanceAuditEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        await db.SaveChangesAsync();
        var audit = Audit();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, audit.Object);
        var created = await service.CreateAsync(new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id, Code = "OTHER_ASSET", Name = "Other asset",
            CoreAccountType = nameof(AccountType.Asset), Status = nameof(AccountClassificationStatus.Active),
            IsPostingClassification = true
        });
        var tracked = await db.AccountClassifications.SingleAsync(item => item.Id == created.Id);
        tracked.RowVersion = [1];
        await db.SaveChangesAsync();
        created.RowVersion = Convert.ToBase64String([1]);
        await service.UpdateAsync(created.Id, new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id, Code = created.Code, Name = "Other assets",
            CoreAccountType = nameof(AccountType.Asset), Status = nameof(AccountClassificationStatus.Active),
            IsPostingClassification = true, RowVersion = created.RowVersion.Length == 0 ? Convert.ToBase64String([1]) : created.RowVersion
        });
        var refreshed = await db.AccountClassifications.AsNoTracking().SingleAsync(item => item.Id == created.Id);
        await service.RetireAsync(created.Id, new RetireAccountClassificationDto
        {
            Reason = "Superseded by the approved hierarchy.",
            RowVersion = refreshed.RowVersion.Length == 0 ? Convert.ToBase64String([1]) : Convert.ToBase64String(refreshed.RowVersion)
        });

        audit.Verify(item => item.RecordAsync(It.Is<FinanceAuditEventDto>(e => e.EventType == ErpSystem.Shared.FinanceAuditEvents.AccountClassificationCreated), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(item => item.RecordAsync(It.Is<FinanceAuditEventDto>(e => e.EventType == ErpSystem.Shared.FinanceAuditEvents.AccountClassificationUpdated && e.BeforeValues != null), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(item => item.RecordAsync(It.Is<FinanceAuditEventDto>(e => e.EventType == ErpSystem.Shared.FinanceAuditEvents.AccountClassificationRetired && e.Reason != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SaveAccountClassificationDto Request(AccountClassification item, Guid bookId, Guid? parentId = null) => new()
    {
        AccountingBookId = bookId, ParentClassificationId = parentId, Code = item.Code, Name = item.Name,
        CoreAccountType = item.CoreAccountType.ToString(), DefaultRevaluationTreatment = item.DefaultRevaluationTreatment.ToString(),
        IsPostingClassification = item.IsPostingClassification, Status = item.Status.ToString(),
        RowVersion = item.RowVersion.Length == 0 ? Convert.ToBase64String([1]) : Convert.ToBase64String(item.RowVersion)
    };

    [Fact]
    public async Task ProvisioningBoundary_IsIdempotentAndOwnsBookClassificationMappings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var service = new FinanceAccountProvisioningService(
            db, CurrentUser(tenantId).Object, NullLogger<FinanceAccountProvisioningService>.Instance);
        var request = new ProvisionFinanceAccountDto
        {
            TenantId = tenantId,
            AccountCode = "1010",
            AccountNumber = "000-1010-0000",
            AccountName = "Payroll clearing",
            CoreAccountType = AccountType.Asset,
            CurrencyCode = "GHS"
        };

        var created = await service.ProvisionAsync(request);
        var repeated = await service.ProvisionAsync(request);

        created.WasCreated.Should().BeTrue();
        repeated.WasCreated.Should().BeFalse();
        repeated.AccountId.Should().Be(created.AccountId);
        repeated.ClassificationCode.Should().Be("CASH");
        repeated.AccountingBookCodes.Should().BeEquivalentTo("IFRS", "LOCAL_STATUTORY", "MANAGEMENT");
        (await db.Accounts.CountAsync()).Should().Be(1);
        (await db.AccountAccountingBooks.CountAsync()).Should().Be(3);
    }

    [Fact]
    public void Migration_ContainsOnlyPhase1ABookClassificationOperations()
    {
        var operations = new TestMigration().BuildUpOperations();

        operations.OfType<CreateTableOperation>().Should().ContainSingle(item => item.Name == "AccountClassifications");
        operations.OfType<AddColumnOperation>().Select(item => $"{item.Table}.{item.Name}").Should().BeEquivalentTo(
            "AccountAccountingBooks.AccountClassificationId",
            "AccountAccountingBooks.RowVersion");
        operations.OfType<AddColumnOperation>().Should().NotContain(column =>
            column.Table == "AssetDisposals"
            || column.Table == "AssetDepreciationSchedules"
            || column.Table == "FinanceSourceDimensionAssignments");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"account-book-classification-{Guid.NewGuid():N}")
            .Options);

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("finance.classification.tests");
        return currentUser;
    }

    private static Mock<IFinanceAuditService> Audit()
    {
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpSystem.Core.Entities.AuditLog());
        return audit;
    }

    private static AccountingBook SeedBook(ApplicationDbContext db, Guid tenantId)
    {
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            Purpose = "Primary", IsActive = true, IsDefault = true, AllowsPosting = true
        };
        db.AccountingBooks.Add(book);
        return book;
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string code, AccountType type)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = code, AccountNumber = code,
            AccountName = $"Account {code}", AccountType = type, Status = AccountStatus.Active,
            CurrencyCode = "GHS", AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private static AccountClassification SeedClassification(
        ApplicationDbContext db, Guid tenantId, Guid bookId, string code, AccountType type)
    {
        var classification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = bookId,
            Code = code, Name = code, CoreAccountType = type,
            DefaultRevaluationTreatment = RevaluationTreatment.Exclude,
            IsPostingClassification = true, Status = AccountClassificationStatus.Active,
            RowVersion = [1]
        };
        db.AccountClassifications.Add(classification);
        return classification;
    }

    private sealed class TestMigration : FinanceBookClassificationFoundation
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }
}
