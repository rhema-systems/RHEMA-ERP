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
        var compatibilitySql = ArchivedMigrationSource.Read("20260317115118_AddCrmEntities.cs");

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

        foreach (var table in new[] { "Leads", "QuoteLineItems", "Campaigns", "CampaignMembers" })
            compatibilitySql.Should().Contain(table);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void ProjectFoundationMigration_PreservesVendorInvoicesThroughTheMissingRename()
    {
        var compatibilitySql = ArchivedMigrationSource.Read("20260407033921_AddProjectPackageBoqFoundation.cs");

        compatibilitySql.Should().Contain("both VendorInvoices and VendorInvoice exist");
        compatibilitySql.Should().Contain(
            "EXEC sys.sp_rename N'[dbo].[VendorInvoices]', N'VendorInvoice'");

        compatibilitySql.Should().Contain("expected only VendorInvoice");
        compatibilitySql.Should().Contain(
            "EXEC sys.sp_rename N'[dbo].[VendorInvoice]', N'VendorInvoices'");
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void CurrentArInvoiceCompatibility_AddsBusinessPartnerColumnInSeparateSqlBatch()
    {
        var source = ArchivedMigrationSource.Read("20260710100000_EnsureCurrentArInvoiceTables.cs");
        source.Should().Contain(
            "ALTER TABLE [dbo].[Invoices] ADD [BusinessPartnerId] uniqueidentifier NULL");
        source.Should().Contain("CREATE TABLE [dbo].[Invoices]");
        source.Should().NotContain("[ReferenceNumber],\n                            [Status]");
        source.IndexOf(
                "ALTER TABLE [dbo].[Invoices] ALTER COLUMN [BusinessPartnerId]",
                StringComparison.Ordinal)
            .Should().BeLessThan(source.LastIndexOf(
                "CREATE INDEX [IX_Invoices_BusinessPartnerId]",
                StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void CustomerPaymentCompatibility_UsesOnlyBusinessPartnerModelColumns()
    {
        var sql = ArchivedMigrationSource.Read("20260720110000_AlignCustomerPaymentsToBusinessPartners.cs");

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

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the repository root should contain ErpSystem.sln");
        return Path.Combine(new[] { directory!.FullName }.Concat(segments).ToArray());
    }
}
