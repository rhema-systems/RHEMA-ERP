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

public sealed class TenderControlsControllerTests
{
    [Fact]
    public async Task GetReturnsTenantScopedNctLifecycle()
    {
        var tenderId = Guid.NewGuid();
        var expected = new ProcurementTenderControlDto
        {
            TenderId = tenderId,
            TenderNumber = "NCT-2026-001",
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            MethodRuleCode = "NCT-GOODS",
            AuthorityRouteReference = "ARR-001",
            Status = ProcurementTenderControlStatus.Advertised
        };
        var fixture = new Fixture();
        fixture.Service.Setup(item => item.GetAsync(tenderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Controller.Get(tenderId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    [InlineData("invalid", 400)]
    [InlineData("forbidden", 403)]
    public async Task GetMapsGovernedFailures(string failure, int expectedStatus)
    {
        var fixture = new Fixture();
        fixture.Service.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "missing" => new ProcurementTenderControlNotFoundException("TENDER_NOT_FOUND", "Missing."),
                "conflict" => new ProcurementTenderControlConflictException("TENDER_STATE", "Conflict."),
                "invalid" => new ProcurementTenderControlValidationException("TENDER_INVALID", "Invalid."),
                _ => new ProcurementTenderControlAuthorizationException("Forbidden.")
            });

        var result = await fixture.Controller.Get(Guid.NewGuid(), CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
    }

    [Fact]
    public async Task OpeningForwardsCorrelationAndSignedRegister()
    {
        var fixture = new Fixture();
        var tenderId = Guid.NewGuid();
        var request = new CompleteProcurementTenderOpeningRequest
        {
            EvidenceReference = "opening-evidence",
            Participants =
            [
                new() { Name = "Officer", Role = "Opening officer", SignatureReference = "sig-1" },
                new() { Name = "Observer", Role = "Observer", IsObserver = true, SignatureReference = "sig-2" }
            ]
        };
        fixture.Service.Setup(item => item.CompleteOpeningAsync(
                tenderId, request, "corr-0204", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementTenderControlDto { TenderId = tenderId });

        var result = await fixture.Controller.CompleteOpening(tenderId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public void MutationRoutesHaveExplicitRoleBoundaries()
    {
        var type = typeof(TenderControlsController);
        var technical = type.GetMethod(nameof(TenderControlsController.SaveTechnical))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
        var decision = type.GetMethod(nameof(TenderControlsController.DecideApproval))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!;

        Assert.Contains("Employee", technical);
        Assert.DoesNotContain("Employee", decision);
        Assert.Contains("Manager", decision);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new TenderControlsController(Service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0204";
        }

        public Mock<IProcurementTenderControlService> Service { get; } = new();
        public TenderControlsController Controller { get; }
    }
}
