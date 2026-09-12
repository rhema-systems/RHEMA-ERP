using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class JournalEntryLifecycleBatch5Tests
{
    [Fact]
    public async Task NoWorkflow_RequestApprovalCompletesThenNormalPostCreatesOneJournalEventWithoutHumanApproval()
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync();

        var completed = await fixture.Controller.RequestApproval(fixture.Id);
        Assert.IsType<OkObjectResult>(completed.Result);
        var saved = await fixture.Db.JournalEntries.SingleAsync(j => j.Id == fixture.Id);
        Assert.Equal("Approved", saved.PostingStatus);
        Assert.Equal("Not Required", saved.ApprovalStatus);
        Assert.False(saved.RequiresApproval);
        Assert.Null(saved.ApprovedByUserId);
        Assert.Null(saved.ApprovedDate);
        Assert.Empty(await fixture.Db.FinancePostingEvents.ToListAsync());

        var post = await fixture.Controller.PostJournalEntry(fixture.Id);
        Assert.IsType<OkObjectResult>(post);
        Assert.Equal("Posted", saved.PostingStatus);
        Assert.Null(saved.ApprovedByUserId);
        Assert.Null(saved.ApprovedDate);
        Assert.Single(await fixture.Db.FinancePostingEvents.Where(e => e.SourceDocumentId == fixture.Id).ToListAsync());
        Assert.IsType<OkObjectResult>(await fixture.Controller.PostJournalEntry(fixture.Id));
        Assert.Single(await fixture.Db.FinancePostingEvents.Where(e => e.SourceDocumentId == fixture.Id).ToListAsync());
        fixture.Workflow.Verify(w => w.GetCurrentWorkflowStepAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        fixture.Budget.Verify(b => b.ReserveManualJournalAsync(fixture.Id, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Budget.Verify(b => b.ValidateManualJournalForPostingAsync(fixture.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ActiveWorkflow_RequestApprovalRemainsPendingAndPostIsDenied()
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync(required: true);
        Assert.IsType<OkObjectResult>((await fixture.Controller.RequestApproval(fixture.Id)).Result);

        var saved = await fixture.Db.JournalEntries.SingleAsync(j => j.Id == fixture.Id);
        Assert.Equal("Pending Approval", saved.PostingStatus);
        Assert.True(saved.RequiresApproval);
        Assert.Null(saved.ApprovedByUserId);
        Assert.IsType<BadRequestObjectResult>(await fixture.Controller.PostJournalEntry(fixture.Id));
        Assert.Empty(await fixture.Db.FinancePostingEvents.ToListAsync());
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("exception")]
    [InlineData("instance")]
    [InlineData("outcome")]
    public async Task FailedOrInconsistentWorkflow_DoesNotCompleteAndReleasesReservation(string kind)
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync();
        if (kind == "exception") fixture.Integration.Setup(w => w.SubmitAsync("JournalEntry", fixture.Id))
            .ThrowsAsync(new InvalidOperationException("Workflow lookup unavailable."));
        else fixture.Integration.Setup(w => w.SubmitAsync("JournalEntry", fixture.Id)).ReturnsAsync(
            new WorkflowIntegrationResult(new WorkflowExecutionResult
            {
                Success = kind != "failure", Message = "Workflow unavailable.",
                WorkflowInstanceId = kind == "instance" ? Guid.NewGuid() : null
            }, kind == "outcome" ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, false));

        Assert.IsType<BadRequestObjectResult>((await fixture.Controller.RequestApproval(fixture.Id)).Result);

        Assert.Equal("Draft", (await fixture.Db.JournalEntries.SingleAsync(j => j.Id == fixture.Id)).PostingStatus);
        Assert.Empty(await fixture.Db.FinancePostingEvents.ToListAsync());
        fixture.Budget.Verify(b => b.ReleaseManualJournalAsync(fixture.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("in-flight")]
    [InlineData("human")]
    [InlineData("workflow-reference")]
    public async Task DirectCompletionCannotReplaceRequiredApprovalOrHistory(string blocker)
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync();
        var saved = await fixture.Db.JournalEntries.SingleAsync(j => j.Id == fixture.Id);
        saved.RequiresApproval = true;
        if (blocker == "active") fixture.Workflow.Setup(w => w.HasActiveApprovalWorkflowAsync("JournalEntry")).ReturnsAsync(true);
        if (blocker == "in-flight") fixture.Workflow.Setup(w => w.HasActiveApprovalInstanceAsync("JournalEntry", fixture.Id)).ReturnsAsync(true);
        if (blocker == "human") saved.ApprovedByUserId = Guid.NewGuid();
        if (blocker == "workflow-reference") saved.ApprovalWorkflowId = "existing-recorded-instance";
        await fixture.Db.SaveChangesAsync();

        Assert.IsType<BadRequestObjectResult>((await fixture.Controller.RequestApproval(fixture.Id)).Result);

        Assert.Equal("Draft", saved.PostingStatus);
        Assert.True(saved.RequiresApproval);
        Assert.Empty(await fixture.Db.FinancePostingEvents.ToListAsync());
    }

    [Theory]
    [InlineData("unbalanced")]
    [InlineData("inactive-account")]
    [InlineData("closed-period")]
    [InlineData("human-approval")]
    public async Task NoWorkflow_DirectCompletionStillEnforcesFinanceRulesWhenPosting(string invalid)
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync();
        Assert.IsType<OkObjectResult>((await fixture.Controller.RequestApproval(fixture.Id)).Result);
        var saved = await fixture.Db.JournalEntries.Include(j => j.Transactions).SingleAsync(j => j.Id == fixture.Id);
        if (invalid == "unbalanced") saved.Transactions.First(t => t.DebitAmount > 0).DebitAmount += 1;
        if (invalid == "inactive-account") (await fixture.Db.Accounts.FirstAsync()).Status = AccountStatus.Inactive;
        if (invalid == "closed-period")
        {
            var period = await fixture.Db.FiscalPeriods.SingleAsync();
            period.IsClosed = true; period.IsOpen = false; period.PeriodStatus = "Closed";
        }
        if (invalid == "human-approval") saved.ApprovedByUserId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();

        Assert.IsType<BadRequestObjectResult>(await fixture.Controller.PostJournalEntry(fixture.Id));

        Assert.Equal("Approved", saved.PostingStatus);
        Assert.Empty(await fixture.Db.FinancePostingEvents.ToListAsync());
    }

    [Fact]
    public async Task DirectCompletionBudgetFailureDoesNotMutateApprovalModeOrClaimCompletion()
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync();
        var saved = await fixture.Db.JournalEntries.SingleAsync(j => j.Id == fixture.Id);
        saved.RequiresApproval = true;
        await fixture.Db.SaveChangesAsync();
        fixture.Budget.Setup(b => b.ValidateManualJournalForPostingAsync(fixture.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Budget commitment is stale."));

        Assert.IsType<BadRequestObjectResult>((await fixture.Controller.RequestApproval(fixture.Id)).Result);

        Assert.True(saved.RequiresApproval);
        Assert.Equal("Draft", saved.PostingStatus);
        Assert.False(fixture.Db.ChangeTracker.HasChanges());
        fixture.Budget.Verify(b => b.ReleaseManualJournalAsync(fixture.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoWorkflow_DoesNotGrantSubmitOrPostPermissions()
    {
        await using var fixture = await OptionalJournalFixture.CreateAsync(grantPermissions: false);
        Assert.IsType<ForbidResult>((await fixture.Controller.RequestApproval(fixture.Id)).Result);
        Assert.IsType<ForbidResult>(await fixture.Controller.PostJournalEntry(fixture.Id));
        fixture.Integration.Verify(w => w.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        fixture.Budget.Verify(b => b.ReserveManualJournalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class OptionalJournalFixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = CreateContext();
        public Mock<IWorkflowService> Workflow { get; } = new();
        public Mock<IWorkflowIntegrationService> Integration { get; } = new();
        public Mock<IFinanceBudgetControlService> Budget { get; } = new();
        public JournalEntryController Controller { get; private set; } = null!;
        public Guid Id { get; private set; }

        public static async Task<OptionalJournalFixture> CreateAsync(bool required = false, bool grantPermissions = true)
        {
            var fixture = new OptionalJournalFixture();
            var tenant = Guid.NewGuid();
            var user = Guid.NewGuid();
            var (debit, credit) = await SeedTenantPeriodAndAccountsAsync(fixture.Db, tenant);
            fixture.Budget.Setup(b => b.ReserveManualJournalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            fixture.Budget.Setup(b => b.ValidateManualJournalForPostingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            var service = CreateJournalService(fixture.Db, tenant, user,
                budgetControl: fixture.Budget.Object, approvalWorkflow: fixture.Workflow.Object);
            fixture.Id = (await service.CreateJournalEntryAsync(CreateJournalDto(debit.Id, credit.Id))).Id;
            fixture.Integration.Setup(w => w.SubmitAsync("JournalEntry", fixture.Id)).ReturnsAsync(
                new WorkflowIntegrationResult(new WorkflowExecutionResult
                {
                    Success = true, WorkflowInstanceId = required ? Guid.NewGuid() : null
                }, required ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, required));
            fixture.Workflow.Setup(w => w.HasActiveApprovalWorkflowAsync("JournalEntry")).ReturnsAsync(required);
            fixture.Workflow.Setup(w => w.GetCurrentWorkflowStepAsync("JournalEntry", fixture.Id))
                .ReturnsAsync(new WorkflowStepInfo { StepName = "Finance approval" });
            if (grantPermissions)
            {
                var role = new ApplicationRole("Journal operator") { Id = Guid.NewGuid(), NormalizedName = "JOURNAL OPERATOR" };
                fixture.Db.Roles.Add(role);
                fixture.Db.UserRoles.Add(new ApplicationUserRole { UserId = user, RoleId = role.Id });
                foreach (var name in new[] { "Finance.JournalEntries.SubmitForApproval", "Finance.JournalEntries.Post" })
                {
                    var permission = new Permission { Id = Guid.NewGuid(), Name = name, DisplayName = name, Category = "Finance" };
                    fixture.Db.Permissions.Add(permission);
                    fixture.Db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, GrantedBy = "Tests" });
                }
                await fixture.Db.SaveChangesAsync();
            }
            fixture.Controller = new JournalEntryController(service, Mock.Of<IGeneralLedgerService>(), fixture.Workflow.Object,
                CreateCurrentUser(tenant, user).Object, Mock.Of<IFinanceAuditService>(), fixture.Db, fixture.Budget.Object, fixture.Integration.Object);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
