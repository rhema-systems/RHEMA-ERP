using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Moq;
using System.Linq.Expressions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class ProjectPreparationLookupAccessTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task Internal_preparer_can_read_setup_but_cannot_change_it(bool authenticated, bool external, bool allowed)
    {
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(x => x.IsAuthenticated).Returns(authenticated);
        user.SetupGet(x => x.IsExternalUser).Returns(external);
        var types = new Mock<IProjectTypeRepository>();
        types.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ProjectType>());
        var priorities = new Mock<IProjectPriorityRepository>();
        priorities.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ProjectPriority>());
        var templates = new Mock<IProjectTemplateRepository>();
        templates.Setup(x => x.GetActiveAsync()).ReturnsAsync(new List<ProjectTemplate>());
        var portfolios = new Mock<IProjectPortfolioRepository>();
        portfolios.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ProjectPortfolio>());
        var programs = new Mock<IProjectProgramRepository>();
        programs.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ProjectProgram>());
        var catalogs = new Mock<IProjectCatalogRepository>();
        catalogs.Setup(x => x.GetByCatalogTypeAsync("methodologies")).ReturnsAsync(new List<ProjectCatalogEntry>());
        var unitOfWork = new Mock<IUnitOfWork>();
        var units = new Mock<IGenericRepository<ProjectUnitTypeTemplate>>();
        units.Setup(x => x.FindAsync(It.IsAny<Expression<Func<ProjectUnitTypeTemplate, bool>>>()))
            .ReturnsAsync(new List<ProjectUnitTypeTemplate>());
        unitOfWork.Setup(x => x.Repository<ProjectUnitTypeTemplate>()).Returns(units.Object);
        var service = new ProjectSetupService(types.Object, priorities.Object, templates.Object,
            portfolios.Object, programs.Object, catalogs.Object, user.Object, unitOfWork.Object);
        Func<Task>[] reads = [
            async () => { await service.GetProjectTypesAsync(); },
            async () => { await service.GetProjectPrioritiesAsync(); },
            async () => { await service.GetProjectTemplatesAsync(); },
            async () => { await service.GetPortfoliosAsync(); },
            async () => { await service.GetProgramsAsync(); },
            async () => { await service.GetCatalogEntriesAsync("methodologies"); },
            async () => { await service.GetProjectUnitTypeTemplatesAsync(); }
        ];
        foreach (var read in reads)
        {
            if (allowed) await read.Should().NotThrowAsync();
            else await read.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        var write = () => service.CreateProjectTypeAsync(new CreateProjectTypeDto { Code = "TEST", Name = "Test" });
        await write.Should().ThrowAsync<UnauthorizedAccessException>();
        var unitWrite = () => service.CreateProjectUnitTypeTemplateAsync(new CreateProjectUnitTypeTemplateDto());
        await unitWrite.Should().ThrowAsync<UnauthorizedAccessException>();
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never,
            "preparer lookups must not seed or change setup");
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task Settings_read_and_write_have_separate_access(bool authenticated, bool external, bool allowed)
    {
        var user = new Mock<ICurrentUserProvider>();
        user.SetupGet(x => x.IsAuthenticated).Returns(authenticated);
        user.SetupGet(x => x.IsExternalUser).Returns(external);
        var repository = new Mock<IProjectManagementSettingsRepository>();
        repository.Setup(x => x.GetOrCreateDefaultAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new ProjectManagementSettings());
        var service = new ProjectManagementSettingsService(repository.Object, user.Object, Mock.Of<IUnitOfWork>());
        var read = () => service.GetSettingsAsync();
        if (allowed) await read.Should().NotThrowAsync();
        else await read.Should().ThrowAsync<UnauthorizedAccessException>();
        var write = () => service.UpdateSettingsAsync(new UpdateProjectManagementSettingsDto());
        await write.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
