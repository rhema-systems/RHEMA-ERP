using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Finance;

public sealed class BusinessPartnerFinanceProfilePolicyTests
{
    [Fact]
    public void ResolveAp_UsesProfileEffectiveOnAccountingDate()
    {
        var partner = ApprovedPartner();
        var role = ActiveRole(BusinessPartnerRoleType.Supplier);
        var older = ApprovedApProfile(role.Id, 1, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30));
        var current = ApprovedApProfile(role.Id, 2, new DateTime(2026, 7, 1), null);

        var result = BusinessPartnerFinanceProfilePolicy.ResolveAp(
            partner,
            role,
            new[] { current, older },
            new DateTime(2026, 5, 15));

        result.IsReady.Should().BeTrue();
        result.ApProfile.Should().BeSameAs(older);
    }

    [Fact]
    public void ResolveAp_WhtPartnerWithoutTin_FailsWithActionableReason()
    {
        var partner = ApprovedPartner();
        partner.TaxIdentificationNumber = null;
        var role = ActiveRole(BusinessPartnerRoleType.Supplier);
        var profile = ApprovedApProfile(role.Id, 1, new DateTime(2026, 1, 1), null);
        profile.SubjectToWithholding = true;
        profile.WithholdingDefaults.Add(new BusinessPartnerApWhtDefault
        {
            IsDefaultForAp = true,
            IsActive = true,
            CategoryCode = "SERVICES"
        });

        var result = BusinessPartnerFinanceProfilePolicy.ResolveAp(
            partner,
            role,
            new[] { profile },
            new DateTime(2026, 9, 24));

        result.IsReady.Should().BeFalse();
        result.Code.Should().Be("AP_WHT_TIN_REQUIRED");
        result.Message.Should().Contain("TIN");
    }

    [Fact]
    public void ResolveAr_InactiveRole_RemainsVisibleButIsNotReadyForNewActivity()
    {
        var partner = ApprovedPartner();
        var role = ActiveRole(BusinessPartnerRoleType.Customer);
        role.Status = BusinessPartnerRoleStatus.Inactive;

        var result = BusinessPartnerFinanceProfilePolicy.ResolveAr(
            partner,
            role,
            Array.Empty<BusinessPartnerArProfileVersion>(),
            new DateTime(2026, 9, 24));

        result.IsReady.Should().BeFalse();
        result.Code.Should().Be("AR_ROLE_INACTIVE");
    }

    [Fact]
    public void ApprovedProfileOverlap_IsRejected()
    {
        var roleId = Guid.NewGuid();
        var existing = ApprovedApProfile(roleId, 1, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var overlaps = BusinessPartnerFinanceProfilePolicy.HasApprovedOverlap(
            new[] { existing },
            roleId,
            new DateTime(2026, 6, 1),
            null);

        overlaps.Should().BeTrue();
    }

    private static BusinessPartner ApprovedPartner() => new()
    {
        Id = Guid.NewGuid(),
        PartnerCode = "BP-001",
        PartnerName = "Canonical Partner",
        RegistrationStatus = "Approved",
        IsActive = true,
        TaxIdentificationNumber = "TIN-001"
    };

    private static BusinessPartnerRole ActiveRole(BusinessPartnerRoleType roleType) => new()
    {
        Id = Guid.NewGuid(),
        RoleType = roleType,
        Status = BusinessPartnerRoleStatus.Active
    };

    private static BusinessPartnerApProfileVersion ApprovedApProfile(
        Guid roleId,
        int version,
        DateTime from,
        DateTime? to) => new()
    {
        Id = Guid.NewGuid(),
        BusinessPartnerRoleId = roleId,
        VersionNumber = version,
        Status = BusinessPartnerFinanceProfileStatus.Approved,
        EffectiveFrom = from,
        EffectiveTo = to
    };
}
