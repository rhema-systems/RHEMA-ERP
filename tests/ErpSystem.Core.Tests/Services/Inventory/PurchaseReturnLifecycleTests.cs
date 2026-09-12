using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class PurchaseReturnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_submission_captures_policy_without_fabricating_an_approver(bool required)
    {
        var f = new Fixture();
        f.Workflow.Setup(x => x.SubmitAsync("PurchaseReturn", f.Return.Id)).ReturnsAsync(Result(required));
        await f.Service.SubmitForApprovalAsync(f.Return.Id, f.User);
        f.Return.ApprovalRequired.Should().Be(required);
        f.Return.Status.Should().Be(required ? "Submitted" : "ReadyToDispatch");
        f.Return.ApprovedById.Should().BeNull();
        f.Return.ApprovedDate.Should().BeNull();
        f.Movements.Should().BeEmpty();
    }

    [Fact]
    public async Task Inactive_workflow_allows_the_requester_to_dispatch_exact_partial_quantity_at_current_value()
    {
        var f = new Fixture();
        f.Workflow.Setup(x => x.SubmitAsync("PurchaseReturn", f.Return.Id)).ReturnsAsync(Result(false));
        await f.Service.SubmitForApprovalAsync(f.Return.Id, f.User);
        await f.Service.ShipAsync(f.Return.Id, f.User);
        f.Return.Status.Should().Be("Shipped");
        f.Return.ApprovedById.Should().BeNull();
        f.WarehouseStock.CurrentStock.Should().Be(9);
        f.BinStock.Quantity.Should().Be(9);
        f.BinStock.AvailableQuantity.Should().Be(9);
        f.Item.CurrentStock.Should().Be(9);
        f.Movements.Should().ContainSingle().Which.TotalValue.Should().Be(-123.45m);
        f.Movements.Single().LocationId.Should().Be(f.Bin.Id);
        f.Return.Items.Single().UnitCost.Should().Be(100, "the GRN estimate must not be overwritten by dispatch valuation");
        f.Valuation.Verify(x => x.ProcessIssueAsync(f.Item.Id, f.Return.WarehouseId, f.Bin.Id, 1,
            InventoryMovementType.SupplierReturn, ReferenceType.Return, f.Return.ReturnNumber, f.Return.Id, null, null), Times.Once);
        f.Tracking.Verify(x => x.StageEventAsync(It.Is<InventoryTrackingMutationRequest>(x =>
            x.LocationId == f.Bin.Id && x.Direction == InventoryTrackingDirection.Issue && x.Quantity == 1), It.IsAny<CancellationToken>()), Times.Once);
        Func<Task> retry = () => f.Service.ShipAsync(f.Return.Id, f.User);
        await retry.Should().ThrowAsync<InventorySupplierReturnException>();
        f.Movements.Should().HaveCount(1);
    }

    [Fact]
    public async Task Intermediate_approval_is_successful_without_final_approval_metadata()
    {
        var f = new Fixture();
        f.Return.Status = "Submitted";
        f.Return.RequestedById = Guid.NewGuid();
        f.Workflow.Setup(x => x.CanUserApproveAsync("PurchaseReturn", f.Return.Id, f.User)).ReturnsAsync(true);
        f.Workflow.Setup(x => x.ProcessApprovalAsync("PurchaseReturn", f.Return.Id, f.User, "Approve", null)).ReturnsAsync(Result(true));
        await f.Service.ApproveAsync(f.Return.Id, f.User);
        f.Return.Status.Should().Be("Submitted");
        f.Return.ApprovedById.Should().BeNull();
        f.Movements.Should().BeEmpty();
    }

    [Theory]
    [InlineData("missing-bin")]
    [InlineData("wrong-tenant-bin")]
    [InlineData("allocated-bin")]
    [InlineData("over-grn")]
    [InlineData("active-instance")]
    [InlineData("scope-denied")]
    [InlineData("approver-dispatch")]
    public async Task Real_dispatch_keeps_source_stock_location_and_actor_guards(string failure)
    {
        var f = new Fixture();
        f.Return.ApprovalRequired = false; f.Return.Status = "ReadyToDispatch";
        switch (failure)
        {
            case "missing-bin": f.Source.StorageLocationId = null; f.Return.Items.Single().LocationId = null; break;
            case "wrong-tenant-bin": f.Bin.TenantId = Guid.NewGuid(); break;
            case "allocated-bin": f.BinStock.AllocatedQuantity = 10; break;
            case "over-grn": f.Source.AcceptedQuantity = 0.5m; break;
            case "active-instance": f.Workflow.Setup(x => x.HasActiveApprovalInstanceAsync("PurchaseReturn", f.Return.Id)).ReturnsAsync(true); break;
            case "scope-denied": f.Access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false, Message = "Denied" }); break;
            case "approver-dispatch": f.Return.ApprovalRequired = true; f.Return.Status = "Approved"; f.Return.ApprovedById = f.User; f.Return.ApprovedDate = DateTime.UtcNow; break;
        }
        Func<Task> run = () => f.Service.ShipAsync(f.Return.Id, f.User);
        await run.Should().ThrowAsync<InventorySupplierReturnException>();
        f.Movements.Should().BeEmpty();
        f.WarehouseStock.CurrentStock.Should().Be(10);
        f.BinStock.Quantity.Should().Be(10);
        f.Unit.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Actual_valuation_failure_prevents_stock_mutation_and_dispatch()
    {
        var f = new Fixture(); f.Return.ApprovalRequired = false; f.Return.Status = "ReadyToDispatch";
        f.Valuation.Setup(x => x.ProcessIssueAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<decimal>(),
            It.IsAny<InventoryMovementType>(), It.IsAny<ReferenceType>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Valuation blocked"));
        Func<Task> run = () => f.Service.ShipAsync(f.Return.Id, f.User);
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("Valuation blocked");
        f.WarehouseStock.CurrentStock.Should().Be(10); f.Movements.Should().BeEmpty();
        f.Return.Status.Should().Be("ReadyToDispatch");
    }

    [Fact]
    public async Task Draft_projection_uses_live_policy_not_default_true_and_read_only_actor_has_no_actions()
    {
        var f = new Fixture();
        f.Return.ApprovalRequired.Should().BeTrue();
        var dto = await f.Service.GetByIdAsync(f.Return.Id);
        dto!.ApprovalRequired.Should().BeFalse(); dto.CanSubmit.Should().BeTrue();
        f.Access.Setup(x => x.CheckCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(x => x.PermissionCode != "procurement.inventory.read"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });
        dto = await f.Service.GetByIdAsync(f.Return.Id);
        dto!.CanSubmit.Should().BeFalse(); dto.CanApprove.Should().BeFalse(); dto.CanDispatch.Should().BeFalse();
    }

    [Fact]
    public async Task Saved_draft_can_change_partial_quantity_and_reason_without_requiring_notes()
    {
        var f = new Fixture();
        await f.Service.UpdateAsync(f.Return.Id, new CreatePurchaseReturnDto
        {
            GoodsReceiptNoteId = f.Grn.Id, SupplierId = f.Return.SupplierId, WarehouseId = f.Return.WarehouseId,
            ReturnReason = "Damage", Items = new() { new() { InventoryItemId = f.Item.Id, GRNItemId = f.Source.Id,
                ReturnQuantity = 2, ReturnReason = "Damage" } }
        }, f.User);
        f.Return.Status.Should().Be("Draft"); f.Return.Items.Should().ContainSingle().Which.ReturnQuantity.Should().Be(2);
        f.Return.ReturnReason.Should().Be("Damage"); f.Return.Notes.Should().BeNull(); f.Movements.Should().BeEmpty();
    }

    [Fact]
    public async Task Draft_edit_detaches_raw_deleted_children_and_reloads_header_before_adding_replacements()
    {
        var f = new Fixture();
        var oldLine = f.Return.Items.Single();
        var reloaded = new PurchaseReturn
        {
            Id = f.Return.Id, TenantId = f.Return.TenantId, ReturnNumber = f.Return.ReturnNumber,
            GoodsReceiptNoteId = f.Grn.Id, SupplierId = f.Return.SupplierId,
            WarehouseId = f.Return.WarehouseId, RequestedById = f.User, Status = "Draft"
        };
        var calls = new List<string>();
        var repository = Mock.Get(f.Unit.Object.Repository<PurchaseReturnItem>());
        repository.Setup(x => x.HardDeleteRangeAsync(It.IsAny<IEnumerable<PurchaseReturnItem>>()))
            .Callback<IEnumerable<PurchaseReturnItem>>(values =>
            {
                values.Should().ContainSingle().Which.Should().BeSameAs(oldLine);
                calls.Add("delete");
            }).Returns(Task.CompletedTask);
        f.Unit.Setup(x => x.ClearTrackedChanges()).Callback(() => calls.Add("detach"));
        f.Returns.SetupSequence(x => x.GetWithItemsAsync(f.Return.Id))
            .ReturnsAsync(f.Return).ReturnsAsync(() => { calls.Add("reload"); return reloaded; });
        repository.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<PurchaseReturnItem>>()))
            .Callback(() => calls.Add("add")).ReturnsAsync((IEnumerable<PurchaseReturnItem> values) => values);

        var result = await f.Service.UpdateAsync(f.Return.Id, new CreatePurchaseReturnDto
        {
            GoodsReceiptNoteId = f.Grn.Id, SupplierId = f.Return.SupplierId, WarehouseId = f.Return.WarehouseId,
            ReturnReason = "Excess", Items = new() { new() { InventoryItemId = f.Item.Id,
                GRNItemId = f.Source.Id, ReturnQuantity = 2, ReturnReason = "Excess" } }
        }, f.User);

        calls.Should().Equal("delete", "detach", "reload", "add");
        reloaded.Items.Should().ContainSingle().Which.ReturnQuantity.Should().Be(2);
        reloaded.Items.Single().Id.Should().NotBe(oldLine.Id);
        result.Id.Should().Be(f.Return.Id);
        result.TotalQuantity.Should().Be(2);
        f.Movements.Should().BeEmpty();
        f.Unit.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("accepted-with-return-pending")]
    [InlineData("accepted-with-replacement-pending")]
    [InlineData("pending-inspection")]
    [InlineData("quality-hold")]
    [InlineData("missing-posted-evidence")]
    [InlineData("wrong-receipt-status")]
    [InlineData("wrong-movement-tenant")]
    [InlineData("same-item-bin-insufficient-posting")]
    [InlineData("same-item-bin-fully-posted")]
    [InlineData("posted-reversal")]
    [InlineData("unposted-reversal")]
    [InlineData("foreign-tenant-reversal")]
    [InlineData("service-line-not-stock")]
    public async Task Stale_inventory_snapshot_is_eligible_only_with_authoritative_accepted_and_posted_source(string condition)
    {
        var f = new Fixture();
        f.LinkAuthoritativeSource(condition);
        f.Workflow.Setup(x => x.SubmitAsync("PurchaseReturn", f.Return.Id)).ReturnsAsync(Result(false));
        Func<Task> submit = () => f.Service.SubmitForApprovalAsync(f.Return.Id, f.User);
        if (condition is "valid" or "accepted-with-return-pending" or "accepted-with-replacement-pending" or
            "same-item-bin-fully-posted" or "unposted-reversal" or "foreign-tenant-reversal" or "service-line-not-stock")
        {
            await submit(); f.Return.Status.Should().Be("ReadyToDispatch");
        }
        else
        {
            await submit.Should().ThrowAsync<InventorySupplierReturnException>();
            f.Return.Status.Should().Be("Draft");
        }
        f.Grn.Status.Should().Be(GRNStatus.PendingInspection, "eligibility must not rewrite the historical GRN");
        f.Source.AcceptedQuantity.Should().Be(0, "accepted quantities come from a read-only source projection");
        f.Movements.Should().BeEmpty();
    }

    [Fact]
    public void Migration_only_adds_fail_closed_snapshot_and_preserves_direct_history_on_down()
    {
        var migration = new PurchaseReturnOptionalApproval();
        var builder = new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(PurchaseReturnOptionalApproval).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(migration, new object[] { builder });
        builder.Operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation>()
            .Should().ContainSingle().Which.DefaultValue.Should().Be(true);
        PurchaseReturnOptionalApproval.GuardSql.Should().Contain("WorkflowApprovalRequiredAtSubmission(i.TenantId,N'PurchaseReturn',i.Id)=0");
        PurchaseReturnOptionalApproval.GuardSql.Should().Contain("i.ApprovedById IS NOT NULL OR i.ApprovedDate IS NOT NULL");
        PurchaseReturnOptionalApproval.GuardSql.Should().NotContain("UPDATE dbo.Inventory");
    }

    [Fact]
    public async Task Return_register_and_source_dropdown_use_real_warehouse_scope_not_an_invalid_global_read_check()
    {
        var f = new Fixture();
        f.LinkAuthoritativeSource("valid");
        f.Access.Setup(x => x.EnforceCapabilityAsync(
                It.Is<ProcurementAccessCapabilityRequest>(request => !request.WarehouseId.HasValue),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAccessValidationException("WAREHOUSE_REQUIRED", "A warehouse ID is required for this permission."));
        f.Access.Setup(x => x.CheckCapabilityAsync(
                It.Is<ProcurementAccessCapabilityRequest>(request => !request.WarehouseId.HasValue),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAccessValidationException("WAREHOUSE_REQUIRED", "A warehouse ID is required for this permission."));

        (await f.Service.GetAllAsync()).Should().ContainSingle().Which.Id.Should().Be(f.Return.Id);
        var sources = (await f.Service.GetSourceGrnsAsync()).ToList();
        sources.Should().ContainSingle().Which.Id.Should().Be(f.Grn.Id);
        sources[0].Items.Should().ContainSingle().Which.AcceptedQuantity.Should().Be(10);
        f.Grn.Status.Should().Be(GRNStatus.PendingInspection);
        f.Source.AcceptedQuantity.Should().Be(0);
        f.Access.Verify(x => x.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.PermissionCode == "procurement.inventory.read" && request.WarehouseId == f.Return.WarehouseId),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        f.Access.Verify(x => x.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => !request.WarehouseId.HasValue),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Return_register_and_source_dropdown_still_deny_an_actor_without_readable_warehouse_scope()
    {
        var f = new Fixture();
        f.Access.Setup(x => x.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });
        Func<Task> readReturns = async () => await f.Service.GetAllAsync();
        Func<Task> readSources = async () => await f.Service.GetSourceGrnsAsync();
        await readReturns.Should().ThrowAsync<InventorySupplierReturnException>().WithMessage("*no assigned warehouse*");
        await readSources.Should().ThrowAsync<InventorySupplierReturnException>().WithMessage("*no assigned warehouse*");
        f.Movements.Should().BeEmpty();
    }

    private static WorkflowIntegrationResult Result(bool required) => new(new WorkflowExecutionResult
    {
        Success = true, WorkflowInstanceId = required ? Guid.NewGuid() : null,
        Status = required ? WorkflowInstanceStatus.InProgress : WorkflowInstanceStatus.Completed
    }, required ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, required);

    private sealed class Fixture
    {
        public Guid User { get; } = Guid.NewGuid();
        public PurchaseReturn Return { get; }
        public GoodsReceiptNoteItem Source { get; }
        public GoodsReceiptNote Grn { get; }
        public WarehouseLocation Bin { get; }
        public InventoryItem Item { get; }
        public WarehouseQuantity WarehouseStock { get; }
        public InventoryLocation BinStock { get; }
        public List<StockMovement> Movements { get; } = new();
        public Mock<IUnitOfWork> Unit { get; } = new();
        public Mock<IPurchaseReturnRepository> Returns { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        public Mock<IProcurementAccessControlService> Access { get; } = new();
        public Mock<IInventoryValuationService> Valuation { get; } = new();
        public Mock<IInventoryTrackingControlService> Tracking { get; } = new();
        public PurchaseReturnService Service { get; }

        public Fixture()
        {
            var tenant = Guid.NewGuid(); var warehouseId = Guid.NewGuid();
            Bin = new() { Id = Guid.NewGuid(), TenantId = tenant, WarehouseId = warehouseId, IsActive = true };
            Item = new() { Id = Guid.NewGuid(), TenantId = tenant, CurrentStock = 10, AvailableStock = 10 };
            WarehouseStock = new() { TenantId = tenant, WarehouseId = warehouseId, InventoryItemId = Item.Id, CurrentStock = 10, AvailableStock = 10 };
            BinStock = new() { TenantId = tenant, InventoryItemId = Item.Id, LocationId = Bin.Id, Quantity = 10, AvailableQuantity = 10 };
            Source = new() { Id = Guid.NewGuid(), TenantId = tenant, InventoryItemId = Item.Id, StorageLocationId = Bin.Id, AcceptedQuantity = 10, UnitCost = 100 };
            var grn = Grn = new GoodsReceiptNote { Id = Guid.NewGuid(), TenantId = tenant, WarehouseId = warehouseId, SupplierId = Guid.NewGuid(), Status = GRNStatus.StockUpdated, StockUpdated = true, Items = new[] { Source } };
            Return = new() { Id = Guid.NewGuid(), TenantId = tenant, GoodsReceiptNoteId = grn.Id, SupplierId = grn.SupplierId.Value, WarehouseId = warehouseId, RequestedById = User, ReturnNumber = "SRT-TEST", ReturnReason = "Excess" };
            Return.Items.Add(new() { Id = Guid.NewGuid(), TenantId = tenant, PurchaseReturnId = Return.Id, PurchaseReturn = Return, InventoryItemId = Item.Id, GoodsReceiptNoteItemId = Source.Id, LocationId = Bin.Id, ReturnQuantity = 1, UnitCost = 100, LineValue = 100 });
            var returns = Returns; returns.Setup(x => x.GetWithItemsAsync(Return.Id)).ReturnsAsync(Return);
            returns.Setup(x => x.GetQueryable(It.IsAny<Expression<Func<PurchaseReturn, bool>>>()))
                .Returns((Expression<Func<PurchaseReturn, bool>> predicate) => new AsyncQuery<PurchaseReturn>(new[] { Return }).Where(predicate));
            var grns = new Mock<IGoodsReceiptNoteRepository>(); grns.Setup(x => x.GetWithItemsAsync(grn.Id)).ReturnsAsync(grn);
            grns.Setup(x => x.GetQueryable(It.IsAny<Expression<Func<GoodsReceiptNote, bool>>>()))
                .Returns((Expression<Func<GoodsReceiptNote, bool>> predicate) => new AsyncQuery<GoodsReceiptNote>(new[] { grn }).Where(predicate));
            var items = new Mock<IInventoryItemRepository>(); items.Setup(x => x.GetByIdAsync(Item.Id)).ReturnsAsync(Item);
            var warehouses = new Mock<IWarehouseRepository>(); warehouses.Setup(x => x.GetByIdAsync(warehouseId)).ReturnsAsync(new Warehouse { Id = warehouseId, TenantId = tenant });
            var quantities = new Mock<IWarehouseQuantityRepository>(); quantities.Setup(x => x.GetByWarehouseAndItemAsync(warehouseId, Item.Id)).ReturnsAsync(WarehouseStock);
            var locations = new Mock<IInventoryLocationRepository>(); locations.Setup(x => x.GetByLocationAndItemAsync(Bin.Id, Item.Id)).ReturnsAsync(() => BinStock);
            var movements = new Mock<IStockMovementRepository>(); movements.Setup(x => x.AddAsync(It.IsAny<StockMovement>())).ReturnsAsync((StockMovement value) => { Movements.Add(value); return value; });
            QueryRepo(new[] { Bin }); QueryRepo(Return.Items);
            QueryRepo(new[] { new Warehouse { Id = warehouseId, TenantId = tenant, IsActive = true } });
            var active = false;
            Unit.SetupGet(x => x.HasActiveTransaction).Returns(() => active);
            Unit.Setup(x => x.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>())).Callback(() => active = true).Returns(Task.CompletedTask);
            Unit.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => active = false).Returns(Task.CompletedTask);
            Unit.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Callback(() => active = false).Returns(Task.CompletedTask);
            Unit.Setup(x => x.ExecuteInStrategyAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>())).Returns((Func<Task<bool>> run, CancellationToken _) => run());
            Unit.Setup(x => x.ExecuteInStrategyAsync(It.IsAny<Func<Task<PurchaseReturnDto>>>(), It.IsAny<CancellationToken>())).Returns((Func<Task<PurchaseReturnDto>> run, CancellationToken _) => run());
            Access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
            Access.Setup(x => x.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
            Valuation.Setup(x => x.ProcessIssueAsync(Item.Id, warehouseId, Bin.Id, 1, InventoryMovementType.SupplierReturn, ReferenceType.Return, Return.ReturnNumber, Return.Id, null, null)).ReturnsAsync(123.45m);
            var actor = new Mock<ICurrentUserProvider>(); actor.SetupGet(x => x.UserId).Returns(User); actor.SetupGet(x => x.TenantId).Returns(tenant); actor.SetupGet(x => x.IsAuthenticated).Returns(true);
            Service = new(returns.Object, grns.Object, items.Object, warehouses.Object, quantities.Object, locations.Object, movements.Object,
                Mock.Of<IConsignmentSettlementService>(), Access.Object, Workflow.Object, Mock.Of<IProcurementControlEventService>(), Unit.Object, actor.Object,
                NullLogger<PurchaseReturnService>.Instance, Valuation.Object, Tracking.Object);
        }

        public void LinkAuthoritativeSource(string condition)
        {
            var tenant = Return.TenantId;
            var po = new PurchaseOrder { Id = Guid.NewGuid(), TenantId = tenant, BusinessPartnerId = Return.SupplierId };
            var poLine = new PurchaseOrderItem { Id = Guid.NewGuid(), TenantId = tenant, PurchaseOrderId = po.Id, InventoryItemId = Item.Id };
            var receipt = new PurchaseOrderReceipt { Id = Guid.NewGuid(), TenantId = tenant, PurchaseOrderId = po.Id, PurchaseOrder = po,
                Status = condition == "wrong-receipt-status" ? "Received" : "Accepted", RequiresInspection = true };
            receipt.Items.Add(new PurchaseOrderReceiptItem { TenantId = tenant, ReceiptId = receipt.Id, PurchaseOrderItemId = poLine.Id,
                PurchaseOrderItem = poLine, AcceptedQuantity = 10, ReceivedQuantity = 10, LocationId = Bin.Id });
            var inspection = new ProcurementReceiptInspectionCase { TenantId = tenant, PurchaseOrderReceiptId = receipt.Id, Sequence = 1,
                Status = condition switch
                {
                    "pending-inspection" => ProcurementReceiptInspectionStatus.PendingApproval,
                    "accepted-with-return-pending" => ProcurementReceiptInspectionStatus.ReturnPending,
                    "accepted-with-replacement-pending" => ProcurementReceiptInspectionStatus.ReplacementPending,
                    _ => ProcurementReceiptInspectionStatus.Approved
                },
                QualityHold = condition == "quality-hold", StockPostedQuantity = 10, StockPostedAtUtc = DateTime.UtcNow };
            var movement = new InventoryMovement { Id = Guid.NewGuid(), TenantId = condition == "wrong-movement-tenant" ? Guid.NewGuid() : tenant,
                WarehouseId = Return.WarehouseId, LocationId = Bin.Id, InventoryItemId = Item.Id, ReferenceId = receipt.Id,
                ReferenceType = ReferenceType.PO, MovementType = InventoryMovementType.PurchaseReceipt, Direction = MovementDirection.In,
                Quantity = 10, IsPosted = condition != "missing-posted-evidence", PostedAt = DateTime.UtcNow };
            Grn.PurchaseOrderReceiptId = receipt.Id; Grn.PurchaseOrderId = po.Id; Grn.Status = GRNStatus.PendingInspection;
            Source.PurchaseOrderItemId = poLine.Id; Source.AcceptedQuantity = 0;
            if (condition is "same-item-bin-insufficient-posting" or "same-item-bin-fully-posted")
            {
                var secondPoLine = new PurchaseOrderItem { Id = Guid.NewGuid(), TenantId = tenant, PurchaseOrderId = po.Id, InventoryItemId = Item.Id };
                receipt.Items.Add(new PurchaseOrderReceiptItem { TenantId = tenant, ReceiptId = receipt.Id,
                    PurchaseOrderItemId = secondPoLine.Id, PurchaseOrderItem = secondPoLine,
                    AcceptedQuantity = 6, ReceivedQuantity = 6, LocationId = Bin.Id });
                inspection.StockPostedQuantity = 16;
                movement.Quantity = condition == "same-item-bin-fully-posted" ? 16 : 10;
            }
            if (condition == "service-line-not-stock")
            {
                var servicePoLine = new PurchaseOrderItem { Id = Guid.NewGuid(), TenantId = tenant, PurchaseOrderId = po.Id, LineType = ItemType.Service };
                receipt.Items.Add(new PurchaseOrderReceiptItem { TenantId = tenant, ReceiptId = receipt.Id,
                    PurchaseOrderItemId = servicePoLine.Id, PurchaseOrderItem = servicePoLine, AcceptedQuantity = 1, ReceivedQuantity = 1 });
            }
            var sourceMovements = new List<InventoryMovement> { movement };
            if (condition is "posted-reversal" or "unposted-reversal" or "foreign-tenant-reversal")
                sourceMovements.Add(new InventoryMovement
                {
                    Id = Guid.NewGuid(), TenantId = condition == "foreign-tenant-reversal" ? Guid.NewGuid() : tenant,
                    WarehouseId = Return.WarehouseId, LocationId = Bin.Id, InventoryItemId = Item.Id,
                    ReferenceId = receipt.Id, ReferenceType = ReferenceType.PO, MovementType = InventoryMovementType.PurchaseReceipt,
                    Direction = MovementDirection.Out, Quantity = 10, ReversedMovementId = movement.Id, IsReversal = true,
                    IsPosted = condition != "unposted-reversal", PostedAt = DateTime.UtcNow
                });
            QueryRepo(new[] { receipt }); QueryRepo(new[] { inspection }); QueryRepo(sourceMovements);
        }

        private void QueryRepo<T>(IEnumerable<T> values) where T : BaseEntity
        {
            var repository = new Mock<IGenericRepository<T>>();
            repository.Setup(x => x.GetQueryable()).Returns(() => new AsyncQuery<T>(values));
            repository.Setup(x => x.GetQueryable(It.IsAny<Expression<Func<T, bool>>>())).Returns((Expression<Func<T, bool>> predicate) => new AsyncQuery<T>(values).Where(predicate));
            Unit.Setup(x => x.Repository<T>()).Returns(repository.Object);
        }
    }

    private sealed class AsyncQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncQuery(IEnumerable<T> values) : base(values) { }
        public AsyncQuery(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncProvider(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
    private sealed class AsyncEnumerator<T>(IEnumerator<T> enumerator) : IAsyncEnumerator<T>
    {
        public T Current => enumerator.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(enumerator.MoveNext());
        public ValueTask DisposeAsync() { enumerator.Dispose(); return ValueTask.CompletedTask; }
    }
    private sealed class AsyncProvider(IQueryProvider provider) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => provider.CreateQuery(expression);
        public IQueryable<T> CreateQuery<T>(Expression expression) => new AsyncQuery<T>(expression);
        public object? Execute(Expression expression) => provider.Execute(expression);
        public T Execute<T>(Expression expression) => provider.Execute<T>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var result = typeof(IQueryProvider).GetMethod(nameof(Execute), 1, new[] { typeof(Expression) })!.MakeGenericMethod(resultType).Invoke(provider, new object[] { expression });
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, new[] { result })!;
        }
    }
}
