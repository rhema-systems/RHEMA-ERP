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

public sealed class InventoryProjectReservationControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Existing_inventory_allocation_is_extended_as_the_authoritative_project_reservation_ledger()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var allocation = model.FindEntityType(typeof(InventoryAllocation))!;
        allocation.GetTableName().Should().Be("InventoryAllocations");
        allocation.FindProperty(nameof(InventoryAllocation.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        allocation.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "IdempotencyKey" }) &&
            index.GetFilter()!.Contains("ProjectRequisition", StringComparison.Ordinal));
        allocation.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "InventoryRequisitionItemId", "Status" }));
        allocation.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryAllocations_ProjectLineage",
            "CK_InventoryAllocations_ProjectHashes",
            "CK_InventoryAllocations_Quantities"
        });

        var action = model.FindEntityType(typeof(InventoryProjectReservationAction))!;
        action.GetTableName().Should().Be("InventoryProjectReservationActions");
        action.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "InventoryAllocationId", "Sequence" }));
    }

    [Fact]
    public void Migration_is_bounded_and_adds_append_only_and_transition_hard_stops()
    {
        var migration = new TDC0611ProjectInventoryReservations();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name)
            .Should().Equal("InventoryProjectReservationActions");
        builder.Operations.OfType<AddColumnOperation>().Should().OnlyContain(value =>
            value.Table == "InventoryAllocations");
        builder.Operations.OfType<DropIndexOperation>().Should().BeEmpty(
            "the pre-existing tenant allocation index must remain available to every allocation owner");
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryProjectReservationActions_Immutable");
        sql.Should().Contain("TR_InventoryAllocations_ProjectReservationGuard");
        sql.Should().Contain("INV_PROJECT_RESERVATION_DELETE_PROHIBITED");
        sql.Should().Contain("INV_PROJECT_RESERVATION_TRANSITION_INVALID");
        sql.Should().Contain("INV_PROJECT_RESERVATION_QUANTITY_INVALID");
        sql.ToUpperInvariant().Should().NotContain("CREATE TABLE [INVENTORYALLOCATIONS]");
    }

    [Fact]
    public void Reservation_lifecycle_composes_existing_issue_scheduler_notification_and_cost_owners()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryProjectReservationService.cs"));
        service.Should().Contain("Repository<InventoryAllocation>()");
        service.Should().Contain("INotificationService");
        service.Should().Contain("CreateNotificationAsync");
        service.Should().Contain("AcquireTransactionLockAsync");
        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().NotContain("Repository<SalesAllocation>");

        var requisition = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryRequisitionService.cs"));
        requisition.Should().Contain("FulfillForIssueAsync");
        requisition.Should().Contain("ReleaseForCancelledRequisitionAsync");

        var scheduler = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services",
            "ProcurementCalendarBackgroundService.cs"));
        scheduler.Should().Contain("ExpireDueAsync");

        var projectCost = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects",
            "ProjectService.MaterialCosts.cs"));
        projectCost.Should().Contain("StockMovement");
        projectCost.Should().NotContain("InventoryProjectReservationAction");
    }

    [Fact]
    public void Current_relational_model_matches_the_compiled_snapshot()
    {
        using var sqlServerContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(local);Database=Tdc0611ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
                .Options);
        var snapshot = sqlServerContext.GetService<IMigrationsAssembly>().ModelSnapshot;
        if (snapshot is null) return;
        var differ = sqlServerContext.GetService<IMigrationsModelDiffer>();
        var initializer = sqlServerContext.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(snapshot.Model, designTime: true);
        var current = sqlServerContext.GetService<IDesignTimeModel>().Model;
        differ.GetDifferences(snapshotModel.GetRelationalModel(), current.GetRelationalModel()).Should().BeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public void Dispose() => _context.Dispose();
}
