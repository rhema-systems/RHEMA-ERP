using System.Reflection;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
