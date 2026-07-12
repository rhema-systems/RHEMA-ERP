using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.AR;
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

#pragma warning disable CS0618 // Regression tests intentionally assert that obsolete legacy posting paths are not used.

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ArReceiptPostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ApprovedArReceipt_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostArPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AR" &&
            e.SourceDocumentType == "CustomerPayment" &&
            e.SourceDocumentId == fixture.Payment.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AR");
        journal.SourceDocumentType.Should().Be("CustomerPayment");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArReceiptPosted && a.TenantId == tenantId)).Should().Be(1);
        // The posting engine keeps Account.Balance as a read-side snapshot for legacy balance APIs.
        fixture.ArAccount.Balance.Should().Be(-100m);
        fixture.BankGlAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task UnapprovedArReceipt_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, payment => payment.Status = "PendingApproval");
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt workflow approval is not complete.");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CustomerPayment")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ReceiptAgainstUnpostedArInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, configureInvoice: invoice => invoice.JournalEntryId = null, seedInvoicePostingEvent: false);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR receipt cannot settle unposted invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task OverSettledArInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, payment => payment.TotalAmount = 125m, allocationAmount: 125m);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AR receipt would over-settle invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantCustomer_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherCustomer = SeedCustomer(db, otherTenantId, otherArAccount.Id);
        fixture.Payment.CustomerId = otherCustomer.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt customer was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantInvoiceSettlement_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var otherCustomer = SeedCustomer(db, otherTenantId, otherArAccount.Id);
        var otherInvoice = SeedPostedInvoice(db, otherTenantId, otherCustomer, otherArAccount, "INV-OTHER", new DateTime(2026, 7, 5), 100m);
        fixture.Allocation.InvoiceId = otherInvoice.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt allocation references an invoice from another tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantArControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        fixture.Customer.DefaultArAccountId = otherArAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt posting AR control account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantBankAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherBankGl = SeedAccount(db, otherTenantId, "1100", AccountType.Asset);
        var otherBank = SeedBankAccount(db, otherTenantId, otherBankGl.Id);
        fixture.Payment.BankAccountId = otherBank.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt bank account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task InactiveBankGlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        fixture.BankGlAccount.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR receipt posting bank/cash account account '1100' is not active.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Payment.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DuplicateArReceiptPosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Payment.Id);
        var second = await service.PostAsync(fixture.Payment.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "CustomerPayment")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "CustomerPayment")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArReceiptDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARReceiptPosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedArReceipt_ShouldNotBeMutated()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedArReceiptAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);

        var update = () => service.UpdateAsync(new PaymentUpdateDto
        {
            Id = fixture.Payment.Id,
            PaymentDate = fixture.Payment.PaymentDate,
            TotalAmount = fixture.Payment.TotalAmount,
            PaymentMethod = fixture.Payment.PaymentMethod
        });
        var allocate = () => service.AllocatePaymentAsync(new PaymentAllocation_CreateDto
        {
            CustomerPaymentId = fixture.Payment.Id,
            Allocations = new List<InvoiceAllocationDto>
            {
                new() { InvoiceId = fixture.Invoice.Id, AllocatedAmount = 1m }
            }
        });
        var reverse = () => service.ReverseAllocationAsync(fixture.Allocation.Id, "test reversal");
        var clear = () => service.ClearPaymentAsync(fixture.Payment.Id, fixture.Payment.PaymentDate);
        var bounce = () => service.BouncedPaymentAsync(fixture.Payment.Id, "test bounce");

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be updated. Use a reversal, void, or adjustment workflow.");
        await allocate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be allocated. Use a reversal, void, or adjustment workflow.");
        await reverse.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");
        await clear.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be cleared by mutation until bank reconciliation integration is migrated to the posting engine.");
        await bounce.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer payments cannot be bounced by mutation until AR receipt reversal posting is implemented.");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-receipt-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (PaymentService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ar-receipt-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        var service = new PaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<PaymentService>>(),
            Mock.Of<IDocumentNumberingService>(),
            postingEngine,
            auditService);

        return (service, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ar.receipt.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ar-receipt-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ArReceiptFixture> SeedApprovedArReceiptAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<CustomerPayment>? configurePayment = null,
        Action<Invoice>? configureInvoice = null,
        decimal allocationAmount = 100m,
        bool seedInvoicePostingEvent = true,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var bankGlAccount = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var taxAccount = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "5200", AccountType.Expense);
        var customer = SeedCustomer(db, tenantId, arAccount.Id);
        var bankAccount = SeedBankAccount(db, tenantId, bankGlAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountAllowedAccountId = discountAccount.Id,
            DefaultBankAccountId = bankAccount.Id
        });

        var invoice = SeedPostedInvoice(db, tenantId, customer, arAccount, "INV-2026-00001", new DateTime(2026, 7, 5), 100m, period.Id, seedInvoicePostingEvent);
        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            LineItemType = LineItemType.GLAccount,
            GLAccountId = revenueAccount.Id,
            Description = "Consulting services",
            Quantity = 1m,
            UnitPrice = 100m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        configureInvoice?.Invoke(invoice);

        var payment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "CP-2026-00001",
            CustomerId = customer.Id,
            PaymentDate = new DateTime(2026, 7, 5),
            TotalAmount = allocationAmount,
            AllocatedAmount = allocationAmount,
            PaymentMethod = "BankTransfer",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        configurePayment?.Invoke(payment);

        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPaymentId = payment.Id,
            InvoiceId = invoice.Id,
            AllocatedAmount = allocationAmount,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.PaidAmount = allocationAmount;
        invoice.Status = allocationAmount >= invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        db.Set<CustomerPayment>().Add(payment);
        db.Set<PaymentAllocation>().Add(allocation);
        await db.SaveChangesAsync();

        return new ArReceiptFixture(payment, allocation, invoice, customer, arAccount, bankGlAccount, bankAccount);
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
        return account;
    }

    private static BusinessPartner SeedCustomer(ApplicationDbContext db, Guid tenantId, Guid arAccountId)
    {
        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = $"CUS-{tenantId.ToString("N")[..6]}",
            PartnerName = "Test Customer",
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

        db.Set<BusinessPartner>().Add(customer);
        return customer;
    }

    private static BankAccount SeedBankAccount(ApplicationDbContext db, Guid tenantId, Guid glAccountId)
    {
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = $"BANK-{tenantId.ToString("N")[..6]}",
            AccountName = "Operating Bank",
            BankName = "Test Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = glAccountId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.BankAccounts.Add(bankAccount);
        return bankAccount;
    }

    private static Invoice SeedPostedInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        BusinessPartner customer,
        Account arAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid? fiscalPeriodId = null,
        bool seedPostingEvent = true)
    {
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
            FiscalPeriodId = fiscalPeriodId ?? Guid.NewGuid(),
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

    private sealed record ArReceiptFixture(
        CustomerPayment Payment,
        PaymentAllocation Allocation,
        Invoice Invoice,
        BusinessPartner Customer,
        Account ArAccount,
        Account BankGlAccount,
        BankAccount BankAccount);
}
