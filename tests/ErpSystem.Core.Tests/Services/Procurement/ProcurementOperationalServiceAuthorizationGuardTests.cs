using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementOperationalServiceAuthorizationGuardTests
{
    private static readonly string[] OperationalServices =
    [
        "BusinessPartnerRegistrationService.cs",
        "ProcurementAppSubmissionService.cs",
        "ProcurementBidderCommunicationService.cs",
        "ProcurementCalendarService.cs",
        "ProcurementContractOperationsService.cs",
        "ProcurementEvaluationCommitteeControlService.cs",
        "ProcurementExceptionalSourcingControlService.cs",
        "ProcurementFrameworkAgreementService.cs",
        "ProcurementFrameworkCallOffService.cs",
        "ProcurementGhanepsExchangeService.cs",
        "ProcurementPrequalificationService.cs",
        "ProcurementPurchaseOrderAmendmentService.cs",
        "ProcurementReceiptInspectionService.cs",
        "ProcurementRequisitionAuthorityRouteService.cs",
        "ProcurementRequisitionBudgetControlService.cs",
        "ProcurementRequisitionLinkageService.cs",
        "ProcurementRequisitionSourcingReleaseService.cs",
        "ProcurementRequisitionSubmissionControlService.cs",
        "ProcurementRfqControlService.cs",
        "ProcurementSourcingCaseService.cs",
        "ProcurementSpecificationTemplateService.cs",
        "ProcurementSupplierApplicantAccessService.cs",
        "ProcurementSupplierAvlService.cs",
        "ProcurementSupplierDueDiligenceService.cs",
        "ProcurementSupplierEvidencePackService.cs",
        "ProcurementSupplierOnboardingTokenService.cs",
        "ProcurementSupplierPerformanceScorecardService.cs",
        "ProcurementSupplierRiskService.cs",
        "ProcurementTenderControlService.cs",
        "ProcurementTenderDocumentControlService.cs"
    ];

    private static readonly string[] ForbiddenLegacyRoleChecks =
    [
        "HasRole(\"TenantAdmin\")",
        "HasRole(\"Admin\")",
        "HasRole(\"Administrator\")",
        "HasRole(\"SystemAdmin\")",
        "Roles.Contains(\"Admin\")",
        "Roles.Contains(\"BusinessPartnerAdmin\")"
    ];

    [Fact]
    public void Operational_services_do_not_bypass_capabilities_with_legacy_admin_roles()
    {
        var serviceDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Core",
            "Services",
            "Procurement");

        foreach (var service in OperationalServices)
        {
            var source = File.ReadAllText(Path.Combine(serviceDirectory, service));
            foreach (var forbidden in ForbiddenLegacyRoleChecks)
            {
                Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData("ProcurementAppSubmissionService.cs")]
    [InlineData("ProcurementRequisitionBudgetControlService.cs")]
    [InlineData("ProcurementRfqControlService.cs")]
    [InlineData("ProcurementSupplierAvlService.cs")]
    [InlineData("ProcurementTenderControlService.cs")]
    public void Internal_read_guards_require_the_registered_records_read_permission(string service)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ErpSystem.Core",
            "Services",
            "Procurement",
            service));

        Assert.Contains(
            "HasRegisteredProcurementPermission(\"procurement.records.read\")",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
               throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
