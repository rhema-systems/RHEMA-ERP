using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.MobilePos;

internal static class MobilePosCustomerEligibilityQueries
{
    public static IQueryable<BusinessPartnerRole> EligibleMobilePosCustomerRoles(
        this ApplicationDbContext db,
        Guid tenantId,
        DateTime effectiveAtUtc)
        => db.BusinessPartnerRoles.Where(role =>
            role.TenantId == tenantId &&
            !role.IsDeleted &&
            role.RoleType == BusinessPartnerRoleType.Customer &&
            role.Status == BusinessPartnerRoleStatus.Active &&
            role.ActiveFromUtc <= effectiveAtUtc &&
            (!role.InactiveFromUtc.HasValue || role.InactiveFromUtc > effectiveAtUtc) &&
            !role.BusinessPartner.IsDeleted &&
            role.BusinessPartner.IsActive &&
            !role.BusinessPartner.IsBlacklisted &&
            (role.BusinessPartner.RegistrationStatus == "Approved" ||
             role.BusinessPartner.RegistrationStatus == "Active") &&
            role.BusinessPartner.ApprovalStatus == "Approved" &&
            db.BusinessPartnerArProfileVersions.Any(profile =>
                profile.TenantId == tenantId &&
                !profile.IsDeleted &&
                profile.BusinessPartnerRoleId == role.Id &&
                profile.Status == BusinessPartnerFinanceProfileStatus.Approved &&
                profile.EffectiveFrom <= effectiveAtUtc &&
                (!profile.EffectiveTo.HasValue || profile.EffectiveTo > effectiveAtUtc)));
}
