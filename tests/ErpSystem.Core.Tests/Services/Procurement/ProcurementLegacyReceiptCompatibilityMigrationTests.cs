using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementLegacyReceiptCompatibilityMigrationTests
{
    [Fact]
    public void ForwardMigrationBackfillsHistoricalSnapshotsAndPreservesNewReceiptGuards()
    {
        var sql = Sql();

        sql.Should().Contain("TDC0501_LEGACY");
        sql.Should().Contain("HistoricalMigration");
        sql.Should().Contain("ReceiptSourceSnapshotJson");
        sql.Should().Contain("ReceiptLineIntegrityHash");
        sql.Should().Contain("priorPurchase.Quantity + priorDirectGrn.Quantity");
        sql.Should().Contain("d.Id IS NOT NULL");
        sql.Should().Contain("legacyRow.Id IS NULL");
        sql.Should().Contain("DISABLE TRIGGER");
        sql.Should().Contain("ENABLE TRIGGER");
    }

    private static string Sql()
    {
        var migration = new TDC0501LegacyReceiptCompatibility();
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));
    }
}
