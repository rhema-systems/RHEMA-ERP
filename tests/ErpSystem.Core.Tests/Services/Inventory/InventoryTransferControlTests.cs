using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
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

public sealed class InventoryTransferControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Ef_model_has_transfer_concurrency_replay_append_only_register_and_central_dms_lineage()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var transfer = model.FindEntityType(typeof(InventoryTransfer))!;
        transfer.FindProperty(nameof(InventoryTransfer.RowVersion))!.IsConcurrencyToken.Should().BeTrue();

        model.FindEntityType(typeof(InventoryTransferAction))!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "InventoryTransferId", "ActionType", "IdempotencyKey" }));
        model.FindEntityType(typeof(InventoryTransferActionLine))!.GetIndexes().Should().Contain(index => index.IsUnique);
        model.FindEntityType(typeof(InventoryTransferDiscrepancy))!.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "InventoryTransferId", "Status" }));

        var evidence = model.FindEntityType(typeof(InventoryTransferDiscrepancyEvidence))!;
        evidence.GetIndexes().Should().Contain(index => index.IsUnique);
        evidence.GetForeignKeys().Should().Contain(key => key.PrincipalEntityType.ClrType == typeof(CentralDocumentVersion));
        evidence.GetForeignKeys().Should().Contain(key => key.PrincipalEntityType.ClrType == typeof(FileUploadRecord));
    }

    [Fact]
    public void Controlled_mutation_requests_require_concurrency_and_replay_fields()
    {
        var mutation = new InventoryTransferMutationContext();
        var mutationResults = new List<ValidationResult>();
        Validator.TryValidateObject(mutation, new ValidationContext(mutation), mutationResults, true).Should().BeFalse();
        mutationResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(InventoryTransferMutationContext.RowVersion));
        mutationResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(InventoryTransferMutationContext.IdempotencyKey));

        var costs = new ShipTransferWithCostsDto();
        var costResults = new List<ValidationResult>();
        Validator.TryValidateObject(costs, new ValidationContext(costs), costResults, true).Should().BeFalse();
        costResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(ShipTransferWithCostsDto.RowVersion));
        costResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(ShipTransferWithCostsDto.IdempotencyKey));
    }

    [Fact]
    public void Migration_is_focused_delta_with_seven_sql_hard_stops_and_no_parallel_stock_updates()
    {
        var migration = new TDC0608ControlledInventoryTransfers();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name).Should().BeEquivalentTo(
            "InventoryTransferActions", "InventoryTransferActionLines", "InventoryTransferDiscrepancies",
            "InventoryTransferDiscrepancyEvidence");
        builder.Operations.OfType<AlterColumnOperation>().Should().BeEmpty();
        builder.Operations.OfType<AddColumnOperation>().Should().Contain(value =>
            value.Table == "InventoryTransfers" && value.Name == "RowVersion");

        var sql = string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        new[]
        {
            "TR_InventoryTransferActions_AppendOnly", "TR_InventoryTransferActionLines_AppendOnly",
            "TR_InventoryTransferItems_ControlledMutation", "TR_InventoryTransferDiscrepancies_ControlledLifecycle",
            "TR_InventoryTransferDiscrepancyEvidence_AppendOnly", "TR_InventoryTransfers_ControlledLifecycle",
            "TR_StockMovements_GovernedInventoryTransfer"
        }.Should().OnlyContain(trigger => sql.Contains(trigger, StringComparison.Ordinal));
        sql.Should().Contain("INV_TRANSFER_TRANSITION_INVALID");
        sql.Should().Contain("INV_TRANSFER_MOVEMENT_UNGOVERNED");
        sql.Should().Contain("CentralDocumentVersions");
        sql.Should().Contain("REPLACEMENT_RECEIVED");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
        sql.Should().NotContain("UPDATE [dbo].[InventoryItems]");
    }

    [Fact]
    public void Action_types_cover_cost_dispatch_receipt_resolution_closure_reversal_and_cancellation()
    {
        Enum.GetValues<InventoryTransferActionType>().Should().BeEquivalentTo(new[]
        {
            InventoryTransferActionType.Submitted, InventoryTransferActionType.Approved,
            InventoryTransferActionType.Rejected, InventoryTransferActionType.ShippingCostsSaved,
            InventoryTransferActionType.Dispatched, InventoryTransferActionType.Received,
            InventoryTransferActionType.DiscrepancyResolved, InventoryTransferActionType.Closed,
            InventoryTransferActionType.ShipmentReversed, InventoryTransferActionType.Cancelled
        });
    }

    [Fact]
    public void Controlled_mutations_wrap_serializable_transactions_in_the_configured_retry_strategy()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory", "InventoryTransferService.cs"));

        Regex.Matches(source, "return await ExecuteControlledMutationAsync", RegexOptions.CultureInvariant)
            .Should().HaveCount(11, "every controlled transfer state mutation must enter the shared retry-safe transaction wrapper");
        source.Should().Contain("_unitOfWork.ExecuteInStrategyAsync(async () =>");
        source.Should().Contain("_unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable)");
        source.Should().Contain("_unitOfWork.ClearTrackedChanges();");
    }

    [Fact]
    public void Discrepancy_stock_and_movement_use_the_selected_bins_effective_inventory_warehouse()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory", "InventoryTransferService.cs"));
        var start = source.IndexOf("public async Task<bool> ResolveDiscrepanciesAsync", StringComparison.Ordinal);
        var end = source.IndexOf("public async Task<bool> CloseAsync", start, StringComparison.Ordinal);
        var resolution = source[start..end];

        resolution.Should().Contain("var inventoryWarehouseId = location?.InventoryWarehouseId ?? warehouseId;");
        resolution.Should().Contain("GetByWarehouseAndItemAsync(inventoryWarehouseId, item.InventoryItemId)");
        resolution.Should().Contain("WarehouseId = inventoryWarehouseId");
        resolution.Should().NotContain("GetByWarehouseAndItemAsync(warehouseId, item.InventoryItemId)");
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
