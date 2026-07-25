using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class RecurringJournalRecurrenceCalculatorTests
{
    [Fact]
    public void SemiMonthly_ClampsMonthEndPresetInShortMonth()
    {
        var template = Template(RecurrenceFrequency.SemiMonthly, new RecurrenceRule(DaysOfMonth: [15, 31]));
        RecurringJournalRecurrenceCalculator.NextScheduledDate(template, new DateOnly(2027, 2, 15))
            .Should().Be(new DateOnly(2027, 2, 28));
    }

    [Fact]
    public void Monthly_PreservesMonthEndAcrossShortMonths()
    {
        var template = Template(RecurrenceFrequency.Monthly, new RecurrenceRule(LastCalendarDay: true));
        template.EffectiveFrom = new DateOnly(2027, 1, 31);
        RecurringJournalRecurrenceCalculator.NextScheduledDate(template, template.EffectiveFrom)
            .Should().Be(new DateOnly(2027, 2, 28));
    }

    [Fact]
    public void Custom_SupportsLastWeekdayOfMonth()
    {
        var template = Template(RecurrenceFrequency.Custom,
            new RecurrenceRule(NthWeek: -1, NthWeekday: (int)DayOfWeek.Friday));
        RecurringJournalRecurrenceCalculator.NextScheduledDate(template, new DateOnly(2027, 1, 1))
            .Should().Be(new DateOnly(2027, 1, 29));
    }

    [Fact]
    public async Task BusinessDayAdjustment_MovesForwardOverWeekendAndHoliday()
    {
        var calendar = new FakeCalendar(new DateOnly(2027, 3, 8));
        var result = await RecurringJournalRecurrenceCalculator.AdjustBusinessDayAsync(
            Guid.NewGuid(), new DateOnly(2027, 3, 6), BusinessDayConvention.NextBusinessDay, calendar);
        result.Should().Be(new DateOnly(2027, 3, 9));
    }

    private static RecurringJournalTemplate Template(RecurrenceFrequency frequency, RecurrenceRule rule) => new()
    {
        TenantId = Guid.NewGuid(), TemplateNumber = "RJ-001", Name = "Test", Frequency = frequency,
        EffectiveFrom = new DateOnly(2027, 1, 1), RecurrenceRuleJson = System.Text.Json.JsonSerializer.Serialize(rule)
    };

    private sealed class FakeCalendar(params DateOnly[] holidays) : IBusinessCalendarProvider
    {
        public Task<bool> IsBusinessDayAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date));
    }
}
