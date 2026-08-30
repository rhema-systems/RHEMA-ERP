using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringCommercialReadinessGuardTests
{
    [Fact]
    public void Civil_readiness_revalidates_authoritative_owner_records_without_creating_a_parallel_commercial_or_finance_store()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Services", "CivilEngineeringDesignService.CommercialReadiness.cs"));
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Projects", "CivilEngineeringDesignDtos.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Projects", "CivilEngineeringDesignCasesController.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "projects", "civil-engineering", "CivilEngineeringCommercialReadinessPanel.tsx"));

        service.Should().Contain("RequiredAsync(designCaseId, false, token)")
            .And.Contain("RequireProjectAsync(designCase.ProjectId)")
            .And.Contain("db.ProjectBoqVersions")
            .And.Contain("db.QuantitySurveyEstimateVersions")
            .And.Contain("db.ProjectBudgetRevisions")
            .And.Contain("db.Contracts")
            .And.Contain("db.BusinessPartners")
            .And.Contain("db.PurchaseRequisitions")
            .And.Contain("db.PurchaseOrders")
            .And.Contain("db.QuantitySurveyValuationWorksheets")
            .And.Contain("db.ProjectPaymentCertificates")
            .And.Contain("db.ProjectCivilIpcEndorsements")
            .And.Contain("CentralDocumentEvidenceRules.CurrentPublished()")
            .And.NotContain("db.SaveChanges")
            .And.NotContain("new Contract")
            .And.NotContain("new PurchaseOrder")
            .And.NotContain("new QuantitySurveyEstimateVersion");
        dto.Should().Contain("CivilEngineeringCommercialReadinessDto")
            .And.Contain("ReadyForExecution")
            .And.Contain("RevalidatedAt");
        controller.Should().Contain("commercial-readiness")
            .And.Contain("WorkspaceRead");
        panel.Should().Contain("Revalidate")
            .And.Contain("Civil does not copy or post these owner records")
            .And.NotContain("<Input")
            .And.NotContain("<Select");
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
