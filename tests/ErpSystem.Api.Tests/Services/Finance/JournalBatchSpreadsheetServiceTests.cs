using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using ErpSystem.Api.Middleware;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ClosedXML.Excel;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class JournalBatchSpreadsheetServiceTests
{
    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Spreadsheet")]
    public async Task TemplateAndPreview_ShouldExposeRequiredSheetsAndRejectFormulas()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedReferenceData(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, userId);

        var template = await service.CreateTemplateAsync();
        template.FileName.Should().Be("journal-batch-import-v1.xlsx");
        template.Content.Should().NotBeEmpty();

        byte[] workbookBytes;
        using (var package = new XLWorkbook(new MemoryStream(template.Content)))
        {
            package.Worksheets.Select(sheet => sheet.Name).Should().Contain(
                ["Instructions", "Batch", "JournalEntries", "JournalLines", "Lookups"]);

            var batch = package.Worksheet("Batch");
            batch.Cell(2, 2).Value = "Formula rejection test";
            batch.Cell(2, 3).Value = period.Id.ToString();
            batch.Cell(2, 4).Value = "IFRS";
            batch.Cell(2, 5).Value = "GHS";
            batch.Cell(2, 6).Value = 100m;
            batch.Cell(2, 7).Value = 1;
            batch.Cell(2, 8).FormulaA1 = "NOW()";

            var journals = package.Worksheet("JournalEntries");
            journals.Cell(2, 1).Value = "J1";
            journals.Cell(2, 2).Value = new DateTime(2026, 7, 15);
            journals.Cell(2, 3).Value = "General";
            journals.Cell(2, 4).Value = "Balanced imported journal";

            var lines = package.Worksheet("JournalLines");
            WriteLine(lines, 2, "J1", 1, "1000", "Debit", 100m);
            WriteLine(lines, 3, "J1", 2, "2000", "Credit", 100m);
            using var stream = new MemoryStream();
            package.SaveAs(stream);
            workbookBytes = stream.ToArray();
        }

        var preview = await service.PreviewAsync(
            new MemoryStream(workbookBytes),
            "journal-batch.xlsx");

        preview.IsValid.Should().BeFalse();
        preview.Issues.Should().Contain(issue => issue.Code == "FORMULA_NOT_ALLOWED");
        (await db.JournalBatchImportSessions.SingleAsync()).Status
            .Should().Be(JournalBatchImportStatus.Invalid);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "SpreadsheetSecurity")]
    public async Task Preview_ShouldRejectVbaBeforeClosedXmlParsesWorkbook()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedReferenceData(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, userId);
        var template = await service.CreateTemplateAsync();

        using var stream = new MemoryStream();
        await stream.WriteAsync(template.Content);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var vba = document.WorkbookPart!.AddNewPart<VbaProjectPart>();
            using var project = new MemoryStream([0x01, 0x00, 0x00, 0x00]);
            vba.FeedData(project);
        }

        var act = () => service.PreviewAsync(
            new MemoryStream(stream.ToArray()),
            "macro-disguised-as-xlsx.xlsx");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*macros are not allowed*");
        (await db.JournalBatchImportSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "SpreadsheetSecurity")]
    public async Task Preview_ShouldRejectUnsafeCompressionRatioBeforeClosedXmlLoads()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = CreateContext();
        SeedReferenceData(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, userId);
        var template = await service.CreateTemplateAsync();

        using var stream = new MemoryStream();
        await stream.WriteAsync(template.Content);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var bomb = archive.CreateEntry("xl/media/compression-test.bin", CompressionLevel.SmallestSize);
            await using var contents = bomb.Open();
            await contents.WriteAsync(new byte[2 * 1024 * 1024]);
        }

        var act = () => service.PreviewAsync(
            new MemoryStream(stream.ToArray()),
            "compressed-attack.xlsx");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unsafe compression ratio*");
        (await db.JournalBatchImportSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "SpreadsheetRoundTrip")]
    public async Task TemplatePreviewCommitAndExport_ShouldRoundTripBatchAndJournalValues()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedReferenceData(db, tenantId);
        await db.SaveChangesAsync();

        var created = new JournalBatchDetailDto
        {
            Id = batchId,
            BatchNumber = "JB-2026-00001",
            Description = "Imported close journals"
        };
        var batches = new Mock<IJournalBatchService>();
        batches
            .Setup(service => service.CreateAsync(It.IsAny<CreateJournalBatchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        batches
            .Setup(service => service.CreateJournalAsync(batchId, It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        batches
            .Setup(service => service.GetByIdAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        var service = CreateService(db, tenantId, userId, batches.Object);
        var template = await service.CreateTemplateAsync();
        var workbookBytes = PopulateValidWorkbook(template.Content, period.Id);

        var preview = await service.PreviewAsync(
            new MemoryStream(workbookBytes),
            "journal-batch.xlsx");
        preview.IsValid.Should().BeTrue();
        preview.JournalCount.Should().Be(1);
        preview.LineCount.Should().Be(2);
        preview.ExpectedDebitTotal.Should().Be(100m);
        preview.ActualDebitTotal.Should().Be(100m);
        var storedSession = await db.JournalBatchImportSessions.SingleAsync();
        storedSession.PreviewTokenHash.Should().NotBe(preview.PreviewToken);
        storedSession.PreviewTokenHash.Should().HaveLength(64);
        storedSession.NormalizedPayloadHash.Should().HaveLength(64);

        var committed = await service.CommitAsync(preview.SessionId, new CommitJournalBatchImportDto
        {
            PreviewToken = preview.PreviewToken,
            IdempotencyKey = "round-trip-import-1"
        });
        committed.Id.Should().Be(batchId);
        batches.Verify(service => service.CreateAsync(
            It.Is<CreateJournalBatchDto>(dto =>
                dto.Description == "Imported close journals" &&
                dto.ExpectedDebitTotal == 100m &&
                dto.ExpectedJournalCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        batches.Verify(service => service.CreateJournalAsync(
            batchId,
            It.Is<CreateJournalEntryDto>(dto =>
                dto.Transactions.Count == 2 &&
                dto.Transactions.Sum(line =>
                    line.TransactionType == "Debit" ? line.Amount : 0m) == 100m),
            It.IsAny<CancellationToken>()), Times.Once);

        SeedExportBatch(db, tenantId, period.Id, batchId);
        await db.SaveChangesAsync();
        var exported = await service.ExportAsync(batchId);
        exported.FileName.Should().Be("JB-2026-00001-journal-batch.xlsx");
        using var exportedWorkbook = new XLWorkbook(new MemoryStream(exported.Content));
        exportedWorkbook.Worksheet("Batch").Cell(2, 2).GetString()
            .Should().Be("Imported close journals");
        exportedWorkbook.Worksheet("JournalEntries").Cell(2, 4).GetString()
            .Should().Be("Balanced imported journal");
        exportedWorkbook.Worksheet("JournalLines").Cell(2, 5).GetValue<decimal>()
            .Should().Be(100m);
        exportedWorkbook.Worksheet("ReviewAndPosting").Cell(2, 2).GetString()
            .Should().Be("JE-2026-000001");
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "SpreadsheetIdempotency")]
    public async Task Commit_ShouldRejectIdempotencyKeyReusedForDifferentNormalizedPayload()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedReferenceData(db, tenantId);
        await db.SaveChangesAsync();
        var created = new JournalBatchDetailDto { Id = batchId, BatchNumber = "JB-IDEMPOTENT" };
        var batches = new Mock<IJournalBatchService>();
        batches.Setup(service => service.CreateAsync(It.IsAny<CreateJournalBatchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        batches.Setup(service => service.CreateJournalAsync(batchId, It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        batches.Setup(service => service.GetByIdAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        var service = CreateService(db, tenantId, userId, batches.Object);
        var template = await service.CreateTemplateAsync();
        var firstBytes = PopulateValidWorkbook(template.Content, period.Id);
        byte[] secondBytes;
        using (var workbook = new XLWorkbook(new MemoryStream(firstBytes)))
        {
            workbook.Worksheet("Batch").Cell(2, 2).Value = "A different normalized payload";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            secondBytes = stream.ToArray();
        }

        var firstPreview = await service.PreviewAsync(new MemoryStream(firstBytes), "first.xlsx");
        var secondPreview = await service.PreviewAsync(new MemoryStream(secondBytes), "second.xlsx");
        await service.CommitAsync(firstPreview.SessionId, new CommitJournalBatchImportDto
        {
            PreviewToken = firstPreview.PreviewToken,
            IdempotencyKey = "payload-bound-key"
        });

        var act = () => service.CommitAsync(secondPreview.SessionId, new CommitJournalBatchImportDto
        {
            PreviewToken = secondPreview.PreviewToken,
            IdempotencyKey = "payload-bound-key"
        });
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*different normalized journal-batch payload*");
        batches.Verify(
            service => service.CreateAsync(It.IsAny<CreateJournalBatchDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void WriteLine(
        IXLWorksheet sheet,
        int row,
        string key,
        int lineNumber,
        string accountNumber,
        string transactionType,
        decimal amount)
    {
        sheet.Cell(row, 1).Value = key;
        sheet.Cell(row, 2).Value = lineNumber;
        sheet.Cell(row, 3).Value = accountNumber;
        sheet.Cell(row, 4).Value = transactionType;
        sheet.Cell(row, 5).Value = amount;
        sheet.Cell(row, 6).Value = "GHS";
        sheet.Cell(row, 8).Value = 1m;
    }

    private static JournalBatchSpreadsheetService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Guid userId,
        IJournalBatchService? batches = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("spreadsheet.tester");

        return new JournalBatchSpreadsheetService(
            db,
            currentUser.Object,
            batches ?? Mock.Of<IJournalBatchService>());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"journal-batch-spreadsheet-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static byte[] PopulateValidWorkbook(byte[] template, Guid fiscalPeriodId)
    {
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var batch = workbook.Worksheet("Batch");
        batch.Cell(2, 1).Value = "1";
        batch.Cell(2, 2).Value = "Imported close journals";
        batch.Cell(2, 3).Value = fiscalPeriodId.ToString();
        batch.Cell(2, 4).Value = "IFRS";
        batch.Cell(2, 5).Value = "GHS";
        batch.Cell(2, 6).Value = 100m;
        batch.Cell(2, 7).Value = 1;

        var journals = workbook.Worksheet("JournalEntries");
        journals.Cell(2, 1).Value = "J1";
        journals.Cell(2, 2).Value = new DateTime(2026, 7, 15);
        journals.Cell(2, 3).Value = "General";
        journals.Cell(2, 4).Value = "Balanced imported journal";
        journals.Cell(2, 5).Value = "IMPORT-REF-1";

        var lines = workbook.Worksheet("JournalLines");
        WriteLine(lines, 2, "J1", 1, "1000", "Debit", 100m);
        WriteLine(lines, 3, "J1", 2, "2000", "Credit", 100m);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SeedExportBatch(
        ApplicationDbContext db,
        Guid tenantId,
        Guid fiscalPeriodId,
        Guid batchId)
    {
        var accounts = db.Accounts.Local.OrderBy(account => account.AccountNumber).ToList();
        var journalId = Guid.NewGuid();
        var journal = new JournalEntry
        {
            Id = journalId,
            TenantId = tenantId,
            JournalEntryNumber = "JE-2026-000001",
            JournalType = "General",
            EntryDate = new DateTime(2026, 7, 15),
            Description = "Balanced imported journal",
            ReferenceNumber = "IMPORT-REF-1",
            SourceModule = "GL",
            TotalDebitAmount = 100m,
            TotalCreditAmount = 100m,
            IsBalanced = true,
            FiscalPeriodId = fiscalPeriodId,
            BookClassification = "IFRS",
            PostingStatus = "Draft",
            ApprovalStatus = "Draft",
            Transactions =
            [
                new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JournalEntryId = journalId,
                    AccountId = accounts[0].Id,
                    TransactionDate = new DateTime(2026, 7, 15),
                    DebitAmount = 100m,
                    FunctionalCurrencyCode = "GHS",
                    TransactionCurrency = "GHS",
                    ExchangeRate = 1m,
                    LineNumber = 1
                },
                new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JournalEntryId = journalId,
                    AccountId = accounts[1].Id,
                    TransactionDate = new DateTime(2026, 7, 15),
                    CreditAmount = 100m,
                    FunctionalCurrencyCode = "GHS",
                    TransactionCurrency = "GHS",
                    ExchangeRate = 1m,
                    LineNumber = 2
                }
            ]
        };
        db.JournalBatches.Add(new JournalBatch
        {
            Id = batchId,
            TenantId = tenantId,
            BatchNumber = "JB-2026-00001",
            Description = "Imported close journals",
            FiscalPeriodId = fiscalPeriodId,
            BookClassification = "IFRS",
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 100m,
            ExpectedJournalCount = 1,
            Items =
            [
                new JournalBatchItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JournalEntryId = journalId,
                    JournalEntry = journal,
                    SequenceNumber = 1
                }
            ]
        });
    }

    private static FiscalPeriod SeedReferenceData(ApplicationDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Spreadsheet Tenant",
            Code = $"JBS-{tenantId:N}"[..12],
            BaseCurrency = "GHS"
        });
        db.Accounts.AddRange(
            new Account
            {
                TenantId = tenantId,
                AccountCode = "1000",
                AccountNumber = "1000",
                AccountName = "Cash",
                AccountType = AccountType.Asset,
                CurrencyCode = "GHS",
                Status = AccountStatus.Active,
                AllowDirectPosting = true
            },
            new Account
            {
                TenantId = tenantId,
                AccountCode = "2000",
                AccountNumber = "2000",
                AccountName = "Accrual",
                AccountType = AccountType.Liability,
                CurrencyCode = "GHS",
                Status = AccountStatus.Active,
                AllowDirectPosting = true
            });
        var period = new FiscalPeriod
        {
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true
        };
        db.FiscalPeriods.Add(period);
        return period;
    }
}
