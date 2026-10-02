using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class CommercialQuantityEvidencePersistenceTests
{
    [Fact]
    public async Task Save_FreezesStableUomEvidence_AndReplayUsesFrozenPolicy()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var uom = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Code = "CASE", Name = "Case", DecimalPlaces = 1, RoundingIncrement = 0.5m };
        db.UnitsOfMeasure.Add(uom);
        await db.SaveChangesAsync();
        var item = new InventoryItem { Id = Guid.NewGuid(), TenantId = tenantId, ItemCode = "ITEM-1", Name = "Item", UnitOfMeasure = "CASE", UnitOfMeasureId = uom.Id };
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync();

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id, WarehouseId = Guid.NewGuid(),
            MovementType = "Adjustment+", Quantity = 1.5m, ReferenceType = ReferenceType.Manual
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();

        movement.UnitOfMeasureId.Should().Be(uom.Id);
        movement.UnitOfMeasureCodeSnapshot.Should().Be("CASE");
        movement.UnitOfMeasureDecimalPlacesSnapshot.Should().Be(1);
        movement.UnitOfMeasureRoundingIncrementSnapshot.Should().Be(0.5m);

        uom.RoundingIncrement = 1m;
        await db.SaveChangesAsync();
        movement.Notes = "replay";
        movement.Quantity = -1.5m;
        await db.SaveChangesAsync();
        movement.UnitOfMeasureRoundingIncrementSnapshot.Should().Be(0.5m);
    }

    [Fact]
    public async Task Save_RejectsQuantityThatViolatesFrozenIncrement()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var uom = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = tenantId, Code = "PAL", Name = "Pallet", DecimalPlaces = 1, RoundingIncrement = 0.5m };
        db.UnitsOfMeasure.Add(uom);
        await db.SaveChangesAsync();
        var movement = new StockMovement { TenantId = tenantId, InventoryItemId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), MovementType = "Receipt", Quantity = 1.2m, ReferenceType = ReferenceType.Manual, UnitOfMeasureId = uom.Id };
        db.StockMovements.Add(movement);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*whole multiple*");
    }

    [Fact]
    public async Task PostedFinanceEvidence_RemainsFrozenAfterUomConfigurationChanges()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new ApplicationDbContext(options, tenantId);
        var uom = new UnitOfMeasure
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "CASE", Name = "Case",
            DecimalPlaces = 1, RoundingIncrement = 0.5m
        };
        db.UnitsOfMeasure.Add(uom);
        var postedLine = new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = Guid.NewGuid(),
            JournalEntryId = Guid.NewGuid(), TransactionDate = DateTime.UtcNow,
            DebitAmount = 10m, AccountingBookId = Guid.NewGuid(), FiscalPeriodId = Guid.NewGuid(),
            CommercialUnitOfMeasureId = uom.Id,
            CommercialUnitOfMeasureCode = "CASE",
            CommercialQuantityDecimalPlaces = 1,
            CommercialQuantityRoundingIncrement = 0.5m
        };
        db.AccountTransactions.Add(postedLine);
        await db.SaveChangesAsync();

        uom.DecimalPlaces = 0;
        uom.RoundingIncrement = 1m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var replayedEvidence = await db.AccountTransactions.SingleAsync(value => value.Id == postedLine.Id);
        replayedEvidence.CommercialUnitOfMeasureId.Should().Be(uom.Id);
        replayedEvidence.CommercialUnitOfMeasureCode.Should().Be("CASE");
        replayedEvidence.CommercialQuantityDecimalPlaces.Should().Be(1);
        replayedEvidence.CommercialQuantityRoundingIncrement.Should().Be(0.5m);
    }
}
