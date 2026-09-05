using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class PurchaseOrdersSodControllerTests
{
    [Fact]
    public void MultiPoProjectionKeepsImmutableHistorySequenceWhileSummaryAdvances()
    {
        var resolver = typeof(PurchaseOrdersController).GetMethod(
            "ResolveHistoryReservationSequence",
            BindingFlags.Static | BindingFlags.NonPublic);

        resolver.Should().NotBeNull();
        resolver!.Invoke(null, ["{\"reservationSequence\":1}", 3])
            .Should().Be(1,
                "an earlier PO history row must retain its reservation event sequence");
        resolver.Invoke(null, [null, 3]).Should().Be(3,
            "legacy events without a sequence must fall back to the current summary sequence");
    }

    [Fact]
    public void DedicatedReadinessRouteIsAuthenticatedAndTenantServiceBacked()
    {
        var controller = typeof(PurchaseOrdersController);
        var method = controller.GetMethod(
            "GetSodReadiness",
            BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should()
            .Be("{id}/sod-readiness");
        controller.GetCustomAttributes()
            .Select(attribute => attribute.GetType().Name)
            .Should().Contain("AuthorizeAttribute");
    }

    [Fact]
    public void ApprovalSubmissionAndReceiptRemainSeparateServerCommands()
    {
        var methods = typeof(PurchaseOrdersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .ToList();

        methods.Should().Contain([
            "ApprovePurchaseOrder",
            "SubmitPurchaseOrder",
            "ReceivePurchaseOrder",
            "UpdatePurchaseOrderStatus"
        ]);
    }

    [Theory]
    [InlineData("Pending Approval", true)]
    [InlineData("pending approval", true)]
    [InlineData("Draft", false)]
    [InlineData("Approved", false)]
    [InlineData(null, false)]
    public void ApprovalDecisionRequiresPriorWorkflowSubmission(string? status, bool expected)
    {
        PurchaseOrdersController.CanRecordApprovalDecision(status).Should().Be(expected);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutDraftOwnershipOrManageCapabilityCannotCancel()
    {
        await using var fixture = await CreateStatusFixtureAsync(authorized: false);

        var result = await fixture.Controller.UpdatePurchaseOrderStatus(
            fixture.PurchaseOrder.Id,
            new UpdateStatusDto { Status = "Cancelled" });

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        JsonSerializer.Serialize(forbidden.Value).Should().Contain("PO_STATUS_FORBIDDEN");
        fixture.Repository.Verify(
            item => item.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthorizedDraftOwnerCanCancelWhileOtherGenericTransitionsRemainDedicated()
    {
        await using var fixture = await CreateStatusFixtureAsync(authorized: true);

        var cancelled = await fixture.Controller.UpdatePurchaseOrderStatus(
            fixture.PurchaseOrder.Id,
            new UpdateStatusDto { Status = "Cancelled" });

        cancelled.Should().BeOfType<NoContentResult>();
        fixture.Repository.Verify(
            item => item.UpdateStatusAsync(fixture.PurchaseOrder.Id, "Cancelled"),
            Times.Once);

        var submitted = await fixture.Controller.UpdatePurchaseOrderStatus(
            fixture.PurchaseOrder.Id,
            new UpdateStatusDto { Status = "Pending Approval" });
        var dedicated = submitted.Should().BeOfType<ConflictObjectResult>().Subject;
        JsonSerializer.Serialize(dedicated.Value).Should()
            .Contain("PO_STATUS_DEDICATED_ROUTE_REQUIRED");
    }

    [Fact]
    public async Task HistoricalMigrationStatusChangesRequireDedicatedMigrationRoute()
    {
        await using var fixture = await CreateStatusFixtureAsync(authorized: true);
        fixture.PurchaseOrder.ProcurementSourceType =
            ProcurementPurchaseOrderSourceType.HistoricalMigration;

        var result = await fixture.Controller.UpdatePurchaseOrderStatus(
            fixture.PurchaseOrder.Id,
            new UpdateStatusDto { Status = "Approved" });

        var dedicated = result.Should().BeOfType<ConflictObjectResult>().Subject;
        JsonSerializer.Serialize(dedicated.Value).Should()
            .Contain("PO_HISTORICAL_STATUS_MIGRATION_ROUTE_REQUIRED");
        fixture.Repository.Verify(
            item => item.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    private static async Task<StatusFixture> CreateStatusFixtureAsync(bool authorized)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var context = new ApplicationDbContext(options, tenantId);
        var unitOfWork = new UnitOfWork(context);
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedById = actorId,
            OrderNumber = "PO-STATUS-001",
            BusinessPartnerId = Guid.NewGuid(),
            Status = "Draft",
            ProcurementSourceType = ProcurementPurchaseOrderSourceType.RfqAward,
            ProcurementSourceId = Guid.NewGuid()
        };
        var repository = new Mock<IPurchaseOrderRepository>();
        repository.Setup(item => item.GetPurchaseOrderByIdAsync(purchaseOrder.Id))
            .ReturnsAsync(purchaseOrder);
        repository.Setup(item => item.UpdateStatusAsync(
                It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var source = new Mock<IProcurementPurchaseOrderSourceService>();
        var authorization = source.Setup(item => item.AuthorizeDraftCancellationAsync(
            purchaseOrder,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()));
        if (authorized)
            authorization.Returns(Task.CompletedTask);
        else
            authorization.ThrowsAsync(
                new ProcurementPurchaseOrderSourceAuthorizationException(
                    "The current user cannot cancel this draft purchase order."));

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(actorId);
        currentUser.SetupGet(item => item.Username).Returns("procurement.owner");

        var controller = new PurchaseOrdersController(
            repository.Object,
            Mock.Of<IPurchaseOrderItemRepository>(),
            Mock.Of<IPurchaseOrderReceiptRepository>(),
            Mock.Of<IPurchaseOrderReceiptItemRepository>(),
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<IInventoryItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<IProjectService>(),
            unitOfWork,
            currentUser.Object,
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<ISupplierValidationService>(),
            source.Object,
            Mock.Of<IProcurementPurchaseOrderComplianceService>(),
            Mock.Of<IProcurementPurchaseOrderSodService>(),
            Mock.Of<IProcurementReceiptSourceControlService>(),
            Mock.Of<IProcurementReceiptInspectionService>(),
            Mock.Of<IProcurementReceiptDocumentService>(),
            Mock.Of<IProcurementControlEventService>(),
            Mock.Of<IProcurementBudgetCommitmentLifecycleService>(),
            Mock.Of<ILogger<PurchaseOrdersController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = $"status-{Guid.NewGuid():N}"
                }
            }
        };
        await context.SaveChangesAsync();
        return new StatusFixture(context, controller, repository, purchaseOrder);
    }

    private sealed record StatusFixture(
        ApplicationDbContext Context,
        PurchaseOrdersController Controller,
        Mock<IPurchaseOrderRepository> Repository,
        PurchaseOrder PurchaseOrder) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
