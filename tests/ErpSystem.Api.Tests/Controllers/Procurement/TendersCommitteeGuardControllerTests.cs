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
        var controller = new TendersController(
            service.Object,
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
}
