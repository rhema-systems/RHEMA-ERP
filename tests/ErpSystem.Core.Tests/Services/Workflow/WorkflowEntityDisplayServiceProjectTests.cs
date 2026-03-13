using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowEntityDisplayServiceProjectTests
{
    [Fact]
    public async Task GetEntityDisplayInfoAsync_ForProject_ShouldReturnProjectDisplayMetadata()
    {
        var projectId = Guid.NewGuid();
        var projectRepository = new Mock<IProjectRepository>();
        projectRepository
            .Setup(x => x.GetByIdAsync(projectId))
            .ReturnsAsync(new Project
            {
                Id = projectId,
                ProjectCode = "PRJ-2026-0001",
                Title = "ERP Rollout"
            });

        var service = new WorkflowEntityDisplayService(
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IPurchaseRequisitionRepository>(),
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<ITenderRepository>(),
            projectRepository.Object,
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<IJobCardRepository>(),
            Mock.Of<IInventoryTransferRepository>(),
            Mock.Of<IInventoryRequisitionRepository>(),
            NullLogger<WorkflowEntityDisplayService>.Instance);

        var result = await service.GetEntityDisplayInfoAsync("Project", projectId);

        result.EntityType.Should().Be("Project");
        result.EntityId.Should().Be(projectId);
        result.EntityNumber.Should().Be("PRJ-2026-0001");
        result.EntityName.Should().Be("ERP Rollout");
        result.ActionUrl.Should().Be($"/development/projects/{projectId}");
    }
}
