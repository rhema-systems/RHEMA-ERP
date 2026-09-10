using System.Security.Cryptography;
using System.Text;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable CS0618 // Regression tests intentionally assert obsolete legacy posting paths are not used.

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ArCreditNotePostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task GovernedSalesCreditNote_PreparesNeutralIntentWithoutAutoApprovalOrOwnerMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, taxAmount: 20m);
        var producer = new Mock<IFinanceProducerIntentService>();
        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        var reversal = new Mock<IFinanceProducerReversalPreparationService>();
        ProducerAccountingIntentDto? captured = null;
        var eventId = Guid.NewGuid();
        producer.Setup(x => x.PrepareAsync(It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>()))
            .Callback<ProducerAccountingIntentDto, CancellationToken>((intent, _) => captured = intent)
            .ReturnsAsync(new AccountingEventDto { Id = eventId, Status = AccountingEventStatuses.PendingApproval,
                ProducerDecisionStatus = ProducerIntentDecisionStatuses.Pending, RequestFingerprint = new string('A', 64) });
        var (service, _) = CreateReturnOrderService(db, tenantId, producer.Object, execution.Object, reversal.Object);

        var result = await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        captured.Should().NotBeNull();
        captured!.PostingRequest.SourceDocumentType.Should().Be("SalesCreditNote");
        captured.PostingRequest.SourceDocumentId.Should().Be(fixture.CreditNote.Id);
        captured.PostingRequest.PostingAction.Should().Be("Post");
        captured.PostingRequest.IdempotencyKey.Should().Be($"AR:SalesCreditNote:{tenantId:N}:{fixture.CreditNote.Id:N}:Post");
        captured.PostingRequest.Lines.Should().HaveCount(3);
        captured.PostingRequest.Lines.Sum(line => line.DebitAmount).Should().Be(120m);
        captured.PostingRequest.Lines.Sum(line => line.CreditAmount).Should().Be(120m);
        result.JournalEntryId.Should().BeNull();
        execution.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task GovernedSalesCreditNote_ApprovedCompatibilityBindingCompletesOnceAndExactRetryIsReadOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var producer = new Mock<IFinanceProducerIntentService>();
        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        var eventId = Guid.NewGuid();
        const string fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var approved = new AccountingEventDto { Id = eventId, Status = AccountingEventStatuses.PendingApproval,
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved, RequestFingerprint = fingerprint };
        producer.Setup(x => x.PrepareAsync(It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(approved);
        producer.Setup(x => x.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(approved);
        var postingId = Guid.NewGuid(); var journalId = Guid.NewGuid();
        execution.Setup(x => x.ExecuteInAmbientTransactionAsync(eventId, It.IsAny<ProducerAccountingIntentDto>(),
                It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceProducerApprovedExecutionResultDto(eventId, fingerprint, AccountingEventStatuses.Posted, postingId, journalId));
        var (service, _) = CreateReturnOrderService(db, tenantId, producer.Object, execution.Object, Mock.Of<IFinanceProducerReversalPreparationService>());

        var first = await service.PostCreditNoteAsync(fixture.CreditNote.Id);
        var second = await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        first.JournalEntryId.Should().Be(journalId);
        second.JournalEntryId.Should().Be(journalId);
        execution.Verify(x => x.ExecuteInAmbientTransactionAsync(eventId, It.IsAny<ProducerAccountingIntentDto>(),
            It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task GovernedSalesCreditNote_MalformedCompatibilityEvidenceRollsBackOwnerEffectAndRecordsFailure(string returnedFingerprint)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var eventId = Guid.NewGuid();
        const string fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var prepared = new AccountingEventDto { Id = eventId, Status = AccountingEventStatuses.PendingApproval,
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved, RequestFingerprint = fingerprint };
        var producer = new Mock<IFinanceProducerIntentService>();
        producer.Setup(x => x.PrepareAsync(It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(prepared);
        producer.Setup(x => x.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(prepared);
        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        execution.Setup(x => x.ExecuteInAmbientTransactionAsync(eventId, It.IsAny<ProducerAccountingIntentDto>(),
                It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceProducerApprovedExecutionResultDto(eventId, returnedFingerprint,
                AccountingEventStatuses.Posted, Guid.NewGuid(), Guid.NewGuid()));
        var (service, _) = CreateReturnOrderService(db, tenantId, producer.Object, execution.Object,
            Mock.Of<IFinanceProducerReversalPreparationService>());

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note compatibility evidence does not bind to the prepared Finance event.");
        db.ChangeTracker.Clear();
        (await db.CreditNotes.SingleAsync(c => c.Id == fixture.CreditNote.Id)).JournalEntryId.Should().BeNull();
        execution.Verify(x => x.RecordFailureAfterRollbackAsync(eventId, It.IsAny<ProducerAccountingIntentDto>(),
            It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task GovernedSalesCreditNote_ReversalUsesIdOnlyExecutionAndAllowsOnlyExactRetry()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var originalEventId = Guid.NewGuid();
        db.AccountingEvents.Add(new AccountingEvent
        {
            Id = originalEventId, TenantId = tenantId, OriginatingModuleCode = "SALES",
            SourceDocumentType = "SalesCreditNote", SourceDocumentId = fixture.CreditNote.Id,
            PostingAction = "Post", IdempotencyKey = $"AR:SalesCreditNote:{tenantId:N}:{fixture.CreditNote.Id:N}:Post",
            Status = AccountingEventStatuses.Posted
        });
        fixture.CreditNote.JournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var reversalEventId = Guid.NewGuid();
        const string fingerprint = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
        var producer = new Mock<IFinanceProducerIntentService>();
        producer.Setup(x => x.GetAsync(reversalEventId, It.IsAny<CancellationToken>())).ReturnsAsync(new AccountingEventDto
        {
            Id = reversalEventId, RequestFingerprint = fingerprint,
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved
        });
        var reversals = new Mock<IFinanceProducerReversalPreparationService>();
        reversals.Setup(x => x.PrepareReversalAsync(It.IsAny<PrepareProducerAccountingReversalDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProducerAccountingReversalPreparationResultDto(reversalEventId, originalEventId, fingerprint,
                AccountingEventStatuses.PendingApproval, ProducerIntentDecisionStatuses.Approved));
        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        execution.Setup(x => x.ExecuteInAmbientTransactionAsync(reversalEventId, It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceProducerApprovedExecutionResultDto(reversalEventId, fingerprint,
                AccountingEventStatuses.Posted, Guid.NewGuid(), Guid.NewGuid()));
        var (service, _) = CreateReturnOrderService(db, tenantId, producer.Object, execution.Object, reversals.Object);
        var request = new ReverseCreditNoteDto { Reason = "Correct approved credit note", ReversalDate = new DateTime(2026, 7, 6) };

        var first = await service.ReverseCreditNoteAsync(fixture.CreditNote.Id, request);
        var replay = await service.ReverseCreditNoteAsync(fixture.CreditNote.Id, request);
        var conflict = () => service.ReverseCreditNoteAsync(fixture.CreditNote.Id,
            new ReverseCreditNoteDto { Reason = request.Reason, ReversalDate = request.ReversalDate!.Value.AddDays(1) });

        replay.ReversalJournalEntryId.Should().Be(first.ReversalJournalEntryId);
        await conflict.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note reversal retry conflicts with the immutable reversal evidence.");
        execution.Verify(x => x.ExecuteInAmbientTransactionAsync(reversalEventId,
            It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "SalesReturns")]
    public async Task ReturnOrder_ShouldRejectDeliveryNoteFromAnotherSalesOrderForSameBusinessPartner()
    {
        var tenantId = Guid.NewGuid();
        var businessPartnerId = Guid.NewGuid();
        await using var db = CreateContext();
        var selectedOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartnerId,
            DocumentNumber = "SO-SELECTED"
        };
        var otherOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartnerId,
            DocumentNumber = "SO-OTHER"
        };
        var deliveryNote = new DeliveryNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SalesOrderId = otherOrder.Id,
            BusinessPartnerId = businessPartnerId,
            DocumentNumber = "DN-OTHER-ORDER"
        };
        db.SalesOrders.AddRange(selectedOrder, otherOrder);
        db.DeliveryNotes.Add(deliveryNote);
        await db.SaveChangesAsync();
        var (service, _) = CreateReturnOrderService(db, tenantId);

        var act = () => service.CreateReturnOrderAsync(new CreateReturnOrderDto
        {
            SalesOrderId = selectedOrder.Id,
            DeliveryNoteId = deliveryNote.Id,
            ReasonCode = ReturnReasonCode.CustomerChanged,
            ReasonDescription = "Wrong fulfillment selected"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Return order delivery note must belong to the selected source sales order.");
        (await db.ReturnOrders.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ApprovedSalesCreditNote_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, taxAmount: 20m);
        var (service, subledgerPostingMock) = CreateReturnOrderService(db, tenantId);

        var result = await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostSalesCreditNoteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AR" &&
            e.SourceDocumentType == "SalesCreditNote" &&
            e.SourceDocumentId == fixture.CreditNote.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AR");
        journal.SourceDocumentType.Should().Be("SalesCreditNote");
        journal.Transactions.Should().HaveCount(3);
        journal.Transactions.Single(t => t.AccountId == fixture.SalesReturnsAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.TaxAccount.Id).DebitAmount.Should().Be(20m);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(120m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArCreditNotePosted && a.TenantId == tenantId)).Should().Be(1);
        // The posting engine keeps Account.Balance as a read-side snapshot for legacy balance APIs.
        fixture.ArAccount.Balance.Should().Be(-120m);
        fixture.SalesReturnsAccount.Balance.Should().Be(100m);
        fixture.TaxAccount.Balance.Should().Be(-20m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task UnapprovedSalesCreditNote_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, cn => cn.CreditNoteStatus = CreditNoteStatus.PendingApproval);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note workflow approval is not complete.");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "SalesCreditNote")).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArCreditNotePostingFailed)).Should().Be(1);
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CreditNoteAgainstUnpostedInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, configureInvoice: invoice => invoice.JournalEntryId = null, seedInvoicePostingEvent: false);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR credit note cannot post against unposted invoice '{fixture.Invoice.InvoiceNumber}'.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ExcessCreditNoteAmount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, amount: 125m);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR credit note would exceed eligible credit amount for invoice '{fixture.Invoice.InvoiceNumber}'.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task SecondPostedSalesCreditNote_ShouldIncludePriorPostedCreditsInTheLimit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, amount: 80m);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);
        await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        var secondCreditNote = CreateApprovedSalesCreditNote(
            fixture,
            "SCN-2026-00002",
            amount: 50m);
        db.CreditNotes.Add(secondCreditNote);
        await db.SaveChangesAsync();
        governed.ClearInvocations();

        var act = () => service.PostCreditNoteAsync(secondCreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR credit note would exceed eligible credit amount for invoice '{fixture.Invoice.InvoiceNumber}'.");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "SalesCreditNote"))
            .Should().Be(1);
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task SalesCreditNoteCannotBeAppliedBeforeCentralPostingExists()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.ApplyCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note must be posted through the central finance posting engine before it can be applied.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedSalesCreditNoteCannotBeAppliedToAnotherInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);
        await service.PostCreditNoteAsync(fixture.CreditNote.Id);
        governed.ClearInvocations();

        var act = () => service.ApplyCreditNoteAsync(fixture.CreditNote.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit notes can only be applied to their original invoice. Use a reversal or adjustment workflow to correct the target.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task StandaloneSalesCreditNoteCanApplyToSameBusinessPartnerInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var standaloneCredit = CreateApprovedSalesCreditNote(fixture, "SCN-2026-STANDALONE", amount: 25m);
        standaloneCredit.OriginalInvoiceId = null;
        db.CreditNotes.Add(standaloneCredit);
        await db.SaveChangesAsync();

        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);
        await service.PostCreditNoteAsync(standaloneCredit.Id);

        var result = await service.ApplyCreditNoteAsync(standaloneCredit.Id, fixture.Invoice.Id);

        result.CreditNoteStatus.Should().Be(CreditNoteStatus.Applied);
        result.AppliedToInvoiceId.Should().Be(fixture.Invoice.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task StandaloneSalesCreditNoteCannotApplyToAnotherBusinessPartnerInvoice()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var standaloneCredit = CreateApprovedSalesCreditNote(fixture, "SCN-2026-OTHER-PARTNER", amount: 25m);
        standaloneCredit.OriginalInvoiceId = null;
        var otherInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = Guid.NewGuid(),
            InvoiceNumber = "INV-2026-OTHER-PARTNER",
            CustomerName = "Other customer",
            TotalAmount = 100m,
            JournalEntryId = Guid.NewGuid(),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.CreditNotes.Add(standaloneCredit);
        db.Invoices.Add(otherInvoice);
        await db.SaveChangesAsync();

        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);
        await service.PostCreditNoteAsync(standaloneCredit.Id);
        governed.ClearInvocations();

        var act = () => service.ApplyCreditNoteAsync(standaloneCredit.Id, otherInvoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit notes can only be applied to invoices for the same business partner.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task SalesCreditNoteApplicationCannotOverSettleAnInvoiceAfterPostedReceipts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, amount: 100m);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);
        await service.PostCreditNoteAsync(fixture.CreditNote.Id);
        SeedPostedCustomerReceiptSettlement(db, fixture, amount: 30m);
        await db.SaveChangesAsync();
        governed.ClearInvocations();

        var act = () => service.ApplyCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR credit note would over-settle invoice '{fixture.Invoice.InvoiceNumber}' when combined with posted receipts and credit notes.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantBusinessPartner_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAr = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherBusinessPartner = SeedBusinessPartner(db, otherTenantId, Guid.NewGuid(), otherAr.Id);
        fixture.CreditNote.BusinessPartnerId = otherBusinessPartner.Id;
        await db.SaveChangesAsync();
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note customer was not found for this tenant.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantArControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAr = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        fixture.BusinessPartner.DefaultArAccountId = otherAr.Id;
        await db.SaveChangesAsync();
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note posting AR control account was not found for this tenant.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantSalesReturnsAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherReturns = SeedAccount(db, otherTenantId, "5200", AccountType.Expense);
        fixture.Settings.DiscountAllowedAccountId = otherReturns.Id;
        await db.SaveChangesAsync();
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note posting sales returns/allowance account was not found for this tenant.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task NonPostableSalesReturnsAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        fixture.SalesReturnsAccount.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note posting sales returns/allowance account account '5200' does not allow direct posting.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateReturnOrderService(db, tenantId);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.CreditNote.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DuplicateSalesCreditNotePosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);

        var first = await service.PostCreditNoteAsync(fixture.CreditNote.Id);
        governed.ClearInvocations();
        var second = await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "SalesCreditNote")).Should().Be(1);
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedSalesCreditNote_ShouldNotBeVoidedByMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var governed = CreateApprovedGovernedProducerMock(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId, governed.Intents.Object, governed.Execution.Object);
        await service.PostCreditNoteAsync(fixture.CreditNote.Id);
        governed.ClearInvocations();

        var act = () => service.VoidCreditNoteAsync(fixture.CreditNote.Id, "test void");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted AR credit notes cannot be voided by mutation. Use a reversal or adjustment workflow.");
        governed.VerifyNoPrepareOrExecution();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedSalesCreditNote_ShouldReverseThroughPostingEngineWithoutMutatingOriginal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId, taxAmount: 20m);
        var (service, _) = CreateReturnOrderService(db, tenantId);
        var posted = await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        var reversed = await service.ReverseCreditNoteAsync(fixture.CreditNote.Id, new ReverseCreditNoteDto
        {
            Reason = "Correct approved credit note"
        });

        reversed.CreditNoteStatus.Should().Be(CreditNoteStatus.Reversed);
        reversed.JournalEntryId.Should().Be(posted.JournalEntryId);
        reversed.ReversalJournalEntryId.Should().NotBeNull();
        reversed.ReversalPostingEventId.Should().NotBeNull();
        reversed.ReversalReason.Should().Be("Correct approved credit note");

        var originalJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == posted.JournalEntryId);
        originalJournal.PostingStatus.Should().Be("Posted");
        originalJournal.IsReversed.Should().BeTrue();
        originalJournal.ReversalJournalEntryId.Should().Be(reversed.ReversalJournalEntryId);
        originalJournal.Transactions.Should().OnlyContain(t =>
            t.PostingStatus == "Posted" && t.IsReversed && t.ReversalTransactionId.HasValue);
        var reversalJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == reversed.ReversalJournalEntryId);
        reversalJournal.SourceDocumentType.Should().Be("SalesCreditNoteReversal");
        reversalJournal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(120m);
        reversalJournal.Transactions.Single(t => t.AccountId == fixture.SalesReturnsAccount.Id).CreditAmount.Should().Be(100m);
        reversalJournal.Transactions.Single(t => t.AccountId == fixture.TaxAccount.Id).CreditAmount.Should().Be(20m);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArCreditNoteReversed && a.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task RepeatedSalesCreditNoteReversal_ShouldReturnTheExistingImmutableReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var (service, _) = CreateReturnOrderService(db, tenantId);
        await service.PostCreditNoteAsync(fixture.CreditNote.Id);

        var first = await service.ReverseCreditNoteAsync(fixture.CreditNote.Id, new ReverseCreditNoteDto { Reason = "Correction" });
        var second = await service.ReverseCreditNoteAsync(fixture.CreditNote.Id, new ReverseCreditNoteDto { Reason = "Repeated request" });

        second.ReversalJournalEntryId.Should().Be(first.ReversalJournalEntryId);
        second.ReversalPostingEventId.Should().Be(first.ReversalPostingEventId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "SalesCreditNoteReversal")).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CompatibilityCustomerCreditNote_ShouldPostThroughFinancePostingEngineAndNotLegacyArPaymentPath()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCompatibilityCreditNoteAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreatePaymentService(db, tenantId);

        var result = await service.CreateCreditNoteAsync(new CreditNoteCreateDto
        {
            CustomerId = fixture.BusinessPartner.Id,
            CreditNoteDate = new DateTime(2026, 7, 5),
            Amount = 100m,
            Reason = "Commercial credit memo",
            Reference = "CN-COMPAT"
        });

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostArPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceDocumentType == "CustomerCreditNote" &&
            e.SourceDocumentId == result.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.SourceDocumentType.Should().Be("CustomerCreditNote");
        journal.Transactions.Single(t => t.AccountId == fixture.SalesReturnsAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(100m);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArCreditNotePosted && a.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task GovernedSalesCreditNote_LockedSourceTamperBlocksExecution()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var originalEventId = DeterministicSalesEventId(fixture.CreditNote.Id, "AR-CREDIT-NOTE-POST");
        db.AccountingEvents.Add(new AccountingEvent
        {
            Id = originalEventId, TenantId = tenantId, OriginatingModuleCode = "SALES",
            SourceDocumentType = "SalesCreditNote", SourceDocumentId = fixture.CreditNote.Id,
            PostingAction = "Post", IdempotencyKey = $"AR:SalesCreditNote:{tenantId:N}:{fixture.CreditNote.Id:N}:Post",
            Status = AccountingEventStatuses.Posted
        });
        fixture.CreditNote.JournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var reversalEventId = Guid.NewGuid();
        const string fingerprint = "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD";
        var producer = new Mock<IFinanceProducerIntentService>();
        producer.Setup(x => x.GetAsync(reversalEventId, It.IsAny<CancellationToken>())).ReturnsAsync(new AccountingEventDto
        {
            Id = reversalEventId, RequestFingerprint = fingerprint, ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved
        });
        var reversals = new Mock<IFinanceProducerReversalPreparationService>();
        reversals.Setup(x => x.PrepareReversalAsync(It.IsAny<PrepareProducerAccountingReversalDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProducerAccountingReversalPreparationResultDto(reversalEventId, originalEventId, fingerprint,
                AccountingEventStatuses.PendingApproval, ProducerIntentDecisionStatuses.Approved));
        var replay = new Mock<IFinanceProducerReplayVerificationService>();
        replay.Setup(x => x.VerifyPostedAsync(originalEventId, It.IsAny<FinanceProducerReplayVerificationRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                db.AccountingEvents.Single(x => x.Id == originalEventId).Status = AccountingEventStatuses.Failed;
                db.SaveChanges();
            })
            .ReturnsAsync(new FinanceProducerReplayVerificationResultDto(
                originalEventId, fingerprint, "owner-effect", AccountingEventStatuses.Posted, Guid.NewGuid(), Guid.NewGuid()));
        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        var (service, _) = CreateReturnOrderService(db, tenantId, producer.Object, execution.Object, reversals.Object, replay.Object);

        var act = () => service.ReverseCreditNoteAsync(fixture.CreditNote.Id,
            new ReverseCreditNoteDto { Reason = "Tamper test", ReversalDate = new DateTime(2026, 7, 6) });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR credit note reversal source authority changed before execution.");
        replay.Verify(x => x.VerifyPostedAsync(originalEventId, It.IsAny<FinanceProducerReplayVerificationRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);
        execution.Verify(x => x.ExecuteInAmbientTransactionAsync(It.IsAny<Guid>(), It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()), Times.Never);
        db.ChangeTracker.Clear();
        var unchanged = await db.CreditNotes.SingleAsync(x => x.Id == fixture.CreditNote.Id);
        unchanged.CreditNoteStatus.Should().Be(CreditNoteStatus.Approved);
        unchanged.ReversalJournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARCreditNotePosting")]
    public async Task GovernedSalesCreditNote_PostCommitAuditFailureDoesNotRecordRollbackFailure()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedSalesCreditNoteAsync(db, tenantId);
        var eventId = Guid.NewGuid();
        const string fingerprint = "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE";
        var prepared = new AccountingEventDto { Id = eventId, Status = AccountingEventStatuses.PendingApproval,
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved, RequestFingerprint = fingerprint };
        var producer = new Mock<IFinanceProducerIntentService>();
        producer.Setup(x => x.PrepareAsync(It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(prepared);
        producer.Setup(x => x.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(prepared);
        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        execution.Setup(x => x.ExecuteInAmbientTransactionAsync(eventId, It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceProducerApprovedExecutionResultDto(eventId, fingerprint, AccountingEventStatuses.Posted, Guid.NewGuid(), Guid.NewGuid()));
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(x => x.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store unavailable"));
        var (service, _) = CreateReturnOrderService(db, tenantId, producer.Object, execution.Object,
            Mock.Of<IFinanceProducerReversalPreparationService>(), Mock.Of<IFinanceProducerReplayVerificationService>(), audit.Object);

        var act = () => service.PostCreditNoteAsync(fixture.CreditNote.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit store unavailable");
        db.ChangeTracker.Clear();
        (await db.CreditNotes.SingleAsync(x => x.Id == fixture.CreditNote.Id)).JournalEntryId.Should().NotBeNull();
        execution.Verify(x => x.RecordFailureAfterRollbackAsync(It.IsAny<Guid>(), It.IsAny<ProducerAccountingIntentDto>(),
            It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-credit-note-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static GovernedSalesCreditNoteProducerMock CreateApprovedGovernedProducerMock(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var accountingEventId = Guid.NewGuid();
        var postingEventId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();
        const string fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var approved = new AccountingEventDto
        {
            Id = accountingEventId,
            Status = AccountingEventStatuses.PendingApproval,
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved,
            RequestFingerprint = fingerprint
        };
        var intents = new Mock<IFinanceProducerIntentService>();
        intents.Setup(x => x.PrepareAsync(It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(approved);
        intents.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(approved);

        var execution = new Mock<IFinanceProducerApprovedExecutionService>();
        execution.Setup(x => x.ExecuteInAmbientTransactionAsync(
                accountingEventId,
                It.IsAny<ProducerAccountingIntentDto>(),
                It.IsAny<ProducerOwnerEffectReceiptDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ProducerAccountingIntentDto, ProducerOwnerEffectReceiptDto, CancellationToken>((eventId, intent, receipt, cancellationToken) =>
            {
                var request = intent.PostingRequest;
                var book = db.AccountingBooks.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
                db.FinancePostingEvents.Add(new FinancePostingEvent
                {
                    Id = postingEventId,
                    TenantId = tenantId,
                    SourceModule = request.SourceModule,
                    OriginModuleCode = request.OriginModuleCode,
                    SourceDocumentType = request.SourceDocumentType,
                    SourceDocumentId = request.SourceDocumentId,
                    PostingAction = request.PostingAction,
                    SourceDocumentReference = request.SourceDocumentReference,
                    IdempotencyKey = request.IdempotencyKey,
                    JournalEntryId = journalEntryId,
                    AccountingBookId = book.Id,
                    PostingStatus = "Posted",
                    PostingDate = request.PostingDate,
                    RequestedAt = DateTime.UtcNow,
                    PostedAt = DateTime.UtcNow,
                    TotalDebitAmount = request.Lines.Sum(line => line.DebitAmount),
                    TotalCreditAmount = request.Lines.Sum(line => line.CreditAmount),
                    FunctionalCurrencyCode = request.FunctionalCurrencyCode,
                    BookClassification = "IFRS",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "governed-producer-mock"
                });
            })
            .ReturnsAsync(new FinanceProducerApprovedExecutionResultDto(
                accountingEventId,
                fingerprint,
                AccountingEventStatuses.Posted,
                postingEventId,
                journalEntryId));

        return new GovernedSalesCreditNoteProducerMock(intents, execution);
    }

    private static (ReturnOrderService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateReturnOrderService(
        ApplicationDbContext db,
        Guid tenantId,
        IFinanceProducerIntentService? producerIntents = null,
        IFinanceProducerApprovedExecutionService? producerExecution = null,
        IFinanceProducerReversalPreparationService? producerReversals = null,
        IFinanceProducerReplayVerificationService? replayVerifier = null,
        IFinanceAuditService? financeAuditService = null)
    {
        var currentUserService = CreateCurrentUserService(tenantId);
        var currentUserProvider = CreateCurrentUserProvider(tenantId, currentUserService.Object.UserId!);
        var auditService = new FinanceAuditService(
            db,
            currentUserService.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ar-credit-note-posting" }
            });
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var workflowMock = new Mock<IWorkflowIntegrationService>();
        workflowMock.Setup(x => x.ProcessApprovalAsync(
                "CreditNote",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                "Approve",
                It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed },
                WorkflowOutcome.Approved));
        workflowMock.Setup(x => x.SubmitAsync("CreditNote", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress },
                WorkflowOutcome.Pending));
        var workflowStatusAdapters = new WorkflowStatusAdapterRegistry(new IWorkflowStatusAdapter[]
        {
            new CreditNoteWorkflowStatusAdapter(),
            new RefundWorkflowStatusAdapter()
        });

        var service = new ReturnOrderService(
            new GenericRepository<ReturnOrder>(db),
            new GenericRepository<ReturnOrderLine>(db),
            new GenericRepository<CreditNote>(db),
            new GenericRepository<CreditNoteLine>(db),
            new GenericRepository<Refund>(db),
            new UnitOfWork(db),
            currentUserProvider.Object,
            Mock.Of<IDocumentNumberingService>(),
            workflowMock.Object,
            workflowStatusAdapters,
            Mock.Of<ILogger<ReturnOrderService>>(),
            financeAuditService: financeAuditService ?? auditService,
            financeProducerIntents: producerIntents,
            financeProducerExecution: producerExecution,
            financeProducerReversals: producerReversals,
            financeProducerReplayVerifier: replayVerifier ?? CreateReplayVerifier().Object);

        return (service, subledgerPostingMock);
    }

    private static Mock<IFinanceProducerReplayVerificationService> CreateReplayVerifier()
    {
        var replay = new Mock<IFinanceProducerReplayVerificationService>();
        replay.Setup(x => x.VerifyPostedAsync(It.IsAny<Guid>(), It.IsAny<FinanceProducerReplayVerificationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceProducerReplayVerificationResultDto(
                Guid.NewGuid(), "replay", "owner-effect", AccountingEventStatuses.Posted, Guid.NewGuid(), Guid.NewGuid()));
        return replay;
    }

    private sealed record GovernedSalesCreditNoteProducerMock(
        Mock<IFinanceProducerIntentService> Intents,
        Mock<IFinanceProducerApprovedExecutionService> Execution)
    {
        public void ClearInvocations()
        {
            Intents.Invocations.Clear();
            Execution.Invocations.Clear();
        }

        public void VerifyNoPrepareOrExecution()
        {
            Intents.Verify(x => x.PrepareAsync(It.IsAny<ProducerAccountingIntentDto>(), It.IsAny<CancellationToken>()), Times.Never);
            Execution.Verify(x => x.ExecuteInAmbientTransactionAsync(It.IsAny<Guid>(), It.IsAny<ProducerAccountingIntentDto>(),
                It.IsAny<ProducerOwnerEffectReceiptDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    private static Guid DeterministicSalesEventId(Guid creditNoteId, string purpose) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{creditNoteId:N}:{purpose}"))[..16]);

    private static (PaymentService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreatePaymentService(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = CreateCurrentUserService(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ar-credit-note-payment-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(x => x.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.ARCreditNote,
                tenantId,
                It.IsAny<DateTime>(),
                nameof(CustomerPayment),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("CN-2026-00001");

        var service = new PaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<PaymentService>>(),
            numbering.Object,
            Mock.Of<IFinanceAccessScopeService>(),
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService);

        return (service, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ar.creditnote.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ar-credit-note-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static Mock<ICurrentUserProvider> CreateCurrentUserProvider(Guid tenantId, string userId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(Guid.Parse(userId));
        currentUser.SetupGet(x => x.Username).Returns("ar.creditnote.poster");
        currentUser.SetupGet(x => x.FullName).Returns("AR Credit Note Poster");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        return currentUser;
    }

    private static async Task<ArCreditNoteFixture> SeedApprovedSalesCreditNoteAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<CreditNote>? configureCreditNote = null,
        Action<Invoice>? configureInvoice = null,
        decimal amount = 100m,
        decimal taxAmount = 0m,
        bool seedInvoicePostingEvent = true,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var salesReturnsAccount = SeedAccount(db, tenantId, "5200", AccountType.Expense);
        var taxAccount = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var businessPartner = SeedBusinessPartner(db, tenantId, Guid.NewGuid(), arAccount.Id);

        var settings = new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountAllowedAccountId = salesReturnsAccount.Id
        };
        db.Set<FinanceSettings>().Add(settings);

        var invoice = SeedPostedInvoice(db, tenantId, businessPartner, arAccount, "INV-2026-00001", new DateTime(2026, 7, 5), 120m, period.Id, seedInvoicePostingEvent);
        configureInvoice?.Invoke(invoice);

        var creditNote = new CreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartner.Id,
            OriginalInvoiceId = invoice.Id,
            DocumentNumber = "SCN-2026-00001",
            DocumentDate = new DateTime(2026, 7, 5),
            Currency = "GHS",
            ExchangeRate = 1m,
            CreditNoteStatus = CreditNoteStatus.Approved,
            TotalAmount = amount + taxAmount,
            TaxAmount = taxAmount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        creditNote.Lines.Add(new CreditNoteLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreditNoteId = creditNote.Id,
            Description = "Returned services",
            Quantity = 1m,
            UnitPrice = amount,
            TaxAmount = taxAmount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        configureCreditNote?.Invoke(creditNote);

        db.CreditNotes.Add(creditNote);
        await db.SaveChangesAsync();

        return new ArCreditNoteFixture(creditNote, invoice, businessPartner, arAccount, salesReturnsAccount, taxAccount, settings);
    }

    private static CreditNote CreateApprovedSalesCreditNote(
        ArCreditNoteFixture fixture,
        string documentNumber,
        decimal amount)
    {
        var creditNote = new CreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.CreditNote.TenantId,
            BusinessPartnerId = fixture.BusinessPartner.Id,
            OriginalInvoiceId = fixture.Invoice.Id,
            DocumentNumber = documentNumber,
            DocumentDate = fixture.CreditNote.DocumentDate,
            Currency = "GHS",
            ExchangeRate = 1m,
            CreditNoteStatus = CreditNoteStatus.Approved,
            TotalAmount = amount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        creditNote.Lines.Add(new CreditNoteLine
        {
            Id = Guid.NewGuid(),
            TenantId = creditNote.TenantId,
            CreditNoteId = creditNote.Id,
            Description = "Returned services",
            Quantity = 1m,
            UnitPrice = amount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        return creditNote;
    }

    private static void SeedPostedCustomerReceiptSettlement(
        ApplicationDbContext db,
        ArCreditNoteFixture fixture,
        decimal amount)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == fixture.CreditNote.TenantId && item.Code == "IFRS");
        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.CreditNote.TenantId,
            CustomerId = fixture.Invoice.BusinessPartnerId,
            PaymentNumber = "CP-2026-00001",
            PaymentDate = fixture.CreditNote.DocumentDate,
            TotalAmount = amount,
            AllocatedAmount = amount,
            PaymentMethod = "BankTransfer",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Status = "Posted",
            JournalEntryId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CustomerPayment>().Add(payment);
        db.Set<PaymentAllocation>().Add(new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = payment.TenantId,
            CustomerPaymentId = payment.Id,
            InvoiceId = fixture.Invoice.Id,
            AllocatedAmount = amount,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = payment.TenantId,
            SourceModule = "AR",
            SourceDocumentType = "CustomerPayment",
            SourceDocumentId = payment.Id,
            PostingAction = "Post",
            SourceDocumentReference = payment.PaymentNumber,
            IdempotencyKey = $"AR:CustomerPayment:{payment.TenantId:N}:{payment.Id:N}:Post",
            JournalEntryId = payment.JournalEntryId,
            AccountingBookId = book.Id,
            PostingStatus = "Posted",
            PostingDate = payment.PaymentDate,
            RequestedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
    }

    private static async Task<CompatibilityCreditNoteFixture> SeedCompatibilityCreditNoteAsync(
        ApplicationDbContext db,
        Guid tenantId)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var salesReturnsAccount = SeedAccount(db, tenantId, "5200", AccountType.Expense);
        var taxAccount = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var businessPartner = SeedBusinessPartner(db, tenantId, Guid.NewGuid(), arAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountAllowedAccountId = salesReturnsAccount.Id
        });

        await db.SaveChangesAsync();
        return new CompatibilityCreditNoteFixture(businessPartner, arAccount, salesReturnsAccount);
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
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
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
        bool isControlAccount = false,
        bool allowDirectPosting = true)
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
            CurrencyCode = "GHS",
            IsControlAccount = isControlAccount,
            AllowDirectPosting = allowDirectPosting
        };

        db.Accounts.Add(account);
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(db, tenantId, book, account);
        return account;
    }

    private static BusinessPartner SeedBusinessPartner(ApplicationDbContext db, Guid tenantId, Guid businessPartnerId, Guid arAccountId)
    {
        var businessPartner = new BusinessPartner
        {
            Id = businessPartnerId,
            TenantId = tenantId,
            PartnerCode = $"CUS-BP-{tenantId.ToString("N")[..6]}",
            PartnerName = "Test Customer BP",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            IsActive = true,
            IsBlacklisted = false,
            DefaultArAccountId = arAccountId,
            CreditLimit = 10000m,
            OutstandingBalance = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.Set<BusinessPartner>().Add(businessPartner);
        return businessPartner;
    }

    private static Invoice SeedPostedInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        BusinessPartner customer,
        Account arAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid fiscalPeriodId,
        bool seedPostingEvent = true)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == tenantId && item.Code == "IFRS");
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            SubTotal = amount,
            TotalAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            Status = InvoiceStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var invoiceJournal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{invoiceNumber}",
            JournalType = "AR Invoice",
            EntryDate = invoiceDate,
            Description = $"Posted invoice {invoiceNumber}",
            ReferenceNumber = invoiceNumber,
            SourceModule = "AR",
            SourceDocumentId = invoice.Id,
            SourceDocumentType = "CustomerInvoice",
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            IsBalanced = true,
            FiscalPeriodId = fiscalPeriodId,
            AccountingBookId = book.Id,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            PostingDate = invoiceDate,
            BookClassification = "IFRS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        invoice.JournalEntryId = invoiceJournal.Id;

        db.Invoices.Add(invoice);
        db.JournalEntries.Add(invoiceJournal);

        if (seedPostingEvent)
        {
            db.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SourceModule = "AR",
                SourceDocumentType = "CustomerInvoice",
                SourceDocumentId = invoice.Id,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                IdempotencyKey = $"AR:CustomerInvoice:{tenantId:N}:{invoice.Id:N}:Post",
                JournalEntryId = invoiceJournal.Id,
                AccountingBookId = book.Id,
                PostingStatus = "Posted",
                PostingDate = invoiceDate,
                RequestedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                TotalDebitAmount = amount,
                TotalCreditAmount = amount,
                FunctionalCurrencyCode = "GHS",
                BookClassification = "IFRS",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        }

        return invoice;
    }

    private sealed record ArCreditNoteFixture(
        CreditNote CreditNote,
        Invoice Invoice,
        BusinessPartner BusinessPartner,
        Account ArAccount,
        Account SalesReturnsAccount,
        Account TaxAccount,
        FinanceSettings Settings);

    private sealed record CompatibilityCreditNoteFixture(
        BusinessPartner BusinessPartner,
        Account ArAccount,
        Account SalesReturnsAccount);
}
