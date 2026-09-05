using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementEvaluationCommitteeLineageMigrationTests
{
    [Fact]
    public void Forward_trigger_accepts_only_exact_historical_source_lineage()
    {
        var sql = MigrationSql("Up");

        sql.Should().Contain("TR_ProcurementEvaluationCommitteeControls_Lifecycle")
            .And.Contain("LEFT JOIN [dbo].[ProcurementSourcingCases] sc")
            .And.Contain("sc.PolicySetId <> i.PolicySetId")
            .And.Contain("sc.PolicyCode <> i.PolicyCode")
            .And.Contain("sc.PolicyVersion <> i.PolicyVersion")
            .And.Contain("sc.MethodRuleId <> i.MethodRuleId")
            .And.Contain("sc.MethodRuleCode <> i.MethodRuleCode")
            .And.Contain("p.SourceConfigurationProfileId <> i.ConfigurationProfileId")
            .And.Contain("mr.PolicySetId <> i.PolicySetId")
            .And.Contain("mr.WorkflowDefinitionId")
            .And.Contain("wi.WorkflowDefinitionId <> i.WorkflowDefinitionId")
            .And.NotContain("sc.Status NOT IN",
                "later sourcing-case closure must not invalidate retained committee lineage");
    }

    [Fact]
    public void Forward_trigger_uses_sourcing_case_time_not_current_policy_state()
    {
        var sql = MigrationSql("Up");

        sql.Should().Contain("p.LifecycleStatus NOT IN (1, 2)")
            .And.Contain("cp.LifecycleStatus NOT IN (1, 2)")
            .And.Contain("p.PublishedAt > sc.CreatedAt")
            .And.Contain("p.EffectiveFrom > sc.CreatedAt")
            .And.Contain("p.RetiredAt < sc.CreatedAt")
            .And.Contain("mr.EffectiveFrom > sc.CreatedAt")
            .And.NotContain("COALESCE(t.CreatedAt, r.CreatedAt, sc.CreatedAt)")
            .And.NotContain("mr.IsAllowed = 0")
            .And.NotContain("mr.IsEnabled = 0")
            .And.NotContain("p.LifecycleStatus <> 1")
            .And.NotContain("cp.LifecycleStatus <> 1");
    }

    [Fact]
    public void Historical_profile_retired_before_case_is_accepted_through_the_exact_published_policy()
    {
        var sql = MigrationSql("Up");

        sql.Should().Contain("cp.LifecycleStatus NOT IN (1, 2)")
            .And.Contain("cp.PublishedAt IS NULL")
            .And.Contain("p.SourceConfigurationProfileId <> i.ConfigurationProfileId")
            .And.NotContain("cp.PublishedAt > sc.CreatedAt")
            .And.NotContain("cp.EffectiveFrom > sc.CreatedAt")
            .And.NotContain("cp.EffectiveTo < sc.CreatedAt")
            .And.NotContain("cp.RetiredAt < sc.CreatedAt");
    }

    [Fact]
    public void Rollback_restores_the_prior_current_publication_rule()
    {
        var sql = MigrationSql("Down");

        sql.Should().Contain("p.LifecycleStatus <> 1")
            .And.Contain("cp.Id IS NOT NULL AND cp.LifecycleStatus <> 1")
            .And.NotContain("LEFT JOIN [dbo].[ProcurementSourcingCases] sc");
    }

    [Fact]
    public void Vps_preflight_covers_the_trigger_replacement_prerequisites()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        script.Should().Contain(
                "MigrationId = N'20260902110000_AllowHistoricalEvaluationCommitteeSourceLineage'")
            .And.Contain(
                "GUARD_COVERAGE|20260902110000_AllowHistoricalEvaluationCommitteeSourceLineage")
            .And.Contain("Historical evaluation committee lineage prerequisites")
            .And.Contain("TR_ProcurementEvaluationCommitteeControls_Lifecycle");
    }

    private static string MigrationSql(string methodName)
    {
        var migration = new AllowHistoricalEvaluationCommitteeSourceLineage();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException(
            "Repository root containing ErpSystem.sln was not found.");
    }
}
