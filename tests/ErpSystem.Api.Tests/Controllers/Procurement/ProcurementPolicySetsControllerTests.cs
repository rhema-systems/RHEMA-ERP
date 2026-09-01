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

public sealed class ProcurementPolicySetsControllerTests
{
    [Fact]
    public void ControllerUsesReadAuditAndAdministrationPermissions()
    {
        var authorize = typeof(ProcurementPolicySetsController).GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull();
        authorize!.Roles.Should().BeNull();
        typeof(ProcurementPolicySetsController).GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        typeof(ProcurementPolicySetsController).GetMethod(nameof(ProcurementPolicySetsController.GetPolicySets))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.records.read");
        typeof(ProcurementPolicySetsController).GetMethod(nameof(ProcurementPolicySetsController.GetHistory))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.audit.read");
        typeof(ProcurementPolicySetsController).GetMethod(nameof(ProcurementPolicySetsController.Update))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
    }

    [Fact]
    public async Task AnonymousCallerReceivesUnauthorized()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/procurement/policy-sets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CallerWithoutAdministrativeRoleReceivesForbidden()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Forbid);
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/procurement/policy-sets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthorizedAdministratorCanReadTenantPolicyList()
    {
        var service = new Mock<IProcurementPolicyService>();
        service.Setup(item => item.GetPolicySetsAsync(It.IsAny<ProcurementPolicySetListRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementConfigurationPagedResult<ProcurementPolicySetSummaryDto>
            {
                Items = new[]
                {
                    new ProcurementPolicySetSummaryDto
                    {
                        Id = Guid.NewGuid(), Code = "TDC-EXECUTABLE", Name = "TDC Executable Policy", Version = 1,
                        RuleCount = 12, RuleFamilyCount = 7, IsComplete = true
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/procurement/policy-sets?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        payload.RootElement.GetProperty("items")[0].GetProperty("code").GetString().Should().Be("TDC-EXECUTABLE");
    }

    [Fact]
    public async Task ServicePublishBypassMapsToForbiddenWithoutLeakingDetails()
    {
        var service = new Mock<IProcurementPolicyService>();
        var policyId = Guid.NewGuid();
        service.Setup(item => item.PublishPolicySetAsync(policyId, It.IsAny<ProcurementPolicyLifecycleRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPolicyAuthorizationException("Only SuperAdmin may publish."));
        var controller = new ProcurementPolicySetsController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Publish(policyId,
            new ProcurementPolicyLifecycleRequest { RowVersion = Convert.ToBase64String(new byte[] { 1 }) }, default);
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task StructuredValidationFailureMapsToUnprocessableEntity()
    {
        var service = new Mock<IProcurementPolicyService>();
        var policyId = Guid.NewGuid();
        service.Setup(item => item.PublishPolicySetAsync(policyId, It.IsAny<ProcurementPolicyLifecycleRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPolicyValidationException("Policy is incomplete.", new ProcurementPolicyValidationResultDto
            {
                Errors = new[] { new ProcurementPolicyValidationIssueDto { Code = "RULE_FAMILY_MISSING", Message = "SOD rule required." } }
            }));
        var controller = new ProcurementPolicySetsController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Publish(policyId,
            new ProcurementPolicyLifecycleRequest { RowVersion = Convert.ToBase64String(new byte[] { 1 }) }, default);
        var objectResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementPolicyService>? service = null)
    {
        service ??= new Mock<IProcurementPolicyService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementPolicyService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}

internal enum PolicyAuthorizationMode { Challenge, Forbid, Success }

internal sealed class PolicyTestEvaluator : IPolicyEvaluator
{
    private readonly PolicyAuthorizationMode _mode;
    public PolicyTestEvaluator(PolicyAuthorizationMode mode) => _mode = mode;

    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        if (_mode == PolicyAuthorizationMode.Challenge) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "policy.test"),
            new Claim(ClaimTypes.Role, "TenantAdmin")
        }, "PolicyTest");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "PolicyTest")));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(
        AuthorizationPolicy policy,
        AuthenticateResult authenticationResult,
        HttpContext context,
        object? resource) => Task.FromResult(_mode switch
        {
            PolicyAuthorizationMode.Challenge => PolicyAuthorizationResult.Challenge(),
            PolicyAuthorizationMode.Forbid => PolicyAuthorizationResult.Forbid(),
            _ => PolicyAuthorizationResult.Success()
        });
}
