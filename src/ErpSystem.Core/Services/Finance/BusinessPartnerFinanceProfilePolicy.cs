using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Finance;

public sealed record BusinessPartnerFinanceProfileReadiness(
    bool IsReady,
    string Code,
    string Message,
    BusinessPartnerApProfileVersion? ApProfile = null,
    BusinessPartnerArProfileVersion? ArProfile = null);

/// <summary>
/// Shared fail-closed policy for selecting canonical counterparty Finance defaults. Every Finance
/// producer must resolve the profile using the document accounting date; using the latest row or
/// today's date would silently change backdated accounting treatment.
/// </summary>
public static class BusinessPartnerFinanceProfilePolicy
{
    public static BusinessPartnerFinanceProfileReadiness ResolveAp(
        BusinessPartner partner,
        BusinessPartnerRole? role,
        IEnumerable<BusinessPartnerApProfileVersion> profiles,
        DateTime accountingDate)
    {
        if (!IsPartnerOperational(partner, out var partnerFailure))
        {
            return partnerFailure;
        }

        if (role is null || role.RoleType is not (BusinessPartnerRoleType.Supplier or BusinessPartnerRoleType.Contractor))
        {
            return Failure("AP_ROLE_REQUIRED", "The Business Partner does not have a Supplier or Contractor role.");
        }

        if (role.Status != BusinessPartnerRoleStatus.Active)
        {
            return Failure("AP_ROLE_INACTIVE", "The Business Partner AP role is inactive for new transactions.");
        }

        var profile = SelectEffective(profiles, role.Id, accountingDate);
        if (profile is null)
        {
            return Failure(
                "AP_PROFILE_REQUIRED",
                $"No approved AP profile is effective on {accountingDate:dd/MM/yyyy}.");
        }

        if (profile.SubjectToWithholding)
        {
            if (string.IsNullOrWhiteSpace(partner.TaxIdentificationNumber))
            {
                return Failure("AP_WHT_TIN_REQUIRED", "The WHT-applicable Business Partner requires a TIN before invoicing.");
            }

            var defaultWht = profile.WithholdingDefaults.SingleOrDefault(x =>
                !x.IsDeleted && x.IsActive && x.IsDefaultForAp);
            if (defaultWht is null)
            {
                return Failure(
                    "AP_WHT_DEFAULT_REQUIRED",
                    "The approved AP profile is WHT-applicable but has no active default WHT configuration.");
            }
        }

        return new BusinessPartnerFinanceProfileReadiness(
            true,
            "READY",
            "The Business Partner has an approved AP profile for the accounting date.",
            ApProfile: profile);
    }

    public static BusinessPartnerFinanceProfileReadiness ResolveAr(
        BusinessPartner partner,
        BusinessPartnerRole? role,
        IEnumerable<BusinessPartnerArProfileVersion> profiles,
        DateTime accountingDate)
    {
        if (!IsPartnerOperational(partner, out var partnerFailure))
        {
            return partnerFailure;
        }

        if (role is null || role.RoleType != BusinessPartnerRoleType.Customer)
        {
            return Failure("AR_ROLE_REQUIRED", "The Business Partner does not have a Customer role.");
        }

        if (role.Status != BusinessPartnerRoleStatus.Active)
        {
            return Failure("AR_ROLE_INACTIVE", "The Business Partner Customer role is inactive for new transactions.");
        }

        var profile = SelectEffective(profiles, role.Id, accountingDate);
        return profile is null
            ? Failure("AR_PROFILE_REQUIRED", $"No approved AR profile is effective on {accountingDate:dd/MM/yyyy}.")
            : new BusinessPartnerFinanceProfileReadiness(
                true,
                "READY",
                "The Business Partner has an approved AR profile for the accounting date.",
                ArProfile: profile);
    }

    public static bool HasApprovedOverlap(
        IEnumerable<BusinessPartnerApProfileVersion> profiles,
        Guid roleId,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        Guid? excludingProfileId = null) =>
        profiles.Any(x =>
            x.BusinessPartnerRoleId == roleId &&
            x.Status == BusinessPartnerFinanceProfileStatus.Approved &&
            x.Id != excludingProfileId &&
            x.EffectiveFrom.Date <= (effectiveTo ?? DateTime.MaxValue).Date &&
            (x.EffectiveTo ?? DateTime.MaxValue).Date >= effectiveFrom.Date);

    public static bool HasApprovedOverlap(
        IEnumerable<BusinessPartnerArProfileVersion> profiles,
        Guid roleId,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        Guid? excludingProfileId = null) =>
        profiles.Any(x =>
            x.BusinessPartnerRoleId == roleId &&
            x.Status == BusinessPartnerFinanceProfileStatus.Approved &&
            x.Id != excludingProfileId &&
            x.EffectiveFrom.Date <= (effectiveTo ?? DateTime.MaxValue).Date &&
            (x.EffectiveTo ?? DateTime.MaxValue).Date >= effectiveFrom.Date);

    private static BusinessPartnerApProfileVersion? SelectEffective(
        IEnumerable<BusinessPartnerApProfileVersion> profiles,
        Guid roleId,
        DateTime accountingDate) => profiles
        .Where(x =>
            x.BusinessPartnerRoleId == roleId &&
            x.Status == BusinessPartnerFinanceProfileStatus.Approved &&
            x.EffectiveFrom.Date <= accountingDate.Date &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= accountingDate.Date))
        .OrderByDescending(x => x.EffectiveFrom)
        .ThenByDescending(x => x.VersionNumber)
        .FirstOrDefault();

    private static BusinessPartnerArProfileVersion? SelectEffective(
        IEnumerable<BusinessPartnerArProfileVersion> profiles,
        Guid roleId,
        DateTime accountingDate) => profiles
        .Where(x =>
            x.BusinessPartnerRoleId == roleId &&
            x.Status == BusinessPartnerFinanceProfileStatus.Approved &&
            x.EffectiveFrom.Date <= accountingDate.Date &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Date >= accountingDate.Date))
        .OrderByDescending(x => x.EffectiveFrom)
        .ThenByDescending(x => x.VersionNumber)
        .FirstOrDefault();

    private static bool IsPartnerOperational(
        BusinessPartner partner,
        out BusinessPartnerFinanceProfileReadiness failure)
    {
        if (!partner.IsActive || partner.IsDeleted)
        {
            failure = Failure("BUSINESS_PARTNER_INACTIVE", "The Business Partner is inactive for new transactions.");
            return false;
        }

        if (partner.IsBlacklisted)
        {
            failure = Failure("BUSINESS_PARTNER_BLACKLISTED", "The Business Partner is blacklisted and cannot be used for a new transaction.");
            return false;
        }

        if (!string.Equals(partner.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            failure = Failure("BUSINESS_PARTNER_NOT_APPROVED", "The Business Partner must be approved before it can be used for a new transaction.");
            return false;
        }

        failure = null!;
        return true;
    }

    private static BusinessPartnerFinanceProfileReadiness Failure(string code, string message) =>
        new(false, code, message);
}
