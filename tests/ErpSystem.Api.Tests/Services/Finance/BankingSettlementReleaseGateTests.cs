using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
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

/// <summary>
/// Release-gate scenarios that cross the banking-settlement boundaries. These tests intentionally
/// exercise the real Finance posting engine so the deposit and returned-cheque GL footprints are
/// eligible for the same reconciliation rules used in production.
/// </summary>
public sealed class BankingSettlementReleaseGateTests
{
    [Theory]
    [InlineData(LiquidityEntryType.CashExpense)]
    [InlineData(LiquidityEntryType.PettyCashReplenishment)]
    [InlineData(LiquidityEntryType.CustomerRefund)]
    [InlineData(LiquidityEntryType.OtherPayment)]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task PostedLiquidityPayment_ShouldRegisterOnce(
        LiquidityEntryType entryType)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var sourceDocumentId = Guid.NewGuid();
        var journal = SeedPostedJournal(
            db,
            setup,
            "CashPayment",
            sourceDocumentId,
            debitAccountId: setup.ExpenseAccount.Id,
            creditAccountId: setup.HoldingGlAccount.Id,
            amount: 150m);
        await db.SaveChangesAsync();
        var service = CreateBankingService(db, tenantId, Guid.NewGuid(), CreateWorkflow());
        var request = new RegisterPostedLiquidityPaymentDto
        {
            AccountTransactionId = journal.Transactions.Single(line => line.CreditAmount > 0m).Id,
            EntryType = entryType
        };

        var registered = await service.RegisterPostedPaymentAsync(request);

        registered.EntryType.Should().Be(entryType);
        registered.Direction.Should().Be(LiquidityEntryDirection.Decrease);
        registered.Amount.Should().Be(150m);
        registered.LiquidityAccountId.Should().Be(setup.HoldingAccount.Id);

