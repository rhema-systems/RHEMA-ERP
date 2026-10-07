using System.Security.Claims;
using ErpSystem.Api.Middleware;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Middleware;

public sealed class TenantAccessValidationMiddlewareTests
{
    [Fact]
    public async Task ActiveTenantMappingAllowsTheRequest()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IUserTenantService>();
        service.Setup(item => item.HasActiveAccessAsync(userId, tenantId)).ReturnsAsync(true);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthorizedContext(userId, tenantId, "/api/finance/accounts");

        await middleware.InvokeAsync(context, service.Object);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        service.Verify(item => item.HasActiveAccessAsync(userId, tenantId), Times.Once);
        service.Verify(item => item.GetAllUserTenantsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(UserTenantStatus.Revoked)]
    [InlineData(UserTenantStatus.Active)]
    public async Task MappingHistoryWithoutActiveAccessBlocksTheRequest(UserTenantStatus status)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IUserTenantService>();
        service.Setup(item => item.HasActiveAccessAsync(userId, tenantId)).ReturnsAsync(false);
        service.Setup(item => item.GetAllUserTenantsAsync(userId)).ReturnsAsync(new[]
        {
            new UserTenant
            {
                Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId, Status = status,
                ExpiresAt = status == UserTenantStatus.Active ? DateTime.UtcNow.AddMinutes(-1) : null,
                GrantedAt = DateTime.UtcNow.AddDays(-1)
            }
        });
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthorizedContext(userId, tenantId, "/api/finance/accounts");

        await middleware.InvokeAsync(context, service.Object);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync()).Should().Contain("TENANT_ACCESS_DENIED");
    }

    [Fact]
    public async Task UserWithNoMappingHistoryRetainsLegacyPrimaryTenantAccess()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IUserTenantService>();
        service.Setup(item => item.HasActiveAccessAsync(userId, tenantId)).ReturnsAsync(false);
        service.Setup(item => item.GetAllUserTenantsAsync(userId))
            .ReturnsAsync(Array.Empty<UserTenant>());
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthorizedContext(userId, tenantId, "/api/finance/accounts");

        await middleware.InvokeAsync(context, service.Object);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task AuthenticationRoutesRemainAvailableForTenantSwitchAndSignOut()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IUserTenantService>();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthorizedContext(userId, tenantId, "/api/auth/me");

        await middleware.InvokeAsync(context, service.Object);

        nextCalled.Should().BeTrue();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OtherProtectedAuthenticationControllerRoutesDoNotBypassTenantValidation()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IUserTenantService>();
        service.Setup(item => item.HasActiveAccessAsync(userId, tenantId)).ReturnsAsync(false);
        service.Setup(item => item.GetAllUserTenantsAsync(userId)).ReturnsAsync(new[]
        {
            new UserTenant
            {
                Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId,
                Status = UserTenantStatus.Revoked, GrantedAt = DateTime.UtcNow.AddDays(-1)
            }
        });
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateAuthorizedContext(userId, tenantId, "/api/auth/session-settings");

        await middleware.InvokeAsync(context, service.Object);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static TenantAccessValidationMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<TenantAccessValidationMiddleware>.Instance);

    private static DefaultHttpContext CreateAuthorizedContext(Guid userId, Guid tenantId, string path)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString())
        }, "Test");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "authorized-test-endpoint"));
        return context;
    }
}
