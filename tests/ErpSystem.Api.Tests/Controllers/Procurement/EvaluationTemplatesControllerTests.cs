using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class EvaluationTemplatesControllerTests
{
    [Fact]
    public void TenderTemplateActionsUseTdcProcurementPermissionsInsteadOfLegacyGenericRoles()
    {
        var type = typeof(EvaluationTemplatesController);
        var readActions = new[]
        {
            nameof(EvaluationTemplatesController.GetAll),
            nameof(EvaluationTemplatesController.GetActive),
            nameof(EvaluationTemplatesController.GetForDropdown),
            nameof(EvaluationTemplatesController.GetById),
            nameof(EvaluationTemplatesController.GetByCategory),
            nameof(EvaluationTemplatesController.GetByTenderType),
            nameof(EvaluationTemplatesController.GetDefault)
        };
        var mutationActions = new[]
        {
            nameof(EvaluationTemplatesController.Create),
            nameof(EvaluationTemplatesController.Update),
            nameof(EvaluationTemplatesController.Delete),
            nameof(EvaluationTemplatesController.ValidateWeights)
        };

        readActions.Select(name => type.GetMethod(name)!
                .GetCustomAttribute<AuthorizeAttribute>()!)
            .Should().OnlyContain(attribute =>
                attribute.Policy == "procurement.records.read" &&
                string.IsNullOrWhiteSpace(attribute.Roles));
        mutationActions.Select(name => type.GetMethod(name)!
                .GetCustomAttribute<AuthorizeAttribute>()!)
            .Should().OnlyContain(attribute =>
                attribute.Policy == "procurement.tender.administer" &&
                string.IsNullOrWhiteSpace(attribute.Roles));
    }

    [Theory]
    [InlineData(typeof(EvaluationCriteriaController))]
    [InlineData(typeof(TenderDocumentTypesController))]
    [InlineData(typeof(TenderTemplatesController))]
    public void RelatedTenderConfigurationDoesNotUseLegacyGenericRoleGates(Type controllerType)
    {
        var actionAuthorization = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == controllerType)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())
            .ToList();

        actionAuthorization.Should().NotBeEmpty();
        Assert.All(actionAuthorization, attribute =>
        {
            attribute.Roles.Should().BeNullOrWhiteSpace();
            attribute.Policy.Should().BeOneOf(
                "procurement.records.read",
                "procurement.tender.administer");
        });
    }

    [Fact]
    public async Task DropdownForwardsSourceCategoryAndTenderTypeFilters()
    {
        var service = new Mock<IEvaluationTemplateService>();
        var expected = new[]
        {
            new EvaluationTemplateListItemDto
            {
                Id = Guid.NewGuid(),
                TemplateName = "Standard Tender Evaluation",
                TemplateCode = "EVAL-TENDER-001",
                Category = "Goods",
                TenderType = "ITB"
            }
        };
        service.Setup(candidate => candidate.GetActiveForDropdownAsync("Goods", "ITB"))
            .ReturnsAsync(expected);
        var controller = new EvaluationTemplatesController(
            service.Object,
            NullLogger<EvaluationTemplatesController>.Instance);

        var result = await controller.GetForDropdown("Goods", "ITB");

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
        service.Verify(candidate => candidate.GetActiveForDropdownAsync("Goods", "ITB"), Times.Once);
    }
}
