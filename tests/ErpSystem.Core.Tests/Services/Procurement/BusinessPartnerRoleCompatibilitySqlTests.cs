using ErpSystem.Data.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerRoleCompatibilitySqlTests
{
    [ProcurementLocalDbFact]
    public async Task UpgradeAllowsDualRolesPreservesOtherGuardsAndProtectsRollback()
    {
        var database = "TdcPartnerRoleTest_" + Guid.NewGuid().ToString("N");
        var settings = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master",
            IntegratedSecurity = true, TrustServerCertificate = true, Pooling = false };
        await using var master = new SqlConnection(settings.ConnectionString); await master.OpenAsync();
        await Execute(master, $"CREATE DATABASE [{database}]");
        try
        {
            settings.InitialCatalog = database;
            await using var sql = new SqlConnection(settings.ConnectionString); await sql.OpenAsync();
            await Execute(sql, "CREATE TABLE BusinessPartners (PartnerType nvarchar(50)); CREATE TABLE BusinessPartnerRegistrations (PartnerType nvarchar(50)); CREATE TABLE TenderProbe (PartnerType nvarchar(50), IsActive bit); CREATE TABLE SubcontractProbe (PartnerType nvarchar(50), IsActive bit);");
            var triggers = new[] {
                ("TR_ProcurementTenderDocumentIssuances_Immutable", "TenderProbe", "('Supplier', 'Contractor', 'Both')"),
                ("TR_QS0521_Subcontracts_Governance", "SubcontractProbe", "('Supplier','Contractor','Both')") };
            foreach (var (name, table, predicate) in triggers)
                await Execute(sql, $"CREATE OR ALTER TRIGGER dbo.{name} ON dbo.{table} AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted bp WHERE bp.IsActive=0 OR bp.PartnerType NOT IN {predicate}) THROW 51000, 'Invalid partner', 1; END");
            var migration = new BusinessPartnerRoleCompatibility();
            async Task Apply(bool up)
            {
                foreach (var operation in (up ? migration.UpOperations : migration.DownOperations).Cast<SqlOperation>())
                    await Execute(sql, operation.Sql);
            }
            await Apply(true); await Apply(true);
            foreach (var (_, table, _) in triggers)
            {
                foreach (var role in new[] { "Supplier", "CustomerAndSupplier", "Vendor", "Manufacturer", "Contractor", "Both" })
                    await Execute(sql, $"INSERT {table} VALUES ('{role}',1)");
                await Denied(sql, $"INSERT {table} VALUES ('Customer',1)", 51000);
                await Denied(sql, $"INSERT {table} VALUES ('CustomerAndSupplier',0)", 51000);
            }
            await Execute(sql, "INSERT BusinessPartners VALUES ('CustomerAndSupplier')");
            var refusal = await Assert.ThrowsAsync<SqlException>(() => Apply(false)); Assert.Equal(51726, refusal.Number);
            await Execute(sql, "DELETE BusinessPartners; INSERT BusinessPartnerRegistrations VALUES ('CustomerAndSupplier')");
            refusal = await Assert.ThrowsAsync<SqlException>(() => Apply(false)); Assert.Equal(51726, refusal.Number);
            await Execute(sql, "DELETE BusinessPartnerRegistrations");
            await Apply(false);
            foreach (var (_, table, _) in triggers)
            {
                await Execute(sql, $"INSERT {table} VALUES ('Supplier',1)");
                await Denied(sql, $"INSERT {table} VALUES ('CustomerAndSupplier',1)", 51000);
            }
            await Execute(sql, "DROP TRIGGER dbo.TR_QS0521_Subcontracts_Governance");
            refusal = await Assert.ThrowsAsync<SqlException>(() => Apply(true)); Assert.Equal(51727, refusal.Number);
        }
        finally { await Execute(master, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]"); }
    }

    private static async Task Execute(SqlConnection connection, string text)
    { await using var command = new SqlCommand(text, connection) { CommandTimeout = 60 }; await command.ExecuteNonQueryAsync(); }
    private static async Task Denied(SqlConnection connection, string text, int number)
    { var error = await Assert.ThrowsAsync<SqlException>(() => Execute(connection, text)); Assert.Equal(number, error.Number); }
}
