using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class WarehouseItemsControllerSecurityTests
{
    [Fact]
    [Trait("Batch", "TDC-INV-STORES")]
    public void Warehouse_assignment_contract_does_not_accept_direct_stock_balances()
    {
        typeof(BulkAssignItemsDto).GetProperty("InitialQuantity").Should().BeNull();
        typeof(UpdateWarehouseItemDto).GetProperty("CurrentStock").Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "TDC-INV-STORES")]
    public void Warehouse_assignment_mutations_use_controlled_permissions_and_empty_balance_guard()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers", "Inventory", "WarehouseItemsController.cs"));

        source.Should().Contain("procurement.inventory.master-data.manage");
        source.Should().Contain("CurrentStock = 0");
        source.Should().Contain("AvailableStock = 0");
        source.Should().Contain("WAREHOUSE_ITEM_BALANCE_NOT_ZERO");
        source.Should().Contain("item.CurrentStock != 0 || item.AvailableStock != 0 || item.AllocatedStock != 0");
        source.Should().NotContain("dto.InitialQuantity");
        source.Should().NotContain("dto.CurrentStock");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
