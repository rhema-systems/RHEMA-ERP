using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementAwardReadinessControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndHasNoPublicDecisionMutation()
    {
        var type = typeof(ProcurementAwardReadinessController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().BeEquivalentTo(
                nameof(ProcurementAwardReadinessController.GetLatest),
                nameof(ProcurementAwardReadinessController.GetHistory),
                nameof(ProcurementAwardReadinessController.GetEvaluatorAwardApproverSodStatus),
                nameof(ProcurementAwardReadinessController.Evaluate));
        typeof(EvaluateProcurementAwardReadinessRequest)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name =>
                name.Contains("Approve", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("IsReady", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateForwardsTenantScopedSourceAssertionsAndCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var request = new EvaluateProcurementAwardReadinessRequest
        {
            IdempotencyKey = "readiness-api-1",
            ExpectedRecommendedSubjectIds = [Guid.NewGuid()],
            ExpectedBusinessPartnerIds = [Guid.NewGuid()],
            ExpectedSourceIntegrityHash = new string('a', 64)
        };
        var expected = new ProcurementAwardReadinessDto
        {
            Id = Guid.NewGuid(),
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = sourceId,
            Status = ProcurementAwardReadinessDecisionStatus.Ready
        };
        fixture.Service.Setup(service => service.EvaluateAsync(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                request,
                "corr-0209",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Controller.Evaluate(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            request,
            default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task LatestAndHistoryForwardSourceAndBoundedHistoryTake()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var latest = new ProcurementAwardReadinessDto
        {
            Id = Guid.NewGuid(),
            SourceType = ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            SourceId = sourceId
        };
        fixture.Service.Setup(service => service.GetLatestAsync(
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(latest);
        fixture.Service.Setup(service => service.GetHistoryAsync(
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                sourceId,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([latest]);

        (await fixture.Controller.GetLatest(
            ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            sourceId,
            default)).Should().BeOfType<OkObjectResult>();
        (await fixture.Controller.GetHistory(
            ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            sourceId,
            25,
            default)).Should().BeOfType<OkObjectResult>();
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task SodStatusReturnsEvaluatorConflictAsVisibleAllowedFalseStatus()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var expected = new ProcurementEvaluatorAwardApproverSodStatusDto
        {
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = sourceId,
            SourceReference = "NCT-2026-001",
            Allowed = false,
            Code = "AWARD_READINESS_EVALUATOR_APPROVER_SOD_VIOLATION",
            Message = "An evaluator cannot approve the same award.",
            CurrentActorUserId = Guid.NewGuid(),
            CurrentActorRoles = ["TDC_EVALUATOR", "TDC_HEAD_OF_PROCUREMENT"],
            CorrelationId = "corr-0209"
        };
        fixture.Service.Setup(service =>
                service.GetEvaluatorAwardApproverSodStatusAsync(
                    ProcurementAwardReadinessSourceType.Tender,
                    sourceId,
                    "corr-0209",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Controller
            .GetEvaluatorAwardApproverSodStatus(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                default);

        var status = result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<ProcurementEvaluatorAwardApproverSodStatusDto>()
            .Subject;
        status.Should().BeSameAs(expected);
        status.Allowed.Should().BeFalse();
        fixture.Service.VerifyAll();
    }

    [Theory]
    [InlineData("cross-tenant", 404, "AWARD_READINESS_SOURCE_NOT_FOUND")]
    [InlineData("ambiguous-lineage", 422, "AWARD_READINESS_EVALUATOR_LINEAGE_AMBIGUOUS")]
    public async Task SodStatusMapsTenantAndLineageFailuresToStructuredProblems(
        string failure,
        int expectedStatus,
        string expectedCode)
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service =>
                service.GetEvaluatorAwardApproverSodStatusAsync(
                    It.IsAny<ProcurementAwardReadinessSourceType>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure == "cross-tenant"
                ? new ProcurementAwardReadinessNotFoundException(
                    expectedCode,
                    "The source was not found in the current tenant.")
                : new ProcurementAwardReadinessValidationException(
                    expectedCode,
                    "Evaluator lineage cannot be resolved unambiguously."));

        var result = await fixture.Controller
            .GetEvaluatorAwardApproverSodStatus(
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                Guid.NewGuid(),
                default);

        var problemResult = result.Should()
            .BeAssignableTo<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(expectedStatus);
        var problem = problemResult.Value.Should()
            .BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(expectedCode);
    }

    [Fact]
    public async Task LatestReturnsStructuredNotFoundWhenNoDecisionExists()
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service => service.GetLatestAsync(
                It.IsAny<ProcurementAwardReadinessSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAwardReadinessDto?)null);

        var result = await fixture.Controller.GetLatest(
            ProcurementAwardReadinessSourceType.RequestForQuotation,
            Guid.NewGuid(),
            default);

        var problem = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        problem.Value.Should().BeOfType<ProblemDetails>()
            .Which.Extensions["code"].Should()
            .Be("AWARD_READINESS_DECISION_NOT_FOUND");
    }

    [Fact]
    public async Task InvalidHistoryTakeIsRejectedBeforeServiceAccess()
    {
        var fixture = new Fixture();

        var result = await fixture.Controller.GetHistory(
            ProcurementAwardReadinessSourceType.Tender,
            Guid.NewGuid(),
            201,
            default);

        var problem = result.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("AWARD_READINESS_HISTORY_TAKE_INVALID");
        fixture.Service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    [InlineData("invalid", 422)]
    [InlineData("forbidden", 403)]
    [InlineData("blocked", 422)]
    public async Task EvaluateMapsDomainFailuresToStructuredProblems(
        string failure,
        int expectedStatus)
    {
        var fixture = new Fixture();
        var decision = new ProcurementAwardReadinessDto
        {
            Id = Guid.NewGuid(),
            Status = ProcurementAwardReadinessDecisionStatus.Blocked
        };
        fixture.Service.Setup(service => service.EvaluateAsync(
                It.IsAny<ProcurementAwardReadinessSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<EvaluateProcurementAwardReadinessRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "missing" => new ProcurementAwardReadinessNotFoundException(
                    "AWARD_READINESS_SOURCE_NOT_FOUND", "Missing."),
                "conflict" => new ProcurementAwardReadinessConflictException(
                    "AWARD_READINESS_STALE", "Stale."),
                "invalid" => new ProcurementAwardReadinessValidationException(
                    "AWARD_READINESS_INVALID", "Invalid."),
                "forbidden" => new ProcurementAwardReadinessAuthorizationException(
                    "Forbidden."),
                _ => new ProcurementAwardReadinessBlockedException(
                    "AWARD_READINESS_BLOCKED", "Blocked.", decision)
            });

        var result = await fixture.Controller.Evaluate(
            ProcurementAwardReadinessSourceType.Tender,
            Guid.NewGuid(),
            new EvaluateProcurementAwardReadinessRequest
            {
                IdempotencyKey = "readiness-api-failure"
            },
            default);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions.Should().ContainKey("code");
        if (failure == "blocked")
            ((ProblemDetails)problem.Value!).Extensions["decision"]
                .Should().BeSameAs(decision);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new ProcurementAwardReadinessController(Service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "trace-0209"
                    }
                }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0209";
        }

        public Mock<IProcurementAwardReadinessService> Service { get; } = new();
        public ProcurementAwardReadinessController Controller { get; }
    }
}
