using ErpSystem.Api.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class OperationalUatBaselineSeederTests
{
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    [Fact]
    public async Task FinanceSeederBoundary_DetachesPreviouslyMaterializedGraph()
    {
        await using var db = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Code = "UAT", Name = "Operational UAT",
            ContactEmail = "uat@example.invalid", Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        db.Add(tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Entries().Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Unchanged);

        var seeder = new OperationalUatBaselineSeeder(
            db, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<OperationalUatBaselineSeeder>.Instance);

        seeder.ResetTrackingAtSeederBoundary();

        db.ChangeTracker.Entries().Should().BeEmpty(
            "Finance reconciliation must not inherit stale entities from access seeding");
    }

    [Fact]
    public void FinanceGovernanceBlocker_IsPreservedWithoutMaskingOtherFailures()
    {
        OperationalUatBaselineSeeder.IsPreservableFinanceGovernanceBlocker(new InvalidOperationException(
                "FINANCE_CLASSIFICATION_ENABLED_MAPPING_LINEAGE_INVALID: user-owned mapping requires review."))
            .Should().BeTrue();
        OperationalUatBaselineSeeder.IsPreservableFinanceGovernanceBlocker(new InvalidOperationException(
                "A database update failed."))
            .Should().BeFalse();
    }

    [Fact]
    public async Task InventoryMasterPass_Twice_CreatesMissingRecordsAndPreservesExistingValues()
    {
        await using var db = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Code = "UAT", Name = "Operational UAT",
            ContactEmail = "uat@example.invalid", Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        var existingCategory = new InventoryCategory
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "FILT", Name = "Tenant-owned filters",
            DefaultUnitOfMeasure = "PACK", IsActive = true, CreatedBy = "Tests"
        };
        var existingWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "DEMO-PM", Name = "Tenant-owned warehouse",
            IsActive = true, CreatedBy = "Tests"
        };
        var existingItem = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, ItemCode = "FILTER-AIR-001",
            Name = "Tenant-owned filter", CategoryId = existingCategory.Id,
            UnitOfMeasure = "PACK", StandardCost = 99m, Status = ItemStatus.Active,
            CreatedBy = "Tests"
        };
        db.AddRange(
            tenant,
            new UnitOfMeasure
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "EA", Name = "Tenant-owned each",
                Category = "Quantity", IsBaseUnit = true, IsActive = true, CreatedBy = "Tests"
            },
            existingCategory,
            existingWarehouse,
            existingItem);
        await db.SaveChangesAsync();

        var seeder = new OperationalUatBaselineSeeder(
            db, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<OperationalUatBaselineSeeder>.Instance);

        var first = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);
        var second = await seeder.EnsureInventoryMasterDataAsync(tenant.Id, CancellationToken.None);

        first.Should().Be(new InventoryMasterSeedCounts(4, 5, 1, 3, 8, 2));
        second.Should().Be(new InventoryMasterSeedCounts(0, 0, 0, 0, 0, 0));
        (await db.UnitsOfMeasure.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(5);
        (await db.InventoryCategories.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(6);
        (await db.Warehouses.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(2);
        (await db.WarehouseLocations.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(3);
        (await db.InventoryItems.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(9);
        (await db.Suppliers.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(2);

        existingCategory.Name.Should().Be("Tenant-owned filters");
        existingCategory.DefaultUnitOfMeasure.Should().Be("PACK");
        existingWarehouse.Name.Should().Be("Tenant-owned warehouse");
        existingItem.Name.Should().Be("Tenant-owned filter");
        existingItem.UnitOfMeasure.Should().Be("PACK");
        existingItem.StandardCost.Should().Be(99m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"operational-uat-{Guid.NewGuid():N}")
            .UseInternalServiceProvider(Provider)
            .Options;
        return new ApplicationDbContext(options);
    }
}
