using System.Net;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Projects;

public class ProjectsControllerSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProjectsControllerSmokeTests(WebApplicationFactory<Program> factory)
    {
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.LookupProjectsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>()))
            .ReturnsAsync(new[]
            {
                new ProjectLookupDto
                {
                    Id = Guid.NewGuid(),
                    ProjectCode = "PRJ-2026-0100",
                    Title = "Smoke Test Project",
                    Status = "Planned"
                }
            });

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IAuthorizationHandler>();
                services.RemoveAll<IProjectService>();
                services.RemoveAll<ICurrentUserProvider>();
                services.AddSingleton<IPolicyEvaluator, TestPolicyEvaluator>();
                services.AddSingleton<IAuthorizationHandler, AllowAnonymousHandler>();
                services.AddSingleton(projectService.Object);
                services.AddSingleton<ICurrentUserProvider>(new FakeCurrentUserProvider(Guid.NewGuid(), Guid.NewGuid()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task LookupProjects_Endpoint_ShouldNotReturnNotFound()
    {
        var response = await _client.GetAsync("/api/projects/lookup?take=5");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}

internal sealed class TestPolicyEvaluator : IPolicyEvaluator
{
    public Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("Test"));
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test")));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, Microsoft.AspNetCore.Authentication.AuthenticateResult authenticationResult, HttpContext context, object? resource)
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
