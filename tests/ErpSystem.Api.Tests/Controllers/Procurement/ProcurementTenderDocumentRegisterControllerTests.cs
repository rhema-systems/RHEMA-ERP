using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementTenderDocumentRegisterControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesOnlyTheSharedControlSurface()
    {
        var type = typeof(ProcurementTenderDocumentRegisterController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/tender-document-register");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "GetReadiness", "Get", "Bind", "Issue", "CreateChange", "DecideChange", "Acknowledge"
            ]);
    }

    [Fact]
    public async Task MutationsForwardControllerCorrelationAndReturnDedicatedContracts()
    {
        var sourceId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.BindAsync(
                It.IsAny<BindProcurementTenderDocumentRegisterRequest>(), "trace-register",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderDocumentRegisterDto
            {
                Id = registerId,
                SourceType = ProcurementTenderDocumentSourceType.Tender,
                SourceId = sourceId
            });
        var controller = Controller(service, "trace-register");

        var result = (CreatedAtActionResult)await controller.Bind(
            new BindProcurementTenderDocumentRegisterRequest
            {
                SourceType = ProcurementTenderDocumentSourceType.Tender,
                SourceId = sourceId
            }, default);

        result.Value.Should().BeAssignableTo<ProcurementTenderDocumentRegisterDto>()
            .Which.Id.Should().Be(registerId);
        service.VerifyAll();
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.GetRegisterAsync(
                It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlNotFoundException("REGISTER_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetRegisterReadinessAsync(
                It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlAuthorizationException("Forbidden."));
        service.Setup(item => item.DecideChangeAsync(
                It.IsAny<Guid>(), It.IsAny<DecideProcurementTenderDocumentChangeRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlConflictException("CHANGE_CONFLICT", "Conflict."));
        service.Setup(item => item.AcknowledgeAsync(
                It.IsAny<AcknowledgeProcurementTenderDocumentRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlValidationException("ACK_INVALID", "Invalid."));
        var controller = Controller(service, "trace-register-problems");

        ((ObjectResult)await controller.Get(
            ProcurementTenderDocumentSourceType.Tender, Guid.Empty, default)).StatusCode.Should().Be(404);
        ((ObjectResult)await controller.GetReadiness(
            ProcurementTenderDocumentSourceType.Tender, Guid.Empty, default)).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.DecideChange(
            Guid.NewGuid(), new DecideProcurementTenderDocumentChangeRequest(), default)).StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Acknowledge(
            new AcknowledgeProcurementTenderDocumentRequest(), default);
        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should().Be("ACK_INVALID");
    }

    [Theory]
    [InlineData("TENDER_DOCUMENT_RECIPIENT_INELIGIBLE")]
    [InlineData("TENDER_DOCUMENT_SAVED_RECIPIENT_REQUIRED")]
    [InlineData("TENDER_DOCUMENT_RECIPIENT_CONTACT_REQUIRED")]
    public async Task IssueValidationRetainsActionableProblemDetailAndCode(string code)
    {
        const string detail = "Select the saved supplier record before issuing this document.";
        var request = new IssueProcurementTenderDocumentControlRequest();
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.IssueAsync(request, "trace-issue", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlValidationException(code, detail));

        var result = (ObjectResult)await Controller(service, "trace-issue").Issue(request, default);

        result.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problem = result.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which;
        problem.Detail.Should().Be(detail);
        problem.Extensions["code"].Should().Be(code);
        service.Verify(item => item.IssueAsync(request, "trace-issue", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BindingForwardsThePendingScheduleRequestWithItsOriginalDates()
    {
        var request = new BindProcurementTenderDocumentRegisterRequest
        {
            SourceType = ProcurementTenderDocumentSourceType.Tender, SourceId = Guid.NewGuid(),
            SubmissionDeadlineUtc = DateTime.UtcNow.AddHours(-2), OpeningScheduledAtUtc = DateTime.UtcNow.AddHours(-1),
            ScheduleChange = new BindProcurementTenderDocumentScheduleChangeRequest
            {
                SubmissionDeadlineUtc = DateTime.UtcNow.AddDays(2), OpeningScheduledAtUtc = DateTime.UtcNow.AddDays(2).AddHours(1),
                Reason = "Preparation delayed", EvidenceReference = "UAT-SCHEDULE", WorkflowDefinitionId = Guid.NewGuid()
            }
        };
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.BindAsync(request, "bind-schedule-correlation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderDocumentRegisterDto { SourceId = request.SourceId, OriginalSubmissionDeadlineUtc = request.SubmissionDeadlineUtc });
        var result = (CreatedAtActionResult)await Controller(service, "bind-schedule-correlation").Bind(request, default);
        result.StatusCode.Should().Be(201);
        result.Value.Should().BeOfType<ProcurementTenderDocumentRegisterDto>().Which.OriginalSubmissionDeadlineUtc.Should().Be(request.SubmissionDeadlineUtc);
        service.Verify(item => item.BindAsync(request, "bind-schedule-correlation", It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("TENDER_DOCUMENT_TENDER_NOT_PUBLISHED")]
    [InlineData("TENDER_DOCUMENT_RESCHEDULE_NOT_ALLOWED")]
    public async Task DocumentStageConflictsRetainCodeAndDetail(string code)
    {
        var service = new Mock<IProcurementTenderDocumentControlService>();
        const string detail = "Complete the required publication stage first.";
        service.Setup(item => item.IssueAsync(It.IsAny<IssueProcurementTenderDocumentControlRequest>(),
            "trace-stage", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlConflictException(code, detail));
        var result = (ObjectResult)await Controller(service, "trace-stage")
            .Issue(new IssueProcurementTenderDocumentControlRequest(), default);
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Which;
        problem.Detail.Should().Be(detail);
        problem.Extensions["code"].Should().Be(code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisterReturnsServerDeterminedNewRecipientCapability(bool allowsNewRecipient)
    {
        var sourceId = Guid.NewGuid();
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.GetRegisterAsync(ProcurementTenderDocumentSourceType.Tender,
                sourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderDocumentRegisterDto { AllowsNewRecipient = allowsNewRecipient });

        var result = (OkObjectResult)await Controller(service, "trace-capability")
            .Get(ProcurementTenderDocumentSourceType.Tender, sourceId, default);

        result.Value.Should().BeAssignableTo<ProcurementTenderDocumentRegisterDto>()
            .Which.AllowsNewRecipient.Should().Be(allowsNewRecipient);
        typeof(IssueProcurementTenderDocumentControlRequest).GetProperty("AllowsNewRecipient").Should().BeNull();
    }

    [Fact]
    public async Task RescheduleForwardsPairedDatesThroughExistingGovernedChangeEndpoint()
    {
        var service = new Mock<IProcurementTenderDocumentControlService>();
        var request = new CreateProcurementTenderDocumentChangeRequest
        {
            ChangeType = ProcurementTenderDocumentChangeType.UnpublishedScheduleReschedule,
            NewValueUtc = DateTime.UtcNow.AddDays(2),
            NewOpeningScheduledAtUtc = DateTime.UtcNow.AddDays(2).AddMinutes(5)
        };
        service.Setup(item => item.CreateChangeAsync(request, "trace-schedule", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderDocumentChangeDto
            {
                ChangeType = request.ChangeType,
                Status = ProcurementTenderDocumentChangeStatus.PendingApproval,
                NewValueUtc = request.NewValueUtc,
                NewOpeningScheduledAtUtc = request.NewOpeningScheduledAtUtc
            });
        var result = (ObjectResult)await Controller(service, "trace-schedule").CreateChange(request, default);
        result.StatusCode.Should().Be(StatusCodes.Status201Created);
        var change = result.Value.Should().BeAssignableTo<ProcurementTenderDocumentChangeDto>().Which;
        change.Status.Should().Be(ProcurementTenderDocumentChangeStatus.PendingApproval);
        change.NewOpeningScheduledAtUtc.Should().Be(request.NewOpeningScheduledAtUtc);
        service.VerifyAll();
    }

    private static ProcurementTenderDocumentRegisterController Controller(
        Mock<IProcurementTenderDocumentControlService> service,
        string traceIdentifier) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier }
            }
        };
}
