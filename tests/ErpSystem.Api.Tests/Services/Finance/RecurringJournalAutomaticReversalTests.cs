using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class RecurringJournalAutomaticReversalTests
{
    [Fact]
    public async Task Processor_DoesNotReverseUntilOriginalIsPostedAndDue()
    {
        await using var db = CreateContext();
        var fixture = await SeedAsync(db, RecurringJournalStatus.Active, RecurringJournalOccurrenceStatus.Approved,
            new DateOnly(2026, 9, 2));
        var posting = new FakeSystemPostingEngine(fixture.JournalId);
        var processor = CreateProcessor(db, posting);

        var beforePosting = await processor.ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2));
        var occurrence = await db.RecurringJournalOccurrences.SingleAsync();
        occurrence.Status = RecurringJournalOccurrenceStatus.Posted;
        occurrence.ReversalDueDate = new DateOnly(2026, 9, 3);
        await db.SaveChangesAsync();
        var beforeDue = await processor.ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2));

        beforePosting.CandidateCount.Should().Be(0);
        beforeDue.CandidateCount.Should().Be(0);
        posting.PostCount.Should().Be(0);
    }

    [Theory]
    [InlineData(RecurringJournalOccurrenceStatus.PendingApproval)]
    [InlineData(RecurringJournalOccurrenceStatus.Approved)]
    // RejectOccurrenceAsync records a rejected occurrence as Failed.
    [InlineData(RecurringJournalOccurrenceStatus.Failed)]
    [InlineData(RecurringJournalOccurrenceStatus.SubmissionFailed)]
    public async Task Processor_NeverReversesAnUnpostedOccurrence(RecurringJournalOccurrenceStatus status)
    {
        await using var db = CreateContext();
        var fixture = await SeedAsync(db, RecurringJournalStatus.Active, status, new DateOnly(2026, 9, 2));
        var posting = new FakeSystemPostingEngine(fixture.JournalId);

        var result = await CreateProcessor(db, posting).ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2));

        result.CandidateCount.Should().Be(0);
        posting.PostCount.Should().Be(0);
    }

    [Theory]
    [InlineData(RecurringJournalStatus.Paused)]
    [InlineData(RecurringJournalStatus.Completed)]
    [InlineData(RecurringJournalStatus.Cancelled)]
    public async Task Processor_ReversesPostedAuthorisedOccurrence_IrrespectiveOfLaterTemplateState(
        RecurringJournalStatus templateStatus)
    {
        await using var db = CreateContext();
        var fixture = await SeedAsync(db, templateStatus, RecurringJournalOccurrenceStatus.Posted,
            new DateOnly(2026, 9, 2));
        var exchangeRateId = Guid.NewGuid();
        var dimensionSetId = Guid.NewGuid();
        var sourceLineId = Guid.NewGuid();
        var posting = new FakeSystemPostingEngine(fixture.JournalId,
        [
            new() { AccountId = Guid.NewGuid(), SourceDocumentLineId = sourceLineId, DebitAmount = 0m, CreditAmount = 1500m,
                TransactionCurrency = "USD", TransactionCreditAmount = 100m, ForeignCurrencyAmount = 100m,
                ExchangeRateId = exchangeRateId, ExchangeRate = 15m, ExchangeRateSource = "BOG", ExchangeRateDate = new DateTime(2026, 8, 31),
                FinanceDimensionSetId = dimensionSetId, SegmentString = "HQ-OPS", LineNumber = 1 },
            new() { AccountId = Guid.NewGuid(), DebitAmount = 500m, CreditAmount = 0m, LineNumber = 2 },
            new() { AccountId = Guid.NewGuid(), DebitAmount = 625m, CreditAmount = 0m, LineNumber = 3 },
            new() { AccountId = Guid.NewGuid(), DebitAmount = 375m, CreditAmount = 0m,
                TransactionCurrency = "USD", TransactionDebitAmount = 25m, ForeignCurrencyAmount = 25m,
                ExchangeRateId = exchangeRateId, ExchangeRate = 15m, ExchangeRateSource = "BOG",
                ExchangeRateDate = new DateTime(2026, 8, 31), FinanceDimensionSetId = dimensionSetId,
                SegmentString = "HQ-OPS", SourceDocumentLineId = Guid.NewGuid(), LineNumber = 4 }
        ]);
        var processor = CreateProcessor(db, posting);

        var result = await processor.ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2));

        result.PostedCount.Should().Be(1);
        posting.LastRequest.Should().NotBeNull();
        posting.LastRequest!.PostingDate.Should().Be(new DateTime(2026, 9, 2));
        posting.LastRequest.BookClassification.Should().Be("MANAGEMENT");
        posting.LastRequest.ReversalOfJournalEntryId.Should().Be(fixture.JournalId);
        posting.LastRequest.Lines[0].ExchangeRateId.Should().Be(exchangeRateId);
        posting.LastRequest.Lines[0].FinanceDimensionSetId.Should().Be(dimensionSetId);
        posting.LastRequest.Lines[0].SourceDocumentLineId.Should().Be(sourceLineId);
        posting.LastRequest.Lines[0].DebitAmount.Should().Be(0m);
        posting.LastRequest.Lines[0].CreditAmount.Should().Be(1500m);
        posting.LastRequest.Lines[0].TransactionCreditAmount.Should().Be(100m);
        posting.LastRequest.Lines.Should().HaveCount(4,
            "the authorised automatic reversal must retain every immutable generated line");
        posting.LastRequest.IdempotencyKey.Should().Be($"RecurringJournal:{fixture.TenantId:N}:{fixture.OccurrenceId:N}:AutoReverse");
        var occurrence = await db.RecurringJournalOccurrences.SingleAsync();
        occurrence.Status.Should().Be(RecurringJournalOccurrenceStatus.Posted);
        occurrence.ReversalStatus.Should().Be(RecurringJournalReversalStatus.Posted);
        occurrence.ReversalProcessedBy.Should().Be(RecurringJournalReversalProcessor.SystemActor);
        occurrence.ReversalJournalEntryId.Should().Be(posting.Result.JournalEntryId);
        occurrence.ReversalPostingEventId.Should().Be(posting.Result.PostingEventId);
    }

    [Fact]
    public async Task Processor_FailsClosedWithoutChangingOriginalOccurrenceStatus()
    {
        await using var db = CreateContext();
        var fixture = await SeedAsync(db, RecurringJournalStatus.Active, RecurringJournalOccurrenceStatus.Posted,
            new DateOnly(2026, 9, 2));
        var posting = new FakeSystemPostingEngine(fixture.JournalId) { Failure = new InvalidOperationException("Posting period is not open.") };

        var result = await CreateProcessor(db, posting).ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2));

        result.FailedCount.Should().Be(1);
        var occurrence = await db.RecurringJournalOccurrences.SingleAsync();
        occurrence.Status.Should().Be(RecurringJournalOccurrenceStatus.Posted);
        occurrence.ReversalStatus.Should().Be(RecurringJournalReversalStatus.Failed);
        occurrence.ReversalError.Should().Contain("Posting period is not open");
        occurrence.ReversalJournalEntryId.Should().BeNull();
    }

    [Fact]
    public async Task Retry_UsesSameIdempotencyAndLinksPostingCommittedBeforeOccurrenceUpdate()
    {
        await using var db = CreateContext();
        var fixture = await SeedAsync(db, RecurringJournalStatus.Active, RecurringJournalOccurrenceStatus.Posted,
            new DateOnly(2026, 9, 2));
        var posting = new FakeSystemPostingEngine(fixture.JournalId) { ReturnDuplicate = true };

        var first = await CreateProcessor(db, posting).ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2), fixture.OccurrenceId);
        var second = await CreateProcessor(db, posting).ProcessTenantAsync(fixture.TenantId, new DateOnly(2026, 9, 2), fixture.OccurrenceId);

        first.ExistingCount.Should().Be(1);
        second.CandidateCount.Should().Be(0);
        posting.PostCount.Should().Be(1);
        (await db.RecurringJournalOccurrences.SingleAsync()).ReversalJournalEntryId.Should().Be(posting.Result.JournalEntryId);
    }

    [Fact]
    public async Task Processor_IsTenantIsolated()
    {
        await using var db = CreateContext();
        var tenantA = await SeedAsync(db, RecurringJournalStatus.Active, RecurringJournalOccurrenceStatus.Posted,
            new DateOnly(2026, 9, 2));
        var tenantB = await SeedAsync(db, RecurringJournalStatus.Active, RecurringJournalOccurrenceStatus.Posted,
            new DateOnly(2026, 9, 2));
        var posting = new FakeSystemPostingEngine(tenantA.JournalId);

        var result = await CreateProcessor(db, posting).ProcessTenantAsync(tenantA.TenantId, new DateOnly(2026, 9, 2));

        result.CandidateCount.Should().Be(1);
        posting.PostCount.Should().Be(1);
        (await db.RecurringJournalOccurrences.SingleAsync(item => item.Id == tenantB.OccurrenceId))
            .ReversalJournalEntryId.Should().BeNull();
    }

    [Fact]
    public void RecoveryEndpoints_RequirePostJournalPermission()
    {
        foreach (var methodName in new[] { nameof(RecurringJournalController.ProcessDueReversals), nameof(RecurringJournalController.RetryReversal) })
            typeof(RecurringJournalController).GetMethod(methodName)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy
                .Should().Be(FinancePermissions.PostJournalEntries);
    }

    private static RecurringJournalReversalProcessor CreateProcessor(ApplicationDbContext db, IFinanceSystemPostingEngine posting) =>
        new(db, posting, Mock.Of<ILogger<RecurringJournalReversalProcessor>>());

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recurring-auto-reversal-{Guid.NewGuid()}").Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Guid TenantId, Guid OccurrenceId, Guid JournalId)> SeedAsync(
        ApplicationDbContext db, RecurringJournalStatus templateStatus, RecurringJournalOccurrenceStatus occurrenceStatus,
        DateOnly reversalDueDate)
    {
        var tenantId = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant", Code = $"T{Guid.NewGuid():N}"[..8], BaseCurrency = "GHS", Status = TenantStatus.Active });
        var template = new RecurringJournalTemplate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, TemplateNumber = "RJ-001", Name = "Accrual", Status = templateStatus,
            EffectiveFrom = new DateOnly(2026, 8, 31), Frequency = RecurrenceFrequency.Monthly, RecurrenceRuleJson = "{}"
        };
        db.RecurringJournalTemplates.Add(template);
        db.RecurringJournalOccurrences.Add(new RecurringJournalOccurrence
        {
            Id = occurrenceId, TenantId = tenantId, TemplateId = template.Id, Template = template,
            TemplateVersion = 1, SequenceNumber = 1, ScheduledDate = new DateOnly(2026, 8, 31), EffectiveDate = new DateOnly(2026, 8, 31),
            Status = occurrenceStatus, JournalEntryId = occurrenceStatus == RecurringJournalOccurrenceStatus.Posted ? journalId : null,
            ReversalDueDate = reversalDueDate, ReversalStatus = RecurringJournalReversalStatus.Scheduled,
            ReversalAuthorizedAt = DateTime.UtcNow.AddDays(-1), ReversalAuthorizedByUserId = Guid.NewGuid()
        });
        if (occurrenceStatus == RecurringJournalOccurrenceStatus.Posted)
            db.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SourceModule = "GL", OriginModuleCode = "FIN",
                SourceDocumentType = nameof(RecurringJournalOccurrence), SourceDocumentId = occurrenceId,
                PostingAction = "PostRecurringJournalOccurrence", JournalEntryId = journalId, PostingStatus = "Posted",
                PostingDate = new DateTime(2026, 8, 31), PostedAt = DateTime.UtcNow, FunctionalCurrencyCode = "GHS",
                BookClassification = "MANAGEMENT"
            });
        await db.SaveChangesAsync();
        return (tenantId, occurrenceId, journalId);
    }

    private sealed class FakeSystemPostingEngine(Guid originalJournalId, IReadOnlyList<FinancePostingLineDto>? lines = null) : IFinanceSystemPostingEngine
    {
        public FinancePostingRequestDto? LastRequest { get; private set; }
        public int PostCount { get; private set; }
        public bool ReturnDuplicate { get; set; }
        public Exception? Failure { get; set; }
        public FinancePostingResultDto Result { get; } = new() { JournalEntryId = Guid.NewGuid(), PostingEventId = Guid.NewGuid(), PostingDate = new DateTime(2026, 9, 2), PostingStatus = "Posted" };

        public Task<FinanceReversalPlanDto> GetReversalPlanAsync(Guid tenantId, Guid postingEventId, string reason,
            DateTime reversalDate, string systemActor, CancellationToken cancellationToken = default) => Task.FromResult(new FinanceReversalPlanDto
            {
                IsDefined = true, OriginalPostingEventId = postingEventId, OriginalJournalEntryId = originalJournalId,
                ReversalDate = reversalDate, Reason = reason,
                ReversalLines = lines ?? [new() { AccountId = Guid.NewGuid(), CreditAmount = 100m }, new() { AccountId = Guid.NewGuid(), DebitAmount = 100m }]
            });

        public Task<FinancePostingResultDto> PostAsync(Guid tenantId, FinancePostingRequestDto request, string systemActor,
            CancellationToken cancellationToken = default)
        {
            PostCount++;
            LastRequest = request;
            if (Failure is not null) throw Failure;
            Result.WasDuplicate = ReturnDuplicate;
            return Task.FromResult(Result);
        }
    }
}
