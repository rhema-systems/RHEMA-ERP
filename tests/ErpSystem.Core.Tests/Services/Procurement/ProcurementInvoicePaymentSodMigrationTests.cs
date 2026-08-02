using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementInvoicePaymentSodMigrationTests
{
    [Fact]
    public void MigrationAddsPaymentAndBatchEvidenceLineage()
    {
        var operations = Operations();

        operations.OfType<AddColumnOperation>().Should().Contain(item =>
            item.Table == "VendorPayment" && item.Name == "InvoicePaymentSodControlEventId");
        operations.OfType<AddColumnOperation>().Should().Contain(item =>
            item.Table == "PaymentBatch" && item.Name == "InvoicePaymentSodControlEventId");
        operations.OfType<AddForeignKeyOperation>().Count(item =>
            item.PrincipalTable == "ProcurementControlEvents").Should().Be(2);
    }

    [Fact]
    public void DirectSqlAuthorizationFailsClosedForManualAndBatchPayments()
    {
        var sql = Sql();

        sql.Should().Contain("TR_PaymentBatch_TDC0506InvoiceProcessorSod");
        sql.Should().Contain("TR_VendorPayment_TDC0506InvoiceProcessorSod");
        sql.Should().Contain("invoice.SubmittedById = batch.ApprovedById");
        sql.Should().Contain("invoice.SubmittedById = payment.AuthorizedById");
        sql.Should().Contain("sodEvent.RuleCode = 'AP-004'");
        sql.Should().Contain("sodEvent.RuleVersion = 'TDC-0506'");
        sql.Should().Contain("priorPayment.JournalEntryId IS NOT NULL");
        sql.Should().Contain("priorPayment.InvoicePaymentSodControlEventId IS NULL");
        sql.Should().Contain("payment.JournalEntryId = priorPayment.JournalEntryId");
        sql.Should().Contain("THROW 51641");
        sql.Should().Contain("THROW 51642");
    }

    [Fact]
    public void ForwardMigrationRepairsAlreadyAppliedHistoricalPaymentTrigger()
    {
        var migration = new TDC0506HistoricalPaymentSodCompatibility();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("CREATE OR ALTER TRIGGER [dbo].[TR_VendorPayment_TDC0506InvoiceProcessorSod]");
        sql.Should().Contain("priorPayment.JournalEntryId IS NOT NULL");
        sql.Should().Contain("payment.JournalEntryId = priorPayment.JournalEntryId");
        sql.Should().Contain("payment.AuthorizedById");
        sql.Should().Contain("THROW 51642");
    }

    [Fact]
    public void ForwardMigrationExcludesReversedOriginalsFromPaymentSodTrigger()
    {
        var migration = new TDC0506EffectivePaymentAllocations();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("CREATE OR ALTER TRIGGER [dbo].[TR_VendorPayment_TDC0506InvoiceProcessorSod]");
        sql.Should().Contain("reversal.OriginalAllocationId = allocation.Id");
        sql.Should().Contain("reversal.IsReversal = 1");
        sql.Should().Contain("priorPayment.JournalEntryId IS NOT NULL");
        sql.Should().Contain("THROW 51642");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0506InvoiceProcessorPaymentSod();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
