using System.Text.RegularExpressions;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class JournalVoucherDocumentBuilderTests
{
    private static readonly DateTime SourceApprovedAt =
        new(2026, 8, 19, 14, 37, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Batch", "FinanceGoLive-JournalVoucher")]
    public async Task Render_OpeningBalanceJournal_ShouldPrintCanonicalSourceApprovalWithoutFalsifyingJournalApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = SeedJournal(db, tenantId, isOpeningBalance: true);
        SeedOpeningBatch(db, tenantId, journal, approvedAt: SourceApprovedAt);
        await db.SaveChangesAsync();
        var builder = new JournalVoucherDocumentBuilder(db, CurrentUser(tenantId).Object);

        var result = await builder.RenderAsync(Request(journal.Id));
        var text = ExtractNormalizedText(result.Content);

        result.Content.Should().StartWith([0x25, 0x50, 0x44, 0x46]);
        result.FileName.Should().Be("FP-MIGRAT-20260820153801093-886e57ae.pdf");
        text.Should().Contain("Journal Approval Journal Approved Date Posted At");
        text.Should().Contain("Not Required - 20 Aug 2026 15:38 UTC");
        text.Should().Contain("Source Approval Evidence");
        text.Should().Contain("Source Document Source Approval Source Approved Date");
        text.Should().Contain("Opening Balance Batch Approved 19 Aug 2026 14:37 UTC");
        text.Should().Contain("OB-20250101-2fde3d0df4ca");
        text.Should().Contain("did not require a separate approval");

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_JOURNAL_VOUCHER_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-JournalVoucher")]
    public async Task Render_ManualJournal_ShouldRetainItsOwnApprovalEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = SeedJournal(db, tenantId, isOpeningBalance: false);
        journal.RequiresApproval = true;
        journal.ApprovalStatus = "Approved";
        journal.ApprovedDate = new DateTime(2026, 8, 18, 11, 12, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var builder = new JournalVoucherDocumentBuilder(db, CurrentUser(tenantId).Object);

        var result = await builder.RenderAsync(Request(journal.Id));
        var text = ExtractNormalizedText(result.Content);

        text.Should().Contain("Journal Approval Journal Approved Date Posted At");
        text.Should().Contain("Approved 18 Aug 2026 11:12 UTC 20 Aug 2026 15:38 UTC");
        text.Should().NotContain("Source Approval Evidence");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-JournalVoucher")]
    public async Task Render_ForeignCurrencyJournal_ShouldLabelFunctionalAmountsAndPrintTransactionEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = SeedJournal(db, tenantId, isOpeningBalance: false);
        journal.PrimaryCurrency = "USD";
        journal.TotalDebitAmount = 875_000m;
        journal.TotalCreditAmount = 875_000m;

        var receivable = Account(tenantId, "000-1100-0000", "Accounts Receivable", AccountType.Asset);
        var clearing = Account(tenantId, "000-1990-0000", "Migration Clearing Account", AccountType.Asset);
        db.Accounts.AddRange(receivable, clearing);

        var receivableLine = Transaction(journal, receivable, 1, 875_000m, 0m);
        receivableLine.TransactionCurrency = "USD";
        receivableLine.TransactionDebitAmount = 70_000m;
        receivableLine.ForeignCurrencyAmount = 70_000m;
        receivableLine.ExchangeRate = 12.5m;

        var clearingLine = Transaction(journal, clearing, 2, 0m, 875_000m);
        clearingLine.TransactionCurrency = "GHS";
        clearingLine.TransactionCreditAmount = 875_000m;
        journal.Transactions.Add(receivableLine);
        journal.Transactions.Add(clearingLine);
        await db.SaveChangesAsync();
        var builder = new JournalVoucherDocumentBuilder(db, CurrentUser(tenantId).Object);

        var result = await builder.RenderAsync(Request(journal.Id));
        var text = ExtractNormalizedText(result.Content);

        text.Should().Contain("Functional Currency");
        text.Should().Contain("FINDEMO-JE-TEST GL GHS Posted");
        text.Should().Contain("USD 70,000.00 @ 12.5");
        text.Should().Contain("GHS functional only");
        text.Should().Contain("Functional Total Debit GHS 875,000.00");
        text.Should().Contain("Functional Total Credit GHS 875,000.00");
        text.Should().NotContain("USD 875,000.00");

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_JOURNAL_VOUCHER_FX_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
    }

    [Theory]
    [Trait("Batch", "FinanceGoLive-JournalVoucher")]
    [InlineData(false, true, true, true, "Posted")]
    [InlineData(true, false, true, true, "Posted")]
    [InlineData(true, true, false, true, "Posted")]
    [InlineData(true, true, true, false, "Posted")]
    [InlineData(true, true, true, true, "Approved")]
    public async Task Render_OpeningBalanceJournal_ShouldFailClosedWhenSourceApprovalEvidenceIsInvalid(
        bool includeSource,
        bool linkJournal,
        bool includeApprovalDate,
        bool sameTenant,
        string sourceStatus)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = SeedJournal(db, tenantId, isOpeningBalance: true);
        if (includeSource)
        {
            SeedOpeningBatch(
                db,
                sameTenant ? tenantId : Guid.NewGuid(),
                journal,
                journalEntryId: linkJournal ? journal.Id : Guid.NewGuid(),
                approvedAt: includeApprovalDate ? SourceApprovedAt : null,
                status: sourceStatus);
        }
        await db.SaveChangesAsync();
        var builder = new JournalVoucherDocumentBuilder(db, CurrentUser(tenantId).Object);

        var action = () => builder.RenderAsync(Request(journal.Id));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*opening-balance approval evidence*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-JournalVoucher")]
    public async Task Render_OpeningBalanceJournal_ShouldFailClosedWhenPostingEventDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = SeedJournal(db, tenantId, isOpeningBalance: true);
        SeedOpeningBatch(db, tenantId, journal, approvedAt: SourceApprovedAt);
        db.FinancePostingEvents.Local.Single().PostingAction = "Reverse";
        await db.SaveChangesAsync();
        var builder = new JournalVoucherDocumentBuilder(db, CurrentUser(tenantId).Object);

        var action = () => builder.RenderAsync(Request(journal.Id));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*opening-balance posting event*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-JournalVoucher")]
    public async Task Render_OpeningBalanceJournal_ShouldFailClosedWhenApprovalWorkflowIsIncomplete()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = SeedJournal(db, tenantId, isOpeningBalance: true);
        SeedOpeningBatch(db, tenantId, journal, approvedAt: SourceApprovedAt);
        var workflow = db.WorkflowInstances.Local.Single();
        workflow.Status = WorkflowInstanceStatus.InProgress;
        workflow.CompletedDate = null;
        await db.SaveChangesAsync();
        var builder = new JournalVoucherDocumentBuilder(db, CurrentUser(tenantId).Object);

        var action = () => builder.RenderAsync(Request(journal.Id));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*approval workflow is incomplete*");
    }

    private static JournalEntry SeedJournal(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpeningBalance)
    {
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Finance Demonstration & Mastery",
            Code = "FINANCE-DEMO",
            BaseCurrency = "GHS"
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "January 2025",
            PeriodCode = "2025-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true
        };
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = "FP-MIGRAT-20260820153801093-886e57ae",
            JournalType = isOpeningBalance ? "Opening Balance" : "General",
            EntryDate = new DateTime(2025, 1, 1),
            Description = isOpeningBalance
                ? "Fixed-asset IFRS opening balances"
                : "Approved manual journal",
            ReferenceNumber = isOpeningBalance ? "OB-20250101-2fde3d0df4ca" : "FINDEMO-JE-TEST",
            SourceModule = isOpeningBalance ? "MIGRATION" : "GL",
            SourceDocumentId = isOpeningBalance ? Guid.NewGuid() : null,
            SourceDocumentType = isOpeningBalance ? nameof(OpeningBalanceBatch) : null,
            TotalDebitAmount = 250_000m,
            TotalCreditAmount = 250_000m,
            IsBalanced = true,
            PrimaryCurrency = "GHS",
            BookClassification = "IFRS",
            FiscalPeriodId = period.Id,
            PostingDate = new DateTime(2026, 8, 20, 15, 38, 0, DateTimeKind.Utc),
            PostingStatus = "Posted",
            RequiresApproval = false,
            ApprovalStatus = "Not Required",
            CreatedAt = new DateTime(2026, 8, 20, 15, 38, 0, DateTimeKind.Utc),
            CreatedBy = "finance.demo.controller"
        };

        db.Tenants.Add(tenant);
        db.FiscalPeriods.Add(period);
        if (isOpeningBalance)
        {
            var equipment = Account(tenantId, "000-1520-0000", "Equipment", AccountType.Asset);
            var accumulatedDepreciation = Account(
                tenantId,
                "000-1590-0000",
                "Accumulated Depreciation",
                AccountType.Asset);
            var migrationClearing = Account(
                tenantId,
                "000-1990-0000",
                "Migration Clearing Account",
                AccountType.Asset);
            db.Accounts.AddRange(equipment, accumulatedDepreciation, migrationClearing);
            journal.Transactions.Add(Transaction(journal, equipment, 1, 250_000m, 0m));
            journal.Transactions.Add(Transaction(journal, accumulatedDepreciation, 2, 0m, 60_000m));
            journal.Transactions.Add(Transaction(journal, migrationClearing, 3, 0m, 190_000m));
        }
        db.JournalEntries.Add(journal);
        return journal;
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
        JournalEntry journal,
        Account account,
        int lineNumber,
        decimal debit,
        decimal credit)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = journal.TenantId,
            JournalEntryId = journal.Id,
            AccountId = account.Id,
            Account = account,
            TransactionDate = journal.EntryDate,
            Description = $"Opening balance {journal.ReferenceNumber}",
            DebitAmount = debit,
            CreditAmount = credit,
            FunctionalCurrencyCode = "GHS",
            TransactionCurrency = "GHS",
            FiscalPeriodId = journal.FiscalPeriodId,
            PostedDate = journal.PostingDate,
            PostingStatus = "Posted",
            BookClassification = "IFRS",
            LineNumber = lineNumber
        };

    private static OpeningBalanceBatch SeedOpeningBatch(
        ApplicationDbContext db,
        Guid tenantId,
        JournalEntry journal,
        Guid? journalEntryId = null,
        DateTime? approvedAt = null,
        string status = "Posted")
    {
        var postingEventId = Guid.NewGuid();
        var batch = new OpeningBalanceBatch
        {
            Id = journal.SourceDocumentId!.Value,
            TenantId = tenantId,
            BatchNumber = "OB-20250101-2fde3d0df4ca",
            SourceReference = "FINDEMO-FA-OB-2025",
            Description = "Fixed-asset IFRS opening balances",
            OpeningDate = new DateTime(2025, 1, 1),
            FiscalPeriodId = journal.FiscalPeriodId,
            BookClassification = "IFRS",
            Status = status,
            IdempotencyKey = "journal-voucher-source-approval-test",
            TotalDebit = 250_000m,
            TotalCredit = 250_000m,
            JournalEntryId = journalEntryId ?? journal.Id,
            PostingEventId = postingEventId,
            WorkflowInstanceId = Guid.NewGuid(),
            ApprovedAt = approvedAt,
            PostedAt = status == "Posted" ? journal.PostingDate : null,
            CreatedAt = new DateTime(2026, 8, 20, 15, 30, 0, DateTimeKind.Utc)
        };
        db.OpeningBalanceBatches.Add(batch);
        db.WorkflowInstances.Add(new WorkflowInstance
        {
            Id = batch.WorkflowInstanceId!.Value,
            TenantId = tenantId,
            WorkflowDefinitionId = Guid.NewGuid(),
            EntityId = batch.Id,
            EntityTypeId = Guid.NewGuid(),
            Status = WorkflowInstanceStatus.Completed,
            InitiatedById = Guid.NewGuid(),
            CreatedDate = batch.CreatedAt,
            StartedDate = batch.CreatedAt,
            CompletedDate = approvedAt ?? SourceApprovedAt
        });
        db.FinancePostingEvents.Add(new FinancePostingEvent
        {
            Id = postingEventId,
            TenantId = tenantId,
            SourceModule = "MIGRATION",
            SourceDocumentType = nameof(OpeningBalanceBatch),
            SourceDocumentId = batch.Id,
            PostingAction = "PostOpeningBalance",
            SourceDocumentReference = batch.BatchNumber,
            JournalEntryId = journalEntryId ?? journal.Id,
            PostingStatus = "Posted",
            PostingDate = batch.OpeningDate,
            RequestedAt = journal.PostingDate!.Value,
            PostedAt = journal.PostingDate,
            TotalDebitAmount = batch.TotalDebit,
            TotalCreditAmount = batch.TotalCredit,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS"
        });
        return batch;
    }

    private static DocumentRenderRequestDto Request(Guid journalId) => new()
    {
        DocumentType = DocumentTypes.FinanceJournalVoucher,
        EntityId = journalId,
        Format = "pdf",
        CopyType = "Original"
    };

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserName).Returns("finance.demo.controller");
        return currentUser;
    }

    private static string ExtractNormalizedText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var reader = new PdfReader(stream);
        using var document = new PdfDocument(reader);
        var pages = Enumerable.Range(1, document.GetNumberOfPages())
            .Select(page => PdfTextExtractor.GetTextFromPage(document.GetPage(page)));
        var text = string.Join(" ", pages)
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("ﬀ", "ff", StringComparison.Ordinal)
            .Replace("ﬁ", "fi", StringComparison.Ordinal)
            .Replace("ﬂ", "fl", StringComparison.Ordinal)
            .Replace("ﬃ", "ffi", StringComparison.Ordinal)
            .Replace("ﬄ", "ffl", StringComparison.Ordinal)
            // iText can drop the shaped "ti" pair from QuestPDF's subset font even when the
            // rendered glyphs are visually present. Normalize the affected accounting labels so
            // assertions verify document semantics rather than PDF text-extractor quirks.
            .Replace("Funconal", "Functional", StringComparison.Ordinal)
            .Replace("funconal", "functional", StringComparison.Ordinal)
            .Replace("Transacon", "Transaction", StringComparison.Ordinal);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"journal-voucher-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
