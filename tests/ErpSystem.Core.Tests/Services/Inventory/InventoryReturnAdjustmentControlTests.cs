using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
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

public sealed class InventoryReturnAdjustmentControlTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void Ef_model_has_return_replay_concurrency_append_only_evidence_and_direct_movement_lineage()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var voucher = model.FindEntityType(typeof(InventoryReturnVoucher))!;
        voucher.FindProperty(nameof(InventoryReturnVoucher.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        voucher.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "IdempotencyKey" }));

        model.FindEntityType(typeof(InventoryReturnVoucherLine))!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "InventoryReturnVoucherId", "InventoryRequisitionItemId" }));
        model.FindEntityType(typeof(InventoryReturnVoucherEvidence))!.GetIndexes().Should().Contain(index => index.IsUnique);
        model.FindEntityType(typeof(InventoryReturnVoucherAction))!.GetIndexes().Should().Contain(index => index.IsUnique);

        var movement = model.FindEntityType(typeof(StockMovement))!;
        movement.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(StockMovement.InventoryReturnVoucherId) &&
            key.PrincipalEntityType.ClrType == typeof(InventoryReturnVoucher));
    }

    [Fact]
    public void Ef_model_has_controlled_adjustment_concurrency_replay_evidence_and_actions()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        var adjustment = model.FindEntityType(typeof(StockAdjustment))!;
        adjustment.FindProperty(nameof(StockAdjustment.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        adjustment.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "IdempotencyKey" }));
        model.FindEntityType(typeof(StockAdjustmentEvidence))!.GetIndexes().Should().Contain(index => index.IsUnique);
        model.FindEntityType(typeof(StockAdjustmentAction))!.GetIndexes().Should().Contain(index => index.IsUnique);
    }

    [Fact]
    public void Requests_require_concurrency_replay_reason_and_controlled_lines()
    {
        var returnRequest = new ReturnRequisitionDto();
        var returnResults = new List<ValidationResult>();
        Validator.TryValidateObject(returnRequest, new ValidationContext(returnRequest), returnResults, true).Should().BeFalse();
        returnResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(ReturnRequisitionDto.ReasonCode));
        returnResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(ReturnRequisitionDto.Reason));
        returnResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(ReturnRequisitionDto.IdempotencyKey));
        returnResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(ReturnRequisitionDto.RowVersion));

        var adjustment = new CreateStockAdjustmentDto();
        var adjustmentResults = new List<ValidationResult>();
        Validator.TryValidateObject(adjustment, new ValidationContext(adjustment), adjustmentResults, true).Should().BeFalse();
        adjustmentResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(CreateStockAdjustmentDto.ReasonCode));
        adjustmentResults.SelectMany(value => value.MemberNames).Should().Contain(nameof(CreateStockAdjustmentDto.IdempotencyKey));
    }

    [Fact]
    public void Migration_is_focused_delta_with_nine_sql_hard_stops_and_finance_lineage()
    {
        var migration = new TDC0607ControlledInventoryReturnsAndAdjustments();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name).Should().BeEquivalentTo(
            "InventoryReturnVouchers", "InventoryReturnVoucherLines", "InventoryReturnVoucherEvidence",
            "InventoryReturnVoucherActions", "StockAdjustmentEvidence", "StockAdjustmentActions");
        builder.Operations.OfType<AlterColumnOperation>().Should().BeEmpty();
        builder.Operations.OfType<AddColumnOperation>().Should().Contain(value =>
            value.Table == "StockMovements" && value.Name == "InventoryReturnVoucherId");

        var sql = string.Join(Environment.NewLine, builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        new[]
        {
            "TR_InventoryReturnVouchers_ControlledLifecycle", "TR_InventoryReturnVoucherLines_AppendOnly",
            "TR_InventoryReturnVoucherEvidence_AppendOnly", "TR_InventoryReturnVoucherActions_AppendOnly",
            "TR_StockAdjustments_ControlledLifecycle", "TR_StockAdjustmentItems_ControlledMutation",
            "TR_StockAdjustmentEvidence_AppendOnly", "TR_StockAdjustmentActions_AppendOnly",
            "TR_StockMovements_GovernedInventoryReturnAdjustment"
        }.Should().OnlyContain(trigger => sql.Contains(trigger, StringComparison.Ordinal));
        sql.Should().Contain("INV_ADJUSTMENT_FINANCE_LINEAGE");
        sql.Should().Contain("FinancePostingEvents");
        sql.Should().Contain("CentralDocumentVersions");
        sql.Should().Contain("INV_RETURN_POSTED_VOUCHER_REQUIRED");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
        sql.Should().NotContain("UPDATE [dbo].[InventoryItems]");
    }

    [Fact]
    public void Store_return_voucher_uses_shared_document_output_type()
    {
        DocumentTypes.InventoryStoreReturnVoucher.Should().Be("Inventory.StoreReturnVoucher");
    }

    [Fact]
    public void Adjustment_creation_replay_hash_includes_evidence_and_normalized_line_payload()
    {
        var method = typeof(StockAdjustmentService).GetMethod(
            "CreateAdjustmentPayloadHash", BindingFlags.Static | BindingFlags.NonPublic)!;
        var itemId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var request = new CreateStockAdjustmentDto
        {
            WarehouseId = Guid.NewGuid(),
            ReasonCode = StockAdjustmentReasonCodes.Damage,
            Description = "Damaged stock",
            Reference = "INSPECTION-1",
            Items =
            {
                new CreateStockAdjustmentItemDto
                {
                    InventoryItemId = itemId,
                    LocationId = locationId,
                    AdjustmentQuantity = -2,
                    LotNumber = " LOT-01 "
                }
            },
            Evidence =
            {
                new InventoryControlEvidenceRequest
                {
                    CentralDocumentVersionId = Guid.NewGuid(),
                    EvidenceReference = " Damage report "
                }
            }
        };
        var original = (string)method.Invoke(null, new object[]
        {
            request, StockAdjustmentReasonCodes.Damage, "Damaged stock", "INSPECTION-1"
        })!;
        request.Evidence[0].CentralDocumentVersionId = Guid.NewGuid();
        var changedEvidence = (string)method.Invoke(null, new object[]
        {
            request, StockAdjustmentReasonCodes.Damage, "Damaged stock", "INSPECTION-1"
        })!;

        original.Should().NotBe(changedEvidence);
    }

    [Fact]
    public void Adjustment_draft_accepts_consignment_scope_and_defers_negative_stock_authorization_to_posting()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory",
            "StockAdjustmentService.cs"));
        var buildStart = source.IndexOf("private async Task BuildLinesAsync", StringComparison.Ordinal);
        var buildEnd = source.IndexOf("private async Task RequireAccessAsync", buildStart, StringComparison.Ordinal);
        var draftLineBuilder = source[buildStart..buildEnd];

        draftLineBuilder.Should().Contain("location.InventoryWarehouseId != adjustment.WarehouseId");
        draftLineBuilder.Should().NotContain("AvailableStock < Math.Abs",
            "negative-stock authority is bound to the saved adjustment and exact posting line");
        source.Should().Contain("PrepareDecreaseAsync(new InventoryStockDecreaseRequest");
        source.Should().Contain("NegativeStockOverrideId = negativeStockOverrideIds.GetValueOrDefault(item.Id)");
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
