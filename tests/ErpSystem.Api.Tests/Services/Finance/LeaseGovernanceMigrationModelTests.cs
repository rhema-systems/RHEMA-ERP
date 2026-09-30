using System.Reflection;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class LeaseGovernanceMigrationModelTests
{
    [Fact]
    public void Down_refuses_to_drop_new_governed_evidence_including_soft_deleted_rows()
    {
        var migration = new LeaseInstalmentApOpenItems();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(LeaseInstalmentApOpenItems)
            .GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var guard = builder.Operations.OfType<SqlOperation>().Single().Sql;
        guard.Should().ContainAll(
            "[AccountingBookId] IS NOT NULL",
            "[RecognitionJournalEntryId] IS NOT NULL",
            "[RecognitionPostingEventId] IS NOT NULL",
            "[ActivationWorkflowInstanceId] IS NOT NULL",
            "[ActivationSubmittedByUserId] IS NOT NULL",
            "[ActivationApprovedByUserId] IS NOT NULL",
            "[LeaseScheduleLineId] IS NOT NULL",
            "[ReplacesLeaseVendorInvoiceId] IS NOT NULL",
            "[LeaseComponent] IS NOT NULL",
            "[SourceDocumentType] = N'LeaseRecognition'",
            "[TransactionType] = N'LeaseRecognition'",
            "THROW 51000");
        guard.Should().NotContain("[IsDeleted]");
        guard.Should().NotContain("[RouAssetId]");
    }

    [Fact]
    public void Model_uses_same_tenant_composite_authority_and_lineage_relationships()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"lease-model-{Guid.NewGuid():N}")
            .Options);

        AssertCompositeForeignKey<LeaseContract, WorkflowInstance>(
            db, "TenantId", "ActivationWorkflowInstanceId");
        AssertCompositeForeignKey<VendorPayment, AccountingBook>(
            db, "TenantId", "AccountingBookId");
        AssertCompositeForeignKey<VendorInvoice, AccountingBook>(
            db, "TenantId", "LeaseAccountingBookId");
        AssertCompositeForeignKey<VendorInvoice, LeaseScheduleLine>(
            db, "TenantId", "LeaseScheduleLineId");

        var invoice = db.Model.FindEntityType(typeof(VendorInvoice))!;
        invoice.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(VendorInvoice) &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "ReplacesLeaseVendorInvoiceId" }));
        invoice.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "LeaseScheduleLineId" }) &&
            index.GetFilter() == "[LeaseScheduleLineId] IS NOT NULL AND [IsDeleted] = 0 AND [Status] <> 7");
    }

    private static void AssertCompositeForeignKey<TDependent, TPrincipal>(
        ApplicationDbContext db,
        params string[] propertyNames)
    {
        db.Model.FindEntityType(typeof(TDependent))!.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TPrincipal) &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }
}
