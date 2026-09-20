using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Cash;
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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CashBankTransactionPostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task ReceiptTransaction_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        var service = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Transaction.Id);

        result.JournalEntryId.Should().NotBeNull();
        result.IsPosted.Should().BeTrue();

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "CASHBANK" &&
            e.SourceDocumentType == "CashBankReceipt" &&
            e.SourceDocumentId == fixture.Transaction.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceDocumentType.Should().Be("CashBankReceipt");
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.OffsetAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.CashBankTransactionPostedAfterApproval && a.TenantId == tenantId)).Should().Be(1);
        (await db.AccountBalances.SingleAsync(x => x.AccountId == fixture.BankGlAccount.Id)).ClosingBalance.Should().Be(100m);
        (await db.AccountBalances.SingleAsync(x => x.AccountId == fixture.OffsetAccount.Id)).ClosingBalance.Should().Be(-100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankReversal")]
    [Trait("Category", "CashBank")]
    public async Task PostedReceipt_ShouldReverseWithCompensatingCashRowTraceAndIdempotency()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.Transaction.Id);

        var first = await service.ReverseAsync(fixture.Transaction.Id, new ReverseCashTransactionDto
        {
            Reason = "Receipt entered against the wrong offset account."
        });
        var retry = await service.ReverseAsync(fixture.Transaction.Id, new ReverseCashTransactionDto
        {
            Reason = "Receipt entered against the wrong offset account."
        });

        first.IsReversed.Should().BeTrue();
        retry.ReversalJournalEntryId.Should().Be(first.ReversalJournalEntryId);
        var correction = await db.Set<CashTransaction>().SingleAsync(item =>
            item.ReversalOfCashTransactionId == fixture.Transaction.Id);
        correction.TransactionType.Should().Be(CashTransactionType.Payment);
        correction.JournalEntryId.Should().Be(first.ReversalJournalEntryId);
        correction.IsPosted.Should().BeTrue();
        fixture.BankAccount.CurrentBalance.Should().Be(0m);
        fixture.BankAccount.AvailableBalance.Should().Be(0m);

        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentId == fixture.Transaction.Id &&
            item.SourceDocumentType == "CashBankReceipt" &&
            item.PostingAction == "Reverse")).Should().Be(1);
        var trace = await service.GetTraceAsync(fixture.Transaction.Id);
        trace.Should().NotBeNull();
        trace!.Postings.Should().HaveCount(2);
        trace.RelatedTransactions.Should().Contain(item => item.Id == correction.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankReversal")]
    [Trait("Category", "CashBank")]
    public async Task ReconciledPostedReceipt_ShouldBeBlockedFromReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.Transaction.Id);
        fixture.Transaction.IsReconciled = true;
        fixture.Transaction.ReconciliationId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var action = () => service.ReverseAsync(fixture.Transaction.Id, new ReverseCashTransactionDto
        {
            Reason = "Receipt requires correction after reconciliation review."
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reconciliation*");
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentId == fixture.Transaction.Id &&
            item.PostingAction == "Reverse")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankReversal")]
    [Trait("Category", "CashBank")]
    public async Task PostedTransfer_ShouldReverseBothOperationalLegsAndRestoreBankSnapshots()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedBankTransferAsync(db, tenantId);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.FromTransaction.Id);

        var result = await service.ReverseAsync(fixture.ToTransaction.Id, new ReverseCashTransactionDto
        {
            Reason = "Transfer was initiated between the wrong TDC bank accounts."
        });

        result.IsReversed.Should().BeTrue();
        var originals = await db.Set<CashTransaction>()
            .Where(item => item.Id == fixture.FromTransaction.Id || item.Id == fixture.ToTransaction.Id)
            .ToListAsync();
        originals.Should().OnlyContain(item => item.IsReversed && item.ReversalCashTransactionId.HasValue);
        var corrections = await db.Set<CashTransaction>()
            .Where(item => item.ReversalOfCashTransactionId.HasValue &&
                (item.ReversalOfCashTransactionId == fixture.FromTransaction.Id ||
                 item.ReversalOfCashTransactionId == fixture.ToTransaction.Id))
            .ToListAsync();
        corrections.Should().HaveCount(2);
        corrections.Should().OnlyContain(item => item.JournalEntryId == result.ReversalJournalEntryId);
        fixture.FromBankAccount.CurrentBalance.Should().Be(0m);
        fixture.ToBankAccount.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task PaymentTransaction_ShouldPostCorrectDebitCreditDirection()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment);
        var service = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Transaction.Id);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.SourceDocumentType.Should().Be("CashBankPayment");
        journal.Transactions.Single(t => t.AccountId == fixture.OffsetAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).CreditAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task BankTransfer_ShouldPostCorrectDirectionAndLinkBothTransferLegs()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedBankTransferAsync(db, tenantId);
        var service = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.FromTransaction.Id);

        result.JournalEntryId.Should().NotBeNull();

        var from = await db.Set<CashTransaction>().SingleAsync(t => t.Id == fixture.FromTransaction.Id);
        var to = await db.Set<CashTransaction>().SingleAsync(t => t.Id == fixture.ToTransaction.Id);
        from.JournalEntryId.Should().Be(result.JournalEntryId);
        to.JournalEntryId.Should().Be(result.JournalEntryId);
        from.IsPosted.Should().BeTrue();
        to.IsPosted.Should().BeTrue();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.SourceDocumentType.Should().Be("CashBankTransfer");
        journal.SourceDocumentId.Should().Be(fixture.FromTransaction.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.ToBankGlAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.FromBankGlAccount.Id).CreditAmount.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CrossCurrencyBankTransfer")]
    [Trait("Category", "CashBank")]
    public async Task CrossCurrencyTransfer_ShouldPreviewCaptureOncePostLossAndReverseEachNativeAmount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCrossCurrencyTransferFoundationAsync(
            db,
            tenantId,
            sourceRate: 15.20m,
            destinationRate: 16.80m,
            configureGainAccount: true,
            configureLossAccount: true);
        var service = CreateService(db, tenantId);
        var pairId = Guid.NewGuid();
        var command = new CreateBankTransferDto
        {
            TransferPairId = pairId,
            TransactionDate = new DateTime(2026, 7, 5),
            FromBankAccountId = fixture.SourceBank.Id,
            ToBankAccountId = fixture.DestinationBank.Id,
            Amount = 100m,
            DestinationAmount = 90m,
            ReferenceNumber = "BOG-ADVICE-001",
            Description = "Convert USD operating funds to the EUR project account."
        };

        var preview = await service.PreviewTransferAsync(command);

        preview.IsCrossCurrency.Should().BeTrue();
        preview.SourceExchangeRateId.Should().Be(fixture.SourceRate.Id);
        preview.DestinationExchangeRateId.Should().Be(fixture.DestinationRate.Id);
        preview.SourceBaseAmount.Should().Be(1_520m);
        preview.DestinationBaseAmount.Should().Be(1_512m);
        preview.RealizedFxGainLossBaseAmount.Should().Be(-8m);
        preview.RealizedFxOutcome.Should().Be("Loss");

        // The UI sends the exact approved records returned by preview. A repeated network command
        // with the same pair id must return these same two rows, never another transfer number.
        command.SourceExchangeRateId = preview.SourceExchangeRateId;
        command.DestinationExchangeRateId = preview.DestinationExchangeRateId;
        var captured = await service.CreateTransferAsync(command);
        var retry = await service.CreateTransferAsync(command);

        retry.FromTransaction.Id.Should().Be(captured.FromTransaction.Id);
        retry.ToTransaction.Id.Should().Be(captured.ToTransaction.Id);
        (await db.Set<CashTransaction>().CountAsync(t => t.TransferPairId == pairId)).Should().Be(2);

        var outgoing = await db.Set<CashTransaction>().SingleAsync(t => t.Id == captured.FromTransaction.Id);
        outgoing.ApprovalStatus = CashTransactionApprovalStatus.Approved;
        await db.SaveChangesAsync();

        var posted = await service.PostAsync(outgoing.Id);
        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == posted.JournalEntryId);

        journal.Transactions.Single(t => t.AccountId == fixture.SourceBankGl.Id).CreditAmount.Should().Be(1_520m);
        journal.Transactions.Single(t => t.AccountId == fixture.DestinationBankGl.Id).DebitAmount.Should().Be(1_512m);
        var realizedLossLines = journal.Transactions
            .Where(t => t.AccountId == fixture.RealizedLoss.Id
                && t.TransactionTag == "CashBankTransfer.RealizedFxLoss")
            .ToList();
        realizedLossLines.Should().HaveCount(2);
        realizedLossLines.Sum(t => t.DebitAmount).Should().Be(8m);
        realizedLossLines.Should().OnlyContain(t => t.CreditAmount == 0m && t.SourceDocumentLineId.HasValue);
        realizedLossLines.Select(t => t.SourceDocumentLineId!.Value).Should().OnlyHaveUniqueItems();
        journal.Transactions.Single(t => t.AccountId == fixture.SourceBankGl.Id).ExchangeRateId.Should().Be(fixture.SourceRate.Id);
        journal.Transactions.Single(t => t.AccountId == fixture.DestinationBankGl.Id).ExchangeRateId.Should().Be(fixture.DestinationRate.Id);

        var postedBanks = await db.BankAccounts
            .Where(b => b.Id == fixture.SourceBank.Id || b.Id == fixture.DestinationBank.Id)
            .ToDictionaryAsync(b => b.Id);
        postedBanks[fixture.SourceBank.Id].CurrentBalance.Should().Be(400m);
        postedBanks[fixture.DestinationBank.Id].CurrentBalance.Should().Be(140m);

        var reversed = await service.ReverseAsync(captured.ToTransaction.Id, new ReverseCashTransactionDto
        {
            Reason = "Transfer conversion was initiated against the wrong TDC project bank account."
        });
        var corrections = await db.Set<CashTransaction>()
            .Where(t => t.TransferPairId.HasValue
                && t.TransferPairId != pairId
                && t.ReversalOfCashTransactionId.HasValue)
            .ToListAsync();

        reversed.IsReversed.Should().BeTrue();
        corrections.Should().HaveCount(2);
        corrections.Single(t => t.TransferLeg == BankTransferLeg.Outgoing).Amount.Should().Be(90m);
        corrections.Single(t => t.TransferLeg == BankTransferLeg.Outgoing).Currency.Should().Be("EUR");
        corrections.Single(t => t.TransferLeg == BankTransferLeg.Incoming).Amount.Should().Be(100m);
        corrections.Single(t => t.TransferLeg == BankTransferLeg.Incoming).Currency.Should().Be("USD");
        corrections.Should().OnlyContain(t => t.TransferFxGainLossBaseAmount == 8m);

        var restoredBanks = await db.BankAccounts
            .Where(b => b.Id == fixture.SourceBank.Id || b.Id == fixture.DestinationBank.Id)
            .ToDictionaryAsync(b => b.Id);
        restoredBanks[fixture.SourceBank.Id].CurrentBalance.Should().Be(500m);
        restoredBanks[fixture.DestinationBank.Id].CurrentBalance.Should().Be(50m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CrossCurrencyBankTransfer")]
    [Trait("Category", "CashBank")]
    public async Task CrossCurrencyTransfer_WhenDestinationValueIsHigher_ShouldCreditRealizedGain()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCrossCurrencyTransferFoundationAsync(
            db,
            tenantId,
            sourceRate: 15m,
            destinationRate: 16m,
            configureGainAccount: true,
            configureLossAccount: true);
        var service = CreateService(db, tenantId);
        var command = new CreateBankTransferDto
        {
            TransferPairId = Guid.NewGuid(),
            TransactionDate = new DateTime(2026, 7, 5),
            FromBankAccountId = fixture.SourceBank.Id,
            ToBankAccountId = fixture.DestinationBank.Id,
            Amount = 100m,
            DestinationAmount = 95m
        };
        var preview = await service.PreviewTransferAsync(command);
        command.SourceExchangeRateId = preview.SourceExchangeRateId;
        command.DestinationExchangeRateId = preview.DestinationExchangeRateId;
        var captured = await service.CreateTransferAsync(command);
        var outgoing = await db.Set<CashTransaction>().SingleAsync(t => t.Id == captured.FromTransaction.Id);
        outgoing.ApprovalStatus = CashTransactionApprovalStatus.Approved;
        await db.SaveChangesAsync();

        var posted = await service.PostAsync(outgoing.Id);
        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == posted.JournalEntryId);

        preview.RealizedFxGainLossBaseAmount.Should().Be(20m);
        preview.RealizedFxOutcome.Should().Be("Gain");
        var realizedGainLines = journal.Transactions
            .Where(t => t.AccountId == fixture.RealizedGain.Id
                && t.TransactionTag == "CashBankTransfer.RealizedFxGain")
            .ToList();
        realizedGainLines.Should().HaveCount(2);
        realizedGainLines.Sum(t => t.CreditAmount).Should().Be(20m);
        realizedGainLines.Should().OnlyContain(t => t.DebitAmount == 0m && t.SourceDocumentLineId.HasValue);
        realizedGainLines.Select(t => t.SourceDocumentLineId!.Value).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CrossCurrencyBankTransfer")]
    [Trait("Category", "CashBank")]
    public async Task CrossCurrencyTransfer_WithoutRequiredLossMapping_ShouldNotPostOrMoveBankBalances()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCrossCurrencyTransferFoundationAsync(
            db,
            tenantId,
            sourceRate: 15.20m,
            destinationRate: 16.80m,
            configureGainAccount: true,
            configureLossAccount: false);
        var service = CreateService(db, tenantId);
        var command = new CreateBankTransferDto
        {
            TransferPairId = Guid.NewGuid(),
            TransactionDate = new DateTime(2026, 7, 5),
            FromBankAccountId = fixture.SourceBank.Id,
            ToBankAccountId = fixture.DestinationBank.Id,
            Amount = 100m,
            DestinationAmount = 90m
        };
        var preview = await service.PreviewTransferAsync(command);
        command.SourceExchangeRateId = preview.SourceExchangeRateId;
        command.DestinationExchangeRateId = preview.DestinationExchangeRateId;
        var captured = await service.CreateTransferAsync(command);
        var outgoing = await db.Set<CashTransaction>().SingleAsync(t => t.Id == captured.FromTransaction.Id);
        outgoing.ApprovalStatus = CashTransactionApprovalStatus.Approved;
        await db.SaveChangesAsync();

        var action = () => service.PostAsync(outgoing.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*realised FX loss account*");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == outgoing.Id)).Should().Be(0);
        var banks = await db.BankAccounts
            .AsNoTracking()
            .Where(b => b.Id == fixture.SourceBank.Id || b.Id == fixture.DestinationBank.Id)
            .ToDictionaryAsync(b => b.Id);
        banks[fixture.SourceBank.Id].CurrentBalance.Should().Be(500m);
        banks[fixture.DestinationBank.Id].CurrentBalance.Should().Be(50m);
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId
            && a.Action == FinanceAuditEvents.CashBankCrossCurrencyTransferPostingBlocked)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task CrossTenantBankAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        SeedTenant(db, otherTenantId, "OTH");
        var otherBankGl = SeedAccount(db, otherTenantId, "1010", AccountType.Asset);
        var otherBank = SeedBankAccount(db, otherTenantId, "BANK-OTH", otherBankGl.Id);
        fixture.Transaction.BankAccountId = otherBank.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash/bank transaction bank account belongs to another tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task CrossTenantOffsetAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        SeedTenant(db, otherTenantId, "OTH");
        var otherOffset = SeedAccount(db, otherTenantId, "4800", AccountType.Revenue);
        fixture.Transaction.GLAccountId = otherOffset.Id;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The cash receipt offset account GL account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task NonPostableOffsetAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment);
        fixture.OffsetAccount.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The cash payment offset account GL account does not allow direct posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, periodIsOpen: false, periodIsClosed: true);
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Transaction.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task DuplicateCashBankPost_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        var service = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Transaction.Id);
        var second = await service.PostAsync(fixture.Transaction.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "CashBankReceipt")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CashBankReceipt")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.CashBankTransactionDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task PostedCashBankTransaction_ShouldNotBeDeletedByMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.Transaction.Id);

        var act = () => service.DeleteAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Cannot delete posted transaction");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankPosting")]
    [Trait("Category", "CashBank")]
    public async Task PostedFlagWithoutPostingEvent_ShouldBeRejectedForDiagnostics()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt);
        fixture.Transaction.IsPosted = true;
        fixture.Transaction.JournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash/bank transaction is marked posted or linked to a journal without a valid finance posting event. Run posting back-reference diagnostics before retrying.");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cash-bank-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CashTransactionService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUserService(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-cash-bank-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var accessScope = CreateUnrestrictedFinanceAccessScope();
        var numberCounter = 0;
        var documentNumbering = new Mock<IDocumentNumberingService>();
        documentNumbering
            .Setup(x => x.GenerateAsync(
                DocumentNumberingModules.Finance,
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"REV-202607-{++numberCounter:0000}");

        return new CashTransactionService(
            db,
            Mock.Of<IBankAccountService>(),
            tenantSettings.Object,
            documentNumbering.Object,
            currentUser.Object,
            accessScope.Object,
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService);
    }

    private static Mock<IFinanceAccessScopeService> CreateUnrestrictedFinanceAccessScope()
    {
        var scope = new Mock<IFinanceAccessScopeService>();
        scope.Setup(x => x.GetPermittedBankAccountIdsAsync(
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid>?)null);
        scope.Setup(x => x.EnsureBankAccountAccessAsync(
                It.IsAny<Guid?>(),
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return scope;
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("cash.bank.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("cash-bank-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<CashBankFixture> SeedCashTransactionAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CashTransactionType transactionType,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var bankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var offset = SeedAccount(db, tenantId, transactionType == CashTransactionType.Receipt ? "4100" : "6100",
            transactionType == CashTransactionType.Receipt ? AccountType.Revenue : AccountType.Expense);
        var bankAccount = SeedBankAccount(db, tenantId, "BANK-001", bankGl.Id);
        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = transactionType == CashTransactionType.Receipt ? "RCT-202607-0001" : "CPY-202607-0001",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = transactionType,
            BankAccountId = bankAccount.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            GLAccountId = offset.Id,
            Description = "Standalone cash/bank transaction",
            IsPosted = false,
            ApprovalStatus = CashTransactionApprovalStatus.Approved,
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().Add(transaction);
        await db.SaveChangesAsync();
        return new CashBankFixture(transaction, bankAccount, bankGl, offset);
    }

    private static async Task<CashBankTransferFixture> SeedBankTransferAsync(ApplicationDbContext db, Guid tenantId)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var fromBankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var toBankGl = SeedAccount(db, tenantId, "1010", AccountType.Asset);
        var fromBank = SeedBankAccount(db, tenantId, "BANK-001", fromBankGl.Id);
        var toBank = SeedBankAccount(db, tenantId, "BANK-002", toBankGl.Id);
        var transferPairId = Guid.NewGuid();
        var fromTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-0001-OUT",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = fromBank.Id,
            ToBankAccountId = toBank.Id,
            TransferPairId = transferPairId,
            TransferLeg = BankTransferLeg.Outgoing,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            TransferCrossRate = 1m,
            Description = "Transfer from operating to savings",
            ApprovalStatus = CashTransactionApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var toTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-0001-IN",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = toBank.Id,
            ToBankAccountId = fromBank.Id,
            TransferPairId = transferPairId,
            TransferLeg = BankTransferLeg.Incoming,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            TransferCrossRate = 1m,
            Description = "Transfer from operating to savings",
            ApprovalStatus = CashTransactionApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await db.SaveChangesAsync();
        return new CashBankTransferFixture(fromTransaction, toTransaction, fromBank, toBank, fromBankGl, toBankGl);
    }

    private static async Task<CrossCurrencyTransferFixture> SeedCrossCurrencyTransferFoundationAsync(
        ApplicationDbContext db,
        Guid tenantId,
        decimal sourceRate,
        decimal destinationRate,
        bool configureGainAccount,
        bool configureLossAccount)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);

        var sourceBankGl = SeedAccount(db, tenantId, "1020-USD", AccountType.Asset, currencyCode: "USD");
        var destinationBankGl = SeedAccount(db, tenantId, "1030-EUR", AccountType.Asset, currencyCode: "EUR");
        var realizedGain = SeedAccount(db, tenantId, "7290", AccountType.Revenue);
        var realizedLoss = SeedAccount(db, tenantId, "8290", AccountType.Expense);
        var sourceBank = SeedBankAccount(
            db,
            tenantId,
            "BANK-USD",
            sourceBankGl.Id,
            currency: "USD",
            openingBalance: 500m);
        var destinationBank = SeedBankAccount(
            db,
            tenantId,
            "BANK-EUR",
            destinationBankGl.Id,
            currency: "EUR",
            openingBalance: 50m);

        var settings = db.FinanceSettings.Local.Single(s => s.TenantId == tenantId);
        settings.RealizedFxGainAccountId = configureGainAccount ? realizedGain.Id : null;
        settings.RealizedFxLossAccountId = configureLossAccount ? realizedLoss.Id : null;
        settings.DirectionalExchangeRatePolicyEnabled = false;
        settings.DefaultTransactionQuoteSide = ExchangeRateQuoteSide.Mid;

        var usdRate = SeedExchangeRate(db, tenantId, "USD", sourceRate, new DateTime(2026, 7, 5));
        var eurRate = SeedExchangeRate(db, tenantId, "EUR", destinationRate, new DateTime(2026, 7, 5));
        await db.SaveChangesAsync();

        return new CrossCurrencyTransferFixture(
            sourceBank,
            destinationBank,
            sourceBankGl,
            destinationBankGl,
            realizedGain,
            realizedLoss,
            usdRate,
            eurRate);
    }

    private static ExchangeRate SeedExchangeRate(
        ApplicationDbContext db,
        Guid tenantId,
        string targetCurrency,
        decimal rate,
        DateTime effectiveDate)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = targetCurrency,
            Rate = rate,
            InverseRate = decimal.Round(1m / rate, 6, MidpointRounding.AwayFromZero),
            EffectiveDate = effectiveDate.Date,
            RateType = ExchangeRateType.Daily,
            QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana test rate",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
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
            ReferenceNumber = $"FIN-{code}",
            BaseCurrency = "GHS",
            ReversalDatePolicy = FinanceReversalDatePolicy.CurrentOpenPeriod,
            MinimumReversalReasonLength = 20,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS Primary",
            Purpose = "Primary",
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
        bool isClosed = false)
    {
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = "FY2026", Year = 2026, FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            Status = "Open", IsActive = true
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };

        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        bool allowDirectPosting = true,
        string currencyCode = "GHS")
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
            AllowDirectPosting = allowDirectPosting
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

    private static BankAccount SeedBankAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        Guid glAccountId,
        string currency = "GHS",
        decimal openingBalance = 0m)
    {
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = accountNumber,
            AccountName = $"Bank {accountNumber}",
            BankName = "Test Bank",
            Currency = currency,
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccountId,
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            AvailableBalance = openingBalance,
            IsActive = true,
            OpeningDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.BankAccounts.Add(bank);
        return bank;
    }

    private sealed record CashBankFixture(
        CashTransaction Transaction,
        BankAccount BankAccount,
        Account BankGlAccount,
        Account OffsetAccount);

    private sealed record CashBankTransferFixture(
        CashTransaction FromTransaction,
        CashTransaction ToTransaction,
        BankAccount FromBankAccount,
        BankAccount ToBankAccount,
        Account FromBankGlAccount,
        Account ToBankGlAccount);

    private sealed record CrossCurrencyTransferFixture(
        BankAccount SourceBank,
        BankAccount DestinationBank,
        Account SourceBankGl,
        Account DestinationBankGl,
        Account RealizedGain,
        Account RealizedLoss,
        ExchangeRate SourceRate,
        ExchangeRate DestinationRate);
}
