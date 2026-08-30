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

public sealed class PrequalificationControllerTests
{
    [Fact]
    public async Task ListAndEligibilityReturnTenantScopedServiceResults()
    {
        var fixture = new Fixture();
        var summary = new ProcurementPrequalificationSummaryDto
        {
            Id = Guid.NewGuid(), Reference = "PQ-2026-001",
            Status = ProcurementPrequalificationStatus.Advertised
        };
        fixture.Service.Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([summary]);
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        fixture.Service.Setup(service => service.CheckEligibilityAsync(
                supplierId, categoryId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierEligibilityDto
            {
                BusinessPartnerId = supplierId, CategoryId = categoryId,
                Eligible = false, Code = "NOT_PREQUALIFIED"
            });

        var list = Assert.IsType<OkObjectResult>(
            await fixture.Controller.List(CancellationToken.None));
        var eligibility = Assert.IsType<OkObjectResult>(
            await fixture.Controller.CheckEligibility(
                supplierId, categoryId, null, CancellationToken.None));

        Assert.Same(summary,
            Assert.IsAssignableFrom<IReadOnlyList<ProcurementPrequalificationSummaryDto>>(list.Value).Single());
        Assert.Equal("NOT_PREQUALIFIED",
            Assert.IsType<ProcurementSupplierEligibilityDto>(eligibility.Value).Code);
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("conflict", 409)]
    [InlineData("invalid", 422)]
    [InlineData("forbidden", 403)]
    public async Task GetMapsStructuredLifecycleFailures(string failure, int expectedStatus)
    {
        var fixture = new Fixture();
        fixture.Service.Setup(service => service.GetAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure switch
            {
                "missing" => new ProcurementPrequalificationNotFoundException("PREQUAL_NOT_FOUND", "Missing."),
                "conflict" => new ProcurementPrequalificationConflictException("PREQUAL_STATE", "Conflict."),
                "invalid" => new ProcurementPrequalificationValidationException("PREQUAL_INVALID", "Invalid."),
                _ => new ProcurementPrequalificationAuthorizationException("Forbidden.")
            });

        var result = await fixture.Controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task EvaluationForwardsCorrelationAndImmutableScorecard()
    {
        var fixture = new Fixture();
        var exerciseId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var request = new EvaluateProcurementPrequalificationApplicationRequest
        {
            Remarks = "Verified.", RecommendationEvidenceReference = "evidence://recommendation",
            RowVersion = "AQ==",
            Scores =
            [
                new ProcurementPrequalificationCriterionScoreRequest
                {
                    CriterionId = Guid.NewGuid(), Score = 80, MeetsRequirement = true,
                    Reason = "Meets the criterion.", EvidenceReference = "evidence://score"
                }
            ]
        };
        fixture.Service.Setup(service => service.EvaluateAsync(
                exerciseId, applicationId, request, "corr-0206", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementPrequalificationApplicationDto { Id = applicationId });

        var result = await fixture.Controller.Evaluate(
            exerciseId, applicationId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        fixture.Service.VerifyAll();
    }

    [Fact]
    public void ApprovalAndSubmissionRoutesUseRegisteredProcurementPermissions()
    {
        var type = typeof(PrequalificationController);
        var controllerAuthorization = type.GetCustomAttribute<AuthorizeAttribute>();
        var evaluatePolicy = type.GetMethod(nameof(PrequalificationController.Evaluate))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy;
        var decidePolicy = type.GetMethod(nameof(PrequalificationController.Decide))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy;
        var supplierSubmission = type.GetMethod(nameof(PrequalificationController.SubmitApplication))!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(controllerAuthorization);
        Assert.Equal("procurement.tender.evaluate", evaluatePolicy);
        Assert.Equal("procurement.sourcing.approve", decidePolicy);
        Assert.Null(supplierSubmission);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Controller = new PrequalificationController(Service.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            Controller.Request.Headers["X-Correlation-ID"] = "corr-0206";
        }

        public Mock<IProcurementPrequalificationService> Service { get; } = new();
        public PrequalificationController Controller { get; }
    }
}
