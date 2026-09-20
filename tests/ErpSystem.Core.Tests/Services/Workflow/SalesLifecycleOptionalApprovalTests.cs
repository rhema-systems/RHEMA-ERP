using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class SalesLifecycleOptionalApprovalTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Mock<ICurrentUserProvider> _user = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IWorkflowIntegrationService> _workflow = new();
    private readonly Mock<IGenericRepository<SalesOrder>> _orders = new();
    private readonly Mock<IGenericRepository<BusinessPartner>> _partners = new();
    private readonly Mock<IGenericRepository<SalesOrderStatusHistory>> _history = new();
    private readonly IWorkflowStatusAdapterRegistry _adapters = new WorkflowStatusAdapterRegistry([
        new SalesOrderWorkflowStatusAdapter(), new SalesAgreementWorkflowStatusAdapter(), new SalesAllocationWorkflowStatusAdapter()
    ]);

    public SalesLifecycleOptionalApprovalTests()
    {
        _user.SetupGet(x => x.UserId).Returns(_actor);
        _user.SetupGet(x => x.TenantId).Returns(_tenant);
        _user.SetupGet(x => x.Username).Returns("sales.user");
    }

    [Theory]
    [InlineData("SalesOrder")]
    [InlineData("SalesAgreement")]
    [InlineData("SalesAllocation")]
    public void Direct_result_completes_normal_lifecycle_without_human_approval(string type)
    {
        var entity = NewEntity(type);
        _adapters.GetAdapter(type).ApplySubmitOutcome(entity, Direct(), _actor);
        Status(entity).Should().Be(type switch { "SalesOrder" => "Confirmed", "SalesAgreement" => "Active", _ => "Allocated" });
        entity.GetType().GetProperty("ApprovedById")?.GetValue(entity).Should().BeNull();
        entity.GetType().GetProperty("ApprovedDate")?.GetValue(entity).Should().BeNull();
        if (entity is SalesOrder order) order.SubmittedById.Should().Be(_actor);
    }

    [Theory]
    [InlineData("SalesOrder")]
    [InlineData("SalesAgreement")]
    [InlineData("SalesAllocation")]
    public void Active_process_still_waits_for_its_actual_approval(string type)
    {
        var entity = NewEntity(type);
        _adapters.GetAdapter(type).ApplySubmitOutcome(entity, Pending(), _actor);
        Status(entity).Should().Be("PendingApproval");
        entity.GetType().GetProperty("ApprovedById")?.GetValue(entity).Should().BeNull();
    }

    [Theory]
    [InlineData("SalesOrder")]
    [InlineData("SalesAgreement")]
    [InlineData("SalesAllocation")]
    public void Failed_or_inflight_result_cannot_be_treated_as_direct(string type)
    {
        var entity = NewEntity(type);
        var before = Status(entity);
        var malformed = new WorkflowIntegrationResult(Pending().ExecutionResult, WorkflowOutcome.Pending, approvalRequired: false);
        var act = () => _adapters.GetAdapter(type).ApplySubmitOutcome(entity, malformed, _actor);
        act.Should().Throw<InvalidOperationException>();
        Status(entity).Should().Be(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_order_confirm_uses_the_same_central_optional_submission(bool required)
    {
        var order = Order();
        _workflow.Setup(x => x.SubmitAsync("SalesOrder", order.Id)).ReturnsAsync(required ? Pending() : Direct());
        var result = await OrderService().ConfirmSalesOrderAsync(order.Id);
        result.OrderStatus.Should().Be(required ? SalesOrderStatus.PendingApproval : SalesOrderStatus.Confirmed);
        order.ApprovedById.Should().BeNull();
        order.ApprovedDate.Should().BeNull();
        _workflow.Verify(x => x.SubmitAsync("SalesOrder", order.Id), Times.Once);
        _history.Verify(x => x.AddAsync(It.Is<SalesOrderStatusHistory>(h => h.Notes != null &&
            h.Notes.Contains(required ? "Submitted for approval" : "approval not required"))), Times.Once);
    }

    [Fact]
    public async Task Legacy_order_confirm_cannot_skip_an_inflight_approval()
    {
        var order = Order(SalesOrderStatus.PendingApproval);
        var act = () => OrderService().ConfirmSalesOrderAsync(order.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*existing approval*");
        order.OrderStatus.Should().Be(SalesOrderStatus.PendingApproval);
        _workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("limit")]
    [InlineData("hold-without-limit")]
    [InlineData("foreign-customer")]
    public async Task Direct_order_submission_does_not_skip_credit_or_tenant_validation(string reason)
    {
        var order = Order();
        _partners.Setup(x => x.GetByIdAsync(order.BusinessPartnerId)).ReturnsAsync(new BusinessPartner {
            Id = order.BusinessPartnerId, TenantId = reason == "foreign-customer" ? Guid.NewGuid() : _tenant,
            CreditLimit = reason == "hold-without-limit" ? null : 10m, IsOnCreditHold = reason == "hold-without-limit"
        });
        var act = () => OrderService().SubmitForApprovalAsync(order.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*credit*");
        order.OrderStatus.Should().Be(SalesOrderStatus.Draft);
        _workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Order_workflow_failure_does_not_fall_back_to_confirmation()
    {
        var order = Order();
        _workflow.Setup(x => x.SubmitAsync("SalesOrder", order.Id)).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = false, Message = "No eligible approver" }, WorkflowOutcome.Pending));
        var act = () => OrderService().SubmitForApprovalAsync(order.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No eligible approver");
        order.OrderStatus.Should().Be(SalesOrderStatus.Draft);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Allocated")]
    [InlineData("Sold")]
    [InlineData("Leased")]
    [InlineData("Approved")]
    [InlineData("PendingApproval")]
    public async Task Manual_allocation_status_cannot_skip_central_submission(string requestedStatus)
    {
        var allocation = new SalesAllocation { Id = Guid.NewGuid(), TenantId = _tenant, Status = "Reserved" };
        var repository = new Mock<IGenericRepository<SalesAllocation>>();
        repository.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<SalesAllocation, bool>>>()))
            .ReturnsAsync((Expression<Func<SalesAllocation, bool>> p) => p.Compile()(allocation) ? allocation : null);
        _uow.Setup(x => x.Repository<SalesAllocation>()).Returns(repository.Object);
        var service = new SalesAllocationService(_uow.Object, _user.Object, _workflow.Object, _adapters,
            NullLogger<SalesAllocationService>.Instance);
        var act = () => service.UpdateAllocationStatusAsync(allocation.Id, new UpdateSalesAllocationStatusDto { Status = requestedStatus });
        await act.Should().ThrowAsync<InvalidOperationException>();
        allocation.Status.Should().Be("Reserved");
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private SalesOrder Order(SalesOrderStatus status = SalesOrderStatus.Draft)
    {
        var order = new SalesOrder { Id = Guid.NewGuid(), TenantId = _tenant, OrderStatus = status,
            DocumentNumber = "SO-OPTIONAL", BusinessPartnerId = Guid.NewGuid(), TotalAmount = 100m };
        _orders.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<Expression<Func<SalesOrder, object>>[]>())).ReturnsAsync(order);
        _partners.Setup(x => x.GetByIdAsync(order.BusinessPartnerId)).ReturnsAsync(new BusinessPartner {
            Id = order.BusinessPartnerId, TenantId = _tenant, CreditLimit = 1000m
        });
        return order;
    }

    private SalesOrderService OrderService() => new(_orders.Object, Mock.Of<IGenericRepository<SalesOrderLine>>(),
        _history.Object, _partners.Object, Mock.Of<IGenericRepository<Quote>>(), Mock.Of<IGenericRepository<PaymentTerm>>(),
        _uow.Object, _user.Object, Mock.Of<IDocumentNumberingService>(), _workflow.Object, _adapters,
        NullLogger<SalesOrderService>.Instance);

    private static WorkflowIntegrationResult Direct() => new(new WorkflowExecutionResult {
        Success = true, Status = WorkflowInstanceStatus.Completed
    }, WorkflowOutcome.Approved, approvalRequired: false);
    private static WorkflowIntegrationResult Pending() => new(new WorkflowExecutionResult {
        Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid()
    }, WorkflowOutcome.Pending);
    private static object NewEntity(string type) => type switch {
        "SalesOrder" => new SalesOrder { OrderStatus = SalesOrderStatus.Draft },
        "SalesAgreement" => new SalesAgreement { AgreementStatus = SalesAgreementStatus.Draft },
        _ => new SalesAllocation { Status = "Reserved" }
    };
    private static string Status(object value) => value switch {
        SalesOrder order => order.OrderStatus.ToString(), SalesAgreement agreement => agreement.AgreementStatus.ToString(),
        SalesAllocation allocation => allocation.Status, _ => throw new ArgumentException()
    };
}
