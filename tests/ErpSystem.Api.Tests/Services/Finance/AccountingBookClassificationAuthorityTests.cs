using ErpSystem.Api.Services.Finance.Settings;
using System.Reflection;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.Sqlite;
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

    [Theory]
    [InlineData(AccountType.Equity)]
    [InlineData(AccountType.Revenue)]
    [InlineData(AccountType.Expense)]
    public async Task NonstandardCoreTypes_CannotBypassMakerCheckerThroughClassificationDefault(AccountType accountType)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        await service.Invoking(item => item.CreateAsync(new SaveAccountClassificationDto
            {
                AccountingBookId = book.Id,
                Code = $"NONSTANDARD_{accountType}",
                Name = $"Nonstandard {accountType}",
                CoreAccountType = accountType.ToString(),
                DefaultRevaluationTreatment = nameof(RevaluationTreatment.Include),
                IsPostingClassification = true,
                Status = nameof(AccountClassificationStatus.Active)
            }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*governed per account, book and currency*");
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
        var root = SeedClassification(db, tenantId, book.Id, "EXPENSE_ROOT", AccountType.Expense);
        root.IsPostingClassification = false;
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        classification.ParentClassificationId = root.Id;
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
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "TDC", Name = "TDC", BaseCurrency = "GHS", Status = TenantStatus.Active });
        SeedAccount(db, tenantId, "1000", AccountType.Asset);
        SeedAccount(db, tenantId, "6100", AccountType.Expense);
        await db.SaveChangesAsync();
        var seeder = new FinanceClassificationManifestSeeder(db, NullLogger.Instance);

        await seeder.SeedAsync(tenantId, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var firstClassifications = await db.AccountClassifications.CountAsync();
        var firstMappings = await db.AccountAccountingBooks.CountAsync();
        var adminCash = await db.AccountClassifications.SingleAsync(item => item.Code == "CASH" && item.AccountingBook!.Code == "IFRS");
        adminCash.DefaultRevaluationTreatment = RevaluationTreatment.Exclude;
        adminCash.UpdatedBy = "finance.admin";
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
        adminCash.DefaultRevaluationTreatment.Should().Be(RevaluationTreatment.Exclude,
            "the manifest must preserve an explicit administrator decision");
        (await db.AccountClassifications.Where(item => item.Code == "CASH" && item.AccountingBook!.Code != "IFRS")
            .AllAsync(item => item.DefaultRevaluationTreatment == RevaluationTreatment.Include)).Should().BeTrue();
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
    public async Task ManifestSeeder_MissingTenantAuthority_FailsWithGovernedError()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var seeder = new FinanceClassificationManifestSeeder(db, NullLogger.Instance);

        await seeder.Invoking(item => item.SeedAsync(tenantId, DateTime.UtcNow))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Tenant functional-currency authority is missing for tenant '{tenantId}'. Persist the tenant before seeding Finance classifications.");
    }

    [Theory]
    [InlineData("ghs")]
    [InlineData("GHS ")]
    [InlineData("123")]
    [InlineData("ÉÉÉ")]
    public async Task ManifestSeeder_NoncanonicalTenantCurrency_FailsWithGovernedError(string currency)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = $"T-{tenantId:N}"[..20],
            Name = "Tenant",
            BaseCurrency = currency,
            Status = TenantStatus.Active
        });
        await db.SaveChangesAsync();
        var seeder = new FinanceClassificationManifestSeeder(db, NullLogger.Instance);

        await seeder.Invoking(item => item.SeedAsync(tenantId, DateTime.UtcNow))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Tenant functional currency for tenant '{tenantId}' must be exactly three uppercase ASCII letters (for example, GHS) before accounting books are seeded.");
    }

    [Fact]
    public async Task ManifestSeeder_UpgradesUntouchedV1BroadMappingsAcrossTenants_AndPreservesAdminRows()
    {
        await using var db = CreateContext();
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var expected = new Dictionary<Guid, (Guid RevenueDeduction, Guid CostOfSales)>();
        foreach (var tenantId in tenants)
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Code = $"T-{tenantId:N}"[..20], Name = "Tenant", BaseCurrency = "GHS", Status = TenantStatus.Active });
            var book = SeedBook(db, tenantId);
            var revenue = SeedClassification(db, tenantId, book.Id, "REVENUE", AccountType.Revenue);
            var expense = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
            revenue.CreatedBy = expense.CreatedBy = "System (FIN-CLASSIFICATION-1.0)";
            var deduction = SeedAccount(db, tenantId, "4210", AccountType.Revenue);
            var cost = SeedAccount(db, tenantId, "5000", AccountType.Expense);
            var admin = SeedAccount(db, tenantId, "7110", AccountType.Expense);
            var disabled = SeedAccount(db, tenantId, "7210", AccountType.Expense);
            db.AccountAccountingBooks.AddRange(
                new AccountAccountingBook { TenantId = tenantId, AccountId = deduction.Id, AccountingBookId = book.Id, AccountClassificationId = revenue.Id, IsEnabled = true },
                new AccountAccountingBook { TenantId = tenantId, AccountId = cost.Id, AccountingBookId = book.Id, AccountClassificationId = expense.Id, IsEnabled = true },
                new AccountAccountingBook { TenantId = tenantId, AccountId = admin.Id, AccountingBookId = book.Id, AccountClassificationId = expense.Id, IsEnabled = true, UpdatedBy = "finance.admin" },
                new AccountAccountingBook { TenantId = tenantId, AccountId = disabled.Id, AccountingBookId = book.Id, AccountClassificationId = expense.Id, IsEnabled = false });
        }
        await db.SaveChangesAsync();
        var seeder = new FinanceClassificationManifestSeeder(db, NullLogger.Instance);

        foreach (var tenantId in tenants) await seeder.SeedAsync(tenantId, DateTime.UtcNow);
        var countAfterUpgrade = await db.AccountAccountingBooks.CountAsync();
        foreach (var tenantId in tenants) await seeder.SeedAsync(tenantId, DateTime.UtcNow);

        (await db.AccountAccountingBooks.CountAsync()).Should().Be(countAfterUpgrade);
        foreach (var tenantId in tenants)
        {
            var rows = await db.AccountAccountingBooks.Include(item => item.Account).Include(item => item.AccountClassification)
                .Where(item => item.TenantId == tenantId && item.AccountingBook.Code == "IFRS").ToListAsync();
            rows.Single(item => item.Account.AccountCode == "4210").AccountClassification!.Code.Should().Be("REVENUE_DEDUCTIONS");
            rows.Single(item => item.Account.AccountCode == "5000").AccountClassification!.Code.Should().Be("COST_OF_SALES");
            rows.Single(item => item.Account.AccountCode == "7110").AccountClassification!.Code.Should().Be("EXPENSE");
            rows.Single(item => item.Account.AccountCode == "7210").AccountClassification!.Code.Should().Be("EXPENSE");
        }
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateSingletonRole_ButAllowsRepeatedCash()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var existing = SeedClassification(db, tenantId, book.Id, "AR_ONE", AccountType.Asset);
        existing.SystemRole = AccountClassificationSystemRole.ReceivableControl;
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var duplicate = () => service.CreateAsync(new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id, Code = "AR_TWO", Name = "AR two", CoreAccountType = nameof(AccountType.Asset),
            SystemRole = nameof(AccountClassificationSystemRole.ReceivableControl), IsPostingClassification = true,
            Status = nameof(AccountClassificationStatus.Active)
        });
        await duplicate.Should().ThrowAsync<InvalidOperationException>().WithMessage("*only once*");

        existing.SystemRole = AccountClassificationSystemRole.Cash;
        await db.SaveChangesAsync();
        var repeatedCash = await service.CreateAsync(new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id, Code = "CASH_TWO", Name = "Cash two", CoreAccountType = nameof(AccountType.Asset),
            SystemRole = nameof(AccountClassificationSystemRole.Cash), IsPostingClassification = true,
            Status = nameof(AccountClassificationStatus.Active)
        });
        repeatedCash.SystemRole.Should().Be(nameof(AccountClassificationSystemRole.Cash));
    }

    [Fact]
    public async Task CreateAsync_RollsBackMutationWhenAuditFails_InRelationalTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await CreateSqliteClassificationSchemaAsync(db);
        var tenantId = Guid.NewGuid();
        var book = SeedBook(db, tenantId);
        await db.SaveChangesAsync();
        var audit = Audit();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .Callback(() => db.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel
                .Should().Be(System.Data.IsolationLevel.Serializable))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, audit.Object);

        var action = () => service.CreateAsync(new SaveAccountClassificationDto
        {
            AccountingBookId = book.Id, Code = "ROLLBACK", Name = "Rollback", CoreAccountType = nameof(AccountType.Asset),
            IsPostingClassification = true, Status = nameof(AccountClassificationStatus.Active)
        });
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");
        db.ChangeTracker.Clear();
        (await db.AccountClassifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SingletonRoleConstraint_RejectsTwoWritersThatBothObservedNoExistingRole()
    {
        var databaseName = $"role-cardinality-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        await using var first = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(keeper).Options);
        await CreateSqliteClassificationSchemaAsync(first, includeRoleIndex: true);
        await using var secondConnection = new SqliteConnection(connectionString);
        await secondConnection.OpenAsync();
        await using var second = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(secondConnection).Options);
        await second.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var tenantId = Guid.NewGuid();
        var book = SeedBook(first, tenantId);
        await first.SaveChangesAsync();

        (await first.AccountClassifications.AnyAsync(item => item.TenantId == tenantId
            && item.AccountingBookId == book.Id && item.SystemRole == AccountClassificationSystemRole.ReceivableControl)).Should().BeFalse();
        (await second.AccountClassifications.AnyAsync(item => item.TenantId == tenantId
            && item.AccountingBookId == book.Id && item.SystemRole == AccountClassificationSystemRole.ReceivableControl)).Should().BeFalse();
        var one = SeedClassification(first, tenantId, book.Id, "AR_ONE", AccountType.Asset);
        var two = SeedClassification(second, tenantId, book.Id, "AR_TWO", AccountType.Asset);
        one.SystemRole = two.SystemRole = AccountClassificationSystemRole.ReceivableControl;

        await first.SaveChangesAsync();
        var staleWriter = () => second.SaveChangesAsync();
        await staleWriter.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RetirementTransaction_PreventsConcurrentChildAndMappingWritersFromPassingStaleChecks()
    {
        var databaseName = $"lifecycle-race-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=1";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        await using var retiringDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(keeper).Options);
        await CreateSqliteClassificationSchemaAsync(retiringDb, includeMappings: true);
        await using var writerConnection = new SqliteConnection(connectionString);
        await writerConnection.OpenAsync();
        await using var writerDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(writerConnection).Options);
        await writerDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var tenantId = Guid.NewGuid();
        var book = SeedBook(retiringDb, tenantId);
        var classification = SeedClassification(retiringDb, tenantId, book.Id, "OTHER_ASSET", AccountType.Asset);
        var account = SeedAccount(retiringDb, tenantId, "1990", AccountType.Asset);
        await retiringDb.SaveChangesAsync();
        await retiringDb.Database.ExecuteSqlRawAsync(
            "UPDATE \"AccountClassifications\" SET \"RowVersion\" = X'01' WHERE \"Id\" = {0}", classification.Id);
        retiringDb.ChangeTracker.Clear();

        var auditEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAudit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audit = Audit();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                auditEntered.TrySetResult();
                await releaseAudit.Task;
                return new ErpSystem.Core.Entities.AuditLog();
            });
        var retiringService = new AccountClassificationService(retiringDb, CurrentUser(tenantId).Object, audit.Object);
        var retirement = retiringService.RetireAsync(classification.Id, new RetireAccountClassificationDto
        {
            Reason = "Superseded", RowVersion = Convert.ToBase64String([1])
        });
        await auditEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            var childWriter = new AccountClassificationService(writerDb, CurrentUser(tenantId).Object, Audit().Object);
            var childAttempt = () => childWriter.CreateAsync(new SaveAccountClassificationDto
            {
                AccountingBookId = book.Id, ParentClassificationId = classification.Id,
                Code = "LATE_CHILD", Name = "Late child", CoreAccountType = nameof(AccountType.Asset),
                IsPostingClassification = true, Status = nameof(AccountClassificationStatus.Active)
            });
            var childFailure = await childAttempt.Should().ThrowAsync<Exception>();
            (childFailure.Which is SqliteException || childFailure.Which is DbUpdateException).Should().BeTrue();

            var mappingWriter = new AccountingBookService(writerDb, CurrentUser(tenantId).Object);
            var mappingAttempt = () => mappingWriter.SyncAccountMappingsAsync(account,
            [
                new AccountAccountingBookUpdateDto
                {
                    AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = true
                }
            ]);
            var mappingFailure = await mappingAttempt.Should().ThrowAsync<Exception>();
            (mappingFailure.Which is SqliteException || mappingFailure.Which is DbUpdateException).Should().BeTrue();
        }
        finally
        {
            releaseAudit.TrySetResult();
        }
        (await retirement).Status.Should().Be(nameof(AccountClassificationStatus.Retired));
    }

    [Fact]
    public async Task RetiredChildren_DoNotBlockParentRetirementContract()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var book = SeedBook(db, tenantId);
        var parent = SeedClassification(db, tenantId, book.Id, "ROOT", AccountType.Asset);
        parent.IsPostingClassification = false;
        var child = SeedClassification(db, tenantId, book.Id, "CHILD", AccountType.Asset);
        child.ParentClassificationId = parent.Id;
        child.Status = AccountClassificationStatus.Retired;
        await db.SaveChangesAsync();

        var dto = (await new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object)
            .GetAsync(book.Id, true)).Single(item => item.Id == parent.Id);
        dto.ChildCount.Should().Be(1);
        dto.NonRetiredChildCount.Should().Be(0);
        dto.CanRetire.Should().BeTrue();
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
        var root = SeedClassification(db, tenantId, book.Id, "EXPENSE_ROOT", AccountType.Expense);
        root.IsPostingClassification = false;
        var classification = SeedClassification(db, tenantId, book.Id, "EXPENSE", AccountType.Expense);
        classification.ParentClassificationId = root.Id;
        var active = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var historical = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        db.AccountAccountingBooks.AddRange(
            new AccountAccountingBook { TenantId = tenantId, AccountId = active.Id, AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = true },
            new AccountAccountingBook { TenantId = tenantId, AccountId = historical.Id, AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = false });
        var layout = new FinancialStatementLayout
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IS_USAGE", Name = "Usage layout",
            StatementType = FinancialStatementType.IncomeStatement, AccountingBookId = book.Id,
            IsActive = true, Revision = 1
        };
        var draft = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutId = layout.Id,
            VersionNumber = 1, Status = FinancialStatementLayoutVersionStatus.Draft, Revision = 1
        };
        var row = new FinancialStatementRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutVersionId = draft.Id,
            RowCode = "EXPENSE", Label = "Expense", RowType = FinancialStatementRowType.Account,
            DisplayOrder = 10, IsVisible = true, SignMultiplier = 1
        };
        row.Mappings.Add(new FinancialStatementRowMapping
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementRowId = row.Id,
            MappingType = FinancialStatementRowMappingType.Classification,
            AccountClassificationId = root.Id, IncludeClassificationDescendants = true
        });
        draft.Rows.Add(row);
        layout.Versions.Add(draft);
        var published = new FinancialStatementLayoutVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutId = layout.Id,
            VersionNumber = 2, Status = FinancialStatementLayoutVersionStatus.Published, Revision = 1
        };
        var publishedRow = new FinancialStatementRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutVersionId = published.Id,
            RowCode = "EXPENSE", Label = "Expense", RowType = FinancialStatementRowType.Account,
            DisplayOrder = 10, IsVisible = true, SignMultiplier = 1
        };
        publishedRow.Mappings.Add(new FinancialStatementRowMapping
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementRowId = publishedRow.Id,
            MappingType = FinancialStatementRowMappingType.Classification,
            AccountClassificationId = root.Id, IncludeClassificationDescendants = true
        });
        published.Rows.Add(publishedRow);
        layout.Versions.Add(published);
        db.FinancialStatementLayouts.Add(layout);
        db.FinancialStatementPublicationAccounts.Add(new FinancialStatementPublicationAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutVersionId = published.Id,
            FinancialStatementRowId = publishedRow.Id, FinancialStatementRowMappingId = publishedRow.Mappings.Single().Id,
            MappingType = FinancialStatementRowMappingType.Classification, AccountId = active.Id,
            RowCode = row.RowCode, AccountNumber = active.AccountNumber, AccountName = active.AccountName,
            AccountType = active.AccountType, AccountingBookId = book.Id, AccountingBookCode = book.Code,
            AccountClassificationId = classification.Id, ClassificationCode = classification.Code,
            ClassificationName = classification.Name, ClassificationPath = classification.Code
        });
        await db.SaveChangesAsync();
        var service = new AccountClassificationService(db, CurrentUser(tenantId).Object, Audit().Object);

        var usage = await service.GetWhereUsedAsync(classification.Id);

        usage.TotalMappings.Should().Be(2);
        usage.EnabledMappings.Should().Be(1);
        usage.Mappings.Should().Contain(item => item.AccountCode == "6100" && item.IsEnabled);
        usage.Mappings.Should().Contain(item => item.AccountCode == "6200" && !item.IsEnabled);
        usage.DraftLayoutReferences.Should().Be(1);
        usage.PublishedLayoutReferences.Should().Be(1);
        usage.LayoutReferences.Should().Contain(item => item.LayoutCode == "IS_USAGE" && !item.IsHistoricalSnapshot);
        usage.LayoutReferences.Should().Contain(item => item.LayoutCode == "IS_USAGE" && item.IsHistoricalSnapshot);

        var rootUsage = await service.GetWhereUsedAsync(root.Id);
        rootUsage.DraftLayoutReferences.Should().Be(1);
        rootUsage.PublishedLayoutReferences.Should().Be(1);

        var reparent = () => service.UpdateAsync(classification.Id, Request(classification, book.Id));
        await reparent.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*draft financial-statement layout*");
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
        SeedProvisioningTenant(db, tenantId);
        await db.SaveChangesAsync();
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

    [Theory]
    [InlineData("1040", AccountType.Asset, "ASSET_OTHER")]
    [InlineData("4930", AccountType.Revenue, "OTHER_INCOME")]
    [InlineData("2210", AccountType.Liability, "OUTPUT_TAX")]
    public async Task ProvisioningBoundary_AdoptsLegacyProcurementSeederIdentityAndIsIdempotent(
        string accountCode,
        AccountType accountType,
        string expectedClassification)
    {
        var tenantId = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedProvisioningTenant(db, tenantId);
        db.Accounts.Add(new Account
        {
            Id = legacyId, TenantId = tenantId, AccountCode = accountCode, AccountNumber = accountCode,
            AccountName = "Legacy Procurement onboarding account", AccountType = accountType,
            CurrencyCode = "GHS", IsSegmented = false, Status = AccountStatus.Active,
            CreatedBy = "Development supplier-onboarding seeder", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new FinanceAccountProvisioningService(
            db, CurrentUser(tenantId).Object, NullLogger<FinanceAccountProvisioningService>.Instance);
        var request = new ProvisionFinanceAccountDto
        {
            TenantId = tenantId, AccountCode = accountCode, AccountNumber = accountCode,
            AccountName = "Legacy Procurement onboarding account", CoreAccountType = accountType,
            CurrencyCode = "GHS"
        };

        var adopted = await service.ProvisionAsync(request);
        var repeated = await service.ProvisionAsync(request);

        adopted.AccountId.Should().Be(legacyId);
        adopted.WasCreated.Should().BeFalse();
        repeated.AccountId.Should().Be(legacyId);
        repeated.WasCreated.Should().BeFalse();
        var account = await db.Accounts.Include(item => item.SegmentValues).SingleAsync();
        account.AccountNumber.Should().NotBe(accountCode);
        account.IsSegmented.Should().BeTrue();
        account.SegmentValues.Where(item => !item.IsDeleted).Should().HaveCount(2);
        adopted.ClassificationCode.Should().Be(expectedClassification);
        adopted.AccountingBookCodes.Should().BeEquivalentTo("IFRS", "LOCAL_STATUTORY", "MANAGEMENT");
        (await db.AccountAccountingBooks.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task ProvisioningBoundary_RejectsLegacySeederWrongTypeWithoutMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedProvisioningTenant(db, tenantId);
        var legacy = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1040", AccountNumber = "1040",
            AccountName = "Conflicting account", AccountType = AccountType.Liability,
            CurrencyCode = "GHS", IsSegmented = false, Status = AccountStatus.Active,
            CreatedBy = "Development supplier-onboarding seeder", CreatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(legacy);
        await db.SaveChangesAsync();
        var service = new FinanceAccountProvisioningService(
            db, CurrentUser(tenantId).Object, NullLogger<FinanceAccountProvisioningService>.Instance);

        var action = () => service.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = tenantId, AccountCode = "1040", AccountName = "Receipt clearing",
            CoreAccountType = AccountType.Asset, CurrencyCode = "GHS"
        });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different core account type*");
        legacy.AccountNumber.Should().Be("1040");
        (await db.AccountSegmentValues.CountAsync()).Should().Be(0);
        (await db.AccountAccountingBooks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProvisioningBoundary_RejectsLegacySeederWithConflictingIdentityEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedProvisioningTenant(db, tenantId);
        var legacy = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1040", AccountNumber = "1040",
            AccountName = "Receipt clearing", AccountType = AccountType.Asset, CurrencyCode = "GHS",
            IsSegmented = false, Status = AccountStatus.Active,
            CreatedBy = "Development supplier-onboarding seeder", CreatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(legacy);
        db.AccountSegmentValues.Add(new AccountSegmentValue
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = legacy.Id,
            SegmentStructureId = Guid.NewGuid(), SegmentPosition = 9, SegmentValue = "CORRUPT",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new FinanceAccountProvisioningService(
            db, CurrentUser(tenantId).Object, NullLogger<FinanceAccountProvisioningService>.Instance);

        var action = () => service.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = tenantId, AccountCode = "1040", AccountName = "Receipt clearing",
            CoreAccountType = AccountType.Asset, CurrencyCode = "GHS"
        });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not ready*");
        legacy.AccountNumber.Should().Be("1040");
        (await db.AccountAccountingBooks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Migration_ContainsOnlyPhase1ABookClassificationOperations()
    {
        var source = ArchivedMigrationSource.Read("20260903044911_FinanceBookClassificationFoundation.cs");

        source.Should().Contain("migrationBuilder.CreateTable(").And.Contain("name: \"AccountClassifications\"");
        source.Should().Contain("name: \"AccountClassificationId\",\n                table: \"AccountAccountingBooks\"");
        source.Should().Contain("name: \"RowVersion\",\n                table: \"AccountAccountingBooks\"");
        source.Should().NotContain("AssetDisposals");
        source.Should().NotContain("AssetDepreciationSchedules");
        source.Should().NotContain("FinanceSourceDimensionAssignments");

        var baseline = new DisposableDevelopmentCurrentModelBaseline();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(DisposableDevelopmentCurrentModelBaseline)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(baseline, [builder]);
        builder.Operations.OfType<CreateTableOperation>()
            .Should().ContainSingle(item => item.Name == "AccountClassifications");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"account-book-classification-{Guid.NewGuid():N}")
            .Options);

    private static void SeedProvisioningTenant(ApplicationDbContext db, Guid tenantId) =>
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = "TDC",
            Name = "TDC",
            Status = TenantStatus.Active
        });

    private static async Task CreateSqliteClassificationSchemaAsync(
        ApplicationDbContext db, bool includeRoleIndex = false, bool includeMappings = false)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var createScript = db.Database.GenerateCreateScript();
        var statements = createScript.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(statement => statement.Contains("CREATE TABLE \"AccountingBooks\"", StringComparison.Ordinal)
                || statement.Contains("CREATE TABLE \"AccountClassifications\"", StringComparison.Ordinal)
                || (includeMappings && statement.Contains("CREATE TABLE \"Accounts\"", StringComparison.Ordinal))
                || (includeMappings && statement.Contains("CREATE TABLE \"AccountAccountingBooks\"", StringComparison.Ordinal))
                || (includeMappings && statement.Contains("CREATE TABLE \"FinancialStatementLayouts\"", StringComparison.Ordinal))
                || (includeMappings && statement.Contains("CREATE TABLE \"FinancialStatementLayoutVersions\"", StringComparison.Ordinal))
                || (includeMappings && statement.Contains("CREATE TABLE \"FinancialStatementRows\"", StringComparison.Ordinal))
                || (includeMappings && statement.Contains("CREATE TABLE \"FinancialStatementRowMappings\"", StringComparison.Ordinal))
                || (includeRoleIndex && statement.Contains(
                    "IX_AccountClassifications_TenantId_AccountingBookId_SystemRole", StringComparison.Ordinal)));
        foreach (var statement in statements)
            await db.Database.ExecuteSqlRawAsync(statement.Replace(
                "\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal));
    }

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
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsActive = true, IsDefault = true, AllowsPosting = true
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

    [Fact]
    public void CardinalityMigration_AddsFilteredSingletonRoleConstraint()
    {
        var source = ArchivedMigrationSource.Read(
            "20260903120000_EnforceFinanceClassificationSystemRoleCardinality.cs");
        source.Should().Contain("IX_AccountClassifications_TenantId_AccountingBookId_SystemRole");
        source.Should().Contain(
            "[IsDeleted] = 0 AND [SystemRole] IS NOT NULL AND [SystemRole] <> 1 AND [SystemRole] <> 2");

        var baseline = new DisposableDevelopmentCurrentModelBaseline();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(DisposableDevelopmentCurrentModelBaseline)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(baseline, [builder]);
        var index = builder.Operations.OfType<CreateIndexOperation>().Single(item =>
            item.Name == "IX_AccountClassifications_TenantId_AccountingBookId_SystemRole");
        index.IsUnique.Should().BeTrue();
        index.Filter.Should().Be(
            "[IsDeleted] = 0 AND [SystemRole] IS NOT NULL AND [SystemRole] <> 1 AND [SystemRole] <> 2");
        index.Filter.Should().NotContain("NOT IN");
    }
}
