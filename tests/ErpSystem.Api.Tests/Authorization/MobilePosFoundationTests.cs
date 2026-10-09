using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.MobilePos;
using ErpSystem.Api.Extensions;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ErpSystem.Api.Tests.Authorization;

public sealed class MobilePosFoundationTests
{
    [Fact]
    public void PermissionCatalogue_ShouldExposeUniqueCapabilitiesForDynamicRoleManagement()
    {
        MobilePosPermissions.All.Should().NotBeEmpty();
        MobilePosPermissions.All.Select(item => item.Name).Should().OnlyHaveUniqueItems();
        MobilePosPermissions.AllNames.Should().Equal(MobilePosPermissions.All.Select(item => item.Name));
        MobilePosPermissions.All.Should().OnlyContain(item =>
            item.Name.StartsWith("MobilePOS.", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(item.DisplayName)
            && !string.IsNullOrWhiteSpace(item.Description)
            && item.Category.StartsWith("Mobile POS", StringComparison.Ordinal));
    }

    [Fact]
    public void RegisteredPolicies_ShouldRequireDatabaseBackedPermissionsWithoutRoleNames()
    {
        var services = new ServiceCollection();
        services.AddErpSystemAuthorization();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        foreach (var permission in MobilePosPermissions.All)
        {
            var policy = options.GetPolicy(permission.Name);
            policy.Should().NotBeNull(permission.Name);
            var requirement = policy!.Requirements.Should().ContainSingle().Which
                .Should().BeOfType<PermissionRequirement>().Which;
            if (permission.Name == MobilePosPermissions.ViewStore)
            {
                requirement.Permissions.Should().BeEquivalentTo(
                    MobilePosPermissions.ViewStore,
                    MobilePosPermissions.ManageStore,
                    MobilePosPermissions.ApproveDevice);
            }
            else
            {
                requirement.Permissions.Should().Equal(permission.Name);
            }
            policy.Requirements.OfType<RolesAuthorizationRequirement>().Should().BeEmpty();
        }
    }

    [Fact]
    public void RuntimeController_ShouldProtectEveryActionWithAMobilePosPermission()
    {
        var actions = typeof(MobilePosRuntimeController).GetMethods()
            .Where(method => method.DeclaringType == typeof(MobilePosRuntimeController)
                             && method.IsPublic
                             && method.GetCustomAttributes(inherit: true)
                                 .Any(attribute => attribute is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute));

        actions.Should().NotBeEmpty();
        actions.Should().OnlyContain(action => action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Any(attribute => MobilePosPermissions.AllNames.Contains(attribute.Policy)));
    }

    [Fact]
    public void AdministrationController_ShouldProtectEveryActionWithAMobilePosPermission()
    {
        var actions = typeof(MobilePosAdministrationController).GetMethods()
            .Where(method => method.DeclaringType == typeof(MobilePosAdministrationController)
                             && method.IsPublic
                             && method.GetCustomAttributes(inherit: true)
                                 .Any(attribute => attribute is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute));

        actions.Should().NotBeEmpty();
        actions.Should().OnlyContain(action => action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Any(attribute => MobilePosPermissions.AllNames.Contains(attribute.Policy)));
    }

    [Fact]
    public void Model_ShouldEnforceTenantScopedStoreTillAssignmentAndDeviceIdentityKeys()
    {
        using var db = CreateContext();

        AssertUniqueFilteredIndex<MobilePosStore>(db, nameof(MobilePosStore.TenantId), nameof(MobilePosStore.Code));
        AssertUniqueFilteredIndex<MobilePosTill>(db, nameof(MobilePosTill.TenantId), nameof(MobilePosTill.TillNumber));
        AssertUniqueFilteredIndex<MobilePosUserStoreAssignment>(
            db,
            nameof(MobilePosUserStoreAssignment.TenantId),
            nameof(MobilePosUserStoreAssignment.UserId));
        AssertUniqueFilteredIndex<MobilePosDevice>(
            db,
            nameof(MobilePosDevice.TenantId),
            nameof(MobilePosDevice.InstallationIdHash));
    }

    [Fact]
    public void StoreModel_ShouldRequireGovernedWalkInCustomerAndRoleReferences()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(MobilePosStore));

        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(MobilePosStore.DefaultWalkInBusinessPartnerId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(MobilePosStore.DefaultWalkInBusinessPartnerRoleId))!.IsNullable.Should().BeFalse();
        entity.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(MobilePosStore.DefaultWalkInBusinessPartnerId)
            && key.DeleteBehavior == DeleteBehavior.Restrict);
        entity.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(MobilePosStore.DefaultWalkInBusinessPartnerRoleId)
            && key.DeleteBehavior == DeleteBehavior.Restrict);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mobile-pos-foundation-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AssertUniqueFilteredIndex<TEntity>(
        ApplicationDbContext db,
        params string[] expectedPropertyNames)
        where TEntity : class
    {
        var entity = db.Model.FindEntityType(typeof(TEntity));
        entity.Should().NotBeNull();
        var index = entity!.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(expectedPropertyNames));
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().NotBeNullOrWhiteSpace();
    }
}
