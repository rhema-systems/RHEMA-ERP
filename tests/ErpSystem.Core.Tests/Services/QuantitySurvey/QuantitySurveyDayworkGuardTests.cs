using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyDayworkGuardTests
{
    [Fact]
    public void Migration_is_scoped_and_enforces_lineage_rates_signatures_evidence_and_parent_readiness()
    {
        var source = Source("src", "ErpSystem.Data", "Migrations",
            "20260811013838_AddQuantitySurveyDayworkLifecycle.cs");

        source.Should().Contain("QuantitySurveyDayworkSheets")
            .And.Contain("QuantitySurveyDayworkLines")
            .And.Contain("QuantitySurveyDayworkEvidence")
            .And.Contain("QuantitySurveyDayworkRevisions")
            .And.Contain("TR_QS0510_DayworkSheets_Governance")
            .And.Contain("TR_QS0510_DayworkLines_Governance")
            .And.Contain("TR_QS0510_DayworkEvidence_AppendOnly")
            .And.Contain("TR_QS0510_DayworkRevisions_AppendOnly")
            .And.Contain("TR_QS0510_VariationDayworkEligibility")
            .And.Contain("QS-DEC-011")
            .And.Contain("BusinessPartnerUsers")
            .And.Contain("QuantitySurveyRateLibraryRates")
            .And.Contain("dbo.UnitsOfMeasure")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("i.VerifiedById = i.ContractorSignedById")
            .And.Contain("i.Status IN (1,2,3)")
            .And.NotContain("dbo.UnitOfMeasures")
            .And.NotContain("CreateTable(\n                name: \"Contracts\"")
            .And.NotContain("CreateTable(\n                name: \"Projects\"")
            .And.NotContain("CreateTable(\n                name: \"QuantitySurveyRateLibraryRates\"")
            .And.NotContain("CreateTable(\n                name: \"CentralDocumentRecords\"");
    }

    [Fact]
    public void Variation_submission_requires_verified_reconciled_daywork_detail()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyVariationService.cs");

        source.Should().Contain("QuantitySurveyDayworkRules.IsSupportedVariationType")
            .And.Contain("sheets.Count == 0")
            .And.Contain("sheet.Status != QuantitySurveyDayworkSheetStatus.Verified")
            .And.Contain("Round(sheets.Sum(sheet => sheet.TotalAmount))")
            .And.Contain("Round(value.EstimatedAmount ?? 0m)");
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
