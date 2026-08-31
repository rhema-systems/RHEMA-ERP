using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        debitAccount.Balance.Should().Be(0m);
        creditAccount.Balance.Should().Be(0m);
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

        cashAccount.Balance.Should().Be(100m);
        revenueAccount.Balance.Should().Be(100m);
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
    public async Task PostAsync_ShouldApplyAccountBalanceMovementUsingNormalBalanceDirection()
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

        await service.PostAsync(new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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

        assetAccount.Balance.Should().Be(70m);
        expenseAccount.Balance.Should().Be(40m);
        liabilityAccount.Balance.Should().Be(100m);
        equityAccount.Balance.Should().Be(25m);
        revenueAccount.Balance.Should().Be(-15m);
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
        debitAccount.Balance.Should().Be(100m);
        creditAccount.Balance.Should().Be(100m);
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
        SeedOpenPeriod(db, tenantId, isOpen: false, isClosed: true);
        var debitAccount = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var creditAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = CreateRequest(tenantId, debitAccount.Id, creditAccount.Id);

        var act = () => service.PostAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
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
            .WithMessage("Posting period is not open.");
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
    public async Task PostAsync_ShouldUpdateAccountCurrencyLinkBalanceAndHistory_WhenForeignCurrencyPosts()
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
            RevaluationRequired = true,
            TransactionRateType = "Daily",
            RevaluationRateType = "Month-End"
        };
        db.AccountCurrencyLinks.Add(usdLink);
        await db.SaveChangesAsync();

        await CreateService(db, tenantId).PostAsync(CreateForeignCurrencyRequest(tenantId, cashAccount.Id, revenueAccount.Id));

        usdLink.ForeignCurrencyBalance.Should().Be(100m);
        usdLink.BaseCurrencyEquivalent.Should().Be(1500m);
        usdLink.CurrentExchangeRate.Should().Be(15m);
        usdLink.RateEffectiveDate.Should().Be(new DateTime(2026, 7, 4));
        usdLink.HasTransactionHistory.Should().BeTrue();
        usdLink.TransactionCount.Should().Be(1);
        usdLink.FirstTransactionDate.Should().Be(new DateTime(2026, 7, 4));
        usdLink.LastTransactionDate.Should().Be(new DateTime(2026, 7, 4));
        exchangeRate.HasBeenUsedInTransactions.Should().BeTrue();
        exchangeRate.TransactionCount.Should().Be(1);
        cashAccount.Balance.Should().Be(1500m);
        revenueAccount.Balance.Should().Be(1500m);
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
        var reversal = await service.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "TEST",
            SourceDocumentType = "TestDocumentReversal",
            SourceDocumentId = request.SourceDocumentId,
            SourceDocumentTenantId = tenantId,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = plan.Reason,
            ReversalType = "Manual",
            PostingAction = plan.PostingAction,
            SourceDocumentReference = "REV-SRC-001",
            Description = "Exact dimension reversal",
            PostingDate = plan.ReversalDate,
            JournalType = "Reversing",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = "GHS",
            Lines = plan.ReversalLines
        });

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
    [Trait("Category", "FinanceDimensions")]
    public void Migration_ShouldAddDimensionFoundationAndNullablePostedLineLinkage()
    {
        var migration = new TestFinanceDimensionsMigration();
        var operations = migration.BuildUpOperations();

        operations.OfType<CreateTableOperation>().Select(x => x.Name).Should().BeEquivalentTo(new[]
        {
            "FinanceDimensionDefinitions",
            "FinanceDimensionValues",
            "FinanceDimensionSets",
            "FinanceDimensionSetItems",
            "FinanceDimensionAccountRules"
        });
        operations.OfType<AddColumnOperation>().Should().ContainSingle(x =>
            x.Table == "AccountTransactions" && x.Name == "FinanceDimensionSetId" && x.IsNullable);
        operations.OfType<AddForeignKeyOperation>().Should().ContainSingle(x =>
            x.Table == "AccountTransactions"
            && x.PrincipalTable == "FinanceDimensionSets"
            && x.OnDelete == ReferentialAction.Restrict);

        var down = migration.BuildDownOperations();
        down.OfType<DropColumnOperation>().Should().ContainSingle(x =>
            x.Table == "AccountTransactions" && x.Name == "FinanceDimensionSetId");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-posting-engine-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestFinanceDimensionsMigration : AddFinanceTransactionDimensions
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }

    private static FinancePostingEngine CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IDictionary<string, string>? claims = null)
    {
        var currentUser = CreateCurrentUser(tenantId, claims);
        return new FinancePostingEngine(db, currentUser.Object, Mock.Of<ILogger<FinancePostingEngine>>());
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
        return account;
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
            Rate = rate,
            InverseRate = decimal.Round(1m / rate, 6, MidpointRounding.AwayFromZero),
            EffectiveDate = new DateTime(2026, 7, 4),
            RateType = ExchangeRateType.Daily,
            RateSource = "Unit Test",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved
        };

        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private static FinancePostingRequestDto CreateRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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

    private static FinancePostingRequestDto CreateForeignCurrencyRequest(Guid tenantId, Guid debitAccountId, Guid creditAccountId)
    {
        return new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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
