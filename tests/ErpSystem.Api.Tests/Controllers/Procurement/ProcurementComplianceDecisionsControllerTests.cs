using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementComplianceDecisionsControllerTests
{
    [Fact]
    public void ControllerRequiresProcurementRecordReadPermission()
    {
        var authorize = typeof(ProcurementComplianceDecisionsController).GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be("procurement.records.read");
        typeof(ProcurementComplianceDecisionsController).GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }

    [Theory]
    [InlineData((int)PolicyAuthorizationMode.Challenge, HttpStatusCode.Unauthorized)]
    [InlineData((int)PolicyAuthorizationMode.Forbid, HttpStatusCode.Forbidden)]
    public async Task UnauthorizedCallersCannotReachTheEvaluator(int modeValue, HttpStatusCode expected)
    {
        var mode = (PolicyAuthorizationMode)modeValue;
        using var factory = CreateFactory(mode);
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/procurement/compliance-decisions/policy-options")).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task AuthorizedAdministratorCanReadEffectiveTenantPolicyOptions()
    {
        var service = new Mock<IProcurementComplianceDecisionService>();
        service.Setup(item => item.GetEffectivePolicyOptionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProcurementCompliancePolicyOptionDto
                {
                    PolicySetId = Guid.NewGuid(), PolicyCode = "TDC-POLICY", PolicyName = "TDC Policy",
                    Version = 2, CurrencyCode = "GHS", IsDefault = true
                }
            });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/procurement/compliance-decisions/policy-options?atUtc=2026-07-20T12:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetArrayLength().Should().Be(1);
        payload.RootElement[0].GetProperty("policyCode").GetString().Should().Be("TDC-POLICY");
    }

    [Fact]
    public async Task AuthorizedEvaluationReturnsExplainableReadOnlyDecisionAndCorrelationId()
    {
        var service = new Mock<IProcurementComplianceDecisionService>();
        service.Setup(item => item.EvaluateAsync(It.IsAny<ProcurementComplianceDecisionRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementComplianceDecisionRequest _, string correlationId, CancellationToken _) =>
                new ProcurementComplianceDecisionDto
                {
                    EvaluationId = Guid.NewGuid(), CorrelationId = correlationId,
                    Outcome = ProcurementComplianceOutcome.ReviewRequired,
                    Policy = new ProcurementCompliancePolicySelectionDto
                    {
                        PolicySetId = Guid.NewGuid(), PolicyCode = "TDC-POLICY", PolicyName = "TDC Policy", Version = 1
                    },
                    MatchedRules = new[]
                    {
                        new ProcurementComplianceRuleReferenceDto
                        {
                            RuleId = Guid.NewGuid(), RuleCode = "THRESHOLD-RFQ", RuleName = "RFQ range",
                            RuleKind = ProcurementPolicyRuleKind.Threshold, SourceDecisionKey = "DEC-001"
                        }
                    }
                });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();
        using var content = new StringContent(JsonSerializer.Serialize(new
        {
            category = "Goods", amount = 2500, currencyCode = "GHS", sourceType = "PurchaseRequisition",
            sourceReference = "PR-SIM-001", actorRoles = Array.Empty<string>(), sourceOwnerRoles = Array.Empty<string>(),
            entityType = "PurchaseRequisition", action = "Submit", justificationProvided = false,
            evidenceReferenceKeys = Array.Empty<string>()
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/procurement/compliance-decisions/evaluate", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("evaluationOnly").GetBoolean().Should().BeTrue();
        payload.RootElement.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("matchedRules")[0].GetProperty("sourceDecisionKey").GetString().Should().Be("DEC-001");
    }

    [Theory]
    [InlineData("validation", 422)]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    public async Task DomainFailuresMapToStructuredTenantSafeResponses(string failure, int status)
    {
        var service = new Mock<IProcurementComplianceDecisionService>();
        service.Setup(item => item.EvaluateAsync(It.IsAny<ProcurementComplianceDecisionRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "validation" => new ProcurementComplianceRequestValidationException("AMOUNT_INVALID", "Amount cannot be negative."),
                "missing" => new ProcurementCompliancePolicyNotFoundException("No effective policy exists."),
                _ => new ProcurementCompliancePolicyConflictException("Multiple default policies exist.")
            });
        var controller = new ProcurementComplianceDecisionsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-domain" }
            }
        };

        var result = await controller.Evaluate(new ProcurementComplianceDecisionRequest(), default);

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(status);
        objectResult.Value.Should().BeAssignableTo<ProblemDetails>();
        ((ProblemDetails)objectResult.Value!).Extensions["correlationId"].Should().Be("trace-domain");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementComplianceDecisionService>? service = null)
    {
        service ??= new Mock<IProcurementComplianceDecisionService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementComplianceDecisionService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
