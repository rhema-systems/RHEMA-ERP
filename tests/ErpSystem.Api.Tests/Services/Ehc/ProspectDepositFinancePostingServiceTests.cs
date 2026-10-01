using ErpSystem.Api.Services.Ehc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Ehc;

public sealed class ProspectDepositFinancePostingServiceTests
{
    [Fact]
    public async Task Cleared_receipt_debits_configured_bank_and_credits_prospect_liability_once()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var bankGlId = Guid.NewGuid();
        var liabilityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        await using var db = Context(tenantId);
        db.Accounts.AddRange(Account(tenantId, bankGlId, AccountType.Asset), Account(tenantId, liabilityId, AccountType.Liability));
        db.BankAccounts.Add(new BankAccount
        {
            Id = bankId, TenantId = tenantId, AccountNumber = "001", AccountName = "Prospect collections",
            BankName = "Test Bank", Currency = "GHS", GLAccountId = bankGlId, IsActive = true
        });
        db.FinanceSettings.Add(new FinanceSettings { Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS" });
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS", IsDefault = true,
            IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS",
            BookType = AccountingBookType.PrimaryFull, LifecycleStatus = AccountingBookLifecycleStatus.Active
        });
        await db.SaveChangesAsync();

        FinancePostingRequestV2Dto? captured = null;
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(x => x.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new FinancePostingResultDto
            {
                PostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid(), PostingStatus = "Posted"
            });
        var service = Service(db, User(tenantId, actorId), posting.Object);
        var receipt = Receipt(tenantId, liabilityId, bankId);

        await service.PostClearedReceiptAsync(receipt);

