using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ErpSystem.Tests.Services.Finance;

namespace ErpSystem.Tests.Services;

public class LandedCostServiceTests
{
    private readonly Mock<ILandedCostRepository> _landedCostRepository = new();
    private readonly Mock<ILandedCostItemRepository> _landedCostItemRepository = new();
    private readonly Mock<ILandedCostAllocationRepository> _landedCostAllocationRepository = new();
    private readonly Mock<IGoodsReceiptNoteRepository> _grnRepository = new();
    private readonly Mock<IPurchaseOrderLandedCostPlanRepository> _poPlanRepository = new();
    private readonly Mock<IBusinessPartnerRepository> _supplierRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<LandedCostService>> _logger = new();

    private LandedCostService CreateService()
    {
        var weights = new Mock<IGenericRepository<LandedCostReceiptWeight>>();
        weights.Setup(r => r.GetQueryable(It.IsAny<Expression<Func<LandedCostReceiptWeight, bool>>>()))
            .Returns(Array.Empty<LandedCostReceiptWeight>().AsAsyncQueryable());
        _unitOfWork.Setup(u => u.Repository<LandedCostReceiptWeight>()).Returns(weights.Object);
        _unitOfWork.SetupGet(u => u.HasActiveTransaction).Returns(true);
        return new LandedCostService(
            _landedCostRepository.Object,
            _landedCostItemRepository.Object,
            _landedCostAllocationRepository.Object,
            _grnRepository.Object,
            _poPlanRepository.Object,
            Mock.Of<IPurchaseOrderReceiptRepository>(),
            _supplierRepository.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    [Fact]
    public async Task AllocationExcludesUnacceptedReceiptLinesAndUsesAcceptedQuantityOnly()
    {
        var accepted = new GoodsReceiptNoteItem { InventoryItemId = Guid.NewGuid(), ReceivedQuantity = 100, AcceptedQuantity = 92, RejectedQuantity = 8, UnitCost = 10 };
        var pending = new GoodsReceiptNoteItem { InventoryItemId = Guid.NewGuid(), ReceivedQuantity = 30, AcceptedQuantity = 0, UnitCost = 10 };
        var grn = new GoodsReceiptNote { Items = new List<GoodsReceiptNoteItem> { accepted, pending } };
        var cost = new LandedCost { GoodsReceiptNoteId = grn.Id, Status = "Draft", Currency = "GHS" };
        var items = new List<LandedCostItem> { new() { LandedCostId = cost.Id, Description = "Freight", AmountInBaseCurrency = 92, AllocationMethod = "ByQuantity" } };
        var allocations = new List<LandedCostAllocation>(); cost.Items = items; cost.Allocations = allocations;
        SetupAllocationHarness(cost, grn, items, allocations);
        await CreateService().AllocateCostsAsync(cost.Id, Guid.NewGuid());
        allocations.Should().ContainSingle();
        allocations[0].GoodsReceiptNoteItemId.Should().Be(accepted.Id);
        allocations[0].Quantity.Should().Be(92); allocations[0].AllocatedAmount.Should().Be(92);
    }

    [Fact]
    public async Task AllocationDoesNotFallbackToReceivingWhenNothingHasBeenAccepted()
    {
        var grn = new GoodsReceiptNote { Items = new List<GoodsReceiptNoteItem> { new() { ReceivedQuantity = 100, AcceptedQuantity = 0 } } };
        var cost = new LandedCost { GoodsReceiptNoteId = grn.Id, Status = "Draft" };
        var allocations = new List<LandedCostAllocation>();
        SetupAllocationHarness(cost, grn, new List<LandedCostItem>(), allocations);
        var action = () => CreateService().AllocateCostsAsync(cost.Id, Guid.NewGuid());
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No accepted GRN lines*");
        allocations.Should().BeEmpty(); cost.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task AllocateCostsAsync_ByValue_AllocatesAcrossReceivedLinesOnly()
    {
        var grnId = Guid.NewGuid();
        var landedCostId = Guid.NewGuid();
        var costItemId = Guid.NewGuid();

        var itemAId = Guid.NewGuid();
        var itemBId = Guid.NewGuid();

        var inventoryA = new InventoryItem { Id = Guid.NewGuid(), Weight = 2, Volume = 3 };
        var inventoryB = new InventoryItem { Id = Guid.NewGuid(), Weight = 1, Volume = 5 };

        var grn = new GoodsReceiptNote
        {
            Id = grnId,
            GRNNumber = "GRN-1",
            Items = new List<GoodsReceiptNoteItem>
            {
                new()
                {
                    Id = itemAId,
                    GoodsReceiptNoteId = grnId,
                    InventoryItemId = inventoryA.Id,
                    InventoryItem = inventoryA,
                    ItemCode = "A",
                    ItemName = "Item A",
                    AcceptedQuantity = 10,
                    UnitCost = 200,
                    LineValue = 2000
                },
                new()
                {
                    Id = itemBId,
                    GoodsReceiptNoteId = grnId,
                    InventoryItemId = inventoryB.Id,
                    InventoryItem = inventoryB,
                    ItemCode = "B",
                    ItemName = "Item B",
                    AcceptedQuantity = 12,
                    UnitCost = 200,
                    LineValue = 2400
                }
            }
        };

        var landedCost = new LandedCost
        {
            Id = landedCostId,
            GoodsReceiptNoteId = grnId,
            Status = "Draft",
            Currency = "USD"
        };

        var costItems = new List<LandedCostItem>
        {
            new()
            {
                Id = costItemId,
                LandedCostId = landedCostId,
                CostType = Core.Enums.LandedCostType.Freight,
                Description = "Shipping",
                AmountInBaseCurrency = 400,
                AllocationMethod = "ByValue"
            }
        };

        var allocations = new List<LandedCostAllocation>();
        landedCost.Items = costItems;
        landedCost.Allocations = allocations;

        SetupAllocationHarness(landedCost, grn, costItems, allocations);

        var service = CreateService();
        var ok = await service.AllocateCostsAsync(landedCostId, Guid.NewGuid());

        ok.Should().BeTrue();
        landedCost.Status.Should().Be("Allocated");

        var aAlloc = allocations.Single(a => a.GoodsReceiptNoteItemId == itemAId);
        var bAlloc = allocations.Single(a => a.GoodsReceiptNoteItemId == itemBId);

        aAlloc.AllocatedAmount.Should().Be(181.82m);
        bAlloc.AllocatedAmount.Should().Be(218.18m);
        allocations.Sum(a => a.AllocatedAmount).Should().Be(400m);
    }

    [Fact]
    public async Task AllocateCostsAsync_ByWeight_AllocatesByCapturedWeightTimesQty()
    {
        var grnId = Guid.NewGuid();
        var landedCostId = Guid.NewGuid();
        var costItemId = Guid.NewGuid();

        var itemAId = Guid.NewGuid();
        var itemBId = Guid.NewGuid();

        var inventoryA = new InventoryItem { Id = Guid.NewGuid(), Weight = 2 };
        var inventoryB = new InventoryItem { Id = Guid.NewGuid(), Weight = 1 };

        var grn = new GoodsReceiptNote
        {
            Id = grnId,
            Items = new List<GoodsReceiptNoteItem>
            {
                new()
                {
                    Id = itemAId,
                    GoodsReceiptNoteId = grnId,
                    InventoryItemId = inventoryA.Id,
                    InventoryItem = inventoryA,
                    UnitWeightKg = 2m, UnitOfMeasure = "EA", WeightStockUom = "EA",
                    AcceptedQuantity = 10
                },
                new()
                {
                    Id = itemBId,
                    GoodsReceiptNoteId = grnId,
                    InventoryItemId = inventoryB.Id,
                    InventoryItem = inventoryB,
                    UnitWeightKg = 1m, UnitOfMeasure = "EA", WeightStockUom = "EA",
                    AcceptedQuantity = 10
                }
            }
        };

        var landedCost = new LandedCost
        {
            Id = landedCostId,
            GoodsReceiptNoteId = grnId,
            Status = "Draft",
            Currency = "USD"
        };

        var costItems = new List<LandedCostItem>
        {
            new()
            {
                Id = costItemId,
                LandedCostId = landedCostId,
                Description = "Freight",
                AmountInBaseCurrency = 300,
                AllocationMethod = "ByWeight"
            }
        };

        var allocations = new List<LandedCostAllocation>();
        landedCost.Items = costItems;
        landedCost.Allocations = allocations;

        SetupAllocationHarness(landedCost, grn, costItems, allocations);

        var service = CreateService();
        var ok = await service.AllocateCostsAsync(landedCostId, Guid.NewGuid());

        ok.Should().BeTrue();

        var aAlloc = allocations.Single(a => a.GoodsReceiptNoteItemId == itemAId);
        var bAlloc = allocations.Single(a => a.GoodsReceiptNoteItemId == itemBId);

        aAlloc.AllocatedAmount.Should().Be(200m);
        bAlloc.AllocatedAmount.Should().Be(100m);
        allocations.Sum(a => a.AllocatedAmount).Should().Be(300m);
    }

    [Fact]
    public async Task SetManualAllocationsAsync_WhenSumDoesNotMatch_ShouldThrow()
    {
        var grnId = Guid.NewGuid();
        var landedCostId = Guid.NewGuid();
        var costItemId = Guid.NewGuid();
        var itemAId = Guid.NewGuid();

        var grn = new GoodsReceiptNote
        {
            Id = grnId,
            Items = new List<GoodsReceiptNoteItem>
            {
                new()
                {
                    Id = itemAId,
                    GoodsReceiptNoteId = grnId,
                    InventoryItemId = Guid.NewGuid(),
                    AcceptedQuantity = 5
                }
            }
        };

        var landedCost = new LandedCost
        {
            Id = landedCostId,
            GoodsReceiptNoteId = grnId,
            Status = "Draft",
            Currency = "USD"
        };

        var costItems = new List<LandedCostItem>
        {
            new()
            {
                Id = costItemId,
                LandedCostId = landedCostId,
                Description = "Customs duty",
                AmountInBaseCurrency = 100,
                AllocationMethod = "Manual"
            }
        };

        var allocations = new List<LandedCostAllocation>();
        landedCost.Items = costItems;
        landedCost.Allocations = allocations;

        SetupAllocationHarness(landedCost, grn, costItems, allocations);

        var service = CreateService();

        var dto = new SetManualLandedCostAllocationsDto
        {
            Allocations = new List<ManualLandedCostAllocationLineDto>
            {
                new() { GoodsReceiptNoteItemId = itemAId, AllocatedAmount = 99 }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetManualAllocationsAsync(landedCostId, costItemId, dto, Guid.NewGuid()));
    }

    [Fact]
    public async Task AllocateCostsAsync_MixedScopes_DoNotSpreadLineCostToAnotherPoLine()
    {
        var sharedInventoryId = Guid.NewGuid();
        var lineA = new GoodsReceiptNoteItem { PurchaseOrderItemId = Guid.NewGuid(), InventoryItemId = sharedInventoryId, AcceptedQuantity = 10, UnitCost = 10 };
        var lineB = new GoodsReceiptNoteItem { PurchaseOrderItemId = Guid.NewGuid(), InventoryItemId = sharedInventoryId, AcceptedQuantity = 10, UnitCost = 10 };
        var grn = new GoodsReceiptNote { Items = new List<GoodsReceiptNoteItem> { lineA, lineB } };
        var landedCost = new LandedCost { GoodsReceiptNoteId = grn.Id, Status = "Draft" };
        var shared = new LandedCostItem { LandedCostId = landedCost.Id, AmountInBaseCurrency = 100, AllocationMethod = "Equal" };
        var targeted = new LandedCostItem { LandedCostId = landedCost.Id, AmountInBaseCurrency = 30,
            AllocationMethod = "ByQuantity", PurchaseOrderItemId = lineA.PurchaseOrderItemId };
        var costs = new List<LandedCostItem> { shared, targeted };
        var allocations = new List<LandedCostAllocation>();
        landedCost.Items = costs; landedCost.Allocations = allocations;
        SetupAllocationHarness(landedCost, grn, costs, allocations);
        await CreateService().AllocateCostsAsync(landedCost.Id, Guid.NewGuid());
        Assert.Equal(80, allocations.Where(a => a.GoodsReceiptNoteItemId == lineA.Id).Sum(a => a.AllocatedAmount));
        Assert.Equal(50, allocations.Where(a => a.GoodsReceiptNoteItemId == lineB.Id).Sum(a => a.AllocatedAmount));
        Assert.Single(allocations.Where(a => a.LandedCostItemId == targeted.Id));
    }

    [Fact]
    public async Task ManualAllocations_CannotMoveTargetedCostToAnotherPoLine()
    {
        var line = new GoodsReceiptNoteItem { PurchaseOrderItemId = Guid.NewGuid(), AcceptedQuantity = 10 };
        var grn = new GoodsReceiptNote { Items = new List<GoodsReceiptNoteItem> { line } };
        var landedCost = new LandedCost { GoodsReceiptNoteId = grn.Id, Status = "Draft" };
        var targeted = new LandedCostItem { LandedCostId = landedCost.Id, AmountInBaseCurrency = 30,
            PurchaseOrderItemId = Guid.NewGuid(), AllocationMethod = "ByQuantity" };
        var costs = new List<LandedCostItem> { targeted };
        var allocations = new List<LandedCostAllocation>();
        landedCost.Items = costs; landedCost.Allocations = allocations;
        SetupAllocationHarness(landedCost, grn, costs, allocations);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().SetManualAllocationsAsync(landedCost.Id, targeted.Id,
            new SetManualLandedCostAllocationsDto { Allocations = new() {
                new() { GoodsReceiptNoteItemId = line.Id, AllocatedAmount = 30 } } }, Guid.NewGuid()));
        Assert.Empty(allocations);
        Assert.Equal("ByQuantity", targeted.AllocationMethod);
    }

    [Fact]
    public async Task InitializeFromPlan_ProrationKeepsScopeAndSkipsUnreceivedLine()
    {
        var poId = Guid.NewGuid();
        var line = new GoodsReceiptNoteItem { PurchaseOrderItemId = Guid.NewGuid(),
            OrderedQuantity = 20, AcceptedQuantity = 10, ReceivedQuantity = 10 };
        var grn = new GoodsReceiptNote { PurchaseOrderId = poId, Items = new List<GoodsReceiptNoteItem> { line } };
        var landedCost = new LandedCost { GoodsReceiptNoteId = grn.Id, Status = "Draft", Currency = "GHS" };
        var costs = new List<LandedCostItem>();
        var allocations = new List<LandedCostAllocation>();
        landedCost.Items = costs; landedCost.Allocations = allocations;
        SetupAllocationHarness(landedCost, grn, costs, allocations);
        // Initial copying creates a new draft; re-copying must not overwrite entered receipt charges.
        _landedCostRepository.Setup(r => r.GetByGRNAsync(grn.Id)).ReturnsAsync(Array.Empty<LandedCost>());
        _landedCostItemRepository.Setup(r => r.GetByLandedCostAsync(It.IsAny<Guid>())).ReturnsAsync(Array.Empty<LandedCostItem>());
        _landedCostAllocationRepository.Setup(r => r.GetByLandedCostAsync(It.IsAny<Guid>())).ReturnsAsync(Array.Empty<LandedCostAllocation>());
        _poPlanRepository.Setup(r => r.GetWithItemsByPurchaseOrderIdAsync(poId)).ReturnsAsync(
            new PurchaseOrderLandedCostPlan { PurchaseOrderId = poId, Currency = "GHS", Items = new List<PurchaseOrderLandedCostPlanItem> {
                new() { Amount = 100, AmountInPlanCurrency = 100, ExchangeRate = 1, AllocationMethod = "ByValue" },
                new() { Amount = 50, AmountInPlanCurrency = 50, ExchangeRate = 1, AllocationMethod = "ByQuantity", PurchaseOrderItemId = line.PurchaseOrderItemId },
                new() { Amount = 999, AmountInPlanCurrency = 999, ExchangeRate = 1, AllocationMethod = "ByQuantity", PurchaseOrderItemId = Guid.NewGuid() }
            } });
        _landedCostItemRepository.Setup(r => r.AddAsync(It.IsAny<LandedCostItem>()))
            .ReturnsAsync((LandedCostItem item) => { costs.Add(item); return item; });
        await CreateService().InitializeFromPurchaseOrderPlanAsync(grn.Id, Guid.NewGuid());
        Assert.Equal(2, costs.Count);
        Assert.Equal(100, costs.Single(c => c.PurchaseOrderItemId == null).AmountInBaseCurrency);
        Assert.Equal(25, costs.Single(c => c.PurchaseOrderItemId == line.PurchaseOrderItemId).AmountInBaseCurrency);
    }

    private void SetupAllocationHarness(
        LandedCost landedCost,
        GoodsReceiptNote grn,
        List<LandedCostItem> costItems,
        List<LandedCostAllocation> allocations)
    {
        _landedCostRepository.Setup(r => r.GetByIdAsync(landedCost.Id)).ReturnsAsync(landedCost);
        _landedCostRepository.Setup(r => r.GetWithDetailsAsync(landedCost.Id)).ReturnsAsync(landedCost);
        _landedCostRepository.Setup(r => r.UpdateAsync(It.IsAny<LandedCost>())).Returns(Task.CompletedTask);

        _grnRepository.Setup(r => r.GetWithItemsAsync(grn.Id)).ReturnsAsync(grn);

        _landedCostItemRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => costItems.SingleOrDefault(i => i.Id == id)!);
        _landedCostItemRepository.Setup(r => r.GetByLandedCostAsync(landedCost.Id)).ReturnsAsync(costItems);
        _landedCostItemRepository.Setup(r => r.UpdateAsync(It.IsAny<LandedCostItem>())).Returns(Task.CompletedTask);

        _landedCostAllocationRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<LandedCostAllocation, bool>>>()))
            .ReturnsAsync((Expression<Func<LandedCostAllocation, bool>> pred) => allocations.Where(pred.Compile()).ToList());

        _landedCostAllocationRepository.Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<LandedCostAllocation>>()))
            .Returns((IEnumerable<LandedCostAllocation> entities) =>
            {
                foreach (var entity in entities)
                    entity.IsDeleted = true;
                return Task.CompletedTask;
            });

        _landedCostAllocationRepository.Setup(r => r.AddAsync(It.IsAny<LandedCostAllocation>()))
            .ReturnsAsync((LandedCostAllocation entity) =>
            {
                entity.Id = Guid.NewGuid();
                allocations.Add(entity);
                return entity;
            });

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }
}
