using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
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

public sealed class InventoryPhysicalCountControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Model_has_schedule_count_line_concurrency_and_immutable_replay_history()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        model.FindEntityType(typeof(PhysicalCount))!.FindProperty(nameof(PhysicalCount.RowVersion))!
            .IsConcurrencyToken.Should().BeTrue();
        model.FindEntityType(typeof(PhysicalCountItem))!.FindProperty(nameof(PhysicalCountItem.RowVersion))!
            .IsConcurrencyToken.Should().BeTrue();
        model.FindEntityType(typeof(InventoryCycleCountSchedule))!.FindProperty(nameof(InventoryCycleCountSchedule.RowVersion))!
            .IsConcurrencyToken.Should().BeTrue();
        model.FindEntityType(typeof(PhysicalCountAction))!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "PhysicalCountId", "ActionType", "IdempotencyKey" }));
    }

    [Fact]
    public void Count_and_recount_requests_require_lineage_concurrency_and_replay_fields()
    {
        var first = new RecordCountItemDto();
        var firstResults = new List<ValidationResult>();
        Validator.TryValidateObject(first, new ValidationContext(first), firstResults, true).Should().BeFalse();
        firstResults.SelectMany(x => x.MemberNames).Should().Contain(nameof(RecordCountItemDto.RowVersion));
        firstResults.SelectMany(x => x.MemberNames).Should().Contain(nameof(RecordCountItemDto.IdempotencyKey));

        var recount = new RecordPhysicalCountRecountRequest();
        var recountResults = new List<ValidationResult>();
        Validator.TryValidateObject(recount, new ValidationContext(recount), recountResults, true).Should().BeFalse();
        recountResults.SelectMany(x => x.MemberNames).Should().Contain(nameof(RecordPhysicalCountRecountRequest.ItemRowVersion));
        recountResults.SelectMany(x => x.MemberNames).Should().Contain(nameof(RecordPhysicalCountRecountRequest.InvestigationNotes));
    }

    [Fact]
    public void Migration_is_focused_delta_with_seven_count_and_freeze_hard_stops()
    {
        var migration = new TDC0609ControlledCycleCounts();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(x => x.Name).Should().BeEquivalentTo(
            "InventoryCycleCountSchedules", "PhysicalCountActions");
        var sql = string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        new[]
        {
            "TR_InventoryCycleCountSchedules_ControlledLifecycle", "TR_PhysicalCountActions_AppendOnly",
            "TR_PhysicalCountItems_ControlledMutation", "TR_PhysicalCounts_ControlledLifecycle",
            "TR_WarehouseQuantities_PhysicalCountFreeze", "TR_InventoryItems_PhysicalCountFreeze",
            "TR_StockMovements_PhysicalCountFreeze"
        }.Should().OnlyContain(trigger => sql.Contains(trigger, StringComparison.Ordinal));
        sql.Should().Contain("INV_COUNT_TRANSITION_INVALID");
        sql.Should().Contain("INV_COUNT_MOVEMENT_FROZEN");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
        sql.Should().NotContain("UPDATE [dbo].[InventoryItems]");
    }

    [Fact]
    public void Service_composes_calendar_stock_adjustment_finance_and_control_event_owners()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory", "PhysicalCountService.Controlled.cs"));
        source.Should().Contain("_stockAdjustmentService.CreateAsync");
        source.Should().Contain("_stockAdjustmentService.SubmitAsync");
        source.Should().Contain("_stockAdjustmentService.DecideAsync");
        source.Should().Contain("_stockAdjustmentService.PostAsync");
        source.Should().Contain("ProcurementCalendarOccurrence");
        source.Should().Contain("_controlEvents.RecordAsync");
        source.Should().NotContain("CurrentStock +=");
        source.Should().NotContain("new StockMovement");
    }

    [Fact]
    public void Legacy_batch_single_approval_and_direct_post_bypasses_are_disabled()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory", "PhysicalCountService.cs"));
        source.Should().Contain("legacy batch-count path is disabled");
        source.Should().Contain("legacy single-approval path is disabled");
        source.Should().Contain("legacy direct quantity overwrite path is disabled");
        source.Should().Contain("IsSystemQuantityVisible");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public void Dispose() => _context.Dispose();
}
