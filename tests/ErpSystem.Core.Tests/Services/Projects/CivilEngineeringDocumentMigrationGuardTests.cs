using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDocumentMigrationGuardTests
{
    private const string MigrationId = "20260815135156_AddCivilEngineeringDocumentRegister";
    private const string DmsGovernanceMigrationId =
        "20260821220000_AddCivilEngineeringDmsGovernanceBaseline";

    [Fact]
    public void Migration_reuses_central_dms_and_hardens_policy_lifecycle_and_revision_lineage()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", MigrationId + ".cs"));
        var metadata = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(
            root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("name: \"ProjectCivilEngineeringDocuments\"")
            .And.Contain("name: \"ProjectCivilEngineeringDocumentRevisions\"")
            .And.Contain("principalTable: \"CentralDocumentRecords\"")
            .And.Contain("principalTable: \"CentralDocumentVersions\"")
            .And.Contain("principalTable: \"CentralDocumentMetadataTemplates\"")
            .And.Contain("TDC-CIV-ENGINEERING-FILE")
            .And.Contain("profile.LifecycleStatus = 1")
            .And.Contain("decision.Status = 2")
            .And.Contain("decision.ApprovalStatus = 1")
            .And.Contain("decision.EvidenceStatus = 2")
            .And.Contain("TR_ProjectCivilEngineeringDocuments_Lineage")
            .And.Contain("TR_ProjectCivilEngineeringDocuments_Lifecycle")
            .And.Contain("TR_ProjectCivilEngineeringDocumentRevisions_AppendOnly")
            .And.Contain("current Published DMS lineage")
            .And.Contain("assigned independent reviewer");
        migration.Should().NotContain("File.WriteAllBytes")
            .And.NotContain("BinaryContent");
        metadata.Should().Contain($"Migration(\"{MigrationId}\")");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Runtime_uses_tenant_project_role_policy_dms_audit_and_serializable_idempotency_controls()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Services", "CivilEngineeringDesignService.Documents.cs"));
        var panel = File.ReadAllText(Path.Combine(
            root, "frontend", "src", "components", "projects", "civil-engineering",
            "CivilEngineeringDocumentRegisterPanel.tsx"));

        source.Should().Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("ClientRequestId")
            .And.Contain("CryptographicOperations.FixedTimeEquals")
            .And.Contain("db.AuditLogs.Add")
            .And.Contain("OwnerUserId == request.ReviewerUserId")
            .And.Contain("RequireProjectAsync")
            .And.Contain("TenantId");
        source.Should().NotContain("File.WriteAllBytes")
            .And.NotContain("new WorkflowInstance");
        panel.Should().Contain("Published DMS file version")
            .And.Contain("Select DMS version")
            .And.Contain("Select owner")
            .And.Contain("Select reviewer")
            .And.Contain("document.ownerUserId === user?.id")
            .And.Contain("document.reviewerUserId === user?.id")
            .And.NotContain("type=\"text\"");
    }

    [Fact]
    public void Dms_governance_baseline_uses_existing_central_permissions_and_retention_controls()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", DmsGovernanceMigrationId + ".cs"));

        migration.Should().Contain("TDC-CIV-ENGINEERING-FILE")
            .And.Contain("TDC-CIVIL-ENGINEERING-RESTRICTED")
            .And.Contain("TDC-CIVIL-ENGINEERING-RETENTION-7Y")
            .And.Contain("civil-engineering.documents.manage")
            .And.Contain("civil-engineering.transactions.approve")
            .And.Contain("RequiresLegalHoldReview")
            .And.Contain("AllowDestruction")
            .And.Contain("NOT EXISTS")
            .And.NotContain("File.WriteAllBytes");
        migration.Should().Contain($"[Migration(\"{DmsGovernanceMigrationId}\")]");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
