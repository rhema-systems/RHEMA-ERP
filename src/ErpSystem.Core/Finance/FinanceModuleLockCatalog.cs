namespace ErpSystem.Core.Finance;

/// <summary>
/// Canonical top-level application modules that currently produce Finance postings.
/// Posting source values such as AP, AR and GL are deliberately kept separate from
/// the top-level origin used by fiscal-period module locks.
/// </summary>
public static class FinanceModuleLockCatalog
{
    public const string Finance = "FIN";
    public const string Inventory = "INV";
    public const string Procurement = "PROC";
    public const string Sales = "SALES";
    public const string HumanResources = "HR";
    public const string QuantitySurvey = "QS";
    public const string Estate = "ESTATE";
    public const string Legal = "LEGAL";
    public const string Maintenance = "MAINT";
    public const string MobilePos = "MOBILEPOS";

    public sealed record Definition(
        string Code,
        string Name,
        string TenantModuleName,
        string Description,
        string IconClass,
        int SortOrder);

    public static IReadOnlyList<Definition> Definitions { get; } =
    [
        new(Finance, "Finance", "Finance",
            "Finance-originated postings including GL, AP, AR, Cash & Bank, Fixed Assets, tax and FX.",
            "fa-calculator", 1),
        new(Inventory, "Inventory", "Inventory",
            "Accounting postings originating from the Inventory module.",
            "fa-boxes", 2),
        new(Procurement, "Procurement", "Procurement",
            "Accounting postings originating from the Procurement module.",
            "fa-shopping-cart", 3),
        new(Sales, "Sales", "Sales",
            "Accounting postings originating from the Sales module.",
            "fa-chart-line", 4),
        new(HumanResources, "Human Resources", "HR",
            "Accounting postings originating from Human Resources, including payroll.",
            "fa-users", 5),
        new(QuantitySurvey, "Quantity Survey", "Quantity Survey",
            "Accounting documents originating from approved Quantity Survey certificates.",
            "fa-ruler-combined", 6),
        new(Estate, "Estate", "Estate",
            "Accounting documents originating from Estate billing and acquisitions.",
            "fa-building", 7),
        new(Legal, "Legal", "Legal",
            "Accounting documents originating from approved Legal procedure cases.",
            "fa-scale-balanced", 8),
        new(Maintenance, "Maintenance", "Maintenance",
            "Accounting documents originating from billable maintenance work orders.",
            "fa-screwdriver-wrench", 9),
        new(MobilePos, "Mobile POS", "Sales",
            "Customer invoices and receipts originating from governed Mobile POS stores and tills.",
            "fa-mobile-screen-button", 10)
    ];

    private static readonly IReadOnlyDictionary<string, Definition> ByCode = Definitions
        .ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetDefinition(string? moduleCode, out Definition definition)
    {
        if (!string.IsNullOrWhiteSpace(moduleCode)
            && ByCode.TryGetValue(moduleCode.Trim(), out var resolved))
        {
            definition = resolved;
            return true;
        }

        definition = null!;
        return false;
    }

    public static string ResolveOriginModuleCode(string sourceModule, string? requestedOriginModuleCode = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedOriginModuleCode))
        {
            var requested = requestedOriginModuleCode.Trim().ToUpperInvariant();
            if (!ByCode.ContainsKey(requested))
            {
                throw new InvalidOperationException(
                    $"Posting origin module '{requestedOriginModuleCode}' is not registered for Finance period locking.");
            }

            return requested;
        }

        var source = sourceModule.Trim();
        if (source.Equals("PAYROLL", StringComparison.OrdinalIgnoreCase))
        {
            return HumanResources;
        }

        if (source.Equals("INV", StringComparison.OrdinalIgnoreCase)
            || source.Equals("INVENTORY", StringComparison.OrdinalIgnoreCase))
        {
            return Inventory;
        }

        if (source.Equals("PROC", StringComparison.OrdinalIgnoreCase)
            || source.Equals("PROCUREMENT", StringComparison.OrdinalIgnoreCase))
        {
            return Procurement;
        }

        if (source.Equals("SALES", StringComparison.OrdinalIgnoreCase))
        {
            return Sales;
        }

        if (source.Equals("MOBILEPOS", StringComparison.OrdinalIgnoreCase)
            || source.Equals("MOBILE POS", StringComparison.OrdinalIgnoreCase))
        {
            return MobilePos;
        }

        // Existing Finance services use several subledger source values. Unknown legacy
        // values also fall back to Finance so old integrations cannot evade a Finance lock.
        return Finance;
    }

    public static bool IsEnabledForTenant(string moduleCode, ISet<string> enabledTenantModules)
    {
        if (!TryGetDefinition(moduleCode, out var definition))
        {
            return false;
        }

        return definition.Code == Finance
            || enabledTenantModules.Contains(definition.TenantModuleName);
    }
}
