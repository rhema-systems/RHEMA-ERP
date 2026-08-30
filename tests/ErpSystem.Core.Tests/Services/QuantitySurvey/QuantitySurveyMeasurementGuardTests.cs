using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyMeasurementGuardTests
{
    [Fact]
    public void Migration_is_narrow_discoverable_and_guards_full_lineage()
    {
        var root = FindRepositoryRoot();
        const string migrationId = "20260810023000_AddQuantitySurveyMeasurementSheets";
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));
        migration.Should().Contain($"[Migration(\"{migrationId}\")] ".Trim());
        Count(migration, "CREATE TABLE [QuantitySurveyMeasurement").Should().Be(4);
        migration.Should().Contain("TR_QsMeasurementSheets_Guard");
        migration.Should().Contain("TR_QsMeasurementLines_Guard");
        migration.Should().Contain("TR_QsMeasurementAttachments_Guard");
        migration.Should().Contain("TR_QsMeasurementRevisions_Guard");
        migration.Should().Contain("ProjectBoqVersions");
        migration.Should().Contain("QS-DEC-007");
        migration.Should().Contain("CentralDocumentMetadataTemplates");
        migration.Should().Contain("quantity-survey-measurement-evidence");
        migration.Should().Contain("exact server-calculated typed formula");
        migration.Should().Contain("revision history is append-only");
        migration.Should().NotContain("FinanceSettings");
        migration.Should().NotContain("ProcurementSupplier");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_reuses_projects_policy_scan_dms_audit_and_retry_controls()
    {
        var service = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Services",
            "QuantitySurvey", "QuantitySurveyMeasurementService.cs"));
        service.Should().Contain("IProjectService");
        service.Should().Contain("projectService.HasProjectAccessAsync(projectId)");
        service.Should().NotContain("projectService.GetProjectByIdAsync(projectId)");
        service.Should().Contain("IControlledFileUploadService");
        service.Should().Contain("ICentralDocumentRepositoryFileService");
        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("ProjectBoqVersionStatuses.Approved");
        service.Should().Contain("ProjectDrawingStatuses.ApprovedForConstruction");
        service.Should().Contain("DecisionKey == \"QS-DEC-007\"");
        service.Should().Contain("FileVirusScanStatus.Clean");
        service.Should().Contain("RequirePublishedGovernance = true");
        service.Should().Contain("DocumentType = sheet.EvidenceMetadataTemplate.DocumentType");
        service.Should().NotContain("DocumentType = request.EvidenceType.ToString()");
        service.Should().Contain("ApplyRowVersion(entity, request.RowVersion)");
        service.Should().Contain("FixedEquals(existing.RequestHash, requestHash)");
        service.Should().NotContain("new Project(");
        service.Should().NotContain("new ProjectBoqVersion");
        service.Should().NotContain("new CentralDocumentRecord");
    }

    [Fact]
    public void Project_access_only_checks_use_the_central_lightweight_authorization_boundary()
    {
        var root = FindRepositoryRoot();
        var interfaceSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Interfaces", "Projects", "IProjectServices.cs"));
        var authorizationSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectService.Authorization.cs"));
        interfaceSource.Should().Contain("Task<bool> HasProjectAccessAsync(Guid projectId)");
        authorizationSource.Should().Contain("GetProjectForOperationAsync(projectId, ProjectAccessOperation.View)")
            .And.Contain("catch (UnauthorizedAccessException)")
            .And.Contain("project.TenantId != _currentUserProvider.TenantId");

        var accessOnlyFiles = new[]
        {
            "QuantitySurveyAdvanceRecoveryService.cs",
            "QuantitySurveyContractClaimService.cs",
            "QuantitySurveyContractCommercialTermsService.cs",
            "QuantitySurveyDayworkService.cs",
            "QuantitySurveyDesignRevisionImpactService.cs",
            "QuantitySurveyEscalationCalculationService.cs",
            "QuantitySurveyEscalationDisputeService.cs",
            "QuantitySurveyFinalAccountService.cs",
            "QuantitySurveyJointMeasurementService.cs",
            "QuantitySurveyMaterialReconciliationService.cs",
            "QuantitySurveyMeasurementService.cs",
            "QuantitySurveyPaymentCertificateService.cs",
            "QuantitySurveySubcontractChargeService.cs",
            "QuantitySurveySubcontractService.cs",
            "QuantitySurveyValuationWorksheetService.cs",
            "QuantitySurveyVariationService.cs"
        };
        foreach (var file in accessOnlyFiles)
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "QuantitySurvey", file));
            source.Should().Contain("HasProjectAccessAsync", $"{file} must keep project authorization without materializing the project aggregate")
                .And.NotContain("GetProjectByIdAsync", $"{file} must not materialize the complete project just to authorize access");
        }

        foreach (var path in new[]
                 {
                     Path.Combine(root, "src", "ErpSystem.Data", "Services", "QuantitySurveyEscalationFormulaService.cs"),
                     Path.Combine(root, "src", "ErpSystem.Api", "Services", "Documents", "QuantitySurvey", "QuantitySurveyPaymentCertificateDocumentBuilder.cs"),
                     Path.Combine(root, "src", "ErpSystem.Api", "Services", "Documents", "QuantitySurvey", "QuantitySurveyEscalationDisputeAuditPackDocumentBuilder.cs")
                 })
        {
            var source = File.ReadAllText(path);
            source.Should().Contain("HasProjectAccessAsync")
                .And.NotContain("GetProjectByIdAsync");
        }
    }

    [Fact]
    public void QuantitySurvey_acceptance_runner_consumes_validated_current_fixture_identifiers()
    {
        var root = FindRepositoryRoot();
        var runner = File.ReadAllText(Path.Combine(root, "e2e-tests", ".qs-assurance-runner.mjs"));
        var seedInvoker = File.ReadAllText(Path.Combine(
            root, "scripts", "quantity-survey", "Invoke-QuantitySurveyE2ESeed.ps1"));
        var seedSql = File.ReadAllText(Path.Combine(
            root, "scripts", "quantity-survey", "seed-quantity-survey-e2e.sql"));

        runner.Should().Contain("Invoke-QuantitySurveyE2ESeed.ps1")
            .And.Contain("ConnectionStrings__DefaultConnection")
            .And.Contain("RhemaQsUatAssurance_[0-9]{8}")
            .And.Contain("fixture.ProjectId")
            .And.Contain("fixture.InterimValuationId")
            .And.Contain("fixture.ApprovedBoqVersionId")
            .And.Contain("fixture.ContractorBusinessPartnerId")
            .And.Contain("fixture.ConsultantBusinessPartnerId");
        runner.Should().NotMatchRegex(
            "QS_ACCEPTANCE_(?:PROJECT|VALUATION|BOQ_VERSION|CONTRACTOR|CONSULTANT)_ID:\\s*'[0-9a-f-]{36}'",
            "acceptance IDs must come from the fixture applied to the current database");
        seedInvoker.Should().Contain("OutputJsonPath")
            .And.Contain("QS-E2E-READY|*");
        seedSql.Should().Contain("QS E2E output validation failed for the fixture project.")
            .And.Contain("QS E2E output validation failed for the approved BoQ version.")
            .And.Contain("QS E2E output validation failed for one or more acceptance actors.");
    }

    [Fact]
    public void Frontend_uses_controlled_selectors_and_stable_retry_ids()
    {
        var frontend = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "src", "components",
            "quantity-survey", "QuantitySurveyMeasurementsDialog.tsx"));
        frontend.Should().Contain("approvedBoqLines.map");
        frontend.Should().Contain("approvedDrawings.map");
        frontend.Should().Contain("quantity-survey.measurements.manage");
        frontend.Should().Contain("quantity-survey.audit.read");
        frontend.Should().Contain("current?.fingerprint === fingerprint");
        frontend.Should().Contain("completeRequest(key)");
        frontend.Should().NotContain("ProjectBoqVersionLineId\" type=\"text");
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
