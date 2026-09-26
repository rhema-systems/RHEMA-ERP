using System.Linq.Expressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public class LandedCostInvoiceLinkTests
{
    private readonly Mock<IUnitOfWork> _unit = new();
    private readonly Mock<ILandedCostRepository> _costs = new();
    private readonly Mock<ILandedCostItemRepository> _items = new();
    private readonly Mock<IGoodsReceiptNoteRepository> _grns = new();
    private readonly BusinessPartner _partner = new() { PartnerCode = "SUP-1", PartnerName = "Supplier" };
    private readonly GoodsReceiptNote _grn = new() { PurchaseOrderId = Guid.NewGuid() };
    private readonly LandedCost _cost;
    private readonly LandedCostItem _item;
    private readonly VendorInvoice _invoice;
    private readonly LandedCostService _service;

    public LandedCostInvoiceLinkTests()
    {
        var tenant = Guid.NewGuid(); _partner.TenantId = tenant; _grn.TenantId = tenant;
        _item = new LandedCostItem { TenantId = tenant, Amount = 310, Currency = "GHS" };
        _cost = new LandedCost { TenantId = tenant, GoodsReceiptNoteId = _grn.Id, Currency = "GHS", Status = "Allocated", Items = new List<LandedCostItem> { _item } };
        _item.LandedCostId = _cost.Id;
        _invoice = new VendorInvoice { TenantId = tenant, InvoiceNumber = "VIN-001", CurrencyCode = "GHS", TotalAmount = 310,
            PurchaseOrderId = _grn.PurchaseOrderId, Supplier = new Supplier { SupplierCode = "SUP-1" } };
        _invoice.SupplierId = _invoice.Supplier.Id;
        _costs.Setup(r => r.GetWithDetailsAsync(_cost.Id)).ReturnsAsync(_cost);
        _grns.Setup(r => r.GetWithItemsAsync(_grn.Id)).ReturnsAsync(_grn);
        _unit.SetupGet(u => u.HasActiveTransaction).Returns(true);
        Repo(Array.Empty<LandedCostReceiptWeight>());
        Repo(Array.Empty<LandedCostSupplierDocument>());
        Repo(new[] { _invoice }); Repo(new[] { _partner }); Repo(new[] { _grn }); Repo(new[] { _cost }); Repo(new[] { _item });
        _service = new LandedCostService(_costs.Object, _items.Object, Mock.Of<ILandedCostAllocationRepository>(), _grns.Object,
            Mock.Of<IPurchaseOrderLandedCostPlanRepository>(), Mock.Of<IPurchaseOrderReceiptRepository>(),
            Mock.Of<IBusinessPartnerRepository>(), _unit.Object, Mock.Of<ILogger<LandedCostService>>());
    }

    [Fact]
    public async Task LinksExistingInvoiceWithoutChangingFinancialAmountsOrStatus()
    {
        var result = await _service.LinkInvoiceAsync(_cost.Id, _item.Id, _invoice.Id, Guid.NewGuid());
        Assert.Equal(_invoice.Id, result.CostItems.Single().InvoiceId);
        Assert.Equal(_invoice.InvoiceNumber, _item.InvoiceNumber); Assert.Equal(_partner.Id, _item.SupplierId);
        Assert.Equal(310, _invoice.TotalAmount); Assert.Equal(VendorInvoiceStatus.Draft, _invoice.Status);
        Assert.Equal("Allocated", _cost.Status); Assert.Equal(310, _item.Amount);
        Assert.Equal(_grn.PurchaseOrderId, result.PurchaseOrderId); Assert.Equal(_grn.Id, result.ReceiptId);
        Assert.Single(await _service.GetByPurchaseOrderAsync(_grn.PurchaseOrderId!.Value));
        Assert.Single(await _service.GetByInvoiceAsync(_invoice.Id));
    }

    [Theory]
    [InlineData("currency")] [InlineData("supplier")] [InlineData("po")]
    [InlineData("void")] [InlineData("opening")] [InlineData("already-linked")]
    public async Task InvalidAssociationsDoNotSave(string reason)
    {
        switch (reason) {
            case "currency": _invoice.CurrencyCode = "EUR"; break;
            case "supplier": _item.SupplierId = Guid.NewGuid(); break;
            case "po": _invoice.PurchaseOrderId = Guid.NewGuid(); break;
            case "void": _invoice.Status = VendorInvoiceStatus.Voided; break;
            case "opening": _invoice.IsOpeningBalance = true; break;
            case "already-linked": _item.InvoiceNumber = "VIN-OTHER"; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LinkInvoiceAsync(_cost.Id, _item.Id, _invoice.Id, Guid.NewGuid()));
        _unit.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForeignTenantInvoiceIsNotLinkable()
    {
        _invoice.TenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => _service.LinkInvoiceAsync(_cost.Id, _item.Id, _invoice.Id, Guid.NewGuid()));
        Assert.Null(_item.InvoiceNumber);
    }

    [Fact]
    public async Task SeparateFreightInvoiceFindsReceiptWithoutAPurchaseOrderLink()
    {
        _invoice.PurchaseOrderId = null;
        await _service.LinkInvoiceAsync(_cost.Id, _item.Id, _invoice.Id, Guid.NewGuid());
        Assert.Single(await _service.GetByInvoiceAsync(_invoice.Id));
        Assert.Null(_invoice.PurchaseOrderId);
    }

    private void Repo<T>(T[] rows) where T : BaseEntity
    {
        var repo = new Mock<IGenericRepository<T>>();
        repo.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> p) => new AsyncQuery<T>(rows.Where(p.Compile())));
        _unit.Setup(u => u.Repository<T>()).Returns(repo.Object);
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
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken token = default)
        {
            var value = inner.Execute(expression);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(TResult).GetGenericArguments()[0]).Invoke(null, new[] { value })!;
        }
    }
    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    }
}
