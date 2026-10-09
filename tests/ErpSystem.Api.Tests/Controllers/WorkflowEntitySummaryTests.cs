using System.Security.Claims;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class WorkflowEntitySummaryTests
{
    [Fact]
    public async Task EntityTypeRepositoryPrefersExactCodeWhenLegacyAliasAlsoNormalizesToIt()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var canonical = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "SALES_ORDER",
            Name = "SalesOrder",
            IsActive = true
        };
        var legacy = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "SalesOrder",
            Name = "Sales Order",
            IsActive = true
        };
        db.WorkflowEntityTypes.AddRange(canonical, legacy);
        await db.SaveChangesAsync();

        var repository = new WorkflowEntityTypeRepository(db);

        var result = await repository.GetByNameAsync("SALES_ORDER", tenantId);

        Assert.NotNull(result);
        Assert.Equal(canonical.Id, result.Id);
    }

    [Fact]
    public async Task EntityTypeRepositoryPrefersCanonicalBusinessPartnerCodeOverLegacyExactName()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var canonical = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BusinessPartner",
            Name = "Business Partner",
            IsActive = true
        };
        var legacy = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BUSINESS_PARTNER",
            Name = "BusinessPartner",
            IsActive = true
        };
        db.WorkflowEntityTypes.AddRange(canonical, legacy);
        await db.SaveChangesAsync();

        var repository = new WorkflowEntityTypeRepository(db);

        var result = await repository.GetByNameAsync("BusinessPartner", tenantId);

        Assert.NotNull(result);
        Assert.Equal(canonical.Id, result.Id);
    }

    [Fact]
    public async Task GetEntitySummaryUsesConfiguredCodeForEveryWorkflowServiceLookup()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var entityTypeId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        const string requestedAlias = "SalesOrder";
        const string configuredCode = "SALES_ORDER";

        var entityTypes = new Mock<IWorkflowEntityTypeRepository>();
        entityTypes.Setup(repository => repository.GetByNameAsync(requestedAlias, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowEntityType
            {
                Id = entityTypeId,
                TenantId = tenantId,
                Code = configuredCode,
                Name = requestedAlias,
                IsActive = true
            });

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(repository => repository.GetByEntityAsync(entityTypeId, salesOrderId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WorkflowInstance
                {
                    Id = workflowInstanceId,
                    TenantId = tenantId,
                    EntityTypeId = entityTypeId,
                    EntityId = salesOrderId,
                    WorkflowDefinitionId = Guid.NewGuid(),
                    InitiatedById = actorId,
                    Status = WorkflowInstanceStatus.InProgress
                }
            });

        var workflowService = new Mock<IWorkflowService>();
        workflowService.Setup(service => service.HasActiveApprovalWorkflowAsync(configuredCode)).ReturnsAsync(true);
        workflowService.Setup(service => service.GetCurrentWorkflowStepAsync(configuredCode, salesOrderId))
            .ReturnsAsync((WorkflowStepInfo?)null);
        workflowService.Setup(service => service.CanUserApproveAsync(configuredCode, salesOrderId, actorId))
            .ReturnsAsync(false);

        var engine = new Mock<IWorkflowEngine>();
        engine.Setup(service => service.GetWorkflowStatusAsync(workflowInstanceId))
            .ReturnsAsync(new WorkflowStatusDto
            {
                WorkflowInstanceId = workflowInstanceId,
                WorkflowName = "Sales order approval",
                EntityId = salesOrderId,
                EntityType = configuredCode,
                Status = WorkflowInstanceStatus.InProgress,
                Progress = new WorkflowProgressDto()
            });

        var controller = CreateController(
            db,
            tenantId,
            actorId,
            engine.Object,
            workflowService.Object,
            instances.Object,
            entityTypes.Object);

        var result = await controller.GetEntitySummary(requestedAlias, salesOrderId);

        Assert.IsType<OkObjectResult>(result);
        workflowService.Verify(service => service.HasActiveApprovalWorkflowAsync(configuredCode), Times.Once);
        workflowService.Verify(service => service.GetCurrentWorkflowStepAsync(configuredCode, salesOrderId), Times.Once);
        workflowService.Verify(service => service.CanUserApproveAsync(configuredCode, salesOrderId, actorId), Times.Once);
        workflowService.Verify(service => service.GetCurrentWorkflowStepAsync(requestedAlias, salesOrderId), Times.Never);
        workflowService.Verify(service => service.CanUserApproveAsync(requestedAlias, salesOrderId, actorId), Times.Never);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"workflow-entity-summary-{Guid.NewGuid()}").Options);

    private static WorkflowController CreateController(
        ApplicationDbContext db,
        Guid tenantId,
        Guid actorId,
        IWorkflowEngine engine,
        IWorkflowService workflowService,
        IWorkflowInstanceRepository instanceRepository,
        IWorkflowEntityTypeRepository entityTypeRepository)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        currentUser.SetupGet(value => value.Roles).Returns(Array.Empty<string>());

        return new WorkflowController(
            engine, workflowService, Mock.Of<IWorkflowDefinitionService>(),
            Mock.Of<IWorkflowDefinitionRepository>(), Mock.Of<IWorkflowInstanceService>(),
            Mock.Of<IWorkflowStepService>(), Mock.Of<IWorkflowApprovalService>(),
            Mock.Of<IWorkflowConditionEvaluator>(), Mock.Of<IWorkflowNotificationService>(),
            instanceRepository, Mock.Of<IWorkflowStepInstanceRepository>(), Mock.Of<IWorkflowApprovalRepository>(),
            entityTypeRepository, Mock.Of<IWorkflowEntityTypeCatalogService>(), db,
            Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IAppEventBus>(), Mock.Of<IFileStorageService>(),
            currentUser.Object, Mock.Of<IProcurementRequisitionBudgetControlService>(),
            Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcedureCaseService>(),
            NullLogger<WorkflowController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "Test"))
                }
            }
        };
    }
}
