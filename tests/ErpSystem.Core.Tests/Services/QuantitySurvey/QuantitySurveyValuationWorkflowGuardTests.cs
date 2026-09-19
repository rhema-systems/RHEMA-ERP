using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyValuationWorkflowGuardTests
{
    private const string MigrationId = "20260810134646_AddQuantitySurveyInterimValuationWorkflow";

    [Fact]
    public void Migration_is_a_narrow_discoverable_lifecycle_delta_with_sql_hard_stops()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", MigrationId + ".cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "FastBuildMigrationMetadata.cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain("migrationBuilder.CreateTable(");
        migration.Should().Contain("name: \"QuantitySurveyValuationWorksheetEvidence\"");
        migration.Should().NotContain("name: \"AspNetRoles\"");
        migration.Should().Contain("defaultValue: \"Draft\"");
        migration.Should().Contain("TR_QsValuationWorksheets_Guard");
        migration.Should().Contain("QS_VALUATION_TRANSITION_INVALID");
        migration.Should().Contain("QS_VALUATION_POLICY_IMMUTABLE");
        migration.Should().Contain("QS_VALUATION_WORKFLOW_INVALID");
        migration.Should().Contain("QS_VALUATION_SOD_BLOCKED");
        migration.Should().Contain("TR_QsValuationWorksheetEvidence_AppendOnly");
        migration.Should().Contain("VirusScanStatus=2");
        migration.Should().Contain("v.Status='Validated'");
        metadata.Should().Contain($"Migration(\"{MigrationId}\")");
        preflight.Should().Contain($"GUARD_COVERAGE|{MigrationId}");
    }

    [Fact]
    public void Service_keeps_shared_workflow_dms_and_decision_owners_atomic()
    {
        var root = FindRepositoryRoot();
        var lifecycle = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyValuationWorksheetLifecycleService.cs"));

        lifecycle.Should().Contain("IsolationLevel.Serializable");
        lifecycle.Should().Contain("workflow.SubmitAsync");
        lifecycle.Should().Contain("workflow.ProcessApprovalAsync");
        lifecycle.Should().Contain("WorkflowStepType.Approval");
        lifecycle.Should().Contain("controlledFiles.UploadAsync");
        lifecycle.Should().Contain("centralDocuments.RegisterAsync");
        lifecycle.Should().Contain("entity.EvidenceMetadataTemplate?.DocumentType");
        lifecycle.Should().NotContain("DocumentType = request.EvidenceType.ToString()");
        lifecycle.Should().Contain("db.ChangeTracker.Clear()");
        lifecycle.Should().Contain("CleanupFailedValuationDocumentAsync");
        lifecycle.Should().Contain("ExceptionDispatchInfo.Capture(originalFailure).Throw()");
        lifecycle.Should().Contain("ProjectExternalAccessPolicies");
        lifecycle.Should().Contain("BusinessPartnerUsers");
        lifecycle.Should().Contain("ValidateValuationReadinessAsync(entity, false, false, token);");
        lifecycle.Should().Contain("QuantitySurveyValuationWorksheetRules.RequireIndependentApprover");
    }

    [Fact]
    public void Frontend_uses_controlled_partner_selectors_and_shared_internal_external_workspaces()
    {
        var root = FindRepositoryRoot();
        var dialog = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "quantity-survey",
            "QuantitySurveyValuationWorksheetDialog.tsx"));
        var portal = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "quantity-survey",
            "QuantitySurveyValuationPortalWorkspace.tsx"));

        dialog.Should().Contain("lookups.contractors.map");
        dialog.Should().Contain("lookups.consultants.map");
        dialog.Should().Contain("submitApproval");
        dialog.Should().Contain("addEvidence");
        dialog.Should().NotContain("contractorBusinessPartnerId\" type=\"text");
        portal.Should().Contain("service.externalList");
        portal.Should().Contain("service.externalGet");
        portal.Should().Contain("service.saveContractorClaim");
        portal.Should().Contain("service.submitContractorClaim");
        portal.Should().Contain("service.endorseConsultant");
    }

    [Fact]
    public void Every_qs_dec_008_runtime_consumer_accepts_the_registered_string_enum_contract()
    {
        var root = FindRepositoryRoot();
        var services = new[]
        {
            "QuantitySurveyPaymentCertificateService.cs",
            "QuantitySurveyAdvanceRecoveryService.cs",
            "QuantitySurveySubcontractService.cs",
            "QuantitySurveySubcontractChargeService.cs"
        };

        foreach (var service in services)
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services",
                "QuantitySurvey", service));
            source.Should().Contain("new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)",
                $"{service} consumes the string-valued QS-DEC-008 taxHandling field");
        }
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
