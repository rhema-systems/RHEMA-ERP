using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyPaymentCertificateSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Generate), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Update), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Submit), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.HandoffToAp), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.RefreshPayment), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.Document), QuantitySurveyAccessControlRegistry.AuditRead)]
    [InlineData(nameof(QuantitySurveyPaymentCertificatesController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyPaymentCertificatesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_tenant_safe_and_returns_safe_problem_details()
    {
        typeof(QuantitySurveyPaymentCertificatesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyPaymentCertificatesController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/payment-certificates");
        var controller = Source("src", "ErpSystem.Api", "Controllers", "QuantitySurvey", "QuantitySurveyPaymentCertificatesController.cs");
        var service = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyPaymentCertificateService.cs");
        controller.Should().Contain("correlationId")
            .And.Contain("catch (ProcurementBudgetCommitmentLifecycleException exception)")
            .And.Contain("exception.Code")
            .And.NotContain("catch (Exception");
        service.Should().Contain("value.TenantId == TenantId")
            .And.Contain("IVendorInvoiceService")
            .And.Contain("IWorkflowIntegrationService")
            .And.Contain("ICentralDocumentRepositoryFileService")
            .And.Contain("IsolationLevel.Serializable")
            .And.NotContain("db.Set<VendorInvoice>().Add")
            .And.NotContain("exception.ToString()");
    }

    [Fact]
    public void Legacy_project_mutations_cannot_bypass_the_governed_workspace()
    {
        var source = Source("src", "ErpSystem.Core", "Services", "Projects", "ProjectService.CommercialAdministration.cs");
        source.Should().Contain("Use the governed QS payment-certificate workspace")
            .And.Contain("Governed QS payment certificates can be amended only")
            .And.Contain("Governed QS payment certificates cannot be deleted");
    }

    [Fact]
    public void Approval_issues_governed_evidence_before_finance_handoff_and_reads_live_finance_state()
    {
        var service = Source("src", "ErpSystem.Api", "Services", "QuantitySurvey",
            "QuantitySurveyPaymentCertificateService.cs");

        service.Should().Contain("if (approve)")
            .And.Contain("resumesCommittedApproval")
            .And.Contain("await RenderAsync(id, correlationId, token)")
            .And.Contain("return await HandoffToApAsync(id")
            .And.Contain("AccessProfile = metadataAccessProfile")
            .And.NotContain("AccessProfile = \"Module restricted\", VersionStatus = \"Approved\"")
            .And.Contain("Reference = $\"QS-CERT:{entity.Id:N}\"")
            .And.Contain("QuantitySurveyPaymentCertificateReconciliationRules.Evaluate")
            .And.Contain("invoice?.JournalEntryId.HasValue == true")
            .And.NotContain("db.Set<VendorInvoice>().Add");
    }

    private static string Source(params string[] path) => File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(path)));
    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
