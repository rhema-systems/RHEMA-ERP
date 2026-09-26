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
using ErpSystem.Core.Models;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CashBankControlledDocumentBuilderTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ControlledCashDocuments")]
    public async Task PaymentSlip_ShouldIssueOneOriginalThenReasonBackedReplacement()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Context;
        var issueService = new FinanceControlledDocumentIssueService(
            db, fixture.CurrentUser.Object, fixture.Audit, fixture.Storage.Object);
        var builder = new CashBankPaymentSlipDocumentBuilder(
            db,
            fixture.CurrentUser.Object,
            fixture.AccessScope.Object,
            issueService);

        var original = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceCashBankPaymentSlip,
            EntityId = fixture.CashPayment.Id,
            Format = "pdf",
            CopyType = ControlledDocumentCopyTypes.Original
        });

        original.Content.Should().StartWith([0x25, 0x50, 0x44, 0x46]); // %PDF
        original.FileName.Should().Be("CP-20260803-0001-original.pdf");
        var originalIssue = await db.FinanceControlledDocumentIssues.SingleAsync();
        originalIssue.CopyNumber.Should().Be(1);
        originalIssue.CopyType.Should().Be(ControlledDocumentCopyTypes.Original);
        originalIssue.ContentSha256.Should().HaveLength(64);
        originalIssue.StoragePath.Should().NotBeNullOrWhiteSpace();
        originalIssue.FileSize.Should().Be(original.Content.LongLength);
        originalIssue.RetainUntilUtc.Should().BeAfter(DateTime.UtcNow.AddYears(6));
        (await issueService.GetRetainedAsync(originalIssue.Id)).Content.Should().Equal(original.Content);

        var duplicateOriginal = () => builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceCashBankPaymentSlip,
            EntityId = fixture.CashPayment.Id,
            Format = "pdf",
            CopyType = ControlledDocumentCopyTypes.Original
        });
        await duplicateOriginal.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*original has already been issued*");

        var replacementWithoutReason = () => builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceCashBankPaymentSlip,
            EntityId = fixture.CashPayment.Id,
            Format = "pdf",
            CopyType = ControlledDocumentCopyTypes.Replacement
        });
        await replacementWithoutReason.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least 20 characters*");

        var replacementReason = "Customer misplaced the first payment-slip copy.";
        var replacement = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceCashBankPaymentSlip,
            EntityId = fixture.CashPayment.Id,
            Format = "pdf",
            CopyType = ControlledDocumentCopyTypes.Replacement,
            Options = new Dictionary<string, string> { ["replacementReason"] = replacementReason }
        });

        replacement.Content.Should().StartWith([0x25, 0x50, 0x44, 0x46]);
        replacement.FileName.Should().Be("CP-20260803-0001-replacement-02.pdf");
        var replacementIssue = await db.FinanceControlledDocumentIssues.SingleAsync(item => item.CopyNumber == 2);
        replacementIssue.CopyType.Should().Be(ControlledDocumentCopyTypes.Replacement);
        replacementIssue.ReplacementReason.Should().Be(replacementReason);
        replacementIssue.StoragePath.Should().NotBeNullOrWhiteSpace();
        (await issueService.GetRetainedAsync(replacementIssue.Id)).Content.Should().Equal(replacement.Content);
        fixture.Audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.CashBankPaymentSlipIssued);
        fixture.Audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.CashBankPaymentSlipReplacementIssued);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ControlledCashDocuments")]
    public async Task CustomerReceipt_ShouldRenderFromPostedCanonicalArPaymentAndRetainIssue()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Context;
        var issueService = new FinanceControlledDocumentIssueService(
            db, fixture.CurrentUser.Object, fixture.Audit, fixture.Storage.Object);
        var builder = new CustomerReceiptDocumentBuilder(
            db,
            fixture.CurrentUser.Object,
            fixture.AccessScope.Object,
            issueService);

        var result = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceArCustomerReceipt,
            EntityId = fixture.CustomerPayment.Id,
            Format = "pdf",
            CopyType = ControlledDocumentCopyTypes.Original
        });

        result.Content.Should().StartWith([0x25, 0x50, 0x44, 0x46]);
        result.FileName.Should().Be("AR-PAY-2026-0001-original.pdf");
        var issue = await db.FinanceControlledDocumentIssues.SingleAsync(item =>
            item.DocumentType == DocumentTypes.FinanceArCustomerReceipt);
        issue.SourceDocumentType.Should().Be(nameof(CustomerPayment));
        issue.SourceDocumentId.Should().Be(fixture.CustomerPayment.Id);
        issue.JournalEntryId.Should().Be(fixture.CustomerPayment.JournalEntryId);
        issue.StoragePath.Should().NotBeNullOrWhiteSpace();
        issue.FileSize.Should().Be(result.Content.LongLength);
        issue.RetainUntilUtc.Should().BeAfter(DateTime.UtcNow.AddYears(6));
        (await issueService.GetRetainedAsync(issue.Id)).Content.Should().Equal(result.Content);
        fixture.Audit.Events.Should().ContainSingle(item => item.EventType == FinanceAuditEvents.CustomerReceiptIssued);
        fixture.AccessScope.Verify(service => service.EnsureBankAccountAccessAsync(
            fixture.CustomerPayment.BankAccountId,
            FinanceAccessLevel.Read,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ControlledCashDocuments")]
    public async Task Builders_ShouldRejectUnpostedOrWrongSourceRecords()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Context;
        var issueService = new FinanceControlledDocumentIssueService(
            db, fixture.CurrentUser.Object, fixture.Audit, fixture.Storage.Object);
        var paymentBuilder = new CashBankPaymentSlipDocumentBuilder(db, fixture.CurrentUser.Object, fixture.AccessScope.Object, issueService);
        var receiptBuilder = new CustomerReceiptDocumentBuilder(db, fixture.CurrentUser.Object, fixture.AccessScope.Object, issueService);

        fixture.CashPayment.TransactionType = CashTransactionType.Receipt;
        fixture.CustomerPayment.Status = "Pending";
        await db.SaveChangesAsync();

        var wrongType = () => paymentBuilder.RenderAsync(new DocumentRenderRequestDto
        {
            EntityId = fixture.CashPayment.Id,
            Format = "pdf"
        });
        await wrongType.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*type Payment*");

        var unpostedReceipt = () => receiptBuilder.RenderAsync(new DocumentRenderRequestDto
        {
            EntityId = fixture.CustomerPayment.Id,
            Format = "pdf"
        });
        await unpostedReceipt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only after*posted*");
        (await db.FinanceControlledDocumentIssues.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ControlledCashDocuments")]
    public void ControlledDocumentTypes_ShouldUseDedicatedIssueAndReplacementPolicies()
    {
        DocumentsController.FinanceDocumentPolicies[DocumentTypes.FinanceCashBankPaymentSlip]
            .Should().Be(FinancePermissions.IssueCashBankDocuments);
        DocumentsController.FinanceDocumentPolicies[DocumentTypes.FinanceArCustomerReceipt]
            .Should().Be(FinancePermissions.IssueCashBankDocuments);
        FinancePermissions.AllNames.Should().Contain(FinancePermissions.ReprintCashBankDocuments);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"controlled-cash-documents-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            BaseCurrency = "GHS",
            Address = "Tema, Ghana"
        };
        var user = new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "chief.accountant",
            NormalizedUserName = "CHIEF.ACCOUNTANT",
            FirstName = "Chief",
            LastName = "Accountant"
        };
        var bankGl = Account(tenantId, "1100", "Operating Bank", AccountType.Asset);
        var counterGl = Account(tenantId, "5100", "Operating Expense", AccountType.Expense);
        var arGl = Account(tenantId, "1200", "Accounts Receivable", AccountType.Asset);
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = "0123456789",
            AccountName = "TDC Operating Account",
            BankName = "TDC Test Bank",
            Currency = "GHS",
            GLAccountId = bankGl.Id
        };
        var paymentJournal = Journal(tenantId, "JE-2026-0101", "Cash/bank payment", userId);
        paymentJournal.Transactions.Add(Line(tenantId, paymentJournal, counterGl, 1, 1_250m, 0m, "Expense allocation"));
        paymentJournal.Transactions.Add(Line(tenantId, paymentJournal, bankGl, 2, 0m, 1_250m, "Bank payment"));
        var cashPayment = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "CP-20260803-0001",
            TransactionDate = new DateTime(2026, 8, 3),
            TransactionType = CashTransactionType.Payment,
            BankAccountId = bank.Id,
            BankAccount = bank,
            Amount = 1_250m,
            Currency = "GHS",
            BaseAmount = 1_250m,
            ReferenceNumber = "PAY-REF-001",
            PayeeOrPayer = "TDC Service Provider",
            Description = "Approved operational payment",
            GLAccountId = counterGl.Id,
            IsPosted = true,
            ApprovalStatus = CashTransactionApprovalStatus.Posted,
            ApprovedById = userId,
            PostedBy = userId,
            PostedDate = new DateTime(2026, 8, 3),
            JournalEntryId = paymentJournal.Id,
            JournalEntry = paymentJournal,
            CreatedBy = "accounts.officer"
        };

        var receiptJournal = Journal(tenantId, "JE-2026-0102", "Customer receipt", userId);
        receiptJournal.Transactions.Add(Line(tenantId, receiptJournal, bankGl, 1, 800m, 0m, "Bank receipt"));
        receiptJournal.Transactions.Add(Line(tenantId, receiptJournal, arGl, 2, 0m, 800m, "Settle receivable"));
        var customer = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "CUS-001",
            PartnerName = "TDC Estate Customer",
            PartnerType = "Customer"
        };
        var customerPayment = new CustomerPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentNumber = "AR-PAY-2026-0001",
            BusinessPartnerId = customer.Id,
            PaymentDate = new DateTime(2026, 8, 3),
            TotalAmount = 800m,
            AllocatedAmount = 800m,
            PaymentMethod = "BankTransfer",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BankAccountId = bank.Id,
            BankAccount = bank,
            TransactionReference = "RCPT-REF-001",
            Status = "Posted",
            JournalEntryId = receiptJournal.Id,
            CreatedBy = "revenue.cashier"
        };

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.Accounts.AddRange(bankGl, counterGl, arGl);
        db.BankAccounts.Add(bank);
        db.JournalEntries.AddRange(paymentJournal, receiptJournal);
        db.Set<BusinessPartner>().Add(customer);
        db.Set<CashTransaction>().Add(cashPayment);
        db.Set<CustomerPayment>().Add(customerPayment);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns(user.UserName);
        var accessScope = new Mock<IFinanceAccessScopeService>();
        accessScope.Setup(service => service.EnsureBankAccountAccessAsync(
                It.IsAny<Guid?>(),
                It.IsAny<FinanceAccessLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var stored = new Dictionary<string, byte[]>();
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.UploadFileAsync(It.IsAny<FileUploadRequest>()))
            .ReturnsAsync((FileUploadRequest request) =>
            {
                using var buffer = new MemoryStream();
                request.FileStream.CopyTo(buffer);
                var path = $"private/{Guid.NewGuid():N}/{request.FileName}";
                stored[path] = buffer.ToArray();
                return new FileStorageResult
                {
                    Success = true,
                    FileName = request.FileName,
                    OriginalFileName = request.FileName,
                    FilePath = path,
                    PublicUrl = string.Empty,
                    FileSize = request.FileSize,
                    ContentType = request.ContentType,
                    Category = request.Category,
                    TenantId = request.TenantId,
                    StorageProvider = "TestPrivateStorage"
                };
            });
        storage.Setup(service => service.DownloadFileAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync((string path, Guid _) => new MemoryStream(stored[path], writable: false));
        storage.Setup(service => service.DeleteFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => stored.Remove(path));
        return new Fixture(
            db, currentUser, accessScope, storage, stored,
            new CapturingAuditService(), cashPayment, customerPayment);
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

    private static JournalEntry Journal(Guid tenantId, string number, string description, Guid postedBy)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = number,
            JournalType = "System Generated",
            EntryDate = new DateTime(2026, 8, 3),
            Description = description,
            TotalDebitAmount = 0m,
            TotalCreditAmount = 0m,
            IsBalanced = true,
            BookClassification = "IFRS",
            FiscalPeriodId = Guid.NewGuid(),
            PostingDate = new DateTime(2026, 8, 3),
            PostedByUserId = postedBy,
            PostingStatus = "Posted"
        };

    private static AccountTransaction Line(
        Guid tenantId,
        JournalEntry journal,
        Account account,
        int lineNumber,
        decimal debit,
        decimal credit,
        string description)
    {
        journal.TotalDebitAmount += debit;
        journal.TotalCreditAmount += credit;
        return new AccountTransaction
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
            FiscalPeriodId = journal.FiscalPeriodId,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            LineNumber = lineNumber
        };
    }

    private sealed record Fixture(
        ApplicationDbContext Context,
        Mock<ICurrentUserService> CurrentUser,
        Mock<IFinanceAccessScopeService> AccessScope,
        Mock<IFileStorageService> Storage,
        Dictionary<string, byte[]> Stored,
        CapturingAuditService Audit,
        CashTransaction CashPayment,
        CustomerPayment CustomerPayment);

    private sealed class CapturingAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = [];

        public Task<AuditLog> RecordAsync(FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
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
            => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }
}
