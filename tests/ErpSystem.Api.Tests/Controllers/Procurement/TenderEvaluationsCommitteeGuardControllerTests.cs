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
