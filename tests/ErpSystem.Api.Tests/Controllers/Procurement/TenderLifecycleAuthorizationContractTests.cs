using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderLifecycleAuthorizationContractTests
{
    [Theory]
    [InlineData(typeof(TendersController), nameof(TendersController.ApproveTender), "procurement.tender.approve")]
    [InlineData(typeof(TendersController), nameof(TendersController.RejectTender), "procurement.tender.approve")]
    [InlineData(typeof(TenderBidsController), nameof(TenderBidsController.VerifyPayment), ProcurementAccessControlRegistry.TenderPaymentVerifyPermission)]
    [InlineData(typeof(TenderControlsController), nameof(TenderControlsController.RecordAward), "procurement.tender.approve")]
    [InlineData(typeof(TenderAwardsController), nameof(TenderAwardsController.CreateAward), "procurement.tender.administer")]
    [InlineData(typeof(TenderAwardsController), nameof(TenderAwardsController.ApproveAward), "procurement.tender.approve")]
    [InlineData(typeof(TenderAwardsController), nameof(TenderAwardsController.RejectAward), "procurement.tender.approve")]
    [InlineData(typeof(TenderNegotiationsController), nameof(TenderNegotiationsController.Create), "procurement.tender.administer")]
    [InlineData(typeof(TenderNegotiationsController), nameof(TenderNegotiationsController.UpdateItem), "procurement.tender.administer")]
    [InlineData(typeof(TenderNegotiationsController), nameof(TenderNegotiationsController.SaveDraft), "procurement.tender.administer")]
    [InlineData(typeof(TenderNegotiationsController), nameof(TenderNegotiationsController.Complete), "procurement.tender.administer")]
    [InlineData(typeof(TenderNegotiationsController), nameof(TenderNegotiationsController.Cancel), "procurement.tender.administer")]
    [InlineData(typeof(ProcurementContractActivationsController), nameof(ProcurementContractActivationsController.GetOverview), "procurement.records.read")]
    [InlineData(typeof(ProcurementContractActivationsController), nameof(ProcurementContractActivationsController.Submit), "procurement.contract.manage")]
    [InlineData(typeof(ProcurementContractActivationsController), nameof(ProcurementContractActivationsController.Decide), "procurement.contract.approve")]
    [InlineData(typeof(ProcurementContractActivationsController), nameof(ProcurementContractActivationsController.Activate), "procurement.contract.approve")]
    [InlineData(typeof(ProcurementContractOperationsController), nameof(ProcurementContractOperationsController.Search), "procurement.records.read")]
    [InlineData(typeof(ProcurementContractOperationsController), nameof(ProcurementContractOperationsController.Get), "procurement.records.read")]
    [InlineData(typeof(ProcurementContractOperationsController), nameof(ProcurementContractOperationsController.ProcessAlerts), "procurement.contract.manage")]
    public void LifecycleEndpointUsesExactPermission(Type controller, string method, string permission)
    {
        controller.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(permission);
    }

    [Fact]
    public void TenderRevisionAndCloseRoutesAreExposedWithExpectedPermissions()
    {
        Policy<TendersController>(nameof(TendersController.CloseTender))
            .Should().Be("procurement.tender.administer");
        Policy<TendersController>(nameof(TendersController.GetRevisions))
            .Should().Be("procurement.records.read");
        Policy<TendersController>(nameof(TendersController.CreateRevision))
            .Should().Be("procurement.tender.administer");
    }

    private static string? Policy<T>(string method) =>
        typeof(T).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy;
}
