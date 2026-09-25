using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.Entities.Estate;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesDutyRosterScheduleTests
{
    [Theory]
    [InlineData("Mon-Fri", "2026-09-23", true)]
    [InlineData("Mon-Fri", "2026-09-26", false)]
    [InlineData("Sat", "2026-09-26", true)]
    [InlineData("Mon, Wed, Fri", "2026-09-24", false)]
    public void DayPattern_SelectsOnlyScheduledDays(string pattern, string date, bool expected)
    {
        var item = new EstateFacilityDutyRoster
        {
            Frequency = "Daily",
            DayPattern = pattern,
            StartDate = new DateTime(2026, 9, 1)
        };

        FacilitiesDutyRosterController.IsScheduledOn(item, DateTime.Parse(date)).Should().Be(expected);
    }

    [Fact]
    public void OneOffDuty_DoesNotRepeat()
    {
        var item = new EstateFacilityDutyRoster
        {
            Frequency = "One-off",
            StartDate = new DateTime(2026, 9, 23)
        };

        FacilitiesDutyRosterController.IsScheduledOn(item, new DateTime(2026, 9, 23)).Should().BeTrue();
        FacilitiesDutyRosterController.IsScheduledOn(item, new DateTime(2026, 9, 24)).Should().BeFalse();
    }

    [Fact]
    public void ShiftEnd_MustBeAfterStart()
    {
        var request = new UpsertEstateFacilityDutyRosterDto
        {
            StaffName = "Cleaner",
            ServiceAreaName = "Block A",
            Frequency = "Daily",
            DayPattern = "Mon-Fri",
            ShiftStart = "17:00",
            ShiftEnd = "08:00"
        };

        FacilitiesDutyRosterController.ValidateRequest(request).Should().Contain("end time after");
    }
}
