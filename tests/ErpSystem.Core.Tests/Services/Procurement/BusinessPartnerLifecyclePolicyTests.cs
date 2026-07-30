using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerLifecyclePolicyTests
{
    [Theory]
    [InlineData("Approved", "Active", true)]
    [InlineData("approved", "active", true)]
    [InlineData("Approved", "Approved", true)]
    [InlineData("Pending", "Active", false)]
    [InlineData("Approved", "PendingApproval", false)]
    public void OperationalApprovalRecognizesCanonicalAndLegacyStates(
        string approvalStatus,
        string registrationStatus,
        bool expected)
    {
        var partner = new BusinessPartner
        {
            IsActive = true,
            IsBlacklisted = false,
            ApprovalStatus = approvalStatus,
            RegistrationStatus = registrationStatus
        };

        BusinessPartnerLifecyclePolicy.IsOperationallyApproved(partner)
            .Should().Be(expected);
    }
}
