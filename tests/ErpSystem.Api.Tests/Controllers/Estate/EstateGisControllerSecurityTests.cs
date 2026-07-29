using System.Reflection;
using ErpSystem.Api.Controllers.Estate;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class EstateGisControllerSecurityTests
{
    [Fact]
    public void Controller_RequiresAuthentication_AndUsesEstateGisRoute()
    {
        var controllerType = typeof(EstateGisController);

        controllerType.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        controllerType.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        controllerType.GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/estate/gis");
    }

    [Fact]
    public void ConfigurationMutation_IsRestrictedToAdministrators()
    {
        var controllerType = typeof(EstateGisController);
        var saveRoles = GetRoles(controllerType, nameof(EstateGisController.SaveConfiguration));
        var testRoles = GetRoles(controllerType, nameof(EstateGisController.TestConnections));

        saveRoles.Should().Contain("TenantAdmin");
        saveRoles.Should().Contain("SystemAdmin");
        saveRoles.Should().NotContain("Estate Manager");
        saveRoles.Should().BeEquivalentTo(testRoles);
        GetRoles(controllerType, nameof(EstateGisController.GetConfiguration))
            .Should().BeEquivalentTo(saveRoles);
    }

    [Fact]
    public void AssetLinking_IsRestrictedToEstateGisOperators()
    {
        var controllerType = typeof(EstateGisController);
        var linkRoles = GetRoles(controllerType, nameof(EstateGisController.LinkAsset));
        var unlinkRoles = GetRoles(controllerType, nameof(EstateGisController.UnlinkAsset));

        linkRoles.Should().Contain("Estate Manager");
        linkRoles.Should().Contain("Land Registry Officer");
        linkRoles.Should().Contain("Survey Officer");
        linkRoles.Should().NotContain("Estate Officer");
        linkRoles.Should().BeEquivalentTo(unlinkRoles);
    }

    [Fact]
    public void GeometryRead_IsAvailableToEstateOperationalRoles()
    {
        var roles = GetRoles(
            typeof(EstateGisController),
            nameof(EstateGisController.GetAssetGeometry));

        roles.Should().Contain("Estate Officer");
        roles.Should().Contain("Land Registry Officer");
        roles.Should().Contain("Survey Officer");
        GetRoles(
                typeof(EstateGisController),
                nameof(EstateGisController.GetRuntimeConfiguration))
            .Should().BeEquivalentTo(roles);
    }

    private static string[] GetRoles(Type controllerType, string methodName)
        => controllerType
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
