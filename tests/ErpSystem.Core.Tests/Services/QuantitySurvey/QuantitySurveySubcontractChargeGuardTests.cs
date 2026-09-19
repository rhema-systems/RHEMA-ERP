using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveySubcontractChargeGuardTests
{
    [Fact]
    public void Migration_is_scoped_and_enforces_charge_dms_workflow_and_valuation_lineage()
    {
        var source = Source("src", "ErpSystem.Data", "LegacyMigrationsArchive",
            "20260811063100_AddQuantitySurveySubcontractChargeLifecycle.cs");

        source.Should().Contain("QuantitySurveySubcontractChargeNotices")
            .And.Contain("QuantitySurveySubcontractChargeEvidence")
            .And.Contain("QuantitySurveySubcontractChargeRevisions")
            .And.Contain("TR_QS0522_SubcontractCharges_Governance")
            .And.Contain("TR_QS0522_SubcontractChargeEvidence_AppendOnly")
            .And.Contain("TR_QS0522_SubcontractChargeRevisions_AppendOnly")
            .And.Contain("TR_QS0521_SubcontractValuations_Governance")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("WorkflowDefinitions")
            .And.Contain("Status IN ('Allocated','Applied')")
            .And.NotContain("CREATE TABLE [dbo].[Projects]")
            .And.NotContain("CREATE TABLE [dbo].[Contracts]")
            .And.NotContain("CREATE TABLE [dbo].[Users]");
    }

    [Fact]
    public void Service_uses_frozen_policy_workflow_dms_notifications_and_server_side_charge_totals()
    {
        var chargeService = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveySubcontractChargeService.cs");
        var valuationService = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveySubcontractService.cs");

        chargeService.Should().Contain("QS-DEC-012")
            .And.Contain("QS-DEC-008")
            .And.Contain("value.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus")
            .And.Contain("centralDocuments")
            .And.Contain("QuantitySurvey.SubcontractChargeNoticeIssued")
            .And.Contain("QuantitySurvey.SubcontractChargeDecision")
            .And.NotContain("BusinessPartnerLifecyclePolicy.IsOperationalRegistration(value.RegistrationStatus)");
        valuationService.Should().Contain("QuantitySurveySubcontractChargeRules.SumApproved")
            .And.Contain("if (charges.Count > 0) await SaveAsync(token)")
            .And.Contain("Only independently approved, unapplied charge notices");
    }

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
