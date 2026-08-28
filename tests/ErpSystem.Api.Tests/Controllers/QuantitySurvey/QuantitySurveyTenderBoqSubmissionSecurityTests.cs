using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyTenderBoqSubmissionSecurityTests
{
    [Fact]
    public void External_tender_boq_controller_requires_authenticated_portal_identity()
    {
        typeof(ExternalTenderBoqSubmissionsController)
            .GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(QuantitySurveyTenderBoqSubmissionsController.History), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyTenderBoqSubmissionsController.Vet), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    public void Internal_routes_require_the_expected_central_qs_permission(
        string action,
        string permission)
    {
        typeof(QuantitySurveyTenderBoqSubmissionsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Theory]
    [InlineData(nameof(ExternalTenderBoqSubmissionsController.Context), "context")]
    [InlineData(nameof(ExternalTenderBoqSubmissionsController.Template), "template")]
    [InlineData(nameof(ExternalTenderBoqSubmissionsController.Preview), "preview")]
    [InlineData(nameof(ExternalTenderBoqSubmissionsController.Commit), "submissions/{submissionId:guid}/commit")]
    [InlineData(nameof(ExternalTenderBoqSubmissionsController.Latest), "submissions/latest")]
    public void External_actions_expose_only_the_controlled_bid_scoped_routes(
        string action,
        string template)
    {
        var method = typeof(ExternalTenderBoqSubmissionsController).GetMethod(action)!;
        var route = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        route.Template.Should().Be(template);
    }
}
