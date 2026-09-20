using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderEvaluationConfigurationControllerTests
{
    private static TenderEvaluationConfigurationException Error() =>
        new("TENDER_EVALUATION_METHOD_MISMATCH", "Align the tender and template before scoring.");

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("submit")]
    [InlineData("approve")]
    [InlineData("publish")]
    public async Task TenderBoundariesReturnStructured422(string stage)
    {
        var id = Guid.NewGuid();
        var service = new Mock<ITenderService>();
        service.Setup(item => item.CreateTenderAsync(It.IsAny<CreateTenderDto>())).ThrowsAsync(Error());
        service.Setup(item => item.UpdateTenderAsync(id, It.IsAny<UpdateTenderDto>())).ThrowsAsync(Error());
        service.Setup(item => item.SubmitTenderForApprovalAsync(id, It.IsAny<Guid>())).ThrowsAsync(Error());
        service.Setup(item => item.ApproveTenderAsync(id, It.IsAny<Guid>(), It.IsAny<string>())).ThrowsAsync(Error());
        service.Setup(item => item.PublishTenderAsync(id, It.IsAny<PublishTenderDto>())).ThrowsAsync(Error());
        var controller = new TendersController(service.Object, Mock.Of<IWorkflowService>(),
            Mock.Of<ICurrentUserProvider>(), Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(), NullLogger<TendersController>.Instance);
        IActionResult? result = stage switch
        {
            "create" => (await controller.CreateTender(new CreateTenderDto())).Result,
            "update" => (await controller.UpdateTender(id, new UpdateTenderDto())).Result,
            "submit" => await controller.SubmitTender(id),
            "approve" => await controller.ApproveTender(id, null),
            _ => (await controller.PublishTender(id, new PublishTenderDto())).Result
        };
        AssertProblem(result);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("submit")]
    [InlineData("qcbs")]
    public async Task EvaluationBoundariesReturnStructured422(string stage)
    {
        var id = Guid.NewGuid();
        var service = new Mock<ITenderEvaluationService>();
        service.Setup(item => item.CreateEvaluationAsync(It.IsAny<CreateEvaluationDto>())).ThrowsAsync(Error());
        service.Setup(item => item.UpdateEvaluationAsync(id, It.IsAny<UpdateEvaluationDto>())).ThrowsAsync(Error());
        service.Setup(item => item.SubmitEvaluationAsync(id, It.IsAny<SubmitEvaluationDto>())).ThrowsAsync(Error());
        service.Setup(item => item.CalculateQCBSScoresAsync(id)).ThrowsAsync(Error());
        var controller = new TenderEvaluationsController(service.Object, NullLogger<TenderEvaluationsController>.Instance);
        IActionResult? result = stage switch
        {
            "create" => (await controller.CreateEvaluation(new CreateEvaluationDto())).Result,
            "update" => (await controller.UpdateEvaluation(id, new UpdateEvaluationDto())).Result,
            "submit" => (await controller.SubmitEvaluation(id, new SubmitEvaluationDto())).Result,
            _ => (await controller.CalculateQCBSScores(id)).Result
        };
        AssertProblem(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InUseTemplateReturns422Not404(bool delete)
    {
        var id = Guid.NewGuid();
        var service = new Mock<IEvaluationTemplateService>();
        service.Setup(item => item.UpdateAsync(id, It.IsAny<UpdateEvaluationTemplateDto>())).ThrowsAsync(Error());
        service.Setup(item => item.DeleteAsync(id)).ThrowsAsync(Error());
        var controller = new EvaluationTemplatesController(service.Object, NullLogger<EvaluationTemplatesController>.Instance);
        AssertProblem(delete ? await controller.Delete(id) : (await controller.Update(id, new UpdateEvaluationTemplateDto())).Result);
    }

    private static void AssertProblem(IActionResult? result)
    {
        var response = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(422);
        problem.Detail.Should().Be(Error().Message);
        problem.Extensions["code"].Should().Be(Error().Code);
    }
}
