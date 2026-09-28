using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.Entities.Estate;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesLeaseReminderTests
{
    [Fact]
    public void LeaseEndDate_uses_full_term_and_clamps_month_end()
    {
        var asset = new EstateManagedAsset
        {
            DateOfTenancy = new DateTime(2025, 1, 31),
            ExternalLeaseTermMonths = 13,
            LeaseTermYears = 50
        };

        Assert.Equal(new DateTime(2026, 2, 28), FacilitiesLeaseReminderService.LeaseEndDate(asset));
    }

    [Theory]
    [InlineData(91, null)]
    [InlineData(90, "90-day")]
    [InlineData(30, "30-day")]
    [InlineData(7, "7-day")]
    [InlineData(-1, "expired")]
    public void ReminderBand_selects_one_milestone(int days, string? expected)
    {
        var today = new DateTime(2026, 9, 25);
        Assert.Equal(expected, FacilitiesLeaseReminderService.ReminderBand(today.AddDays(days), today));
    }
}
