using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryDirectedOperationServiceTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();
    private readonly Mock<IInventoryRequisitionService> _requisitions = new();
    private readonly Mock<IInventoryTransferService> _transfers = new();
    private readonly InventoryDirectedOperationService _service;

    public InventoryDirectedOperationServiceTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        _unitOfWork = new UnitOfWork(_context);
        _currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(value => value.UserId).Returns(_userId);
        _currentUser.SetupGet(value => value.Username).Returns("warehouse.operator@test.local");
        _currentUser.SetupGet(value => value.FullName).Returns("Warehouse Operator");
        _currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(value => value.IsExternalUser).Returns(false);
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        _service = new InventoryDirectedOperationService(_unitOfWork, _currentUser.Object, _access.Object,
            _requisitions.Object, _transfers.Object);
    }

    [Fact]
    public async Task Replenishment_suggestion_uses_live_pick_face_shortfall_and_reserve_bin()
    {
        var (warehouse, item, reserve, pick) = await SeedBinsAsync();
        item.ReorderLevel = 10;
        item.ReorderQuantity = 5;
        await _context.AddRangeAsync(
            new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = reserve.Id, Quantity = 20, AvailableQuantity = 20 },
            new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = pick.Id, Quantity = 2, AvailableQuantity = 2 });
        await _context.SaveChangesAsync();

        var result = await _service.GetSuggestionsAsync(warehouse.Id, InventoryDirectedTaskType.Replenishment);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            TaskType = InventoryDirectedTaskType.Replenishment,
            WarehouseId = warehouse.Id,
            InventoryItemId = item.Id,
            SourceLocationId = (Guid?)reserve.Id,
            DestinationLocationId = (Guid?)pick.Id,
            Quantity = 8m,
            SourceDocumentType = "InventoryLocationReplenishment",
            IsQuarantine = false
        });
        result.Single().DestinationCapacity!.HasCapacity.Should().BeTrue();
        result.Single().SuggestionKey.Should().HaveLength(64);
    }

    [Fact]
    public async Task Picking_confirmation_delegates_exact_quantity_location_and_tracking_to_requisition_owner()
    {
        var (warehouse, item, reserve, _) = await SeedBinsAsync();
        var requisition = new InventoryRequisition
        {
            TenantId = _tenantId, WarehouseId = warehouse.Id, DepartmentId = Guid.NewGuid(),
            RequisitionNumber = "REQ-DIRECT-001", Status = RequisitionStatus.Approved
        };
        var line = new InventoryRequisitionItem
        {
            TenantId = _tenantId, InventoryRequisitionId = requisition.Id, InventoryRequisition = requisition,
            InventoryItemId = item.Id, InventoryItem = item, ApprovedQuantity = 4, IssuedQuantity = 0
        };
        var task = NewDirectedTask(warehouse, item, reserve.Id, null, InventoryDirectedTaskType.Picking,
            "InventoryRequisition", requisition.Id, line.Id, 4);
        await _context.AddRangeAsync(requisition, line,
            new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = reserve.Id, Quantity = 10, AvailableQuantity = 10 },
            task);
        await _context.SaveChangesAsync();
        _requisitions.Setup(value => value.IssueAsync(requisition.Id, It.IsAny<IssueRequisitionDto>())).ReturnsAsync(true);

        var result = await _service.ConfirmTaskAsync(task.Id, new ConfirmInventoryDirectedTaskRequest
        {
            RowVersion = Convert.ToBase64String(task.RowVersion), Comment = "Four units picked.",
            LotNumber = "LOT-01", SerialNumber = "SER-01"
        }, "corr-pick");

        result.Status.Should().Be(InventoryDirectedTaskStatus.Completed);
        _requisitions.Verify(value => value.IssueAsync(requisition.Id, It.Is<IssueRequisitionDto>(request =>
            request.Items.Count == 1 && request.Items[0].ItemId == line.Id &&
            request.Items[0].IssuedQuantity == 4 && request.Items[0].LocationId == reserve.Id &&
            request.Items[0].LotNumber == "LOT-01" && request.Items[0].SerialNumber == "SER-01")), Times.Once);
        (await _context.Set<InventoryDirectedTaskAction>().SingleAsync()).ActionType.Should().Be(InventoryDirectedTaskActionType.PickConfirmed);
        (await _context.Set<AuditLog>().SingleAsync(value => value.Resource == "InventoryDirectedOperation")).Action.Should().Be("Confirm");
    }

    [Fact]
    public async Task Replenishment_confirmation_creates_an_ordinary_same_warehouse_transfer_and_waits_for_it()
    {
        var (warehouse, item, reserve, pick) = await SeedBinsAsync();
        var sourceBalance = new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = reserve.Id, Quantity = 10, AvailableQuantity = 10 };
        var destinationBalance = new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = pick.Id, Quantity = 1, AvailableQuantity = 1 };
        var task = NewDirectedTask(warehouse, item, reserve.Id, pick.Id, InventoryDirectedTaskType.Replenishment,
            "InventoryLocationReplenishment", destinationBalance.Id, sourceBalance.Id, 6);
        await _context.AddRangeAsync(sourceBalance, destinationBalance, task);
        await _context.SaveChangesAsync();
        var transferId = Guid.NewGuid();
        _transfers.Setup(value => value.CreateAsync(It.IsAny<CreateInventoryTransferDto>(), _userId))
            .ReturnsAsync(new InventoryTransferDto { Id = transferId, TransferNumber = "TRF-001" });

        var result = await _service.ConfirmTaskAsync(task.Id, new ConfirmInventoryDirectedTaskRequest
        {
            RowVersion = Convert.ToBase64String(task.RowVersion), Comment = "Reserve stock verified."
        }, "corr-replenish");

        result.Status.Should().Be(InventoryDirectedTaskStatus.AwaitingStockMove);
        result.LinkedInventoryTransferId.Should().Be(transferId);
        _transfers.Verify(value => value.CreateAsync(It.Is<CreateInventoryTransferDto>(request =>
            request.SourceWarehouseId == warehouse.Id && request.DestinationWarehouseId == warehouse.Id &&
            request.TransferType == TransferType.Replenishment && request.Items.Count == 1 &&
            request.Items[0].InventoryItemId == item.Id && request.Items[0].RequestedQuantity == 6 &&
            request.Items[0].SourceLocationId == reserve.Id && request.Items[0].DestinationLocationId == pick.Id), _userId), Times.Once);
    }

    [Fact]
    public async Task Location_authority_filters_suggestions_and_external_users_fail_closed()
    {
        var (warehouse, item, reserve, pick) = await SeedBinsAsync();
        item.ReorderLevel = 5;
        item.ReorderQuantity = 5;
        await _context.AddRangeAsync(
            new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = reserve.Id, Quantity = 10, AvailableQuantity = 10 },
            new InventoryLocation { TenantId = _tenantId, InventoryItemId = item.Id, LocationId = pick.Id, Quantity = 0, AvailableQuantity = 0 });
        await _context.SaveChangesAsync();
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken __) =>
                new ProcurementAccessCapabilityDecisionDto { Allowed = request.LocationId != pick.Id });

        (await _service.GetSuggestionsAsync(warehouse.Id, InventoryDirectedTaskType.Replenishment)).Should().BeEmpty();
        _currentUser.SetupGet(value => value.IsExternalUser).Returns(true);
        var denied = () => _service.GetTasksAsync();
        await denied.Should().ThrowAsync<InventoryDirectedOperationAuthorizationException>();
    }

    [Fact]
    public void Ef_model_has_tenant_replay_source_and_concurrency_controls()
    {
        var entity = _context.Model.FindEntityType(typeof(InventoryDirectedTask))!;
        entity.FindProperty(nameof(InventoryDirectedTask.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        entity.GetIndexes().Should().Contain(value => value.IsUnique && value.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "IdempotencyKey" }));
        entity.GetIndexes().Should().Contain(value => value.IsUnique && value.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "SuggestionKey" }));
        var action = _context.Model.FindEntityType(typeof(InventoryDirectedTaskAction))!;
        action.GetIndexes().Should().Contain(value => value.IsUnique && value.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "TaskId", "Sequence" }));
    }

    [Fact]
    public void Migration_is_a_two_table_delta_with_tenant_state_and_append_only_sql_guards()
    {
        var migration = new TDC0605DirectedWarehouseOperations();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name)
            .Should().BeEquivalentTo("InventoryDirectedTasks", "InventoryDirectedTaskActions");
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryDirectedTasks_Integrity");
        sql.Should().Contain("TR_InventoryDirectedTaskActions_AppendOnly");
        sql.Should().Contain("Directed task bins must belong to the task warehouse and tenant.");
        sql.Should().Contain("Directed task actions are append-only");
        sql.Should().Contain("LinkedInventoryTransferId");
        sql.Should().NotContain("UPDATE [dbo].[InventoryLocations]");
        sql.Should().NotContain("UPDATE [dbo].[WarehouseQuantities]");
    }

    private async Task<(Warehouse warehouse, InventoryItem item, WarehouseLocation reserve, WarehouseLocation pick)> SeedBinsAsync()
    {
        var warehouse = new Warehouse { TenantId = _tenantId, Code = "WH-01", Name = "Main", IsActive = true };
        var item = new InventoryItem { TenantId = _tenantId, CategoryId = Guid.NewGuid(), ItemCode = "ITEM-01", Name = "Directed item", Weight = 2, Volume = 1 };
        var reserve = new WarehouseLocation { TenantId = _tenantId, WarehouseId = warehouse.Id, LocationCode = "RSV-01", Name = "Reserve", IsActive = true, IsPickingLocation = false };
        var pick = new WarehouseLocation { TenantId = _tenantId, WarehouseId = warehouse.Id, LocationCode = "PICK-01", Name = "Pick face", IsActive = true, IsPickingLocation = true, MaxWeight = 100, MaxVolume = 100, MaxItems = 10 };
        var tenant = new Tenant { Id = _tenantId, Name = "Directed test tenant", Code = $"T{Guid.NewGuid():N}"[..16] };
        var user = new ApplicationUser { Id = _userId, TenantId = _tenantId, UserName = "warehouse.operator@test.local", FirstName = "Warehouse", LastName = "Operator", IsActive = true };
        if (!await _context.Set<Tenant>().AnyAsync(value => value.Id == _tenantId)) await _context.AddAsync(tenant);
        if (!await _context.Set<ApplicationUser>().AnyAsync(value => value.Id == _userId)) await _context.AddAsync(user);
        await _context.AddRangeAsync(warehouse, item, reserve, pick);
        await _context.SaveChangesAsync();
        return (warehouse, item, reserve, pick);
    }

    private InventoryDirectedTask NewDirectedTask(
        Warehouse warehouse,
        InventoryItem item,
        Guid? sourceLocationId,
        Guid? destinationLocationId,
        InventoryDirectedTaskType type,
        string sourceType,
        Guid sourceDocumentId,
        Guid sourceLineId,
        decimal quantity) => new()
    {
        TenantId = _tenantId,
        TaskNumber = $"DWO-{Guid.NewGuid():N}"[..18],
        TaskType = type,
        Status = InventoryDirectedTaskStatus.InProgress,
        WarehouseId = warehouse.Id,
        InventoryItemId = item.Id,
        SourceLocationId = sourceLocationId,
        DestinationLocationId = destinationLocationId,
        Quantity = quantity,
        SourceDocumentType = sourceType,
        SourceDocumentId = sourceDocumentId,
        SourceLineId = sourceLineId,
        SourceReference = "TEST-SOURCE",
        SuggestionKey = new string('a', 64),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        PayloadHash = new string('b', 64),
        CapacitySnapshotJson = "{}",
        AssignedToUserId = _userId,
        CreatedByUserId = _userId,
        AssignedAtUtc = DateTime.UtcNow,
        StartedAtUtc = DateTime.UtcNow,
        Reason = "Focused directed operation test.",
        CorrelationId = "test-correlation",
        IntegrityHash = new string('c', 64),
        RowVersion = new byte[] { 1 }
    };

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        _unitOfWork.Dispose();
    }
}
