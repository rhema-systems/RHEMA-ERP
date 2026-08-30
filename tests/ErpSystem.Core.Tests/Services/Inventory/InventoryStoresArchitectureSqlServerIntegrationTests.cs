using System.Reflection;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

/// <summary>
/// Real SQL Server acceptance gates for the TDC Inventory/Stores stock ledger.
/// Every test owns and removes a disposable database; no shared ERP data is used.
/// </summary>
public sealed class InventoryStoresArchitectureSqlServerIntegrationTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    [Trait("Batch", "TDC-INV-STORES")]
    public async Task Fresh_transfer_lifecycle_enforces_independent_roles_tenant_scope_stock_and_replay_controls()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var sourceWarehouseId = Guid.NewGuid();
        var destinationWarehouseId = Guid.NewGuid();
        var sourceBalanceId = Guid.NewGuid();
        var destinationBalanceId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        await context.Database.EnsureCreatedAsync();
        await database.ApplySqlOperationsAsync(new TDC0610NegativeStockControl());
        await database.ApplySqlOperationsAsync(new TDC0608ControlledInventoryTransfers());

        var maker = NewUser(tenantId, "maker");
        var approver = NewUser(tenantId, "approver");
        var dispatcher = NewUser(tenantId, "dispatcher");
        var receiver = NewUser(tenantId, "receiver");
        var closer = NewUser(tenantId, "closer");
        var unauthorized = NewUser(tenantId, "unauthorized");
        var otherTenantMaker = NewUser(otherTenantId, "other-maker");
        var actors = new[] { maker, approver, dispatcher, receiver, closer };
        var sourceWarehouse = new Warehouse
        {
            Id = sourceWarehouseId,
            TenantId = tenantId,
            Code = "SQL-SOURCE",
            Name = "SQL source warehouse",
            IsActive = true
        };
        var destinationWarehouse = new Warehouse
        {
            Id = destinationWarehouseId,
            TenantId = tenantId,
            Code = "SQL-DEST",
            Name = "SQL destination warehouse",
            IsActive = true
        };
        var otherTenantSource = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Code = "OTHER-SOURCE",
            Name = "Other tenant source",
            IsActive = true
        };
        var otherTenantDestination = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Code = "OTHER-DEST",
            Name = "Other tenant destination",
            IsActive = true
        };

        context.AddRange(
            new Tenant
            {
                Id = tenantId,
                Name = "Inventory SQL tenant",
                Code = $"INV-{tenantId:N}"[..24],
                BaseCurrency = "GHS"
            },
            new Tenant
            {
                Id = otherTenantId,
                Name = "Other Inventory SQL tenant",
                Code = $"INV-{otherTenantId:N}"[..24],
                BaseCurrency = "GHS"
            },
            new InventoryCategory
            {
                Id = categoryId,
                TenantId = tenantId,
                Code = "SQL-STOCK",
                Name = "SQL stock"
            },
            sourceWarehouse,
            destinationWarehouse,
            otherTenantSource,
            otherTenantDestination,
            new InventoryItem
            {
                Id = itemId,
                TenantId = tenantId,
                CategoryId = categoryId,
                ItemCode = "SQL-ITEM-001",
                Name = "SQL inventory item",
                UnitOfMeasure = "EA",
                CurrentStock = 5m,
                AvailableStock = 5m
            },
            new WarehouseQuantity
            {
                Id = sourceBalanceId,
                TenantId = tenantId,
                InventoryItemId = itemId,
                WarehouseId = sourceWarehouseId,
                CurrentStock = 10m,
                AvailableStock = 10m,
                AllocatedStock = 0m
            },
            new WarehouseQuantity
            {
                Id = destinationBalanceId,
                TenantId = tenantId,
                InventoryItemId = itemId,
                WarehouseId = destinationWarehouseId,
                CurrentStock = 0m,
                AvailableStock = 0m,
                AllocatedStock = 0m
            },
            maker,
            approver,
            dispatcher,
            receiver,
            closer,
            unauthorized,
            otherTenantMaker);
        context.UserTenants.AddRange(actors.Select(user => new UserTenant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Status = UserTenantStatus.Active,
            GrantedAt = DateTime.UtcNow.AddDays(-1)
        }));
        context.UserTenants.Add(new UserTenant
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            UserId = otherTenantMaker.Id,
            Status = UserTenantStatus.Active,
            GrantedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var negativeMutation = async () => await database.ExecuteAsync(
            "UPDATE dbo.WarehouseQuantities SET CurrentStock = -1, AvailableStock = -1 WHERE Id = @id;",
            new SqlParameter("@id", sourceBalanceId));
        var stockError = await negativeMutation.Should().ThrowAsync<SqlException>();
        stockError.Which.Number.Should().Be(51060);
        stockError.Which.Message.Should().Contain("INV_NEGATIVE_STOCK_SQL_PROHIBITED");

        context.ChangeTracker.Clear();
        context.InventoryMovements.Add(NewMovement(tenantId, itemId, sourceWarehouseId, "MOV-SQL-001"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.InventoryMovements.Add(NewMovement(tenantId, itemId, sourceWarehouseId, "MOV-SQL-001"));
        var replay = async () => await context.SaveChangesAsync();
        (await replay.Should().ThrowAsync<DbUpdateException>()).Which.InnerException
            .Should().BeOfType<SqlException>().Which.Number.Should().Be(2601);

        context.ChangeTracker.Clear();
        var transfer = new InventoryTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransferNumber = "TRF-SQL-001",
            SourceWarehouseId = sourceWarehouseId,
            DestinationWarehouseId = destinationWarehouseId,
            RequestDate = DateTime.UtcNow,
            Status = TransferStatus.Draft,
            RequestedById = maker.Id,
            TotalItems = 1,
            TotalQuantity = 5m,
            TotalValue = 50m
        };
        var transferLine = new InventoryTransferItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InventoryTransferId = transfer.Id,
            InventoryItemId = itemId,
            RequestedQuantity = 5m,
            UnitOfMeasure = "EA",
            UnitCost = 10m,
            LineValue = 50m
        };
        context.AddRange(transfer, transferLine);
        await context.SaveChangesAsync();

        var otherTransfer = new InventoryTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            TransferNumber = "TRF-OTHER-001",
            SourceWarehouseId = otherTenantSource.Id,
            DestinationWarehouseId = otherTenantDestination.Id,
            RequestDate = DateTime.UtcNow,
            Status = TransferStatus.Draft,
            RequestedById = otherTenantMaker.Id
        };
        context.InventoryTransfers.Add(otherTransfer);
        await context.SaveChangesAsync();
        (await context.InventoryTransfers.CountAsync()).Should().Be(1);
        (await context.InventoryTransfers.IgnoreQueryFilters().CountAsync()).Should().Be(2);

        var unauthorizedAction = NewAction(tenantId, transfer.Id, 1,
            InventoryTransferActionType.Submitted, unauthorized.Id, "unauthorized-submit");
        context.InventoryTransferActions.Add(unauthorizedAction);
        var unauthorizedAttempt = async () => await context.SaveChangesAsync();
        var unauthorizedError = await unauthorizedAttempt.Should().ThrowAsync<DbUpdateException>();
        unauthorizedError.Which.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().Be(51802);
        context.ChangeTracker.Clear();

        transfer = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        var submitAction = NewAction(tenantId, transfer.Id, 1,
            InventoryTransferActionType.Submitted, maker.Id, "submit-1");
        context.InventoryTransferActions.Add(submitAction);
        await context.SaveChangesAsync();
        transfer.Status = TransferStatus.Submitted;
        await context.SaveChangesAsync();

        await using (var selfApprovalTransaction = await context.Database.BeginTransactionAsync())
        {
            var selfApprovalAction = NewAction(tenantId, transfer.Id, 2,
                InventoryTransferActionType.Approved, maker.Id, "self-approve");
            context.InventoryTransferActions.Add(selfApprovalAction);
            await context.SaveChangesAsync();
            transfer.ApprovedById = maker.Id;
            transfer.ApprovalDate = DateTime.UtcNow;
            transfer.Status = TransferStatus.Approved;
            var selfApproval = async () => await context.SaveChangesAsync();
            var selfApprovalError = await selfApproval.Should().ThrowAsync<DbUpdateException>();
            selfApprovalError.Which.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().Be(51854);
            await selfApprovalTransaction.RollbackAsync();
        }

        context.ChangeTracker.Clear();
        transfer = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        transfer.Status.Should().Be(TransferStatus.Submitted);
        (await context.InventoryTransferActions.CountAsync(value => value.InventoryTransferId == transfer.Id))
            .Should().Be(1);

        transfer.TotalValue = 999m;
        var amendment = async () => await context.SaveChangesAsync();
        var amendmentError = await amendment.Should().ThrowAsync<DbUpdateException>();
        amendmentError.Which.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().Be(51853);
        context.ChangeTracker.Clear();

        transfer = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        await using (var approvalTransaction = await context.Database.BeginTransactionAsync())
        {
            var approveAction = NewAction(tenantId, transfer.Id, 2,
                InventoryTransferActionType.Approved, approver.Id, "approve-1");
            context.InventoryTransferActions.Add(approveAction);
            await context.SaveChangesAsync();
            transfer.ApprovedById = approver.Id;
            transfer.ApprovalDate = DateTime.UtcNow;
            transfer.Status = TransferStatus.Approved;
            await context.SaveChangesAsync();
            await approvalTransaction.CommitAsync();
        }

        context.ChangeTracker.Clear();
        transfer = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        transferLine = await context.InventoryTransferItems.SingleAsync(value => value.Id == transferLine.Id);
        var sourceBalance = await context.WarehouseQuantities.SingleAsync(value => value.Id == sourceBalanceId);
        await using (var dispatchTransaction = await context.Database.BeginTransactionAsync())
        {
            var dispatchAction = NewAction(tenantId, transfer.Id, 3,
                InventoryTransferActionType.Dispatched, dispatcher.Id, "dispatch-1");
            context.InventoryTransferActions.Add(dispatchAction);
            await context.SaveChangesAsync();
            context.InventoryTransferActionLines.Add(NewActionLine(tenantId, dispatchAction.Id, transferLine.Id,
                dispatchedQuantity: 5m));
            await context.SaveChangesAsync();
            transferLine.ShippedQuantity = 5m;
            await context.SaveChangesAsync();
            context.StockMovements.Add(NewStockMovement(tenantId, itemId, sourceWarehouseId, transfer,
                dispatcher.Id, "TransferOut", -5m, 5m));
            sourceBalance.CurrentStock = 5m;
            sourceBalance.AvailableStock = 5m;
            await context.SaveChangesAsync();
            transfer.ShippedById = dispatcher.Id;
            transfer.ShippedDate = DateTime.UtcNow;
            transfer.Status = TransferStatus.InTransit;
            await context.SaveChangesAsync();
            await dispatchTransaction.CommitAsync();
        }

        context.ChangeTracker.Clear();
        transfer = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        transferLine = await context.InventoryTransferItems.SingleAsync(value => value.Id == transferLine.Id);
        var destinationBalance = await context.WarehouseQuantities.SingleAsync(value => value.Id == destinationBalanceId);
        await using (var receiveTransaction = await context.Database.BeginTransactionAsync())
        {
            var receiveAction = NewAction(tenantId, transfer.Id, 4,
                InventoryTransferActionType.Received, receiver.Id, "receive-1");
            context.InventoryTransferActions.Add(receiveAction);
            await context.SaveChangesAsync();
            context.InventoryTransferActionLines.Add(NewActionLine(tenantId, receiveAction.Id, transferLine.Id,
                receivedQuantity: 5m));
            await context.SaveChangesAsync();
            transferLine.ReceivedQuantity = 5m;
            await context.SaveChangesAsync();
            context.StockMovements.Add(NewStockMovement(tenantId, itemId, destinationWarehouseId, transfer,
                receiver.Id, "TransferIn", 5m, 5m));
            destinationBalance.CurrentStock = 5m;
            destinationBalance.AvailableStock = 5m;
            await context.SaveChangesAsync();
            transfer.ReceivedById = receiver.Id;
            transfer.ReceivedDate = DateTime.UtcNow;
            transfer.Status = TransferStatus.Received;
            await context.SaveChangesAsync();
            await receiveTransaction.CommitAsync();
        }

        context.ChangeTracker.Clear();
        transfer = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        await using (var closeTransaction = await context.Database.BeginTransactionAsync())
        {
            context.InventoryTransferActions.Add(NewAction(tenantId, transfer.Id, 5,
                InventoryTransferActionType.Closed, closer.Id, "close-1"));
            await context.SaveChangesAsync();
            transfer.ClosedById = closer.Id;
            transfer.CompletedDate = DateTime.UtcNow;
            transfer.Status = TransferStatus.Completed;
            await context.SaveChangesAsync();
            await closeTransaction.CommitAsync();
        }

        context.ChangeTracker.Clear();
        var completed = await context.InventoryTransfers.SingleAsync(value => value.Id == transfer.Id);
        completed.Status.Should().Be(TransferStatus.Completed);
        completed.RequestedById.Should().Be(maker.Id);
        completed.ApprovedById.Should().Be(approver.Id);
        completed.ShippedById.Should().Be(dispatcher.Id);
        completed.ReceivedById.Should().Be(receiver.Id);
        completed.ClosedById.Should().Be(closer.Id);
        (await context.InventoryTransferActions.CountAsync(value => value.InventoryTransferId == transfer.Id))
            .Should().Be(5);
        (await context.StockMovements.CountAsync(value => value.ReferenceId == transfer.Id)).Should().Be(2);
        (await context.WarehouseQuantities.SingleAsync(value => value.Id == sourceBalanceId)).CurrentStock.Should().Be(5m);
        (await context.WarehouseQuantities.SingleAsync(value => value.Id == destinationBalanceId)).CurrentStock.Should().Be(5m);

        context.InventoryTransferActions.Add(NewAction(tenantId, transfer.Id, 6,
            InventoryTransferActionType.Approved, approver.Id, "approve-1"));
        var actionReplay = async () => await context.SaveChangesAsync();
        var actionReplayError = await actionReplay.Should().ThrowAsync<DbUpdateException>();
        actionReplayError.Which.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().BeOneOf(2601, 2627);
    }

    private static InventoryMovement NewMovement(Guid tenantId, Guid itemId, Guid warehouseId, string number) => new()
    {
        TenantId = tenantId,
        MovementNumber = number,
        InventoryItemId = itemId,
        WarehouseId = warehouseId,
        MovementType = InventoryMovementType.AdjustmentIn,
        Direction = MovementDirection.In,
        Quantity = 1m,
        UnitCost = 10m,
        TotalValue = 10m,
        MovementDate = DateTime.UtcNow,
        PostingDate = DateTime.UtcNow,
        ReferenceType = ReferenceType.Adjustment,
        ReferenceNumber = "SQL-INVENTORY-GATE",
        RunningBalance = 6m,
        RunningValue = 60m,
        IsPosted = true,
        PostedAt = DateTime.UtcNow
    };

    private static ApplicationUser NewUser(Guid tenantId, string label)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var username = $"{label}.{suffix}@inventory.test";
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = username,
            NormalizedEmail = username.ToUpperInvariant(),
            FirstName = label,
            LastName = "Inventory",
            IsActive = true
        };
    }

    private static InventoryTransferAction NewAction(
        Guid tenantId,
        Guid transferId,
        int sequence,
        InventoryTransferActionType actionType,
        Guid actorId,
        string idempotencyKey) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        InventoryTransferId = transferId,
        Sequence = sequence,
        ActionType = actionType,
        ActorUserId = actorId,
        OccurredAtUtc = DateTime.UtcNow,
        IdempotencyKey = idempotencyKey,
        PayloadHash = new string('a', 64),
        CorrelationId = $"sql-{idempotencyKey}",
        SnapshotJson = "{}",
        IntegrityHash = new string('b', 64)
    };

    private static InventoryTransferActionLine NewActionLine(
        Guid tenantId,
        Guid actionId,
        Guid transferLineId,
        decimal dispatchedQuantity = 0m,
        decimal receivedQuantity = 0m) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        InventoryTransferActionId = actionId,
        InventoryTransferItemId = transferLineId,
        DispatchedQuantity = dispatchedQuantity,
        ReceivedQuantity = receivedQuantity,
        IntegrityHash = new string('c', 64)
    };

    private static StockMovement NewStockMovement(
        Guid tenantId,
        Guid itemId,
        Guid warehouseId,
        InventoryTransfer transfer,
        Guid actorId,
        string movementType,
        decimal quantity,
        decimal runningBalance) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        InventoryItemId = itemId,
        WarehouseId = warehouseId,
        MovementType = movementType,
        Quantity = quantity,
        UnitCost = 10m,
        TotalValue = quantity * 10m,
        MovementDate = DateTime.UtcNow,
        ReferenceType = ReferenceType.Transfer,
        ReferenceNumber = transfer.TransferNumber,
        ReferenceId = transfer.Id,
        RunningBalance = runningBalance,
        RunningValue = runningBalance * 10m,
        ProcessedById = actorId
    };

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")))
                Skip = "Set RHEMA_TEST_SQLSERVER to run the disposable Inventory/Stores SQL Server gate.";
        }
    }

    private sealed class DisposableSqlDatabase : IAsyncDisposable
    {
        private readonly string _databaseName;
        private readonly string _masterConnectionString;

        private DisposableSqlDatabase(string databaseName, string connectionString, string masterConnectionString)
        {
            _databaseName = databaseName;
            ConnectionString = connectionString;
            _masterConnectionString = masterConnectionString;
        }

        public string ConnectionString { get; }

        public static async Task<DisposableSqlDatabase> CreateAsync()
        {
            var baseConnection = Environment.GetEnvironmentVariable("RHEMA_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("RHEMA_TEST_SQLSERVER is required.");
            var databaseName = $"RhemaERP_InventoryArchitecture_{Guid.NewGuid():N}";
            var masterBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = "master",
                TrustServerCertificate = true
            };
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = databaseName,
                TrustServerCertificate = true
            };
            var result = new DisposableSqlDatabase(databaseName, databaseBuilder.ConnectionString,
                masterBuilder.ConnectionString);
            await using var master = new SqlConnection(result._masterConnectionString);
            await master.OpenAsync();
            await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
            return result;
        }

        public async Task ApplySqlOperationsAsync(Migration migration)
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(migration, [builder]);
            foreach (var operation in builder.Operations.OfType<SqlOperation>())
                await ExecuteAsync(operation.Sql);
        }

        public async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (parameters.Length > 0) command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task ExecuteAsync(SqlConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var master = new SqlConnection(_masterConnectionString);
            await master.OpenAsync();
            await ExecuteAsync(master,
                $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;");
        }
    }
}
