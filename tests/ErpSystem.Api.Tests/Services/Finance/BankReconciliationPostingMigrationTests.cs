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
using ErpSystem.Core.DTOs.Workflow;
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

public sealed class BankReconciliationPostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task ManualMatch_ShouldOnlyAllowSameTenantPostedCashTransaction()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, 100m);
        var statementLine = SeedStatementLine(db, tenantId, fixture.BankAccount.Id, creditAmount: 100m);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = fixture.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 100m,
            StatementId = statementLine.BankStatementId
        });

        var match = await service.CreateManualMatchAsync(new CreateManualMatchDto
        {
            ReconciliationId = reconciliation.Id,
            CashTransactionId = fixture.Transaction.Id,
            BankStatementLineId = statementLine.Id
        });

        match.CashTransactionId.Should().Be(fixture.Transaction.Id);
        (await db.Set<CashTransaction>().SingleAsync(t => t.Id == fixture.Transaction.Id)).IsReconciled.Should().BeTrue();
        (await db.Set<ReconciliationMatch>().CountAsync(m => m.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task ManualMatch_ShouldRejectUnpostedCashTransaction()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 0m);
        var offset = SeedAccount(db, tenantId, "4100", AccountType.Revenue);
        var transaction = SeedCashTransaction(db, tenantId, setup.BankAccount.Id, offset.Id, CashTransactionType.Receipt, CashTransactionApprovalStatus.Captured, isPosted: false);
        var statementLine = SeedStatementLine(db, tenantId, setup.BankAccount.Id, creditAmount: 100m);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 0m,
            StatementId = statementLine.BankStatementId
        });

        var act = () => service.CreateManualMatchAsync(new CreateManualMatchDto
        {
            ReconciliationId = reconciliation.Id,
            CashTransactionId = transaction.Id,
            BankStatementLineId = statementLine.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only posted cash/bank transactions can be reconciled.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task ManualMatch_ShouldRejectCrossTenantStatementLine()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, 100m);
        var otherSetup = SeedBankSetup(db, otherTenantId, "BANK-OTH", 0m);
        var otherStatementLine = SeedStatementLine(db, otherTenantId, otherSetup.BankAccount.Id, creditAmount: 100m);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = fixture.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 100m
        });

        var act = () => service.CreateManualMatchAsync(new CreateManualMatchDto
        {
            ReconciliationId = reconciliation.Id,
            CashTransactionId = fixture.Transaction.Id,
            BankStatementLineId = otherStatementLine.Id
        });

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Statement line not found");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task StartReconciliation_ShouldRejectCrossTenantBankAccount()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var otherSetup = SeedBankSetup(db, otherTenantId, "BANK-OTH", 0m);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);

        var act = () => service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = otherSetup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 0m
        });

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Bank account not found");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task BankChargeAdjustment_ShouldPostThroughFinancePostingEngineAndAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 100m);
        var bankCharge = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId, documentPrefix: "ADJ-BCHG");
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 90m
        });

        var adjustment = await service.CreateAndPostAdjustmentAsync(reconciliation.Id, new CreateReconciliationAdjustmentDto
        {
            AdjustmentType = ReconciliationAdjustmentType.BankCharge,
            TransactionDate = new DateTime(2026, 7, 6),
            Amount = 10m,
            OffsetAccountId = bankCharge.Id,
            IdempotencyKey = "BCHG-001",
            Notes = "Monthly bank charge"
        });

        adjustment.CashTransactionType.Should().Be(CashTransactionType.Payment);
        adjustment.JournalEntryId.Should().NotBeNull();
        adjustment.PostingEventId.Should().NotBeNull();
        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync(j => j.Id == adjustment.JournalEntryId);
        journal.SourceModule.Should().Be("CASHBANK");
        journal.Transactions.Single(t => t.AccountId == bankCharge.Id).DebitAmount.Should().Be(10m);
        journal.Transactions.Single(t => t.AccountId == setup.BankGlAccount.Id).CreditAmount.Should().Be(10m);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.BankReconciliationAdjustmentPosted && a.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task InterestIncomeAdjustment_ShouldPostThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 100m);
        var interestIncome = SeedAccount(db, tenantId, "4800", AccountType.Revenue);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId, documentPrefix: "ADJ-INT");
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 110m
        });

        var adjustment = await service.CreateAndPostAdjustmentAsync(reconciliation.Id, new CreateReconciliationAdjustmentDto
        {
            AdjustmentType = ReconciliationAdjustmentType.InterestIncome,
            TransactionDate = new DateTime(2026, 7, 6),
            Amount = 10m,
            OffsetAccountId = interestIncome.Id,
            IdempotencyKey = "INT-001"
        });

        adjustment.CashTransactionType.Should().Be(CashTransactionType.Receipt);
        var journal = await db.JournalEntries.Include(j => j.Transactions).SingleAsync(j => j.Id == adjustment.JournalEntryId);
        journal.Transactions.Single(t => t.AccountId == setup.BankGlAccount.Id).DebitAmount.Should().Be(10m);
        journal.Transactions.Single(t => t.AccountId == interestIncome.Id).CreditAmount.Should().Be(10m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task DuplicateAdjustmentIdempotencyKey_ShouldReturnExistingPostedAdjustment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 100m);
        var bankCharge = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId, documentPrefix: "ADJ-DUP");
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 90m
        });
        var dto = new CreateReconciliationAdjustmentDto
        {
            AdjustmentType = ReconciliationAdjustmentType.BankCharge,
            TransactionDate = new DateTime(2026, 7, 6),
            Amount = 10m,
            OffsetAccountId = bankCharge.Id,
            IdempotencyKey = "DUP-BCHG-001"
        };

        var first = await service.CreateAndPostAdjustmentAsync(reconciliation.Id, dto);
        var second = await service.CreateAndPostAdjustmentAsync(reconciliation.Id, dto);

        second.WasDuplicate.Should().BeTrue();
        second.CashTransactionId.Should().Be(first.CashTransactionId);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == first.CashTransactionId)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.BankReconciliationAdjustmentDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task AdjustmentWithoutIdempotencyKey_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 100m);
        var bankCharge = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId, documentPrefix: "ADJ-NOKEY");
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 90m
        });

        var act = () => service.CreateAndPostAdjustmentAsync(reconciliation.Id, new CreateReconciliationAdjustmentDto
        {
            AdjustmentType = ReconciliationAdjustmentType.BankCharge,
            TransactionDate = new DateTime(2026, 7, 6),
            Amount = 10m,
            OffsetAccountId = bankCharge.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Reconciliation adjustment idempotency key is required.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task ClosedPeriodAdjustmentPosting_ShouldFail()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 100m, periodStatus: "Closed");
        var bankCharge = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId, documentPrefix: "ADJ-CLOSED");
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 90m
        });

        var act = () => service.CreateAndPostAdjustmentAsync(reconciliation.Id, new CreateReconciliationAdjustmentDto
        {
            AdjustmentType = ReconciliationAdjustmentType.BankCharge,
            TransactionDate = new DateTime(2026, 7, 6),
            Amount = 10m,
            OffsetAccountId = bankCharge.Id,
            IdempotencyKey = "CLOSED-BCHG-001"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*period*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task ZeroAmountAdjustment_ShouldFailAsUnbalancedOrInvalid()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 100m);
        var bankCharge = SeedAccount(db, tenantId, "6200", AccountType.Expense);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = setup.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 100m
        });

        var act = () => service.CreateAndPostAdjustmentAsync(reconciliation.Id, new CreateReconciliationAdjustmentDto
        {
            AdjustmentType = ReconciliationAdjustmentType.BankCharge,
            TransactionDate = new DateTime(2026, 7, 6),
            Amount = 0m,
            OffsetAccountId = bankCharge.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Reconciliation adjustment amount must be greater than zero.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task FinalizedReconciliation_ShouldNotAllowMatchRemoval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, 100m);
        var statementLine = SeedStatementLine(db, tenantId, fixture.BankAccount.Id, creditAmount: 100m);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = fixture.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 100m,
            StatementId = statementLine.BankStatementId
        });
        var match = await service.CreateManualMatchAsync(new CreateManualMatchDto
        {
            ReconciliationId = reconciliation.Id,
            CashTransactionId = fixture.Transaction.Id,
            BankStatementLineId = statementLine.Id
        });
        await service.FinalizeReconciliationAsync(reconciliation.Id);

        var act = () => service.RemoveMatchAsync(match.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Finalized bank reconciliations cannot be changed by remove-match.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-BankReconciliation")]
    [Trait("Category", "CashBank")]
    public async Task ValidSameTenantReconciliation_ShouldFinalizeAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedPostedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, 100m);
        var statementLine = SeedStatementLine(db, tenantId, fixture.BankAccount.Id, creditAmount: 100m);
        await db.SaveChangesAsync();
        var service = CreateReconciliationService(db, tenantId);
        var reconciliation = await service.StartReconciliationAsync(new StartReconciliationDto
        {
            BankAccountId = fixture.BankAccount.Id,
            ReconciliationDate = new DateTime(2026, 7, 6),
            StatementBalance = 100m,
            StatementId = statementLine.BankStatementId
        });
        await service.CreateManualMatchAsync(new CreateManualMatchDto
        {
            ReconciliationId = reconciliation.Id,
            CashTransactionId = fixture.Transaction.Id,
            BankStatementLineId = statementLine.Id
        });

        var finalized = await service.FinalizeReconciliationAsync(reconciliation.Id);

        finalized.Status.Should().Be(ReconciliationStatus.Completed);
        finalized.Difference.Should().Be(0m);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.BankReconciliationFinalized && a.TenantId == tenantId)).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"bank-reconciliation-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static BankReconciliationService CreateReconciliationService(
        ApplicationDbContext db,
        Guid tenantId,
        string documentPrefix = "BRC")
    {
        var currentUser = CreateCurrentUserService(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-bank-reconciliation" }
            });
        var cashService = CreateCashTransactionService(db, currentUser.Object, auditService, documentPrefix);
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("BankReconciliation", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        workflow.Setup(x => x.CanUserApproveAsync("BankReconciliation", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(x => x.ProcessApprovalStepAsync("BankReconciliation", It.IsAny<Guid>(), It.IsAny<Guid>(), "Approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });

        return new BankReconciliationService(
            db,
            new BankReconciliationEngine(),
            currentUser.Object,
            workflow.Object,
            cashService,
            auditService);
    }

    private static CashTransactionService CreateCashTransactionService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceAuditService auditService,
        string documentPrefix)
    {
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var counter = 0;
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
            .ReturnsAsync(() => $"{documentPrefix}-{++counter:0000}");

        return new CashTransactionService(
            db,
            Mock.Of<IBankAccountService>(),
            tenantSettings.Object,
            documentNumbering.Object,
            currentUser,
            postingEngine,
            auditService);
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("bank.reconciliation.tests");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("bank-reconciliation-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<CashFixture> SeedPostedCashTransactionAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CashTransactionType transactionType,
        decimal amount)
    {
        var setup = SeedBankSetup(db, tenantId, "BANK-001", 0m);
        var offset = SeedAccount(
            db,
            tenantId,
            transactionType == CashTransactionType.Receipt ? "4100" : "6200",
            transactionType == CashTransactionType.Receipt ? AccountType.Revenue : AccountType.Expense);
        await db.SaveChangesAsync();
        var transaction = SeedCashTransaction(
            db,
            tenantId,
            setup.BankAccount.Id,
            offset.Id,
            transactionType,
            CashTransactionApprovalStatus.Approved,
            isPosted: false,
            amount: amount);
        await db.SaveChangesAsync();
        var cashService = CreateCashTransactionService(
            db,
            CreateCurrentUserService(tenantId).Object,
            new FinanceAuditService(
                db,
                CreateCurrentUserService(tenantId).Object,
                new HttpContextAccessor { HttpContext = new DefaultHttpContext() }),
            "SEED");
        await cashService.PostAsync(transaction.Id);
        var posted = await db.Set<CashTransaction>().SingleAsync(t => t.Id == transaction.Id);
        return new CashFixture(posted, setup.BankAccount, setup.BankGlAccount, offset);
    }

    private static CashTransaction SeedCashTransaction(
        ApplicationDbContext db,
        Guid tenantId,
        Guid bankAccountId,
        Guid offsetAccountId,
        CashTransactionType transactionType,
        CashTransactionApprovalStatus approvalStatus,
        bool isPosted,
        decimal amount = 100m)
    {
        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = $"{transactionType.ToString().ToUpperInvariant()}-{Guid.NewGuid():N}"[..24],
            TransactionDate = new DateTime(2026, 7, 6),
            TransactionType = transactionType,
            BankAccountId = bankAccountId,
            Amount = amount,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = amount,
            GLAccountId = offsetAccountId,
            Description = "Seed cash transaction",
            ApprovalStatus = approvalStatus,
            IsPosted = isPosted,
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().Add(transaction);
        return transaction;
    }

    private static BankStatementLine SeedStatementLine(
        ApplicationDbContext db,
        Guid tenantId,
        Guid bankAccountId,
        decimal debitAmount = 0m,
        decimal creditAmount = 0m)
    {
        var statement = new BankStatement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankAccountId = bankAccountId,
            StatementDate = new DateTime(2026, 7, 6),
            StatementNumber = $"STMT-{Guid.NewGuid():N}"[..20],
            OpeningBalance = 0m,
            ClosingBalance = creditAmount - debitAmount,
            TotalDebits = debitAmount,
            TotalCredits = creditAmount,
            ImportedAt = DateTime.UtcNow
        };
        var line = new BankStatementLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankStatementId = statement.Id,
            TransactionDate = new DateTime(2026, 7, 6),
            Description = "Statement line",
            ReferenceNumber = $"REF-{Guid.NewGuid():N}"[..16],
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            Balance = creditAmount - debitAmount,
            IsMatched = false
        };
        db.Set<BankStatement>().Add(statement);
        db.Set<BankStatementLine>().Add(line);
        return line;
    }

    private static BankSetup SeedBankSetup(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        decimal openingBalance,
        string glAccountNumber = "1000",
        string periodStatus = "Open")
    {
        SeedTenant(db, tenantId);
        SeedPeriod(db, tenantId, periodStatus);
        var bankGl = SeedAccount(db, tenantId, glAccountNumber, AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = accountNumber,
            AccountName = $"Bank {accountNumber}",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = bankGl.Id,
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            AvailableBalance = openingBalance,
            IsActive = true,
            OpeningDate = new DateTime(2026, 7, 1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.BankAccounts.Add(bank);
        return new BankSetup(bank, bankGl);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId)
    {
        if (db.Tenants.Local.Any(t => t.Id == tenantId) || db.Tenants.Any(t => t.Id == tenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}"[..20],
            Code = tenantId.ToString("N")[..6].ToUpperInvariant(),
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static void SeedPeriod(ApplicationDbContext db, Guid tenantId, string periodStatus)
    {
        if (db.FiscalPeriods.Local.Any(p => p.TenantId == tenantId && p.PeriodCode == "2026-07") ||
            db.FiscalPeriods.Any(p => p.TenantId == tenantId && p.PeriodCode == "2026-07"))
        {
            return;
        }

        var isOpen = string.Equals(periodStatus, "Open", StringComparison.OrdinalIgnoreCase);
        db.FiscalPeriods.Add(new FiscalPeriod
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
            PeriodStatus = periodStatus,
            IsOpen = isOpen,
            IsClosed = !isOpen,
            IsLocked = !isOpen
        });
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType)
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

    private sealed record BankSetup(BankAccount BankAccount, Account BankGlAccount);

    private sealed record CashFixture(
        CashTransaction Transaction,
        BankAccount BankAccount,
        Account BankGlAccount,
        Account OffsetAccount);
}
