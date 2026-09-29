using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CustomerBalanceAdjustmentPostingTests
{
    [Theory]
    [InlineData("AR")]
    [InlineData("AP")]
    public async Task Standard_ar_ap_adjustment_4000_posts_to_exact_primary_book(
        string module)
    {
        await using var fixture = new Fixture(100m);
        var request = fixture.Request("StandardAdjustment", "Debit");
        request.Module = module;
        request.Amount = 4_000m;

        await fixture.Service.CreateAndPostAsync(request);

        var posting = Assert.Single(fixture.Postings);
        Assert.Equal(module, posting.SourceModule);
        Assert.Equal(4_000m, posting.Lines.Sum(line => line.DebitAmount));
        Assert.Equal(4_000m, posting.Lines.Sum(line => line.CreditAmount));
        Assert.Equal("BASE", posting.AccountingBookCode);
        Assert.Equal(request.AdjustmentDate.Date, posting.PostingDate.Date);
        Assert.Equal(fixture.TenantId, posting.SourceDocumentTenantId);
    }

    [Theory]
    [InlineData("FinanceCharge", "Debit", 100, 125)]
    [InlineData("Writeoff", "Credit", 100, 75)]
    [InlineData("OverpaymentWriteoff", "Debit", -100, -75)]
    public async Task Explicit_contra_posts_balanced_entry_and_replay_keeps_original_accounts(
        string purpose, string direction, decimal openingBalance, decimal expectedBalance)
    {
        await using var f = new Fixture(openingBalance);
        var request = f.Request(purpose, direction);
        var result = await f.Service.CreateAndPostAsync(request);
        Assert.Equal(expectedBalance, f.Customer.OutstandingBalance);
        var posting = Assert.Single(f.Postings);
        Assert.Equal(25m, posting.Lines.Sum(l => l.DebitAmount));
        Assert.Equal(25m, posting.Lines.Sum(l => l.CreditAmount));
        Assert.Equal(f.Control.Id, posting.Lines[0].AccountId);
        Assert.Equal(purpose == "Writeoff" ? f.Expense.Id : f.Revenue.Id, posting.Lines[1].AccountId);
        Assert.Equal("BASE", posting.AccountingBookCode);
        Assert.Equal(request.AdjustmentDate.Date, posting.PostingDate.Date);
        Assert.Equal(f.TenantId, posting.SourceDocumentTenantId);
        f.Customer.CustomerFinanceChargesAccountId = Guid.NewGuid();
        f.Customer.CustomerWriteoffAccountId = Guid.NewGuid();
        f.Customer.CustomerOverpaymentWriteoffAccountId = Guid.NewGuid();
        await f.Context.SaveChangesAsync();
        var replay = await f.Service.CreateAndPostAsync(request);
        Assert.Equal(result.Id, replay.Id);
        Assert.Single(f.Postings);
        Assert.Equal(expectedBalance, f.Customer.OutstandingBalance);
        request.Amount++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateAndPostAsync(request));
    }

    [Theory]
    [InlineData("Writeoff", "Debit", 100)]
    [InlineData("Writeoff", "Credit", 10)]
    [InlineData("OverpaymentWriteoff", "Debit", 100)]
    [InlineData("OverpaymentWriteoff", "Debit", -10)]
    public async Task Rejects_wrong_direction_or_insufficient_balance_before_posting(string purpose, string direction, decimal balance)
    {
        await using var f = new Fixture(balance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateAndPostAsync(f.Request(purpose, direction)));
        Assert.Empty(f.Postings);
        Assert.Single(f.Context.SubledgerAdjustmentJournals); // unchanged posted opening fixture
    }

    [Fact]
    public async Task Reversal_uses_captured_control_and_contra_after_customer_defaults_change()
    {
        await using var f = new Fixture(100);
        var original = await f.Service.CreateAndPostAsync(f.Request("Writeoff", "Credit"));
        f.Customer.DefaultArAccountId = Guid.NewGuid();
        f.Customer.CustomerWriteoffAccountId = Guid.NewGuid();
        await f.Context.SaveChangesAsync();
        await f.Service.ReverseAsync(original.Id, new() { Reason = "Verification correction" });
        Assert.Equal(2, f.Postings.Count);
        Assert.Equal(f.Control.Id, f.Postings[1].Lines[0].AccountId);
        Assert.Equal(f.Expense.Id, f.Postings[1].Lines[1].AccountId);
        Assert.All(f.Postings, posting => Assert.Equal("BASE", posting.AccountingBookCode));
        Assert.Equal(100m, f.Customer.OutstandingBalance);
    }

    [Fact]
    public async Task Reversal_rejects_stale_original_book_evidence_without_mutation()
    {
        await using var f = new Fixture(100);
        var original = await f.Service.CreateAndPostAsync(f.Request("Writeoff", "Credit"));
        var journal = await f.Context.JournalEntries.SingleAsync(item => item.Id == original.JournalEntryId);
        journal.BookClassification = "IFRS";
        await f.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.ReverseAsync(original.Id, new() { Reason = "Verification correction" }));

        Assert.Single(f.Postings);
        var unchanged = await f.Context.SubledgerAdjustmentJournals.SingleAsync(item => item.Id == original.Id);
        Assert.Equal(SubledgerAdjustmentStatuses.Posted, unchanged.Status);
        Assert.Null(unchanged.ReversalAdjustmentId);
    }

    [Fact]
    public async Task Standard_adjustment_rejects_ambiguous_primary_book_before_mutation()
    {
        await using var f = new Fixture(100);
        f.Context.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = f.TenantId, Code = "SECONDARY_PRIMARY",
            Name = "Invalid second primary",
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
        await f.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.CreateAndPostAsync(f.Request("FinanceCharge", "Debit")));

        Assert.Empty(f.Postings);
        Assert.Single(f.Context.SubledgerAdjustmentJournals);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("type")]
    [InlineData("control")]
    [InlineData("request")]
    [InlineData("currency")]
    [InlineData("balance-cache")]
    public async Task Rejects_invalid_mapping_or_request_before_mutation(string defect)
    {
        await using var f = new Fixture(100);
        var request = f.Request("FinanceCharge", "Debit");
        if (defect == "tenant")
        {
            var foreignAccount = new Account { Id=Guid.NewGuid(), TenantId=Guid.NewGuid(),
                AccountName="Other tenant income", AccountCode="OTHER-INCOME", AccountNumber="OTHER-INCOME",
                CurrencyCode="GHS", AccountType=AccountType.Revenue, Status=AccountStatus.Active,
                AllowDirectPosting=true };
            f.Context.Add(foreignAccount);
            request.ContraAccountId = foreignAccount.Id;
        }
        if (defect == "type") f.Revenue.AccountType = AccountType.Asset;
        if (defect == "control") f.Revenue.IsControlAccount = true;
        if (defect == "request") request.RequestId = null;
        if (defect == "currency") request.CurrencyCode = "USD";
        if (defect == "balance-cache") f.Customer.OutstandingBalance = 1000;
        await f.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateAndPostAsync(request));
        Assert.Empty(f.Postings);
        Assert.Equal(defect == "balance-cache" ? 1000m : 100m, f.Customer.OutstandingBalance);
    }

    [Fact]
    public async Task Legacy_default_does_not_supply_missing_explicit_contra_account()
    {
        await using var f = new Fixture(100);
        var request = f.Request("FinanceCharge", "Debit");
        request.ContraAccountId = Guid.Empty;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateAndPostAsync(request));
        Assert.Empty(f.Postings);
    }

    [Fact]
    public async Task Missing_approved_profile_rejects_before_posting()
    {
        await using var f = new Fixture(100);
        f.Context.Set<BusinessPartnerArProfileVersion>().RemoveRange(f.Context.Set<BusinessPartnerArProfileVersion>());
        await f.Context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateAndPostAsync(f.Request("FinanceCharge", "Debit")));
        Assert.Contains("AR_PROFILE_REQUIRED", error.Message);
        Assert.Empty(f.Postings);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; }
        public SubledgerAdjustmentJournalService Service { get; }
        public BusinessPartner Customer { get; }
        public Account Control { get; }
        public Account Expense { get; }
        public Account Revenue { get; }
        public AccountingBook Book { get; }
        public List<FinancePostingRequestV2Dto> Postings { get; } = [];
        private readonly Guid _tenant = Guid.NewGuid();
        public Guid TenantId => _tenant;
        public Fixture(decimal balance)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
            Context = new ApplicationDbContext(options);
            var user = new Mock<ICurrentUserService>();
            user.SetReturnsDefault<Guid>(_tenant);
            user.SetReturnsDefault<Guid?>(_tenant);
            user.SetReturnsDefault(_tenant.ToString());
            user.SetupGet(u => u.Claims).Returns(new Dictionary<string, string>());
            var numbering = new Mock<IDocumentNumberingService>();
            numbering.SetReturnsDefault(Task.FromResult("AR-ADJ-TEST"));
            var settings = new Mock<ITenantSettingsService>();
            settings.Setup(s => s.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
            Book = new AccountingBook
            {
                Id = Guid.NewGuid(), TenantId = _tenant, Code = "BASE", Name = "Primary base book",
                BookType = AccountingBookType.PrimaryFull,
                LifecycleStatus = AccountingBookLifecycleStatus.Active,
                IsDefault = true, IsActive = true, AllowsPosting = true
            };
            var engine = new Mock<IFinancePostingEngine>();
            engine.Setup(e => e.PostAsync(It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FinancePostingRequestV2Dto r, CancellationToken _) =>
                {
                    Postings.Add(r);
                    var journalId = Guid.NewGuid();
                    Context.JournalEntries.Add(new JournalEntry
                    {
                        Id = journalId,
                        TenantId = _tenant,
                        JournalEntryNumber = $"JE-{Postings.Count}",
                        JournalType = r.JournalType,
                        EntryDate = r.PostingDate,
                        PostingDate = r.PostingDate,
                        Description = r.Description,
                        SourceModule = r.SourceModule,
                        SourceDocumentType = r.SourceDocumentType,
                        SourceDocumentId = r.SourceDocumentId,
                        AccountingBookId = Book.Id,
                        BookClassification = r.AccountingBookCode,
                        FiscalPeriodId = Guid.NewGuid(),
                        PostingStatus = "Posted",
                        IsBalanced = true
                    });
                    return new FinancePostingResultDto
                    {
                        JournalEntryId = journalId,
                        PostingEventId = Guid.NewGuid(),
                        PostingDate = r.PostingDate,
                        PostingStatus = "Posted"
                    };
                });
            Control = Account(AccountType.Asset, true);
            Expense = Account(AccountType.Expense, false);
            Revenue = Account(AccountType.Revenue, false);
            Customer = new BusinessPartner { Id = Guid.NewGuid(), TenantId = _tenant, PartnerName = "Customer", PartnerCode = "CUSTOMER", PartnerType = "Customer", RegistrationStatus = "Approved", ApprovalStatus = "Approved", IsActive = true,
                OutstandingBalance = balance, DefaultArAccountId = Guid.NewGuid(), CustomerFinanceChargesAccountId = Revenue.Id, CustomerWriteoffAccountId = Expense.Id, CustomerOverpaymentWriteoffAccountId = Revenue.Id };
            var role = new BusinessPartnerRole { Id = Guid.NewGuid(), TenantId = _tenant,
                BusinessPartnerId = Customer.Id, RoleType = BusinessPartnerRoleType.Customer };
            var supplierRole = new BusinessPartnerRole { Id = Guid.NewGuid(), TenantId = _tenant,
                BusinessPartnerId = Customer.Id, RoleType = BusinessPartnerRoleType.Supplier };
            var profile = new BusinessPartnerArProfileVersion { Id = Guid.NewGuid(), TenantId = _tenant,
                BusinessPartnerRoleId = role.Id, VersionNumber = 1,
                Status = BusinessPartnerFinanceProfileStatus.Approved, EffectiveFrom = new DateTime(2026, 1, 1) };
            var apProfile = new BusinessPartnerApProfileVersion { Id = Guid.NewGuid(), TenantId = _tenant,
                BusinessPartnerRoleId = supplierRole.Id, VersionNumber = 1,
                Status = BusinessPartnerFinanceProfileStatus.Approved, EffectiveFrom = new DateTime(2026, 1, 1) };
            Context.AddRange(role, supplierRole, profile, apProfile);
            Context.AddRange(Book, Control, Expense, Revenue, Customer,
                new FinanceSettings { Id = Guid.NewGuid(), TenantId = _tenant,
                    ControlAccountArId = Control.Id, ControlAccountApId = Control.Id });
            Context.Add(new SubledgerAdjustmentJournal { Id=Guid.NewGuid(), TenantId=_tenant, Module="AR", BusinessPartnerId=Customer.Id,
                AdjustmentNumber="AR-EXISTING-TEST", AdjustmentDate=new DateTime(2026,9,1),
                AdjustmentType=balance < 0 ? "Credit" : "Debit", Amount=Math.Abs(balance), BaseCurrencyAmount=Math.Abs(balance),
                CurrencyCode="GHS", ExchangeRate=1, ContraAccountId=Expense.Id, JournalEntryId=Guid.NewGuid(), Reason="Existing posted balance" });
            Context.SaveChanges();
            Service = new(Context, user.Object, numbering.Object, engine.Object, settings.Object);
        }
        private Account Account(AccountType type, bool control) => new() { Id=Guid.NewGuid(), TenantId=_tenant, AccountName=type.ToString(), AccountCode=Guid.NewGuid().ToString(), AccountNumber=Guid.NewGuid().ToString(), CurrencyCode="GHS", AccountType=type, Status=AccountStatus.Active, AllowDirectPosting=!control, IsControlAccount=control };
        public CreateSubledgerAdjustmentJournalDto Request(string purpose, string direction) => new() { RequestId=Guid.NewGuid(), Module="AR", Purpose=purpose, BusinessPartnerId=Customer.Id, ContraAccountId=purpose == "Writeoff" ? Expense.Id : Revenue.Id, AdjustmentType=direction, Amount=25, CurrencyCode="GHS", ExchangeRate=1, AdjustmentDate=new DateTime(2026,9,25), Reason="Customer balance correction" };
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
