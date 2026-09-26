using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAcceptedSupplyServiceTests
{
    [Theory]
    [InlineData(9.99999, 1, 8.9999)]
    [InlineData(0.00009, 0, 0)]
    [InlineData(4, 5, 0)]
    public void InvoiceAvailabilityCannotRoundUpBeyondAcceptedSupply(decimal accepted, decimal invoiced, decimal expected)
    {
        var entry = new ErpSystem.Core.DTOs.Finance.ApGoodsInvoiceEntryLineDto { AcceptedQuantity = accepted, InvoicedQuantity = invoiced };
        Assert.Equal(expected, entry.AvailableQuantity);
    }

    [Fact]
    public async Task ApprovedReceiptContributesOnlyAcceptedQuantityWhileAnotherReceiptIsPending()
    {
        var tenant = Guid.NewGuid();
        await using var context = Context(tenant);
        var order = Order(tenant, ProcurementCategoryClass.Goods);
        var accepted = AddReceipt(context, order, 100m, 92m);
        AddReceipt(context, order, 100m, 80m, pending: 20m);
        await context.SaveChangesAsync();
        using var unit = new UnitOfWork(context);
        var service = Service(unit, tenant);
        var result = await service.ResolveAsync(ProcurementAcceptedSupplyKind.GoodsReceiptInspection, order.Id, order.Id, null);
        result.Lines.Should().ContainSingle().Which.AcceptedQuantity.Should().Be(92m);
        var receipts = await service.GetGoodsReceiptLinesAsync(order.Id);
        receipts.Should().ContainSingle().Which.InspectionCaseId.Should().Be(accepted.Id);
    }

    [Fact]
    public async Task ReturnedBaseUnitsUseOriginalReceiptConversionAndIgnoreForeignReturns()
    {
        var tenant = Guid.NewGuid();
        await using var context = Context(tenant);
        var order = Order(tenant, ProcurementCategoryClass.Goods);
        var inspection = AddReceipt(context, order, 100m, 92m);
        var grn = new GoodsReceiptNote { TenantId = tenant, GRNNumber = "GRN-RETURN", PurchaseOrderId = order.Id,
            PurchaseOrderReceiptId = inspection.PurchaseOrderReceiptId, Status = GRNStatus.StockUpdated, StockUpdated = true };
        var grnLine = new GoodsReceiptNoteItem { TenantId = tenant, GoodsReceiptNote = grn, GoodsReceiptNoteId = grn.Id,
            PurchaseOrderItemId = order.Items.Single().Id, ReceivedQuantity = 200m, AcceptedQuantity = 184m };
        context.Add(grnLine);
        context.Add(new PurchaseReturnItem { TenantId = tenant, GoodsReceiptNoteItemId = grnLine.Id, ReturnQuantity = 4m, StockReversed = true,
            PurchaseReturn = new PurchaseReturn { TenantId = tenant, ReturnNumber = "RETURN", GoodsReceiptNoteId = grn.Id, Status = "Shipped" } });
        context.Add(new PurchaseReturnItem { TenantId = Guid.NewGuid(), GoodsReceiptNoteItemId = grnLine.Id, ReturnQuantity = 90m, StockReversed = true,
            PurchaseReturn = new PurchaseReturn { TenantId = Guid.NewGuid(), ReturnNumber = "FOREIGN", GoodsReceiptNoteId = grn.Id, Status = "Shipped" } });
        context.Add(new PurchaseReturnItem { TenantId = tenant, GoodsReceiptNoteItemId = grnLine.Id, ReturnQuantity = 10m, StockReversed = false,
            PurchaseReturn = new PurchaseReturn { TenantId = tenant, ReturnNumber = "DRAFT", GoodsReceiptNoteId = grn.Id, Status = "Draft" } });
        await context.SaveChangesAsync();
        using var unit = new UnitOfWork(context);
        var result = await Service(unit, tenant).GetGoodsReceiptLinesAsync(order.Id);
        result.Should().ContainSingle();
        result[0].AcceptedQuantity.Should().Be(92m);
        result[0].ReturnedQuantity.Should().Be(2m);
        result[0].NetAcceptedQuantity.Should().Be(90m);
    }

    [Fact]
    public async Task LatestInspectionSupersedesPriorDecisionWithoutDoubleCounting()
    {
        var tenant = Guid.NewGuid();
        await using var context = Context(tenant);
        var order = Order(tenant, ProcurementCategoryClass.Goods);
        var previous = AddReceipt(context, order, 100m, 92m);
        var latest = new ProcurementReceiptInspectionCase { TenantId = tenant, Sequence = 2,
            PurchaseOrderReceiptId = previous.PurchaseOrderReceiptId, Status = ProcurementReceiptInspectionStatus.Approved,
            AcceptedQuantity = 80m, ApEligibleQuantity = 80m };
        latest.Lines.Add(new ProcurementReceiptInspectionLine { TenantId = tenant, InspectionCaseId = latest.Id,
            PurchaseOrderReceiptItemId = previous.Lines.Single().PurchaseOrderReceiptItemId,
            PurchaseOrderReceiptItem = previous.Lines.Single().PurchaseOrderReceiptItem, AcceptedQuantity = 80m });
        context.Add(latest);
        await context.SaveChangesAsync();
        using var unit = new UnitOfWork(context);
        var result = await Service(unit, tenant).GetGoodsReceiptLinesAsync(order.Id);
        result.Should().ContainSingle().Which.NetAcceptedQuantity.Should().Be(80m);
        latest.Status = ProcurementReceiptInspectionStatus.Draft;
        await context.SaveChangesAsync();
        (await Service(unit, tenant).GetGoodsReceiptLinesAsync(order.Id)).Should().BeEmpty();
    }

    private static ProcurementReceiptInspectionCase AddReceipt(ApplicationDbContext context, PurchaseOrder order,
        decimal received, decimal accepted, decimal pending = 0m)
    {
        var poLine = new PurchaseOrderItem { TenantId = order.TenantId, PurchaseOrder = order, PurchaseOrderId = order.Id, UnitPrice = 10m };
        order.Items.Add(poLine);
        var receipt = new PurchaseOrderReceipt { TenantId = order.TenantId, PurchaseOrder = order, PurchaseOrderId = order.Id,
            ReceiptNumber = "RCV-" + Guid.NewGuid().ToString("N"), Status = "Accepted" };
        var source = new PurchaseOrderReceiptItem { TenantId = order.TenantId, Receipt = receipt, ReceiptId = receipt.Id,
            PurchaseOrderItem = poLine, PurchaseOrderItemId = poLine.Id, ReceivedQuantity = received, AcceptedQuantity = accepted };
        var inspection = new ProcurementReceiptInspectionCase { TenantId = order.TenantId, Sequence = 1,
            PurchaseOrderReceipt = receipt, PurchaseOrderReceiptId = receipt.Id, ReceivedQuantity = received,
            AcceptedQuantity = accepted, RejectedQuantity = received - accepted - pending, PendingQuantity = pending,
            Status = pending > 0 ? ProcurementReceiptInspectionStatus.Draft : ProcurementReceiptInspectionStatus.Approved,
            ApEligibleQuantity = pending > 0 ? 0m : accepted };
        inspection.Lines.Add(new ProcurementReceiptInspectionLine { TenantId = order.TenantId, InspectionCaseId = inspection.Id,
            PurchaseOrderReceiptItem = source, PurchaseOrderReceiptItemId = source.Id, ReceivedQuantity = received,
            AcceptedQuantity = accepted, PendingQuantity = pending, RejectedQuantity = received - accepted - pending });
        context.Add(inspection);
        return inspection;
    }

    [Theory]
    [InlineData(ProcurementCategoryClass.Goods, "No governed goods receipt")]
    [InlineData(ProcurementCategoryClass.TechnicalServices, "No approved, documented Project deliverable")]
    [InlineData(ProcurementCategoryClass.Works, "Works invoices are created only through")]
    public async Task OptionsRouteEachCategoryToItsExistingAuthoritativeOwner(
        ProcurementCategoryClass category,
        string expectedReason)
    {
        var tenantId = Guid.NewGuid();
        var order = Order(tenantId, category);
        await using var context = Context(tenantId);
        context.Add(order);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var result = await Service(unitOfWork, tenantId).GetOptionsAsync(order.Id);

        result.Category.Should().Be(category);
        result.Ready.Should().BeFalse();
        result.BlockedReasons.Should().ContainSingle()
            .Which.Should().Contain(expectedReason);
        if (category == ProcurementCategoryClass.Works)
            result.WorksHandoffRoute.Should().Be("/quantity-survey/payment-certificates");
    }

    [Fact]
    public async Task OptionsFailClosedForAnotherTenantsPurchaseOrder()
    {
        var ownerTenantId = Guid.NewGuid();
        var requesterTenantId = Guid.NewGuid();
        var order = Order(ownerTenantId, ProcurementCategoryClass.Goods);
        await using var context = Context(requesterTenantId);
        context.Add(order);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var action = () => Service(unitOfWork, requesterTenantId).GetOptionsAsync(order.Id);

        var exception = await action.Should()
            .ThrowAsync<ProcurementAcceptedSupplyValidationException>();
        exception.Which.Code.Should().Be("ACCEPTED_SUPPLY_PO_NOT_FOUND");
    }

    [Fact]
    public async Task OptionsFailClosedWhenGovernedCategorySnapshotIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var order = Order(tenantId, null);
        await using var context = Context(tenantId);
        context.Add(order);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var action = () => Service(unitOfWork, tenantId).GetOptionsAsync(order.Id);

        var exception = await action.Should()
            .ThrowAsync<ProcurementAcceptedSupplyValidationException>();
        exception.Which.Code.Should().Be("PURCHASE_ORDER_CATEGORY_REQUIRED");
    }

    private static PurchaseOrder Order(Guid tenantId, ProcurementCategoryClass? category) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OrderNumber = $"PO-{Guid.NewGuid():N}",
        BusinessPartnerId = Guid.NewGuid(),
        ProcurementCategory = category,
        Currency = "GHS"
    };

    private static ApplicationDbContext Context(Guid tenantId) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options,
        tenantId);

    private static ProcurementAcceptedSupplyService Service(
        IUnitOfWork unitOfWork,
        Guid tenantId)
    {
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.IsAuthenticated).Returns(true);
        return new ProcurementAcceptedSupplyService(unitOfWork, current.Object);
    }
}
