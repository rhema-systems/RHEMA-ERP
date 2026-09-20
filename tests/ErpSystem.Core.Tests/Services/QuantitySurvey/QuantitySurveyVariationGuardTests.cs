using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyVariationGuardTests
{
    [Fact]
    public void Migration_is_scoped_and_enforces_lineage_lifecycle_sod_and_append_only_history()
    {
        var source = Source("src", "ErpSystem.Data", "LegacyMigrationsArchive",
            "20260810234912_AddQuantitySurveyVariationLifecycle.cs");

        source.Should().Contain("QuantitySurveyVariationValuationLines")
            .And.Contain("QuantitySurveyVariationEvidence")
            .And.Contain("QuantitySurveyVariationRevisions")
            .And.Contain("TR_QS0508_ProjectVariation_Governance")
            .And.Contain("TR_QS0508_VariationLines_Governance")
            .And.Contain("TR_QS0508_VariationEvidence_AppendOnly")
            .And.Contain("TR_QS0508_VariationRevisions_AppendOnly")
            .And.Contain("QS-DEC-011")
            .And.Contain("QS_VARIATION")
            .And.Contain("ProjectBoqVersionLines")
            .And.Contain("CentralDocumentMetadataTemplates")
            .And.Contain("i.ApprovedById = i.PreparedById")
            .And.Contain("i.ApprovedById = i.SubmittedById")
            .And.Contain("Direct legacy variation mutation is disabled")
            .And.NotContain("CreateTable(\n                name: \"Contracts\"")
            .And.NotContain("CreateTable(\n                name: \"Projects\"")
            .And.NotContain("CreateTable(\n                name: \"ProjectBoqVersions\"");
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
