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

public sealed class ProcurementEvaluationCommitteesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationWithoutPreemptingSeededTdcCapabilities()
    {
        var type = typeof(ProcurementEvaluationCommitteesController);
        var controllerAuthorization = type.GetCustomAttribute<AuthorizeAttribute>();
        var bindAuthorization = type.GetMethod(nameof(ProcurementEvaluationCommitteesController.Bind))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var decideAuthorization = type.GetMethod(nameof(ProcurementEvaluationCommitteesController.DecideScoreRecall))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var responseAuthorization = type.GetMethod(
            nameof(ProcurementEvaluationCommitteesController.RespondToAppointment))!
            .GetCustomAttribute<AuthorizeAttribute>();

        controllerAuthorization.Should().NotBeNull(
            "every committee endpoint must require an authenticated principal");
        controllerAuthorization!.Roles.Should().BeNullOrWhiteSpace(
            "seeded TDC_EVALUATOR and TDC procurement roles must reach the authoritative service capability checks");
        bindAuthorization.Should().NotBeNull();
        bindAuthorization!.Roles.Should().BeNullOrWhiteSpace(
            "committee administration is enforced by the procurement.tender.administer capability");
        decideAuthorization.Should().NotBeNull();
        decideAuthorization!.Roles.Should().BeNullOrWhiteSpace(
            "recall decisions are enforced by the approval capability, workflow outcome, and SOD service checks");
        responseAuthorization.Should().BeNull();
        type.GetMethod("LockScoreSheet").Should().BeNull(
            "caller-supplied score snapshots must never bypass authoritative evaluation services");
    }

    [Theory]
    [InlineData("TDC_EVALUATOR",
        nameof(ProcurementEvaluationCommitteesController.RespondToAppointment))]
    [InlineData("TDC_EVALUATOR",
        nameof(ProcurementEvaluationCommitteesController.SubmitConflictDeclaration))]
    [InlineData("TDC_PROCUREMENT_OFFICER",
        nameof(ProcurementEvaluationCommitteesController.Bind))]
    [InlineData("TDC_HEAD_OF_PROCUREMENT",
        nameof(ProcurementEvaluationCommitteesController.DecideScoreRecall))]
    public void SeededTdcRolesReachAuthoritativeServiceChecks(
        string seededRole,
        string actionName)
    {
        var type = typeof(ProcurementEvaluationCommitteesController);
        var action = type.GetMethod(actionName);
        var authorization = type.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action!.GetCustomAttributes<AuthorizeAttribute>())
            .ToList();

        action.Should().NotBeNull();
        authorization.Should().NotBeEmpty(
            "authentication must remain mandatory for {0}", actionName);
        authorization.Should().OnlyContain(attribute =>
                string.IsNullOrWhiteSpace(attribute.Roles),
            "{0} must reach service capability, appointment-ownership, workflow, and SOD checks instead of being rejected by generic controller roles",
            seededRole);
    }

    [Fact]
    public async Task ReadinessOptionsAndScorerEligibilityForwardTenantScopedSourceAndCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        fixture.Service.Setup(service => service.GetReadinessAsync(
                ProcurementEvaluationSourceType.Tender, sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeReadinessDto
            {
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = sourceId
            });
        fixture.Service.Setup(service => service.GetOptionsAsync(
                ProcurementEvaluationSourceType.Tender, sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeOptionsDto());
        fixture.Service.Setup(service => service.EnsureScorerEligibleAsync(
                ProcurementEvaluationSourceType.Tender,
                sourceId,
                ProcurementEvaluationPhase.Technical,
                "corr-0208",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationScorerEligibilityDto
            {
                Allowed = true,
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = sourceId,
                Phase = ProcurementEvaluationPhase.Technical
            });

        (await fixture.Controller.GetReadiness(
            ProcurementEvaluationSourceType.Tender, sourceId, default))
            .Should().BeOfType<OkObjectResult>();
        (await fixture.Controller.GetOptions(
            ProcurementEvaluationSourceType.Tender, sourceId, default))
            .Should().BeOfType<OkObjectResult>();
        var eligibility = await fixture.Controller.GetScorerEligibility(
            ProcurementEvaluationSourceType.Tender,
            sourceId,
            ProcurementEvaluationPhase.Technical,
            default);

        eligibility.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<ProcurementEvaluationScorerEligibilityDto>()
            .Which.Allowed.Should().BeTrue();
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task BindAndRecallDecisionForwardImmutableContractsAndControllerCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var committeeId = Guid.NewGuid();
        var recallId = Guid.NewGuid();
        var bind = new BindProcurementEvaluationCommitteeRequest
        {
            SourceType = ProcurementEvaluationSourceType.RequestForQuotation,
            SourceId = sourceId,
            CommitteeTemplateId = Guid.NewGuid(),
            Purpose = "Evaluate the RFQ.",
            IdempotencyKey = "bind-1"
        };
        var decision = new DecideProcurementEvaluationScoreRecallRequest
        {
            Approve = true,
            RowVersion = "AQ==",
            DecisionReference = "approval://recall",
            EvidenceReference = "evidence://recall",
            IdempotencyKey = "recall-decision-1"
        };
        fixture.Service.Setup(service => service.BindAsync(
                bind, "corr-0208", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeDto
            {
                Id = committeeId,
                SourceType = bind.SourceType,
                SourceId = sourceId
            });
        fixture.Service.Setup(service => service.DecideScoreRecallAsync(
                recallId, decision, "corr-0208", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationScoreRecallDto
            {
                Id = recallId,
                Status = ProcurementEvaluationScoreRecallStatus.Approved,
                AuthorizedNewAttempt = 2
            });

        var bound = await fixture.Controller.Bind(bind, default);
        var decided = await fixture.Controller.DecideScoreRecall(recallId, decision, default);

        bound.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeAssignableTo<ProcurementEvaluationCommitteeDto>()
            .Which.Id.Should().Be(committeeId);
        decided.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<ProcurementEvaluationScoreRecallDto>()
            .Which.AuthorizedNewAttempt.Should().Be(2);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task RetireDraftForwardsGovernedRequestAndCorrelation()
    {
        var fixture = new Fixture();
        var committeeId = Guid.NewGuid();
        var request = new RetireProcurementEvaluationCommitteeDraftRequest
        {
            RowVersion = "AQ==",
            Reason = "The selected committee composition is incorrect.",
            IdempotencyKey = "retire-draft-1"
        };
        fixture.Service.Setup(service => service.RetireDraftAsync(
                committeeId, request, "corr-0208", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeDto
            {
                Id = committeeId,
                Status = ProcurementEvaluationCommitteeControlStatus.Retired
            });

        var result = await fixture.Controller.RetireDraft(
            committeeId, request, default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<ProcurementEvaluationCommitteeDto>()
            .Which.Status.Should().Be(ProcurementEvaluationCommitteeControlStatus.Retired);
        fixture.Service.VerifyAll();
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    [InlineData("invalid", 422)]
    [InlineData("forbidden", 403)]
    public async Task GetMapsDomainFailuresToStructuredProblems(
        string failure,
        int expectedStatus)
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service => service.GetAsync(
                It.IsAny<ProcurementEvaluationSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "missing" => new ProcurementEvaluationCommitteeNotFoundException(
                    "EVALUATION_COMMITTEE_NOT_FOUND", "Missing."),
                "conflict" => new ProcurementEvaluationCommitteeConflictException(
                    "EVALUATION_COMMITTEE_STATE", "Conflict."),
                "invalid" => new ProcurementEvaluationCommitteeValidationException(
                    "EVALUATION_COMMITTEE_INVALID", "Invalid."),
                _ => new ProcurementEvaluationCommitteeAuthorizationException("Forbidden.")
            });

        var result = await fixture.Controller.Get(
            ProcurementEvaluationSourceType.Tender, Guid.NewGuid(), default);

        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions.Should().ContainKey("code");
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new ProcurementEvaluationCommitteesController(Service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "trace-0208"
                    }
                }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0208";
        }

        public Mock<IProcurementEvaluationCommitteeControlService> Service { get; } = new();
        public ProcurementEvaluationCommitteesController Controller { get; }
    }
}
