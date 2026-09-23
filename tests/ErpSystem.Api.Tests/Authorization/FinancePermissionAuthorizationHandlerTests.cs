using System.Security.Claims;
using ErpSystem.Api.Authorization;
using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Authorization;

public sealed class FinancePermissionAuthorizationHandlerTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public void FinancialControllerRoleContract_ShouldSeparateAccountingBookCheckerFromMaker()
    {
        FinancePermissions.FinancialControllerNames.Should().Contain(new[]
        {
            FinancePermissions.ApproveAccountingBookTransitions,
            FinancePermissions.ApproveAccountingBookPeriods,
            FinancePermissions.ApproveAccountingBookInitialization
        });

        FinancePermissions.FinancialControllerNames.Should().NotContain(
            FinancePermissions.AccountingBookMakerNames);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public async Task PermissionAuthorization_ShouldDenyUnauthenticatedUsers()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, tenantId, FinancePermissions.ViewFinance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var authorized = await AuthorizeAsync(db, principal, FinancePermissions.ViewFinance);

        authorized.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public async Task PermissionAuthorization_ShouldDenyAuthenticatedUserWithoutPermission()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, tenantId, FinancePermissions.ViewFinance);

        var principal = CreatePrincipal(userId, tenantId, "Finance User");

        var authorized = await AuthorizeAsync(db, principal, FinancePermissions.PostJournalEntries);

        authorized.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public async Task PermissionAuthorization_ShouldAllowAuthenticatedUserWithPermission()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, tenantId, FinancePermissions.PostJournalEntries);

        var principal = CreatePrincipal(userId, tenantId, "Financial Controller");

        var authorized = await AuthorizeAsync(db, principal, FinancePermissions.PostJournalEntries);

        authorized.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "TenantIsolation")]
    public async Task PermissionAuthorization_ShouldDenyCrossTenantPermissionUse()
    {
        await using var db = CreateContext();
        var userTenantId = Guid.NewGuid();
        var requestedTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, userTenantId, FinancePermissions.PostJournalEntries);
        await SeedTenantAsync(db, requestedTenantId);

        var principal = CreatePrincipal(userId, requestedTenantId, "Financial Controller");

        var authorized = await AuthorizeAsync(db, principal, FinancePermissions.PostJournalEntries);

        authorized.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public async Task PermissionAuthorization_ShouldDenyWorkflowApprovalWithoutPermission()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, tenantId, FinancePermissions.ViewFinance);

        var principal = CreatePrincipal(userId, tenantId, "Finance User");

        var authorized = await AuthorizeAsync(db, principal, FinancePermissions.WorkflowApprove);

        authorized.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public async Task PermissionAuthorization_ShouldDenyReportExportWithoutPermission()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithPermissionAsync(db, userId, tenantId, FinancePermissions.RunFinanceReports);

        var principal = CreatePrincipal(userId, tenantId, "Finance Analyst");

        var authorized = await AuthorizeAsync(db, principal, FinancePermissions.ExportFinanceReports);

        authorized.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-2")]
    [Trait("Category", "FinanceSecurity")]
    public async Task PermissionAuthorization_ShouldAllowSuperAdminOnlyWithinActiveTenantScope()
    {
        await using var db = CreateContext();
        var userTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedUserWithoutPermissionAsync(db, userId, userTenantId);
        await SeedTenantAsync(db, otherTenantId);

        var inTenantPrincipal = CreatePrincipal(userId, userTenantId, Constants.Roles.SuperAdmin);
        var crossTenantPrincipal = CreatePrincipal(userId, otherTenantId, Constants.Roles.SuperAdmin);

        var inTenantAuthorized = await AuthorizeAsync(db, inTenantPrincipal, FinancePermissions.DisposeFixedAssets);
        var crossTenantAuthorized = await AuthorizeAsync(db, crossTenantPrincipal, FinancePermissions.DisposeFixedAssets);

        inTenantAuthorized.Should().BeTrue();
        crossTenantAuthorized.Should().BeFalse();
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
        string permissionName)
    {
        await SeedUserWithoutPermissionAsync(db, userId, tenantId);

        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        db.Roles.Add(new ApplicationRole("Finance Test Role")
        {
            Id = roleId,
            NormalizedName = "FINANCE TEST ROLE"
        });
        db.Permissions.Add(new Permission
        {
            Id = permissionId,
            Name = permissionName,
            DisplayName = permissionName,
            Category = "Finance",
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
            FirstName = "Finance",
            LastName = "Tester",
            TenantId = tenantId,
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedTenantAsync(ApplicationDbContext db, Guid tenantId)
    {
        if (await db.Tenants.AnyAsync(tenant => tenant.Id == tenantId))
        {
            return;
        }

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
