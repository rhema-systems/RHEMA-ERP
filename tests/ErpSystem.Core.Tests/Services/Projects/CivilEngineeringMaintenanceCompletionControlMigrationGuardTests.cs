using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceCompletionControlMigrationGuardTests
{
    [Fact]
    public void Migration_has_frozen_lineage_forward_lifecycle_and_append_only_history()
    {
        var migration = new AddCivilEngineeringMaintenanceCompletionControls();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_CivilEngineeringMaintenanceCompletionControls_Lineage")
            .And.Contain("TR_CivilEngineeringMaintenanceCompletionControls_Lifecycle")
            .And.Contain("TR_CivilEngineeringMaintenanceCompletionRevisions_AppendOnly")
            .And.Contain("handoff.Stage<>'Awarded'")
            .And.Contain("prior.Stage='AwaitingClosure' AND value.Stage='Closed'")
            .And.Contain("REFERENCES JobCard(Id)")
            .And.Contain("LEFT JOIN JobCard jobCard")
            .And.NotContain("JobCards");
        migration.GetType().GetCustomAttributes<MigrationAttribute>().Select(value => value.Id)
            .Should().Contain("20260821063000_AddCivilEngineeringMaintenanceCompletionControls");
    }
}
