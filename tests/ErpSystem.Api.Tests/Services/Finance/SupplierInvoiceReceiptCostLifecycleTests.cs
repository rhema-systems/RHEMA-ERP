using System.Linq.Expressions;
using System.Reflection;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>Public invoice lifecycle with the real Inventory value owner; Finance transport retains captured balanced journal evidence.</summary>
public sealed class SupplierInvoiceReceiptCostLifecycleTests
{
    [Theory]
    [InlineData(ValuationMethod.WeightedAverage, 100, 200, 0)]
    [InlineData(ValuationMethod.WeightedAverage, 40, 80, 120)]
    [InlineData(ValuationMethod.WeightedAverage, 0, 0, 200)]
    [InlineData(ValuationMethod.FIFO, 100, 200, 0)]
    [InlineData(ValuationMethod.FIFO, 40, 80, 120)]
    [InlineData(ValuationMethod.StandardCost, 100, 0, 200)]
    public async Task Public_post_and_retry_preserve_original_GRNI_and_revalue_only_retained_stock(
        ValuationMethod method, int retained, int inventoryDifference, int ppvDifference)
    {
        var f = new Fixture(method, retained);
        var invoice = f.Invoice(100, 12);
        var preview = await f.Service.GetDistributionAsync(invoice.Id);
        Assert.NotEmpty(preview.Lines);
        await f.Service.PostAsync(invoice.Id);
        var first = Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.Equal(1000, first.ReceiptFunctionalAmount);
        Assert.Equal(inventoryDifference, first.InventoryAdjustmentAmount);
        Assert.Equal(ppvDifference, first.PurchasePriceVarianceAmount);
        Assert.Equal(retained, f.Balance.QuantityOnHand);
        Assert.Equal(retained * 10 + inventoryDifference, f.Balance.TotalValue);
        Assert.Equal(1000, f.OriginalAccrual.CreditAmount);
        Assert.Equal(1000, f.ReceiptMovement.TotalValue);
        var count = f.Rows<InventoryMovement>().Count;
        await f.Service.PostAsync(invoice.Id);
        Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.Equal(count, f.Rows<InventoryMovement>().Count);
        Assert.All(f.Requests, request => Assert.Equal(request.Lines.Sum(x => x.DebitAmount), request.Lines.Sum(x => x.CreditAmount)));
    }

    [Fact]
    public async Task Invoice_discount_and_recoverable_tax_stay_separate_from_original_receipt_price_difference()
    {
        var f = new Fixture(ValuationMethod.WeightedAverage, 100);
        f.Policy.PurchasePriceDifferenceHandling = PurchasePriceDifferencePolicy.PurchasePriceVariance;
        var invoice = f.Invoice(100, 12, discountPercent: 10, taxPercent: 10);
        await f.Service.PostAsync(invoice.Id);
        var cost = Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.Equal(1080, cost.InvoiceNetForeignAmount);
        Assert.Equal(80, cost.PurchasePriceVarianceAmount);
        Assert.Equal(1000, f.Balance.TotalValue);
        var posted = Assert.Single(f.Requests);
        Assert.Equal(108, Assert.Single(posted.Lines.Where(x => x.TransactionTag == "AP-Tax-VAT")).DebitAmount);
        Assert.Equal(1188, Assert.Single(posted.Lines.Where(x => x.TransactionTag == "AP-Control")).CreditAmount);
    }

    [Theory]
    [InlineData(ValuationMethod.WeightedAverage)]
    [InlineData(ValuationMethod.FIFO)]
    public async Task Public_post_follows_retained_receipt_quantity_into_controlled_transit(ValuationMethod method)
    {
        var f = new Fixture(method, 100);
        var transit = f.SeedRetainedTransit(40);
        await f.Service.PostAsync(f.Invoice(100, 12).Id);
        Assert.Equal(60, f.Balance.QuantityOnHand); Assert.Equal(720, f.Balance.TotalValue);
        Assert.Equal(40, transit.QuantityOnHand); Assert.Equal(480, transit.TotalValue);
        var cost = Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.Equal(200, cost.InventoryAdjustmentAmount); Assert.Equal(0, cost.PurchasePriceVarianceAmount);
        Assert.Equal(2, f.Rows<VendorInvoiceReceiptCostValuation>().Count);
    }

