using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderPublicationContractControllerTests
{
    [Theory]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(404)]
    [InlineData(403)]
    public async Task Publication_preserves_document_guard_problem_details(int status)
    {
        const string code = "TENDER_DOCUMENT_TEST_GUARD";
        const string detail = "Exact controlled document failure.";
        Exception exception = status switch
        {
            409 => new ProcurementTenderDocumentControlConflictException(code, detail),
            422 => new ProcurementTenderDocumentControlValidationException(code, detail),
            404 => new ProcurementTenderDocumentControlNotFoundException(code, detail),
            _ => new ProcurementTenderDocumentControlAuthorizationException(detail)
        };
        var service = new Mock<ITenderService>();
        service.Setup(item => item.PublishTenderAsync(It.IsAny<Guid>(), It.IsAny<PublishTenderDto>()))
            .ThrowsAsync(exception);
        var result = await Controller(service.Object).PublishTender(Guid.NewGuid(), new PublishTenderDto());
        var failure = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        failure.StatusCode.Should().Be(status);
        var problem = failure.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(status);
        problem.Detail.Should().Be(detail);
        problem.Extensions["code"].Should().Be(status == 403 ? "TENDER_DOCUMENT_FORBIDDEN" : code);
    }

    [Fact]
    public async Task Publication_preserves_the_structured_control_failure()
    {
        var service = new Mock<ITenderService>();
        service.Setup(item => item.PublishTenderAsync(It.IsAny<Guid>(), It.IsAny<PublishTenderDto>()))
            .ThrowsAsync(new ProcurementTenderControlValidationException("TENDER_ADVANCED_AUTHORITY_ROUTE_REQUIRED", "Exact authority route required."));
        var result = await Controller(service.Object).PublishTender(Guid.NewGuid(), new PublishTenderDto());
        var failure = result.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        var problem = failure.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("TENDER_ADVANCED_AUTHORITY_ROUTE_REQUIRED");
    }

    [Fact]
    public async Task Release_only_itb_read_does_not_invent_an_advanced_method_or_case()
    {
        var tenderId = Guid.NewGuid();
        var expected = new TenderDetailDto
        {
            Id = tenderId,
            TenderNumber = "TND-REL-001",
            TenderType = "ITB",
            Status = "Approved",
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = null,
            SourcingMethod = null
        };
        var service = new Mock<ITenderService>();
        service.Setup(item => item.GetTenderByIdAsync(tenderId)).ReturnsAsync(expected);

        var result = await Controller(service.Object).GetTender(tenderId);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actual = response.Value.Should().BeOfType<TenderDetailDto>().Subject;
        actual.SourcingReleaseId.Should().Be(expected.SourcingReleaseId);
        actual.SourcingCaseId.Should().BeNull();
        actual.SourcingMethod.Should().BeNull();
    }

    [Fact]
    public async Task Advanced_nct_read_retains_the_case_and_selected_method()
    {
        var tenderId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var service = new Mock<ITenderService>();
        service.Setup(item => item.GetTenderByIdAsync(tenderId)).ReturnsAsync(new TenderDetailDto
        {
            Id = tenderId,
            TenderNumber = "TND-NCT-001",
            TenderType = "ITB",
            Status = "Approved",
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = caseId,
            SourcingMethod = ProcurementMethodType.NationalCompetitiveTendering
        });

        var result = await Controller(service.Object).GetTender(tenderId);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actual = response.Value.Should().BeOfType<TenderDetailDto>().Subject;
        actual.SourcingCaseId.Should().Be(caseId);
        actual.SourcingMethod.Should().Be(ProcurementMethodType.NationalCompetitiveTendering);
    }

    private static TendersController Controller(ITenderService service) => new(
        service,
        Mock.Of<IWorkflowService>(),
        Mock.Of<ICurrentUserProvider>(),
        Mock.Of<IControlledFileUploadService>(),
        Mock.Of<ICentralDocumentRepositoryFileService>(),
        Mock.Of<ILogger<TendersController>>())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "corr-publication-contract"
            }
        }
    };
}
