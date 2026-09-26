using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementEnterpriseMigrationSqlTests
{
    [ProcurementLocalDbFact]
    public async Task AdditiveMigrationsPreserveOldRowsAndEnforceComputedReferencesAndDocumentLineage()
    {
        // Deliberately independent of the application's connection settings and databases.
        var databaseName = "TdcProcurementMigrationTest_" + Guid.NewGuid().ToString("N");
        var masterConnection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 30, Pooling = false
        };
        await using var master = new SqlConnection(masterConnection.ConnectionString);
        await master.OpenAsync();
        await Execute(master, $"CREATE DATABASE [{databaseName}]");
        try
        {
            var testConnection = new SqlConnectionStringBuilder(masterConnection.ConnectionString) { InitialCatalog = databaseName };
            await using var connection = new SqlConnection(testConnection.ConnectionString);
            await connection.OpenAsync();
            await Execute(connection, """
                CREATE TABLE ProcurementPlanItems (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, ProcurementBudgetId uniqueidentifier NULL);
                CREATE TABLE TenderBidItems (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, TenderBidId uniqueidentifier NOT NULL);
                CREATE TABLE TenderBidDocuments (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, TenderBidId uniqueidentifier NOT NULL);
                CREATE TABLE ProcurementSettings (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL);
                CREATE TABLE InventoryItems (Id uniqueidentifier NOT NULL PRIMARY KEY, Weight decimal(18,4) NULL);
                CREATE TABLE PurchaseOrderReceiptItems (Id uniqueidentifier NOT NULL PRIMARY KEY);
                CREATE TABLE GoodsReceiptNoteItems (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NULL, GoodsReceiptNoteId uniqueidentifier NULL, IsDeleted bit NOT NULL DEFAULT 0, UnitOfMeasure nvarchar(20) NULL);
                CREATE TABLE Tenants (Id uniqueidentifier NOT NULL PRIMARY KEY);
                CREATE TABLE LandedCosts (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, GoodsReceiptNoteId uniqueidentifier NOT NULL, Status nvarchar(50) NOT NULL, IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE PurchaseOrders (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE BusinessPartners (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE GoodsReceiptNotes (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, PurchaseOrderId uniqueidentifier NULL, IsDeleted bit NOT NULL DEFAULT 0);
                CREATE TABLE LandedCostItems (Id uniqueidentifier NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL, LandedCostId uniqueidentifier NOT NULL, SupplierId uniqueidentifier NULL, CostType int NOT NULL, Amount decimal(18,2) NOT NULL, Currency nvarchar(10) NOT NULL, IsDeleted bit NOT NULL DEFAULT 0);
                INSERT InventoryItems VALUES (NEWID(), 1.2345);
                INSERT PurchaseOrderReceiptItems VALUES (NEWID());
                INSERT GoodsReceiptNoteItems (Id, TenantId, GoodsReceiptNoteId, UnitOfMeasure) VALUES ('44444444-4444-4444-4444-444444444444', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '55555555-5555-5555-5555-555555555555', 'EA');
                INSERT Tenants VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'), ('ffffffff-ffff-ffff-ffff-ffffffffffff');
                INSERT LandedCosts (Id,TenantId,GoodsReceiptNoteId,Status) VALUES ('66666666-6666-6666-6666-666666666666','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','55555555-5555-5555-5555-555555555555','Draft');
                INSERT BusinessPartners (Id,TenantId) VALUES ('88888888-8888-8888-8888-888888888888','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
                INSERT PurchaseOrders (Id,TenantId) VALUES ('99999999-9999-9999-9999-999999999999','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
                INSERT GoodsReceiptNotes (Id,TenantId,PurchaseOrderId) VALUES ('55555555-5555-5555-5555-555555555555','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','99999999-9999-9999-9999-999999999999');
                INSERT LandedCostItems (Id,TenantId,LandedCostId,SupplierId,CostType,Amount,Currency) VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','66666666-6666-6666-6666-666666666666','88888888-8888-8888-8888-888888888888',1,125,'GHS');
                INSERT ProcurementPlanItems VALUES ('11111111-1111-1111-1111-111111111111','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
                INSERT TenderBidItems VALUES ('22222222-2222-2222-2222-222222222222','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','cccccccc-cccc-cccc-cccc-cccccccccccc');
                INSERT TenderBidDocuments VALUES ('33333333-3333-3333-3333-333333333333','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','cccccccc-cccc-cccc-cccc-cccccccccccc');
                INSERT ProcurementSettings VALUES (NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
                """);
            using var generation = new DbContext(new DbContextOptionsBuilder().UseSqlServer(testConnection.ConnectionString).Options);
            var generator = generation.GetService<IMigrationsSqlGenerator>();
            var initializer = generation.GetService<IModelRuntimeInitializer>();
            Migration[] migrations = [new ProcurementPlanLineReference(), new TenderBidItemDocuments(), new ProcurementAutomaticTenderClosing(), new ReceiptItemWeightSnapshots(), new LandedCostReceiptWeights(), new LandedCostSupplierDocuments()];
            foreach (var migration in migrations)
                foreach (var command in generator.Generate(migration.UpOperations, initializer.Initialize(migration.TargetModel, designTime: true)))
                    await Execute(connection, command.CommandText);

            (await Scalar(connection, "SELECT ReferenceNumber FROM ProcurementPlanItems")).Should().Be("PPL-11111111111111111111111111111111");
            (await Scalar(connection, "SELECT CONVERT(varchar(36),ProcurementBudgetId) FROM ProcurementPlanItems"))!.ToString()!.ToLowerInvariant()
                .Should().Be("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            (await Scalar(connection, "SELECT COUNT(*) FROM TenderBidDocuments WHERE TenderBidItemId IS NULL")).Should().Be(1);
            (await Scalar(connection, "SELECT AutoCloseTenders FROM ProcurementSettings")).Should().Be(false);
            (await Scalar(connection, "SELECT Weight FROM InventoryItems")).Should().Be(1.2345m);
            (await Scalar(connection, "SELECT COUNT(*) FROM InventoryItems WHERE WeightUnit IS NULL")).Should().Be(1);
            (await Scalar(connection, "SELECT COUNT(*) FROM PurchaseOrderReceiptItems WHERE UnitWeightKg IS NULL AND WeightOverridden = 0")).Should().Be(1);
            await Execute(connection, "UPDATE GoodsReceiptNoteItems SET UnitWeightKg = 0.123456, WeightStockUom = 'EA', WeightOverridden = 1");
            (await Scalar(connection, "SELECT UnitWeightKg FROM GoodsReceiptNoteItems")).Should().Be(0.123456m);
            await Execute(connection, """
                INSERT LandedCostReceiptWeights (Id,TenantId,LandedCostId,GoodsReceiptNoteItemId,UnitWeightKg,StockUom,Reason,CreatedAt,IsDeleted)
                VALUES ('77777777-7777-7777-7777-777777777777','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','66666666-6666-6666-6666-666666666666','44444444-4444-4444-4444-444444444444',1.234567,'EA','Measured',GETUTCDATE(),0);
                """);
            (await Scalar(connection, "SELECT UnitWeightKg FROM LandedCostReceiptWeights")).Should().Be(1.234567m);
            (await Scalar(connection, "SELECT UnitWeightKg FROM GoodsReceiptNoteItems")).Should().Be(0.123456m);
            Func<Task> wrongWeightTenant = () => Execute(connection, "UPDATE LandedCostReceiptWeights SET TenantId='ffffffff-ffff-ffff-ffff-ffffffffffff'");
            (await wrongWeightTenant.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51711);
            Func<Task> wrongWeightUom = () => Execute(connection, "UPDATE LandedCostReceiptWeights SET StockUom='BOX'");
            (await wrongWeightUom.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51711);
            await Execute(connection, "UPDATE LandedCosts SET Status='Allocated'");
            Func<Task> changeAllocatedWeight = () => Execute(connection, "UPDATE LandedCostReceiptWeights SET UnitWeightKg=9");
            (await changeAllocatedWeight.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51711);
            const string supplierDocumentInsert = """
                INSERT LandedCostSupplierDocuments (Id,TenantId,LandedCostItemId,LandedCostId,GoodsReceiptNoteId,PurchaseOrderId,BusinessPartnerId,CostType,Amount,Currency,CreatedAt,IsDeleted)
                VALUES ('33333333-3333-3333-3333-333333333333','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb','66666666-6666-6666-6666-666666666666','55555555-5555-5555-5555-555555555555','99999999-9999-9999-9999-999999999999','88888888-8888-8888-8888-888888888888',1,125,'GHS',GETUTCDATE(),0);
                """;
            await Execute(connection, supplierDocumentInsert);
            (await Scalar(connection, "SELECT DocumentNumber FROM LandedCostSupplierDocuments")).Should().Be("LCSD-33333333333333333333333333333333");
            Func<Task> duplicateCharge = () => Execute(connection, supplierDocumentInsert.Replace("33333333-3333-3333-3333-333333333333", Guid.NewGuid().ToString()));
            (await duplicateCharge.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(2601);
            Func<Task> foreignDocument = () => Execute(connection, supplierDocumentInsert.Replace("33333333-3333-3333-3333-333333333333", Guid.NewGuid().ToString()).Replace("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "ffffffff-ffff-ffff-ffff-ffffffffffff"));
            (await foreignDocument.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51714);
            Func<Task> rewriteDocument = () => Execute(connection, "UPDATE LandedCostSupplierDocuments SET Amount=126");
            (await rewriteDocument.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51713);
            (await Scalar(connection, "SELECT COUNT(*) FROM GoodsReceiptNoteItems")).Should().Be(1, "supplier charge documents do not create physical goods lines");
            await Execute(connection, "UPDATE InventoryItems SET Weight=1.234567");
            var precisionGuard = new ReceiptItemWeightSnapshots().DownOperations
                .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Single().Sql;
            Func<Task> unsafeRollback = () => Execute(connection, precisionGuard);
            (await unsafeRollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51710);
            await Execute(connection, "UPDATE InventoryItems SET Weight=1.2345");
            Func<Task> overwrite = () => Execute(connection, "UPDATE ProcurementPlanItems SET ReferenceNumber='PPL-manual'");
            await overwrite.Should().ThrowAsync<SqlException>();

            await Execute(connection, """
                INSERT TenderBidDocuments VALUES (NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','cccccccc-cccc-cccc-cccc-cccccccccccc','22222222-2222-2222-2222-222222222222');
                INSERT TenderBidDocuments VALUES (NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','cccccccc-cccc-cccc-cccc-cccccccccccc','22222222-2222-2222-2222-222222222222');
                """);
            Func<Task> wrongBid = () => Execute(connection, "INSERT TenderBidDocuments VALUES (NEWID(),'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',NEWID(),'22222222-2222-2222-2222-222222222222')");
            (await wrongBid.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
            Func<Task> wrongTenant = () => Execute(connection, "INSERT TenderBidDocuments VALUES (NEWID(),NEWID(),'cccccccc-cccc-cccc-cccc-cccccccccccc','22222222-2222-2222-2222-222222222222')");
            (await wrongTenant.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(547);
            (await Scalar(connection, "SELECT COUNT(*) FROM TenderBidDocuments")).Should().Be(3);

            foreach (var migration in migrations.Reverse())
                foreach (var command in generator.Generate(migration.DownOperations, initializer.Initialize(migration.TargetModel, designTime: true)))
                    await Execute(connection, command.CommandText);
            (await Scalar(connection, "SELECT COUNT(*) FROM TenderBidDocuments")).Should().Be(3);
            (await Scalar(connection, "SELECT COUNT(*) FROM ProcurementPlanItems")).Should().Be(1);
        }
        finally
        {
            // Only this exact GUID-named database, created above by this test, is removed.
            await Execute(master, $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]");
        }
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> Scalar(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        return await command.ExecuteScalarAsync();
    }
}

public sealed class ProcurementLocalDbFactAttribute : FactAttribute
{
    public ProcurementLocalDbFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("TDC_PROCUREMENT_MIGRATION_TESTS") != "1")
            Skip = "Opt in with TDC_PROCUREMENT_MIGRATION_TESTS=1 on Windows with MSSQLLocalDB; uses a disposable test database.";
    }
}
