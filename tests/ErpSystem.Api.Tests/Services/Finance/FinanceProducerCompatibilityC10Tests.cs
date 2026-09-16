using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerCompatibilityC10Tests
{
    [Fact]
    public void ExecutionResults_ExposeBoundCompatibilityEvidenceWithoutBookSelectionSurface()
    {
        foreach (var type in new[]
                 {
                     typeof(FinanceProducerApprovedExecutionResult),
                     typeof(FinanceProducerIntentGroupApprovedExecutionResult),
                     typeof(FinanceProducerIntentGroupMemberExecutionResult)
                 })
        {
            type.GetProperties().Select(property => property.Name)
                .Should().NotContain(name => name.Contains("Book", StringComparison.OrdinalIgnoreCase));
            type.GetProperties().Select(property => property.PropertyType)
                .Should().NotContain(typeof(AccountingEventDto));
        }
    }

    [Fact]
    public async Task MultiBookEvent_ReturnsOnlyExistingDefaultBookCompatibilityRepresentation()
    {
        await using var db = Db();
        var tenantId = Guid.NewGuid();
        var secondary = Book(tenantId, "LOCAL", isDefault: false, AccountingBookType.ParallelFull);
        var primary = Book(tenantId, "IFRS", isDefault: true, AccountingBookType.PrimaryFull);
        db.AccountingBooks.AddRange(secondary, primary);
        await db.SaveChangesAsync();
        var primaryPosting = Posting(primary.Id, order: 2);
        var accountingEvent = Event(Posting(secondary.Id, order: 1), primaryPosting);

        var result = await FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId, accountingEvent);

        result.AccountingEventId.Should().Be(accountingEvent.Id);
        result.AccountingEventRequestFingerprint.Should().Be(accountingEvent.RequestFingerprint);
        result.FinancePostingEventId.Should().Be(primaryPosting.FinancePostingEventId!.Value);
        result.JournalEntryId.Should().Be(primaryPosting.JournalEntryId!.Value);
    }

    [Fact]
    public async Task ExactRetry_ReturnsTheSameFrozenRepresentationEvidence()
    {
        await using var db = Db();
        var tenantId = Guid.NewGuid();
        var primary = Book(tenantId, "IFRS", isDefault: true, AccountingBookType.PrimaryFull);
        db.AccountingBooks.Add(primary);
        await db.SaveChangesAsync();
        var accountingEvent = Event(Posting(primary.Id, order: 1));

        var first = await FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId, accountingEvent);
        var retry = await FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId, accountingEvent);

        retry.Should().Be(first);
    }

    [Fact]
    public async Task Reversal_ReturnsOriginalFrozenDefaultRepresentationAfterLifecycleDrift()
    {
        await using var db = Db();
        var tenantId = Guid.NewGuid();
        var primary = Book(tenantId, "IFRS", isDefault: true, AccountingBookType.PrimaryFull);
        primary.IsActive = false;
        primary.AllowsPosting = false;
        var parallel = Book(tenantId, "LOCAL", isDefault: false, AccountingBookType.ParallelFull);
        db.AccountingBooks.AddRange(primary, parallel);
        await db.SaveChangesAsync();
        var primaryPosting = Posting(primary.Id, order: 1);
        var reversal = Event(primaryPosting, Posting(parallel.Id, order: 2));
        reversal.EventKind = AccountingEventKinds.Reversal;

        var result = await FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId, reversal);

        result.FinancePostingEventId.Should().Be(primaryPosting.FinancePostingEventId!.Value);
        result.JournalEntryId.Should().Be(primaryPosting.JournalEntryId!.Value);
    }

    [Fact]
    public async Task MissingOrAmbiguousDefaultAuthority_FailsClosed()
    {
        var tenantId = Guid.NewGuid();
        await using (var absent = Db())
        {
            var action = () => FinanceProducerCompatibilityAuthority.ResolveAsync(absent, tenantId,
                Event(Posting(Guid.NewGuid(), order: 1)));
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("PRIMARY_BOOK_AUTHORITY_AMBIGUOUS*");
        }

        await using (var ambiguous = Db())
        {
            var first = Book(tenantId, "IFRS", true, AccountingBookType.PrimaryFull);
            var second = Book(tenantId, "IFRS2", true, AccountingBookType.PrimaryFull);
            ambiguous.AccountingBooks.AddRange(first, second);
            await ambiguous.SaveChangesAsync();
            var action = () => FinanceProducerCompatibilityAuthority.ResolveAsync(ambiguous, tenantId,
                Event(Posting(first.Id, order: 1), Posting(second.Id, order: 2)));
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("PRIMARY_BOOK_AUTHORITY_AMBIGUOUS*");
        }
    }

    [Fact]
    public async Task AbsentDuplicateFailedOrIncompleteRepresentation_FailsClosed()
    {
        await using var db = Db();
        var tenantId = Guid.NewGuid();
        var primary = Book(tenantId, "IFRS", true, AccountingBookType.PrimaryFull);
        db.AccountingBooks.Add(primary);
        await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId,
                Event(Posting(Guid.NewGuid(), order: 1))))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_COMPATIBILITY_REPRESENTATION_ABSENT*");

        await FluentActions.Awaiting(() => FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId,
                Event(Posting(primary.Id, order: 1), Posting(primary.Id, order: 2))))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_COMPATIBILITY_REPRESENTATION_AMBIGUOUS*");

        var failed = Posting(primary.Id, order: 1);
        failed.Status = AccountingEventStatuses.Failed;
        await FluentActions.Awaiting(() => FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId, Event(failed)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_COMPATIBILITY_REPRESENTATION_INCOMPLETE*");

        var incomplete = Posting(primary.Id, order: 1);
        incomplete.JournalEntryId = null;
        await FluentActions.Awaiting(() => FinanceProducerCompatibilityAuthority.ResolveAsync(db, tenantId, Event(incomplete)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_COMPATIBILITY_REPRESENTATION_INCOMPLETE*");
    }

    [Fact]
    public async Task GroupResult_IsBoundToDistinctImmutableMemberOrderAndIdentity()
    {
        await using var db = Db();
        var tenantId = Guid.NewGuid();
        var primary = Book(tenantId, "IFRS", true, AccountingBookType.PrimaryFull);
        db.AccountingBooks.Add(primary);
        await db.SaveChangesAsync();
        var first = Event(Posting(primary.Id, order: 1));
        var second = Event(Posting(primary.Id, order: 1));
        var group = new ProducerIntentGroupDto
        {
            Id = Guid.NewGuid(), Status = ProducerIntentGroupStatuses.Posted,
            Members =
            [
                new ProducerIntentGroupMemberDto { MemberOrder = 2, MemberFingerprint = new string('B', 64), AccountingEvent = second },
                new ProducerIntentGroupMemberDto { MemberOrder = 1, MemberFingerprint = new string('A', 64), AccountingEvent = first }
            ]
        };

        var result = await FinanceProducerCompatibilityAuthority.ResolveGroupAsync(db, tenantId, group);

        result.ProducerIntentGroupId.Should().Be(group.Id);
        result.GroupFingerprint.Should().Be(group.GroupFingerprint);
        result.Status.Should().Be(ProducerIntentGroupStatuses.Posted);
        result.Members.Select(member => member.MemberOrder).Should().Equal(1, 2);
        result.Members.Select(member => member.MemberFingerprint).Should().Equal(new string('A', 64), new string('B', 64));
        result.Members.Select(member => member.AccountingEventId).Should().Equal(first.Id, second.Id);

        group.Members =
        [
            new ProducerIntentGroupMemberDto { MemberOrder = 1, MemberFingerprint = new string('A', 64), AccountingEvent = first },
            new ProducerIntentGroupMemberDto { MemberOrder = 1, MemberFingerprint = new string('B', 64), AccountingEvent = second }
        ];
        await FluentActions.Awaiting(() => FinanceProducerCompatibilityAuthority.ResolveGroupAsync(db, tenantId, group))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PRODUCER_COMPATIBILITY_GROUP_MEMBER_CONFLICT*");
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AccountingBook Book(Guid tenantId, string code, bool isDefault, AccountingBookType type) => new()
    {
        TenantId = tenantId, Code = code, Name = code, BookType = type, IsDefault = isDefault,
        IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS"
    };

    private static AccountingEventDto Event(params AccountingEventPostingDto[] postings) => new()
    {
        Id = Guid.NewGuid(), Status = AccountingEventStatuses.Posted,
        RequestFingerprint = Guid.NewGuid().ToString("N").PadRight(64, 'A'), Postings = postings
    };

    private static AccountingEventPostingDto Posting(Guid bookId, int order) => new()
    {
        AccountingBookId = bookId, AccountingBookCode = $"BOOK_{order}", SelectionOrder = order,
        Status = AccountingEventStatuses.Posted, FinancePostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid()
    };
}
