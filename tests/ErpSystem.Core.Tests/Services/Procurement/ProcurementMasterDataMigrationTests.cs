using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementMasterDataMigrationTests
{
    [Fact]
    public void FreshAndCorrectiveMigrationsPermitRevalidationFailureCancellation()
    {
        Sql(MigrationOperations(new AddProcurementMasterDataChangeControls()))
            .Should().Contain("(d.Status = 6 AND i.Status = 5)");
        var correctiveOperations = MigrationOperations(new EnsureProcurementMasterDataCancellationTransition());
        Sql(correctiveOperations)
            .Should().Contain("(d.Status = 6 AND i.Status = 5)");
        correctiveOperations.OfType<CreateIndexOperation>()
            .Where(item => item.Name is
                "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus" or
                "IX_ProcurementPolicySets_TenantId_PolicyKey_LifecycleStatus" or
                "IX_ProcurementMasterDataControlPolicies_TenantId_ResourceType_Status")
            .Should().HaveCount(3).And.OnlyContain(item => !item.IsUnique);
    }

    private static IReadOnlyList<MigrationOperation> MigrationOperations(Migration migration)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });
        return builder.Operations;
    }

    private static string Sql(IEnumerable<MigrationOperation> operations) =>
        string.Join(Environment.NewLine, operations.OfType<SqlOperation>().Select(item => item.Sql));
}
