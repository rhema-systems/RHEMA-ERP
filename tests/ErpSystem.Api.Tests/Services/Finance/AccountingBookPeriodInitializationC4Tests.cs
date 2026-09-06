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
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookPeriodInitializationC4Tests
{
    [Fact]
    public void Controllers_UseCanonicalReadManageAndCheckerPermissions_AndExposeNoDelete()
    {
        var periods = typeof(AccountingBookPeriodsController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Get))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ViewFinance);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Create))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBookPeriods);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.RequestTransition))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBookPeriods);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Approve))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookPeriods);
        periods.Single(item => item.Name == nameof(AccountingBookPeriodsController.Reject))
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookPeriods);
        periods.Should().NotContain(item => item.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        var initialization = typeof(AccountingBookInitializationController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (var read in new[] { nameof(AccountingBookInitializationController.Get), nameof(AccountingBookInitializationController.GetReadiness), nameof(AccountingBookInitializationController.Prepare) })
            initialization.Single(item => item.Name == read).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ViewFinance);
        foreach (var manage in new[] { nameof(AccountingBookInitializationController.Configure), nameof(AccountingBookInitializationController.Submit) })
            initialization.Single(item => item.Name == manage).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ManageAccountingBookInitialization);
        foreach (var decide in new[] { nameof(AccountingBookInitializationController.Approve), nameof(AccountingBookInitializationController.Reject) })
            initialization.Single(item => item.Name == decide).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(FinancePermissions.ApproveAccountingBookInitialization);
        initialization.Should().NotContain(item => item.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Period_ReadDoesNotCreate_AndWrongTenantFails()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = PeriodService(db, state.TenantId, Guid.NewGuid());

        (await service.GetAsync(state.Book.Id)).Should().BeEmpty();
        await FluentActions.Awaiting(() => service.CreateAsync(state.Book.Id,
            new CreateAccountingBookPeriodDto { FiscalPeriodId = Guid.NewGuid() }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*current tenant*");
        db.AccountingBookPeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task Period_OpenRequiresOuterOpenPeriod_AndMakerChecker()
    {
        await using var db = Context();
        var state = Seed(db);
        state.Period.IsOpen = false;
        await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var service = PeriodService(db, state.TenantId, maker);
        var created = await service.CreateAsync(state.Book.Id, new CreateAccountingBookPeriodDto { FiscalPeriodId = state.Period.Id });
        var entity = db.AccountingBookPeriods.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => service.RequestTransitionAsync(state.Book.Id, created.Id, new RequestAccountingBookPeriodTransitionDto
        { TargetStatus = "Open", Reason = "Open first book period", RowVersion = Convert.ToBase64String([1]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant fiscal period must be open*");
    }

    [Fact]
    public async Task Period_TransitionRequiresExactRouteBook_AndDifferentChecker()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var created = await PeriodService(db, state.TenantId, maker).CreateAsync(state.Book.Id,
            new CreateAccountingBookPeriodDto { FiscalPeriodId = state.Period.Id });
        var entity = db.AccountingBookPeriods.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();
        var request = new RequestAccountingBookPeriodTransitionDto { TargetStatus = "Open", Reason = "Open", RowVersion = Convert.ToBase64String([1]) };

        await FluentActions.Awaiting(() => PeriodService(db, state.TenantId, maker).RequestTransitionAsync(Guid.NewGuid(), created.Id, request))
            .Should().ThrowAsync<KeyNotFoundException>();
        entity.PendingStatus.Should().BeNull();

        await PeriodService(db, state.TenantId, maker).RequestTransitionAsync(state.Book.Id, created.Id, request);
        entity.RowVersion = [2]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => PeriodService(db, state.TenantId, maker).ApproveAsync(state.Book.Id, created.Id,
            new DecideAccountingBookPeriodTransitionDto { Reason = "self", RowVersion = Convert.ToBase64String([2]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*checker must differ*");
        entity.RowVersion = [3]; await db.SaveChangesAsync();
        var approved = await PeriodService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id, created.Id,
            new DecideAccountingBookPeriodTransitionDto { Reason = "approved", RowVersion = Convert.ToBase64String([3]) });
        approved.Status.Should().Be("Open");
        approved.PendingStatus.Should().BeNull();
    }

    [Fact]
    public async Task Period_StaleRowVersionRejectsWithoutPendingMutation()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var created = await PeriodService(db, state.TenantId, Guid.NewGuid()).CreateAsync(state.Book.Id,
            new CreateAccountingBookPeriodDto { FiscalPeriodId = state.Period.Id });
        var entity = db.AccountingBookPeriods.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => PeriodService(db, state.TenantId, Guid.NewGuid()).RequestTransitionAsync(state.Book.Id, created.Id,
            new RequestAccountingBookPeriodTransitionDto { TargetStatus = "Open", Reason = "stale", RowVersion = Convert.ToBase64String([9]) }))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        entity.PendingStatus.Should().BeNull();
        entity.WorkflowInstanceId.Should().BeNull();
    }

    [Fact]
    public async Task Initialization_IsBalancedCompleteAndIdempotent_ButChangedEvidenceConflicts()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());
        var request = Independent(state, "init-1", 0m);

        var first = await service.ConfigureAsync(state.Book.Id, request);
        var retry = await service.ConfigureAsync(state.Book.Id, request);
        retry.Id.Should().Be(first.Id);
        retry.IsBalanced.Should().BeTrue();
        retry.IsCoverageComplete.Should().BeTrue();

        var changed = Independent(state, "init-1", 0m);
        changed.Reason = "Conflicting governed reason";
        await FluentActions.Awaiting(() => service.ConfigureAsync(state.Book.Id, changed))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("INITIALIZATION_IDEMPOTENCY_CONFLICT:*");
    }

    [Fact]
    public async Task Initialization_StaleConfigureAndDecisionRejectWithoutMutation()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var maker = Guid.NewGuid(); var service = InitializationService(db, state.TenantId, maker);
        await service.ConfigureAsync(state.Book.Id, Independent(state, "stale-original", 0m));
        var entity = db.AccountingBookInitializations.Single(); entity.RowVersion = [2]; await db.SaveChangesAsync();
        var replacement = Independent(state, "stale-replacement", 0m); replacement.RowVersion = Convert.ToBase64String([9]);

        await FluentActions.Awaiting(() => service.ConfigureAsync(state.Book.Id, replacement))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        entity.IdempotencyKey.Should().Be("stale-original");
        entity.InitializationStatus.Should().Be(AccountingBookInitializationStatus.Draft);

        await service.SubmitAsync(state.Book.Id);
        entity.RowVersion = [3]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "stale checker", RowVersion = Convert.ToBase64String([8]) }))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        entity.InitializationStatus.Should().Be(AccountingBookInitializationStatus.PendingApproval);
        entity.ApprovedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Initialization_RejectsSubmittedAmountsThatDoNotReconcileToPostedBookEvidence()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var service = InitializationService(db, state.TenantId, Guid.NewGuid());

        await FluentActions.Awaiting(() => service.ConfigureAsync(state.Book.Id, Independent(state, "init-2", 5m)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*posted exact-book balances*");
    }

    [Theory]
    [InlineData("BaseBookCopyAtCutoff")]
    [InlineData("BaseBalancesWithOpeningAdjustments")]
    public async Task Initialization_BaseModesUseExactActiveSourceBookEvidence(string mode)
    {
        await using var db = Context();
        var state = Seed(db);
        var source = new AccountingBook { TenantId = state.TenantId, Code = "SOURCE", Name = "Source", Purpose = "Reporting",
            BookType = AccountingBookType.ParallelFull, LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS", IsActive = true, AllowsPosting = true };
        db.AccountingBooks.Add(source);
        var cutoff = db.FiscalPeriods.Local.Single(item => item.PeriodCode == "2025-12");
        foreach (var account in new[] { state.Debit, state.Credit })
            db.AccountBalances.Add(new AccountBalance { TenantId = state.TenantId, AccountId = account.Id, AccountingBookId = source.Id,
                FiscalPeriodId = cutoff.Id, BookClassification = source.Code, Currency = "GHS", ClosingBalance = 0m });
        await db.SaveChangesAsync();
        var request = Independent(state, $"base-{mode}", 0m); request.Mode = mode; request.SourceAccountingBookId = source.Id;

        var result = await InitializationService(db, state.TenantId, Guid.NewGuid()).ConfigureAsync(state.Book.Id, request);
        result.SourceAccountingBookId.Should().Be(source.Id);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task Initialization_RejectionPreservesEvidenceAndCreatesVersionedSuccessor()
    {
        await using var db = Context();
        var state = Seed(db); await db.SaveChangesAsync();
        var maker = Guid.NewGuid(); var service = InitializationService(db, state.TenantId, maker);
        await service.ConfigureAsync(state.Book.Id, Independent(state, "rejected-v1", 0m));
        await service.SubmitAsync(state.Book.Id);
        var first = db.AccountingBookInitializations.Single(); first.RowVersion = [4]; await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).RejectAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Evidence incomplete", RowVersion = Convert.ToBase64String([4]) });

        var second = await service.ConfigureAsync(state.Book.Id, Independent(state, "replacement-v2", 0m));
        second.Version.Should().Be(2);
        second.SupersedesInitializationId.Should().Be(first.Id);
        db.AccountingBookInitializations.Single(item => item.Id == first.Id).InitializationStatus.Should().Be(AccountingBookInitializationStatus.Rejected);
    }

    [Fact]
    public async Task ActivationReadiness_RequiresApprovedInitializationAndExactFirstPostingPeriod()
    {
        await using var db = Context();
        var state = Seed(db);
        await db.SaveChangesAsync();
        var maker = Guid.NewGuid();
        var service = InitializationService(db, state.TenantId, maker);
        var draft = await service.ConfigureAsync(state.Book.Id, Independent(state, "init-3", 0m));
        (await service.GetReadinessAsync(state.Book.Id)).IsReady.Should().BeFalse();

        await service.SubmitAsync(state.Book.Id);
        var entity = db.AccountingBookInitializations.Single(); entity.RowVersion = [3]; await db.SaveChangesAsync();
        await InitializationService(db, state.TenantId, Guid.NewGuid()).ApproveAsync(state.Book.Id,
            new DecideAccountingBookInitializationDto { Reason = "Independent review complete", RowVersion = Convert.ToBase64String([3]) });
        db.AccountingBookPeriods.Add(new AccountingBookPeriod { TenantId = state.TenantId, AccountingBookId = state.Book.Id,
            FiscalPeriodId = state.Period.Id, PeriodStatus = AccountingBookPeriodStatus.Open });
        await db.SaveChangesAsync();

        var readiness = await service.GetReadinessAsync(state.Book.Id);
        readiness.IsReady.Should().BeTrue();
        readiness.RequiredPeriodCount.Should().Be(1);
        readiness.ReadyPeriodCount.Should().Be(1);
    }


    private static ConfigureAccountingBookInitializationDto Independent(State state, string key, decimal firstDebit) => new()
    {
        Mode = "IndependentOpeningBalances", CutoffDate = new DateTime(2025, 12, 31), IdempotencyKey = key, Reason = "Governed test opening",
        Lines = new[]
        {
            new AccountingBookInitializationLineDto { AccountId = state.Debit.Id, CurrencyCode = "GHS", OpeningDebit = firstDebit },
            new AccountingBookInitializationLineDto { AccountId = state.Credit.Id, CurrencyCode = "GHS", OpeningCredit = firstDebit }
        }
    };

    private static State Seed(ApplicationDbContext db)
    {
        var tenant = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenant, Code = "C4", Name = "C4", Status = TenantStatus.Active, BaseCurrency = "GHS" });
        db.FinanceSettings.Add(new FinanceSettings { TenantId = tenant, BaseCurrency = "GHS", CoaType = "Segmented" });
        var book = new AccountingBook { TenantId = tenant, Code = "IFRS", Name = "IFRS", Purpose = "Reporting",
            BookType = AccountingBookType.PrimaryFull, LifecycleStatus = AccountingBookLifecycleStatus.Initializing,
            FunctionalCurrencyCode = "GHS", IsDefault = true, IsActive = false, AllowsPosting = false };
        db.AccountingBooks.Add(book);
        var period = new FiscalPeriod { TenantId = tenant, FiscalYearId = Guid.NewGuid(), PeriodName = "January 2026", PeriodCode = "2026-01",
            PeriodNumber = 1, PeriodType = PeriodType.Monthly, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31),
            PeriodDays = 31, PeriodStatus = "Open", IsOpen = true };
        db.FiscalPeriods.Add(period);
        db.FiscalPeriods.Add(new FiscalPeriod { TenantId = tenant, FiscalYearId = Guid.NewGuid(), PeriodName = "December 2025", PeriodCode = "2025-12",
            PeriodNumber = 12, PeriodType = PeriodType.Monthly, StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            PeriodDays = 31, PeriodStatus = "Closed", IsOpen = false, IsClosed = true });
        var debit = Account(tenant, "1000", AccountType.Asset); var credit = Account(tenant, "3000", AccountType.Equity);
        db.Accounts.AddRange(debit, credit);
        var assetClass = Classification(tenant, book.Id, "ASSET", AccountType.Asset); var equityClass = Classification(tenant, book.Id, "EQUITY", AccountType.Equity);
        db.AccountClassifications.AddRange(assetClass, equityClass);
        db.AccountAccountingBooks.AddRange(Mapping(tenant, book.Id, debit.Id, assetClass.Id), Mapping(tenant, book.Id, credit.Id, equityClass.Id));
        return new State(tenant, book, period, debit, credit);
    }
    private static Account Account(Guid tenant, string code, AccountType type) => new() { TenantId = tenant, AccountCode = code, AccountNumber = code,
        AccountName = code, AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active };
    private static AccountClassification Classification(Guid tenant, Guid book, string code, AccountType type) => new() { TenantId = tenant,
        AccountingBookId = book, Code = code, Name = code, CoreAccountType = type, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
    private static AccountAccountingBook Mapping(Guid tenant, Guid book, Guid account, Guid classification) => new() { TenantId = tenant,
        AccountingBookId = book, AccountId = account, AccountClassificationId = classification, IsEnabled = true };
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"c4-{Guid.NewGuid():N}").Options);
    private static IAccountingBookPeriodService PeriodService(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? audit = null) =>
        new AccountingBookPeriodService(db, User(tenant, actor).Object, Workflow().Object, audit ?? Audit().Object);
    private static IAccountingBookInitializationService InitializationService(ApplicationDbContext db, Guid tenant, Guid actor, IFinanceAuditService? audit = null) =>
        new AccountingBookInitializationService(db, User(tenant, actor).Object, Workflow().Object, audit ?? Audit().Object);
    private static Mock<ICurrentUserService> User(Guid tenant, Guid actor)
    { var mock = new Mock<ICurrentUserService>(); mock.SetupGet(item => item.TenantId).Returns(tenant); mock.SetupGet(item => item.UserId).Returns(actor.ToString()); mock.SetupGet(item => item.UserName).Returns("c4.test"); return mock; }
    private static Mock<IWorkflowService> Workflow()
    { var mock = new Mock<IWorkflowService>(); mock.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
      mock.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
      mock.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
      mock.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
          .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed }); return mock; }
    private static Mock<IFinanceAuditService> Audit()
    { var mock = new Mock<IFinanceAuditService>(); mock.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog()); return mock; }
    private sealed record State(Guid TenantId, AccountingBook Book, FiscalPeriod Period, Account Debit, Account Credit);
}
