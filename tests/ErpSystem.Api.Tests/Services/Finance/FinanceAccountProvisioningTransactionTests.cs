using System.Data.Common;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceAccountProvisioningTransactionTests
{
    [Fact]
    public async Task SqlServerProductionRetryStrategy_IsSelectedWithoutOpeningAConnection()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=offline.invalid;Database=offline_only;Integrated Security=true;Encrypt=true",
                sql => sql.EnableRetryOnFailure())
            .Options);

        var strategy = db.Database.CreateExecutionStrategy();

        strategy.RetriesOnFailure.Should().BeTrue();
        strategy.GetType().Name.Should().Be("SqlServerRetryingExecutionStrategy");
    }

    [Fact]
    public async Task OwnedTransaction_RunsInsideRetryingExecutionStrategy_AndExactRepeatIsIdempotent()
    {
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true);
        var service = fixture.Service();

        fixture.Db.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        var created = await service.ProvisionAsync(Request(fixture.TenantId));
        var repeated = await service.ProvisionAsync(Request(fixture.TenantId));

        created.WasCreated.Should().BeTrue();
        repeated.WasCreated.Should().BeFalse();
        repeated.AccountId.Should().Be(created.AccountId);
        (await fixture.Db.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(1);
        (await fixture.Db.AccountSegmentValues.CountAsync(item => item.AccountId == created.AccountId)).Should().Be(2);
        (await fixture.Db.AccountAccountingBooks.CountAsync(item => item.AccountId == created.AccountId)).Should().Be(3);
    }

    [Fact]
    public async Task OwnedTransaction_RetriesTheCompleteUnitAfterAnOfflineTransientReadFailure()
    {
        var transient = new OneShotTenantReadFailureInterceptor();
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true, transient);
        var tenant = await fixture.Db.Tenants.SingleAsync(item => item.Id == fixture.TenantId);
        tenant.AllowSelfRegistration = true;
        tenant.UpdatedBy = "caller-owned-before-finance";

        var result = await fixture.Service().ProvisionAsync(Request(fixture.TenantId));

        transient.FailuresInjected.Should().Be(1);
        result.WasCreated.Should().BeTrue();
        fixture.Db.ChangeTracker.Clear();
        var persistedTenant = await fixture.Db.Tenants.AsNoTracking().SingleAsync(item => item.Id == fixture.TenantId);
        persistedTenant.AllowSelfRegistration.Should().BeTrue();
        persistedTenant.UpdatedBy.Should().Be("caller-owned-before-finance");
        (await fixture.Db.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(1);
        (await fixture.Db.AccountSegmentStructures.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CallerOwnedTransaction_IsNeitherNestedNorCommittedByProvisioningBoundary()
    {
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true);
        var service = fixture.Service();
        var strategy = fixture.Db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var callerTransaction = await fixture.Db.Database.BeginTransactionAsync();
            var transactionId = callerTransaction.TransactionId;

            await service.ProvisionAsync(Request(fixture.TenantId));

            fixture.Db.Database.CurrentTransaction.Should().NotBeNull();
            fixture.Db.Database.CurrentTransaction!.TransactionId.Should().Be(transactionId);
            await callerTransaction.RollbackAsync();
        });

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(0);
        (await fixture.Db.AccountSegmentStructures.CountAsync()).Should().Be(0);
        (await fixture.Db.FinanceDimensionDefinitions.CountAsync()).Should().Be(0);
        (await fixture.Db.AccountingBooks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OwnedTransaction_FailureRollsBackManifestAndProvisioningWrites()
    {
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true);
        fixture.Db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountCode = "1040", AccountNumber = "1040",
            AccountName = "Conflicting account", AccountType = AccountType.Liability, CurrencyCode = "GHS",
            Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "test"
        });
        await fixture.Db.SaveChangesAsync();
        var service = fixture.Service();

        await service.Invoking(item => item.ProvisionAsync(Request(fixture.TenantId)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different core account type*");

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.Accounts.CountAsync()).Should().Be(1);
        (await fixture.Db.AccountSegmentStructures.CountAsync()).Should().Be(0);
        (await fixture.Db.FinanceDimensionDefinitions.CountAsync()).Should().Be(0);
        (await fixture.Db.AccountingBooks.CountAsync()).Should().Be(0);
        (await fixture.Db.AccountClassifications.CountAsync()).Should().Be(0);
        (await fixture.Db.AccountAccountingBooks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void SourceContract_KeepsLeafSeedersTransactionNeutralAndCallerTransactionUncommitted()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var provisioning = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL",
            "FinanceAccountProvisioningService.cs"));
        var segmentManifest = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Seeders",
            "FinanceSegmentDimensionManifestSeeder.cs"));

        provisioning.Should().Contain("Database.CreateExecutionStrategy()")
            .And.Contain("strategy.ExecuteAsync")
            .And.Contain("Database.CurrentTransaction != null")
            .And.Contain("System.Transactions.Transaction.Current != null");
        segmentManifest.Should().NotContain("BeginTransaction")
            .And.NotContain("CommitAsync")
            .And.Contain("SaveChangesAsync");
    }

    private static ProvisionFinanceAccountDto Request(Guid tenantId) => new()
    {
        TenantId = tenantId,
        AccountCode = "1040",
        AccountNumber = "1040",
        AccountName = "Receipt clearing",
        CoreAccountType = AccountType.Asset,
        CurrencyCode = "GHS"
    };

    public sealed class OfflineRetryingExecutionStrategyFactory(ExecutionStrategyDependencies dependencies)
        : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new OfflineRetryingExecutionStrategy(dependencies);
    }

    private sealed class OfflineRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 2, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryableOfflineException;
    }

    private sealed class RetryableOfflineException : Exception;

    private sealed class OneShotTenantReadFailureInterceptor : DbCommandInterceptor
    {
        private int _remaining = 1;

        public int FailuresInjected { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Accounts\"", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _remaining, 0) == 1)
            {
                FailuresInjected++;
                throw new RetryableOfflineException();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(SqliteConnection connection, ApplicationDbContext db, Guid tenantId)
        {
            _connection = connection;
            Db = db;
            TenantId = tenantId;
        }

        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }

        public static async Task<Fixture> CreateAsync(
            bool useRetryingStrategy,
            params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection);
            if (interceptors.Length > 0)
                builder.AddInterceptors(interceptors);
            if (useRetryingStrategy)
                builder.ReplaceService<IExecutionStrategyFactory, OfflineRetryingExecutionStrategyFactory>();

            var db = new ApplicationDbContext(builder.Options);
            await CreateSchemaAsync(db);
            var tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId, Code = "TDC", Name = "TDC", BaseCurrency = "GHS", Status = TenantStatus.Active
            });
            await db.SaveChangesAsync();
            return new Fixture(connection, db, tenantId);
        }

        private static async Task CreateSchemaAsync(ApplicationDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            var requiredTables = new[]
            {
                "Tenants", "AccountSegmentStructures", "SegmentLookupValues", "FinanceDimensionDefinitions",
                "FinanceSettings", "Accounts", "AccountSegmentValues", "AccountingBooks", "AccountClassifications",
                "AccountAccountingBooks"
            };
            var statements = db.Database.GenerateCreateScript()
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Where(statement => requiredTables.Any(table => statement.Contains(
                    $"CREATE TABLE \"{table}\"", StringComparison.Ordinal)));
            foreach (var statement in statements)
            {
                var sqliteStatement = statement
                    .Replace("nvarchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                    .Replace("\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal);
                await db.Database.ExecuteSqlRawAsync(sqliteStatement);
            }
        }

        public FinanceAccountProvisioningService Service()
        {
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            currentUser.SetupGet(item => item.UserName).Returns("transaction.tests");
            return new FinanceAccountProvisioningService(
                Db, currentUser.Object, NullLogger<FinanceAccountProvisioningService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
