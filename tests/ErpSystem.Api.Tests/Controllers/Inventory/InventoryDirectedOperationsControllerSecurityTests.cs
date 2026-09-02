using System.Reflection;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryDirectedOperationsControllerSecurityTests
{
    private const string ReadPermission = "procurement.inventory.read";

    [Fact]
    public void Controller_requires_internal_tenant_authentication()
    {
        var type = typeof(InventoryDirectedOperationsController);
        type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().ContainSingle(attribute => attribute.Policy == "InternalOnly");
        type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(InventoryDirectedOperationsController.GetAssignees))]
    [InlineData(nameof(InventoryDirectedOperationsController.GetSuggestions))]
    [InlineData(nameof(InventoryDirectedOperationsController.GetTasks))]
    [InlineData(nameof(InventoryDirectedOperationsController.GetTask))]
    public void Directed_operation_reads_require_the_registered_inventory_read_permission(string action)
    {
        ProcurementAccessControlRegistry.FindPermission(ReadPermission).Should().NotBeNull();
        typeof(InventoryDirectedOperationsController).GetMethod(action)!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().ContainSingle(attribute => attribute.Policy == ReadPermission);
    }

    [Theory]
    [InlineData(nameof(InventoryDirectedOperationsController.CreateTask))]
    [InlineData(nameof(InventoryDirectedOperationsController.StartTask))]
    [InlineData(nameof(InventoryDirectedOperationsController.ConfirmTask))]
    [InlineData(nameof(InventoryDirectedOperationsController.ReconcileTask))]
    [InlineData(nameof(InventoryDirectedOperationsController.CancelTask))]
    public void Directed_operation_mutations_remain_post_only_without_a_legacy_role_gate(string action)
    {
        var method = typeof(InventoryDirectedOperationsController).GetMethod(action)!;
        method.GetCustomAttribute<HttpPostAttribute>().Should().NotBeNull();
        method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().OnlyContain(attribute => string.IsNullOrWhiteSpace(attribute.Roles));
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public void Inventory_and_stores_controllers_have_no_legacy_generic_role_authorization()
    {
        var legacyRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Admin", "SuperAdmin", "TenantAdmin", "Manager", "Employee"
        };
        var controllerTypes = typeof(InventoryDirectedOperationsController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) &&
                (type.Namespace == "ErpSystem.Api.Controllers.Inventory" ||
                 type == typeof(InventoryItemsController) ||
                 type == typeof(InventoryItemIdentifiersController)))
            .ToArray();

        var offenders = controllerTypes.SelectMany(type =>
                type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    .Select(attribute => (Owner: type.Name, attribute.Roles))
                    .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                        .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                            .Select(attribute => (Owner: $"{type.Name}.{method.Name}", attribute.Roles)))))
            .Where(candidate => (candidate.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(legacyRoles.Contains))
            .Select(candidate => candidate.Owner)
            .ToArray();

        offenders.Should().BeEmpty();
    }
}
