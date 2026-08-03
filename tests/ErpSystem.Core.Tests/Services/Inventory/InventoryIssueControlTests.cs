using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Core.DTOs.Documents;
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

public sealed class InventoryIssueControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Ef_model_has_requisition_concurrency_voucher_replay_and_append_only_lineage()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var requisition = model.FindEntityType(typeof(InventoryRequisition))!;
        requisition.FindProperty(nameof(InventoryRequisition.RowVersion))!.IsConcurrencyToken.Should().BeTrue();

        var voucher = model.FindEntityType(typeof(InventoryIssueVoucher))!;
        voucher.FindProperty(nameof(InventoryIssueVoucher.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        voucher.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "InventoryRequisitionId", "IdempotencyKey" }));
        voucher.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryIssueVouchers_Sod",
            "CK_InventoryIssueVouchers_Acknowledgement",
            "CK_InventoryIssueVouchers_Hashes"
        });

        var action = model.FindEntityType(typeof(InventoryIssueVoucherAction))!;
        action.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "InventoryIssueVoucherId", "Sequence" }));
    }

    [Fact]
    public void Stock_movement_has_required_voucher_lineage_relationship()
    {
        var movement = _context.Model.FindEntityType(typeof(StockMovement))!;
        movement.FindProperty(nameof(StockMovement.InventoryIssueVoucherId)).Should().NotBeNull();
        movement.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(StockMovement.InventoryIssueVoucherId) &&
            key.PrincipalEntityType.ClrType == typeof(InventoryIssueVoucher));
    }

    [Fact]
    public void Issue_request_requires_replay_concurrency_receiver_and_positive_lines()
    {
        var request = new IssueRequisitionDto
        {
            Items = new List<IssueRequisitionItemDto>()
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, true).Should().BeFalse();
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.IdempotencyKey));
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.RowVersion));
        results.SelectMany(value => value.MemberNames).Should().Contain(nameof(IssueRequisitionDto.ReceiverUserId));
    }

    [Fact]
    public void Migration_is_three_table_delta_with_sql_hard_stops_and_no_parallel_stock_update()
    {
        var migration = new TDC0606ControlledInventoryIssue();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name).Should().BeEquivalentTo(
            "InventoryIssueVouchers", "InventoryIssueVoucherLines", "InventoryIssueVoucherActions");
        builder.Operations.OfType<AddColumnOperation>().Should().Contain(value =>
            value.Table == "InventoryRequisitions" && value.Name == "RowVersion");
        builder.Operations.OfType<AddColumnOperation>().Should().Contain(value =>
            value.Table == "StockMovements" && value.Name == "InventoryIssueVoucherId");

        var sql = string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryIssueVouchers_ControlledLifecycle");
        sql.Should().Contain("INV_ISSUE_APPROVED_SOURCE_REQUIRED");
        sql.Should().Contain("d.ReceiverComment IS NULL");
        sql.Should().Contain("i.IntegrityHash <> d.IntegrityHash");
        sql.Should().Contain("i.IsDeleted <> d.IsDeleted");
        sql.Should().Contain("TR_InventoryIssueVoucherLines_AppendOnly");
        sql.Should().Contain("line.InventoryRequisitionItemId = i.InventoryRequisitionItemId");
        sql.Should().Contain(") > r.IssuedQuantity");
        sql.Should().Contain("TR_InventoryIssueVoucherActions_AppendOnly");
        sql.Should().Contain("TR_StockMovements_GovernedRequisitionIssue");
        sql.Should().Contain("INV_ISSUE_APPROVED_VOUCHER_REQUIRED");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
        sql.Should().NotContain("UPDATE [dbo].[InventoryItems]");
    }

    [Fact]
    public void Store_issue_voucher_uses_shared_document_output_type()
    {
        DocumentTypes.InventoryStoreIssueVoucher.Should().Be("Inventory.StoreIssueVoucher");
    }

    public void Dispose() => _context.Dispose();
}
