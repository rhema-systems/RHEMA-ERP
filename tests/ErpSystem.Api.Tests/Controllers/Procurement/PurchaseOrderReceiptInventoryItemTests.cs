using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class PurchaseOrderReceiptInventoryItemTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task MissingItem_RequiresExplicitReceiverDecision()
    {
        var fixture = await CreateFixtureAsync();
        var request = CreateRequest(fixture.PurchaseOrderItem.Id);

        var action = () => ResolveAsync(
            fixture.Controller,
            fixture.PurchaseOrder,
            [request]);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("*Confirm item creation for this line*");
        fixture.Context.InventoryItems.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmedMissingItem_IsCreatedAndLinkedBeforeReceiptSnapshot()
    {
        var fixture = await CreateFixtureAsync();
        var request = CreateRequest(fixture.PurchaseOrderItem.Id);
        request.CreateInventoryItemIfMissing = true;
        request.InventoryCategoryId = fixture.Category.Id;
        request.ProposedItemCode = "RCV-KEYBOARD";

        await ResolveAsync(
            fixture.Controller,
            fixture.PurchaseOrder,
            [request]);
        await fixture.UnitOfWork.SaveChangesAsync();

        var created = await fixture.Context.InventoryItems.SingleAsync();
        created.ItemCode.Should().Be("RCV-KEYBOARD");
        created.Name.Should().Be("Wireless Keyboard");
        created.CategoryId.Should().Be(fixture.Category.Id);
        created.UnitOfMeasure.Should().Be("EA");
        created.LastPurchaseCost.Should().Be(399m);

        var linkedLine = await fixture.Context.PurchaseOrderItems
            .SingleAsync(item => item.Id == fixture.PurchaseOrderItem.Id);
        linkedLine.InventoryItemId.Should().Be(created.Id);
    }

    private static ReceivePurchaseOrderItemDto CreateRequest(
        Guid purchaseOrderItemId) => new()
    {
        PurchaseOrderItemId = purchaseOrderItemId,
        ReceivedQuantity = 10m,
        WarehouseId = Guid.NewGuid(),
        LocationId = Guid.NewGuid()
    };

    private static async Task ResolveAsync(
        PurchaseOrdersController controller,
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<ReceivePurchaseOrderItemDto> receiptLines)
    {
        var method = typeof(PurchaseOrdersController).GetMethod(
            "ResolveReceiptInventoryItemsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(
                nameof(PurchaseOrdersController),
                "ResolveReceiptInventoryItemsAsync");
        var task = (Task)(method.Invoke(
            controller,
            [purchaseOrder, receiptLines, "receipt-item-test", CancellationToken.None])
            ?? throw new InvalidOperationException("Resolver returned no task."));
        await task;
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var tenantId = TenantId;
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"receipt-item-{Guid.NewGuid():N}")
            .Options;
        var context = new ApplicationDbContext(options, tenantId);
        var unitOfWork = new UnitOfWork(context);

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "PO-2026-0001",
            BusinessPartnerId = Guid.NewGuid(),
            Currency = "GHS",
            Status = "Approved"
        };
        var category = new InventoryCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IT",
            Name = "Information Technology",
            IsActive = true
        };
        var purchaseOrderItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = purchaseOrder.Id,
            ItemDescription = "Wireless Keyboard",
            UnitOfMeasure = "EA",
            OrderedQuantity = 10m,
            UnitPrice = 399m,
            LineTotal = 3990m
        };
        context.PurchaseOrders.Add(purchaseOrder);
        context.InventoryCategories.Add(category);
        context.PurchaseOrderItems.Add(purchaseOrderItem);
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.Username).Returns("stores.officer");

        var controller = new PurchaseOrdersController(
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<IPurchaseOrderItemRepository>(),
            Mock.Of<IPurchaseOrderReceiptRepository>(),
            Mock.Of<IPurchaseOrderReceiptItemRepository>(),
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<IInventoryItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            Mock.Of<IProcurementBudgetService>(),
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<IProjectService>(),
            unitOfWork,
            currentUser.Object,
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<ISupplierValidationService>(),
            Mock.Of<IProcurementPurchaseOrderSourceService>(),
            Mock.Of<IProcurementPurchaseOrderComplianceService>(),
            Mock.Of<IProcurementPurchaseOrderSodService>(),
            Mock.Of<IProcurementReceiptSourceControlService>(),
            Mock.Of<IProcurementReceiptInspectionService>(),
            Mock.Of<IProcurementReceiptDocumentService>(),
            Mock.Of<IProcurementControlEventService>(),
            Mock.Of<ILogger<PurchaseOrdersController>>());

        return new Fixture(
            context,
            unitOfWork,
            controller,
            purchaseOrder,
            purchaseOrderItem,
            category);
    }

    private sealed record Fixture(
        ApplicationDbContext Context,
        UnitOfWork UnitOfWork,
        PurchaseOrdersController Controller,
        PurchaseOrder PurchaseOrder,
        PurchaseOrderItem PurchaseOrderItem,
        InventoryCategory Category);
}
