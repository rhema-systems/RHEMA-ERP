using System.Reflection;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryNegativeStockControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Override_model_is_tenant_scoped_single_use_and_concurrency_protected()
    {
        var entity = _context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InventoryNegativeStockOverride))!;
        entity.FindProperty(nameof(InventoryNegativeStockOverride.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        entity.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "WorkflowInstanceId" }));
        entity.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryNegativeStockOverrides_Quantity",
            "CK_InventoryNegativeStockOverrides_Expiry",
            "CK_InventoryNegativeStockOverrides_Integrity",
            "CK_InventoryNegativeStockOverrides_Consumption"
        });
    }

    [Fact]
    public void Migration_adds_only_override_register_and_two_atomic_stock_hard_stops()
    {
        var migration = new TDC0610NegativeStockControl();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name)
            .Should().Equal("InventoryNegativeStockOverrides");
        var sql = string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_WarehouseQuantities_NegativeStockGuard");
        sql.Should().Contain("TR_InventoryItems_NegativeStockGuard");
        sql.Should().Contain("CURRENT_TRANSACTION_ID()");
        sql.Should().Contain("TDC0610_OVERRIDE_ID");
        sql.Should().Contain("TDC0610_TRANSACTION_ID");
        sql.Should().Contain("INV_NEGATIVE_STOCK_SQL_PROHIBITED");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
        sql.Should().NotContain("UPDATE [dbo].[InventoryItems]");
    }

    [Fact]
    public void Current_relational_model_matches_the_compiled_migration_snapshot()
    {
        using var sqlServerContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(local);Database=Tdc0610ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
                .Options);
        var snapshot = sqlServerContext.GetService<IMigrationsAssembly>().ModelSnapshot;
        if (snapshot is null)
            return; // Normal fast Debug builds intentionally omit the snapshot; focused tooling and Release compile it.

        var differ = sqlServerContext.GetService<IMigrationsModelDiffer>();
        var runtimeInitializer = sqlServerContext.GetService<IModelRuntimeInitializer>();
        var snapshotModel = runtimeInitializer.Initialize(snapshot.Model, designTime: true);
        var current = sqlServerContext.GetService<IDesignTimeModel>().Model;
        var operations = differ.GetDifferences(snapshotModel.GetRelationalModel(), current.GetRelationalModel());
        operations.Should().BeEmpty(
            "the migration snapshot must match the current model; operations: {0}",
            string.Join(", ", operations.Select(value => value.GetType().Name)));
    }

    [Fact]
    public void Concurrent_decrement_owners_compose_the_same_atomic_guard()
    {
        var root = FindRepositoryRoot();
        var paths = new[]
        {
            Path.Combine("src", "ErpSystem.Core", "Services", "Inventory", "InventoryRequisitionService.cs"),
            Path.Combine("src", "ErpSystem.Core", "Services", "Inventory", "InventoryTransferService.cs"),
            Path.Combine("src", "ErpSystem.Core", "Services", "Inventory", "StockAdjustmentService.cs"),
            Path.Combine("src", "ErpSystem.Core", "Services", "Inventory", "InventoryManagementService.cs"),
            Path.Combine("src", "ErpSystem.Core", "Services", "Maintenance", "WorkOrderPartService.cs"),
            Path.Combine("src", "ErpSystem.Core", "Services", "Maintenance", "WorkOrderToolService.cs"),
            Path.Combine("src", "ErpSystem.Api", "Controllers", "Inventory", "WarehouseLocationsController.cs")
        };
        paths.Should().OnlyContain(path =>
            File.ReadAllText(Path.Combine(root, path)).Contains("PrepareDecreaseAsync", StringComparison.Ordinal));

        var guard = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory", "InventoryNegativeStockControlService.cs"));
        guard.Should().Contain("AcquireTransactionLockAsync");
        guard.Should().Contain("inventory-stock:");
        guard.Should().Contain("ControlledEmergencyOverride");
        guard.Should().Contain("CentralDocumentEvidenceRules.CurrentPublished()");
        guard.Should().Contain("FileVirusScanStatus.Clean");
        guard.Should().Contain("WorkflowInstanceStatus.Completed");
        guard.Should().Contain("INV_NEGATIVE_STOCK_PROHIBITED");
        guard.Should().Contain("PermissionCode = permission");
        guard.Should().Contain("\"procurement.inventory.read\", \"Inventory.EmergencyOverride\"");
        guard.Should().Contain("CheckCapabilityAsync");
    }

    [Fact]
    public async Task Exact_bin_shortage_is_governed_even_when_item_and_warehouse_aggregates_are_sufficient()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouse = new Warehouse
        {
            TenantId = tenantId, Code = "NEG-BIN", Name = "Negative bin warehouse", IsActive = true
        };
        var location = new WarehouseLocation
        {
            TenantId = tenantId, WarehouseId = warehouse.Id, LocationCode = "NEG-BIN-01", IsActive = true
        };
        var item = new InventoryItem
        {
            TenantId = tenantId, CategoryId = Guid.NewGuid(), ItemCode = "NEG-BIN-ITEM", Name = "Exact-bin item",
            CurrentStock = 100m, AvailableStock = 100m
        };
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        await context.AddRangeAsync(warehouse, location, item,
            new WarehouseQuantity
            {
                TenantId = tenantId, WarehouseId = warehouse.Id, InventoryItemId = item.Id,
                CurrentStock = 100m, AvailableStock = 100m
            },
            new InventoryLocation
            {
                TenantId = tenantId, LocationId = location.Id, InventoryItemId = item.Id,
                Quantity = 2m, AvailableQuantity = 2m
            });
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.IsAuthenticated).Returns(true);
        current.SetupGet(value => value.IsExternalUser).Returns(false);
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.UserId).Returns(userId);
        var configuration = new Mock<IProcurementConfigurationService>();
        configuration.Setup(value => value.GetEffectiveProfileAsync(
                "TDC-PROCUREMENT", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementConfigurationProfileDto
            {
                Id = Guid.NewGuid(), Version = 1,
                Decisions = new[]
                {
                    new ProcurementConfigurationDecisionDto
                    {
                        Id = Guid.NewGuid(), DecisionKey = "DEC-010", IsComplete = true,
                        Value = JsonSerializer.SerializeToElement(new ProcurementNegativeStockDecisionValueDto
                        {
                            DefaultPolicy = ProcurementNegativeStockPolicy.Prohibited,
                            EmergencyOverrideEligible = false,
                            OverridePermission = "Inventory.EmergencyOverride"
                        })
                    }
                }
            });
        var mutationStore = new Mock<IInventoryNegativeStockMutationStore>();
        mutationStore.SetupGet(value => value.HasRequiredTransaction).Returns(true);
        var service = new InventoryNegativeStockControlService(unitOfWork, current.Object, configuration.Object,
            Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcurementControlEventService>(), mutationStore.Object);
        await unitOfWork.BeginTransactionAsync();

        var prepare = () => service.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
        {
            InventoryItemId = item.Id,
            WarehouseId = warehouse.Id,
            LocationId = location.Id,
            Quantity = 5m,
            ReferenceId = Guid.NewGuid(),
            ReferenceType = "StockAdjustment",
            ReferenceNumber = "ADJ-NEG-BIN",
            CorrelationId = "neg-bin"
        });

        await prepare.Should().ThrowAsync<InventoryNegativeStockControlException>()
            .Where(error => error.Code == "INV_NEGATIVE_STOCK_PROHIBITED");
        await unitOfWork.RollbackAsync();
    }

    [Fact]
    public void Project_material_cost_remains_a_projection_not_a_parallel_stock_mutator()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Projects", "ProjectService.MaterialCosts.cs"));
        source.Should().Contain("StockMovement");
        source.Should().NotContain("CurrentStock -=");
        source.Should().NotContain("AvailableStock -=");
        source.Should().NotContain("new InventoryTransaction");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public void Dispose() => _context.Dispose();
}
