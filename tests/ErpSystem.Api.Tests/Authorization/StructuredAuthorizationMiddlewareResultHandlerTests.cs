using System.Text.Json;
using ErpSystem.Api.Authorization;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ErpSystem.Api.Tests.Authorization;

public class StructuredAuthorizationMiddlewareResultHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnProblemDetailsWithMissingPermission_ForForbiddenApiRequest()
    {
        var requirement = new PermissionRequirement(FinancePermissions.WorkflowApprove);
        var policy = new AuthorizationPolicy(
            new IAuthorizationRequirement[] { requirement },
            Array.Empty<string>());
        var failure = AuthorizationFailure.Failed(new IAuthorizationRequirement[] { requirement });
        var authorizationResult = PolicyAuthorizationResult.Forbid(failure);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/finance/approvals/00000000-0000-0000-0000-000000000001/approve";
        context.Response.Body = new MemoryStream();

        var handler = new StructuredAuthorizationMiddlewareResultHandler();
        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizationResult);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("title").GetString().Should().Be("Permission denied");
        document.RootElement.GetProperty("detail").GetString().Should().Contain(FinancePermissions.WorkflowApprove);
        document.RootElement.GetProperty("requiredPermissions")[0].GetString().Should().Be(FinancePermissions.WorkflowApprove);
    }
}
