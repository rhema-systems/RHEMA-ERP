using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveySubcontractGuardTests
{
    [Fact]
    public void Migration_is_scoped_and_enforces_lineage_terminal_immutability_and_append_only_evidence()
    {
        var source = Source("src", "ErpSystem.Data", "Migrations",
            "20260811050412_AddQuantitySurveySubcontractLifecycle.cs");

        source.Should().Contain("QuantitySurveySubcontracts")
            .And.Contain("QuantitySurveySubcontractValuations")
            .And.Contain("QuantitySurveySubcontractEvidence")
            .And.Contain("QuantitySurveySubcontractRevisions")
            .And.Contain("TR_QS0521_Subcontracts_Governance")
            .And.Contain("TR_QS0521_SubcontractValuations_Governance")
            .And.Contain("TR_QS0521_SubcontractEvidence_AppendOnly")
            .And.Contain("TR_QS0521_SubcontractRevisions_AppendOnly")
            .And.Contain("c.ContractType <> 'Works'")
            .And.Contain("c.AllowSubcontracting = 0")
            .And.Contain("c.SubcontractPaymentTermId <> i.PaymentTermId")
            .And.Contain("i.ApprovedBackChargeAmount <> 0 OR i.ApprovedContraChargeAmount <> 0")
            .And.Contain("Approved or paid QS subcontract valuation financial and policy lineage is immutable")
            .And.NotContain("name: \"Projects\"")
            .And.NotContain("name: \"Contracts\"")
            .And.NotContain("name: \"VendorInvoices\"")
            .And.NotContain("name: \"CentralDocumentRecords\"");

        var certificateGuard = Source("src", "ErpSystem.Data", "Migrations",
            "20260811061000_HardenQuantitySurveySubcontractCertificates.cs");
        certificateGuard.Should().Contain("TR_QS0521_SubcontractCertificates_Governance")
            .And.Contain("QuantitySurveySubcontractValuationId")
            .And.Contain("SubcontractorBusinessPartnerId")
            .And.Contain("certificate totals do not reconcile")
            .And.Contain("financial lineage is immutable")
            .And.Contain("VendorInvoice");
    }

    [Fact]
    public void Service_reuses_module_owners_and_preserves_tenant_external_and_idempotency_guards()
    {
        var source = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveySubcontractService.cs");

        source.Should().Contain("IsolationLevel.Serializable")
            .And.Contain("IControlledFileUploadService")
            .And.Contain("ICentralDocumentRepositoryFileService")
            .And.Contain("IVendorInvoiceService")
            .And.Contain("IWorkflowIntegrationService")
            .And.Contain("BusinessPartnerUsers.IgnoreQueryFilters()")
            .And.Contain("ProjectExternalAccessPolicies")
            .And.Contain("value.TenantId == TenantId")
            .And.Contain("LastMutationClientRequestId == request.ClientRequestId")
            .And.Contain("QuantitySurveySubcontractRevisions.AsNoTracking()")
            .And.Contain("ApprovedBackChargeAmount = 0")
            .And.Contain("ApprovedContraChargeAmount = 0")
            .And.NotContain("catch (Exception")
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
