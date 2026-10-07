using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class UserTenantServiceTests
{
    [Fact]
    public async Task GrantCreatesOneAuditedMappingAndExactReplayIsIdempotent()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var actorId = Guid.NewGuid();
        var current = CurrentUser(actorId);
        var service = Service(unitOfWork, current.Object);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var created = await service.GrantUserAccessToTenantAsync(
            userId, tenantId, grantedBy: "security.admin", notes: "Approved mapping");
        var firstUpdatedAt = created.UpdatedAt;
        var replay = await service.GrantUserAccessToTenantAsync(
            userId, tenantId, grantedBy: "security.admin", notes: "Approved mapping");

        (await context.UserTenants.CountAsync()).Should().Be(1);
        replay.Id.Should().Be(created.Id);
        replay.UpdatedAt.Should().Be(firstUpdatedAt);
        replay.CreatedBy.Should().Be("security.admin");
        replay.CreatedById.Should().Be(actorId);
        replay.GrantedBy.Should().Be("security.admin");
    }

    [Fact]
    public async Task GrantReactivatesTheExistingRevokedMappingWithoutCreatingADuplicate()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var actorId = Guid.NewGuid();
        var current = CurrentUser(actorId);
        var service = Service(unitOfWork, current.Object);
        var mapping = new UserTenant
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = UserTenantStatus.Revoked,
            AccessLevel = UserTenantAccessLevel.ReadOnly,
            GrantedAt = DateTime.UtcNow.AddDays(-5),
            GrantedBy = "old.admin",
            Notes = "Original approval",
            CreatedBy = "old.admin"
        };
        var originalGrantedAt = mapping.GrantedAt;
        context.UserTenants.Add(mapping);
        await context.SaveChangesAsync();

        var result = await service.GrantUserAccessToTenantAsync(
            mapping.UserId,
            mapping.TenantId,
            UserTenantAccessLevel.Standard,
            "security.admin",
            notes: "Reapproved mapping");

        (await context.UserTenants.CountAsync()).Should().Be(1);
        result.Id.Should().Be(mapping.Id);
        result.Status.Should().Be(UserTenantStatus.Active);
        result.ReactivatedAt.Should().NotBeNull();
        result.StatusChangedBy.Should().Be("security.admin");
        result.UpdatedBy.Should().Be("security.admin");
        result.LastModifiedById.Should().Be(actorId);
        result.GrantedBy.Should().Be("old.admin");
        result.GrantedAt.Should().Be(originalGrantedAt);
        result.Notes.Should().Contain("Original approval");
        result.Notes.Should().Contain("[REACTIVATED]");
        var noteAfterReactivation = result.Notes;
        var updatedAfterReactivation = result.UpdatedAt;

        await service.GrantUserAccessToTenantAsync(
            mapping.UserId,
            mapping.TenantId,
            UserTenantAccessLevel.Standard,
            "security.admin",
            notes: "Reapproved mapping");

        result.Notes.Should().Be(noteAfterReactivation);
        result.UpdatedAt.Should().Be(updatedAfterReactivation);
    }

    [Fact]
    public async Task RepeatedRevokeIsIdempotent()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(Guid.NewGuid());
        var service = Service(unitOfWork, current.Object);
        var mapping = new UserTenant
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = UserTenantStatus.Active
        };
        context.UserTenants.Add(mapping);
        await context.SaveChangesAsync();

        var longReason = new string('x', 700);
        await service.RevokeUserAccessFromTenantAsync(
            mapping.UserId, mapping.TenantId, "security.admin", longReason);
        var note = mapping.Notes;
        var updatedAt = mapping.UpdatedAt;
        await service.RevokeUserAccessFromTenantAsync(
            mapping.UserId, mapping.TenantId, "security.admin", longReason);

        mapping.Status.Should().Be(UserTenantStatus.Revoked);
        mapping.Notes.Should().Be(note);
        mapping.UpdatedAt.Should().Be(updatedAt);
        mapping.Notes!.Length.Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public async Task ExpiredMappingDoesNotGrantActiveAccess()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var mapping = new UserTenant
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = UserTenantStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        context.UserTenants.Add(mapping);
        await context.SaveChangesAsync();

        var service = Service(unitOfWork, CurrentUser(Guid.NewGuid()).Object);

        (await service.HasActiveAccessAsync(mapping.UserId, mapping.TenantId)).Should().BeFalse();
        (await service.GetActiveUserTenantsAsync(mapping.UserId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeMovesPrimaryTenantToAnotherActiveMapping()
    {
        await using var context = Context();
        using var unitOfWork = new UnitOfWork(context);
        var userId = Guid.NewGuid();
        var revokedTenantId = Guid.NewGuid();
        var replacementTenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "mapped.user",
            TenantId = revokedTenantId,
            IsActive = true
        };
        context.UserTenants.AddRange(
            new UserTenant { UserId = userId, TenantId = revokedTenantId, Status = UserTenantStatus.Active },
            new UserTenant { UserId = userId, TenantId = replacementTenantId, Status = UserTenantStatus.Active, IsDefault = true });
        await context.SaveChangesAsync();

        var userManager = UserManager();
        userManager.Setup(item => item.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        userManager.Setup(item => item.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var service = new UserTenantService(
            unitOfWork,
            userManager.Object,
            NullLogger<UserTenantService>.Instance,
            CurrentUser(Guid.NewGuid()).Object);

        await service.RevokeUserAccessFromTenantAsync(userId, revokedTenantId, "security.admin", "Access removed");

        user.TenantId.Should().Be(replacementTenantId);
        userManager.Verify(item => item.UpdateAsync(user), Times.Once);
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static UserTenantService Service(UnitOfWork unitOfWork, ICurrentUserService current) =>
        new(unitOfWork, UserManager().Object, NullLogger<UserTenantService>.Instance, current);

    private static Mock<ICurrentUserService> CurrentUser(Guid actorId)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.UserId).Returns(actorId.ToString());
        current.SetupGet(item => item.UserName).Returns("security.admin");
        return current;
    }

    private static Mock<UserManager<ApplicationUser>> UserManager()
    {
        var store = Mock.Of<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store, null!, null!, Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(), null!, null!, null!, null!);
    }
}
