using System.ComponentModel.DataAnnotations;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryItemsProfileControllerSecurityTests
{
    [Fact]
    [Trait("Batch", "TDC-0616")]
    public void Controller_and_profile_routes_are_internal_and_explicit()
    {
        typeof(InventoryItemsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Should().Contain(value => value.Policy == "InternalOnly");

        var import = typeof(InventoryItemsController).GetMethod(nameof(InventoryItemsController.ImportInventoryItems))!;
        import.GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>()
            .Single().Template.Should().Be("import");

        var history = typeof(InventoryItemsController).GetMethod(nameof(InventoryItemsController.GetInventoryItemHistory))!;
        history.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>()
            .Single().Template.Should().Be("{id:guid}/history");
    }

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public void Update_requires_row_version_and_import_is_bounded()
    {
        typeof(UpdateInventoryItemDto).GetProperty(nameof(UpdateInventoryItemDto.RowVersion))!
            .GetCustomAttributes(typeof(RequiredAttribute), true).Should().ContainSingle();

        var items = typeof(ImportInventoryItemsDto).GetProperty(nameof(ImportInventoryItemsDto.Items))!;
        items.GetCustomAttributes(typeof(MinLengthAttribute), true).Cast<MinLengthAttribute>().Single().Length.Should().Be(1);
        items.GetCustomAttributes(typeof(MaxLengthAttribute), true).Cast<MaxLengthAttribute>().Single().Length.Should().Be(500);
    }
}
