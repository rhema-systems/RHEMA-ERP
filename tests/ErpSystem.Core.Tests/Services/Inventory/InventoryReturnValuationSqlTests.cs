using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryReturnValuationSqlTests
{
    [InventoryReturnSqlFact]
    public async Task Return_trigger_validates_original_cost_tracking_quantity_and_immutability_on_real_sql()
    {
        var settings = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TDC_RETURN_SQL_TEST_CONNECTION"));
        settings.InitialCatalog = "master";
        var database = "TdcReturnValuation_" + Guid.NewGuid().ToString("N");
        await using var master = new SqlConnection(settings.ConnectionString);
        await master.OpenAsync();
        await Execute(master, $"CREATE DATABASE [{database}]");
        try
        {
            settings.InitialCatalog = database;
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await Execute(connection, Schema);
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            var migration = new AlignStoreReturnWithOriginalIssueValuation();
            migration.GetType().GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(migration, [builder]);
            builder.Operations.Should().ContainSingle().Which.Should().BeOfType<SqlOperation>();
            await Execute(connection, ((SqlOperation)builder.Operations.Single()).Sql);
            await Execute(connection, Seed);
            await Insert(connection, 1900, 1900); // correct actual cost, despite estimate 2000
            await Rejected(() => Insert(connection, 2000, 2000)); // stale request estimate
            await Rejected(() => Insert(connection, 1900, 1900, quantity: 3)); // exceeds issue
            await Rejected(() => Insert(connection, 1900, 1900, serial: "wrong"));
            await Rejected(() => Insert(connection, 1900, 1900, mutation: "UPDATE InventoryReturnVoucherLines SET UnitCost=2000"), 51651);
            await Rejected(() => Insert(connection, 1900, 1900, mutation: "DELETE InventoryReturnVoucherLines"), 51651);
            await Execute(connection, "UPDATE InventoryIssueFinanceLineages SET TenantId=NEWID()");
            await Rejected(() => Insert(connection, 1900, 1900));
            await Execute(connection, "UPDATE InventoryIssueFinanceLineages SET TenantId='00000000-0000-0000-0000-000000000001',ReturnedQuantity=1; UPDATE InventoryRequisitionItems SET IssuedQuantity=1");
            await Insert(connection, 1900, 1900); // remaining partial return
            await Execute(connection, """
                DECLARE @newLine uniqueidentifier=NEWID();
                INSERT InventoryIssueVoucherLines
                    SELECT @newLine,TenantId,InventoryIssueVoucherId,InventoryRequisitionItemId,NULL,NULL,NULL,0 FROM InventoryIssueVoucherLines;
                INSERT InventoryIssueFinanceLineages
                    SELECT NEWID(),TenantId,@newLine,2,0,3900,DATEADD(SECOND,1,SYSUTCDATETIME()),0 FROM InventoryIssueFinanceLineages;
                UPDATE InventoryRequisitionItems SET IssuedQuantity=3;
                """);
            await Insert(connection, 1925, 3850, quantity: 2); // 1 remaining original unit + 1 later unit
            await Rejected(() => Insert(connection, 1950, 3900, quantity: 2));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            // Only the unique disposable database created above is removed; never the configured catalogue.
            await Execute(master, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }

    private static async Task Rejected(Func<Task> action, int number = 51652)
    {
        var error = await action.Should().ThrowAsync<SqlException>();
        error.Which.Number.Should().Be(number);
    }

    private static async Task Insert(SqlConnection connection, decimal cost, decimal value, int quantity = 1,
        string? serial = null, string? mutation = null)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT InventoryReturnVoucherLines
                (Id,TenantId,InventoryReturnVoucherId,InventoryRequisitionItemId,InventoryItemId,LocationId,Quantity,UnitCost,TotalValue,IntegrityHash,IsDeleted,SerialNumber)
                SELECT NEWID(),v.TenantId,v.Id,r.Id,r.InventoryItemId,l.Id,@qty,@cost,@value,REPLICATE('a',64),0,@serial
                FROM InventoryReturnVouchers v CROSS JOIN InventoryRequisitionItems r CROSS JOIN WarehouseLocations l;
                """ + mutation;
            command.Parameters.AddWithValue("@qty", quantity);
            command.Parameters.AddWithValue("@cost", cost);
            command.Parameters.AddWithValue("@value", value);
            command.Parameters.AddWithValue("@serial", (object?)serial ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
        }
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();
    }

    private const string Schema = """
        CREATE TABLE InventoryReturnVouchers(Id uniqueidentifier,TenantId uniqueidentifier,InventoryRequisitionId uniqueidentifier,WarehouseId uniqueidentifier,Status int,IsDeleted bit);
        CREATE TABLE InventoryRequisitionItems(Id uniqueidentifier,TenantId uniqueidentifier,InventoryRequisitionId uniqueidentifier,InventoryItemId uniqueidentifier,UnitCost decimal(18,4),IssuedQuantity decimal(18,4),IsDeleted bit);
        CREATE TABLE InventoryItems(Id uniqueidentifier,TenantId uniqueidentifier,IsDeleted bit);
        CREATE TABLE WarehouseLocations(Id uniqueidentifier,TenantId uniqueidentifier,WarehouseId uniqueidentifier,IsDeleted bit,IsActive bit);
        CREATE TABLE InventoryReturnVoucherLines(Id uniqueidentifier,TenantId uniqueidentifier,InventoryReturnVoucherId uniqueidentifier,InventoryRequisitionItemId uniqueidentifier,InventoryItemId uniqueidentifier,LocationId uniqueidentifier,Quantity decimal(18,4),UnitCost decimal(18,4),TotalValue decimal(18,2),SerialNumber nvarchar(100),LotNumber nvarchar(100),BatchNumber nvarchar(100),IntegrityHash varchar(64),IsDeleted bit);
        CREATE TABLE InventoryIssueVouchers(Id uniqueidentifier,TenantId uniqueidentifier,InventoryRequisitionId uniqueidentifier,IssuedAtUtc datetime2,IsDeleted bit);
        CREATE TABLE InventoryIssueVoucherLines(Id uniqueidentifier,TenantId uniqueidentifier,InventoryIssueVoucherId uniqueidentifier,InventoryRequisitionItemId uniqueidentifier,SerialNumber nvarchar(100),LotNumber nvarchar(100),BatchNumber nvarchar(100),IsDeleted bit);
        CREATE TABLE InventoryIssueFinanceLineages(Id uniqueidentifier,TenantId uniqueidentifier,InventoryIssueVoucherLineId uniqueidentifier,IssuedQuantity decimal(18,4),ReturnedQuantity decimal(18,4),IssuedValue decimal(18,2),CreatedAt datetime2,IsDeleted bit);
        """;

    private const string Seed = """
        DECLARE @tenant uniqueidentifier='00000000-0000-0000-0000-000000000001',@req uniqueidentifier=NEWID(),@item uniqueidentifier=NEWID(),@line uniqueidentifier=NEWID(),@wh uniqueidentifier=NEWID(),@issue uniqueidentifier=NEWID(),@il uniqueidentifier=NEWID();
        INSERT InventoryItems VALUES (@item,@tenant,0);
        INSERT WarehouseLocations VALUES(NEWID(),@tenant,@wh,0,1);
        INSERT InventoryRequisitionItems VALUES(@line,@tenant,@req,@item,2000,2,0);
        INSERT InventoryReturnVouchers VALUES(NEWID(),@tenant,@req,@wh,1,0);
        INSERT InventoryIssueVouchers VALUES(@issue,@tenant,@req,SYSUTCDATETIME(),0);
        INSERT InventoryIssueVoucherLines VALUES(@il,@tenant,@issue,@line,NULL,NULL,NULL,0);
        INSERT InventoryIssueFinanceLineages VALUES(NEWID(),@tenant,@il,2,0,3800,SYSUTCDATETIME(),0);
        """;
}

public sealed class InventoryReturnSqlFactAttribute : FactAttribute
{
    public InventoryReturnSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TDC_RETURN_SQL_TEST_CONNECTION")))
            Skip = "Set TDC_RETURN_SQL_TEST_CONNECTION for the isolated real SQL trigger test.";
    }
}
