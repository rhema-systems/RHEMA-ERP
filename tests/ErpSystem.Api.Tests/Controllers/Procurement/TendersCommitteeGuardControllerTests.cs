using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TendersCommitteeGuardControllerTests
{
    [Fact]
    public async Task ReleaseOnlyTenderEvaluatorAssignmentReturnsSuccess()
    {
        var service = new Mock<ITenderService>();
        var tenderId = Guid.NewGuid();
        var request = new AssignEvaluatorsDto();
        service.Setup(item => item.AssignEvaluatorsAsync(tenderId, request))
            .Returns(Task.CompletedTask);
        var controller = Controller(service.Object);

        var result = await controller.AssignEvaluators(tenderId, request);

        result.Should().BeOfType<OkObjectResult>();
        service.Verify(item => item.AssignEvaluatorsAsync(tenderId, request), Times.Once);
    }

    [Fact]
    public async Task StandaloneEvaluatorAssignmentAndRemovalReturnControlledConflict()
    {
        var service = new Mock<ITenderService>();
        service.Setup(item => item.AssignEvaluatorsAsync(
                It.IsAny<Guid>(), It.IsAny<AssignEvaluatorsDto>()))
            .ThrowsAsync(new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_COMMITTEE_MEMBERSHIP_REQUIRED",
                "Use the exact controlled committee."));
        service.Setup(item => item.RemoveEvaluatorAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new ProcurementEvaluationCommitteeConflictException(
                "EVALUATION_COMMITTEE_MEMBERSHIP_REQUIRED",
                "Use the exact controlled committee."));
        var controller = Controller(service.Object);

        var assign = await controller.AssignEvaluators(
            Guid.NewGuid(), new AssignEvaluatorsDto());
        var remove = await controller.RemoveEvaluator(
            Guid.NewGuid(), Guid.NewGuid());

        assign.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should()
            .Be("EVALUATION_COMMITTEE_MEMBERSHIP_REQUIRED");
        remove.Should().BeOfType<ConflictObjectResult>();
    }

    private static TendersController Controller(ITenderService service) => new(
            service,
            Mock.Of<IWorkflowService>(),
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(),
            Mock.Of<ILogger<TendersController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "corr-assignment-guard"
                }
            }
        };
}
