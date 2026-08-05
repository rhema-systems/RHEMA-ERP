using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
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

public sealed class CashBankWorkflowApprovalHardeningTests
{
    [Theory]
    [InlineData(CashTransactionApprovalStatus.Captured, "submitted and approved")]
    [InlineData(CashTransactionApprovalStatus.Submitted, "submitted but not approved")]
    [InlineData(CashTransactionApprovalStatus.Rejected, "Rejected cash/bank transaction cannot be posted")]
    [InlineData(CashTransactionApprovalStatus.Returned, "Returned cash/bank transaction cannot be posted")]
    [InlineData(CashTransactionApprovalStatus.Cancelled, "Cancelled cash/bank transaction cannot be posted")]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task CashBankTransaction_ShouldNotPostUntilApproved(CashTransactionApprovalStatus status, string expectedMessage)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Receipt, status);
        var service = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{expectedMessage}*");
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.CashBankTransactionPostingBlockedApprovalMissing)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task Submit_ShouldUseWorkflowEngineAndAuditSubmission()
    {
        var tenantId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment, CashTransactionApprovalStatus.Captured);
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(x => x.SubmitAsync("CashTransaction", fixture.Transaction.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = workflowInstanceId
                },
                WorkflowOutcome.Pending));
        var service = CreateService(db, tenantId, workflow.Object);

        var result = await service.SubmitAsync(fixture.Transaction.Id);

        result.ApprovalStatus.Should().Be(CashTransactionApprovalStatus.Submitted);
        result.WorkflowInstanceId.Should().Be(workflowInstanceId);
        workflow.Verify(x => x.SubmitAsync("CashTransaction", fixture.Transaction.Id), Times.Once);
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.CashBankTransactionSubmitted)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task Approve_ShouldRejectUnauthorizedApprover()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment, CashTransactionApprovalStatus.Submitted);
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(x => x.CanUserApproveAsync("CashTransaction", fixture.Transaction.Id, It.IsAny<Guid>()))
            .ReturnsAsync(false);
        var service = CreateService(db, tenantId, workflow.Object);

        var act = () => service.ApproveAsync(fixture.Transaction.Id, "ok");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("The current user is not authorized to approve this cash/bank workflow step.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task ApprovedCashBankTransaction_ShouldPostThroughFinancePostingEngineAndAuditApprovedPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment, CashTransactionApprovalStatus.Submitted);
        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(x => x.CanUserApproveAsync("CashTransaction", fixture.Transaction.Id, It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(x => x.ProcessApprovalAsync("CashTransaction", fixture.Transaction.Id, It.IsAny<Guid>(), "Approve", "approved"))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed },
                WorkflowOutcome.Approved));
        var service = CreateService(db, tenantId, workflow.Object);

        var approved = await service.ApproveAsync(fixture.Transaction.Id, "approved");
        var posted = await service.PostAsync(fixture.Transaction.Id);

        approved.ApprovalStatus.Should().Be(CashTransactionApprovalStatus.Approved);
        posted.ApprovalStatus.Should().Be(CashTransactionApprovalStatus.Posted);
        posted.JournalEntryId.Should().NotBeNull();
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.CashBankTransactionApproved)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a =>
            a.TenantId == tenantId &&
            a.Action == FinanceAuditEvents.CashBankTransactionPostedAfterApproval)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task CrossTenantWorkflowSubmit_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, otherTenantId, CashTransactionType.Receipt, CashTransactionApprovalStatus.Captured);
        var service = CreateService(db, tenantId);

        var act = () => service.SubmitAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash/bank transaction was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task PostedCashBankTransaction_ShouldNotBeDeletedByMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedCashTransactionAsync(db, tenantId, CashTransactionType.Payment, CashTransactionApprovalStatus.Approved);
        var service = CreateService(db, tenantId);
        await service.PostAsync(fixture.Transaction.Id);

        var act = () => service.DeleteAsync(fixture.Transaction.Id);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Cannot delete posted transaction");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task BankTransferCreate_ShouldCaptureWithoutImmediatePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var fromBankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var toBankGl = SeedAccount(db, tenantId, "1010", AccountType.Asset);
        var fromBank = SeedBankAccount(db, tenantId, "BANK-001", fromBankGl.Id);
        var toBank = SeedBankAccount(db, tenantId, "BANK-002", toBankGl.Id);
        await db.SaveChangesAsync();
        var bankAccountService = new Mock<IBankAccountService>();
        var service = CreateService(db, tenantId, bankAccountService: bankAccountService.Object);

        var (fromTransaction, toTransaction) = await service.CreateTransferAsync(new CreateBankTransferDto
        {
            FromBankAccountId = fromBank.Id,
            ToBankAccountId = toBank.Id,
            TransactionDate = new DateTime(2026, 7, 5),
            Amount = 250m,
            Description = "Control transfer"
        });

        fromTransaction.ApprovalStatus.Should().Be(CashTransactionApprovalStatus.Captured);
        toTransaction.ApprovalStatus.Should().Be(CashTransactionApprovalStatus.Captured);
        fromTransaction.IsPosted.Should().BeFalse();
        toTransaction.IsPosted.Should().BeFalse();
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CashBankTransfer")).Should().Be(0);
        bankAccountService.Verify(x => x.UpdateBalanceAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashBankWorkflow")]
    [Trait("Category", "CashBank")]
    public async Task ApprovedBankTransfer_ShouldPostAndAdjustOperationalBalancesOnce()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedBankTransferAsync(db, tenantId, CashTransactionApprovalStatus.Approved);
        fixture.FromBankAccount.CurrentBalance = 500m;
        fixture.FromBankAccount.AvailableBalance = 500m;
        fixture.ToBankAccount.CurrentBalance = 50m;
        fixture.ToBankAccount.AvailableBalance = 50m;
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        await service.PostAsync(fixture.FromTransaction.Id);
        await service.PostAsync(fixture.FromTransaction.Id);

        var fromBank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.FromBankAccount.Id);
        var toBank = await db.BankAccounts.SingleAsync(b => b.Id == fixture.ToBankAccount.Id);
        fromBank.CurrentBalance.Should().Be(400m);
        fromBank.AvailableBalance.Should().Be(400m);
        toBank.CurrentBalance.Should().Be(150m);
        toBank.AvailableBalance.Should().Be(150m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cash-bank-workflow-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CashTransactionService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IWorkflowIntegrationService? workflowIntegrationService = null,
        IBankAccountService? bankAccountService = null)
    {
        var currentUser = CreateCurrentUserService(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-cash-bank-workflow" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
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
            .ReturnsAsync("TRF-202607-0001");
        var accessScope = CreateUnrestrictedFinanceAccessScope();

        return new CashTransactionService(
            db,
            bankAccountService ?? Mock.Of<IBankAccountService>(),
            tenantSettings.Object,
            documentNumbering.Object,
            currentUser.Object,
            accessScope.Object,
            new FinanceReversalPolicyService(db, currentUser.Object),
            postingEngine,
            auditService,
            workflowIntegrationService);
    }

    private static Mock<IFinanceAccessScopeService> CreateUnrestrictedFinanceAccessScope()
    {
        var scope = new Mock<IFinanceAccessScopeService>();
        scope.Setup(x => x.GetPermittedBankAccountIdsAsync(
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid>?)null);
        return scope;
    }

    private static Mock<ICurrentUserService> CreateCurrentUserService(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("cash.bank.workflow");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("cash-bank-workflow-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<CashBankFixture> SeedCashTransactionAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CashTransactionType transactionType,
        CashTransactionApprovalStatus approvalStatus)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var bankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var offset = SeedAccount(db, tenantId, transactionType == CashTransactionType.Receipt ? "4100" : "6100",
            transactionType == CashTransactionType.Receipt ? AccountType.Revenue : AccountType.Expense);
        var bankAccount = SeedBankAccount(db, tenantId, "BANK-001", bankGl.Id);
        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = transactionType == CashTransactionType.Receipt ? "RCT-202607-9001" : "CPY-202607-9001",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = transactionType,
            BankAccountId = bankAccount.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            GLAccountId = offset.Id,
            Description = "Workflow cash/bank transaction",
            IsPosted = false,
            ApprovalStatus = approvalStatus,
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().Add(transaction);
        await db.SaveChangesAsync();
        return new CashBankFixture(transaction, bankAccount, bankGl, offset);
    }

    private static async Task<CashBankTransferFixture> SeedBankTransferAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CashTransactionApprovalStatus approvalStatus)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId);
        var fromBankGl = SeedAccount(db, tenantId, "1000", AccountType.Asset);
        var toBankGl = SeedAccount(db, tenantId, "1010", AccountType.Asset);
        var fromBank = SeedBankAccount(db, tenantId, "BANK-001", fromBankGl.Id);
        var toBank = SeedBankAccount(db, tenantId, "BANK-002", toBankGl.Id);
        var fromTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-9001-OUT",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = fromBank.Id,
            ToBankAccountId = toBank.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            Description = "Approved transfer",
            ApprovalStatus = approvalStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        var toTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "TRF-202607-9001-IN",
            TransactionDate = new DateTime(2026, 7, 5),
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = toBank.Id,
            ToBankAccountId = fromBank.Id,
            Amount = 100m,
            Currency = "GHS",
            ExchangeRate = 1m,
            BaseAmount = 100m,
            Description = "Approved transfer",
            ApprovalStatus = approvalStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        db.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await db.SaveChangesAsync();
        return new CashBankTransferFixture(fromTransaction, toTransaction, fromBank, toBank, fromBankGl, toBankGl);
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        if (db.Tenants.Any(t => t.Id == tenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static FiscalPeriod SeedOpenPeriod(ApplicationDbContext db, Guid tenantId)
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
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        return period;
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

    private static BankAccount SeedBankAccount(ApplicationDbContext db, Guid tenantId, string accountNumber, Guid glAccountId)
    {
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = accountNumber,
            AccountName = $"Bank {accountNumber}",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccountId,
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
}
