using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderEvaluationsCommitteeGuardControllerTests
{
    [Fact]
    public async Task DraftCreateReturnsCreatedWithoutRequiringControllerCommitteeSetup()
    {
        var fixture = new Fixture();
        var request = new CreateEvaluationDto { TenderBidId = Guid.NewGuid() };
        var created = new TenderEvaluationDto
        {
            Id = Guid.NewGuid(),
            TenderBidId = request.TenderBidId,
            Status = "Draft"
        };
        fixture.Service.Setup(service => service.CreateEvaluationAsync(request))
            .ReturnsAsync(created);

        var result = await fixture.Controller.CreateEvaluation(request);

        var response = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        response.Value.Should().BeSameAs(created);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task DraftUpdateReturnsOkWithoutRequiringControllerCommitteeSetup()
    {
        var fixture = new Fixture();
        var evaluationId = Guid.NewGuid();
        var request = new UpdateEvaluationDto { QualityScore = 80m };
        var updated = new TenderEvaluationDto
        {
            Id = evaluationId,
            Status = "Draft",
            QualityScore = request.QualityScore
        };
        fixture.Service.Setup(service => service.UpdateEvaluationAsync(evaluationId, request))
            .ReturnsAsync(updated);

        var result = await fixture.Controller.UpdateEvaluation(evaluationId, request);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        response.Value.Should().BeSameAs(updated);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task SubmitWithoutActiveCommitteeReturnsStructuredScorerConflict()
    {
        var fixture = new Fixture();
        var evaluationId = Guid.NewGuid();
        var request = new SubmitEvaluationDto { ConfirmSubmission = true };
        fixture.Service.Setup(service => service.SubmitEvaluationAsync(evaluationId, request))
            .ThrowsAsync(new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORER_INELIGIBLE",
                "No active evaluation committee control exists."));

        var result = await fixture.Controller.SubmitEvaluation(evaluationId, request);

        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().Be("No active evaluation committee control exists.");
        problem.Extensions["code"].Should().Be("EVALUATION_SCORER_INELIGIBLE");
        problem.Extensions["correlationId"].Should().Be("corr-legacy-evaluation");
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task SubmittedDeleteReturnsExplicitStructuredConflict()
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service => service.DeleteEvaluationAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORE_SHEET_LOCKED",
                "Submitted score is immutable."));

        var result = await fixture.Controller.DeleteEvaluation(Guid.NewGuid());

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("EVALUATION_SCORE_SHEET_LOCKED");
    }

    [Fact]
    public async Task LegacySubmitForwardsSignedEvidenceContract()
    {
        var fixture = new Fixture();
        var evaluationId = Guid.NewGuid();
        var request = new SubmitEvaluationDto
        {
            ConfirmSubmission = true,
            SignatureReference = "signature://member",
            EvidenceReference = "evidence://score-sheet",
            IdempotencyKey = "legacy-score-submit-1"
        };
        fixture.Service.Setup(service => service.SubmitEvaluationAsync(evaluationId, request))
            .ReturnsAsync(new TenderEvaluationDto
            {
                Id = evaluationId,
                Status = "Submitted"
            });

        var result = await fixture.Controller.SubmitEvaluation(evaluationId, request);

        result.Result.Should().BeOfType<OkObjectResult>();
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task ConsolidatedDecisionMapsRecalledSheetToConflict()
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service => service.CalculateQCBSScoresAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_SCORE_SHEETS_NOT_CURRENT",
                "A recalled score cannot satisfy readiness."));

        var result = await fixture.Controller.CalculateQCBSScores(Guid.NewGuid());

        result.Result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("EVALUATION_SCORE_SHEETS_NOT_CURRENT");
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new TenderEvaluationsController(
                Service.Object,
                Mock.Of<ILogger<TenderEvaluationsController>>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "corr-legacy-evaluation"
                    }
                }
            };
        }

        public Mock<ITenderEvaluationService> Service { get; } = new();
        public TenderEvaluationsController Controller { get; }
    }
}
