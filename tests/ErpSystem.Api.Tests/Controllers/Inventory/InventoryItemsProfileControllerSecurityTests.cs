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

    [Fact]
    [Trait("Batch", "TDC-0616")]
    public void Import_and_history_enforce_inventory_permissions_before_data_access()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers", "InventoryItemsController.cs"));
        var importStart = source.IndexOf("ImportInventoryItems(", StringComparison.Ordinal);
        var importEnd = source.IndexOf("GetInventoryItemHistory", importStart, StringComparison.Ordinal);
        var import = source[importStart..importEnd];
        var historyStart = importEnd;
        var historyEnd = source.IndexOf("DeleteInventoryItem", historyStart, StringComparison.Ordinal);
        var history = source[historyStart..historyEnd];

        import.Should().Contain("procurement.inventory.master-data.manage");
        import.IndexOf("HasInventoryCapabilityAsync", StringComparison.Ordinal)
            .Should().BeLessThan(import.IndexOf("GuardDirectMutationAsync", StringComparison.Ordinal));
        history.Should().Contain("procurement.inventory.read");
        history.Should().Contain("procurement.inventory.master-data.manage");
        history.IndexOf("HasInventoryCapabilityAsync", StringComparison.Ordinal)
            .Should().BeLessThan(history.IndexOf("Repository<InventoryItem>", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Batch", "TDC-INV-STORES")]
    public void Stock_reconciliation_is_dry_run_by_default_and_requires_controlled_permissions()
    {
        var method = typeof(InventoryItemsController)
            .GetMethod(nameof(InventoryItemsController.ReconcileStockTotals))!;
        method.GetParameters().Single(value => value.Name == "dryRun").DefaultValue.Should().Be(true);
        method.GetParameters().Single(value => value.Name == "reason")
            .GetCustomAttributes(typeof(MaxLengthAttribute), true)
            .Cast<MaxLengthAttribute>().Single().Length.Should().Be(500);

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers", "InventoryItemsController.cs"));
        var start = source.IndexOf("ReconcileStockTotals(", StringComparison.Ordinal);
        var end = source.IndexOf("GetInventoryItemAllocations", start, StringComparison.Ordinal);
        var reconcile = source[start..end];

        reconcile.Should().Contain("GetTenantId()");
        reconcile.Should().Contain("procurement.inventory.master-data.manage");
        reconcile.Should().Contain("procurement.inventory.adjust.approve");
        reconcile.Should().Contain("InventoryStock.Reconciled");
        reconcile.Should().NotContain("DefaultTenantId");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
