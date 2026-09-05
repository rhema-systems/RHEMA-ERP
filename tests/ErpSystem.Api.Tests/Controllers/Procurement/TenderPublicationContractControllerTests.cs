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
