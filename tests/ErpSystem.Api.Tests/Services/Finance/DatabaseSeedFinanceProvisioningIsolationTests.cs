using ErpSystem.Api.Extensions;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class DatabaseSeedFinanceProvisioningIsolationTests
{
    [Fact]
    public async Task FinanceThenSupplierSeedOrder_Twice_PreservesUnchangedGraphAndConvergesFinanceManifests()
    {
        var connectionString = $"Data Source=database-seed-finance-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        await using (var setup = new ApplicationDbContext(
                         new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(keeper).Options))
        {
            var createScript = setup.Database.GenerateCreateScript()
                .Replace("nvarchar(max)", "TEXT", StringComparison.OrdinalIgnoreCase)
                .Replace("N'", "'", StringComparison.Ordinal)
                .Replace("LEN(", "LENGTH(", StringComparison.OrdinalIgnoreCase)
                .Replace("ISJSON(", "json_valid(", StringComparison.OrdinalIgnoreCase)
                .Replace("ISNULL(", "ifnull(", StringComparison.OrdinalIgnoreCase)
                .Replace("DATEADD(day, 2555, [SourceOccurredAtUtc])",
                    "datetime([SourceOccurredAtUtc], '+2555 days')", StringComparison.OrdinalIgnoreCase)
                .Replace("DATEADD(day, 90, [NoticeDate])",
                    "datetime([NoticeDate], '+90 days')", StringComparison.OrdinalIgnoreCase)
                .Replace("CONVERT(date, [DueDate])", "date([DueDate])", StringComparison.OrdinalIgnoreCase)
                .Replace("CONVERT(date, [CreatedAt])", "date([CreatedAt])", StringComparison.OrdinalIgnoreCase)
                .Replace("DATEDIFF(day, [WeekStart], [WeekEnd])",
                    "CAST(julianday([WeekEnd]) - julianday([WeekStart]) AS INTEGER)", StringComparison.OrdinalIgnoreCase)
                .Replace("DATEDIFF(day, CONVERT(date, '19000101', 112), CONVERT(date, [WeekStart]))",
                    "CAST(julianday(date([WeekStart])) - julianday('1900-01-01') AS INTEGER)", StringComparison.OrdinalIgnoreCase)
                .Replace("DAY([IndexPeriod])", "CAST(strftime('%d', [IndexPeriod]) AS INTEGER)", StringComparison.OrdinalIgnoreCase)
                .Replace("DAY([BaseIndexPeriod])", "CAST(strftime('%d', [BaseIndexPeriod]) AS INTEGER)", StringComparison.OrdinalIgnoreCase)
                .Replace("DAY([CurrentIndexPeriod])", "CAST(strftime('%d', [CurrentIndexPeriod]) AS INTEGER)", StringComparison.OrdinalIgnoreCase)
                .Replace("\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal);
            createScript = Regex.Replace(
                createScript,
                "(?m)^(\\s*)CONSTRAINT (\"[^\"]+\") CHECK \\(.*\\)(,?)\\r?$",
                "$1CONSTRAINT $2 CHECK (1 = 1)$3",
                RegexOptions.CultureInvariant);
            await setup.Database.ExecuteSqlRawAsync(createScript);
            await AddSupplierPrerequisitesAsync(setup, tenantId);
        }

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserName).Returns("database-seed-order.tests");
        var authorityWrites = new FinanceAuthorityWriteCounter();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(currentUser.Object);
        services.AddDbContext<ApplicationDbContext>(options => options
            .UseSqlite(connectionString)
            .AddInterceptors(authorityWrites));
        services.AddFinanceAccountProvisioning();
        services.AddScoped<ProcurementSupplierOnboardingTestSeeder>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        async Task<string> RunProductionFinanceSupplierPassAsync(bool assertMaterializedGraph)
        {
            await using var passScope = provider.CreateAsyncScope();
            var passDb = passScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await new FinanceDataSeeder(passDb, NullLogger<FinanceDataSeeder>.Instance).SeedAsync();
            var preSupplierInventory = Inventory(passDb.ChangeTracker.Entries());
            preSupplierInventory.Pending.Should().Be(0);
            if (assertMaterializedGraph)
            {
                preSupplierInventory.Unchanged.Should().BeGreaterThan(50,
                    "the real Finance seed pass intentionally retains a substantial unchanged graph in its scoped context");
                preSupplierInventory.MaterializedRelationships.Should().BeGreaterThan(0);
            }

            var supplierSeeder = passScope.ServiceProvider.GetRequiredService<ProcurementSupplierOnboardingTestSeeder>();
            await supplierSeeder.SeedAsync();
            passDb.ChangeTracker.DetectChanges();
            passDb.ChangeTracker.Entries().Should().OnlyContain(entry => entry.State == EntityState.Unchanged);
            (await GetGlf003ViolationCountAsync(passDb)).Should().Be(0,
                "seed-created applicability must never be enabled before its book is active and allows posting");
            return await FinanceAuthorityStateAsync(passDb, tenantId);
        }

        var passOneState = await RunProductionFinanceSupplierPassAsync(assertMaterializedGraph: true);
        var passTwoState = await RunProductionFinanceSupplierPassAsync(assertMaterializedGraph: false);
        passTwoState.Should().Be(passOneState, "the exact second production-order pass must converge byte-for-byte");

        await using (var exactRetryScope = provider.CreateAsyncScope())
        {
            var retryDb = exactRetryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            authorityWrites.Arm();
            await new FinanceClassificationManifestSeeder(retryDb, NullLogger.Instance)
                .SeedAsync(tenantId, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            await exactRetryScope.ServiceProvider.GetRequiredService<ProcurementSupplierOnboardingTestSeeder>()
                .SeedAsync();
            (await GetGlf003ViolationCountAsync(retryDb)).Should().Be(0);
        }
        authorityWrites.Commands.Should().Be(0,
            "the exact manifest/provisioning retry must not write Finance books, classifications, accounts, segments, or mappings; observed commands: {0}",
            string.Join("\n---\n", authorityWrites.CommandTexts));

        await using var verify = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options);
        var accounts = await verify.Accounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && new[] { "1040", "4930", "2210" }.Contains(item.AccountCode))
            .ToListAsync();
        accounts.Should().HaveCount(3);
        accounts.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        (await verify.AccountSegmentStructures.CountAsync(item => item.TenantId == tenantId && !item.IsDeleted))
            .Should().Be(2);
        (await verify.FinanceDimensionDefinitions.CountAsync(item => item.TenantId == tenantId && !item.IsDeleted))
            .Should().Be(6);
        (await verify.AccountSegmentValues.CountAsync(item =>
            item.TenantId == tenantId && accounts.Select(account => account.Id).Contains(item.AccountId)))
            .Should().Be(6);
        (await verify.AccountAccountingBooks.CountAsync(item =>
            item.TenantId == tenantId && accounts.Select(account => account.Id).Contains(item.AccountId)))
            .Should().Be(9);
        (await verify.AccountAccountingBooks.Where(item => item.TenantId == tenantId)
            .AllAsync(item => !item.IsEnabled)).Should().BeTrue(
            "the three freshly configured books are deliberately non-posting until governed activation");
    }

    private static async Task AddSupplierPrerequisitesAsync(ApplicationDbContext db, Guid tenantId)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(item => item.Code == "DEFAULT");
        tenant.Id.Should().Be(tenantId, "the model seed and DatabaseSeedingService use the same stable default tenant");
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@test.local",
            NormalizedEmail = "ADMIN@TEST.LOCAL",
            FirstName = "Seed",
            LastName = "Admin",
            IsActive = true,
            CreatedBy = "database-seed-order.tests"
        };
        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "SUPPLIER_TEST", Name = "Supplier test",
            IsActive = true, CreatedBy = "database-seed-order.tests"
        };
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(), TenantId = tenantId, EntityTypeId = entityType.Id,
            Name = "Business Partner Approval", LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            IsActive = true, PublishedAt = DateTime.UtcNow, CreatedBy = "database-seed-order.tests"
        };
        definition.Steps.Add(new WorkflowStep
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WorkflowDefinitionId = definition.Id,
            Name = "PendingApproval", StepType = WorkflowStepType.Approval, Order = 1,
            CreatedBy = "database-seed-order.tests"
        });
        db.AddRange(user, entityType, definition);
        await db.SaveChangesAsync();
    }

    private static TrackerInventory Inventory(IEnumerable<EntityEntry> entries)
    {
        var materialized = 0;
        var unchanged = 0;
        var pending = 0;
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Unchanged)
                unchanged++;
            else if (entry.State is not EntityState.Detached)
                pending++;
            materialized += entry.Navigations.Count(navigation => navigation.IsLoaded
                || navigation.CurrentValue is System.Collections.IEnumerable collection && collection.Cast<object>().Any()
                || navigation.CurrentValue is not null and not System.Collections.IEnumerable);
        }
        return new TrackerInventory(unchanged, pending, materialized);
    }

    private static async Task<long> GetGlf003ViolationCountAsync(ApplicationDbContext db)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM "AccountAccountingBooks" m
            JOIN "Accounts" a ON a."Id"=m."AccountId"
            JOIN "AccountingBooks" b ON b."Id"=m."AccountingBookId"
            LEFT JOIN "AccountClassifications" c ON c."Id"=m."AccountClassificationId"
            WHERE m."IsDeleted"=0 AND m."IsEnabled"=1 AND
              (a."IsDeleted"=1 OR b."IsDeleted"=1 OR b."IsActive"=0 OR b."AllowsPosting"=0 OR
               m."TenantId"<>a."TenantId" OR m."TenantId"<>b."TenantId" OR c."Id" IS NULL OR
               c."IsDeleted"=1 OR c."Status"<>2 OR c."IsPostingClassification"=0 OR
               c."TenantId"<>m."TenantId" OR c."AccountingBookId"<>m."AccountingBookId" OR
               c."CoreAccountType"<>a."AccountType")
            """;
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<string> FinanceAuthorityStateAsync(ApplicationDbContext db, Guid tenantId)
    {
        var books = await db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Code).Select(item => $"B|{item.Id:N}|{item.Code}|{item.LifecycleStatus}|{item.IsActive}|{item.AllowsPosting}|{item.IsDeleted}")
            .ToListAsync();
        var classifications = await db.AccountClassifications.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.AccountingBookId).ThenBy(item => item.Code)
            .Select(item => $"C|{item.Id:N}|{item.AccountingBookId:N}|{item.Code}|{item.CoreAccountType}|{item.Status}|{item.IsPostingClassification}|{item.IsDeleted}")
            .ToListAsync();
        var accounts = await db.Accounts.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.AccountCode)
            .Select(item => $"A|{item.Id:N}|{item.AccountCode}|{item.AccountNumber}|{item.AccountType}|{item.IsDeleted}")
            .ToListAsync();
        var segments = await db.AccountSegmentValues.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.AccountId).ThenBy(item => item.SegmentPosition)
            .Select(item => $"S|{item.Id:N}|{item.AccountId:N}|{item.SegmentStructureId:N}|{item.SegmentPosition}|{item.SegmentValue}|{item.SegmentLookupValueId:N}|{item.IsDeleted}")
            .ToListAsync();
        var mappings = await db.AccountAccountingBooks.AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.AccountId).ThenBy(item => item.AccountingBookId)
            .Select(item => $"M|{item.Id:N}|{item.AccountId:N}|{item.AccountingBookId:N}|{item.AccountClassificationId:N}|{item.IsEnabled}|{item.IsDeleted}")
            .ToListAsync();
        return string.Join('\n', books.Concat(classifications).Concat(accounts).Concat(segments).Concat(mappings));
    }

    private sealed class FinanceAuthorityWriteCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private static readonly Regex Mutation = new(
            "\\b(?:INSERT|UPDATE|DELETE|MERGE)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] AuthorityTables =
            ["AccountingBooks", "AccountClassifications", "Accounts", "AccountSegmentValues", "AccountAccountingBooks"];
        private int _armed;
        private int _commands;
        public int Commands => Volatile.Read(ref _commands);
        public IReadOnlyCollection<string> CommandTexts => _commandTexts;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _commandTexts = new();
        public void Arm() => Volatile.Write(ref _armed, 1);

        private void Count(System.Data.Common.DbCommand command)
        {
            if (Volatile.Read(ref _armed) == 1 && Mutation.IsMatch(command.CommandText)
                && AuthorityTables.Any(table => command.CommandText.Contains(table, StringComparison.Ordinal)))
            {
                Interlocked.Increment(ref _commands);
                _commandTexts.Enqueue(command.CommandText);
            }
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }
    }

    private sealed record TrackerInventory(int Unchanged, int Pending, int MaterializedRelationships);
}
