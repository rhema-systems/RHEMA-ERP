using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

/// <summary>
/// Exercises real adjustment orchestration with a save-boundary assertion matching
/// the deployed count-freeze predicate. SQL atomic rollback is separately verified
/// in rehearsal; EF InMemory does not implement database rollback.
/// </summary>
public sealed class StockAdjustmentCountFreezeOrderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_item_in_two_bins_flushes_each_signed_balance_delta_before_any_stock_movement(bool negativeFirst)
    {
        await using var fixture = new Fixture(negativeFirst);
        await fixture.SeedAsync();

        var result = await fixture.PostAsync();

        result.Status.Should().Be("Posted");
        fixture.Boundary.Deltas.Should().Equal(negativeFirst ? new[] { -1m, 2m } : new[] { 2m, -1m });
        fixture.Boundary.AllBalanceWritesHadTransaction.Should().BeTrue();
        fixture.MovementAdds.Should().Be(2);
        fixture.Item.CurrentStock.Should().Be(31m);
        fixture.WarehouseQuantity.CurrentStock.Should().Be(31m);
        fixture.Locations[0].Quantity.Should().Be(12m);
        fixture.Locations[1].Quantity.Should().Be(19m);
        var movements = await fixture.Context.Set<StockMovement>().ToListAsync();
        movements.Should().HaveCount(2);
        movements.Select(value => value.Quantity).Should().BeEquivalentTo(new[] { 2m, -1m });
        movements.Sum(value => value.TotalValue).Should().Be(10m);
        movements.Should().OnlyContain(value => value.ReferenceId == fixture.Adjustment.Id &&
            value.WarehouseId == fixture.Warehouse.Id && value.ProcessedById == fixture.ActorId);
        fixture.Count.FreezeReleasedAtUtc.Should().BeNull("only the enclosing count may release its freeze after complete posting");
        fixture.Unit.HasActiveTransaction.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_line_failure_stages_no_movements_and_preserves_transaction_ownership(bool outerCountOwnsTransaction)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.FailSecondValuation = true;
        if (outerCountOwnsTransaction) await fixture.Unit.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var act = () => fixture.PostAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Second line valuation failed.");
        fixture.Boundary.Deltas.Should().Equal(2m);
        fixture.Boundary.AllBalanceWritesHadTransaction.Should().BeTrue();
        fixture.MovementAdds.Should().Be(0, "movement drafts must not be staged until every balance line succeeds");
        (await fixture.Context.Set<StockMovement>().CountAsync()).Should().Be(0);
        (await fixture.Context.Set<PhysicalCount>().SingleAsync()).FreezeReleasedAtUtc.Should().BeNull();
        fixture.Unit.HasActiveTransaction.Should().Be(outerCountOwnsTransaction,
            "a standalone post rolls back its own transaction; an enclosing count must retain ownership of rollback");
        if (outerCountOwnsTransaction) await fixture.Unit.RollbackAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordering_fix_does_not_allow_a_requester_or_an_unassigned_scope_to_post(bool requester)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        if (requester)
        {
            fixture.Adjustment.RequestedById = fixture.ActorId;
            await fixture.Context.SaveChangesAsync();
        }
        else fixture.DenyPostingScope = true;

        var act = () => fixture.PostAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        fixture.Boundary.Deltas.Should().BeEmpty();
        fixture.MovementAdds.Should().Be(0);
        fixture.Finance.Verify(service => service.PostAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Count.FreezeReleasedAtUtc.Should().BeNull();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public FreezeBoundary Boundary { get; } = new();
        public ApplicationDbContext Context { get; }
        public UnitOfWork Unit { get; }
        public Warehouse Warehouse { get; }
        public InventoryItem Item { get; }
        public WarehouseQuantity WarehouseQuantity { get; }
        public WarehouseLocation[] Bins { get; }
        public InventoryLocation[] Locations { get; }
        public StockAdjustment Adjustment { get; }
        public PhysicalCount Count { get; }
        public Mock<IInventoryAdjustmentFinancePostingService> Finance { get; } = new();
        public int MovementAdds { get; private set; }
        public bool FailSecondValuation { get; set; }
        public bool DenyPostingScope { get; set; }
        private StockAdjustmentService Service { get; }
        private int ValuationCalls { get; set; }
        private bool NegativeFirst { get; }

        public Fixture(bool negativeFirst = false)
        {
            NegativeFirst=negativeFirst;
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .AddInterceptors(Boundary).Options);
            Unit = new UnitOfWork(Context);
            Warehouse = new Warehouse { TenantId=TenantId, Code="FREEZE", Name="Count warehouse", IsActive=true };
            Bins = new[] { "BIN-A", "BIN-B" }.Select(code => new WarehouseLocation
            {
                TenantId=TenantId, WarehouseId=Warehouse.Id, LocationCode=code, Name=code, IsActive=true
            }).ToArray();
            Item = new InventoryItem
            {
                TenantId=TenantId, ItemCode="TWO-BINS", Name="Same counted item", UnitOfMeasure="EA",
                Status=ItemStatus.Active, ItemType=ItemType.StockItem, CurrentStock=30, AvailableStock=30,
                AverageCost=10, ValuationMethod=ValuationMethod.WeightedAverage
            };
            WarehouseQuantity = new WarehouseQuantity
            {
                TenantId=TenantId, WarehouseId=Warehouse.Id, InventoryItemId=Item.Id,
                CurrentStock=30, AvailableStock=30, AverageCost=10
            };
            Locations = Bins.Select((bin,index) => new InventoryLocation
            {
                TenantId=TenantId, InventoryItemId=Item.Id, LocationId=bin.Id,
                Quantity=index == 0 ? 10 : 20, AvailableQuantity=index == 0 ? 10 : 20, AverageCost=10
            }).ToArray();
            Adjustment = new StockAdjustment
            {
                TenantId=TenantId, WarehouseId=Warehouse.Id, AdjustmentNumber="ADJ-FROZEN-COUNT", Status="Approved",
                ReasonCode=StockAdjustmentReasonCodes.PhysicalCount, RequestedById=Guid.NewGuid(), ApprovedById=Guid.NewGuid(),
                RowVersion=[1,2,3], TotalAdjustmentValue=10
            };
            Adjustment.Items = Bins.Select((bin,index) => new StockAdjustmentItem
            {
                TenantId=TenantId, AdjustmentId=Adjustment.Id, InventoryItemId=Item.Id, LocationId=bin.Id,
                InventoryItem=Item, Location=bin, SystemQuantity=index == 0 ? 10 : 20,
                PhysicalQuantity=index == 0 ? 12 : 19, AdjustmentQuantity=index == 0 ? 2 : -1,
                UnitCost=10, AdjustmentValue=index == 0 ? 20 : -10,
                CreatedAt=DateTime.UtcNow.Date.AddSeconds(negativeFirst ? 1-index : index)
            }).ToList();
            Count = new PhysicalCount
            {
                TenantId=TenantId, WarehouseId=Warehouse.Id, CountNumber="PC-FREEZE", Status="ReadyToPost",
                StockAdjustmentId=Adjustment.Id, FreezeInventory=true, FreezeStartedAtUtc=DateTime.UtcNow,
                InitiatedById=Adjustment.RequestedById, CountedById=Adjustment.RequestedById,
                FinanceApprovedById=ActorId, RowVersion=[4,5,6]
            };
            Boundary.Adjustment = Adjustment;
            Boundary.HasTransaction = () => Unit.HasActiveTransaction;
            var actor = new Mock<ICurrentUserProvider>();
            actor.SetupGet(value => value.UserId).Returns(ActorId);
            actor.SetupGet(value => value.TenantId).Returns(TenantId);
            actor.SetupGet(value => value.Username).Returns("finance.count.reviewer");
            actor.SetupGet(value => value.IsAuthenticated).Returns(true);
            var items = new Mock<IInventoryItemRepository>();
            items.Setup(value => value.GetByIdAsync(Item.Id)).ReturnsAsync(Item);
            var warehouses = new Mock<IWarehouseRepository>();
            warehouses.Setup(value => value.GetByIdAsync(Warehouse.Id)).ReturnsAsync(Warehouse);
            var bins = new Mock<IWarehouseLocationRepository>();
            foreach (var bin in Bins) bins.Setup(value => value.GetByIdAsync(bin.Id)).ReturnsAsync(bin);
            var warehouseQuantities = new Mock<IWarehouseQuantityRepository>();
            warehouseQuantities.Setup(value => value.GetByWarehouseAndItemAsync(Warehouse.Id,Item.Id)).ReturnsAsync(WarehouseQuantity);
            var movements = new Mock<IStockMovementRepository>();
            movements.Setup(value => value.AddAsync(It.IsAny<StockMovement>())).ReturnsAsync((StockMovement movement) =>
            {
                Boundary.Deltas.Should().HaveCount(2, "all per-line balance saves must finish before the first movement INSERT is staged");
                MovementAdds++;
                Context.Add(movement);
                return movement;
            });
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(value => value.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),It.IsAny<string>(),It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request,string _,CancellationToken __) => new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed=!DenyPostingScope && request.PermissionCode=="procurement.inventory.adjust.approve" &&
                        request.WarehouseId==Warehouse.Id && Bins.Any(bin => bin.Id==request.LocationId) && request.RequireLocationScope,
                    Message="Posting scope denied."
                });
            access.Setup(value => value.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),It.IsAny<string>(),It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed=true });
            Finance.Setup(value => value.PostAsync(It.IsAny<StockAdjustment>(),It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryAdjustmentFinancePostingResult(Guid.NewGuid(),Guid.NewGuid()));
            var negative = new Mock<IInventoryNegativeStockControlService>();
            negative.Setup(value => value.PrepareDecreaseAsync(It.IsAny<InventoryStockDecreaseRequest>(),It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryStockDecreaseAuthorization());
            var valuation = new Mock<IInventoryValuationService>();
            valuation.Setup(value => value.ProcessAdjustmentAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),
                    It.IsAny<decimal>(),It.IsAny<decimal>(),It.IsAny<decimal>(),It.IsAny<bool>(),It.IsAny<string>(),
                    It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<Guid?>(),It.IsAny<DateTime?>(),It.IsAny<decimal?>(),It.IsAny<Guid?>()))
                .Returns(new InvocationFunc(invocation =>
                {
                    if (++ValuationCalls == 2 && FailSecondValuation) throw new InvalidOperationException("Second line valuation failed.");
                    var delta = (decimal)invocation.Arguments[3];
                    var unitCost = (decimal)invocation.Arguments[4];
                    return Task.FromResult(Math.Abs(delta*unitCost));
                }));
            Service = new StockAdjustmentService(Mock.Of<IStockAdjustmentRepository>(),items.Object,movements.Object,
                warehouseQuantities.Object,bins.Object,warehouses.Object,Mock.Of<IConsignmentSettlementService>(),actor.Object,Unit,
                Mock.Of<IInventoryTrackingControlService>(),negative.Object,access.Object,Mock.Of<IProcurementSodGuardService>(),
                Mock.Of<IWorkflowIntegrationService>(),Mock.Of<IProcurementControlEventService>(),Finance.Object,valuation.Object,
                NullLogger<StockAdjustmentService>.Instance);
        }

        public async Task SeedAsync()
        {
            Context.AddRange(Warehouse,Item,WarehouseQuantity,Adjustment,Count);
            Context.AddRange(Bins);
            Context.AddRange(Locations);
            await Context.SaveChangesAsync();
            // The context overwrites Added timestamps. Set deterministic saved-line
            // ordering after seeding so both positive-first and negative-first run.
            foreach (var line in Adjustment.Items)
                line.CreatedAt=DateTime.UtcNow.Date.AddSeconds((line.AdjustmentQuantity < 0) == NegativeFirst ? 0 : 1);
            await Context.SaveChangesAsync();
            Boundary.Enabled=true;
        }

        public Task<StockAdjustmentDetailDto> PostAsync() => Service.PostAsync(Adjustment.Id,ActorId,
            new StockAdjustmentActionRequest { IdempotencyKey="frozen-count-post",RowVersion=Convert.ToBase64String(Adjustment.RowVersion) });

        public async ValueTask DisposeAsync()
        {
            Unit.Dispose();
            await Context.DisposeAsync();
        }
    }

    private sealed class FreezeBoundary : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public StockAdjustment Adjustment { get; set; } = null!;
        public Func<bool> HasTransaction { get; set; } = null!;
        public List<decimal> Deltas { get; } = [];
        public bool AllBalanceWritesHadTransaction { get; private set; } = true;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result,CancellationToken cancellationToken=default)
        {
            if (!Enabled) return ValueTask.FromResult(result);
            var context=eventData.Context!;
            context.ChangeTracker.DetectChanges();
            foreach (var entry in context.ChangeTracker.Entries<WarehouseQuantity>().Where(value => value.State==EntityState.Modified))
            {
                var before=entry.Property(value => value.CurrentStock).OriginalValue;
                var after=entry.Property(value => value.CurrentStock).CurrentValue;
                if (after==before) continue;
                AllBalanceWritesHadTransaction &= HasTransaction();
                context.Set<StockMovement>().Local.Should().NotContain(value => value.ReferenceId==Adjustment.Id,
                    "the existing SQL freeze exemption rejects quantity writes after an adjustment movement exists or is staged first by EF");
                Adjustment.Status.Should().Be("Posted");
                Adjustment.Items.Should().Contain(value => value.InventoryItemId==entry.Entity.InventoryItemId && value.AdjustmentQuantity==after-before,
                    "the deployed freeze predicate allows one exact approved line delta, not a combined same-item delta");
                Deltas.Add(after-before);
            }
            return ValueTask.FromResult(result);
        }
    }
}
