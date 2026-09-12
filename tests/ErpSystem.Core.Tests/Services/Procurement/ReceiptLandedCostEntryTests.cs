using System.Data;
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

    public ReceiptLandedCostEntryTests()
    {
        _grn = new GoodsReceiptNote { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), GRNNumber = "GRN-TEST", Items = new List<GoodsReceiptNoteItem> {
            new() { Id = Guid.NewGuid(), PurchaseOrderItemId = _line, ReceivedQuantity = 5, AcceptedQuantity = 5, OrderedQuantity = 20, UnitCost = 10, LineValue = 50 }
        } };
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
}
