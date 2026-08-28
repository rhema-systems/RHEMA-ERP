using ErpSystem.Api.Services.Identity;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Identity;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Identity;

public sealed class HrIdentityReconciliationServiceTests
{
    [Fact]
    public async Task Access_service_blocks_inactive_or_cross_tenant_hr_links()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var employee = NewEmployee(tenantId, StaffStatus.Terminated, isActive: false);
        var user = NewUser(tenantId, employee.Id, true);
        db.Employees.Add(employee);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new HrIdentityAccessService(db);

        var inactive = await service.EvaluateAsync(user.Id);
        inactive.IsAllowed.Should().BeFalse();
        inactive.Code.Should().Be("HR_EMPLOYMENT_INACTIVE");

        employee.IsActive = true;
        employee.StaffStatus = StaffStatus.Active;
        employee.TenantId = otherTenantId;
        await db.SaveChangesAsync();
        var crossTenant = await service.EvaluateAsync(user.Id);
        crossTenant.IsAllowed.Should().BeFalse();
        crossTenant.Code.Should().Be("HR_IDENTITY_LINK_INVALID");
    }

    [Fact]
    public async Task Reconciliation_revokes_access_and_requires_review_before_reactivation()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        var employee = NewEmployee(tenant.Id, StaffStatus.Terminated, isActive: false);
        var user = NewUser(tenant.Id, employee.Id, true);
        var role = new ApplicationRole("Procurement Officer") { Id = Guid.NewGuid() };
        db.AddRange(tenant, employee, user, role);
        db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        var relationship = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Status = UserTenantStatus.Active,
            IsDefault = true
        };
        db.UserTenants.Add(relationship);
        db.UserSessions.Add(new UserSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            UserId = user.Id,
            IpAddress = "127.0.0.1",
            UserAgent = "test",
            DeviceFingerprint = "test",
            DeviceType = "Desktop",
            Browser = "Test",
            OperatingSystem = "Test",
            Location = "Test",
            TerminationReason = string.Empty,
            JwtTokenId = "test-jti",
            IsActive = true
        });
        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        db.ProcurementResponsibilityAssignments.Add(new ProcurementResponsibilityAssignment
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            RoleId = role.Id,
            RoleName = role.Name!,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            Reason = "Approved assignment",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var service = NewService(db);

        var run = await service.RunAsync(
            tenant.Id, null, HrIdentityReconciliationTrigger.Scheduled, "test:inactive");

        run.FailedCount.Should().Be(0);
        (await db.Users.SingleAsync(item => item.Id == user.Id)).IsActive.Should().BeFalse();
        (await db.UserSessions.SingleAsync()).IsActive.Should().BeFalse();
        (await db.RefreshTokens.SingleAsync()).IsRevoked.Should().BeTrue();
        (await db.UserTenants.SingleAsync()).Status.Should().Be(UserTenantStatus.Suspended);
        (await db.ProcurementResponsibilityAssignments.SingleAsync()).IsActive.Should().BeFalse();
        (await db.BlacklistedTokens.SingleAsync()).Jti.Should().Be("test-jti");

        employee.IsActive = true;
        employee.StaffStatus = StaffStatus.Active;
        employee.TerminationDate = null;
        await db.SaveChangesAsync();
        await service.RunAsync(tenant.Id, null, HrIdentityReconciliationTrigger.Scheduled, "test:eligible-again");

        var state = await db.HrIdentityReconciliationStates.SingleAsync();
        state.ReactivationReviewRequired.Should().BeTrue();
        (await db.Users.SingleAsync(item => item.Id == user.Id)).IsActive.Should().BeFalse();
        (await new HrIdentityAccessService(db).EvaluateAsync(user.Id)).Code
            .Should().Be("IDENTITY_USER_INACTIVE");
    }

    [Fact]
    public async Task Manager_change_reassigns_pending_approval_without_rewriting_history()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        var oldManagerEmployee = NewEmployee(tenant.Id, StaffStatus.Active, true);
        var newManagerEmployee = NewEmployee(tenant.Id, StaffStatus.Active, true);
        var initiatorEmployee = NewEmployee(tenant.Id, StaffStatus.Active, true);
        initiatorEmployee.ManagerId = oldManagerEmployee.Id;
        var oldManager = NewUser(tenant.Id, oldManagerEmployee.Id, true);
        var newManager = NewUser(tenant.Id, newManagerEmployee.Id, true);
        var initiator = NewUser(tenant.Id, initiatorEmployee.Id, true);
        var entityType = new WorkflowEntityType
        {
            TenantId = tenant.Id,
            Code = "TEST",
            Name = "Test entity"
        };
        var definition = new WorkflowDefinition
        {
            TenantId = tenant.Id,
            Name = "Test approval",
            EntityTypeId = entityType.Id
        };
        var step = new WorkflowStep
        {
            TenantId = tenant.Id,
            WorkflowDefinitionId = definition.Id,
            Name = "Manager approval",
            StepType = WorkflowStepType.Approval,
            Order = 1
        };
        var instance = new WorkflowInstance
        {
            TenantId = tenant.Id,
            WorkflowDefinitionId = definition.Id,
            EntityId = Guid.NewGuid(),
            EntityTypeId = entityType.Id,
            InitiatedById = initiator.Id,
            Status = WorkflowInstanceStatus.InProgress
        };
        var stepInstance = new WorkflowStepInstance
        {
            TenantId = tenant.Id,
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id,
            AssignedToId = oldManager.Id,
            Status = WorkflowStepInstanceStatus.InProgress
        };
        var approval = new WorkflowApproval
        {
            TenantId = tenant.Id,
            StepInstanceId = stepInstance.Id,
            ApproverId = oldManager.Id,
            Status = WorkflowApprovalStatus.Pending
        };
        db.AddRange(tenant, oldManagerEmployee, newManagerEmployee, initiatorEmployee,
            oldManager, newManager, initiator, entityType, definition, step, instance, stepInstance, approval);
        await db.SaveChangesAsync();
        var service = NewService(db);
        await service.RunAsync(tenant.Id, null, HrIdentityReconciliationTrigger.Scheduled, "test:baseline");

        initiatorEmployee.ManagerId = newManagerEmployee.Id;
        await db.SaveChangesAsync();
        var run = await service.RunAsync(
            tenant.Id, null, HrIdentityReconciliationTrigger.Scheduled, "test:manager-change");

        run.FailedCount.Should().Be(0);
        var updated = await db.WorkflowApprovals.SingleAsync(item => item.Id == approval.Id);
        updated.Status.Should().Be(WorkflowApprovalStatus.Pending);
        updated.OriginalApproverId.Should().Be(oldManager.Id);
        updated.ApproverId.Should().Be(newManager.Id);
        (await db.WorkflowStepInstances.SingleAsync(item => item.Id == stepInstance.Id)).AssignedToId
            .Should().Be(newManager.Id);
        (await db.WorkflowActivityLogs.CountAsync(item => item.WorkflowInstanceId == instance.Id))
            .Should().Be(1);
    }

    private static HrIdentityReconciliationService NewService(ApplicationDbContext db)
        => new(db, NullLogger<HrIdentityReconciliationService>.Instance);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"hr-identity-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Tenant NewTenant() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test tenant",
        Code = $"T{Guid.NewGuid():N}"[..12],
        Status = TenantStatus.Active
    };

    private static Employee NewEmployee(Guid tenantId, StaffStatus status, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        EmployeeNumber = $"E{Guid.NewGuid():N}"[..12],
        FirstName = "Test",
        LastName = "Employee",
        EmailAddress = $"{Guid.NewGuid():N}@example.test",
        PositionId = Guid.NewGuid(),
        StaffStatus = status,
        IsActive = isActive,
        TerminationDate = status == StaffStatus.Terminated ? DateTime.UtcNow.AddDays(-1) : null
    };

    private static ApplicationUser NewUser(Guid tenantId, Guid employeeId, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        EmployeeId = employeeId,
        UserName = $"user-{Guid.NewGuid():N}",
        NormalizedUserName = $"USER-{Guid.NewGuid():N}",
        Email = $"{Guid.NewGuid():N}@example.test",
        NormalizedEmail = $"{Guid.NewGuid():N}@EXAMPLE.TEST",
        FirstName = "Test",
        LastName = "User",
        IsActive = isActive,
        SecurityStamp = Guid.NewGuid().ToString()
    };
}
