using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceExecutionLinkMigrationGuardTests
{
    [Fact]
    public void Migration_has_tenant_lineage_lifecycle_and_append_only_hard_stops()
    {
        var migration = new AddCivilEngineeringMaintenanceExecutionLinks();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        var sql = string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        sql.Should().Contain("TR_CivilEngineeringMaintenanceExecutionLinks_Lineage")
            .And.Contain("TR_CivilEngineeringMaintenanceExecutionLinks_Lifecycle")
            .And.Contain("TR_CivilEngineeringMaintenanceExecutionLinkRevisions_AppendOnly")
            .And.Contain("handoff.Stage<>'Awarded'")
            .And.Contain("workOrder.AssetId<>value.MaintenanceAssetId")
            .And.Contain("REFERENCES JobCard(Id)")
            .And.Contain("LEFT JOIN JobCard jobCard")
            .And.NotContain("JobCards");
        migration.GetType().GetCustomAttributes<MigrationAttribute>()
            .Select(attribute => attribute.Id)
            .Should().Contain("20260821060000_AddCivilEngineeringMaintenanceExecutionLinks");
    }
}
