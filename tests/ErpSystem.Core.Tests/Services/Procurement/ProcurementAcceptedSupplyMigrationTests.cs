using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAcceptedSupplyMigrationTests
{
    [Fact]
    public void MigrationPersistsCategoryAndAcceptedSupplyLineage()
    {
        var operations = Operations();
        operations.OfType<AddColumnOperation>().Select(value => value.Name)
            .Should().Contain([
                "ProcurementCategory",
                "AcceptedSupplyKind",
                "AcceptedSupplySourceId",
                "AcceptedSupplySourceReference",
                "AcceptedSupplySnapshotHash",
                "AcceptedSupplyValidatedAtUtc"
            ]);
        operations.OfType<CreateIndexOperation>()
            .Should().ContainSingle(value =>
                value.Name == "UX_VendorInvoice_AcceptedCertificate" && value.IsUnique);
    }

    [Fact]
    public void ApprovalTriggerFailsClosedForMissingOrWrongCategoryEvidence()
    {
        var sql = Sql();

        sql.Should().Contain("TR_VendorInvoice_AcceptedSupplyProtected");
        sql.Should().Contain("ISNULL(i.AcceptedSupplyKind, -1) <> 1");
        sql.Should().Contain("ISNULL(i.AcceptedSupplyKind, -1) <> 2");
        sql.Should().Contain("ISNULL(i.AcceptedSupplyKind, -1) <> 3");
        sql.Should().Contain("po.ProcurementCategory = 1");
        sql.Should().Contain("THROW 52052");
    }

    [Fact]
    public void SubmittedAcceptedSupplyLineageIsImmutable()
    {
        var sql = Sql();

        sql.Should().Contain("d.Status NOT IN (1, 8)");
        sql.Should().Contain("AcceptedSupplySnapshotHash");
        sql.Should().Contain("AcceptedSupplyValidatedAtUtc");
        sql.Should().Contain("THROW 52051");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new AddCategoryAwareAcceptedSupply();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(value => value.Sql));
}
