using ErpSystem.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace ErpSystem.Api.Tests.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task BearerAuthenticatedApiResponseIsPrivateAndNeverStored()
    {
        var context = new DefaultHttpContext();
        context.Request.Path =
            "/api/procurement/supplier-applicant-access/portal";
        context.Request.Headers.Authorization = "Bearer applicant-token";

        var middleware = new SecurityHeadersMiddleware(
            _ =>
            {
                context.Response.Headers.CacheControl = "public, max-age=300";
                context.Response.Headers.Vary = "Origin";
                return Task.CompletedTask;
            },
            Options.Create(new SecurityHeadersOptions()),
            NullLogger<SecurityHeadersMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers.CacheControl.ToString()
            .Should().Be("private, no-store");
        context.Response.Headers.Pragma.ToString()
            .Should().Be("no-cache");
        context.Response.Headers.Vary.ToString()
            .Split(',', StringSplitOptions.TrimEntries)
            .Should().BeEquivalentTo("Origin", HeaderNames.Authorization);
    }

    [Fact]
    public async Task AnonymousApiResponseDoesNotReceiveAuthenticatedCacheHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Path =
            "/api/procurement/supplier-applicant-access/public-security";

        var middleware = new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new SecurityHeadersOptions()),
            NullLogger<SecurityHeadersMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.Response.Headers.CacheControl.Should().BeEmpty();
        context.Response.Headers.Pragma.Should().BeEmpty();
        context.Response.Headers.Vary.Should().BeEmpty();
    }
}
