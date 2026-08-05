using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Middleware;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Api.Services.Spreadsheets;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class JournalBatchSpreadsheetService : IJournalBatchSpreadsheetService
{
    private const string TemplateVersion = "1";
    private const int MaximumFileBytes = 10 * 1024 * 1024;
    // Defensive parser ceilings. They are not a certified performance/SLA envelope.
    private const int MaximumJournalRows = 5_000;
    private const int MaximumLineRows = 50_000;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IJournalBatchService _batches;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JournalBatchSpreadsheetService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IJournalBatchService batches)
    {
        _context = context;
        _currentUser = currentUser;
        _batches = batches;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    private Guid UserId
    {
        get
        {
            if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId == Guid.Empty)
                throw new InvalidOperationException("An authenticated user is required.");
            return userId;
        }
    }

    public async Task<JournalBatchFileDto> CreateTemplateAsync(CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        AddInstructions(workbook);
        AddBatchSheet(workbook);
        AddEntriesSheet(workbook);
        AddLinesSheet(workbook);
        await AddLookupsSheetAsync(workbook, cancellationToken);
        return new JournalBatchFileDto
        {
            Content = ToByteArray(workbook),
            FileName = $"journal-batch-import-v{TemplateVersion}.xlsx"
        };
    }

    public async Task<JournalBatchImportPreviewDto> PreviewAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .xlsx journal-batch workbooks are accepted.");

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0 || memory.Length > MaximumFileBytes)
            throw new InvalidOperationException($"Workbook size must be between 1 byte and {MaximumFileBytes / 1024 / 1024} MB.");
        var bytes = memory.ToArray();
        SpreadsheetSecurityInspector.ValidateXlsxPackage(bytes);
        if (SpreadsheetSecurityInspector.HasVbaProject(bytes))
            throw new InvalidOperationException("VBA projects and macros are not allowed in journal-batch workbooks.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        using var workbook = new XLWorkbook(new MemoryStream(bytes, writable: false));
        var issues = new List<JournalBatchImportIssueDto>();
        var batchSheet = RequireSheet(workbook, "Batch", issues);
        var entriesSheet = RequireSheet(workbook, "JournalEntries", issues);
        var linesSheet = RequireSheet(workbook, "JournalLines", issues);

        RejectUnsafeWorkbookContent(workbook, bytes, issues);
        var payload = new ImportPayload();
        if (batchSheet != null)
            payload.Batch = ReadBatch(batchSheet, issues);
        if (entriesSheet != null)
            payload.Journals = ReadJournals(entriesSheet, issues);
        if (linesSheet != null)
            payload.Lines = ReadLines(linesSheet, issues);

        await ValidatePayloadAsync(payload, issues, cancellationToken);
        var normalizedPayloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var normalizedPayloadHash = ComputeNormalizedPayloadHash(payload);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new JournalBatchImportSession
        {
            TenantId = TenantId,
            PreviewTokenHash = HashSecret(token),
            FileHash = hash,
            NormalizedPayloadHash = normalizedPayloadHash,
            TemplateVersion = payload.Batch.TemplateVersion,
            OriginalFileName = Path.GetFileName(fileName),
            Status = issues.Any(x => x.Severity == "Error")
                ? JournalBatchImportStatus.Invalid
                : JournalBatchImportStatus.Previewed,
            UploadedByUserId = UserId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            JournalCount = payload.Journals.Count,
            LineCount = payload.Lines.Count,
            ErrorCount = issues.Count(x => x.Severity == "Error"),
            NormalizedPayloadJson = normalizedPayloadJson,
            IssuesJson = JsonSerializer.Serialize(issues, JsonOptions),
            CreatedBy = _currentUser.UserName,
            CreatedById = UserId
        };
        _context.JournalBatchImportSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return MapPreview(session, payload, issues, token);
    }

    public async Task<JournalBatchDetailDto> CommitAsync(
        Guid sessionId,
        CommitJournalBatchImportDto dto,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.JournalBatchImportSessions.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == sessionId && !x.IsDeleted,
            cancellationToken)
            ?? throw new ArgumentException("Import preview session was not found for this tenant.");
        if (session.UploadedByUserId != UserId)
            throw new UnauthorizedAccessException("Only the user who previewed the workbook can commit it.");
        if (!FixedTimeHashEquals(session.PreviewTokenHash, HashSecret(dto.PreviewToken)))
            throw new InvalidOperationException("The import preview token is invalid.");
        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            session.Status = JournalBatchImportStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The import preview has expired. Preview the workbook again.");
        }
        if (session.Status == JournalBatchImportStatus.Committed && session.CommittedJournalBatchId.HasValue)
        {
            if (!string.Equals(session.IdempotencyKey, dto.IdempotencyKey.Trim(), StringComparison.Ordinal))
            {
                throw new ConflictException(
                    "This import session was already committed with a different idempotency key.");
            }
            return await _batches.GetByIdAsync(session.CommittedJournalBatchId.Value, cancellationToken)
                   ?? throw new InvalidOperationException("The committed journal batch could not be reloaded.");
        }
        if (session.Status != JournalBatchImportStatus.Previewed || session.ErrorCount != 0)
            throw new InvalidOperationException("A workbook with blocking validation errors cannot be committed.");

        var duplicate = await _context.JournalBatchImportSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == TenantId &&
                     x.IdempotencyKey == dto.IdempotencyKey &&
                     x.Status == JournalBatchImportStatus.Committed &&
                     x.CommittedJournalBatchId.HasValue &&
                     !x.IsDeleted,
                cancellationToken);
        if (duplicate?.CommittedJournalBatchId is { } duplicateBatchId)
        {
            if (!FixedTimeHashEquals(duplicate.NormalizedPayloadHash, session.NormalizedPayloadHash))
            {
                throw new ConflictException(
                    "The idempotency key was already used for a different normalized journal-batch payload.");
            }
            return await _batches.GetByIdAsync(duplicateBatchId, cancellationToken)
                   ?? throw new InvalidOperationException("The previously committed journal batch could not be reloaded.");
        }

        var payload = JsonSerializer.Deserialize<ImportPayload>(session.NormalizedPayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("The normalized import payload is unavailable.");
        var revalidationIssues = new List<JournalBatchImportIssueDto>();
        await ValidatePayloadAsync(payload, revalidationIssues, cancellationToken);
        if (revalidationIssues.Any(x => x.Severity == "Error"))
        {
            session.Status = JournalBatchImportStatus.Invalid;
            session.ErrorCount = revalidationIssues.Count(x => x.Severity == "Error");
            session.IssuesJson = JsonSerializer.Serialize(revalidationIssues, JsonOptions);
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Reference data changed after preview. Review the new import errors.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var created = await _batches.CreateAsync(new CreateJournalBatchDto
            {
                Description = payload.Batch.Description,
                FiscalPeriodId = payload.Batch.FiscalPeriodId,
                BookClassification = payload.Batch.BookClassification,
                ControlCurrencyCode = payload.Batch.ControlCurrencyCode,
                ExpectedDebitTotal = payload.Batch.ExpectedDebitTotal,
                ExpectedJournalCount = payload.Batch.ExpectedJournalCount,
                Notes = payload.Batch.Notes
            }, cancellationToken);

            foreach (var journal in payload.Journals)
            {
                var lines = payload.Lines
                    .Where(x => string.Equals(x.ClientJournalKey, journal.ClientJournalKey, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.LineNumber)
                    .ToList();
                await _batches.CreateJournalAsync(created.Id, new CreateJournalEntryDto
                {
                    TransactionDate = journal.TransactionDate,
                    JournalType = journal.JournalType,
                    Description = journal.Description,
                    Reference = journal.Reference,
                    BookClassification = payload.Batch.BookClassification,
                    SourceModule = "GL",
                    SourceDocumentType = "JournalBatchSpreadsheetImport",
                    Notes = journal.Notes,
                    FiscalPeriodId = payload.Batch.FiscalPeriodId,
                    Transactions = lines.Select(x => new CreateAccountTransactionDto
                    {
                        AccountId = x.AccountId,
                        Amount = x.Amount,
                        TransactionType = x.TransactionType,
                        Description = x.Description,
                        Reference = x.Reference ?? journal.Reference ?? string.Empty,
                        CurrencyCode = x.CurrencyCode,
                        ForeignAmount = x.ForeignAmount,
                        ExchangeRate = x.ExchangeRate,
                        LineNumber = x.LineNumber
                    }).ToList()
                }, cancellationToken);
            }

            session.Status = JournalBatchImportStatus.Committed;
            session.IdempotencyKey = dto.IdempotencyKey.Trim();
            session.CommittedJournalBatchId = created.Id;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = _currentUser.UserName;
            session.LastModifiedById = UserId;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await _batches.GetByIdAsync(created.Id, cancellationToken)
                   ?? throw new InvalidOperationException("Imported journal batch could not be reloaded.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            var failed = await _context.JournalBatchImportSessions.FirstOrDefaultAsync(
                x => x.TenantId == TenantId && x.Id == sessionId,
                cancellationToken);
            if (failed != null)
            {
                failed.Status = JournalBatchImportStatus.Failed;
                failed.ErrorMessage = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
                await _context.SaveChangesAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task<JournalBatchFileDto> CreateErrorWorkbookAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.JournalBatchImportSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == TenantId &&
                     x.Id == sessionId &&
                     x.UploadedByUserId == UserId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new ArgumentException("Import preview session was not found for this tenant.");
        var issues = JsonSerializer.Deserialize<List<JournalBatchImportIssueDto>>(session.IssuesJson, JsonOptions) ?? [];
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Errors");
        WriteHeaders(sheet, ["Sheet", "Row", "Column", "Value", "Code", "Severity", "Message"]);
        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index];
            var row = index + 2;
            SetCellValue(sheet, row, 1, issue.Sheet);
            SetCellValue(sheet, row, 2, issue.Row);
            SetCellValue(sheet, row, 3, issue.Column);
            SetCellValue(sheet, row, 4, issue.Value);
            SetCellValue(sheet, row, 5, issue.Code);
            SetCellValue(sheet, row, 6, issue.Severity);
            SetCellValue(sheet, row, 7, issue.Message);
        }
        AdjustColumns(sheet, 12, 80);
        return new JournalBatchFileDto
        {
            Content = ToByteArray(workbook),
            FileName = $"journal-batch-import-errors-{session.Id:N}.xlsx"
        };
    }

    public async Task<JournalBatchFileDto> ExportAsync(
        Guid journalBatchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _context.JournalBatches
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Id == journalBatchId && !x.IsDeleted)
            .Include(x => x.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.JournalEntry)
                    .ThenInclude(journal => journal.Transactions)
                        .ThenInclude(transaction => transaction.Account)
            .Include(x => x.Items.Where(item => !item.IsDeleted))
                .ThenInclude(item => item.Reviews.Where(review => !review.IsDeleted))
            .Include(x => x.PostingRuns.Where(run => !run.IsDeleted))
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException("Journal batch was not found for this tenant.");

        using var workbook = new XLWorkbook();
        AddInstructions(workbook);
        var batchSheet = AddBatchSheet(workbook);
        SetCellValue(batchSheet, 2, 1, TemplateVersion);
        SetCellValue(batchSheet, 2, 2, batch.Description);
        SetCellValue(batchSheet, 2, 3, batch.FiscalPeriodId);
        SetCellValue(batchSheet, 2, 4, batch.BookClassification);
        SetCellValue(batchSheet, 2, 5, batch.ControlCurrencyCode);
        SetCellValue(batchSheet, 2, 6, batch.ExpectedDebitTotal);
        SetCellValue(batchSheet, 2, 7, batch.ExpectedJournalCount);
        SetCellValue(batchSheet, 2, 8, batch.Notes);

        var entriesSheet = AddEntriesSheet(workbook);
        var linesSheet = AddLinesSheet(workbook);
        var itemKeys = new Dictionary<Guid, string>();
        var entryRow = 2;
        var lineRow = 2;
        foreach (var item in batch.Items.OrderBy(x => x.SequenceNumber))
        {
            var journal = item.JournalEntry;
            var key = $"J{item.SequenceNumber:0000}";
            itemKeys[item.Id] = key;
            SetCellValue(entriesSheet, entryRow, 1, key);
            SetCellValue(entriesSheet, entryRow, 2, journal.EntryDate);
            entriesSheet.Cell(entryRow, 2).Style.NumberFormat.Format = "yyyy-mm-dd";
            SetCellValue(entriesSheet, entryRow, 3, journal.JournalType);
            SetCellValue(entriesSheet, entryRow, 4, journal.Description);
            SetCellValue(entriesSheet, entryRow, 5, journal.ReferenceNumber);
            SetCellValue(entriesSheet, entryRow, 6, journal.Notes);
            entryRow++;

            foreach (var line in journal.Transactions.Where(x => !x.IsDeleted).OrderBy(x => x.LineNumber))
            {
                SetCellValue(linesSheet, lineRow, 1, key);
                SetCellValue(linesSheet, lineRow, 2, line.LineNumber);
                SetCellValue(linesSheet, lineRow, 3, line.Account?.AccountNumber);
                SetCellValue(linesSheet, lineRow, 4, line.DebitAmount > 0 ? "Debit" : "Credit");
                SetCellValue(linesSheet, lineRow, 5, line.DebitAmount > 0 ? line.DebitAmount : line.CreditAmount);
                SetCellValue(linesSheet, lineRow, 6, line.TransactionCurrency);
                SetCellValue(linesSheet, lineRow, 7, line.ForeignCurrencyAmount);
                SetCellValue(linesSheet, lineRow, 8, line.ExchangeRate);
                SetCellValue(linesSheet, lineRow, 9, line.Description);
                SetCellValue(linesSheet, lineRow, 10, line.SourceReferenceNumber);
                lineRow++;
            }
        }

        var reviewSheet = workbook.Worksheets.Add("ReviewAndPosting");
        WriteHeaders(reviewSheet, ["ClientJournalKey", "JournalNumber", "ReviewStatus", "ReviewerId", "ReviewedAt", "RejectionReason", "PostingStatus", "PostingRun", "PostedAt"]);
        var reviewRow = 2;
        foreach (var item in batch.Items.OrderBy(x => x.SequenceNumber))
        {
            SetCellValue(reviewSheet, reviewRow, 1, itemKeys[item.Id]);
            SetCellValue(reviewSheet, reviewRow, 2, item.JournalEntry.JournalEntryNumber);
            SetCellValue(reviewSheet, reviewRow, 3, item.ReviewStatus.ToString());
            SetCellValue(reviewSheet, reviewRow, 4, item.FinalReviewedByUserId);
            SetCellValue(reviewSheet, reviewRow, 5, item.FinalReviewedAt);
            SetCellValue(reviewSheet, reviewRow, 6, item.FinalRejectionReason);
            SetCellValue(reviewSheet, reviewRow, 7, item.PostingStatus.ToString());
            SetCellValue(reviewSheet, reviewRow, 8, batch.PostingRuns.FirstOrDefault(x => x.Id == item.PostedInRunId)?.RunNumber);
            SetCellValue(reviewSheet, reviewRow, 9, item.PostedAt);
            reviewRow++;
        }
        AdjustColumns(reviewSheet, 12, 60);
        await AddLookupsSheetAsync(workbook, cancellationToken);
        return new JournalBatchFileDto
        {
            Content = ToByteArray(workbook),
            FileName = $"{batch.BatchNumber}-journal-batch.xlsx"
        };
    }

    private static void AddInstructions(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Instructions");
        var rows = new[]
        {
            ("Template version", TemplateVersion),
            ("Purpose", "Create one journal batch containing multiple independently balanced manual journals."),
            ("Required sheets", "Batch, JournalEntries, JournalLines"),
            ("Rules", "Do not rename sheets or columns. Do not use formulas, macros, or external links."),
            ("ClientJournalKey", "A workbook-local key joining JournalEntries to JournalLines; it is not stored as the journal number."),
            ("Control total", "ExpectedDebitTotal must equal the sum of each journal's debit total."),
            ("Dates", "Use ISO yyyy-mm-dd dates within the selected open fiscal period."),
            ("Amounts", "Each line must be Debit or Credit with a positive amount; each journal must balance.")
        };
        for (var row = 0; row < rows.Length; row++)
        {
            SetCellValue(sheet, row + 1, 1, rows[row].Item1);
            SetCellValue(sheet, row + 1, 2, rows[row].Item2);
        }
        sheet.Range(1, 1, rows.Length, 1).Style.Font.Bold = true;
        AdjustColumns(sheet, 16, 100);
    }

    private static IXLWorksheet AddBatchSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Batch");
        WriteHeaders(sheet, ["TemplateVersion", "Description", "FiscalPeriodId", "BookClassification", "ControlCurrencyCode", "ExpectedDebitTotal", "ExpectedJournalCount", "Notes"]);
        SetCellValue(sheet, 2, 1, TemplateVersion);
        SetCellValue(sheet, 2, 4, "IFRS");
        SetCellValue(sheet, 2, 5, "GHS");
        return sheet;
    }

    private static IXLWorksheet AddEntriesSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("JournalEntries");
        WriteHeaders(sheet, ["ClientJournalKey", "TransactionDate", "JournalType", "Description", "Reference", "Notes"]);
        return sheet;
    }

    private static IXLWorksheet AddLinesSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("JournalLines");
        WriteHeaders(sheet, ["ClientJournalKey", "LineNumber", "AccountNumber", "TransactionType", "Amount", "CurrencyCode", "ForeignAmount", "ExchangeRate", "Description", "Reference"]);
        return sheet;
    }

    private async Task AddLookupsSheetAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("Lookups");
        WriteHeaders(sheet, ["AccountNumber", "AccountName", "AllowDirectPosting", "FiscalPeriodId", "FiscalPeriodName", "StartDate", "EndDate"]);
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && !x.IsDeleted && x.Status == AccountStatus.Active)
            .OrderBy(x => x.AccountNumber)
            .Select(x => new { x.AccountNumber, x.AccountName, x.AllowDirectPosting })
            .ToListAsync(cancellationToken);
        var periods = await _context.FiscalPeriods.AsNoTracking()
            .Where(x => x.TenantId == TenantId && !x.IsDeleted && x.IsOpen && !x.IsClosed && !x.IsLocked)
            .OrderBy(x => x.StartDate)
            .Select(x => new { x.Id, x.PeriodName, x.StartDate, x.EndDate })
            .ToListAsync(cancellationToken);
        var rows = Math.Max(accounts.Count, periods.Count);
        for (var index = 0; index < rows; index++)
        {
            var row = index + 2;
            if (index < accounts.Count)
            {
                SetCellValue(sheet, row, 1, accounts[index].AccountNumber);
                SetCellValue(sheet, row, 2, accounts[index].AccountName);
                SetCellValue(sheet, row, 3, accounts[index].AllowDirectPosting);
            }
            if (index < periods.Count)
            {
                SetCellValue(sheet, row, 4, periods[index].Id);
                SetCellValue(sheet, row, 5, periods[index].PeriodName);
                SetCellValue(sheet, row, 6, periods[index].StartDate);
                SetCellValue(sheet, row, 7, periods[index].EndDate);
                sheet.Range(row, 6, row, 7).Style.NumberFormat.Format = "yyyy-mm-dd";
            }
        }
        AdjustColumns(sheet, 12, 50);
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var column = 0; column < headers.Count; column++)
            SetCellValue(sheet, 1, column + 1, headers[column]);
        var range = sheet.Range(1, 1, 1, headers.Count);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 78, 121);
        range.Style.Font.FontColor = XLColor.White;
        sheet.SheetView.FreezeRows(1);
        range.SetAutoFilter();
        AdjustColumns(sheet, 12, 40);
    }

    private static IXLWorksheet? RequireSheet(
        XLWorkbook workbook,
        string name,
        ICollection<JournalBatchImportIssueDto> issues)
    {
        if (!workbook.TryGetWorksheet(name, out var sheet))
        {
            issues.Add(ImportIssue(name, 0, null, "MISSING_SHEET", $"Required worksheet '{name}' is missing."));
            return null;
        }
        return sheet;
    }

    private static void RejectUnsafeWorkbookContent(
        XLWorkbook workbook,
        byte[] workbookBytes,
        ICollection<JournalBatchImportIssueDto> issues)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            foreach (var cell in sheet.CellsUsed(XLCellsUsedOptions.AllContents))
            {
                if (cell.HasFormula)
                    issues.Add(ImportIssue(sheet.Name, cell.Address.RowNumber, cell.Address.ToString(), "FORMULA_NOT_ALLOWED", "Formulas are not allowed in journal-batch workbooks.", cell.FormulaA1));
            }
        }
        if (SpreadsheetSecurityInspector.HasExternalRelationships(workbookBytes))
            issues.Add(ImportIssue("Workbook", 0, null, "EXTERNAL_LINK_NOT_ALLOWED", "External workbook links are not allowed."));
    }

    private static ImportBatchHeader ReadBatch(
        IXLWorksheet sheet,
        ICollection<JournalBatchImportIssueDto> issues)
    {
        var header = new ImportBatchHeader
        {
            TemplateVersion = Text(sheet, 2, 1),
            Description = Text(sheet, 2, 2),
            BookClassification = Text(sheet, 2, 4),
            ControlCurrencyCode = Text(sheet, 2, 5),
            Notes = NullIfEmpty(Text(sheet, 2, 8))
        };
        if (!Guid.TryParse(Text(sheet, 2, 3), out var periodId))
            issues.Add(ImportIssue(sheet.Name, 2, "FiscalPeriodId", "INVALID_PERIOD_ID", "FiscalPeriodId must be a valid GUID.", Text(sheet, 2, 3)));
        else
            header.FiscalPeriodId = periodId;
        if (!TryDecimal(sheet.Cell(2, 6), out var expected) || expected <= 0)
            issues.Add(ImportIssue(sheet.Name, 2, "ExpectedDebitTotal", "INVALID_EXPECTED_TOTAL", "ExpectedDebitTotal must be greater than zero.", Text(sheet, 2, 6)));
        else
            header.ExpectedDebitTotal = decimal.Round(expected, 2);
        if (!string.IsNullOrWhiteSpace(Text(sheet, 2, 7)))
        {
            if (!int.TryParse(Text(sheet, 2, 7), out var expectedCount) || expectedCount <= 0)
                issues.Add(ImportIssue(sheet.Name, 2, "ExpectedJournalCount", "INVALID_EXPECTED_COUNT", "ExpectedJournalCount must be a positive whole number.", Text(sheet, 2, 7)));
            else
                header.ExpectedJournalCount = expectedCount;
        }
        return header;
    }

    private static List<ImportJournal> ReadJournals(
        IXLWorksheet sheet,
        ICollection<JournalBatchImportIssueDto> issues)
    {
        var result = new List<ImportJournal>();
        var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.AllContents)?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumJournalRows)
        {
            issues.Add(ImportIssue(sheet.Name, 0, null, "TOO_MANY_JOURNALS", $"A workbook may contain at most {MaximumJournalRows:N0} journal rows."));
            lastRow = MaximumJournalRows + 1;
        }
        for (var row = 2; row <= lastRow; row++)
        {
            if (Enumerable.Range(1, 6).All(column => string.IsNullOrWhiteSpace(Text(sheet, row, column))))
                continue;
            var journal = new ImportJournal
            {
                ClientJournalKey = Text(sheet, row, 1),
                JournalType = string.IsNullOrWhiteSpace(Text(sheet, row, 3)) ? "General" : Text(sheet, row, 3),
                Description = Text(sheet, row, 4),
                Reference = NullIfEmpty(Text(sheet, row, 5)),
                Notes = NullIfEmpty(Text(sheet, row, 6)),
                SourceRow = row
            };
            if (!TryDate(sheet.Cell(row, 2), out var date))
                issues.Add(ImportIssue(sheet.Name, row, "TransactionDate", "INVALID_DATE", "TransactionDate must be a valid date.", Text(sheet, row, 2)));
            else
                journal.TransactionDate = date.Date;
            result.Add(journal);
        }
        return result;
    }

    private static List<ImportLine> ReadLines(
        IXLWorksheet sheet,
        ICollection<JournalBatchImportIssueDto> issues)
    {
        var result = new List<ImportLine>();
        var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.AllContents)?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumLineRows)
        {
            issues.Add(ImportIssue(sheet.Name, 0, null, "TOO_MANY_LINES", $"A workbook may contain at most {MaximumLineRows:N0} journal-line rows."));
            lastRow = MaximumLineRows + 1;
        }
        for (var row = 2; row <= lastRow; row++)
        {
            if (Enumerable.Range(1, 10).All(column => string.IsNullOrWhiteSpace(Text(sheet, row, column))))
                continue;
            var line = new ImportLine
            {
                ClientJournalKey = Text(sheet, row, 1),
                AccountNumber = Text(sheet, row, 3),
                TransactionType = Text(sheet, row, 4),
                CurrencyCode = NullIfEmpty(Text(sheet, row, 6)),
                Description = NullIfEmpty(Text(sheet, row, 9)),
                Reference = NullIfEmpty(Text(sheet, row, 10)),
                SourceRow = row
            };
            if (!int.TryParse(Text(sheet, row, 2), out var lineNumber) || lineNumber <= 0)
                issues.Add(ImportIssue(sheet.Name, row, "LineNumber", "INVALID_LINE_NUMBER", "LineNumber must be a positive whole number.", Text(sheet, row, 2)));
            else
                line.LineNumber = lineNumber;
            if (!TryDecimal(sheet.Cell(row, 5), out var amount) || amount <= 0)
                issues.Add(ImportIssue(sheet.Name, row, "Amount", "INVALID_AMOUNT", "Amount must be greater than zero.", Text(sheet, row, 5)));
            else
                line.Amount = decimal.Round(amount, 2);
            if (TryNullableDecimal(sheet.Cell(row, 7), out var foreignAmount))
                line.ForeignAmount = foreignAmount;
            else
                issues.Add(ImportIssue(sheet.Name, row, "ForeignAmount", "INVALID_FOREIGN_AMOUNT", "ForeignAmount must be numeric when provided.", Text(sheet, row, 7)));
            if (TryNullableDecimal(sheet.Cell(row, 8), out var exchangeRate))
                line.ExchangeRate = exchangeRate;
            else
                issues.Add(ImportIssue(sheet.Name, row, "ExchangeRate", "INVALID_EXCHANGE_RATE", "ExchangeRate must be numeric when provided.", Text(sheet, row, 8)));
            result.Add(line);
        }
        return result;
    }

    private async Task ValidatePayloadAsync(
        ImportPayload payload,
        ICollection<JournalBatchImportIssueDto> issues,
        CancellationToken cancellationToken)
    {
        if (payload.Batch.TemplateVersion != TemplateVersion)
            issues.Add(ImportIssue("Batch", 2, "TemplateVersion", "UNSUPPORTED_TEMPLATE_VERSION", $"TemplateVersion must be {TemplateVersion}.", payload.Batch.TemplateVersion));
        if (string.IsNullOrWhiteSpace(payload.Batch.Description))
            issues.Add(ImportIssue("Batch", 2, "Description", "DESCRIPTION_REQUIRED", "Batch description is required."));
        if (string.IsNullOrWhiteSpace(payload.Batch.BookClassification))
            issues.Add(ImportIssue("Batch", 2, "BookClassification", "BOOK_REQUIRED", "BookClassification is required."));
        if (payload.Batch.ControlCurrencyCode.Length != 3)
            issues.Add(ImportIssue("Batch", 2, "ControlCurrencyCode", "INVALID_CURRENCY", "ControlCurrencyCode must contain three characters.", payload.Batch.ControlCurrencyCode));
        var baseCurrency = await _context.Tenants
            .AsNoTracking()
            .Where(x => x.Id == TenantId)
            .Select(x => x.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseCurrency))
            issues.Add(ImportIssue("Batch", 2, "ControlCurrencyCode", "BASE_CURRENCY_NOT_CONFIGURED", "The tenant base currency is not configured."));
        else if (!string.Equals(payload.Batch.ControlCurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase))
            issues.Add(ImportIssue("Batch", 2, "ControlCurrencyCode", "CONTROL_CURRENCY_MISMATCH", $"ControlCurrencyCode must be the tenant base currency {baseCurrency}.", payload.Batch.ControlCurrencyCode));

        var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == payload.Batch.FiscalPeriodId && !x.IsDeleted,
            cancellationToken);
        if (period == null)
            issues.Add(ImportIssue("Batch", 2, "FiscalPeriodId", "PERIOD_NOT_FOUND", "Fiscal period was not found for this tenant."));
        else if (!period.IsOpen || period.IsClosed || period.IsLocked)
            issues.Add(ImportIssue("Batch", 2, "FiscalPeriodId", "PERIOD_NOT_OPEN", $"Fiscal period '{period.PeriodName}' is not open and unlocked."));

        if (payload.Journals.Count == 0)
            issues.Add(ImportIssue("JournalEntries", 0, null, "NO_JOURNALS", "At least one journal entry is required."));
        var duplicateKeys = payload.Journals
            .Where(x => !string.IsNullOrWhiteSpace(x.ClientJournalKey))
            .GroupBy(x => x.ClientJournalKey, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var journal in payload.Journals)
        {
            if (string.IsNullOrWhiteSpace(journal.ClientJournalKey))
                issues.Add(ImportIssue("JournalEntries", journal.SourceRow, "ClientJournalKey", "JOURNAL_KEY_REQUIRED", "ClientJournalKey is required."));
            else if (duplicateKeys.Contains(journal.ClientJournalKey))
                issues.Add(ImportIssue("JournalEntries", journal.SourceRow, "ClientJournalKey", "DUPLICATE_JOURNAL_KEY", "ClientJournalKey must be unique.", journal.ClientJournalKey));
            if (string.IsNullOrWhiteSpace(journal.Description))
                issues.Add(ImportIssue("JournalEntries", journal.SourceRow, "Description", "JOURNAL_DESCRIPTION_REQUIRED", "Journal description is required."));
            if (period != null && (journal.TransactionDate < period.StartDate.Date || journal.TransactionDate > period.EndDate.Date))
                issues.Add(ImportIssue("JournalEntries", journal.SourceRow, "TransactionDate", "DATE_OUTSIDE_PERIOD", $"Date must be inside fiscal period '{period.PeriodName}'.", journal.TransactionDate.ToString("yyyy-MM-dd")));
        }

        var accountNumbers = payload.Lines
            .Select(x => x.AccountNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var accounts = await _context.Accounts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && !x.IsDeleted && accountNumbers.Contains(x.AccountNumber))
            .ToDictionaryAsync(x => x.AccountNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var knownKeys = payload.Journals.Select(x => x.ClientJournalKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var line in payload.Lines)
        {
            if (!knownKeys.Contains(line.ClientJournalKey))
                issues.Add(ImportIssue("JournalLines", line.SourceRow, "ClientJournalKey", "UNKNOWN_JOURNAL_KEY", "ClientJournalKey does not exist on JournalEntries.", line.ClientJournalKey));
            if (!accounts.TryGetValue(line.AccountNumber, out var account))
                issues.Add(ImportIssue("JournalLines", line.SourceRow, "AccountNumber", "ACCOUNT_NOT_FOUND", "AccountNumber was not found for this tenant.", line.AccountNumber));
            else
            {
                line.AccountId = account.Id;
                if (account.Status != AccountStatus.Active || !account.AllowDirectPosting || account.IsControlAccount)
                    issues.Add(ImportIssue("JournalLines", line.SourceRow, "AccountNumber", "ACCOUNT_NOT_POSTABLE", "Account must be active, allow direct posting, and not be a control account.", line.AccountNumber));
            }
            if (!line.TransactionType.Equals("Debit", StringComparison.OrdinalIgnoreCase) &&
                !line.TransactionType.Equals("Credit", StringComparison.OrdinalIgnoreCase))
                issues.Add(ImportIssue("JournalLines", line.SourceRow, "TransactionType", "INVALID_TRANSACTION_TYPE", "TransactionType must be Debit or Credit.", line.TransactionType));
        }

        decimal batchDebit = 0;
        foreach (var journal in payload.Journals)
        {
            var lines = payload.Lines.Where(x => x.ClientJournalKey.Equals(journal.ClientJournalKey, StringComparison.OrdinalIgnoreCase)).ToList();
            if (lines.Count < 2)
                issues.Add(ImportIssue("JournalEntries", journal.SourceRow, "ClientJournalKey", "TOO_FEW_LINES", "Each journal requires at least two lines.", journal.ClientJournalKey));
            if (lines.GroupBy(x => x.LineNumber).Any(x => x.Count() > 1))
                issues.Add(ImportIssue("JournalLines", 0, "LineNumber", "DUPLICATE_LINE_NUMBER", $"Journal '{journal.ClientJournalKey}' contains duplicate line numbers."));
            var debit = lines.Where(x => x.TransactionType.Equals("Debit", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var credit = lines.Where(x => x.TransactionType.Equals("Credit", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            if (decimal.Round(debit, 2) != decimal.Round(credit, 2) || debit <= 0)
                issues.Add(ImportIssue("JournalEntries", journal.SourceRow, "ClientJournalKey", "JOURNAL_UNBALANCED", $"Journal '{journal.ClientJournalKey}' debit {debit:N2} does not equal credit {credit:N2}."));
            batchDebit += debit;
        }
        if (decimal.Round(batchDebit, 2) != decimal.Round(payload.Batch.ExpectedDebitTotal, 2))
            issues.Add(ImportIssue("Batch", 2, "ExpectedDebitTotal", "CONTROL_TOTAL_MISMATCH", $"Expected debit {payload.Batch.ExpectedDebitTotal:N2} does not equal imported debit {batchDebit:N2}."));
        if (payload.Batch.ExpectedJournalCount.HasValue && payload.Batch.ExpectedJournalCount.Value != payload.Journals.Count)
            issues.Add(ImportIssue("Batch", 2, "ExpectedJournalCount", "EXPECTED_COUNT_MISMATCH", $"Expected {payload.Batch.ExpectedJournalCount.Value} journals but imported {payload.Journals.Count}."));
    }

    private static string ComputeNormalizedPayloadHash(ImportPayload payload)
    {
        var canonical = new
        {
            batch = new
            {
                templateVersion = NormalizeForHash(payload.Batch.TemplateVersion, upperCase: true),
                description = NormalizeForHash(payload.Batch.Description),
                payload.Batch.FiscalPeriodId,
                bookClassification = NormalizeForHash(payload.Batch.BookClassification, upperCase: true),
                controlCurrencyCode = NormalizeForHash(payload.Batch.ControlCurrencyCode, upperCase: true),
                payload.Batch.ExpectedDebitTotal,
                payload.Batch.ExpectedJournalCount,
                notes = NormalizeOptionalForHash(payload.Batch.Notes)
            },
            journals = payload.Journals
                .OrderBy(journal => journal.ClientJournalKey, StringComparer.OrdinalIgnoreCase)
                .Select(journal => new
                {
                    clientJournalKey = NormalizeForHash(journal.ClientJournalKey, upperCase: true),
                    transactionDate = journal.TransactionDate.Date,
                    journalType = NormalizeForHash(journal.JournalType, upperCase: true),
                    description = NormalizeForHash(journal.Description),
                    reference = NormalizeOptionalForHash(journal.Reference),
                    notes = NormalizeOptionalForHash(journal.Notes)
                })
                .ToList(),
            lines = payload.Lines
                .OrderBy(line => line.ClientJournalKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(line => line.LineNumber)
                .Select(line => new
                {
                    clientJournalKey = NormalizeForHash(line.ClientJournalKey, upperCase: true),
                    line.LineNumber,
                    accountNumber = NormalizeForHash(line.AccountNumber, upperCase: true),
                    line.AccountId,
                    transactionType = NormalizeForHash(line.TransactionType, upperCase: true),
                    line.Amount,
                    currencyCode = NormalizeOptionalForHash(line.CurrencyCode, upperCase: true),
                    line.ForeignAmount,
                    line.ExchangeRate,
                    description = NormalizeOptionalForHash(line.Description),
                    reference = NormalizeOptionalForHash(line.Reference)
                })
                .ToList()
        };
        return HashSecret(JsonSerializer.Serialize(canonical, JsonOptions));
    }

    private static string HashSecret(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeHashEquals(string expectedHex, string actualHex)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHex),
                Convert.FromHexString(actualHex));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeForHash(string? value, bool upperCase = false)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return upperCase ? normalized.ToUpperInvariant() : normalized;
    }

    private static string? NormalizeOptionalForHash(string? value, bool upperCase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return NormalizeForHash(value, upperCase);
    }

    private static JournalBatchImportPreviewDto MapPreview(
        JournalBatchImportSession session,
        ImportPayload payload,
        IReadOnlyList<JournalBatchImportIssueDto> issues,
        string previewToken)
        => new()
        {
            SessionId = session.Id,
            PreviewToken = previewToken,
            ExpiresAt = session.ExpiresAt,
            IsValid = session.Status == JournalBatchImportStatus.Previewed,
            TemplateVersion = session.TemplateVersion,
            FileName = session.OriginalFileName,
            JournalCount = session.JournalCount,
            LineCount = session.LineCount,
            ExpectedDebitTotal = payload.Batch.ExpectedDebitTotal,
            ActualDebitTotal = payload.Journals.Sum(journal => payload.Lines
                .Where(line => line.ClientJournalKey.Equals(journal.ClientJournalKey, StringComparison.OrdinalIgnoreCase) &&
                               line.TransactionType.Equals("Debit", StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.Amount)),
            Issues = issues
        };

    private static JournalBatchImportIssueDto ImportIssue(
        string sheet,
        int row,
        string? column,
        string code,
        string message,
        string? value = null)
        => new()
        {
            Sheet = sheet,
            Row = row,
            Column = column,
            Code = code,
            Message = message,
            Value = value,
            Severity = "Error"
        };

    private static string Text(IXLWorksheet sheet, int row, int column)
        => sheet.Cell(row, column).GetFormattedString(CultureInfo.InvariantCulture).Trim();

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool TryDate(IXLCell cell, out DateTime date)
    {
        if (cell.TryGetValue(out date))
            return true;
        return DateTime.TryParse(
            cell.GetFormattedString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out date);
    }

    private static bool TryDecimal(IXLCell cell, out decimal result)
    {
        if (cell.TryGetValue(out result))
            return true;
        return decimal.TryParse(
            cell.GetFormattedString(CultureInfo.InvariantCulture),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool TryNullableDecimal(IXLCell cell, out decimal? result)
    {
        if (cell.IsEmpty())
        {
            result = null;
            return true;
        }
        if (TryDecimal(cell, out var parsed))
        {
            result = parsed;
            return true;
        }
        result = null;
        return false;
    }

    private static byte[] ToByteArray(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCellValue(
        IXLWorksheet worksheet,
        int row,
        int column,
        object? value)
    {
        var cell = worksheet.Cell(row, column);
        if (value == null)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value switch
        {
            Guid guid => guid.ToString(),
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            string text => text,
            bool boolean => boolean,
            byte number => number,
            short number => number,
            int number => number,
            long number => number,
            float number => number,
            double number => number,
            decimal number => number,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static void AdjustColumns(
        IXLWorksheet worksheet,
        double minimumWidth,
        double maximumWidth)
    {
        foreach (var column in worksheet.ColumnsUsed(XLCellsUsedOptions.AllContents))
        {
            var estimatedWidth = column.CellsUsed(XLCellsUsedOptions.AllContents)
                .Select(cell => cell.GetFormattedString(CultureInfo.InvariantCulture).Length)
                .DefaultIfEmpty((int)minimumWidth)
                .Max() + 2;
            column.Width = Math.Clamp(estimatedWidth, minimumWidth, maximumWidth);
        }
    }

    private sealed class ImportPayload
    {
        public ImportBatchHeader Batch { get; set; } = new();
        public List<ImportJournal> Journals { get; set; } = [];
        public List<ImportLine> Lines { get; set; } = [];
    }

    private sealed class ImportBatchHeader
    {
        public string TemplateVersion { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string BookClassification { get; set; } = string.Empty;
        public string ControlCurrencyCode { get; set; } = string.Empty;
        public decimal ExpectedDebitTotal { get; set; }
        public int? ExpectedJournalCount { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class ImportJournal
    {
        public string ClientJournalKey { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string JournalType { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public int SourceRow { get; set; }
    }

    private sealed class ImportLine
    {
        public string ClientJournalKey { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public Guid AccountId { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? CurrencyCode { get; set; }
        public decimal? ForeignAmount { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public int SourceRow { get; set; }
    }
}
