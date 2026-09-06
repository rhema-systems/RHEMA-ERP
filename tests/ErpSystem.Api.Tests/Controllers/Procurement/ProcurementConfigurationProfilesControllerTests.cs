using System.Net;
using System.Net.Http.Json;
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

    [Fact]
    public void WithdrawDecisionUsesOnlyTheScopedPostAndExistingAdministrativePermission()
    {
        var method = typeof(ProcurementConfigurationProfilesController)
            .GetMethod(nameof(ProcurementConfigurationProfilesController.WithdrawDecision))!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should()
            .Be("{id:guid}/decisions/{decisionKey}/withdraw");
        method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
        method.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().BeNull();
    }

    [Fact]
    public async Task WithdrawalPostForwardsExactDecisionAndBodyAndReturnsUpdatedProfile()
    {
        var profileId = Guid.NewGuid();
        const string version = "AQIDBA==";
        const string reason = "Architecture baseline alignment; preserve the other thirteen decisions.";
        var service = new Mock<IProcurementConfigurationService>();
        service.Setup(item => item.WithdrawDecisionAsync(profileId, "DEC-011",
                It.Is<WithdrawProcurementConfigurationDecisionRequest>(request =>
                    request.RowVersion == version && request.Reason == reason),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementConfigurationProfileDto
            {
                Id = profileId, ProfileCode = "TDC-ACCEPTANCE", Version = 3
            });
        using var factory = CreateFactory(ConfigurationAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/procurement/configuration-profiles/{profileId}/decisions/DEC-011/withdraw",
            new { rowVersion = version, reason });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("id").GetGuid().Should().Be(profileId);
        payload.RootElement.GetProperty("version").GetInt32().Should().Be(3);
        service.Verify(item => item.WithdrawDecisionAsync(profileId, "DEC-011",
            It.Is<WithdrawProcurementConfigurationDecisionRequest>(request =>
                request.RowVersion == version && request.Reason == reason),
            It.Is<string>(correlation => !string.IsNullOrWhiteSpace(correlation)),
            It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(item => item.RetireProfileAsync(It.IsAny<Guid>(),
            It.IsAny<ProcurementConfigurationLifecycleRequest>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    [InlineData(false, HttpStatusCode.Forbidden)]
    public async Task WithdrawalPostRetainsAuthenticationAndAuthorizationGates(
        bool anonymous, HttpStatusCode expectedStatus)
    {
        var service = new Mock<IProcurementConfigurationService>();
        using var factory = CreateFactory(anonymous
            ? ConfigurationAuthorizationMode.Challenge : ConfigurationAuthorizationMode.Forbid, service);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/procurement/configuration-profiles/{Guid.NewGuid()}/decisions/DEC-011/withdraw",
            new { rowVersion = "AQIDBA==", reason = "Governed withdrawal" });
        response.StatusCode.Should().Be(expectedStatus);
        service.Verify(item => item.WithdrawDecisionAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<WithdrawProcurementConfigurationDecisionRequest>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithdrawalServiceFailuresPreserveProblemDetailsAndExactRequest(bool validationFailure)
    {
        var profileId = Guid.NewGuid();
        const string detail = "The selected DEC-011 decision cannot be withdrawn in its current state.";
        var request = new WithdrawProcurementConfigurationDecisionRequest
        {
            RowVersion = "AQIDBA==", Reason = "Architecture baseline alignment"
        };
        var validation = new ProcurementConfigurationValidationResultDto
        {
            Errors = new[] { new ProcurementConfigurationValidationIssueDto
            {
                DecisionKey = "DEC-011", Code = "WITHDRAWAL_INVALID", Message = detail
            } }
        };
        Exception failure = validationFailure
            ? new ProcurementConfigurationValidationException(detail, validation)
            : new ProcurementConfigurationConflictException(detail);
        var service = new Mock<IProcurementConfigurationService>();
        using var cancellation = new CancellationTokenSource();
        service.Setup(item => item.WithdrawDecisionAsync(profileId, "DEC-011", request,
                "withdrawal-correlation", cancellation.Token)).ThrowsAsync(failure);
        var context = new DefaultHttpContext { TraceIdentifier = "withdrawal-correlation" };
        context.Request.Path = $"/api/procurement/configuration-profiles/{profileId}/decisions/DEC-011/withdraw";
        var controller = new ProcurementConfigurationProfilesController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.WithdrawDecision(profileId, "DEC-011", request, cancellation.Token);
        var response = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = response.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        response.StatusCode.Should().Be(validationFailure ? 422 : 409);
        problem.Status.Should().Be(response.StatusCode);
        problem.Detail.Should().Be(detail);
        problem.Instance.Should().Be(context.Request.Path.Value);
        problem.Extensions["correlationId"].Should().Be("withdrawal-correlation");
        if (validationFailure)
        {
            var errors = problem.Should().BeOfType<ValidationProblemDetails>().Subject;
            errors.Errors["DEC-011"].Should().ContainSingle().Which.Should().Be(detail);
            errors.Extensions["issues"].Should().BeSameAs(validation.Errors);
        }
        service.Verify(item => item.WithdrawDecisionAsync(profileId, "DEC-011", request,
            "withdrawal-correlation", cancellation.Token), Times.Once);
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
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.test/");
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
