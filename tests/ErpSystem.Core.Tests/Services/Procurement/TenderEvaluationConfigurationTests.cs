using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderEvaluationConfigurationTests
{
    [Theory]
    [InlineData("WeightedAverage", false)]
    [InlineData("SimpleAverage", false)]
    [InlineData("PassFail", false)]
    [InlineData("QCBS", true)]
    public void CompatibleMethodsRemainAvailable(string method, bool qcbs)
    {
        var template = Template(method);
        var action = () => TenderEvaluationConfiguration.Validate(template, qcbs, 70, 30, 80);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("QCBS", false)]
    [InlineData("WeightedAverage", true)]
    [InlineData("SimpleAverage", true)]
    [InlineData("PassFail", true)]
    public void IncompatibleMethodsFailWithActionableCode(string method, bool qcbs)
    {
        var action = () => TenderEvaluationConfiguration.Validate(Template(method), qcbs, 70, 30, 80);
        action.Should().Throw<TenderEvaluationConfigurationException>()
            .Which.Code.Should().Be("TENDER_EVALUATION_METHOD_MISMATCH");
    }

    [Theory]
    [InlineData(60, 40, 80)]
    [InlineData(70, 30, 75)]
    public void QcbsSettingsCannotDrift(decimal technical, decimal financial, decimal threshold)
    {
        var action = () => TenderEvaluationConfiguration.Validate(Template("QCBS"), true, technical, financial, threshold);
        action.Should().Throw<TenderEvaluationConfigurationException>()
            .Which.Code.Should().Be("TENDER_EVALUATION_WEIGHTS_MISMATCH");
    }

    [Fact]
    public void UnknownMethodIsNotTreatedAsPassFail()
    {
        var action = () => TenderEvaluationConfiguration.Validate(Template("Unknown"), false, 70, 30, 80);
        action.Should().Throw<TenderEvaluationConfigurationException>()
            .Which.Code.Should().Be("TENDER_EVALUATION_METHOD_UNSUPPORTED");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    [InlineData("inactive")]
    public async Task UnavailableTemplateFailsClosed(string state)
    {
        var template = Template("WeightedAverage");
        var tenant = template.TenantId;
        template.TenantId = state == "foreign" ? Guid.NewGuid() : tenant;
        template.IsDeleted = state == "deleted";
        template.IsActive = state != "inactive";
        var repository = new Mock<IGenericRepository<EvaluationTemplate>>();
        repository.Setup(item => item.GetByIdAsync(template.Id)).ReturnsAsync(state == "missing" ? null : template);
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(item => item.Repository<EvaluationTemplate>()).Returns(repository.Object);
        var action = () => TenderEvaluationConfiguration.ValidateAsync(unit.Object, tenant, template.Id, false, 70, 30, 80);
        (await action.Should().ThrowAsync<TenderEvaluationConfigurationException>())
            .Which.Code.Should().Be("TENDER_EVALUATION_TEMPLATE_UNAVAILABLE");
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Approved")]
    [InlineData("Published")]
    [InlineData("Closed")]
    [InlineData("Awarded")]
    public async Task TemplateUsedOutsideDraftCannotBeRewritten(string status)
    {
        var template = Template("WeightedAverage");
        var templates = new Mock<IEvaluationTemplateRepository>();
        templates.Setup(item => item.GetByIdAsync(template.Id)).ReturnsAsync(template);
        var tenders = new Mock<IGenericRepository<Tender>>();
        var tender = new Tender { TenantId = template.TenantId, EvaluationTemplateId = template.Id, Status = status };
        tenders.Setup(item => item.FindAsync(It.IsAny<Expression<Func<Tender, bool>>>()))
            .ReturnsAsync((Expression<Func<Tender, bool>> predicate) => new[] { tender }.Where(predicate.Compile()));
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(item => item.Repository<Tender>()).Returns(tenders.Object);
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(item => item.TenantId).Returns(template.TenantId);
        var criteria = new Mock<IEvaluationTemplateCriterionRepository>();
        var service = new EvaluationTemplateService(templates.Object, criteria.Object,
            Mock.Of<IEvaluationCriterionRepository>(), unit.Object, current.Object, NullLogger<EvaluationTemplateService>.Instance);

        var action = () => service.UpdateAsync(template.Id, new UpdateEvaluationTemplateDto { ScoringMethod = "QCBS" });
        (await action.Should().ThrowAsync<TenderEvaluationConfigurationException>())
            .Which.Code.Should().Be("EVALUATION_TEMPLATE_IN_USE");
        template.ScoringMethod.Should().Be("WeightedAverage");
        criteria.Verify(item => item.DeleteByTemplateIdAsync(It.IsAny<Guid>()), Times.Never);
        unit.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static EvaluationTemplate Template(string method) => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), ScoringMethod = method,
        TechnicalWeight = 70, FinancialWeight = 30, MinimumTechnicalScore = 80
    };
}
