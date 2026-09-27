namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Compatibility boundary for persisted partner classifications. Both retains its legacy
/// supplier/contractor classification and historical AR eligibility. New dual-role partners
/// use the explicit CustomerAndSupplier value; adding a role never creates another identity.
/// Arrays are also usable in EF predicates (Contains translates to SQL).
/// </summary>
public static class BusinessPartnerRoles
{
    public const string CustomerAndSupplier = "CustomerAndSupplier";
    public static readonly string[] CustomerTypes = { "Customer", "Both", CustomerAndSupplier };
    public static readonly string[] SupplierTypes = { "Supplier", "Vendor", "Manufacturer", "Both", CustomerAndSupplier };
    public static readonly string[] ProcurementTypes = { "Supplier", "Vendor", "Manufacturer", "Contractor", "Both", CustomerAndSupplier };

    public static bool HasCustomer(string? type) => CustomerTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
    public static bool HasSupplier(string? type) => SupplierTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
    public static bool CanProcure(string? type) => ProcurementTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static void Validate(string type)
    {
        if (!CustomerTypes.Concat(ProcurementTypes).Contains(type, StringComparer.Ordinal))
            throw new InvalidOperationException("Select a supported business partner role.");
    }

    public static void ValidateRoleChange(string previous, string next)
    {
        if (previous == next) return;
        Validate(next);
        // Do not remove roles from partners whose historical transactions still depend on them.
        if (next != CustomerAndSupplier || previous is not ("Supplier" or "Vendor" or "Manufacturer" or "Customer"))
            throw new InvalidOperationException("Existing roles cannot be removed. A supplier or customer can be enabled for both roles.");
    }
}
