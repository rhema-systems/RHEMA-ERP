using ErpSystem.Api.Services;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Workflow;

public sealed class OptionalApprovalConfigurationTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IWorkflowEntityTypeRepository> _types = new();
    private readonly Mock<IWorkflowDefinitionRepository> _definitions = new();
    private readonly Mock<IWorkflowInstanceRepository> _instances = new();
    private readonly WorkflowEntityType _type;

    public OptionalApprovalConfigurationTests()
    {
        _user.SetupGet(x => x.TenantId).Returns(_tenant);
        _type = new WorkflowEntityType { Id = Guid.NewGuid(), TenantId = _tenant, Code = "STOCK_ADJUSTMENT", Name = "Stock Adjustment", IsActive = true };
        _types.Setup(x => x.GetByNameAsync("StockAdjustment", _tenant, default)).ReturnsAsync(_type);
        _definitions.Setup(x => x.GetActiveByEntityTypeAsync(_type.Id, default)).ReturnsAsync(Array.Empty<WorkflowDefinition>());
        _instances.Setup(x => x.GetByEntityAsync(_type.Id, It.IsAny<string>(), default)).ReturnsAsync(Array.Empty<WorkflowInstance>());
    }

    private SimpleWorkflowService Service() => new(
        null!, _types.Object, _definitions.Object, _instances.Object,
        null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
        _user.Object, null!, null!, NullLogger<SimpleWorkflowService>.Instance);

    [Theory]
    [InlineData(true, false, WorkflowDefinitionLifecycleStatus.Published, true)]
    [InlineData(false, false, WorkflowDefinitionLifecycleStatus.Published, false)]
    [InlineData(true, true, WorkflowDefinitionLifecycleStatus.Published, false)]
    [InlineData(true, false, WorkflowDefinitionLifecycleStatus.Draft, false)]
    [InlineData(true, false, WorkflowDefinitionLifecycleStatus.Retired, false)]
    public async Task Only_active_published_current_definitions_require_new_approval(bool active, bool deleted, WorkflowDefinitionLifecycleStatus status, bool expected)
    {
        _definitions.Setup(x => x.GetActiveByEntityTypeAsync(_type.Id, default)).ReturnsAsync(new[]
        {
            new WorkflowDefinition { TenantId = _tenant, EntityTypeId = _type.Id, IsActive = active, IsDeleted = deleted, LifecycleStatus = status }
        });
        (await Service().HasActiveApprovalWorkflowAsync("StockAdjustment")).Should().Be(expected);
    }

    [Fact]
    public async Task Another_tenants_definition_cannot_enable_current_tenant_approval()
    {
        _definitions.Setup(x => x.GetActiveByEntityTypeAsync(_type.Id, default)).ReturnsAsync(new[]
        {
            new WorkflowDefinition { TenantId = Guid.NewGuid(), EntityTypeId = _type.Id, IsActive = true, LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published }
        });
        (await Service().HasActiveApprovalWorkflowAsync("StockAdjustment")).Should().BeFalse();
    }

    [Fact]
    public async Task Disabled_entity_type_does_not_enable_new_approval()
    {
        _type.IsActive = false;
        (await Service().HasActiveApprovalWorkflowAsync("StockAdjustment")).Should().BeFalse();
        _definitions.Verify(x => x.GetActiveByEntityTypeAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Missing_tenant_is_an_error_not_a_direct_completion_permission()
    {
        _user.SetupGet(x => x.TenantId).Returns((Guid?)null);
        var act = () => Service().HasActiveApprovalWorkflowAsync("StockAdjustment");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Created, true)]
    [InlineData(WorkflowInstanceStatus.InProgress, true)]
    [InlineData(WorkflowInstanceStatus.Waiting, true)]
    [InlineData(WorkflowInstanceStatus.Suspended, true)]
    [InlineData(WorkflowInstanceStatus.Completed, false)]
    [InlineData(WorkflowInstanceStatus.Cancelled, false)]
    public async Task Inflight_instance_survives_deactivation(WorkflowInstanceStatus status, bool expected)
    {
        var id = Guid.NewGuid();
        _type.IsActive = false;
        _instances.Setup(x => x.GetByEntityAsync(_type.Id, id.ToString(), default)).ReturnsAsync(new[]
        {
            new WorkflowInstance { TenantId = _tenant, EntityTypeId = _type.Id, EntityId = id, Status = status }
        });
        (await Service().HasActiveApprovalInstanceAsync("StockAdjustment", id)).Should().Be(expected);
    }
}
