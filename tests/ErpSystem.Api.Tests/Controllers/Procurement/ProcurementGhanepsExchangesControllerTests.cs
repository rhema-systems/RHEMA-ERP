using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementGhanepsExchangesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesOnlyControlledActions()
    {
        var type = typeof(ProcurementGhanepsExchangesController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/ghaneps-exchanges/{sourceType}/{sourceId:guid}");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().BeEquivalentTo(
                nameof(ProcurementGhanepsExchangesController.GetOverview),
                nameof(ProcurementGhanepsExchangesController.GetStatus),
                nameof(ProcurementGhanepsExchangesController.GetOptions),
                nameof(ProcurementGhanepsExchangesController.GetHistory),
                nameof(ProcurementGhanepsExchangesController.GetEvent),
                nameof(ProcurementGhanepsExchangesController.PrepareExport),
                nameof(ProcurementGhanepsExchangesController.RecordImport),
                nameof(ProcurementGhanepsExchangesController.RecordAttempt),
                nameof(ProcurementGhanepsExchangesController.Retry),
                nameof(ProcurementGhanepsExchangesController.RecordAcknowledgement),
                nameof(ProcurementGhanepsExchangesController.Reconcile));
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())
            .Should().NotContain(attribute =>
                !string.IsNullOrWhiteSpace(attribute.Roles));
    }

    [Fact]
    public void RoutesAreSourceScopedAndExposeNoArbitraryMutation()
    {
        var expected = new Dictionary<string, (string Verb, string? Template)>
        {
            [nameof(ProcurementGhanepsExchangesController.GetOverview)] =
                ("GET", null),
            [nameof(ProcurementGhanepsExchangesController.GetStatus)] =
                ("GET", "status"),
            [nameof(ProcurementGhanepsExchangesController.GetOptions)] =
                ("GET", "options"),
            [nameof(ProcurementGhanepsExchangesController.GetHistory)] =
                ("GET", "history"),
            [nameof(ProcurementGhanepsExchangesController.GetEvent)] =
                ("GET", "events/{exchangeEventId:guid}"),
            [nameof(ProcurementGhanepsExchangesController.PrepareExport)] =
                ("POST", "exports"),
            [nameof(ProcurementGhanepsExchangesController.RecordImport)] =
                ("POST", "imports"),
            [nameof(ProcurementGhanepsExchangesController.RecordAttempt)] =
                ("POST", "events/{exchangeEventId:guid}/attempts"),
            [nameof(ProcurementGhanepsExchangesController.Retry)] =
                ("POST", "events/{exchangeEventId:guid}/retry"),
            [nameof(ProcurementGhanepsExchangesController.RecordAcknowledgement)] =
                ("POST", "events/{exchangeEventId:guid}/acknowledgements"),
            [nameof(ProcurementGhanepsExchangesController.Reconcile)] =
                ("POST", "events/{exchangeEventId:guid}/reconciliations")
        };

        foreach (var (methodName, route) in expected)
        {
            var attribute = typeof(ProcurementGhanepsExchangesController)
                .GetMethod(methodName)!
                .GetCustomAttributes()
                .OfType<HttpMethodAttribute>()
                .Single();
            attribute.HttpMethods.Should().ContainSingle().Which
                .Should().Be(route.Verb);
            attribute.Template.Should().Be(route.Template);
        }

        typeof(ProcurementGhanepsExchangesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType ==
                typeof(ProcurementGhanepsExchangesController))
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.HttpMethods)
            .Should().OnlyContain(verb => verb == "GET" || verb == "POST");
    }

    [Fact]
    public void RequestContractsCannotSupplyTenantOrArbitraryEventStatus()
    {
        var requestTypes = new[]
        {
            typeof(PrepareProcurementGhanepsExportRequest),
            typeof(RecordProcurementGhanepsImportRequest),
            typeof(RecordProcurementGhanepsAttemptRequest),
            typeof(RetryProcurementGhanepsExchangeRequest),
            typeof(RecordProcurementGhanepsAcknowledgementRequest),
            typeof(ReconcileProcurementGhanepsExchangeRequest)
        };

        requestTypes.SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .Should().NotContain(["TenantId", "Status"]);
        requestTypes.Skip(2).SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .Should().NotContain(["SourceType", "SourceId"]);
        typeof(ReconcileProcurementGhanepsExchangeRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("Outcome");
        typeof(ProcurementGhanepsExchangeEventDto).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("TenantId");
        typeof(ProcurementGhanepsRouteBoundMutationRequest).GetProperties()
            .Should().OnlyContain(property =>
                property.GetCustomAttribute<JsonIgnoreAttribute>() != null);
    }

    [Theory]
    [InlineData("Tender", ProcurementGhanepsSourceType.Tender)]
    [InlineData("tEnDeR", ProcurementGhanepsSourceType.Tender)]
    [InlineData("RequestForQuotation", ProcurementGhanepsSourceType.RequestForQuotation)]
    [InlineData("exceptionalsourcing", ProcurementGhanepsSourceType.ExceptionalSourcing)]
    public async Task CanonicalSourceNamesAreParsedCaseInsensitively(
        string routeSourceType,
        ProcurementGhanepsSourceType expectedSourceType)
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        fixture.Service.Setup(service => service.GetOverviewAsync(
                expectedSourceType,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Overview(expectedSourceType, sourceId));

        var result = await fixture.Controller.GetOverview(
            routeSourceType, sourceId, default);

        result.Should().BeOfType<OkObjectResult>();
        fixture.Service.VerifyAll();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("AppSubmission")]
    [InlineData("Tender,RequestForQuotation")]
    public async Task NumericOrUnsupportedSourceTypesFailClosed(
        string routeSourceType)
    {
        var fixture = new Fixture();

        var result = await fixture.Controller.GetOverview(
            routeSourceType, Guid.NewGuid(), default);

        var problem = result.Should()
            .BeOfType<UnprocessableEntityObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("GHANEPS_EXCHANGE_SOURCE_TYPE_INVALID");
        fixture.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OverviewStatusAndOptionsForwardOnlyTheRouteSource()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var overview = Overview(ProcurementGhanepsSourceType.Tender, sourceId);
        var options = new ProcurementGhanepsExchangeOptionsDto
        {
            SourceType = ProcurementGhanepsSourceType.Tender,
            SourceId = sourceId
        };
        fixture.Service.Setup(service => service.GetOverviewAsync(
                ProcurementGhanepsSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);
        fixture.Service.Setup(service => service.GetOptionsAsync(
                ProcurementGhanepsSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);

        (await fixture.Controller.GetOverview(
            "Tender", sourceId, default)).Should()
            .BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(overview);
        (await fixture.Controller.GetStatus(
            "Tender", sourceId, default)).Should()
            .BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(overview);
        (await fixture.Controller.GetOptions(
            "Tender", sourceId, default)).Should()
            .BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(options);

        fixture.Service.Verify(service => service.GetOverviewAsync(
            ProcurementGhanepsSourceType.Tender,
            sourceId,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task HistoryIsFlattenedInReverseChronologicalOrderWithEventLineage()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var firstEventId = Guid.NewGuid();
        var secondEventId = Guid.NewGuid();
        var older = History(firstEventId, "export", new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc));
        var newer = History(secondEventId, "acknowledgement", new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc));
        var overview = Overview(
            ProcurementGhanepsSourceType.Tender,
            sourceId,
            [
                Event(ProcurementGhanepsSourceType.Tender, sourceId, firstEventId, [older]),
                Event(ProcurementGhanepsSourceType.Tender, sourceId, secondEventId, [newer])
            ]);
        fixture.Service.Setup(service => service.GetOverviewAsync(
                ProcurementGhanepsSourceType.Tender,
                sourceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);

        var result = await fixture.Controller.GetHistory(
            "Tender", sourceId, default);

        var history = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should()
            .BeAssignableTo<IEnumerable<ProcurementGhanepsExchangeHistoryItemDto>>()
            .Subject.ToArray();
        history.Should().ContainInOrder(newer, older);
        history.Select(item => item.ExchangeEventId)
            .Should().ContainInOrder(secondEventId, firstEventId);
    }

    [Fact]
    public async Task EventReadFailsClosedForAHostileCrossSourceIdentifier()
    {
        var fixture = new Fixture();
        var routeSourceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        fixture.Service.Setup(service => service.GetAsync(
                eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Event(
                ProcurementGhanepsSourceType.Tender,
                Guid.NewGuid(),
                eventId));

        var result = await fixture.Controller.GetEvent(
            "Tender", routeSourceId, eventId, default);

        var problem = result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("GHANEPS_EXCHANGE_ROUTE_RESOURCE_NOT_FOUND");
    }

    [Fact]
    public async Task ExportAndImportBindAuthoritativeRouteBeforeCoreValidation()
    {
        var fixture = new Fixture();
        var routeSourceId = Guid.NewGuid();
        var exportRequest = new PrepareProcurementGhanepsExportRequest
        {
            SourceType = ProcurementGhanepsSourceType.RequestForQuotation,
            SourceId = routeSourceId
        };
        var importRequest = new RecordProcurementGhanepsImportRequest
        {
            SourceType = ProcurementGhanepsSourceType.Tender,
            SourceId = Guid.NewGuid()
        };
        fixture.Service.Setup(service => service.PrepareExportAsync(
                exportRequest, "corr-0212", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementGhanepsExchangeValidationException(
                "GHANEPS_EXCHANGE_SOURCE_ROUTE_MISMATCH",
                "Route mismatch."));
        fixture.Service.Setup(service => service.RecordImportAsync(
                importRequest, "corr-0212", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementGhanepsExchangeValidationException(
                "GHANEPS_EXCHANGE_SOURCE_ROUTE_MISMATCH",
                "Route mismatch."));

        var exportResult = await fixture.Controller.PrepareExport(
            "Tender",
            routeSourceId,
            exportRequest,
            default);
        var importResult = await fixture.Controller.RecordImport(
            "Tender",
            routeSourceId,
            importRequest,
            default);

        foreach (var result in new[] { exportResult, importResult })
        {
            var problem = result.Should()
                .BeOfType<UnprocessableEntityObjectResult>().Which.Value
                .Should().BeOfType<ValidationProblemDetails>().Subject;
            problem.Extensions["code"].Should()
                .Be("GHANEPS_EXCHANGE_SOURCE_ROUTE_MISMATCH");
        }
        exportRequest.RouteSourceType.Should()
            .Be(ProcurementGhanepsSourceType.Tender);
        exportRequest.RouteSourceId.Should().Be(routeSourceId);
        exportRequest.SourceType.Should()
            .Be(ProcurementGhanepsSourceType.RequestForQuotation);
        importRequest.RouteSourceType.Should()
            .Be(ProcurementGhanepsSourceType.Tender);
        importRequest.RouteSourceId.Should().Be(routeSourceId);
        importRequest.SourceId.Should().NotBe(routeSourceId);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task OmittedCreateBodySourceUsesAuthoritativeRouteMetadata()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var request = new PrepareProcurementGhanepsExportRequest();
        var created = Event(
            ProcurementGhanepsSourceType.RequestForQuotation,
            sourceId);
        fixture.Service.Setup(service => service.PrepareExportAsync(
                request, "corr-0212", It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await fixture.Controller.PrepareExport(
            "RequestForQuotation", sourceId, request, default);

        result.Should().BeOfType<CreatedAtActionResult>();
        request.SourceType.Should()
            .Be(ProcurementGhanepsSourceType.RequestForQuotation);
        request.SourceId.Should().Be(sourceId);
        request.RouteSourceType.Should()
            .Be(ProcurementGhanepsSourceType.RequestForQuotation);
        request.RouteSourceId.Should().Be(sourceId);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task ExportAndImportForwardMatchedSourceRouteAndCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var exportRequest = new PrepareProcurementGhanepsExportRequest
        {
            SourceType = ProcurementGhanepsSourceType.ExceptionalSourcing,
            SourceId = sourceId
        };
        var importRequest = new RecordProcurementGhanepsImportRequest
        {
            SourceType = ProcurementGhanepsSourceType.ExceptionalSourcing,
            SourceId = sourceId
        };
        var exportEvent = Event(
            ProcurementGhanepsSourceType.ExceptionalSourcing,
            sourceId);
        var importEvent = Event(
            ProcurementGhanepsSourceType.ExceptionalSourcing,
            sourceId);
        fixture.Service.Setup(service => service.PrepareExportAsync(
                exportRequest, "corr-0212", It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportEvent);
        fixture.Service.Setup(service => service.RecordImportAsync(
                importRequest, "corr-0212", It.IsAny<CancellationToken>()))
            .ReturnsAsync(importEvent);

        var exportResult = await fixture.Controller.PrepareExport(
            "exceptionalsourcing", sourceId, exportRequest, default);
        var importResult = await fixture.Controller.RecordImport(
            "ExceptionalSourcing", sourceId, importRequest, default);

        exportResult.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeSameAs(exportEvent);
        importResult.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeSameAs(importEvent);
        exportRequest.RouteSourceType.Should()
            .Be(ProcurementGhanepsSourceType.ExceptionalSourcing);
        exportRequest.RouteSourceId.Should().Be(sourceId);
        importRequest.RouteSourceType.Should()
            .Be(ProcurementGhanepsSourceType.ExceptionalSourcing);
        importRequest.RouteSourceId.Should().Be(sourceId);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task NestedActionsForwardAuthoritativeRouteMetadataAndCorrelation()
    {
        var fixture = new Fixture();
        var sourceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var current = Event(
            ProcurementGhanepsSourceType.RequestForQuotation,
            sourceId,
            eventId);
        var attempt = new RecordProcurementGhanepsAttemptRequest();
        var retry = new RetryProcurementGhanepsExchangeRequest();
        var acknowledgement =
            new RecordProcurementGhanepsAcknowledgementRequest();
        var reconciliation =
            new ReconcileProcurementGhanepsExchangeRequest();
        fixture.Service.Setup(service => service.RecordAttemptAsync(
                eventId, attempt, "corr-0212",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        fixture.Service.Setup(service => service.RetryAsync(
                eventId, retry, "corr-0212",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        fixture.Service.Setup(service => service.RecordAcknowledgementAsync(
                eventId, acknowledgement, "corr-0212",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        fixture.Service.Setup(service => service.ReconcileAsync(
                eventId, reconciliation, "corr-0212",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var results = new IActionResult[]
        {
            await fixture.Controller.RecordAttempt(
                "RequestForQuotation", sourceId, eventId, attempt, default),
            await fixture.Controller.Retry(
                "RequestForQuotation", sourceId, eventId, retry, default),
            await fixture.Controller.RecordAcknowledgement(
                "RequestForQuotation", sourceId, eventId, acknowledgement, default),
            await fixture.Controller.Reconcile(
                "RequestForQuotation", sourceId, eventId, reconciliation, default)
        };

        Assert.All(results, result =>
            Assert.Equal(
                StatusCodes.Status200OK,
                Assert.IsType<OkObjectResult>(result).StatusCode));
        foreach (var request in new ProcurementGhanepsRouteBoundMutationRequest[]
                 {
                     attempt,
                     retry,
                     acknowledgement,
                     reconciliation
                 })
        {
            request.RouteSourceType.Should()
                .Be(ProcurementGhanepsSourceType.RequestForQuotation);
            request.RouteSourceId.Should().Be(sourceId);
        }
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task CrossSourceNestedMutationIsDelegatedToAuditedCoreBoundary()
    {
        var fixture = new Fixture();
        var eventId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var request = new RetryProcurementGhanepsExchangeRequest();
        fixture.Service.Setup(service => service.RetryAsync(
                eventId,
                request,
                "corr-0212",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementGhanepsExchangeNotFoundException(
                "GHANEPS_EXCHANGE_ROUTE_RESOURCE_NOT_FOUND",
                "Wrong source."));

        var result = await fixture.Controller.Retry(
            "Tender",
            sourceId,
            eventId,
            request,
            default);

        var problem = result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("GHANEPS_EXCHANGE_ROUTE_RESOURCE_NOT_FOUND");
        request.RouteSourceType.Should().Be(ProcurementGhanepsSourceType.Tender);
        request.RouteSourceId.Should().Be(sourceId);
        fixture.Service.VerifyAll();
    }

    [Theory]
    [InlineData("missing", 404, "GHANEPS_EXCHANGE_NOT_FOUND")]
    [InlineData("forbidden", 403, "GHANEPS_EXCHANGE_ACCESS_FORBIDDEN")]
    [InlineData("conflict", 409, "GHANEPS_EXCHANGE_STALE")]
    [InlineData("invalid", 422, "GHANEPS_EXCHANGE_INVALID")]
    public async Task DomainFailuresMapToStructuredProblems(
        string failure,
        int expectedStatus,
        string expectedCode)
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service => service.GetOverviewAsync(
                It.IsAny<ProcurementGhanepsSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "missing" => new ProcurementGhanepsExchangeNotFoundException(
                    expectedCode, "Missing."),
                "forbidden" =>
                    new ProcurementGhanepsExchangeAuthorizationException(
                        "Forbidden."),
                "conflict" => new ProcurementGhanepsExchangeConflictException(
                    expectedCode, "Conflict."),
                _ => new ProcurementGhanepsExchangeValidationException(
                    expectedCode, "Invalid.")
            });

        var result = await fixture.Controller.GetOverview(
            "Tender", Guid.NewGuid(), default);

        var objectResult = result.Should()
            .BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
        var problem = objectResult.Value.Should()
            .BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(expectedCode);
        problem.Extensions["correlationId"].Should().Be("corr-0212");
    }

    [Fact]
    public async Task AnonymousCallerCannotReadSourceStatus()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var path =
            $"/api/procurement/ghaneps-exchanges/Tender/{Guid.NewGuid()}/status";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(
            "X-Correlation-ID",
            "corr-0212-anonymous");

        using var response = await client.SendAsync(request);

        await AssertStructuredProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            "corr-0212-anonymous",
            path);
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RawQueueAndArbitraryMutationRoutesDoNotExist()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Success);
        using var client = factory.CreateClient();
        var sourceRoute =
            $"/api/procurement/ghaneps-exchanges/Tender/{Guid.NewGuid()}";
        var eventRoute = $"{sourceRoute}/events/{Guid.NewGuid()}";
        var requests = new[]
        {
            (HttpMethod.Post, sourceRoute, "corr-0212-post-root"),
            (HttpMethod.Post, eventRoute, "corr-0212-post-event"),
            (HttpMethod.Put, eventRoute, "corr-0212-put"),
            (HttpMethod.Patch, eventRoute, "corr-0212-patch"),
            (HttpMethod.Delete, eventRoute, "corr-0212-delete")
        };

        foreach (var (method, path, correlationId) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.TryAddWithoutValidation(
                "X-Correlation-ID",
                correlationId);
            if (method != HttpMethod.Delete)
                request.Content = new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json");

            using var response = await client.SendAsync(request);

            await AssertStructuredProblemAsync(
                response,
                HttpStatusCode.MethodNotAllowed,
                "METHOD_NOT_ALLOWED",
                correlationId,
                path);
            response.Content.Headers.Allow.Should().Contain("GET");
        }
    }

    private static async Task AssertStructuredProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedCorrelationId,
        string expectedInstance)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/problem+json");
        response.Headers.GetValues("X-Correlation-ID").Should()
            .ContainSingle(expectedCorrelationId);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        root.GetProperty("code").GetString().Should().Be(expectedCode);
        root.GetProperty("correlationId").GetString().Should()
            .Be(expectedCorrelationId);
        root.GetProperty("instance").GetString().Should().Be(expectedInstance);
    }

    private static ProcurementGhanepsExchangeOverviewDto Overview(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        IReadOnlyList<ProcurementGhanepsExchangeEventDto>? events = null) =>
        new()
        {
            SourceType = sourceType,
            SourceId = sourceId,
            Events = events ?? Array.Empty<ProcurementGhanepsExchangeEventDto>()
        };

    private static ProcurementGhanepsExchangeEventDto Event(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        Guid? eventId = null,
        IReadOnlyList<ProcurementGhanepsExchangeHistoryItemDto>? history = null) =>
        new()
        {
            Id = eventId ?? Guid.NewGuid(),
            SourceType = sourceType,
            SourceId = sourceId,
            EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
            EventReference = "event-ref",
            History = history ??
                Array.Empty<ProcurementGhanepsExchangeHistoryItemDto>()
        };

    private static ProcurementGhanepsExchangeHistoryItemDto History(
        Guid eventId,
        string kind,
        DateTime occurredAtUtc) => new()
        {
            ExchangeEventId = eventId,
            EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
            EventReference = "event-ref",
            Kind = kind,
            RecordId = Guid.NewGuid(),
            Sequence = 1,
            OccurredAtUtc = occurredAtUtc
        };

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode)
    {
        var service = new Mock<IProcurementGhanepsExchangeService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementGhanepsExchangeService>();
                services.AddSingleton<IPolicyEvaluator>(
                    new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new ProcurementGhanepsExchangesController(
                Service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "trace-0212"
                    }
                }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0212";
        }

        public Mock<IProcurementGhanepsExchangeService> Service { get; } =
            new();
        public ProcurementGhanepsExchangesController Controller { get; }
    }
}
