using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Finance.Budget;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Opt-in, prefix-safe SQL Server gates for the Finance budgeting release boundary.
/// Set RHEMA_TEST_SQLSERVER to a connection whose login may create and drop disposable databases.
/// </summary>
public sealed class FinanceBudgetSqlServerReleaseGateTests
{
    [SqlServerFact]
    [Trait("Batch", "FinanceBudgetProductionReadiness")]
    [Trait("Category", "SqlServerConcurrency")]
    public async Task TwoConcurrentDocuments_CannotOverspendTheSameBudgetCell()
    {
        await using var database = await DisposableDatabase.CreateAsync("RACE");
        await using (var schema = database.Context())
        {
            await schema.Database.EnsureCreatedAsync();
            await SeedBudgetAsync(schema, database);
        }
        await using (var warmup = database.Context())
        {
            var warmupRequest = BuildRequest(database, Guid.NewGuid(), "sql-budget-warmup", 1m);
            (await CreateService(warmup, database.TenantId).EvaluateAsync(warmupRequest)).IsAllowed
                .Should().BeTrue();
        }

        var first = ReserveAsync(database, Guid.NewGuid(), "sql-budget-race-a", 700m);
        var second = ReserveAsync(database, Guid.NewGuid(), "sql-budget-race-b", 700m);
        var outcomes = await Task.WhenAll(CaptureAsync(first), CaptureAsync(second));

        outcomes.Count(result => result.Success).Should().Be(1);
        var failure = outcomes.Single(result => !result.Success).Error;
        failure.Should().BeOfType<FinanceBudgetCommitmentConflictException>()
            .Which.Code.Should().Be("BUDGET_INSUFFICIENT");
        await using var verification = database.Context();
        (await verification.FinanceBudgetReservations.AsNoTracking()
            .Where(row => row.TenantId == database.TenantId && row.Status == "Reserved")
            .SumAsync(row => row.ReservedAmount)).Should().Be(700m);
    }

    [SqlServerFact]
    [Trait("Batch", "FinanceBudgetProductionReadiness")]
    [Trait("Category", "SqlServerMigration")]
    public async Task DimensionRevisionMigration_UpAndDown_PreserveLegacyRowsAndEnforceExactCellIdentity()
    {
        await using var database = await DisposableDatabase.CreateAsync("MIGRATION");
        await database.ExecuteAsync("""
CREATE TABLE FinanceDimensionSets (Id uniqueidentifier NOT NULL CONSTRAINT PK_FinanceDimensionSets PRIMARY KEY);
CREATE TABLE BudgetRevisions (Id uniqueidentifier NOT NULL CONSTRAINT PK_BudgetRevisions PRIMARY KEY);
CREATE TABLE Accounts (Id uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY);
CREATE TABLE FiscalPeriods (Id uniqueidentifier NOT NULL CONSTRAINT PK_FiscalPeriods PRIMARY KEY);
CREATE TABLE AccountSegmentValues (Id uniqueidentifier NOT NULL CONSTRAINT PK_AccountSegmentValues PRIMARY KEY);
CREATE TABLE BudgetRevisionLines (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_BudgetRevisionLines PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL,
    BudgetRevisionId uniqueidentifier NOT NULL,
    AccountId uniqueidentifier NOT NULL,
    FiscalPeriodId uniqueidentifier NOT NULL,
    SegmentValueId uniqueidentifier NULL,
    CurrentAmount decimal(18,2) NOT NULL,
    AdjustmentAmount decimal(18,2) NOT NULL,
    NewAmount decimal(18,2) NOT NULL,
    IsDeleted bit NOT NULL,
    CONSTRAINT FK_BudgetRevisionLines_BudgetRevisions_BudgetRevisionId FOREIGN KEY(BudgetRevisionId) REFERENCES BudgetRevisions(Id),
    CONSTRAINT FK_BudgetRevisionLines_Accounts_AccountId FOREIGN KEY(AccountId) REFERENCES Accounts(Id),
    CONSTRAINT FK_BudgetRevisionLines_FiscalPeriods_FiscalPeriodId FOREIGN KEY(FiscalPeriodId) REFERENCES FiscalPeriods(Id),
    CONSTRAINT FK_BudgetRevisionLines_AccountSegmentValues_SegmentValueId FOREIGN KEY(SegmentValueId) REFERENCES AccountSegmentValues(Id));
CREATE UNIQUE INDEX IX_BudgetRevisionLines_TenantId_BudgetRevisionId_SegmentValueId_AccountId_FiscalPeriodId
    ON BudgetRevisionLines(TenantId, BudgetRevisionId, SegmentValueId, AccountId, FiscalPeriodId) WHERE IsDeleted=0;
DECLARE @tenant uniqueidentifier=NEWID(), @revision uniqueidentifier=NEWID(), @account uniqueidentifier=NEWID(), @period uniqueidentifier=NEWID(), @line uniqueidentifier=NEWID();
INSERT BudgetRevisions VALUES(@revision); INSERT Accounts VALUES(@account); INSERT FiscalPeriods VALUES(@period);
INSERT BudgetRevisionLines VALUES(@line,@tenant,@revision,@account,@period,NULL,100,25,125,0);
""");

        await database.ApplyMigrationAsync(up: true);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM BudgetRevisionLines WHERE FinanceDimensionSetId IS NULL AND NewAmount=125"))
            .Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'BudgetRevisionLines') AND name=N'UX_BudgetRevisionLines_Cell' AND is_unique=1"))
            .Should().Be(1);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_BudgetRevisionLines_FinanceDimensionSets_FinanceDimensionSetId'"))
            .Should().Be(1);

