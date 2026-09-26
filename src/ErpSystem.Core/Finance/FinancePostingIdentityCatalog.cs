namespace ErpSystem.Core.Finance;

/// <summary>
/// Finance-owned contract for source identities that may participate in governed
/// accounting-book selection. Producer modules must register here before emitting
/// an accounting intent; policy authors can therefore select identities instead of
/// retyping implementation strings.
/// </summary>
public static class FinancePostingIdentityCatalog
{
    public sealed record Definition(
        string OriginatingModuleCode,
        string ModuleName,
        string SourceDocumentType,
        string DocumentTypeName,
        string PostingAction,
        string PostingActionName);

    public static IReadOnlyList<Definition> Definitions { get; } =
    [
        new(FinanceModuleLockCatalog.Finance, "Finance", "MANUALJOURNAL", "Manual journal", "POST", "Post"),
        new(FinanceModuleLockCatalog.Finance, "Finance", "OPENINGBALANCEBATCH", "Opening balance batch", "POSTOPENINGBALANCE", "Post opening balance"),

        new(FinanceModuleLockCatalog.Inventory, "Inventory", "GOODS.RECEIPT", "Goods receipt", "POST", "Post"),
        new(FinanceModuleLockCatalog.Inventory, "Inventory", "INVENTORY_DISPOSAL", "Inventory disposal", "DISPOSE", "Dispose"),
        new(FinanceModuleLockCatalog.Inventory, "Inventory", "INVENTORYDISPOSAL", "Inventory disposal proceeds", "POSTDISPOSALPROCEEDS", "Post disposal proceeds"),
        new(FinanceModuleLockCatalog.Inventory, "Inventory", "STOCKADJUSTMENT", "Stock adjustment", "POSTOPENINGSTOCK", "Post opening stock"),
        new(FinanceModuleLockCatalog.Inventory, "Inventory", "STOCKADJUSTMENT", "Stock adjustment", "POSTSTOCKADJUSTMENT", "Post stock adjustment"),

        new(FinanceModuleLockCatalog.Sales, "Sales", "CUSTOMERINVOICE", "Customer invoice", "POST", "Post"),
        new(FinanceModuleLockCatalog.Sales, "Sales", "CUSTOMERPAYMENT", "Customer payment", "POST", "Post"),
        new(FinanceModuleLockCatalog.Sales, "Sales", "SALESCREDITNOTE", "Sales credit note", "POST", "Post")
    ];

    private static readonly IReadOnlyDictionary<string, Definition> ByIdentity = Definitions
        .ToDictionary(Key, StringComparer.Ordinal);

    public static Definition Require(string module, string documentType, string postingAction)
    {
        var identity = FinancePreparedIdentityNormalizer.Normalize(module, documentType, postingAction);
        if (!ByIdentity.TryGetValue(Key(identity.OriginatingModuleCode, identity.SourceDocumentType, identity.PostingAction), out var definition))
            throw new InvalidOperationException(
                $"FINANCE_POSTING_IDENTITY_NOT_REGISTERED: '{identity.OriginatingModuleCode} / {identity.SourceDocumentType} / {identity.PostingAction}' is not registered in the Finance posting identity catalog.");
        return definition;
    }

    public static bool IsRegistered(string module, string documentType, string postingAction)
    {
        try { Require(module, documentType, postingAction); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private static string Key(Definition value) => Key(value.OriginatingModuleCode, value.SourceDocumentType, value.PostingAction);
    private static string Key(string module, string documentType, string postingAction) => $"{module}|{documentType}|{postingAction}";
}