    [Fact]
    public async Task Two_partial_invoices_clear_the_exact_original_receipt_once()
    {
        var f = new Fixture(ValuationMethod.WeightedAverage, 100);
        f.Policy.PurchasePriceDifferenceHandling = PurchasePriceDifferencePolicy.PurchasePriceVariance;
        await f.Service.PostAsync(f.Invoice(40, 12).Id);
        await f.Service.PostAsync(f.Invoice(60, 9).Id);
        var costs = f.Rows<VendorInvoiceReceiptCostAllocation>();
        Assert.Equal(2, costs.Count);
        Assert.Equal(100, costs.Sum(x => x.PurchaseQuantity));
        Assert.Equal(1000, costs.Sum(x => x.ReceiptFunctionalAmount));
        Assert.Equal(20, costs.Sum(x => x.PurchasePriceVarianceAmount));
        Assert.Equal(1000, f.Balance.TotalValue);
    }

    [Fact]
    public async Task Foreign_invoice_separates_original_accrual_price_difference_and_currency_difference()
    {
        var f = new Fixture(ValuationMethod.WeightedAverage, 100, receiptRate: 10, invoiceRate: 11);
        f.Policy.PurchasePriceDifferenceHandling = PurchasePriceDifferencePolicy.PurchasePriceVariance;
        await f.Service.PostAsync(f.Invoice(100, 12).Id);
        var cost = Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.Equal(10000, cost.ReceiptFunctionalAmount);
        Assert.Equal(1000, cost.ExchangeDifferenceFunctionalAmount);
        Assert.Equal(2200, cost.PurchasePriceVarianceAmount);
        Assert.Equal(13200, cost.InvoiceFunctionalAmount);
        Assert.Equal(10000, f.Balance.TotalValue);
    }

    [Theory]
    [InlineData(ValuationMethod.WeightedAverage, 0)]
    [InlineData(ValuationMethod.WeightedAverage, 60)]
    [InlineData(ValuationMethod.FIFO, 0)]
    [InlineData(ValuationMethod.FIFO, 60)]
    public async Task Public_void_reverses_only_remaining_value_and_reclassifies_consumed_difference(ValuationMethod method, int issued)
    {
        var f = new Fixture(method, 100);
        var invoice = f.Invoice(100, 12);
        await f.Service.PostAsync(invoice.Id);
        if (issued > 0) await f.Issue(issued);
        await f.Service.VoidAsync(invoice.Id, "Supplier cancelled invoice");
        Assert.Equal(100 - issued, f.Balance.QuantityOnHand);
        Assert.Equal((100 - issued) * 10, f.Balance.TotalValue);
        var cost = Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.NotNull(cost.ReversalJournalEntryId);
        Assert.Equal(issued > 0, cost.ReversalReclassificationJournalEntryId.HasValue);
        var count = f.Rows<InventoryMovement>().Count;
        await f.Service.VoidAsync(invoice.Id, "Repeated request");
        Assert.Equal(count, f.Rows<InventoryMovement>().Count);
        Assert.All(f.Requests, request => Assert.Equal(request.Lines.Sum(x => x.DebitAmount), request.Lines.Sum(x => x.CreditAmount)));
    }

    [Fact]
    public async Task Lower_invoice_price_and_later_void_preserve_negative_adjustment_direction()
    {
        var f = new Fixture(ValuationMethod.WeightedAverage, 100);
        var invoice = f.Invoice(100, 9);
        await f.Service.PostAsync(invoice.Id);
        Assert.Equal(-100, Assert.Single(f.Rows<VendorInvoiceReceiptCostAllocation>()).InventoryAdjustmentAmount);
        Assert.Equal(900, f.Balance.TotalValue);
        await f.Issue(60);
        await f.Service.VoidAsync(invoice.Id, "Cancelled lower-price invoice");
        Assert.Equal(40, f.Balance.QuantityOnHand); Assert.Equal(400, f.Balance.TotalValue);
        var correction = Assert.Single(f.Requests.Where(x => x.SourceDocumentType == "VendorInvoiceReceiptCostReclassification"));
        Assert.Equal(60, correction.Lines.Sum(x => x.DebitAmount));
        Assert.Equal(60, correction.Lines.Sum(x => x.CreditAmount));
    }

