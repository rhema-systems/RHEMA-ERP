using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementLegacyRoleAuthorizationSecurityTests
{
    private static readonly Type[] SweptControllers = typeof(AwardVerificationsController).Assembly
        .GetTypes()
        .Where(type =>
            !type.IsAbstract &&
            typeof(ControllerBase).IsAssignableFrom(type) &&
            string.Equals(type.Namespace, typeof(AwardVerificationsController).Namespace, StringComparison.Ordinal))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    private static readonly HashSet<string> LegacyGenericRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuperAdmin", "TenantAdmin", "Manager", "Employee"
    };

    [Fact]
    public void SweptControllersDoNotUseLegacyGenericRoleGates()
    {
        var violations = SweptControllers
            .SelectMany(type => AuthorizeAttributes(type)
                .Select(attribute => new { Endpoint = type.Name, attribute.Roles }))
            .Where(item => SplitRoles(item.Roles).Any(LegacyGenericRoles.Contains))
            .Select(item => $"{item.Endpoint}: {item.Roles}")
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Legacy generic procurement role gates remain:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void SweptControllerPoliciesAreRegisteredProcurementPermissions()
    {
        var registered = ProcurementAccessControlRegistry.Permissions
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = SweptControllers
            .SelectMany(AuthorizeAttributes)
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy) && policy!.StartsWith("procurement.", StringComparison.OrdinalIgnoreCase))
            .Where(policy => !registered.Contains(policy!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(unknown.Length == 0,
            $"Unregistered procurement policies are referenced: {string.Join(", ", unknown)}");
    }

    [Theory]
    [MemberData(nameof(CriticalPolicyMappings))]
    public void CriticalEndpointsUseTheExpectedPermissionPolicy(Type controller, string action, string policy)
    {
        var authorization = controller.GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
        Assert.Equal(policy, authorization!.Policy);
        Assert.Null(authorization.Roles);
    }

    public static TheoryData<Type, string, string> CriticalPolicyMappings => new()
    {
        { typeof(AwardVerificationsController), nameof(AwardVerificationsController.GetVerificationById), "procurement.records.read" },
        { typeof(AwardVerificationsController), nameof(AwardVerificationsController.CompleteVerification), "procurement.tender.evaluate" },
        { typeof(ContractsController), nameof(ContractsController.GetContract), "procurement.records.read" },
        { typeof(ContractsController), nameof(ContractsController.ActivateContract), "procurement.contract.approve" },
        { typeof(EvaluationCriteriaController), nameof(EvaluationCriteriaController.GetAll), "procurement.records.read" },
        { typeof(EvaluationCriteriaController), nameof(EvaluationCriteriaController.Create), "procurement.tender.administer" },
        { typeof(EvaluationTemplatesController), nameof(EvaluationTemplatesController.GetForDropdown), "procurement.records.read" },
        { typeof(EvaluationTemplatesController), nameof(EvaluationTemplatesController.Create), "procurement.tender.administer" },
        { typeof(ExceptionalSourcingControlsController), nameof(ExceptionalSourcingControlsController.RecordRecommendation), "procurement.tender.evaluate" },
        { typeof(PerformanceBondsController), nameof(PerformanceBondsController.ReviewBond), "procurement.contract.approve" },
        { typeof(PrequalificationController), nameof(PrequalificationController.Evaluate), "procurement.tender.evaluate" },
        { typeof(PrequalificationController), nameof(PrequalificationController.Decide), "procurement.sourcing.approve" },
        { typeof(ProcurementAccessControlsController), nameof(ProcurementAccessControlsController.CreateAssignment), "procurement.access.manage" },
        { typeof(ProcurementAccessControlsController), nameof(ProcurementAccessControlsController.AddCommitteeMember), "procurement.access.manage" },
        { typeof(ProcurementConfigurationProfilesController), nameof(ProcurementConfigurationProfilesController.GetHistory), "procurement.audit.read" },
        { typeof(ProcurementConfigurationProfilesController), nameof(ProcurementConfigurationProfilesController.Update), "procurement.access.manage" },
        { typeof(ProcurementMasterDataChangesController), nameof(ProcurementMasterDataChangesController.CreatePolicy), "procurement.access.manage" },
        { typeof(ProcurementMasterDataChangesController), nameof(ProcurementMasterDataChangesController.ActivatePolicy), "procurement.access.manage" },
        { typeof(ProcurementPolicySetsController), nameof(ProcurementPolicySetsController.GetPolicySets), "procurement.records.read" },
        { typeof(ProcurementPolicySetsController), nameof(ProcurementPolicySetsController.Publish), "procurement.access.manage" },
        { typeof(ProcurementSettingsController), nameof(ProcurementSettingsController.UpdateSettings), "procurement.access.manage" },
        { typeof(ProcurementSodControlsController), nameof(ProcurementSodControlsController.GetBlockedAttempts), "procurement.audit.read" },
        { typeof(RfqsController), nameof(RfqsController.CompleteOpening), "procurement.tender.administer" },
        { typeof(RfqsController), nameof(RfqsController.DecideEvaluation), "procurement.tender.approve" },
        { typeof(TenderAwardsController), nameof(TenderAwardsController.ApproveAward), "procurement.tender.approve" },
        { typeof(TenderAwardsController), nameof(TenderAwardsController.CreatePurchaseOrderFromAward), "procurement.purchase-order.create" },
        { typeof(TenderBidsController), nameof(TenderBidsController.OpenBid), "procurement.tender.administer" },
        { typeof(TenderControlsController), nameof(TenderControlsController.SaveTechnical), "procurement.tender.evaluate" },
        { typeof(TenderControlsController), nameof(TenderControlsController.RecordContract), "procurement.contract.manage" },
        { typeof(TenderDocumentTypesController), nameof(TenderDocumentTypesController.GetActive), "procurement.records.read" },
        { typeof(TenderDocumentTypesController), nameof(TenderDocumentTypesController.Create), "procurement.tender.administer" },
        { typeof(TenderEvaluationsController), nameof(TenderEvaluationsController.GetConsolidatedEvaluation), "procurement.records.read" },
        { typeof(TenderEvaluationsController), nameof(TenderEvaluationsController.CalculateQCBSScores), "procurement.tender.evaluate" },
        { typeof(TenderTemplatesController), nameof(TenderTemplatesController.GetActiveTemplates), "procurement.records.read" },
        { typeof(TenderTemplatesController), nameof(TenderTemplatesController.CreateTenderFromTemplate), "procurement.tender.administer" },
        { typeof(TendersController), nameof(TendersController.GetTenderEvaluators), "procurement.records.read" },
        { typeof(TendersController), nameof(TendersController.AssignEvaluators), "procurement.tender.administer" }
    };

    private static IEnumerable<AuthorizeAttribute> AuthorizeAttributes(Type controller) =>
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.DeclaringType == controller)
                .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)));

    private static IEnumerable<string> SplitRoles(string? roles) =>
        string.IsNullOrWhiteSpace(roles)
            ? Array.Empty<string>()
            : roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
