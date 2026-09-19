using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyJointMeasurementGuardTests
{
    [Fact]
    public void Migration_is_narrow_discoverable_and_enforces_joint_measurement_lifecycle()
    {
        var root = FindRepositoryRoot();
        const string migrationId = "20260810041753_AddQuantitySurveyJointMeasurements";
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain($"[Migration(\"{migrationId}\")]" );
        Count(migration, "CREATE TABLE [QuantitySurveyJointMeasurement").Should().Be(5);
        migration.Should().Contain("TR_QsJointRequest_Guard");
        migration.Should().Contain("Invalid joint-measurement lifecycle transition.");
        migration.Should().Contain("independent maker-checker lineage");
        migration.Should().Contain("central-DMS lineage");
        migration.Should().Contain("QS-DEC-007");
        migration.Should().Contain("QS-DEC-013");
        migration.Should().Contain("e.[Code]='QS_MEASUREMENT'");
        migration.Should().NotContain("FinanceSettings");
        migration.Should().NotContain("ProcurementSupplierOnboarding");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_reuses_shared_workflow_dms_external_access_and_remeasurement_owners()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyJointMeasurementService.cs"));
        var project = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects",
            "ProjectServices.cs"));
        var approval = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects",
            "ProjectService.BoqRemeasurements.cs"));

        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("BusinessPartnerUsers");
        service.Should().Contain("ProjectExternalAccessPolicies");
        service.Should().Contain("WorkflowSignatureValidator.Validate");
        service.Should().Contain("workflow.SubmitAsync");
        service.Should().Contain("controlledFiles.UploadAsync");
        service.Should().Contain("DocumentType = entity.EvidenceMetadataTemplate.DocumentType");
        service.Should().NotContain("DocumentType = request.EvidenceType.ToString()");
        service.Should().Contain("CreateProjectBoqRemeasurementAsync");
        service.Should().Contain("RemeasurementClientRequestId");
        project.Should().Contain("JointMeasurement");
        approval.Should().Contain("QuantitySurveyJointMeasurementStatuses.Applied");
    }

    [Fact]
    public void Frontend_uses_controlled_selectors_and_shared_internal_external_workspace()
    {
        var root = FindRepositoryRoot();
        var workspace = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "quantity-survey",
            "QuantitySurveyJointMeasurementWorkspace.tsx"));
        var externalPage = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "external-portal",
            "projects", "[id]", "page.tsx"));

        workspace.Should().Contain("approvedBoqLines.map");
        workspace.Should().Contain("contractorPartners.map");
        workspace.Should().Contain("consultantPartners.map");
        workspace.Should().Contain("recordedMeasurements.map");
        workspace.Should().Contain("requestIds.current");
        workspace.Should().NotContain("businessPartnerId\" type=\"text");
        externalPage.Should().Contain("QuantitySurveyJointMeasurementWorkspace");
    }

    private static int Count(string value, string token) => value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
