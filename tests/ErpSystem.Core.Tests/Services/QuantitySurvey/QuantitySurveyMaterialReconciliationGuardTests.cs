using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyMaterialReconciliationGuardTests
{
    [Fact]
    public void Valuation_basis_uses_only_the_governed_rate_source()
    {
        QuantitySurveyMaterialReconciliationRules.AppliedRate(
            QuantitySurveyMaterialValuationBasis.DeliveredCost, 12.25m, 15m).Should().Be(12.25m);
        QuantitySurveyMaterialReconciliationRules.AppliedRate(
            QuantitySurveyMaterialValuationBasis.ApprovedRate, 12.25m, 15m).Should().Be(15m);
        QuantitySurveyMaterialReconciliationRules.AppliedRate(
            QuantitySurveyMaterialValuationBasis.LowerOfCostOrApprovedRate, 12.25m, 15m).Should().Be(12.25m);
        QuantitySurveyMaterialReconciliationRules.LineValue(4m, 12.25m).Should().Be(49m);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Line_value_rejects_non_positive_commercial_inputs(decimal quantity, decimal rate)
    {
        Action action = () => QuantitySurveyMaterialReconciliationRules.LineValue(quantity, rate);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Migration_enforces_sources_lifecycle_sod_payment_lineage_and_append_only_history()
    {
        var source = Source("src", "ErpSystem.Data", "Migrations",
            "20260810223350_AddQuantitySurveyMaterialReconciliationLifecycle.cs");

        source.Should().Contain("TR_QS0507_MaterialReconciliation_Governance")
            .And.Contain("TR_QS0507_MaterialReconciliationLines_Governance")
            .And.Contain("TR_QS0507_MaterialReconciliationRevisions_AppendOnly")
            .And.Contain("TR_QS0507_PaymentCertificateMaterialLineage")
            .And.Contain("BusinessPartnerUsers")
            .And.Contain("InventoryIssueVoucherLines")
            .And.Contain("QuantitySurveyValuationWorksheetEvidence")
            .And.Contain("QuantitySurveyRateLibraryRates")
            .And.Contain("Invalid Quantity Survey material-reconciliation lifecycle transition")
            .And.Contain("material-reconciliation revisions are append-only")
            .And.Contain("Payment certificates must freeze exact values")
            .And.Contain("i.ApprovedById = i.PreparedById")
            .And.Contain("i.ApprovedById = i.ContractorConfirmedById")
            .And.Contain("i.ApprovedById = i.SubmittedById");
    }

    [Fact]
    public void Service_uses_controlled_module_owners_and_atomic_idempotent_mutations()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyMaterialReconciliationService.cs");

        source.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("CreateExecutionStrategy")
            .And.Contain("QuantitySurveyMaterialReconciliationRevisions.AsNoTracking()")
            .And.Contain("QuantitySurveyValuationWorksheetEvidence")
            .And.Contain("InventoryIssueVoucherLine")
            .And.Contain("QuantitySurveyRateLibraryRates")
            .And.Contain("IWorkflowIntegrationService")
            .And.Contain("QuantitySurveyAuditEventMap")
            .And.NotContain("exception.ToString()");
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
