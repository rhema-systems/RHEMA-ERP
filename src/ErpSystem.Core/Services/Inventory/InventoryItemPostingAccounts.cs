using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public static class InventoryItemPostingAccounts
{
    public static InventoryItemPostingAccountsDto Read(InventoryItem item) => new()
    {
        InventoryAccountId = item.InventoryAccountId,
        InventoryOffsetAccountId = item.InventoryOffsetAccountId,
        CostOfGoodsSoldAccountId = item.CostOfGoodsSoldAccountId,
        SalesAccountId = item.SalesAccountId,
        MarkdownsAccountId = item.MarkdownsAccountId,
        SalesReturnsAccountId = item.SalesReturnsAccountId,
        InUseAccountId = item.InUseAccountId,
        InServiceAccountId = item.InServiceAccountId,
        DamagedAccountId = item.DamagedAccountId,
        VarianceAccountId = item.VarianceAccountId,
        DropShipItemsAccountId = item.DropShipItemsAccountId,
        PurchasePriceVarianceAccountId = item.PurchasePriceVarianceAccountId,
        UnrealisedPurchasePriceVarianceAccountId = item.UnrealisedPurchasePriceVarianceAccountId,
        InventoryReturnsAccountId = item.InventoryReturnsAccountId,
        AssemblyVarianceAccountId = item.AssemblyVarianceAccountId,
        StandardCostRevaluationAccountId = item.StandardCostRevaluationAccountId,
    };

    public static void Apply(InventoryItemPostingAccountsDto? values, InventoryItem item)
    {
        if (values is null) return;
        if (values.ProvidedFields.Contains(nameof(values.InventoryAccountId))) item.InventoryAccountId = values.InventoryAccountId;
        if (values.ProvidedFields.Contains(nameof(values.InventoryOffsetAccountId))) item.InventoryOffsetAccountId = values.InventoryOffsetAccountId;
        if (values.ProvidedFields.Contains(nameof(values.CostOfGoodsSoldAccountId))) item.CostOfGoodsSoldAccountId = values.CostOfGoodsSoldAccountId;
        if (values.ProvidedFields.Contains(nameof(values.SalesAccountId))) item.SalesAccountId = values.SalesAccountId;
        if (values.ProvidedFields.Contains(nameof(values.MarkdownsAccountId))) item.MarkdownsAccountId = values.MarkdownsAccountId;
        if (values.ProvidedFields.Contains(nameof(values.SalesReturnsAccountId))) item.SalesReturnsAccountId = values.SalesReturnsAccountId;
        if (values.ProvidedFields.Contains(nameof(values.InUseAccountId))) item.InUseAccountId = values.InUseAccountId;
        if (values.ProvidedFields.Contains(nameof(values.InServiceAccountId))) item.InServiceAccountId = values.InServiceAccountId;
        if (values.ProvidedFields.Contains(nameof(values.DamagedAccountId))) item.DamagedAccountId = values.DamagedAccountId;
        if (values.ProvidedFields.Contains(nameof(values.VarianceAccountId))) item.VarianceAccountId = values.VarianceAccountId;
        if (values.ProvidedFields.Contains(nameof(values.DropShipItemsAccountId))) item.DropShipItemsAccountId = values.DropShipItemsAccountId;
        if (values.ProvidedFields.Contains(nameof(values.PurchasePriceVarianceAccountId))) item.PurchasePriceVarianceAccountId = values.PurchasePriceVarianceAccountId;
        if (values.ProvidedFields.Contains(nameof(values.UnrealisedPurchasePriceVarianceAccountId))) item.UnrealisedPurchasePriceVarianceAccountId = values.UnrealisedPurchasePriceVarianceAccountId;
        if (values.ProvidedFields.Contains(nameof(values.InventoryReturnsAccountId))) item.InventoryReturnsAccountId = values.InventoryReturnsAccountId;
        if (values.ProvidedFields.Contains(nameof(values.AssemblyVarianceAccountId))) item.AssemblyVarianceAccountId = values.AssemblyVarianceAccountId;
        if (values.ProvidedFields.Contains(nameof(values.StandardCostRevaluationAccountId))) item.StandardCostRevaluationAccountId = values.StandardCostRevaluationAccountId;
    }

    public static IEnumerable<(string Purpose, Guid? AccountId, AccountType[] Types)> GetMappings(InventoryItem item)
    {
        yield return (nameof(item.InventoryAccountId), item.InventoryAccountId, new[] { AccountType.Asset });
        yield return (nameof(item.InventoryOffsetAccountId), item.InventoryOffsetAccountId, new[] { AccountType.Asset, AccountType.Liability });
        yield return (nameof(item.CostOfGoodsSoldAccountId), item.CostOfGoodsSoldAccountId, new[] { AccountType.Expense });
        yield return (nameof(item.SalesAccountId), item.SalesAccountId, new[] { AccountType.Revenue });
        yield return (nameof(item.MarkdownsAccountId), item.MarkdownsAccountId, new[] { AccountType.Revenue, AccountType.Expense });
        yield return (nameof(item.SalesReturnsAccountId), item.SalesReturnsAccountId, new[] { AccountType.Revenue, AccountType.Expense });
        yield return (nameof(item.InUseAccountId), item.InUseAccountId, new[] { AccountType.Expense });
        yield return (nameof(item.InServiceAccountId), item.InServiceAccountId, new[] { AccountType.Expense });
        yield return (nameof(item.DamagedAccountId), item.DamagedAccountId, new[] { AccountType.Expense });
        yield return (nameof(item.VarianceAccountId), item.VarianceAccountId, new[] { AccountType.Expense, AccountType.Revenue });
        yield return (nameof(item.DropShipItemsAccountId), item.DropShipItemsAccountId, new[] { AccountType.Asset, AccountType.Expense });
        yield return (nameof(item.PurchasePriceVarianceAccountId), item.PurchasePriceVarianceAccountId, new[] { AccountType.Expense, AccountType.Revenue });
        yield return (nameof(item.UnrealisedPurchasePriceVarianceAccountId), item.UnrealisedPurchasePriceVarianceAccountId, new[] { AccountType.Asset, AccountType.Liability, AccountType.Expense, AccountType.Revenue });
        yield return (nameof(item.InventoryReturnsAccountId), item.InventoryReturnsAccountId, new[] { AccountType.Asset });
        yield return (nameof(item.AssemblyVarianceAccountId), item.AssemblyVarianceAccountId, new[] { AccountType.Expense, AccountType.Revenue });
        yield return (nameof(item.StandardCostRevaluationAccountId), item.StandardCostRevaluationAccountId, new[] { AccountType.Expense, AccountType.Revenue });
    }

    public static async Task ValidateAsync(IUnitOfWork unitOfWork, InventoryItem item, CancellationToken ct = default)
    {
        var mappings = GetMappings(item).Where(value => value.AccountId.HasValue).ToArray();
        if (mappings.Length == 0) return;
        var ids = mappings.Select(value => value.AccountId!.Value).Distinct().ToArray();
        var accounts = await unitOfWork.Repository<Account>().GetQueryable(account =>
                account.TenantId == item.TenantId && ids.Contains(account.Id) && !account.IsDeleted)
            .ToDictionaryAsync(account => account.Id, ct);
        foreach (var mapping in mappings)
        {
            if (!accounts.TryGetValue(mapping.AccountId!.Value, out var account) ||
                account.Status != AccountStatus.Active || (!account.AllowDirectPosting && !account.IsControlAccount) ||
                !mapping.Types.Contains(account.AccountType))
                throw new InventoryItemProfileValidationException("ITEM_POSTING_ACCOUNT_INVALID",
                    $"{mapping.Purpose.Replace("AccountId", "")}: select an active posting account of the correct type in the current tenant, or use the default.");
        }
    }
}
