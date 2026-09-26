using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSodSqlTests
{
    [ProcurementLocalDbFact]
    public async Task SqlReceiptGuardsAndApClassificationRespectTenantScopeWithoutRemovingLineageChecks()
    {
        var databaseName = "TdcProcurementSodTest_" + Guid.NewGuid().ToString("N");
        var masterSettings = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        await using var master = new SqlConnection(masterSettings.ConnectionString);
        await master.OpenAsync();
        await Execute(master, $"CREATE DATABASE [{databaseName}]");
        try
        {
            var settings = new SqlConnectionStringBuilder(masterSettings.ConnectionString) { InitialCatalog = databaseName };
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await Execute(connection, """
                CREATE TABLE ProcurementSettings (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE PurchaseOrders (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, Status nvarchar(32), CreatedById uniqueidentifier NULL, RequestedById uniqueidentifier NULL, ApprovedById uniqueidentifier NULL);
                CREATE TABLE PurchaseOrderReceipts (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, PurchaseOrderId uniqueidentifier, ReceivedById uniqueidentifier NULL);
                CREATE TABLE VendorInvoice (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, PurchaseOrderId uniqueidentifier NULL, AcceptedSupplyKind int NULL);
                CREATE TABLE VendorInvoiceLineItem (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, VendorInvoiceId uniqueidentifier, LandedCostItemId uniqueidentifier NULL);
                CREATE TABLE PaymentBatchInvoice (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, PaymentBatchId uniqueidentifier, VendorInvoiceId uniqueidentifier);
                CREATE TABLE VendorPaymentAllocation (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, VendorPaymentId uniqueidentifier, VendorInvoiceId uniqueidentifier, IsReversal bit NOT NULL DEFAULT 0, OriginalAllocationId uniqueidentifier NULL);
                INSERT ProcurementSettings(Id,TenantId) VALUES(NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
                """);
            var migration = new ProcurementSodConfiguration();
            using var generation = new DbContext(new DbContextOptionsBuilder().UseSqlServer(settings.ConnectionString).Options);
            var generator = generation.GetService<IMigrationsSqlGenerator>();
            foreach (var command in generator.Generate(migration.UpOperations.OfType<AddColumnOperation>().ToArray()))
                await Execute(connection, command.CommandText);
            var sql = migration.UpOperations.OfType<SqlOperation>().Select(operation => operation.Sql).ToArray();
            sql.Count(command => command.Contains("CREATE OR ALTER TRIGGER")).Should().Be(15);
            await Execute(connection, "SET PARSEONLY ON");
            try
            {
                foreach (var command in sql) await Execute(connection, command);
            }
            finally { await Execute(connection, "SET PARSEONLY OFF"); }
            foreach (var command in sql.Where(command => command.Contains("CREATE OR ALTER FUNCTION") || command.Contains("TR_PurchaseOrders_SodHardStop") || command.Contains("TR_PurchaseOrderReceipts_SodHardStop")))
                await Execute(connection, command);
            (await Scalar(connection, "SELECT dbo.ProcurementSodRequired('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')")).Should().Be(true);
            await Execute(connection, """
                INSERT PurchaseOrders(Id,TenantId,Status,CreatedById,RequestedById)
                VALUES('11111111-1111-1111-1111-111111111111','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Draft','cccccccc-cccc-cccc-cccc-cccccccccccc','cccccccc-cccc-cccc-cccc-cccccccccccc');
                """);
            const string sameActorReceipt = "INSERT PurchaseOrderReceipts(Id,TenantId,PurchaseOrderId,ReceivedById) VALUES(NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','11111111-1111-1111-1111-111111111111','cccccccc-cccc-cccc-cccc-cccccccccccc')";
            Func<Task> enabledReceipt = () => Execute(connection, sameActorReceipt);
            (await enabledReceipt.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51261);
            await Execute(connection, "UPDATE ProcurementSettings SET EnforceSegregationOfDuties=0");
            await Execute(connection, sameActorReceipt);
            await Execute(connection, "UPDATE PurchaseOrders SET Status='Approved',ApprovedById=CreatedById");
            Func<Task> missingReceiver = () => Execute(connection, "INSERT PurchaseOrderReceipts(Id,TenantId,PurchaseOrderId) VALUES(NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','11111111-1111-1111-1111-111111111111')");
            (await missingReceiver.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51261);
            (await Scalar(connection, "SELECT dbo.ProcurementSodRequired('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb')")).Should().Be(true);
            await Execute(connection, """
                INSERT VendorInvoice(Id,TenantId,PurchaseOrderId) VALUES('22222222-2222-2222-2222-222222222222','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','11111111-1111-1111-1111-111111111111');
                INSERT VendorInvoice(Id,TenantId) VALUES('33333333-3333-3333-3333-333333333333','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
                INSERT VendorPaymentAllocation(Id,TenantId,VendorPaymentId,VendorInvoiceId) VALUES('44444444-4444-4444-4444-444444444444','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','55555555-5555-5555-5555-555555555555','22222222-2222-2222-2222-222222222222');
                """);
            const string paymentPolicy = "SELECT dbo.ProcurementApSodRequired('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','VendorPayment','55555555-5555-5555-5555-555555555555')";
            (await Scalar(connection, paymentPolicy)).Should().Be(false);
            await Execute(connection, "INSERT VendorPaymentAllocation(Id,TenantId,VendorPaymentId,VendorInvoiceId) VALUES('66666666-6666-6666-6666-666666666666','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','55555555-5555-5555-5555-555555555555','33333333-3333-3333-3333-333333333333')");
            (await Scalar(connection, paymentPolicy)).Should().Be(true);
            await Execute(connection, "INSERT VendorPaymentAllocation(Id,TenantId,VendorPaymentId,VendorInvoiceId,IsReversal,OriginalAllocationId) VALUES(NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','55555555-5555-5555-5555-555555555555','33333333-3333-3333-3333-333333333333',1,'66666666-6666-6666-6666-666666666666')");
            (await Scalar(connection, paymentPolicy)).Should().Be(false);
            (await Scalar(connection, "SELECT dbo.ProcurementApSodRequired('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','VendorInvoice','33333333-3333-3333-3333-333333333333')")).Should().Be(true);
            // Restoring the original guard must enforce separation again, even while the setting remains off.
            foreach (var command in migration.DownOperations.OfType<SqlOperation>().Where(operation => operation.Sql.Contains("TR_PurchaseOrderReceipts_SodHardStop")))
                await Execute(connection, command.Sql);
            (await enabledReceipt.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51261);
        }
        finally
        {
            await Execute(master, $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]");
        }
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }
    private static async Task<object?> Scalar(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return await command.ExecuteScalarAsync();
    }
}
