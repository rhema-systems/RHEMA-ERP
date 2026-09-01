using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringWeeklySupervisionMigrationGuardTests
{
    [Fact]
    public void Migration_protects_weekly_report_tenant_lineage_lifecycle_dms_and_append_only_evidence()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260820213000_AddCivilEngineeringWeeklySupervisionReports.cs"));
        Assert.Contains("CREATE TABLE ProjectCivilWeeklySupervisionReports", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE ProjectCivilWeeklySupervisionActivities", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE ProjectCivilWeeklySupervisionEvidence", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE ProjectCivilWeeklySupervisionReviews", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionReports_Lineage", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionReports_Lifecycle", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionEvidence_Lineage", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionRevisions_AppendOnly", sql, StringComparison.Ordinal);
        Assert.Contains("PROJECT_WEEKLY_REPORT", sql, StringComparison.Ordinal);
        Assert.Contains("CIV-CFG-006", sql, StringComparison.Ordinal);
        Assert.Contains("CentralDocumentVersions", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Forward_migration_adds_governed_progress_without_replacing_the_existing_weekly_report_owner()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260822003200_GovernCivilWeeklyProgressControls.cs"));

        Assert.Contains("ALTER TABLE dbo.ProjectCivilWeeklySupervisionReports ADD", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE ProjectCivilWeekly", sql, StringComparison.Ordinal);
        Assert.Contains("HasGovernedProgressControl", sql, StringComparison.Ordinal);
        Assert.Contains("ProjectMilestoneId", sql, StringComparison.Ordinal);
        Assert.Contains("ProjectActionItems", sql, StringComparison.Ordinal);
        Assert.Contains("ProjectDecisions", sql, StringComparison.Ordinal);
        Assert.Contains("CK_ProjectCivilWeeklySupervisionReports_ProgressControl", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionReports_Lineage", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionReports_Lifecycle", sql, StringComparison.Ordinal);
        Assert.Contains("52143", sql, StringComparison.Ordinal);
        Assert.Contains("52144", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Forward_schedule_snapshot_repair_preserves_legacy_reports_and_freezes_new_schedule_evidence()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260822003300_SnapshotCivilWeeklyMilestoneSchedule.cs"));

        Assert.Contains("MilestoneTargetDateSnapshot", sql, StringComparison.Ordinal);
        Assert.Contains("MilestoneActualDateSnapshot", sql, StringComparison.Ordinal);
        Assert.Contains("CK_ProjectCivilWeeklySupervisionReports_MilestoneScheduleSnapshot", sql, StringComparison.Ordinal);
        Assert.Contains("HasGovernedProgressControl]=0", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilWeeklySupervisionReports_Lifecycle", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE ProjectCivilWeekly", sql, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ErpSystem.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
