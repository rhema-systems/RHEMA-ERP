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

    [Fact]
    public void ContactCorrectionTriggerAllowsOnlyExactGovernedRecoveryContext()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestContactCorrectionMigration().ApplyUp(builder);

        var sql = builder.Operations.OfType<SqlOperation>()
            .Single().Sql;

        sql.Should().Contain("TDC_SUPPLIER_CONTACT_ACCESS_ID");
        sql.Should().Contain("TDC_SUPPLIER_CONTACT_ACTOR_ID");
        sql.Should().Contain("TDC_SUPPLIER_CONTACT_HASH");
        sql.Should().Contain("@correctionAccessId = i.Id");
        sql.Should().Contain("@correctionContactHash = i.VerifiedContactHashSha256");
        sql.Should().Contain("d.Status IN (1, 5)");
        sql.Should().Contain("actor.TenantId = i.TenantId");
        sql.Should().Contain("actor.IsActive = 1");
        sql.Should().Contain("registration.Status = 'Approved'");
        sql.Should().Contain("outside a verified contact-correction transaction");
    }

    [Fact]
    public void PreProvisioningCorrectionKeepsSubjectImmutableWhileAllowingMissingAccessLink()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestPreProvisioningContactCorrectionMigration().ApplyUp(builder);

        var sql = builder.Operations.OfType<SqlOperation>()
            .Single().Sql;

        sql.Should().Contain("registration.BusinessPartnerId IS NOT NULL");
        sql.Should().Contain("i.BusinessPartnerId IS NULL");
        sql.Should().Contain("ISNULL(i.BusinessPartnerId");
        sql.Should().Contain("ISNULL(d.BusinessPartnerId");
        sql.Should().NotContain("AND d.BusinessPartnerId IS NOT NULL");
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

    private sealed class TestContactCorrectionMigration :
        AllowControlledSupplierApplicantContactCorrection
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class TestPreProvisioningContactCorrectionMigration :
        AllowPreProvisioningSupplierContactCorrection
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
