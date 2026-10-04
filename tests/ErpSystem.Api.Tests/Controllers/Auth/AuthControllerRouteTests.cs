using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Auth;

public class AuthControllerRouteTests
{
    [Fact]
    public async Task GetSessionSettings_ShouldReturnPersistedTenantTimeout()
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService
            .Setup(service => service.GetSecuritySettingsAsync())
            .ReturnsAsync(new ErpSystem.Core.Entities.Security
            {
                SessionTimeoutMinutes = 45,
                JwtTokenLifetimeMinutes = 120
            });

        using var factory = CreateFactory(settingsService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/session-settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<SessionSettingsResponse>();
        payload.Should().NotBeNull();
        payload!.SessionTimeoutMinutes.Should().Be(45);
        payload.JwtTokenLifetimeMinutes.Should().Be(120);
    }

    [Fact]
    public void GetPublicLoginConfiguration_ShouldRemainAnonymousAtStableRoute()
    {
        var method = typeof(AuthController).GetMethod(
            nameof(AuthController.GetPublicLoginConfiguration),
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<HttpGetAttribute>()?.Template
            .Should().Be("/api/public/config/login");
    }

    private static WebApplicationFactory<Program> CreateFactory(Mock<ISettingsService> settingsService)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.example.test");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationHandler>();
                services.RemoveAll<ISettingsService>();

                services.AddSingleton<IPolicyEvaluator>(new TestPolicyEvaluator());
                services.AddSingleton<IAuthorizationHandler, AllowAnonymousHandler>();
                services.AddSingleton(settingsService.Object);
            });
        });

    private sealed class SessionSettingsResponse
    {
        public int SessionTimeoutMinutes { get; set; }
        public int JwtTokenLifetimeMinutes { get; set; }
    }
}

internal sealed class TestPolicyEvaluator : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "test.user@erp.local"),
                },
                "Test"));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, HttpContext context, object? resource)
        => Task.FromResult(PolicyAuthorizationResult.Success());
}

internal sealed class AllowAnonymousHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.Requirements)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
