using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class RfqsControllerStatutoryControlsTests
{
    [Fact]
    public async Task ControlsEndpointReturnsTenantScopedLifecycleDto()
    {
        var rfqId = Guid.NewGuid();
        var expected = new ProcurementRfqControlDto
        {
            RfqId = rfqId,
            RfqNumber = "RFQ-2026-001",
            RfqStatus = "Sent",
            MethodRuleCode = "RFQ-GOODS-001",
            MinimumQuotationCount = 3,
            QualifiedInvitationCount = 4,
            QuotesRemainSealed = true
        };
        var fixture = new Fixture();
        fixture.Controls.Setup(service => service.GetAsync(rfqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await fixture.Controller.GetControls(rfqId);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
    }

    [Theory]
    [InlineData("not-found", 404)]
    [InlineData("validation", 422)]
    [InlineData("forbidden", 403)]
    public async Task ControlsEndpointMapsGovernedFailuresToProblemDetails(string failure, int expectedStatus)
    {
        var fixture = new Fixture();
        var rfqId = Guid.NewGuid();
        fixture.Controls.Setup(service => service.GetAsync(rfqId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "not-found" => new ProcurementRfqControlNotFoundException("RFQ_NOT_FOUND", "Missing."),
                "validation" => new ProcurementRfqControlValidationException("RFQ_SOURCE_LINEAGE_STALE", "Stale."),
                _ => new ProcurementRfqControlAuthorizationException("Forbidden.")
            });

        var response = await fixture.Controller.GetControls(rfqId);

        var result = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal("trace-rfq-controls", problem.Extensions["correlationId"]);
    }

    [Fact]
    public async Task MutatingEndpointMapsOptimisticConcurrencyToConflict()
    {
        var fixture = new Fixture();
        var rfqId = Guid.NewGuid();
        fixture.Controls.Setup(service => service.SaveEvaluationAsync(
                rfqId, It.IsAny<SaveProcurementRfqEvaluationRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var response = await fixture.Controller.SaveEvaluation(rfqId, new SaveProcurementRfqEvaluationRequest());

        var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal("RFQ_CONTROL_VERSION_CONFLICT", problem.Extensions["code"]);
    }

    [Fact]
    public void StatutoryRoutesDeclareExplicitRoleBoundaries()
    {
        var controllerType = typeof(RfqsController);
        var controlsRoles = controllerType.GetMethod(nameof(RfqsController.GetControls))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
        var evaluationRoles = controllerType.GetMethod(nameof(RfqsController.SaveEvaluation))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
        var decisionRoles = controllerType.GetMethod(nameof(RfqsController.DecideEvaluation))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;

        Assert.Contains("TDC_INTERNAL_AUDIT", controlsRoles);
        Assert.Contains("TDC_OBSERVER", controlsRoles);
        Assert.Contains("TDC_EVALUATOR", evaluationRoles);
        Assert.DoesNotContain("TDC_EVALUATOR", decisionRoles);
        Assert.Contains("TDC_HEAD_OF_PROCUREMENT", decisionRoles);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new RfqsController(
                Mock.Of<IRfqService>(),
                Controls.Object,
                Mock.Of<IRfqInvitationDocumentService>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<IBusinessPartnerUserRepository>(),
                Mock.Of<ICurrentUserProvider>(),
                Mock.Of<ILogger<RfqsController>>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-rfq-controls" }
                }
            };
        }

        public Mock<IProcurementRfqControlService> Controls { get; } = new();
        public RfqsController Controller { get; }
    }
}
