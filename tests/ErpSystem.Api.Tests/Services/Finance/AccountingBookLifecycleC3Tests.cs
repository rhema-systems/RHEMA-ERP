using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookLifecycleC3Tests
{
    [Fact]
    public async Task Read_DoesNotOpportunisticallyCreateAccountingBooks()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        await db.SaveChangesAsync();
        var service = Service(db, tenantId);

        (await service.GetBooksAsync(includeInactive: true)).Should().BeEmpty();
        db.AccountingBooks.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_EnforcesExactlyOnePrimary_AndCanonicalConcreteCodes()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        await db.SaveChangesAsync();
        var service = Service(db, tenantId);

        var primary = await service.CreateAsync(Full("ifrs", "PrimaryFull"));
        primary.Code.Should().Be("IFRS");
        primary.LifecycleStatus.Should().Be("Draft");
        primary.IsDefault.Should().BeTrue();
        primary.AllowsPosting.Should().BeFalse();

        foreach (var pseudo in new[] { "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS" })
        {
            var action = () => service.CreateAsync(Full(pseudo, "ParallelFull"));
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
        await FluentActions.Awaiting(() => service.CreateAsync(Full("OTHER_PRIMARY", "PrimaryFull")))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*one primary/default*");
    }

    [Fact]
    public async Task Delta_RequiresSameTenantBase_RejectsFullBaseAndCycles()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        var foreignTenantId = SeedTenantAuthority(db, "FOREIGN");
        var primary = SeedBook(db, tenantId, "IFRS", AccountingBookType.PrimaryFull, isDefault: true);
        var foreign = SeedBook(db, foreignTenantId, "FOREIGN", AccountingBookType.PrimaryFull, isDefault: true);
        await db.SaveChangesAsync();
        var service = Service(db, tenantId);

        await FluentActions.Awaiting(() => service.CreateAsync(Full("FULL_WITH_BASE", "ParallelFull", primary.Id)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*full accounting book cannot have a base*");
        await FluentActions.Awaiting(() => service.CreateAsync(Delta("DELTA_FOREIGN", foreign.Id)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid for this tenant*");

        var first = await service.CreateAsync(Delta("DELTA_ONE", primary.Id));
        var second = await service.CreateAsync(Delta("DELTA_TWO", first.Id));
        var update = Delta("DELTA_ONE", second.Id, rowVersion: Convert.ToBase64String([1]));
        await FluentActions.Awaiting(() => service.UpdateAsync(first.Id, update))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*cycles are prohibited*");
    }

    [Fact]
    public async Task ActivationFailsClosedUntilC4Readiness_AndMakerCannotCheckOwnTransition()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        var actor = Guid.NewGuid();
        var book = SeedBook(db, tenantId, "IFRS", AccountingBookType.PrimaryFull, isDefault: true,
            status: AccountingBookLifecycleStatus.Initializing);
        book.RowVersion = [1];
        await db.SaveChangesAsync();
        var workflow = Workflow();
        var service = Service(db, tenantId, actor, workflow: workflow.Object);

        await FluentActions.Awaiting(() => service.RequestTransitionAsync(book.Id, new RequestAccountingBookTransitionDto
        {
            TargetStatus = "Active", Reason = "ready", RowVersion = Convert.ToBase64String([1])
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*ACTIVATION_NOT_READY*");
        workflow.Verify(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);

        // The failed transition rolls its unit of work back and clears tracking; reload the
        // durable row before arranging the independent maker-checker assertion.
        book = await db.AccountingBooks.SingleAsync(item => item.Id == book.Id);
        book.PendingLifecycleStatus = AccountingBookLifecycleStatus.Suspended;
        book.PendingTransitionReason = "control";
        book.TransitionRequestedByUserId = actor;
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => service.ApproveTransitionAsync(book.Id, new DecideAccountingBookTransitionDto
        {
            Reason = "approve", RowVersion = Convert.ToBase64String([1])
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*Maker-checker*");
    }

    [Fact]
    public async Task BaseSuspension_BlocksDirectAndTransitiveDraftOrConfiguringDeltaDependents()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        var primary = SeedBook(db, tenantId, "IFRS", AccountingBookType.PrimaryFull, isDefault: true,
            status: AccountingBookLifecycleStatus.Active);
        var direct = SeedBook(db, tenantId, "DELTA_DIRECT", AccountingBookType.Delta,
            status: AccountingBookLifecycleStatus.Configuring);
        direct.BaseAccountingBookId = primary.Id;
        var nested = SeedBook(db, tenantId, "DELTA_NESTED", AccountingBookType.Delta,
            status: AccountingBookLifecycleStatus.Draft);
        nested.BaseAccountingBookId = direct.Id;
        await db.SaveChangesAsync();
        var workflow = Workflow();

        await FluentActions.Awaiting(() => Service(db, tenantId, workflow: workflow.Object).RequestTransitionAsync(
                primary.Id,
                new RequestAccountingBookTransitionDto
                {
                    TargetStatus = "Suspended", Reason = "control", RowVersion = Convert.ToBase64String([1])
                }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DELTA_DIRECT*DELTA_NESTED*");
        workflow.Verify(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeltaAdvancement_RequiresEveryBaseAncestorAtCompatibleStatus()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        var primary = SeedBook(db, tenantId, "IFRS", AccountingBookType.PrimaryFull, isDefault: true,
            status: AccountingBookLifecycleStatus.Initializing);
        var middle = SeedBook(db, tenantId, "DELTA_MIDDLE", AccountingBookType.Delta,
            status: AccountingBookLifecycleStatus.Draft);
        middle.BaseAccountingBookId = primary.Id;
        var leaf = SeedBook(db, tenantId, "DELTA_LEAF", AccountingBookType.Delta,
            status: AccountingBookLifecycleStatus.Configuring);
        leaf.BaseAccountingBookId = middle.Id;
        await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => Service(db, tenantId).RequestTransitionAsync(
                leaf.Id,
                new RequestAccountingBookTransitionDto
                {
                    TargetStatus = "Initializing", Reason = "prepare", RowVersion = Convert.ToBase64String([1])
                }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DELTA_MIDDLE*Draft*Initializing*");
    }

    [Fact]
    public async Task Approval_RevalidatesStaleBaseLineageBeforeWorkflowOrAuditMutation()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        var primary = SeedBook(db, tenantId, "IFRS", AccountingBookType.PrimaryFull, isDefault: true,
            status: AccountingBookLifecycleStatus.Initializing);
        var delta = SeedBook(db, tenantId, "DELTA", AccountingBookType.Delta,
            status: AccountingBookLifecycleStatus.Configuring);
        delta.BaseAccountingBookId = primary.Id;
        await db.SaveChangesAsync();
        var workflow = Workflow();
        var audit = Audit();

        await Service(db, tenantId, maker, workflow.Object, audit.Object).RequestTransitionAsync(delta.Id,
            new RequestAccountingBookTransitionDto
            {
                TargetStatus = "Initializing", Reason = "prepare", RowVersion = Convert.ToBase64String([1])
            });
        primary = await db.AccountingBooks.SingleAsync(item => item.Id == primary.Id);
        primary.LifecycleStatus = AccountingBookLifecycleStatus.Suspended;
        primary.IsActive = false;
        primary.AllowsPosting = false;
        await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => Service(db, tenantId, checker, workflow.Object, audit.Object)
                .ApproveTransitionAsync(delta.Id, new DecideAccountingBookTransitionDto
                {
                    Reason = "approve", RowVersion = Convert.ToBase64String([1])
                }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Suspended*Initializing*");
        workflow.Verify(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        audit.Verify(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()), Times.Once);
        var unchanged = await db.AccountingBooks.AsNoTracking().SingleAsync(item => item.Id == delta.Id);
        unchanged.LifecycleStatus.Should().Be(AccountingBookLifecycleStatus.Configuring);
        unchanged.PendingLifecycleStatus.Should().Be(AccountingBookLifecycleStatus.Initializing);
    }

    [Fact]
    public async Task StructuralIdentity_IsImmutableAfterUse_AndStaleRowVersionFails()
    {
        await using var db = NewDatabase();
        var tenantId = SeedTenantAuthority(db);
        var book = SeedBook(db, tenantId, "IFRS", AccountingBookType.PrimaryFull, isDefault: true);
        book.RowVersion = [4];
        db.JournalEntries.Add(new JournalEntry
        {
            TenantId = tenantId, AccountingBookId = book.Id, BookClassification = book.Code,
            JournalEntryNumber = "JE-C3-1", EntryDate = DateTime.UtcNow, Description = "Use evidence"
        });
        await db.SaveChangesAsync();
        var service = Service(db, tenantId);

        var structural = UpdateFull("IFRS_CHANGED", "PrimaryFull", Convert.ToBase64String([4]));
        await FluentActions.Awaiting(() => service.UpdateAsync(book.Id, structural)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable after accounting use*");

        var stale = UpdateFull("IFRS", "PrimaryFull", Convert.ToBase64String([9]));
        await FluentActions.Awaiting(() => service.UpdateAsync(book.Id, stale)).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task AuditFailure_RollsBackBookMutationOnRelationalProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await CreateBookSchemaAsync(db);
        var tenantId = SeedTenantAuthority(db);
        await db.SaveChangesAsync();
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var service = Service(db, tenantId, audit: audit.Object);

        await FluentActions.Awaiting(() => service.CreateAsync(Full("IFRS", "PrimaryFull")))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("audit unavailable");
        (await db.AccountingBooks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Controller_UsesCanonicalPermissions_AndExposesNoDelete()
    {
        var methods = typeof(AccountingBooksController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        methods.Single(item => item.Name == nameof(AccountingBooksController.GetBooks))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ViewFinance);
        methods.Single(item => item.Name == nameof(AccountingBooksController.Create))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBooks);
        methods.Single(item => item.Name == nameof(AccountingBooksController.RequestTransition))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.RequestAccountingBookTransitions);
        methods.Single(item => item.Name == nameof(AccountingBooksController.ApproveTransition))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookTransitions);
        methods.Should().NotContain(item => item.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    private static ApplicationDbContext NewDatabase() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"C3-{Guid.NewGuid():N}").Options);

    private static Guid SeedTenantAuthority(ApplicationDbContext db, string code = "TDC")
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Code = code, Name = code, Status = TenantStatus.Active, BaseCurrency = "GHS" });
        db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS" });
        return tenantId;
    }

    private static AccountingBook SeedBook(ApplicationDbContext db, Guid tenantId, string code, AccountingBookType type,
        bool isDefault = false, AccountingBookLifecycleStatus status = AccountingBookLifecycleStatus.Configuring)
    {
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = code, Purpose = "Reporting",
            BookType = type, LifecycleStatus = status, FunctionalCurrencyCode = type == AccountingBookType.Delta ? null : "GHS",
            IsDefault = isDefault, IsActive = status == AccountingBookLifecycleStatus.Active,
            AllowsPosting = status == AccountingBookLifecycleStatus.Active, RowVersion = [1]
        };
        db.AccountingBooks.Add(book);
        return book;
    }

    private static CreateAccountingBookDto Full(string code, string type, Guid? baseId = null) => new()
    { Code = code, Name = code, Purpose = "Reporting", BookType = type, FunctionalCurrencyCode = "GHS", BaseAccountingBookId = baseId };

    private static UpdateAccountingBookDto UpdateFull(string code, string type, string rowVersion) => new()
    { Code = code, Name = code, Purpose = "Reporting", BookType = type, FunctionalCurrencyCode = "GHS", RowVersion = rowVersion };

    private static UpdateAccountingBookDto Delta(string code, Guid baseId, string rowVersion) => new()
    { Code = code, Name = code, Purpose = "Adjustment", BookType = "Delta", BaseAccountingBookId = baseId, RowVersion = rowVersion };
    private static CreateAccountingBookDto Delta(string code, Guid baseId) => new()
    { Code = code, Name = code, Purpose = "Adjustment", BookType = "Delta", BaseAccountingBookId = baseId };

    private static AccountingBookService Service(ApplicationDbContext db, Guid tenantId, Guid? actor = null,
        IWorkflowService? workflow = null, IFinanceAuditService? audit = null)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.TenantId).Returns(tenantId);
        current.SetupGet(item => item.UserId).Returns((actor ?? Guid.NewGuid()).ToString());
        current.SetupGet(item => item.UserName).Returns("finance.c3.tests");
        return new AccountingBookService(db, current.Object, workflow ?? Workflow().Object, audit ?? Audit().Object);
    }

    private static Mock<IWorkflowService> Workflow()
    {
        var value = new Mock<IWorkflowService>();
        value.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        value.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid()
            });
        value.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
        value.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        return value;
    }

    private static Mock<IFinanceAuditService> Audit()
    {
        var value = new Mock<IFinanceAuditService>();
        value.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        return value;
    }

    private static async Task CreateBookSchemaAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        var statements = db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(statement => statement.Contains("CREATE TABLE \"Tenants\"", StringComparison.Ordinal)
                || statement.Contains("CREATE TABLE \"FinanceSettings\"", StringComparison.Ordinal)
                || statement.Contains("CREATE TABLE \"AccountingBooks\"", StringComparison.Ordinal)
                || statement.Contains("IX_AccountingBooks_", StringComparison.Ordinal));
        foreach (var statement in statements)
            await db.Database.ExecuteSqlRawAsync(statement.Replace(
                "\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X''", StringComparison.Ordinal));
    }
}
