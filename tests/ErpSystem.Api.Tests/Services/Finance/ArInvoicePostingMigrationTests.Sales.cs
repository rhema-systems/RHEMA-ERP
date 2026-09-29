using ErpSystem.Api.Services.Sales;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ArInvoicePostingMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sales_stock_invoice_posts_once_with_balanced_canonical_journal_and_delivery_never_issues_again(bool taxable)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenant, invoice =>
        {
            invoice.Status = InvoiceStatus.ReadyToPost;
            invoice.ApprovalRequired = false;
            invoice.TaxAmount = taxable ? 15m : 0m;
            invoice.TotalAmount = invoice.BaseCurrencyAmount = 100m + invoice.TaxAmount;
        });
        var line = fixture.Invoice.LineItems.Single();
        var itemId = Guid.NewGuid(); var warehouseId = Guid.NewGuid(); var locationId = Guid.NewGuid();
        line.LineItemType = LineItemType.Inventory; line.InventoryItemId = itemId;
        line.WarehouseId = warehouseId; line.LocationId = locationId; line.Unit = "EA";
        line.LotNumber = "LOT-A"; line.SerialNumber = "SERIAL-A";
        line.TaxAmount = fixture.Invoice.TaxAmount; line.TaxRate = taxable ? 15 : 0;
        line.TaxTreatment = taxable ? TaxTreatment.Standard : TaxTreatment.Exempt;
        line.TaxGroupId = taxable ? Guid.NewGuid() : null;
        var cogs = SeedAccount(db, tenant, "5100", AccountType.Expense);
        var inventory = SeedAccount(db, tenant, "1300", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var settings = await db.FinanceSettings.SingleAsync(x => x.TenantId == tenant);
        var fallbackCogs = SeedAccount(db, tenant, "5199", AccountType.Expense);
        var fallbackInventory = SeedAccount(db, tenant, "1399", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        settings.ControlAccountCOGSId = fallbackCogs.Id; settings.ControlAccountInventoryId = fallbackInventory.Id;
        var stockItem = new InventoryItem { Id = itemId, TenantId = tenant, ItemCode = "SALES-STOCK", Name = "Sales stock",
            InventoryAccountId = inventory.Id, CostOfGoodsSoldAccountId = cogs.Id, UnitOfMeasure = "EA" };
        db.InventoryItems.Add(stockItem);
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(), TenantId = tenant, DocumentNumber = "SO-STOCK", BusinessPartnerId = fixture.Customer.Id,
            CustomerName = fixture.Customer.PartnerName, Currency = "GHS", OrderType = SalesOrderType.Standard,
            OrderStatus = SalesOrderStatus.Confirmed, SubTotal = 100, TaxAmount = fixture.Invoice.TaxAmount,
            TotalAmount = fixture.Invoice.TotalAmount, InvoiceId = fixture.Invoice.Id, WarehouseId = warehouseId,
            InvoiceGenerationKey = "stock-test", InvoiceGenerationHash = "hash", InvoiceGeneratedById = Guid.NewGuid()
        };
        order.Lines.Add(new SalesOrderLine
        {
            Id = line.Id, TenantId = tenant, SalesOrderId = order.Id, InventoryItemId = itemId,
            Description = line.Description, Quantity = 1, UnitPrice = 100, Unit = "EA", GLAccountId = fixture.RevenueAccount.Id,
            WarehouseId = warehouseId, LocationId = locationId, LotNumber = line.LotNumber, SerialNumber = line.SerialNumber,
            TaxAmount = line.TaxAmount, TaxRate = line.TaxRate, TaxGroupId = line.TaxGroupId
        });
        fixture.Invoice.Reference = order.DocumentNumber;
        order.InvoiceSourceJson = SalesOrderInvoiceGuard.SourceSnapshot(order);
        order.InvoiceEconomicsJson = SalesOrderInvoiceGuard.Snapshot(fixture.Invoice);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();

        var observedOrder = new List<string>();
        var tracking = new Mock<IInventoryTrackingControlService>(MockBehavior.Strict);
        tracking.Setup(x => x.StageEventAsync(It.IsAny<InventoryTrackingMutationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryTrackingMutationRequest, CancellationToken>((request, _) =>
            {
                request.InventoryItemId.Should().Be(itemId); request.WarehouseId.Should().Be(warehouseId);
                request.LocationId.Should().Be(locationId); request.Quantity.Should().Be(1m);
                request.Direction.Should().Be(InventoryTrackingDirection.Issue);
                request.ReferenceId.Should().Be(fixture.Invoice.Id); request.ReferenceLineId.Should().Be(line.Id);
                request.LotNumber.Should().Be("LOT-A"); request.SerialNumber.Should().Be("SERIAL-A");
                observedOrder.Add("tracking");
            }).Returns(Task.CompletedTask);
        var valuation = new Mock<IInventoryValuationService>(MockBehavior.Strict);
        valuation.Setup(x => x.ResetProcessingAttempt()).Callback(() => observedOrder.Add("reset"));
        valuation.Setup(x => x.ProcessIssueAsync(itemId, warehouseId, locationId, 1m, InventoryMovementType.SalesIssue,
                ReferenceType.SalesInvoice, fixture.Invoice.InvoiceNumber, fixture.Invoice.Id, "LOT-A", "SERIAL-A"))
            .Callback(() => observedOrder.Add("issue")).ReturnsAsync(40m);
        var taxEngine = new Mock<ITaxCalculationEngine>();
        taxEngine.Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResultDto
            {
                TaxGroupId = line.TaxGroupId, TotalTaxAmount = fixture.Invoice.TaxAmount,
                TaxBreakdowns = taxable ? [new TaxBreakdownDto
                {
                    TaxId = Guid.NewGuid(), TaxCode = "VAT", TaxName = "VAT", TaxableAmount = 100, TaxRate = 15, TaxAmount = 15,
                    TaxPayableAccountId = fixture.TaxAccount.Id, EffectiveFrom = new DateTime(2026, 1, 1)
                }] : []
            });
        var dimensions = new Mock<IFinanceSourceDimensionService>();
        dimensions.Setup(x => x.GetPostingDimensionsAsync(SalesOrderInvoiceGuard.Producer, fixture.Invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>());
        var (service, legacy) = CreateService(db, tenant, taxEngine: taxEngine.Object, valuation: valuation.Object, tracking: tracking.Object, dimensions: dimensions.Object);

        var posted = await service.PostAsync(fixture.Invoice.Id, SalesOrderInvoiceGuard.Producer);
        // A subsequent profile change must not redirect the original journal or cause another issue.
        stockItem.InventoryAccountId = fallbackInventory.Id;
        stockItem.CostOfGoodsSoldAccountId = fallbackCogs.Id;
        await db.SaveChangesAsync();
        var replay = await service.PostAsync(fixture.Invoice.Id, SalesOrderInvoiceGuard.Producer);
        await service.SendInvoiceAsync(fixture.Invoice.Id, SalesOrderInvoiceGuard.Producer);

        posted.JournalEntryId.Should().NotBeNull(); replay.JournalEntryId.Should().Be(posted.JournalEntryId);
        observedOrder.Should().Equal("reset", "tracking", "issue", "reset", "reset");
        valuation.Verify(x => x.ResetProcessingAttempt(), Times.Exactly(3));
        valuation.Verify(x => x.ProcessIssueAsync(itemId, warehouseId, locationId, 1m, InventoryMovementType.SalesIssue,
            ReferenceType.SalesInvoice, fixture.Invoice.InvoiceNumber, fixture.Invoice.Id, "LOT-A", "SERIAL-A"), Times.Once);
        tracking.Verify(x => x.StageEventAsync(It.IsAny<InventoryTrackingMutationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        legacy.Verify(x => x.PostArInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        var journal = await db.JournalEntries.Include(x => x.Transactions).SingleAsync(x => x.Id == posted.JournalEntryId);
        journal.Transactions.Sum(x => x.DebitAmount).Should().Be(journal.Transactions.Sum(x => x.CreditAmount));
        journal.Transactions.Single(x => x.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(taxable ? 115 : 100);
        journal.Transactions.Single(x => x.AccountId == fixture.RevenueAccount.Id).CreditAmount.Should().Be(100);
        journal.Transactions.Single(x => x.AccountId == cogs.Id).DebitAmount.Should().Be(40);
        journal.Transactions.Single(x => x.AccountId == inventory.Id).CreditAmount.Should().Be(40);
        journal.Transactions.Should().NotContain(x => x.AccountId == fallbackCogs.Id || x.AccountId == fallbackInventory.Id);
        journal.Transactions.Single(x => x.AccountId == inventory.Id).SourceDocumentLineId.Should().Be(line.Id);
        if (taxable) journal.Transactions.Single(x => x.AccountId == fixture.TaxAccount.Id).CreditAmount.Should().Be(15);
        journal.Transactions.Should().HaveCount(taxable ? 5 : 4);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        fixture.Invoice.LineItems.Single().CostTotal.Should().Be(40);

        // Execute the real fulfillment service against the posted source; it has no second valuation call.
        var delivery = new DeliveryNote
        {
            TenantId = tenant, SalesOrderId = order.Id, SalesOrder = order, BusinessPartnerId = fixture.Customer.Id,
            CustomerName = fixture.Customer.PartnerName, DocumentNumber = "DN-STOCK", DeliveryStatus = DeliveryNoteStatus.Shipped,
            Lines = [new DeliveryNoteLine { TenantId = tenant, SalesOrderLineId = line.Id, InventoryItemId = itemId,
                Description = line.Description, DispatchedQuantity = 1, WarehouseId = warehouseId, LocationId = locationId }]
        };
        db.Set<DeliveryNote>().Add(delivery); await db.SaveChangesAsync();
        var uow = new UnitOfWork(db);
        var actor = new Mock<ICurrentUserProvider>(); actor.SetupGet(x => x.TenantId).Returns(tenant); actor.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var deliveries = new DeliveryService(uow.Repository<DeliveryNote>(), uow.Repository<DeliveryNoteLine>(), uow.Repository<SalesOrder>(),
            uow.Repository<SalesOrderLine>(), uow.Repository<SalesOrderStatusHistory>(), uow, actor.Object,
            Mock.Of<ILogger<DeliveryService>>(), Mock.Of<IDocumentNumberingService>());
        await deliveries.ConfirmDeliveryAsync(delivery.Id, new ConfirmDeliveryDto { ReceiverName = "Customer receiver" });
        observedOrder.Should().Equal("reset", "tracking", "issue", "reset", "reset");
        delivery.Lines.Single().IsStockDeducted.Should().BeTrue();
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Sales_description_over_canonical_limit_fails_before_account_or_stock_queries()
    {
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var act = () => SalesOrderInvoiceGuard.RequirePostingLineAsync(unitOfWork.Object, Guid.NewGuid(),
            new InvoiceLineItemCreateDto { Description = new string('x', 201) }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*200 characters*reapprove*no text has been truncated*");
        unitOfWork.VerifyNoOtherCalls();
    }
}
