using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class MigrationRehearsalCorrectionTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public void CrmMigration_ReconcilesOnlyACompleteEmptyRedundantGraph()
    {
        var compatibilitySql = new ExposedCrmMigration().Operations()
            .OfType<SqlOperation>()
            .First().Sql;

        compatibilitySql.Should().Contain("@RedundantCrmTableCount NOT IN (0, 7)");
        compatibilitySql.Should().Contain("the redundant plural CRM graph contains data");
        compatibilitySql.Should().Contain("DROP TABLE [dbo].[CampaignMembers]");
        compatibilitySql.Should().Contain("DROP TABLE [dbo].[QuoteLineItems]");
        compatibilitySql.Should().Contain("DROP TABLE [dbo].[Leads]");
        compatibilitySql.IndexOf("DROP TABLE [dbo].[CampaignMembers]", StringComparison.Ordinal)
            .Should().BeLessThan(
                compatibilitySql.IndexOf("DROP TABLE [dbo].[Campaigns]", StringComparison.Ordinal));
        compatibilitySql.IndexOf("DROP TABLE [dbo].[QuoteLineItems]", StringComparison.Ordinal)
            .Should().BeLessThan(
                compatibilitySql.IndexOf("DROP TABLE [dbo].[Quotes]", StringComparison.Ordinal));

        var restoredTables = new ExposedCrmMigration().BuildDownOperations()
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .ToArray();
        restoredTables.Should().Contain(["Leads", "QuoteLineItems", "Campaigns", "CampaignMembers"]);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void ProjectFoundationMigration_PreservesVendorInvoicesThroughTheMissingRename()
    {
        var compatibilitySql = new ExposedProjectFoundationMigration().Operations()
            .OfType<SqlOperation>()
            .First().Sql;

        compatibilitySql.Should().Contain("both VendorInvoices and VendorInvoice exist");
        compatibilitySql.Should().Contain(
            "EXEC sys.sp_rename N'[dbo].[VendorInvoices]', N'VendorInvoice'");

        var downSql = new ExposedProjectFoundationMigration().BuildDownOperations()
            .OfType<SqlOperation>()
            .Select(operation => operation.Sql)
            .ToArray();
        downSql.First().Should().Contain("expected only VendorInvoice");
        downSql.Last().Should().Contain(
            "EXEC sys.sp_rename N'[dbo].[VendorInvoice]', N'VendorInvoices'");
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void CurrentArInvoiceCompatibility_AddsBusinessPartnerColumnInSeparateSqlBatch()
    {
        var operations = new ExposedCurrentArInvoiceMigration().Operations()
            .OfType<SqlOperation>()
            .ToList();

        operations.Should().HaveCountGreaterThan(1);
        operations[0].Sql.Should().Contain(
            "ALTER TABLE [dbo].[Invoices] ADD [BusinessPartnerId] uniqueidentifier NULL");
        operations[1].Sql.Should().Contain("CREATE TABLE [dbo].[Invoices]");
        operations[1].Sql.Should().NotContain("[ReferenceNumber],\n                            [Status]");
        operations[1].Sql.IndexOf(
                "ALTER TABLE [dbo].[Invoices] ALTER COLUMN [BusinessPartnerId]",
                StringComparison.Ordinal)
            .Should().BeLessThan(operations[1].Sql.LastIndexOf(
                "CREATE INDEX [IX_Invoices_BusinessPartnerId]",
                StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void CustomerPaymentCompatibility_UsesOnlyBusinessPartnerModelColumns()
    {
        var sql = new ExposedCustomerPaymentMigration().Operations()
            .OfType<SqlOperation>()
            .Single().Sql;

        sql.Should().Contain("INSERT INTO [dbo].[BusinessPartners]");
        sql.Should().NotContain("[DeletedBy], [ReferenceNumber]");
        sql.Should().NotContain("c.[DeletedBy],\n            LEFT(COALESCE(NULLIF(c.[ReferenceNumber]");
    }

    [Fact]
    public void FinanceSeed_AssignsCanonicalIdentityBeforePayrollEnrichment()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "ErpSystem.Data", "Seeders", "FinanceDataSeeder.cs"));

        source.Should().Contain(
            ".Concat(GetPayrollChartOfAccounts(tenantId, baseDate))");
        source.Should().NotContain("existing.AccountNumber = account.AccountNumber;");
        source.Should().NotContain("existing.IsSegmented = account.IsSegmented;");
    }

    [Fact]
    public void ProtectedBalanceSheetSeed_UsesManifestStableRootCodes()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "ErpSystem.Data", "Seeders", "FinanceFinancialStatementStandardSeeder.cs"));

        source.Should().Contain("new[] { \"ASSETS\", \"LIABILITIES\", \"EQUITY_ROOT\" }");
        source.Should().NotContain("\"ASSET_ROOT\", \"LIABILITY_ROOT\"");
    }

    private sealed class ExposedCrmMigration : AddCrmEntities
    {
        public IReadOnlyList<MigrationOperation> Operations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedProjectFoundationMigration : AddProjectPackageBoqFoundation
    {
        public IReadOnlyList<MigrationOperation> Operations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedCurrentArInvoiceMigration : EnsureCurrentArInvoiceTables
    {
        public IReadOnlyList<MigrationOperation> Operations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedCustomerPaymentMigration : AlignCustomerPaymentsToBusinessPartners
    {
        public IReadOnlyList<MigrationOperation> Operations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the repository root should contain ErpSystem.sln");
        return Path.Combine(new[] { directory!.FullName }.Concat(segments).ToArray());
    }
}
