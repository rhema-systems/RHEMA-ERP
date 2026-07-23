using System.Net;
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

public sealed class PurchaseRequisitionBudgetControlsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesBudgetReadinessHistoryAndSubmitActions()
    {
        typeof(PurchaseRequisitionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(PurchaseRequisitionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(PurchaseRequisitionsController))
            .Select(method => method.Name);

        methods.Should().Contain([
            nameof(PurchaseRequisitionsController.GetBudgetReadiness),
            nameof(PurchaseRequisitionsController.GetBudgetControlHistory),
            nameof(PurchaseRequisitionsController.SubmitPurchaseRequisition)
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadBudgetReadinessOrHistoryOrSubmit()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/budget-readiness"),
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/budget-control-history"),
            await client.PostAsync($"/api/PurchaseRequisitions/{id}/submit", null)
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BudgetHardStopMapsToStructured422WithoutStartingWorkflowOrChangingPrStatus()
    {
        var fixture = new ControllerFixture();
        var readiness = fixture.BudgetReadiness(canReserve: false, "PR_BUDGET_INSUFFICIENT");
        fixture.Budget.Setup(item => item.ReserveAsync(
                fixture.Requisition, "trace-pr-budget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(readiness);

        var result = (ObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.StatusCode.Should().Be(422);
        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_BUDGET_INSUFFICIENT");
        problem.Extensions["budgetReadiness"].Should().BeSameAs(readiness);
        fixture.Workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        fixture.Workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        fixture.Repository.Verify(item => item.UpdateRequisitionAsync(It.IsAny<PurchaseRequisition>()), Times.Never);
        fixture.Requisition.Status.Should().Be("Draft");
        fixture.UnitOfWork.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistingCommitmentReturnsIdempotentSuccessWithoutStartingAnotherWorkflow()
    {
        var fixture = new ControllerFixture();
        var readiness = fixture.BudgetReadiness(canReserve: true, "PR_BUDGET_COMMITMENT_ACTIVE");
        readiness.Basis = "ExistingCommitment";
        readiness.CommitmentId = Guid.NewGuid();
        readiness.CommitmentReference = "BCR-PR-2026-105";
        readiness.CommitmentStatus = "Reserved";
        fixture.Budget.Setup(item => item.ReserveAsync(
                fixture.Requisition, "trace-pr-budget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(readiness);

        var result = (OkObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.Value.Should().NotBeNull();
        fixture.Workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        fixture.Workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        fixture.Repository.Verify(item => item.UpdateRequisitionAsync(It.IsAny<PurchaseRequisition>()), Times.Never);
        fixture.UnitOfWork.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryAfterSubmittedStatusReturnsStableIdempotentSuccessForActiveCommitment()
    {
        var fixture = new ControllerFixture(status: "Pending Approval");
        var readiness = fixture.BudgetReadiness(canReserve: true, "PR_BUDGET_COMMITMENT_ACTIVE");
        readiness.Basis = "ExistingCommitment";
        readiness.CommitmentId = Guid.NewGuid();
        readiness.CommitmentReference = "BCR-PR-2026-105";
        readiness.CommitmentStatus = "Reserved";
        fixture.Budget.Setup(item => item.GetReadinessAsync(
                fixture.Requisition.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readiness);

        var result = (OkObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.Value.Should().NotBeNull();
        fixture.Submission.Verify(item => item.EnforceAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Budget.Verify(item => item.ReserveAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        fixture.Workflow.Verify(item => item.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        fixture.UnitOfWork.Verify(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancellationSetsReleaseEligibleStatusBeforeBudgetReleaseSave()
    {
        var fixture = new ControllerFixture(status: "Pending Approval");
        fixture.Budget.Setup(item => item.ReleaseAsync(
                It.Is<PurchaseRequisition>(requisition => requisition.Status == "Cancelled"),
                "Purchase requisition cancelled.",
                "procurement.requisition.create",
                "trace-pr-budget",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseRequisitionBudgetReleaseDto
            {
                RequisitionId = fixture.Requisition.Id,
                Released = true,
                ReleasedAmount = 100m,
                AvailableAmount = 500m,
                Message = "Released"
            });

        var result = await fixture.Controller.UpdatePurchaseRequisitionStatus(
            fixture.Requisition.Id,
            new UpdateStatusDto { Status = "Cancelled" });

        result.Should().BeOfType<NoContentResult>();
        fixture.Budget.VerifyAll();
        fixture.Repository.Verify(item => item.UpdateRequisitionAsync(fixture.Requisition), Times.Once);
        fixture.UnitOfWork.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WorkflowFailureRollsBackTheAtomicReservationTransaction()
    {
        var fixture = new ControllerFixture();
        fixture.Budget.Setup(item => item.ReserveAsync(
                fixture.Requisition, "trace-pr-budget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.BudgetReadiness(canReserve: true, "PR_BUDGET_AVAILABLE"));
        fixture.Workflow.Setup(item => item.SubmitAsync(
                "PurchaseRequisition", fixture.Requisition.Id, fixture.AuthorityRoute.WorkflowDefinitionId))
            .ThrowsAsync(new InvalidOperationException("No active workflow definition."));

        var result = (ObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.StatusCode.Should().Be(400);
        fixture.UnitOfWork.Verify(item => item.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Repository.Verify(item => item.UpdateRequisitionAsync(It.IsAny<PurchaseRequisition>()), Times.Never);
    }

    [Fact]
    public async Task RejectedWorkflowReleasesTheCommitmentInsideTheSameStatusTransaction()
    {
        var fixture = new ControllerFixture(status: "Pending Approval");
        fixture.Workflow.Setup(item => item.CanUserApproveAsync(
                "PurchaseRequisition", fixture.Requisition.Id, fixture.UserId))
            .ReturnsAsync(true);
        fixture.Workflow.Setup(item => item.ProcessApprovalAsync(
                "PurchaseRequisition", fixture.Requisition.Id, fixture.UserId, "reject", "Budget no longer approved."))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Cancelled,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Rejected));
        fixture.Budget.Setup(item => item.ReleaseAsync(
                fixture.Requisition,
                "Budget no longer approved.",
                "procurement.requisition.approve",
                "trace-pr-budget",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseRequisitionBudgetReleaseDto
            {
                RequisitionId = fixture.Requisition.Id,
                Released = true,
                ReleasedAmount = 100m,
                AvailableAmount = 500m,
                Message = "Released"
            });

        var result = (OkObjectResult)await fixture.Controller.ApprovePurchaseRequisition(
            fixture.Requisition.Id,
            new ApprovalDto { Approved = false, Comments = "Budget no longer approved." });

        result.Value.Should().NotBeNull();
        fixture.Requisition.Status.Should().Be("Rejected");
        fixture.Budget.VerifyAll();
        fixture.UnitOfWork.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Repository.Verify(item => item.UpdateRequisitionAsync(fixture.Requisition), Times.Once);
    }

    private sealed class ControllerFixture
    {
        public ControllerFixture(string status = "Draft")
        {
            UserId = Guid.NewGuid();
            Requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), RequisitionNumber = "PR-2026-105",
                RequestedById = UserId, Status = status, Currency = "GHS", TotalAmount = 100m
            };
            Repository.Setup(item => item.GetRequisitionByIdAsync(Requisition.Id)).ReturnsAsync(Requisition);
            Repository.Setup(item => item.UpdateRequisitionAsync(It.IsAny<PurchaseRequisition>()))
                .ReturnsAsync((PurchaseRequisition item) => item);
            CurrentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            CurrentUser.SetupGet(item => item.UserId).Returns(UserId);
            AuthorityRoute = new ProcurementRequisitionAuthorityRoute
            {
                Id = Guid.NewGuid(), TenantId = Requisition.TenantId, PurchaseRequisitionId = Requisition.Id,
                AttemptNumber = 1, RouteReference = "ARR-PR-2026-105-A1", WorkflowDefinitionId = Guid.NewGuid()
            };
            Authority.Setup(item => item.EnforceSubmissionAsync(
                    Requisition, "trace-pr-budget", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAuthorityRouteDecisionDto
                {
                    IsReady = true,
                    DecisionCode = "PR_AUTHORITY_ROUTE_READY",
                    Category = ProcurementCategoryClass.Goods,
                    Amount = Requisition.TotalAmount,
                    CurrencyCode = Requisition.Currency,
                    Workflow = new ProcurementAuthorityWorkflowSelectionDto
                    {
                        WorkflowDefinitionId = AuthorityRoute.WorkflowDefinitionId,
                        Name = "TDC Purchase Requisition Approval",
                        Version = 1
                    },
                    Steps = [new ProcurementAuthorityRouteStepDecisionDto { Sequence = 1 }]
                });
            Authority.Setup(item => item.CaptureAsync(
                    Requisition, It.IsAny<ProcurementAuthorityRouteDecisionDto>(), "trace-pr-budget", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AuthorityRoute);
            Authority.Setup(item => item.GetLatestRouteAsync(Requisition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(AuthorityRoute);
            Authority.Setup(item => item.GetReadinessAsync(Requisition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(AuthorityReadiness());
            Authority.Setup(item => item.EnforceApprovalAsync(
                    Requisition, "trace-pr-budget", It.IsAny<CancellationToken>()))
                .ReturnsAsync(AuthorityReadiness());
            Submission.Setup(item => item.EnforceAsync(
                    Requisition, "trace-pr-budget", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PurchaseRequisitionSubmissionReadinessDto
                {
                    RequisitionId = Requisition.Id,
                    RequisitionNumber = Requisition.RequisitionNumber,
                    Status = Requisition.Status,
                    IsCompliant = true,
                    CanSubmit = true,
                    DecisionCode = "PR_APP_ACKNOWLEDGED",
                    Message = "Acknowledged APP",
                    Basis = "AcknowledgedAPP"
                });
            UnitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> operation, CancellationToken _) => operation());
            UnitOfWork.Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(item => item.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(item => item.RollbackAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            StatusAdapters.Setup(item => item.GetAdapter("PurchaseRequisition"))
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
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-pr-budget" }
                }
            };
        }

        public Guid UserId { get; }
        public PurchaseRequisition Requisition { get; }
        public ProcurementRequisitionAuthorityRoute AuthorityRoute { get; }
        public Mock<IPurchaseRequisitionRepository> Repository { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        public Mock<IWorkflowStatusAdapterRegistry> StatusAdapters { get; } = new();
        public Mock<IProcurementRequisitionSubmissionControlService> Submission { get; } = new();
        public Mock<IProcurementRequisitionBudgetControlService> Budget { get; } = new();
        public Mock<IProcurementRequisitionAuthorityRouteService> Authority { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public PurchaseRequisitionsController Controller { get; }

        public PurchaseRequisitionBudgetReadinessDto BudgetReadiness(bool canReserve, string code) => new()
        {
            RequisitionId = Requisition.Id,
            RequisitionNumber = Requisition.RequisitionNumber,
            Status = Requisition.Status,
            IsCompliant = canReserve,
            CanReserve = canReserve,
            DecisionCode = code,
            Message = canReserve ? "Approved budget is available." : "Approved budget is insufficient.",
            Basis = canReserve ? "ApprovedBudget" : null,
            BudgetId = Guid.NewGuid(),
            BudgetCode = "BUD-2026-105",
            Currency = "GHS",
            RequestedAmount = 100m,
            AllocatedAmount = 500m,
            AvailableAmount = canReserve ? 500m : 50m,
            ShortfallAmount = canReserve ? 0m : 50m,
            RequiredActions = canReserve ? [] : ["Ask Finance to approve sufficient budget."]
        };

        private PurchaseRequisitionAuthorityReadinessDto AuthorityReadiness() => new()
        {
            RequisitionId = Requisition.Id,
            RequisitionNumber = Requisition.RequisitionNumber,
            Status = Requisition.Status,
            IsCompliant = true,
            CanSubmit = true,
            DecisionCode = "PR_AUTHORITY_ROUTE_CAPTURED",
            Message = "Authority route captured.",
            AuthorityRouteId = AuthorityRoute.Id,
            RouteReference = AuthorityRoute.RouteReference,
            WorkflowDefinitionId = AuthorityRoute.WorkflowDefinitionId
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
