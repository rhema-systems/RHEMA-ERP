using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
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

public class LandedCostSupplierInvoiceTests
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
    private readonly Mock<ILandedCostService> _landed = new();
    private readonly Mock<IWorkflowIntegrationService> _approval = new();
    private readonly Mock<IFinancePostingEngine> _finance = new();

    public LandedCostSupplierInvoiceTests()
    {
        _partner = new BusinessPartner { TenantId = _tenant, PartnerCode = "CARRIER", PartnerName = "Carrier", PartnerType = "Supplier", IsActive = true, RegistrationStatus = "Approved" };
        _supplier = new Supplier { TenantId = _tenant, SupplierCode = "CARRIER", Name = "Carrier", IsActive = true, Status = "Active" };
        _accrual = new Account { TenantId = _tenant, AccountCode = "2110", AccountName = "Accrual", Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false };
        _ap = new Account { TenantId = _tenant, AccountCode = "2100", AccountName = "AP", Status = AccountStatus.Active, IsControlAccount = true, AllowDirectPosting = false };
        _cost = new LandedCost { TenantId = _tenant, LandedCostNumber = "LC-TEST", Status = "Posted", Currency = "GHS", TotalCost = 360 };
        _freight = new LandedCostItem { TenantId = _tenant, LandedCostId = _cost.Id, LandedCost = _cost, Description = "Freight", Amount = 310, AmountInBaseCurrency = 310, Currency = "GHS", ExchangeRate = 1 };
        _handling = new LandedCostItem { TenantId = _tenant, LandedCostId = _cost.Id, LandedCost = _cost, Description = "Handling", Amount = 50, AmountInBaseCurrency = 50, Currency = "GHS", ExchangeRate = 1 };
        _cost.Items = new List<LandedCostItem> { _freight, _handling };
        _journal = new JournalEntry { TenantId = _tenant };
        _posting = new FinancePostingEvent { TenantId = _tenant, SourceDocumentId = _cost.Id, SourceModule = "Inventory", SourceDocumentType = "InventoryLandedCost", PostingAction = "PostLandedCost", PostingStatus = "Posted", JournalEntryId = _journal.Id };
        _credit = new AccountTransaction { TenantId = _tenant, JournalEntryId = _journal.Id, AccountId = _accrual.Id, CreditAmount = 360, FunctionalCurrencyCode = "GHS" };
        Repo(new List<LandedCost> { _cost }); Repo(new List<LandedCostItem> { _freight, _handling });
        _partners.Add(_partner); _suppliers.Add(_supplier); Repo(_partners); Repo(_suppliers);
        Repo(new List<JournalEntry> { _journal }); Repo(new List<FinancePostingEvent> { _posting });
        Repo(new List<AccountTransaction> { _credit }); Repo(new List<Account> { _accrual, _ap });
        Repo(new List<FinanceSettings> { new() { TenantId = _tenant, BaseCurrency = "GHS", ControlAccountApId = _ap.Id, ControlAccountGRVAccrualId = Guid.NewGuid() } });
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
        _service = new VendorInvoiceService(_unit.Object, user.Object, Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<VendorInvoiceService>>(), numbering.Object, Mock.Of<IWorkflowService>(), financePostingEngine: _finance.Object,
            sourceDimensions: Mock.Of<IFinanceSourceDimensionService>(), landedCosts: _landed.Object, workflowIntegration: _approval.Object);
        _landed.Setup(s => s.PostToInventoryAsync(_cost.Id, It.IsAny<Guid>())).ReturnsAsync(() => { _cost.Status = "Posted"; return true; });
    }

    private CreateLandedCostInvoicesDto Request(string secondReference = "BILL-1") => new()
    {
        InvoiceDate = new DateTime(2026, 9, 9), Charges = new()
        {
            new() { CostItemId = _freight.Id, SupplierId = _partner.Id, SupplierInvoiceNumber = "BILL-1", TaxTreatment = TaxTreatment.OutOfScope },
            new() { CostItemId = _handling.Id, SupplierId = _partner.Id, SupplierInvoiceNumber = secondReference, TaxTreatment = TaxTreatment.OutOfScope }
        }
    };

    [Fact]
    public async Task GroupsSameSupplierBillIntoOneDraftWithoutApprovingOrPosting()
    {
        var result = await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = Assert.Single(result); Assert.Equal(360, invoice.TotalAmount); Assert.Equal(2, invoice.LineItems.Count);
        Assert.Equal(VendorInvoiceStatus.Draft, invoice.Status); Assert.Null(invoice.JournalEntryId); Assert.Null(invoice.PurchaseOrderId);
        Assert.Equal(invoice.InvoiceNumber, _freight.InvoiceNumber); Assert.Equal(invoice.InvoiceNumber, _handling.InvoiceNumber);
        Assert.Equal(_partner.Id, _freight.SupplierId); Assert.All(invoice.LineItems, l => Assert.NotNull(l.LandedCostItemId));
        _unit.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GeneratedDraftDistribution_ClearsTheOriginalLandedCostAccrualWithoutPosting()
    {
        var created = Assert.Single(await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        _unit.Invocations.Clear();
        var distribution = await _service.GetDistributionAsync(created.Id);

        Assert.Equal("Proposed", distribution.Status);
        Assert.Equal(360m, distribution.TotalDebit);
        Assert.Equal(360m, distribution.TotalCredit);
        Assert.Equal(360m, distribution.Lines.Where(line => line.AccountId == _accrual.Id).Sum(line => line.Debit));
        Assert.Contains(distribution.Lines, line => line.AccountId == _ap.Id && line.Credit == 360m);
        Assert.Equal(VendorInvoiceStatus.Draft, Assert.Single(_invoices).Status);
        Assert.Null(distribution.JournalEntryId);
        _unit.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _finance.Verify(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprovedLinkedContractor_GeneratesEditableDraftUsingExistingCanonicalSupplier()
    {
        _partner.PartnerType = "Contractor";
        var created = Assert.Single(await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        Assert.Equal(_supplier.Id, created.SupplierId);
        Assert.Equal(VendorInvoiceStatus.Draft, created.Status);
        Assert.Null(created.JournalEntryId);
        Assert.Single(_suppliers);
        Assert.Equal("Contractor", _partner.PartnerType);
        var distribution = await _service.GetDistributionAsync(created.Id);
        Assert.Equal(360m, distribution.TotalDebit);
        Assert.Equal(360m, distribution.TotalCredit);
        _finance.Verify(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StandaloneLandedCost_NewContractorIdentityIsFlushedWithinTransactionBeforeDraftQueries()
    {
        // A real EF repository does not return Added-but-unsaved rows in database queries.
        // List-backed mocks used elsewhere cannot detect this onboarding boundary.
        await using var db = new ErpSystem.Data.ApplicationDbContext(
            new DbContextOptionsBuilder<ErpSystem.Data.ApplicationDbContext>()
                .UseInMemoryDatabase($"new-lc-contractor-{Guid.NewGuid()}").Options);
        var identities = new ErpSystem.Data.UnitOfWork(db).Repository<Supplier>();
        _unit.Setup(value => value.Repository<Supplier>()).Returns(identities);
        _suppliers.Clear();
        _partner.PartnerType = "Contractor";
        var saveBoundaries = new List<bool>();
        _unit.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken token) =>
            {
                saveBoundaries.Add(_transaction);
                var changed = await db.SaveChangesAsync(token);
                _suppliers.Clear();
                _suppliers.AddRange(await db.Suppliers.ToListAsync(token));
                return changed;
            });

        var first = Assert.Single(await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        var canonical = Assert.Single(await db.Suppliers.ToListAsync());
        Assert.Equal(canonical.Id, first.SupplierId);
        Assert.Equal(_partner.PartnerCode, canonical.SupplierCode);
        Assert.Equal(VendorInvoiceStatus.Draft, first.Status);
        var retry = Assert.Single(await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        Assert.Equal(first.Id, retry.Id);
        Assert.Single(await db.Suppliers.ToListAsync());
        Assert.Single(_invoices);
        Assert.NotEmpty(saveBoundaries);
        Assert.All(saveBoundaries, active => Assert.True(active));
        _unit.Verify(value => value.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _finance.Verify(value => value.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryReturnsSameInvoiceAndDifferentBillReferencesStaySeparate()
    {
        var request = Request("BILL-2");
        var first = await _service.CreateFromLandedCostAsync(_cost.Id, request, _producer);
        var again = await _service.CreateFromLandedCostAsync(_cost.Id, request, _producer);
        Assert.Equal(2, first.Count); Assert.Equal(first.Select(i => i.Id), again.Select(i => i.Id)); Assert.Equal(2, _invoices.Count);
        request.Charges[0].SupplierInvoiceNumber = "CHANGED";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateFromLandedCostAsync(_cost.Id, request, _producer));
        Assert.Equal(2, _invoices.Count);
    }

    [Fact]
    public async Task ApprovedInvoiceClearsOriginalAccrualAndCreditsAPNotInventory()
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = _invoices.Single(); invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        var posting = await BuildPosting(invoice);
        Assert.Equal(360, posting.Lines.Sum(l => l.DebitAmount)); Assert.Equal(360, posting.Lines.Sum(l => l.CreditAmount));
        Assert.All(posting.Lines.Where(l => l.DebitAmount > 0), l => Assert.Equal(_accrual.Id, l.AccountId));
        Assert.Equal(_ap.Id, Assert.Single(posting.Lines.Where(l => l.CreditAmount > 0)).AccountId);
        Assert.Contains(invoice.Id.ToString("N"), posting.IdempotencyKey); Assert.True(posting.ReturnExistingOnDuplicate);
    }

    [Theory]
    [InlineData("not-posted")] [InlineData("foreign")] [InlineData("reversed")] [InlineData("bad-credit")]
    public async Task MissingOrInvalidPostedSourceCannotGenerateInvoices(string reason)
    {
        if (reason == "not-posted") _cost.Status = "Allocated";
        if (reason == "foreign") _cost.TenantId = Guid.NewGuid();
        if (reason == "reversed") _journal.ReversalJournalEntryId = Guid.NewGuid();
        if (reason == "bad-credit") _credit.CreditAmount = 359;
        await Assert.ThrowsAnyAsync<Exception>(() => _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        Assert.Empty(_invoices);
    }

    [Theory]
    [InlineData("amount")] [InlineData("supplier")] [InlineData("account")] [InlineData("unapproved")]
    public async Task CannotPostChangedSourceOrUnapprovedInvoice(string reason)
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = _invoices.Single(); invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        if (reason == "amount") invoice.LineItems.First().UnitPrice++;
        if (reason == "supplier") _freight.SupplierId = Guid.NewGuid();
        if (reason == "account") invoice.LineItems.First().GLAccountId = _ap.Id;
        if (reason == "unapproved") invoice.Status = VendorInvoiceStatus.Draft;
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildPosting(invoice));
    }

    [Fact]
    public async Task RequiresSupplierReferenceAndTaxGroupOnlyWhenStandardIsSelected()
    {
        var request = Request();
        request.Charges[0].TaxTreatment = TaxTreatment.Standard;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateFromLandedCostAsync(_cost.Id, request, _producer));
        request.Charges[0].TaxTreatment = TaxTreatment.OutOfScope; request.Charges[0].SupplierInvoiceNumber = "";
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateFromLandedCostAsync(_cost.Id, request, _producer));
    }

    [Fact]
    public async Task DifferentSuppliersWithSameNameAndReferenceRemainSeparate()
    {
        var partner = new BusinessPartner { TenantId = _tenant, PartnerCode = "HANDLER", PartnerName = "Carrier", PartnerType = "Supplier", IsActive = true, RegistrationStatus = "Approved" };
        var supplier = new Supplier { TenantId = _tenant, SupplierCode = "HANDLER", Name = "Carrier", IsActive = true, Status = "Active" };
        _partners.Add(partner); _suppliers.Add(supplier);
        var request = Request(); request.Charges[1].SupplierId = partner.Id;
        var result = await _service.CreateFromLandedCostAsync(_cost.Id, request, _producer);
        Assert.Equal(2, result.Count); Assert.Equal(2, result.Select(i => i.SupplierId).Distinct().Count());
        Assert.Equal(310, result.Single(i => i.SupplierId == _supplier.Id).TotalAmount);
        Assert.Equal(50, result.Single(i => i.SupplierId == supplier.Id).TotalAmount);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("blacklisted")] [InlineData("foreign")] [InlineData("unapproved")]
    public async Task IneligibleCostSupplierCannotCreateInvoice(string reason)
    {
        if (reason == "inactive") _partner.IsActive = false;
        if (reason == "blacklisted") _supplier.IsBlacklisted = true;
        if (reason == "foreign") _partner.TenantId = Guid.NewGuid();
        if (reason == "unapproved") _partner.RegistrationStatus = "Draft";
        await Assert.ThrowsAnyAsync<Exception>(() => _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        Assert.Empty(_invoices);
    }

    [Fact]
    public async Task NullOrDuplicateChargeRequestsDoNotCreateInvoices()
    {
        var request = Request(); request.Charges = null!;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateFromLandedCostAsync(_cost.Id, request, _producer));
        request = Request(); request.Charges.Add(request.Charges[0]);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateFromLandedCostAsync(_cost.Id, request, _producer));
        Assert.Empty(_invoices);
    }

    [Fact]
    public async Task DraftEditCannotRemoveSourceOrChangeAmounts()
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = _invoices.Single();
        var dto = new VendorInvoiceUpdateDto { Id = invoice.Id, CurrencyCode = "GHS", ExchangeRate = 1, MatchingType = InvoiceMatchingType.None, SupplierInvoiceNumber = "BILL-1",
            LineItems = invoice.LineItems.Select(l => new VendorInvoiceLineItemCreateDto { Id = l.Id, Quantity = 1, UnitPrice = l.UnitPrice, GLAccountId = l.GLAccountId, LineItemType = l.LineItemType }).ToList() };
        var method = typeof(VendorInvoiceService).GetMethod("ValidateLandedCostInvoiceUpdate", BindingFlags.NonPublic | BindingFlags.Static)!;
        method.Invoke(null, new object[] { invoice, dto });
        dto.LineItems[0].UnitPrice++;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { invoice, dto })).InnerException);
        dto.LineItems.RemoveAt(0);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { invoice, dto })).InnerException);
    }

    [Fact]
    public async Task DeletingUnpostedDraftReleasesChargesButPreservesHistoricalSourceIds()
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = _invoices.Single();
        await _service.DeleteAsync(invoice.Id);
        Assert.True(invoice.IsDeleted); Assert.All(_lines, l => { Assert.True(l.IsDeleted); Assert.NotNull(l.LandedCostItemId); });
        Assert.Null(_freight.InvoiceNumber); Assert.Null(_handling.InvoiceNumber);
        var regenerated = await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        Assert.NotEqual(invoice.Id, Assert.Single(regenerated).Id);
    }

    [Fact]
    public async Task CannotGenerateOverAnExistingManualInvoiceLink()
    {
        _freight.InvoiceNumber = "EXISTING-AP-INVOICE";
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer));
        Assert.Empty(_invoices);
    }

    [Fact]
    public void GenericInvoiceJsonCannotForgeLandedCostSource()
    {
        var line = JsonSerializer.Deserialize<VendorInvoiceLineItemCreateDto>("{\"LandedCostItemId\":\"" + _freight.Id + "\"}");
        Assert.Null(line!.LandedCostItemId);
    }

    private Task<FinancePostingRequestDto> BuildPosting(VendorInvoice invoice) =>
        (Task<FinancePostingRequestDto>)typeof(VendorInvoiceService).GetMethod("BuildApInvoicePostingRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_service, new object?[] { invoice, Array.Empty<Guid>(), null, CancellationToken.None })!;

    private PostLandedCostDto PostRequest() => new()
    {
        InvoiceDate = new DateTime(2026, 9, 9),
        Charges = Request().Charges.Select(c => new LandedCostBillingChargeDto
        { CostItemId = c.CostItemId, SupplierId = c.SupplierId, SupplierInvoiceNumber = c.SupplierInvoiceNumber }).ToList()
    };

    [Fact]
    public async Task OnePostCreatesPendingTaxDraftAndRetryDoesNotRepostInventory()
    {
        _cost.Status = "Allocated";
        var result = await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        Assert.True(result.InventoryPosted); Assert.False(result.InvoicesPending);
        var invoice = Assert.Single(result.Invoices);
        Assert.Equal(VendorInvoiceStatus.Draft, invoice.Status); Assert.Equal(0, invoice.TaxAmount);
        Assert.All(invoice.LineItems, l => Assert.Equal(TaxTreatment.PendingReview, l.TaxTreatment));
        var again = await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        Assert.Equal(invoice.Id, Assert.Single(again.Invoices).Id);
        _landed.Verify(s => s.PostToInventoryAsync(_cost.Id, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyPostedVoucherGeneratesOnlyDrafts()
    {
        var result = await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        Assert.Single(result.Invoices); _landed.Verify(s => s.PostToInventoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task InvoiceFailureAfterInventoryPostingIsPendingAndSafelyRecoverable()
    {
        _cost.Status = "Allocated"; _credit.CreditAmount = 359;
        var result = await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        Assert.True(result.InventoryPosted); Assert.True(result.InvoicesPending); Assert.Empty(result.Invoices);
        Assert.Equal("BILL-1", _freight.ReferenceNumber); Assert.Equal(_partner.Id, _freight.SupplierId);
        _credit.CreditAmount = 360;
        var again = await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        Assert.False(again.InvoicesPending); Assert.Single(again.Invoices);
        _landed.Verify(s => s.PostToInventoryAsync(_cost.Id, It.IsAny<Guid>()), Times.Once);
    }

    [Theory]
    [InlineData("supplier")] [InlineData("reference")] [InlineData("missing-line")] [InlineData("foreign")] [InlineData("draft")]
    public async Task InvalidBillingNeverPostsInventory(string reason)
    {
        _cost.Status = "Allocated"; var request = PostRequest();
        if (reason == "supplier") request.Charges[0].SupplierId = Guid.Empty;
        if (reason == "reference") request.Charges[0].SupplierInvoiceNumber = "";
        if (reason == "missing-line") request.Charges.RemoveAt(0);
        if (reason == "foreign") _cost.TenantId = Guid.NewGuid();
        if (reason == "draft") _cost.Status = "Draft";
        await Assert.ThrowsAnyAsync<Exception>(() => _service.PostLandedCostAsync(_cost.Id, request, _producer));
        _landed.Verify(s => s.PostToInventoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never); Assert.Empty(_invoices);
    }

    [Fact]
    public async Task PendingTaxBlocksSubmitApprovalAndPostingUntilReviewed()
    {
        await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        var invoice = Assert.Single(_invoices);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitForApprovalAsync(invoice.Id));
        invoice.Status = VendorInvoiceStatus.PendingApproval;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveAsync(invoice.Id));
        invoice.Status = VendorInvoiceStatus.Approved; invoice.ApprovalStatus = "Approved";
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildPosting(invoice));
        foreach (var line in invoice.LineItems) line.TaxTreatment = TaxTreatment.OutOfScope;
        var posting = await BuildPosting(invoice);
        Assert.Equal(360, posting.Lines.Sum(l => l.DebitAmount));
        var again = await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        Assert.Equal(invoice.Id, Assert.Single(again.Invoices).Id);
        Assert.All(invoice.LineItems, l => Assert.Equal(TaxTreatment.OutOfScope, l.TaxTreatment));
    }

    [Fact]
    public async Task SubmitLoadsTaxLinesBeforeFreezingOrStartingWorkflow()
    {
        await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        var invoice = Assert.Single(_invoices);
        // A real header lookup does not Include line items. The final check must use
        // the loaded lines, not rely on entities already being tracked by creation.
        Mock.Get(_unit.Object.Repository<VendorInvoice>())
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<VendorInvoice, bool>>>()))
            .ReturnsAsync(new VendorInvoice { Id = invoice.Id, TenantId = _tenant, Status = VendorInvoiceStatus.Draft });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitForApprovalAsync(invoice.Id));
        Assert.Contains("Complete the tax treatment", error.Message);
    }

    [Fact]
    public async Task NoApproval_SubmitPostsReviewedLandedCostInvoiceThroughFinanceWithoutHumanApproval()
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = Assert.Single(_invoices);
        SetSubmission(invoice.Id, required: false);
        FinancePostingRequestDto? postedRequest = null;
        var journalId = Guid.NewGuid();
        _finance.Setup(f => f.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) =>
            {
                Assert.True(_transaction);
                postedRequest = request;
            })
            .ReturnsAsync(new FinancePostingResultDto { JournalEntryId = journalId, PostingEventId = Guid.NewGuid() });

        var result = await _service.SubmitForApprovalAsync(invoice.Id);

        Assert.False(result.ApprovalRequired);
        Assert.Equal("NotRequired", invoice.ApprovalStatus);
        Assert.Equal(VendorInvoiceStatus.Approved, invoice.Status);
        Assert.Null(invoice.ApprovedById);
        Assert.Null(invoice.ApprovedDate);
        Assert.NotNull(invoice.SubmittedById);
        Assert.Equal(journalId, result.JournalEntryId);
        Assert.NotNull(postedRequest);
        Assert.Equal(360m, postedRequest.Lines.Sum(l => l.DebitAmount));
        Assert.Equal(360m, postedRequest.Lines.Sum(l => l.CreditAmount));
        Assert.Equal(360m, postedRequest.Lines.Where(l => l.AccountId == _accrual.Id).Sum(l => l.DebitAmount));
        Assert.Contains(postedRequest.Lines, l => l.AccountId == _ap.Id && l.CreditAmount == 360m);
        Mock.Get(_unit.Object.Repository<VendorInvoice>()).Verify(r => r.UpdateAsync(It.IsAny<VendorInvoice>()), Times.Never);
        _unit.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ActiveApproval_SubmitRemainsPendingAndCannotPost()
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = Assert.Single(_invoices);
        SetSubmission(invoice.Id, required: true);

        var result = await _service.SubmitForApprovalAsync(invoice.Id);

        Assert.True(result.ApprovalRequired);
        Assert.Equal(VendorInvoiceStatus.PendingApproval, invoice.Status);
        Assert.Null(invoice.ApprovedById);
        Assert.Null(invoice.JournalEntryId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PostAsync(invoice.Id));
        _finance.Verify(f => f.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("instance")]
    [InlineData("outcome")]
    [InlineData("human")]
    public async Task InvalidDirectSubmission_DoesNotPostOrEraseApprovalHistory(string invalid)
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = Assert.Single(_invoices);
        var human = invalid == "human" ? Guid.NewGuid() : (Guid?)null;
        invoice.ApprovedById = human;
        var submission = SetSubmission(invoice.Id, required: false, invalidOutcome: invalid == "outcome");
        if (invalid == "failure") submission.ExecutionResult.Success = false;
        if (invalid == "instance") submission.ExecutionResult.WorkflowInstanceId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitForApprovalAsync(invoice.Id));

        Assert.Equal(VendorInvoiceStatus.Draft, invoice.Status);
        Assert.True(invoice.ApprovalRequired);
        Assert.Equal(human, invoice.ApprovedById);
        Assert.Null(invoice.JournalEntryId);
        _finance.Verify(f => f.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _unit.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoApproval_StillRequiresLandedCostTaxReviewBeforeWorkflowOrPosting()
    {
        await _service.PostLandedCostAsync(_cost.Id, PostRequest(), _producer);
        var invoice = Assert.Single(_invoices);
        SetSubmission(invoice.Id, required: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitForApprovalAsync(invoice.Id));

        Assert.Contains("Complete the tax treatment", error.Message);
        Assert.Equal(VendorInvoiceStatus.Draft, invoice.Status);
        _approval.Verify(w => w.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        _finance.Verify(f => f.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoApproval_FinancePostingFailureRollsBackSubmissionWithoutCreatingJournalLink()
    {
        await _service.CreateFromLandedCostAsync(_cost.Id, Request(), _producer);
        var invoice = Assert.Single(_invoices);
        SetSubmission(invoice.Id, required: false);
        _finance.Setup(f => f.PostAsync(It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The fiscal period is closed."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitForApprovalAsync(invoice.Id));

        Assert.Contains("fiscal period is closed", error.Message);
        Assert.Null(invoice.JournalEntryId);
        _unit.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unit.Verify(u => u.ClearTrackedChanges(), Times.Once);
        // Only the earlier draft creation committed; submission owns the rollback.
        _unit.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private WorkflowIntegrationResult SetSubmission(Guid invoiceId, bool required, bool invalidOutcome = false)
    {
        var result = new WorkflowIntegrationResult(
            new WorkflowExecutionResult
            {
                Success = true,
                WorkflowInstanceId = required ? Guid.NewGuid() : null
            }, required || invalidOutcome ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, required);
        _approval.Setup(w => w.SubmitAsync("VendorInvoice", invoiceId)).ReturnsAsync(result);
        return result;
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
