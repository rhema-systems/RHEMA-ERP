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
using ErpSystem.Core.Interfaces.Inventory;
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

public sealed class ArInvoicePostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task SentArInvoice_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostArInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AR" &&
            e.SourceDocumentType == "CustomerInvoice" &&
            e.SourceDocumentId == fixture.Invoice.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AR");
        journal.SourceDocumentType.Should().Be("CustomerInvoice");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.ArAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.RevenueAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArInvoicePosted && a.TenantId == tenantId)).Should().Be(1);
        fixture.ArAccount.Balance.Should().Be(0m);
        fixture.RevenueAccount.Balance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DraftArInvoice_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice => invoice.Status = InvoiceStatus.Draft);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only sent AR invoices can be posted.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task UnbalancedArInvoice_ShouldFailBeforeLedgerPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.TotalAmount = 125m;
            invoice.BaseCurrencyAmount = 125m;
        });
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR invoice amount does not match posting line totals.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArInvoicePostingFailed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantCustomer_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherCustomer = SeedCustomer(db, otherTenantId, fixture.ArAccount.Id);
        fixture.Invoice.BusinessPartnerId = otherCustomer.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR invoice customer was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantRevenueAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherRevenue = SeedAccount(db, otherTenantId, "4000", AccountType.Revenue);
        fixture.Invoice.LineItems.Single().GLAccountId = otherRevenue.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR posting revenue account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task CrossTenantArControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherArAccount = SeedAccount(db, otherTenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        fixture.Customer.DefaultArAccountId = otherArAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR posting AR control account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task NonPostableRevenueAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        fixture.RevenueAccount.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AR posting revenue account account '4000' does not allow direct posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Invoice.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task DuplicateArInvoicePosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Invoice.Id);
        var second = await service.PostAsync(fixture.Invoice.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ArInvoiceDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ARInvoicePosting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task PostedArInvoice_ShouldNotBeEditedOrDeleted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedSentArInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Invoice.Id);

        var update = () => service.UpdateAsync(new InvoiceUpdateDto
        {
            Id = fixture.Invoice.Id,
            InvoiceDate = fixture.Invoice.InvoiceDate,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            LineItems = new List<InvoiceLineItemUpdateDto>()
        });
        var delete = () => service.DeleteAsync(fixture.Invoice.Id);

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer invoices cannot be updated. Use a reversal, credit note, or adjustment.");
        await delete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted customer invoices cannot be deleted. Use a reversal, credit note, or adjustment.");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ar-invoice-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (InvoiceService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ar-invoice-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();

        var service = new InvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<ITaxCalculationEngine>(),
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<InvoiceService>>(),
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
        currentUser.SetupGet(x => x.UserName).Returns("ar.invoice.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ar-invoice-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ArInvoiceFixture> SeedSentArInvoiceAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<Invoice>? configureInvoice = null,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var arAccount = SeedAccount(db, tenantId, "1200", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var revenueAccount = SeedAccount(db, tenantId, "4000", AccountType.Revenue);
        var taxAccount = SeedAccount(db, tenantId, "2200", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "5200", AccountType.Expense);
        var customer = SeedCustomer(db, tenantId, arAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountArId = arAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountAllowedAccountId = discountAccount.Id
        });

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "INV-2026-00001",
            BusinessPartnerId = customer.Id,
            CustomerName = customer.PartnerName,
            CustomerAddress = customer.PhysicalAddress,
            InvoiceDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            SubTotal = 100m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100m,
            PaidAmount = 0m,
            CreditedAmount = 0m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 100m,
            PaymentTermsDays = 30,
            Status = InvoiceStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

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
            TaxRate = 0m,
            TaxAmount = 0m,
            DiscountPercentage = 0m,
            DiscountAmount = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        configureInvoice?.Invoke(invoice);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return new ArInvoiceFixture(invoice, customer, revenueAccount, arAccount, taxAccount, discountAccount);
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

    private sealed record ArInvoiceFixture(
        Invoice Invoice,
        BusinessPartner Customer,
        Account RevenueAccount,
        Account ArAccount,
        Account TaxAccount,
        Account DiscountAccount);
}
