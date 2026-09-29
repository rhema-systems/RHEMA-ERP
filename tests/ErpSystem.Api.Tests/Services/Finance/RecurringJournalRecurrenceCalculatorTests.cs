using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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
    public void Monthly_LastBusinessDay_SkipsWeekendAtMonthEnd()
    {
        var template = Template(RecurrenceFrequency.Monthly, new RecurrenceRule(LastBusinessDay: true));
        RecurringJournalRecurrenceCalculator.NextScheduledDate(template, new DateOnly(2027, 7, 1))
            .Should().Be(new DateOnly(2027, 7, 30));
    }

    [Fact]
    public void SemiMonthly_RespectsMonthInterval()
    {
        var template = Template(RecurrenceFrequency.SemiMonthly, new RecurrenceRule(DaysOfMonth: [1, 15]));
        template.Interval = 2;
        RecurringJournalRecurrenceCalculator.NextScheduledDate(template, new DateOnly(2027, 1, 15))
            .Should().Be(new DateOnly(2027, 3, 1));
    }

    [Fact]
    public async Task BusinessDayAdjustment_MovesForwardOverWeekendAndHoliday()
    {
        var calendar = new FakeCalendar(new DateOnly(2027, 3, 8));
        var result = await RecurringJournalRecurrenceCalculator.AdjustBusinessDayAsync(
            Guid.NewGuid(), new DateOnly(2027, 3, 6), BusinessDayConvention.NextBusinessDay, calendar);
        result.Should().Be(new DateOnly(2027, 3, 9));
    }

    [Fact]
    [Trait("Requirement", "FR-GL-006")]
    public async Task DueProcessor_ShouldCreateOnePendingOccurrenceWithoutPostingMoney()
    {
        var tenantId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 31);
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.RecurringJournalTemplates.Add(new RecurringJournalTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateNumber = "RJ-2026-00001",
            Name = "Monthly rates accrual",
            JournalType = "Recurring",
            BookClassification = "IFRS",
            CurrencyCode = "GHS",
            DefinitionKey = Guid.NewGuid(),
            Version = 1,
            Status = RecurringJournalStatus.Active,
            EffectiveFrom = dueDate,
            EndDate = dueDate,
            Frequency = RecurrenceFrequency.Monthly,
            RecurrenceRuleJson = "{\"lastCalendarDay\":true}",
            BusinessDayConvention = BusinessDayConvention.PreviousBusinessDay,
            NextDueDate = dueDate,
            CreatedById = Guid.NewGuid(),
            Lines =
            [
                new() { Id = Guid.NewGuid(), TenantId = tenantId, LineNumber = 1, AccountId = Guid.NewGuid(), IsDebit = true, FixedAmount = 1500m, DimensionValuesJson = "{\"DEPT\":\"FIN\"}" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, LineNumber = 2, AccountId = Guid.NewGuid(), IsDebit = true, FixedAmount = 1000m },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, LineNumber = 3, AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 2000m },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, LineNumber = 4, AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 500m }
            ]
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(service => service.StartApprovalWorkflowAsAsync(
                nameof(RecurringJournalOccurrence), It.IsAny<Guid>(), It.IsAny<Guid>(), tenantId))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var processor = new RecurringJournalGenerationProcessor(
            db,
            new FakeCalendar(),
            audit.Object,
            workflow.Object,
            Mock.Of<ILogger<RecurringJournalGenerationProcessor>>());

        var first = await processor.ProcessTenantAsync(tenantId, dueDate, "test-scheduler");
        var second = await processor.ProcessTenantAsync(tenantId, dueDate, "test-scheduler");

        first.GeneratedCount.Should().Be(1);
        second.GeneratedCount.Should().Be(0);
        (await db.RecurringJournalOccurrences.CountAsync()).Should().Be(1,
            "retries and multiple API nodes must not duplicate a scheduled accounting event");
        var occurrence = await db.RecurringJournalOccurrences.SingleAsync();
        occurrence.Status.Should().Be(RecurringJournalOccurrenceStatus.PendingApproval);
        occurrence.WorkflowInstanceId.Should().NotBeNull("every generated occurrence must enter the shared approval workbench");
        occurrence.JournalEntryId.Should().BeNull("the scheduler may prepare work but must never post money");
        occurrence.TemplateSnapshotJson.Should().Contain("Monthly rates accrual");
        occurrence.TemplateSnapshotJson.Should().Contain("DEPT");
        occurrence.TemplateSnapshotJson.Should().Contain("sourceLineId");
        occurrence.TemplateSnapshotJson.Split("sourceLineId").Should().HaveCount(5,
            "all four immutable source-line identities must be frozen into the occurrence");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        audit.Verify(service => service.RecordAsync(
            It.Is<FinanceAuditEventDto>(item => item.EventType == FinanceAuditEvents.RecurringJournalOccurrenceGenerated),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RecurringJournalTemplate Template(RecurrenceFrequency frequency, RecurrenceRule rule) => new()
    {
        TenantId = Guid.NewGuid(), TemplateNumber = "RJ-001", Name = "Test", Frequency = frequency,
        EffectiveFrom = new DateOnly(2027, 1, 1), RecurrenceRuleJson = System.Text.Json.JsonSerializer.Serialize(rule)
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class FakeCalendar(params DateOnly[] holidays) : IBusinessCalendarProvider
    {
        public Task<bool> IsBusinessDayAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date));
    }
}
