using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierEvidenceBindingLineageMigrationTests
{
    [Fact]
    public void ExceptionFingerprintIndexAllowsOneRowPerOccurrence()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestExceptionOccurrenceMigration().ApplyUp(builder);

        var index = builder.Operations.OfType<CreateIndexOperation>().Single();
        index.Name.Should().Be("IX_SystemExceptionLogs_TenantId_Fingerprint");
        index.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void TriggerValidatesSnapshotLineageWithoutComparingUnrelatedHashes()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestMigration().ApplyUp(builder);

        var sql = builder.Operations.OfType<SqlOperation>()
            .Single().Sql;

        sql.Should().Contain("p.[PackCode] <> i.[PackCode]");
        sql.Should().Contain("p.[Version] <> i.[PackVersion]");
        sql.Should().Contain("JSON_VALUE(i.[PackSnapshotJson], '$.id')");
        sql.Should().Contain("JSON_VALUE(i.[PackSnapshotJson], '$.tenantId')");
        sql.Should().NotContain("p.[IntegrityHash] <> i.[PackSnapshotHash]");
    }

    private sealed class TestMigration :
        FixSupplierEvidencePackBindingLineageTrigger
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class TestExceptionOccurrenceMigration :
        RecordEverySystemExceptionOccurrence
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
