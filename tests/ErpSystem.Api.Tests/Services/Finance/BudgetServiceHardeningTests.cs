using ErpSystem.Api.Services.Finance.Budget;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public class BudgetServiceHardeningTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    [Fact]
    public async Task GetMyReturnsAsync_ReturnsOnlyAssignmentsForCurrentUser()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        db.FiscalYears.Add(new FiscalYear
        {
            Id = scenario.FiscalYearId,
            TenantId = TenantId,
            FiscalYearName = "FY Test"
        });
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.AddRange(
            CreateReturn(scenario.Id, CurrentUserId),
            CreateReturn(scenario.Id, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = (await service.GetMyReturnsAsync()).ToList();

        result.Should().ContainSingle();
        result[0].AssignedToUserId.Should().Be(CurrentUserId);
    }

    [Fact]
    public async Task GetReturnAsync_DeniesAUserWhoIsNeitherAssigneeNorBudgetSupervisor()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var budgetReturn = CreateReturn(scenario.Id, Guid.NewGuid());
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = () => service.GetReturnAsync(budgetReturn.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task BulkSaveEntriesAsync_RejectsChangesOutsideCollection()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario("Approved");
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new BulkSaveBudgetEntriesDto
        {
            BudgetReturnId = budgetReturn.Id
        };

        var act = () => service.BulkSaveEntriesAsync(request);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Collecting*");
    }

    [Fact]
    public async Task SubmitScenarioAsync_RequiresEveryReturnToBeApproved()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        db.FiscalYears.Add(new FiscalYear
        {
            Id = scenario.FiscalYearId,
            TenantId = TenantId,
            FiscalYearName = "FY Test"
        });
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(CreateReturn(scenario.Id, CurrentUserId));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = () => service.SubmitScenarioAsync(
            scenario.Id,
            Convert.ToBase64String(scenario.RowVersion));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public async Task UpdateReturnAsync_RejectsConflictingAssignAndClearRequest()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new UpdateBudgetReturnDto
        {
            AssignedToUserId = Guid.NewGuid(),
            ClearAssignedToUser = true,
            RowVersion = Convert.ToBase64String(budgetReturn.RowVersion)
        };

        var act = () => service.UpdateReturnAsync(budgetReturn.Id, request);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*set and cleared*");
    }

    [Fact]
    public async Task CreateReturnAsync_RequiresAssignPermissionWhenAnAssigneeIsProvided()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        db.BudgetScenarios.Add(scenario);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id,
            AssignedToUserId = Guid.NewGuid()
        };

        var act = () => service.CreateReturnAsync(request);

        await act.Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Assign Budget Returns*");
    }

    [Fact]
    public async Task AdoptScenarioAsync_SupersedesTheExistingOfficialBudget()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var existingOfficial = CreateScenario("Approved");
        existingOfficial.FiscalYearId = fiscalYear.Id;
        existingOfficial.Name = "Original Budget";
        existingOfficial.IsActive = true;
        var replacement = CreateScenario("Approved");
        replacement.FiscalYearId = fiscalYear.Id;
        replacement.Name = "Revised Budget";
        db.FiscalYears.Add(fiscalYear);
        db.BudgetScenarios.AddRange(existingOfficial, replacement);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.AdoptScenarioAsync(replacement.Id, new AdoptBudgetScenarioDto
        {
            RowVersion = Convert.ToBase64String(replacement.RowVersion),
            EffectiveDate = new DateTime(2026, 7, 1),
            Reason = "Board-approved mid-year revision"
        });

        result.IsActive.Should().BeTrue();
        result.Status.Should().Be("Approved");
        result.AdoptionReason.Should().Be("Board-approved mid-year revision");
        existingOfficial.IsActive.Should().BeFalse();
        existingOfficial.Status.Should().Be("Superseded");
        existingOfficial.SupersessionReason.Should().Be("Board-approved mid-year revision");
    }

    [Fact]
    public async Task GetConsolidatedViewAsync_ApprovedScopeExcludesDraftReturns()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var scenario = CreateScenario();
        scenario.FiscalYearId = fiscalYear.Id;
        var period = CreatePeriod(fiscalYear.Id);
        var account = CreateAccount(AccountType.Expense);
        var approvedReturn = CreateReturn(scenario.Id, CurrentUserId);
        approvedReturn.Status = "Approved";
        var draftReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        db.Accounts.Add(account);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.AddRange(approvedReturn, draftReturn);
        db.BudgetEntries.AddRange(
            CreateEntry(approvedReturn.Id, account.Id, period.Id, 125m),
            CreateEntry(draftReturn.Id, account.Id, period.Id, 75m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetConsolidatedViewAsync(scenario.Id, approvedOnly: true);

        result.IncludedReturnCount.Should().Be(1);
        result.TotalExpenseBudget.Should().Be(125m);
        result.Lines.Should().ContainSingle();
        result.Lines[0].Contributions.Should().ContainSingle();
        result.Lines[0].Contributions[0].BudgetReturnId.Should().Be(approvedReturn.Id);
    }

    [Fact]
    public async Task GetActiveBudgetVsActualAsync_UsesOnlyTheExplicitlyAdoptedScenario()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.Name = "Official";
        official.IsActive = true;
        var alternative = CreateScenario("Approved");
        alternative.FiscalYearId = fiscalYear.Id;
        alternative.Name = "Alternative";
        db.FiscalYears.Add(fiscalYear);
        db.BudgetScenarios.AddRange(official, alternative);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetActiveBudgetVsActualAsync(fiscalYear.Id);

        result.ScenarioId.Should().Be(official.Id);
        result.IsOfficial.Should().BeTrue();
    }

    [Fact]
    public async Task CreateRevisionAsync_RejectsAVirementThatDoesNotNetToZero()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var expense = CreateAccount(AccountType.Expense);
        var revenue = CreateAccount(AccountType.Revenue);
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.IsActive = true;
        var budgetReturn = CreateReturn(official.Id, CurrentUserId);
        budgetReturn.Status = "Approved";
        db.AddRange(fiscalYear, period, expense, revenue, official, budgetReturn);
        db.BudgetEntries.Add(CreateEntry(budgetReturn.Id, expense.Id, period.Id, 100m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = CreateRevisionRequest(official.Id, period.Id, expense.Id, revenue.Id, -25m, 20m);

        var act = () => service.CreateRevisionAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly zero*");
    }

    [Fact]
    public async Task ApplyRevisionAsync_CreatesAnImmutableOfficialSuccessorAndSupersedesSource()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var expense = CreateAccount(AccountType.Expense);
        var revenue = CreateAccount(AccountType.Revenue);
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.Name = "FY2026 Original";
        official.IsActive = true;
        official.VersionNumber = 1;
        var budgetReturn = CreateReturn(official.Id, CurrentUserId);
        budgetReturn.Status = "Approved";
        db.AddRange(fiscalYear, period, expense, revenue, official, budgetReturn);
        db.BudgetEntries.AddRange(
            CreateEntry(budgetReturn.Id, expense.Id, period.Id, 100m),
            CreateEntry(budgetReturn.Id, revenue.Id, period.Id, 50m));

        var revision = new BudgetRevision
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RevisionNumber = "BR-2026-00001",
            RevisionType = "Virement",
            SourceScenarioId = official.Id,
            EffectiveDate = new DateTime(2026, 7, 1),
            BoardResolutionReference = "TDC/BOARD/2026/047",
            BoardResolutionDate = new DateTime(2026, 6, 25),
            Justification = "Move approved funds to the higher-priority revenue activity.",
            Status = "Approved",
            SubmittedByUserId = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow,
            RowVersion = new byte[8],
            Lines = new List<BudgetRevisionLine>
            {
                CreateRevisionLine(expense.Id, period.Id, -25m),
                CreateRevisionLine(revenue.Id, period.Id, 25m)
            }
        };
        db.BudgetRevisions.Add(revision);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ApplyRevisionAsync(
            revision.Id,
            Convert.ToBase64String(revision.RowVersion));

        result.Status.Should().Be("Applied");
        result.ResultScenarioId.Should().NotBeNull();
        official.IsActive.Should().BeFalse();
        official.Status.Should().Be("Superseded");
        var successor = await db.BudgetScenarios
            .Include(item => item.BudgetReturns).ThenInclude(item => item.BudgetEntries)
            .SingleAsync(item => item.Id == result.ResultScenarioId);
        successor.IsActive.Should().BeTrue();
        successor.ParentScenarioId.Should().Be(official.Id);
        successor.VersionType.Should().Be("Virement");
        successor.VersionNumber.Should().Be(2);
        successor.BudgetReturns.Single().BudgetEntries.Sum(item => item.AmountBase).Should().Be(150m);
        successor.BudgetReturns.Single().BudgetEntries.Single(item => item.AccountId == expense.Id).AmountBase.Should().Be(75m);
        successor.BudgetReturns.Single().BudgetEntries.Single(item => item.AccountId == revenue.Id).AmountBase.Should().Be(75m);
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"budget-hardening-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, TenantId);
    }

    private BudgetService CreateService(ApplicationDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(TenantId);
        currentUser.SetupGet(service => service.UserId).Returns(CurrentUserId.ToString());

        return new BudgetService(db, currentUser.Object, Mock.Of<IWorkflowService>());
    }

    private BudgetScenario CreateScenario(string status = "Collecting") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FiscalYearId = Guid.NewGuid(),
            Name = "FY Budget",
            BaseCurrencyCode = "GHS",
            Status = status,
            RowVersion = new byte[8]
        };

    private BudgetReturn CreateReturn(Guid scenarioId, Guid assignedToUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BudgetScenarioId = scenarioId,
            AssignedToUserId = assignedToUserId,
            Status = "Draft",
            RowVersion = new byte[8]
        };

    private FiscalYear CreateFiscalYear() =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FiscalYearName = "FY2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };

    private FiscalPeriod CreatePeriod(Guid fiscalYearId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FiscalYearId = fiscalYearId,
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31)
        };

    private Account CreateAccount(AccountType accountType) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            AccountCode = accountType == AccountType.Revenue ? "4000" : "5000",
            AccountName = accountType == AccountType.Revenue ? "Revenue" : "Expense",
            AccountType = accountType
        };

    private BudgetEntry CreateEntry(
        Guid returnId,
        Guid accountId,
        Guid periodId,
        decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BudgetReturnId = returnId,
            AccountId = accountId,
            FiscalPeriodId = periodId,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Amount = amount,
            AmountBase = amount,
            RowVersion = new byte[8]
        };

    private CreateBudgetRevisionDto CreateRevisionRequest(
        Guid scenarioId,
        Guid periodId,
        Guid releaseAccountId,
        Guid increaseAccountId,
        decimal release,
        decimal increase) =>
        new()
        {
            SourceScenarioId = scenarioId,
            RevisionType = "Virement",
            EffectiveDate = new DateTime(2026, 7, 1),
            BoardResolutionReference = "TDC/BOARD/2026/047",
            BoardResolutionDate = new DateTime(2026, 6, 25),
            Justification = "Move approved funds to the higher-priority operational activity.",
            Lines = new List<BudgetRevisionLineInputDto>
            {
                new() { AccountId = releaseAccountId, FiscalPeriodId = periodId, AdjustmentAmountBase = release },
                new() { AccountId = increaseAccountId, FiscalPeriodId = periodId, AdjustmentAmountBase = increase }
            }
        };

    private BudgetRevisionLine CreateRevisionLine(Guid accountId, Guid periodId, decimal adjustment) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            AccountId = accountId,
            FiscalPeriodId = periodId,
            AdjustmentAmountBase = adjustment
        };
}
