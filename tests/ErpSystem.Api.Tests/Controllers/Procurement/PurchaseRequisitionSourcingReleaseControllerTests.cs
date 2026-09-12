using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
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

public sealed class PurchaseRequisitionSourcingReleaseControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesDedicatedSourcingControlRoutes()
    {
        typeof(PurchaseRequisitionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(PurchaseRequisitionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(PurchaseRequisitionsController))
            .Select(method => method.Name);

        methods.Should().Contain([
            nameof(PurchaseRequisitionsController.GetSourcingReadiness),
            nameof(PurchaseRequisitionsController.GetSourcingReleaseHistory),
            nameof(PurchaseRequisitionsController.ReleaseForSourcing),
            nameof(PurchaseRequisitionsController.CreateRfqFromPurchaseRequisition)
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadReleaseOrEnterSourcingRoutes()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/sourcing-readiness"),
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/sourcing-release-history"),
            await client.PostAsJsonAsync($"/api/PurchaseRequisitions/{id}/sourcing-release", new { reason = "Acceptance release" }),
            await client.PostAsync($"/api/PurchaseRequisitions/{id}/create-rfq", null),
            await client.PostAsJsonAsync("/api/procurement/Tenders", new { sourcePurchaseRequisitionId = id, title = "Blocked tender", tenderType = "NCT" })
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadinessHistoryAndReleaseMapTenantSafeStableFailures()
    {
        var fixture = new Fixture();
        fixture.Sourcing.Setup(service => service.GetReadinessAsync(
                fixture.RequisitionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionSourcingAuthorizationException("Forbidden."));
        fixture.Sourcing.Setup(service => service.GetHistoryAsync(
                fixture.RequisitionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionSourcingNotFoundException("PR_NOT_FOUND", "Missing."));
        fixture.Sourcing.Setup(service => service.ReleaseAsync(
                fixture.RequisitionId, "Attempt blocked release", "trace-sourcing-api", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionSourcingBlockedException(fixture.BlockedReadiness));

        var forbidden = (ObjectResult)(await fixture.Controller.GetSourcingReadiness(
            fixture.RequisitionId, default)).Result!;
        var missing = (ObjectResult)(await fixture.Controller.GetSourcingReleaseHistory(
            fixture.RequisitionId, default)).Result!;
        var blocked = (ObjectResult)(await fixture.Controller.ReleaseForSourcing(
            fixture.RequisitionId,
            new ReleasePurchaseRequisitionForSourcingRequest { Reason = "Attempt blocked release" },
            default)).Result!;

        forbidden.StatusCode.Should().Be(403);
        forbidden.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_SOURCING_CONTROL_FORBIDDEN");
        missing.StatusCode.Should().Be(404);
        missing.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_NOT_FOUND");
        blocked.StatusCode.Should().Be(422);
        var problem = blocked.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_SOURCING_WORKFLOW_INCOMPLETE");
        problem.Extensions["readiness"].Should().BeSameAs(fixture.BlockedReadiness);
    }

    [Fact]
    public async Task SuccessfulReleaseUsesAuthenticatedTraceAndReturnsImmutableDto()
    {
        var fixture = new Fixture();
        var release = new PurchaseRequisitionSourcingReleaseDto
        {
            Id = Guid.NewGuid(),
            RequisitionId = fixture.RequisitionId,
            RequisitionNumber = "PR-SOURCING-API-001",
            ReleaseReference = "SRL-PR-SOURCING-API-001-A1",
            AttemptNumber = 1,
            ControlFingerprint = new string('a', 64),
            IntegrityHash = new string('b', 64)
        };
        fixture.Sourcing.Setup(service => service.ReleaseAsync(
                fixture.RequisitionId, "Approved for controlled sourcing", "trace-sourcing-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        var response = (OkObjectResult)(await fixture.Controller.ReleaseForSourcing(
            fixture.RequisitionId,
            new ReleasePurchaseRequisitionForSourcingRequest { Reason = "Approved for controlled sourcing" },
            default)).Result!;

        response.Value.Should().BeSameAs(release);
        fixture.Sourcing.Verify(service => service.ReleaseAsync(
            fixture.RequisitionId, "Approved for controlled sourcing", "trace-sourcing-api", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RfqCreationAndTenderCreationReturnStructuredHardStopsBeforeMutation()
    {
        var fixture = new Fixture();
        fixture.Rfq.Setup(service => service.CreateRfqFromPurchaseRequisitionAsync(
                fixture.RequisitionId, It.IsAny<CreateRfqFromPurchaseRequisitionDto?>()))
            .ThrowsAsync(new ProcurementRequisitionSourcingBlockedException(fixture.BlockedReadiness));
        var rfqResult = (ObjectResult)(await fixture.Controller.CreateRfqFromPurchaseRequisition(
            fixture.RequisitionId)).Result!;

        var tenderService = new Mock<ITenderService>();
        tenderService.Setup(service => service.CreateTenderAsync(It.IsAny<CreateTenderDto>()))
            .ThrowsAsync(new ProcurementRequisitionSourcingBlockedException(fixture.BlockedReadiness));
        var tenderController = new TendersController(
            tenderService.Object,
            Mock.Of<IWorkflowService>(),
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(),
            NullLogger<TendersController>.Instance);
        var tenderResult = (ObjectResult)(await tenderController.CreateTender(new CreateTenderDto
        {
            SourcePurchaseRequisitionId = fixture.RequisitionId,
            Title = "Controlled tender",
            TenderType = "NCT"
        })).Result!;

        rfqResult.StatusCode.Should().Be(422);
        rfqResult.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_SOURCING_WORKFLOW_INCOMPLETE");
        tenderResult.StatusCode.Should().Be(422);
        tenderResult.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("PR_SOURCING_WORKFLOW_INCOMPLETE");
        fixture.Rfq.Verify(service => service.CreateRfqFromPurchaseRequisitionAsync(
            fixture.RequisitionId, It.IsAny<CreateRfqFromPurchaseRequisitionDto?>()), Times.Once);
        tenderService.Verify(service => service.CreateTenderAsync(It.IsAny<CreateTenderDto>()), Times.Once);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            BlockedReadiness = new PurchaseRequisitionSourcingReadinessDto
            {
                RequisitionId = RequisitionId,
                RequisitionNumber = "PR-SOURCING-API-001",
                Status = "Approved",
                IsCompliant = false,
                IsReleased = false,
                DecisionCode = "PR_SOURCING_WORKFLOW_INCOMPLETE",
                Message = "The exact authority workflow has not completed.",
                RequiredActions = ["Complete the exact authority workflow."]
            };
            Controller = new PurchaseRequisitionsController(
                Mock.Of<IPurchaseRequisitionRepository>(),
                Mock.Of<IPurchaseRequisitionItemRepository>(),
                Rfq.Object,
                Mock.Of<ITenantContext>(),
                Mock.Of<IUnitOfWork>(),
                Mock.Of<ICurrentUserProvider>(),
                Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(),
                Mock.Of<IWorkflowService>(),
                Mock.Of<IProcurementRequisitionLinkageService>(),
                Mock.Of<IProcurementRequisitionSubmissionControlService>(),
                Mock.Of<IProcurementRequisitionBudgetControlService>(),
                Mock.Of<IProcurementRequisitionAuthorityRouteService>(),
                Sourcing.Object,
                Mock.Of<IProcurementAccessControlService>(),
                Mock.Of<IAppEventBus>(),
                NullLogger<PurchaseRequisitionsController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-sourcing-api" }
                }
            };
        }

        public Guid RequisitionId { get; } = Guid.NewGuid();
        public Mock<IRfqService> Rfq { get; } = new();
        public Mock<IProcurementRequisitionSourcingReleaseService> Sourcing { get; } = new();
        public PurchaseRequisitionsController Controller { get; }
        public PurchaseRequisitionSourcingReadinessDto BlockedReadiness { get; }
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