        Assert.NotNull(captured);
        Assert.Equal("ProspectDepositReceipt", captured!.SourceDocumentType);
        Assert.Equal(FinanceModuleLockCatalog.Sales, captured.OriginModuleCode);
        Assert.Equal(receipt.Id, captured.SourceDocumentId);
        Assert.Collection(captured.Lines.OrderBy(x => x.LineNumber),
            debit =>
            {
                Assert.Equal(bankGlId, debit.AccountId);
                Assert.Equal(receipt.Amount, debit.DebitAmount);
                Assert.Equal(0m, debit.CreditAmount);
            },
            credit =>
            {
                Assert.Equal(liabilityId, credit.AccountId);
                Assert.Equal(0m, credit.DebitAmount);
                Assert.Equal(receipt.Amount, credit.CreditAmount);
            });
        posting.Verify(x => x.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Customer_conversion_reclassifies_liability_without_a_second_cash_debit()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var prospectLiabilityId = Guid.NewGuid();
        var customerAdvanceId = Guid.NewGuid();
        var originalJournalId = Guid.NewGuid();
        var originalEventId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        await using var db = Context(tenantId);
        db.Accounts.AddRange(Account(tenantId, prospectLiabilityId, AccountType.Liability), Account(tenantId, customerAdvanceId, AccountType.Liability));
        db.FinanceSettings.Add(new FinanceSettings
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "GHS", CustomerAdvanceAccountId = customerAdvanceId
        });
        var partner = new BusinessPartner
        {
            Id = partnerId, TenantId = tenantId, PartnerCode = "CUS-001", PartnerName = "Prospect Customer",
            PartnerType = "Customer", RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true
        };
        var role = new BusinessPartnerRole
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = partnerId,
            RoleType = BusinessPartnerRoleType.Customer, Status = BusinessPartnerRoleStatus.Active
        };
        var profile = new BusinessPartnerArProfileVersion
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerRoleId = role.Id, VersionNumber = 1,
            Status = BusinessPartnerFinanceProfileStatus.Approved, EffectiveFrom = DateTime.UtcNow.AddDays(-1)
        };
        db.BusinessPartners.Add(partner);
        db.Set<BusinessPartnerRole>().Add(role);
        db.Set<BusinessPartnerArProfileVersion>().Add(profile);
        var receipt = Receipt(tenantId, prospectLiabilityId, Guid.NewGuid());
        receipt.Status = ProspectDepositReceiptStatuses.Cleared;
        receipt.PostingEventId = originalEventId;
        receipt.JournalEntryId = originalJournalId;
        db.JournalEntries.Add(new JournalEntry
        {
            Id = originalJournalId, TenantId = tenantId, JournalEntryNumber = "JE-PROSPECT-001",
            JournalType = "Property Prospect Deposit", EntryDate = DateTime.UtcNow.Date,
            Description = "Prospect deposit", SourceModule = "AR", OriginModuleCode = FinanceModuleLockCatalog.Sales,
            SourceDocumentId = receipt.Id, SourceDocumentType = "ProspectDepositReceipt",
            TotalDebitAmount = receipt.Amount, TotalCreditAmount = receipt.Amount, IsBalanced = true,
            BookClassification = "IFRS", AccountingBookId = bookId, FiscalPeriodId = Guid.NewGuid(),
            PostingStatus = "Posted"
        });
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = originalEventId, TenantId = tenantId, SourceModule = "AR", OriginModuleCode = FinanceModuleLockCatalog.Sales,
            SourceDocumentType = "ProspectDepositReceipt", SourceDocumentId = receipt.Id, PostingAction = "Post",
            JournalEntryId = originalJournalId, PostingStatus = "Posted", PostingDate = DateTime.UtcNow.Date,
            TotalDebitAmount = receipt.Amount, TotalCreditAmount = receipt.Amount, FunctionalCurrencyCode = "GHS",
            PrimaryTransactionCurrencyCode = "GHS", BookClassification = "IFRS", AccountingBookId = bookId
        });
        await db.SaveChangesAsync();

        FinancePostingRequestV2Dto? captured = null;
        var postingResult = new FinancePostingResultDto
        {
            PostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid(), PostingStatus = "Posted",
            PostingDate = DateTime.UtcNow.Date
        };
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(x => x.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestV2Dto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(postingResult);
        var authorityId = Guid.NewGuid();
        var authorities = new Mock<IFinanceSourceBookAuthorityService>();
        authorities.Setup(x => x.RetainExistingPostedOriginalAsync(
                It.IsAny<FinanceSourceBookAuthorityFreezeRequest>(), postingResult.JournalEntryId,
                postingResult.PostingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceBookAuthorityResult(authorityId, 1, FinanceModuleLockCatalog.Finance,
                "CustomerPayment", Guid.NewGuid(), "Post", postingResult.PostingDate, "LEGACY_POSTED", null,
                "CustomerPayment", bookId, "IFRS", "GHS", "GHS", "RETAINED_POSTED_ORIGINAL", "hash",
                postingResult.PostingEventId, postingResult.JournalEntryId));
        var service = Service(db, User(tenantId, actorId), posting.Object, authorities.Object);

        var result = await service.TransferToCustomerAdvanceAsync(receipt, partnerId);

        Assert.NotNull(captured);
        Assert.Equal("CustomerPayment", captured!.SourceDocumentType);
        Assert.Equal(FinanceModuleLockCatalog.Finance, captured.OriginModuleCode);
        Assert.DoesNotContain(captured.Lines, line => line.TransactionTag == "PROSPECT-DEPOSIT-CASH");
        Assert.Collection(captured.Lines.OrderBy(x => x.LineNumber),
            release =>
            {
                Assert.Equal(prospectLiabilityId, release.AccountId);
                Assert.Equal(receipt.Amount, release.DebitAmount);
            },
            advance =>
            {
                Assert.Equal(customerAdvanceId, advance.AccountId);
                Assert.Equal(receipt.Amount, advance.CreditAmount);
            });
        var payment = await db.Set<CustomerPayment>().SingleAsync(x => x.Id == result.CustomerPaymentId);
        Assert.True(payment.IsCustomerAdvance);
        Assert.Equal(partnerId, payment.BusinessPartnerId);
        Assert.Equal(postingResult.JournalEntryId, payment.JournalEntryId);
        Assert.Equal(authorityId, payment.SourceBookAuthorityId);
    }

    private static ProspectDepositFinancePostingService Service(
        ApplicationDbContext db,
        ICurrentUserService user,
        IFinancePostingEngine posting,
        IFinanceSourceBookAuthorityService? authorities = null) => new(
            db, user, posting, authorities ?? Mock.Of<IFinanceSourceBookAuthorityService>(),
            NullLogger<ProspectDepositFinancePostingService>.Instance);

    private static ApplicationDbContext Context(Guid tenantId) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"prospect-deposit-{Guid.NewGuid():N}")
            .Options,
        tenantId);

    private static ICurrentUserService User(Guid tenantId, Guid actorId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        user.SetupGet(x => x.UserId).Returns(actorId.ToString());
        user.SetupGet(x => x.UserName).Returns("sales.manager");
        user.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        return user.Object;
    }

    private static Account Account(Guid tenantId, Guid id, AccountType type) => new()
    {
        Id = id, TenantId = tenantId, AccountCode = id.ToString("N")[..8], AccountNumber = id.ToString("N")[..8],
        AccountName = type.ToString(), AccountType = type, CurrencyCode = "GHS", Status = AccountStatus.Active,
        AllowDirectPosting = true
    };

    private static ProspectDepositReceipt Receipt(Guid tenantId, Guid liabilityId, Guid bankId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProspectId = Guid.NewGuid(), TicketId = Guid.NewGuid(),
        LeadId = Guid.NewGuid(), OpportunityId = Guid.NewGuid(), ReceiptNumber = $"PDR-{Guid.NewGuid():N}"[..22],
        Amount = 25000m, Currency = "GHS", PaymentMethod = "BankTransfer", ReceivedAt = DateTime.UtcNow,
        Status = ProspectDepositReceiptStatuses.Pending, DepositLiabilityAccountId = liabilityId, BankAccountId = bankId
    };
}
