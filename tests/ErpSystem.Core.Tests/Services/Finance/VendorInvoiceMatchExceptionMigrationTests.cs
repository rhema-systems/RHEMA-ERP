using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Finance;

public sealed class VendorInvoiceMatchExceptionMigrationTests
{
    [Fact]
    public void MigrationCreatesTenantScopedRegisterAndImmutableEvidence()
    {
        var sql = Sql();

        sql.Should().Contain("CREATE TABLE [dbo].[VendorInvoiceMatchException]");
        sql.Should().Contain("IX_VendorInvoiceMatchException_TenantId_VendorInvoiceId_Sequence");
        sql.Should().Contain("IX_VendorInvoiceMatchException_TenantId_IdempotencyKey");
        sql.Should().Contain("TR_VendorInvoiceMatchExceptionEvidence_TDC0507Immutable");
        sql.Should().Contain("TR_VendorInvoiceMatchExceptionAction_TDC0507Immutable");
        sql.Should().Contain("AP_MATCH_EXCEPTION_TENANT_MISMATCH");
    }

    [Fact]
    public void DirectSqlApprovalRequiresExactAp006EventAndTwoIndependentApprovers()
    {
        var sql = Sql();

        sql.Should().Contain("evt.RuleCode = 'AP-006'");
        sql.Should().Contain("evt.RuleVersion = 'TDC-0507'");
        sql.Should().Contain("evt.Action = 'InvoiceMatchExceptionApproved'");
        sql.Should().Contain("COUNT(DISTINCT approval.ProcessedById)");
        sql.Should().Contain("approval.ProcessedById IN (i.RequestedById, invoice.SubmittedById)");
        sql.Should().Contain("JSON_VALUE(evt.ResultValuesJson, '$.invoiceSnapshotHash')");
        sql.Should().Contain("THROW 51656");
    }

    [Fact]
    public void EveryTriggerUsesItsOwnSqlServerBatch()
    {
        var triggerOperations = Operations().OfType<SqlOperation>()
            .Where(item => item.Sql.Contains("CREATE OR ALTER TRIGGER", StringComparison.Ordinal))
            .ToList();

        triggerOperations.Should().HaveCount(4);
        triggerOperations.Should().OnlyContain(item =>
            item.Sql.TrimStart().StartsWith("CREATE OR ALTER TRIGGER", StringComparison.Ordinal) &&
            item.Sql.Split("CREATE OR ALTER TRIGGER", StringSplitOptions.None).Length == 2);
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0507InvoiceMatchExceptions();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
