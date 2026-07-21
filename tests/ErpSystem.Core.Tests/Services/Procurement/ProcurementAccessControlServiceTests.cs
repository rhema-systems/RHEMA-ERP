using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAccessControlServiceTests
{
    [Fact]
    public void RegistryContainsTheCompleteTdcLeastPrivilegeBaseline()
    {
        ProcurementAccessControlRegistry.Roles.Should().HaveCount(19).And.OnlyHaveUniqueItems(item => item.Code);
        ProcurementAccessControlRegistry.Permissions.Should().HaveCount(33).And.OnlyHaveUniqueItems(item => item.Code);
        ProcurementAccessControlRegistry.Committees.Should().HaveCount(4).And.OnlyHaveUniqueItems(item => item.Code);
        ProcurementAccessControlRegistry.Workflows.Should().HaveCount(13).And.OnlyHaveUniqueItems(item => item.Code);

        var audit = ProcurementAccessControlRegistry.Roles.Single(item => item.Code == ProcurementAccessControlRegistry.InternalAuditRole);
        audit.IsReadOnly.Should().BeTrue();
        audit.PermissionCodes.Select(ProcurementAccessControlRegistry.FindPermission)
            .Should().OnlyContain(permission => permission != null && !permission.IsMutation);
    }

    [Fact]
    public async Task SeederIsIdempotentAndLeavesEverySharedWorkflowDraftAndInactive()
    {
        await using var fixture = new Fixture();

        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);

        (await fixture.Context.Roles.CountAsync(item => item.Name!.StartsWith("TDC_"))).Should().Be(19);
        (await fixture.Context.Permissions.CountAsync(item => item.Category == ProcurementAccessControlRegistry.Category)).Should().Be(33);
        (await fixture.Context.ProcurementCommittees.CountAsync(item => item.TenantId == fixture.TenantId)).Should().Be(4);
        var workflows = await fixture.Context.WorkflowDefinitions.Where(item => item.TenantId == fixture.TenantId).ToListAsync();
        workflows.Should().HaveCount(13).And.OnlyContain(item =>
            item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft && !item.IsActive && item.PublishedAt == null);
    }

    [Fact]
    public async Task RestrictedStoresDutyAllowsOnlyTheAssignedTenantWarehouse()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.AssignStoresOfficerAsync(fixture.WarehouseId);

        var allowed = await fixture.Service.CheckCapabilityAsync(Request(fixture.WarehouseId), "trace-allowed");
        var denied = await fixture.Service.EnforceCapabilityAsync(Request(fixture.OtherWarehouseId), "trace-denied");

        allowed.Allowed.Should().BeTrue();
        allowed.MatchedRoles.Should().ContainSingle(ProcurementAccessControlRegistry.Roles.Single(item => item.Code == "TDC_STORES_OFFICER").Code);
        denied.Allowed.Should().BeFalse();
        denied.Code.Should().Be("ACCESS_WAREHOUSE_DENIED");
        (await fixture.Context.AuditLogs.SingleAsync(item => item.Action == "PROCUREMENT_ACCESS_DENIED"))
            .ResourceId.Should().Be($"WH-{fixture.OtherWarehouseId:N}");
        (await fixture.Context.ProcurementControlEvents.SingleAsync()).Result
            .Should().Be(ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task ForeignTenantWarehouseAndAssignmentCannotBeUsed()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.Seeder.SeedTenantAsync(fixture.ForeignTenantId, fixture.UserId);
        var role = await fixture.Context.Roles.SingleAsync(item => item.Name == "TDC_STORES_OFFICER");
        fixture.Context.ProcurementResponsibilityAssignments.Add(new ProcurementResponsibilityAssignment
        {
            TenantId = fixture.ForeignTenantId, UserId = fixture.UserId, RoleId = role.Id, RoleName = role.Name!,
            WarehouseScopeMode = ProcurementWarehouseScopeMode.All, EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true, Reason = "Foreign tenant fixture"
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CheckCapabilityAsync(Request(fixture.WarehouseId), "trace-tenant");

        result.Allowed.Should().BeFalse();
        result.Code.Should().Be("ACCESS_PERMISSION_DENIED");
        result.MatchedAssignmentIds.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignmentRejectsWarehouseFromAnotherTenant()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);

        var action = () => fixture.Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_OFFICER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
            WarehouseIds = new() { fixture.ForeignWarehouseId },
            EffectiveFrom = DateTime.UtcNow.Date,
            Reason = "Cross tenant attempt"
        }, "trace-cross-tenant");

        await action.Should().ThrowAsync<ProcurementAccessNotFoundException>();
        (await fixture.Context.ProcurementResponsibilityAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StaleAssignmentUpdateIsRejectedWithoutChangingTheDuty()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        var created = await fixture.AssignStoresOfficerAsync(fixture.WarehouseId);
        var assignment = await fixture.Context.ProcurementResponsibilityAssignments.SingleAsync(item => item.Id == created.Id);
        assignment.RowVersion = new byte[] { 1, 2, 3, 4 };
        await fixture.Context.SaveChangesAsync();
        var auditCount = await fixture.Context.AuditLogs.CountAsync();

        var action = () => fixture.Service.SaveAssignmentAsync(created.Id, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_MANAGER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.All,
            EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
            Reason = "Stale update acceptance check",
            RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9 })
        }, "trace-stale");

        await action.Should().ThrowAsync<ProcurementAccessConflictException>();
        assignment.RoleName.Should().Be("TDC_STORES_OFFICER");
        (await fixture.Context.AuditLogs.CountAsync()).Should().Be(auditCount);
    }

    private static ProcurementAccessCapabilityRequest Request(Guid warehouseId) => new()
    {
        PermissionCode = "procurement.inventory.read",
        WarehouseId = warehouseId,
        SourceType = "Warehouse",
        SourceReference = $"WH-{warehouseId:N}"
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            WarehouseId = Guid.NewGuid();
            OtherWarehouseId = Guid.NewGuid();
            ForeignWarehouseId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            var user = new ApplicationUser
            {
                Id = UserId, TenantId = TenantId, UserName = "stores@tdc.test", NormalizedUserName = "STORES@TDC.TEST",
                FirstName = "Stores", LastName = "Officer", IsActive = true
            };
            Context.Users.Add(user);
            Context.UserTenants.Add(new UserTenant { UserId = UserId, TenantId = TenantId, Status = UserTenantStatus.Active, User = user });
            Context.Warehouses.AddRange(
                new Warehouse { Id = WarehouseId, TenantId = TenantId, Code = "MAIN", Name = "Main Stores", IsActive = true },
                new Warehouse { Id = OtherWarehouseId, TenantId = TenantId, Code = "SPARES", Name = "Spares", IsActive = true },
                new Warehouse { Id = ForeignWarehouseId, TenantId = ForeignTenantId, Code = "FOREIGN", Name = "Foreign", IsActive = true });
            Context.SaveChanges();
            _currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("stores@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Stores Officer");
            _currentUser.SetupGet(item => item.Roles).Returns(new HashSet<string> { "TenantAdmin" });
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => role == "TenantAdmin");
            _unitOfWork = new UnitOfWork(Context);
            var controlEvents = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementAccessControlService(_unitOfWork, _currentUser.Object, controlEvents,
                NullLogger<ProcurementAccessControlService>.Instance);
            Seeder = new ProcurementAccessControlSeeder(Context, NullLogger<ProcurementAccessControlSeeder>.Instance);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public Guid WarehouseId { get; }
        public Guid OtherWarehouseId { get; }
        public Guid ForeignWarehouseId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementAccessControlService Service { get; }
        public ProcurementAccessControlSeeder Seeder { get; }

        public Task<ProcurementResponsibilityAssignmentDto> AssignStoresOfficerAsync(Guid warehouseId) =>
            Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
            {
                UserId = UserId,
                RoleName = "TDC_STORES_OFFICER",
                WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
                WarehouseIds = new() { warehouseId },
                EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
                Reason = "Acceptance fixture"
            }, "trace-assign");

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
