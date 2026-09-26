using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public class ProcurementAutoInvoiceTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Mock<IUnitOfWork> _unit = new();
    private readonly List<VendorInvoice> _invoices = new();
    private readonly List<VendorInvoiceLineItem> _lines = new();
    private readonly List<BusinessPartner> _partners = new();
    private readonly List<Supplier> _suppliers = new();
    private readonly LandedCost _cost;
    private readonly LandedCostItem _freight;
    private readonly LandedCostItem _handling;
    private readonly BusinessPartner _partner;
    private readonly Supplier _supplier;
    private readonly Account _accrual;
    private readonly Account _ap;
    private readonly JournalEntry _journal;
    private readonly FinancePostingEvent _posting;
    private readonly AccountTransaction _credit;
    private readonly VendorInvoiceService _service;
    private readonly FinancePostingProducerContext _producer = new(FinanceDimensionRouteId.FinanceApVendorInvoice);
    private bool _transaction;
    private readonly List<PurchaseOrder> _orders = new();
    private readonly List<PurchaseOrderItem> _orderItems = new();
    private readonly List<GoodsReceiptNote> _receipts = new();
    private readonly List<GoodsReceiptNoteItem> _receiptItems = new();
    private readonly List<VendorInvoiceReceiptAllocation> _allocations = new();
    private readonly List<ProcurementAcceptedReceiptLineDto> _acceptedLines = new();
    private readonly Mock<IProcurementAcceptedSupplyService> _accepted = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();
    private readonly Mock<IProcurementConfigurationService> _configuration = new();
    private readonly Mock<ILandedCostService> _landed = new();
    private readonly List<LandedCostSupplierDocument> _documents = new();
    private readonly FinanceSettings _settings;
    private readonly GoodsReceiptNote _receipt;
    private readonly Mock<IWorkflowIntegrationService> _approval = new();
    private readonly Mock<IFinancePostingEngine> _finance = new();

    public ProcurementAutoInvoiceTests()
    {
        _partner = new BusinessPartner { TenantId = _tenant, PartnerCode = "CARRIER", PartnerName = "Carrier", PartnerType = "Supplier", IsActive = true, RegistrationStatus = "Approved" };
        _supplier = new Supplier { TenantId = _tenant, SupplierCode = "CARRIER", Name = "Carrier", IsActive = true, Status = "Active" };
        _accrual = new Account { TenantId = _tenant, AccountCode = "2110", AccountName = "Accrual", Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false };
        _ap = new Account { TenantId = _tenant, AccountCode = "2100", AccountName = "AP", Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false };
        _cost = new LandedCost { TenantId = _tenant, LandedCostNumber = "LC-TEST", Status = "Posted", Currency = "GHS", TotalCost = 360 };
        _freight = new LandedCostItem { TenantId = _tenant, LandedCostId = _cost.Id, LandedCost = _cost, Description = "Freight", Amount = 310, AmountInBaseCurrency = 310, Currency = "GHS", ExchangeRate = 1 };
        _handling = new LandedCostItem { TenantId = _tenant, LandedCostId = _cost.Id, LandedCost = _cost, Description = "Handling", Amount = 50, AmountInBaseCurrency = 50, Currency = "GHS", ExchangeRate = 1 };
        _cost.Items = new List<LandedCostItem> { _freight, _handling };
        _receipt = new GoodsReceiptNote { TenantId = _tenant, GRNNumber = "GRN-LC-TEST" };
        _cost.GoodsReceiptNoteId = _receipt.Id;
        Repo(new List<GoodsReceiptNote> { _receipt }); Repo(_documents); Repo(new List<AuditLog>());
        _journal = new JournalEntry { TenantId = _tenant };
        _posting = new FinancePostingEvent { TenantId = _tenant, SourceDocumentId = _cost.Id, SourceModule = "Inventory", SourceDocumentType = "InventoryLandedCost", PostingAction = "PostLandedCost", PostingStatus = "Posted", JournalEntryId = _journal.Id };
        _credit = new AccountTransaction { TenantId = _tenant, JournalEntryId = _journal.Id, AccountId = _accrual.Id, CreditAmount = 360, FunctionalCurrencyCode = "GHS" };
        Repo(new List<LandedCost> { _cost }); Repo(new List<LandedCostItem> { _freight, _handling });
        _partners.Add(_partner); _suppliers.Add(_supplier); Repo(_partners); Repo(_suppliers);
        Repo(new List<ApSupplierIdentityLink>());
        Repo(new List<JournalEntry> { _journal }); Repo(new List<FinancePostingEvent> { _posting });
        Repo(new List<AccountTransaction> { _credit }); Repo(new List<Account> { _accrual, _ap });
        _settings = new FinanceSettings { TenantId = _tenant, BaseCurrency = "GHS", ControlAccountApId = _ap.Id, ControlAccountGRVAccrualId = Guid.NewGuid() };
        Repo(new List<FinanceSettings> { _settings });
        Repo(new List<PaymentTerm>()); Repo(new List<FinancePurchaseOrderReceipt>()); Repo(_lines);
        var invoiceRepo = Repo(_invoices);
        invoiceRepo.Setup(r => r.AddAsync(It.IsAny<VendorInvoice>())).ReturnsAsync((VendorInvoice invoice) =>
        {
            _invoices.Add(invoice); invoice.Supplier = _suppliers.Single(s => s.Id == invoice.SupplierId);
            foreach (var line in invoice.LineItems) { line.VendorInvoice = invoice; _lines.Add(line); }
            return invoice;
        });
        _unit.SetupGet(u => u.HasActiveTransaction).Returns(() => _transaction);
        _unit.Setup(u => u.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>())).Callback(() => _transaction = true).Returns(Task.CompletedTask);
        _unit.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => _transaction = false).Returns(Task.CompletedTask);
        _unit.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Callback(() => _transaction = false).Returns(Task.CompletedTask);
        _unit.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task<List<VendorInvoiceDto>>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<List<VendorInvoiceDto>>> action, CancellationToken _) => action());
        _unit.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task<VendorInvoiceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<VendorInvoiceDto>> action, CancellationToken _) => action());
        _unit.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        var user = new Mock<ICurrentUserService>(); user.SetupGet(u => u.TenantId).Returns(_tenant);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString()); user.SetupGet(u => u.UserName).Returns("AP officer");
        var numbering = new Mock<IDocumentNumberingService>(); var number = 0;
        numbering.Setup(n => n.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => $"INV-{++number}");
        var supplierIdentity = new Mock<IApSupplierIdentityService>();
        supplierIdentity.Setup(service => service.ResolveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid partnerId, CancellationToken _) =>
            {
                var partner = _partners.Single(item => item.Id == partnerId);
                var supplier = _suppliers.Single(item =>
                    string.Equals(item.SupplierCode, partner.PartnerCode, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(new ApSupplierIdentityDto
                {
                    BusinessPartnerId = partner.Id,
                    SupplierId = supplier.Id,
                    PartnerCode = partner.PartnerCode,
                    SupplierCode = supplier.SupplierCode,
                    DisplayName = supplier.Name,
                    IsVerified = true
                });
            });
        _partner.ApprovalStatus = "Approved";
        _settings.ControlAccountGRVAccrualId = _accrual.Id;
        Repo(new List<PurchaseOrderReceipt>()); Repo(new List<InventoryMovement>());
        Repo(_orders); Repo(_orderItems); Repo(_receipts); Repo(_receiptItems);
        var allocationRepo = Repo(_allocations);
        allocationRepo.Setup(r => r.AddAsync(It.IsAny<VendorInvoiceReceiptAllocation>())).ReturnsAsync((VendorInvoiceReceiptAllocation a) =>
        { a.VendorInvoice = _invoices.Single(i => i.Id == a.VendorInvoiceId); a.VendorInvoiceLineItem = _lines.Single(l => l.Id == a.VendorInvoiceLineItemId); _allocations.Add(a); return a; });
        _accepted.Setup(a => a.GetGoodsReceiptLinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => (IReadOnlyList<ProcurementAcceptedReceiptLineDto>)_acceptedLines.Where(l => l.PurchaseOrderId == id).ToList());
        _access.Setup(a => a.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        _configuration.Setup(c => c.GetEffectiveProfileAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementConfigurationProfileDto { Id = Guid.NewGuid(), ProfileCode = "TDC-PROCUREMENT", Version = 1 });
        _accepted.Setup(a => a.ResolveAsync(It.IsAny<ProcurementAcceptedSupplyKind>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAcceptedSupplyKind kind, Guid source, Guid? po, Guid? invoice, CancellationToken _) => new ProcurementAcceptedSupplyResolutionDto
            {
                Kind = kind, SourceId = source, PurchaseOrderId = po, SourceReference = "Approved inspections", BusinessPartnerId = _partner.Id,
                CurrencyCode = "GHS", Category = ProcurementCategoryClass.Goods,
                Lines = _acceptedLines.Where(l => l.PurchaseOrderId == po).GroupBy(l => l.PurchaseOrderItemId)
                    .Select(g => new ProcurementAcceptedSupplyLineDto { PurchaseOrderItemId = g.Key, AcceptedQuantity = g.Sum(l => l.NetAcceptedQuantity), UnitPrice = 20 }).ToList()
            });
        _service = new VendorInvoiceService(_unit.Object, user.Object, Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<VendorInvoiceService>>(), numbering.Object, Mock.Of<IWorkflowService>(), financePostingEngine: _finance.Object,
            sourceDimensions: Mock.Of<IFinanceSourceDimensionService>(), landedCosts: _landed.Object,
            workflowIntegration: _approval.Object, apSupplierIdentityService: supplierIdentity.Object, acceptedSupply: _accepted.Object, receiptAccess: _access.Object,
            procurementConfiguration: _configuration.Object, procurementControlEvents: Mock.Of<IProcurementControlEventService>());
        _landed.Setup(s => s.PostToInventoryAsync(_cost.Id, It.IsAny<Guid>())).ReturnsAsync(() => { _cost.Status = "Posted"; return true; });
    }


    [Theory]
    [InlineData(EstatePayableKind.SurveyorFee)]
    [InlineData(EstatePayableKind.VendorConsideration)]
    [InlineData(EstatePayableKind.StampDuty)]
    [InlineData(EstatePayableKind.OtherAcquisitionCosts)]
    public async Task EstatePayableCreateReadAndTaxReviewPreserveCanonicalSupplierAndSource(EstatePayableKind kind)
    {
        _partner.SubjectToWithholdingDeduction = true;
        _partner.WithholdingTaxRate = 7.5m;
        Repo(new List<Tax>());
        var acquisition = new LandAcquisition
        {
            TenantId = _tenant,
            StageOrder = (int)(kind == EstatePayableKind.SurveyorFee ? AcquisitionProcedure.CadastralSurvey :
                kind == EstatePayableKind.VendorConsideration ? AcquisitionProcedure.VendorPayment : AcquisitionProcedure.StampDutyPayment),
            WorkspaceDataJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["2"] = new { surveyorFeeAmount = 200m },
                ["6"] = new { agreementPaymentAmount = 200m },
                ["8"] = new { agreedAmount = 200m },
                ["14"] = new { otherAcquisitionServicesJson = "[{\"serviceName\":\"Legal costs\",\"amount\":200}]" }
            }),
            StampDutyAssessment = new StampDutyAssessment { TenantId = _tenant, IsApproved = true, DutyAmount = 200m }
        };
        Repo(new List<LandAcquisition> { acquisition });
        var create = new VendorInvoiceCreateDto
        {
            EstateAcquisitionId = acquisition.Id, EstatePayableKind = kind,
            SupplierId = _supplier.Id, SupplierInvoiceNumber = $"ESTATE-{kind}",
            InvoiceDate = new DateTime(2026, 9, 24), DueDate = new DateTime(2026, 10, 24),
            CurrencyCode = "GHS", ExchangeRate = 1m, Reference = $"Estate source {kind}",
            ApplyBusinessPartnerDefaults = false, ApplySupplierWithholdingDefaults = null,
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new() { LineItemType = "Service", Description = $"Estate {kind}", GLAccountId = _accrual.Id,
                    Quantity = 1m, UnitPrice = 200m, TaxTreatment = TaxTreatment.PendingReview }
            }
        };
        var created = await _service.CreateAsync(create);
        Assert.Equal(_supplier.Id, created.SupplierId);
        Assert.Equal(acquisition.Id, created.EstateAcquisitionId);
        Assert.Equal(kind, created.EstatePayableKind);
        Assert.Equal(200m, created.SubTotal);
        Assert.True(created.WithholdingDecisionPending);
        Assert.Null(created.ApplySupplierWithholdingDefaults);
        Assert.Equal(TaxTreatment.PendingReview, Assert.Single(created.LineItems).TaxTreatment);
        var read = Assert.IsType<VendorInvoiceDto>(await _service.GetByIdAsync(created.Id));
        Assert.Equal(created.EstateAcquisitionId, read.EstateAcquisitionId);
        Assert.Equal(kind, read.EstatePayableKind);
        var original = Assert.Single(_invoices.Single().LineItems);
        var update = new VendorInvoiceUpdateDto
        {
            Id = created.Id, InvoiceDate = create.InvoiceDate, DueDate = create.DueDate,
            CurrencyCode = create.CurrencyCode, ExchangeRate = create.ExchangeRate, Reference = create.Reference,
            ApplySupplierWithholdingDefaults = false,
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new() { Id = original.Id, LineItemType = original.LineItemType, Description = original.Description,
                    GLAccountId = original.GLAccountId, Quantity = original.Quantity, UnitPrice = original.UnitPrice,
                    TaxTreatment = TaxTreatment.OutOfScope }
            }
        };
        var reviewed = await _service.UpdateAsync(update);
        Assert.Equal(_supplier.Id, reviewed.SupplierId);
        Assert.Equal(acquisition.Id, reviewed.EstateAcquisitionId);
        Assert.Equal(kind, reviewed.EstatePayableKind);
        Assert.Equal(200m, reviewed.SubTotal);
        Assert.Equal(200m, reviewed.TotalAmount);
        Assert.False(reviewed.WithholdingDecisionPending);
        Assert.False(reviewed.ApplySupplierWithholdingDefaults);
        Assert.Equal(original.Id, Assert.Single(reviewed.LineItems).Id);
        Assert.Equal(TaxTreatment.OutOfScope, Assert.Single(reviewed.LineItems).TaxTreatment);
        var persisted = Assert.IsType<VendorInvoiceDto>(await _service.GetByIdAsync(created.Id));
        Assert.Equal(TaxTreatment.OutOfScope, Assert.Single(persisted.LineItems).TaxTreatment);
        Assert.Null(persisted.JournalEntryId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(create));
        Assert.Single(_invoices);
        acquisition.WorkspaceDataJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["2"] = new { surveyorFeeAmount = 201m },
            ["6"] = new { agreementPaymentAmount = 201m },
            ["8"] = new { agreedAmount = 200m },
            ["14"] = new { otherAcquisitionServicesJson = "[{\"serviceName\":\"Legal costs\",\"amount\":201}]" }
        });
        acquisition.StampDutyAssessment.DutyAmount = 201m;
        var staleSource = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(update));
        Assert.Contains("source amount changed", staleSource.Message);
    }

    private ProcurementAcceptedReceiptLineDto AddReceipt(decimal accepted = 10m, decimal returned = 0m, PurchaseOrderItem? item = null)
    {
        if (item == null)
        {
            var order = new PurchaseOrder { TenantId = _tenant, BusinessPartnerId = _partner.Id, BusinessPartner = _partner, OrderNumber = $"PO-{_orders.Count + 1}",
                Status = "Approved", Currency = "GHS", ProcurementCategory = ProcurementCategoryClass.Goods, OrderDate = new DateTime(2026, 9, 1) };
            item = new PurchaseOrderItem { TenantId = _tenant, PurchaseOrderId = order.Id, PurchaseOrder = order,
                ItemDescription = "Accepted product", UnitOfMeasure = "EA", OrderedQuantity = 100, UnitPrice = 20 };
            order.Items.Add(item); _orders.Add(order); _orderItems.Add(item);
        }
        var receipt = new GoodsReceiptNote { TenantId = _tenant, PurchaseOrderId = item.PurchaseOrderId, SupplierId = _partner.Id,
            GRNNumber = $"GRN-{_receipts.Count + 1}", StockUpdated = true, WarehouseId = Guid.NewGuid(), ReceiptDate = new DateTime(2026, 9, 2).AddDays(_receipts.Count) };
        var line = new GoodsReceiptNoteItem { TenantId = _tenant, GoodsReceiptNoteId = receipt.Id, GoodsReceiptNote = receipt,
            PurchaseOrderItemId = item.Id, AcceptedQuantity = accepted, ReceivedQuantity = accepted };
        _receipts.Add(receipt); _receiptItems.Add(line);
        var source = new ProcurementAcceptedReceiptLineDto { PurchaseOrderId = item.PurchaseOrderId, PurchaseOrderItemId = item.Id,
            PurchaseOrderReceiptId = Guid.NewGuid(), PurchaseOrderReceiptItemId = Guid.NewGuid(), InspectionCaseId = Guid.NewGuid(),
            GoodsReceiptNoteId = receipt.Id, GoodsReceiptNoteItemId = line.Id, ReceiptDate = receipt.ReceiptDate,
            ReceiptNumber = receipt.GRNNumber, AcceptedQuantity = accepted, ReturnedQuantity = returned, UnitPrice = item.UnitPrice };
        _acceptedLines.Add(source); return source;
    }

    private ProcurementAutoInvoiceRequestDto Request(params (ProcurementAcceptedReceiptLineDto Source, decimal Quantity)[] selected) => new()
    {
        RequestId = Guid.NewGuid(), BusinessPartnerId = _partner.Id, SupplierInvoiceNumber = "SUP-BILL", InvoiceDate = new DateTime(2026, 9, 24),
        Lines = selected.Select(s => new ProcurementAutoInvoiceSelectionDto { GoodsReceiptNoteItemId = s.Source.GoodsReceiptNoteItemId!.Value, Quantity = s.Quantity }).ToList()
    };

    [Fact]
    public async Task ConcurrentRequestsForTheSameReceiptCannotBothReserveItsRemainingQuantity()
    {
        var source = AddReceipt();
        var scope = new AsyncLocal<List<SemaphoreSlim>?>();
        var locks = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>();
        var bothAtOrderLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contenders = 0;
        _unit.SetupGet(u => u.HasActiveTransaction).Returns(() => scope.Value != null);
        _unit.Setup(u => u.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .Callback(() => scope.Value = new List<SemaphoreSlim>()).Returns(Task.CompletedTask);
        // Model the unit-of-work's transaction-scoped resource locks. Both requests
        // reach the shared PO lock before either can read/reserve invoice quantities.
        _unit.Setup(u => u.AcquireTransactionLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string resource, CancellationToken token) =>
            {
                if (resource.StartsWith("tdc-ap-match:"))
                {
                    if (Interlocked.Increment(ref contenders) == 2) bothAtOrderLock.SetResult();
                    await bothAtOrderLock.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
                }
                var gate = locks.GetOrAdd(resource, _ => new SemaphoreSlim(1));
                await gate.WaitAsync(token); scope.Value!.Add(gate);
            });
        void Release()
        {
            foreach (var gate in scope.Value!.AsEnumerable().Reverse()) gate.Release();
            scope.Value = null;
        }
        _unit.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Callback(Release).Returns(Task.CompletedTask);
        _unit.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Callback(Release).Returns(Task.CompletedTask);
        async Task<Exception?> Attempt()
        {
            try { await _service.CreateAutoInvoiceAsync(Request((source, 6)), _producer); return null; }
            catch (Exception error) { return error; }
        }
        var results = await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        Assert.Single(results.Where(error => error == null));
        var failure = Assert.IsType<InvalidOperationException>(Assert.Single(results.Where(error => error != null)));
        Assert.Contains("no longer available", failure.Message);
        Assert.Single(_invoices); Assert.Equal(6m, Assert.Single(_allocations).Quantity);
    }

    [Fact]
    public async Task ConsolidatedReadinessMatchesEachPurchaseOrderAndRejectsLostAcceptance()
    {
        var a = AddReceipt(); var b = AddReceipt();
        var draft = await _service.CreateAutoInvoiceAsync(Request((a, 2), (b, 3)), _producer);
        var ready = await _service.GetThreeWayMatchReadinessAsync(draft.Id);
        Assert.True(ready.IsRequired); Assert.True(ready.ApprovalReady, ready.Message); Assert.True(ready.IsMatched);
        _acceptedLines.Remove(b);
        var changed = await _service.GetThreeWayMatchReadinessAsync(draft.Id);
        Assert.False(changed.ApprovalReady); Assert.False(changed.IsMatched);
        Assert.Contains(changed.Checks, c => c.CheckKey == "AP_MATCH_ACCEPTED_QUANTITY_EXCEEDED");
    }

    [Fact]
    public async Task QuantityToleranceCannotAuthorizeUnacceptedGoods()
    {
        var a = AddReceipt(92);
        _settings.ApInvoiceQuantityTolerancePercent = 20;
        var draft = await _service.CreateAutoInvoiceAsync(Request((a, 92)), _producer);
        _lines.Single().Quantity = 100; _allocations.Single().Quantity = 100;
        var result = await _service.GetThreeWayMatchReadinessAsync(draft.Id);
        Assert.False(result.ApprovalReady);
        Assert.Contains(result.Checks, c => c.CheckKey == "AP_MATCH_ACCEPTED_QUANTITY_EXCEEDED" && !c.Passed);
    }

    [Fact]
    public async Task NewRequestCannotReuseReservedDraftQuantity()
    {
        var a = AddReceipt(); await _service.CreateAutoInvoiceAsync(Request((a, 6)), _producer);
        Assert.Equal(4, Assert.Single(Assert.Single(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id)).Lines).AvailableQuantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAutoInvoiceAsync(Request((a, 5)), _producer));
        Assert.Single(_invoices); Assert.Single(_allocations);
    }

    [Fact]
    public async Task EligibleReceiptsDeductReturnsAndCommittedLegacyInvoicesInReceiptOrder()
    {
        var first = AddReceipt(10, 2); var second = AddReceipt(12, 0, _orderItems.Single());
        var invoice = new VendorInvoice { TenantId = _tenant, PurchaseOrderId = first.PurchaseOrderId, Status = VendorInvoiceStatus.Approved };
        invoice.LineItems.Add(new VendorInvoiceLineItem { TenantId = _tenant, PurchaseOrderItemId = first.PurchaseOrderItemId, Quantity = 11 });
        _invoices.Add(invoice);
        var receipt = Assert.Single(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id));
        Assert.Equal(second.GoodsReceiptNoteId, receipt.GoodsReceiptNoteId);
        var line = Assert.Single(receipt.Lines); Assert.Equal(9, line.AvailableQuantity); Assert.Equal(3, line.InvoicedQuantity);
    }

    [Fact]
    public async Task UnsubmittedManualDraftDoesNotReserveReceipts()
    {
        var source = AddReceipt();
        var invoice = new VendorInvoice { TenantId = _tenant, PurchaseOrderId = source.PurchaseOrderId, Status = VendorInvoiceStatus.Draft };
        invoice.LineItems.Add(new VendorInvoiceLineItem { TenantId = _tenant, PurchaseOrderItemId = source.PurchaseOrderItemId, Quantity = 10 }); _invoices.Add(invoice);
        Assert.Equal(10, Assert.Single(Assert.Single(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id)).Lines).AvailableQuantity);
    }

    [Theory]
    [InlineData(VendorInvoiceStatus.Draft)] [InlineData(VendorInvoiceStatus.Rejected)] [InlineData(VendorInvoiceStatus.Approved)]
    public async Task AutoInvoiceAllocationReservesQuantityAcrossEditableAndApprovedStates(VendorInvoiceStatus status)
    {
        var source = AddReceipt(10, 1);
        var invoice = new VendorInvoice { TenantId = _tenant, AutoInvoiceRequestId = Guid.NewGuid(), Status = status };
        var line = new VendorInvoiceLineItem { TenantId = _tenant, VendorInvoiceId = invoice.Id, PurchaseOrderItemId = source.PurchaseOrderItemId,
            PurchaseOrderItem = _orderItems.Single(), Quantity = 4, VendorInvoice = invoice };
        invoice.LineItems.Add(line); _invoices.Add(invoice);
        _allocations.Add(new VendorInvoiceReceiptAllocation { TenantId = _tenant, VendorInvoiceId = invoice.Id, VendorInvoice = invoice,
            VendorInvoiceLineItem = line, VendorInvoiceLineItemId = line.Id, PurchaseOrderId = source.PurchaseOrderId,
            PurchaseOrderItemId = source.PurchaseOrderItemId, PurchaseOrderReceiptItemId = source.PurchaseOrderReceiptItemId, Quantity = 4 });
        Assert.Equal(5, Assert.Single(Assert.Single(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id)).Lines).AvailableQuantity);
        invoice.Status = VendorInvoiceStatus.Voided;
        Assert.Equal(9, Assert.Single(Assert.Single(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id)).Lines).AvailableQuantity);
    }

    [Fact]
    public async Task WarehouseAuthorizationFiltersReceiptListAndRejectsDirectSelection()
    {
        var source = AddReceipt();
        _access.Setup(a => a.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });
        Assert.Empty(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAutoInvoiceAsync(Request((source, 1)), _producer));
        Assert.Empty(_invoices); _unit.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("foreign")] [InlineData("customer")] [InlineData("inactive")] [InlineData("unapproved")]
    public async Task SupplierEligibilityCannotBeBypassed(string invalid)
    {
        if (invalid == "foreign") _partner.TenantId = Guid.NewGuid();
        if (invalid == "customer") _partner.PartnerType = "Customer";
        if (invalid == "inactive") _partner.IsActive = false;
        if (invalid == "unapproved") _partner.ApprovalStatus = "Pending";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetAutoInvoiceReceiptsAsync(_partner.Id));
    }

    [Fact]
    public async Task MultiplePurchaseOrdersCreateOneDraftAndRetryReturnsSameInvoice()
    {
        var a = AddReceipt(); var b = AddReceipt(); var request = Request((a, 2), (b, 3));
        var created = await _service.CreateAutoInvoiceAsync(request, _producer);
        Assert.Equal(100, created.SubTotal); Assert.True(created.IsProcurementAutoInvoice); Assert.Null(created.PurchaseOrderId);
        Assert.Equal(2, _allocations.Count); Assert.Single(_invoices); Assert.Null(created.JournalEntryId);
        Assert.All(_lines, line => Assert.Equal(TaxTreatment.PendingReview, line.TaxTreatment));
        Assert.Equal(created.Id, (await _service.CreateAutoInvoiceAsync(request, _producer)).Id);
        Assert.Single(_invoices); Assert.Equal(2, _allocations.Count);
        request.Lines[0].Quantity = 3;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAutoInvoiceAsync(request, _producer));
        _finance.Verify(f => f.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Draft")] [InlineData("PendingApproval")] [InlineData("Rejected")]
    [InlineData("Cancelled")] [InlineData("Unknown")]
    public async Task UnapprovedPurchaseOrderCannotSupplyAutoInvoice(string status)
    {
        var source = AddReceipt();
        _orders.Single().Status = status;
        Assert.Empty(await _service.GetAutoInvoiceReceiptsAsync(_partner.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAutoInvoiceAsync(Request((source, 1)), _producer));
        Assert.Empty(_invoices);
    }

    [Fact]
    public async Task MatchingRejectsPurchaseOrderWhoseApprovalWasWithdrawn()
    {
        var source = AddReceipt();
        var created = await _service.CreateAutoInvoiceAsync(Request((source, 1)), _producer);
        _orders.Single().Status = "Rejected";
        var readiness = await _service.GetThreeWayMatchReadinessAsync(created.Id);
        Assert.False(readiness.ApprovalReady);
        Assert.Contains(readiness.Checks, check => check.CheckKey == "AP_MATCH_PO_NOT_APPROVED" && !check.Passed);
    }

    [Theory]
    [InlineData("currency")] [InlineData("quantity")] [InlineData("supplier")] [InlineData("unposted")]
    public async Task StaleOrIncompatibleSelectionRollsBackBeforeInvoiceCreation(string invalid)
    {
        var a = AddReceipt(); var b = AddReceipt();
        if (invalid == "currency") _orders[1].Currency = "USD";
        if (invalid == "supplier") _receipts[1].SupplierId = Guid.NewGuid();
        if (invalid == "unposted") _receipts[1].StockUpdated = false;
        var request = Request((a, 2), (b, invalid == "quantity" ? 11 : 3));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAutoInvoiceAsync(request, _producer));
        Assert.Empty(_invoices); Assert.Empty(_allocations);
        _unit.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    private Mock<IGenericRepository<T>> Repo<T>(List<T> rows) where T : BaseEntity
    {
        var repo = new Mock<IGenericRepository<T>>();
        repo.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> p) => new AsyncQuery<T>(rows.Where(r => !r.IsDeleted).Where(p.Compile())));
        repo.Setup(r => r.GetQueryableIncludingDeleted(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> p) => new AsyncQuery<T>(rows.Where(p.Compile())));
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> p) => rows.FirstOrDefault(p.Compile()));
        repo.Setup(r => r.AddAsync(It.IsAny<T>())).ReturnsAsync((T row) => { rows.Add(row); return row; });
        repo.Setup(r => r.DeleteAsync(It.IsAny<T>())).Callback((T row) => row.IsDeleted = true).Returns(Task.CompletedTask);
        _unit.Setup(u => u.Repository<T>()).Returns(repo.Object); return repo;
    }
    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> rows) : base(rows) { }
        public AsyncQuery(Expression e) : base(e) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default) => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
    private sealed class AsyncProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression e) => new AsyncQuery<T>(e);
        public IQueryable<TElement> CreateQuery<TElement>(Expression e) => new AsyncQuery<TElement>(e);
        public object? Execute(Expression e) => inner.Execute(e);
        public TResult Execute<TResult>(Expression e) => inner.Execute<TResult>(e);
        public TResult ExecuteAsync<TResult>(Expression e, CancellationToken token = default) =>
            (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(TResult).GetGenericArguments()[0]).Invoke(null, new[] { inner.Execute(e) })!;
    }
    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}
