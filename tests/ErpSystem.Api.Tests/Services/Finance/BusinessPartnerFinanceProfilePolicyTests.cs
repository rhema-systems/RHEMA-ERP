using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BusinessPartnerFinanceProfilePolicyTests
{
    [Fact]
    public void ResolveAp_AcceptsWorkflowApprovedActivePartner()
    {
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(),
            RoleType = BusinessPartnerRoleType.Supplier,
            Status = BusinessPartnerRoleStatus.Active
        };
        var partner = new BusinessPartner
        {
            RegistrationStatus = "Active",
            ApprovalStatus = "Approved",
            IsActive = true
        };
        var profile = new BusinessPartnerApProfileVersion
        {
            BusinessPartnerRoleId = role.Id,
            Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2026, 1, 1),
            SubjectToWithholding = false
        };

        var result = BusinessPartnerFinanceProfilePolicy.ResolveAp(
            partner, role, [profile], new DateTime(2026, 9, 27));

        result.IsReady.Should().BeTrue();
        result.Code.Should().Be("READY");
        result.ApProfile.Should().BeSameAs(profile);
    }

    [Theory]
    [InlineData("PendingApproval", "Pending")]
    [InlineData("Active", "Pending")]
    [InlineData("PendingApproval", "Approved")]
    public void ResolveAp_RejectsPartnerWithoutCompletedIdentityApproval(
        string registrationStatus,
        string approvalStatus)
    {
        var partner = new BusinessPartner
        {
            RegistrationStatus = registrationStatus,
            ApprovalStatus = approvalStatus,
            IsActive = true
        };

        var result = BusinessPartnerFinanceProfilePolicy.ResolveAp(
            partner, null, [], new DateTime(2026, 9, 27));

        result.IsReady.Should().BeFalse();
        result.Code.Should().Be("BUSINESS_PARTNER_NOT_APPROVED");
    }

    [Fact]
    public void ResolveAr_Uses_end_dated_superseded_profile_for_historical_accounting_date()
    {
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(),
            RoleType = BusinessPartnerRoleType.Customer,
            Status = BusinessPartnerRoleStatus.Active
        };
        var partner = new BusinessPartner
        {
            RegistrationStatus = "Active",
            ApprovalStatus = "Approved",
            IsActive = true
        };
        var historical = new BusinessPartnerArProfileVersion
        {
            BusinessPartnerRoleId = role.Id,
            VersionNumber = 1,
            Status = BusinessPartnerFinanceProfileStatus.Superseded,
            EffectiveFrom = new DateTime(2026, 1, 1),
            EffectiveTo = new DateTime(2026, 10, 1),
            CreditLimit = 500m
        };
        var current = new BusinessPartnerArProfileVersion
        {
            BusinessPartnerRoleId = role.Id,
            VersionNumber = 2,
            Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = new DateTime(2026, 10, 2),
            CreditLimit = 5_000m
        };

        var result = BusinessPartnerFinanceProfilePolicy.ResolveAr(
            partner, role, new[] { current, historical }, new DateTime(2026, 9, 30));

        result.IsReady.Should().BeTrue();
        result.ArProfile.Should().BeSameAs(historical);
    }
}
