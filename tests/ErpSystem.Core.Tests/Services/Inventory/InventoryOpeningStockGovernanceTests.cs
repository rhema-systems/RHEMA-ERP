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
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryOpeningStockGovernanceTests
{
    [Fact]
    public async Task Disposal_participant_denies_forged_authority_before_any_stock_mutation()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserProvider>(MockBehavior.Strict);
        currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
        currentUser.SetupGet(value => value.UserId).Returns(makerId);
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        currentUser.SetupGet(value => value.Username).Returns("inventory.disposal.maker@tenant.test");
        currentUser.SetupGet(value => value.FullName).Returns("Inventory Disposal Maker");
        var service = CreateService(currentUser.Object);
        var valid = DisposalRequest(tenantId, makerId);

        foreach (var forged in new[]
                 {
                     valid with { TenantId = Guid.NewGuid() },
                     valid with { DisposalCaseId = Guid.Empty },
                     valid with { FinanceApprovalId = Guid.Empty },
                     valid with { PreparedOwnerEffectFingerprint = string.Empty },
                     valid with { RequestedById = Guid.Empty },
                     valid with { AdjustmentId = Guid.NewGuid() },
                     valid with { ItemIds = [Guid.Empty] },
                     PositiveDisposalRequest(valid)
                 })
        {
            await FluentActions.Awaiting(() => service.PreviewDisposalAsync(forged)).Should()
                .ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task Ordinary_adjustment_entry_point_rejects_initial_stock()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        await using var context = Context();
        var service = CreateService(context, currentUser.Object, AllowAllAccess().Object);
        var request = new CreateStockAdjustmentDto
        {
            WarehouseId = Guid.NewGuid(),
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Description = "Opening schedule",
            Reference = "SOURCE-001",
            IdempotencyKey = "ordinary-path-must-reject",
            Items =
            [
                new CreateStockAdjustmentItemDto
                {
                    InventoryItemId = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    AdjustmentQuantity = 1m,
                    UnitCost = 1m
                }
            ]
        };

        var action = () => service.CreateAsync(request, userId);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dedicated opening-stock endpoint*");
    }

    [Fact]
    public async Task Ordinary_draft_update_cannot_convert_to_initial_stock_or_create_side_effects()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouse = Warehouse(tenantId, "ORDINARY-WH");
        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdjustmentNumber = "ADJ-ORDINARY-001",
            AdjustmentDate = DateTime.UtcNow.Date,
            BookClassification = "IFRS",
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            ReasonCode = StockAdjustmentReasonCodes.Found,
            Description = "Ordinary adjustment",
            Reference = "ORDINARY-001",
            Status = "Draft",
            RequestedById = userId,
            IdempotencyKey = "ordinary-001",
            PayloadHash = new string('E', 64),
            IntegrityHash = new string('F', 64)
        };
        context.AddRange(warehouse, adjustment);
        await context.SaveChangesAsync();
        var access = new Mock<IProcurementAccessControlService>(MockBehavior.Strict);
        var service = CreateService(context, CurrentUser(tenantId, userId).Object, access.Object);

        var action = () => service.UpdateAsync(adjustment.Id, new UpdateStockAdjustmentDto
        {
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Description = "Crafted opening conversion",
            Reference = "CRAFTED-OPENING",
            AdjustmentDate = new DateTime(2025, 1, 1),
            RowVersion = Convert.ToBase64String(adjustment.RowVersion)
        }, userId);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dedicated opening-stock endpoint*");
        context.ChangeTracker.HasChanges().Should().BeFalse();
        access.VerifyNoOtherCalls();
        context.ChangeTracker.Clear();
        var persisted = await context.StockAdjustments.IgnoreQueryFilters().SingleAsync();
        persisted.ReasonCode.Should().Be(StockAdjustmentReasonCodes.Found);
        persisted.Description.Should().Be("Ordinary adjustment");
        persisted.Reference.Should().Be("ORDINARY-001");
        (await context.StockAdjustmentActions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.AuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Opening_schedule_identity_is_deterministic_and_tenant_scoped()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var date = new DateTime(2025, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        var first = StockAdjustmentService.CreateOpeningStockIdempotencyKey(
            tenantId, warehouseId, date, "ifrs", " schedule-001 ");
        var replay = StockAdjustmentService.CreateOpeningStockIdempotencyKey(
            tenantId, warehouseId, date, "IFRS", "SCHEDULE-001");
        var otherTenant = StockAdjustmentService.CreateOpeningStockIdempotencyKey(
            Guid.NewGuid(), warehouseId, date, "IFRS", "SCHEDULE-001");

        first.Should().Be(replay);
        first.Should().NotBe(otherTenant);
        first.Should().StartWith("OPENING-STOCK:");
        first.Length.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void Opening_payload_hash_binds_explicit_unit_cost_and_book()
    {
        var request = new CreateStockAdjustmentDto
        {
            WarehouseId = Guid.NewGuid(),
            AdjustmentDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Description = "Opening inventory schedule",
            Reference = "SCHEDULE-001",
            IdempotencyKey = "unused-in-payload",
            Items =
            [
                new CreateStockAdjustmentItemDto
                {
                    InventoryItemId = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    AdjustmentQuantity = 10m,
                    UnitCost = 5m
                }
            ]
        };
        var first = StockAdjustmentService.CreateOpeningStockPayloadHash(
            request, request.Description!, request.Reference!, "IFRS");

        request.Items[0].UnitCost = 6m;
        var changedCost = StockAdjustmentService.CreateOpeningStockPayloadHash(
            request, request.Description!, request.Reference!, "IFRS");
        request.Items[0].UnitCost = 5m;
        var changedBook = StockAdjustmentService.CreateOpeningStockPayloadHash(
            request, request.Description!, request.Reference!, "LOCAL_GAAP");

        changedCost.Should().NotBe(first);
        changedBook.Should().NotBe(first);
    }

    [Fact]
    public async Task Dedicated_create_preserves_book_unit_cost_is_idempotent_and_retains_immutable_evidence()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouse = Warehouse(tenantId, "FINDEMO-WH");
        var location = Location(tenantId, warehouse, "OPEN-01");
        var item = Item(tenantId, "FINDEMO-ITEM-001");
        context.AddRange(warehouse, location, item);
        await context.SaveChangesAsync();
        var service = CreateService(context, CurrentUser(tenantId, userId).Object, AllowAllAccess().Object);
        var request = OpeningRequest(warehouse.Id, location.Id, item.Id);

        var created = await service.CreateOpeningStockAsync(request, userId);
        request.SourceScheduleReference = request.SourceScheduleReference.ToLowerInvariant();
        var replay = await service.CreateOpeningStockAsync(request, userId);

        created.Id.Should().Be(replay.Id);
        created.Status.Should().Be("Draft");
        created.ReasonCode.Should().Be(StockAdjustmentReasonCodes.InitialStock);
        created.Reference.Should().Be("FINDEMO-INV-SCHEDULE-001");
        created.BookClassification.Should().Be("LOCAL_GAAP");
        created.Items.Should().ContainSingle().Which.UnitCost.Should().Be(123.4567m);
        var persisted = await context.StockAdjustments.IgnoreQueryFilters()
            .Include(x => x.Items).SingleAsync();
        persisted.BookClassification.Should().Be("LOCAL_GAAP");
        persisted.Items.Should().ContainSingle().Which.UnitCost.Should().Be(123.4567m);
        persisted.IdempotencyKey.Should().StartWith("OPENING-STOCK:");
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(1);

        var update = () => service.UpdateAsync(created.Id, new UpdateStockAdjustmentDto
        {
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Description = "Attempted mutation",
            Reference = request.SourceScheduleReference,
            RowVersion = created.RowVersion,
            Items =
            [
                new CreateStockAdjustmentItemDto
                {
                    InventoryItemId = item.Id,
                    LocationId = location.Id,
                    AdjustmentQuantity = 2m,
                    UnitCost = 1m
                }
            ]
        }, userId);
        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable*");

        request.Items[0].UnitCost = 124m;
        var conflictingReplay = () => service.CreateOpeningStockAsync(request, userId);
        await conflictingReplay.Should().ThrowAsync<StockAdjustmentIdempotencyConflictException>();
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Dedicated_create_recovers_existing_logical_source_with_distinct_key_and_checks_payload()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouse = Warehouse(tenantId, "SOURCE-GUARD-WH");
        var location = Location(tenantId, warehouse, "SOURCE-GUARD-LOC");
        var item = Item(tenantId, "SOURCE-GUARD-ITEM");
        context.AddRange(warehouse, location, item);
        await context.SaveChangesAsync();
        var request = OpeningRequest(warehouse.Id, location.Id, item.Id);
        var service = CreateService(context, CurrentUser(tenantId, userId).Object, AllowAllAccess().Object);
        var created = await service.CreateOpeningStockAsync(request, userId);

        var persisted = await context.StockAdjustments.IgnoreQueryFilters().SingleAsync();
        persisted.IdempotencyKey = "LEGACY-DISTINCT-OPENING-KEY";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        service = CreateService(context, CurrentUser(tenantId, userId).Object, AllowAllAccess().Object);

        var recovered = await service.CreateOpeningStockAsync(request, userId);

        recovered.Id.Should().Be(created.Id);
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(1);

        request.Items[0].UnitCost += 1m;
        var conflictingSourceReplay = () => service.CreateOpeningStockAsync(request, userId);
        await conflictingSourceReplay.Should().ThrowAsync<StockAdjustmentIdempotencyConflictException>();
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("cross-tenant-item")]
    [InlineData("inactive-item")]
    [InlineData("inactive-location")]
    [InlineData("missing-location")]
    public async Task Dedicated_create_rejects_invalid_canonical_inventory_evidence_before_persistence(string scenario)
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouse = Warehouse(tenantId, "OWNED-WH");
        var location = Location(tenantId, warehouse, "OWNED-LOC");
        var item = Item(tenantId, "OWNED-ITEM");
        if (scenario == "cross-tenant-item") item.TenantId = Guid.NewGuid();
        if (scenario == "inactive-item") item.Status = ItemStatus.Inactive;
        if (scenario == "inactive-location") location.IsActive = false;
        context.AddRange(warehouse, location, item);
        await context.SaveChangesAsync();
        var service = CreateService(context, CurrentUser(tenantId, userId).Object, AllowAllAccess().Object);
        var request = OpeningRequest(warehouse.Id, location.Id, item.Id);
        if (scenario == "missing-location") request.Items[0].LocationId = Guid.Empty;

        var action = () => service.CreateOpeningStockAsync(request, userId);

        await action.Should().ThrowAsync<Exception>();
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Opening_options_are_tenant_scoped_and_return_only_active_owned_accessible_evidence()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var owned = Warehouse(tenantId, "OWNED");
        var ownedLocation = Location(tenantId, owned, "OWNED-01");
        var otherTenant = Guid.NewGuid();
        var foreign = Warehouse(otherTenant, "FOREIGN");
        var foreignLocation = Location(otherTenant, foreign, "FOREIGN-01");
        var activeItem = Item(tenantId, "ACTIVE-ITEM");
        var inactiveItem = Item(tenantId, "INACTIVE-ITEM");
        inactiveItem.Status = ItemStatus.Inactive;
        var foreignItem = Item(otherTenant, "FOREIGN-ITEM");
        context.AddRange(owned, ownedLocation, foreign, foreignLocation, activeItem, inactiveItem, foreignItem);
        await context.SaveChangesAsync();
        var service = CreateService(context, CurrentUser(tenantId, userId).Object, AllowAllAccess().Object);

        var result = await service.GetOpeningStockOptionsAsync(userId);

        result.IsReady.Should().BeTrue();
        result.Blockers.Should().BeEmpty();
        result.Warehouses.Should().ContainSingle().Which.Id.Should().Be(owned.Id);
        result.Warehouses[0].Locations.Should().ContainSingle().Which.Id.Should().Be(ownedLocation.Id);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(activeItem.Id);
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Opening_options_fail_closed_with_clear_location_and_item_blockers()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        context.Add(Warehouse(tenantId, "WAREHOUSE-WITHOUT-LOCATION"));
        await context.SaveChangesAsync();
        var service = CreateService(context, CurrentUser(tenantId, userId).Object, AllowAllAccess().Object);

        var result = await service.GetOpeningStockOptionsAsync(userId);

        result.IsReady.Should().BeFalse();
        result.Warehouses.Should().BeEmpty();
        result.Items.Should().BeEmpty();
        result.Blockers.Should().Contain(message => message.Contains("warehouse location", StringComparison.OrdinalIgnoreCase));
        result.Blockers.Should().Contain(message => message.Contains("stock item", StringComparison.OrdinalIgnoreCase));
        (await context.StockAdjustments.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Opening_stock_migration_keeps_legacy_controls_and_adds_only_governed_trigger_exceptions()
    {
        var operations = OpeningStockMigrationOperations("Up");
        operations.Should().HaveCount(3);
        var column = operations[0].Should().BeOfType<AddColumnOperation>().Which;
        column.Table.Should().Be("StockAdjustments");
        column.Name.Should().Be("BookClassification");

        var sql = operations.OfType<SqlOperation>().Select(operation => operation.Sql).ToList();
        var lifecycle = sql.Single(value => value.Contains(
            "TR_StockAdjustments_ControlledLifecycle", StringComparison.Ordinal));
        var lines = sql.Single(value => value.Contains(
            "TR_StockAdjustmentItems_ControlledMutation", StringComparison.Ordinal));

        lifecycle.Should().Contain(
            "d.ReasonCode <> N'INITIAL_STOCK' AND i.ReasonCode = N'INITIAL_STOCK'");
        lifecycle.Should().Contain("INV_OPENING_STOCK_INSERT_INVALID");
        lifecycle.Should().Contain("ISNULL(i.IdempotencyKey, N'') NOT LIKE N'OPENING-STOCK:%'");
        lifecycle.Should().Contain("ISNULL(i.CorrelationId, N'') NOT LIKE N'opening-stock:%'");
        lifecycle.Should().Contain("UPPER(LTRIM(RTRIM(i.BookClassification))) = N'ALL_ACTIVE_BOOKS'");
        lifecycle.Should().Contain(
            "WHERE d.ReasonCode = N'INITIAL_STOCK' AND");
        lifecycle.Should().Contain("INV_OPENING_STOCK_SOURCE_IMMUTABLE");
        var openingSourceStart = lifecycle.IndexOf(
            "WHERE d.ReasonCode = N'INITIAL_STOCK' AND", StringComparison.Ordinal);
        var openingSourceEnd = lifecycle.IndexOf("THROW 51694", openingSourceStart, StringComparison.Ordinal);
        var openingSourceGuard = lifecycle[openingSourceStart..openingSourceEnd];
        openingSourceGuard.Should().ContainAll(
            "i.WarehouseId <> d.WarehouseId",
            "i.Reference <> d.Reference",
            "i.AdjustmentDate <> d.AdjustmentDate",
            "i.ReasonCode <> d.ReasonCode",
            "i.BookClassification",
            "i.Description",
            "i.TotalAdjustmentValue",
            "i.RequestedById",
            "i.IdempotencyKey",
            "i.PayloadHash",
            "i.CorrelationId");
        lifecycle.Should().Contain(
            "OR ISNULL(i.BookClassification, N'') <> ISNULL(d.BookClassification, N'')");
        lifecycle.Should().Contain(
            "i.ReasonCode = N'INITIAL_STOCK' AND f.PostingAction = N'PostOpeningStock'");
        lifecycle.Should().Contain(
            "i.ReasonCode <> N'INITIAL_STOCK' AND f.PostingAction = N'PostStockAdjustment'");
        lifecycle.Should().Contain("f.PostingAction = N'ReverseStockAdjustment'");
        lifecycle.Should().Contain("c.Status IN (N'PendingFinanceApproval', N'PendingAuditAttestation')");

        lines.Should().Contain("sourceAdjustment.ReasonCode = N'INITIAL_STOCK'");
        lines.Should().Contain("targetAdjustment.ReasonCode = N'INITIAL_STOCK'");
        lines.Should().Contain("INV_OPENING_STOCK_LINE_IMMUTABLE");
        lines.Should().Contain("i.UnitCost <= 0");
        lines.Should().Contain("a.ReasonCode = N'INITIAL_STOCK' AND i.AdjustmentQuantity <= 0");
        lines.Should().Contain("i.SystemQuantity <> 0 OR i.PhysicalQuantity <> i.AdjustmentQuantity");
        lines.Should().Contain("w.IsConsignmentWarehouse = 1 OR l.IsConsignmentBin = 1");
        lines.Should().Contain(
            "LEFT JOIN Warehouses w ON w.Id = a.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1");
        lines.Should().Contain("a.ReasonCode <> N'INITIAL_STOCK'");
        lines.Should().Contain("i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost");
        lines.Should().Contain("i.AdjustmentValue <> ROUND(i.AdjustmentQuantity * i.UnitCost, 2)");
        lines.Should().Contain("item.TenantId = i.TenantId AND item.IsDeleted = 0");
        lines.Should().Contain("l.TenantId = i.TenantId AND l.IsDeleted = 0 AND l.IsActive = 1");
        lines.Should().Contain("l.WarehouseId <> a.WarehouseId");
    }

    [Fact]
    public void Opening_stock_migration_down_restores_legacy_triggers_before_dropping_book()
    {
        var operations = OpeningStockMigrationOperations("Down");
        operations.Should().HaveCount(3);
        operations[0].Should().BeOfType<SqlOperation>().Which.Sql
            .Should().Contain("TR_StockAdjustments_ControlledLifecycle");
        operations[1].Should().BeOfType<SqlOperation>().Which.Sql
            .Should().Contain("TR_StockAdjustmentItems_ControlledMutation");
        var drop = operations[2].Should().BeOfType<DropColumnOperation>().Which;
        drop.Table.Should().Be("StockAdjustments");
        drop.Name.Should().Be("BookClassification");

        var sql = operations.OfType<SqlOperation>().Select(operation => operation.Sql).ToList();
        var lifecycle = sql.Single(value => value.Contains(
            "TR_StockAdjustments_ControlledLifecycle", StringComparison.Ordinal));
        var lines = sql.Single(value => value.Contains(
            "TR_StockAdjustmentItems_ControlledMutation", StringComparison.Ordinal));
        lifecycle.Should().NotContain("BookClassification");
        lifecycle.Should().NotContain("PostOpeningStock");
        lifecycle.Should().NotContain("INV_OPENING_STOCK_CONVERSION_BLOCKED");
        lifecycle.Should().NotContain("INV_OPENING_STOCK_INSERT_INVALID");
        lifecycle.Should().NotContain("INV_OPENING_STOCK_SOURCE_IMMUTABLE");
        lifecycle.Should().Contain("f.PostingAction = N'PostStockAdjustment'");
        lifecycle.Should().Contain("c.Status IN (N'PendingFinanceApproval', N'PendingAuditAttestation')");
        lines.Should().NotContain("INV_OPENING_STOCK_LINE_IMMUTABLE");
        lines.Should().NotContain("a.ReasonCode <> N'INITIAL_STOCK'");
        lines.Should().NotContain("IsConsignmentWarehouse");
        lines.Should().NotContain("IsConsignmentBin");
        lines.Should().Contain("OR i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost");
    }

    private static StockAdjustmentService CreateService(ICurrentUserProvider currentUser) => new(
        Mock.Of<IStockAdjustmentRepository>(),
        Mock.Of<IInventoryItemRepository>(),
        Mock.Of<IStockMovementRepository>(),
        Mock.Of<IWarehouseQuantityRepository>(),
        Mock.Of<IWarehouseLocationRepository>(),
        Mock.Of<IWarehouseRepository>(),
        Mock.Of<IConsignmentSettlementService>(),
        currentUser,
        Mock.Of<IUnitOfWork>(),
        Mock.Of<IInventoryTrackingControlService>(),
        Mock.Of<IInventoryNegativeStockControlService>(),
        Mock.Of<IProcurementAccessControlService>(),
        Mock.Of<IProcurementSodGuardService>(),
        Mock.Of<IWorkflowIntegrationService>(),
        Mock.Of<IProcurementControlEventService>(),
        Mock.Of<IInventoryAdjustmentFinancePostingService>(),
        Mock.Of<IInventoryValuationService>(),
        NullLogger<StockAdjustmentService>.Instance);

    private static StockAdjustmentService CreateService(
        ApplicationDbContext context,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl)
    {
        var unitOfWork = new UnitOfWork(context);
        return new StockAdjustmentService(
            new StockAdjustmentRepository(context),
            new InventoryItemRepository(context),
            new StockMovementRepository(context),
            new WarehouseQuantityRepository(context),
            new WarehouseLocationRepository(context),
            new WarehouseRepository(context),
            Mock.Of<IConsignmentSettlementService>(),
            currentUser,
            unitOfWork,
            Mock.Of<IInventoryTrackingControlService>(),
            Mock.Of<IInventoryNegativeStockControlService>(),
            accessControl,
            Mock.Of<IProcurementSodGuardService>(),
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IProcurementControlEventService>(),
            Mock.Of<IInventoryAdjustmentFinancePostingService>(),
            Mock.Of<IInventoryValuationService>(),
            NullLogger<StockAdjustmentService>.Instance);
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static IReadOnlyList<MigrationOperation> OpeningStockMigrationOperations(string method)
    {
        var migration = new AddInventoryOpeningStockBook();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static Mock<ICurrentUserProvider> CurrentUser(Guid tenantId, Guid userId)
    {
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(x => x.IsAuthenticated).Returns(true);
        current.SetupGet(x => x.UserId).Returns(userId);
        current.SetupGet(x => x.TenantId).Returns(tenantId);
        current.SetupGet(x => x.Username).Returns("inventory.maker@tenant.test");
        return current;
    }

    private static Mock<IProcurementAccessControlService> AllowAllAccess()
    {
        var access = new Mock<IProcurementAccessControlService>();
        static ProcurementAccessCapabilityDecisionDto Allowed(
            ProcurementAccessCapabilityRequest request,
            string correlationId) => new()
        {
            Allowed = true,
            PermissionCode = request.PermissionCode,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            CorrelationId = correlationId
        };
        access.Setup(x => x.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) =>
                Allowed(request, correlationId));
        access.Setup(x => x.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) =>
                Allowed(request, correlationId));
        return access;
    }

    private static Warehouse Warehouse(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Code = code,
        Name = code,
        IsActive = true
    };

    private static WarehouseLocation Location(Guid tenantId, Warehouse warehouse, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        WarehouseId = warehouse.Id,
        Warehouse = warehouse,
        LocationCode = code,
        Name = code,
        IsActive = true
    };

    private static InventoryItem Item(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ItemCode = code,
        Name = code,
        UnitOfMeasure = "EA",
        ItemType = ItemType.StockItem,
        Status = ItemStatus.Active
    };

    private static CreateOpeningStockAdjustmentDto OpeningRequest(
        Guid warehouseId,
        Guid locationId,
        Guid itemId) => new()
    {
        WarehouseId = warehouseId,
        OpeningDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        BookClassification = "local_gaap",
        SourceScheduleReference = "FINDEMO-INV-SCHEDULE-001",
        Description = "Approved inventory migration schedule",
        Items =
        [
            new CreateOpeningStockItemDto
            {
                InventoryItemId = itemId,
                LocationId = locationId,
                Quantity = 10m,
                UnitCost = 123.4567m,
                Notes = "Opening evidence"
            }
        ]
    };

    private static InventoryDisposalStockAdjustmentRequest DisposalRequest(Guid tenantId, Guid makerId)
    {
        var disposalId = Guid.NewGuid();
        var adjustmentId = DeterministicGuid($"RHEMA:INV_DISPOSAL:ADJUSTMENT:V1:{tenantId:N}:{disposalId:N}");
        return new InventoryDisposalStockAdjustmentRequest
        {
            TenantId = tenantId, DisposalCaseId = disposalId, FinanceApprovalId = Guid.NewGuid(),
            PreparedOwnerEffectFingerprint = new string('A', 64), AdjustmentId = adjustmentId,
            ItemIds = [Guid.NewGuid()], AdjustmentNumber = $"IDP-SA-{disposalId:N}"[..23].ToUpperInvariant(),
            PostingDateUtc = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc), RequestedById = makerId,
            Create = new CreateStockAdjustmentDto
            {
                WarehouseId = Guid.NewGuid(), ReasonCode = StockAdjustmentReasonCodes.WriteOff,
                Description = "Disposal authority validation test", Reference = "DISPOSAL-TEST",
                IdempotencyKey = $"disposal:{disposalId:N}:adjustment",
                Items = [new CreateStockAdjustmentItemDto
                {
                    InventoryItemId = Guid.NewGuid(), LocationId = Guid.NewGuid(), AdjustmentQuantity = -1m, UnitCost = 1m
                }]
            }
        };
    }

    private static InventoryDisposalStockAdjustmentRequest PositiveDisposalRequest(
        InventoryDisposalStockAdjustmentRequest request) => request with
    {
        Create = new CreateStockAdjustmentDto
        {
            WarehouseId = request.Create.WarehouseId, ReasonCode = request.Create.ReasonCode,
            Description = request.Create.Description, Reference = request.Create.Reference,
            IdempotencyKey = request.Create.IdempotencyKey,
            Items = request.Create.Items.Select(item => new CreateStockAdjustmentItemDto
            {
                InventoryItemId = item.InventoryItemId, LocationId = item.LocationId,
                AdjustmentQuantity = 1m, UnitCost = item.UnitCost
            }).ToList()
        }
    };

    private static Guid DeterministicGuid(string canonical) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)).AsSpan(0, 16));
}
