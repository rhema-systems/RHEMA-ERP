using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Owns the canonical supplier categories shared by supplier onboarding and
/// procurement eligibility checks. Registration selections are intentionally
/// broad; more detailed supplier classifications can still be added later.
/// </summary>
public static class ProcurementSupplierCategoryRegistry
{
    public sealed record Definition(
        ProcurementSupplierRegistrationCategory RegistrationCategory,
        string Code,
        string Name,
        string Description);

    public static IReadOnlyList<Definition> Definitions { get; } =
    [
        new(
            ProcurementSupplierRegistrationCategory.Goods,
            "GOODS",
            "Goods",
            "Suppliers approved through the Goods registration category."),
        new(
            ProcurementSupplierRegistrationCategory.Works,
            "WORKS",
            "Works",
            "Suppliers and contractors approved through the Works registration category."),
        new(
            ProcurementSupplierRegistrationCategory.Services,
            "SERVICES",
            "Services",
            "Suppliers approved through the Services registration category.")
    ];

    public static string CodeFor(
        ProcurementSupplierRegistrationCategory category) =>
        Definitions.Single(item => item.RegistrationCategory == category).Code;

    public static string CodeFor(ProcurementCategoryClass category) => category switch
    {
        ProcurementCategoryClass.Goods => CodeFor(ProcurementSupplierRegistrationCategory.Goods),
        ProcurementCategoryClass.Works => CodeFor(ProcurementSupplierRegistrationCategory.Works),
        ProcurementCategoryClass.TechnicalServices or
        ProcurementCategoryClass.ConsultancyServices or
        ProcurementCategoryClass.GeneralServices =>
            CodeFor(ProcurementSupplierRegistrationCategory.Services),
        _ => throw new ArgumentOutOfRangeException(
            nameof(category), category, "The procurement category is not supported.")
    };
}
