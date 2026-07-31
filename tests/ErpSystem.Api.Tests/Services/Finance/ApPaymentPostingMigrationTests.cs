using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
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

public sealed class ApPaymentPostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApprovedApPayment_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Payment.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostApPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AP" &&
            e.SourceDocumentType == "VendorPayment" &&
            e.SourceDocumentId == fixture.Payment.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AP");
        journal.SourceDocumentType.Should().Be("VendorPayment");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.BankGlAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApPaymentPosted && a.TenantId == tenantId)).Should().Be(1);
        // The posting engine keeps Account.Balance as a read-side snapshot for legacy balance APIs.
        fixture.ApAccount.Balance.Should().Be(-100m);
        fixture.BankGlAccount.Balance.Should().Be(-100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task SupplierAdvance_ShouldPostAndApplyThroughFinancePostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var supplierAdvanceAccount = SeedAccount(db, tenantId, "1500", AccountType.Asset);
        var settings = await db.Set<FinanceSettings>().SingleAsync(s => s.TenantId == tenantId);
        settings.SupplierAdvanceAccountId = supplierAdvanceAccount.Id;
        db.Remove(fixture.Allocation);
        fixture.Payment.AllocatedAmount = 0m;
        fixture.Invoice.PaidAmount = 0m;
        fixture.Invoice.Status = VendorInvoiceStatus.Approved;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var initialPosting = await service.PostAsync(fixture.Payment.Id);
        var application = await service.AllocatePaymentAsync(fixture.Payment.Id, new List<VendorPaymentAllocationCreateDto>
        {
            new() { VendorInvoiceId = fixture.Invoice.Id, AllocatedAmount = 100m }
        });

        (await db.Set<VendorPayment>().SingleAsync(p => p.Id == fixture.Payment.Id)).IsSupplierAdvance.Should().BeTrue();
        (await db.JournalEntries.SingleAsync(j => j.Id == initialPosting.JournalEntryId)).SourceDocumentType.Should().Be("VendorPayment");
        var allocation = await db.Set<VendorPaymentAllocation>().SingleAsync(a => a.Id == application.Allocations.Single().Id);
        allocation.ApplicationJournalEntryId.Should().NotBeNull();
        allocation.ApplicationPostingEventId.Should().NotBeNull();
        var applicationJournal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == allocation.ApplicationJournalEntryId);
        applicationJournal.SourceDocumentType.Should().Be("VendorPaymentAdvanceApplication");
        applicationJournal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).DebitAmount.Should().Be(100m);
        applicationJournal.Transactions.Single(t => t.AccountId == supplierAdvanceAccount.Id).CreditAmount.Should().Be(100m);
        subledgerPostingMock.Verify(x => x.PostApPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task UnapprovedApPayment_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, payment => payment.Status = VendorPaymentStatus.PendingAuthorization);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment workflow approval is not complete.");
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "VendorPayment")).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PaymentAgainstUnpostedApInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, configureInvoice: invoice => invoice.JournalEntryId = null, seedInvoicePostingEvent: false);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AP payment cannot settle unposted invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task OverSettledApInvoice_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, payment => payment.TotalAmount = 125m, allocationAmount: 125m);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"AP payment would over-settle invoice '{fixture.Invoice.InvoiceNumber}'.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantSupplier_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherSupplier = SeedSupplier(db, otherTenantId, fixture.ApAccount.Id);
        fixture.Payment.SupplierId = otherSupplier.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment supplier was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantInvoiceSettlement_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherInvoice = SeedPostedInvoice(db, otherTenantId, fixture.Supplier, fixture.ApAccount, "VI-OTHER", new DateTime(2026, 7, 5), 100m);
        fixture.Allocation.VendorInvoiceId = otherInvoice.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Vendor invoice with Id '{otherInvoice.Id}' not found.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantApControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherApAccount = SeedAccount(db, otherTenantId, "2000", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        fixture.Supplier.DefaultApAccountId = otherApAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment posting AP control account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantBankAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherBankGl = SeedAccount(db, otherTenantId, "1100", AccountType.Asset);
        var otherBank = SeedBankAccount(db, otherTenantId, otherBankGl.Id);
        fixture.Payment.BankAccountId = otherBank.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment bank account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task InactiveBankGlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        fixture.BankGlAccount.Status = AccountStatus.Inactive;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP payment posting bank/cash account account '1100' is not active.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Payment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Payment.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task DuplicateApPaymentPosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Payment.Id);
        var second = await service.PostAsync(fixture.Payment.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync(j => j.SourceDocumentType == "VendorPayment")).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentType == "VendorPayment")).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApPaymentDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPaymentPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PostedApPayment_ShouldUseBalancedControlledVoidInsteadOfMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApPaymentAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Payment.Id);

        var allocate = () => service.AllocatePaymentAsync(fixture.Payment.Id, new List<VendorPaymentAllocationCreateDto>
        {
            new() { VendorInvoiceId = fixture.Invoice.Id, AllocatedAmount = 1m }
        });
        var reverse = () => service.ReverseAllocationAsync(fixture.Allocation.Id, "test reversal");

        await allocate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted vendor payments cannot be allocated. Use a reversal, void, or adjustment workflow.");
        await reverse.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted vendor payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");

        var first = await service.VoidPaymentAsync(fixture.Payment.Id, "test void");
        var second = await service.VoidPaymentAsync(fixture.Payment.Id, "idempotent retry");

        first.Status.Should().Be(VendorPaymentStatus.Voided);
        second.Status.Should().Be(VendorPaymentStatus.Voided);
        var original = await db.JournalEntries
            .Include(item => item.ReversalJournalEntry)
                .ThenInclude(item => item!.Transactions)
            .SingleAsync(item => item.Id == first.JournalEntryId);
        original.IsReversed.Should().BeTrue();
        original.ReversalJournalEntryId.Should().NotBeNull();
        original.ReversalJournalEntry!.IsBalanced.Should().BeTrue();
        original.ReversalJournalEntry.TotalDebitAmount.Should().Be(original.TotalCreditAmount);
        original.ReversalJournalEntry.TotalCreditAmount.Should().Be(original.TotalDebitAmount);
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorPayment" && item.PostingAction == "Reverse")).Should().Be(1);
        (await db.Set<VendorPaymentAllocation>().CountAsync(item =>
            item.IsReversal && item.OriginalAllocationId == fixture.Allocation.Id)).Should().Be(1);
        var restoredInvoice = await db.Set<VendorInvoice>().SingleAsync(item => item.Id == fixture.Invoice.Id);
        restoredInvoice.PaidAmount.Should().Be(0m);
        restoredInvoice.Status.Should().Be(VendorInvoiceStatus.Approved);
        (await db.AuditLogs.CountAsync(item => item.Action == FinanceAuditEvents.ApPaymentReversed)).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ap-payment-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (VendorPaymentService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ap-payment-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var invoicePaymentSod = new Mock<IProcurementInvoicePaymentSodService>();
        invoicePaymentSod
            .Setup(x => x.RevalidatePaymentAuthorizationAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var procurementControlEvents = new Mock<IProcurementControlEventService>();
        procurementControlEvents
            .Setup(x => x.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
                new ProcurementControlEventDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EventKey = request.EventKey,
                    EventType = request.EventType,
                    Action = request.Action,
                    Result = request.Result,
                    SourceType = request.SourceType,
                    SourceId = request.SourceId,
                    SourceReference = request.SourceReference ?? string.Empty,
                    CorrelationId = request.CorrelationId,
                    OccurredAtUtc = request.OccurredAtUtc == default
                        ? DateTime.UtcNow
                        : request.OccurredAtUtc,
                    RecordedAtUtc = DateTime.UtcNow,
                    IntegrityValid = true
                });

        var service = new VendorPaymentService(
            new UnitOfWork(db),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<ILogger<VendorPaymentService>>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IWorkflowService>(),
            postingEngine,
            auditService,
            procurementControlEvents: procurementControlEvents.Object,
            invoicePaymentSod: invoicePaymentSod.Object);

        return (service, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ap.payment.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ap-payment-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ApPaymentFixture> SeedApprovedApPaymentAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<VendorPayment>? configurePayment = null,
        Action<VendorInvoice>? configureInvoice = null,
        decimal allocationAmount = 100m,
        bool seedInvoicePostingEvent = true,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        var period = SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var apAccount = SeedAccount(db, tenantId, "2000", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var bankGlAccount = SeedAccount(db, tenantId, "1100", AccountType.Asset);
        var expenseAccount = SeedAccount(db, tenantId, "6000", AccountType.Expense);
        var taxAccount = SeedAccount(db, tenantId, "2300", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "5100", AccountType.Revenue);
        var supplier = SeedSupplier(db, tenantId, apAccount.Id);
        var bankAccount = SeedBankAccount(db, tenantId, bankGlAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountReceivedAccountId = discountAccount.Id,
            DefaultBankAccountId = bankAccount.Id
        });

        var invoice = SeedPostedInvoice(db, tenantId, supplier, apAccount, "VI-2026-00001", new DateTime(2026, 7, 5), 100m, period.Id, seedInvoicePostingEvent);
        invoice.ExpenseAccountId = expenseAccount.Id;
        configureInvoice?.Invoke(invoice);

        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "VP-2026-00001",
            SupplierId = supplier.Id,
            PaymentDate = new DateTime(2026, 7, 5),
            TotalAmount = allocationAmount,
            AllocatedAmount = allocationAmount,
            PaymentMethod = VendorPaymentMethod.BankTransfer,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            Status = VendorPaymentStatus.Authorized,
            AuthorizedById = Guid.NewGuid(),
            AuthorizedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        configurePayment?.Invoke(payment);

        var allocation = new VendorPaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorPaymentId = payment.Id,
            VendorInvoiceId = invoice.Id,
            AllocatedAmount = allocationAmount,
            AllocationDate = payment.PaymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.PaidAmount = 0m;
        invoice.Status = VendorInvoiceStatus.Approved;

        db.Set<VendorPayment>().Add(payment);
        db.Set<VendorPaymentAllocation>().Add(allocation);
        await db.SaveChangesAsync();

        return new ApPaymentFixture(payment, allocation, invoice, supplier, apAccount, bankGlAccount, bankAccount);
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

    private static Supplier SeedSupplier(ApplicationDbContext db, Guid tenantId, Guid apAccountId)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = $"SUP-{tenantId.ToString("N")[..6]}",
            Name = "Test Supplier",
            SupplierType = "Vendor",
            IsActive = true,
            Status = "Active",
            DefaultApAccountId = apAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.Suppliers.Add(supplier);
        return supplier;
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

    private static VendorInvoice SeedPostedInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        Supplier supplier,
        Account apAccount,
        string invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        Guid? fiscalPeriodId = null,
        bool seedPostingEvent = true)
    {
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            SupplierInvoiceNumber = invoiceNumber,
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            InvoiceDate = invoiceDate,
            ReceivedDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            SubTotal = amount,
            TotalAmount = amount,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedDate = DateTime.UtcNow,
            ApAccountId = apAccount.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var invoiceJournal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = $"JE-{invoiceNumber}",
            JournalType = "AP Invoice",
            EntryDate = invoiceDate,
            Description = $"Posted invoice {invoiceNumber}",
            ReferenceNumber = invoiceNumber,
            SourceModule = "AP",
            SourceDocumentId = invoice.Id,
            SourceDocumentType = "VendorInvoice",
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

        db.VendorInvoices.Add(invoice);
        db.JournalEntries.Add(invoiceJournal);

        if (seedPostingEvent)
        {
            db.FinancePostingEvents.Add(new FinancePostingEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SourceModule = "AP",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                IdempotencyKey = $"AP:VendorInvoice:{tenantId:N}:{invoice.Id:N}:Post",
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

    private sealed record ApPaymentFixture(
        VendorPayment Payment,
        VendorPaymentAllocation Allocation,
        VendorInvoice Invoice,
        Supplier Supplier,
        Account ApAccount,
        Account BankGlAccount,
        BankAccount BankAccount);
}
