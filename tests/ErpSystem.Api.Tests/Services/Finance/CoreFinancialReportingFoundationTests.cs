using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CoreFinancialReportingFoundationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "Reporting")]
    public async Task TrialBalance_ShouldDeriveFromPostedGlAndExcludeDraftJournals()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var cash = SeedAccount(db, tenantId, AccountType.Asset, "1000", "Cash", balance: 999m);
        var equity = SeedAccount(db, tenantId, AccountType.Equity, "3000", "Equity", balance: 999m);
        SeedJournal(db, tenantId, period.Id, "JE-POSTED", "Posted", (cash.Id, 100m, 0m), (equity.Id, 0m, 100m));
        SeedJournal(db, tenantId, period.Id, "JE-DRAFT", "Draft", (cash.Id, 50m, 0m), (equity.Id, 0m, 50m));
        await db.SaveChangesAsync();

        var service = CreateGeneralLedgerService(db, tenantId);

        var report = await service.GenerateTrialBalanceAsync(new TrialBalanceRequestDto
        {
            AsAtDate = new DateTime(2026, 7, 31),
            PeriodStart = new DateTime(2026, 7, 1),
            IncludeZeroBalances = true
        });

        report.TotalDebits.Should().Be(100m);
        report.TotalCredits.Should().Be(100m);
        report.IsBalanced.Should().BeTrue();
        report.Lines.Single(l => l.AccountId == cash.Id).PeriodDebits.Should().Be(100m);
        report.Lines.Single(l => l.AccountId == equity.Id).PeriodCredits.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "Reporting")]
    public async Task BalanceSheet_ShouldDeriveFromPostedGlAndNormalizeCreditBalanceSections()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var cash = SeedAccount(db, tenantId, AccountType.Asset, "1000", "Cash");
        var equity = SeedAccount(db, tenantId, AccountType.Equity, "3000", "Retained Earnings");
        SeedJournal(db, tenantId, period.Id, "JE-POSTED", "Posted", (cash.Id, 100m, 0m), (equity.Id, 0m, 100m));
        SeedJournal(db, tenantId, period.Id, "JE-DRAFT", "Draft", (cash.Id, 25m, 0m), (equity.Id, 0m, 25m));
        await db.SaveChangesAsync();

        var service = CreateGeneralLedgerService(db, tenantId);

        var report = await service.GenerateBalanceSheetAsync(new BalanceSheetRequestDto
        {
            AsAtDate = new DateTime(2026, 7, 31)
        });

        report.TotalAssets.Should().Be(100m);
        report.TotalEquity.Should().Be(100m);
        report.TotalLiabilities.Should().Be(0m);
        report.IsBalanced.Should().BeTrue();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "Reporting")]
    public async Task IncomeStatement_ShouldDeriveFromPostedGlAndExcludeDraftJournals()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var cash = SeedAccount(db, tenantId, AccountType.Asset, "1000", "Cash");
        var revenue = SeedAccount(db, tenantId, AccountType.Revenue, "4000", "Sales", category: "Revenue");
        var expense = SeedAccount(db, tenantId, AccountType.Expense, "5000", "Rent", category: "Operating Expenses");
        SeedJournal(db, tenantId, period.Id, "JE-SALE", "Posted", (cash.Id, 200m, 0m), (revenue.Id, 0m, 200m));
        SeedJournal(db, tenantId, period.Id, "JE-EXP", "Posted", (expense.Id, 50m, 0m), (cash.Id, 0m, 50m));
        SeedJournal(db, tenantId, period.Id, "JE-DRAFT", "Draft", (cash.Id, 999m, 0m), (revenue.Id, 0m, 999m));
        await db.SaveChangesAsync();

        var service = CreateGeneralLedgerService(db, tenantId);

        var report = await service.GenerateIncomeStatementAsync(new IncomeStatementRequestDto
        {
            PeriodStart = new DateTime(2026, 7, 1),
            PeriodEnd = new DateTime(2026, 7, 31)
        });

        report.TotalRevenue.Should().Be(200m);
        report.TotalOperatingExpenses.Should().Be(50m);
        report.NetProfit.Should().Be(150m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "TenantIsolation")]
    public async Task Reports_ShouldRejectCrossTenantAccountFilter()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, AccountType.Asset, "1000", "Other Cash");
        await db.SaveChangesAsync();
        var service = CreateGeneralLedgerService(db, tenantId);

        var act = () => service.GenerateTrialBalanceAsync(new TrialBalanceRequestDto
        {
            AsAtDate = new DateTime(2026, 7, 31),
            AccountIds = new List<Guid> { otherAccount.Id }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("One or more report account filters do not belong to the current tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "TenantIsolation")]
    public async Task Reports_ShouldRejectCrossTenantSegmentFilter()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherSegment = SeedSegment(db, otherTenantId, "BR", "Branch", "001");
        await db.SaveChangesAsync();
        var service = CreateGeneralLedgerService(db, tenantId);

        var act = () => service.GenerateTrialBalanceAsync(new TrialBalanceRequestDto
        {
            AsAtDate = new DateTime(2026, 7, 31),
            SegmentFilters = new List<FinanceSegmentFilterDto>
            {
                new() { SegmentStructureId = otherSegment.Id, SegmentValue = "001" }
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("One or more report segment filters do not belong to the current tenant or are not reporting dimensions.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "Reporting")]
    public async Task CashBankLedger_ShouldUsePostedGlAndExcludeUnpostedCashTransactions()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var period = SeedPeriod(db, tenantId);
        var cash = SeedAccount(db, tenantId, AccountType.Asset, "1000", "Bank GL");
        var revenue = SeedAccount(db, tenantId, AccountType.Revenue, "4000", "Revenue");
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountNumber = "BANK-001",
            AccountName = "Operating Bank",
            BankName = "Bank",
            Currency = "GHS",
            AccountType = BankAccountType.Checking,
            GLAccountId = cash.Id,
            CurrentBalance = 999m,
            IsActive = true
        };
        db.BankAccounts.Add(bankAccount);
        db.Set<CashTransaction>().Add(new CashTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = "UNPOSTED-001",
            TransactionDate = new DateTime(2026, 7, 10),
            TransactionType = CashTransactionType.Receipt,
            Amount = 500m,
            BankAccountId = bankAccount.Id,
            IsPosted = false
        });
        SeedJournal(db, tenantId, period.Id, "JE-CASH", "Posted", (cash.Id, 100m, 0m), (revenue.Id, 0m, 100m));
        await db.SaveChangesAsync();

        var service = CreateGeneralLedgerService(db, tenantId);

        var report = await service.GenerateCashBankLedgerAsync(new CashBankLedgerRequestDto
        {
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31)
        });

        var account = report.Accounts.Should().ContainSingle().Subject;
        account.Receipts.Should().Be(100m);
        account.ClosingBalance.Should().Be(100m);
        account.StoredSnapshotBalance.Should().Be(999m);
        account.SnapshotVariance.Should().Be(899m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApAging_ShouldExcludeUnpostedVendorInvoices()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var supplierId = Guid.NewGuid();
        var posted = SeedVendorInvoice(db, tenantId, supplierId, "AP-POSTED", 100m);
        SeedVendorInvoice(db, tenantId, supplierId, "AP-UNPOSTED", 250m);
        SeedPostingEvent(db, tenantId, "AP", "VendorInvoice", posted.Id, posted.InvoiceNumber, posted.TotalAmount);
        await db.SaveChangesAsync();

        var service = CreateApReportsService(db, tenantId);

        var report = await service.GetAgingReportAsync(new DateTime(2026, 7, 31));

        report.TotalOutstanding.Should().Be(100m);
        report.TotalInvoices.Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-Reporting")]
    [Trait("Category", "AccountsReceivable")]
    public async Task ArAging_ShouldExcludeUnpostedCustomerInvoices()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedTenant(db, tenantId);
        var customerId = Guid.NewGuid();
        var posted = SeedCustomerInvoice(db, tenantId, customerId, "AR-POSTED", 100m);
        SeedCustomerInvoice(db, tenantId, customerId, "AR-UNPOSTED", 250m);
        SeedPostingEvent(db, tenantId, "AR", "CustomerInvoice", posted.Id, posted.InvoiceNumber, posted.TotalAmount);
        await db.SaveChangesAsync();

        var service = CreateArReportsService(db, tenantId);

        var report = await service.GetAgingReportAsync(new DateTime(2026, 7, 31));

        report.Summary.GrandTotal.Should().Be(100m);
        report.Customers.Should().ContainSingle();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-reporting-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static GeneralLedgerService CreateGeneralLedgerService(ApplicationDbContext db, Guid tenantId)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetCompanyNameAsync()).ReturnsAsync("Tenant Co");
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");

        var reportingOptions = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase($"finance-reporting-read-{Guid.NewGuid()}")
            .Options;

        return new GeneralLedgerService(
            db,
            new ReportingDbContext(reportingOptions),
            currentUser.Object,
            tenantSettings.Object,
            Mock.Of<IFiscalPeriodService>(),
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IAccountingBookService>(),
            Mock.Of<IFinancePostingEngine>());
    }

    private static ApReportsService CreateApReportsService(ApplicationDbContext db, Guid tenantId)
    {
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(x => x.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        return new ApReportsService(
            new UnitOfWork(db),
            CreateCurrentUser(tenantId).Object,
            tenantSettings.Object,
            Mock.Of<ILogger<ApReportsService>>());
    }

    private static ArReportsService CreateArReportsService(ApplicationDbContext db, Guid tenantId)
        => new(
            new UnitOfWork(db),
            CreateCurrentUser(tenantId).Object,
            Mock.Of<ILogger<ArReportsService>>());

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(x => x.UserName).Returns("finance.reporting");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("reporting-tests");
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
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId)
    {
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            IsOpen = true,
            IsClosed = false,
            PeriodStatus = "Open"
        };
        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        AccountType type,
        string number,
        string name,
        string? category = null,
        decimal balance = 0m)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = type,
            AccountCategory = category,
            IFRSLineItem = category ?? name,
            BaseLineItem = category ?? name,
            LocalLineItem = category ?? name,
            Status = AccountStatus.Active,
            AllowDirectPosting = true,
            Balance = balance
        };
        db.Accounts.Add(account);
        return account;
    }

    private static AccountSegmentStructure SeedSegment(
        ApplicationDbContext db,
        Guid tenantId,
        string code,
        string name,
        string value)
    {
        var segment = new AccountSegmentStructure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SegmentCode = code,
            SegmentName = name,
            SegmentPosition = 1,
            SegmentLength = value.Length,
            IsReportingDimension = true,
            IsActive = true
        };
        db.AccountSegmentStructures.Add(segment);
        return segment;
    }

    private static void SeedJournal(
        ApplicationDbContext db,
        Guid tenantId,
        Guid periodId,
        string number,
        string status,
        params (Guid AccountId, decimal Debit, decimal Credit)[] lines)
    {
        var journalId = Guid.NewGuid();
        var journal = new JournalEntry
        {
            Id = journalId,
            TenantId = tenantId,
            JournalEntryNumber = number,
            JournalType = "General",
            EntryDate = new DateTime(2026, 7, 10),
            Description = number,
            FiscalPeriodId = periodId,
            PostingStatus = status,
            BookClassification = "IFRS",
            TotalDebitAmount = lines.Sum(l => l.Debit),
            TotalCreditAmount = lines.Sum(l => l.Credit),
            IsBalanced = lines.Sum(l => l.Debit) == lines.Sum(l => l.Credit)
        };
        db.JournalEntries.Add(journal);

        var lineNumber = 1;
        foreach (var line in lines)
        {
            db.AccountTransactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = journalId,
                AccountId = line.AccountId,
                FiscalPeriodId = periodId,
                TransactionDate = journal.EntryDate,
                DebitAmount = line.Debit,
                CreditAmount = line.Credit,
                PostingStatus = status,
                BookClassification = "IFRS",
                LineNumber = lineNumber++,
                SourceModule = "GL",
                SourceDocumentType = "ManualJournalEntry"
            });
        }
    }

    private static VendorInvoice SeedVendorInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        Guid supplierId,
        string number,
        decimal amount)
    {
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = number,
            SupplierId = supplierId,
            SupplierName = "Supplier",
            InvoiceDate = new DateTime(2026, 7, 10),
            DueDate = new DateTime(2026, 7, 31),
            CurrencyCode = "GHS",
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            SubTotal = amount,
            TotalAmount = amount
        };
        db.VendorInvoices.Add(invoice);
        return invoice;
    }

    private static Invoice SeedCustomerInvoice(
        ApplicationDbContext db,
        Guid tenantId,
        Guid customerId,
        string number,
        decimal amount)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = number,
            BusinessPartnerId = customerId,
            CustomerName = "Customer",
            InvoiceDate = new DateTime(2026, 7, 10),
            DueDate = new DateTime(2026, 7, 31),
            CurrencyCode = "GHS",
            Status = InvoiceStatus.Sent,
            SubTotal = amount,
            TotalAmount = amount
        };
        db.Invoices.Add(invoice);
        return invoice;
    }

    private static void SeedPostingEvent(
        ApplicationDbContext db,
        Guid tenantId,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string reference,
        decimal amount)
    {
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentReference = reference,
            PostingAction = "Post",
            PostingStatus = "Posted",
            PostingDate = new DateTime(2026, 7, 10),
            PostedAt = DateTime.UtcNow,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            TotalDebitAmount = amount,
            TotalCreditAmount = amount
        });
    }
}
