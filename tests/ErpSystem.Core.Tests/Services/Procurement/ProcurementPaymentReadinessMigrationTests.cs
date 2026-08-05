using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPaymentReadinessMigrationTests
{
    [Fact]
    public void MigrationAddsAllocationLineageAndExactBatchInvoiceSelections()
    {
        var operations = Operations();
        var columns = operations.OfType<AddColumnOperation>().Select(item => item.Name);
        var batchTable = operations.OfType<CreateTableOperation>()
            .Single(item => item.Name == "PaymentBatchInvoice");

        columns.Should().Contain([
            "PaymentReadinessControlEventId",
            "PaymentReadinessSnapshotHash",
            "PaymentReadinessEvaluatedAtUtc"
        ]);
        batchTable.Columns.Select(item => item.Name).Should().Contain([
            "PaymentBatchId",
            "PaymentBatchItemId",
            "VendorPaymentId",
            "VendorInvoiceId",
            "Amount",
            "PaymentReadinessControlEventId",
            "PaymentReadinessSnapshotHash",
            "PaymentReadinessEvaluatedAtUtc"
        ]);
        operations.OfType<CreateIndexOperation>()
            .Should().Contain(item => item.IsUnique && item.Columns.SequenceEqual(
                new[] { "TenantId", "PaymentBatchId", "VendorInvoiceId" }));
    }

    [Fact]
    public void DirectAllocationAndBatchTransitionsFailClosedInSql()
    {
        var sql = Sql();

        sql.Should().Contain("TR_VendorPaymentAllocation_TDC0505PaymentReadiness");
        sql.Should().Contain("TR_PaymentBatchInvoice_TDC0505PaymentReadiness");
        sql.Should().Contain("TR_PaymentBatch_TDC0505Readiness");
        sql.Should().Contain("TR_PaymentBatchInvoice_TDC0505ImmutableSelection");
        sql.Should().Contain("THROW 51621");
        sql.Should().Contain("THROW 51622");
        sql.Should().Contain("THROW 51623");
        sql.Should().Contain("THROW 51624");
        sql.Should().Contain("paymentEvent.RuleCode = 'AP-003'");
        sql.Should().Contain("paymentEvent.RuleVersion = 'TDC-0505'");
        sql.Should().Contain("PaymentBatchInvoiceApproved");
        sql.Should().Contain("PaymentBatchInvoiceProcessed");
    }

    [Fact]
    public void PaymentReadinessConsumesCurrentMatchReceiptAndStrictExceptionEvidence()
    {
        var sql = Sql();

        sql.Should().Contain("invoice.MatchingControlEventId");
        sql.Should().Contain("ProcurementReceiptInspectionCases");
        sql.Should().Contain("exceptionEvent.RuleCode = 'AP-006'");
        sql.Should().Contain("exceptionEvent.RuleVersion = 'TDC-0507'");
        sql.Should().Contain("ProcurementControlEventEvidenceLinks");
        sql.Should().Contain("workflow.InitiatedById <> exceptionEvent.ActorUserId");
        Enumerable.Range(1, 14).Should().OnlyContain(number => sql.Contains($"'DEC-{number:000}'"));
    }

    [Fact]
    public void TerminalZeroEligibleReceiptPatchUpdatesBothDatabaseReadinessGuards()
    {
        var migration = new TDC0505TerminalReceiptEligibility();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sql = string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("TR_VendorPaymentAllocation_TDC0505PaymentReadiness");
        sql.Should().Contain("TR_PaymentBatchInvoice_TDC0505PaymentReadiness");
        sql.Should().Contain("latestInspection.Status = 8");
        sql.Should().Contain("latestInspection.ApEligibleQuantity = 0");
        sql.Should().Contain("@alterKeywordIndex - @createKeywordIndex");
        sql.Should().Contain("LEN(N'CREATE')");
        sql.Should().Contain("THROW 51625");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0505PaymentReadiness();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
