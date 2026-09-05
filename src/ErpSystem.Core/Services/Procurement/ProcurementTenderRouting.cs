using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

internal static class ProcurementTenderRouting
{
    // A case records sourcing lineage; it does not, by itself, opt into an
    // advanced authority route. Partial authority metadata must fail closed in
    // the advanced service, never silently downgrade to the standard route.
    internal static bool HasAdvancedAuthority(Guid? routeId, string? routeReference) =>
        routeId.HasValue || !string.IsNullOrWhiteSpace(routeReference);

    internal static bool RequiresControlledLifecycle(ProcurementMethodType method, bool hasAdvancedAuthority) =>
        method is ProcurementMethodType.QualityBasedSelection or ProcurementMethodType.QualityAndCostBasedSelection ||
        (hasAdvancedAuthority && method is
            (ProcurementMethodType.NationalCompetitiveTendering or ProcurementMethodType.InternationalCompetitiveTendering));
}
