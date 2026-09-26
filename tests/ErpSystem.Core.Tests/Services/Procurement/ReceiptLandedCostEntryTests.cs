using System.Data;
using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public class ReceiptLandedCostEntryTests
{
    private readonly Mock<ILandedCostRepository> _costs = new();
    private readonly Mock<ILandedCostItemRepository> _items = new();
    private readonly Mock<ILandedCostAllocationRepository> _allocations = new();
    private readonly Mock<IGoodsReceiptNoteRepository> _grns = new();
    private readonly Mock<IPurchaseOrderLandedCostPlanRepository> _plans = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Dictionary<Guid, LandedCost> _saved = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _line = Guid.NewGuid();
    private readonly GoodsReceiptNote _grn;
    private readonly LandedCostService _service;
    private readonly List<LandedCostReceiptWeight> _weights = new();
    private readonly List<AuditLog> _audits = new();

    public ReceiptLandedCostEntryTests()
    {
        _grn = new GoodsReceiptNote { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), GRNNumber = "GRN-TEST", Items = new List<GoodsReceiptNoteItem> {
            new() { Id = Guid.NewGuid(), PurchaseOrderItemId = _line, ReceivedQuantity = 5, AcceptedQuantity = 5, OrderedQuantity = 20, UnitCost = 10, LineValue = 50 }
        } };
        foreach (var line in _grn.Items) { line.TenantId = _grn.TenantId; line.UnitOfMeasure = "EA"; }
        Repo(_weights); Repo(_audits); Repo(new List<LandedCostSupplierDocument>());
        _grns.Setup(r => r.GetWithItemsAsync(_grn.Id)).ReturnsAsync(_grn);
        _costs.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _saved.GetValueOrDefault(id)!);
        _costs.Setup(r => r.GetWithDetailsAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _saved.GetValueOrDefault(id)!);
        _costs.Setup(r => r.AddAsync(It.IsAny<LandedCost>())).ReturnsAsync((LandedCost c) => { _saved.Add(c.Id, c); return c; });
        _items.Setup(r => r.AddAsync(It.IsAny<LandedCostItem>())).ReturnsAsync((LandedCostItem i) => { _saved[i.LandedCostId].Items.Add(i); return i; });
        _items.Setup(r => r.GetByLandedCostAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => _saved[id].Items.Where(i => !i.IsDeleted).ToList());
        _items.Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<LandedCostItem>>())).Returns((IEnumerable<LandedCostItem> items) => {
            foreach (var i in items) i.IsDeleted = true; return Task.CompletedTask;
        });
        _allocations.Setup(r => r.GetByLandedCostAsync(It.IsAny<Guid>())).ReturnsAsync(new List<LandedCostAllocation>());
        _uow.SetupGet(u => u.HasActiveTransaction).Returns(true);
        _service = new LandedCostService(_costs.Object, _items.Object, _allocations.Object, _grns.Object, _plans.Object,
            Mock.Of<IPurchaseOrderReceiptRepository>(), Mock.Of<IBusinessPartnerRepository>(), _uow.Object, Mock.Of<ILogger<LandedCostService>>());
    }

    private CreateLandedCostDto Input(Guid? target = null) => new() { RequestId = Guid.NewGuid(), GoodsReceiptNoteId = _grn.Id, Currency = "GHS", CostItems = new() {
        new() { PurchaseOrderItemId = target, CostType = LandedCostType.Freight, Description = "Actual freight", Amount = 300, Currency = "GHS", ExchangeRate = 1, AllocationMethod = "ByValue" }
    } };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WeightAllocationUsesReceiptOrVoucherSnapshotEvenAfterMasterChanges(bool useVoucherWeight)
    {
        var first = _grn.Items.Single();
        first.UnitWeightKg = 2m; first.UnitOfMeasure = "EA"; first.WeightStockUom = "EA";
        first.InventoryItem = new InventoryItem { Weight = 999m, WeightUnit = "kg" };
        var second = new GoodsReceiptNoteItem
        {
            Id = Guid.NewGuid(), GoodsReceiptNoteId = _grn.Id, PurchaseOrderItemId = Guid.NewGuid(),
            ReceivedQuantity = 10m, AcceptedQuantity = 10m, OrderedQuantity = 10m,
            UnitWeightKg = 1m, UnitOfMeasure = "EA", WeightStockUom = "EA",
            InventoryItem = new InventoryItem { Weight = 0.01m, WeightUnit = "kg" }
        };
        _grn.Items.Add(second);
        var allocations = new List<LandedCostAllocation>();
        _allocations.Setup(r => r.FindAsync(It.IsAny<Expression<Func<LandedCostAllocation, bool>>>()))
            .ReturnsAsync(new List<LandedCostAllocation>());
        _allocations.Setup(r => r.AddAsync(It.IsAny<LandedCostAllocation>()))
            .ReturnsAsync((LandedCostAllocation allocation) => { allocations.Add(allocation); return allocation; });
        _allocations.Setup(r => r.GetByLandedCostAsync(It.IsAny<Guid>())).ReturnsAsync(allocations);
        var input = Input(); input.CostItems[0].AllocationMethod = "ByWeight";
        var cost = await _service.CreateAsync(input, _user);
        if (useVoucherWeight)
        {
            first.UnitWeightKg = null;
            _weights.Add(new LandedCostReceiptWeight { TenantId = _grn.TenantId, LandedCostId = cost.Id,
                GoodsReceiptNoteItemId = first.Id, UnitWeightKg = 4m, StockUom = "EA", Reason = "Historical receipt weight declared" });
        }
        (await _service.AllocateCostsAsync(cost.Id, _user)).Should().BeTrue();
        allocations.Should().HaveCount(2);
        allocations.Single(a => a.GoodsReceiptNoteItemId == first.Id).AllocatedAmount.Should().Be(useVoucherWeight ? 200m : 150m);
        allocations.Single(a => a.GoodsReceiptNoteItemId == second.Id).AllocatedAmount.Should().Be(useVoucherWeight ? 100m : 150m);
        first.InventoryItem.Weight.Should().Be(999m);
        second.InventoryItem.Weight.Should().Be(0.01m);
    }

    [Fact]
    public async Task CreateWithoutPoEstimateSavesOnlyReceiptDraft()
    {
        var result = await _service.CreateAsync(Input(), _user);
        result.Status.Should().Be("Draft"); result.TotalCostAmount.Should().Be(300); result.AllocatedAmount.Should().Be(0);
        _saved[result.Id].TenantId.Should().Be(_grn.TenantId);
        _plans.Invocations.Should().BeEmpty();
        _allocations.Verify(r => r.AddAsync(It.IsAny<LandedCostAllocation>()), Times.Never);
    }

    [Fact]
    public async Task DirectLineChargeIsNotProratedLikePoEstimate()
    {
        var result = await _service.CreateAsync(Input(_line), _user);
        var item = _saved[result.Id].Items.Single(); item.PurchaseOrderItemId.Should().Be(_line); item.Amount.Should().Be(300);
    }

    [Fact]
    public async Task ForeignOrUnreceivedTargetIsRejectedBeforeSaving()
    {
        var call = () => _service.CreateAsync(Input(Guid.NewGuid()), _user);
        await call.Should().ThrowAsync<ArgumentException>().WithMessage("*not a received stock line*");
        _saved.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(-1, 1)] [InlineData(10, 0)] [InlineData(10, 2)]
    public async Task InvalidAmountsOrSameCurrencyRatesDoNotSave(decimal amount, decimal rate)
    {
        var input = Input(); input.CostItems[0].Amount = amount; input.CostItems[0].ExchangeRate = rate;
        await FluentActions.Awaiting(() => _service.CreateAsync(input, _user)).Should().ThrowAsync<ArgumentException>();
        _saved.Should().BeEmpty();
    }

    [Fact]
    public async Task RetryOfSameRequestDoesNotDuplicateCosts()
    {
        var input = Input(); var first = await _service.CreateAsync(input, _user);
        var retry = await _service.CreateAsync(input, _user);
        retry.Id.Should().Be(first.Id); _saved.Should().HaveCount(1); _saved[first.Id].Items.Should().HaveCount(1);
        input.CostItems[0].Amount = 400;
        await FluentActions.Awaiting(() => _service.CreateAsync(input, _user)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*already saved*");
    }

    [Fact]
    public async Task DraftEditReplacesEstimateWithoutAddingDuplicate()
    {
        var input = Input(_line); var first = await _service.CreateAsync(input, _user);
        var detail = await _service.GetByIdAsync(first.Id);
        var edit = new UpdateLandedCostDto { GoodsReceiptNoteId = _grn.Id, EditToken = detail!.EditToken, Currency = "GHS", CostItems = input.CostItems };
        edit.CostItems[0].Amount = 350;
        var result = await _service.UpdateDraftAsync(first.Id, edit, _user);
        result.Id.Should().Be(first.Id); result.TotalCostAmount.Should().Be(350);
        _saved[first.Id].Items.Count(i => !i.IsDeleted).Should().Be(1);
        _saved[first.Id].Items.Single(i => !i.IsDeleted).PurchaseOrderItemId.Should().Be(_line);
        await FluentActions.Awaiting(() => _service.UpdateDraftAsync(first.Id, edit, _user)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed*");
    }

    [Theory]
    [InlineData("Allocated")] [InlineData("Approved")] [InlineData("Posted")] [InlineData("Cancelled")]
    public async Task NonDraftCostsCannotBeOverwritten(string status)
    {
        var input = Input(); var first = await _service.CreateAsync(input, _user); _saved[first.Id].Status = status;
        await FluentActions.Awaiting(() => _service.UpdateDraftAsync(first.Id, new UpdateLandedCostDto { GoodsReceiptNoteId = _grn.Id,
            Currency = "GHS", CostItems = input.CostItems, EditToken = "stale" }, _user)).Should().ThrowAsync<InvalidOperationException>().WithMessage("Only Draft*");
        _saved[first.Id].TotalCost.Should().Be(300);
    }

    [Fact]
    public async Task SaveUsesRetryStrategyAndRollsBackOnPersistenceFailure()
    {
        var active = false;
        _uow.SetupGet(u => u.HasActiveTransaction).Returns(() => active);
        _uow.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task<LandedCostDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<LandedCostDto>> operation, CancellationToken _) => operation());
        _uow.Setup(u => u.BeginTransactionAsync(IsolationLevel.Serializable, It.IsAny<CancellationToken>())).Callback(() => active = true).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("test failure"));
        await FluentActions.Awaiting(() => _service.CreateAsync(Input(), _user)).Should().ThrowAsync<InvalidOperationException>();
        _uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once); _uow.Verify(u => u.ClearTrackedChanges(), Times.Once);
    }

    [Fact]
    public async Task CopyingPoEstimatesCannotOverwriteEnteredReceiptCharges()
    {
        _grn.PurchaseOrderId = Guid.NewGuid();
        var saved = await _service.CreateAsync(Input(), _user);
        _costs.Setup(r => r.GetByGRNAsync(_grn.Id)).ReturnsAsync(_saved.Values.ToList());
        _plans.Setup(r => r.GetWithItemsByPurchaseOrderIdAsync(_grn.PurchaseOrderId.Value)).ReturnsAsync(
            new ErpSystem.Core.Entities.Procurement.PurchaseOrderLandedCostPlan { Items = new List<ErpSystem.Core.Entities.Procurement.PurchaseOrderLandedCostPlanItem> { new() { Amount = 200 } } });
        await FluentActions.Awaiting(() => _service.InitializeFromPurchaseOrderPlanAsync(_grn.Id, _user))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        _saved[saved.Id].TotalCost.Should().Be(300);
    }

    [Fact]
    public async Task VoucherWeightDeclarationPreservesReceiptAndMasterAndRejectsStaleOrForeignEdits()
    {
        var line = _grn.Items.Single(); line.InventoryItem = new InventoryItem { Weight = 99m, WeightUnit = "kg" };
        var cost = await _service.CreateAsync(Input(), _user);
        var before = (await _service.GetByIdAsync(cost.Id))!;
        var request = new SetLandedCostReceiptWeightDto { UnitWeightKg = 1.123456m, Reason = "Measured shipping weight", EditToken = before.EditToken };
        await FluentActions.Awaiting(() => _service.SetReceiptWeightAsync(cost.Id, Guid.NewGuid(), request, _user))
            .Should().ThrowAsync<ArgumentException>();
        var result = await _service.SetReceiptWeightAsync(cost.Id, line.Id, request, _user);
        result.ReceiptWeights.Single().UnitWeightKg.Should().Be(1.123456m);
        result.ReceiptWeights.Single().IsOverridden.Should().BeTrue();
        line.UnitWeightKg.Should().BeNull(); line.InventoryItem.Weight.Should().Be(99m);
        _audits.Single().Action.Should().Be("LANDED_COST_WEIGHT_DECLARED");
        await FluentActions.Awaiting(() => _service.SetReceiptWeightAsync(cost.Id, line.Id, request, _user))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*changed*");
        _weights.Should().HaveCount(1);
        _saved[cost.Id].Status = "Allocated";
        request.EditToken = result.EditToken;
        await FluentActions.Awaiting(() => _service.SetReceiptWeightAsync(cost.Id, line.Id, request, _user))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Draft*");
        _audits.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Draft")] [InlineData("Posted")] [InlineData("Cancelled")]
    public async Task ApprovalCannotResetUnallocatedOrTerminalVoucher(string status)
    {
        var cost = await _service.CreateAsync(Input(), _user); _saved[cost.Id].Status = status;
        await FluentActions.Awaiting(() => _service.ApproveAsync(cost.Id, _user)).Should().ThrowAsync<InvalidOperationException>();
        _saved[cost.Id].Status.Should().Be(status);
    }
    private void Repo<T>(List<T> rows) where T : BaseEntity
    {
        var repo = new Mock<IGenericRepository<T>>();
        repo.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> p) => new AsyncQuery<T>(rows.Where(p.Compile())));
        repo.Setup(r => r.AddAsync(It.IsAny<T>())).ReturnsAsync((T row) => { rows.Add(row); return row; });
        _uow.Setup(u => u.Repository<T>()).Returns(repo.Object);
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