        var duplicate = () => service.RegisterPostedPaymentAsync(request);
        await duplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in the banking queue*");
        (await db.LiquidityAccountEntries.CountAsync(entry =>
            entry.SourceDocumentType == nameof(AccountTransaction) &&
            entry.SourceDocumentId == request.AccountTransactionId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task ControlledNetBanking_ShouldEnforceAmountAndPercentageLimits()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var receipt = SeedLiquidityEntry(setup, LiquidityEntryType.CustomerReceipt, LiquidityEntryDirection.Increase, 1_000m);
        var deduction = SeedLiquidityEntry(setup, LiquidityEntryType.CashExpense, LiquidityEntryDirection.Decrease, 250m);
        db.LiquidityAccountEntries.AddRange(receipt, deduction);
        setup.Settings.MaximumDepositDeductionAmount = 200m;
        setup.Settings.MaximumDepositDeductionPercentage = null;
        await db.SaveChangesAsync();
        var service = CreateBankingService(db, tenantId, Guid.NewGuid(), CreateWorkflow());
        var request = CreateDepositRequest(setup, receipt, deduction, deductionAmount: 250m);

        var overAmount = () => service.CreateDepositAsync(request);
        await overAmount.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The deposit exceeds the configured maximum deduction amount.");

        setup.Settings.MaximumDepositDeductionAmount = null;
        setup.Settings.MaximumDepositDeductionPercentage = 20m;
        await db.SaveChangesAsync();

        var overPercentage = () => service.CreateDepositAsync(request);
        await overPercentage.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The deposit exceeds the configured maximum deduction percentage.");
        receipt.AllocatedAmount.Should().Be(0m);
        deduction.AllocatedAmount.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank-Dimensions")]
    public async Task DepositDraftEdit_ShouldPreserveStableAllocationIdentity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var receipt = SeedLiquidityEntry(
            setup,
            LiquidityEntryType.CustomerReceipt,
            LiquidityEntryDirection.Increase,
            500m);
        db.LiquidityAccountEntries.Add(receipt);
        await db.SaveChangesAsync();
        var service = CreateBankingService(db, tenantId, Guid.NewGuid(), CreateWorkflow());
        var created = await service.CreateDepositAsync(CreateDepositRequest(setup, receipt));
        var allocationId = created.Allocations.Single().Id;

        var updated = await service.UpdateDepositAsync(created.Id, new UpdateBankDepositDto
        {
            BankAccountId = setup.BankAccount.Id,
            DepositDate = created.DepositDate,
            DepositReference = "SLIP-001-EDITED",
            RowVersion = created.RowVersion,
            Allocations =
            [
                new BankDepositAllocationRequestDto
                {
                    LiquidityAccountEntryId = receipt.Id,
                    AllocationType = BankDepositAllocationType.Receipt,
                    Amount = 400m
                }
            ]
        });

        updated.Allocations.Should().ContainSingle();
        updated.Allocations.Single().Id.Should().Be(allocationId);
        updated.Allocations.Single().Amount.Should().Be(400m);
        (await db.LiquidityAccountEntries.SingleAsync(item => item.Id == receipt.Id))
            .AllocatedAmount.Should().Be(400m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank-ReadModels")]
    public async Task DepositRegister_ShouldKeepSummaryBoundedAndLoadAllocationsOnlyWhenRequested()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var firstReceipt = SeedLiquidityEntry(
            setup,
            LiquidityEntryType.CustomerReceipt,
            LiquidityEntryDirection.Increase,
            500m);
        var secondReceipt = SeedLiquidityEntry(
            setup,
            LiquidityEntryType.CustomerReceipt,
            LiquidityEntryDirection.Increase,
            300m);
        db.LiquidityAccountEntries.AddRange(firstReceipt, secondReceipt);
        await db.SaveChangesAsync();
        var service = CreateBankingService(db, tenantId, Guid.NewGuid(), CreateWorkflow());
        await service.CreateDepositAsync(CreateDepositRequest(setup, firstReceipt));
        var secondRequest = CreateDepositRequest(setup, secondReceipt);
        secondRequest.DepositReference = "SLIP-002";
        await service.CreateDepositAsync(secondRequest);

        var summary = await service.GetDepositsAsync(limit: 1);
        summary.Should().ContainSingle();
        summary.Single().Allocations.Should().BeEmpty();

        var selectableEvidence = await service.GetDepositsAsync(includeAllocations: true, limit: 10);
        selectableEvidence.Should().HaveCount(2);
        selectableEvidence.Should().OnlyContain(item => item.Allocations.Count == 1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task Deposit_ShouldRequireMakerCheckerThenPostOneNetBankTransaction()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId, requireEvidence: true);
        var receipt700 = SeedLiquidityEntry(setup, LiquidityEntryType.CustomerReceipt, LiquidityEntryDirection.Increase, 700m);
        var receipt300 = SeedLiquidityEntry(setup, LiquidityEntryType.CustomerReceipt, LiquidityEntryDirection.Increase, 300m);
        var deduction150 = SeedLiquidityEntry(setup, LiquidityEntryType.CashExpense, LiquidityEntryDirection.Decrease, 150m);
        var evidence = SeedEvidence(db, tenantId, makerId, "deposit-slip.pdf");
        db.LiquidityAccountEntries.AddRange(receipt700, receipt300, deduction150);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var maker = CreateBankingService(db, tenantId, makerId, workflow);
        var approver = CreateBankingService(db, tenantId, approverId, workflow);

        var deposit = await maker.CreateDepositAsync(CreateDepositRequest(
            setup,
            new[] { receipt700, receipt300 },
            deduction150,
            deductionAmount: 150m));
        await maker.LinkDepositAttachmentAsync(deposit.Id, new LinkBankingAttachmentDto
        {
            FileUploadRecordId = evidence.Id,
            DocumentType = "Deposit Slip",
            IsPrimaryEvidence = true
        });
        var submitted = await maker.SubmitDepositAsync(deposit.Id);

        submitted.Status.Should().Be(BankDepositStatus.Submitted);
        var makerApproval = () => maker.ApproveDepositAsync(deposit.Id);
        await makerApproval.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*submitted this banking document cannot approve*");

        var posted = await approver.ApproveDepositAsync(deposit.Id, "Deposit slip verified");

        posted.Status.Should().Be(BankDepositStatus.Posted);
        posted.TotalReceipts.Should().Be(1_000m);
        posted.TotalDeductions.Should().Be(150m);
        posted.NetAmount.Should().Be(850m);
        posted.Attachments.Should().ContainSingle(attachment => attachment.IsPrimaryEvidence);

        var makerConfirmation = () => maker.ConfirmDepositAsync(posted.Id, new ConfirmBankDepositDto
        {
            BankConfirmationReference = "BANK-ACK-001",
            BankConfirmationDate = posted.DepositDate,
            ConfirmationEvidenceFileId = evidence.Id,
            RowVersion = posted.RowVersion
        });
        await makerConfirmation.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*submitted this banking document cannot confirm*");

        var confirmed = await approver.ConfirmDepositAsync(posted.Id, new ConfirmBankDepositDto
        {
            BankConfirmationReference = "BANK-ACK-001",
            BankConfirmationDate = posted.DepositDate,
            ConfirmationEvidenceFileId = evidence.Id,
            Notes = "Bank-stamped deposit advice verified.",
            RowVersion = posted.RowVersion
        });
        confirmed.ConfirmationStatus.Should().Be(BankDepositConfirmationStatus.Confirmed);
        confirmed.BankConfirmationReference.Should().Be("BANK-ACK-001");
        confirmed.BankConfirmationEvidence.Should().NotBeNull();
        confirmed.IsReconciled.Should().BeFalse();
        (await db.AuditLogs.SingleAsync(log =>
            log.ResourceId == posted.Id.ToString() &&
            log.Action == FinanceAuditEvents.BankDepositConfirmed)).NewValues.Should().Contain("BANK-ACK-001");

        var cashTransaction = await db.Set<CashTransaction>().SingleAsync(transaction =>
            transaction.Id == posted.CashTransactionId);
        cashTransaction.TransactionType.Should().Be(CashTransactionType.Deposit);
        cashTransaction.Amount.Should().Be(850m);
        cashTransaction.IsPosted.Should().BeTrue();
        (await db.Set<CashTransaction>().CountAsync(transaction =>
            transaction.BankAccountId == setup.BankAccount.Id &&
            transaction.TransactionType == CashTransactionType.Deposit)).Should().Be(1);
        var journal = await db.JournalEntries
            .Include(entry => entry.Transactions)
            .SingleAsync(entry => entry.Id == posted.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.TotalDebitAmount.Should().Be(1_000m);
        journal.TotalCreditAmount.Should().Be(1_000m);
        journal.Transactions.Single(line => line.AccountId == setup.BankGlAccount.Id)
            .DebitAmount.Should().Be(850m);
        setup.BankAccount.CurrentBalance.Should().Be(850m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task DepositConfirmation_ShouldRejectInvalidLifecycleDateAndDuplicateBankReference()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var firstReceipt = SeedLiquidityEntry(setup, LiquidityEntryType.CustomerReceipt, LiquidityEntryDirection.Increase, 500m);
        var secondReceipt = SeedLiquidityEntry(setup, LiquidityEntryType.CustomerReceipt, LiquidityEntryDirection.Increase, 600m);
        db.LiquidityAccountEntries.AddRange(firstReceipt, secondReceipt);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var maker = CreateBankingService(db, tenantId, Guid.NewGuid(), workflow);
        var confirmer = CreateBankingService(db, tenantId, Guid.NewGuid(), workflow);

        var firstDraft = await maker.CreateDepositAsync(CreateDepositRequest(setup, firstReceipt));
        var draftConfirmation = () => confirmer.ConfirmDepositAsync(firstDraft.Id, new ConfirmBankDepositDto
        {
            BankConfirmationReference = "ACK-DUPLICATE",
            BankConfirmationDate = firstDraft.DepositDate,
            RowVersion = firstDraft.RowVersion
        });
        await draftConfirmation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only a posted bank deposit*");

        await maker.SubmitDepositAsync(firstDraft.Id);
        var firstPosted = await confirmer.ApproveDepositAsync(firstDraft.Id);
        var futureConfirmation = () => confirmer.ConfirmDepositAsync(firstPosted.Id, new ConfirmBankDepositDto
        {
            BankConfirmationReference = "ACK-DUPLICATE",
            BankConfirmationDate = DateTime.UtcNow.Date.AddDays(1),
            RowVersion = firstPosted.RowVersion
        });
        await futureConfirmation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be in the future*");

        await confirmer.ConfirmDepositAsync(firstPosted.Id, new ConfirmBankDepositDto
        {
            BankConfirmationReference = "ACK-DUPLICATE",
            BankConfirmationDate = firstPosted.DepositDate,
            RowVersion = firstPosted.RowVersion
        });

        var secondRequest = CreateDepositRequest(setup, secondReceipt);
        secondRequest.DepositReference = "SLIP-002";
        var secondDraft = await maker.CreateDepositAsync(secondRequest);
        await maker.SubmitDepositAsync(secondDraft.Id);
        var secondPosted = await confirmer.ApproveDepositAsync(secondDraft.Id);
        var duplicateReference = () => confirmer.ConfirmDepositAsync(secondPosted.Id, new ConfirmBankDepositDto
        {
            BankConfirmationReference = "ACK-DUPLICATE",
            BankConfirmationDate = secondPosted.DepositDate,
            RowVersion = secondPosted.RowVersion
        });
        await duplicateReference.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already linked to another deposit*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task PostedDeposit_ShouldMatchImportedStatementAndFinalizeReconciliation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var receipt = SeedLiquidityEntry(setup, LiquidityEntryType.CustomerReceipt, LiquidityEntryDirection.Increase, 850m);
        db.LiquidityAccountEntries.Add(receipt);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var maker = CreateBankingService(db, tenantId, Guid.NewGuid(), workflow);
        var approver = CreateBankingService(db, tenantId, Guid.NewGuid(), workflow);
        var deposit = await maker.CreateDepositAsync(CreateDepositRequest(setup, receipt));
        await maker.SubmitDepositAsync(deposit.Id);
        var posted = await approver.ApproveDepositAsync(deposit.Id);
        var statementLine = SeedStatementLine(
            db,
            tenantId,
            setup.BankAccount.Id,
            posted.DepositDate,
            posted.DepositReference,
            posted.NetAmount);
        await db.SaveChangesAsync();
        var reconciliationService = CreateReconciliationService(db, tenantId);
        var reconciliation = await reconciliationService.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = posted.DepositDate,
            StatementBalance = 850m,
            StatementId = statementLine.BankStatementId
        });

        var matches = (await reconciliationService.AutoMatchAsync(reconciliation.Id)).ToArray();
        var finalized = await reconciliationService.FinalizeReconciliationAsync(reconciliation.Id);

        matches.Should().ContainSingle();
        matches[0].CashTransactionId.Should().Be(posted.CashTransactionId!.Value);
        matches[0].BankStatementLineId.Should().Be(statementLine.Id);
        finalized.Status.Should().Be(ReconciliationStatus.Completed);
        finalized.Difference.Should().Be(0m);
        var refreshedDeposit = await maker.GetDepositAsync(posted.Id);
        refreshedDeposit.Should().NotBeNull();
        refreshedDeposit!.IsReconciled.Should().BeTrue();
        refreshedDeposit.BankReconciliationId.Should().Be(reconciliation.Id);
        refreshedDeposit.ReconciliationStatus.Should().Be(ReconciliationStatus.Completed);
        (await db.Set<CashTransaction>().SingleAsync(transaction => transaction.Id == posted.CashTransactionId))
            .IsReconciled.Should().BeTrue();
        (await db.Set<BankStatementLine>().SingleAsync(line => line.Id == statementLine.Id))
            .IsMatched.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankingSettlement")]
    [Trait("Category", "CashBank")]
    public async Task ReturnedCheque_ShouldRequireEvidenceAndApprovalThenReopenReceivable()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = await SeedSetupAsync(db, tenantId);
        var receipt = SeedChequeReceipt(db, setup, amount: 100m);
        var queueEntry = SeedLiquidityEntry(
            setup,
            LiquidityEntryType.CustomerReceipt,
            LiquidityEntryDirection.Increase,
            100m,
            nameof(CustomerPayment),
            receipt.Payment.Id);
        db.LiquidityAccountEntries.Add(queueEntry);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var maker = CreateBankingService(db, tenantId, makerId, workflow);
        var approver = CreateBankingService(db, tenantId, approverId, workflow);
        var deposit = await maker.CreateDepositAsync(CreateDepositRequest(setup, queueEntry));
        await maker.SubmitDepositAsync(deposit.Id);
        var postedDeposit = await approver.ApproveDepositAsync(deposit.Id);

        var returned = await maker.CreateReturnedChequeAsync(new CreateReturnedChequeCaseDto
        {
            CustomerPaymentId = receipt.Payment.Id,
            BankDepositBatchId = postedDeposit.Id,
            BankAccountId = setup.BankAccount.Id,
            ReturnDate = postedDeposit.DepositDate.AddDays(1),
            BankReference = "BANK-RETURN-001",
            ReturnReason = "Insufficient funds",
            BankChargeAmount = 5m,
            ChargeTreatment = ReturnedChequeChargeTreatment.CustomerRecoverable,
            DrawerBank = "Drawer Bank"
        });
        var duplicate = () => maker.CreateReturnedChequeAsync(new CreateReturnedChequeCaseDto
        {
            CustomerPaymentId = receipt.Payment.Id,
            BankDepositBatchId = postedDeposit.Id,
            BankAccountId = setup.BankAccount.Id,
            ReturnDate = postedDeposit.DepositDate.AddDays(1),
            BankReference = "BANK-RETURN-002",
            ReturnReason = "Duplicate case",
            ChargeTreatment = ReturnedChequeChargeTreatment.CustomerRecoverable
        });
        await duplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("An active returned-cheque case already exists for this receipt.");

        var missingEvidence = () => maker.SubmitReturnedChequeAsync(returned.Id);
        await missingEvidence.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Primary bank-return evidence is required before submission.");
        var evidence = SeedEvidence(db, tenantId, makerId, "returned-cheque.png");
        await db.SaveChangesAsync();
        await maker.LinkReturnedChequeAttachmentAsync(returned.Id, new LinkBankingAttachmentDto
        {
            FileUploadRecordId = evidence.Id,
            DocumentType = "Bank Return Advice",
            IsPrimaryEvidence = true
        });
        await maker.SubmitReturnedChequeAsync(returned.Id);

        var postedReturn = await approver.ApproveReturnedChequeAsync(returned.Id, "Return advice verified");

        postedReturn.Status.Should().Be(ReturnedChequeCaseStatus.Posted);
        postedReturn.ReturnedAmount.Should().Be(100m);
        postedReturn.CustomerRecoverableChargeAmount.Should().Be(5m);
        var payment = await db.Set<CustomerPayment>().SingleAsync(item => item.Id == receipt.Payment.Id);
        payment.Status.Should().Be("Bounced");
        payment.AllocatedAmount.Should().Be(0m);
        var allocation = await db.Set<PaymentAllocation>().SingleAsync(item => item.Id == receipt.Allocation.Id);
        allocation.IsReversal.Should().BeTrue();
        var invoice = await db.Invoices.SingleAsync(item => item.Id == receipt.Invoice.Id);
        invoice.PaidAmount.Should().Be(0m);
        invoice.Status.Should().Be(InvoiceStatus.Sent);
        var bankDebit = await db.Set<CashTransaction>().SingleAsync(transaction =>
            transaction.Id == postedReturn.ReturnCashTransactionId);
        bankDebit.TransactionType.Should().Be(CashTransactionType.ReturnedCheque);
        bankDebit.Amount.Should().Be(105m);
        setup.BankAccount.CurrentBalance.Should().Be(-5m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"banking-settlement-release-gate-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<BankingSetup> SeedSetupAsync(
        ApplicationDbContext db,
        Guid tenantId,
        bool requireEvidence = false)
    {
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Banking Release Gate Tenant",
            Code = tenantId.ToString("N")[..6].ToUpperInvariant(),
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        };
        var book = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            Purpose = "Primary", BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
            IsDefault = true, IsActive = true, AllowsPosting = true
        };
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
            PeriodStatus = "Open",
            IsOpen = true
        };
        var bankGl = SeedAccount(tenantId, "1100", AccountType.Asset);
        var holdingGl = SeedAccount(tenantId, "1110", AccountType.Asset);
        var arControl = SeedAccount(tenantId, "1200", AccountType.Asset, isControl: true, directPosting: false);
        var expense = SeedAccount(tenantId, "6200", AccountType.Expense);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = "BANK-001",
            AccountName = "Operating Bank",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = bankGl.Id,
            IsActive = true,
            OpeningDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var holding = new LiquidityAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "UNDEPOSITED-CASH",
            Name = "Undeposited Cash",
            AccountType = LiquidityAccountType.UndepositedCash,
            Currency = "GHS",
            GLAccountId = holdingGl.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var settings = new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            BankDepositPolicy = DepositPolicy.ControlledNetBanking,
            RequireBankDepositPrimaryEvidence = requireEvidence,
            AutoPostBankDepositAfterApproval = true,
            ControlAccountArId = arControl.Id,
            ReturnedChequeBankChargeAccountId = expense.Id
        };
        db.Tenants.Add(tenant);
        db.AccountingBooks.Add(book);
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        db.Accounts.AddRange(bankGl, holdingGl, arControl, expense);
        FinancePostingAuthorityFixture.SeedEnabledBookMappings(
            db, tenantId, book, bankGl, holdingGl, arControl, expense);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, book.Code);
        db.BankAccounts.Add(bank);
        db.LiquidityAccounts.Add(holding);
        db.Set<FinanceSettings>().Add(settings);
        await db.SaveChangesAsync();
        return new BankingSetup(tenantId, period, bank, bankGl, holding, holdingGl, arControl, expense, settings);
    }

    private static BankingSettlementService CreateBankingService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId,
        Mock<IWorkflowIntegrationService> workflow)
    {
        var currentUser = CreateCurrentUser(tenantId, userId);
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(service => service.GenerateAsync(
                DocumentNumberingModules.Finance,
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"DOC-{Guid.NewGuid():N}"[..24]);
        var audit = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        var posting = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            audit);
        return new BankingSettlementService(
            db,
            currentUser.Object,
            numbering.Object,
            workflow.Object,
            posting,
            audit);
    }

    private static BankReconciliationService CreateReconciliationService(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId, Guid.NewGuid());
        var audit = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        return new BankReconciliationService(
            db,
            new BankReconciliationEngine(),
            currentUser.Object,
            Mock.Of<IWorkflowService>(),
            Mock.Of<ICashTransactionService>(),
            audit);
    }

    private static Mock<IWorkflowIntegrationService> CreateWorkflow()
    {
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(service => service.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync(() => new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Pending));
        workflow.Setup(service => service.CanUserApproveAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(service => service.ProcessApprovalAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                "Approve",
                It.IsAny<string?>()))
            .ReturnsAsync(() => new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Completed,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Approved));
        return workflow;
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns($"user-{userId:N}");
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("banking-release-gate-tests");
        currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        currentUser.SetupGet(service => service.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static Account SeedAccount(
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        bool isControl = false,
        bool directPosting = true)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            IsControlAccount = isControl,
            AllowDirectPosting = directPosting
        };

    private static LiquidityAccountEntry SeedLiquidityEntry(
        BankingSetup setup,
        LiquidityEntryType entryType,
        LiquidityEntryDirection direction,
        decimal amount,
        string sourceDocumentType = "TestSource",
        Guid? sourceDocumentId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            LiquidityAccountId = setup.HoldingAccount.Id,
            EntryNumber = $"LQE-{Guid.NewGuid():N}"[..20],
            EntryDate = new DateTime(2026, 7, 6),
            EntryType = entryType,
            Direction = direction,
            Amount = amount,
            Currency = "GHS",
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId ?? Guid.NewGuid(),
            ReferenceNumber = $"REF-{Guid.NewGuid():N}"[..16],
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

    private static CreateBankDepositDto CreateDepositRequest(
        BankingSetup setup,
        LiquidityAccountEntry receipt)
        => CreateDepositRequest(setup, new[] { receipt }, null, 0m);

    private static CreateBankDepositDto CreateDepositRequest(
        BankingSetup setup,
        LiquidityAccountEntry receipt,
        LiquidityAccountEntry deduction,
        decimal deductionAmount)
        => CreateDepositRequest(setup, new[] { receipt }, deduction, deductionAmount);

    private static CreateBankDepositDto CreateDepositRequest(
        BankingSetup setup,
        IReadOnlyCollection<LiquidityAccountEntry> receipts,
        LiquidityAccountEntry? deduction,
        decimal deductionAmount)
    {
        var request = new CreateBankDepositDto
        {
            BankAccountId = setup.BankAccount.Id,
            DepositDate = new DateTime(2026, 7, 6),
            DepositReference = "SLIP-001"
        };
        request.Allocations.AddRange(receipts.Select(receipt => new BankDepositAllocationRequestDto
        {
            LiquidityAccountEntryId = receipt.Id,
            AllocationType = BankDepositAllocationType.Receipt,
            Amount = receipt.Amount
        }));
        if (deduction != null)
        {
            request.Allocations.Add(new BankDepositAllocationRequestDto
            {
                LiquidityAccountEntryId = deduction.Id,
                AllocationType = BankDepositAllocationType.Deduction,
                Amount = deductionAmount
            });
        }
        return request;
    }

    private static JournalEntry SeedPostedJournal(
        ApplicationDbContext db,
        BankingSetup setup,
        string sourceDocumentType,
        Guid sourceDocumentId,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount)
    {
        var book = db.AccountingBooks.Local.Single(item => item.TenantId == setup.TenantId && item.Code == "IFRS");
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            JournalEntryNumber = $"JE-{Guid.NewGuid():N}"[..20],
            JournalType = "Cash Payment",
            EntryDate = new DateTime(2026, 7, 6),
            Description = "Posted cash payment",
            ReferenceNumber = "PAY-001",
            SourceModule = "CashManagement",
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            TotalDebitAmount = amount,
            TotalCreditAmount = amount,
            IsBalanced = true,
            FiscalPeriodId = setup.Period.Id,
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            PostingDate = new DateTime(2026, 7, 6),
            BookClassification = "IFRS",
            AccountingBookId = book.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        journal.Transactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            JournalEntryId = journal.Id,
            AccountId = debitAccountId,
            TransactionDate = journal.EntryDate,
            DebitAmount = amount,
            FunctionalCurrencyCode = "GHS",
            FiscalPeriodId = setup.Period.Id,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            AccountingBookId = book.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        journal.Transactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            JournalEntryId = journal.Id,
            AccountId = creditAccountId,
            TransactionDate = journal.EntryDate,
            CreditAmount = amount,
            TransactionCreditAmount = amount,
            FunctionalCurrencyCode = "GHS",
            FiscalPeriodId = setup.Period.Id,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            AccountingBookId = book.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        db.JournalEntries.Add(journal);
        return journal;
    }

    private static FileUploadRecord SeedEvidence(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId,
        string fileName)
    {
        var evidence = new FileUploadRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "banking-evidence",
            FilePath = $"banking/{fileName}",
            StoredFileName = $"{Guid.NewGuid():N}-{fileName}",
            OriginalFileName = fileName,
            ContentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "image/png",
            FileSize = 1_024,
            StorageProvider = "Test",
            UploadedByUserId = userId,
            VirusScanStatus = FileVirusScanStatus.Clean,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.FileUploadRecords.Add(evidence);
        return evidence;
    }

    private static BankStatementLine SeedStatementLine(
        ApplicationDbContext db,
        Guid tenantId,
        Guid bankAccountId,
        DateTime transactionDate,
        string reference,
        decimal creditAmount)
    {
        var statement = new BankStatement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankAccountId = bankAccountId,
            StatementDate = transactionDate,
            StatementNumber = $"STMT-{Guid.NewGuid():N}"[..20],
            OpeningBalance = 0m,
            ClosingBalance = creditAmount,
            TotalCredits = creditAmount,
            ImportedAt = DateTime.UtcNow
        };
        var line = new BankStatementLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankStatementId = statement.Id,
            TransactionDate = transactionDate,
            Description = "Deposit slip",
            ReferenceNumber = reference,
            CreditAmount = creditAmount,
            Balance = creditAmount
        };
        db.Set<BankStatement>().Add(statement);
        db.Set<BankStatementLine>().Add(line);
        return line;
    }

    private static ChequeReceipt SeedChequeReceipt(
        ApplicationDbContext db,
        BankingSetup setup,
        decimal amount)
    {
        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            PartnerCode = "CUS-CHEQUE",
            PartnerName = "Cheque Customer",
            PartnerType = "Customer",
            RegistrationStatus = "Approved",
            IsActive = true,
            DefaultArAccountId = setup.ArControlAccount.Id,
            OutstandingBalance = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            InvoiceNumber = "INV-CHEQUE-001",
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            InvoiceDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            SubTotal = amount,
            TotalAmount = amount,
            PaidAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            Status = InvoiceStatus.Paid,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var receiptJournal = SeedPostedJournal(
            db,
            setup,
            nameof(CustomerPayment),
            Guid.NewGuid(),
            setup.HoldingGlAccount.Id,
            setup.ArControlAccount.Id,
            amount);
        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            PaymentNumber = "CP-CHEQUE-001",
            CustomerId = customer.Id,
            PaymentDate = new DateTime(2026, 7, 5),
            TotalAmount = amount,
            AllocatedAmount = amount,
            PaymentMethod = "Cheque",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            LiquidityAccountId = setup.HoldingAccount.Id,
            CheckNumber = "CHQ-001",
            ChequeDrawerBank = "Drawer Bank",
            Status = "Posted",
            JournalEntryId = receiptJournal.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        receiptJournal.SourceDocumentId = payment.Id;
        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = setup.TenantId,
            CustomerPaymentId = payment.Id,
            InvoiceId = invoice.Id,
            AllocatedAmount = amount,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<BusinessPartner>().Add(customer);
        db.Invoices.Add(invoice);
        db.Set<CustomerPayment>().Add(payment);
        db.Set<PaymentAllocation>().Add(allocation);
        return new ChequeReceipt(payment, invoice, allocation);
    }

    private sealed record BankingSetup(
        Guid TenantId,
        FiscalPeriod Period,
        BankAccount BankAccount,
        Account BankGlAccount,
        LiquidityAccount HoldingAccount,
        Account HoldingGlAccount,
        Account ArControlAccount,
        Account ExpenseAccount,
        FinanceSettings Settings);

    private sealed record ChequeReceipt(
        CustomerPayment Payment,
        Invoice Invoice,
        PaymentAllocation Allocation);
}
