using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ExceptionalSourcingControlsControllerTests
{
    [Fact]
    public async Task GetReturnsTenantScopedExceptionalHistory()
    {
        var tenderId = Guid.NewGuid();
        var expected = new ProcurementExceptionalSourcingControlDto
        {
            TenderId = tenderId, TenderNumber = "SS-2026-001",
            Method = ProcurementMethodType.SingleSource,
            Status = ProcurementExceptionalSourcingControlStatus.Prepared
        };
        var fixture = new Fixture();
        fixture.Service.Setup(item => item.GetAsync(tenderId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await fixture.Controller.Get(tenderId, CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    [InlineData("invalid", 422)]
    [InlineData("forbidden", 403)]
    public async Task GetMapsStructuredGovernedFailures(string failure, int expectedStatus)
    {
        var fixture = new Fixture();
        fixture.Service.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "missing" => new ProcurementExceptionalSourcingNotFoundException("EXCEPTIONAL_NOT_FOUND", "Missing."),
                "conflict" => new ProcurementExceptionalSourcingConflictException("EXCEPTIONAL_STATE", "Conflict."),
                "invalid" => new ProcurementExceptionalSourcingValidationException("EXCEPTIONAL_INVALID", "Invalid."),
                _ => new ProcurementExceptionalSourcingAuthorizationException("Forbidden.")
            });

        var result = await fixture.Controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task FilingForwardsCorrelationAndImmutableReferences()
    {
        var fixture = new Fixture();
        var tenderId = Guid.NewGuid();
        var request = new RecordProcurementPostAwardFilingRequest
        {
            FilingReference = "PPA-FILE-001", FilingEvidenceReference = "evidence://filing",
            ExceptionReportReference = "EXR-001", ExceptionReportEvidenceReference = "evidence://report",
            RowVersion = "AQ=="
        };
        fixture.Service.Setup(item => item.RecordPostAwardFilingAsync(
                tenderId, request, "corr-0205", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementExceptionalSourcingControlDto { TenderId = tenderId });

        var result = await fixture.Controller.RecordPostAwardFiling(tenderId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public void MutationRoutesHaveExplicitRoleBoundaries()
    {
        var type = typeof(ExceptionalSourcingControlsController);
        var recommendation = type.GetMethod(nameof(ExceptionalSourcingControlsController.RecordRecommendation))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
        var decision = type.GetMethod(nameof(ExceptionalSourcingControlsController.DecideApproval))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;

        Assert.Contains("Employee", recommendation);
        Assert.DoesNotContain("Employee", decision);
        Assert.Contains("Manager", decision);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new ExceptionalSourcingControlsController(Service.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0205";
        }

        public Mock<IProcurementExceptionalSourcingControlService> Service { get; } = new();
        public ExceptionalSourcingControlsController Controller { get; }
    }
}
