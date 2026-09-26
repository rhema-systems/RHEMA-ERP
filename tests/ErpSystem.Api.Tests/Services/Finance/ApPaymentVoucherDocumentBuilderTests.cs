using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApPaymentVoucherDocumentBuilderTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ApPaymentVoucher")]
    public async Task Render_ShouldProduceTenantScopedAuditedPdfFromCanonicalPayment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var payment = SeedPayment(db, tenantId, VendorPaymentStatus.Processed);
        await db.SaveChangesAsync();
        var accessScope = PermittedAccessScope();
        var audit = new CapturingFinanceAuditService();
        var builder = new ApPaymentVoucherDocumentBuilder(
            db,
            CurrentUser(tenantId, "finance.officer").Object,
            accessScope.Object,
            audit);

        var result = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceApPaymentVoucher,
            EntityId = payment.Id,
            Format = "pdf",
            CopyType = "Original"
        });

        result.Content.Should().StartWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        result.FileName.Should().Be("AP-PAY-2026-0001-original.pdf");
        result.DocumentType.Should().Be(DocumentTypes.FinanceApPaymentVoucher);
        audit.Events.Should().ContainSingle(item =>
            item.EventType == FinanceAuditEvents.ApPaymentVoucherGenerated &&
            item.TenantId == tenantId &&
            item.SourceDocumentId == payment.Id &&
            item.Resource == "Finance.APPaymentVoucher");
        accessScope.Verify(service => service.EnsureBankAccountAccessAsync(
            payment.BankAccountId,
            FinanceAccessLevel.Read,
            It.IsAny<CancellationToken>()), Times.Once);

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_AP_VOUCHER_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            // Opt-in output supports the PDF skill's render-and-inspect gate while keeping normal
            // test runs free of working-tree artifacts.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApPaymentVoucher")]
    public async Task Render_ShouldRejectDraftBecausePdfMustNotImplyPaymentAuthority()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var payment = SeedPayment(db, tenantId, VendorPaymentStatus.Draft);
        await db.SaveChangesAsync();
        var builder = new ApPaymentVoucherDocumentBuilder(
            db,
            CurrentUser(tenantId, "payment.preparer").Object,
            PermittedAccessScope().Object);

        var action = () => builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceApPaymentVoucher,
            EntityId = payment.Id,
            Format = "pdf"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be printed*Draft*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApPaymentVoucher")]
    public async Task Render_ShouldConcealCrossTenantPaymentEvenWhenIdentifierIsKnown()
    {
        var ownerTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var payment = SeedPayment(db, ownerTenantId, VendorPaymentStatus.Processed);
        await db.SaveChangesAsync();
        var builder = new ApPaymentVoucherDocumentBuilder(
            db,
            CurrentUser(Guid.NewGuid(), "cross.tenant").Object,
            PermittedAccessScope().Object);

        var action = () => builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceApPaymentVoucher,
            EntityId = payment.Id,
            Format = "pdf"
        });

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApPaymentVoucher")]
    public async Task Render_ShouldEnforceBankScopeBeforeDisclosingVoucher()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var payment = SeedPayment(db, tenantId, VendorPaymentStatus.Processed);
        await db.SaveChangesAsync();
        var accessScope = new Mock<IFinanceAccessScopeService>();
        accessScope.Setup(service => service.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Read,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Payment bank account is outside the current Finance data scope."));
        var builder = new ApPaymentVoucherDocumentBuilder(
            db,
            CurrentUser(tenantId, "restricted.user").Object,
            accessScope.Object);

        var action = () => builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceApPaymentVoucher,
            EntityId = payment.Id,
            Format = "pdf"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*outside the current Finance data scope*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApPaymentVoucher")]
    public void PaymentVoucherDocumentType_ShouldUseFinanceReportExportPolicy()
    {
        DocumentsController.FinanceDocumentPolicies[DocumentTypes.FinanceApPaymentVoucher]
            .Should().Be(FinancePermissions.ExportFinanceReports);
    }

    private static VendorPayment SeedPayment(
        ApplicationDbContext db,
        Guid tenantId,
        VendorPaymentStatus status)
    {
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            BaseCurrency = "GHS",
            Address = "Tema, Ghana"
        };
        var supplier = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-001",
            PartnerName = "TDC Works Supplier",
            PartnerType = "Supplier",
            TaxIdentificationNumber = "TIN-001",
            IsActive = true
        };
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = "0123456789",
            AccountName = "TDC Operating Account",
            BankName = "TDC Test Bank",
            Currency = "GHS"
        };
        var authorizedById = Guid.NewGuid();
        var authorizedBy = new ApplicationUser
        {
            Id = authorizedById,
            TenantId = tenantId,
            UserName = "chief.accountant",
            NormalizedUserName = "CHIEF.ACCOUNTANT",
            FirstName = "Chief",
            LastName = "Accountant"
        };
        var payment = new VendorPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "AP-PAY-2026-0001",
            BusinessPartnerId = supplier.Id,
            BusinessPartner = supplier,
            BusinessPartnerCode = supplier.PartnerCode,
            BusinessPartnerName = supplier.PartnerName,
            BusinessPartnerTaxIdentificationNumber = supplier.TaxIdentificationNumber,
            PaymentDate = new DateTime(2026, 8, 3),
            TotalAmount = 12_345.67m,
            AllocatedAmount = 0m,
            IsSupplierAdvance = true,
            PaymentMethod = VendorPaymentMethod.BankTransfer,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bankAccount.Id,
            BankAccount = bankAccount,
            TransactionReference = "TDC-PAY-REF-001",
            WithholdingTaxRate = 3m,
            WithholdingTaxAmount = 370.37m,
            Status = status,
            AuthorizedById = status == VendorPaymentStatus.Draft ? null : authorizedById,
            AuthorizedDate = status == VendorPaymentStatus.Draft ? null : new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc),
            CreatedBy = "payment.preparer"
        };

        if (status != VendorPaymentStatus.Draft)
        {
            var fiscalPeriodId = Guid.NewGuid();
            var apAccount = Account(tenantId, "2100", "Accounts Payable Control", AccountType.Liability);
            var bankGlAccount = Account(tenantId, "1100", "Operating Bank", AccountType.Asset);
            var whtAccount = Account(tenantId, "2150", "WHT Payable", AccountType.Liability);
            var journal = new JournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryNumber = "JE-2026-00803",
                JournalType = "System Generated",
                EntryDate = payment.PaymentDate,
                Description = $"Vendor payment {payment.PaymentNumber}",
                ReferenceNumber = payment.PaymentNumber,
                SourceModule = "AP",
                SourceDocumentId = payment.Id,
                SourceDocumentType = "VendorPayment",
                TotalDebitAmount = 12_716.04m,
                TotalCreditAmount = 12_716.04m,
                IsBalanced = true,
                BookClassification = "IFRS",
                FiscalPeriodId = fiscalPeriodId,
                PostingDate = payment.PaymentDate,
                PostingStatus = "Posted"
            };
            journal.Transactions.Add(Transaction(
                tenantId, journal, apAccount, fiscalPeriodId, 1,
                "Settle supplier liability", debit: 12_716.04m, credit: 0m));
            journal.Transactions.Add(Transaction(
                tenantId, journal, bankGlAccount, fiscalPeriodId, 2,
                "Release supplier payment", debit: 0m, credit: 12_345.67m));
            journal.Transactions.Add(Transaction(
                tenantId, journal, whtAccount, fiscalPeriodId, 3,
                "Recognize WHT payable", debit: 0m, credit: 370.37m));

            payment.JournalEntryId = journal.Id;
            db.Accounts.AddRange(apAccount, bankGlAccount, whtAccount);
            db.JournalEntries.Add(journal);
            db.Users.Add(authorizedBy);
        }

        db.Tenants.Add(tenant);
        db.Set<BusinessPartner>().Add(supplier);
        db.BankAccounts.Add(bankAccount);
        db.Set<VendorPayment>().Add(payment);
        return payment;
    }

    private static Account Account(Guid tenantId, string number, string name, AccountType type)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = type,
            CurrencyCode = "GHS",
            IsSegmented = false
        };

    private static AccountTransaction Transaction(
        Guid tenantId,
        JournalEntry journal,
        Account account,
        Guid fiscalPeriodId,
        int lineNumber,
        string description,
        decimal debit,
        decimal credit)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = journal.Id,
            JournalEntry = journal,
            AccountId = account.Id,
            Account = account,
            TransactionDate = journal.EntryDate,
            Description = description,
            DebitAmount = debit,
            CreditAmount = credit,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrency = "GHS",
            FiscalPeriodId = fiscalPeriodId,
            PostedDate = journal.PostingDate,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            LineNumber = lineNumber
        };

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId, string userName)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserName).Returns(userName);
        return currentUser;
    }

    private static Mock<IFinanceAccessScopeService> PermittedAccessScope()
    {
        var accessScope = new Mock<IFinanceAccessScopeService>();
        accessScope.Setup(service => service.EnsureBankAccountAccessAsync(
                It.IsAny<Guid?>(),
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return accessScope;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ap-payment-voucher-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public Task<AuditLog> RecordAsync(
            FinanceAuditEventDto auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog { Id = Guid.NewGuid() });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}
