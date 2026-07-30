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
