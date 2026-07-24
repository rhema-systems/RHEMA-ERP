using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementTenderDocumentTemplatesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesDedicatedLifecycleAndOptionRoutes()
    {
        var type = typeof(ProcurementTenderDocumentTemplatesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/tender-document-templates");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "GetSummary", "GetWorkflowOptions", "GetPolicyOptions", "Search", "Get",
                "Create", "Update", "Submit", "Publish", "Reject", "Clone", "Retire", "DeleteDraft"
            ]);
    }

    [Fact]
    public async Task PolicyOptionsAreServedThroughTheTemplateReaderBoundary()
    {
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.GetTemplatePolicyOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProcurementTenderDocumentPolicyOptionDto
                {
                    Id = Guid.NewGuid(),
                    Code = "TDC-POLICY",
                    Name = "TDC Policy",
                    Version = 3,
                    SourceConfigurationProfileId = Guid.NewGuid(),
                    SourceConfigurationProfileCode = "TDC-PROCUREMENT"
                }
            ]);
        var controller = Controller(service, "trace-policy-options");

        var result = (OkObjectResult)await controller.GetPolicyOptions(default);

        result.Value.Should().BeAssignableTo<IReadOnlyList<ProcurementTenderDocumentPolicyOptionDto>>()
            .Which.Should().ContainSingle(item =>
                item.Code == "TDC-POLICY" &&
                item.SourceConfigurationProfileCode == "TDC-PROCUREMENT");
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementTenderDocumentControlService>();
        service.Setup(item => item.GetTemplateAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlNotFoundException("DOC_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetTemplateSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlAuthorizationException("Forbidden."));
        service.Setup(item => item.GetTemplateWorkflowOptionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlConflictException("DOC_CONFLICT", "Conflict."));
        service.Setup(item => item.GetTemplatePolicyOptionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlValidationException("POLICY_REQUIRED", "Invalid."));
        var controller = Controller(service, "trace-template-problems");

        ((ObjectResult)await controller.Get(Guid.Empty, default)).StatusCode.Should().Be(404);
        ((ObjectResult)await controller.GetSummary(default)).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.GetWorkflowOptions(default)).StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.GetPolicyOptions(default);
        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should().Be("POLICY_REQUIRED");
    }

    private static ProcurementTenderDocumentTemplatesController Controller(
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
