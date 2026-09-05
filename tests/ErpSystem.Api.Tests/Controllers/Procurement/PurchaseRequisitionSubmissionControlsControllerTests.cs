using System.Net;
using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class PurchaseRequisitionSubmissionControlsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesSubmissionReadinessHistoryAndSubmitActions()
    {
        typeof(PurchaseRequisitionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(PurchaseRequisitionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(PurchaseRequisitionsController))
            .Select(method => method.Name);

        methods.Should().Contain([
            nameof(PurchaseRequisitionsController.GetSubmissionReadiness),
            nameof(PurchaseRequisitionsController.GetSubmissionControlHistory),
            nameof(PurchaseRequisitionsController.SubmitPurchaseRequisition)
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadReadinessOrHistoryOrSubmit()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/submission-readiness"),
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/submission-control-history"),
            await client.PostAsync($"/api/PurchaseRequisitions/{id}/submit", null)
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BlockedSubmissionMapsToStructured422WithoutStartingWorkflow()
    {
        var id = Guid.NewGuid();
        var requisition = new PurchaseRequisition
        {
            Id = id, TenantId = Guid.NewGuid(), RequisitionNumber = "PR-2026-104",
            RequestedById = Guid.NewGuid(), Status = "Draft", TotalAmount = 100m,
            Items =
            [
                new PurchaseRequisitionItem
                {
                    Quantity = 1m,
                    EstimatedUnitPrice = 100m,
                    LineTotal = 100m
                }
            ]
        };
        var repository = new Mock<IPurchaseRequisitionRepository>();
        repository.Setup(item => item.GetRequisitionByIdAsync(id)).ReturnsAsync(requisition);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.UserId).Returns(requisition.RequestedById);
        var workflow = new Mock<IWorkflowIntegrationService>();
        var submission = new Mock<IProcurementRequisitionSubmissionControlService>();
        var readiness = new PurchaseRequisitionSubmissionReadinessDto
        {
            RequisitionId = id, RequisitionNumber = requisition.RequisitionNumber, Status = "Draft",
            IsCompliant = false, CanSubmit = false, DecisionCode = "PR_APP_OR_EXCEPTION_REQUIRED",
            Message = "Acknowledged APP linkage or an approved exception is required.",
            RequiredActions = ["Link an acknowledged APP plan item."]
        };
        submission.Setup(item => item.EnforceAsync(requisition, "trace-pr-submit", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionSubmissionBlockedException(readiness));
        var controller = Controller(repository.Object, currentUser.Object, workflow.Object, submission.Object);

        var result = (ObjectResult)await controller.SubmitPurchaseRequisition(id);

        result.StatusCode.Should().Be(422);
        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_APP_OR_EXCEPTION_REQUIRED");
        problem.Extensions["submissionReadiness"].Should().BeSameAs(readiness);
        workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        repository.Verify(item => item.UpdateRequisitionAsync(It.IsAny<PurchaseRequisition>()), Times.Never);
    }

    [Fact]
    public async Task UnpricedDraftCannotEnterApprovalWorkflow()
    {
        var id = Guid.NewGuid();
        var requisition = new PurchaseRequisition
        {
            Id = id,
            TenantId = Guid.NewGuid(),
            RequisitionNumber = "PR-2026-UNPRICED",
            RequestedById = Guid.NewGuid(),
            Status = "Draft",
            TotalAmount = 0
        };
        var repository = new Mock<IPurchaseRequisitionRepository>();
        repository.Setup(item => item.GetRequisitionByIdAsync(id)).ReturnsAsync(requisition);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.UserId).Returns(requisition.RequestedById);
        var workflow = new Mock<IWorkflowIntegrationService>();
        var submission = new Mock<IProcurementRequisitionSubmissionControlService>();
        var controller = Controller(repository.Object, currentUser.Object, workflow.Object, submission.Object);

        var result = (ObjectResult)await controller.SubmitPurchaseRequisition(id);

        result.StatusCode.Should().Be(422);
        result.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_ESTIMATE_REQUIRED");
        submission.Verify(item => item.EnforceAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DirectSubmittedStatusIsRejectedBeforeRepositoryMutation()
    {
        var repository = new Mock<IPurchaseRequisitionRepository>();
        var controller = Controller(
            repository.Object,
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IProcurementRequisitionSubmissionControlService>());

        var result = (ObjectResult)await controller.UpdatePurchaseRequisitionStatus(
            Guid.NewGuid(), new UpdateStatusDto { Status = "Submitted" });

        result.StatusCode.Should().Be(409);
        result.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_WORKFLOW_STATUS_REQUIRES_ACTION");
        repository.Verify(item => item.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    private static PurchaseRequisitionsController Controller(
        IPurchaseRequisitionRepository repository,
        ICurrentUserProvider currentUser,
        IWorkflowIntegrationService workflow,
        IProcurementRequisitionSubmissionControlService submission) => new(
        repository,
        Mock.Of<IPurchaseRequisitionItemRepository>(),
        Mock.Of<IRfqService>(),
        Mock.Of<ITenantContext>(),
        Mock.Of<IUnitOfWork>(),
        currentUser,
        workflow,
        Mock.Of<IWorkflowStatusAdapterRegistry>(),
        Mock.Of<IWorkflowService>(),
        Mock.Of<IProcurementRequisitionLinkageService>(),
        submission,
        Mock.Of<IProcurementRequisitionBudgetControlService>(),
        Mock.Of<IProcurementRequisitionAuthorityRouteService>(),
        Mock.Of<IProcurementRequisitionSourcingReleaseService>(),
        Mock.Of<IProcurementAccessControlService>(),
        Mock.Of<IAppEventBus>(),
        NullLogger<PurchaseRequisitionsController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-pr-submit" }
        }
    };

    private static WebApplicationFactory<Program> CreateFactory(PolicyAuthorizationMode mode) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
            });
        });
}
