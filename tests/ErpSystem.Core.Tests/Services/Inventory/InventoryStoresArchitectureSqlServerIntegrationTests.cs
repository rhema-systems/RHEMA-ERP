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
    public async Task Warehouse_balance_cannot_bypass_negative_stock_and_movement_numbers_are_replay_safe()
    {
        await using var database = await DisposableSqlDatabase.CreateAsync();
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var balanceId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        await context.Database.EnsureCreatedAsync();
        await database.ApplySqlOperationsAsync(new TDC0610NegativeStockControl());

        context.AddRange(
            new Tenant
            {
                Id = tenantId,
                Name = "Inventory SQL tenant",
                Code = $"INV-{tenantId:N}"[..24],
                BaseCurrency = "GHS"
            },
            new InventoryCategory
            {
                Id = categoryId,
                TenantId = tenantId,
                Code = "SQL-STOCK",
                Name = "SQL stock"
            },
            new Warehouse
            {
                Id = warehouseId,
                TenantId = tenantId,
                Code = "SQL-WH",
                Name = "SQL warehouse",
                IsActive = true
            },
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
                Id = balanceId,
                TenantId = tenantId,
                InventoryItemId = itemId,
                WarehouseId = warehouseId,
                CurrentStock = 5m,
                AvailableStock = 5m,
                AllocatedStock = 0m
            });
        await context.SaveChangesAsync();

        var negativeMutation = async () => await database.ExecuteAsync(
            "UPDATE dbo.WarehouseQuantities SET CurrentStock = -1, AvailableStock = -1 WHERE Id = @id;",
            new SqlParameter("@id", balanceId));
        var stockError = await negativeMutation.Should().ThrowAsync<SqlException>();
        stockError.Which.Number.Should().Be(51060);
        stockError.Which.Message.Should().Contain("INV_NEGATIVE_STOCK_SQL_PROHIBITED");

        context.ChangeTracker.Clear();
        context.InventoryMovements.Add(NewMovement(tenantId, itemId, warehouseId, "MOV-SQL-001"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.InventoryMovements.Add(NewMovement(tenantId, itemId, warehouseId, "MOV-SQL-001"));
        var replay = async () => await context.SaveChangesAsync();
        (await replay.Should().ThrowAsync<DbUpdateException>()).Which.InnerException
            .Should().BeOfType<SqlException>().Which.Number.Should().Be(2601);
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
