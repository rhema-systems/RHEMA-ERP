using System.Reflection;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
    [Trait("Batch", "FinanceDimensionCertification")]
    [Trait("Category", "SqlServerTransaction")]
    public async Task DimensionPromotion_ShouldOwnSerializableTransactionInsideRetryExecutionStrategy()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        await using (var seed = database.CreateContext())
        {
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "SQL Dimension Certification Test",
                Code = $"DC-{tenantId:N}"[..12],
                Status = TenantStatus.Active,
                BaseCurrency = "GHS"
            });
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateRetryingContext();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName).Returns("sql.dimension.governor");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(
                It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });
        var provider = new SqlDimensionReadinessProvider();
        var service = new FinanceDimensionCertificationService(
            context, currentUser.Object, audit.Object, [provider]);

        var assessment = await service.AssessReadinessAsync(
            provider.RouteId, FinanceDimensionCertificationState.Enforced);
        var promoted = await service.PromoteAsync(provider.RouteId, new PromoteFinanceDimensionRouteDto
        {
            ReadinessAssessmentId = assessment.Id,
            TargetState = FinanceDimensionCertificationState.Enforced,
            Reason = "Relational promotion gate completed without blockers."
        });

        promoted.State.Should().Be(FinanceDimensionCertificationState.Enforced);
        await using var verification = database.CreateContext();
        (await verification.FinanceDimensionRouteCertifications.AsNoTracking()
                .SingleAsync(item => item.TenantId == tenantId && item.RouteId == provider.RouteId))
            .State.Should().Be(FinanceDimensionCertificationState.Enforced);
        (await verification.FinanceDimensionReadinessAssessments.AsNoTracking()
                .SingleAsync(item => item.Id == assessment.Id))
            .ConsumedAt.Should().NotBeNull();
    }

    [SqlServerFact]
    [Trait("Batch", "FinanceDimensionCertification")]
    [Trait("Category", "SqlServerMigration")]
    public async Task DimensionCertificationPermissionMigrationSql_ShouldBeIdempotentAndGrantOnlyGovernanceRoles()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        await using var context = database.CreateContext();
        context.Roles.Add(new ApplicationRole("Financial Controller")
        {
            Id = Guid.NewGuid(),
            NormalizedName = "FINANCIAL CONTROLLER",
            IsSystemRole = true,
            CreatedBy = "sql-permission-gate"
        });
        await context.SaveChangesAsync();

        var migration = new AddFinanceDimensionSourceInfrastructure();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var permissionSql = builder.Operations.OfType<SqlOperation>().Single(operation =>
            operation.Sql.Contains("Finance.Dimensions.Certification.Manage", StringComparison.Ordinal));

        await context.Database.ExecuteSqlRawAsync(permissionSql.Sql);
        await context.Database.ExecuteSqlRawAsync(permissionSql.Sql);

        var permission = await context.Permissions.AsNoTracking().SingleAsync(item =>
            item.Name == FinancePermissions.ManageDimensionCertification && !item.IsDeleted);
        var grantedRoles = await context.RolePermissions.AsNoTracking()
            .Where(item => item.PermissionId == permission.Id)
            .Select(item => item.Role.Name)
            .OrderBy(name => name)
            .ToListAsync();
        grantedRoles.Should().Equal("Financial Controller", "SuperAdmin", "TenantAdmin");
        (await context.RolePermissions.CountAsync(item => item.PermissionId == permission.Id))
            .Should().Be(3, "running the permission upgrade twice must not duplicate grants");
    }

    [SqlServerFact]
    [Trait("Batch", "FinancePerformance")]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CurrentSqlServerModel_ShouldExposeTheMeasuredLedgerAccessIndexInKeyOrder()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT STRING_AGG(column_definition.name, ',')
                WITHIN GROUP (ORDER BY index_column.key_ordinal)
            FROM sys.indexes AS index_definition
            INNER JOIN sys.tables AS table_definition
                ON table_definition.object_id = index_definition.object_id
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = index_definition.object_id
                AND index_column.index_id = index_definition.index_id
            INNER JOIN sys.columns AS column_definition
                ON column_definition.object_id = index_column.object_id
                AND column_definition.column_id = index_column.column_id
            WHERE table_definition.name = N'AccountTransactions'
                AND index_definition.name = N'IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId'
                AND index_column.key_ordinal > 0;
            """;

        // Key order matters: changing it can leave the index present while making the tenant/book
        // prefix unusable for the trial-balance and journal-inquiry query shapes it was designed for.
        Convert.ToString(await command.ExecuteScalarAsync())
            .Should().Be("TenantId,BookClassification,TransactionDate,AccountId");
    }

    [SqlServerFact]
    [Trait("Batch", "FinanceSchema")]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CurrentSqlServerSchema_ShouldExposeEveryLineScopedDeductionEvidenceColumn()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.columns AS column_definition
            INNER JOIN sys.tables AS table_definition
                ON table_definition.object_id = column_definition.object_id
            WHERE
                (table_definition.name = N'VendorPaymentAllocation'
                    AND column_definition.name IN
                        (N'DiscountFunctionalAmount', N'WithholdingTaxFunctionalAmount'))
                OR
                (table_definition.name = N'PaymentAllocation'
                    AND column_definition.name IN
                        (N'DiscountFunctionalAmount', N'WithholdingTaxAmount',
                         N'WithholdingTaxFunctionalAmount', N'VatWithholdingAmount',
                         N'VatWithholdingFunctionalAmount'));
            """;

        // This SQL Server check complements the provider-neutral migration-operation test. It
        // guards the relational names consumed by AP/AR integrations after the model is created.
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(7);
    }

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
    [Trait("Batch", "FinancePostingEngine")]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task ConcurrentSameBookSubmissions_ShouldConvergeOnOnePostingEventAndJournal()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var seeded = await database.SeedPostingAccountsAsync();
        await using var firstContext = database.CreateRetryingContext();
        await using var secondContext = database.CreateRetryingContext();
        var sourceId = Guid.NewGuid();

        FinancePostingRequestV2Dto Request() => new()
        {
            SourceModule = "TEST", SourceDocumentType = "C1ConcurrentPosting",
            SourceDocumentId = sourceId, SourceDocumentTenantId = seeded.TenantId,
            PostingAction = "Post", Description = "Concurrent C1 posting",
            PostingDate = new DateTime(2026, 7, 15), JournalType = "System Generated",
            AccountingBookCode = "IFRS", FunctionalCurrencyCode = "GHS",
            IdempotencyKey = $"C1|CONCURRENT|{sourceId:N}|IFRS|POST", ReturnExistingOnDuplicate = true,
            Lines =
            [
                new FinancePostingLineDto { AccountId = seeded.DebitAccountId, DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = seeded.CreditAccountId, CreditAmount = 100m }
            ]
        };

        var results = await Task.WhenAll(
            CreateSqlPostingEngine(firstContext, seeded.TenantId).PostAsync(Request()),
            CreateSqlPostingEngine(secondContext, seeded.TenantId).PostAsync(Request()));

        results.Select(result => result.PostingEventId).Distinct().Should().ContainSingle();
        results.Select(result => result.JournalEntryId).Distinct().Should().ContainSingle();
        results.Count(result => result.WasDuplicate).Should().Be(1);
        await using var verification = database.CreateContext();
        (await verification.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await verification.JournalEntries.CountAsync()).Should().Be(1);
        (await verification.AccountBalances.CountAsync()).Should().Be(2);
        (await verification.AccountBalances.SingleAsync(item => item.AccountId == seeded.DebitAccountId))
            .ClosingBalance.Should().Be(100m);
        (await verification.AccountBalances.SingleAsync(item => item.AccountId == seeded.CreditAccountId))
            .ClosingBalance.Should().Be(-100m);
    }

    [SqlServerFact]
    [Trait("Batch", "FinancePostingEngine")]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task ConcurrentCaseVariantDifferentBookSubmissions_ShouldMoveGenericBalancesExactlyOnce()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var seeded = await database.SeedPostingAccountsAsync(includeParallelBook: true);
        await using var firstContext = database.CreateRetryingContext();
        await using var secondContext = database.CreateRetryingContext();
        var sourceId = Guid.NewGuid();

        FinancePostingRequestV2Dto Request(string book, string sourceType, string action, string key) => new()
        {
            SourceModule = "TEST", SourceDocumentType = sourceType,
            SourceDocumentId = sourceId, SourceDocumentTenantId = seeded.TenantId,
            PostingAction = action, Description = "Concurrent cross-book C1 posting",
            PostingDate = new DateTime(2026, 7, 15), JournalType = "System Generated",
            AccountingBookCode = book, FunctionalCurrencyCode = "GHS",
            IdempotencyKey = key, ReturnExistingOnDuplicate = true,
            Lines =
            [
                new FinancePostingLineDto { AccountId = seeded.DebitAccountId, DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = seeded.CreditAccountId, CreditAmount = 100m }
            ]
        };

        static async Task<(FinancePostingResultDto? Result, Exception? Error)> CaptureAsync(
            Task<FinancePostingResultDto> action)
        {
            try { return (await action, null); }
            catch (Exception exception) { return (null, exception); }
        }

        var outcomes = await Task.WhenAll(
            CaptureAsync(CreateSqlPostingEngine(firstContext, seeded.TenantId).PostAsync(
                Request("IFRS", "C1CaseSource", "Post", $"C1|CASE|{sourceId:N}"))),
            CaptureAsync(CreateSqlPostingEngine(secondContext, seeded.TenantId).PostAsync(
                Request("local_statutory", "c1casesource", "post", $"c1|case|{sourceId:N}"))));

        outcomes.Count(item => item.Result is not null).Should().Be(1);
        outcomes.Count(item => item.Error?.Message.Contains("PARALLEL_BOOK_POSTING_DISABLED", StringComparison.Ordinal) == true)
            .Should().Be(1);
        await using var verification = database.CreateContext();
        (await verification.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await verification.JournalEntries.CountAsync()).Should().Be(1);
        (await verification.Accounts.SingleAsync(item => item.Id == seeded.DebitAccountId)).Balance.Should().Be(100m);
        (await verification.Accounts.SingleAsync(item => item.Id == seeded.CreditAccountId)).Balance.Should().Be(-100m);
        (await verification.AccountBalances.CountAsync()).Should().Be(2);
        (await verification.AccountBalances.SingleAsync(item => item.AccountId == seeded.DebitAccountId))
            .ClosingBalance.Should().Be(100m);
    }

    [SqlServerFact]
    [Trait("Batch", "FinancePostingEngine")]
    [Trait("Category", "MultiBookBalanceC2")]
    public async Task ConcurrentPostingAndApprovedRebuild_ShouldSerializeWithoutLostOrDuplicateProjection()
    {
        await using var database = await SqlServerJournalBatchDatabase.CreateAsync();
        var seeded = await database.SeedPostingAccountsAsync();
        await using var postingContext = database.CreateContext();
        await using var rebuildContext = database.CreateContext();
        var sourceId = Guid.NewGuid();
        var request = new FinancePostingRequestV2Dto
        {
            SourceModule = "TEST", SourceDocumentType = "C2ConcurrentRebuild",
            SourceDocumentId = sourceId, SourceDocumentTenantId = seeded.TenantId,
            PostingAction = "Post", Description = "Concurrent C2 posting/rebuild",
            PostingDate = new DateTime(2026, 7, 15), JournalType = "System Generated",
            AccountingBookCode = "IFRS", FunctionalCurrencyCode = "GHS",
            IdempotencyKey = $"C2|CONCURRENT-REBUILD|{sourceId:N}|IFRS|POST", ReturnExistingOnDuplicate = true,
            Lines =
            [
                new FinancePostingLineDto { AccountId = seeded.DebitAccountId, DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = seeded.CreditAccountId, CreditAmount = 100m }
            ]
        };
        var rebuild = new BookBalanceReconciliationRequestDto(
            "IFRS", true, "Concurrent C2 reconciliation", $"C2|REBUILD|{sourceId:N}", Guid.NewGuid());

        await Task.WhenAll(
            CreateSqlPostingEngine(postingContext, seeded.TenantId).PostAsync(request),
            new BookBalanceReadModelService(rebuildContext).ReconcileAsync(seeded.TenantId, rebuild, Guid.NewGuid()));

        await using var verification = database.CreateContext();
        (await verification.JournalEntries.CountAsync()).Should().Be(1);
        (await verification.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await verification.AccountBalances.CountAsync()).Should().Be(2);
        (await verification.AccountBalances.SingleAsync(item => item.AccountId == seeded.DebitAccountId))
            .ClosingBalance.Should().Be(100m);
        (await verification.AccountBalances.SingleAsync(item => item.AccountId == seeded.CreditAccountId))
            .ClosingBalance.Should().Be(-100m);
        (await verification.FinanceBalanceRebuildRuns.CountAsync()).Should().Be(1);
    }

    private static FinancePostingEngine CreateSqlPostingEngine(ApplicationDbContext context, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName).Returns("sql.c1.poster");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("sql-c1-release-gate");
        return new FinancePostingEngine(context, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>());
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

        public ApplicationDbContext CreateRetryingContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure())
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
            var bookId = Guid.NewGuid();
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
            context.AccountingBooks.Add(new AccountingBook
            {
                Id = bookId, TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
                IsDefault = true, IsActive = true, AllowsPosting = true
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
                AccountingBookId = bookId,
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

        public async Task<SeededPostingAccounts> SeedPostingAccountsAsync(bool includeParallelBook = false)
        {
            var tenantId = Guid.NewGuid();
            var fiscalYearId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var debitAccountId = Guid.NewGuid();
            var creditAccountId = Guid.NewGuid();
            var bookId = Guid.NewGuid();
            var parallelBookId = Guid.NewGuid();

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
            context.AccountingBooks.Add(new AccountingBook
            {
                Id = bookId, TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
                IsDefault = true, IsActive = true, AllowsPosting = true
            });
            if (includeParallelBook)
            {
                context.AccountingBooks.Add(new AccountingBook
                {
                    Id = parallelBookId, TenantId = tenantId, Code = "LOCAL_STATUTORY", Name = "Local Statutory",
                    IsDefault = false, IsActive = true, AllowsPosting = true
                });
            }
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
            context.AccountAccountingBooks.AddRange(
                new AccountAccountingBook
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = debitAccountId,
                    AccountingBookId = bookId, IsEnabled = true
                },
                new AccountAccountingBook
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = creditAccountId,
                    AccountingBookId = bookId, IsEnabled = true
                });
            if (includeParallelBook)
            {
                context.AccountAccountingBooks.AddRange(
                    new AccountAccountingBook
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, AccountId = debitAccountId,
                        AccountingBookId = parallelBookId, IsEnabled = true
                    },
                    new AccountAccountingBook
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, AccountId = creditAccountId,
                        AccountingBookId = parallelBookId, IsEnabled = true
                    });
            }
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

    private sealed class SqlDimensionReadinessProvider : IFinanceDimensionReadinessProvider
    {
        public FinanceDimensionRouteId RouteId => FinanceDimensionRouteId.FinanceApVendorInvoice;

        public Task<FinanceDimensionReadinessContribution> EvaluateAsync(
            Guid tenantId,
            FinanceDimensionRouteDefinition route,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FinanceDimensionReadinessContribution("sql-ready:v1", []));
    }
}
