using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyContractClaimGuardTests
{
    [Fact]
    public void Migration_is_scoped_and_enforces_claim_lineage_workflow_sod_and_append_only_history()
    {
        var source = Source("src", "ErpSystem.Data", "Migrations",
            "20260811010013_AddQuantitySurveyContractClaimLifecycle.cs");

        source.Should().Contain("QuantitySurveyContractClaims")
            .And.Contain("QuantitySurveyContractClaimEvidence")
            .And.Contain("QuantitySurveyContractClaimRevisions")
            .And.Contain("TR_QS0509_ContractClaims_Governance")
            .And.Contain("TR_QS0509_ContractClaimEvidence_AppendOnly")
            .And.Contain("TR_QS0509_ContractClaimRevisions_AppendOnly")
            .And.Contain("QS-DEC-011")
            .And.Contain("QS_CLAIM")
            .And.Contain("SourcePurchaseRequisitionId")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("i.ApprovedById = i.SubmittedById")
            .And.Contain("i.ApprovedById = i.QsVettedById")
            .And.Contain("wi.Status NOT IN (3,4)")
            .And.NotContain("CreateTable(\n                name: \"Contracts\"")
            .And.NotContain("CreateTable(\n                name: \"Projects\"")
            .And.NotContain("CreateTable(\n                name: \"WorkflowDefinitions\"")
            .And.NotContain("CreateTable(\n                name: \"CentralDocumentRecords\"");
    }

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { Environment.CurrentDirectory, Path.GetDirectoryName(sourceFile) })
            for (var directory = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
