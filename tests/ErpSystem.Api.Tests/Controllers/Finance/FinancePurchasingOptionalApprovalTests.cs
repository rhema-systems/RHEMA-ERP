using System.Data;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

// Actual controller + tracked EF aggregate + actual receipt posting adapter tests.
// Only workflow/Finance owners and the transaction boundary are mocked. InMemory
// cannot prove SQL rollback or trigger semantics; those require separate SQL verification.
public sealed class FinancePurchasingOptionalApprovalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task NoWorkflow_PoCompletesFromEditableState_WithoutCreatingApproval(int originalStatus)
    {
        await using var f = await Fixture.CreateAsync();
        f.Order.Status = originalStatus;
        await f.Db.SaveChangesAsync();

        var response = await f.PoController().SubmitForApproval(f.Order.Id);

        var dto = response.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<FinancePurchaseOrderDto>().Subject;
        dto.Status.Should().Be(2);
        dto.ApprovalRequired.Should().BeFalse();
        f.Order.LastModifiedById.Should().Be(f.ActorId);
        f.AssertCommitted();
        f.Engine.Verify(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveDefinitionOrRetainedInstance_PoRemainsPending(bool retainedInstance)
    {
        await using var f = await Fixture.CreateAsync();
        f.RequireApproval(retainedInstance);

        var response = await f.PoController().SubmitForApproval(f.Order.Id);

        response.Result.Should().BeOfType<OkObjectResult>();
        f.Order.Status.Should().Be(9);
        f.Order.ApprovalRequired.Should().BeTrue();
        f.AssertCommitted();
        if (retainedInstance)
            f.Workflow.Verify(x => x.HasActiveApprovalWorkflowAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(9)]
    public async Task PoCannotBypassSubmittedOrCompletedState(int status)
    {
        await using var f = await Fixture.CreateAsync();
        f.Order.Status = status;
        await f.Db.SaveChangesAsync();
        (await f.PoController().SubmitForApproval(f.Order.Id)).Result.Should().BeOfType<BadRequestObjectResult>();
        f.Workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        f.Order.ApprovalRequired.Should().BeTrue();
    }

    [Fact]
    public async Task NoWorkflow_ReceiptPostsBalancedSourceThroughFinanceOwner_WithNoHumanApprover()
    {
        await using var f = await Fixture.CreateAsync();
        FinancePostingRequestDto? posted = null;
        f.Engine.Setup(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) =>
            {
                f.Transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
                f.Receipt.ApprovalRequired.Should().BeFalse();
                f.Receipt.ApprovedById.Should().BeNull();
                f.Receipt.ApprovedAt.Should().BeNull();
                posted = request;
            }).ReturnsAsync(new FinancePostingResultDto());

        var response = await f.ReceiptController().SubmitForApproval(f.Receipt.Id);

        var dto = response.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<FinancePurchaseOrderReceiptDto>().Subject;
        dto.ApprovalRequired.Should().BeFalse();
        f.Receipt.Status.Should().Be(FinancePurchaseOrderReceiptStatus.Approved);
        f.Receipt.SubmittedById.Should().Be(f.ActorId);
        f.Receipt.WorkflowInstanceId.Should().BeNull();
        posted.Should().NotBeNull();
        posted!.SourceDocumentId.Should().Be(f.Receipt.Id);
        posted.SourceDocumentTenantId.Should().Be(f.TenantId);
        posted.SourceDocumentType.Should().Be("FinancePurchaseOrderReceipt");
        posted.IdempotencyKey.Should().Be($"FinancePurchaseOrderReceipt:{f.TenantId:N}:{f.Receipt.Id:N}:Post");
        posted.Lines.Should().HaveCount(2);
        posted.Lines.Single(x => x.AccountId == f.ExpenseId).DebitAmount.Should().Be(100m);
        posted.Lines.Single(x => x.AccountId == f.AccrualId).CreditAmount.Should().Be(100m);
        f.AssertCommitted();

        // A replay of Submit cannot post a second time.
        (await f.ReceiptController().SubmitForApproval(f.Receipt.Id)).Result.Should().BeOfType<BadRequestObjectResult>();
        f.Engine.Verify(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Receipt_ActiveDefinitionOrRetainedInstance_StaysPendingWithoutPosting(bool retainedInstance)
    {
        await using var f = await Fixture.CreateAsync();
        f.RequireApproval(retainedInstance);
        (await f.ReceiptController().SubmitForApproval(f.Receipt.Id)).Result.Should().BeOfType<OkObjectResult>();
        f.Receipt.Status.Should().Be(FinancePurchaseOrderReceiptStatus.PendingApproval);
        f.Receipt.ApprovalRequired.Should().BeTrue();
        f.Receipt.WorkflowInstanceId.Should().Be(f.InstanceId);
        f.Receipt.ApprovedById.Should().BeNull();
        f.Engine.Verify(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        f.AssertCommitted();
    }

    [Theory]
    [InlineData(false, "lookup")]
    [InlineData(true, "lookup")]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    [InlineData(false, "instance")]
    [InlineData(true, "instance")]
    [InlineData(false, "pending")]
    [InlineData(true, "pending")]
    [InlineData(false, "policy-drift")]
    [InlineData(true, "policy-drift")]
    public async Task WorkflowLookupFailureOrInconsistentResult_FailsClosed(bool receipt, string error)
    {
        await using var f = await Fixture.CreateAsync();
        if (error == "lookup")
            f.Workflow.Setup(x => x.HasActiveApprovalWorkflowAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Workflow lookup unavailable"));
        else
            f.Workflow.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(
                new WorkflowIntegrationResult(new WorkflowExecutionResult
                {
                    Success = error != "failure",
                    Status = error == "pending" ? WorkflowInstanceStatus.InProgress : WorkflowInstanceStatus.Completed,
                    WorkflowInstanceId = error == "instance" ? f.InstanceId : null
                }, error == "pending" ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, error == "policy-drift"));

        Func<Task> action = async () =>
        {
            if (receipt) await f.ReceiptController().SubmitForApproval(f.Receipt.Id);
            else await f.PoController().SubmitForApproval(f.Order.Id);
        };
        await action.Should().ThrowAsync<InvalidOperationException>();
        f.AssertRolledBack();
        f.Db.ChangeTracker.Entries().Should().BeEmpty();
        f.Engine.Verify(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("finance")]
    [InlineData("mapping")]
    [InlineData("human")]
    public async Task DirectReceipt_RetainsFinanceAndHistoricalMetadataGuards(string error)
    {
        await using var f = await Fixture.CreateAsync();
        if (error == "finance")
            f.Engine.Setup(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Posting period is closed"));
        else if (error == "mapping")
            (await f.Db.FinanceSettings.SingleAsync()).ControlAccountGRVAccrualId = null;
        else
            f.Receipt.ApprovedById = Guid.NewGuid();
        await f.Db.SaveChangesAsync();

        Func<Task> action = async () => await f.ReceiptController().SubmitForApproval(f.Receipt.Id);
        await action.Should().ThrowAsync<InvalidOperationException>();
        f.AssertRolledBack();
        if (error != "finance")
            f.Engine.Verify(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DirectReceipt_RequiresAuthenticatedActorAndTenantOwnedRecord()
    {
        await using var f = await Fixture.CreateAsync();
        f.User.SetupGet(x => x.UserId).Returns((string?)null);
        (await f.ReceiptController().SubmitForApproval(f.Receipt.Id)).Result.Should().BeOfType<UnauthorizedObjectResult>();
        f.User.SetupGet(x => x.UserId).Returns(f.ActorId.ToString());
        f.User.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        (await f.ReceiptController().SubmitForApproval(f.Receipt.Id)).Result.Should().BeOfType<NotFoundResult>();
        (await f.PoController().SubmitForApproval(f.Order.Id)).Result.Should().BeOfType<NotFoundResult>();
        f.Workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid InstanceId { get; } = Guid.NewGuid();
        public Guid ExpenseId { get; } = Guid.NewGuid();
        public Guid AccrualId { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; private set; } = null!;
        public FinancePurchaseOrder Order { get; private set; } = null!;
        public FinancePurchaseOrderReceipt Receipt { get; private set; } = null!;
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        public Mock<IFinancePostingEngine> Engine { get; } = new();
        public Mock<IDbContextTransaction> Transaction { get; } = new();
        // Share the expensive application model, while the scoped manager still
        // belongs to one DbContext/test. A provider per test recompiles the full
        // ERP model and makes this focused suite unnecessarily slow.
        private static readonly ServiceProvider Provider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddScoped<IDbContextTransactionManager>(_ =>
            {
                var manager = new Mock<IDbContextTransactionManager>();
                manager.As<IRelationalTransactionManager>();
                return manager.Object;
            }).BuildServiceProvider();

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            f.User.SetupGet(x => x.TenantId).Returns(f.TenantId);
            f.User.SetupGet(x => x.UserId).Returns(f.ActorId.ToString());
            f.User.SetupGet(x => x.UserName).Returns("finance.operator");
            f.User.SetupGet(x => x.IsAuthenticated).Returns(true);
            f.Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-optional-{Guid.NewGuid()}")
                .UseInternalServiceProvider(Provider).Options);
            var manager = Mock.Get(f.Db.GetService<IDbContextTransactionManager>());
            manager.As<IRelationalTransactionManager>()
                .Setup(x => x.BeginTransactionAsync(IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                .ReturnsAsync(f.Transaction.Object);
            manager.SetupGet(x => x.CurrentTransaction).Returns(f.Transaction.Object);
            f.Transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            f.Transaction.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            f.Transaction.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
            f.Workflow.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(
                new WorkflowIntegrationResult(new WorkflowExecutionResult
                { Success = true, Status = WorkflowInstanceStatus.Completed }, WorkflowOutcome.Approved, false));
            f.Engine.Setup(x => x.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FinancePostingResultDto());
            var vendor = new BusinessPartner { Id = Guid.NewGuid(), TenantId = f.TenantId,
                PartnerCode = "SUP-TEST", PartnerName = "Test supplier" };
            f.Order = new FinancePurchaseOrder { Id = Guid.NewGuid(), TenantId = f.TenantId,
                OrderNumber = "FPO-TEST", VendorId = vendor.Id, Vendor = vendor, TotalAmount = 100m };
            var line = new FinancePurchaseOrderItem { Id = Guid.NewGuid(), TenantId = f.TenantId,
                FinancePurchaseOrder = f.Order, FinancePurchaseOrderId = f.Order.Id, LineType = 2,
                Description = "Service", GlAccountId = f.ExpenseId, OrderedQuantity = 2m,
                ReceivedQuantity = 2m, UnitPrice = 50m, LineTotal = 100m };
            f.Order.Items.Add(line);
            f.Receipt = new FinancePurchaseOrderReceipt { Id = Guid.NewGuid(), TenantId = f.TenantId,
                FinancePurchaseOrder = f.Order, FinancePurchaseOrderId = f.Order.Id, ReceiptNumber = "FGRV-TEST" };
            f.Receipt.Items.Add(new FinancePurchaseOrderReceiptItem { Id = Guid.NewGuid(), TenantId = f.TenantId,
                FinancePurchaseOrderReceipt = f.Receipt, FinancePurchaseOrderReceiptId = f.Receipt.Id,
                FinancePurchaseOrderItem = line, FinancePurchaseOrderItemId = line.Id, QuantityReceived = 2m });
            f.Db.FinancePurchaseOrderReceipts.Add(f.Receipt);
            f.Db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = f.TenantId,
                BaseCurrency = "GHS", ControlAccountGRVAccrualId = f.AccrualId });
            await f.Db.SaveChangesAsync();
            return f;
        }

        public void RequireApproval(bool retained)
        {
            Workflow.Setup(x => x.HasActiveApprovalInstanceAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(retained);
            Workflow.Setup(x => x.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(!retained);
            Workflow.Setup(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(
                new WorkflowIntegrationResult(new WorkflowExecutionResult
                { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = InstanceId }, WorkflowOutcome.Pending));
        }

        public FinancePurchaseOrderController PoController() => new(Db, User.Object,
            Mock.Of<INotificationService>(), Mock.Of<IWorkflowService>(), Mock.Of<IDocumentNumberingService>(),
            NullLogger<FinancePurchaseOrderController>.Instance, Workflow.Object);

        public FinancePurchaseOrderReceiptController ReceiptController() => new(Db, User.Object,
            Mock.Of<IDocumentNumberingService>(), Mock.Of<IWorkflowService>(),
            new FinancePurchaseOrderReceiptPostingService(Db, User.Object, Engine.Object),
            NullLogger<FinancePurchaseOrderReceiptController>.Instance, Workflow.Object);

        public void AssertCommitted()
        {
            Transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            Transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        public void AssertRolledBack()
        {
            Transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            Transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
        }
    }
}
