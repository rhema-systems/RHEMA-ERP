using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDirectTaskMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_project_work_item_assignee_policy_dms_and_append_only_task_controls()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821170000_AddCivilEngineeringDirectTaskControls.cs"));

        sql.Should().Contain("TR_ProjectCivilDirectTaskControls_Lineage")
            .And.Contain("TR_ProjectCivilDirectTaskControls_Lifecycle")
            .And.Contain("TR_ProjectCivilDirectTaskRevisions_Lineage")
            .And.Contain("TR_ProjectCivilDirectTaskRevisions_AppendOnly")
            .And.Contain("PROJECT_TASK")
            .And.Contain("CIV-CFG-010")
            .And.Contain("ProjectWorkItems")
            .And.Contain("ProjectMembers")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("OPENJSON(decision.ValueJson,'$.assigneeRoleIds')")
            .And.Contain("AFTER INSERT AS");
        sql.Should().Contain("[Migration(\"20260821170000_AddCivilEngineeringDirectTaskControls\")]");
    }

    [Fact]
    public void Feedback_migration_enforces_append_only_events_dms_lineage_idempotency_and_lifecycle_transitions()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821180000_AddCivilEngineeringDirectTaskFeedbackWorkflow.cs"));

        sql.Should().Contain("ProjectCivilDirectTaskFeedbackEntries")
            .And.Contain("IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_ClientRequestId")
            .And.Contain("IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_Sequence")
            .And.Contain("TR_ProjectCivilDirectTaskFeedbackEntries_Lineage")
            .And.Contain("TR_ProjectCivilDirectTaskFeedbackEntries_AppendOnly")
            .And.Contain("TR_ProjectCivilDirectTaskControls_FeedbackLifecycle")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("record.CurrentVersion=version.VersionNumber")
            .And.Contain("version.Status='Published'")
            .And.NotContain("version.IsCurrent=1")
            .And.Contain("LastFeedbackClientRequestId")
            .And.Contain("Invalid Civil direct-task lifecycle transition");
        sql.Should().Contain("[Migration(\"20260821180000_AddCivilEngineeringDirectTaskFeedbackWorkflow\")]");
    }

    [Fact]
    public void Urgent_task_migration_enforces_frozen_sla_controlled_escalation_and_single_escalation_lineage()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821190000_AddCivilEngineeringUrgentTaskControls.cs"));

        sql.Should().Contain("TR_ProjectCivilDirectTaskControls_UrgentPath")
            .And.Contain("UrgentEscalationClientRequestId")
            .And.Contain("UrgentEscalationRequestHash")
            .And.Contain("urgentEscalationRoleIds")
            .And.Contain("urgentResponseHours")
            .And.Contain("Civil urgent-task path and escalation lineage are immutable")
            .And.Contain("CK_ProjectCivilDirectTaskControls_UrgentPath");
        sql.Should().Contain("[Migration(\"20260821190000_AddCivilEngineeringUrgentTaskControls\")]");
    }

    [Fact]
    public void Mobile_field_feedback_migration_enforces_tenant_owned_measurements_and_offline_capture_bounds()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821200000_AddCivilEngineeringMobileFieldFeedback.cs"));

        sql.Should().Contain("MeasurementValue decimal(18,4)")
            .And.Contain("MeasurementUnitId uniqueidentifier")
            .And.Contain("CapturedOfflineAtUtc datetime2")
            .And.Contain("FK_ProjectCivilDirectTaskFeedbackEntries_MeasurementUnit")
            .And.Contain("CK_ProjectCivilDirectTaskFeedbackEntries_Measurement")
            .And.Contain("TR_ProjectCivilDirectTaskFeedbackEntries_FieldCapture")
            .And.Contain("unit.TenantId=value.TenantId")
            .And.Contain("unit.IsActive=1")
            .And.Contain("DATEADD(day,-31,value.CreatedAt)");
        sql.Should().Contain("[Migration(\"20260821200000_AddCivilEngineeringMobileFieldFeedback\")]");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
