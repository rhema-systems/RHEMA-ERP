using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.Administration;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public class GlobalSearchAdministrationTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Mock<IUserTenantService> mappings = new(MockBehavior.Strict);
    private readonly Mock<ITenantService> tenants = new(MockBehavior.Strict);
    private readonly Mock<IUserService> users = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserService> current = new();

    private UserTenantMappingsController UserController(bool authenticated = true, bool withTenant = true)
    {
        current.SetupGet(user => user.IsAuthenticated).Returns(authenticated);
        current.SetupGet(user => user.TenantId).Returns(withTenant ? tenantId : null);
        return new UserTenantMappingsController(mappings.Object, tenants.Object, users.Object, current.Object);
    }

    private void ActiveTenant() => tenants.Setup(owner => owner.GetTenantByIdAsync(tenantId))
        .ReturnsAsync(new Tenant { Id = tenantId, Code = "TEST", Name = "Test", Status = TenantStatus.Active });
    private static JsonElement Data(IActionResult response) => JsonSerializer.SerializeToElement(
        Assert.IsType<OkObjectResult>(response).Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task TenantUserSearchUsesOnlyCurrentTenantOwnerAndProjectsNoContactOrSecurityFields()
    {
        ActiveTenant();
        var scoped = Enumerable.Range(1, 25).Select(i => new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = $"finance-{i:D2}", FirstName = "Finance", LastName = "User",
            Email = "private@example.invalid", PhoneNumber = "private", PasswordHash = "private", AuthenticatorKey = "private",
        }).ToArray();
        mappings.Setup(owner => owner.GetActiveTenantUsersAsync(tenantId)).ReturnsAsync(scoped);
        var controller = UserController();
        var rows = Data(await controller.SearchCurrentUsers(" FINANCE ", 100));
        Assert.Equal(20, rows.GetArrayLength());
        Assert.Equal(new[] { "id", "name", "status", "username" }, rows[0].EnumerateObject().Select(field => field.Name).OrderBy(name => name));
        Assert.Equal(scoped[0].Id, rows[0].GetProperty("id").GetGuid());
        Assert.Single(Data(await controller.SearchCurrentUsers("finance", 0)).EnumerateArray());
        mappings.Verify(owner => owner.GetActiveTenantUsersAsync(tenantId), Times.Exactly(2));
        users.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingIdentityOrTenantFailsBeforeOwnerReads(bool authenticated, bool withTenant)
    {
        var controller = UserController(authenticated, withTenant);
        Assert.IsType<ForbidResult>(await controller.SearchCurrentUsers("finance"));
        Assert.IsType<ForbidResult>(await controller.GetCurrentUserSummary(Guid.NewGuid()));
        tenants.VerifyNoOtherCalls(); mappings.VerifyNoOtherCalls(); users.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("deleted")]
    [InlineData("other-tenant")]
    public async Task TenantUserDetailRejectsUnavailableMappings(string mode)
    {
        ActiveTenant();
        var id = Guid.NewGuid();
        var relationship = new UserTenant
        {
            UserId = id, TenantId = mode == "other-tenant" ? Guid.NewGuid() : tenantId,
            Status = mode == "revoked" ? UserTenantStatus.Revoked : UserTenantStatus.Active,
            IsDeleted = mode == "deleted", ExpiresAt = mode == "expired" ? DateTime.UtcNow.AddDays(-1) : null,
            User = new ApplicationUser { Id = id, UserName = "finance" },
        };
        mappings.Setup(owner => owner.GetUserTenantRelationshipAsync(id, tenantId)).ReturnsAsync(relationship);
        Assert.IsType<NotFoundResult>(await UserController().GetCurrentUserSummary(id));
        users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TenantUserDetailUsesTheSameActiveMappingAndMinimalProjection()
    {
        ActiveTenant();
        var id = Guid.NewGuid();
        mappings.Setup(owner => owner.GetUserTenantRelationshipAsync(id, tenantId)).ReturnsAsync(new UserTenant
        {
            UserId = id, TenantId = tenantId, Status = UserTenantStatus.Active,
            User = new ApplicationUser { Id = id, UserName = "finance", FirstName = "Finance", Email = "private@example.invalid" },
        });
        var row = Data(await UserController().GetCurrentUserSummary(id));
        Assert.Equal(id, row.GetProperty("id").GetGuid());
        Assert.Equal(new[] { "id", "name", "status", "username" }, row.EnumerateObject().Select(field => field.Name).OrderBy(name => name));
        users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RoleSearchAndDetailReuseGlobalRoleOwnerWithoutExposingPermissions()
    {
        var owner = new Mock<IRolePermissionService>(MockBehavior.Strict);
        var roles = Enumerable.Range(1, 25).Select(i => new ApplicationRole($"Finance-{i:D2}")
        { Id = Guid.NewGuid(), Description = "Finance role", IsSystemRole = true }).ToArray();
        owner.Setup(service => service.GetAllRolesWithPermissionsAsync()).ReturnsAsync(roles);
        owner.Setup(service => service.GetRoleWithPermissionsByIdAsync(roles[0].Id)).ReturnsAsync(roles[0]);
        var controller = new RoleController(Mock.Of<IRoleService>(), Mock.Of<ErpSystem.Data.Services.IPermissionService>(),
            owner.Object, NullLogger<RoleController>.Instance, Mock.Of<IAuditLogService>(), current.Object);
        var rows = Data(await controller.SearchRoleSummaries(" finance ", 100));
        Assert.Equal(20, rows.GetArrayLength());
        Assert.Equal(new[] { "description", "id", "name", "status" }, rows[0].EnumerateObject().Select(field => field.Name).OrderBy(name => name));
        Assert.Equal(roles[0].Id, Data(await controller.GetRoleSummary(roles[0].Id)).GetProperty("id").GetGuid());
        Assert.Empty(Data(await controller.SearchRoleSummaries("x")).EnumerateArray());
        owner.VerifyAll(); owner.VerifyNoOtherCalls();
    }

    [Fact]
    public void SearchAndDetailKeepExactIdentityReadRoles()
    {
        const string expected = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin;
        Assert.Equal(expected, typeof(UserTenantMappingsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        foreach (var name in new[] { nameof(RoleController.SearchRoleSummaries), nameof(RoleController.GetRoleSummary) })
            Assert.Equal(expected, typeof(RoleController).GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }
}
