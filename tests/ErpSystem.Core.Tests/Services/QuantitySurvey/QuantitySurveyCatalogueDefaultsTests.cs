using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyCatalogueDefaultsTests
{
    [Fact]
    public void QuantitySurveyCatalogueFamiliesAreRegisteredWithoutUnlicensedBundledItems()
    {
        var catalogues = ProjectCatalogDefaults.GetRecommendedCatalogs()
            .Where(group => ProjectCatalogDefaults.IsQuantitySurveyCatalogType(group.Key))
            .ToList();

        catalogues.Select(group => group.Key).Should().BeEquivalentTo([
            ProjectCatalogDefaults.QuantitySurveySections,
            ProjectCatalogDefaults.QuantitySurveyTrades,
            ProjectCatalogDefaults.QuantitySurveyCostCodes,
            ProjectCatalogDefaults.QuantitySurveyMeasurementCodes
        ]);
        catalogues.Should().OnlyContain(group => group.Items.Count == 0);
    }

    [Theory]
    [InlineData("QS-SECTIONS")]
    [InlineData(" qs-trades ")]
    [InlineData("qs-cost-codes")]
    [InlineData("qs-measurement-codes")]
    public void QuantitySurveyCatalogueRecognitionIsNormalized(string catalogType)
    {
        ProjectCatalogDefaults.IsQuantitySurveyCatalogType(catalogType).Should().BeTrue();
    }

    [Fact]
    public async Task QuantitySurveyCatalogueQueryAppliesTenantRepositoryEffectiveAndStandardFilters()
    {
        var now = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var repository = new Mock<IProjectCatalogRepository>();
        repository
            .Setup(value => value.GetByCatalogTypeAsync(ProjectCatalogDefaults.QuantitySurveyMeasurementCodes))
            .ReturnsAsync([
                Entry("A1", "Excavation", "Cesmm4", now.AddDays(-10), null, true),
                Entry("A2", "Expired excavation", "Cesmm4", now.AddDays(-20), now.AddDays(-1), true),
                Entry("B1", "Inactive excavation", "Cesmm4", now.AddDays(-10), null, false),
                Entry("C1", "Concrete", "Smm7", now.AddDays(-10), null, true)
            ]);
        var service = CreateService(repository);

        var result = (await service.GetQuantitySurveyCatalogEntriesAsync(
            ProjectCatalogDefaults.QuantitySurveyMeasurementCodes,
            "excavation",
            "Cesmm4",
            now,
            includeInactive: false)).ToList();

        result.Should().ContainSingle().Which.Code.Should().Be("A1");
    }

    [Fact]
    public async Task QuantitySurveyCatalogueCreationRequiresAnEffectiveFromDate()
    {
        var service = CreateService(new Mock<IProjectCatalogRepository>());

        var action = () => service.CreateQuantitySurveyCatalogEntryAsync(new CreateProjectCatalogEntryDto
        {
            CatalogType = ProjectCatalogDefaults.QuantitySurveySections,
            Code = "SECTION-1",
            Name = "Section 1"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*effective-from date is required*");
    }

    [Fact]
    public async Task GenericProjectCatalogueApiCannotBeUsedForQuantitySurveyCatalogues()
    {
        var service = CreateService(new Mock<IProjectCatalogRepository>());

        var action = () => service.GetCatalogEntriesAsync(ProjectCatalogDefaults.QuantitySurveySections);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*governed quantity-survey catalogue API*");
    }

    private static ProjectSetupService CreateService(Mock<IProjectCatalogRepository> repository)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
        currentUser.SetupGet(value => value.IsExternalUser).Returns(false);
        currentUser.SetupGet(value => value.TenantId).Returns(Guid.NewGuid());
        currentUser.Setup(value => value.HasRole(It.IsAny<string>())).Returns(true);

        return new ProjectSetupService(
            Mock.Of<IProjectTypeRepository>(),
            Mock.Of<IProjectPriorityRepository>(),
            Mock.Of<IProjectTemplateRepository>(),
            Mock.Of<IProjectPortfolioRepository>(),
            Mock.Of<IProjectProgramRepository>(),
            repository.Object,
            currentUser.Object,
            Mock.Of<IUnitOfWork>());
    }

    private static ProjectCatalogEntry Entry(
        string code,
        string name,
        string standard,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        bool active) => new()
    {
        CatalogType = ProjectCatalogDefaults.QuantitySurveyMeasurementCodes,
        Code = code,
        Name = name,
        StandardCode = standard,
        EffectiveFrom = effectiveFrom,
        EffectiveTo = effectiveTo,
        IsActive = active
    };
}