    [Fact]
    public async Task Finance_failure_precedes_inventory_cost_mutation()
    {
        var f = new Fixture(ValuationMethod.FIFO, 100);
        f.FailFinance = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostAsync(f.Invoice(100, 12).Id));
        Assert.Equal(1000, f.Balance.TotalValue);
        Assert.Empty(f.Rows<VendorInvoiceReceiptCostAllocation>());
        Assert.Empty(f.Rows<VendorInvoiceReceiptCostValuation>());
        Assert.Equal(1, f.Rollbacks);
    }

    private sealed class Fixture : DefaultValueProvider
    {
        private readonly Dictionary<Type, object> rows = [];
        private readonly Dictionary<Type, object> repos = [];
        private readonly Mock<IUnitOfWork> unit = new();
        private readonly Mock<IFinancePostingEngine> finance = new();
        private bool transaction;
        private readonly Guid tenant = Guid.NewGuid(), actor = Guid.NewGuid(), book = Guid.NewGuid();
        private readonly decimal invoiceRate;
        private readonly BusinessPartner partner;
        private readonly BusinessPartnerRole role;
        private readonly BusinessPartnerApProfileVersion profile;
        private readonly PurchaseOrder order;
        private readonly PurchaseOrderItem orderLine;
        private readonly InventoryItem item;
        private readonly Account ap;
        private readonly Account inputTax;
        public readonly ProcurementSettings Policy;
        public readonly InventoryBalance Balance;
        public readonly InventoryMovement ReceiptMovement;
        public readonly AccountTransaction OriginalAccrual;
        public readonly VendorInvoiceService Service;
        private readonly InventoryValuationService valuation;
        public readonly List<FinancePostingRequestV2Dto> Requests = [];
        public bool FailFinance;
        public int Rollbacks;

        public Fixture(ValuationMethod method, decimal retained, decimal receiptRate = 1, decimal invoiceRate = 1)
        {
            this.invoiceRate = invoiceRate;
            unit.DefaultValueProvider = this;
            unit.SetupGet(x => x.HasActiveTransaction).Returns(() => transaction);
            unit.Setup(x => x.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>())).Callback(() => transaction = true).Returns(Task.CompletedTask);
            unit.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => transaction = false).Returns(Task.CompletedTask);
            unit.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Callback(() => { transaction = false; Rollbacks++; }).Returns(Task.CompletedTask);
            unit.Setup(x => x.ExecuteInStrategyAsync(It.IsAny<Func<Task<VendorInvoiceDto>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task<VendorInvoiceDto>> run, CancellationToken _) => run());
            unit.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var user = new Mock<ICurrentUserService>(); user.SetupGet(x => x.TenantId).Returns(tenant); user.SetupGet(x => x.UserId).Returns(actor.ToString());
            user.SetupGet(x => x.IsAuthenticated).Returns(true); user.SetupGet(x => x.UserName).Returns("Receipt test");
            var inventoryUser = new Mock<ICurrentUserProvider>(); inventoryUser.SetupGet(x => x.TenantId).Returns(tenant); inventoryUser.SetupGet(x => x.UserId).Returns(actor);
            valuation = new InventoryValuationService(unit.Object, NullLogger<InventoryValuationService>.Instance, inventoryUser.Object, Moq.Mock.Of<IProcurementReceiptSourceControlService>());
            ap = Account("AP", true); var grni = Account("GRNI", true); var inventoryAccount = Account("Inventory", true);
            var ppv = Account("PPV", false); inputTax = Account("VAT", false); var fx = Account("FX", false);
            Add(new FinanceSettings { BaseCurrency = "GHS", ControlAccountApId = ap.Id, ApInvoicePriceTolerancePercent = 100,
                ApInvoiceQuantityTolerancePercent = 100, UnrealizedFxGainAccountId = fx.Id, UnrealizedFxLossAccountId = fx.Id });
            Policy = Add(new ProcurementSettings { PurchasePriceDifferenceHandling = PurchasePriceDifferencePolicy.RevalueInventory });
            partner = Add(new BusinessPartner { PartnerName = "Supplier", PartnerCode = "SUP", IsActive = true, RegistrationStatus = "Approved", PartnerType = "Supplier" });
            role = Add(new BusinessPartnerRole { BusinessPartnerId = partner.Id, BusinessPartner = partner, RoleType = BusinessPartnerRoleType.Supplier,
                Status = BusinessPartnerRoleStatus.Active, ActiveFromUtc = new DateTime(2020, 1, 1) });
            profile = Add(new BusinessPartnerApProfileVersion { BusinessPartnerRoleId = role.Id, BusinessPartnerRole = role, VersionNumber = 1,
                Status = BusinessPartnerFinanceProfileStatus.Approved, EffectiveFrom = new DateTime(2020, 1, 1) });
            var wh = Add(new Warehouse { Name = "Main", Code = "WH" });
            var location = Add(new WarehouseLocation { WarehouseId = wh.Id, LocationCode = "BIN", IsActive = true });
            item = Add(new InventoryItem { ItemCode = "ITEM", Name = "Item", ValuationMethod = method,
                InventoryAccountId = inventoryAccount.Id, PurchasePriceVarianceAccountId = ppv.Id, StandardCost = 10 * receiptRate });
            order = Add(new PurchaseOrder { OrderNumber = "PO", BusinessPartnerId = partner.Id, BusinessPartner = partner,
                ProcurementCategory = ProcurementCategoryClass.Goods, Currency = receiptRate == 1 ? "GHS" : "USD", TotalAmount = 1000, Status = "Approved" });
            orderLine = Add(new PurchaseOrderItem { PurchaseOrderId = order.Id, PurchaseOrder = order, InventoryItemId = item.Id,
                InventoryItem = item, LineType = ItemType.StockItem, OrderedQuantity = 100, UnitPrice = 10 });
            order.Items.Add(orderLine);
            var receipt = Add(new PurchaseOrderReceipt { PurchaseOrderId = order.Id, PurchaseOrder = order, ReceiptNumber = "RCV", ReceiptDate = new DateTime(2026, 9, 1) });
            var receiptLine = Add(new PurchaseOrderReceiptItem { ReceiptId = receipt.Id, PurchaseOrderItemId = orderLine.Id,
                ReceivedQuantity = 100, AcceptedQuantity = 100, LocationId = location.Id });
            var grn = Add(new GoodsReceiptNote { PurchaseOrderId = order.Id, PurchaseOrderReceiptId = receipt.Id, GRNNumber = "GRN", WarehouseId = wh.Id, SupplierId = partner.Id });
            var grnLine = Add(new GoodsReceiptNoteItem { GoodsReceiptNoteId = grn.Id, GoodsReceiptNote = grn, PurchaseOrderItemId = orderLine.Id,
                InventoryItemId = item.Id, ReceivedQuantity = 100, AcceptedQuantity = 100 });
            ReceiptMovement = Add(new InventoryMovement { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id,
                MovementType = InventoryMovementType.PurchaseReceipt, Direction = MovementDirection.In, Quantity = 100, UnitCost = 10 * receiptRate,
                TotalValue = 1000 * receiptRate, RunningBalance = 100, RunningValue = 1000 * receiptRate, ReferenceId = receipt.Id,
                ReferenceType = ReferenceType.PO, IsPosted = true, MovementNumber = "IMV-00001", CreatedAt = new DateTime(2026, 9, 1), MovementDate = new DateTime(2026, 9, 1) });
            if (method == ValuationMethod.FIFO)
            {
                var layer = Add(new InventoryLayer { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id, SourceId = receipt.Id,
                    OriginalQuantity = 100, RemainingQuantity = retained, UnitCost = 10 * receiptRate, RemainingValue = retained * 10 * receiptRate, LayerDate = ReceiptMovement.MovementDate });
                ReceiptMovement.CostLayerId = layer.Id;
            }
            if (retained < 100) Add(new InventoryMovement { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id,
                MovementType = InventoryMovementType.RequisitionIssue, Direction = MovementDirection.Out, Quantity = 100 - retained,
                TotalValue = (100 - retained) * 10 * receiptRate, RunningBalance = retained, RunningValue = retained * 10 * receiptRate,
                IsPosted = true, MovementNumber = "IMV-00002", CreatedAt = new DateTime(2026, 9, 2), MovementDate = new DateTime(2026, 9, 2) });
            Balance = Add(new InventoryBalance { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id,
                QuantityOnHand = retained, QuantityAvailable = retained, TotalValue = retained * 10 * receiptRate, AverageUnitCost = 10 * receiptRate });
            var originalJournal = Add(new JournalEntry { AccountingBookId = book, BookClassification = "IFRS", PostingStatus = "Posted" });
            Add(new FinancePostingEvent { SourceDocumentId = receipt.Id, SourceDocumentType = "ProcurementPurchaseOrderReceipt", PostingAction = "PostAcceptedInventoryReceipt",
                PostingStatus = "Posted", JournalEntryId = originalJournal.Id, AccountingBookId = book, FunctionalCurrencyCode = "GHS" });
            OriginalAccrual = Add(new AccountTransaction { JournalEntryId = originalJournal.Id, AccountId = grni.Id, SourceDocumentLineId = item.Id,
                AccountingBookId = book, FunctionalCurrencyCode = "GHS", TransactionTag = "INV-RECEIPT-GRV-ACCRUAL", CreditAmount = 1000 * receiptRate });
            Add(new AccountTransaction { JournalEntryId = originalJournal.Id, AccountId = inventoryAccount.Id, SourceDocumentLineId = item.Id,
                AccountingBookId = book, FunctionalCurrencyCode = "GHS", TransactionTag = "INV-RECEIPT-CONTROL", DebitAmount = 1000 * receiptRate });
            Add(new ProcurementReceiptCostBasis { PurchaseOrderReceiptId = receipt.Id, PurchaseOrderReceiptItemId = receiptLine.Id, PurchaseOrderItemId = orderLine.Id,
                InventoryItemId = item.Id, InventoryMovementId = ReceiptMovement.Id, WarehouseId = wh.Id, LocationId = location.Id,
                PurchaseQuantity = 100, BaseQuantity = 100, ConversionToBase = 1, PurchaseCurrency = order.Currency, FunctionalCurrency = "GHS",
                ExchangeRateToFunctional = receiptRate, PurchaseUnitCost = 10, PurchaseAmount = 1000, FunctionalAccrualAmount = 1000 * receiptRate,
                FunctionalInventoryAmount = 1000 * receiptRate });
            var accepted = new Mock<IProcurementAcceptedSupplyService>();
            accepted.Setup(x => x.GetGoodsReceiptLinesAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProcurementAcceptedReceiptLineDto> {
                new() { PurchaseOrderId = order.Id, PurchaseOrderItemId = orderLine.Id, PurchaseOrderReceiptId = receipt.Id, PurchaseOrderReceiptItemId = receiptLine.Id,
                    InspectionCaseId = Guid.NewGuid(), GoodsReceiptNoteId = grn.Id, GoodsReceiptNoteItemId = grnLine.Id, ReceiptDate = receipt.ReceiptDate, AcceptedQuantity = 100, UnitPrice = 10 } });
            accepted.Setup(x => x.ResolveAsync(ProcurementAcceptedSupplyKind.GoodsReceiptInspection, order.Id, order.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAcceptedSupplyResolutionDto { Kind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection, SourceId = order.Id,
                    PurchaseOrderId = order.Id, BusinessPartnerId = partner.Id, CurrencyCode = order.Currency, SourceReference = "GRN", Category = ProcurementCategoryClass.Goods,
                    Lines = [new() { PurchaseOrderItemId = orderLine.Id, AcceptedQuantity = 100, UnitPrice = 10 }] });
            var config = new Mock<IProcurementConfigurationService>();
            config.Setup(x => x.GetEffectiveProfileAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementConfigurationProfileDto { Id = Guid.NewGuid(), ProfileCode = "TDC-PROCUREMENT", Version = 1 });
            var events = new Mock<IProcurementControlEventService>();
            events.Setup(x => x.RecordAsync(It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementControlEventDto { Id = Guid.NewGuid() });
            var taxes = new Mock<ITaxCalculationEngine>();
            taxes.Setup(x => x.CalculateTaxesAsync(It.IsAny<TaxCalculationRequestDto>(), It.IsAny<CancellationToken>())).ReturnsAsync((TaxCalculationRequestDto request, CancellationToken _) =>
                new TaxCalculationResultDto { TotalTaxAmount = request.BaseAmount * .1m, TaxBreakdowns = [new TaxBreakdownDto {
                    TaxId = Guid.NewGuid(), TaxCode = "VAT", TaxName = "VAT", TaxRate = 10, TaxableAmount = request.BaseAmount,
                    TaxAmount = request.BaseAmount * .1m, IsInputTaxDeductible = true, TaxReceivableAccountId = inputTax.Id }] });
            finance.Setup(x => x.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>())).ReturnsAsync((FinancePostingRequestV2Dto request, CancellationToken _) => Post(request));
            finance.Setup(x => x.GetReversalPlanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid eventId, string reason, DateTime? date, CancellationToken _) => {
                    var original = Rows<FinancePostingEvent>().Single(x => x.Id == eventId);
                    return new FinanceReversalPlanDto { IsDefined = true, OriginalPostingEventId = eventId,
                        OriginalJournalEntryId = original.JournalEntryId!.Value, ReversalDate = date!.Value, Reason = reason,
                        ReversalLines = Rows<AccountTransaction>().Where(x => x.JournalEntryId == original.JournalEntryId).Select(x => new FinancePostingLineDto {
                            AccountId = x.AccountId, DebitAmount = x.CreditAmount, CreditAmount = x.DebitAmount,
                            TransactionDebitAmount = x.TransactionCreditAmount, TransactionCreditAmount = x.TransactionDebitAmount,
                            TransactionCurrency = x.TransactionCurrency, ExchangeRate = 1,
                            SourceDocumentLineId = x.SourceDocumentLineId, TransactionTag = x.TransactionTag }).ToArray() };
                });
            Service = new VendorInvoiceService(unit.Object, user.Object, valuation, NullLogger<VendorInvoiceService>.Instance,
                Moq.Mock.Of<IDocumentNumberingService>(), Moq.Mock.Of<IWorkflowService>(), financePostingEngine: finance.Object, taxEngine: taxes.Object,
                procurementConfiguration: config.Object, procurementControlEvents: events.Object, acceptedSupply: accepted.Object);
        }

        public VendorInvoice Invoice(decimal quantity, decimal price, decimal discountPercent = 0, decimal taxPercent = 0)
        {
            var net = decimal.Round(quantity * price * (1 - discountPercent / 100), 2);
            var invoice = Add(new VendorInvoice { InvoiceNumber = $"INV-{Rows<VendorInvoice>().Count + 1}", BusinessPartnerId = partner.Id,
                BusinessPartner = partner, BusinessPartnerRoleId = role.Id, BusinessPartnerApProfileVersionId = profile.Id,
                SupplierName = partner.PartnerName, PurchaseOrderId = order.Id, AcceptedSupplyKind = ProcurementAcceptedSupplyKind.GoodsReceiptInspection,
                AcceptedSupplySourceId = order.Id, Status = VendorInvoiceStatus.Approved, ApprovalRequired = false, ApprovalStatus = "NotRequired",
                CurrencyCode = order.Currency, ExchangeRate = invoiceRate, InvoiceDate = new DateTime(2026, 9, 10),
                SubTotal = quantity * price, DiscountAmount = quantity * price - net, TaxAmount = net * taxPercent / 100, TotalAmount = net * (1 + taxPercent / 100) });
            var line = Add(new VendorInvoiceLineItem { VendorInvoiceId = invoice.Id, VendorInvoice = invoice, PurchaseOrderItemId = orderLine.Id,
                PurchaseOrderItem = orderLine, InventoryItemId = item.Id, InventoryItem = item, Quantity = quantity, UnitPrice = price,
                DiscountPercentage = discountPercent, DiscountAmount = quantity * price - net, TaxAmount = net * taxPercent / 100,
                TaxRate = taxPercent, TaxTreatment = taxPercent == 0 ? TaxTreatment.OutOfScope : TaxTreatment.Standard, Description = "Stock", LineItemType = "Product" });
            invoice.LineItems.Add(line); return invoice;
        }

        public Task<decimal> Issue(decimal quantity) => valuation.ProcessIssueAsync(item.Id, Balance.WarehouseId, Balance.LocationId,
            quantity, InventoryMovementType.RequisitionIssue, ReferenceType.Requisition, "ISSUE-AFTER-INVOICE", Guid.NewGuid());

        public InventoryBalance SeedRetainedTransit(decimal quantity)
        {
            var wh = Add(new Warehouse { Code = "TRANSIT", Name = "Transit", WarehouseType = "Transit" });
            var location = Add(new WarehouseLocation { WarehouseId = wh.Id, LocationCode = "TRANSIT", IsInTransitLocation = true,
                LocationHierarchyType = WarehouseLocationType.InTransit, IsPickingLocation = false, IsReceivingLocation = false });
            var dispatch = Guid.NewGuid();
            Balance.QuantityOnHand -= quantity; Balance.TotalValue -= quantity * 10;
            var transit = Add(new InventoryBalance { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id,
                QuantityOnHand = quantity, TotalValue = quantity * 10, AverageUnitCost = 10 });
            var source = Add(new InventoryMovement { InventoryItemId = item.Id, WarehouseId = Balance.WarehouseId, LocationId = Balance.LocationId,
                MovementType = InventoryMovementType.TransferOut, Direction = MovementDirection.Out, Quantity = quantity, TotalValue = quantity * 10,
                RunningBalance = Balance.QuantityOnHand, RunningValue = Balance.TotalValue, IsPosted = true, TransferDispatchAllocationId = dispatch,
                TransferLeg = "SourceOut", MovementNumber = "IMV-00002", CreatedAt = new DateTime(2026, 9, 2), MovementDate = new DateTime(2026, 9, 2) });
            var incoming = Add(new InventoryMovement { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id,
                MovementType = InventoryMovementType.TransferIn, Direction = MovementDirection.In, Quantity = quantity, TotalValue = quantity * 10,
                RunningBalance = quantity, RunningValue = quantity * 10, IsPosted = true, TransferDispatchAllocationId = dispatch,
                TransferLeg = "TransitIn", MovementNumber = "IMV-00003", CreatedAt = new DateTime(2026, 9, 2).AddSeconds(1), MovementDate = new DateTime(2026, 9, 2) });
            if (item.ValuationMethod == ValuationMethod.FIFO)
            {
                var original = Rows<InventoryLayer>().Single(); original.RemainingQuantity -= quantity; original.RemainingValue -= quantity * 10;
                var layer = Add(new InventoryLayer { InventoryItemId = item.Id, WarehouseId = wh.Id, LocationId = location.Id,
                    SourceId = dispatch, OriginalQuantity = quantity, RemainingQuantity = quantity, UnitCost = 10,
                    RemainingValue = quantity * 10, LayerDate = original.LayerDate });
                incoming.CostLayerId = layer.Id;
            }
            return transit;
        }

        private FinancePostingResultDto Post(FinancePostingRequestV2Dto request)
        {
            if (FailFinance) throw new InvalidOperationException("Fiscal period is closed.");
            Requests.Add(request);
            Assert.Equal(request.Lines.Sum(x => x.DebitAmount), request.Lines.Sum(x => x.CreditAmount));
            var old = Rows<FinancePostingEvent>().SingleOrDefault(x => x.SourceDocumentId == request.SourceDocumentId && x.SourceDocumentType == request.SourceDocumentType && x.PostingAction == request.PostingAction);
            var duplicate = old is not null;
            if (old is null)
            {
                var journal = Add(new JournalEntry { SourceDocumentId = request.SourceDocumentId, SourceDocumentType = request.SourceDocumentType,
                    AccountingBookId = book, BookClassification = "IFRS", PostingStatus = "Posted" });
                if (request.ReversalOfJournalEntryId.HasValue)
                {
                    var original = Rows<JournalEntry>().Single(x => x.Id == request.ReversalOfJournalEntryId);
                    original.IsReversed = true; original.ReversalJournalEntryId = journal.Id;
                }
                old = Add(new FinancePostingEvent { SourceModule = "AP", SourceDocumentId = request.SourceDocumentId, SourceDocumentType = request.SourceDocumentType,
                    PostingAction = request.PostingAction, PostingStatus = "Posted", PostingDate = request.PostingDate,
                    JournalEntryId = journal.Id, AccountingBookId = book, BookClassification = "IFRS", FunctionalCurrencyCode = "GHS" });
                foreach (var line in request.Lines) Add(new AccountTransaction { JournalEntryId = journal.Id, AccountId = line.AccountId,
                    SourceDocumentLineId = line.SourceDocumentLineId, TransactionTag = line.TransactionTag, DebitAmount = line.DebitAmount, CreditAmount = line.CreditAmount,
                    AccountingBookId = book, FunctionalCurrencyCode = "GHS", TransactionCurrency = line.TransactionCurrency,
                    TransactionDebitAmount = line.TransactionDebitAmount, TransactionCreditAmount = line.TransactionCreditAmount, LineNumber = line.LineNumber ?? 0 });
            }
            return new FinancePostingResultDto { PostingEventId = old.Id, JournalEntryId = old.JournalEntryId!.Value,
                PostingDate = request.PostingDate, PostingStatus = "Posted", FunctionalCurrencyCode = "GHS", WasDuplicate = duplicate,
                TotalDebitAmount = request.Lines.Sum(x => x.DebitAmount), TotalCreditAmount = request.Lines.Sum(x => x.CreditAmount) };
        }

        private Account Account(string code, bool control) => Add(new Account { AccountCode = code, AccountName = code, Status = AccountStatus.Active,
            IsControlAccount = control, AllowDirectPosting = !control, AccountType = control ? AccountType.Asset : AccountType.Expense });
        private T Add<T>(T row) where T : BaseEntity
        {
            if (row is TenantEntity t) t.TenantId = tenant;
            // Some legacy AP entities redeclare TenantId; populate their persisted
            // property as well as the inherited tenant contract in this fixture.
            row.GetType().GetProperty(nameof(TenantEntity.TenantId))?.SetValue(row, tenant);
            Rows<T>().Add(row); return row;
        }
        public List<T> Rows<T>() where T : BaseEntity { if (!rows.TryGetValue(typeof(T), out var list)) rows[typeof(T)] = list = new List<T>(); return (List<T>)list; }
        protected override object? GetDefaultValue(Type type, Mock mock)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGenericRepository<>))
                return typeof(Fixture).GetMethod(nameof(Repository), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type.GenericTypeArguments[0]).Invoke(this, null);
            if (type == typeof(Task)) return Task.CompletedTask;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        private IGenericRepository<T> Repository<T>() where T : BaseEntity
        {
            if (repos.TryGetValue(typeof(T), out var existing)) return (IGenericRepository<T>)existing;
            var list = Rows<T>(); var mock = new Mock<IGenericRepository<T>>();
            mock.Setup(x => x.GetQueryable()).Returns(() => new AsyncQuery<T>(list));
            mock.Setup(x => x.GetQueryable(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> p) => new AsyncQuery<T>(list.Where(p.Compile())));
            mock.Setup(x => x.GetQueryableIncludingDeleted(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> p) => new AsyncQuery<T>(list.Where(p.Compile())));
            mock.Setup(x => x.GetAddedEntities()).Returns(() => list.ToArray());
            mock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> p) => list.FirstOrDefault(p.Compile()));
            mock.Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> p) => list.Any(p.Compile()));
            mock.Setup(x => x.UpdateAsync(It.IsAny<T>())).Returns(Task.CompletedTask);
            mock.Setup(x => x.AddAsync(It.IsAny<T>())).ReturnsAsync((T row) => { Link(row); list.Add(row); return row; });
            mock.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<T>>())).ReturnsAsync((IEnumerable<T> values) => { var added = values.ToArray(); foreach (var row in added) Link(row); list.AddRange(added); return added; });
            repos[typeof(T)] = mock.Object; return mock.Object;
        }
        private void Link(BaseEntity row)
        {
            if (row is VendorInvoiceReceiptAllocation a) { a.VendorInvoice = Rows<VendorInvoice>().Single(x => x.Id == a.VendorInvoiceId);
                a.VendorInvoiceLineItem = Rows<VendorInvoiceLineItem>().Single(x => x.Id == a.VendorInvoiceLineItemId); }
        }
    }

    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> values) : base(values) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default) => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
    private sealed class AsyncProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new AsyncQuery<T>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new AsyncQuery<TElement>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken token = default) =>
            (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(TResult).GenericTypeArguments[0]).Invoke(null, [inner.Execute(expression)])!;
    }
    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}
