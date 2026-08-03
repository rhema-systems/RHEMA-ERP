using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryProjectReservationControllerSecurityTests
{
    [Fact]
    public void Register_requires_internal_authentication_and_has_no_anonymous_override()
    {
        var type = typeof(InventoryProjectReservationsController);
        type.GetCustomAttribute<AuthorizeAttribute>(inherit: true)!.Policy.Should().Be("InternalOnly");
        type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/inventory/project-reservations");
    }

    [Theory]
    [InlineData(nameof(InventoryProjectReservationsController.Reserve), null)]
    [InlineData(nameof(InventoryProjectReservationsController.Release), "{id:guid}/release")]
    [InlineData(nameof(InventoryProjectReservationsController.Substitute), "{id:guid}/substitute")]
    public void Mutations_are_post_only_and_cannot_be_anonymous(string methodName, string? template)
    {
        var method = typeof(InventoryProjectReservationsController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }
}
