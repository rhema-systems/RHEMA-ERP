using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

/// <summary>
/// Real DbContext/save-boundary tests. SQL transaction/row-lock behavior requires
/// the coordinated SQL acceptance run; InMemory assertions do not claim rollback.
/// </summary>
public sealed class InventoryCurrentCostProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Value_only_landed_cost_uses_pending_value_and_preserves_saved_count_and_movement(bool synchronous)
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(300, 572127);
        var countLine = new PhysicalCountItem { TenantId=fixture.Tenant, InventoryItemId=fixture.Item.Id,
            PhysicalCountId=Guid.NewGuid(), LocationId=scope.Bin.Id, UnitCost=1918.85m,
            SystemQuantity=299, CountedQuantity=300, VarianceQuantity=1, VarianceValue=1918.85m };
        var movement = new InventoryMovement { TenantId=fixture.Tenant, InventoryItemId=fixture.Item.Id,
            WarehouseId=scope.Warehouse.Id, LocationId=scope.Bin.Id, MovementNumber="IMMUTABLE-HISTORY",
            UnitCost=1918.85m, Quantity=1, TotalValue=1918.85m, IsPosted=true };
        var journal = new JournalEntry { TenantId=fixture.Tenant, JournalEntryNumber="JE-COST-HISTORY",
            Description="Retained count journal", TotalDebitAmount=1918.85m, TotalCreditAmount=1918.85m };
        fixture.Db.AddRange(countLine, movement, journal);
        await fixture.Db.SaveChangesAsync();

        scope.Balance.TotalValue = 575654.86m;
        // A pre-save database query still returns the old value: this was the
        // former landed-cost failure. The projection must overlay tracked changes.
        (await fixture.Db.Set<InventoryBalance>().AsNoTracking().SingleAsync()).TotalValue.Should().Be(572127);
        if (synchronous) fixture.Db.SaveChanges(); else await fixture.Db.SaveChangesAsync();

        fixture.Item.AverageCost.Should().Be(1918.85m);
        scope.WarehouseQuantity.AverageCost.Should().Be(1918.85m);
        scope.Location.AverageCost.Should().Be(1918.85m);
        scope.Balance.TotalValue.Should().Be(575654.86m);
        scope.Balance.AverageUnitCost.Should().Be(1918.85m);
        scope.Balance.QuantityOnHand.Should().Be(300);
        scope.WarehouseQuantity.CurrentStock.Should().Be(300);
        scope.Location.Quantity.Should().Be(300);
        fixture.Item.CurrentStock.Should().Be(300);
        countLine.UnitCost.Should().Be(1918.85m);
        countLine.VarianceValue.Should().Be(1918.85m);
        movement.UnitCost.Should().Be(1918.85m);
        movement.TotalValue.Should().Be(1918.85m);
        journal.TotalDebitAmount.Should().Be(1918.85m);
        journal.TotalCreditAmount.Should().Be(1918.85m);
    }

    [Theory]
    [InlineData(ValuationMethod.FIFO)]
    [InlineData(ValuationMethod.WeightedAverage)]
    [InlineData(ValuationMethod.StandardCost)]
    public async Task Real_receipt_and_issue_project_the_authoritative_remaining_value(ValuationMethod method)
    {
        using var fixture = new Fixture();
        fixture.Item.ValuationMethod = method;
        fixture.Item.StandardCost = 12;
        var scope = fixture.Scope(0, 0);
        await fixture.Db.SaveChangesAsync();
        var valuation = fixture.Valuation();
        await valuation.ProcessReceiptAsync(fixture.Item.Id, scope.Warehouse.Id, scope.Bin.Id,
            10, 10, ReferenceType.PO, "PO-COST-TEST", Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();
        await valuation.ProcessReceiptAsync(fixture.Item.Id, scope.Warehouse.Id, scope.Bin.Id,
            10, 20, ReferenceType.PO, "PO-COST-TEST2", Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(method == ValuationMethod.StandardCost ? 12 : 15);

        var issuedValue = await valuation.ProcessIssueAsync(fixture.Item.Id, scope.Warehouse.Id, scope.Bin.Id,
            10, InventoryMovementType.RequisitionIssue, ReferenceType.Requisition, "ISSUE-COST-TEST", Guid.NewGuid());
        await fixture.Db.SaveChangesAsync();

        var expected = method == ValuationMethod.FIFO ? 20 : method == ValuationMethod.StandardCost ? 12 : 15;
        fixture.Item.AverageCost.Should().Be(expected);
        scope.WarehouseQuantity.AverageCost.Should().Be(expected);
        scope.Location.AverageCost.Should().Be(expected);
        issuedValue.Should().Be(method == ValuationMethod.FIFO ? 100 : method == ValuationMethod.StandardCost ? 120 : 150);
        fixture.Item.StandardCost.Should().Be(12, "current average is not the configured standard cost");
    }

    [Fact]
    public async Task Multiple_added_bins_are_counted_once_and_new_cache_rows_remain_added_until_save()
    {
        using var fixture = new Fixture();
        var first = fixture.Scope(10, 100);
        var second = fixture.Scope(20, 400, first.Warehouse, first.WarehouseQuantity);
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(16.67m);
        first.WarehouseQuantity.AverageCost.Should().Be(16.67m);
        first.Location.AverageCost.Should().Be(10);
        second.Location.AverageCost.Should().Be(20);
        (await fixture.Db.Set<InventoryBalance>().CountAsync()).Should().Be(2);
        (await fixture.Db.Set<WarehouseQuantity>().CountAsync()).Should().Be(1);
        (await fixture.Db.Set<InventoryLocation>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Current_item_average_is_weighted_across_owned_warehouses_not_consignment()
    {
        using var fixture = new Fixture();
        var first = fixture.Scope(10, 100);
        var second = fixture.Scope(30, 900);
        var consigned = fixture.Scope(10, 9000);
        consigned.Warehouse.IsConsignmentWarehouse = true;
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(25);
        first.WarehouseQuantity.AverageCost.Should().Be(10);
        second.WarehouseQuantity.AverageCost.Should().Be(30);
        consigned.WarehouseQuantity.AverageCost.Should().Be(900);
        consigned.Location.AverageCost.Should().Be(900);

        second.Balance.TotalValue = 600;
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(17.5m);
        consigned.WarehouseQuantity.AverageCost.Should().Be(900);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zero_or_deleted_last_balance_resets_only_current_averages(bool deleted)
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(10, 100);
        fixture.Item.StandardCost = 37;
        await fixture.Db.SaveChangesAsync();
        if (deleted) scope.Balance.IsDeleted = true;
        else { scope.Balance.QuantityOnHand = 0; scope.Balance.TotalValue = 0; }
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(0);
        scope.Location.AverageCost.Should().Be(0);
        scope.WarehouseQuantity.AverageCost.Should().Be(0);
        fixture.Item.StandardCost.Should().Be(37);
        fixture.Item.CurrentStock.Should().Be(10, "the projection must never reconcile quantities");
    }

    [Fact]
    public async Task Rounded_current_average_uses_mapped_value_precision_and_away_from_zero_not_bankers_rounding()
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(2, 2.45m);
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(1.23m);
        scope.Balance.TotalValue = 2.4651m;
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(1.24m);
        scope.Balance.TotalValue.Should().Be(2.4651m, "rounding the display projection must not rewrite valuation");
    }

    [Fact]
    public async Task Foreign_tenant_costs_are_not_read_or_updated_by_an_owned_balance_change()
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(10, 100);
        var foreignItem = new InventoryItem { TenantId=Guid.NewGuid(), ItemCode="FOREIGN", Name="Foreign", AverageCost=999 };
        fixture.Db.Add(foreignItem);
        await fixture.Db.SaveChangesAsync();
        scope.Balance.TotalValue = 200;
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(20);
        foreignItem.AverageCost.Should().Be(999);
    }

    [Fact]
    public async Task Tenant_bound_context_rejects_a_foreign_balance_before_persistence()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, Guid.NewGuid());
        db.Add(new InventoryBalance { TenantId=Guid.NewGuid(), InventoryItemId=Guid.NewGuid(),
            WarehouseId=Guid.NewGuid(), QuantityOnHand=10, TotalValue=100 });
        var save = () => db.SaveChangesAsync();
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*current tenant*");
        (await db.Set<InventoryBalance>().IgnoreQueryFilters().AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Unrelated_save_does_not_run_a_valuation_catch_up_or_modify_approval_history()
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(10, 100);
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost = 999; // represent a legacy cache written by an unrelated path
        await fixture.Db.SaveChangesAsync();
        fixture.Item.Name = "Only descriptive master-data edit";
        scope.Balance.QuantityAllocated = 1;
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(999);
        scope.Balance.TotalValue.Should().Be(100);
    }

    [Fact]
    public async Task Nonzero_unlocated_balance_with_exact_bins_fails_without_guessing_or_double_counting()
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(10, 100);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.Add(new InventoryBalance { TenantId=fixture.Tenant, InventoryItemId=fixture.Item.Id,
            WarehouseId=scope.Warehouse.Id, QuantityOnHand=10, TotalValue=100 });
        var save = () => fixture.Db.SaveChangesAsync();
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*both exact-bin and nonzero unlocated balances*");
        (await fixture.Db.Set<InventoryBalance>().AsNoTracking().CountAsync()).Should().Be(1);
        fixture.Item.AverageCost.Should().Be(10);
    }

    [Fact]
    public async Task Zero_unlocated_placeholder_does_not_duplicate_exact_bin_value()
    {
        using var fixture = new Fixture();
        var scope = fixture.Scope(10, 100);
        fixture.Db.Add(new InventoryBalance { TenantId=fixture.Tenant, InventoryItemId=fixture.Item.Id, WarehouseId=scope.Warehouse.Id });
        await fixture.Db.SaveChangesAsync();
        fixture.Item.AverageCost.Should().Be(10);
    }

    [Fact]
    public void Sql_save_ownership_keeps_retry_strategy_and_existing_transaction_boundary_explicit()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src", "ErpSystem.Data", "ApplicationDbContext.cs")))
            directory = directory.Parent;
        directory.Should().NotBeNull();
        var source = File.ReadAllText(Path.Combine(directory!.FullName, "src", "ErpSystem.Data", "ApplicationDbContext.cs"));
        var start = source.IndexOf("public override async Task<int> SaveChangesAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private void NormalizeProcurementAwardReadinessAuditEnvelopes", start, StringComparison.Ordinal);
        var saveOwner = source[start..end];
        saveOwner.Should().Contain("Database.CurrentTransaction == null");
        saveOwner.Should().Contain("Database.CreateExecutionStrategy().ExecuteAsync(async () =>");
        saveOwner.Should().Contain("Database.CreateExecutionStrategy().Execute(() =>");
        saveOwner.IndexOf("ExecuteAsync(async () =>", StringComparison.Ordinal)
            .Should().BeLessThan(saveOwner.IndexOf("Database.BeginTransactionAsync", StringComparison.Ordinal));
        saveOwner.IndexOf("Execute(() =>", StringComparison.Ordinal)
            .Should().BeLessThan(saveOwner.IndexOf("Database.BeginTransaction()", StringComparison.Ordinal));
        saveOwner.Should().Contain("base.SaveChangesAsync(acceptAllChanges, cancellationToken)");
        saveOwner.Should().Contain("base.SaveChanges(acceptAllChanges)");
        // This is a source guard, not a substitute for SQL retry/rollback acceptance.
    }

    private sealed class Fixture : IDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        public InventoryItem Item { get; }
        public Fixture()
        {
            Item = new InventoryItem { TenantId=Tenant, ItemCode="COST-PROJECTION", Name="Projection test item",
                UnitOfMeasure="EA", ValuationMethod=ValuationMethod.WeightedAverage };
            Db.Add(Item);
        }
        public ScopeRows Scope(decimal quantity, decimal value, Warehouse? warehouse = null, WarehouseQuantity? warehouseQuantity = null)
        {
            warehouse ??= new Warehouse { TenantId=Tenant, Code="WH-"+Guid.NewGuid().ToString("N")[..6], Name="Projection warehouse" };
            if (Db.Entry(warehouse).State == EntityState.Detached) Db.Add(warehouse);
            warehouseQuantity ??= new WarehouseQuantity { TenantId=Tenant, InventoryItemId=Item.Id, WarehouseId=warehouse.Id };
            if (Db.Entry(warehouseQuantity).State == EntityState.Detached) Db.Add(warehouseQuantity);
            warehouseQuantity.CurrentStock += quantity;
            Item.CurrentStock += quantity;
            var bin = new WarehouseLocation { TenantId=Tenant, WarehouseId=warehouse.Id, LocationCode="BIN-"+Guid.NewGuid().ToString("N")[..6], Name="Projection bin" };
            var location = new InventoryLocation { TenantId=Tenant, InventoryItemId=Item.Id, LocationId=bin.Id, Quantity=quantity };
            var balance = new InventoryBalance { TenantId=Tenant, InventoryItemId=Item.Id, WarehouseId=warehouse.Id,
                LocationId=bin.Id, QuantityOnHand=quantity, QuantityAvailable=quantity, TotalValue=value,
                AverageUnitCost=quantity > 0 ? value/quantity : 0 };
            Db.AddRange(bin, location, balance);
            return new(warehouse, warehouseQuantity, bin, location, balance);
        }
        public InventoryValuationService Valuation()
        {
            var actor = new Mock<ICurrentUserProvider>();
            actor.SetupGet(value => value.TenantId).Returns(Tenant);
            actor.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
            return new(new UnitOfWork(Db), NullLogger<InventoryValuationService>.Instance, actor.Object,
                Mock.Of<IProcurementReceiptSourceControlService>());
        }
        public void Dispose() => Db.Dispose();
    }
    private sealed record ScopeRows(Warehouse Warehouse, WarehouseQuantity WarehouseQuantity, WarehouseLocation Bin,
        InventoryLocation Location, InventoryBalance Balance);
}