        await database.ApplyMigrationAsync(up: false);

        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'BudgetRevisionLines') AND name=N'FinanceDimensionSetId'"))
            .Should().Be(0);
        (await database.ScalarAsync<int>("SELECT COUNT(*) FROM BudgetRevisionLines WHERE NewAmount=125"))
            .Should().Be(1);
    }

    private static async Task ReserveAsync(
        DisposableDatabase database,
        Guid sourceDocumentId,
        string key,
        decimal amount)
    {
        await using var context = database.Context();
        await CreateService(context, database.TenantId).ReserveAsync(
            BuildRequest(database, sourceDocumentId, key, amount));
    }

    private static FinanceBudgetCommitmentRequestDto BuildRequest(
        DisposableDatabase database,
        Guid sourceDocumentId,
        string key,
        decimal amount) => new()
        {
            SourceDocumentType = "SqlServerBudgetGate",
            SourceDocumentId = sourceDocumentId,
            SourceDocumentReference = key,
            SourceVersion = "V1",
            BudgetDate = new DateTime(2026, 1, 15),
            IdempotencyKey = key,
            CorrelationId = $"corr-{key}",
            Lines =
            [
                new FinanceBudgetCommitmentLineDto
                {
                    SourceLineId = "L1",
                    BudgetEntryId = database.BudgetEntryId,
                    AccountId = database.AccountId,
                    FiscalPeriodId = database.PeriodId,
                    TransactionAmount = amount,
                    TransactionCurrencyCode = "GHS"
                }
            ]
        };

    private static async Task<(bool Success, Exception? Error)> CaptureAsync(Task task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    private static FinanceBudgetCommitmentService CreateService(ApplicationDbContext context, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName).Returns("sql.budget.gate");
        return new FinanceBudgetCommitmentService(context, currentUser.Object, Mock.Of<IExchangeRateService>());
    }

    private static async Task SeedBudgetAsync(ApplicationDbContext context, DisposableDatabase database)
    {
        var tenantId = database.TenantId;
        var entryId = database.BudgetEntryId;
        var yearId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var periodId = database.PeriodId;
        var accountId = database.AccountId;
        context.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Finance Budget SQL Gate", Code = $"BG-{tenantId:N}"[..12],
            Status = TenantStatus.Active, BaseCurrency = "GHS"
        });
        context.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS",
            CoaType = "Segmented", AccountSeparator = "-"
        });
        context.AccountingBooks.Add(new AccountingBook
        {
            Id = bookId, TenantId = tenantId, Code = "PRIMARY", Name = "Primary",
            BookType = AccountingBookType.PrimaryFull, FunctionalCurrencyCode = "GHS",
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            EffectiveFromUtc = new DateTime(2026, 1, 1),
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
        context.FiscalYears.Add(new FiscalYear
        {
            Id = yearId, TenantId = tenantId, FiscalYearName = "FY2026", FiscalYearCode = "FY2026",
            Year = 2026, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
        });
        context.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = periodId, TenantId = tenantId, FiscalYearId = yearId,
            PeriodName = "January 2026", PeriodCode = "2026-01", PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31), IsOpen = true
        });
        context.Accounts.Add(new Account
        {
            Id = accountId, TenantId = tenantId, AccountCode = "5000", AccountNumber = "5000",
            AccountName = "Controlled expense", AccountType = AccountType.Expense,
            Status = AccountStatus.Active, BudgetTrackingEnabled = true, AllowDirectPosting = true
        });
        context.BudgetScenarios.Add(new BudgetScenario
        {
            Id = scenarioId, TenantId = tenantId, FiscalYearId = yearId, Name = "FY2026 Adopted",
            Status = "Approved", IsActive = true, AdoptedAt = new DateTime(2025, 12, 20),
            AdoptionEffectiveDate = new DateTime(2026, 1, 1), VersionNumber = 1
        });
        context.BudgetReturns.Add(new BudgetReturn
        {
            Id = returnId, TenantId = tenantId, BudgetScenarioId = scenarioId,
            Status = "Approved", ApprovedDate = new DateTime(2025, 12, 20)
        });
        context.BudgetEntries.Add(new BudgetEntry
        {
            Id = entryId, TenantId = tenantId, BudgetReturnId = returnId, AccountId = accountId,
            FiscalPeriodId = periodId, Amount = 1_000m, AmountBase = 1_000m,
            CurrencyCode = "GHS", ExchangeRate = 1m
        });
        await context.SaveChangesAsync();
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run prefix-safe disposable Finance budget gates.";
        }
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private static readonly Regex Safe = new("^RHEMAERP_BUDGET_GATE_[A-Z0-9_]{1,64}$", RegexOptions.CultureInvariant);
        private readonly string _name;
        private readonly string _master;
        private readonly string _target;
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid BudgetEntryId { get; } = Guid.NewGuid();
        public Guid AccountId { get; } = Guid.NewGuid();
        public Guid PeriodId { get; } = Guid.NewGuid();

        private DisposableDatabase(string name, string master, string target) =>
            (_name, _master, _target) = (name, master, target);

        public static async Task<DisposableDatabase> CreateAsync(string suffix)
        {
            var configured = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var name = $"RHEMAERP_BUDGET_GATE_{suffix}_{Guid.NewGuid():N}".ToUpperInvariant();
            AssertSafe(name);
            var master = new SqlConnectionStringBuilder(configured) { InitialCatalog = "master", TrustServerCertificate = true };
            var target = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, TrustServerCertificate = true };
            var database = new DisposableDatabase(name, master.ConnectionString, target.ConnectionString);
            await database.ExecuteMasterAsync($"CREATE DATABASE [{name}]");
            return database;
        }

        public ApplicationDbContext Context() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_target).Options);

        public async Task ApplyMigrationAsync(bool up)
        {
            await using var context = Context();
            var migration = new ExposedMigration();
            var generator = context.GetService<IMigrationsSqlGenerator>();
            var operations = up ? migration.BuildUpOperations() : migration.BuildDownOperations();
            foreach (var command in generator.Generate(operations, context.Model))
                await ExecuteAsync(command.CommandText);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqlConnection(_target);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqlConnection(_target);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            AssertSafe(_name);
            await ExecuteMasterAsync($"IF DB_ID(N'{_name}') IS NOT NULL BEGIN ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]; END");
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
                throw new InvalidOperationException("Refusing unsafe Finance budget gate database target.");
        }
    }

    private sealed class ExposedMigration : AddDimensionAwareBudgetRevisions
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }
}
