using System.Reflection;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTransferOptionalApprovalTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Mock<IWorkflowIntegrationService> _workflow = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();
    private readonly List<(TransferStatus Status, bool ApprovalRequired)> _saveStates = [];
    private InventoryTransfer _transfer = null!;
    private WarehouseLocation _source = null!;
    private InventoryTransferService _service = null!;

    [Fact]
    public async Task Confirmed_inactive_readies_transfer_without_approval_or_stock_and_persists_action_first()
    {
        await Setup();
        _workflow.Setup(x => x.SubmitAsync("InventoryTransfer", _transfer.Id)).ReturnsAsync(Result(false));
        (await _service.SubmitForApprovalAsync(_transfer.Id, _actor)).Should().BeTrue();
        _transfer.Status.Should().Be(TransferStatus.Approved);
        _transfer.ApprovalRequired.Should().BeFalse();
        _transfer.ApprovedById.Should().BeNull();
        _transfer.ApprovalDate.Should().BeNull();
        _saveStates.Should().Equal((TransferStatus.Draft, true), (TransferStatus.Approved, false));
        var action = (await _db.Set<InventoryTransferAction>().ToListAsync()).Should().ContainSingle().Which;
        action.ActionType.Should().Be(InventoryTransferActionType.Submitted);
        action.SnapshotJson.Should().Contain("\"ApprovalRequired\":false");
        (await _db.Set<StockMovement>().CountAsync()).Should().Be(0);
        _access.Verify(x => x.EnforceCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(r =>
            r.RequireLocationScope && r.PermissionCode == "procurement.inventory.transfer" && r.LocationId == _source.Id),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Active_workflow_remains_submitted_and_requires_assigned_independent_approver()
    {
        await Setup();
        _workflow.Setup(x => x.SubmitAsync("InventoryTransfer", _transfer.Id)).ReturnsAsync(Result(true));
        await _service.SubmitForApprovalAsync(_transfer.Id, _actor);
        _transfer.Status.Should().Be(TransferStatus.Submitted);
        _transfer.ApprovalRequired.Should().BeTrue();
        _transfer.ApprovedById.Should().BeNull();
        await FluentActions.Awaiting(() => _service.ApproveAsync(_transfer.Id, _actor)).Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => _service.ApproveAsync(_transfer.Id, Guid.NewGuid())).Should().ThrowAsync<UnauthorizedAccessException>();
        _workflow.Verify(x => x.ProcessApprovalAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Immediate_active_completion_and_failed_lookup_do_not_save_a_ready_transfer(bool active, bool success)
    {
        await Setup();
        _workflow.Setup(x => x.SubmitAsync("InventoryTransfer", _transfer.Id)).ReturnsAsync(new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = success, Status = WorkflowInstanceStatus.Completed }, WorkflowOutcome.Approved, active));
        await FluentActions.Awaiting(() => _service.SubmitForApprovalAsync(_transfer.Id, _actor)).Should().ThrowAsync<InvalidOperationException>();
        _saveStates.Should().BeEmpty();
        (await _db.Set<InventoryTransferAction>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("missing-bin")]
    [InlineData("foreign-bin")]
    [InlineData("inactive-bin")]
    [InlineData("same-bin")]
    public async Task Invalid_lines_or_scope_are_rejected_before_workflow(string problem)
    {
        await Setup();
        if (problem == "empty") _transfer.Items.Clear();
        if (problem == "zero") _transfer.Items.Single().RequestedQuantity = 0;
        if (problem == "missing-bin") _transfer.Items.Single().SourceLocationId = null;
        if (problem == "foreign-bin") _source.TenantId = Guid.NewGuid();
        if (problem == "inactive-bin") _source.IsActive = false;
        if (problem == "same-bin") _transfer.Items.Single().DestinationLocationId = _source.Id;
        await FluentActions.Awaiting(() => _service.SubmitForApprovalAsync(_transfer.Id, _actor)).Should().ThrowAsync<InvalidOperationException>();
        _workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Direct_mode_never_exposes_approval_route_and_reaches_normal_quantity_validation_for_requester_dispatch()
    {
        await Setup();
        _transfer.Status = TransferStatus.Approved;
        _transfer.ApprovalRequired = false;
        await FluentActions.Awaiting(() => _service.ApproveAsync(_transfer.Id, Guid.NewGuid())).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _service.RejectAsync(_transfer.Id, "No", Guid.NewGuid())).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _service.ShipAsync(_transfer.Id, _actor, shippedItems: new Dictionary<Guid, decimal>()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("At least one positive dispatch quantity is required.");
        (await _db.Set<StockMovement>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Active_transfer_requester_still_cannot_dispatch()
    {
        await Setup();
        _transfer.Status = TransferStatus.Approved;
        _transfer.ApprovedById = Guid.NewGuid();
        await FluentActions.Awaiting(() => _service.ShipAsync(_transfer.Id, _actor)).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconciled_direct_transfer_can_be_closed_by_same_operator_but_active_transfer_cannot(bool active)
    {
        await Setup();
        _transfer.ApprovalRequired = active;
        _transfer.Status = TransferStatus.Received;
        _transfer.ShippedById = _actor;
        _transfer.ReceivedById = _actor;
        _transfer.Items.Single().ShippedQuantity = 2;
        _transfer.Items.Single().ReceivedQuantity = 2;
        var close = () => _service.CloseAsync(_transfer.Id, _actor, new CloseInventoryTransferRequest
        { RowVersion = Convert.ToBase64String(_transfer.RowVersion), IdempotencyKey = "close-direct" });
        if (active)
            await close.Should().ThrowAsync<UnauthorizedAccessException>();
        else
        {
            (await close()).Should().BeTrue();
            _transfer.Status.Should().Be(TransferStatus.Completed);
            _transfer.ApprovedById.Should().BeNull();
            _transfer.ClosedById.Should().Be(_actor);
        }
    }

    [Fact]
    public void Migration_patches_exact_existing_guard_and_retains_nullable_approver_operational_separation()
    {
        static MigrationBuilder Build(Migration migration)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
            return builder;
        }
        var previous = Build(new TDC0608ControlledInventoryTransfers()).Operations.OfType<SqlOperation>()
            .ToDictionary(x => Regex.Match(x.Sql, @"CREATE OR ALTER TRIGGER \[dbo\]\.\[([^\]]+)\]").Groups[1].Value, x => x.Sql);
        var next = Build(new InventoryTransferOptionalApproval());
        next.Operations.OfType<AddColumnOperation>().Should().ContainSingle().Which.DefaultValue.Should().Be(true);
        foreach (var operation in next.Operations.OfType<SqlOperation>())
        {
            var trigger = Regex.Match(operation.Sql, @"OBJECT_ID\(N'dbo\.([^']+)',N'TR'\)").Groups[1].Value;
            var before = Regex.Match(operation.Sql, "DECLARE @before nvarchar\\(max\\)=N'((?:''|[^'])*)';", RegexOptions.Singleline).Groups[1].Value.Replace("''", "'");
            var after = Regex.Match(operation.Sql, "SET @definition=REPLACE\\(@definition,@before,N'((?:''|[^'])*)'\\);", RegexOptions.Singleline).Groups[1].Value.Replace("''", "'");
            before.Should().NotBeEmpty();
            previous[trigger].Split(before, StringSplitOptions.None).Should().HaveCount(2, "every surgical anchor must occur exactly once");
            previous[trigger] = previous[trigger].Replace(before, after, StringComparison.Ordinal);
        }
        var lifecycle = previous["TR_InventoryTransfers_ControlledLifecycle"];
        lifecycle.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'InventoryTransfer',i.Id)=0");
        lifecycle.Should().Contain("d.Id IS NULL AND i.ApprovalRequired=0");
        lifecycle.Should().Contain("i.ClosedById <> i.ShippedById AND i.ClosedById <> i.ReceivedById");
        lifecycle.Should().Contain("i.ApprovalRequired=0 OR");
        lifecycle.Should().NotContain("i.ShippedById NOT IN");
        lifecycle.Should().Contain("INV_TRANSFER_SOURCE_IMMUTABLE");
        lifecycle.Should().Contain("INV_TRANSFER_STATE_INVALID");
        previous["TR_InventoryTransferDiscrepancies_ControlledLifecycle"].Should().Contain("t.ApprovalRequired=1 AND i.ResolvedById IN");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Final_good_receipt_completes_automatically_with_real_receiver_and_no_extra_stock_or_approval(bool active)
    {
        await Setup();
        var receiver = Guid.NewGuid();
        _transfer.ApprovalRequired = active;
        _transfer.ApprovedById = active ? Guid.NewGuid() : null;
        var approvedBy = _transfer.ApprovedById;
        _transfer.Status = TransferStatus.Received;
        _transfer.ReceivedById = receiver;
        _transfer.Items.Single().ShippedQuantity = 2;
        _transfer.Items.Single().ReceivedQuantity = 2;
        var receipt = await SaveReceiptAction(receiver);

        await AutoComplete(receiver, receipt);
        _transfer.Status.Should().Be(TransferStatus.Completed);
        _transfer.ClosedById.Should().Be(receiver);
        _transfer.CompletedDate.Should().NotBeNull();
        _transfer.ApprovedById.Should().Be(approvedBy);
        var completed = await _db.Set<InventoryTransferAction>().SingleAsync(x => x.ActionType == InventoryTransferActionType.Closed);
        completed.Sequence.Should().Be(receipt.Sequence + 1);
        completed.SnapshotJson.Should().Contain("\"AutomaticCompletion\":true").And.Contain(receipt.Id.ToString());
        (await _db.Set<StockMovement>().CountAsync()).Should().Be(0);
        // Repeating the receipt completion cannot append another closing action.
        await AutoComplete(receiver, receipt);
        (await _db.Set<InventoryTransferAction>().CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(1, 0, 0, false)]
    [InlineData(1, 1, 0, true)]
    [InlineData(1, 0, 1, true)]
    [InlineData(2, 0, 0, true)]
    public async Task Missing_good_units_or_legacy_discrepancies_never_auto_complete(decimal received, decimal damaged, decimal shortage, bool open)
    {
        await Setup();
        _transfer.Status = TransferStatus.Received;
        _transfer.HasOpenDiscrepancy = open;
        var line = _transfer.Items.Single();
        line.ShippedQuantity = 2;
        line.ReceivedQuantity = received;
        line.DamagedQuantity = damaged;
        line.ShortageQuantity = shortage;
        var receipt = await SaveReceiptAction(_actor);
        await AutoComplete(_actor, receipt);
        _transfer.Status.Should().Be(TransferStatus.Received);
        _transfer.CompletedDate.Should().BeNull();
        (await _db.Set<InventoryTransferAction>().CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public async Task Normal_receipt_accepts_only_good_quantities_and_does_not_book_damage_or_shortage_as_received(decimal damaged, decimal shortage)
    {
        await Setup();
        _transfer.ApprovalRequired = false;
        _transfer.Status = TransferStatus.InTransit;
        _transfer.Items.Single().ShippedQuantity = 2;
        await FluentActions.Awaiting(() => _service.ReceiveAsync(_transfer.Id, _actor,
            [new() { Id = _transfer.Items.Single().Id, ReceivedQuantity = 1, DamagedQuantity = damaged, ShortageQuantity = shortage }]))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Receive only the quantity available in good condition.*");
        _transfer.Status.Should().Be(TransferStatus.InTransit);
        _transfer.Items.Single().ReceivedQuantity.Should().Be(0);
        (await _db.Set<InventoryTransferAction>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Automatic_completion_migration_keeps_guards_and_requires_a_linked_good_receipt()
    {
        static MigrationBuilder Build(Migration migration)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
            return builder;
        }
        var definitions = Build(new TDC0608ControlledInventoryTransfers()).Operations.OfType<SqlOperation>()
            .ToDictionary(x => Regex.Match(x.Sql, @"CREATE OR ALTER TRIGGER \[dbo\]\.\[([^\]]+)\]").Groups[1].Value, x => x.Sql);
        foreach (var migration in new Migration[] { new InventoryTransferOptionalApproval(), new InventoryTransferAutomaticCompletion() })
        foreach (var operation in Build(migration).Operations.OfType<SqlOperation>())
        {
            var trigger = Regex.Match(operation.Sql, @"OBJECT_ID\(N'dbo\.([^']+)',N'TR'\)").Groups[1].Value;
            var before = Regex.Match(operation.Sql, "DECLARE @before nvarchar\\(max\\)=N'((?:''|[^'])*)';", RegexOptions.Singleline).Groups[1].Value.Replace("''", "'");
            var after = Regex.Match(operation.Sql, "SET @definition=REPLACE\\(@definition,@before,N'((?:''|[^'])*)'\\);", RegexOptions.Singleline).Groups[1].Value.Replace("''", "'");
            before.Should().NotBeEmpty();
            definitions[trigger].Split(before, StringSplitOptions.None).Should().HaveCount(2);
            definitions[trigger] = definitions[trigger].Replace(before, after, StringComparison.Ordinal);
        }
        var lifecycle = definitions["TR_InventoryTransfers_ControlledLifecycle"];
        lifecycle.Should().Contain("line.ReceivedQuantity<>line.ShippedQuantity");
        lifecycle.Should().Contain("line.DamagedQuantity<>0 OR line.ShortageQuantity<>0");
        lifecycle.Should().Contain("receipt.ActorUserId=completed.ActorUserId");
        lifecycle.Should().Contain("completed.Sequence=receipt.Sequence+1");
        lifecycle.Should().Contain("$.Metadata.AutomaticCompletion");
        lifecycle.Should().Contain("INV_TRANSFER_SOURCE_IMMUTABLE").And.Contain("INV_TRANSFER_STATE_INVALID");
        lifecycle.Should().Contain("i.ReceivedById IN (i.RequestedById, i.ApprovedById, i.ShippedById)");
    }

    private async Task<InventoryTransferAction> SaveReceiptAction(Guid receiver)
    {
        var receipt = new InventoryTransferAction { TenantId = _tenant, InventoryTransferId = _transfer.Id,
            ActionType = InventoryTransferActionType.Received, ActorUserId = receiver, Sequence = 1,
            IdempotencyKey = "receipt-test", CorrelationId = "receipt-test", PayloadHash = new string('a',64),
            IntegrityHash = new string('b',64), OccurredAtUtc = DateTime.UtcNow };
        _db.Add(receipt);
        await _db.SaveChangesAsync();
        return receipt;
    }

    private Task AutoComplete(Guid receiver, InventoryTransferAction receipt) => (Task)typeof(InventoryTransferService)
        .GetMethod("CompleteAfterReceiptAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(_service, [_transfer, receiver, receipt, "receipt-test"])!;

    private static WorkflowIntegrationResult Result(bool active) => new(new WorkflowExecutionResult
    {
        Success = true, Status = active ? WorkflowInstanceStatus.InProgress : WorkflowInstanceStatus.Completed,
        WorkflowInstanceId = active ? Guid.NewGuid() : null
    }, active ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, active);

    private async Task Setup()
    {
        var warehouse = new Warehouse { TenantId = _tenant, Code = "TRANSFER-WH", Name = "Transfer warehouse", IsActive = true };
        _source = new WarehouseLocation { TenantId = _tenant, WarehouseId = warehouse.Id, LocationCode = "FROM", IsActive = true };
        var destination = new WarehouseLocation { TenantId = _tenant, WarehouseId = warehouse.Id, LocationCode = "TO", IsActive = true };
        _transfer = new InventoryTransfer { TenantId = _tenant, TransferNumber = "TRF-OPTIONAL", RequestedById = _actor,
            SourceWarehouseId = warehouse.Id, DestinationWarehouseId = warehouse.Id, TotalItems = 1, TotalQuantity = 2, TotalValue = 20 };
        _transfer.Items.Add(new InventoryTransferItem { TenantId = _tenant, InventoryTransferId = _transfer.Id,
            InventoryItemId = Guid.NewGuid(), RequestedQuantity = 2, UnitCost = 10, UnitOfMeasure = "EA",
            SourceLocationId = _source.Id, DestinationLocationId = destination.Id });
        _db.Add(_transfer);
        await _db.SaveChangesAsync();
        var transferRepo = new Mock<IInventoryTransferRepository>();
        transferRepo.Setup(x => x.GetWithItemsAsync(_transfer.Id)).ReturnsAsync(_transfer);
        var locations = new Mock<IWarehouseLocationRepository>();
        locations.Setup(x => x.GetByIdAsync(_source.Id)).ReturnsAsync(_source);
        locations.Setup(x => x.GetByIdAsync(destination.Id)).ReturnsAsync(destination);
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(x => x.UserId).Returns(_actor);
        current.SetupGet(x => x.TenantId).Returns(_tenant);
        current.SetupGet(x => x.Username).Returns("requester");
        _access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        var realUow = new UnitOfWork(_db);
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(x => x.HasActiveTransaction).Returns(true);
        uow.Setup(x => x.Repository<InventoryTransferAction>()).Returns(realUow.Repository<InventoryTransferAction>());
        uow.Setup(x => x.Repository<InventoryTransferActionLine>()).Returns(realUow.Repository<InventoryTransferActionLine>());
        uow.Setup(x => x.Repository<AuditLog>()).Returns(realUow.Repository<AuditLog>());
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken token) =>
        { _saveStates.Add((_transfer.Status, _transfer.ApprovalRequired)); return await _db.SaveChangesAsync(token); });
        var registry = new Mock<IWorkflowStatusAdapterRegistry>();
        registry.Setup(x => x.GetAdapter("InventoryTransfer")).Returns(new InventoryTransferWorkflowStatusAdapter());
        _service = new InventoryTransferService(transferRepo.Object, Mock.Of<IInventoryTransferItemRepository>(),
            Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(), locations.Object,
            Mock.Of<IWarehouseQuantityRepository>(), Mock.Of<IInventoryLocationRepository>(), Mock.Of<IStockMovementRepository>(),
            Mock.Of<IConsignmentSettlementService>(), uow.Object, current.Object, _workflow.Object, registry.Object,
            Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(), _access.Object,
            Mock.Of<IProcurementControlEventService>(), NullLogger<InventoryTransferService>.Instance);
    }

    public void Dispose() => _db.Dispose();
}
