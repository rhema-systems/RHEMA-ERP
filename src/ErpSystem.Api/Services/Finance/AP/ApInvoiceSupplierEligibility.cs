using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>Shared read/command eligibility; display names never establish supplier identity.</summary>
internal static class ApInvoiceSupplierEligibility
{
    internal static bool IsActiveSupplier(Supplier supplier) =>
        !supplier.IsDeleted && supplier.IsActive && !supplier.IsBlacklisted && supplier.Status == "Active";

    internal static bool IsEligiblePartner(BusinessPartner partner, bool hasCanonicalSupplier) =>
        !partner.IsDeleted && partner.IsActive && !partner.IsBlacklisted &&
        partner.RegistrationStatus is "Active" or "Approved" &&
        (CanOnboard(partner) || (hasCanonicalSupplier && partner.PartnerType == "Contractor"));

    internal static bool CanOnboard(BusinessPartner partner) =>
        BusinessPartnerRoles.HasSupplier(partner.PartnerType);

    internal static bool IsLinked(Supplier supplier, BusinessPartner partner) =>
        supplier.Id == partner.Id || (!string.IsNullOrWhiteSpace(partner.PartnerCode) &&
            string.Equals(supplier.SupplierCode, partner.PartnerCode, StringComparison.OrdinalIgnoreCase));
}
