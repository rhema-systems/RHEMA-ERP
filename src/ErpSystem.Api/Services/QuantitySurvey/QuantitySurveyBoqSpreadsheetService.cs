using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ErpSystem.Api.Middleware;
using ErpSystem.Api.Services.Spreadsheets;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyBoqSpreadsheetService : IQuantitySurveyBoqSpreadsheetService
{
    private const string TemplateVersion = "1";
    private const int MaximumFileBytes = 15 * 1024 * 1024;
    private const int MaximumRows = 2_000;
    private const int HeaderRow = 6;
    private const int FirstDataRow = HeaderRow + 1;
    private const int LastColumn = 20;
    private const string ImportMode = "Import";
    private const string ExportMode = "ReadOnlyExport";
    private const string ReconciliationDeclaration =
        "I confirm that the staged BoQ lines, validation results, totals, and source workbook have been reviewed and reconciled before posting.";
    private static readonly string[] ItemTypes = ["Item", "ProvisionalSum", "PrimeCost", "Variation", "Allowance"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectService _projects;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ITimeLimitedDataProtector _templateProtector;

    public QuantitySurveyBoqSpreadsheetService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IProjectService projects,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _projects = projects;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _templateProtector = dataProtectionProvider
            .CreateProtector("ErpSystem.QuantitySurvey.BoqSpreadsheet.Template.v1")
            .ToTimeLimitedDataProtector();
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A valid tenant context is required.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required.");

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? UserId.ToString()
        : _currentUser.UserName.Trim();

    public async Task<QuantitySurveyBoqFileDto> CreateTemplateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadWorkbookContextAsync(projectId, cancellationToken);
        using var workbook = CreateWorkbook(context, ImportMode, []);
        return File(workbook, $"{SafeFileName(context.Project.ProjectCode)}-boq-import-v{TemplateVersion}.xlsx");
    }

    public async Task<QuantitySurveyBoqFileDto> ExportAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadWorkbookContextAsync(projectId, cancellationToken);
        var items = (await _projects.GetProjectBoqItemsAsync(projectId)).ToList();
        var rows = items.Select((item, index) => ImportLine.FromProjectItem(item, index + 1)).ToList();
        using var workbook = CreateWorkbook(context, ExportMode, rows);
        return File(workbook, $"{SafeFileName(context.Project.ProjectCode)}-boq-export-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    public async Task<QuantitySurveyBoqImportPreviewDto> PreviewAsync(
        Guid projectId,
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .xlsx BoQ workbooks are accepted.");

        var context = await LoadWorkbookContextAsync(projectId, cancellationToken);
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0 || memory.Length > MaximumFileBytes)
            throw new InvalidOperationException($"Workbook size must be between 1 byte and {MaximumFileBytes / 1024 / 1024} MB.");

        var bytes = memory.ToArray();
        SpreadsheetSecurityInspector.ValidateXlsxPackage(bytes, maximumWorksheets: 8);
        if (SpreadsheetSecurityInspector.HasVbaProject(bytes))
            throw new InvalidOperationException("VBA projects and macros are not allowed in BoQ workbooks.");
        if (SpreadsheetSecurityInspector.HasExternalRelationships(bytes))
            throw new InvalidOperationException("External links and external workbook relationships are not allowed.");

        var issues = new List<QuantitySurveyBoqImportIssueDto>();
        List<ImportLine> lines;
        using (var workbook = new XLWorkbook(new MemoryStream(bytes, writable: false)))
        {
            ValidateControlSheet(workbook, context, issues);
            lines = ReadLines(workbook, issues);
            RejectUnexpectedFormulas(workbook, lines, issues);
        }

        var resolved = await ValidateLinesAsync(projectId, lines, context, issues, cancellationToken);
        var normalizedJson = JsonSerializer.Serialize(lines, JsonOptions);
        var normalizedHash = Hash(normalizedJson);
        var previewToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sessionId = Guid.NewGuid();
        var fileHash = Convert.ToHexString(SHA256.HashData(bytes));

        var upload = await _controlledFiles.UploadAsync(
            new ControlledFileUploadRequest
            {
                TenantId = TenantId,
                ActorUserId = UserId,
                ActorName = UserName,
                Category = ControlledFileUploadCategories.QuantitySurveyBoqImport,
                FileName = Path.GetFileName(fileName),
                ContentType = string.IsNullOrWhiteSpace(contentType)
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : contentType,
                FileSize = bytes.LongLength,
                OpenReadStream = () => new MemoryStream(bytes, writable: false)
            },
            cancellationToken);

        CentralDocumentRepositoryLink document;
        try
        {
            document = await _centralDocuments.RegisterAsync(
                new CentralDocumentRepositoryRegistration
                {
                    TenantId = TenantId,
                    ActorUserId = UserId,
                    ActorName = UserName,
                    FileUploadRecordId = upload.Record.Id,
                    SourceModule = "QuantitySurvey",
                    SourceLabel = "Quantity Survey BoQ import staging",
                    SourceEntityType = nameof(QuantitySurveyBoqImportSession),
                    SourceRecordId = sessionId,
                    SourceRecordReference = context.Project.ProjectCode,
                    Title = $"{context.Project.ProjectCode} BoQ import - {upload.Record.OriginalFileName}",
                    DocumentType = "BoQImportWorkbook",
                    AccessProfile = "Module restricted",
                    VersionStatus = issues.Any(IsError) ? "Validation failed" : "Validated",
                    ChangeSummary = "Original BoQ workbook retained for controlled preview and signed reconciliation.",
                    RequirePublishedGovernance = false,
                    MetadataValues =
                    [
                        new("projectId", "Project ID", projectId.ToString(), "guid"),
                        new("projectCode", "Project code", context.Project.ProjectCode),
                        new("templateVersion", "Template version", TemplateVersion),
                        new("fileHash", "File SHA-256", fileHash),
                        new("validationStatus", "Validation status", issues.Any(IsError) ? "Invalid" : "Previewed"),
                        new("lineCount", "Line count", lines.Count.ToString(CultureInfo.InvariantCulture), "number")
                    ]
                },
                cancellationToken);
        }
        catch
        {
            await _controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            throw;
        }

        var session = new QuantitySurveyBoqImportSession
        {
            Id = sessionId,
            TenantId = TenantId,
            ProjectId = projectId,
            CentralDocumentRecordId = document.DocumentRecordId,
            CentralDocumentVersionId = document.DocumentVersionId,
            FileUploadRecordId = document.FileUploadRecordId,
            PreviewTokenHash = Hash(previewToken),
            FileHash = fileHash,
            NormalizedPayloadHash = normalizedHash,
            TemplateVersion = TemplateVersion,
            OriginalFileName = upload.Record.OriginalFileName,
            Status = issues.Any(IsError) ? QuantitySurveyBoqImportStatus.Invalid : QuantitySurveyBoqImportStatus.Previewed,
            UploadedByUserId = UserId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(45),
            LineCount = lines.Count,
            ErrorCount = issues.Count(IsError),
            NormalizedPayloadJson = normalizedJson,
            IssuesJson = JsonSerializer.Serialize(issues, JsonOptions),
            CreatedBy = UserName,
            CreatedById = UserId
        };

        try
        {
            _db.QuantitySurveyBoqImportSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, cancellationToken);
            throw;
        }

        return MapPreview(session, resolved, issues, previewToken);
    }

    public async Task<QuantitySurveyBoqImportCommitResultDto> CommitAsync(
        Guid projectId,
        Guid sessionId,
        CommitQuantitySurveyBoqImportDto request,
        CancellationToken cancellationToken = default)
    {
        if (!request.ReconciliationConfirmed)
            throw new InvalidOperationException("Confirm the signed reconciliation declaration before posting the staged BoQ.");
        var idempotencyKey = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new InvalidOperationException("An idempotency key is required.");

        await _projects.GetProjectBoqItemsAsync(projectId);
        var session = await _db.QuantitySurveyBoqImportSessions.FirstOrDefaultAsync(
            item => item.Id == sessionId && item.ProjectId == projectId && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The BoQ import preview was not found for this project and tenant.");

        if (session.UploadedByUserId != UserId)
            throw new UnauthorizedAccessException("Only the user who staged this workbook can sign and post it.");
        if (!FixedTimeEquals(session.PreviewTokenHash, Hash(request.PreviewToken ?? string.Empty)))
            throw new InvalidOperationException("The BoQ import preview token is invalid.");
        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            session.Status = QuantitySurveyBoqImportStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The BoQ import preview has expired. Preview the workbook again.");
        }
        if (session.Status == QuantitySurveyBoqImportStatus.Committed)
        {
            if (!string.Equals(session.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))
                throw new ConflictException("This import session was already posted with a different idempotency key.");
            return MapCommit(session);
        }
        if (session.Status != QuantitySurveyBoqImportStatus.Previewed || session.ErrorCount != 0)
            throw new InvalidOperationException("A workbook with blocking validation errors cannot be posted.");
        if (!FixedTimeEquals(Hash(session.NormalizedPayloadJson), session.NormalizedPayloadHash))
            throw new InvalidOperationException("The staged BoQ payload failed its integrity check. Preview the original workbook again.");

        var duplicate = await _db.QuantitySurveyBoqImportSessions.AsNoTracking().FirstOrDefaultAsync(
            item => item.TenantId == TenantId
                && item.IdempotencyKey == idempotencyKey
                && item.Status == QuantitySurveyBoqImportStatus.Committed
                && !item.IsDeleted,
            cancellationToken);
        if (duplicate != null)
        {
            if (duplicate.ProjectId != projectId
                || !FixedTimeEquals(duplicate.NormalizedPayloadHash, session.NormalizedPayloadHash))
                throw new ConflictException("The idempotency key was already used for a different BoQ payload.");
            return MapCommit(duplicate);
        }

        var lines = JsonSerializer.Deserialize<List<ImportLine>>(session.NormalizedPayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("The normalized BoQ import payload is unavailable.");
        var context = await LoadWorkbookContextAsync(projectId, cancellationToken);
        var revalidationIssues = new List<QuantitySurveyBoqImportIssueDto>();
        var resolved = await ValidateLinesAsync(projectId, lines, context, revalidationIssues, cancellationToken);
        if (revalidationIssues.Any(IsError))
        {
            session.Status = QuantitySurveyBoqImportStatus.Invalid;
            session.ErrorCount = revalidationIssues.Count(IsError);
            session.IssuesJson = JsonSerializer.Serialize(revalidationIssues, JsonOptions);
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Reference data or existing BoQ lines changed after preview. Download the validation report and preview again.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var line in resolved.OrderBy(item => item.Source.RowNumber))
            {
                await _projects.AddProjectBoqItemAsync(projectId, new CreateProjectBoqItemDto
                {
                    ProjectPackageId = line.PackageId,
                    SectionCatalogEntryId = line.SectionId,
                    TradeCatalogEntryId = line.TradeId,
                    CostCodeCatalogEntryId = line.CostCodeId,
                    MeasurementCodeCatalogEntryId = line.MeasurementCodeId,
                    LineNumber = line.Source.LineNumber,
                    ItemCode = line.Source.ItemCode,
                    ItemType = line.Source.ItemType,
                    Description = line.Source.Description,
                    Quantity = line.Source.Quantity,
                    UnitOfMeasure = line.Source.UnitOfMeasure,
                    UnitRate = line.Source.UnitRate,
                    Currency = line.Source.Currency,
                    Notes = line.Source.Notes,
                    SortOrder = line.Source.SortOrder
                });
            }

            var now = DateTime.UtcNow;
            session.Status = QuantitySurveyBoqImportStatus.Committed;
            session.IdempotencyKey = idempotencyKey;
            session.CommittedLineCount = resolved.Count;
            session.ReconciledByUserId = UserId;
            session.ReconciledAt = now;
            session.ReconciliationDeclaration = ReconciliationDeclaration;
            session.CommittedAt = now;
            session.UpdatedAt = now;
            session.UpdatedBy = UserName;
            session.LastModifiedById = UserId;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapCommit(session);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            var committed = await _db.QuantitySurveyBoqImportSessions.AsNoTracking().FirstOrDefaultAsync(
                item => item.TenantId == TenantId
                    && item.IdempotencyKey == idempotencyKey
                    && item.Status == QuantitySurveyBoqImportStatus.Committed
                    && !item.IsDeleted,
                cancellationToken);
            if (committed != null)
            {
                if (committed.ProjectId != projectId
                    || !FixedTimeEquals(committed.NormalizedPayloadHash, session.NormalizedPayloadHash))
                    throw new ConflictException("The idempotency key was already used for a different BoQ payload.");
                return MapCommit(committed);
            }
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<QuantitySurveyBoqFileDto> CreateErrorWorkbookAsync(
        Guid projectId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await _projects.GetProjectBoqItemsAsync(projectId);
        var session = await _db.QuantitySurveyBoqImportSessions.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == sessionId && item.ProjectId == projectId && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The BoQ import preview was not found.");
        if (session.UploadedByUserId != UserId)
            throw new UnauthorizedAccessException("Only the user who staged this workbook can download its validation report.");

        var issues = JsonSerializer.Deserialize<List<QuantitySurveyBoqImportIssueDto>>(session.IssuesJson, JsonOptions) ?? [];
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Validation Issues");
        var headers = new[] { "Severity", "Code", "Row", "Client Line Key", "Field", "Message" };
        for (var column = 1; column <= headers.Length; column++)
            sheet.Cell(1, column).Value = headers[column - 1];
        for (var index = 0; index < issues.Count; index++)
        {
            var row = index + 2;
            var issue = issues[index];
            sheet.Cell(row, 1).Value = issue.Severity;
            sheet.Cell(row, 2).Value = issue.Code;
            sheet.Cell(row, 3).Value = issue.RowNumber;
            sheet.Cell(row, 4).Value = issue.ClientLineKey;
            sheet.Cell(row, 5).Value = issue.Field;
            sheet.Cell(row, 6).Value = issue.Message;
        }
        StyleHeader(sheet.Range(1, 1, 1, headers.Length));
        sheet.Columns().AdjustToContents(1, 80);
        sheet.Column(6).Width = 80;
        sheet.Column(6).Style.Alignment.WrapText = true;
        return File(workbook, $"boq-import-{sessionId:N}-validation.xlsx");
    }

    private async Task<WorkbookContext> LoadWorkbookContextAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await _projects.GetProjectBoqItemsAsync(projectId);
        var project = await _db.Projects.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == projectId && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The project was not found for this tenant.");
        var packages = await _db.ProjectPackages.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == TenantId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code).ToListAsync(cancellationToken);
        var effectiveAt = DateTime.UtcNow;
        var catalogTypes = new[]
        {
            ProjectCatalogDefaults.QuantitySurveySections,
            ProjectCatalogDefaults.QuantitySurveyTrades,
            ProjectCatalogDefaults.QuantitySurveyCostCodes,
            ProjectCatalogDefaults.QuantitySurveyMeasurementCodes
        };
        var catalogs = await _db.ProjectCatalogEntries.AsNoTracking()
            .Where(item => item.TenantId == TenantId
                && item.IsActive
                && catalogTypes.Contains(item.CatalogType)
                && (!item.EffectiveFrom.HasValue || item.EffectiveFrom <= effectiveAt)
                && (!item.EffectiveTo.HasValue || item.EffectiveTo >= effectiveAt)
                && !item.IsDeleted)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code).ToListAsync(cancellationToken);
        var units = await _db.UnitsOfMeasure.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code).ToListAsync(cancellationToken);
        var currencies = await _db.Currencies.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.CurrencyCode)
            .Select(item => item.CurrencyCode).ToListAsync(cancellationToken);
        return new WorkbookContext(project, packages, catalogs, units.Select(item => item.Code).ToList(), currencies);
    }

    private XLWorkbook CreateWorkbook(WorkbookContext context, string mode, IReadOnlyList<ImportLine> rows)
    {
        var workbook = new XLWorkbook();
        AddInstructions(workbook, context, mode);
        AddBoqSheet(workbook, context, mode, rows);
        AddLookups(workbook, context);
        AddControlSheet(workbook, context, mode);
        return workbook;
    }

    private void AddInstructions(XLWorkbook workbook, WorkbookContext context, string mode)
    {
        var sheet = workbook.Worksheets.Add("Instructions");
        sheet.Cell("A1").Value = mode == ImportMode ? "TDC Quantity Survey BoQ Import" : "TDC Quantity Survey BoQ Export";
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 16;
        var instructions = mode == ImportMode
            ? new[]
            {
                "Enter BoQ lines only on the 'BOQ Lines' sheet.",
                "Use controlled codes from the Lookups sheet. Do not paste formulas, macros, or external links.",
                "Template Version, Project ID, control data, and Line Total formulas are locked and verified by the server.",
                "Preview validates codes, units, quantities, rates, duplicates, formulas, and existing project lines before anything is posted.",
                "A successful preview must be signed by the same authenticated user through the reconciliation confirmation before posting.",
                "The original workbook and checksum are retained in the central DMS."
            }
            : new[]
            {
                "This is a read-only controlled export of the current project BoQ.",
                "It cannot be posted as an import. Download a fresh import template for staged changes.",
                "The export is protected to preserve controlled classifications and calculated totals."
            };
        sheet.Cell("A3").Value = "Project";
        sheet.Cell("B3").Value = $"{context.Project.ProjectCode} - {context.Project.Title}";
        sheet.Cell("A4").Value = "Generated UTC";
        sheet.Cell("B4").Value = DateTime.UtcNow;
        for (var index = 0; index < instructions.Length; index++)
            sheet.Cell(index + 6, 1).Value = $"{index + 1}. {instructions[index]}";
        sheet.Column(1).Width = 120;
        sheet.Column(1).Style.Alignment.WrapText = true;
        sheet.Column(2).Width = 50;
    }

    private void AddBoqSheet(XLWorkbook workbook, WorkbookContext context, string mode, IReadOnlyList<ImportLine> rows)
    {
        var sheet = workbook.Worksheets.Add("BOQ Lines");
        sheet.Cell(1, 1).Value = $"{context.Project.ProjectCode} - {context.Project.Title}";
        sheet.Cell(2, 1).Value = mode == ImportMode ? "Controlled import staging" : "Read-only current BoQ export";
        sheet.Cell(3, 1).Value = "Use codes exactly as listed on the Lookups sheet.";
        var headers = new[]
        {
            "Template Version", "Project ID", "Client Line Key", "Package Code", "Section Code", "Trade Code",
            "Cost Code", "Measurement Standard", "Measurement Code", "Line Number", "Item Code", "Item Type",
            "Description", "Unit", "Quantity", "Unit Rate", "Currency", "Sort Order", "Notes", "Line Total"
        };
        for (var column = 1; column <= headers.Length; column++)
            sheet.Cell(HeaderRow, column).Value = headers[column - 1];
        StyleHeader(sheet.Range(HeaderRow, 1, HeaderRow, headers.Length));

        var populatedRows = Math.Max(rows.Count, mode == ImportMode ? MaximumRows : 1);
        for (var index = 0; index < populatedRows; index++)
        {
            var row = FirstDataRow + index;
            sheet.Cell(row, 1).Value = TemplateVersion;
            sheet.Cell(row, 2).Value = context.Project.Id.ToString();
            if (index < rows.Count)
                WriteLine(sheet, row, rows[index]);
            sheet.Cell(row, LastColumn).FormulaA1 = $"IF(OR(O{row}=\"\",P{row}=\"\"),\"\",O{row}*P{row})";
        }

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Range(FirstDataRow, 1, FirstDataRow + populatedRows - 1, LastColumn).Style.Protection.Locked = true;
        if (mode == ImportMode)
            sheet.Range(FirstDataRow, 3, FirstDataRow + populatedRows - 1, LastColumn - 1).Style.Protection.Locked = false;
        sheet.Protect("TDC-QS-BOQ");
        sheet.Column(1).Hide();
        sheet.Column(2).Hide();
        sheet.Columns(3, LastColumn).AdjustToContents(1, 60);
        sheet.Column(13).Width = 55;
        sheet.Column(19).Width = 45;
        sheet.Columns(13, 19).Style.Alignment.WrapText = true;
        sheet.Range(FirstDataRow, 15, FirstDataRow + populatedRows - 1, 16).Style.NumberFormat.Format = "0.0000";
        sheet.Range(FirstDataRow, 20, FirstDataRow + populatedRows - 1, 20).Style.NumberFormat.Format = "0.00";
    }

    private static void WriteLine(IXLWorksheet sheet, int row, ImportLine line)
    {
        sheet.Cell(row, 3).Value = line.ClientLineKey;
        sheet.Cell(row, 4).Value = line.PackageCode;
        sheet.Cell(row, 5).Value = line.SectionCode;
        sheet.Cell(row, 6).Value = line.TradeCode;
        sheet.Cell(row, 7).Value = line.CostCode;
        sheet.Cell(row, 8).Value = line.MeasurementStandard;
        sheet.Cell(row, 9).Value = line.MeasurementCode;
        sheet.Cell(row, 10).Value = line.LineNumber;
        sheet.Cell(row, 11).Value = line.ItemCode;
        sheet.Cell(row, 12).Value = line.ItemType;
        sheet.Cell(row, 13).Value = line.Description;
        sheet.Cell(row, 14).Value = line.UnitOfMeasure;
        sheet.Cell(row, 15).Value = line.Quantity;
        sheet.Cell(row, 16).Value = line.UnitRate;
        sheet.Cell(row, 17).Value = line.Currency;
        sheet.Cell(row, 18).Value = line.SortOrder;
        sheet.Cell(row, 19).Value = line.Notes;
    }

    private static void AddLookups(XLWorkbook workbook, WorkbookContext context)
    {
        var sheet = workbook.Worksheets.Add("Lookups");
        var groups = new List<(string Header, IEnumerable<string> Values)>
        {
            ("Package Code", context.Packages.Where(item => !string.IsNullOrWhiteSpace(item.Code)).Select(item => item.Code!)),
            ("Section Code", context.Catalogs.Where(item => item.CatalogType == ProjectCatalogDefaults.QuantitySurveySections).Select(item => item.Code)),
            ("Trade Code", context.Catalogs.Where(item => item.CatalogType == ProjectCatalogDefaults.QuantitySurveyTrades).Select(item => item.Code)),
            ("Cost Code", context.Catalogs.Where(item => item.CatalogType == ProjectCatalogDefaults.QuantitySurveyCostCodes).Select(item => item.Code)),
            ("Measurement Standard", context.Catalogs
                .Where(item => item.CatalogType == ProjectCatalogDefaults.QuantitySurveyMeasurementCodes && !string.IsNullOrWhiteSpace(item.StandardCode))
                .Select(item => item.StandardCode!)),
            ("Measurement Code", context.Catalogs.Where(item => item.CatalogType == ProjectCatalogDefaults.QuantitySurveyMeasurementCodes).Select(item => $"{item.StandardCode}:{item.Code}")),
            ("Unit", context.Units),
            ("Currency", context.Currencies),
            ("Item Type", ItemTypes)
        };
        for (var column = 1; column <= groups.Count; column++)
        {
            sheet.Cell(1, column).Value = groups[column - 1].Header;
            var values = groups[column - 1].Values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
            for (var index = 0; index < values.Count; index++)
                sheet.Cell(index + 2, column).Value = values[index];
        }
        StyleHeader(sheet.Range(1, 1, 1, groups.Count));
        sheet.Columns().AdjustToContents(1, 45);
        sheet.Protect("TDC-QS-LOOKUPS");
    }

    private void AddControlSheet(XLWorkbook workbook, WorkbookContext context, string mode)
    {
        var sheet = workbook.Worksheets.Add("Control");
        var control = new TemplateControl(TenantId, context.Project.Id, TemplateVersion, mode, DateTime.UtcNow);
        var protectedValue = _templateProtector.Protect(JsonSerializer.Serialize(control, JsonOptions), TimeSpan.FromDays(30));
        sheet.Cell("A1").Value = "TemplateVersion";
        sheet.Cell("B1").Value = TemplateVersion;
        sheet.Cell("A2").Value = "ProjectId";
        sheet.Cell("B2").Value = context.Project.Id.ToString();
        sheet.Cell("A3").Value = "Mode";
        sheet.Cell("B3").Value = mode;
        sheet.Cell("A4").Value = "ControlToken";
        sheet.Cell("B4").Value = protectedValue;
        sheet.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    private void ValidateControlSheet(XLWorkbook workbook, WorkbookContext context, ICollection<QuantitySurveyBoqImportIssueDto> issues)
    {
        if (!workbook.TryGetWorksheet("Control", out var sheet))
        {
            AddIssue(issues, null, null, "Workbook", "CONTROL_MISSING", "The protected template control sheet is missing.");
            return;
        }
        var version = sheet.Cell("B1").GetString().Trim();
        var projectValue = sheet.Cell("B2").GetString().Trim();
        var mode = sheet.Cell("B3").GetString().Trim();
        var token = sheet.Cell("B4").GetString();
        if (version != TemplateVersion)
            AddIssue(issues, null, null, "TemplateVersion", "TEMPLATE_VERSION", $"Template version '{version}' is not supported.");
        if (!Guid.TryParse(projectValue, out var projectId) || projectId != context.Project.Id)
            AddIssue(issues, null, null, "ProjectId", "PROJECT_MISMATCH", "The workbook belongs to a different project.");
        if (!string.Equals(mode, ImportMode, StringComparison.Ordinal))
            AddIssue(issues, null, null, "Workbook", "READ_ONLY_EXPORT", "A read-only export cannot be posted. Use a fresh import template.");

        try
        {
            var json = _templateProtector.Unprotect(token, out _);
            var control = JsonSerializer.Deserialize<TemplateControl>(json, JsonOptions);
            if (control == null
                || control.TenantId != TenantId
                || control.ProjectId != context.Project.Id
                || control.TemplateVersion != TemplateVersion
                || control.Mode != ImportMode)
                AddIssue(issues, null, null, "Workbook", "CONTROL_TAMPERED", "The protected template controls do not match this tenant, project, or import mode.");
        }
        catch (CryptographicException)
        {
            AddIssue(issues, null, null, "Workbook", "CONTROL_INVALID", "The protected template control is invalid or expired. Download a fresh template.");
        }
    }

    private static List<ImportLine> ReadLines(XLWorkbook workbook, ICollection<QuantitySurveyBoqImportIssueDto> issues)
    {
        if (!workbook.TryGetWorksheet("BOQ Lines", out var sheet))
        {
            AddIssue(issues, null, null, "Workbook", "SHEET_MISSING", "The 'BOQ Lines' worksheet is missing.");
            return [];
        }
        var requiredHeaders = new[]
        {
            "Template Version", "Project ID", "Client Line Key", "Package Code", "Section Code", "Trade Code",
            "Cost Code", "Measurement Standard", "Measurement Code", "Line Number", "Item Code", "Item Type",
            "Description", "Unit", "Quantity", "Unit Rate", "Currency", "Sort Order", "Notes", "Line Total"
        };
        for (var column = 1; column <= requiredHeaders.Length; column++)
        {
            if (!string.Equals(sheet.Cell(HeaderRow, column).GetString().Trim(), requiredHeaders[column - 1], StringComparison.Ordinal))
                AddIssue(issues, HeaderRow, null, requiredHeaders[column - 1], "HEADER_MISMATCH", $"Column {column} must be '{requiredHeaders[column - 1]}'.");
        }

        var lastUsed = Math.Max(sheet.LastRowUsed()?.RowNumber() ?? HeaderRow, HeaderRow);
        if (lastUsed - HeaderRow > MaximumRows)
            AddIssue(issues, null, null, "Workbook", "ROW_LIMIT", $"The workbook exceeds the {MaximumRows:N0}-line limit.");
        var lines = new List<ImportLine>();
        for (var row = FirstDataRow; row <= Math.Min(lastUsed, HeaderRow + MaximumRows); row++)
        {
            if (Enumerable.Range(3, 17).All(column => sheet.Cell(row, column).IsEmpty()))
                continue;
            var clientKey = Text(sheet.Cell(row, 3));
            var quantity = Decimal(sheet.Cell(row, 15), issues, row, clientKey, "Quantity", required: true) ?? 0;
            var unitRate = Decimal(sheet.Cell(row, 16), issues, row, clientKey, "Unit Rate", required: false);
            var sortOrder = Integer(sheet.Cell(row, 18), issues, row, clientKey, "Sort Order");
            lines.Add(new ImportLine
            {
                RowNumber = row,
                TemplateVersion = Text(sheet.Cell(row, 1)),
                ProjectId = Text(sheet.Cell(row, 2)),
                ClientLineKey = clientKey,
                PackageCode = Text(sheet.Cell(row, 4)),
                SectionCode = NullIfEmpty(Text(sheet.Cell(row, 5))),
                TradeCode = NullIfEmpty(Text(sheet.Cell(row, 6))),
                CostCode = NullIfEmpty(Text(sheet.Cell(row, 7))),
                MeasurementStandard = NullIfEmpty(Text(sheet.Cell(row, 8))),
                MeasurementCode = NullIfEmpty(Text(sheet.Cell(row, 9))),
                LineNumber = NullIfEmpty(Text(sheet.Cell(row, 10))),
                ItemCode = NullIfEmpty(Text(sheet.Cell(row, 11))),
                ItemType = Text(sheet.Cell(row, 12)),
                Description = Text(sheet.Cell(row, 13)),
                UnitOfMeasure = Text(sheet.Cell(row, 14)),
                Quantity = quantity,
                UnitRate = unitRate,
                Currency = Text(sheet.Cell(row, 17)),
                SortOrder = sortOrder,
                Notes = NullIfEmpty(Text(sheet.Cell(row, 19)))
            });
        }
        if (lines.Count == 0)
            AddIssue(issues, null, null, "Workbook", "NO_LINES", "Enter at least one BoQ line before previewing the workbook.");
        return lines;
    }

    private static void RejectUnexpectedFormulas(
        XLWorkbook workbook,
        IReadOnlyList<ImportLine> lines,
        ICollection<QuantitySurveyBoqImportIssueDto> issues)
    {
        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var cell in worksheet.CellsUsed(cell => cell.HasFormula))
            {
                if (worksheet.Name == "BOQ Lines"
                    && cell.Address.ColumnNumber == LastColumn
                    && cell.Address.RowNumber >= FirstDataRow
                    && IsExpectedTotalFormula(cell.Address.RowNumber, cell.FormulaA1))
                    continue;
                AddIssue(issues, cell.Address.RowNumber, null, cell.Address.ToString(), "UNSAFE_FORMULA", $"Unexpected formula found in '{worksheet.Name}'!{cell.Address}.");
            }
        }
        if (!workbook.TryGetWorksheet("BOQ Lines", out var boqSheet))
            return;
        foreach (var line in lines)
        {
            var formula = boqSheet.Cell(line.RowNumber, LastColumn).FormulaA1;
            if (!IsExpectedTotalFormula(line.RowNumber, formula))
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Line Total", "LOCKED_FORMULA_CHANGED", "The locked Line Total formula was removed or changed.");
            if (boqSheet.Cell(line.RowNumber, 1).GetString().Trim() != TemplateVersion)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Template Version", "LOCKED_CELL_CHANGED", "The locked template version was changed.");
        }
    }

    public static bool IsExpectedTotalFormula(int row, string? formula)
    {
        var normalized = (formula ?? string.Empty).Replace(" ", string.Empty).TrimStart('=').ToUpperInvariant();
        return normalized == $"IF(OR(O{row}=\"\",P{row}=\"\"),\"\",O{row}*P{row})";
    }

    private async Task<List<ResolvedLine>> ValidateLinesAsync(
        Guid projectId,
        IReadOnlyList<ImportLine> lines,
        WorkbookContext context,
        ICollection<QuantitySurveyBoqImportIssueDto> issues,
        CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedLine>();
        var packages = UniqueLookup(context.Packages.Where(item => !string.IsNullOrWhiteSpace(item.Code)), item => item.Code!);
        var sections = CatalogLookup(context, ProjectCatalogDefaults.QuantitySurveySections, includeStandard: false);
        var trades = CatalogLookup(context, ProjectCatalogDefaults.QuantitySurveyTrades, includeStandard: false);
        var costs = CatalogLookup(context, ProjectCatalogDefaults.QuantitySurveyCostCodes, includeStandard: false);
        var measurements = CatalogLookup(context, ProjectCatalogDefaults.QuantitySurveyMeasurementCodes, includeStandard: true);
        var units = context.Units.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currencies = context.Currencies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await _db.ProjectBoqItems.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.TenantId == TenantId && !item.IsDeleted)
            .Select(item => new { item.ProjectPackageId, item.LineNumber, item.ItemCode })
            .ToListAsync(cancellationToken);

        foreach (var duplicate in lines.Where(item => !string.IsNullOrWhiteSpace(item.ClientLineKey)).GroupBy(item => Normalize(item.ClientLineKey)).Where(group => group.Count() > 1))
            foreach (var line in duplicate)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Client Line Key", "DUPLICATE_CLIENT_KEY", $"Client line key '{line.ClientLineKey}' occurs more than once.");

        foreach (var line in lines)
        {
            if (line.TemplateVersion != TemplateVersion)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Template Version", "LOCKED_CELL_CHANGED", "The locked template version was changed.");
            if (!Guid.TryParse(line.ProjectId, out var lineProjectId) || lineProjectId != projectId)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Project ID", "LOCKED_CELL_CHANGED", "The locked project identifier was changed.");
            if (string.IsNullOrWhiteSpace(line.ClientLineKey))
                AddIssue(issues, line.RowNumber, null, "Client Line Key", "REQUIRED", "Client Line Key is required and must be unique within the workbook.");
            if (string.IsNullOrWhiteSpace(line.Description))
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Description", "REQUIRED", "Description is required.");
            if (line.Description.Length > 1000)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Description", "MAX_LENGTH", "Description cannot exceed 1,000 characters.");
            if (line.Quantity <= 0)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Quantity", "QUANTITY", "Quantity must be greater than zero.");
            if (line.UnitRate < 0)
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Unit Rate", "UNIT_RATE", "Unit Rate cannot be negative.");
            if (!ItemTypes.Contains(line.ItemType, StringComparer.OrdinalIgnoreCase))
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Item Type", "ITEM_TYPE", $"Item Type must be one of: {string.Join(", ", ItemTypes)}.");
            if (!units.Contains(line.UnitOfMeasure))
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Unit", "UNIT_NOT_FOUND", $"Unit '{line.UnitOfMeasure}' is not an active tenant unit of measure.");
            if (!currencies.Contains(line.Currency))
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Currency", "CURRENCY_NOT_FOUND", $"Currency '{line.Currency}' is not an active tenant currency.");

            var package = Resolve(packages, line.PackageCode, line, "Package Code", "PACKAGE_NOT_FOUND", issues);
            var section = ResolveOptional(sections, line.SectionCode, line, "Section Code", "SECTION_NOT_FOUND", issues);
            var trade = ResolveOptional(trades, line.TradeCode, line, "Trade Code", "TRADE_NOT_FOUND", issues);
            var cost = ResolveOptional(costs, line.CostCode, line, "Cost Code", "COST_CODE_NOT_FOUND", issues);
            ProjectCatalogEntry? measurement = null;
            if (!string.IsNullOrWhiteSpace(line.MeasurementCode))
            {
                if (string.IsNullOrWhiteSpace(line.MeasurementStandard))
                    AddIssue(issues, line.RowNumber, line.ClientLineKey, "Measurement Standard", "REQUIRED", "Measurement Standard is required when Measurement Code is supplied.");
                else
                    measurement = ResolveOptional(measurements, $"{line.MeasurementStandard}:{line.MeasurementCode}", line, "Measurement Code", "MEASUREMENT_NOT_FOUND", issues);
            }
            else if (!string.IsNullOrWhiteSpace(line.MeasurementStandard))
                AddIssue(issues, line.RowNumber, line.ClientLineKey, "Measurement Code", "REQUIRED", "Measurement Code is required when Measurement Standard is supplied.");

            if (package != null)
            {
                if (!string.IsNullOrWhiteSpace(line.LineNumber)
                    && (lines.Any(other => other.RowNumber != line.RowNumber && Normalize(other.PackageCode) == Normalize(line.PackageCode) && Normalize(other.LineNumber) == Normalize(line.LineNumber))
                        || existing.Any(item => item.ProjectPackageId == package.Id && Normalize(item.LineNumber) == Normalize(line.LineNumber))))
                    AddIssue(issues, line.RowNumber, line.ClientLineKey, "Line Number", "DUPLICATE_LINE", $"Line number '{line.LineNumber}' already exists for package '{line.PackageCode}'.");
                if (!string.IsNullOrWhiteSpace(line.ItemCode)
                    && (lines.Any(other => other.RowNumber != line.RowNumber && Normalize(other.PackageCode) == Normalize(line.PackageCode) && Normalize(other.ItemCode) == Normalize(line.ItemCode))
                        || existing.Any(item => item.ProjectPackageId == package.Id && Normalize(item.ItemCode) == Normalize(line.ItemCode))))
                    AddIssue(issues, line.RowNumber, line.ClientLineKey, "Item Code", "DUPLICATE_ITEM", $"Item code '{line.ItemCode}' already exists for package '{line.PackageCode}'.");
                resolved.Add(new ResolvedLine(line, package.Id, section?.Id, trade?.Id, cost?.Id, measurement?.Id));
            }
        }
        return resolved;
    }

    private static Dictionary<string, T> UniqueLookup<T>(IEnumerable<T> values, Func<T, string> key)
        => values.GroupBy(item => Normalize(key(item))).Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());

    private static Dictionary<string, ProjectCatalogEntry> CatalogLookup(WorkbookContext context, string type, bool includeStandard)
        => UniqueLookup(
            context.Catalogs.Where(item => item.CatalogType == type),
            item => includeStandard ? $"{item.StandardCode}:{item.Code}" : item.Code);

    private static T? Resolve<T>(
        IReadOnlyDictionary<string, T> lookup,
        string? code,
        ImportLine line,
        string field,
        string issueCode,
        ICollection<QuantitySurveyBoqImportIssueDto> issues) where T : class
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            AddIssue(issues, line.RowNumber, line.ClientLineKey, field, "REQUIRED", $"{field} is required.");
            return null;
        }
        if (lookup.TryGetValue(Normalize(code), out var value))
            return value;
        AddIssue(issues, line.RowNumber, line.ClientLineKey, field, issueCode, $"{field} '{code}' is unavailable, inactive, ambiguous, or not effective.");
        return null;
    }

    private static T? ResolveOptional<T>(
        IReadOnlyDictionary<string, T> lookup,
        string? code,
        ImportLine line,
        string field,
        string issueCode,
        ICollection<QuantitySurveyBoqImportIssueDto> issues) where T : class
        => string.IsNullOrWhiteSpace(code) ? null : Resolve(lookup, code, line, field, issueCode, issues);

    private static QuantitySurveyBoqImportPreviewDto MapPreview(
        QuantitySurveyBoqImportSession session,
        IReadOnlyList<ResolvedLine> lines,
        IReadOnlyList<QuantitySurveyBoqImportIssueDto> issues,
        string token) => new()
        {
            SessionId = session.Id,
            ProjectId = session.ProjectId,
            PreviewToken = token,
            Status = session.Status.ToString(),
            ExpiresAt = session.ExpiresAt,
            LineCount = session.LineCount,
            ErrorCount = session.ErrorCount,
            CentralDocumentRecordId = session.CentralDocumentRecordId,
            CentralDocumentVersionId = session.CentralDocumentVersionId,
            Lines = lines.Select(line => new QuantitySurveyBoqImportLinePreviewDto
            {
                RowNumber = line.Source.RowNumber,
                ClientLineKey = line.Source.ClientLineKey,
                PackageCode = line.Source.PackageCode,
                SectionCode = line.Source.SectionCode,
                TradeCode = line.Source.TradeCode,
                CostCode = line.Source.CostCode,
                MeasurementStandard = line.Source.MeasurementStandard,
                MeasurementCode = line.Source.MeasurementCode,
                LineNumber = line.Source.LineNumber,
                ItemCode = line.Source.ItemCode,
                ItemType = line.Source.ItemType,
                Description = line.Source.Description,
                UnitOfMeasure = line.Source.UnitOfMeasure,
                Quantity = line.Source.Quantity,
                UnitRate = line.Source.UnitRate,
                Currency = line.Source.Currency,
                LineTotal = line.Source.UnitRate.HasValue ? line.Source.Quantity * line.Source.UnitRate.Value : null
            }).ToList(),
            Issues = issues
        };

    private QuantitySurveyBoqImportCommitResultDto MapCommit(QuantitySurveyBoqImportSession session) => new()
    {
        SessionId = session.Id,
        ProjectId = session.ProjectId,
        CommittedLineCount = session.CommittedLineCount,
        CommittedAt = session.CommittedAt ?? DateTime.UtcNow,
        ReconciledBy = session.ReconciledByUserId == UserId ? UserName : session.UpdatedBy ?? "Authenticated user",
        CentralDocumentRecordId = session.CentralDocumentRecordId
    };

    private static QuantitySurveyBoqFileDto File(XLWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new QuantitySurveyBoqFileDto { Content = stream.ToArray(), FileName = fileName };
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
    }

    private static decimal? Decimal(
        IXLCell cell,
        ICollection<QuantitySurveyBoqImportIssueDto> issues,
        int row,
        string? key,
        string field,
        bool required)
    {
        if (cell.IsEmpty())
        {
            if (required) AddIssue(issues, row, key, field, "REQUIRED", $"{field} is required.");
            return null;
        }
        if (cell.TryGetValue<decimal>(out var value)) return value;
        AddIssue(issues, row, key, field, "NUMBER", $"{field} must be a number.");
        return null;
    }

    private static int? Integer(
        IXLCell cell,
        ICollection<QuantitySurveyBoqImportIssueDto> issues,
        int row,
        string? key,
        string field)
    {
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<int>(out var value) && value >= 0) return value;
        AddIssue(issues, row, key, field, "INTEGER", $"{field} must be a non-negative whole number.");
        return null;
    }

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool FixedTimeEquals(string left, string right)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right)); }
        catch (FormatException) { return false; }
    }
    private static bool IsError(QuantitySurveyBoqImportIssueDto issue) => issue.Severity == "Error";
    private static string SafeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
    private static void AddIssue(
        ICollection<QuantitySurveyBoqImportIssueDto> issues,
        int? row,
        string? key,
        string? field,
        string code,
        string message) => issues.Add(new QuantitySurveyBoqImportIssueDto
        {
            RowNumber = row,
            ClientLineKey = key,
            Field = field,
            Code = code,
            Message = message
        });

    private sealed record TemplateControl(Guid TenantId, Guid ProjectId, string TemplateVersion, string Mode, DateTime IssuedAtUtc);
    private sealed record WorkbookContext(
        Project Project,
        IReadOnlyList<ProjectPackage> Packages,
        IReadOnlyList<ProjectCatalogEntry> Catalogs,
        IReadOnlyList<string> Units,
        IReadOnlyList<string> Currencies);
    private sealed record ResolvedLine(
        ImportLine Source,
        Guid PackageId,
        Guid? SectionId,
        Guid? TradeId,
        Guid? CostCodeId,
        Guid? MeasurementCodeId);

    private sealed class ImportLine
    {
        public int RowNumber { get; set; }
        public string TemplateVersion { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string ClientLineKey { get; set; } = string.Empty;
        public string PackageCode { get; set; } = string.Empty;
        public string? SectionCode { get; set; }
        public string? TradeCode { get; set; }
        public string? CostCode { get; set; }
        public string? MeasurementStandard { get; set; }
        public string? MeasurementCode { get; set; }
        public string? LineNumber { get; set; }
        public string? ItemCode { get; set; }
        public string ItemType { get; set; } = "Item";
        public string Description { get; set; } = string.Empty;
        public string UnitOfMeasure { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? UnitRate { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int? SortOrder { get; set; }
        public string? Notes { get; set; }

        public static ImportLine FromProjectItem(ProjectBoqItemDto item, int index) => new()
        {
            RowNumber = FirstDataRow + index - 1,
            TemplateVersion = QuantitySurveyBoqSpreadsheetService.TemplateVersion,
            ProjectId = item.ProjectId.ToString(),
            ClientLineKey = $"EXPORT-{index:000000}",
            PackageCode = item.PackageCode ?? string.Empty,
            SectionCode = item.SectionCode,
            TradeCode = item.TradeCode,
            CostCode = item.CostCode,
            MeasurementStandard = item.MeasurementStandard,
            MeasurementCode = item.MeasurementCode,
            LineNumber = item.LineNumber,
            ItemCode = item.ItemCode,
            ItemType = item.ItemType,
            Description = item.Description,
            UnitOfMeasure = item.UnitOfMeasure ?? string.Empty,
            Quantity = item.Quantity,
            UnitRate = item.UnitRate,
            Currency = item.Currency,
            SortOrder = item.SortOrder,
            Notes = item.Notes
        };
    }
}
