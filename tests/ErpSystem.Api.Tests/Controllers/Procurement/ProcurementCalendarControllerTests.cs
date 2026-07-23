using System.Net;
using System.Reflection;
using System.Text;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
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
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementCalendarControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesCompleteCalendarLifecycle()
    {
        typeof(ProcurementCalendarController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(ProcurementCalendarController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ProcurementCalendarController))
            .Select(method => method.Name);

        methods.Should().Contain([
            "GetSummary", "GetProfiles", "GetProfile", "CreateProfile", "UpdateProfile", "CloneProfile",
            "PublishProfile", "RetireProfile", "DeleteDraft", "GetOccurrences", "GetOccurrence",
            "Acknowledge", "Complete", "Cancel", "GetRuns", "Run", "GetTimeZones"
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadCalendar()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/procurement/calendar/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedCallerCanTraverseQueriesLifecycleTasksAndRuns()
    {
        var profileId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var service = CalendarService(profileId, occurrenceId);
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();
        var save = Json("""
            {"profileCode":"TDC-ANNUAL-CALENDAR","name":"Annual calendar","timeZoneId":"UTC",
             "generationHorizonDays":365,"catchUpDays":30,"effectiveFromUtc":"2026-07-22T00:00:00Z",
             "changeSummary":"Controlled dates","rules":[]}
            """);
        var lifecycle = Json("""{"rowVersion":"AQ==","reason":"Reviewed","approvalReference":"APPROVAL-001"}""");

        var responses = new List<HttpResponseMessage>
        {
            await client.GetAsync("/api/procurement/calendar/summary"),
            await client.GetAsync("/api/procurement/calendar/time-zones"),
            await client.GetAsync("/api/procurement/calendar/profiles"),
            await client.GetAsync($"/api/procurement/calendar/profiles/{profileId}"),
            await client.PostAsync("/api/procurement/calendar/profiles", save),
            await client.PutAsync($"/api/procurement/calendar/profiles/{profileId}", Json("""
                {"profileCode":"TDC-ANNUAL-CALENDAR","name":"Annual calendar","timeZoneId":"UTC",
                 "generationHorizonDays":365,"catchUpDays":30,"effectiveFromUtc":"2026-07-22T00:00:00Z",
                 "changeSummary":"Controlled dates","rowVersion":"AQ==","rules":[]}
                """)),
            await client.PostAsync($"/api/procurement/calendar/profiles/{profileId}/clone",
                Json("""{"changeSummary":"Next version","effectiveFromUtc":"2027-01-01T00:00:00Z"}""")),
            await client.PostAsync($"/api/procurement/calendar/profiles/{profileId}/publish", lifecycle),
            await client.PostAsync($"/api/procurement/calendar/profiles/{profileId}/retire", lifecycle),
            await client.GetAsync("/api/procurement/calendar/occurrences?page=1&pageSize=20"),
            await client.GetAsync($"/api/procurement/calendar/occurrences/{occurrenceId}"),
            await client.PostAsync($"/api/procurement/calendar/occurrences/{occurrenceId}/acknowledge", lifecycle),
            await client.PostAsync($"/api/procurement/calendar/occurrences/{occurrenceId}/complete", lifecycle),
            await client.PostAsync($"/api/procurement/calendar/occurrences/{occurrenceId}/cancel", lifecycle),
            await client.GetAsync("/api/procurement/calendar/runs?take=20"),
            await client.PostAsync("/api/procurement/calendar/runs", Json("""{"reason":"Manual reconciliation","horizonDays":30}"""))
        };
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/procurement/calendar/profiles/{profileId}")
        {
            Content = lifecycle
        };
        responses.Add(await client.SendAsync(delete));

        responses.Should().OnlyContain(response => response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementCalendarService>();
        service.Setup(item => item.GetProfileAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementCalendarNotFoundException("Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementCalendarAuthorizationException("Forbidden."));
        service.Setup(item => item.GetRunsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementCalendarConflictException("Conflict."));
        service.Setup(item => item.RunAsync(It.IsAny<ProcurementCalendarRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementCalendarValidationException("REASON_REQUIRED", "Invalid."));
        var controller = Controller(service, "trace-calendar");

        ((ObjectResult)await controller.GetProfile(Guid.Empty, default)).StatusCode.Should().Be(404);
        ((ObjectResult)await controller.GetSummary(default)).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.GetRuns(50, default)).StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Run(new ProcurementCalendarRunRequest(), default);
        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["code"]
            .Should().Be("REASON_REQUIRED");
    }

    private static Mock<IProcurementCalendarService> CalendarService(Guid profileId, Guid occurrenceId)
    {
        var service = new Mock<IProcurementCalendarService>();
        var profile = new ProcurementCalendarProfileDto { Id = profileId, ProfileCode = "TDC-ANNUAL-CALENDAR", RowVersion = "AQ==" };
        var occurrence = new ProcurementCalendarOccurrenceDto { Id = occurrenceId, OccurrenceKey = "CALENDAR-2026", RowVersion = "AQ==" };
        var run = new ProcurementCalendarRunDto { Id = Guid.NewGuid(), RunKey = "calendar-run" };
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementCalendarSummaryDto());
        service.Setup(item => item.GetTimeZones()).Returns(["UTC"]);
        service.Setup(item => item.GetProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
        service.Setup(item => item.GetProfileAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        service.Setup(item => item.CreateProfileAsync(It.IsAny<SaveProcurementCalendarProfileRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        service.Setup(item => item.UpdateProfileAsync(profileId, It.IsAny<SaveProcurementCalendarProfileRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        service.Setup(item => item.CloneProfileAsync(profileId, It.IsAny<CloneProcurementCalendarProfileRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        service.Setup(item => item.PublishProfileAsync(profileId, It.IsAny<ProcurementCalendarLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        service.Setup(item => item.RetireProfileAsync(profileId, It.IsAny<ProcurementCalendarLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        service.Setup(item => item.DeleteDraftAsync(profileId, It.IsAny<ProcurementCalendarLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        service.Setup(item => item.SearchOccurrencesAsync(It.IsAny<ProcurementCalendarOccurrenceSearchRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementCalendarOccurrencePageDto());
        service.Setup(item => item.GetOccurrenceAsync(occurrenceId, It.IsAny<CancellationToken>())).ReturnsAsync(occurrence);
        service.Setup(item => item.AcknowledgeAsync(occurrenceId, It.IsAny<ProcurementCalendarOccurrenceActionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(occurrence);
        service.Setup(item => item.CompleteAsync(occurrenceId, It.IsAny<ProcurementCalendarOccurrenceActionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(occurrence);
        service.Setup(item => item.CancelAsync(occurrenceId, It.IsAny<ProcurementCalendarOccurrenceActionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(occurrence);
        service.Setup(item => item.GetRunsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([run]);
        service.Setup(item => item.RunAsync(It.IsAny<ProcurementCalendarRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(run);
        return service;
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private static ProcurementCalendarController Controller(Mock<IProcurementCalendarService> service, string traceIdentifier) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier }
            }
        };

    private static WebApplicationFactory<Program> CreateFactory(PolicyAuthorizationMode mode,
        Mock<IProcurementCalendarService>? service = null)
    {
        service ??= new Mock<IProcurementCalendarService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementCalendarService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
