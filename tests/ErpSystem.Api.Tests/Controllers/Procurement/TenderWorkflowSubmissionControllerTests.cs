using ErpSystem.Api.Controllers.Procurement;
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

public sealed class TenderWorkflowSubmissionControllerTests
{
    [Fact]
    public async Task MissingPublishedWorkflowReturnsGovernedUnprocessableEntity()
    {
        var tenderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new Mock<ITenderService>();
        service.Setup(item => item.SubmitTenderForApprovalAsync(tenderId, userId))
            .ThrowsAsync(new ProcurementTenderWorkflowValidationException(
                "TENDER_WORKFLOW_NOT_CONFIGURED",
                "A published Tender approval workflow with an independent approver must be configured before submission."));
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(provider => provider.UserId).Returns(userId);
        var controller = new TendersController(
            service.Object,
            Mock.Of<IWorkflowService>(),
            currentUser.Object,
            Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(),
            Mock.Of<ILogger<TendersController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "corr-tender-workflow-required"
                }
            }
        };

        var result = await controller.SubmitTender(tenderId);

        var response = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Title.Should().Be("TENDER_WORKFLOW_NOT_CONFIGURED");
        problem.Extensions["code"].Should().Be("TENDER_WORKFLOW_NOT_CONFIGURED");
        problem.Extensions["correlationId"].Should().Be("corr-tender-workflow-required");
    }
}
