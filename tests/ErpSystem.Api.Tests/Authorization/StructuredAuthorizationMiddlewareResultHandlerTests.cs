using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Authorization;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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

    [Fact]
    public async Task HandleAsync_ShouldAuditQuantitySurveyPermissionDenialWithoutQueryOrBodyData()
    {
        const string permission = "quantity-survey.transactions.approve";
        var requirement = new PermissionRequirement(permission);
        var policy = new AuthorizationPolicy([requirement], []);
        var authorizationResult = PolicyAuthorizationResult.Forbid(
            AuthorizationFailure.Failed([requirement]));
        var userId = Guid.NewGuid();
        var audit = new Mock<IAuditLogService>(MockBehavior.Strict);
        object? capturedValues = null;
        audit.Setup(service => service.LogUserActionAsync(
                userId,
                "qs.approver",
                "QuantitySurveyAuthorizationDenied",
                "QuantitySurveyApi",
                "qs-denial-correlation",
                null,
                It.IsAny<object>(),
                "127.0.0.1",
                "qs-security-test"))
            .Callback<Guid, string, string, string, string?, object?, object?, string?, string?>(
                (_, _, _, _, _, _, values, _, _) => capturedValues = values)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection()
            .AddSingleton(audit.Object)
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "qs-denial-correlation",
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "qs.approver")
            ], "test"))
        };
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/quantity-survey/payment-certificates/certificate-id/approve";
        context.Request.QueryString = new QueryString("?secret=must-not-be-audited");
        context.Request.Headers.UserAgent = "qs-security-test";
        context.Response.Body = new MemoryStream();

        var handler = new StructuredAuthorizationMiddlewareResultHandler(
            services.GetRequiredService<IServiceScopeFactory>());
        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizationResult);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        audit.VerifyAll();
        capturedValues.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedValues);
        json.Should().Contain(permission)
            .And.Contain(context.Request.Path.Value!)
            .And.NotContain("secret")
            .And.NotContain("must-not-be-audited");
    }
}
