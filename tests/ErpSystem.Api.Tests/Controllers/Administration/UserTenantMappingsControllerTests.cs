using System.Reflection;
using ErpSystem.Api.Controllers.Administration;
using ErpSystem.Api.Models;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Administration;

public sealed class UserTenantMappingsControllerTests
{
    [Fact]
    public void ControllerAllowsOnlyTenantAdminOrSuperAdmin()
    {
        var authorize = typeof(UserTenantMappingsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(
            Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin);
    }

    [Fact]
    public void LegacyTenantUserReadRouteAlsoRequiresTenantAdminOrSuperAdmin()
    {
        var method = typeof(ErpSystem.Api.Controllers.AuthController)
            .GetMethod(nameof(ErpSystem.Api.Controllers.AuthController.GetTenantUsers));

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(
            Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin);
    }

    [Fact]
    public void CompleteTenantRegisterRequiresSuperAdmin()
    {
        var method = typeof(ErpSystem.Api.Controllers.TenantController)
            .GetMethod(nameof(ErpSystem.Api.Controllers.TenantController.GetAdministrationTenants));

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(Constants.Roles.SuperAdmin);
    }

    [Fact]
    public async Task GrantRejectsCrossTenantMutationBeforeCallingTheService()
    {
        var fixture = Fixture(Guid.NewGuid());

        var result = await fixture.Controller.Grant(new SaveTenantUserMappingRequest
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid()
        }, default);

        result.Should().BeOfType<ForbidResult>();
        fixture.UserTenants.VerifyNoOtherCalls();
        fixture.Tenants.VerifyNoOtherCalls();
        fixture.Users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SuperAdminCanGrantAccessToAnotherTenant()
    {
        var currentTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), TenantId = currentTenantId, UserName = "cross.tenant.user",
            FirstName = "Cross", LastName = "Tenant", IsActive = true
        };
        var fixture = Fixture(currentTenantId, isSuperAdmin: true);
        fixture.Tenants.Setup(item => item.GetTenantByIdAsync(targetTenantId))
            .ReturnsAsync(new Tenant
            {
                Id = targetTenantId, Name = "Target Tenant", Code = "TARGET", Status = TenantStatus.Active
            });
        fixture.Users.Setup(item => item.GetUserByIdAsync(user.Id)).ReturnsAsync(user);
        fixture.UserTenants.Setup(item => item.GrantUserAccessToTenantAsync(
                user.Id, targetTenantId, UserTenantAccessLevel.Standard, "ict.admin", null,
                "Approved cross-tenant mapping"))
            .ReturnsAsync(new UserTenant
            {
                Id = Guid.NewGuid(), UserId = user.Id, TenantId = targetTenantId,
                Status = UserTenantStatus.Active, GrantedBy = "ict.admin",
                GrantedAt = DateTime.UtcNow
            });

        var result = await fixture.Controller.Grant(new SaveTenantUserMappingRequest
        {
            UserId = user.Id,
            TenantId = targetTenantId,
            Reason = "Approved cross-tenant mapping"
        }, default);

