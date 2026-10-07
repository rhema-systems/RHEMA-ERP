using System.Security.Claims;
using ErpSystem.Api.Authorization;
using ErpSystem.Api.Extensions;
using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ErpSystem.Api.Tests.Authorization;

public sealed class CrmPermissionAuthorizationTests
{
    [Theory]
    [InlineData(CrmPermissions.Read)]
    [InlineData(CrmPermissions.Manage)]
    public async Task PermissionAuthorization_ShouldAllowArbitraryRoleWithAssignedCrmPermission(string permission)
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, tenantId, "Regional Opportunity Analyst", permission);

        var authorized = await AuthorizeAsync(
            db,
            CreatePrincipal(userId, tenantId, "Regional Opportunity Analyst"),
            permission);

        authorized.Should().BeTrue();
    }

    [Fact]
    public async Task PermissionAuthorization_ShouldDenyRoleNameWithoutAssignedPermission()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithoutPermissionAsync(db, userId, tenantId);

        var authorized = await AuthorizeAsync(
            db,
            CreatePrincipal(userId, tenantId, "Sales Manager"),
            CrmPermissions.Read);

        authorized.Should().BeFalse();
    }

    [Fact]
    public async Task PermissionAuthorization_ShouldUseEstablishedSuperAdminBypassWithinActiveTenant()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithoutPermissionAsync(db, userId, tenantId);

        var authorized = await AuthorizeAsync(
            db,
            CreatePrincipal(userId, tenantId, Constants.Roles.SuperAdmin),
            CrmPermissions.Manage);

        authorized.Should().BeTrue();
    }

    [Theory]
    [InlineData(CrmPermissions.Read)]
    [InlineData(CrmPermissions.Manage)]
    public void RegisteredCrmPolicy_ShouldUseDatabaseBackedPermissionRequirement(string permission)
    {
        var services = new ServiceCollection();
        services.AddErpSystemAuthorization();
        using var provider = services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(permission);

        policy.Should().NotBeNull();
        var permissionRequirement = policy!.Requirements.Should().ContainSingle().Which
            .Should().BeOfType<PermissionRequirement>().Which;
        permissionRequirement.Permissions.Should().Equal(permission);
        policy.Requirements.OfType<AssertionRequirement>().Should().BeEmpty();
    }

    [Fact]
    public void PermissionCatalogue_ShouldExposeCrmEntriesForRoleManagement()
    {
        CrmPermissions.All.Select(permission => permission.Name)
            .Should().BeEquivalentTo(CrmPermissions.Access, CrmPermissions.Read, CrmPermissions.Manage);
        CrmPermissions.All.Should().OnlyContain(permission =>
            permission.Category == CrmPermissions.Category
            && !string.IsNullOrWhiteSpace(permission.DisplayName)
            && !string.IsNullOrWhiteSpace(permission.Description));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<bool> AuthorizeAsync(
        ApplicationDbContext db,
        ClaimsPrincipal principal,
        string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);
        var handler = new PermissionAuthorizationHandler(
            db,
            NullLogger<PermissionAuthorizationHandler>.Instance);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static async Task SeedUserWithPermissionAsync(
        ApplicationDbContext db,
        Guid userId,
        Guid tenantId,
        string roleName,
        string permissionName)
    {
        await SeedUserWithoutPermissionAsync(db, userId, tenantId);

        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        db.Roles.Add(new ApplicationRole(roleName)
        {
            Id = roleId,
            NormalizedName = roleName.ToUpperInvariant()
        });
        db.Permissions.Add(new Permission
        {
            Id = permissionId,
            Name = permissionName,
            DisplayName = permissionName,
            Category = CrmPermissions.Category,
            IsSystemPermission = true
        });
        db.UserRoles.Add(new ApplicationUserRole
        {
            UserId = userId,
            RoleId = roleId
        });
        db.RolePermissions.Add(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            GrantedBy = "Tests"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedUserWithoutPermissionAsync(
        ApplicationDbContext db,
        Guid userId,
        Guid tenantId)
    {
        await SeedTenantAsync(db, tenantId);
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId:N}@erp.local",
            NormalizedUserName = $"USER-{userId:N}@ERP.LOCAL",
            Email = $"user-{userId:N}@erp.local",
            NormalizedEmail = $"USER-{userId:N}@ERP.LOCAL",
            FirstName = "CRM",
            LastName = "Tester",
            TenantId = tenantId,
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedTenantAsync(ApplicationDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Code = tenantId.ToString("N")[..8].ToUpperInvariant(),
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        });
        await db.SaveChangesAsync();
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, Guid tenantId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(Constants.Claims.TenantId, tenantId.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
