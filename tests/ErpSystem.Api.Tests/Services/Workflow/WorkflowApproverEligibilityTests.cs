using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Services;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Workflow;

public sealed class WorkflowApproverEligibilityTests
{
    [Fact]
    public async Task WorkflowStepRouteLookup_OrdersByMappedOrderColumn()
    {
        await using var db = CreateDbContext();
        var definitionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        db.WorkflowSteps.AddRange(
            new WorkflowStep
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WorkflowDefinitionId = definitionId,
                Name = "Final", StepType = WorkflowStepType.Approval, Order = 2
            },
            new WorkflowStep
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WorkflowDefinitionId = definitionId,
                Name = "First", StepType = WorkflowStepType.Approval, Order = 1
            });
        await db.SaveChangesAsync();

        var repository = new WorkflowStepRepository(db);
        var steps = (await repository.GetByWorkflowDefinitionAsync(definitionId)).ToList();

        Assert.Equal(["First", "Final"], steps.Select(step => step.Name));
    }

    [Fact]
    public async Task MandatoryRoleStep_RejectsInitiatorOnlyAndOtherTenantActors()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var otherTenantControllerId = Guid.NewGuid();
        var role = CreateRole("Financial Controller");

        db.Roles.Add(role);
        db.Users.AddRange(
            CreateUser(initiatorId, tenantId, "tenant.controller"),
            CreateUser(otherTenantControllerId, otherTenantId, "other.controller"));
        db.UserRoles.AddRange(
            new ApplicationUserRole { UserId = initiatorId, RoleId = role.Id },
            new ApplicationUserRole { UserId = otherTenantControllerId, RoleId = role.Id });
        db.UserTenants.AddRange(
            CreateMembership(initiatorId, tenantId),
            CreateMembership(otherTenantControllerId, otherTenantId));
        await db.SaveChangesAsync();

        var service = new WorkflowRuntimeGovernanceService(db);
        var step = CreateRoleApprovalStep("Financial Controller Final Approval", role.Name!, preventInitiator: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnsureMandatoryApprovalActorsAvailableAsync(
                tenantId,
                initiatorId,
                "Journal Entry Approval",
                [step]));

        Assert.Contains("Financial Controller Final Approval", exception.Message);
        Assert.Contains("no independent active user", exception.Message);
    }

    [Fact]
    public async Task MandatoryRoleStep_AllowsIndependentActiveTenantActor()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var independentApproverId = Guid.NewGuid();
        var role = CreateRole("Financial Controller");

        db.Roles.Add(role);
        db.Users.AddRange(
            CreateUser(initiatorId, tenantId, "tenant.controller"),
            CreateUser(independentApproverId, tenantId, "independent.controller"));
        db.UserRoles.AddRange(
            new ApplicationUserRole { UserId = initiatorId, RoleId = role.Id },
            new ApplicationUserRole { UserId = independentApproverId, RoleId = role.Id });
        db.UserTenants.AddRange(
            CreateMembership(initiatorId, tenantId),
            CreateMembership(independentApproverId, tenantId));
        await db.SaveChangesAsync();

        var service = new WorkflowRuntimeGovernanceService(db);
        var step = CreateRoleApprovalStep("Financial Controller Final Approval", role.Name!, preventInitiator: true);

        await service.EnsureMandatoryApprovalActorsAvailableAsync(
            tenantId,
            initiatorId,
            "Journal Entry Approval",
            [step]);
    }

    private static WorkflowStep CreateRoleApprovalStep(
        string name,
        string role,
        bool preventInitiator)
    {
        var configuration = new WorkflowStepConfigurationDto
        {
            ApprovalConfig = new WorkflowApprovalConfigDto
            {
                ApprovalType = WorkflowApprovalType.Single,
                MinApprovalsRequired = 1,
                PreventInitiatorApproval = preventInitiator,
                ApproverRules =
                [
                    new WorkflowAssignmentRuleDto
                    {
                        ApprovalGroup = 1,
                        AssignmentType = WorkflowAssignmentType.Role,
                        Role = role
                    }
                ]
            }
        };

        return new WorkflowStep
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            WorkflowDefinitionId = Guid.NewGuid(),
            Name = name,
            StepType = WorkflowStepType.Approval,
            IsRequired = true,
            Configuration = JsonSerializer.Serialize(configuration, WorkflowJsonOptions)
        };
    }

    private static readonly JsonSerializerOptions WorkflowJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static ApplicationRole CreateRole(string name) => new(name)
    {
        Id = Guid.NewGuid(),
        NormalizedName = name.ToUpperInvariant(),
        IsSystemRole = true
    };

    private static ApplicationUser CreateUser(Guid id, Guid tenantId, string userName) => new()
    {
        Id = id,
        TenantId = tenantId,
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        FirstName = "UAT",
        LastName = "User",
        IsActive = true,
        SecurityStamp = Guid.NewGuid().ToString("N")
    };

    private static UserTenant CreateMembership(Guid userId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TenantId = tenantId,
        Status = UserTenantStatus.Active,
        GrantedAt = DateTime.UtcNow
    };

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"workflow-approver-eligibility-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
