using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.UnitAccounting;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class AllocationRunBatchServiceTests
{
    [Fact]
    [Trait("Batch", "UnitAccounting")]
    [Trait("Category", "Workflow")]
    public async Task CreateRunBatchAsync_ShouldPersistCalculatedSnapshotAndBlockDuplicatePeriodRun()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateRunBatchAsync(CreateRunRequest(fixture));

        batch.Status.Should().Be("Draft");
        batch.SourcePeriodBalance.Should().Be(1000m);
        batch.TotalAllocated.Should().Be(1000m);
        batch.AllocationDate.Should().Be(fixture.Period.EndDate.Date);
        batch.Lines.Should().HaveCount(2);
        batch.Lines.Select(line => line.AllocatedAmount).Should().BeEquivalentTo(new[] { 600m, 400m });

        var persisted = await db.AllocationRunBatches
            .Include(item => item.Lines)
            .SingleAsync(item => item.Id == batch.Id);
        persisted.Status.Should().Be(AllocationRunBatchStatus.Draft);
        persisted.Lines.Should().HaveCount(2);

        var duplicate = () => service.CreateRunBatchAsync(CreateRunRequest(fixture));
        await duplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    [Trait("Batch", "UnitAccounting")]
    [Trait("Category", "Workflow")]
    public async Task SubmitRunBatchAsync_ShouldStartWorkflowAndPreventPostingBeforeApproval()
    {
        var tenantId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow(workflowInstanceId: workflowInstanceId);
        var service = CreateService(db, tenantId, workflow.Object);

        var created = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        var submitted = await service.SubmitRunBatchAsync(created.Id, "Ready for review");
        var post = () => service.PostRunBatchAsync(created.Id);

        submitted.Status.Should().Be("PendingApproval");
        submitted.WorkflowInstanceId.Should().Be(workflowInstanceId);
        submitted.SubmittedByName.Should().Be("allocation.tester");
        await post.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only approved allocation run batches can be posted.");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        workflow.Verify(
            item => item.StartApprovalWorkflowAsync("AllocationRunBatch", created.Id),
            Times.Once);
    }

    [Fact]
    [Trait("Batch", "UnitAccounting")]
    [Trait("Category", "Workflow")]
    public async Task ApproveRunBatchAsync_ShouldReleaseForPostingOnlyWhenWorkflowCompletes()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.SetupSequence(item => item.ProcessApprovalStepAsync(
                "AllocationRunBatch",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                "Approve",
                It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress
            })
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed
            });
        var service = CreateService(db, tenantId, workflow.Object);

        var created = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        await service.SubmitRunBatchAsync(created.Id);
        var firstApproval = await service.ApproveRunBatchAsync(created.Id, "First approval");
        var finalApproval = await service.ApproveRunBatchAsync(created.Id, "Final approval");

        firstApproval.Status.Should().Be("PendingApproval");
        firstApproval.ApprovedAt.Should().BeNull();
        finalApproval.Status.Should().Be("Approved");
        finalApproval.ApprovedByName.Should().Be("allocation.tester");
    }

    [Fact]
    [Trait("Batch", "UnitAccounting")]
    [Trait("Category", "PostingEngine")]
    public async Task PostRunBatchAsync_ShouldPostApprovedSnapshotThroughFinancePostingEngineIdempotently()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow(approvalStatus: WorkflowInstanceStatus.Completed);
        var service = CreateService(db, tenantId, workflow.Object);

        var created = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        await service.SubmitRunBatchAsync(created.Id);
        await service.ApproveRunBatchAsync(created.Id, "Approved");

        var firstPost = await service.PostRunBatchAsync(created.Id);
        var secondPost = await service.PostRunBatchAsync(created.Id);

        firstPost.Status.Should().Be("Posted");
        firstPost.JournalEntryId.Should().NotBeNull();
        firstPost.JournalEntryNumber.Should().NotBeNullOrWhiteSpace();
        secondPost.JournalEntryId.Should().Be(firstPost.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "AllocationRunBatch")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "AllocationRunBatch")).Should().Be(1);
        (await db.AccountTransactions.CountAsync(t => t.SourceDocumentType == "AllocationRunBatch")).Should().Be(3);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == firstPost.JournalEntryId!.Value);
        journal.TotalDebitAmount.Should().Be(1000m);
        journal.TotalCreditAmount.Should().Be(1000m);
        journal.Transactions.Where(t => t.AccountId == fixture.SourceAccount.Id).Single().CreditAmount.Should().Be(1000m);
        journal.Transactions.Where(t => t.AccountId == fixture.TargetOne.Id).Single().DebitAmount.Should().Be(600m);
        journal.Transactions.Where(t => t.AccountId == fixture.TargetTwo.Id).Single().DebitAmount.Should().Be(400m);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e => e.SourceDocumentId == created.Id);
        postingEvent.IdempotencyKey.Should().Be(firstPost.Lines.Count > 0
            ? (await db.AllocationRunBatches.SingleAsync(b => b.Id == created.Id)).IdempotencyKey
            : null);
        (await db.AllocationRules.SingleAsync(rule => rule.Id == fixture.Rule.Id)).LastRunDate.Should().NotBeNull();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"allocation-run-batches-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static AllocationService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IWorkflowService? workflow = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>());

        return new AllocationService(
            new UnitOfWork(db),
            currentUser.Object,
            workflow ?? CreateWorkflow().Object,
            postingEngine,
            Mock.Of<ILogger<AllocationService>>());
    }

    private static Mock<IWorkflowService> CreateWorkflow(
        Guid? workflowInstanceId = null,
        WorkflowInstanceStatus approvalStatus = WorkflowInstanceStatus.Completed)
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch")).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync("AllocationRunBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = workflowInstanceId ?? Guid.NewGuid()
            });
        workflow.Setup(item => item.CanUserApproveAsync("AllocationRunBatch", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(item => item.ProcessApprovalStepAsync(
                "AllocationRunBatch",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                "Approve",
                It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = approvalStatus
            });
        return workflow;
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("allocation.tester");
        currentUser.SetupGet(item => item.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(item => item.UserAgent).Returns("allocation-run-batch-tests");
        return currentUser;
    }

    private static CreateAllocationRunBatchDto CreateRunRequest(AllocationFixture fixture)
        => new(fixture.Rule.Id, fixture.Period.Id, fixture.Period.EndDate, "Allocate shared rent");

    private static AllocationFixture SeedAllocationFixture(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var source = SeedAccount(db, tenantId, "6100", "Shared Rent", AccountType.Expense);
        var targetOne = SeedAccount(db, tenantId, "6110", "Rent - Sales", AccountType.Expense);
        var targetTwo = SeedAccount(db, tenantId, "6120", "Rent - Admin", AccountType.Expense);
        db.Set<AccountBalance>().Add(new AccountBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = source.Id,
            FiscalPeriodId = period.Id,
            BookClassification = "IFRS",
            Currency = "GHS",
            ClosingBalance = 1000m,
            ClosingBalanceType = "DR"
        });

        var rule = new AllocationRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "ALLOC-RENT",
            Name = "Shared rent allocation",
            SourceAccountId = source.Id,
            SourceAccount = source,
            AllocationType = AllocationType.FixedPercentage,
            IsActive = true,
            Targets =
            {
                new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TargetAccountId = targetOne.Id,
                    TargetAccount = targetOne,
                    FixedPercentage = 60m
                },
                new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TargetAccountId = targetTwo.Id,
                    TargetAccount = targetTwo,
                    FixedPercentage = 40m
                }
            }
        };
        db.AllocationRules.Add(rule);
        return new AllocationFixture(period, source, targetOne, targetTwo, rule);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Allocation Tenant",
            Code = "ALC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ReferenceNumber = "FIN-SETTINGS",
            Status = "Active"
        });
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        string accountName,
        AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = accountName,
            AccountType = accountType,
            CurrencyCode = "GHS",
            Status = AccountStatus.Active,
            AllowDirectPosting = true
        };
        db.Accounts.Add(account);
        return account;
    }

    private sealed record AllocationFixture(
        FiscalPeriod Period,
        Account SourceAccount,
        Account TargetOne,
        Account TargetTwo,
        AllocationRule Rule);
}
