using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FiscalYearCloseTests
{
    [Fact]
    public async Task Close_TransfersOnlyBaseIncomeAndDoesNotReplicateItsClosingJournal()
    {
        var f = await FixtureWithPostedActivityAsync();
        var parallel = await AddBookActivityAsync(f, "USD_PARALLEL", AccountingBookType.ParallelFull, 200m, "USD");
        var delta = await AddBookActivityAsync(f, "IFRS_ADJUSTMENTS", AccountingBookType.Delta, 30m, "GHS");
        var result = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        result.Success.Should().BeTrue();
        (await f.Db.AccountingBookPeriods.CountAsync()).Should().Be(0);
        result.NetIncomeTransferred.Should().Be(100m);
        result.AccountingBookId.Should().Be(f.Book.Id);
        var cycle = await f.Db.YearEndBookCloseCycles.SingleAsync();
        cycle.AccountingBookCode.Should().Be("BASE");
        cycle.FunctionalCurrencyCode.Should().Be("GHS");
        cycle.PeriodAuthoritySnapshotJson.Should().Contain("TENANT-FISCAL-PERIOD-CLOSE-V2")
            .And.Contain("FinanceCloseCycleId").And.Contain("ApprovedByUserId");
        f.FiscalYear.IsClosed.Should().BeFalse();
        f.FiscalYear.ClosingJournalEntryId.Should().BeNull();
        var journal = await f.Db.JournalEntries.Include(x => x.Transactions).SingleAsync(x => x.Id == result.ClosingJournalEntryId);
        journal.AccountingBookId.Should().Be(f.Book.Id);
        journal.Transactions.Should().HaveCount(3).And.OnlyContain(x => x.AccountingBookId == f.Book.Id);
        journal.Transactions.Single(x => x.AccountId == f.Revenue.Id).DebitAmount.Should().Be(140m);
        journal.Transactions.Single(x => x.AccountId == f.Expense.Id).CreditAmount.Should().Be(40m);
        journal.Transactions.Single(x => x.AccountId == f.RetainedEarnings.Id).CreditAmount.Should().Be(100m);
        (await IncomeAsync(f, f.Book.Id)).Should().Be(0m);
        (await IncomeAsync(f, parallel.Id)).Should().Be(200m);
        (await IncomeAsync(f, delta.Id)).Should().Be(30m);
        (await f.Db.JournalEntries.CountAsync(x => x.ReplicatedFromJournalEntryId == journal.Id)).Should().Be(0);
        (await f.Db.AccountBalances.Where(x => x.AccountingBookId == f.Book.Id && x.AccountId == f.Revenue.Id)
            .SumAsync(x => x.PeriodCredits - x.PeriodDebits)).Should().Be(0m);
    }

    [Fact]
    public async Task ParallelBook_ClosesIndependentlyInItsOwnCurrency()
    {
        var f = await FixtureWithPostedActivityAsync();
        var parallel = await AddBookActivityAsync(f, "USD_PARALLEL", AccountingBookType.ParallelFull, 200m, "USD");
        await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        var request = CloseRequest(f, "parallel-1"); request.AccountingBookId = parallel.Id;
        var result = await f.GlService.CloseFiscalYearAsync(request);
        result.NetIncomeTransferred.Should().Be(200m);
        (await IncomeAsync(f, parallel.Id)).Should().Be(0m);
        (await IncomeAsync(f, f.Book.Id)).Should().Be(0m);
        var posting = await f.Db.FinancePostingEvents.SingleAsync(x => x.JournalEntryId == result.ClosingJournalEntryId);
        posting.BookClassification.Should().Be("USD_PARALLEL");
        posting.FunctionalCurrencyCode.Should().Be("USD");
        (await f.Db.YearEndBookCloseCycles.CountAsync()).Should().Be(2);
        var reopened = await f.GlService.ReopenFiscalYearAsync(f.FiscalYear.Id, new FiscalYearReopenRequestDto
        {
            AccountingBookId = parallel.Id, BookCloseCycleId = result.BookCloseCycleId!.Value,
            Reason = "Correct the parallel book's independently closed year."
        });
        var reversal = await f.Db.FinancePostingEvents.SingleAsync(x => x.JournalEntryId == reopened.ReversalJournalEntryId);
        reversal.AccountingBookId.Should().Be(parallel.Id);
        reversal.BookClassification.Should().Be("USD_PARALLEL");
        reversal.FunctionalCurrencyCode.Should().Be("USD");
        (await IncomeAsync(f, parallel.Id)).Should().Be(200m);
        (await IncomeAsync(f, f.Book.Id)).Should().Be(0m);
    }

    [Fact]
    public async Task Close_PreservesFrozenCodingAndTransfersEachDimensionBucketIntoEquity()
    {
        var f = await FixtureWithPostedActivityAsync();
        var revenue = await f.Db.AccountTransactions.SingleAsync(x => x.AccountId == f.Revenue.Id);
        var expense = await f.Db.AccountTransactions.SingleAsync(x => x.AccountId == f.Expense.Id);
        var revenueSet = await AttachFrozenCodingAsync(f, revenue, "A");
        var expenseSet = await AttachFrozenCodingAsync(f, expense, "B");
        var result = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        var journal = await f.Db.JournalEntries.Include(x => x.Transactions)
            .ThenInclude(x => x.FinanceDimensionSnapshot)!.ThenInclude(x => x!.Items)
            .SingleAsync(x => x.Id == result.ClosingJournalEntryId);
        journal.Transactions.Should().HaveCount(4);
        journal.Transactions.Single(x => x.AccountId == f.Revenue.Id).FinanceDimensionSetId.Should().Be(revenueSet);
        journal.Transactions.Single(x => x.AccountId == f.Expense.Id).FinanceDimensionSetId.Should().Be(expenseSet);
        var earnings = journal.Transactions.Where(x => x.AccountId == f.RetainedEarnings.Id).ToList();
        earnings.Single(x => x.FinanceDimensionSetId == revenueSet).CreditAmount.Should().Be(140m);
        earnings.Single(x => x.FinanceDimensionSetId == expenseSet).DebitAmount.Should().Be(40m);
        journal.Transactions.Should().OnlyContain(x => x.FinanceDimensionSnapshot != null
            && x.FinanceDimensionSnapshot.Items.All(item => item.DimensionValueNameSnapshot.StartsWith("Original ")));
        journal.Transactions.Single(x => x.AccountId == f.Revenue.Id).SegmentString.Should().Be("CC=A");
        foreach (var accountId in new[] { f.Revenue.Id, f.Expense.Id })
        {
            var buckets = (await f.Db.AccountTransactions.Where(x => x.AccountingBookId == f.Book.Id
                && x.AccountId == accountId).ToListAsync())
                .GroupBy(x => new { x.AccountId, x.FinanceDimensionSetId, x.SegmentString });
            buckets.Should().OnlyContain(bucket => bucket.Sum(x => x.CreditAmount - x.DebitAmount) == 0m);
        }
        await f.GlService.ReopenFiscalYearAsync(f.FiscalYear.Id, new FiscalYearReopenRequestDto
        {
            AccountingBookId = f.Book.Id, BookCloseCycleId = result.BookCloseCycleId!.Value,
            Reason = "Restore the original coded income for a correction."
        });
        (await IncomeAsync(f, f.Book.Id)).Should().Be(100m);
        var second = await f.GlService.CloseFiscalYearAsync(CloseRequest(f, "coded-cycle-2"));
        second.NetIncomeTransferred.Should().Be(100m);
        (await IncomeAsync(f, f.Book.Id)).Should().Be(0m);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("account")]
    [InlineData("dimensions")]
    [InlineData("segment")]
    public async Task YearEndLeaf_RejectsTamperedClosingBalancePlan(string defect)
    {
        var f = await FixtureWithPostedActivityAsync(mutateClose: request =>
        {
            var line = request.Lines[0];
            if (defect == "amount") line.DebitAmount += 1m;
            if (defect == "account") line.AccountId = Guid.NewGuid();
            if (defect == "dimensions") line.FinanceDimensionSetId = Guid.NewGuid();
            if (defect == "segment") line.SegmentString = "FORGED";
        });
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(CloseRequest(f)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*exact frozen nominal-balance plan*");
        (await f.Db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "YearEndClose")).Should().Be(0);
    }

    [Fact]
    public async Task CloseRetry_ReturnsOriginalAndChangedAuthorityConflicts()
    {
        var f = await FixtureWithPostedActivityAsync();
        var request = CloseRequest(f);
        var first = await f.GlService.CloseFiscalYearAsync(request);
        var retry = await f.GlService.CloseFiscalYearAsync(request);
        retry.BookCloseCycleId.Should().Be(first.BookCloseCycleId);
        retry.ClosingJournalEntryId.Should().Be(first.ClosingJournalEntryId);
        (await f.Db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "YearEndClose")).Should().Be(1);
        request.AccountingBookId = Guid.NewGuid();
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(request)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*idempotency conflict*");
    }

    [Fact]
    public async Task ReopenAndReclose_PreserveOriginalEvidenceAndCreateAnotherCycle()
    {
        var f = await FixtureWithPostedActivityAsync();
        var parallel = await AddBookActivityAsync(f, "USD_PARALLEL", AccountingBookType.ParallelFull, 200m, "USD");
        var first = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        var reopen = new FiscalYearReopenRequestDto { AccountingBookId = f.Book.Id, BookCloseCycleId = first.BookCloseCycleId!.Value,
            Reason = "Late supplier invoices require the year-end correction." };
        (await f.GlService.ReopenFiscalYearAsync(f.FiscalYear.Id, reopen)).Success.Should().BeTrue();
        var firstCycle = await f.Db.YearEndBookCloseCycles.SingleAsync();
        firstCycle.Status.Should().Be("Reopened");
        firstCycle.ClosingJournalEntryId.Should().Be(first.ClosingJournalEntryId);
        firstCycle.ReversalJournalEntryId.Should().NotBeNull();
        (await IncomeAsync(f, f.Book.Id)).Should().Be(100m);
        (await IncomeAsync(f, parallel.Id)).Should().Be(200m);
        var retry = await f.GlService.ReopenFiscalYearAsync(f.FiscalYear.Id, reopen);
        retry.ReversalJournalEntryId.Should().Be(firstCycle.ReversalJournalEntryId);
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(CloseRequest(f))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*new key*");
        var second = await f.GlService.CloseFiscalYearAsync(CloseRequest(f, "cycle-2"));
        second.ClosingJournalEntryId.Should().NotBe(first.ClosingJournalEntryId!.Value);
        second.NetIncomeTransferred.Should().Be(100m);
        (await f.Db.YearEndBookCloseCycles.MaxAsync(x => x.CycleNumber)).Should().Be(2);
        (await IncomeAsync(f, f.Book.Id)).Should().Be(0m);
        (await f.Db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "YearEndCloseReversal")).Should().Be(1);
        var reverse = await f.Db.JournalEntries.Include(x => x.Transactions).SingleAsync(x => x.Id == firstCycle.ReversalJournalEntryId);
        reverse.OriginalJournalEntryId.Should().Be(first.ClosingJournalEntryId);
        reverse.Transactions.Should().OnlyContain(x => x.FunctionalCurrencyCode == "GHS" && x.AccountingBookId == f.Book.Id);
    }

    [Fact]
    public async Task ReopenRetry_RequiresBoundExactReversalAuthority()
    {
        var f = await FixtureWithPostedActivityAsync();
        var closed = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        var request = new FiscalYearReopenRequestDto
        {
            AccountingBookId = f.Book.Id,
            BookCloseCycleId = closed.BookCloseCycleId!.Value,
            Reason = "Late supplier invoices require the year-end correction."
        };
        var reopened = await f.GlService.ReopenFiscalYearAsync(f.FiscalYear.Id, request);
        var reversalEvent = await f.Db.FinancePostingEvents.SingleAsync(item =>
            item.JournalEntryId == reopened.ReversalJournalEntryId);
        reversalEvent.IsDeleted = true;
        await f.Db.SaveChangesAsync();

        await f.GlService.Invoking(service => service.ReopenFiscalYearAsync(f.FiscalYear.Id, request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SOURCE_BOOK_AUTHORITY_ORIGINAL_EVENT_INVALID*");
        (await f.Db.YearEndBookCloseCycles.SingleAsync()).Status.Should().Be("Reopened");
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("MissingPeriodCoverage")]
    [InlineData("Locked")]
    [InlineData("Inconsistent")]
    [InlineData("MissingCloseEvidence")]
    [InlineData("SelfApproved")]
    public async Task Close_RequiresCompleteGovernedTenantPeriodAuthority(string defect)
    {
        var f = await FixtureWithPostedActivityAsync();
        var certification = await f.Db.FinanceCloseCertifications.SingleAsync();
        if (defect == "Open")
        {
            f.Period.PeriodStatus = "Open";
            f.Period.IsOpen = true;
            f.Period.IsClosed = false;
        }
        if (defect == "MissingPeriodCoverage") f.Period.EndDate = new DateTime(2026, 6, 30);
        if (defect == "Locked") f.Period.IsLocked = true;
        if (defect == "Inconsistent") f.Period.IsClosed = false;
        if (defect == "MissingCloseEvidence")
        {
            f.Db.FinanceCloseCertifications.Remove(certification);
            f.Db.FinanceCloseCycles.Remove(await f.Db.FinanceCloseCycles.SingleAsync());
        }
        if (defect == "SelfApproved") certification.PreparedByUserId = certification.ApprovedByUserId;
        await f.Db.SaveChangesAsync();
        (await f.GlService.CloseFiscalYearAsync(CloseRequest(f))).Success.Should().BeFalse();
        (await f.Db.YearEndBookCloseCycles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Close_IgnoresIrrelevantLegacyBookPeriodRows()
    {
        var f = await FixtureWithPostedActivityAsync();
        FinancePostingAuthorityFixture.SeedExactBookPeriod(
            f.Db, f.FiscalYear.TenantId, f.Period, f.Book.Code);
        var legacy = f.Db.AccountingBookPeriods.Local.Single();
        legacy.PeriodStatus = AccountingBookPeriodStatus.Open;
        legacy.PendingStatus = AccountingBookPeriodStatus.Closed;
        legacy.RequestedByUserId = Guid.NewGuid();
        legacy.DecidedByUserId = legacy.RequestedByUserId;
        await f.Db.SaveChangesAsync();

        var result = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));

        result.Success.Should().BeTrue();
        (await f.Db.AccountingBookPeriods.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Close_CapturesCurrentTenantPeriodRecloseCycleAfterGovernedReopen()
    {
        var f = await FixtureWithPostedActivityAsync();
        var originalCycle = await f.Db.FinanceCloseCycles.SingleAsync();
        var originalCertification = await f.Db.FinanceCloseCertifications.SingleAsync();
        var reopenedAt = DateTime.UtcNow.AddMinutes(-2);
        originalCycle.Status = FinanceCloseStatuses.Reopened;
        originalCycle.ReopenedAt = reopenedAt;
        originalCycle.ReopenedByUserId = Guid.NewGuid();
        originalCycle.ReopenReason = "Approved correction required a governed tenant period reopen.";
        originalCertification.IsSuperseded = true;
        originalCertification.SupersededAt = reopenedAt;
        originalCertification.SupersededReason = "Superseded by the approved tenant period reopen.";
        f.Period.HasBeenReopened = true;
        f.Period.ReopenCount = 1;
        f.Period.LastReopenedDate = reopenedAt;
        f.Period.LastReopenedByUserId = originalCycle.ReopenedByUserId;
        f.Period.ReopenReason = originalCycle.ReopenReason;
        var currentCycle = SeedGovernedTenantPeriodClose(f.Db, f.Period, cycleNumber: 2);
        await f.Db.SaveChangesAsync();

        var result = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));

        result.Success.Should().BeTrue();
        var snapshot = (await f.Db.YearEndBookCloseCycles.SingleAsync()).PeriodAuthoritySnapshotJson;
        snapshot.Should().Contain(currentCycle.Id.ToString()).And.NotContain(originalCycle.Id.ToString());
    }

    [Fact]
    public async Task Close_RejectsLockedFiscalYearAndLegacyYearWideClose()
    {
        var f = await FixtureWithPostedActivityAsync();
        f.FiscalYear.IsLocked = true; await f.Db.SaveChangesAsync();
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(CloseRequest(f))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*locked*");
        f.FiscalYear.IsLocked = false; f.FiscalYear.IsClosed = true; await f.Db.SaveChangesAsync();
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(CloseRequest(f))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*legacy*");
        f.FiscalYear.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Close_RejectsWrongBookCurrencyAndNonEquityRetainedEarnings()
    {
        var f = await FixtureWithPostedActivityAsync();
        var request = CloseRequest(f); request.RetainedEarningsAccountId = f.Revenue.Id;
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(request)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*equity*");
        // InMemory cannot roll back its transaction; use a fresh fixture for the currency denial.
        f = await FixtureWithPostedActivityAsync();
        var transaction = await f.Db.AccountTransactions.FirstAsync(x => x.AccountId == f.Revenue.Id);
        transaction.FunctionalCurrencyCode = "USD"; await f.Db.SaveChangesAsync();
        await f.GlService.Invoking(x => x.CloseFiscalYearAsync(CloseRequest(f))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*currency*");
        (await f.Db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "YearEndClose")).Should().Be(0);
    }

    [Fact]
    public async Task DraftJournalInAnotherBook_DoesNotBlockBaseClose()
    {
        var f = await FixtureWithPostedActivityAsync();
        var parallel = await AddBookActivityAsync(f, "USD_PARALLEL", AccountingBookType.ParallelFull, 200m, "USD");
        f.Db.JournalEntries.Add(new JournalEntry { TenantId = f.FiscalYear.TenantId, AccountingBookId = parallel.Id,
            FiscalPeriodId = f.Period.Id, JournalEntryNumber = "PAR-DRAFT", Description = "Parallel adjustment",
            EntryDate = f.Period.EndDate, PostingStatus = "Draft" });
        await f.Db.SaveChangesAsync();
        (await f.GlService.CloseFiscalYearAsync(CloseRequest(f))).Success.Should().BeTrue();
    }

    [Fact]
    public async Task UnpostedJournalInSelectedBook_BlocksClose()
    {
        var f = await FixtureWithPostedActivityAsync();
        f.Db.JournalEntries.Add(new JournalEntry { TenantId = f.FiscalYear.TenantId, AccountingBookId = f.Book.Id,
            FiscalPeriodId = f.Period.Id, JournalEntryNumber = "BASE-DRAFT", Description = "Base adjustment",
            EntryDate = f.Period.EndDate, PostingStatus = "Draft" });
        await f.Db.SaveChangesAsync();
        (await f.GlService.CloseFiscalYearAsync(CloseRequest(f))).Message.Should().Contain("unposted journal");
        (await f.Db.YearEndBookCloseCycles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PostingEngine_RejectsForgedYearEndRouteAndUnboundCycle()
    {
        var f = await FixtureWithPostedActivityAsync();
        var forged = ActivityRequest(f.FiscalYear.TenantId, "FORGED", new[] {
            new FinancePostingLineDto { AccountId = f.Cash.Id, DebitAmount = 10m },
            new FinancePostingLineDto { AccountId = f.Revenue.Id, CreditAmount = 10m } });
        forged.SourceModule = "GL"; forged.SourceDocumentType = "YearEndClose"; forged.AllowPostingToClosedPeriod = true;
        await f.Engine.Invoking(x => x.PostAsync(forged)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*governed book-close cycle*");
        await f.Engine.Invoking(x => x.PostYearEndAsync(forged, Guid.NewGuid())).Should().ThrowAsync<InvalidOperationException>().WithMessage("*durable*");
    }

    [Fact]
    public async Task ClosedBookYear_BlocksOrdinaryPostEvenIfPeriodWasOpened()
    {
        var f = await FixtureWithPostedActivityAsync();
        await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        f.Period.IsClosed = false; f.Period.IsOpen = true; await f.Db.SaveChangesAsync();
        var request = ActivityRequest(f.FiscalYear.TenantId, "LATE", new[] {
            new FinancePostingLineDto { AccountId = f.Cash.Id, DebitAmount = 10m },
            new FinancePostingLineDto { AccountId = f.Revenue.Id, CreditAmount = 10m } });
        await f.Engine.Invoking(x => x.PostAsync(request)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*closed for this fiscal year*");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("book")]
    [InlineData("currency")]
    [InlineData("source")]
    [InlineData("reversal")]
    public async Task YearEndLeaf_RejectsForgedBoundCycleAuthority(string defect)
    {
        var f = await FixtureWithPostedActivityAsync();
        var closed = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        var cycle = await f.Db.YearEndBookCloseCycles.SingleAsync();
        var request = new FinancePostingRequestV2Dto
        {
            SourceModule = "GL", SourceDocumentType = "YearEndCloseReversal",
            SourceDocumentId = cycle.Id, SourceDocumentTenantId = cycle.TenantId,
            AccountingBookCode = cycle.AccountingBookCode, FunctionalCurrencyCode = cycle.FunctionalCurrencyCode,
            ReversalOfJournalEntryId = closed.ClosingJournalEntryId, PostingAction = "Reverse",
            FiscalPeriodId = f.Period.Id, PostingDate = f.FiscalYear.EndDate,
            AllowPostingToClosedPeriod = true,
            IdempotencyKey = $"GL:YearEndCloseReversal:{cycle.TenantId:N}:{cycle.AccountingBookId:N}:{cycle.Id:N}"
        };
        if (defect == "tenant") request.SourceDocumentTenantId = Guid.NewGuid();
        if (defect == "book") request.AccountingBookCode = "IFRS";
        if (defect == "currency") request.FunctionalCurrencyCode = "USD";
        if (defect == "source") request.SourceDocumentId = Guid.NewGuid();
        if (defect == "reversal") request.ReversalOfJournalEntryId = Guid.NewGuid();
        await f.Engine.Invoking(x => x.PostYearEndAsync(request, cycle.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match its exact book-close cycle*");
        (await f.Db.FinancePostingEvents.CountAsync(x => x.SourceDocumentType == "YearEndCloseReversal")).Should().Be(0);
        cycle.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task YearEndLeaf_RejectsTenantCloseEvidenceChangedAfterCapture()
    {
        var f = await FixtureWithPostedActivityAsync();
        var closed = await f.GlService.CloseFiscalYearAsync(CloseRequest(f));
        var cycle = await f.Db.YearEndBookCloseCycles.SingleAsync();
        f.Period.ClosedDate = f.Period.ClosedDate!.Value.AddSeconds(1);
        await f.Db.SaveChangesAsync();
        var request = new FinancePostingRequestV2Dto
        {
            SourceModule = "GL",
            SourceDocumentType = "YearEndCloseReversal",
            SourceDocumentId = cycle.Id,
            SourceDocumentTenantId = cycle.TenantId,
            AccountingBookCode = cycle.AccountingBookCode,
            FunctionalCurrencyCode = cycle.FunctionalCurrencyCode,
            ReversalOfJournalEntryId = closed.ClosingJournalEntryId,
            PostingAction = "Reverse",
            FiscalPeriodId = f.Period.Id,
            PostingDate = f.FiscalYear.EndDate,
            AllowPostingToClosedPeriod = true,
            IdempotencyKey = $"GL:YearEndCloseReversal:{cycle.TenantId:N}:{cycle.AccountingBookId:N}:{cycle.Id:N}"
        };

        await f.Engine.Invoking(item => item.PostYearEndAsync(request, cycle.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unchanged captured tenant fiscal-period close authority*");
        (await f.Db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "YearEndCloseReversal")).Should().Be(0);
    }

    [Fact]
    public void FiscalYearEndpoints_PreserveDistinctCloseAndReopenPermissions()
    {
        var controller = typeof(ErpSystem.Api.Controllers.Finance.FiscalPeriodController);
        var close = controller.GetMethod("CloseFiscalYear")!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(x => x.Policy);
        var reopen = controller.GetMethod("ReopenFiscalYear")!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(x => x.Policy);
        close.Should().Contain(FinancePermissions.CloseAccountingPeriods);
        reopen.Should().Contain(FinancePermissions.ReopenAccountingPeriods);
        close.Should().NotContain(FinancePermissions.ReopenAccountingPeriods);
    }

    [Fact]
    public async Task OrdinaryPrimaryPosting_StillCreatesParallelReplica()
    {
        var f = await FixtureWithPostedActivityAsync();
        var parallel = await AddBookActivityAsync(f, "USD_PARALLEL", AccountingBookType.ParallelFull, 200m, "USD");
        f.Period.IsOpen = true; f.Period.IsClosed = false;
        f.Db.ExchangeRates.Add(new ExchangeRate { TenantId = f.FiscalYear.TenantId, BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD", Rate = 2m, InverseRate = 0.5m, EffectiveDate = new DateTime(2026, 1, 1),
            RateType = ExchangeRateType.Daily, QuoteSide = ExchangeRateQuoteSide.Mid, RateSource = "Approved fixture",
            ApprovalStatus = RateApprovalStatus.Approved, IsActive = true, CreatedByUserId = Guid.NewGuid() });
        await f.Db.SaveChangesAsync();
        var request = ActivityRequest(f.FiscalYear.TenantId, "NORMAL-REPLICA", new[] {
            new FinancePostingLineDto { AccountId = f.Cash.Id, DebitAmount = 10m },
            new FinancePostingLineDto { AccountId = f.Revenue.Id, CreditAmount = 10m } });
        var posted = await f.Engine.PostAsync(request);
        var replica = await f.Db.JournalEntries.SingleAsync(x => x.ReplicatedFromJournalEntryId == posted.JournalEntryId);
        replica.AccountingBookId.Should().Be(parallel.Id);
        replica.TotalDebitAmount.Should().Be(20m);
        request.AccountingBookCode = parallel.Code;
        request.FunctionalCurrencyCode = "USD";
        await f.Engine.Invoking(x => x.PostAsync(request)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PARALLEL_DIRECT_POSTING_FORBIDDEN*");
    }

    [Fact]
    public void Migration_PreservesEvidenceAndDefinesConcurrencyAndTenantBookKeys()
    {
        var migration = new ErpSystem.Data.Migrations.YearEndBookCloseCycles();
        var table = migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>().Single();
        table.Name.Should().Be("YearEndBookCloseCycles");
        table.Columns.Single(x => x.Name == "RowVersion").IsRowVersion.Should().BeTrue();
        table.Columns.Single(x => x.Name == "NetIncomeTransferred").ColumnType.Should().Be("decimal(18,2)");
        table.ForeignKeys.Should().Contain(x => x.Columns.SequenceEqual(new[] { "TenantId", "FiscalYearId" }));
        table.ForeignKeys.Should().Contain(x => x.Columns.SequenceEqual(new[] { "TenantId", "ClosingJournalEntryId", "AccountingBookId" }));
        migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>()
            .Should().Contain(x => x.IsUnique && x.Filter == "[Status] IN ('Closing', 'Closed')");
        migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
            .Should().Contain(x => x.Sql.Contains("ImmutableEvidence") && x.Sql.Contains("cannot be deleted"));
        migration.DownOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
            .Should().Contain(x => x.Sql.Contains("IF EXISTS") && x.Sql.Contains("retained book-year close evidence"));

        using var modelDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=invalid.example;Database=unused;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        modelDb.Model.FindEntityType(typeof(YearEndBookCloseCycle))!
            .FindProperty(nameof(YearEndBookCloseCycle.NetIncomeTransferred))!
            .GetColumnType().Should().Be("decimal(18,2)");
    }

    [Fact]
    public async Task RelationalYearEndLeaf_RejectsMissingSerializableTransactionWithoutOpeningDatabase()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=invalid.example;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options;
        await using var db = new ApplicationDbContext(options);
        var engine = new FinancePostingEngine(db, CreateCurrentUser(tenantId).Object, Mock.Of<ILogger<FinancePostingEngine>>());
        await engine.Invoking(x => x.PostYearEndAsync(new FinancePostingRequestV2Dto(), Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*serializable transaction*");
    }

    [Fact]
    public async Task NoNominalActivity_ClosesAndReopensWithoutInventingAJournal()
    {
        var f = await FixtureWithPostedActivityAsync();
        var emptyBook = await AddBookActivityAsync(f, "EMPTY_DELTA", AccountingBookType.Delta, 0m, "GHS");
        var request = CloseRequest(f); request.AccountingBookId = emptyBook.Id;
        var result = await f.GlService.CloseFiscalYearAsync(request);
        result.Success.Should().BeTrue();
        result.ClosingJournalEntryId.Should().BeNull();
        result.NetIncomeTransferred.Should().Be(0m);
        var reopened = await f.GlService.ReopenFiscalYearAsync(f.FiscalYear.Id, new FiscalYearReopenRequestDto
        {
            AccountingBookId = emptyBook.Id, BookCloseCycleId = result.BookCloseCycleId!.Value,
            Reason = "Reopen the no-activity close for correction."
        });
        reopened.ReversalJournalEntryId.Should().BeNull();
        (await f.Db.YearEndBookCloseCycles.SingleAsync()).Status.Should().Be("Reopened");
        (await f.Db.FinancePostingEvents.CountAsync(x => x.AccountingBookId == emptyBook.Id)).Should().Be(0);
    }

    private static YearEndCloseRequestDto CloseRequest(Fixture f, string key = "cycle-1") => new()
    { FiscalYearId = f.FiscalYear.Id, AccountingBookId = f.Book.Id, RetainedEarningsAccountId = f.RetainedEarnings.Id, IdempotencyKey = key };

    private static async Task<Guid> AttachFrozenCodingAsync(Fixture f, AccountTransaction source, string code)
    {
        var tenantId = f.FiscalYear.TenantId;
        var definition = await f.Db.FinanceDimensionDefinitions.SingleOrDefaultAsync(x => x.Code == "CC");
        if (definition == null)
        {
            definition = new FinanceDimensionDefinition { TenantId = tenantId, Code = "CC",
                Name = "Current renamed cost centre", IsActive = false };
            f.Db.FinanceDimensionDefinitions.Add(definition);
        }
        var value = new FinanceDimensionValue { TenantId = tenantId, FinanceDimensionDefinitionId = definition.Id,
            Code = code, Name = "Current renamed " + code, IsActive = false, EffectiveDate = f.FiscalYear.StartDate };
        f.Db.FinanceDimensionValues.Add(value);
        var set = new FinanceDimensionSet { TenantId = tenantId, CombinationHash = new string(code[0], 64),
            DisplayValue = "CC=" + code };
        set.Items.Add(new FinanceDimensionSetItem { TenantId = tenantId, FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionValueId = value.Id, DimensionCodeSnapshot = "CC", DimensionNameSnapshot = "Original cost centre",
            DimensionValueCodeSnapshot = code, DimensionValueNameSnapshot = "Original " + code });
        var snapshot = new FinanceDimensionSnapshot { TenantId = tenantId, FinanceDimensionSetId = set.Id,
            CombinationHashSnapshot = set.CombinationHash, DisplayValueSnapshot = set.DisplayValue };
        snapshot.Items.Add(new FinanceDimensionSnapshotItem { TenantId = tenantId,
            FinanceDimensionDefinitionId = definition.Id, FinanceDimensionValueId = value.Id,
            DimensionCodeSnapshot = "CC", DimensionNameSnapshot = "Original cost centre",
            DimensionValueCodeSnapshot = code, DimensionValueNameSnapshot = "Original " + code });
        f.Db.FinanceDimensionSets.Add(set);
        f.Db.FinanceDimensionSnapshots.Add(snapshot);
        source.FinanceDimensionSetId = set.Id;
        source.FinanceDimensionSnapshotId = snapshot.Id;
        source.SegmentString = "CC=" + code;
        await f.Db.SaveChangesAsync();
        return set.Id;
    }

    private static Task<decimal> IncomeAsync(Fixture f, Guid bookId) => f.Db.AccountTransactions
        .Where(x => x.AccountingBookId == bookId && (x.AccountId == f.Revenue.Id || x.AccountId == f.Expense.Id))
        .SumAsync(x => x.CreditAmount - x.DebitAmount);

    private static async Task<AccountingBook> AddBookActivityAsync(Fixture f, string code, AccountingBookType type, decimal income, string currency)
    {
        var book = new AccountingBook { TenantId = f.FiscalYear.TenantId, Code = code, Name = code, BookType = type,
            BaseAccountingBookId = f.Book.Id, FunctionalCurrencyCode = type == AccountingBookType.Delta ? null : currency,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, IsActive = true, AllowsPosting = true,
            ReplicationStartDate = type == AccountingBookType.ParallelFull ? f.FiscalYear.StartDate : null,
            ParallelOpeningMode = type == AccountingBookType.ParallelFull ? ParallelBookOpeningMode.ZeroOpening : null };
        f.Db.AccountingBooks.Add(book);
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(f.Db, f.FiscalYear.TenantId, book, f.Cash, f.Revenue, f.Expense, f.RetainedEarnings);
        var journal = new JournalEntry { TenantId = f.FiscalYear.TenantId, AccountingBookId = book.Id, BookClassification = code,
            FiscalPeriodId = f.Period.Id, JournalEntryNumber = code + "-ACTIVITY", Description = "Existing book activity",
            EntryDate = new DateTime(2026, 6, 15), PostingStatus = "Posted", TotalDebitAmount = income, TotalCreditAmount = income };
        journal.Transactions = new List<AccountTransaction> {
            new() { TenantId = f.FiscalYear.TenantId, AccountingBookId = book.Id, BookClassification = code,
                AccountId = f.Cash.Id, FiscalPeriodId = f.Period.Id, TransactionDate = journal.EntryDate, DebitAmount = income,
                FunctionalCurrencyCode = currency, TransactionCurrency = currency, PostingStatus = "Posted", LineNumber = 1 },
            new() { TenantId = f.FiscalYear.TenantId, AccountingBookId = book.Id, BookClassification = code,
                AccountId = f.Revenue.Id, FiscalPeriodId = f.Period.Id, TransactionDate = journal.EntryDate, CreditAmount = income,
                FunctionalCurrencyCode = currency, TransactionCurrency = currency, PostingStatus = "Posted", LineNumber = 2 }
        };
        f.Db.JournalEntries.Add(journal);
        await f.Db.SaveChangesAsync();
        return book;
    }

    private sealed record Fixture(
        ApplicationDbContext Db,
        GeneralLedgerService GlService,
        FiscalYear FiscalYear,
        Account Revenue,
        Account Expense,
        Account RetainedEarnings,
        Account Cash,
        AccountingBook Book,
        FiscalPeriod Period,
        FinancePostingEngine Engine);

    private static async Task<Fixture> FixtureWithPostedActivityAsync(
        bool closePeriod = true, Action<FinancePostingRequestV2Dto>? mutateClose = null)
    {
        var tenantId = Guid.NewGuid();
        var db = CreateContext();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Year Close Tenant",
            Code = "YEC",
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.FinanceSettings.Add(new FinanceSettings { TenantId = tenantId, BaseCurrency = "GHS" });
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "BASE", Name = "Base Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        };
        db.AccountingBooks.Add(book);

        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = "Open",
            IsActive = true
        };
        db.FiscalYears.Add(fiscalYear);

        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "FY2026",
            PeriodCode = "2026",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            PeriodDays = 365,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            AllowFutureDating = true
        };
        db.FiscalPeriods.Add(period);

        var cash = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var revenue = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var expense = SeedAccount(db, tenantId, "5000", AccountType.Expense);
        var retainedEarnings = SeedAccount(db, tenantId, "3900", AccountType.Equity);
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(
            db, tenantId, book, cash, revenue, expense, retainedEarnings);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var engine = new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>());

        // Post real activity through the engine: revenue 140, expense 40 (net income 100).
        await engine.PostAsync(ActivityRequest(tenantId, "REV-1", new[]
        {
            new FinancePostingLineDto { AccountId = cash.Id, Description = "Cash in", DebitAmount = 140m },
            new FinancePostingLineDto { AccountId = revenue.Id, Description = "Sales", CreditAmount = 140m }
        }));
        await engine.PostAsync(ActivityRequest(tenantId, "EXP-1", new[]
        {
            new FinancePostingLineDto { AccountId = expense.Id, Description = "Rent", DebitAmount = 40m },
            new FinancePostingLineDto { AccountId = cash.Id, Description = "Cash out", CreditAmount = 40m }
        }));

        if (closePeriod)
        {
            SeedGovernedTenantPeriodClose(db, period);
            await db.SaveChangesAsync();
        }

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        var reportingOptions = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase($"fiscal-year-close-read-{Guid.NewGuid()}")
            .Options;

        var glService = new GeneralLedgerService(
            db,
            new ReportingDbContext(reportingOptions),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<IFiscalPeriodService>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IAccountingBookService>(),
            BuildYearEndEngine(engine, mutateClose),
            sourceBookAuthority: new FinanceSourceBookAuthorityService(db, currentUser.Object));

        return new Fixture(db, glService, fiscalYear, revenue, expense, retainedEarnings, cash, book, period, engine);
    }

    private static FinanceCloseCycle SeedGovernedTenantPeriodClose(
        ApplicationDbContext db,
        FiscalPeriod period,
        int cycleNumber = 1)
    {
        var preparedBy = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var preparedAt = DateTime.UtcNow.AddMinutes(-5);
        var approvedAt = DateTime.UtcNow;
        period.IsCloseInitiated = true;
        period.CloseInitiatedDate = preparedAt.AddMinutes(-5);
        period.CloseInitiatedByUserId = preparedBy;
        period.IsOpen = false;
        period.IsClosed = true;
        period.IsLocked = false;
        period.PeriodStatus = "Closed";
        period.ClosedDate = approvedAt;
        period.ClosedByUserId = approvedBy;
        var cycle = new FinanceCloseCycle
        {
            Id = Guid.NewGuid(),
            TenantId = period.TenantId,
            FiscalPeriodId = period.Id,
            CycleNumber = cycleNumber,
            TemplateCode = "TDC-YEAR-END",
            CloseType = FinanceCloseTemplateTypes.YearEnd,
            TemplateVersion = 1,
            Status = FinanceCloseStatuses.Closed,
            EvaluationCount = 1,
            StartedAt = period.CloseInitiatedDate.Value,
            StartedByUserId = preparedBy,
            StartedByUserName = "period.preparer",
            PreparedAt = preparedAt,
            ClosedAt = approvedAt,
            CreatedBy = "period.preparer",
            CreatedById = preparedBy
        };
        db.FinanceCloseCycles.Add(cycle);
        db.FinanceCloseCertifications.Add(new FinanceCloseCertification
        {
            Id = Guid.NewGuid(),
            TenantId = period.TenantId,
            FinanceCloseCycleId = cycle.Id,
            PreparedByUserId = preparedBy,
            PreparedByUserName = "period.preparer",
            PreparedAt = preparedAt,
            PreparerDeclaration = "All mandatory tenant period controls are complete.",
            ReviewedByUserId = approvedBy,
            ReviewedByUserName = "period.approver",
            ReviewedAt = approvedAt,
            ReviewerDeclaration = "I independently reviewed and approve this period close.",
            ApprovedByUserId = approvedBy,
            ApprovedByUserName = "period.approver",
            ApprovedAt = approvedAt,
            CreatedBy = "period.preparer",
            CreatedById = preparedBy
        });
        return cycle;
    }

    private static IFinancePostingEngine BuildYearEndEngine(
        FinancePostingEngine engine, Action<FinancePostingRequestV2Dto>? mutateClose)
    {
        if (mutateClose == null) return engine;
        var guarded = new Mock<IFinancePostingEngine>();
        guarded.Setup(item => item.PostYearEndAsync(It.IsAny<FinancePostingRequestV2Dto>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((FinancePostingRequestV2Dto request, Guid cycleId, CancellationToken ct) =>
            {
                mutateClose(request);
                return engine.PostYearEndAsync(request, cycleId, ct);
            });
        return guarded.Object;
    }

    private static FinancePostingRequestV2Dto ActivityRequest(Guid tenantId, string reference, FinancePostingLineDto[] lines)
    {
        return new FinancePostingRequestV2Dto
        {
            SourceModule = "TEST",
            SourceDocumentType = "YearActivity",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = reference,
            Description = $"Activity {reference}",
            PostingDate = new DateTime(2026, 6, 15),
            JournalType = "System Generated",
            AccountingBookCode = "BASE",
            FunctionalCurrencyCode = "GHS",
            Lines = lines
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fiscal-year-close-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("year.closer");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("fiscal-year-close-tests");
        return currentUser;
    }

    private static Account SeedAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, AccountType accountType)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        return account;
    }
}
