using System.Reflection;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

/// <summary>
/// Guards the complete Procurement, Inventory/Stores, Quantity Survey and Civil
/// Engineering API surface against falling back to the legacy generic role model.
/// Module services may still enforce tenant, project, warehouse, assignment and
/// maker/checker scope in addition to these endpoint permissions.
/// </summary>
public sealed class CrossModuleLegacyAuthorizationSecurityTests
{
    private static readonly HashSet<string> LegacyGenericRolesAndPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Administrator",
        "Employee",
        "Manager",
        "SuperAdmin",
        "TenantAdmin",
        // These broad legacy module policies are role aliases, not an operation permission.
        "Inventory",
        "Procurement"
    };

    private static readonly Type[] ScopedControllers = typeof(AwardVerificationsController).Assembly
        .GetTypes()
        .Where(IsScopedController)
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void CompleteModuleEndpointSurfaceDoesNotUseLegacyGenericRolesOrPolicies()
    {
        ScopedControllers.Should().NotBeEmpty();

        var violations = ScopedControllers
            .SelectMany(AuthorizationOwners)
            .SelectMany(owner => owner.Attributes.Select(attribute => new
            {
                owner.Name,
                attribute.Roles,
                attribute.Policy
            }))
            .Where(item =>
                SplitRoles(item.Roles).Any(LegacyGenericRolesAndPolicies.Contains) ||
                (!string.IsNullOrWhiteSpace(item.Policy) &&
                 LegacyGenericRolesAndPolicies.Contains(item.Policy!)))
            .Select(item => $"{item.Name}: roles='{item.Roles}', policy='{item.Policy}'")
            .ToArray();

        violations.Should().BeEmpty(
            "TDC module endpoints must use registered operation permissions, not generic legacy roles or broad role-alias policies");
    }

    [Fact]
    public void ReferencedModulePermissionPoliciesAreRegistered()
    {
        var registered = ProcurementAccessControlRegistry.Permissions
            .Select(permission => permission.Code)
            .Concat(QuantitySurveyAccessControlRegistry.Permissions.Select(permission => permission.Code))
            .Concat(CivilEngineeringAccessControlRegistry.Permissions.Select(permission => permission.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var violations = ScopedControllers
            .SelectMany(AuthorizationOwners)
            .SelectMany(owner => owner.Attributes.Select(attribute => new { owner.Name, attribute.Policy }))
            .Where(item => IsModulePermission(item.Policy) && !registered.Contains(item.Policy!))
            .Select(item => $"{item.Name}: {item.Policy}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        violations.Should().BeEmpty(
            "every module operation policy referenced by an endpoint must be present in its access-control registry");
    }

    [Fact]
    public void DeliberateTdcRoleGatesDoNotContainGenericFallbackRoles()
    {
        var roleGates = ScopedControllers
            .SelectMany(AuthorizationOwners)
            .SelectMany(owner => owner.Attributes
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Roles))
                .Select(attribute => new { owner.Name, Roles = SplitRoles(attribute.Roles).ToArray() }))
            .ToArray();

        roleGates.Should().OnlyContain(gate => gate.Roles.All(role => role.StartsWith("TDC_", StringComparison.Ordinal)),
            "the few intentional direct role gates must be explicit TDC governance roles without generic fallbacks");
    }

    private static bool IsScopedController(Type type)
    {
        if (type.IsAbstract || !typeof(ControllerBase).IsAssignableFrom(type)) return false;

        if (type.Namespace is "ErpSystem.Api.Controllers.Procurement" or
            "ErpSystem.Api.Controllers.Inventory" or
            "ErpSystem.Api.Controllers.QuantitySurvey")
            return true;

        if (type.Namespace == "ErpSystem.Api.Controllers.Projects" &&
            (type.Name.StartsWith("CivilEngineering", StringComparison.Ordinal) ||
             type.Name == "ProjectsController"))
            return true;

        return type == typeof(InventoryItemsController) ||
               type == typeof(InventoryItemIdentifiersController);
    }

    private static IEnumerable<(string Name, IReadOnlyList<AuthorizeAttribute> Attributes)> AuthorizationOwners(Type controller)
    {
        yield return (controller.Name, controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray());

        foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                     .Where(method => method.DeclaringType == controller))
        {
            yield return ($"{controller.Name}.{method.Name}",
                method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray());
        }
    }

    private static bool IsModulePermission(string? policy) =>
        policy?.StartsWith("procurement.", StringComparison.OrdinalIgnoreCase) == true ||
        policy?.StartsWith("quantity-survey.", StringComparison.OrdinalIgnoreCase) == true ||
        policy?.StartsWith("civil-engineering.", StringComparison.OrdinalIgnoreCase) == true ||
        string.Equals(policy, "Inventory.EmergencyOverride", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitRoles(string? roles) =>
        string.IsNullOrWhiteSpace(roles)
            ? Array.Empty<string>()
            : roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
