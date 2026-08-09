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
        ProcurementAccessControlRegistry.Permissions.Should().HaveCount(39).And.OnlyHaveUniqueItems(item => item.Code);
        ProcurementAccessControlRegistry.Committees.Should().HaveCount(4).And.OnlyHaveUniqueItems(item => item.Code);
        ProcurementAccessControlRegistry.Workflows.Should().HaveCount(13).And.OnlyHaveUniqueItems(item => item.Code);

        var audit = ProcurementAccessControlRegistry.Roles.Single(item => item.Code == ProcurementAccessControlRegistry.InternalAuditRole);
        audit.IsReadOnly.Should().BeTrue();
        audit.PermissionCodes.Select(ProcurementAccessControlRegistry.FindPermission)
            .Should().OnlyContain(permission => permission != null && !permission.IsMutation);

        ProcurementAccessControlRegistry.FindPermission(
                ProcurementAccessControlRegistry.SupplierPaymentVerifyPermission)
            .Should().NotBeNull().And.Match<ProcurementPermissionDefinition>(permission =>
                permission.IsMutation);
        ProcurementAccessControlRegistry.FindRole("TDC_PROCUREMENT_OFFICER")!
            .PermissionCodes.Should().Contain(
                ProcurementAccessControlRegistry.SupplierPaymentVerifyPermission);
    }

    [Fact]
    public async Task SeederIsIdempotentAndLeavesEverySharedWorkflowDraftAndInactive()
    {
        await using var fixture = new Fixture();

        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);

        (await fixture.Context.Roles.CountAsync(item => item.Name!.StartsWith("TDC_"))).Should().Be(19);
        (await fixture.Context.Permissions.CountAsync(item => item.Category == ProcurementAccessControlRegistry.Category)).Should().Be(39);
        (await fixture.Context.ProcurementCommittees.CountAsync(item => item.TenantId == fixture.TenantId)).Should().Be(4);
        var workflows = await fixture.Context.WorkflowDefinitions.Where(item => item.TenantId == fixture.TenantId).ToListAsync();
        workflows.Should().HaveCount(13).And.OnlyContain(item =>
            item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft && !item.IsActive && item.PublishedAt == null);
    }

    [Fact]
    public async Task SecurityRoleGrantsAnUnscopedPrivilegeWithoutAProcurementResponsibilityAssignment()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.GrantSecurityRoleAsync("TDC_PROCUREMENT_OFFICER");

        var decision = await fixture.Service.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = ProcurementAccessControlRegistry.SupplierPaymentVerifyPermission,
            SourceType = "SupplierOnboardingPayment",
            SourceReference = "PAYMENT-001"
        }, "trace-security-role");

        decision.Allowed.Should().BeTrue();
        decision.MatchedRoles.Should().ContainSingle("TDC_PROCUREMENT_OFFICER");
        decision.MatchedAssignmentIds.Should().BeEmpty();
        (await fixture.Context.ProcurementResponsibilityAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RepeatedCapabilityEnforcementAppendsDistinctAuditEventsForTheSameCorrelation()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.GrantSecurityRoleAsync("TDC_PROCUREMENT_OFFICER");
        var request = new ProcurementAccessCapabilityRequest
        {
            PermissionCode = ProcurementAccessControlRegistry.SupplierPaymentVerifyPermission,
            SourceType = "SupplierDocument",
            SourceReference = "DOC-001"
        };

        var first = await fixture.Service.EnforceCapabilityAsync(request, "trace-repeated-decision");
        var second = await fixture.Service.EnforceCapabilityAsync(request, "trace-repeated-decision");

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeTrue();
        var events = await fixture.Context.ProcurementControlEvents
            .Where(item => item.EventType == "AccessDecision" &&
                           item.CorrelationId == "trace-repeated-decision")
            .ToListAsync();
        events.Should().HaveCount(2);
        events.Select(item => item.EventKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ActiveContextScopeCannotBeCreatedBeforeTheSecurityRoleIsGranted()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);

        var action = () => fixture.Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_OFFICER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
            WarehouseIds = new() { fixture.WarehouseId },
            LocationScopeMode = ProcurementLocationScopeMode.All,
            EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
            Reason = "Scope must not replace Security membership"
        }, "trace-role-required");

        (await action.Should().ThrowAsync<ProcurementAccessValidationException>())
            .Which.Code.Should().Be("SECURITY_ROLE_REQUIRED");
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
    public async Task RestrictedLocationDutyAllowsOnlyTheAssignedLocationInsideTheWarehouse()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.AssignStoresOfficerAsync(fixture.WarehouseId, fixture.LocationId);

        var allowed = await fixture.Service.CheckCapabilityAsync(
            Request(fixture.WarehouseId, fixture.LocationId), "trace-location-allowed");
        var denied = await fixture.Service.EnforceCapabilityAsync(
            Request(fixture.WarehouseId, fixture.OtherLocationId), "trace-location-denied");

        allowed.Allowed.Should().BeTrue();
        allowed.LocationId.Should().Be(fixture.LocationId);
        denied.Allowed.Should().BeFalse();
        denied.Code.Should().Be("ACCESS_LOCATION_DENIED");
        (await fixture.Context.AuditLogs.SingleAsync(item => item.Action == "PROCUREMENT_ACCESS_DENIED"))
            .NewValues.Should().Contain(fixture.OtherLocationId.ToString());
    }

    [Fact]
    public async Task RestrictedLocationDutyRequiresAnExplicitLocationForStoreTransactions()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.AssignStoresOfficerAsync(fixture.WarehouseId, fixture.LocationId);
        var request = Request(fixture.WarehouseId);
        request.RequireLocationScope = true;

        var denied = await fixture.Service.EnforceCapabilityAsync(request, "trace-location-required");

        denied.Allowed.Should().BeFalse();
        denied.Code.Should().Be("ACCESS_LOCATION_REQUIRED");
    }

    [Fact]
    public async Task RestrictedAssignmentAcceptsAConsignmentBinForItsEffectiveInventoryWarehouse()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.GrantSecurityRoleAsync("TDC_STORES_OFFICER");
        var consignmentLocationId = Guid.NewGuid();
        fixture.Context.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = consignmentLocationId,
            TenantId = fixture.TenantId,
            WarehouseId = fixture.WarehouseId,
            LocationCode = "SUPPLIER-CONSIGNMENT",
            Name = "Supplier consignment bin",
            IsActive = true,
            IsConsignmentBin = true,
            ConsignmentWarehouseId = fixture.OtherWarehouseId
        });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_OFFICER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
            WarehouseIds = new() { fixture.OtherWarehouseId },
            LocationScopeMode = ProcurementLocationScopeMode.Restricted,
            LocationIds = new() { consignmentLocationId },
            EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
            Reason = "Consignment stores assignment regression"
        }, "trace-consignment-assignment");

        var allowed = await fixture.Service.CheckCapabilityAsync(
            Request(fixture.OtherWarehouseId, consignmentLocationId), "trace-consignment-capability");

        allowed.Allowed.Should().BeTrue();
        allowed.WarehouseId.Should().Be(fixture.OtherWarehouseId);
        allowed.LocationId.Should().Be(consignmentLocationId);
        (await fixture.Context.ProcurementResponsibilityLocations.SingleAsync())
            .WarehouseId.Should().Be(fixture.OtherWarehouseId);
    }

    [Fact]
    public async Task CrossStoreTransferAuthorityRequiresBothAssignedWarehousesAndLocations()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.GrantSecurityRoleAsync("TDC_STORES_OFFICER");
        await fixture.Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_OFFICER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
            WarehouseIds = new() { fixture.WarehouseId, fixture.OtherWarehouseId },
            LocationScopeMode = ProcurementLocationScopeMode.Restricted,
            LocationIds = new() { fixture.LocationId, fixture.DestinationLocationId },
            EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
            Reason = "Cross-store acceptance fixture"
        }, "trace-cross-store-assignment");

        var source = await fixture.Service.CheckCapabilityAsync(
            Request(fixture.WarehouseId, fixture.LocationId), "trace-cross-store-source");
        var destination = await fixture.Service.CheckCapabilityAsync(
            Request(fixture.OtherWarehouseId, fixture.DestinationLocationId), "trace-cross-store-destination");
        var unassigned = await fixture.Service.EnforceCapabilityAsync(
            Request(fixture.WarehouseId, fixture.OtherLocationId), "trace-cross-store-unassigned");

        source.Allowed.Should().BeTrue();
        destination.Allowed.Should().BeTrue();
        unassigned.Allowed.Should().BeFalse();
        unassigned.Code.Should().Be("ACCESS_LOCATION_DENIED");
    }

    [Fact]
    public async Task LocationAssignmentModelHasTenantWarehouseAndLocationRelationships()
    {
        await using var fixture = new Fixture();
        var entity = fixture.Context.Model.FindEntityType(typeof(ProcurementResponsibilityLocation));

        entity.Should().NotBeNull();
        var principals = entity!.GetForeignKeys().Select(key => key.PrincipalEntityType.ClrType).ToList();
        principals.Should().Contain(typeof(ProcurementResponsibilityAssignment));
        principals.Should().Contain(typeof(Warehouse));
        principals.Should().Contain(typeof(WarehouseLocation));
        principals.Should().Contain(typeof(Tenant));
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ProcurementResponsibilityLocation.TenantId),
                nameof(ProcurementResponsibilityLocation.AssignmentId),
                nameof(ProcurementResponsibilityLocation.WarehouseLocationId)
            }));
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
            LocationScopeMode = ProcurementLocationScopeMode.All,
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
        await fixture.GrantSecurityRoleAsync("TDC_STORES_OFFICER");

        var action = () => fixture.Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_OFFICER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
            WarehouseIds = new() { fixture.ForeignWarehouseId },
            LocationScopeMode = ProcurementLocationScopeMode.All,
            EffectiveFrom = DateTime.UtcNow.Date,
            Reason = "Cross tenant attempt"
        }, "trace-cross-tenant");

        await action.Should().ThrowAsync<ProcurementAccessNotFoundException>();
        (await fixture.Context.ProcurementResponsibilityAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AssignmentRejectsLocationFromAnotherTenant()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        await fixture.GrantSecurityRoleAsync("TDC_STORES_OFFICER");

        var action = () => fixture.Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_OFFICER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.All,
            LocationScopeMode = ProcurementLocationScopeMode.Restricted,
            LocationIds = new() { fixture.ForeignLocationId },
            EffectiveFrom = DateTime.UtcNow.Date,
            Reason = "Cross tenant location attempt"
        }, "trace-cross-tenant-location");

        await action.Should().ThrowAsync<ProcurementAccessNotFoundException>();
        (await fixture.Context.ProcurementResponsibilityAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StaleAssignmentUpdateIsRejectedWithoutChangingTheDuty()
    {
        await using var fixture = new Fixture();
        await fixture.Seeder.SeedTenantAsync(fixture.TenantId, fixture.UserId);
        var created = await fixture.AssignStoresOfficerAsync(fixture.WarehouseId);
        await fixture.GrantSecurityRoleAsync("TDC_STORES_MANAGER");
        var assignment = await fixture.Context.ProcurementResponsibilityAssignments.SingleAsync(item => item.Id == created.Id);
        assignment.RowVersion = new byte[] { 1, 2, 3, 4 };
        await fixture.Context.SaveChangesAsync();
        var auditCount = await fixture.Context.AuditLogs.CountAsync();

        var action = () => fixture.Service.SaveAssignmentAsync(created.Id, new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = fixture.UserId,
            RoleName = "TDC_STORES_MANAGER",
            WarehouseScopeMode = ProcurementWarehouseScopeMode.All,
            LocationScopeMode = ProcurementLocationScopeMode.All,
            EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
            Reason = "Stale update acceptance check",
            RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9 })
        }, "trace-stale");

        await action.Should().ThrowAsync<ProcurementAccessConflictException>();
        assignment.RoleName.Should().Be("TDC_STORES_OFFICER");
        (await fixture.Context.AuditLogs.CountAsync()).Should().Be(auditCount);
    }

    private static ProcurementAccessCapabilityRequest Request(Guid warehouseId, Guid? locationId = null) => new()
    {
        PermissionCode = "procurement.inventory.read",
        WarehouseId = warehouseId,
        LocationId = locationId,
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
            LocationId = Guid.NewGuid();
            OtherLocationId = Guid.NewGuid();
            DestinationLocationId = Guid.NewGuid();
            ForeignLocationId = Guid.NewGuid();
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
            Context.WarehouseLocations.AddRange(
                new WarehouseLocation { Id = LocationId, TenantId = TenantId, WarehouseId = WarehouseId, LocationCode = "MAIN-A", Name = "Main A", IsActive = true },
                new WarehouseLocation { Id = OtherLocationId, TenantId = TenantId, WarehouseId = WarehouseId, LocationCode = "MAIN-B", Name = "Main B", IsActive = true },
                new WarehouseLocation { Id = DestinationLocationId, TenantId = TenantId, WarehouseId = OtherWarehouseId, LocationCode = "SPARES-A", Name = "Spares A", IsActive = true },
                new WarehouseLocation { Id = ForeignLocationId, TenantId = ForeignTenantId, WarehouseId = ForeignWarehouseId, LocationCode = "FOREIGN-A", Name = "Foreign A", IsActive = true });
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
        public Guid LocationId { get; }
        public Guid OtherLocationId { get; }
        public Guid DestinationLocationId { get; }
        public Guid ForeignLocationId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementAccessControlService Service { get; }
        public ProcurementAccessControlSeeder Seeder { get; }

        public async Task<ProcurementResponsibilityAssignmentDto> AssignStoresOfficerAsync(Guid warehouseId, Guid? locationId = null)
        {
            await GrantSecurityRoleAsync("TDC_STORES_OFFICER");
            return await Service.SaveAssignmentAsync(null, new SaveProcurementResponsibilityAssignmentRequest
            {
                UserId = UserId,
                RoleName = "TDC_STORES_OFFICER",
                WarehouseScopeMode = ProcurementWarehouseScopeMode.Restricted,
                WarehouseIds = new() { warehouseId },
                LocationScopeMode = locationId.HasValue ? ProcurementLocationScopeMode.Restricted : ProcurementLocationScopeMode.All,
                LocationIds = locationId.HasValue ? new() { locationId.Value } : new(),
                EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
                Reason = "Acceptance fixture"
            }, "trace-assign");
        }

        public async Task GrantSecurityRoleAsync(string roleName)
        {
            var role = await Context.Roles.SingleAsync(item => item.Name == roleName);
            if (!await Context.Set<ApplicationUserRole>().AnyAsync(item => item.UserId == UserId && item.RoleId == role.Id))
            {
                Context.Set<ApplicationUserRole>().Add(new ApplicationUserRole { UserId = UserId, RoleId = role.Id });
                await Context.SaveChangesAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