        result.Should().BeOfType<OkObjectResult>();
        fixture.UserTenants.Verify(item => item.GrantUserAccessToTenantAsync(
            user.Id, targetTenantId, UserTenantAccessLevel.Standard, "ict.admin", null,
            "Approved cross-tenant mapping"), Times.Once);
    }

    [Fact]
    public async Task GetUsersRejectsCrossTenantReadBeforeCallingTheService()
    {
        var fixture = Fixture(Guid.NewGuid());

        var result = await fixture.Controller.GetUsers(Guid.NewGuid(), default);

        result.Should().BeOfType<ForbidResult>();
        fixture.UserTenants.VerifyNoOtherCalls();
        fixture.Tenants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUsersReturnsOnlyMappingsFromTheScopedTenantService()
    {
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserName = "evaluator",
            FirstName = "Internal", LastName = "Evaluator", IsActive = true
        };
        var mapping = new UserTenant
        {
            Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenantId,
            User = user, Status = UserTenantStatus.Active, GrantedAt = DateTime.UtcNow
        };
        var fixture = Fixture(tenantId);
        fixture.Tenants.Setup(item => item.GetTenantByIdAsync(tenantId))
            .ReturnsAsync(new Tenant { Id = tenantId, Name = "Tenant", Code = "TEN", Status = TenantStatus.Active });
        fixture.UserTenants.Setup(item => item.GetActiveTenantUsersAsync(tenantId))
            .ReturnsAsync(new[] { user });
        fixture.UserTenants.Setup(item => item.GetUserTenantRelationshipAsync(user.Id, tenantId))
            .ReturnsAsync(mapping);

        var result = await fixture.Controller.GetUsers(tenantId, default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<TenantUserMapping>>()
            .Which.Should().ContainSingle(item => item.UserId == user.Id.ToString());
    }

    [Fact]
    public async Task GrantCreatesOrReactivatesMappingThroughTheAuthoritativeService()
    {
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserName = "evaluator",
            FirstName = "Internal", LastName = "Evaluator", IsActive = true
        };
        var fixture = Fixture(tenantId);
        fixture.Tenants.Setup(item => item.GetTenantByIdAsync(tenantId))
            .ReturnsAsync(new Tenant { Id = tenantId, Name = "Tenant", Code = "TEN", Status = TenantStatus.Active });
        fixture.Users.Setup(item => item.GetUserByIdAsync(user.Id)).ReturnsAsync(user);
        fixture.UserTenants.Setup(item => item.GrantUserAccessToTenantAsync(
                user.Id, tenantId, UserTenantAccessLevel.Standard, "ict.admin", null,
                "Approved evaluator mapping"))
            .ReturnsAsync(new UserTenant
            {
                Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenantId,
                Status = UserTenantStatus.Active, GrantedBy = "ict.admin",
                GrantedAt = DateTime.UtcNow
            });

        var result = await fixture.Controller.Grant(new SaveTenantUserMappingRequest
        {
            UserId = user.Id,
            TenantId = tenantId,
            Reason = "Approved evaluator mapping"
        }, default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<TenantUserMapping>()
            .Which.IsActive.Should().BeTrue();
        fixture.UserTenants.Verify(item => item.GrantUserAccessToTenantAsync(
            user.Id, tenantId, UserTenantAccessLevel.Standard, "ict.admin", null,
            "Approved evaluator mapping"), Times.Once);
    }

    [Fact]
    public async Task GrantNormalizesUnspecifiedExpiryToUtc()
    {
        var tenantId = Guid.NewGuid();
        var expiry = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Unspecified);
        var expectedExpiry = DateTime.SpecifyKind(expiry, DateTimeKind.Utc);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserName = "expiring.user",
            FirstName = "Expiring", LastName = "User", IsActive = true
        };
        var fixture = Fixture(tenantId);
        fixture.Tenants.Setup(item => item.GetTenantByIdAsync(tenantId))
            .ReturnsAsync(new Tenant { Id = tenantId, Name = "Tenant", Code = "TEN", Status = TenantStatus.Active });
        fixture.Users.Setup(item => item.GetUserByIdAsync(user.Id)).ReturnsAsync(user);
        fixture.UserTenants.Setup(item => item.GrantUserAccessToTenantAsync(
                user.Id, tenantId, UserTenantAccessLevel.Standard, "ict.admin", expectedExpiry,
                "Granted through tenant administration."))
            .ReturnsAsync(new UserTenant
            {
                Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenantId,
                Status = UserTenantStatus.Active, GrantedBy = "ict.admin",
                GrantedAt = DateTime.UtcNow, ExpiresAt = expectedExpiry
            });

        var result = await fixture.Controller.Grant(new SaveTenantUserMappingRequest
        {
            UserId = user.Id,
            TenantId = tenantId,
            ExpiresAt = expiry
        }, default);

        result.Should().BeOfType<OkObjectResult>();
        fixture.UserTenants.Verify(item => item.GrantUserAccessToTenantAsync(
            user.Id, tenantId, UserTenantAccessLevel.Standard, "ict.admin",
            It.Is<DateTime?>(value => value.HasValue &&
                value.Value.Kind == DateTimeKind.Utc && value.Value == expectedExpiry),
            "Granted through tenant administration."), Times.Once);
    }

    [Fact]
    public async Task RevokeIsTenantScopedAndUsesTheAuthoritativeIdempotentService()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixture = Fixture(tenantId);

        var result = await fixture.Controller.Revoke(
            tenantId, userId, default);

        result.Should().BeOfType<NoContentResult>();
        fixture.UserTenants.Verify(item => item.RevokeUserAccessFromTenantAsync(
            userId, tenantId, "ict.admin", "Revoked through tenant administration."), Times.Once);
    }

    [Fact]
    public async Task RevokeRejectsSelfRevocationWithoutCallingTheService()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var fixture = Fixture(tenantId, actorId);

        var result = await fixture.Controller.Revoke(tenantId, actorId, default);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeOfType<ProblemDetails>()
            .Which.Extensions["code"].Should().Be("USER_TENANT_SELF_REVOKE_PROHIBITED");
        fixture.UserTenants.VerifyNoOtherCalls();
    }

    private static TestFixture Fixture(Guid tenantId, Guid? actorId = null, bool isSuperAdmin = false)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.IsAuthenticated).Returns(true);
        current.SetupGet(item => item.TenantId).Returns(tenantId);
        current.SetupGet(item => item.UserId).Returns((actorId ?? Guid.NewGuid()).ToString());
        current.SetupGet(item => item.UserName).Returns("ict.admin");
        current.SetupGet(item => item.FullName).Returns("ICT Administrator");
        current.Setup(item => item.IsInRole(Constants.Roles.SuperAdmin)).Returns(isSuperAdmin);
        return new TestFixture(current);
    }

    private sealed class TestFixture
    {
        public TestFixture(Mock<ICurrentUserService> current)
        {
            Controller = new UserTenantMappingsController(
                UserTenants.Object, Tenants.Object, Users.Object, current.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "tenant-map-test" }
                }
            };
        }

        public Mock<IUserTenantService> UserTenants { get; } = new();
        public Mock<ITenantService> Tenants { get; } = new();
        public Mock<IUserService> Users { get; } = new();
        public UserTenantMappingsController Controller { get; }
    }
}
