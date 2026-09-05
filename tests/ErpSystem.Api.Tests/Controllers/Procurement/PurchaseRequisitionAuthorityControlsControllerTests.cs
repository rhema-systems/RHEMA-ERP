using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
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
using Microsoft.EntityFrameworkCore;
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
    public async Task OptionalAuthorityGuidanceDoesNotBlockConfiguredWorkflowSubmission()
    {
        var fixture = new ControllerFixture();
        var readiness = fixture.AuthorityReadiness(
            isCompliant: false, code: "PR_AUTHORITY_ROUTE_AMBIGUOUS");
        fixture.Authority.Setup(service => service.EnforceSubmissionAsync(
                fixture.Requisition, "trace-pr-authority", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionAuthorityBlockedException(readiness));
        fixture.Workflow.Setup(service => service.SubmitAsync(
                "PurchaseRequisition", fixture.Requisition.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Pending));

        var result = (ObjectResult)await fixture.Controller.SubmitPurchaseRequisition(fixture.Requisition.Id);

        result.StatusCode.Should().Be(200);
        fixture.Authority.Verify(service => service.EnforceSubmissionAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Workflow.Verify(service => service.SubmitAsync(
            "PurchaseRequisition", fixture.Requisition.Id), Times.Once);
    }

    [Fact]
    public async Task ReadySubmissionStartsConfiguredWorkflowWithoutAuthorityBandPrerequisite()
    {
        var fixture = new ControllerFixture();
        var calls = new List<string>();
        fixture.Workflow.Setup(service => service.SubmitAsync(
                "PurchaseRequisition", fixture.Requisition.Id))
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
        calls.Should().Equal("workflow");
        fixture.Workflow.Verify(service => service.SubmitAsync(
            "PurchaseRequisition", fixture.Requisition.Id), Times.Once);
        fixture.Authority.Verify(service => service.CaptureAsync(
            It.IsAny<PurchaseRequisition>(), It.IsAny<ProcurementAuthorityRouteDecisionDto>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Repository.Verify(service => service.UpdateRequisitionAsync(fixture.Requisition), Times.Once);
        fixture.UnitOfWork.Verify(service => service.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(service => service.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Requisition.Status.Should().Be("Pending Approval");
    }

    [Fact]
    public async Task RequesterSelfApprovalIsRejectedBeforeWorkflowMutation()
    {
        var fixture = new ControllerFixture(status: "Pending Approval");
        fixture.Requisition.RequestedById = fixture.UserId;

        var result = (ObjectResult)await fixture.Controller.ApprovePurchaseRequisition(
            fixture.Requisition.Id, new ApprovalDto { Approved = true });

        result.StatusCode.Should().Be(403);
        result.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_SELF_APPROVAL_FORBIDDEN");
        fixture.Workflow.Verify(service => service.CanUserApproveAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        fixture.Workflow.Verify(service => service.ProcessApprovalAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        fixture.Repository.Verify(service => service.UpdateRequisitionAsync(
            It.IsAny<PurchaseRequisition>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AssignedWorkflowApproverWithoutEffectiveCapabilityIsDeniedBeforeMutation(bool approved)
    {
        var fixture = new ControllerFixture(status: "Pending Approval");
        fixture.Workflow.Setup(service => service.CanUserApproveAsync(
                "PurchaseRequisition", fixture.Requisition.Id, fixture.UserId))
            .ReturnsAsync(true);
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(),
                "trace-pr-authority",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false,
                Code = "ACCESS_PERMISSION_DENIED",
                Message = "The current actor has no Security role granting this procurement privilege.",
                PermissionCode = "procurement.requisition.approve",
                ActorUserId = fixture.UserId,
                TenantId = fixture.Requisition.TenantId
            });

        var result = (ObjectResult)await fixture.Controller.ApprovePurchaseRequisition(
            fixture.Requisition.Id, new ApprovalDto
            {
                Approved = approved,
                Comments = approved ? "Approved." : "Returned for correction."
            });

        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_APPROVAL_CAPABILITY_REQUIRED");
        problem.Detail.Should().Contain("procurement.requisition.approve");
        problem.Detail.Should().Contain("no Security role");
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.requisition.approve" &&
                request.SourceType == "PurchaseRequisition" &&
                request.SourceReference == fixture.Requisition.RequisitionNumber),
            "trace-pr-authority",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Workflow.Verify(service => service.CanUserApproveAsync(
            "PurchaseRequisition", fixture.Requisition.Id, fixture.UserId), Times.Once);
        fixture.Workflow.Verify(service => service.ProcessApprovalAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        fixture.Budget.Verify(service => service.GetLinkedControlReadinessAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(service => service.ExecuteInStrategyAsync(
            It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Repository.Verify(service => service.UpdateRequisitionAsync(
            It.IsAny<PurchaseRequisition>()), Times.Never);
        fixture.Requisition.Status.Should().Be("Pending Approval");
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
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "The current actor has an effective approval capability.",
                    PermissionCode = "procurement.requisition.approve",
                    ActorUserId = UserId,
                    TenantId = Requisition.TenantId
                });
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
            Budget.Setup(service => service.GetReadinessAsync(
                    Requisition.Id, It.IsAny<CancellationToken>()))
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
            Workflow.Setup(service => service.HasActiveApprovalWorkflowAsync("PurchaseRequisition"))
                .ReturnsAsync(true);
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
                Access.Object,
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
        public Mock<IProcurementAccessControlService> Access { get; } = new();
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
            builder.UseSetting("CandidatePortal:PortalUrl", "https://candidate.test/");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
            });
        });
}

public sealed class PurchaseRequisitionWorkflowMutationCapabilityTests
{
    [Theory]
    [InlineData(WorkflowApprovalAction.Approve)]
    [InlineData(WorkflowApprovalAction.Reject)]
    [InlineData(WorkflowApprovalAction.Delegate)]
    [InlineData(WorkflowApprovalAction.RequestMoreInfo)]
    public async Task GenericWorkflowMutationWithoutEffectiveCapabilityIsDeniedBeforeEngineMutation(
        WorkflowApprovalAction action)
    {
        await using var fixture = await WorkflowFixture.CreateAsync("PurchaseRequisition");

        var response = await fixture.WorkflowController.ProcessApproval(
            fixture.Approval.Id,
            new ProcessApprovalRequest
            {
                Action = action,
                Comments = "Governed workflow action.",
                DelegateToId = action == WorkflowApprovalAction.Delegate ? Guid.NewGuid() : null
            });

        var result = response.Result.Should().BeOfType<ObjectResult>().Subject;
        AssertCapabilityForbidden(result);
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.requisition.approve" &&
                request.SourceType == "PurchaseRequisition" &&
                request.SourceReference == fixture.RequisitionNumber),
            "trace-pr-workflow",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Engine.Verify(service => service.ProcessStepAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<WorkflowStepAction>(), It.IsAny<object?>(), It.IsAny<string?>()),
            Times.Never);
        fixture.Db.ChangeTracker.HasChanges().Should().BeFalse();
        var persisted = await fixture.Db.WorkflowApprovals.AsNoTracking().SingleAsync(item => item.Id == fixture.Approval.Id);
        persisted.Status.Should().Be(WorkflowApprovalStatus.Pending);
        persisted.ProcessedById.Should().BeNull();
        persisted.ProcessedDate.Should().BeNull();
    }

    [Fact]
    public async Task AuthorizedPurchaseRequisitionDelegateUsesSharedWorkflowEngine()
    {
        await using var fixture = await WorkflowFixture.CreateAsync("PurchaseRequisition", accessAllowed: true);
        fixture.Engine.Setup(service => service.ProcessStepAsync(
                fixture.StepInstance.Id,
                fixture.UserId,
                WorkflowStepAction.Delegate,
                It.IsAny<object?>(),
                "Delegated."))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true });

        var response = await fixture.WorkflowController.ProcessApproval(
            fixture.Approval.Id,
            new ProcessApprovalRequest
            {
                Action = WorkflowApprovalAction.Delegate,
                DelegateToId = Guid.NewGuid(),
                Comments = "Delegated."
            });

        response.Result.Should().BeOfType<OkObjectResult>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.requisition.approve" &&
                request.SourceReference == fixture.RequisitionNumber),
            "trace-pr-workflow",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Engine.Verify(service => service.ProcessStepAsync(
            fixture.StepInstance.Id,
            fixture.UserId,
            WorkflowStepAction.Delegate,
            It.IsAny<object?>(),
            "Delegated."), Times.Once);
    }

    [Theory]
    [InlineData(WorkflowApprovalAction.Approve)]
    [InlineData(WorkflowApprovalAction.Reject)]
    [InlineData((WorkflowApprovalAction)999)]
    public async Task AuthorizedPurchaseRequisitionDecisionUsesCanonicalDomainEndpointWithoutEngineMutation(
        WorkflowApprovalAction action)
    {
        await using var fixture = await WorkflowFixture.CreateAsync("PurchaseRequisition", accessAllowed: true);

        var response = await fixture.WorkflowController.ProcessApproval(
            fixture.Approval.Id,
            new ProcessApprovalRequest { Action = action, Comments = "Governed decision." });

        var result = response.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var payload = JsonSerializer.Serialize(result.Value);
        payload.Should().Contain("PROCUREMENT_REQUISITION_DOMAIN_APPROVAL_REQUIRED");
        payload.Should().Contain($"/api/PurchaseRequisitions/{fixture.EntityId}/approve");
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.requisition.approve" &&
                request.SourceReference == fixture.RequisitionNumber),
            "trace-pr-workflow",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Engine.Verify(service => service.ProcessStepAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<WorkflowStepAction>(), It.IsAny<object?>(), It.IsAny<string?>()),
            Times.Never);
        fixture.Db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task PurchaseRequisitionSendBackWithoutEffectiveCapabilityIsDeniedBeforeMutation()
    {
        await using var fixture = await WorkflowFixture.CreateAsync("PurchaseRequisition");

        var result = (ObjectResult)await fixture.GovernanceController.SendBack(
            fixture.Approval.Id,
            new SendBackWorkflowRequest { Instructions = "Correct the supporting evidence." },
            CancellationToken.None);

        AssertCapabilityForbidden(result);
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.requisition.approve" &&
                request.SourceType == "PurchaseRequisition" &&
                request.SourceReference == fixture.RequisitionNumber),
            "trace-pr-governance",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Governance.Verify(service => service.CalculateDueDateAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Db.ChangeTracker.HasChanges().Should().BeFalse();
        var persisted = await fixture.Db.WorkflowApprovals.AsNoTracking().SingleAsync(item => item.Id == fixture.Approval.Id);
        persisted.Status.Should().Be(WorkflowApprovalStatus.Pending);
        persisted.ProcessedById.Should().BeNull();
        persisted.ProcessedDate.Should().BeNull();
        persisted.Comments.Should().BeNull();
        (await fixture.Db.WorkflowCorrectionRequests.CountAsync()).Should().Be(0);
        (await fixture.Db.WorkflowActivityLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuthorizedPurchaseRequisitionSendBackRecordsCorrection()
    {
        await using var fixture = await WorkflowFixture.CreateAsync("PurchaseRequisition", accessAllowed: true);
        fixture.Governance.Setup(service => service.CalculateDueDateAsync(
                fixture.Approval.TenantId,
                It.IsAny<DateTime>(),
                It.IsAny<double?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddHours(8));

        var result = await fixture.GovernanceController.SendBack(
            fixture.Approval.Id,
            new SendBackWorkflowRequest
            {
                CorrectionOwnerId = fixture.UserId,
                Instructions = "Correct the supporting evidence."
            },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.requisition.approve" &&
                request.SourceReference == fixture.RequisitionNumber),
            "trace-pr-governance",
            It.IsAny<CancellationToken>()), Times.Once);
        var persisted = await fixture.Db.WorkflowApprovals.AsNoTracking().SingleAsync(item => item.Id == fixture.Approval.Id);
        persisted.Status.Should().Be(WorkflowApprovalStatus.MoreInfoRequested);
        persisted.ProcessedById.Should().Be(fixture.UserId);
        persisted.Comments.Should().Be("Correct the supporting evidence.");
        (await fixture.Db.WorkflowCorrectionRequests.CountAsync()).Should().Be(1);
        (await fixture.Db.WorkflowActivityLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task NonPurchaseRequisitionWorkflowMutationPreservesSharedEndpointBehavior()
    {
        await using var fixture = await WorkflowFixture.CreateAsync("LeaveRequest", accessAllowed: false);
        fixture.Engine.Setup(service => service.ProcessStepAsync(
                fixture.StepInstance.Id,
                fixture.UserId,
                WorkflowStepAction.Delegate,
                It.IsAny<object?>(),
                "Delegated."))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true });

        var response = await fixture.WorkflowController.ProcessApproval(
            fixture.Approval.Id,
            new ProcessApprovalRequest
            {
                Action = WorkflowApprovalAction.Delegate,
                DelegateToId = Guid.NewGuid(),
                Comments = "Delegated."
            });

        response.Result.Should().BeOfType<OkObjectResult>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Engine.Verify(service => service.ProcessStepAsync(
            fixture.StepInstance.Id,
            fixture.UserId,
            WorkflowStepAction.Delegate,
            It.IsAny<object?>(),
            "Delegated."), Times.Once);
    }

    [Fact]
    public async Task SuperAdminCanUsePurchaseRequisitionSharedWorkflowMutationWithoutCapabilityLookup()
    {
        await using var fixture = await WorkflowFixture.CreateAsync("PurchaseRequisition", superAdmin: true);
        fixture.Engine.Setup(service => service.ProcessStepAsync(
                fixture.StepInstance.Id,
                fixture.UserId,
                WorkflowStepAction.Delegate,
                It.IsAny<object?>(),
                "Delegated."))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true });

        var response = await fixture.WorkflowController.ProcessApproval(
            fixture.Approval.Id,
            new ProcessApprovalRequest
            {
                Action = WorkflowApprovalAction.Delegate,
                DelegateToId = Guid.NewGuid(),
                Comments = "Delegated."
            });

        response.Result.Should().BeOfType<OkObjectResult>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Engine.Verify(service => service.ProcessStepAsync(
            fixture.StepInstance.Id,
            fixture.UserId,
            WorkflowStepAction.Delegate,
            It.IsAny<object?>(),
            "Delegated."), Times.Once);
    }

    private static void AssertCapabilityForbidden(ObjectResult result)
    {
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_APPROVAL_CAPABILITY_REQUIRED");
        problem.Extensions["correlationId"].Should().NotBeNull();
        problem.Detail.Should().Contain("procurement.requisition.approve");
        problem.Detail.Should().Contain("no Security role");
    }

    private sealed class WorkflowFixture : IAsyncDisposable
    {
        private WorkflowFixture(
            ApplicationDbContext db,
            Guid userId,
            Guid entityId,
            string requisitionNumber,
            WorkflowStepInstance stepInstance,
            WorkflowApproval approval,
            Mock<IWorkflowEngine> engine,
            Mock<IWorkflowApprovalRepository> approvalRepository,
            Mock<ICurrentUserService> currentUser,
            Mock<IProcurementAccessControlService> access,
            Mock<IWorkflowRuntimeGovernanceService> governance)
        {
            Db = db;
            UserId = userId;
            EntityId = entityId;
            RequisitionNumber = requisitionNumber;
            StepInstance = stepInstance;
            Approval = approval;
            Engine = engine;
            ApprovalRepository = approvalRepository;
            CurrentUser = currentUser;
            Access = access;
            Governance = governance;

            var workflowHttpContext = AuthenticatedContext(userId, "trace-pr-workflow");
            WorkflowController = new WorkflowController(
                engine.Object,
                Mock.Of<IWorkflowService>(),
                Mock.Of<IWorkflowDefinitionService>(),
                Mock.Of<IWorkflowDefinitionRepository>(),
                Mock.Of<IWorkflowInstanceService>(),
                Mock.Of<IWorkflowStepService>(),
                Mock.Of<IWorkflowApprovalService>(),
                Mock.Of<IWorkflowConditionEvaluator>(),
                Mock.Of<IWorkflowNotificationService>(),
                Mock.Of<IWorkflowInstanceRepository>(),
                Mock.Of<IWorkflowStepInstanceRepository>(),
                approvalRepository.Object,
                Mock.Of<IWorkflowEntityTypeRepository>(),
                Mock.Of<IWorkflowEntityTypeCatalogService>(),
                db,
                Mock.Of<IWorkflowStatusAdapterRegistry>(),
                Mock.Of<IAppEventBus>(),
                Mock.Of<IFileStorageService>(),
                currentUser.Object,
                Mock.Of<IProcurementRequisitionBudgetControlService>(),
                access.Object,
                Mock.Of<IProcedureCaseService>(),
                NullLogger<WorkflowController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = workflowHttpContext }
            };

            GovernanceController = new WorkflowGovernanceController(
                db,
                currentUser.Object,
                governance.Object,
                access.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = AuthenticatedContext(userId, "trace-pr-governance")
                }
            };
        }

        public static async Task<WorkflowFixture> CreateAsync(
            string entityTypeCode,
            bool accessAllowed = false,
            bool superAdmin = false)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var entityId = Guid.NewGuid();
            var requisitionNumber = $"PR-WF-{Guid.NewGuid():N}".ToUpperInvariant();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"pr-workflow-capability-{Guid.NewGuid():N}")
                    .Options,
                tenantId);

            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = entityTypeCode,
                Name = entityTypeCode
            };
            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"{entityTypeCode} approval",
                EntityTypeId = entityType.Id,
                EntityType = entityType
            };
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definition.Id,
                WorkflowDefinition = definition,
                Name = "Approval",
                StepType = WorkflowStepType.Approval,
                Order = 1
            };
            var instance = new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definition.Id,
                WorkflowDefinition = definition,
                EntityId = entityId,
                EntityTypeId = entityType.Id,
                EntityType = entityType,
                InitiatedById = Guid.NewGuid(),
                Status = WorkflowInstanceStatus.InProgress
            };
            var stepInstance = new WorkflowStepInstance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowInstanceId = instance.Id,
                WorkflowInstance = instance,
                WorkflowStepId = step.Id,
                WorkflowStep = step,
                Status = WorkflowStepInstanceStatus.InProgress
            };
            var approval = new WorkflowApproval
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StepInstanceId = stepInstance.Id,
                StepInstance = stepInstance,
                ApproverId = userId,
                Status = WorkflowApprovalStatus.Pending
            };
            var user = new ApplicationUser
            {
                Id = userId,
                TenantId = tenantId,
                UserName = "workflow.approver",
                NormalizedUserName = "WORKFLOW.APPROVER",
                FirstName = "Workflow",
                LastName = "Approver",
                IsActive = true
            };
            db.AddRange(user, entityType, definition, step, instance, stepInstance, approval);
            if (entityTypeCode.Equals("PurchaseRequisition", StringComparison.OrdinalIgnoreCase))
            {
                db.PurchaseRequisitions.Add(new PurchaseRequisition
                {
                    Id = entityId,
                    TenantId = tenantId,
                    RequisitionNumber = requisitionNumber,
                    RequestedById = Guid.NewGuid(),
                    Status = "Pending Approval"
                });
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var engine = new Mock<IWorkflowEngine>();
            var approvalRepository = new Mock<IWorkflowApprovalRepository>();
            approvalRepository.Setup(repository => repository.GetByIdAsync(approval.Id)).ReturnsAsync(approval);
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
            currentUser.SetupGet(service => service.UserName).Returns("workflow.approver");
            currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
            currentUser.SetupGet(service => service.Roles).Returns(superAdmin ? ["SuperAdmin"] : []);
            currentUser.Setup(service => service.IsInRole(It.IsAny<string>()))
                .Returns((string role) => superAdmin && role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase));
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = accessAllowed,
                    Code = accessAllowed ? "ACCESS_ALLOWED" : "ACCESS_PERMISSION_DENIED",
                    Message = accessAllowed
                        ? "The current actor has an effective approval capability."
                        : "The current actor has no Security role granting this procurement privilege.",
                    PermissionCode = "procurement.requisition.approve",
                    ActorUserId = userId,
                    TenantId = tenantId
                });
            var governance = new Mock<IWorkflowRuntimeGovernanceService>();

            return new WorkflowFixture(
                db,
                userId,
                entityId,
                requisitionNumber,
                stepInstance,
                approval,
                engine,
                approvalRepository,
                currentUser,
                access,
                governance);
        }

        public ApplicationDbContext Db { get; }
        public Guid UserId { get; }
        public Guid EntityId { get; }
        public string RequisitionNumber { get; }
        public WorkflowStepInstance StepInstance { get; }
        public WorkflowApproval Approval { get; }
        public Mock<IWorkflowEngine> Engine { get; }
        public Mock<IWorkflowApprovalRepository> ApprovalRepository { get; }
        public Mock<ICurrentUserService> CurrentUser { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public Mock<IWorkflowRuntimeGovernanceService> Governance { get; }
        public WorkflowController WorkflowController { get; }
        public WorkflowGovernanceController GovernanceController { get; }

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static DefaultHttpContext AuthenticatedContext(Guid userId, string traceIdentifier)
        {
            var context = new DefaultHttpContext { TraceIdentifier = traceIdentifier };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));
            return context;
        }
    }
}
