using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountBookCurrencyPolicyServiceTests
{
    [Fact]
    public async Task GetAndSave_ResolveInheritanceAndOrdinaryOverrideForExactBookCurrency()
    {
        await using var fixture = await Fixture.CreateAsync(AccountType.Asset, RevaluationTreatment.Include);
        var service = fixture.CreateService(fixture.MakerId);

        var inherited = (await service.GetForAccountAsync(fixture.Account.Id)).Single();
        inherited.EffectiveRevaluationRequired.Should().BeTrue();
        inherited.EffectiveSource.Should().Be("Classification");
        inherited.RevaluationOverride.Should().BeNull();

        var saved = await service.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
            new SaveAccountBookCurrencyPolicyDto
            {
                RevaluationOverride = false,
                Reason = "Exclude this asset exposure",
                ConfirmNonstandardInclusion = false
            });

        saved.EffectiveRevaluationRequired.Should().BeFalse();
        saved.EffectiveSource.Should().Be("CurrencyOverride");
        saved.AccountingBookCode.Should().Be("IFRS");
        saved.CurrencyCode.Should().Be("USD");
        fixture.AuditEvents.Should().ContainSingle(item => item.EventType == FinanceAuditEvents.FxPolicyOverrideChanged);
    }

    [Fact]
    public async Task EffectiveOverrides_AreIsolatedByBookAndCurrency()
    {
        await using var fixture = await Fixture.CreateAsync(AccountType.Asset, RevaluationTreatment.Exclude);
        var localBook = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, Code = "LOCAL_STATUTORY", Name = "Local Statutory",
            Purpose = "Statutory", IsActive = true, AllowsPosting = true
        };
        var localClassification = new AccountClassification
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountingBookId = localBook.Id,
            Code = "ASSET_OTHER", Name = "Other Assets", CoreAccountType = AccountType.Asset,
            DefaultRevaluationTreatment = RevaluationTreatment.Exclude,
            Status = AccountClassificationStatus.Active, IsPostingClassification = true
        };
        var localMapping = new AccountAccountingBook
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountId = fixture.Account.Id,
            AccountingBookId = localBook.Id, AccountClassificationId = localClassification.Id, IsEnabled = true
        };
        var euroLink = new AccountCurrencyLink
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountId = fixture.Account.Id,
            LinkedCurrencyCode = "EUR", IsActive = true, EffectiveDate = DateTime.UtcNow.AddDays(-1)
        };
        fixture.Db.AddRange(localBook, localClassification, localMapping, euroLink);
        await fixture.Db.SaveChangesAsync();
        var service = fixture.CreateService(fixture.MakerId);

        await service.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
            new SaveAccountBookCurrencyPolicyDto { RevaluationOverride = true, Reason = "Include IFRS USD only" });
        await service.SaveAsync(fixture.Account.Id, euroLink.Id, localBook.Id,
            new SaveAccountBookCurrencyPolicyDto { RevaluationOverride = true, Reason = "Include local EUR only" });

        var policies = await service.GetForAccountAsync(fixture.Account.Id);
        policies.Should().HaveCount(4);
        policies.Single(item => item.AccountingBookCode == "IFRS" && item.CurrencyCode == "USD")
            .EffectiveRevaluationRequired.Should().BeTrue();
        policies.Single(item => item.AccountingBookCode == "IFRS" && item.CurrencyCode == "EUR")
            .EffectiveRevaluationRequired.Should().BeFalse();
        policies.Single(item => item.AccountingBookCode == "LOCAL_STATUTORY" && item.CurrencyCode == "USD")
            .EffectiveRevaluationRequired.Should().BeFalse();
        policies.Single(item => item.AccountingBookCode == "LOCAL_STATUTORY" && item.CurrencyCode == "EUR")
            .EffectiveRevaluationRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Save_RequiresReasonAndRejectsInvalidAccountBookCurrencyLineage()
    {
        await using var fixture = await Fixture.CreateAsync(AccountType.Asset, RevaluationTreatment.Exclude);
        var service = fixture.CreateService(fixture.MakerId);

        await service.Invoking(item => item.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
                new SaveAccountBookCurrencyPolicyDto { RevaluationOverride = true, Reason = " " }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*reason is required*");
        await service.Invoking(item => item.SaveAsync(Guid.NewGuid(), fixture.Link.Id, fixture.Book.Id,
                new SaveAccountBookCurrencyPolicyDto { RevaluationOverride = true, Reason = "Valid reason" }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not enabled*");
    }

    [Fact]
    public async Task NonstandardInclusion_IsPendingUntilDifferentAuthorizedCheckerCompletesWorkflow()
    {
        await using var fixture = await Fixture.CreateAsync(AccountType.Revenue, RevaluationTreatment.Exclude);
        var maker = fixture.CreateService(fixture.MakerId);

        await maker.Invoking(item => item.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
                new SaveAccountBookCurrencyPolicyDto
                {
                    RevaluationOverride = true,
                    Reason = "Revalue this foreign revenue balance",
                    ConfirmNonstandardInclusion = false
                }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*confirmation is required*");

        var pending = await maker.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
            new SaveAccountBookCurrencyPolicyDto
            {
                RevaluationOverride = true,
                Reason = "Revalue this foreign revenue balance",
                ConfirmNonstandardInclusion = true
            });
        pending.LifecycleStatus.Should().Be("PendingApproval");
        pending.EffectiveRevaluationRequired.Should().BeFalse("the pending override must not replace the current effective policy");
        pending.PendingRevaluationOverride.Should().BeTrue();

        var entity = await fixture.Db.AccountBookCurrencyPolicies.SingleAsync();
        entity.RowVersion = [1];
        await fixture.Db.SaveChangesAsync();

        await maker.Invoking(item => item.ApproveAsync(fixture.Account.Id, entity.Id,
                new DecideAccountBookCurrencyPolicyDto { Reason = "Maker cannot approve", RowVersion = Convert.ToBase64String([1]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Maker-checker*");

        var checker = fixture.CreateService(fixture.CheckerId);
        var approved = await checker.ApproveAsync(fixture.Account.Id, entity.Id,
            new DecideAccountBookCurrencyPolicyDto
            {
                Reason = "Independent finance approval",
                RowVersion = Convert.ToBase64String([1])
            });

        approved.LifecycleStatus.Should().Be("Active");
        approved.EffectiveRevaluationRequired.Should().BeTrue();
        approved.IsNonstandardInclusion.Should().BeTrue();
        approved.Warning.Should().Contain("Non-standard revaluation policy");
        fixture.AuditEvents.Should().Contain(item => item.EventType == FinanceAuditEvents.FxPolicyOverrideRequested && item.Reason != null);
        fixture.AuditEvents.Should().Contain(item => item.EventType == FinanceAuditEvents.FxPolicyOverrideApproved && item.BeforeValues != null && item.AfterValues != null);
    }

    [Fact]
    public async Task RejectedNonstandardRequest_LeavesInheritedPolicyEffective()
    {
        await using var fixture = await Fixture.CreateAsync(AccountType.Expense, RevaluationTreatment.Exclude);
        var pending = await fixture.CreateService(fixture.MakerId).SaveAsync(
            fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
            new SaveAccountBookCurrencyPolicyDto
            {
                RevaluationOverride = true,
                Reason = "Request expense revaluation",
                ConfirmNonstandardInclusion = true
            });
        var entity = await fixture.Db.AccountBookCurrencyPolicies.SingleAsync();
        entity.RowVersion = [1];
        await fixture.Db.SaveChangesAsync();

        var rejected = await fixture.CreateService(fixture.CheckerId).RejectAsync(
            fixture.Account.Id, pending.Id!.Value,
            new DecideAccountBookCurrencyPolicyDto
            {
                Reason = "Policy is not justified",
                RowVersion = Convert.ToBase64String([1])
            });

        rejected.LifecycleStatus.Should().Be("Rejected");
        rejected.RevaluationOverride.Should().BeNull();
        rejected.EffectiveRevaluationRequired.Should().BeFalse();
        rejected.PendingRevaluationOverride.Should().BeNull();
    }

    [Fact]
    public async Task OrdinaryOverride_RollsBackWhenAuditPersistenceFails()
    {
        await using var fixture = await Fixture.CreateRelationalAsync(AccountType.Asset, RevaluationTreatment.Exclude);
        var currentUser = Fixture.CurrentUser(fixture.TenantId, fixture.MakerId, "maker");
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var service = new AccountBookCurrencyPolicyService(fixture.Db, currentUser.Object, fixture.Workflow.Object, audit.Object);

        await service.Invoking(item => item.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
                new SaveAccountBookCurrencyPolicyDto { RevaluationOverride = true, Reason = "Valid audited override" }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");

        (await fixture.Db.AccountBookCurrencyPolicies.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NonstandardRequest_RollsBackWhenWorkflowStartFails()
    {
        await using var fixture = await Fixture.CreateRelationalAsync(AccountType.Revenue, RevaluationTreatment.Exclude);
        fixture.Workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(AccountBookCurrencyPolicyService.WorkflowEntityType))
            .ReturnsAsync(true);
        fixture.Workflow.Setup(item => item.StartApprovalWorkflowAsync(AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("workflow unavailable"));
        var service = fixture.CreateService(fixture.MakerId);

        await service.Invoking(item => item.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
                new SaveAccountBookCurrencyPolicyDto
                {
                    RevaluationOverride = true,
                    Reason = "Request governed revenue revaluation",
                    ConfirmNonstandardInclusion = true
                }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("workflow unavailable");

        (await fixture.Db.AccountBookCurrencyPolicies.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NonstandardApproval_RollsBackActivationWhenAuditFails()
    {
        await using var fixture = await Fixture.CreateRelationalAsync(AccountType.Equity, RevaluationTreatment.Exclude);
        fixture.Workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(AccountBookCurrencyPolicyService.WorkflowEntityType))
            .ReturnsAsync(true);
        fixture.Workflow.Setup(item => item.StartApprovalWorkflowAsync(AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid()
            });
        fixture.Workflow.Setup(item => item.CanUserApproveAsync(
                AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>(), fixture.CheckerId))
            .ReturnsAsync(true);
        fixture.Workflow.Setup(item => item.ProcessApprovalStepAsync(
                AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>(), fixture.CheckerId, "Approve", It.IsAny<string>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        var pending = await fixture.CreateService(fixture.MakerId).SaveAsync(
            fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
            new SaveAccountBookCurrencyPolicyDto
            {
                RevaluationOverride = true,
                Reason = "Request governed equity revaluation",
                ConfirmNonstandardInclusion = true
            });
        await fixture.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE AccountBookCurrencyPolicies SET RowVersion = {new byte[] { 1 }} WHERE Id = {pending.Id!.Value}");
        fixture.Db.ChangeTracker.Clear();
        var currentUser = Fixture.CurrentUser(fixture.TenantId, fixture.CheckerId, "checker");
        var failingAudit = new Mock<IFinanceAuditService>();
        failingAudit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("approval audit unavailable"));
        var checker = new AccountBookCurrencyPolicyService(
            fixture.Db, currentUser.Object, fixture.Workflow.Object, failingAudit.Object);

        await checker.Invoking(item => item.ApproveAsync(fixture.Account.Id, pending.Id.Value,
                new DecideAccountBookCurrencyPolicyDto
                {
                    Reason = "Independent approval",
                    RowVersion = Convert.ToBase64String([1])
                }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("approval audit unavailable");

        var stored = await fixture.Db.AccountBookCurrencyPolicies.AsNoTracking().SingleAsync();
        stored.LifecycleStatus.Should().Be("PendingApproval");
        stored.RevaluationOverride.Should().BeNull();
        stored.PendingRevaluationOverride.Should().BeTrue();
    }

    [Fact]
    public async Task ExistingPolicy_RejectsAStaleRowVersion()
    {
        await using var fixture = await Fixture.CreateRelationalAsync(AccountType.Asset, RevaluationTreatment.Exclude);
        var service = fixture.CreateService(fixture.MakerId);
        var created = await service.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
            new SaveAccountBookCurrencyPolicyDto
            {
                RevaluationOverride = true,
                Reason = "Initial governed decision"
            });
        created.Id.Should().NotBeNull();

        await fixture.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE AccountBookCurrencyPolicies SET RowVersion = {new byte[] { 2 }} WHERE Id = {created.Id!.Value}");
        fixture.Db.ChangeTracker.Clear();

        await service.Invoking(item => item.SaveAsync(fixture.Account.Id, fixture.Link.Id, fixture.Book.Id,
                new SaveAccountBookCurrencyPolicyDto
                {
                    RevaluationOverride = false,
                    Reason = "Stale conflicting decision",
                    RowVersion = Convert.ToBase64String([1])
                }))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();

        var stored = await fixture.Db.AccountBookCurrencyPolicies.AsNoTracking().SingleAsync();
        stored.RevaluationOverride.Should().BeTrue();
        stored.OverrideReason.Should().Be("Initial governed decision");
    }

    [Fact]
    public void Migration_CreatesBookScopedPolicyEvidenceAndRetiresLegacyBoolean()
    {
        var operations = new PhaseFourMigration().BuildUpOperations();

        operations.OfType<CreateTableOperation>().Should().ContainSingle(item => item.Name == "AccountBookCurrencyPolicies");
        operations.OfType<DropColumnOperation>().Should().ContainSingle(item =>
            item.Table == "AccountCurrencyLinks" && item.Name == "RevaluationRequired");
        operations.OfType<AddColumnOperation>().Should().Contain(item => item.Table == "FxRevaluationBatches" && item.Name == "AccountingBookId");
        operations.OfType<AddColumnOperation>().Should().Contain(item => item.Table == "FxRevaluationLines" && item.Name == "AccountClassificationCode");
        operations.OfType<CreateIndexOperation>().Should().Contain(item =>
            item.Table == "AccountBookCurrencyPolicies" && item.IsUnique && item.Filter!.Contains("[IsDeleted] = 0"));
        operations.OfType<SqlOperation>().Select(item => item.Sql).Should().Contain(item =>
            item.Contains("Phase 4 preflight failed", StringComparison.Ordinal)
            && item.Contains("RevaluationRequired", StringComparison.Ordinal));
    }

    [Fact]
    public void PhaseFourMigration_IsDiscoverableByEfCore()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Phase4MigrationDiscovery;Trusted_Connection=True")
            .Options);

        db.GetService<IMigrationsAssembly>().Migrations.Keys.Should()
            .Contain("20260903190453_AddBookScopedFxRevaluationPolicy");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, Guid tenantId, Account account, AccountingBook book,
            AccountCurrencyLink link, Guid makerId, Guid checkerId, Mock<IWorkflowService> workflow,
            List<FinanceAuditEventDto> auditEvents, SqliteConnection? connection = null)
        {
            Db = db;
            TenantId = tenantId;
            Account = account;
            Book = book;
            Link = link;
            MakerId = makerId;
            CheckerId = checkerId;
            Workflow = workflow;
            AuditEvents = auditEvents;
            Connection = connection;
        }

        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }
        public Account Account { get; }
        public AccountingBook Book { get; }
        public AccountCurrencyLink Link { get; }
        public Guid MakerId { get; }
        public Guid CheckerId { get; }
        public Mock<IWorkflowService> Workflow { get; }
        public List<FinanceAuditEventDto> AuditEvents { get; }
        private SqliteConnection? Connection { get; }

        public static async Task<Fixture> CreateAsync(AccountType accountType, RevaluationTreatment treatment)
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"fx-policy-{Guid.NewGuid():N}").Options);
            var tenantId = Guid.NewGuid();
            var makerId = Guid.NewGuid();
            var checkerId = Guid.NewGuid();
            var account = new Account
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "TEST", AccountNumber = "TEST",
                AccountName = "Policy test", AccountType = accountType, Status = AccountStatus.Active,
                CurrencyCode = "GHS", IsMultiCurrency = true
            };
            var book = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
                Purpose = "Primary", IsActive = true, AllowsPosting = true
            };
            var classification = new AccountClassification
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
                Code = accountType.ToString().ToUpperInvariant(), Name = accountType.ToString(),
                CoreAccountType = accountType, DefaultRevaluationTreatment = treatment,
                Status = AccountClassificationStatus.Active, IsPostingClassification = true
            };
            var mapping = new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = true
            };
            var link = new AccountCurrencyLink
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                LinkedCurrencyCode = "USD", IsActive = true, EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };
            db.AddRange(account, book, classification, mapping, link);
            await db.SaveChangesAsync();

            var workflow = new Mock<IWorkflowService>();
            workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(AccountBookCurrencyPolicyService.WorkflowEntityType)).ReturnsAsync(true);
            workflow.Setup(item => item.StartApprovalWorkflowAsync(AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>()))
                .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
            workflow.Setup(item => item.CanUserApproveAsync(AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>(), checkerId)).ReturnsAsync(true);
            workflow.Setup(item => item.ProcessApprovalStepAsync(AccountBookCurrencyPolicyService.WorkflowEntityType, It.IsAny<Guid>(), checkerId, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string _, Guid _, Guid _, string action, string? _) => new WorkflowExecutionResult
                {
                    Success = true,
                    Status = action == "Approve" ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Cancelled
                });
            return new Fixture(db, tenantId, account, book, link, makerId, checkerId, workflow, []);
        }

        public static async Task<Fixture> CreateRelationalAsync(AccountType accountType, RevaluationTreatment treatment)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            var script = db.Database.GenerateCreateScript();
            var requiredTables = new[]
            {
                "Accounts", "AccountingBooks", "AccountClassifications", "AccountAccountingBooks",
                "AccountCurrencyLinks", "AccountBookCurrencyPolicies"
            };
            foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Where(statement => requiredTables.Any(table => statement.Contains($"CREATE TABLE \"{table}\"", StringComparison.Ordinal))))
            {
                await db.Database.ExecuteSqlRawAsync(statement.Replace(
                    "\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal));
            }

            var fixture = await CreateSeededAsync(db, accountType, treatment, connection);
            return fixture;
        }

        private static async Task<Fixture> CreateSeededAsync(
            ApplicationDbContext db, AccountType accountType, RevaluationTreatment treatment, SqliteConnection connection)
        {
            var tenantId = Guid.NewGuid();
            var makerId = Guid.NewGuid();
            var checkerId = Guid.NewGuid();
            var account = new Account
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "TEST", AccountNumber = "TEST",
                AccountName = "Policy test", AccountType = accountType, Status = AccountStatus.Active,
                CurrencyCode = "GHS", IsMultiCurrency = true
            };
            var book = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
                Purpose = "Primary", IsActive = true, AllowsPosting = true
            };
            var classification = new AccountClassification
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
                Code = accountType.ToString().ToUpperInvariant(), Name = accountType.ToString(),
                CoreAccountType = accountType, DefaultRevaluationTreatment = treatment,
                Status = AccountClassificationStatus.Active, IsPostingClassification = true
            };
            var mapping = new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                AccountingBookId = book.Id, AccountClassificationId = classification.Id, IsEnabled = true
            };
            var link = new AccountCurrencyLink
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                LinkedCurrencyCode = "USD", IsActive = true, EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };
            db.AddRange(account, book, classification, mapping, link);
            await db.SaveChangesAsync();
            var workflow = new Mock<IWorkflowService>();
            return new Fixture(db, tenantId, account, book, link, makerId, checkerId, workflow, [], connection);
        }

        public AccountBookCurrencyPolicyService CreateService(Guid actorId)
        {
            var currentUser = CurrentUser(TenantId, actorId, actorId == MakerId ? "maker" : "checker");
            var audit = new Mock<IFinanceAuditService>();
            audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
                .Callback<FinanceAuditEventDto, CancellationToken>((item, _) => AuditEvents.Add(item))
                .ReturnsAsync(new AuditLog());
            return new AccountBookCurrencyPolicyService(Db, currentUser.Object, Workflow.Object, audit.Object);
        }

        public static Mock<ICurrentUserService> CurrentUser(Guid tenantId, Guid actorId, string userName)
        {
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(actorId.ToString());
            currentUser.SetupGet(item => item.UserName).Returns(userName);
            return currentUser;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Connection != null) await Connection.DisposeAsync();
        }
    }

    private sealed class PhaseFourMigration : AddBookScopedFxRevaluationPolicy
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }
}
