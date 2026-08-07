using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Opt-in release gates that exercise SQL Server's real transaction, rowversion,
/// and filtered unique-index behavior. Set RHEMA_TEST_SQLSERVER to a SQL Server
/// connection string whose login may create and drop disposable databases.
/// </summary>
public sealed class JournalBatchSqlServerReleaseGateTests
{
    [SqlServerFact]
    [Trait("Batch", "FinancePostingEngine")]
    [Trait("Category", "SqlServerIntegration")]
    public async Task PostingWithAlreadyTrackedAccounts_ShouldSynchronizeBalancesWithoutMutatingTrackerEnumeration()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var seeded = await database.SeedPostingAccountsAsync();

        await using var context = database.CreateContext();

        // Consumer modules often load the configured control accounts while preparing their own
        // transaction before handing the posting request to Finance. Keep both accounts tracked here
        // to reproduce that integration shape against SQL Server's raw balance-update path.
        var trackedAccounts = await context.Accounts
            .Where(account => account.TenantId == seeded.TenantId &&
                              (account.Id == seeded.DebitAccountId || account.Id == seeded.CreditAccountId))
            .ToListAsync();
        trackedAccounts.Should().HaveCount(2);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(seeded.TenantId);
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName).Returns("sql.finance.integration");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("sql-server-release-gate");

        var engine = new FinancePostingEngine(
            context,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>());
        var request = new FinancePostingRequestDto
        {
            SourceModule = "PROC",
            OriginModuleCode = "PROC",
            SourceDocumentType = "SupplierPaymentReconciliation",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = seeded.TenantId,
            PostingAction = "Post",
            SourceDocumentReference = "SQL-TRACKED-ACCOUNT-001",
            Description = "SQL Server tracked-account posting regression",
            PostingDate = new DateTime(2026, 7, 15),
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines =
            [
                new FinancePostingLineDto
                {
                    AccountId = seeded.DebitAccountId,
                    Description = "Supplier control",
                    DebitAmount = 100m
                },
                new FinancePostingLineDto
                {
                    AccountId = seeded.CreditAccountId,
                    Description = "Bank",
                    CreditAmount = 100m
                }
            ]
        };

        var result = await engine.PostAsync(request);

        result.PostingStatus.Should().Be("Posted");
        trackedAccounts.Single(account => account.Id == seeded.DebitAccountId).Balance.Should().Be(-100m);
        trackedAccounts.Single(account => account.Id == seeded.CreditAccountId).Balance.Should().Be(-100m);

        // Verify the durable SQL values as well as the in-memory snapshot. This guards against fixing
        // the enumerator by detaching entities while accidentally losing or double-applying balances.
        await using var verification = database.CreateContext();
        (await verification.Accounts.AsNoTracking()
                .SingleAsync(account => account.Id == seeded.DebitAccountId))
            .Balance.Should().Be(-100m);
        (await verification.Accounts.AsNoTracking()
                .SingleAsync(account => account.Id == seeded.CreditAccountId))
            .Balance.Should().Be(-100m);
        (await verification.FinancePostingEvents.AsNoTracking()
                .CountAsync(postingEvent => postingEvent.SourceDocumentId == request.SourceDocumentId))
            .Should().Be(1);
    }

    [SqlServerFact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "SqlServerTransaction")]
    public async Task ReversalConstructionRollback_ShouldLeaveSourceAndDatabaseUnchanged()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var seeded = await database.SeedSourceBatchAsync();
        var reversalId = Guid.NewGuid();

        await using (var context = database.CreateContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var source = await context.JournalBatches.SingleAsync(batch => batch.Id == seeded.BatchId);
            source.ReversalStatus = JournalBatchReversalStatus.ReversalPending;
            context.JournalBatches.Add(new JournalBatch
            {
                Id = reversalId,
                TenantId = seeded.TenantId,
                BatchNumber = "JB-SQL-REV-00001",
                Description = "Disposable reversal",
                FiscalPeriodId = seeded.PeriodId,
                BookClassification = "IFRS",
                ControlCurrencyCode = "GHS",
                ExpectedDebitTotal = 100m,
                ExpectedJournalCount = 1,
                BatchType = JournalBatchType.Reversal,
                ReversalOfJournalBatchId = source.Id
            });
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verification = database.CreateContext();
        (await verification.JournalBatches.AsNoTracking()
                .SingleAsync(batch => batch.Id == seeded.BatchId))
            .ReversalStatus.Should().Be(JournalBatchReversalStatus.NotReversed);
        (await verification.JournalBatches.AsNoTracking()
                .AnyAsync(batch => batch.Id == reversalId))
            .Should().BeFalse();
    }

    [SqlServerFact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "SqlServerConcurrency")]
    public async Task ConcurrentIdempotencyAndReversalClaims_ShouldHaveSingleWinners()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var seeded = await database.SeedSourceBatchAsync();

        var postingOutcomes = await Task.WhenAll(
            TryInsertPostingRunAsync(database, seeded, Guid.NewGuid()),
            TryInsertPostingRunAsync(database, seeded, Guid.NewGuid()));
        postingOutcomes.Count(outcome => outcome).Should().Be(1);

        var claimRunIds = await CreateClaimRunsAsync(database, seeded);
        var claimOutcomes = await Task.WhenAll(
            TryClaimPostingItemAsync(database, seeded, claimRunIds[0]),
            TryClaimPostingItemAsync(database, seeded, claimRunIds[1]));
        claimOutcomes.Count(outcome => outcome).Should().Be(1);

        var reversalOutcomes = await Task.WhenAll(
            TryInsertReversalAsync(database, seeded, "JB-SQL-REV-A"),
            TryInsertReversalAsync(database, seeded, "JB-SQL-REV-B"));
        reversalOutcomes.Count(outcome => outcome).Should().Be(1);

        await using var verification = database.CreateContext();
        (await verification.JournalBatchPostingRuns.CountAsync(run =>
                run.JournalBatchId == seeded.BatchId &&
                run.IdempotencyKey == "sql-concurrent-post"))
            .Should().Be(1);
        (await verification.JournalBatches.CountAsync(batch =>
                batch.ReversalOfJournalBatchId == seeded.BatchId))
            .Should().Be(1);
        var claimedItem = await verification.JournalBatchItems.SingleAsync(item => item.Id == seeded.ItemId);
        claimedItem.PostingStatus.Should().Be(JournalBatchItemPostingStatus.Posting);
        claimRunIds.Should().Contain(claimedItem.PostingClaimRunId!.Value);
    }

    private static async Task<bool> TryInsertPostingRunAsync(
        SqlServerJournalBatchDatabase database,
        SeededBatch seeded,
        Guid userId)
    {
        await using var context = database.CreateContext();
        context.JournalBatchPostingRuns.Add(new JournalBatchPostingRun
        {
            TenantId = seeded.TenantId,
            JournalBatchId = seeded.BatchId,
            RunNumber = Random.Shared.Next(1, int.MaxValue),
            IdempotencyKey = "sql-concurrent-post",
            Status = JournalBatchPostingRunStatus.Pending,
            RequestedByUserId = userId,
            RequestedAt = DateTime.UtcNow,
            SelectedDebitTotal = 100m,
            SelectedEntryCount = 1
        });
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private static async Task<Guid[]> CreateClaimRunsAsync(
        SqlServerJournalBatchDatabase database,
        SeededBatch seeded)
    {
        var runIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await using var context = database.CreateContext();
        context.JournalBatchPostingRuns.AddRange(
            new JournalBatchPostingRun
            {
                Id = runIds[0],
                TenantId = seeded.TenantId,
                JournalBatchId = seeded.BatchId,
                RunNumber = 1001,
                IdempotencyKey = $"claim-{runIds[0]:N}",
                RequestedByUserId = Guid.NewGuid(),
                RequestedAt = DateTime.UtcNow,
                SelectedDebitTotal = 100m,
                SelectedEntryCount = 1
            },
            new JournalBatchPostingRun
            {
                Id = runIds[1],
                TenantId = seeded.TenantId,
                JournalBatchId = seeded.BatchId,
                RunNumber = 1002,
                IdempotencyKey = $"claim-{runIds[1]:N}",
                RequestedByUserId = Guid.NewGuid(),
                RequestedAt = DateTime.UtcNow,
                SelectedDebitTotal = 100m,
                SelectedEntryCount = 1
            });
        await context.SaveChangesAsync();
        return runIds;
    }

    private static async Task<bool> TryClaimPostingItemAsync(
        SqlServerJournalBatchDatabase database,
        SeededBatch seeded,
        Guid runId)
    {
        await using var context = database.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var affected = await context.JournalBatchItems
            .Where(item =>
                item.TenantId == seeded.TenantId &&
                item.Id == seeded.ItemId &&
                item.ReviewStatus == JournalBatchItemReviewStatus.Approved &&
                item.PostingStatus == JournalBatchItemPostingStatus.Ready &&
                item.PostingClaimRunId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PostingStatus, JournalBatchItemPostingStatus.Posting)
                .SetProperty(item => item.PostingClaimRunId, runId)
                .SetProperty(item => item.PostingClaimedAt, DateTime.UtcNow));
        await transaction.CommitAsync();
        return affected == 1;
    }

    private static async Task<bool> TryInsertReversalAsync(
        SqlServerJournalBatchDatabase database,
        SeededBatch seeded,
        string batchNumber)
    {
        await using var context = database.CreateContext();
        context.JournalBatches.Add(new JournalBatch
        {
            TenantId = seeded.TenantId,
            BatchNumber = batchNumber,
            Description = "Concurrent reversal claim",
            FiscalPeriodId = seeded.PeriodId,
            BookClassification = "IFRS",
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 100m,
            ExpectedJournalCount = 1,
            BatchType = JournalBatchType.Reversal,
            ReversalOfJournalBatchId = seeded.BatchId
        });
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
            {
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable SQL Server journal-batch release gates.";
            }
        }
    }

    private sealed class SqlServerJournalBatchDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private SqlServerJournalBatchDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static async Task<SqlServerJournalBatchDatabase> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var builder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = $"RhemaERP_JournalBatch_{Guid.NewGuid():N}",
                TrustServerCertificate = true
            };
            var database = new SqlServerJournalBatchDatabase(builder.ConnectionString);
            await using var context = database.CreateContext();
            try
            {
                await context.Database.EnsureCreatedAsync();
                return database;
            }
            catch
            {
                await context.Database.EnsureDeletedAsync();
                throw;
            }
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(_connectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task<SeededBatch> SeedSourceBatchAsync()
        {
            var tenantId = Guid.NewGuid();
            var fiscalYearId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            await using var context = CreateContext();
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "SQL Journal Batch Test",
                Code = $"SQL-{tenantId:N}"[..12],
                BaseCurrency = "GHS"
            });
            context.FiscalYears.Add(new FiscalYear
            {
                Id = fiscalYearId,
                TenantId = tenantId,
                FiscalYearName = "Fiscal Year 2026",
                FiscalYearCode = $"FY-{tenantId:N}"[..12],
                Year = 2026,
                FiscalYearType = "Calendar",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalDays = 365,
                NumberOfPeriods = 12,
                Status = "Open"
            });
            context.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = periodId,
                TenantId = tenantId,
                FiscalYearId = fiscalYearId,
                PeriodName = "July 2026",
                PeriodCode = $"P-{tenantId:N}"[..12],
                PeriodNumber = 7,
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 31),
                PeriodDays = 31,
                PeriodStatus = "Open",
                IsOpen = true
            });
            var journal = new JournalEntry
            {
                Id = journalId,
                TenantId = tenantId,
                JournalEntryNumber = $"JE-{tenantId:N}"[..20],
                JournalType = "General",
                EntryDate = new DateTime(2026, 7, 15),
                Description = "Claimable SQL journal",
                TotalDebitAmount = 100m,
                TotalCreditAmount = 100m,
                IsBalanced = true,
                BookClassification = "IFRS",
                FiscalPeriodId = periodId,
                PostingStatus = "Approved",
                ApprovalStatus = "Approved"
            };
            context.JournalBatches.Add(new JournalBatch
            {
                Id = batchId,
                TenantId = tenantId,
                BatchNumber = $"JB-{tenantId:N}"[..20],
                Description = "Posted source batch",
                FiscalPeriodId = periodId,
                BookClassification = "IFRS",
                ControlCurrencyCode = "GHS",
                ExpectedDebitTotal = 100m,
                ExpectedJournalCount = 1,
                ApprovalStatus = JournalBatchApprovalStatus.Approved,
                PostingStatus = JournalBatchPostingStatus.Posted,
                ReversalStatus = JournalBatchReversalStatus.NotReversed,
                Items =
                [
                    new JournalBatchItem
                    {
                        Id = itemId,
                        TenantId = tenantId,
                        JournalEntryId = journalId,
                        JournalEntry = journal,
                        SequenceNumber = 1,
                        ReviewStatus = JournalBatchItemReviewStatus.Approved,
                        PostingStatus = JournalBatchItemPostingStatus.Ready
                    }
                ]
            });
            await context.SaveChangesAsync();
            return new SeededBatch(tenantId, periodId, batchId, itemId);
        }

        public async Task<SeededPostingAccounts> SeedPostingAccountsAsync()
        {
            var tenantId = Guid.NewGuid();
            var fiscalYearId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var debitAccountId = Guid.NewGuid();
            var creditAccountId = Guid.NewGuid();

            await using var context = CreateContext();
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "SQL Finance Posting Test",
                Code = $"FP-{tenantId:N}"[..12],
                Status = TenantStatus.Active,
                BaseCurrency = "GHS"
            });
            context.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BaseCurrency = "GHS",
                CoaType = "Segmented",
                AccountSeparator = "-"
            });
            context.FiscalYears.Add(new FiscalYear
            {
                Id = fiscalYearId,
                TenantId = tenantId,
                FiscalYearName = "Fiscal Year 2026",
                FiscalYearCode = $"FY-{tenantId:N}"[..12],
                Year = 2026,
                FiscalYearType = "Calendar",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalDays = 365,
                NumberOfPeriods = 12,
                Status = "Open"
            });
            context.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = periodId,
                TenantId = tenantId,
                FiscalYearId = fiscalYearId,
                PeriodName = "July 2026",
                PeriodCode = $"P-{tenantId:N}"[..12],
                PeriodNumber = 7,
                PeriodType = PeriodType.Monthly,
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 31),
                PeriodDays = 31,
                PeriodStatus = "Open",
                IsOpen = true
            });
            context.Accounts.AddRange(
                new Account
                {
                    Id = debitAccountId,
                    TenantId = tenantId,
                    AccountCode = "2100",
                    AccountNumber = "2100",
                    AccountName = "Supplier Control",
                    AccountType = AccountType.Liability,
                    Status = AccountStatus.Active,
                    CurrencyCode = "GHS",
                    AllowDirectPosting = true
                },
                new Account
                {
                    Id = creditAccountId,
                    TenantId = tenantId,
                    AccountCode = "1100",
                    AccountNumber = "1100",
                    AccountName = "Operating Bank",
                    AccountType = AccountType.Asset,
                    Status = AccountStatus.Active,
                    CurrencyCode = "GHS",
                    AllowDirectPosting = true
                });
            await context.SaveChangesAsync();

            return new SeededPostingAccounts(tenantId, debitAccountId, creditAccountId);
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }

    private sealed record SeededBatch(Guid TenantId, Guid PeriodId, Guid BatchId, Guid ItemId);
    private sealed record SeededPostingAccounts(Guid TenantId, Guid DebitAccountId, Guid CreditAccountId);
}
