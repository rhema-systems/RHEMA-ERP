using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
            index.IsUnique && index.GetFilter() == null && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    "InventoryReturnVoucherId", "InventoryRequisitionItemId", "LocationId",
                    "LotNumber", "BatchNumber", "SerialNumber"
                }));
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
    public void Recount_retirement_migration_allows_only_linked_approved_adjustments_in_review_stages()
    {
        var migration = new Phase6ReviewCycleCountAdjustmentRetirement();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { up });
        var upSql = string.Join(Environment.NewLine,
            up.Operations.OfType<SqlOperation>().Select(value => value.Sql));

        upSql.Should().Contain("d.Status = N'Approved' AND i.Status = N'Cancelled'");
        upSql.Should().Contain("FROM PhysicalCounts c");
        upSql.Should().Contain("c.StockAdjustmentId = i.Id");
        upSql.Should().Contain("N'PendingFinanceApproval', N'PendingAuditAttestation'");

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { down });
        var downSql = string.Join(Environment.NewLine,
            down.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        downSql.Should().NotContain("d.Status = N'Approved' AND i.Status = N'Cancelled'");
    }

    [Fact]
    public void Store_return_voucher_uses_shared_document_output_type()
    {
        DocumentTypes.InventoryStoreReturnVoucher.Should().Be("Inventory.StoreReturnVoucher");
    }

    [Fact]
    public async Task Governed_adjustment_updates_authoritative_balance_and_immutable_movement()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var category = new InventoryCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "ADJ", Name = "Adjustments"
        };
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = category.Id, Category = category,
            ItemCode = "ADJ-001", Name = "Adjustment item", UnitOfMeasure = "EA",
            ValuationMethod = ValuationMethod.WeightedAverage, AverageCost = 10m
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "ADJ-WH", Name = "Adjustment warehouse", IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id, Warehouse = warehouse,
            LocationCode = "ADJ-01", Name = "Adjustment bin", IsActive = true
        };
        _context.AddRange(category, item, warehouse, location);
        await _context.SaveChangesAsync();
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.UserId).Returns(userId);
        var unitOfWork = new UnitOfWork(_context);
        var service = new InventoryValuationService(unitOfWork,
            NullLogger<InventoryValuationService>.Instance, current.Object,
            Mock.Of<IProcurementReceiptSourceControlService>());
        var referenceId = Guid.NewGuid();

        await service.ProcessAdjustmentAsync(item.Id, warehouse.Id, location.Id,
            5m, 10m, 0m, false, "ADJ-TEST", referenceId);
        await unitOfWork.SaveChangesAsync();
        await service.ProcessAdjustmentAsync(item.Id, warehouse.Id, location.Id,
            -2m, 10m, 5m, false, "ADJ-TEST", referenceId);
        await unitOfWork.SaveChangesAsync();

        var balance = await _context.InventoryBalances.SingleAsync();
        balance.QuantityOnHand.Should().Be(3m);
        balance.QuantityAvailable.Should().Be(3m);
        balance.TotalValue.Should().Be(30m);
        var movements = await _context.InventoryMovements.OrderBy(value => value.MovementDate).ToListAsync();
        movements.Should().HaveCount(2);
        movements.Select(value => value.MovementType).Should().Equal(
            InventoryMovementType.AdjustmentIn, InventoryMovementType.AdjustmentOut);
        movements.Sum(value => value.Direction == MovementDirection.In ? value.TotalValue : -value.TotalValue)
            .Should().Be(30m);
    }

    [Fact]
    public async Task Fifo_positive_adjustment_reversal_consumes_only_its_original_cost_layer()
    {
        var tenantId = Guid.NewGuid();
        var category = new InventoryCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "FIFO-ADJ", Name = "FIFO adjustments"
        };
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = category.Id, Category = category,
            ItemCode = "FIFO-ADJ-001", Name = "FIFO adjustment item", UnitOfMeasure = "EA",
            ValuationMethod = ValuationMethod.FIFO
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "FIFO-WH", Name = "FIFO warehouse", IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id, Warehouse = warehouse,
            LocationCode = "FIFO-01", Name = "FIFO bin", IsActive = true
        };
        _context.AddRange(category, item, warehouse, location);
        await _context.SaveChangesAsync();
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
        var unitOfWork = new UnitOfWork(_context);
        var service = new InventoryValuationService(unitOfWork,
            NullLogger<InventoryValuationService>.Instance, current.Object,
            Mock.Of<IProcurementReceiptSourceControlService>());
        var olderSourceId = Guid.NewGuid();
        var adjustmentSourceId = Guid.NewGuid();

        await service.ProcessAdjustmentAsync(item.Id, warehouse.Id, location.Id,
            1m, 10m, 0m, false, "OLDER-STOCK", olderSourceId);
        await unitOfWork.SaveChangesAsync();
        await service.ProcessAdjustmentAsync(item.Id, warehouse.Id, location.Id,
            1m, 15m, 1m, false, "ADJ-FIFO", adjustmentSourceId);
        await unitOfWork.SaveChangesAsync();
        var reversedValue = await service.ProcessAdjustmentAsync(item.Id, warehouse.Id, location.Id,
            -1m, 15m, 2m, false, "ADJ-FIFO", adjustmentSourceId,
            reversalSourceId: adjustmentSourceId);
        await unitOfWork.SaveChangesAsync();

        reversedValue.Should().Be(15m);
        var layers = await _context.InventoryLayers.OrderBy(value => value.LayerDate).ToListAsync();
        layers.Single(value => value.SourceId == olderSourceId).RemainingQuantity.Should().Be(1m);
        layers.Single(value => value.SourceId == adjustmentSourceId).RemainingQuantity.Should().Be(0m);
        var balance = await _context.InventoryBalances.SingleAsync();
        balance.QuantityOnHand.Should().Be(1m);
        balance.TotalValue.Should().Be(10m);
        var reversalMovement = await _context.InventoryMovements
            .OrderByDescending(value => value.MovementDate).FirstAsync();
        reversalMovement.TotalValue.Should().Be(15m);
        reversalMovement.CostLayerId.Should().Be(layers.Single(value => value.SourceId == adjustmentSourceId).Id);
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
                    LotNumber = " LOT-01 ",
                    BatchNumber = " BATCH-01 ",
                    ManufactureDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ExpiryDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
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
        request.Items[0].BatchNumber = "BATCH-02";
        var changedTracking = (string)method.Invoke(null, new object[]
        {
            request, StockAdjustmentReasonCodes.Damage, "Damaged stock", "INSPECTION-1"
        })!;
        changedTracking.Should().NotBe(changedEvidence);
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
        draftLineBuilder.Should().Contain(
            "adjustment.ReasonCode is not (StockAdjustmentReasonCodes.CycleCount or StockAdjustmentReasonCodes.PhysicalCount)",
            "controlled counts must preserve one governed adjustment when line variances have opposite signs");
        draftLineBuilder.Should().NotContain("AvailableStock < Math.Abs",
            "negative-stock authority is bound to the saved adjustment and exact posting line");
        source.Should().Contain("PrepareDecreaseAsync(new InventoryStockDecreaseRequest");
        source.Should().Contain("NegativeStockOverrideId = negativeStockOverrideIds.GetValueOrDefault(item.Id)");
    }

    [Fact]
    public void Return_voucher_lists_page_until_the_authorized_limit_is_filled()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryReturnControlService.cs"));
        var start = source.IndexOf("private async Task<IReadOnlyList<InventoryReturnVoucherDto>> GetListAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private IQueryable<InventoryReturnVoucher> Query", start, StringComparison.Ordinal);
        var list = source[start..end];

        list.Should().Contain("while (allowed.Count < take)");
        list.Should().Contain(".Skip(offset).Take(pageSize)");
        list.Should().Contain("offset += candidates.Count");
    }

    [Fact]
    public void Controlled_returns_validate_bins_by_effective_inventory_warehouse()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryReturnControlService.cs"));
        var start = source.IndexOf("private async Task ValidateLocationAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task<List<(CentralDocumentVersion", start, StringComparison.Ordinal);
        var validation = source[start..end];

        validation.Should().Contain("location.InventoryWarehouseId != warehouseId");
        validation.Should().NotContain("location.WarehouseId != warehouseId");
    }

    [Fact]
    public void Return_and_adjustment_evidence_require_a_clean_central_dms_upload()
    {
        var root = FindRepositoryRoot();
        var returnSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryReturnControlService.cs"));
        var adjustmentSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Inventory",
            "StockAdjustmentService.cs"));

        returnSource.Should().Contain("x.VirusScanStatus == FileVirusScanStatus.Clean");
        returnSource.Should().Contain("INV_RETURN_EVIDENCE_NOT_CLEAN");
        adjustmentSource.Should().Contain("x.VirusScanStatus == FileVirusScanStatus.Clean");
        adjustmentSource.Should().Contain("successful clean malware scan");
    }

    [Fact]
    public void Controlled_returns_preserve_distinct_serial_lines_and_validate_their_aggregate_quantity()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryReturnControlService.cs"));
        var start = source.IndexOf("public async Task<InventoryReturnVoucherDto> RequestAsync", StringComparison.Ordinal);
        var end = source.IndexOf("public async Task<InventoryReturnVoucherDto> DecideAsync", start, StringComparison.Ordinal);
        var request = source[start..end];

        request.Should().Contain("INV_RETURN_DUPLICATE_TRACKING_LINE");
        request.Should().Contain("request.Items.GroupBy(value => value.ItemId)");
        request.Should().Contain("group.Sum(value => value.ReturnedQuantity)");
        request.Should().NotContain("INV_RETURN_DUPLICATE_LINE");
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
