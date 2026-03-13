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

namespace ErpSystem.Tests.Services;

public class LandedCostServiceTests
{
    private readonly Mock<ILandedCostRepository> _landedCostRepository = new();
    private readonly Mock<ILandedCostItemRepository> _landedCostItemRepository = new();
    private readonly Mock<ILandedCostAllocationRepository> _landedCostAllocationRepository = new();
    private readonly Mock<IGoodsReceiptNoteRepository> _grnRepository = new();
    private readonly Mock<IPurchaseOrderLandedCostPlanRepository> _poPlanRepository = new();
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<LandedCostService>> _logger = new();

    private LandedCostService CreateService()
    {
        return new LandedCostService(
            _landedCostRepository.Object,
            _landedCostItemRepository.Object,
            _landedCostAllocationRepository.Object,
            _grnRepository.Object,
            _poPlanRepository.Object,
            _supplierRepository.Object,
            _unitOfWork.Object,
            _logger.Object);
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
    public async Task AllocateCostsAsync_ByWeight_AllocatesByItemWeightTimesQty()
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
                    AcceptedQuantity = 10
                },
                new()
                {
                    Id = itemBId,
                    GoodsReceiptNoteId = grnId,
                    InventoryItemId = inventoryB.Id,
                    InventoryItem = inventoryB,
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

        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
    }
}

