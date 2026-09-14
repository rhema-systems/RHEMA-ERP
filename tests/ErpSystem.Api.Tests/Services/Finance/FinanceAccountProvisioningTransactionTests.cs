using System.Data.Common;
using System.Text.RegularExpressions;
using ErpSystem.Api.Extensions;
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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
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
    public void LockResources_AreCanonicalTenantBoundAndDeterministicWithoutAProvider()
    {
        var tenant = Guid.Parse("9c63a702-f177-4b45-b3a3-37408ef340f3");

        var lower = FinanceAccountProvisioningService.BuildProvisioningLockResources(tenant, " 1040 ");
        var canonical = FinanceAccountProvisioningService.BuildProvisioningLockResources(tenant, "1040");
        var otherAccount = FinanceAccountProvisioningService.BuildProvisioningLockResources(tenant, "4930");
        var otherTenant = FinanceAccountProvisioningService.BuildProvisioningLockResources(Guid.NewGuid(), "1040");

        lower.Should().Be(canonical);
        canonical.Manifest.Should().Be("RHEMA:FIN:PROVISION:MANIFEST:9c63a702f1774b45b3a337408ef340f3");
        canonical.Account.Should().Be("RHEMA:FIN:PROVISION:ACCOUNT:9c63a702f1774b45b3a337408ef340f3:1040");
        otherAccount.Manifest.Should().Be(canonical.Manifest);
        otherAccount.Account.Should().NotBe(canonical.Account);
        otherTenant.Manifest.Should().NotBe(canonical.Manifest);
        canonical.Manifest.Length.Should().BeLessThan(255);
        canonical.Account.Length.Should().BeLessThan(255);
    }

    [Fact]
    public async Task OwnedTransaction_RunsInsideRetryingExecutionStrategy_AndExactRepeatIsIdempotent()
    {
        var writes = new WriteCommandCounter();
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true, writes);
        var service = fixture.Service();

        fixture.Db.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        var created = await service.ProvisionAsync(Request(fixture.TenantId));
        var writesAfterCreate = writes.Commands;
        var repeated = await service.ProvisionAsync(Request(fixture.TenantId));

        created.WasCreated.Should().BeTrue();
        repeated.WasCreated.Should().BeFalse();
        repeated.AccountId.Should().Be(created.AccountId);
        (await fixture.Db.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(1);
        (await fixture.Db.AccountSegmentValues.CountAsync(item => item.AccountId == created.AccountId)).Should().Be(2);
        (await fixture.Db.AccountAccountingBooks.CountAsync(item => item.AccountId == created.AccountId)).Should().Be(3);
        writes.Commands.Should().Be(writesAfterCreate, "an exact converged retry must issue no INSERT, UPDATE or DELETE");
    }

    [Fact]
    public async Task OwnedTransaction_RetriesTheCompleteUnitAfterAnOfflineTransientReadFailure()
    {
        var transient = new OneShotTenantReadFailureInterceptor();
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true, transient);
        var result = await fixture.Service().ProvisionAsync(Request(fixture.TenantId));

        transient.FailuresInjected.Should().Be(1);
        result.WasCreated.Should().BeTrue();
        (await fixture.Db.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(1);
        (await fixture.Db.AccountSegmentStructures.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ScopedOwnedTransaction_PreservesUnchangedMaterializedCallerGraph_AndConvergesExactly()
    {
        await using var fixture = await ScopedFixture.CreateAsync(useRetryingStrategy: false);
        var tenant = await fixture.Db.Tenants.SingleAsync(item => item.Id == fixture.TenantId);
        var navigation = fixture.Db.Entry(tenant).Collection(item => item.UserTenants);
        var materializedCollection = tenant.UserTenants;
        navigation.IsLoaded = true;

        var created = await fixture.Service.ProvisionAsync(Request(fixture.TenantId));
        var repeated = await fixture.Service.ProvisionAsync(Request(fixture.TenantId));

        repeated.AccountId.Should().Be(created.AccountId);
        created.WasCreated.Should().BeTrue();
        repeated.WasCreated.Should().BeFalse();
        fixture.Db.Entry(tenant).State.Should().Be(EntityState.Unchanged);
        tenant.UserTenants.Should().BeSameAs(materializedCollection);
        navigation.IsLoaded.Should().BeTrue();
        fixture.Db.ChangeTracker.Entries().Should().OnlyContain(entry => entry.State == EntityState.Unchanged);
        await using var verify = fixture.CreateVerificationContext();
        (await verify.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(1);
        (await verify.AccountSegmentValues.CountAsync(item => item.AccountId == created.AccountId)).Should().Be(2);
        (await verify.AccountAccountingBooks.CountAsync(item => item.AccountId == created.AccountId)).Should().Be(3);
    }

    [Fact]
    public async Task ScopedOwnedTransaction_RejectsCallerPendingScalarBeforeCreatingIsolationScope()
    {
        await using var fixture = await ScopedFixture.CreateAsync(useRetryingStrategy: false);
        var tenant = await fixture.Db.Tenants.SingleAsync(item => item.Id == fixture.TenantId);
        tenant.AllowSelfRegistration = true;

        await fixture.Service.Invoking(item => item.ProvisionAsync(Request(fixture.TenantId)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_ACCOUNT_PROVISIONING_CALLER_HAS_PENDING_WORK:*");

        fixture.Db.Entry(tenant).State.Should().Be(EntityState.Modified);
        tenant.AllowSelfRegistration.Should().BeTrue();
        await using var verify = fixture.CreateVerificationContext();
        (await verify.Accounts.CountAsync()).Should().Be(0);
        (await verify.AccountSegmentStructures.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ScopedOwnedTransaction_RejectsPendingRelationshipWithoutChangingCallerGraph()
    {
        await using var fixture = await ScopedFixture.CreateAsync(useRetryingStrategy: false);
        var firstAccount = TestAccount(fixture.TenantId, "1100");
        var secondAccount = TestAccount(fixture.TenantId, "1200");
        var structure = new AccountSegmentStructure
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, SegmentCode = "TEST_SEGMENT",
            SegmentName = "Test segment", SegmentPosition = 1, SegmentLength = 4,
            DataType = "Numeric", IsActive = true, LifecycleStatus = AccountSegmentLifecycleStatus.Active
        };
        var segment = new AccountSegmentValue
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Account = firstAccount,
            AccountId = firstAccount.Id, SegmentStructure = structure, SegmentStructureId = structure.Id,
            SegmentPosition = 1, SegmentValue = "1100"
        };
        fixture.Db.AddRange(firstAccount, secondAccount, structure, segment);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var callerSegment = await fixture.Db.AccountSegmentValues
            .Include(item => item.Account).SingleAsync(item => item.Id == segment.Id);
        var callerSecondAccount = await fixture.Db.Accounts.SingleAsync(item => item.Id == secondAccount.Id);
        callerSegment.Account = callerSecondAccount;
        callerSegment.AccountId = secondAccount.Id;

        await fixture.Service.Invoking(item => item.ProvisionAsync(Request(fixture.TenantId)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_ACCOUNT_PROVISIONING_CALLER_HAS_PENDING_WORK:*");

        callerSegment.Account.Should().BeSameAs(callerSecondAccount);
        callerSegment.AccountId.Should().Be(secondAccount.Id);
        fixture.Db.Entry(callerSegment).State.Should().Be(EntityState.Modified);
        await using var verify = fixture.CreateVerificationContext();
        (await verify.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(0);
        (await verify.AccountSegmentValues.AsNoTracking().SingleAsync(item => item.Id == segment.Id))
            .AccountId.Should().Be(firstAccount.Id);
    }

    [Fact]
    public async Task ScopedOwnedTransaction_PostWriteTransientFailureRetriesWholeUnitWithoutCallerGraphCorruption()
    {
        var transient = new OneShotPostWriteFailureInterceptor();
        await using var fixture = await ScopedFixture.CreateAsync(useRetryingStrategy: true, transient);
        var tenant = await fixture.Db.Tenants.SingleAsync(item => item.Id == fixture.TenantId);
        var navigation = fixture.Db.Entry(tenant).Collection(item => item.UserTenants);
        var materializedCollection = tenant.UserTenants;
        navigation.IsLoaded = true;
        transient.Arm();

        var result = await fixture.Service.ProvisionAsync(Request(fixture.TenantId));

        transient.FailuresInjected.Should().Be(1);
        fixture.Db.Entry(tenant).State.Should().Be(EntityState.Unchanged);
        tenant.UserTenants.Should().BeSameAs(materializedCollection);
        navigation.IsLoaded.Should().BeTrue();
        await using var verify = fixture.CreateVerificationContext();
        (await verify.Accounts.CountAsync(item => item.AccountCode == "1040")).Should().Be(1);
        (await verify.AccountSegmentValues.CountAsync(item => item.AccountId == result.AccountId)).Should().Be(2);
        (await verify.AccountAccountingBooks.CountAsync(item => item.AccountId == result.AccountId)).Should().Be(3);
        (await verify.AccountSegmentStructures.CountAsync()).Should().Be(2);
        (await verify.FinanceDimensionDefinitions.CountAsync()).Should().Be(6);
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
        fixture.Db.ChangeTracker.Clear();
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
    public async Task OwnedRetry_RejectsPretrackedAddedStateBeforeAnyFinanceWrite()
    {
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true);
        var pending = new Tenant
        {
            Id = Guid.NewGuid(), Code = "PENDING", Name = "Pending caller tenant",
            BaseCurrency = "GHS", Status = TenantStatus.Active
        };
        fixture.Db.Tenants.Add(pending);

        await fixture.Service().Invoking(item => item.ProvisionAsync(Request(fixture.TenantId)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_ACCOUNT_PROVISIONING_TRACKER_NOT_RETRY_SAFE:*");

        fixture.Db.Entry(pending).State.Should().Be(EntityState.Added);
        (await fixture.Db.Accounts.AsNoTracking().CountAsync()).Should().Be(0);
        (await fixture.Db.AccountSegmentStructures.AsNoTracking().CountAsync()).Should().Be(0);
        (await fixture.Db.FinanceDimensionDefinitions.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OwnedRetry_RejectsLoadedNavigationBeforeWritesWithoutChangingItsState()
    {
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true);
        var tenant = await fixture.Db.Tenants.SingleAsync(item => item.Id == fixture.TenantId);
        var navigation = fixture.Db.Entry(tenant).Navigations.First();
        var relationshipValue = navigation.CurrentValue;
        navigation.IsLoaded = true;

        await fixture.Service().Invoking(item => item.ProvisionAsync(Request(fixture.TenantId)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_ACCOUNT_PROVISIONING_TRACKER_NOT_RETRY_SAFE:*");

        navigation.IsLoaded.Should().BeTrue();
        navigation.CurrentValue.Should().BeSameAs(relationshipValue);
        fixture.Db.Entry(tenant).State.Should().Be(EntityState.Unchanged);
        (await fixture.Db.Accounts.AsNoTracking().CountAsync()).Should().Be(0);
        (await fixture.Db.AccountSegmentStructures.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OwnedRetry_RejectsModifiedRelationshipBeforeWritesWithoutChangingCallerIntent()
    {
        await using var fixture = await Fixture.CreateAsync(useRetryingStrategy: true);
        var firstAccount = TestAccount(fixture.TenantId, "1100");
        var secondAccount = TestAccount(fixture.TenantId, "1200");
        var structure = new AccountSegmentStructure
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, SegmentCode = "TEST_SEGMENT",
            SegmentName = "Test segment", SegmentPosition = 1, SegmentLength = 4,
            DataType = "Numeric", IsActive = true, LifecycleStatus = AccountSegmentLifecycleStatus.Active
        };
        var segment = new AccountSegmentValue
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Account = firstAccount,
            AccountId = firstAccount.Id, SegmentStructure = structure, SegmentStructureId = structure.Id,
            SegmentPosition = 1, SegmentValue = "1100"
        };
        fixture.Db.AddRange(firstAccount, secondAccount, structure, segment);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var callerSegment = await fixture.Db.AccountSegmentValues
            .Include(item => item.Account).SingleAsync(item => item.Id == segment.Id);
        var callerSecondAccount = await fixture.Db.Accounts.SingleAsync(item => item.Id == secondAccount.Id);
        callerSegment.Account = callerSecondAccount;
        callerSegment.AccountId = secondAccount.Id;

        await fixture.Service().Invoking(item => item.ProvisionAsync(Request(fixture.TenantId)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_ACCOUNT_PROVISIONING_TRACKER_NOT_RETRY_SAFE:*");

        callerSegment.Account.Should().BeSameAs(callerSecondAccount);
        callerSegment.AccountId.Should().Be(secondAccount.Id);
        fixture.Db.Entry(callerSegment).State.Should().Be(EntityState.Modified);
        var durableSegment = await fixture.Db.AccountSegmentValues.AsNoTracking()
            .SingleAsync(item => item.Id == segment.Id);
        durableSegment.AccountId.Should().Be(firstAccount.Id);
        (await fixture.Db.Accounts.AsNoTracking().CountAsync(item => item.AccountCode == "1040")).Should().Be(0);
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
            .And.Contain("System.Transactions.Transaction.Current != null")
            .And.Contain("sys.sp_getapplock")
            .And.Contain("RHEMA:FIN:PROVISION:MANIFEST:")
            .And.Contain("RHEMA:FIN:PROVISION:ACCOUNT:")
            .And.Contain("@LockOwner = 'Transaction'")
            .And.Contain("CaptureRetryableTrackedState")
            .And.Contain("EnsureCallerTrackerHasNoPendingWork")
            .And.Contain("CreateAsyncScope")
            .And.Contain("EnsureEquivalentIsolatedContext");
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

    private static Account TestAccount(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = code, AccountNumber = code,
        AccountName = $"Test {code}", AccountType = AccountType.Asset, CurrencyCode = "GHS",
        Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "transaction.tests"
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

    private sealed class WriteCommandCounter : DbCommandInterceptor
    {
        private static readonly Regex Mutation = new(
            @"\b(?:INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private int _commands;
        public int Commands => Volatile.Read(ref _commands);

        private void Count(DbCommand command)
        {
            if (Mutation.IsMatch(command.CommandText))
                Interlocked.Increment(ref _commands);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class OneShotPostWriteFailureInterceptor : DbCommandInterceptor
    {
        private int _armed;
        private int _remaining = 1;

        public int FailuresInjected { get; private set; }

        public void Arm() => Volatile.Write(ref _armed, 1);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1
                && command.CommandText.Contains("INSERT INTO \"AccountSegmentStructures\"", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _remaining, 0) == 1)
            {
                FailuresInjected++;
                throw new RetryableOfflineException();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ScopedFixture : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _keeper;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private ScopedFixture(
            string connectionString,
            SqliteConnection keeper,
            ServiceProvider provider,
            AsyncServiceScope scope,
            Guid tenantId)
        {
            _connectionString = connectionString;
            _keeper = keeper;
            _provider = provider;
            _scope = scope;
            TenantId = tenantId;
            Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Service = scope.ServiceProvider.GetRequiredService<ErpSystem.Core.Interfaces.Finance.IFinanceAccountProvisioningService>();
        }

        public ApplicationDbContext Db { get; }
        public ErpSystem.Core.Interfaces.Finance.IFinanceAccountProvisioningService Service { get; }
        public Guid TenantId { get; }

        public static async Task<ScopedFixture> CreateAsync(
            bool useRetryingStrategy,
            params IInterceptor[] interceptors)
        {
            var connectionString = $"Data Source=finance-provisioning-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keeper = new SqliteConnection(connectionString);
            await keeper.OpenAsync();
            var setupOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(keeper).Options;
            await using (var setup = new ApplicationDbContext(setupOptions))
            {
                await Fixture.CreateSchemaAsync(setup);
                var tenantId = Guid.NewGuid();
                setup.Tenants.Add(new Tenant
                {
                    Id = tenantId, Code = "TDC", Name = "TDC", BaseCurrency = "GHS", Status = TenantStatus.Active
                });
                await setup.SaveChangesAsync();

                var currentUser = new Mock<ICurrentUserService>();
                currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
                currentUser.SetupGet(item => item.UserName).Returns("transaction.tests");
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton(currentUser.Object);
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite(connectionString);
                    if (interceptors.Length > 0)
                        options.AddInterceptors(interceptors);
                    if (useRetryingStrategy)
                        options.ReplaceService<IExecutionStrategyFactory, OfflineRetryingExecutionStrategyFactory>();
                });
                services.AddFinanceAccountProvisioning();
                var provider = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });
                var scope = provider.CreateAsyncScope();
                return new ScopedFixture(connectionString, keeper, provider, scope, tenantId);
            }
        }

        public ApplicationDbContext CreateVerificationContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connectionString).Options);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _keeper.DisposeAsync();
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
            db.ChangeTracker.Clear();
            return new Fixture(connection, db, tenantId);
        }

        internal static async Task CreateSchemaAsync(ApplicationDbContext db)
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

public sealed class FinanceAccountProvisioningConcurrencySqlServerTests
{
    [SqlServerFact]
    public async Task TwoIndependentContexts_ConvergeOnOneDurableAccountAndManifest()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        await using (var seed = database.Context())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId, Code = "FPRACE", Name = "Finance provisioning race",
                BaseCurrency = "GHS", Status = TenantStatus.Active
            });
            await seed.SaveChangesAsync();
        }

        var firstLock = new HoldFirstProvisioningLockInterceptor();
        var secondMutations = new MutationCountingInterceptor();
        await using var firstContext = database.Context(firstLock);
        await using var secondContext = database.Context(secondMutations);
        var firstService = Service(firstContext, tenantId, "finance.provisioning.race.first");
        var secondService = Service(secondContext, tenantId, "finance.provisioning.race.second");

        var first = Task.Run(() => firstService.ProvisionAsync(Request(tenantId)));
        await firstLock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var second = Task.Run(() => secondService.ProvisionAsync(Request(tenantId)));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        second.IsCompleted.Should().BeFalse("the second independent transaction must wait for the tenant/account application locks");
        firstLock.Release.TrySetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(60));

        results.Select(item => item.AccountId).Distinct().Should().ContainSingle();
        results.Count(item => item.WasCreated).Should().Be(1);
        secondMutations.MutationCommands.Should().Be(0,
            "the converged exact retry must be read-only after acquiring the transaction locks");

        await using var verify = database.Context();
        var account = await verify.Accounts.AsNoTracking().SingleAsync(item =>
            item.TenantId == tenantId && item.AccountCode == "1040");
        account.Id.Should().Be(results[0].AccountId);
        (await verify.AccountSegmentValues.AsNoTracking().CountAsync(item => item.AccountId == account.Id)).Should().Be(2);
        (await verify.AccountAccountingBooks.AsNoTracking().CountAsync(item => item.AccountId == account.Id)).Should().Be(3);
        var structures = await verify.AccountSegmentStructures.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted).Select(item => item.SegmentCode).ToListAsync();
        structures.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        var dimensions = await verify.FinanceDimensionDefinitions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted).Select(item => item.Code).ToListAsync();
        dimensions.Should().HaveCount(6).And.OnlyHaveUniqueItems();
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

    private static FinanceAccountProvisioningService Service(
        ApplicationDbContext db,
        Guid tenantId,
        string userName)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserName).Returns(userName);
        return new FinanceAccountProvisioningService(
            db, currentUser.Object, NullLogger<FinanceAccountProvisioningService>.Instance);
    }

    private sealed class HoldFirstProvisioningLockInterceptor : DbCommandInterceptor
    {
        private int _held;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("sys.sp_getapplock", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref _held, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class MutationCountingInterceptor : DbCommandInterceptor
    {
        private static readonly Regex Mutation = new(
            @"\b(?:INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private int _mutationCommands;
        public int MutationCommands => Volatile.Read(ref _mutationCommands);

        private void Count(DbCommand command)
        {
            if (Mutation.IsMatch(command.CommandText))
                Interlocked.Increment(ref _mutationCommands);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Finance provisioning concurrency gate.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex Safe = new(
            "^RHEMAERP_GL_REHEARSAL_FINPROV_[A-F0-9]{32}$", RegexOptions.CultureInvariant);
        private readonly string _name;
        private readonly string _master;
        private readonly string _target;

        private DisposableDatabase(string name, string master, string target) =>
            (_name, _master, _target) = (name, master, target);

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_GL_REHEARSAL_FINPROV_{Guid.NewGuid():N}".ToUpperInvariant();
            AssertSafe(name);
            var master = new SqlConnectionStringBuilder(configured)
                { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured)
                { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public ApplicationDbContext Context(params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(_target, sql => sql.EnableRetryOnFailure());
            if (interceptors.Length > 0)
                options.AddInterceptors(interceptors);
            return new ApplicationDbContext(options.Options);
        }

        public async ValueTask DisposeAsync()
        {
            AssertSafe(_name);
            await ExecuteMasterAsync(
                $"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
        }

        private async Task ExecuteMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_master);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        private static void AssertSafe(string name)
        {
            if (!Safe.IsMatch(name))
                throw new InvalidOperationException("Unsafe Finance provisioning rehearsal database target.");
        }
    }
}
