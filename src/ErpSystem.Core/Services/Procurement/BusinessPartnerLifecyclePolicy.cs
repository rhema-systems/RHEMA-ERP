using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Defines the canonical business-partner states that are eligible for
/// operational procurement use.
/// </summary>
public static class BusinessPartnerLifecyclePolicy
{
    public const string ApprovedApprovalStatus = "Approved";
    public const string ActiveRegistrationStatus = "Active";
    public const string LegacyApprovedRegistrationStatus = "Approved";

    public static bool IsApproved(string? approvalStatus) =>
        string.Equals(
            approvalStatus,
            ApprovedApprovalStatus,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsOperationalRegistration(string? registrationStatus) =>
        string.Equals(
            registrationStatus,
            ActiveRegistrationStatus,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            registrationStatus,
            LegacyApprovedRegistrationStatus,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsOperationallyApproved(BusinessPartner partner) =>
        partner.IsActive &&
        !partner.IsBlacklisted &&
        IsApproved(partner.ApprovalStatus) &&
        IsOperationalRegistration(partner.RegistrationStatus);
}
