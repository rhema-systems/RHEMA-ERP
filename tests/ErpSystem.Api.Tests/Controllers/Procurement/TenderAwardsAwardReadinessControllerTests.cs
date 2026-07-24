using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderAwardsAwardReadinessControllerTests
{
    [Fact]
    public void CreateAwardRequiresAuthenticationWithoutGenericRolePreemption()
    {
        var type = typeof(TenderAwardsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetMethod(nameof(TenderAwardsController.CreateAward))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().BeNullOrEmpty();
        type.GetMethod(nameof(TenderAwardsController.CreateAward))!
            .GetCustomAttribute<AllowAnonymousAttribute>()
            .Should().BeNull();
    }

    [Fact]
    public async Task CreateAwardForwardsCorrelationToAuthoritativeLegacyGate()
    {
        var fixture = new Fixture();
        var request = new CreateAwardDto
        {
            TenderId = Guid.NewGuid(),
            TenderBidId = Guid.NewGuid(),
            AwardedAmount = 500m
        };
        var expected = new TenderAwardDto { Id = Guid.NewGuid() };
        fixture.Service.Setup(service => service.CreateAwardAsync(
                request.TenderId,
                request,
                "corr-legacy-award",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Controller.CreateAward(request, default);

        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeSameAs(expected);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public async Task CreateAwardReturnsDecisionWhenReadinessHardStopBlocks()
    {
        var fixture = new Fixture();
        var request = new CreateAwardDto
        {
            TenderId = Guid.NewGuid(),
            TenderBidId = Guid.NewGuid(),
            AwardedAmount = 500m
        };
        var decision = new ProcurementAwardReadinessDto
        {
            Id = Guid.NewGuid(),
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = request.TenderId,
            Status = ProcurementAwardReadinessDecisionStatus.Blocked
        };
        fixture.Service.Setup(service => service.CreateAwardAsync(
                request.TenderId,
                request,
                "corr-legacy-award",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAwardReadinessBlockedException(
                "AWARD_READINESS_RECOMMENDATION_MISMATCH",
                "The selected bid contradicts the retained recommendation.",
                decision));

        var result = await fixture.Controller.CreateAward(request, default);

        var problem = result.Result.Should()
            .BeOfType<UnprocessableEntityObjectResult>().Subject;
        problem.Value.Should().NotBeNull();
        problem.Value!.GetType().GetProperty("code")!.GetValue(problem.Value)
            .Should().Be("AWARD_READINESS_RECOMMENDATION_MISMATCH");
        problem.Value.GetType().GetProperty("decision")!.GetValue(problem.Value)
            .Should().BeSameAs(decision);
    }

    [Theory]
    [InlineData("cross-tenant", 404, "AWARD_READINESS_SOURCE_NOT_FOUND")]
    [InlineData("evaluator-or-external", 403, "AWARD_READINESS_ACCESS_FORBIDDEN")]
    [InlineData("blocked", 422, "AWARD_READINESS_BLOCKED")]
    public async Task CreateAwardDirectRouteMapsReadinessHardStops(
        string failure,
        int expectedStatus,
        string expectedCode)
    {
        var fixture = new Fixture();
        var request = new CreateAwardDto
        {
            TenderId = Guid.NewGuid(),
            TenderBidId = Guid.NewGuid(),
            AwardedAmount = 500m
        };
        fixture.Service.Setup(service => service.CreateAwardAsync(
                request.TenderId,
                request,
                "corr-legacy-award",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "cross-tenant" => new ProcurementAwardReadinessNotFoundException(
                    "AWARD_READINESS_SOURCE_NOT_FOUND",
                    "The tender was not found in the current tenant."),
                "evaluator-or-external" => new ProcurementAwardReadinessAuthorizationException(
                    "An evaluator cannot approve the same award."),
                _ => new ProcurementAwardReadinessBlockedException(
                    "AWARD_READINESS_BLOCKED",
                    "Award readiness controls are not satisfied.",
                    new ProcurementAwardReadinessDto
                    {
                        SourceType = ProcurementAwardReadinessSourceType.Tender,
                        SourceId = request.TenderId
                    })
            });

        var response = await fixture.Controller.CreateAward(request, default);

        var result = response.Result.Should()
            .BeAssignableTo<ObjectResult>().Subject;
        result.StatusCode.Should().Be(expectedStatus);
        result.Value.Should().NotBeNull();
        result.Value!.GetType().GetProperty("status")!.GetValue(result.Value)
            .Should().Be(expectedStatus);
        result.Value.GetType().GetProperty("code")!.GetValue(result.Value)
            .Should().Be(expectedCode);
        fixture.Service.VerifyAll();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new TenderAwardsController(
                Service.Object,
                Mock.Of<ILogger<TenderAwardsController>>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        TraceIdentifier = "trace-legacy-award"
                    }
                }
            };
            Controller.Request.Headers["X-Correlation-ID"] =
                "corr-legacy-award";
        }

        public Mock<ITenderAwardService> Service { get; } = new();
        public TenderAwardsController Controller { get; }
    }
}
