using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyAdvanceRecoveryGuardTests
{
    [Fact]
    public void Recovery_uses_the_governed_percentage_and_caps_at_the_remaining_advance()
    {
        QuantitySurveyAdvanceRecoveryRules.CalculateCertificateRecovery(1_000m, 10m, 250m, 0m)
            .Should().Be(25m);
        QuantitySurveyAdvanceRecoveryRules.CalculateCertificateRecovery(1_000m, 10m, 250m, 985m)
            .Should().Be(15m);
        QuantitySurveyAdvanceRecoveryRules.Remaining(1_000m, 1_025m).Should().Be(0m);
    }

    [Theory]
    [InlineData(0, 10, 100, 0)]
    [InlineData(100, 0, 100, 0)]
    [InlineData(100, 101, 100, 0)]
    [InlineData(100, 10, -1, 0)]
    [InlineData(100, 10, 100, -1)]
    public void Recovery_rejects_invalid_commercial_inputs(
        decimal original, decimal percentage, decimal gross, decimal committed)
    {
        Action action = () => QuantitySurveyAdvanceRecoveryRules.CalculateCertificateRecovery(
            original, percentage, gross, committed);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Migration_enforces_finance_lineage_maker_checker_and_append_only_audit()
    {
        var source = Source("src", "ErpSystem.Data", "LegacyMigrationsArchive",
            "20260810185711_AddQuantitySurveyAdvanceRecoveryLifecycle.cs");
        source.Should().Contain("TR_QsAdvanceRecoveryAgreements_QS0505Guard")
            .And.Contain("TR_ProjectPaymentCertificates_QS0505AdvanceRecoveryGuard")
            .And.Contain("TR_QsAdvanceRecoveryRevisions_QS0505AppendOnly")
            .And.Contain("[IsSupplierAdvance] = 1")
            .And.Contain("s.[SupplierCode] = bp.[PartnerCode]")
            .And.Contain("i.[ApprovedById] = i.[PreparedById]")
            .And.Contain("i.[ApprovedById] = i.[SubmittedById]")
            .And.Contain("advance-recovery audit revisions are append-only")
            .And.Contain("QuantitySurveyAdvanceRecoveryAgreementId");
    }

    [Fact]
    public void Qs_consumes_the_finance_owned_supplier_advance_read_contract()
    {
        var qs = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyAdvanceRecoveryService.cs");
        var finance = Source("src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorPaymentService.cs");
        qs.Should().Contain("IVendorPaymentService vendorPaymentService")
            .And.Contain("GetPostedSupplierAdvancesAsync(contract.BusinessPartnerId")
            .And.NotContain("payment.SupplierId != contract.BusinessPartnerId");
        finance.Should().Contain("GetPostedSupplierAdvancesAsync")
            .And.Contain("ResolveSupplierIdForQueryAsync")
            .And.Contain("payment.IsSupplierAdvance")
            .And.Contain("payment.JournalEntryId.HasValue");
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
