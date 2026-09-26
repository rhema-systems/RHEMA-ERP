using ErpSystem.Data.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAutoInvoiceSqlTests
{
    [ProcurementLocalDbFact]
    public async Task MigrationPreservesManualInvoicesAndGuardsReceiptAuthorityAndRollback()
    {
        var database = "TdcAutoInvoiceTest_" + Guid.NewGuid().ToString("N");
        var connectionString = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master",
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        await using var master = new SqlConnection(connectionString.ConnectionString); await master.OpenAsync();
        await Execute(master, $"CREATE DATABASE [{database}]");
        try
        {
            connectionString.InitialCatalog = database;
            await using var sql = new SqlConnection(connectionString.ConnectionString); await sql.OpenAsync();
            await Execute(sql, "CREATE TABLE Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY)");
            foreach (var table in new Dictionary<string, string>
            {
                ["VendorInvoice"] = "Status int NOT NULL DEFAULT 1, PurchaseOrderId uniqueidentifier NULL, CurrencyCode nvarchar(10) NOT NULL DEFAULT 'GHS', IsOpeningBalance bit NOT NULL DEFAULT 0, AcceptedSupplyKind int NULL, AcceptedSupplySourceId uniqueidentifier NULL, AcceptedSupplySourceReference nvarchar(100) NULL, AcceptedSupplySnapshotHash nvarchar(64) NULL, AcceptedSupplyValidatedAtUtc datetime2 NULL, MatchingControlEventId uniqueidentifier NULL, MatchingStatus int NOT NULL DEFAULT 0, MatchingSnapshotHash nvarchar(64) NULL",
                ["VendorInvoiceLineItem"] = "VendorInvoiceId uniqueidentifier NOT NULL, PurchaseOrderItemId uniqueidentifier NULL, Quantity decimal(18,4) NOT NULL",
                ["PurchaseOrders"] = "Currency nvarchar(10) NOT NULL DEFAULT 'GHS'",
                ["PurchaseOrderItems"] = "PurchaseOrderId uniqueidentifier NOT NULL",
                ["PurchaseOrderReceipts"] = "PurchaseOrderId uniqueidentifier NOT NULL",
                ["PurchaseOrderReceiptItems"] = "ReceiptId uniqueidentifier NOT NULL, PurchaseOrderItemId uniqueidentifier NOT NULL",
                ["ProcurementReceiptInspectionCases"] = "PurchaseOrderReceiptId uniqueidentifier NOT NULL",
                ["GoodsReceiptNotes"] = "PurchaseOrderReceiptId uniqueidentifier NULL, PurchaseOrderId uniqueidentifier NULL",
                ["GoodsReceiptNoteItems"] = "GoodsReceiptNoteId uniqueidentifier NOT NULL, PurchaseOrderItemId uniqueidentifier NULL",
                ["ProcurementControlEvents"] = "SourceId uniqueidentifier NOT NULL, SourceType nvarchar(100) NOT NULL, EventType nvarchar(100) NOT NULL, Action nvarchar(100) NOT NULL, RuleCode nvarchar(100) NOT NULL, RuleVersion nvarchar(100) NOT NULL, Result int NOT NULL, ResultValuesJson nvarchar(max) NULL, DecisionKeysJson nvarchar(max) NULL"
            }) await Execute(sql, $"CREATE TABLE [{table.Key}] (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0, {table.Value})");
            await Execute(sql, "ALTER TABLE VendorInvoice ADD CONSTRAINT CK_VendorInvoice_AcceptedSupplyCoherent CHECK (AcceptedSupplyKind IS NULL OR AcceptedSupplyKind BETWEEN 1 AND 3); ALTER TABLE VendorInvoice ADD CONSTRAINT CK_VendorInvoice_AcceptedSupplyPurchaseOrder CHECK (AcceptedSupplyKind IS NULL OR AcceptedSupplyKind=3 OR PurchaseOrderId IS NOT NULL)");
            var tenant = Guid.NewGuid(); var foreign = Guid.NewGuid(); var manual = Guid.NewGuid();
            await Execute(sql, $"INSERT Tenants VALUES ('{tenant}'),('{foreign}'); INSERT VendorInvoice (Id,TenantId) VALUES ('{manual}','{tenant}')");
            using var generation = new DbContext(new DbContextOptionsBuilder().UseSqlServer(connectionString.ConnectionString).Options);
            var generator = generation.GetService<IMigrationsSqlGenerator>();
            var migration = new ProcurementAutoInvoiceReceipts();
            async Task Migrate(bool up)
            {
                foreach (var command in generator.Generate(up ? migration.UpOperations : migration.DownOperations)) await Execute(sql, command.CommandText);
            }
            await Migrate(true); await Migrate(false); await Migrate(true);
            await Execute(sql, $"UPDATE VendorInvoice SET Status=3 WHERE Id='{manual}'");
            var invoice = Guid.NewGuid(); var line = Guid.NewGuid(); var po = Guid.NewGuid(); var pi = Guid.NewGuid();
            var receipt = Guid.NewGuid(); var ri = Guid.NewGuid(); var inspection = Guid.NewGuid(); var grn = Guid.NewGuid(); var gi = Guid.NewGuid();
            var request = Guid.NewGuid(); var hash = new string('a', 64); var allocation = Guid.NewGuid();
            await Execute(sql, $"""
                INSERT PurchaseOrders (Id,TenantId) VALUES ('{po}','{tenant}');
                INSERT PurchaseOrderItems (Id,TenantId,PurchaseOrderId) VALUES ('{pi}','{tenant}','{po}');
                INSERT PurchaseOrderReceipts (Id,TenantId,PurchaseOrderId) VALUES ('{receipt}','{tenant}','{po}');
                INSERT PurchaseOrderReceiptItems (Id,TenantId,ReceiptId,PurchaseOrderItemId) VALUES ('{ri}','{tenant}','{receipt}','{pi}');
                INSERT ProcurementReceiptInspectionCases (Id,TenantId,PurchaseOrderReceiptId) VALUES ('{inspection}','{tenant}','{receipt}');
                INSERT GoodsReceiptNotes (Id,TenantId,PurchaseOrderReceiptId,PurchaseOrderId) VALUES ('{grn}','{tenant}','{receipt}','{po}');
                INSERT GoodsReceiptNoteItems (Id,TenantId,GoodsReceiptNoteId,PurchaseOrderItemId) VALUES ('{gi}','{tenant}','{grn}','{pi}');
                INSERT VendorInvoice (Id,TenantId,AutoInvoiceRequestId,AutoInvoiceRequestHash,AcceptedSupplyKind,AcceptedSupplySourceId,AcceptedSupplySourceReference,AcceptedSupplySnapshotHash,AcceptedSupplyValidatedAtUtc)
                    VALUES ('{invoice}','{tenant}','{request}','{hash}',4,'{invoice}','INV-AUTO','{hash}',GETUTCDATE());
                INSERT VendorInvoiceLineItem (Id,TenantId,VendorInvoiceId,PurchaseOrderItemId,Quantity) VALUES ('{line}','{tenant}','{invoice}','{pi}',2);
                """);
            string InsertAllocation(Guid id, Guid owner) => $"""
                INSERT VendorInvoiceReceiptAllocations (Id,TenantId,VendorInvoiceId,VendorInvoiceLineItemId,PurchaseOrderId,PurchaseOrderItemId,
                    PurchaseOrderReceiptId,PurchaseOrderReceiptItemId,InspectionCaseId,GoodsReceiptNoteId,GoodsReceiptNoteItemId,Quantity,CreatedAt,IsDeleted)
                VALUES ('{id}','{owner}','{invoice}','{line}','{po}','{pi}','{receipt}','{ri}','{inspection}','{grn}','{gi}',2,GETUTCDATE(),0)
                """;
            await Denied(sql, InsertAllocation(allocation, foreign), 51722);
            await Execute(sql, $"UPDATE VendorInvoice SET CurrencyCode='USD' WHERE Id='{invoice}'");
            await Denied(sql, InsertAllocation(allocation, tenant), 51722);
            await Execute(sql, $"UPDATE VendorInvoice SET CurrencyCode='GHS' WHERE Id='{invoice}'");
            await Execute(sql, InsertAllocation(allocation, tenant));
            await Denied(sql, "UPDATE VendorInvoiceReceiptAllocations SET Quantity=3", 51721);
            await Denied(sql, "DELETE FROM VendorInvoiceReceiptAllocations", 51721);
            await Denied(sql, $"UPDATE VendorInvoiceLineItem SET Quantity=3 WHERE Id='{line}'", 51724);
            await Denied(sql, $"UPDATE VendorInvoice SET AutoInvoiceRequestId=NEWID() WHERE Id='{invoice}'", 51723);
            await Denied(sql, $"UPDATE VendorInvoice SET AcceptedSupplySnapshotHash=NULL WHERE Id='{invoice}'", 51723);
            await Denied(sql, $"UPDATE VendorInvoice SET Status=3 WHERE Id='{invoice}'", 51723);
            await Denied(sql, $"UPDATE VendorInvoice SET Status=3,MatchingStatus=2,MatchingControlEventId=NEWID() WHERE Id='{invoice}'", 51725);
            var audit = Guid.NewGuid();
            var decisions = "[" + string.Join(",", Enumerable.Range(1, 14).Select(i => $"\"DEC-{i:000}\"")) + "]";
            var resultValues = System.Text.Json.JsonSerializer.Serialize(new { snapshotHash = hash });
            await Execute(sql, $"""
                INSERT ProcurementControlEvents (Id,TenantId,SourceId,SourceType,EventType,Action,RuleCode,RuleVersion,Result,ResultValuesJson,DecisionKeysJson)
                VALUES ('{audit}','{tenant}','{invoice}','VendorInvoice','InvoiceThreeWayMatching','InvoiceThreeWayMatchEvaluated','AP-002','TDC-0504',2,'{resultValues}','{decisions}');
                UPDATE VendorInvoice SET Status=3,MatchingStatus=2,MatchingControlEventId='{audit}',MatchingSnapshotHash='{hash}' WHERE Id='{invoice}';
                """);
            await Denied(sql, $"UPDATE VendorInvoice SET MatchingSnapshotHash='{new string('b', 64)}' WHERE Id='{invoice}'", 51725);
            var down = await Assert.ThrowsAsync<SqlException>(() => Migrate(false)); Assert.Equal(51720, down.Number);
            Assert.Equal(2, Convert.ToInt32(await Scalar(sql, "SELECT COUNT(*) FROM VendorInvoice")));
            Assert.Equal(1, Convert.ToInt32(await Scalar(sql, "SELECT COUNT(*) FROM VendorInvoiceReceiptAllocations")));
        }
        finally { await Execute(master, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]"); }
    }
    private static async Task Execute(SqlConnection connection, string text)
    { await using var command = new SqlCommand(text, connection) { CommandTimeout = 60 }; await command.ExecuteNonQueryAsync(); }
    private static async Task<object?> Scalar(SqlConnection connection, string text)
    { await using var command = new SqlCommand(text, connection) { CommandTimeout = 60 }; return await command.ExecuteScalarAsync(); }
    private static async Task Denied(SqlConnection connection, string text, int number)
    { var error = await Assert.ThrowsAsync<SqlException>(() => Execute(connection, text)); Assert.Equal(number, error.Number); }
}
