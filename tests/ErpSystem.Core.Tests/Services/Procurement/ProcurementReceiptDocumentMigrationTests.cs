using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptDocumentMigrationTests
{
    [Fact]
    public void MigrationCreatesOnlyTheReceiptDocumentRegisterAndSharedDmsProjections()
    {
        var sql = Sql();

        sql.Should().Contain("CREATE TABLE [dbo].[ProcurementReceiptDocuments]");
        sql.Should().Contain("CREATE TABLE [dbo].[ProcurementReceiptDocumentSignatures]");
        sql.Should().Contain("CREATE TABLE [dbo].[ProcurementReceiptDocumentActions]");
        sql.Should().Contain("CentralDocumentMetadataTemplates");
        sql.Should().Contain("CentralDocumentGenerationTemplates");
        sql.Should().Contain("'TDC-GRN'");
        sql.Should().Contain("'TDC-MRN'");
        sql.Should().Contain("vw_ProcurementReceiptDocumentReconciliation");
        Operations().Should().OnlyContain(item => item is SqlOperation);
    }

    [Fact]
    public void SqlHardStopsProtectTenantLineageIssueReadinessAndImmutableHistory()
    {
        var sql = Sql();

        sql.Should().Contain("RCV_DOCUMENT_TENANT_LINEAGE_INVALID");
        sql.Should().Contain("RCV_DOCUMENT_LINEAGE_IMMUTABLE");
        sql.Should().Contain("RCV_DOCUMENT_STATUS_INVALID");
        sql.Should().Contain("RCV_DOCUMENT_ISSUE_BLOCKED");
        sql.Should().Contain("ProcurementReceiptInspectionCases");
        sql.Should().Contain("ProcurementReceiptInspectionEvidence");
        sql.Should().Contain("$.signatureRequirements");
        sql.Should().Contain("$.evidenceRequirements");
        sql.Should().Contain("sequentialDocuments");
        sql.Should().Contain("RCV_DOCUMENT_SIGNATORY_SOD");
        sql.Should().Contain("RCV_DOCUMENT_ACTION_IMMUTABLE");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void UniquenessAndReconciliationAreAlwaysTenantScoped()
    {
        var sql = Sql();

        sql.Should().Contain("[TenantId], [PurchaseOrderReceiptId], [DocumentKind]");
        sql.Should().Contain("[TenantId], [DocumentNumber]");
        sql.Should().Contain("d.TenantId = r.TenantId");
        sql.Should().Contain("po.TenantId = r.TenantId");
        sql.Should().Contain("r.IsDeleted = 0");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0509ReceiptDocumentLifecycle();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
