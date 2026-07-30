using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementBidderCommunicationsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesOnlyControlledActions()
    {
        var type = typeof(ProcurementBidderCommunicationsController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/bidder-communications");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().BeEquivalentTo(
                nameof(ProcurementBidderCommunicationsController.GetOverview),
                nameof(ProcurementBidderCommunicationsController.GetHistory),
                nameof(ProcurementBidderCommunicationsController.Initialize),
                nameof(ProcurementBidderCommunicationsController.ApproveLetter),
                nameof(ProcurementBidderCommunicationsController.DispatchLetter),
                nameof(ProcurementBidderCommunicationsController.RecordDelivery),
                nameof(ProcurementBidderCommunicationsController.RecordAcknowledgement),
                nameof(ProcurementBidderCommunicationsController.FileAppeal),
                nameof(ProcurementBidderCommunicationsController.ResolveAppeal),
                nameof(ProcurementBidderCommunicationsController.RegisterSecurity),
                nameof(ProcurementBidderCommunicationsController.RecordSecurityAction),
                nameof(ProcurementBidderCommunicationsController.GetExternalOverview),
                nameof(ProcurementBidderCommunicationsController.RecordExternalAcknowledgement),
                nameof(ProcurementBidderCommunicationsController.FileExternalAppeal));
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())
            .Should().NotContain(attribute =>
                !string.IsNullOrWhiteSpace(attribute.Roles));
    }

    [Fact]
    public void ActionBodiesCannotOverrideSourceRecipientOrBusinessPartnerIdentity()
    {
        var controlledActionRequests = new[]
        {
            typeof(ApproveProcurementBidderCommunicationLetterRequest),
            typeof(DispatchProcurementBidderCommunicationLetterRequest),
            typeof(RecordProcurementBidderCommunicationDeliveryRequest),
            typeof(RecordProcurementBidderCommunicationAcknowledgementRequest),
            typeof(FileProcurementBidderAppealRequest),
            typeof(ResolveProcurementBidderAppealRequest),
            typeof(RegisterProcurementTenderSecurityRequest),
            typeof(RecordProcurementTenderSecurityActionRequest)
        };

        controlledActionRequests
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .Should().NotContain([
                "SourceType", "SourceId", "RecipientId", "BusinessPartnerId",
                "AwardId", "AwardReference", "Status"
            ]);
    }

    [Fact]
    public void RoutesExposeTheExactInternalAndSupplierScopedSurface()
    {
        var expected = new Dictionary<string, (string Verb, string Template)>
        {
            [nameof(ProcurementBidderCommunicationsController.GetOverview)] =
                ("GET", "{sourceType}/{sourceId:guid}"),
            [nameof(ProcurementBidderCommunicationsController.GetHistory)] =
                ("GET", "{sourceType}/{sourceId:guid}/history"),
            [nameof(ProcurementBidderCommunicationsController.Initialize)] =
                ("POST", "{sourceType}/{sourceId:guid}/initialize"),
            [nameof(ProcurementBidderCommunicationsController.ApproveLetter)] =
                ("POST", "{sourceType}/{sourceId:guid}/recipients/{recipientId:guid}/letters/approve"),
            [nameof(ProcurementBidderCommunicationsController.DispatchLetter)] =
                ("POST", "{sourceType}/{sourceId:guid}/letters/{letterVersionId:guid}/dispatch"),
            [nameof(ProcurementBidderCommunicationsController.RecordDelivery)] =
                ("POST", "{sourceType}/{sourceId:guid}/dispatches/{dispatchId:guid}/delivery"),
            [nameof(ProcurementBidderCommunicationsController.RecordAcknowledgement)] =
                ("POST", "{sourceType}/{sourceId:guid}/dispatches/{dispatchId:guid}/acknowledgement"),
            [nameof(ProcurementBidderCommunicationsController.FileAppeal)] =
                ("POST", "{sourceType}/{sourceId:guid}/recipients/{recipientId:guid}/appeals"),
            [nameof(ProcurementBidderCommunicationsController.ResolveAppeal)] =
                ("POST", "{sourceType}/{sourceId:guid}/appeals/{appealId:guid}/resolve"),
            [nameof(ProcurementBidderCommunicationsController.RegisterSecurity)] =
                ("POST", "{sourceType}/{sourceId:guid}/recipients/{recipientId:guid}/securities"),
            [nameof(ProcurementBidderCommunicationsController.RecordSecurityAction)] =
                ("POST", "{sourceType}/{sourceId:guid}/securities/{securityId:guid}/actions"),
            [nameof(ProcurementBidderCommunicationsController.GetExternalOverview)] =
                ("GET", "external/{sourceType}/{sourceId:guid}"),
            [nameof(ProcurementBidderCommunicationsController.RecordExternalAcknowledgement)] =
                ("POST", "external/{sourceType}/{sourceId:guid}/dispatches/{dispatchId:guid}/acknowledgement"),
            [nameof(ProcurementBidderCommunicationsController.FileExternalAppeal)] =
                ("POST", "external/{sourceType}/{sourceId:guid}/appeals")
        };

        foreach (var (methodName, route) in expected)
        {
            var attribute = typeof(ProcurementBidderCommunicationsController)
                .GetMethod(methodName)!
                .GetCustomAttributes()
                .OfType<HttpMethodAttribute>()
                .Single();
            attribute.HttpMethods.Should().ContainSingle().Which
                .Should().Be(route.Verb);
            attribute.Template.Should().Be(route.Template);
        }
    }

    [Fact]
    public async Task OverviewAndHistoryForwardTheTenantScopedRouteSource()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var expected = Overview(sourceId);
        fixture.Service.Setup(service => service.GetOverviewAsync(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        (await fixture.Controller.GetOverview(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
        (await fixture.Controller.GetHistory(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);

        fixture.Service.Verify(service => service.GetOverviewAsync(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InitializeRejectsHostileBodySourceMismatchBeforeServiceAccess()
    {
        var fixture = new Fixture();
        var routeSourceId = Guid.NewGuid();

        var result = await fixture.Controller.Initialize(
            ProcurementAwardReadinessSourceType.Tender,
            routeSourceId,
            new InitializeProcurementBidderCommunicationRegisterRequest
            {
                SourceType =
                    ProcurementAwardReadinessSourceType.RequestForQuotation,
                SourceId = Guid.NewGuid()
            },
            default);

        var problem = result.Should()
            .BeOfType<UnprocessableEntityObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("BIDDER_COMMUNICATION_SOURCE_ROUTE_MISMATCH");
        fixture.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InitializeForwardsMatchedSourceAndCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var request = new InitializeProcurementBidderCommunicationRegisterRequest
        {
            SourceType = ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            SourceId = sourceId,
            IdempotencyKey = "initialize-0211"
        };
        var expected = Overview(
            sourceId,
            ProcurementAwardReadinessSourceType.ExceptionalSourcing);
        fixture.Service.Setup(service => service.InitializeAsync(
                request, "corr-0211", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Controller.Initialize(
            request.SourceType, sourceId, request, default);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeSameAs(expected);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task InternalActionsForwardControlledIdentifiersAndCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var letterId = Guid.NewGuid();
        var dispatchId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var approve = new ApproveProcurementBidderCommunicationLetterRequest();
        var dispatch = new DispatchProcurementBidderCommunicationLetterRequest();
        var delivery = new RecordProcurementBidderCommunicationDeliveryRequest();
        var acknowledgement =
            new RecordProcurementBidderCommunicationAcknowledgementRequest();
        var appeal = new FileProcurementBidderAppealRequest();
        var resolve = new ResolveProcurementBidderAppealRequest();
        var security = new RegisterProcurementTenderSecurityRequest();
        var securityAction = new RecordProcurementTenderSecurityActionRequest();
        var overview = Overview(sourceId);
        overview.Recipients =
        [
            new ProcurementBidderCommunicationRecipientDto
            {
                Id = recipientId,
                Appeals =
                [
                    new ProcurementBidderAppealDto { Id = appealId }
                ],
                SecurityInstruments =
                [
                    new ProcurementTenderSecurityInstrumentDto
                    {
                        Id = securityId
                    }
                ],
                LetterVersions =
                [
                    new ProcurementBidderCommunicationLetterVersionDto
                    {
                        Id = letterId,
                        Dispatches =
                        [
                            new ProcurementBidderCommunicationDispatchDto
                            {
                                Id = dispatchId
                            }
                        ]
                    }
                ]
            }
        ];
        fixture.Service.Setup(service => service.GetOverviewAsync(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);

        fixture.Service.Setup(service => service.ApproveLetterAsync(
                recipientId, approve, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementBidderCommunicationLetterVersionDto());
        fixture.Service.Setup(service => service.DispatchLetterAsync(
                letterId, dispatch, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementBidderCommunicationDispatchDto());
        fixture.Service.Setup(service => service.RecordDeliveryAsync(
                dispatchId, delivery, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementBidderCommunicationDeliveryDto());
        fixture.Service.Setup(service => service.RecordAcknowledgementAsync(
                dispatchId, acknowledgement, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProcurementBidderCommunicationAcknowledgementDto());
        fixture.Service.Setup(service => service.FileAppealAsync(
                recipientId, appeal, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementBidderAppealDto());
        fixture.Service.Setup(service => service.ResolveAppealAsync(
                appealId, resolve, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementBidderAppealDecisionDto());
        fixture.Service.Setup(service => service.RegisterSecurityAsync(
                recipientId, security, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderSecurityInstrumentDto());
        fixture.Service.Setup(service => service.RecordSecurityActionAsync(
                securityId, securityAction, "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderSecurityActionDto());

        var results = new IActionResult[]
        {
            await fixture.Controller.ApproveLetter(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, recipientId, approve, default),
            await fixture.Controller.DispatchLetter(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, letterId, dispatch, default),
            await fixture.Controller.RecordDelivery(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, dispatchId, delivery, default),
            await fixture.Controller.RecordAcknowledgement(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, dispatchId, acknowledgement, default),
            await fixture.Controller.FileAppeal(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, recipientId, appeal, default),
            await fixture.Controller.ResolveAppeal(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, appealId, resolve, default),
            await fixture.Controller.RegisterSecurity(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, recipientId, security, default),
            await fixture.Controller.RecordSecurityAction(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId, securityId, securityAction, default)
        };

        Assert.All(results, result =>
            Assert.Equal(
                StatusCodes.Status201Created,
                Assert.IsType<ObjectResult>(result).StatusCode));
        fixture.Service.Verify(service => service.GetOverviewAsync(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            It.IsAny<CancellationToken>()), Times.Exactly(8));
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task CoreTenantLineageRejectionMapsToNotFoundOnDirectRoute()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var letterId = Guid.NewGuid();
        var overview = Overview(sourceId);
        overview.Recipients =
        [
            new ProcurementBidderCommunicationRecipientDto
            {
                Id = Guid.NewGuid(),
                LetterVersions =
                [
                    new ProcurementBidderCommunicationLetterVersionDto
                    {
                        Id = letterId
                    }
                ]
            }
        ];
        fixture.Service.Setup(service => service.GetOverviewAsync(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);
        fixture.Service.Setup(service => service.DispatchLetterAsync(
                letterId,
                It.IsAny<DispatchProcurementBidderCommunicationLetterRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementBidderCommunicationNotFoundException(
                "BIDDER_COMMUNICATION_NOT_FOUND",
                "The letter was not found in the current tenant."));

        var result = await fixture.Controller.DispatchLetter(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            letterId,
            new DispatchProcurementBidderCommunicationLetterRequest(),
            default);

        var problem = result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("BIDDER_COMMUNICATION_NOT_FOUND");
    }

    [Fact]
    public async Task InternalRouteCannotMutateAResourceOutsideItsSource()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        fixture.Service.Setup(service => service.GetOverviewAsync(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Overview(sourceId));

        var result = await fixture.Controller.DispatchLetter(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            Guid.NewGuid(),
            new DispatchProcurementBidderCommunicationLetterRequest(),
            default);

        var problem = result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("BIDDER_COMMUNICATION_ROUTE_RESOURCE_NOT_FOUND");
        fixture.Service.Verify(service => service.DispatchLetterAsync(
            It.IsAny<Guid>(),
            It.IsAny<DispatchProcurementBidderCommunicationLetterRequest>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExternalActionsUseOnlyTheCurrentSuppliersFilteredRecipient()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var dispatchId = Guid.NewGuid();
        var external = Overview(sourceId);
        external.Recipients =
        [
            new ProcurementBidderCommunicationRecipientDto
            {
                Id = recipientId,
                LetterVersions =
                [
                    new ProcurementBidderCommunicationLetterVersionDto
                    {
                        Dispatches =
                        [
                            new ProcurementBidderCommunicationDispatchDto
                            {
                                Id = dispatchId
                            }
                        ]
                    }
                ]
            }
        ];
        fixture.Service.Setup(service =>
                service.GetExternalOverviewAsync(
                    ProcurementAwardReadinessSourceType.Tender,
                    sourceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(external);
        var acknowledgement =
            new RecordProcurementBidderCommunicationAcknowledgementRequest();
        var appeal = new FileProcurementBidderAppealRequest();
        fixture.Service.Setup(service =>
                service.RecordExternalAcknowledgementAsync(
                    dispatchId,
                    acknowledgement,
                    "corr-0211",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProcurementBidderCommunicationAcknowledgementDto());
        fixture.Service.Setup(service => service.FileExternalAppealAsync(
                recipientId,
                appeal,
                "corr-0211",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementBidderAppealDto());

        (await fixture.Controller.GetExternalOverview(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            default)).Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(external);
        (await fixture.Controller.RecordExternalAcknowledgement(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            dispatchId,
            acknowledgement,
            default)).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        (await fixture.Controller.FileExternalAppeal(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            appeal,
            default)).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task ExternalRouteCannotAcknowledgeAnotherRecipientsDispatch()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        fixture.Service.Setup(service =>
                service.GetExternalOverviewAsync(
                    ProcurementAwardReadinessSourceType.Tender,
                    sourceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Overview(sourceId));

        var result = await fixture.Controller
            .RecordExternalAcknowledgement(
                ProcurementAwardReadinessSourceType.Tender,
                sourceId,
                Guid.NewGuid(),
                new RecordProcurementBidderCommunicationAcknowledgementRequest(),
                default);

        result.Should().BeOfType<NotFoundObjectResult>();
        fixture.Service.Verify(service =>
            service.RecordExternalAcknowledgementAsync(
                It.IsAny<Guid>(),
                It.IsAny<RecordProcurementBidderCommunicationAcknowledgementRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExternalAppealFailsClosedWhenSupplierRecipientIsAmbiguous()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var external = Overview(sourceId);
        external.Recipients =
        [
            new ProcurementBidderCommunicationRecipientDto
            {
                Id = Guid.NewGuid()
            },
            new ProcurementBidderCommunicationRecipientDto
            {
                Id = Guid.NewGuid()
            }
        ];
        fixture.Service.Setup(service =>
                service.GetExternalOverviewAsync(
                    ProcurementAwardReadinessSourceType.Tender,
                    sourceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(external);

        var result = await fixture.Controller.FileExternalAppeal(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            new FileProcurementBidderAppealRequest(),
            default);

        var problem = result.Should()
            .BeOfType<UnprocessableEntityObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("BIDDER_COMMUNICATION_EXTERNAL_RECIPIENT_AMBIGUOUS");
        fixture.Service.Verify(service => service.FileExternalAppealAsync(
            It.IsAny<Guid>(),
            It.IsAny<FileProcurementBidderAppealRequest>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("cross-tenant", 404, "BIDDER_COMMUNICATION_NOT_FOUND")]
    [InlineData("forbidden", 403, "BIDDER_COMMUNICATION_ACCESS_FORBIDDEN")]
    [InlineData("conflict", 409, "BIDDER_COMMUNICATION_STALE")]
    [InlineData("invalid", 422, "BIDDER_COMMUNICATION_INVALID")]
    public async Task DomainFailuresMapToStructuredProblems(
        string failure,
        int expectedStatus,
        string expectedCode)
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        fixture.Service.Setup(service => service.GetOverviewAsync(
                It.IsAny<ProcurementAwardReadinessSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "cross-tenant" =>
                    new ProcurementBidderCommunicationNotFoundException(
                    expectedCode, "Missing."),
                "forbidden" =>
                    new ProcurementBidderCommunicationAuthorizationException(
                        "Forbidden."),
                "conflict" => new ProcurementBidderCommunicationConflictException(
                    expectedCode, "Conflict."),
                _ => new ProcurementBidderCommunicationValidationException(
                    expectedCode, "Invalid.")
            });

        var result = await fixture.Controller.GetOverview(
            ProcurementAwardReadinessSourceType.Tender,
            sourceId,
            default);

        var objectResult = result.Should()
            .BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
        var problem = objectResult.Value.Should()
            .BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(expectedCode);
        problem.Extensions["correlationId"].Should().Be("corr-0211");
    }

    private static ProcurementBidderCommunicationOverviewDto Overview(
        Guid sourceId,
        ProcurementAwardReadinessSourceType sourceType =
            ProcurementAwardReadinessSourceType.Tender) => new()
        {
            Id = Guid.NewGuid(),
            SourceType = sourceType,
            SourceId = sourceId
        };

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new ProcurementBidderCommunicationsController(
                Service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "trace-0211"
                    }
                }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0211";
        }

        public Mock<IProcurementBidderCommunicationService> Service { get; } =
            new();
        public ProcurementBidderCommunicationsController Controller { get; }
    }
}
