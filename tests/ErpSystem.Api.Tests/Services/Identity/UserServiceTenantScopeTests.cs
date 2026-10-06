using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Identity;

public sealed class UserServiceTenantScopeTests
{
    [Fact]
    public async Task Tenant_queries_include_primary_and_active_assignments_only()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantA = Tenant("TEN-A");
        var tenantB = Tenant("TEN-B");
        db.Tenants.AddRange(tenantA, tenantB);

        var primary = User("primary-a", tenantA.Id);
        var assigned = User("assigned-a", tenantB.Id);
        var other = User("other-b", tenantB.Id);
        var expired = User("expired-a", tenantB.Id);
        db.Users.AddRange(primary, assigned, other, expired);
        db.UserTenants.AddRange(
            Assignment(assigned.Id, tenantA.Id, DateTime.UtcNow.AddDays(1)),
            Assignment(expired.Id, tenantA.Id, DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        var visible = (await service.GetUsersForTenantAsync(tenantA.Id)).ToArray();

        visible.Select(user => user.Id).Should().BeEquivalentTo(new[] { primary.Id, assigned.Id });
        (await service.GetUserByIdForTenantAsync(other.Id, tenantA.Id)).Should().BeNull();
        (await service.GetUserByIdForTenantAsync(assigned.Id, tenantA.Id)).Should().NotBeNull();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"user-service-tenant-{Guid.NewGuid():N}"));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<IUserService, UserService>();
        return services.BuildServiceProvider();
    }

    private static Tenant Tenant(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        Status = TenantStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    private static ApplicationUser User(string name, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        Email = $"{name}@example.test",
        NormalizedEmail = $"{name}@example.test".ToUpperInvariant(),
        FirstName = name,
        LastName = "User",
        TenantId = tenantId,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        IsActive = true
    };

    private static UserTenant Assignment(Guid userId, Guid tenantId, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TenantId = tenantId,
        Status = UserTenantStatus.Active,
        GrantedAt = DateTime.UtcNow,
        ExpiresAt = expiresAt
    };
}
