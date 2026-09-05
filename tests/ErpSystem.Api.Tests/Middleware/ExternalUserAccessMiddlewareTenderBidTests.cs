using System.Security.Claims;
using ErpSystem.Api.Middleware;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ErpSystem.Api.Tests.Middleware;

public sealed class ExternalUserAccessMiddlewareTenderBidTests
{
    [Theory]
    [InlineData("/api/external-portal/tender-bids/51dda53f-d12f-4c1f-88c9-06070123801a/quantity-survey-boq/context")]
    [InlineData("/api/external-portal/tender-bids/51dda53f-d12f-4c1f-88c9-06070123801a/quantity-survey-boq/submissions/latest")]
    public async Task External_supplier_can_reach_bid_scoped_tender_boq_reads(string path)
    {
        var middleware = new ExternalUserAccessMiddleware();
        var context = CreateExternalContext(path);
        var nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/api/external-portal/admin")]
    [InlineData("/api/external-portal/tender-bids-internal")]
    [InlineData("/api/quantity-survey/tender-bids/51dda53f-d12f-4c1f-88c9-06070123801a/boq-submissions")]
    public async Task External_supplier_remains_blocked_from_non_supplier_external_and_internal_routes(string path)
    {
        var middleware = new ExternalUserAccessMiddleware();
        var context = CreateExternalContext(path);
        var nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static DefaultHttpContext CreateExternalContext(string path)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, Constants.Roles.ExternalUser)],
            authenticationType: "Test");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
