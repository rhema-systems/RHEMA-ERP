using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Workflow;
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

public sealed class PurchaseRequisitionAuthorityControlsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesReadinessHistorySubmitAndApprovalActions()
    {
        typeof(PurchaseRequisitionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(PurchaseRequisitionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(PurchaseRequisitionsController))
            .Select(method => method.Name);

        methods.Should().Contain([
            nameof(PurchaseRequisitionsController.GetAuthorityReadiness),
            nameof(PurchaseRequisitionsController.GetAuthorityRouteHistory),
            nameof(PurchaseRequisitionsController.SubmitPurchaseRequisition),
            nameof(PurchaseRequisitionsController.ApprovePurchaseRequisition)
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadAuthorityControlsSubmitOrApprove()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/authority-readiness"),
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/authority-route-history"),
            await client.PostAsync($"/api/PurchaseRequisitions/{id}/submit", null),
            await client.PostAsJsonAsync($"/api/PurchaseRequisitions/{id}/approve", new { approved = true })
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadinessAndHistoryMapTenantAuthorizationAndNotFoundFailures()
    {
        var fixture = new ControllerFixture();
        fixture.Authority.Setup(service => service.GetReadinessAsync(
                fixture.Requisition.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionAuthorityAuthorizationException("Forbidden."));
        fixture.Authority.Setup(service => service.GetHistoryAsync(
                fixture.Requisition.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionAuthorityNotFoundException("PR_NOT_FOUND", "Missing."));

        var forbidden = (ObjectResult)(await fixture.Controller.GetAuthorityReadiness(
            fixture.Requisition.Id, default)).Result!;
        var missing = (ObjectResult)(await fixture.Controller.GetAuthorityRouteHistory(
            fixture.Requisition.Id, default)).Result!;

        forbidden.StatusCode.Should().Be(403);
        forbidden.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_AUTHORITY_CONTROL_FORBIDDEN");
        missing.StatusCode.Should().Be(404);
        missing.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_NOT_FOUND");
    }

    [Fact]
    public async Task AuthorityHardStopOccursBeforeBudgetReservationTransactionOrWorkflowStart()
    {
        var fixture = new ControllerFixture();
        var readiness = fixture.AuthorityReadiness(
            isCompliant: false, code: "PR_AUTHORITY_ROUTE_AMBIGUOUS");
        fixture.Authority.Setup(service => service.EnforceSubmissionAsync(
                fixture.Requisition, "trace-pr-authority", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionAuthorityBlockedException(readiness));

        var result = (ObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.StatusCode.Should().Be(422);
        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_AUTHORITY_ROUTE_AMBIGUOUS");
        problem.Extensions["authorityReadiness"].Should().BeSameAs(readiness);
        fixture.Budget.Verify(service => service.ReserveAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(service => service.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Workflow.Verify(service => service.SubmitAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ReadySubmissionCapturesRouteThenStartsExactSelectedWorkflowVersionAtomically()
    {
        var fixture = new ControllerFixture();
        var calls = new List<string>();
        fixture.Authority.Setup(service => service.CaptureAsync(
                fixture.Requisition, fixture.Decision, "trace-pr-authority", It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("capture"))
            .ReturnsAsync(fixture.Route);
        fixture.Workflow.Setup(service => service.SubmitAsync(
                "PurchaseRequisition", fixture.Requisition.Id, fixture.Route.WorkflowDefinitionId))
            .Callback(() => calls.Add("workflow"))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Pending));
        fixture.Authority.Setup(service => service.GetReadinessAsync(
                fixture.Requisition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.AuthorityReadiness(true, "PR_AUTHORITY_ROUTE_CAPTURED"));

        var result = (OkObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.Value.Should().NotBeNull();
        calls.Should().Equal("capture", "workflow");
        fixture.Workflow.Verify(service => service.SubmitAsync(
            "PurchaseRequisition", fixture.Requisition.Id, fixture.Route.WorkflowDefinitionId), Times.Once);
        fixture.Workflow.Verify(service => service.SubmitAsync(
            It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        fixture.Repository.Verify(service => service.UpdateRequisitionAsync(fixture.Requisition), Times.Once);
        fixture.UnitOfWork.Verify(service => service.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(service => service.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Requisition.Status.Should().Be("Pending Approval");
    }

    [Fact]
    public async Task ApprovalSodHardStopPreventsWorkflowAuthorizationAndApprovalMutation()
    {
        var fixture = new ControllerFixture(status: "Pending Approval");
        var readiness = fixture.AuthorityReadiness(
            isCompliant: false, code: "SOD_INITIATOR_APPROVER_CONFLICT");
        fixture.Authority.Setup(service => service.EnforceApprovalAsync(
                fixture.Requisition, "trace-pr-authority", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionAuthorityBlockedException(readiness));

        var result = (ObjectResult)await fixture.Controller.ApprovePurchaseRequisition(
            fixture.Requisition.Id, new ApprovalDto { Approved = true });

        result.StatusCode.Should().Be(403);
        result.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("SOD_INITIATOR_APPROVER_CONFLICT");
        fixture.Workflow.Verify(service => service.CanUserApproveAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        fixture.Workflow.Verify(service => service.ProcessApprovalAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        fixture.Repository.Verify(service => service.UpdateRequisitionAsync(
            It.IsAny<PurchaseRequisition>()), Times.Never);
    }

    [Fact]
    public async Task DraftRequisitionCannotBeApprovedBeforeConfiguredWorkflowSubmission()
    {
        var fixture = new ControllerFixture(status: "Draft");

        var result = (ObjectResult)await fixture.Controller.ApprovePurchaseRequisition(
            fixture.Requisition.Id, new ApprovalDto { Approved = true });

        result.StatusCode.Should().Be(400);
        result.Value.Should().Be("Purchase requisition cannot be approved in current status: Draft");
        fixture.Authority.Verify(service => service.EnforceApprovalAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Workflow.Verify(service => service.ProcessApprovalAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        fixture.Repository.Verify(service => service.UpdateRequisitionAsync(
            It.IsAny<PurchaseRequisition>()), Times.Never);
    }

    private sealed class ControllerFixture
    {
        public ControllerFixture(string status = "Draft")
        {
            UserId = Guid.NewGuid();
            Requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                RequisitionNumber = "PR-AUTHORITY-API-001",
                RequestedById = Guid.NewGuid(),
                Status = status,
                ProcurementCategory = ProcurementCategoryClass.Goods,
                Currency = "GHS",
                TotalAmount = 2500m
            };
            Route = new ProcurementRequisitionAuthorityRoute
            {
                Id = Guid.NewGuid(),
                TenantId = Requisition.TenantId,
                PurchaseRequisitionId = Requisition.Id,
                AttemptNumber = 1,
                RouteReference = "ARR-PR-AUTHORITY-API-001-A1",
                WorkflowDefinitionId = Guid.NewGuid(),
                WorkflowName = "TDC Purchase Requisition Approval",
                WorkflowVersion = 4
            };
            Decision = new ProcurementAuthorityRouteDecisionDto
            {
                IsReady = true,
                DecisionCode = "PR_AUTHORITY_ROUTE_READY",
                Category = ProcurementCategoryClass.Goods,
                Amount = Requisition.TotalAmount,
                CurrencyCode = Requisition.Currency,
                Workflow = new ProcurementAuthorityWorkflowSelectionDto
                {
                    WorkflowDefinitionId = Route.WorkflowDefinitionId,
                    Name = Route.WorkflowName,
                    Version = Route.WorkflowVersion
                },
                Steps = [new ProcurementAuthorityRouteStepDecisionDto { Sequence = 1 }]
            };
            Repository.Setup(service => service.GetRequisitionByIdAsync(Requisition.Id))
                .ReturnsAsync(Requisition);
            Repository.Setup(service => service.UpdateRequisitionAsync(It.IsAny<PurchaseRequisition>()))
                .ReturnsAsync((PurchaseRequisition item) => item);
            CurrentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            CurrentUser.SetupGet(item => item.UserId).Returns(UserId);
            Submission.Setup(service => service.EnforceAsync(
                    Requisition, "trace-pr-authority", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PurchaseRequisitionSubmissionReadinessDto
                {
                    RequisitionId = Requisition.Id,
                    RequisitionNumber = Requisition.RequisitionNumber,
                    Status = status,
                    IsCompliant = true,
                    CanSubmit = true,
                    DecisionCode = "PR_APP_ACKNOWLEDGED",
                    Message = "Acknowledged APP",
                    Basis = "AcknowledgedAPP"
                });
            Authority.Setup(service => service.EnforceSubmissionAsync(
                    Requisition, "trace-pr-authority", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Decision);
            Budget.Setup(service => service.ReserveAsync(
                    Requisition, "trace-pr-authority", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PurchaseRequisitionBudgetReadinessDto
                {
                    RequisitionId = Requisition.Id,
                    RequisitionNumber = Requisition.RequisitionNumber,
                    Status = status,
                    IsCompliant = true,
                    CanReserve = true,
                    DecisionCode = "PR_BUDGET_AVAILABLE",
                    Message = "Budget available.",
                    Basis = "ApprovedBudget",
                    Currency = "GHS",
                    RequestedAmount = Requisition.TotalAmount,
                    AvailableAmount = 10000m
                });
            UnitOfWork.Setup(service => service.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> operation, CancellationToken _) => operation());
            UnitOfWork.Setup(service => service.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(service => service.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(service => service.RollbackAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            StatusAdapters.Setup(service => service.GetAdapter("PurchaseRequisition"))
                .Returns(new PurchaseRequisitionWorkflowStatusAdapter());

            Controller = new PurchaseRequisitionsController(
                Repository.Object,
                Mock.Of<IPurchaseRequisitionItemRepository>(),
                Mock.Of<IRfqService>(),
                Mock.Of<ITenantContext>(),
                UnitOfWork.Object,
                CurrentUser.Object,
                Workflow.Object,
                StatusAdapters.Object,
                Mock.Of<IWorkflowService>(),
                Mock.Of<IProcurementRequisitionLinkageService>(),
                Submission.Object,
                Budget.Object,
                Authority.Object,
                Mock.Of<IProcurementRequisitionSourcingReleaseService>(),
                Mock.Of<IAppEventBus>(),
                NullLogger<PurchaseRequisitionsController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-pr-authority" }
                }
            };
        }

        public Guid UserId { get; }
        public PurchaseRequisition Requisition { get; }
        public ProcurementAuthorityRouteDecisionDto Decision { get; }
        public ProcurementRequisitionAuthorityRoute Route { get; }
        public Mock<IPurchaseRequisitionRepository> Repository { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        public Mock<IWorkflowStatusAdapterRegistry> StatusAdapters { get; } = new();
        public Mock<IProcurementRequisitionSubmissionControlService> Submission { get; } = new();
        public Mock<IProcurementRequisitionBudgetControlService> Budget { get; } = new();
        public Mock<IProcurementRequisitionAuthorityRouteService> Authority { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public PurchaseRequisitionsController Controller { get; }

        public PurchaseRequisitionAuthorityReadinessDto AuthorityReadiness(bool isCompliant, string code) => new()
        {
            RequisitionId = Requisition.Id,
            RequisitionNumber = Requisition.RequisitionNumber,
            Status = Requisition.Status,
            IsCompliant = isCompliant,
            CanSubmit = isCompliant && string.Equals(Requisition.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            DecisionCode = code,
            Message = isCompliant ? "Authority route is ready." : "Authority route is blocked.",
            AuthorityRouteId = isCompliant ? Route.Id : null,
            RouteReference = isCompliant ? Route.RouteReference : null,
            WorkflowDefinitionId = isCompliant ? Route.WorkflowDefinitionId : null,
            WorkflowName = isCompliant ? Route.WorkflowName : null,
            WorkflowVersion = isCompliant ? Route.WorkflowVersion : null
        };
    }

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
