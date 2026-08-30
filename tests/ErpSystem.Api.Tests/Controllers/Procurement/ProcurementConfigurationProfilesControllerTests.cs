using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementConfigurationProfilesControllerTests
{
    [Fact]
    public void ControllerUsesReadAuditAndAdministrationPermissions()
    {
        var authorize = typeof(ProcurementConfigurationProfilesController)
            .GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull();
        authorize!.Roles.Should().BeNull();
        typeof(ProcurementConfigurationProfilesController).GetMethod(nameof(ProcurementConfigurationProfilesController.GetProfiles))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.records.read");
        typeof(ProcurementConfigurationProfilesController).GetMethod(nameof(ProcurementConfigurationProfilesController.GetHistory))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.audit.read");
        typeof(ProcurementConfigurationProfilesController).GetMethod(nameof(ProcurementConfigurationProfilesController.Update))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
    }

    [Fact]
    public async Task AnonymousCallerReceivesUnauthorized()
    {
        using var factory = CreateFactory(ConfigurationAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/procurement/configuration-profiles/schemas"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CallerWithoutAdministrativeRoleReceivesForbidden()
    {
        using var factory = CreateFactory(ConfigurationAuthorizationMode.Forbid);
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/procurement/configuration-profiles"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthorizedAdministratorCanReadTenantProfileList()
    {
        var service = new Mock<IProcurementConfigurationService>();
        service.Setup(item => item.GetProfilesAsync(
                It.IsAny<ProcurementConfigurationProfileListRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementConfigurationPagedResult<ProcurementConfigurationProfileSummaryDto>
            {
                Items = new[] { new ProcurementConfigurationProfileSummaryDto { Id = Guid.NewGuid(), ProfileCode = "TDC-PROCUREMENT", Name = "TDC Policy", Version = 1 } },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });
        using var factory = CreateFactory(ConfigurationAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/procurement/configuration-profiles?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        payload.RootElement.GetProperty("items")[0].GetProperty("profileCode").GetString().Should().Be("TDC-PROCUREMENT");
    }

    [Fact]
    public async Task ServiceAuthorizationRejectionMapsToForbiddenWithoutLeakingDetails()
    {
        var service = new Mock<IProcurementConfigurationService>();
        var profileId = Guid.NewGuid();
        service.Setup(item => item.PublishProfileAsync(
                profileId, It.IsAny<ProcurementConfigurationLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementConfigurationAuthorizationException("Only SuperAdmin may publish."));
        var controller = new ProcurementConfigurationProfilesController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Publish(profileId, new ProcurementConfigurationLifecycleRequest { RowVersion = string.Empty }, default);
        result.Should().BeOfType<ForbidResult>();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        ConfigurationAuthorizationMode mode,
        Mock<IProcurementConfigurationService>? service = null)
    {
        service ??= new Mock<IProcurementConfigurationService>();
        service.Setup(item => item.GetDecisionSchemas()).Returns(Array.Empty<ProcurementDecisionSchemaDto>());
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementConfigurationService>();
                services.AddSingleton<IPolicyEvaluator>(new ConfigurationPolicyEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}

internal enum ConfigurationAuthorizationMode { Challenge, Forbid, Success }

internal sealed class ConfigurationPolicyEvaluator : IPolicyEvaluator
{
    private readonly ConfigurationAuthorizationMode _mode;
    public ConfigurationPolicyEvaluator(ConfigurationAuthorizationMode mode) => _mode = mode;

    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        if (_mode == ConfigurationAuthorizationMode.Challenge)
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "procurement.test"), new Claim(ClaimTypes.Role, "TenantAdmin") }, "ConfigurationTest");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "ConfigurationTest")));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(
        AuthorizationPolicy policy,
        AuthenticateResult authenticationResult,
        HttpContext context,
        object? resource) => Task.FromResult(_mode switch
        {
            ConfigurationAuthorizationMode.Challenge => PolicyAuthorizationResult.Challenge(),
            ConfigurationAuthorizationMode.Forbid => PolicyAuthorizationResult.Forbid(),
            _ => PolicyAuthorizationResult.Success()
        });
}
