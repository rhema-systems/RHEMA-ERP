using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinancePostingEngineTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudgetAdapter")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldConsumeGenericProducerReservationsInsidePostingTransaction()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var creditAccount = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        await db.SaveChangesAsync();
        var currentUser = CreateCurrentUser(tenantId);
        var commitments = new Mock<IFinanceBudgetCommitmentService>(MockBehavior.Strict);
        var reservationId = Guid.NewGuid();
        commitments.Setup(service => service.ConsumeForPostingAsync(
                tenantId,
                "VendorInvoice",
                It.IsAny<Guid>(),
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { reservationId })),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            budgetCommitments: commitments.Object);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.SourceDocumentType = "VendorInvoice";
        request.BudgetReservationSourceDocumentType = "VendorInvoice";
        request.BudgetReservationIds = new[] { reservationId };

        var result = await service.PostAsync(request);

        commitments.VerifyAll();
        result.PostingStatus.Should().Be("Posted");
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudgetAdapter")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRollbackLedger_WhenGenericReservationConsumptionFails()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var creditAccount = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        await db.SaveChangesAsync();
        var currentUser = CreateCurrentUser(tenantId);
        var commitments = new Mock<IFinanceBudgetCommitmentService>();
        commitments.Setup(service => service.ConsumeForPostingAsync(
                tenantId,
                "VendorInvoice",
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FinanceBudgetCommitmentConflictException(
                "BUDGET_POSTING_RESERVATION_MISMATCH", "Reservation drift."));
        var service = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            budgetCommitments: commitments.Object);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.SourceDocumentType = "VendorInvoice";
        request.BudgetReservationSourceDocumentType = "VendorInvoice";
        request.BudgetReservationIds = new[] { Guid.NewGuid() };

        var action = () => service.PostAsync(request);

        await action.Should().ThrowAsync<FinanceBudgetCommitmentConflictException>();
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.AccountBalances.CountAsync(item => item.AccountId == debitAccount.Id)).Should().Be(0);
        (await db.AccountBalances.CountAsync(item => item.AccountId == creditAccount.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldCreatePostedLedgerEntriesAndPostingEvent_WhenBalancedSameTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var cashAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, cashAccount.Id, revenueAccount.Id);

        var result = await service.PostAsync(request);

        result.PostingStatus.Should().Be("Posted");
        result.WasDuplicate.Should().BeFalse();
        result.TotalDebitAmount.Should().Be(100m);
        result.TotalCreditAmount.Should().Be(100m);
        result.FunctionalCurrencyCode.Should().Be("GHS");
        result.JournalEntryId.Should().NotBeEmpty();
        result.PostingEventId.Should().NotBeEmpty();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.TenantId.Should().Be(tenantId);
        journal.PostingStatus.Should().Be("Posted");
        journal.TotalDebitAmount.Should().Be(100m);
        journal.TotalCreditAmount.Should().Be(100m);
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Should().OnlyContain(t => t.TenantId == tenantId && t.PostingStatus == "Posted");

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e => e.Id == result.PostingEventId);
        postingEvent.TenantId.Should().Be(tenantId);
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);
        postingEvent.OriginModuleCode.Should().Be("FIN");
        journal.OriginModuleCode.Should().Be("FIN");

        (await db.AccountBalances.SingleAsync(item => item.AccountId == cashAccount.Id)).ClosingBalance.Should().Be(100m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == revenueAccount.Id)).ClosingBalance.Should().Be(-100m);
    }

    [Fact]
    [Trait("Category", "PostingEngine")]
    [Trait("Category", "RecurringJournal")]
    public async Task PostAsync_ShouldPostEveryBalancedRecurringJournalLineWithStableSourceIdentity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var expenseA = SeedAccount(db, tenantId, "6101", AccountType.Expense);
        var expenseB = SeedAccount(db, tenantId, "6102", AccountType.Expense);
        var accrualA = SeedAccount(db, tenantId, "2101", AccountType.Liability);
        var accrualB = SeedAccount(db, tenantId, "2102", AccountType.Liability);
        await db.SaveChangesAsync();
        var sourceLineIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var request = CreateRequest(tenantId, expenseA.Id, accrualA.Id);
        request.SourceModule = "GL";
        request.SourceDocumentType = "RecurringJournalOccurrence";
        request.PostingAction = "PostRecurringJournalOccurrence";
        request.Lines =
        [
            new() { AccountId = expenseA.Id, SourceDocumentLineId = sourceLineIds[0], DebitAmount = 600m, LineNumber = 1 },
            new() { AccountId = expenseB.Id, SourceDocumentLineId = sourceLineIds[1], DebitAmount = 400m, LineNumber = 2 },
            new() { AccountId = accrualA.Id, SourceDocumentLineId = sourceLineIds[2], CreditAmount = 750m, LineNumber = 3 },
            new() { AccountId = accrualB.Id, SourceDocumentLineId = sourceLineIds[3], CreditAmount = 250m, LineNumber = 4 }
        ];

        var result = await CreateService(db, tenantId).PostAsync(request);

        result.PostingStatus.Should().Be("Posted");
        result.TotalDebitAmount.Should().Be(1000m);
        result.TotalCreditAmount.Should().Be(1000m);
        var journal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        journal.Transactions.Should().HaveCount(4);
        journal.Transactions.OrderBy(line => line.LineNumber).Select(line => line.SourceDocumentLineId)
            .Should().Equal(sourceLineIds.Cast<Guid?>());
        journal.BookClassification.Should().Be("IFRS");
    }

    [Fact]
    [Trait("Category", "PostingEngine")]
    [Trait("Category", "FiscalPeriod")]
    public async Task PostAsync_ShouldRejectFutureDatedPosting_WhenPeriodPolicyIsDisabled()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        period.AllowFutureDating = false;
        var debitAccount = SeedAccount(db, tenantId, "6101", AccountType.Expense);
        var creditAccount = SeedAccount(db, tenantId, "2101", AccountType.Liability);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.PostingDate = DateTime.UtcNow.Date.AddDays(1);
        request.FiscalPeriodId = period.Id;

        var action = () => CreateService(db, tenantId).PostAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Future-dated posting is not allowed*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "PostingEngine")]
    [Trait("Category", "FiscalPeriod")]
    public async Task PostAsync_ShouldAllowFutureDatedPosting_WhenPeriodPolicyIsEnabled()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        period.AllowFutureDating = true;
        var debitAccount = SeedAccount(db, tenantId, "6102", AccountType.Expense);
        var creditAccount = SeedAccount(db, tenantId, "2102", AccountType.Liability);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.PostingDate = DateTime.UtcNow.Date.AddDays(1);
        request.FiscalPeriodId = period.Id;

        var result = await CreateService(db, tenantId).PostAsync(request);

        result.PostingStatus.Should().Be("Posted");
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldApplyBookBalanceMovementUsingSignedDebitMinusCredit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var assetAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, "5000", AccountType.Expense);
        var liabilityAccount = SeedAccount(db, tenantId, "2000", AccountType.Liability);
        var equityAccount = SeedAccount(db, tenantId, "3000", AccountType.Equity);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        await service.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "TEST",
            SourceDocumentType = "NormalBalanceDocument",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "NB-001",
            Description = "Normal balance direction test",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto { AccountId = assetAccount.Id, Description = "Asset debit", DebitAmount = 100m },
                new FinancePostingLineDto { AccountId = expenseAccount.Id, Description = "Expense debit", DebitAmount = 40m },
                new FinancePostingLineDto { AccountId = revenueAccount.Id, Description = "Revenue debit", DebitAmount = 15m },
                new FinancePostingLineDto { AccountId = liabilityAccount.Id, Description = "Liability credit", CreditAmount = 100m },
                new FinancePostingLineDto { AccountId = equityAccount.Id, Description = "Equity credit", CreditAmount = 25m },
                new FinancePostingLineDto { AccountId = assetAccount.Id, Description = "Asset credit", CreditAmount = 30m }
            }
        });

        (await db.AccountBalances.SingleAsync(item => item.AccountId == assetAccount.Id)).ClosingBalance.Should().Be(70m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == expenseAccount.Id)).ClosingBalance.Should().Be(40m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == liabilityAccount.Id)).ClosingBalance.Should().Be(-100m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == equityAccount.Id)).ClosingBalance.Should().Be(-25m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == revenueAccount.Id)).ClosingBalance.Should().Be(15m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectUnbalancedPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.Lines = new[]
        {
            request.Lines[0],
            new FinancePostingLineDto
            {
                AccountId = creditAccount.Id,
                Description = "Revenue",
                CreditAmount = 90m
            }
        };

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting is not balanced. Total debits must equal total credits.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldReturnExistingPosting_WhenSourceDocumentPostedAgain()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var first = await service.PostAsync(request);
        var second = await service.PostAsync(request);

        second.WasDuplicate.Should().BeTrue();
        second.PostingEventId.Should().Be(first.PostingEventId);
        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == debitAccount.Id)).ClosingBalance.Should().Be(100m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == creditAccount.Id)).ClosingBalance.Should().Be(-100m);
        (await db.AccountBalances.CountAsync()).Should().Be(2);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == debitAccount.Id)).PeriodDebits.Should().Be(100m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == creditAccount.Id)).PeriodCredits.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectCrossTenantSourceDocument()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.SourceDocumentTenantId = otherTenantId;

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Source document belongs to another tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectClosedPeriod()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        var error = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_PERIOD_NOT_OPEN:*");
        error.Which.Message.Should().Contain($"book '{request.AccountingBookCode}'")
            .And.Contain("is Closed")
            .And.Contain($"fiscal period '{period.PeriodCode}'");
    }

    [Fact]
    [Trait("Category", "AccountingBookPeriodC4")]
    public async Task PostAsync_ShouldRejectPosting_WhenBookPeriodAuthorityIsMissing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        db.AccountingBookPeriods.RemoveRange(db.AccountingBookPeriods.Local);
        var debit = SeedAccount(db, tenantId, "6110", AccountType.Expense);
        var credit = SeedAccount(db, tenantId, "2110", AccountType.Liability);
        await db.SaveChangesAsync();

        var act = () => CreateService(db, tenantId)
            .PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_PERIOD_REQUIRED:*");
        db.JournalEntries.Should().BeEmpty();
        (await db.AccountBalances.CountAsync(item => item.AccountId == debit.Id)).Should().Be(0);
        (await db.AccountBalances.CountAsync(item => item.AccountId == credit.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "AccountingBookPeriodC4")]
    public async Task PostAsync_ShouldRejectPosting_WhenBookPeriodAuthorityIsClosed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        db.AccountingBookPeriods.Local.Single().PeriodStatus = AccountingBookPeriodStatus.Closed;
        var debit = SeedAccount(db, tenantId, "6120", AccountType.Expense);
        var credit = SeedAccount(db, tenantId, "2120", AccountType.Liability);
        await db.SaveChangesAsync();

        var act = () => CreateService(db, tenantId)
            .PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_PERIOD_NOT_OPEN:*");
        db.JournalEntries.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "ModuleLocks")]
    public async Task PostAsync_ShouldRejectPosting_WhenOriginModuleIsLocked()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        var financeModule = SeedModule(db, tenantId, "FIN", "Finance");
        db.PeriodModuleLocks.Add(new PeriodModuleLock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            ModuleDefinitionId = financeModule.Id,
            IsLocked = true,
            LockedDate = DateTime.UtcNow,
            LockReason = "Month-end processing"
        });
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var act = () => CreateService(db, tenantId)
            .PostAsync(CreateRequest(tenantId, debitAccount.Id, creditAccount.Id));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting blocked: Finance is locked for FY2026*Reason: Month-end processing");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "ModuleLocks")]
    public async Task PostAsync_ShouldAllowOnlyUnexpiredReopenedModule_DuringPartialLock()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        period.IsGlobalLockSuspended = true;
        var financeModule = SeedModule(db, tenantId, "FIN", "Finance");
        var salesModule = SeedModule(db, tenantId, "SALES", "Sales");
        db.PeriodModuleLocks.AddRange(
            new PeriodModuleLock
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FiscalPeriodId = period.Id,
                ModuleDefinitionId = financeModule.Id, IsLocked = true, LockReason = "Global close"
            },
            new PeriodModuleLock
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FiscalPeriodId = period.Id,
                ModuleDefinitionId = salesModule.Id, IsLocked = false,
                ReopenExpiresAtUtc = DateTime.UtcNow.AddHours(4), UnlockReason = "Authorized Sales correction"
            });
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.SourceModule = "AR";
        request.OriginModuleCode = "SALES";
        var result = await CreateService(db, tenantId).PostAsync(request);

        result.OriginModuleCode.Should().Be("SALES");
        (await db.JournalEntries.SingleAsync()).OriginModuleCode.Should().Be("SALES");
    }

    [Fact]
    [Trait("Category", "ModuleLocks")]
    public async Task PostAsync_ShouldFailClosed_WhenTemporaryReopeningHasExpired()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId);
        period.IsGlobalLockSuspended = true;
        var salesModule = SeedModule(db, tenantId, "SALES", "Sales");
        db.PeriodModuleLocks.Add(new PeriodModuleLock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            ModuleDefinitionId = salesModule.Id,
            IsLocked = false,
            ReopenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
            UnlockReason = "Correction window"
        });
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);
        request.OriginModuleCode = "SALES";
        var act = () => CreateService(db, tenantId).PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting blocked: Sales is locked for FY2026*Temporary reopening expired*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClose")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldAuditPostingBlockedByClosedPeriod_WhenFinanceAuditIsConfigured()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(db, currentUser.Object, new HttpContextAccessor());
        var service = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_PERIOD_NOT_OPEN:*");
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.PostingBlockedPeriodClosedLocked)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectInactiveAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var inactiveCreditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue, AccountStatus.Inactive);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, inactiveCreditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot post to inactive GL account(s): 4000.");
    }

    [Fact]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectForeignCurrency_WhenMultiCurrencyAccountMissingActiveCurrencyLink()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var cashAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        SeedExchangeRate(db, tenantId, "USD", 15m);
        await db.SaveChangesAsync();

        var request = CreateForeignCurrencyRequest(tenantId, cashAccount.Id, revenueAccount.Id);

        var act = () => CreateService(db, tenantId).PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account '1000' does not have an active USD currency link*");
    }

    [Fact]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldUpdateBookCurrencyExposure_WithoutMutatingCurrencyLinkConfiguration()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var cashAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var exchangeRate = SeedExchangeRate(db, tenantId, "USD", 15m);
        var usdLink = new AccountCurrencyLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = cashAccount.Id,
            LinkedCurrencyCode = "USD",
            IsActive = true,
            EffectiveDate = new DateTime(2026, 1, 1),
            TransactionRateType = "Daily",
            RevaluationRateType = "Month-End"
        };
        db.AccountCurrencyLinks.Add(usdLink);
        await db.SaveChangesAsync();

        await CreateService(db, tenantId).PostAsync(CreateForeignCurrencyRequest(tenantId, cashAccount.Id, revenueAccount.Id));

        usdLink.ForeignCurrencyBalance.Should().Be(0m);
        usdLink.BaseCurrencyEquivalent.Should().Be(0m);
        usdLink.TransactionCount.Should().Be(0);
        var exposure = await db.AccountCurrencyExposures.SingleAsync(item => item.AccountId == cashAccount.Id);
        exposure.AccountingBookCode.Should().Be("IFRS");
        exposure.TransactionCurrencyCode.Should().Be("USD");
        exposure.SignedForeignBalance.Should().Be(100m);
        exposure.SignedFunctionalBalance.Should().Be(1500m);
        exposure.TransactionCount.Should().Be(1);
        exposure.FirstTransactionDate.Should().Be(new DateTime(2026, 7, 4));
        exposure.LastTransactionDate.Should().Be(new DateTime(2026, 7, 4));
        exchangeRate.HasBeenUsedInTransactions.Should().BeTrue();
        exchangeRate.TransactionCount.Should().Be(1);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == cashAccount.Id)).ClosingBalance.Should().Be(1500m);
        (await db.AccountBalances.SingleAsync(item => item.AccountId == revenueAccount.Id)).ClosingBalance.Should().Be(-1500m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectMissingTenantContext()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(
            db,
            tenantId,
            new Dictionary<string, string> { ["sub"] = Guid.NewGuid().ToString() });
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Finance tenant context is required.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task PostAsync_ShouldRejectOtherTenantAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTHER");
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var otherTenantCreditAccount = SeedAccount(db, otherTenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, otherTenantCreditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("One or more posting accounts were not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-4")]
    [Trait("Category", "PostingEngine")]
    public async Task GetReversalPlanAsync_ShouldReturnOppositeLinesWithoutPostingReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var posted = await service.PostAsync(CreateRequest(tenantId, debitAccount.Id, creditAccount.Id));

        var plan = await service.GetReversalPlanAsync(posted.PostingEventId, "Correction required", new DateTime(2026, 7, 5));

        plan.IsDefined.Should().BeTrue();
        plan.OriginalPostingEventId.Should().Be(posted.PostingEventId);
        plan.OriginalJournalEntryId.Should().Be(posted.JournalEntryId);
        plan.PostingAction.Should().Be("Reverse");
        plan.ReversalDate.Should().Be(new DateTime(2026, 7, 5));
        plan.ReversalLines.Should().HaveCount(2);
        plan.ReversalLines.Sum(l => l.DebitAmount).Should().Be(100m);
        plan.ReversalLines.Sum(l => l.CreditAmount).Should().Be(100m);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "FinanceDimensions")]
    public async Task PostAsync_ShouldResolveAndReuseCanonicalDimensionSet_WithoutChangingLegacyLines()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var expense = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var payable = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        SeedDimensionValue(db, tenantId, "DEPARTMENT", "Department", "EST", "Estate", 1);
        SeedDimensionValue(db, tenantId, "FUND", "Fund", "CAPEX", "Capital", 2);
        await db.SaveChangesAsync();

        var firstRequest = CreateRequest(tenantId, expense.Id, payable.Id);
        firstRequest.Lines[0].Dimensions = new[]
        {
            new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "EST" },
            new FinancePostingDimensionValueDto { DimensionCode = "FUND", ValueCode = "CAPEX" }
        };
        var service = CreateService(db, tenantId);
        var first = await service.PostAsync(firstRequest);

        var secondRequest = CreateRequest(tenantId, expense.Id, payable.Id);
        secondRequest.Lines[0].Dimensions = new[]
        {
            new FinancePostingDimensionValueDto { DimensionCode = "fund", ValueCode = "capex" },
            new FinancePostingDimensionValueDto { DimensionCode = "department", ValueCode = "est" }
        };
        var second = await service.PostAsync(secondRequest);

        var sets = await db.FinanceDimensionSets.Include(x => x.Items).ToListAsync();
        sets.Should().ContainSingle();
        sets[0].Items.Should().HaveCount(2);
        sets[0].DisplayValue.Should().Be("DEPARTMENT=EST · FUND=CAPEX");

        var firstLines = await db.AccountTransactions.Where(x => x.JournalEntryId == first.JournalEntryId)
            .OrderBy(x => x.LineNumber).ToListAsync();
        var secondLines = await db.AccountTransactions.Where(x => x.JournalEntryId == second.JournalEntryId)
            .OrderBy(x => x.LineNumber).ToListAsync();
        firstLines[0].FinanceDimensionSetId.Should().Be(sets[0].Id);
        secondLines[0].FinanceDimensionSetId.Should().Be(sets[0].Id);
        firstLines[0].FinanceDimensionSnapshotId.Should().NotBeNull();
        secondLines[0].FinanceDimensionSnapshotId.Should().NotBeNull();
        firstLines[0].FinanceDimensionSnapshotId!.Value.Should().NotBe(
            secondLines[0].FinanceDimensionSnapshotId!.Value,
            "each posting line freezes exact evidence even when its canonical set is reused");
        firstLines[1].FinanceDimensionSetId.Should().BeNull("legacy and control lines remain compatible while adapters are certified");
        secondLines[1].FinanceDimensionSetId.Should().BeNull();
        var snapshots = await db.FinanceDimensionSnapshots.Include(snapshot => snapshot.Items).ToListAsync();
        snapshots.Should().HaveCount(2);
        snapshots.Should().OnlyContain(snapshot =>
            snapshot.FinanceDimensionSetId == sets[0].Id &&
            snapshot.SnapshotSource == "PostingResolution" &&
            snapshot.SnapshotQuality == "Exact");
        snapshots.SelectMany(snapshot => snapshot.Items)
            .Should().OnlyContain(item =>
                (item.DimensionNameSnapshot == "Department" || item.DimensionNameSnapshot == "Fund") &&
                (item.DimensionValueNameSnapshot == "Estate" || item.DimensionValueNameSnapshot == "Capital"));
    }

    [Fact]
    [Trait("Category", "FinanceDimensions")]
    public async Task PostAsync_ShouldRejectDimensionValueFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTHER");
        SeedOpenPeriod(db, tenantId);
        var expense = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var payable = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        SeedDimensionValue(db, otherTenantId, "DEPARTMENT", "Department", "EST", "Estate", 1);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, expense.Id, payable.Id);
        request.Lines[0].Dimensions = new[]
        {
            new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "EST" }
        };

        var act = () => CreateService(db, tenantId).PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("One or more Finance dimensions were not found or are inactive for this tenant.");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FinanceDimensionSets.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "FinanceDimensions")]
    public async Task PostAsync_ShouldRejectArbitraryStoredDimensionSetOnOrdinaryPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var expense = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var payable = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        SeedDimensionValue(db, tenantId, "DEPARTMENT", "Department", "EST", "Estate", 1);
        await db.SaveChangesAsync();

        var original = CreateRequest(tenantId, expense.Id, payable.Id);
        original.Lines[0].Dimensions = new[]
        {
            new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "EST" }
        };
        await CreateService(db, tenantId).PostAsync(original);
        var storedSetId = await db.FinanceDimensionSets.Select(x => x.Id).SingleAsync();

        var forged = CreateRequest(tenantId, expense.Id, payable.Id);
        forged.Lines[0].FinanceDimensionSetId = storedSetId;
        var act = () => CreateService(db, tenantId).PostAsync(forged);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stored Finance dimension-set IDs may only be reused from an exact Finance journal line.");
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "FinanceDimensions")]
    public async Task Reversal_ShouldPreserveExactHistoricalDimensionSet_AfterValueDeactivation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var cash = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var revenue = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var department = SeedDimensionValue(db, tenantId, "DEPARTMENT", "Department", "SALES", "Sales", 1);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, cash.Id, revenue.Id);
        foreach (var line in request.Lines)
        {
            line.Dimensions = new[]
            {
                new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "SALES" }
            };
        }
        var service = CreateService(db, tenantId);
        var original = await service.PostAsync(request);
        var originalSetId = await db.FinanceDimensionSets.Select(x => x.Id).SingleAsync();

        department.IsActive = false;
        department.Name = "Renamed after posting";
        await db.SaveChangesAsync();

        var plan = await service.GetReversalPlanAsync(original.PostingEventId, "Correct classification", new DateTime(2026, 7, 5));
        plan.ReversalLines.Should().OnlyContain(x => x.FinanceDimensionSetId == originalSetId);
        var reversal = await service.ReverseAsync(
            original.PostingEventId,
            plan.Reason,
            plan.ReversalDate);

        var reversalLines = await db.AccountTransactions.Where(x => x.JournalEntryId == reversal.JournalEntryId).ToListAsync();
        reversalLines.Should().OnlyContain(x => x.FinanceDimensionSetId == originalSetId);
        (await db.FinanceDimensionSets.CountAsync()).Should().Be(1);
        (await db.FinanceDimensionSetItems.SingleAsync()).DimensionValueNameSnapshot.Should().Be("Sales");
        var snapshots = await db.FinanceDimensionSnapshots.Include(snapshot => snapshot.Items).ToListAsync();
        snapshots.Should().HaveCount(4);
        snapshots.Select(snapshot => snapshot.Id).Should().OnlyHaveUniqueItems();
        snapshots.Should().OnlyContain(snapshot => snapshot.FinanceDimensionSetId == originalSetId);
        snapshots.SelectMany(snapshot => snapshot.Items).Should().OnlyContain(item =>
            item.DimensionValueNameSnapshot == "Sales" && item.SnapshotQuality == "Exact");
    }

    [Fact]
    [Trait("Category", "AccountingBookAuthority")]
    public async Task PostAsync_ShouldRejectPseudoBookAtSingleBookBoundary()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var credit = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        await db.SaveChangesAsync();
        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        request.AccountingBookCode = "ALL_ACTIVE_BOOKS";
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());

        var action = () => CreateService(db, tenantId, financeAuditService: audit.Object).PostAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ALL_ACTIVE_BOOKS must be expanded*");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        audit.Verify(item => item.RecordAsync(
            It.Is<FinanceAuditEventDto>(entry =>
                entry.EventType == FinanceAuditEvents.PostingBlockedAccountingBookAuthority
                && entry.Reason == "BOOK_CODE_PSEUDO"
                && entry.ResourceId == "ALL_ACTIVE_BOOKS"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("IFRS", true)]
    [InlineData("LOCAL_STATUTORY", false)]
    [InlineData("UNKNOWN", false)]
    [Trait("Category", "AccountingBookAuthority")]
    public async Task ProcurementV2Boundary_RejectsDisabledUnmappedOrWrongBookWithoutPosting(
        string requestedBook,
        bool disableDebitMapping)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1040", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4930", AccountType.Revenue);
        await db.SaveChangesAsync();
        if (disableDebitMapping)
        {
            (await db.AccountAccountingBooks.SingleAsync(item => item.AccountId == debit.Id)).IsEnabled = false;
            await db.SaveChangesAsync();
        }
        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        request.SourceModule = "Procurement";
        request.OriginModuleCode = "PROC";
        request.SourceDocumentType = "SupplierOnboardingTokenPayment";
        request.AccountingBookCode = requestedBook;
        request.IdempotencyKey = $"PROCUREMENT|SUPPLIER-ONBOARDING|{request.SourceDocumentId:N}|{requestedBook}|POST";
        var action = () => CreateService(db, tenantId).PostAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>();
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "AccountingBookAuthority")]
    public async Task ExactReversal_ShouldUseDisabledHistoricalMapping_AndRemainIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var credit = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var original = await service.PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));

        foreach (var mapping in await db.AccountAccountingBooks.ToListAsync())
            mapping.IsEnabled = false;
        (await db.AccountingBooks.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();

        var reversal = await service.ReverseAsync(original.PostingEventId, "Correct exact posting", new DateTime(2026, 7, 5));
        var duplicate = await service.ReverseAsync(original.PostingEventId, "Correct exact posting", new DateTime(2026, 7, 5));

        reversal.WasDuplicate.Should().BeFalse();
        duplicate.WasDuplicate.Should().BeTrue();
        duplicate.JournalEntryId.Should().Be(reversal.JournalEntryId);
        (await db.JournalEntries.CountAsync()).Should().Be(2);
        (await db.AccountBalances.ToListAsync()).Should().OnlyContain(item => item.ClosingBalance == 0m);
    }

    [Fact]
    [Trait("Category", "FinanceRounding")]
    public async Task Rounded_invoice_replay_and_exact_reversal_preserve_one_frozen_evidence_record()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var ar = SeedAccount(db, tenantId, "1200", AccountType.Asset);
        var revenue = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var gain = SeedAccount(db, tenantId, "4099", AccountType.Revenue);
        var loss = SeedAccount(db, tenantId, "6099", AccountType.Expense);
        var sourceId = Guid.NewGuid();
        db.Invoices.Add(new Invoice
        {
            Id = sourceId, TenantId = tenantId, InvoiceNumber = "ROUND-ENGINE-1",
            CustomerName = "Rounding customer", BusinessPartnerId = Guid.NewGuid(),
            CurrencyCode = "GHS", ExchangeRate = 1m, TotalAmount = 10.03m,
            BaseCurrencyAmount = 10.03m, InvoiceDate = new DateTime(2026, 7, 4)
        });
        var settings = db.FinanceSettings.Local.Single(x => x.TenantId == tenantId);
        settings.InvoiceRoundingEnabled = true;
        settings.InvoiceRoundingIncrement = 0.05m;
        settings.InvoiceRoundingMethod = GovernedRoundingMethod.Nearest;
        settings.InvoiceRoundingGainAccountId = gain.Id;
        settings.InvoiceRoundingLossAccountId = loss.Id;
        await db.SaveChangesAsync();
        FinancePostingRequestV2Dto Request() => new()
        {
            SourceModule = "AR", SourceDocumentType = "CustomerInvoice", SourceDocumentId = sourceId,
            SourceDocumentTenantId = tenantId, PostingAction = "Post", SourceDocumentReference = "ROUND-ENGINE-1",
            Description = "Rounded invoice", PostingDate = new DateTime(2026, 7, 4), JournalType = "AR Invoice",
            AccountingBookCode = "IFRS", FunctionalCurrencyCode = "GHS",
            IdempotencyKey = $"AR:CustomerInvoice:{tenantId:N}:{sourceId:N}:Post", ReturnExistingOnDuplicate = true,
            Lines =
            [
                new FinancePostingLineDto { AccountId = ar.Id, DebitAmount = 10.03m,
                    TransactionCurrency = "GHS", TransactionDebitAmount = 10.03m,
                    TransactionTag = "AR-Control", LineNumber = 1 },
                new FinancePostingLineDto { AccountId = revenue.Id, CreditAmount = 10.03m,
                    TransactionCurrency = "GHS", TransactionCreditAmount = 10.03m,
                    TransactionTag = "AR-Revenue", LineNumber = 2 }
            ]
        };
        var engine = CreateService(db, tenantId);
        var producer = new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice);

        var original = await engine.PostAsync(Request(), producer);
        var originalEvent = await db.FinancePostingEvents.SingleAsync(x => x.Id == original.PostingEventId);
        var evidenceId = originalEvent.FinanceRoundingEvidenceId;
        evidenceId.Should().NotBeNull();
        settings.InvoiceRoundingIncrement = 1m;
        settings.InvoiceRoundingMethod = GovernedRoundingMethod.Down;
        await db.SaveChangesAsync();

        var replay = await engine.PostAsync(Request(), producer);
        replay.WasDuplicate.Should().BeTrue();
        replay.JournalEntryId.Should().Be(original.JournalEntryId);
        (await db.FinanceRoundingEvidence.CountAsync()).Should().Be(1);
        (await db.Invoices.SingleAsync(x => x.Id == sourceId)).TotalAmount.Should().Be(10.05m);

        var reversal = await engine.ReverseAsync(original.PostingEventId,
            "Reverse frozen rounded invoice", new DateTime(2026, 7, 5));
        var duplicateReversal = await engine.ReverseAsync(original.PostingEventId,
            "Reverse frozen rounded invoice", new DateTime(2026, 7, 5));
        duplicateReversal.WasDuplicate.Should().BeTrue();
        duplicateReversal.JournalEntryId.Should().Be(reversal.JournalEntryId);
        var events = await db.FinancePostingEvents.OrderBy(x => x.PostingAction).ToListAsync();
        events.Should().HaveCount(2).And.OnlyContain(x => x.FinanceRoundingEvidenceId == evidenceId);
        var originalLines = await db.AccountTransactions.Where(x => x.JournalEntryId == original.JournalEntryId)
            .OrderBy(x => x.LineNumber).ToListAsync();
        var reversalLines = await db.AccountTransactions.Where(x => x.JournalEntryId == reversal.JournalEntryId)
            .OrderBy(x => x.LineNumber).ToListAsync();
        reversalLines.Should().HaveSameCount(originalLines);
        for (var index = 0; index < originalLines.Count; index++)
        {
            reversalLines[index].AccountId.Should().Be(originalLines[index].AccountId);
            reversalLines[index].DebitAmount.Should().Be(originalLines[index].CreditAmount);
            reversalLines[index].CreditAmount.Should().Be(originalLines[index].DebitAmount);
            reversalLines[index].TransactionDebitAmount.Should().Be(originalLines[index].TransactionCreditAmount);
            reversalLines[index].TransactionCreditAmount.Should().Be(originalLines[index].TransactionDebitAmount);
        }
    }

    [Fact]
    [Trait("Category", "TaxPrecision")]
    public async Task SupplierDebitNoteExactReversal_ShouldCloneSignedPrecisionEvidence_AndReplayIdempotently()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "6100", AccountType.Expense);
        var credit = SeedAccount(db, tenantId, "2100", AccountType.Liability);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        request.SourceModule = "AP";
        request.OriginModuleCode = "FIN";
        request.SourceDocumentType = "SupplierDebitNote";
        request.IdempotencyKey = $"AP:SupplierDebitNote:{tenantId:N}:{request.SourceDocumentId:N}:Post";
        var original = await service.PostAsync(request);
        var taxId = Guid.NewGuid();
        var sourceLineId = Guid.NewGuid();
        db.Set<TaxCalculation>().Add(new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentType = request.SourceDocumentType,
            DocumentId = request.SourceDocumentId,
            DocumentLineId = sourceLineId,
            TaxId = taxId,
            PostingAccountId = credit.Id,
            CurrencyCode = "X04",
            CurrencyDecimalPlaces = 4,
            BaseAmount = 100m,
            TaxableAmount = 100m,
            TaxRate = 15m,
            TaxAmount = 15m,
            RawTaxAmount = 15.004m,
            RoundingAdjustment = -0.004m,
            AllocationSequence = 1,
            TaxRoundingScope = TaxRoundingScope.Document,
            TaxRoundingMethod = GovernedRoundingMethod.Up,
            TaxRoundingIncrement = 0.0001m,
            CompoundBasis = CompoundBasis.BaseOnly,
            CalculationOrder = 1,
            CalculationDate = request.PostingDate
        });
        await db.SaveChangesAsync();

        var reversalDate = new DateTime(2026, 7, 5);
        var reversal = await service.ReverseAsync(original.PostingEventId, "Correct tax posting", reversalDate);
        var duplicate = await service.ReverseAsync(original.PostingEventId, "Correct tax posting", reversalDate);

        reversal.WasDuplicate.Should().BeFalse();
        duplicate.WasDuplicate.Should().BeTrue();
        var evidence = await db.Set<TaxCalculation>()
            .Where(item => item.TenantId == tenantId
                && item.DocumentType == "FinancePostingEventReversal"
                && item.DocumentId == original.PostingEventId)
            .ToListAsync();
        evidence.Should().ContainSingle();
        evidence[0].DocumentLineId.Should().Be(sourceLineId);
        evidence[0].TaxId.Should().Be(taxId);
        evidence[0].BaseAmount.Should().Be(-100m);
        evidence[0].TaxableAmount.Should().Be(-100m);
        evidence[0].TaxRate.Should().Be(15m);
        evidence[0].TaxAmount.Should().Be(-15m);
        evidence[0].RawTaxAmount.Should().Be(-15.004m);
        evidence[0].RoundingAdjustment.Should().Be(0.004m);
        evidence[0].CurrencyCode.Should().Be("X04");
        evidence[0].CurrencyDecimalPlaces.Should().Be(4);
        evidence[0].AllocationSequence.Should().Be(1);
        evidence[0].TaxRoundingScope.Should().Be(TaxRoundingScope.Document);
        evidence[0].TaxRoundingMethod.Should().Be(GovernedRoundingMethod.Up);
        evidence[0].TaxRoundingIncrement.Should().Be(0.0001m);
        evidence[0].CalculationDate.Should().Be(reversalDate);
    }

    [Fact]
    [Trait("Category", "TaxPrecision")]
    public void PrecisionMigrations_TargetPhysicalInvoiceTables_AndPreserveUnknownLegacyEvidence()
    {
        var migration = new FinanceTaxPrecisionCompletion();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(FinanceTaxPrecisionCompletion)
            .GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var alteredTables = builder.Operations.OfType<AlterColumnOperation>()
            .Select(operation => operation.Table)
            .ToArray();
        alteredTables.Should().Contain(["Invoices", "InvoiceLineItem", "VendorInvoice", "VendorInvoiceLineItem"]);
        alteredTables.Should().NotContain(["InvoiceLineItems", "VendorInvoices", "VendorInvoiceLineItems"]);

        var evidenceColumns = builder.Operations.OfType<AddColumnOperation>()
            .Where(operation => operation.Table == "TaxCalculations")
            .ToDictionary(operation => operation.Name);
        foreach (var column in new[]
                 {
                     "CurrencyCode", "CurrencyDecimalPlaces", "RawTaxAmount", "RoundingAdjustment",
                     "AllocationSequence", "TaxRoundingScope", "TaxRoundingMethod", "TaxRoundingIncrement"
                 })
        {
            evidenceColumns.Should().ContainKey(column);
            evidenceColumns[column].IsNullable.Should().BeTrue();
            evidenceColumns[column].DefaultValue.Should().BeNull();
        }

        builder.Operations.OfType<AddCheckConstraintOperation>()
            .Should().ContainSingle(operation =>
                operation.Name == "CK_TaxCalculations_PrecisionEvidence"
                && operation.Sql.Contains("[RawTaxAmount] IS NULL")
                && operation.Sql.Contains("[TaxRoundingIncrement] > 0"));

        using var discoveryContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=FinancePrecisionMigrationDiscovery;Trusted_Connection=True")
            .Options);
        discoveryContext.GetService<IMigrationsAssembly>().Migrations.Keys.Should().Contain([
            "20261002183000_FinanceTaxPrecisionCompletion",
            "20261002213000_FinancePrecisionStorageCorrections"]);
    }

    public static TheoryData<string> ExactReversalMutationCases => new()
    {
        { "account" },
        { "side" },
        { "amount" },
        { "currency" },
        { "rate" },
        { "source-line" },
        { "dimensions" }
    };

    [Theory]
    [MemberData(nameof(ExactReversalMutationCases))]
    [Trait("Category", "AccountingBookAuthority")]
    public async Task PostAsync_ShouldRejectAlteredExactReversalEvidence_ForV2(string mutation)
    {
        var fixture = await CreateExactReversalFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(db, fixture.TenantId);
        var original = await service.PostAsync(fixture.OriginalRequest);
        var plan = await service.GetReversalPlanAsync(
            original.PostingEventId,
            "Reject altered immutable evidence",
            new DateTime(2026, 7, 5));
        var lines = plan.ReversalLines.ToList();

        switch (mutation)
        {
            case "account":
                lines[0].AccountId = fixture.AlternateAccountId;
                break;
            case "side":
                foreach (var line in lines)
                {
                    (line.DebitAmount, line.CreditAmount) = (line.CreditAmount, line.DebitAmount);
                    (line.TransactionDebitAmount, line.TransactionCreditAmount) =
                        (line.TransactionCreditAmount, line.TransactionDebitAmount);
                }
                break;
            case "amount":
                lines[0].CreditAmount = 3000m;
                lines[0].TransactionCreditAmount = 200m;
                lines[0].ForeignCurrencyAmount = 200m;
                lines[1].DebitAmount = 3000m;
                lines[1].TransactionDebitAmount = 3000m;
                break;
            case "currency":
                lines[0].TransactionCurrency = "EUR";
                lines[0].ExchangeRateId = fixture.EurRateId;
                break;
            case "rate":
                lines[0].ExchangeRateId = fixture.AlternateUsdRateId;
                break;
            case "source-line":
                lines[0].SourceDocumentLineId = Guid.NewGuid();
                break;
            case "dimensions":
                lines[0].FinanceDimensionSetId = null;
                lines[0].Dimensions = new[]
                {
                    new FinancePostingDimensionValueDto { DimensionCode = "PROJECT", ValueCode = "ALT" }
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var action = () => PostDirectReversalAsync(
            service,
            fixture.TenantId,
            plan,
            lines);

        await action.Should().ThrowAsync<InvalidOperationException>();
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "AccountingBookAuthority")]
    public async Task PostAsync_ShouldAcceptUnchangedExactReversalPlan_ForV2()
    {
        var fixture = await CreateExactReversalFixtureAsync();
        await using var db = fixture.Db;
        var service = CreateService(db, fixture.TenantId);
        var original = await service.PostAsync(fixture.OriginalRequest);
        var plan = await service.GetReversalPlanAsync(
            original.PostingEventId,
            "Preserve immutable evidence",
            new DateTime(2026, 7, 5));

        var reversal = await PostDirectReversalAsync(
            service,
            fixture.TenantId,
            plan,
            plan.ReversalLines);

        reversal.PostingStatus.Should().Be("Posted");
        (await db.JournalEntries.CountAsync()).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task PostAsync_ShouldPersistStableBookIdentityAndReturnSameBookRetry()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        await db.SaveChangesAsync();
        var book = await db.AccountingBooks.SingleAsync();
        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        request.IdempotencyKey = "C1|SAME-BOOK|POST";
        var service = CreateService(db, tenantId);

        var first = await service.PostAsync(request);
        var retry = await service.PostAsync(request);

        retry.WasDuplicate.Should().BeTrue();
        retry.PostingEventId.Should().Be(first.PostingEventId);
        retry.JournalEntryId.Should().Be(first.JournalEntryId);
        var postingEvent = await db.FinancePostingEvents.Include(item => item.JournalEntry)!
            .ThenInclude(journal => journal!.Transactions).SingleAsync();
        postingEvent.AccountingBookId.Should().Be(book.Id);
        postingEvent.BookClassification.Should().Be(book.Code);
        postingEvent.JournalEntry!.AccountingBookId.Should().Be(book.Id);
        postingEvent.JournalEntry.Transactions.Should().OnlyContain(line =>
            line.AccountingBookId == book.Id && line.BookClassification == book.Code);
    }

    [Fact]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task PostAsync_ShouldRejectConflictingPayloadForSameBookIdempotencyIdentity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var first = CreateRequest(tenantId, debit.Id, credit.Id);
        first.IdempotencyKey = "C1|PAYLOAD-CONFLICT|POST";
        await service.PostAsync(first);
        var conflicting = CreateRequest(tenantId, debit.Id, credit.Id);
        conflicting.IdempotencyKey = first.IdempotencyKey;
        conflicting.SourceDocumentId = Guid.NewGuid();
        conflicting.Description = "Conflicting economic payload";

        var action = () => service.PostAsync(conflicting);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*conflicting canonical request evidence*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("source-type")]
    [InlineData("source-reference")]
    [InlineData("idempotency")]
    [InlineData("line-description")]
    [InlineData("line-reference")]
    [InlineData("line-notes")]
    [InlineData("line-tag")]
    [InlineData("line-source-id")]
    [InlineData("budget-id")]
    [InlineData("budget-source")]
    [InlineData("tax-evidence")]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task PostAsync_ShouldFingerprintEveryNormalizedProducerEvidenceField(string mutation)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var first = CreateRequest(tenantId, debit.Id, credit.Id);
        first.IdempotencyKey = "C1|FINGERPRINT|POST";
        await service.PostAsync(first);

        var retry = CreateRequest(tenantId, debit.Id, credit.Id);
        retry.SourceDocumentId = first.SourceDocumentId;
        retry.IdempotencyKey = first.IdempotencyKey;
        switch (mutation)
        {
            case "origin": retry.OriginModuleCode = "PROC"; break;
            case "source-type": retry.SourceDocumentType = "ChangedDocument"; break;
            case "source-reference": retry.SourceDocumentReference = "SRC-CHANGED"; break;
            case "idempotency": retry.IdempotencyKey = "C1|FINGERPRINT|CHANGED"; break;
            case "line-description": retry.Lines[0].Description = "Changed line"; break;
            case "line-reference": retry.Lines[0].SourceReferenceNumber = "LINE-CHANGED"; break;
            case "line-notes": retry.Lines[0].Notes = "Changed notes"; break;
            case "line-tag": retry.Lines[0].TransactionTag = "Changed tag"; break;
            case "line-source-id": retry.Lines[0].SourceDocumentLineId = Guid.NewGuid(); break;
            case "budget-id": retry.BudgetReservationIds = [Guid.NewGuid()]; break;
            case "budget-source": retry.BudgetReservationSourceDocumentType = "PurchaseOrder"; break;
            case "tax-evidence": retry.TaxCalculationSnapshots =
                [
                    new FinanceTaxCalculationSnapshotDto
                    {
                        DocumentType = "Invoice", DocumentId = retry.SourceDocumentId,
                        TaxId = Guid.NewGuid(), BaseAmount = 100m, TaxableAmount = 100m,
                        TaxRate = 0.15m, TaxAmount = 15m, CalculationOrder = 1,
                        CalculationDate = retry.PostingDate
                    }
                ]; break;
        }

        await FluentActions.Invoking(() => service.PostAsync(retry)).Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*conflicting canonical request evidence*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task PostAsync_ShouldFailClosedForLegacyEventWithoutCanonicalRequestFingerprint()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        await db.SaveChangesAsync();
        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        var service = CreateService(db, tenantId);
        await service.PostAsync(request);
        var stored = await db.FinancePostingEvents.SingleAsync();
        stored.RequestFingerprint = null;
        stored.RequestFingerprintVersion = null;
        await db.SaveChangesAsync();

        await FluentActions.Invoking(() => service.PostAsync(request)).Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("LEGACY_POSTING_RETRY_UNAVAILABLE:*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task PostAsync_ShouldRejectDifferentBookRepresentationAsParallelDisabled(bool matchByIdempotency)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var localBook = SeedAdditionalBook(db, tenantId, "LOCAL_STATUTORY", debit.Id, credit.Id);
        var primaryBook = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.IsDefault);
        localBook.BookType = AccountingBookType.ParallelFull;
        localBook.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        localBook.BaseAccountingBookId = primaryBook.Id;
        localBook.FunctionalCurrencyCode = "USD";
        localBook.ReplicationStartDate = new DateTime(2027, 1, 1);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var first = CreateRequest(tenantId, debit.Id, credit.Id);
        first.IdempotencyKey = "C1|PARALLEL-GATE|POST";
        await service.PostAsync(first);
        var second = CreateRequest(tenantId, debit.Id, credit.Id);
        second.AccountingBookCode = localBook.Code;
        if (matchByIdempotency)
        {
            second.SourceDocumentId = Guid.NewGuid();
            second.IdempotencyKey = first.IdempotencyKey;
        }
        else
        {
            second.SourceDocumentId = first.SourceDocumentId;
            second.IdempotencyKey = "C1|PARALLEL-GATE|LOCAL|POST";
        }

        var action = () => service.PostAsync(second);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PARALLEL_DIRECT_POSTING_FORBIDDEN:*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "AccountingBookModelV2")]
    public async Task PostAsync_ShouldFailAtomicallyWhenActiveParallelHasNoApprovedRate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var primary = db.AccountingBooks.Local.Single(item => item.Code == "IFRS");
        primary.BookType = AccountingBookType.PrimaryFull;
        primary.FunctionalCurrencyCode = "GHS";
        primary.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var parallel = SeedAdditionalBook(db, tenantId, "USD_PARALLEL", debit.Id, credit.Id);
        parallel.BookType = AccountingBookType.ParallelFull;
        parallel.BaseAccountingBookId = primary.Id;
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId)
            .PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));

        var failure = await action.Should().ThrowAsync<InvalidOperationException>();
        failure.WithMessage("PARALLEL_EXCHANGE_RATE_REQUIRED:*");
        failure.Which.Message.Should().Contain("GHS/USD")
            .And.Contain("No Primary or Parallel ledger posting was committed")
            .And.Contain("Finance > Exchange Rates")
            .And.Contain("retry the posting action");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "AccountingBookModelV2")]
    public async Task PostAsync_ShouldRejectStaleOpenEndedDailyRateForParallelReplica()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var primary = db.AccountingBooks.Local.Single(item => item.Code == "IFRS");
        primary.BookType = AccountingBookType.PrimaryFull;
        primary.FunctionalCurrencyCode = "GHS";
        primary.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var parallel = SeedAdditionalBook(db, tenantId, "USD_PARALLEL", debit.Id, credit.Id);
        parallel.BookType = AccountingBookType.ParallelFull;
        parallel.BaseAccountingBookId = primary.Id;
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var staleRate = SeedExchangeRate(db, tenantId, "USD", 12.5m);
        staleRate.EffectiveDate = new DateTime(2024, 12, 15);
        staleRate.EndDate = null;
        await db.SaveChangesAsync();

        var action = () => CreateService(db, tenantId)
            .PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));

        var failure = await action.Should().ThrowAsync<InvalidOperationException>();
        failure.WithMessage("PARALLEL_EXCHANGE_RATE_REQUIRED:*");
        failure.Which.Message.Should().Contain("exact-date Daily")
            .And.Contain("2026-07-04");
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "AccountingBookModelV2")]
    public async Task PostAsync_ShouldCreateImmutableSourceToTargetParallelReplica()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var primary = db.AccountingBooks.Local.Single(item => item.Code == "IFRS");
        primary.BookType = AccountingBookType.PrimaryFull;
        primary.FunctionalCurrencyCode = "GHS";
        primary.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var parallel = SeedAdditionalBook(db, tenantId, "USD_PARALLEL", debit.Id, credit.Id);
        parallel.BookType = AccountingBookType.ParallelFull;
        parallel.BaseAccountingBookId = primary.Id;
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var rate = SeedExchangeRate(db, tenantId, "USD", 12.5m);
        SeedDimensionValue(db, tenantId, "DEPARTMENT", "Department", "FIN", "Finance", 1);
        await db.SaveChangesAsync();

        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        request.Lines[0].Dimensions = new[]
        {
            new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "FIN" }
        };
        var result = await CreateService(db, tenantId).PostAsync(request);

        var replica = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.AccountingBookId == parallel.Id);
        var primaryJournal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        replica.ReplicatedFromJournalEntryId.Should().Be(result.JournalEntryId);
        replica.ReplicationExchangeRateId.Should().Be(rate.Id);
        replica.ReplicationExchangeRate.Should().Be(0.08m);
        replica.ReplicationRateDate.Should().Be(new DateTime(2026, 7, 4));
        replica.TotalDebitAmount.Should().Be(8m);
        replica.TotalCreditAmount.Should().Be(8m);
        replica.Transactions.Should().OnlyContain(item => item.FunctionalCurrencyCode == "USD"
            && item.TransactionCurrency == "GHS" && item.ExchangeRate == 0.08m);
        var primaryDimensionLine = primaryJournal.Transactions.Single(item => item.LineNumber == 1);
        var replicaDimensionLine = replica.Transactions.Single(item => item.LineNumber == 1);
        primaryDimensionLine.FinanceDimensionSnapshotId.Should().NotBeNull();
        replicaDimensionLine.FinanceDimensionSnapshotId.Should().NotBeNull();
        replicaDimensionLine.FinanceDimensionSnapshotId!.Value.Should().NotBe(primaryDimensionLine.FinanceDimensionSnapshotId!.Value,
            "each Primary and Parallel ledger line owns distinct immutable dimension evidence");
        var snapshots = await db.FinanceDimensionSnapshots.Include(item => item.Items)
            .Where(item => item.Id == primaryDimensionLine.FinanceDimensionSnapshotId
                || item.Id == replicaDimensionLine.FinanceDimensionSnapshotId)
            .ToListAsync();
        snapshots.Should().HaveCount(2);
        snapshots.Select(item => item.FinanceDimensionSetId).Distinct().Should().ContainSingle();
        snapshots.SelectMany(item => item.Items).Should().OnlyContain(item =>
            item.DimensionCodeSnapshot == "DEPARTMENT" && item.DimensionValueCodeSnapshot == "FIN");
    }

    [Fact]
    [Trait("Category", "AccountingBookModelV2")]
    public async Task ReverseAsync_ShouldReuseOriginalParallelReplicaRateAfterMarketRateChanges()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var primary = db.AccountingBooks.Local.Single(item => item.Code == "IFRS");
        primary.BookType = AccountingBookType.PrimaryFull;
        primary.FunctionalCurrencyCode = "GHS";
        primary.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var parallel = SeedAdditionalBook(db, tenantId, "USD_PARALLEL", debit.Id, credit.Id);
        parallel.BookType = AccountingBookType.ParallelFull;
        parallel.BaseAccountingBookId = primary.Id;
        parallel.FunctionalCurrencyCode = "USD";
        parallel.ReplicationStartDate = new DateTime(2026, 1, 1);
        parallel.LifecycleStatus = AccountingBookLifecycleStatus.Active;
        var originalRate = SeedExchangeRate(db, tenantId, "USD", 12.5m);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var original = await service.PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));

        originalRate.IsActive = false;
        originalRate.EndDate = new DateTime(2026, 7, 4);
        db.ExchangeRates.Add(new ExchangeRate
        {
            TenantId = tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD",
            Rate = 0.1m, InverseRate = 10m, EffectiveDate = new DateTime(2026, 7, 5),
            RateType = ExchangeRateType.Daily, QuoteSide = ExchangeRateQuoteSide.Mid,
            IsActive = true, RateSource = "Later market rate", ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(), CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await service.ReverseAsync(original.PostingEventId, "Reverse with original FX evidence", new DateTime(2026, 7, 5));

        var replicas = await db.JournalEntries.Where(item => item.AccountingBookId == parallel.Id)
            .OrderBy(item => item.EntryDate).ThenBy(item => item.CreatedAt).ToListAsync();
        replicas.Should().HaveCount(2);
        replicas.Should().OnlyContain(item => item.ReplicationExchangeRateId == originalRate.Id
            && item.ReplicationExchangeRate == 0.08m && item.ReplicationRateDate == new DateTime(2026, 7, 4));
        replicas[1].OriginalJournalEntryId.Should().Be(replicas[0].Id);
    }

    [Fact]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task ReverseAsync_ShouldPreserveOriginalRelationalBookIdentity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);
        var original = await service.PostAsync(CreateRequest(tenantId, debit.Id, credit.Id));
        var reversal = await service.ReverseAsync(original.PostingEventId, "C1 exact reversal", new DateTime(2026, 7, 5));

        var events = await db.FinancePostingEvents.Include(item => item.JournalEntry)!
            .ThenInclude(journal => journal!.Transactions).OrderBy(item => item.PostingDate).ToListAsync();
        events.Should().HaveCount(2);
        events.Should().OnlyContain(item => item.AccountingBookId == events[0].AccountingBookId
            && item.BookClassification == events[0].BookClassification
            && item.JournalEntry!.AccountingBookId == events[0].AccountingBookId
            && item.JournalEntry.Transactions.All(line => line.AccountingBookId == events[0].AccountingBookId));
        reversal.JournalEntryId.Should().NotBe(original.JournalEntryId);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("transaction")]
    [Trait("Category", "MultiBookIdentityC1")]
    public async Task PostAsync_ShouldFailClosedWhenStoredBookIdentityConflicts(string corruptedEvidence)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var debit = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var credit = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var localBook = SeedAdditionalBook(db, tenantId, "LOCAL_STATUTORY", debit.Id, credit.Id);
        await db.SaveChangesAsync();
        var request = CreateRequest(tenantId, debit.Id, credit.Id);
        var service = CreateService(db, tenantId);
        await service.PostAsync(request);
        var postingEvent = await db.FinancePostingEvents.Include(item => item.JournalEntry)!
            .ThenInclude(journal => journal!.Transactions).SingleAsync();
        if (corruptedEvidence == "event")
        {
            // AccountingBookId is an identifying FK and EF correctly forbids mutating it in
            // place. Corrupt the stored book code instead; the transaction branch below still
            // exercises an ID mismatch, while both prove replay fails closed on book identity.
            postingEvent.BookClassification = localBook.Code;
        }
        else
            postingEvent.JournalEntry!.Transactions.First().AccountingBookId = localBook.Id;
        await db.SaveChangesAsync();

        var action = () => service.PostAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Finance posting*");
    }

    [Fact]
    [Trait("Category", "MultiBookIdentityC1")]
    public void StableBookIdentityMigration_ShouldPreflightBackfillAndCreateBookQualifiedConstraints()
    {
        var sql = ArchivedMigrationSource.Read("20260905151918_AddStablePostingAccountingBookIdentity.cs");

        sql.Should().Contain("C1_BOOK_ID_PREFLIGHT_JOURNAL");
        sql.Should().Contain("C1_BOOK_ID_PREFLIGHT_TRANSACTION");
        sql.Should().Contain("C1_BOOK_ID_PREFLIGHT_EVENT");
        sql.Should().Contain("C1_BOOK_ID_BACKFILL_INCOMPLETE");
        sql.Should().Contain("ALL_ACTIVE_BOOKS");
        sql.Should().Contain("b.TenantId = j.TenantId");
        sql.Should().Contain("j.TenantId <> t.TenantId");
        sql.Should().Contain("Latin1_General_100_BIN2");
        sql.Should().Contain("DATALENGTH(j.BookClassification)");
        foreach (var token in new[] { "JournalEntries", "AccountingBookId", "FinancePostingEvents",
            "RequestFingerprint", "TenantId", "IdempotencyKey", "AccountTransactions", "JournalEntryId",
            "C1_BOOK_ID_DOWN_BLOCKED" }) sql.Should().Contain(token);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C1MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var discoveryContext = new ApplicationDbContext(options);
        var currentMigrationIds = discoveryContext.GetService<IMigrationsAssembly>().Migrations.Keys.ToArray();
        currentMigrationIds.Should().Contain("20260916132000_DisposableDevelopmentCurrentModelBaseline");
        currentMigrationIds.Should().Equal(currentMigrationIds.OrderBy(id => id, StringComparer.Ordinal));
        currentMigrationIds.Should().OnlyContain(id =>
            string.CompareOrdinal(id, "20260916132000_DisposableDevelopmentCurrentModelBaseline") >= 0);
        currentMigrationIds.Should().NotContain("20260905151918_AddStablePostingAccountingBookIdentity");
    }

    [Fact]
    [Trait("Category", "FinanceDimensions")]
    public void Migration_ShouldAddDimensionFoundationAndNullablePostedLineLinkage()
    {
        var source = ArchivedMigrationSource.Read("20260824190000_AddFinanceTransactionDimensions.cs");
        foreach (var token in new[]
        {
            "FinanceDimensionDefinitions",
            "FinanceDimensionValues",
            "FinanceDimensionSets",
            "FinanceDimensionSetItems",
            "FinanceDimensionAccountRules",
            "AccountTransactions", "FinanceDimensionSetId", "FinanceDimensionSets",
            "onDelete: ReferentialAction.Restrict", "migrationBuilder.DropColumn"
        }) source.Should().Contain(token);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-posting-engine-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }


    private static FinancePostingEngine CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IDictionary<string, string>? claims = null,
        IFinanceAuditService? financeAuditService = null)
    {
        var currentUser = CreateCurrentUser(tenantId, claims);
        return new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            financeAuditService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(
        Guid tenantId,
        IDictionary<string, string>? claims = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(claims ?? new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("finance-posting-engine-tests");
        return currentUser;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            CoaType = "Segmented",
            AccountSeparator = "-"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS Primary",
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS",
            IsDefault = true,
            IsActive = true,
            AllowsPosting = true
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false,
        bool isLocked = false)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "FY2026",
            PeriodCode = "2026",
            PeriodNumber = 1,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            PeriodDays = 365,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = isLocked
        };

        db.FiscalPeriods.Add(period);
        foreach (var book in db.AccountingBooks.Local.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToList())
        {
            db.AccountingBookPeriods.Add(new AccountingBookPeriod
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id, FiscalPeriodId = period.Id,
                PeriodStatus = isLocked ? AccountingBookPeriodStatus.Locked
                    : isClosed ? AccountingBookPeriodStatus.Closed
                    : isOpen ? AccountingBookPeriodStatus.Open : AccountingBookPeriodStatus.Future
            });
        }
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        string currencyCode = "GHS",
        bool isMultiCurrency = false)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = status,
            CurrencyCode = currencyCode,
            IsMultiCurrency = isMultiCurrency,
            AllowDirectPosting = true
        };

        db.Accounts.Add(account);
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        db.AccountAccountingBooks.Add(new AccountAccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountId = account.Id,
            AccountingBookId = book.Id,
            IsEnabled = true
        });
        return account;
    }

    private static AccountingBook SeedAdditionalBook(
        ApplicationDbContext db,
        Guid tenantId,
        string code,
        params Guid[] accountIds)
    {
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            IsActive = true,
            AllowsPosting = true
        };
        db.AccountingBooks.Add(book);
        foreach (var period in db.FiscalPeriods.Local.Where(item => item.TenantId == tenantId && !item.IsDeleted).ToList())
            db.AccountingBookPeriods.Add(new AccountingBookPeriod { Id = Guid.NewGuid(), TenantId = tenantId,
                AccountingBookId = book.Id, FiscalPeriodId = period.Id,
                PeriodStatus = period.IsLocked ? AccountingBookPeriodStatus.Locked : period.IsClosed ? AccountingBookPeriodStatus.Closed
                    : period.IsOpen ? AccountingBookPeriodStatus.Open : AccountingBookPeriodStatus.Future });
        foreach (var accountId in accountIds)
        {
            db.AccountAccountingBooks.Add(new AccountAccountingBook
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AccountId = accountId,
                AccountingBookId = book.Id, IsEnabled = true
            });
        }
        return book;
    }

    private static ModuleDefinition SeedModule(
        ApplicationDbContext db,
        Guid tenantId,
        string moduleCode,
        string moduleName)
    {
        var module = new ModuleDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleCode = moduleCode,
            ModuleName = moduleName,
            IsActive = true,
            IsSystem = true
        };
        db.ModuleDefinitions.Add(module);
        return module;
    }

    private static FinanceDimensionValue SeedDimensionValue(
        ApplicationDbContext db,
        Guid tenantId,
        string dimensionCode,
        string dimensionName,
        string valueCode,
        string valueName,
        int displayOrder)
    {
        var definition = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = dimensionCode,
            Name = dimensionName,
            Classification = "Analytical",
            ValueSourceType = "Lookup",
            IsActive = true,
            DisplayOrder = displayOrder
        };
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = valueCode,
            Name = valueName,
            EffectiveDate = new DateTime(2026, 1, 1),
            IsActive = true
        };
        db.FinanceDimensionDefinitions.Add(definition);
        db.FinanceDimensionValues.Add(value);
        return value;
    }

    private static ExchangeRate SeedExchangeRate(
        ApplicationDbContext db,
        Guid tenantId,
        string targetCurrencyCode,
        decimal rate)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = targetCurrencyCode,
            Rate = decimal.Round(1m / rate, 6, MidpointRounding.AwayFromZero),
            InverseRate = rate,
            EffectiveDate = new DateTime(2026, 7, 4),
            RateType = ExchangeRateType.Daily,
            RateSource = "Unit Test",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved
        };

        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private static async Task<ExactReversalFixture> CreateExactReversalFixtureAsync()
    {
        var tenantId = Guid.NewGuid();
        var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var cash = SeedAccount(db, tenantId, "1000", AccountType.Asset, isMultiCurrency: true);
        var revenue = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var alternateCash = SeedAccount(db, tenantId, "1010", AccountType.Asset, isMultiCurrency: true);
        var usdRate = SeedExchangeRate(db, tenantId, "USD", 15m);
        var alternateUsdRate = SeedExchangeRate(db, tenantId, "USD", 15m);
        alternateUsdRate.RateSource = "Alternative unit-test source";
        var eurRate = SeedExchangeRate(db, tenantId, "EUR", 15m);
        SeedDimensionValue(db, tenantId, "DEPARTMENT", "Department", "SALES", "Sales", 1);
        SeedDimensionValue(db, tenantId, "PROJECT", "Project", "ALT", "Alternative", 2);
        foreach (var accountId in new[] { cash.Id, alternateCash.Id })
        {
            foreach (var currency in new[] { "USD", "EUR" })
            {
                db.AccountCurrencyLinks.Add(new AccountCurrencyLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AccountId = accountId,
                    LinkedCurrencyCode = currency,
                    IsActive = true,
                    EffectiveDate = new DateTime(2026, 1, 1),
                    TransactionRateType = "Daily",
                    RevaluationRateType = "Month-End"
                });
            }
        }
        await db.SaveChangesAsync();

        var request = CreateForeignCurrencyRequest(tenantId, cash.Id, revenue.Id);
        request.Lines[0].ExchangeRateId = usdRate.Id;
        request.Lines[0].SourceDocumentLineId = Guid.NewGuid();
        request.Lines[1].SourceDocumentLineId = Guid.NewGuid();
        foreach (var line in request.Lines)
        {
            line.Dimensions = new[]
            {
                new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "SALES" }
            };
        }

        return new ExactReversalFixture(
            db,
            tenantId,
            alternateCash.Id,
            alternateUsdRate.Id,
            eurRate.Id,
            request);
    }

    private static Task<FinancePostingResultDto> PostDirectReversalAsync(
        FinancePostingEngine service,
        Guid tenantId,
        FinanceReversalPlanDto plan,
        IReadOnlyList<FinancePostingLineDto> lines)
    {
        return service.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "TEST",
            SourceDocumentType = "DirectReversalV2",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = plan.Reason,
            ReversalType = "Exact",
            PostingAction = "Reverse",
            Description = "Direct V2 exact reversal",
            PostingDate = plan.ReversalDate,
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = lines
        });
    }

    private sealed record ExactReversalFixture(
        ApplicationDbContext Db,
        Guid TenantId,
        Guid AlternateAccountId,
        Guid AlternateUsdRateId,
        Guid EurRateId,
        FinancePostingRequestV2Dto OriginalRequest);

    private static FinancePostingRequestV2Dto CreateRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestV2Dto
        {
            SourceModule = "TEST",
            SourceDocumentType = "TestDocument",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "SRC-001",
            Description = "Batch 4 posting engine test",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto
                {
                    AccountId = debitAccountId,
                    Description = "Cash",
                    DebitAmount = 100m
                },
                new FinancePostingLineDto
                {
                    AccountId = creditAccountId,
                    Description = "Revenue",
                    CreditAmount = 100m
                }
            }
        };
    }

    private static FinancePostingRequestV2Dto CreateForeignCurrencyRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestV2Dto
        {
            SourceModule = "TEST",
            SourceDocumentType = "ForeignCurrencyDocument",
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentTenantId = tenantId,
            PostingAction = "Post",
            SourceDocumentReference = "FX-001",
            Description = "Foreign currency posting engine test",
            PostingDate = new DateTime(2026, 7, 4),
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = new[]
            {
                new FinancePostingLineDto
                {
                    AccountId = debitAccountId,
                    Description = "USD cash",
                    DebitAmount = 1500m,
                    TransactionCurrency = "USD",
                    TransactionDebitAmount = 100m,
                    ForeignCurrencyAmount = 100m
                },
                new FinancePostingLineDto
                {
                    AccountId = creditAccountId,
                    Description = "Revenue",
                    CreditAmount = 1500m
                }
            }
        };
    }
}
