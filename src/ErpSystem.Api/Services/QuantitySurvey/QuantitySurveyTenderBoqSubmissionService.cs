using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ErpSystem.Api.Services.Spreadsheets;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyTenderBoqSubmissionService : IQuantitySurveyTenderBoqSubmissionService
{
    private const string TemplateVersion = "1";
    private const int HeaderRow = 5;
    private const int FirstDataRow = HeaderRow + 1;
    private const int MaximumRows = 2_000;
    private const string WorkbookContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string ReconciliationDeclaration =
        "I confirm that the tenderer BoQ lines, validation findings, totals, and source workbook have been reviewed and reconciled before submission.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IQuantitySurveyConfigurationService _configuration;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ITimeLimitedDataProtector _templateProtector;
    private readonly bool _externalSubmissionsEnabled;

    public QuantitySurveyTenderBoqSubmissionService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IQuantitySurveyConfigurationService configuration,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration? deploymentConfiguration = null)
    {
        _db = db;
        _currentUser = currentUser;
        _configuration = configuration;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _externalSubmissionsEnabled = (deploymentConfiguration?["QuantitySurvey:OptionalFeatures"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("external-submissions", StringComparer.Ordinal);
        _templateProtector = dataProtectionProvider
            .CreateProtector("ErpSystem.QuantitySurvey.TenderBoqSubmission.Template.v1")
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

    private string ActorRoles => string.Join(",", _currentUser.Roles
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value));

    public async Task<QuantitySurveyTenderBoqContextDto> GetExternalContextAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default)
        => MapContext(await LoadContextAsync(tenderBidId, requireExternalOwner: true, cancellationToken));

    public async Task<QuantitySurveyBoqFileDto> CreateExternalTemplateAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(tenderBidId, requireExternalOwner: true, cancellationToken);
        EnsureChannel(context.Policy, QuantitySurveyExternalSubmissionChannel.ControlledExcel);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tenderer BOQ");
        sheet.Cell("A1").Value = "Tenderer BoQ Submission";
        sheet.Cell("A2").Value = $"Tender: {context.Tender.TenderNumber} - {context.Tender.Title}";
        sheet.Cell("A3").Value = $"Bid: {context.Bid.BidNumber} | Project: {context.Project.ProjectCode}";
        sheet.Range(1, 1, 1, 11).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Range(2, 1, 2, 11).Merge();
        sheet.Range(3, 1, 3, 11).Merge();

        var headers = new[]
        {
            "Tender Item ID", "BoQ Version Line ID", "BoQ Line Key", "Line Number", "Item Code",
            "Description", "Unit", "Tender Quantity", "Offered Quantity", "Unit Rate", "Line Total"
        };
        for (var column = 1; column <= headers.Length; column++)
            sheet.Cell(HeaderRow, column).Value = headers[column - 1];
        StyleHeader(sheet.Range(HeaderRow, 1, HeaderRow, headers.Length));

        var row = FirstDataRow;
        foreach (var baseline in context.Rows)
        {
            sheet.Cell(row, 1).Value = baseline.TenderItem.Id.ToString();
            sheet.Cell(row, 2).Value = baseline.BoqLine.Id.ToString();
            sheet.Cell(row, 3).Value = baseline.BoqLine.LineKey.ToString();
            sheet.Cell(row, 4).Value = baseline.BoqLine.LineNumber ?? baseline.TenderItem.LineNumber.ToString(CultureInfo.InvariantCulture);
            sheet.Cell(row, 5).Value = baseline.BoqLine.ItemCode ?? baseline.TenderItem.ItemCode ?? string.Empty;
            sheet.Cell(row, 6).Value = baseline.BoqLine.Description;
            sheet.Cell(row, 7).Value = baseline.BoqLine.UnitOfMeasure ?? baseline.TenderItem.UnitOfMeasure ?? string.Empty;
            sheet.Cell(row, 8).Value = baseline.TenderItem.Quantity;
            sheet.Cell(row, 9).Value = baseline.BidItem.OfferedQuantity > 0
                ? baseline.BidItem.OfferedQuantity
                : baseline.TenderItem.Quantity;
            if (baseline.BidItem.UnitPrice > 0)
                sheet.Cell(row, 10).Value = baseline.BidItem.UnitPrice;
            sheet.Cell(row, 11).FormulaA1 = $"ROUND(I{row}*J{row},2)";
            row++;
        }

        var lastRow = Math.Max(FirstDataRow, row - 1);
        sheet.Range(FirstDataRow, 1, lastRow, 11).Style.Protection.Locked = true;
        sheet.Range(FirstDataRow, 9, lastRow, 10).Style.Protection.Locked = false;
        sheet.Columns(1, 3).Hide();
        sheet.Column(6).Width = 55;
        sheet.Columns(4, 5).Width = 16;
        sheet.Columns(7, 11).AdjustToContents(1, 24);
        sheet.Range(FirstDataRow, 8, lastRow, 11).Style.NumberFormat.Format = "#,##0.00##";
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Protect("TDC-QS-TENDER-BOQ");

        AddControlSheet(workbook, context);
        await using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return new QuantitySurveyBoqFileDto
        {
            Content = memory.ToArray(),
            ContentType = WorkbookContentType,
            FileName = $"{SafeFileName(context.Tender.TenderNumber)}-{SafeFileName(context.Bid.BidNumber)}-tenderer-boq.xlsx"
        };
    }

    public async Task<QuantitySurveyTenderBoqSubmissionDto> PreviewExternalAsync(
        Guid tenderBidId,
        Stream stream,
        string fileName,
        string contentType,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(tenderBidId, requireExternalOwner: true, cancellationToken);
        EnsureChannel(context.Policy, QuantitySurveyExternalSubmissionChannel.ControlledExcel);
        ValidateFilePolicy(context.Policy, fileName);

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var maximumBytes = context.Policy.MaximumFileSizeMb * 1024L * 1024L;
        if (memory.Length == 0 || memory.Length > maximumBytes)
            throw new InvalidOperationException($"Workbook size must be between 1 byte and {context.Policy.MaximumFileSizeMb} MB.");

        var bytes = memory.ToArray();
        SpreadsheetSecurityInspector.ValidateXlsxPackage(bytes, maximumWorksheets: 4);
        if (SpreadsheetSecurityInspector.HasVbaProject(bytes))
            throw new InvalidOperationException("VBA projects and macros are not allowed in tenderer BoQ workbooks.");
        if (SpreadsheetSecurityInspector.HasExternalRelationships(bytes))
            throw new InvalidOperationException("External links and external workbook relationships are not allowed.");

        var issues = new List<QuantitySurveyTenderBoqIssueDto>();
        List<ParsedLine> parsed;
        using (var workbook = new XLWorkbook(new MemoryStream(bytes, writable: false)))
        {
            ValidateControlSheet(workbook, context, issues);
            parsed = ReadAndValidateLines(workbook, context, issues);
        }

        var normalizedPayload = JsonSerializer.Serialize(parsed, JsonOptions);
        var normalizedHash = Hash(normalizedPayload);
        var previewToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var submissionId = Guid.NewGuid();
        var fileHash = Convert.ToHexString(SHA256.HashData(bytes));

        var upload = await _controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId,
            ActorUserId = UserId,
            ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyTenderBoqSubmission,
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? WorkbookContentType : contentType,
            FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, writable: false)
        }, cancellationToken);

        CentralDocumentRepositoryLink document;
        try
        {
            document = await _centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId,
                ActorUserId = UserId,
                ActorName = UserName,
                FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey",
                SourceLabel = "Quantity Survey / Tenderer BoQ submissions",
                SourceEntityType = nameof(QuantitySurveyTenderBoqSubmission),
                SourceRecordId = submissionId,
                SourceRecordReference = context.Bid.BidNumber,
                Title = $"{context.Tender.TenderNumber} - {context.Bid.BidNumber} tenderer BoQ",
                DocumentType = "TendererBoQWorkbook",
                MetadataTemplateCode = "TDC-PROC-TENDER",
                AccessProfile = "Procurement tender restricted",
                VersionStatus = issues.Any(IsError) ? "Validation failed" : "Validated",
                ChangeSummary = "Original tenderer workbook retained before signed reconciliation.",
                RequirePublishedGovernance = true,
                MetadataValues =
                [
                    new("tenderNumber", "Tender number", context.Tender.TenderNumber),
                    new("bidNumber", "Bid number", context.Bid.BidNumber),
                    new("projectCode", "Project code", context.Project.ProjectCode),
                    new("boqPublication", "BoQ publication", $"v{context.Publication.VersionNumber}"),
                    new("checksumSha256", "Checksum SHA-256", fileHash)
                ]
            }, cancellationToken);
        }
        catch
        {
            await _controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            throw;
        }

        var entity = new QuantitySurveyTenderBoqSubmission
        {
            Id = submissionId,
            TenantId = TenantId,
            TenderBidId = context.Bid.Id,
            TenderId = context.Tender.Id,
            ProjectId = context.Project.Id,
            TenderBoqVersionId = context.Publication.Id,
            BusinessPartnerId = context.Bid.BusinessPartnerId,
            Channel = QuantitySurveyExternalSubmissionChannel.ControlledExcel,
            Status = issues.Any(IsError)
                ? QuantitySurveyTenderBoqSubmissionStatus.ValidationFailed
                : QuantitySurveyTenderBoqSubmissionStatus.Validated,
            VettingStatus = QuantitySurveyTenderBoqVettingStatus.Pending,
            FileUploadRecordId = upload.Record.Id,
            CentralDocumentRecordId = document.DocumentRecordId,
            CentralDocumentVersionId = document.DocumentVersionId,
            OriginalFileName = upload.Record.OriginalFileName,
            FileHash = fileHash,
            NormalizedPayloadHash = normalizedHash,
            PreviewTokenHash = Hash(previewToken),
            TenderBoqSnapshotHash = context.Publication.SnapshotHash,
            SubmittedByUserId = UserId,
            SubmittedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            LineCount = parsed.Count,
            ErrorCount = issues.Count(IsError),
            WarningCount = issues.Count(item => !IsError(item)),
            TenderBoqTotal = context.Rows.Sum(item => item.BoqLine.LineAmount ?? 0m),
            SubmittedTotal = parsed.Sum(item => item.CalculatedLineTotal),
            NormalizedPayloadJson = normalizedPayload,
            IssuesJson = JsonSerializer.Serialize(issues, JsonOptions),
            AuditAction = QuantitySurveyAuditEventMap.StageTenderBoqSubmission,
            ActorRoles = ActorRoles,
            CorrelationId = NormalizeCorrelationId(correlationId)
        };
        foreach (var line in parsed.Where(item => item.IsResolved))
            entity.Lines.Add(ToEntity(entity, line));

        try
        {
            _db.QuantitySurveyTenderBoqSubmissions.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _centralDocuments.DeleteAsync(
                    TenantId, document.DocumentRecordId, UserId, cancellationToken);
            }
            catch
            {
                // Preserve the original persistence failure; central cleanup is best effort.
            }
            try
            {
                await _controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            }
            catch
            {
                // Preserve the original persistence failure; the cleanup worker can remove the orphan.
            }
            throw;
        }

        return Map(entity, previewToken);
    }

    public async Task<QuantitySurveyTenderBoqSubmissionDto> CommitExternalAsync(
        Guid tenderBidId,
        Guid submissionId,
        CommitQuantitySurveyTenderBoqSubmissionDto request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(tenderBidId, requireExternalOwner: true, cancellationToken);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);
            var entity = await _db.QuantitySurveyTenderBoqSubmissions
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == submissionId && item.TenderBidId == tenderBidId &&
                    item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Tenderer BoQ submission was not found.");

            if (!FixedEquals(entity.PreviewTokenHash, Hash(request.PreviewToken)))
                throw new UnauthorizedAccessException("The tenderer BoQ preview token is invalid.");
            if (entity.Status == QuantitySurveyTenderBoqSubmissionStatus.Committed)
                return Map(entity);
            if (entity.Status != QuantitySurveyTenderBoqSubmissionStatus.Validated || entity.ErrorCount > 0)
                throw new InvalidOperationException("Only a validated tenderer BoQ without errors can be committed.");
            if (entity.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("The tenderer BoQ preview has expired. Upload the workbook again.");
            if (context.Publication.Id != entity.TenderBoqVersionId ||
                !FixedEquals(context.Publication.SnapshotHash, entity.TenderBoqSnapshotHash))
                throw new InvalidOperationException("The approved tender BoQ changed after preview. Upload a fresh workbook.");
            if (!string.Equals(request.ReconciliationDeclaration?.Trim(), ReconciliationDeclaration, StringComparison.Ordinal))
                throw new InvalidOperationException("Confirm the fixed tenderer BoQ reconciliation declaration before committing.");
            if (context.Policy.RequireSignature && string.IsNullOrWhiteSpace(request.SignatoryName))
                throw new InvalidOperationException("The configured external-submission policy requires the signatory name.");

            var selectedItems = context.Rows.ToDictionary(item => item.TenderItem.Id);
            if (entity.Lines.Count != selectedItems.Count ||
                entity.Lines.Any(line => !selectedItems.ContainsKey(line.TenderItemId)))
                throw new InvalidOperationException("The staged workbook does not cover every selected tender item.");

            var bidItems = await _db.TenderBidItems
                .Where(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            var bidItemsByTenderItem = bidItems.ToDictionary(item => item.TenderItemId);
            foreach (var line in entity.Lines)
            {
                if (!bidItemsByTenderItem.TryGetValue(line.TenderItemId, out var bidItem))
                    throw new InvalidOperationException("A selected tender item was removed after workbook validation.");
                bidItem.OfferedQuantity = line.OfferedQuantity;
                bidItem.UnitPrice = line.UnitPrice;
                bidItem.TotalPrice = line.CalculatedLineTotal;
                bidItem.UpdatedAt = DateTime.UtcNow;
            }

            context.Bid.TotalBidAmount = entity.Lines.Sum(item => item.CalculatedLineTotal);
            context.Bid.UpdatedAt = DateTime.UtcNow;
            entity.Status = QuantitySurveyTenderBoqSubmissionStatus.Committed;
            entity.CommittedLineCount = entity.Lines.Count;
            entity.ReconciliationDeclaration = ReconciliationDeclaration;
            entity.SignatoryName = request.SignatoryName?.Trim();
            entity.CommittedByUserId = UserId;
            entity.CommittedAt = DateTime.UtcNow;
            entity.AuditAction = QuantitySurveyAuditEventMap.CommitTenderBoqSubmission;
            entity.ActorRoles = ActorRoles;
            entity.CorrelationId = NormalizeCorrelationId(correlationId);
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(entity);
        });
    }

    public async Task<QuantitySurveyTenderBoqSubmissionDto?> GetLatestExternalAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default)
    {
        await LoadContextAsync(tenderBidId, requireExternalOwner: true, cancellationToken);
        var entity = await _db.QuantitySurveyTenderBoqSubmissions.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity == null ? null : Map(entity);
    }

    public async Task EnsureReadyForTenderSubmissionAsync(
        Guid tenderBidId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var bidScope = await _db.TenderBids.AsNoTracking()
            .Where(item => item.Id == tenderBidId && item.TenantId == TenantId && !item.IsDeleted)
            .Select(item => new { item.BusinessPartnerId,
                HasProject = item.Tender.SourcePurchaseRequisition != null &&
                    item.Tender.SourcePurchaseRequisition.ProjectId.HasValue })
            .SingleOrDefaultAsync(cancellationToken);
        if (bidScope is null || !bidScope.HasProject) return;
        await EnsureExternalOwnerAsync(bidScope.BusinessPartnerId, cancellationToken);

        // Ordinary Procurement prices remain the default architecture route. Do not
        // silently activate the optional QS exchange just because a PR has a project.
        // Records that already entered that exchange retain their existing controls.
        if (!_externalSubmissionsEnabled && !await _db.QuantitySurveyTenderBoqSubmissions.AsNoTracking()
                .AnyAsync(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted,
                    cancellationToken)) return;

        var context = await LoadContextAsync(tenderBidId, requireExternalOwner: true, cancellationToken);
        var payload = PortalPayload(context);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadHash = Hash(payloadJson);
        var committed = await _db.QuantitySurveyTenderBoqSubmissions.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted &&
                (item.Status == QuantitySurveyTenderBoqSubmissionStatus.Committed ||
                 item.Status == QuantitySurveyTenderBoqSubmissionStatus.Vetted ||
                 item.Status == QuantitySurveyTenderBoqSubmissionStatus.Rejected) &&
                item.TenderBoqVersionId == context.Publication.Id)
            .OrderByDescending(item => item.VettedAt ?? item.CommittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (committed != null && SubmissionMatchesBid(committed.Lines, context.Rows))
        {
            if (committed.Status == QuantitySurveyTenderBoqSubmissionStatus.Rejected)
                throw new InvalidOperationException(
                    "The current tenderer BoQ was rejected during QS vetting. Correct and resubmit it before submitting the bid.");
            return;
        }

        EnsureChannel(context.Policy, QuantitySurveyExternalSubmissionChannel.ExternalPortal);
        if (context.Policy.RequireEvidence)
        {
            var hasControlledEvidence = await _db.TenderBidDocuments.AsNoTracking()
                .AnyAsync(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted &&
                    item.CentralDocumentRecordId.HasValue && item.CentralDocumentVersionId.HasValue,
                    cancellationToken);
            if (!hasControlledEvidence)
                throw new InvalidOperationException("The configured QS external-submission policy requires a clean-scanned tender document in central DMS before portal-entered BoQ lines can be submitted.");
        }

        var issues = ValidatePortalPayload(context, payload);
        if (issues.Any(IsError))
            throw new InvalidOperationException(string.Join("; ", issues.Where(IsError).Take(5).Select(item => item.Message)));

        var evidence = await _db.TenderBidDocuments.AsNoTracking()
            .Where(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted &&
                item.CentralDocumentRecordId.HasValue && item.CentralDocumentVersionId.HasValue)
            .OrderByDescending(item => item.UploadedDate)
            .FirstOrDefaultAsync(cancellationToken);
        var entity = new QuantitySurveyTenderBoqSubmission
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TenderBidId = context.Bid.Id,
            TenderId = context.Tender.Id, ProjectId = context.Project.Id,
            TenderBoqVersionId = context.Publication.Id, BusinessPartnerId = context.Bid.BusinessPartnerId,
            Channel = QuantitySurveyExternalSubmissionChannel.ExternalPortal,
            Status = QuantitySurveyTenderBoqSubmissionStatus.Committed,
            VettingStatus = QuantitySurveyTenderBoqVettingStatus.Pending,
            FileUploadRecordId = evidence?.FileUploadRecordId,
            CentralDocumentRecordId = evidence?.CentralDocumentRecordId,
            CentralDocumentVersionId = evidence?.CentralDocumentVersionId,
            OriginalFileName = "Portal-entered tenderer BoQ",
            FileHash = payloadHash, NormalizedPayloadHash = payloadHash,
            PreviewTokenHash = Hash($"portal:{Guid.NewGuid():N}"),
            TenderBoqSnapshotHash = context.Publication.SnapshotHash,
            SubmittedByUserId = UserId, SubmittedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow, LineCount = payload.Count,
            ErrorCount = 0, WarningCount = issues.Count,
            CommittedLineCount = payload.Count,
            TenderBoqTotal = context.Rows.Sum(item => item.BoqLine.LineAmount ?? 0m),
            SubmittedTotal = payload.Sum(item => item.CalculatedLineTotal),
            NormalizedPayloadJson = payloadJson,
            IssuesJson = JsonSerializer.Serialize(issues, JsonOptions),
            AuditAction = QuantitySurveyAuditEventMap.CommitTenderBoqSubmission,
            ActorRoles = ActorRoles, CorrelationId = NormalizeCorrelationId(correlationId),
            ReconciliationDeclaration = "Portal-entered bidder lines validated against the approved tender BoQ at submission.",
            SignatoryName = context.BusinessPartner.PartnerName,
            CommittedByUserId = UserId, CommittedAt = DateTime.UtcNow
        };
        foreach (var line in payload)
            entity.Lines.Add(ToEntity(entity, line));
        _db.QuantitySurveyTenderBoqSubmissions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuantitySurveyTenderBoqSubmissionDto>> GetInternalHistoryAsync(
        Guid tenderBidId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInternalBidExistsAsync(tenderBidId, cancellationToken);
        var entities = await _db.QuantitySurveyTenderBoqSubmissions.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(item => Map(item)).ToArray();
    }

    public async Task<QuantitySurveyTenderBoqSubmissionDto> VetInternalAsync(
        Guid tenderBidId,
        Guid submissionId,
        VetQuantitySurveyTenderBoqSubmissionDto request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInternalBidExistsAsync(tenderBidId, cancellationToken);
        var entity = await _db.QuantitySurveyTenderBoqSubmissions
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == submissionId && item.TenderBidId == tenderBidId &&
                item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Tenderer BoQ submission was not found.");
        var accepted = string.Equals(request.Decision, "Accepted", StringComparison.OrdinalIgnoreCase);
        var rejected = string.Equals(request.Decision, "Rejected", StringComparison.OrdinalIgnoreCase);
        if (!accepted && !rejected)
            throw new InvalidOperationException("The tenderer BoQ vetting decision must be Accepted or Rejected.");
        if (entity.Status is QuantitySurveyTenderBoqSubmissionStatus.Vetted or
            QuantitySurveyTenderBoqSubmissionStatus.Rejected)
        {
            var sameOutcome = accepted
                ? entity.VettingStatus == QuantitySurveyTenderBoqVettingStatus.Accepted
                : entity.VettingStatus == QuantitySurveyTenderBoqVettingStatus.Rejected;
            if (sameOutcome && string.Equals(entity.VettingNote, request.Note.Trim(), StringComparison.Ordinal))
                return Map(entity);
            throw new InvalidOperationException("The tenderer BoQ already has a final QS vetting outcome.");
        }
        if (!FixedEquals(Convert.ToBase64String(entity.RowVersion), request.RowVersion))
            throw new DbUpdateConcurrencyException("The tenderer BoQ submission changed after it was opened. Refresh and try again.");
        if (entity.Status != QuantitySurveyTenderBoqSubmissionStatus.Committed)
            throw new InvalidOperationException("Only a committed tenderer BoQ can be vetted.");
        if (entity.ErrorCount > 0)
            throw new InvalidOperationException("A tenderer BoQ with validation errors cannot be accepted.");

        entity.VettingStatus = accepted
            ? QuantitySurveyTenderBoqVettingStatus.Accepted
            : QuantitySurveyTenderBoqVettingStatus.Rejected;
        entity.Status = accepted
            ? QuantitySurveyTenderBoqSubmissionStatus.Vetted
            : QuantitySurveyTenderBoqSubmissionStatus.Rejected;
        entity.VettingNote = request.Note.Trim();
        entity.VettedByUserId = UserId;
        entity.VettedAt = DateTime.UtcNow;
        entity.AuditAction = accepted
            ? QuantitySurveyAuditEventMap.AcceptTenderBoqSubmission
            : QuantitySurveyAuditEventMap.RejectTenderBoqSubmission;
        entity.ActorRoles = ActorRoles;
        entity.CorrelationId = NormalizeCorrelationId(correlationId);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private async Task<SubmissionContext> LoadContextAsync(
        Guid tenderBidId,
        bool requireExternalOwner,
        CancellationToken cancellationToken)
    {
        var bid = await _db.TenderBids
            .Include(item => item.BusinessPartner)
            .Include(item => item.Tender)
                .ThenInclude(item => item.SourcePurchaseRequisition)
            .SingleOrDefaultAsync(item => item.Id == tenderBidId && item.TenantId == TenantId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Tender bid was not found.");
        if (requireExternalOwner)
            await EnsureExternalOwnerAsync(bid.BusinessPartnerId, cancellationToken);
        if (bid.Status != "Draft" && requireExternalOwner)
            throw new InvalidOperationException("Tenderer BoQ lines can be changed only while the bid is Draft.");

        var projectId = bid.Tender.SourcePurchaseRequisition?.ProjectId
            ?? throw new InvalidOperationException("This tender is not linked to a project purchase requisition and cannot use the QS tenderer BoQ control.");
        var project = await _db.Projects.SingleOrDefaultAsync(item => item.Id == projectId &&
            item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The tender project is not available in this tenant.");
        var publication = await _db.ProjectBoqVersions.AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.TenantId == TenantId &&
                item.VersionType == QuantitySurveyBoqVersionType.Approved &&
                item.Status == ProjectBoqVersionStatuses.Approved && item.PublishedAt.HasValue && !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("The project has no approved published BoQ available for tenderer submission.");

        var bidItems = await _db.TenderBidItems.AsNoTracking()
            .Where(item => item.TenderBidId == tenderBidId && item.TenantId == TenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (bidItems.Count == 0)
            throw new InvalidOperationException("Select the tender lots/items and save the Draft bid before downloading the tenderer BoQ.");
        var tenderItemIds = bidItems.Select(item => item.TenderItemId).Distinct().ToList();
        var tenderItems = await _db.TenderItems.AsNoTracking()
            .Where(item => tenderItemIds.Contains(item.Id) && item.TenderId == bid.TenderId &&
                item.TenantId == TenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (tenderItems.Count != tenderItemIds.Count)
            throw new InvalidOperationException("One or more selected bid items do not belong to this tenant and tender.");

        var rows = AlignRows(bidItems, tenderItems, publication.Lines.ToList());
        var policy = await LoadPolicyAsync(cancellationToken);
        return new SubmissionContext(bid, bid.Tender, bid.BusinessPartner, project, publication, rows, policy);
    }

    private async Task EnsureExternalOwnerAsync(Guid businessPartnerId, CancellationToken cancellationToken)
    {
        var owner = await _db.BusinessPartners.AsNoTracking().AnyAsync(item => item.Id == businessPartnerId &&
            item.TenantId == TenantId && item.UserId == UserId && !item.IsDeleted, cancellationToken);
        if (owner) return;
        var linked = await _db.BusinessPartnerUsers.AsNoTracking().AnyAsync(item =>
            item.BusinessPartnerId == businessPartnerId && item.UserId == UserId && item.TenantId == TenantId &&
            item.IsActive && !item.IsDeleted, cancellationToken);
        if (!linked) throw new UnauthorizedAccessException("The tender bid is not assigned to the current business-partner identity.");
    }

    private async Task EnsureInternalBidExistsAsync(Guid tenderBidId, CancellationToken cancellationToken)
    {
        var exists = await _db.TenderBids.AsNoTracking().AnyAsync(item => item.Id == tenderBidId &&
            item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        if (!exists) throw new KeyNotFoundException("Tender bid was not found.");
    }

    private async Task<QsExternalSubmissionValue> LoadPolicyAsync(CancellationToken cancellationToken)
    {
        var profile = await _configuration.GetEffectiveProfileAsync(DateTime.UtcNow, cancellationToken)
            ?? throw new InvalidOperationException("No Published QS configuration profile is effective for this tenant and date.");
        var decision = profile.Decisions.SingleOrDefault(item => item.DecisionKey == "QS-DEC-013" && item.IsComplete)
            ?? throw new InvalidOperationException("The effective QS profile does not contain an approved external-submission policy.");
        return decision.Value.Deserialize<QsExternalSubmissionValue>(JsonOptions)
            ?? throw new InvalidOperationException("The effective QS external-submission policy is invalid.");
    }

    private static List<BaselineRow> AlignRows(
        IReadOnlyCollection<TenderBidItem> bidItems,
        IReadOnlyCollection<TenderItem> tenderItems,
        IReadOnlyCollection<ProjectBoqVersionLine> publicationLines)
    {
        var bidByTenderItem = bidItems.ToDictionary(item => item.TenderItemId);
        var result = new List<BaselineRow>(tenderItems.Count);
        var usedLineIds = new HashSet<Guid>();
        foreach (var tenderItem in tenderItems.OrderBy(item => item.LineNumber))
        {
            var candidates = !string.IsNullOrWhiteSpace(tenderItem.ItemCode)
                ? publicationLines.Where(line => string.Equals(line.ItemCode?.Trim(), tenderItem.ItemCode.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                : publicationLines.Where(line => string.Equals(line.LineNumber?.Trim(), tenderItem.LineNumber.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count != 1)
                throw new InvalidOperationException($"Tender item '{tenderItem.ItemCode ?? tenderItem.LineNumber.ToString()}' cannot be matched uniquely to the approved BoQ publication. Align the tender items before accepting bidder prices.");
            var line = candidates[0];
            if (!usedLineIds.Add(line.Id))
                throw new InvalidOperationException($"Multiple tender items resolve to approved BoQ line '{line.ItemCode ?? line.LineNumber}'.");
            if (!EqualText(tenderItem.UnitOfMeasure, line.UnitOfMeasure) ||
                Math.Abs(tenderItem.Quantity - line.Quantity) > 0.0001m)
                throw new InvalidOperationException($"Tender item '{tenderItem.ItemCode ?? tenderItem.LineNumber.ToString()}' quantity or unit differs from the approved BoQ publication.");
            result.Add(new BaselineRow(bidByTenderItem[tenderItem.Id], tenderItem, line));
        }
        return result;
    }

    private static QuantitySurveyTenderBoqContextDto MapContext(SubmissionContext context) => new()
    {
        TenderBidId = context.Bid.Id,
        BidNumber = context.Bid.BidNumber,
        TenderId = context.Tender.Id,
        TenderNumber = context.Tender.TenderNumber,
        TenderTitle = context.Tender.Title,
        ProjectId = context.Project.Id,
        ProjectCode = context.Project.ProjectCode,
        ProjectName = context.Project.Title,
        TenderBoqVersionId = context.Publication.Id,
        TenderBoqVersionNumber = context.Publication.VersionNumber,
        TenderBoqSnapshotHash = context.Publication.SnapshotHash,
        Currency = context.Bid.Currency ?? context.Publication.Lines.Select(item => item.Currency).FirstOrDefault() ?? string.Empty,
        LineCount = context.Rows.Count,
        TenderBoqTotal = context.Rows.Sum(item => item.BoqLine.LineAmount ?? 0m),
        CanUpload = context.Bid.Status == "Draft" && context.Policy.Channels.Contains(QuantitySurveyExternalSubmissionChannel.ControlledExcel),
        RequiresSignature = context.Policy.RequireSignature,
        MaximumFileSizeMb = context.Policy.MaximumFileSizeMb,
        ReconciliationDeclaration = ReconciliationDeclaration
    };

    private void AddControlSheet(XLWorkbook workbook, SubmissionContext context)
    {
        var control = new TemplateControl(TenantId, context.Bid.Id, context.Tender.Id, context.Project.Id,
            context.Publication.Id, context.Publication.SnapshotHash, TemplateVersion);
        var token = _templateProtector.Protect(JsonSerializer.Serialize(control, JsonOptions), TimeSpan.FromDays(7));
        var sheet = workbook.Worksheets.Add("Control");
        sheet.Cell("A1").Value = "TemplateVersion";
        sheet.Cell("B1").Value = TemplateVersion;
        sheet.Cell("A2").Value = "TenderBidId";
        sheet.Cell("B2").Value = context.Bid.Id.ToString();
        sheet.Cell("A3").Value = "BoqPublicationId";
        sheet.Cell("B3").Value = context.Publication.Id.ToString();
        sheet.Cell("A4").Value = "ControlToken";
        sheet.Cell("B4").Value = token;
        sheet.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    private void ValidateControlSheet(
        XLWorkbook workbook,
        SubmissionContext context,
        ICollection<QuantitySurveyTenderBoqIssueDto> issues)
    {
        if (!workbook.TryGetWorksheet("Control", out var sheet))
        {
            AddIssue(issues, null, "CONTROL_MISSING", "The protected template control sheet is missing.");
            return;
        }
        if (sheet.Cell("B1").GetString().Trim() != TemplateVersion)
            AddIssue(issues, null, "TEMPLATE_VERSION", "The workbook template version is not supported.");
        if (!Guid.TryParse(sheet.Cell("B2").GetString(), out var bidId) || bidId != context.Bid.Id)
            AddIssue(issues, null, "BID_MISMATCH", "The workbook belongs to a different tender bid.");
        if (!Guid.TryParse(sheet.Cell("B3").GetString(), out var publicationId) || publicationId != context.Publication.Id)
            AddIssue(issues, null, "PUBLICATION_MISMATCH", "The workbook belongs to a different approved BoQ publication.");
        try
        {
            var json = _templateProtector.Unprotect(sheet.Cell("B4").GetString(), out _);
            var control = JsonSerializer.Deserialize<TemplateControl>(json, JsonOptions);
            if (control == null || control.TenantId != TenantId || control.TenderBidId != context.Bid.Id ||
                control.TenderId != context.Tender.Id || control.ProjectId != context.Project.Id ||
                control.BoqPublicationId != context.Publication.Id ||
                !FixedEquals(control.BoqSnapshotHash, context.Publication.SnapshotHash) ||
                control.TemplateVersion != TemplateVersion)
                AddIssue(issues, null, "CONTROL_TAMPERED", "The protected controls do not match this tenant, tender, bid, project, or approved BoQ publication.");
        }
        catch (CryptographicException)
        {
            AddIssue(issues, null, "CONTROL_INVALID", "The protected template control is invalid or expired. Download a fresh template.");
        }
    }

    private static List<ParsedLine> ReadAndValidateLines(
        XLWorkbook workbook,
        SubmissionContext context,
        ICollection<QuantitySurveyTenderBoqIssueDto> issues)
    {
        if (!workbook.TryGetWorksheet("Tenderer BOQ", out var sheet))
        {
            AddIssue(issues, null, "SHEET_MISSING", "The 'Tenderer BOQ' worksheet is missing.");
            return [];
        }
        var requiredHeaders = new[]
        {
            "Tender Item ID", "BoQ Version Line ID", "BoQ Line Key", "Line Number", "Item Code",
            "Description", "Unit", "Tender Quantity", "Offered Quantity", "Unit Rate", "Line Total"
        };
        for (var column = 1; column <= requiredHeaders.Length; column++)
            if (!string.Equals(sheet.Cell(HeaderRow, column).GetString().Trim(), requiredHeaders[column - 1], StringComparison.Ordinal))
                AddIssue(issues, HeaderRow, "HEADER_CHANGED", $"Column {column} must be '{requiredHeaders[column - 1]}'.");

        var baselineByTenderItem = context.Rows.ToDictionary(item => item.TenderItem.Id);
        var result = new List<ParsedLine>();
        var seenTenderItems = new HashSet<Guid>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? HeaderRow;
        if (lastRow - HeaderRow > MaximumRows)
            AddIssue(issues, null, "ROW_LIMIT", $"A maximum of {MaximumRows} tenderer BoQ rows is allowed.");
        for (var row = FirstDataRow; row <= Math.Min(lastRow, HeaderRow + MaximumRows); row++)
        {
            if (sheet.Range(row, 1, row, 11).Cells().All(cell => cell.IsEmpty())) continue;
            if (!Guid.TryParse(sheet.Cell(row, 1).GetString(), out var tenderItemId) ||
                !Guid.TryParse(sheet.Cell(row, 2).GetString(), out var boqLineId) ||
                !Guid.TryParse(sheet.Cell(row, 3).GetString(), out var lineKey) ||
                !baselineByTenderItem.TryGetValue(tenderItemId, out var baseline))
            {
                AddIssue(issues, row, "LINE_IDENTITY", "The protected tender/BoQ line identity is invalid or does not belong to this bid.");
                result.Add(ParsedLine.Unresolved(row));
                continue;
            }
            var lineIssues = new List<QuantitySurveyTenderBoqIssueDto>();
            if (!seenTenderItems.Add(tenderItemId))
                AddIssue(lineIssues, row, "DUPLICATE_LINE", "The tender item appears more than once.");
            if (boqLineId != baseline.BoqLine.Id || lineKey != baseline.BoqLine.LineKey)
                AddIssue(lineIssues, row, "LINEAGE_CHANGED", "The approved BoQ line identity was changed.");
            if (!EqualText(sheet.Cell(row, 4).GetString(), baseline.BoqLine.LineNumber ?? baseline.TenderItem.LineNumber.ToString(CultureInfo.InvariantCulture)) ||
                !EqualText(sheet.Cell(row, 5).GetString(), baseline.BoqLine.ItemCode ?? baseline.TenderItem.ItemCode) ||
                !EqualText(sheet.Cell(row, 6).GetString(), baseline.BoqLine.Description) ||
                !EqualText(sheet.Cell(row, 7).GetString(), baseline.BoqLine.UnitOfMeasure ?? baseline.TenderItem.UnitOfMeasure))
                AddIssue(lineIssues, row, "LOCKED_TEXT_CHANGED", "Line number, item code, description, or unit differs from the approved tender BoQ.");
            if (!TryDecimal(sheet.Cell(row, 8), out var tenderQuantity) ||
                Math.Abs(tenderQuantity - baseline.TenderItem.Quantity) > 0.0001m)
                AddIssue(lineIssues, row, "TENDER_QUANTITY_CHANGED", "The locked tender quantity differs from the approved tender item.");
            if (!TryDecimal(sheet.Cell(row, 9), out var offeredQuantity) || offeredQuantity <= 0)
                AddIssue(lineIssues, row, "OFFERED_QUANTITY", "Offered quantity must be greater than zero.");
            if (!TryDecimal(sheet.Cell(row, 10), out var unitPrice) || unitPrice <= 0)
                AddIssue(lineIssues, row, "UNIT_PRICE", "Unit rate must be greater than zero.");
            var expectedFormula = $"ROUND(I{row}*J{row},2)";
            var actualFormula = NormalizeFormula(sheet.Cell(row, 11).FormulaA1);
            if (!string.Equals(actualFormula, NormalizeFormula(expectedFormula), StringComparison.OrdinalIgnoreCase))
                AddIssue(lineIssues, row, "TOTAL_FORMULA_CHANGED", "The locked line-total formula was removed or changed.");
            var calculated = Math.Round(offeredQuantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            var status = lineIssues.Any(IsError) ? "Invalid" :
                Math.Abs(offeredQuantity - baseline.TenderItem.Quantity) > 0.0001m ? "QuantityChanged" : "Matched";
            var parsed = new ParsedLine(row, tenderItemId, boqLineId, lineKey,
                baseline.BoqLine.LineNumber, baseline.BoqLine.ItemCode, baseline.BoqLine.Description,
                baseline.BoqLine.UnitOfMeasure ?? baseline.TenderItem.UnitOfMeasure,
                baseline.TenderItem.Quantity, offeredQuantity, unitPrice, calculated, calculated, 0m,
                status, lineIssues);
            result.Add(parsed);
            foreach (var issue in lineIssues) issues.Add(issue);
        }
        foreach (var missing in context.Rows.Where(item => !seenTenderItems.Contains(item.TenderItem.Id)))
            AddIssue(issues, null, "MISSING_LINE", $"Tender item '{missing.TenderItem.ItemCode ?? missing.TenderItem.LineNumber.ToString()}' is missing from the workbook.");
        return result;
    }

    private static List<ParsedLine> PortalPayload(SubmissionContext context) => context.Rows
        .Select((baseline, index) =>
        {
            var calculated = Math.Round(baseline.BidItem.OfferedQuantity * baseline.BidItem.UnitPrice, 2,
                MidpointRounding.AwayFromZero);
            var difference = Math.Round(baseline.BidItem.TotalPrice - calculated, 2,
                MidpointRounding.AwayFromZero);
            return new ParsedLine(index + 1, baseline.TenderItem.Id, baseline.BoqLine.Id,
                baseline.BoqLine.LineKey, baseline.BoqLine.LineNumber, baseline.BoqLine.ItemCode,
                baseline.BoqLine.Description, baseline.BoqLine.UnitOfMeasure ?? baseline.TenderItem.UnitOfMeasure,
                baseline.TenderItem.Quantity, baseline.BidItem.OfferedQuantity, baseline.BidItem.UnitPrice,
                baseline.BidItem.TotalPrice, calculated, difference,
                Math.Abs(baseline.BidItem.OfferedQuantity - baseline.TenderItem.Quantity) > 0.0001m
                    ? "QuantityChanged" : "Matched", []);
        }).ToList();

    private static List<QuantitySurveyTenderBoqIssueDto> ValidatePortalPayload(
        SubmissionContext context,
        IReadOnlyCollection<ParsedLine> payload)
    {
        var issues = new List<QuantitySurveyTenderBoqIssueDto>();
        foreach (var line in payload)
        {
            if (line.OfferedQuantity <= 0)
                AddIssue(issues, line.RowNumber, "OFFERED_QUANTITY", "Offered quantity must be greater than zero.");
            if (line.UnitPrice <= 0)
                AddIssue(issues, line.RowNumber, "UNIT_PRICE", "Unit rate must be greater than zero.");
            if (Math.Abs(line.ArithmeticDifference) > 0.01m)
                AddIssue(issues, line.RowNumber, "ARITHMETIC_MISMATCH", "Stored line total does not equal offered quantity multiplied by unit rate.");
        }
        var storedTotal = context.Bid.TotalBidAmount;
        var calculatedTotal = payload.Sum(item => item.CalculatedLineTotal);
        if (Math.Abs(storedTotal - calculatedTotal) > 0.01m)
            AddIssue(issues, null, "BID_TOTAL_MISMATCH", "Bid total does not equal the sum of calculated tenderer BoQ lines.");
        return issues;
    }

    private static bool SubmissionMatchesBid(
        ICollection<QuantitySurveyTenderBoqSubmissionLine> submitted,
        IReadOnlyCollection<BaselineRow> current)
    {
        if (submitted.Count != current.Count) return false;
        var byTenderItem = submitted.ToDictionary(item => item.TenderItemId);
        return current.All(item => byTenderItem.TryGetValue(item.TenderItem.Id, out var line) &&
            line.ProjectBoqVersionLineId == item.BoqLine.Id &&
            line.LineKey == item.BoqLine.LineKey &&
            Math.Abs(line.OfferedQuantity - item.BidItem.OfferedQuantity) <= 0.0001m &&
            Math.Abs(line.UnitPrice - item.BidItem.UnitPrice) <= 0.01m &&
            Math.Abs(line.CalculatedLineTotal - item.BidItem.TotalPrice) <= 0.01m);
    }

    private static QuantitySurveyTenderBoqSubmissionLine ToEntity(
        QuantitySurveyTenderBoqSubmission submission,
        ParsedLine line) => new()
    {
        Id = Guid.NewGuid(), TenantId = submission.TenantId, SubmissionId = submission.Id,
        TenderBidId = submission.TenderBidId, TenderItemId = line.TenderItemId,
        ProjectBoqVersionLineId = line.ProjectBoqVersionLineId, LineKey = line.LineKey,
        RowNumber = line.RowNumber, LineNumber = line.LineNumber, ItemCode = line.ItemCode,
        Description = line.Description, UnitOfMeasure = line.UnitOfMeasure,
        TenderQuantity = line.TenderQuantity, OfferedQuantity = line.OfferedQuantity,
        UnitPrice = line.UnitPrice, SubmittedLineTotal = line.SubmittedLineTotal,
        CalculatedLineTotal = line.CalculatedLineTotal, ArithmeticDifference = line.ArithmeticDifference,
        ComparisonStatus = line.ComparisonStatus,
        FindingsJson = JsonSerializer.Serialize(line.Findings, JsonOptions)
    };

    private static QuantitySurveyTenderBoqSubmissionDto Map(
        QuantitySurveyTenderBoqSubmission entity,
        string? previewToken = null)
    {
        var issues = Deserialize<List<QuantitySurveyTenderBoqIssueDto>>(entity.IssuesJson) ?? [];
        return new QuantitySurveyTenderBoqSubmissionDto
        {
            Id = entity.Id, TenderBidId = entity.TenderBidId,
            TenderBoqVersionId = entity.TenderBoqVersionId,
            Status = entity.Status.ToString(), VettingStatus = entity.VettingStatus.ToString(),
            Channel = entity.Channel.ToString(), OriginalFileName = entity.OriginalFileName,
            LineCount = entity.LineCount, ErrorCount = entity.ErrorCount,
            WarningCount = entity.WarningCount, CommittedLineCount = entity.CommittedLineCount,
            TenderBoqTotal = entity.TenderBoqTotal, SubmittedTotal = entity.SubmittedTotal,
            SubmittedAt = entity.SubmittedAt, ExpiresAt = entity.ExpiresAt,
            CommittedAt = entity.CommittedAt, VettedAt = entity.VettedAt,
            VettingNote = entity.VettingNote,
            CentralDocumentRecordId = entity.CentralDocumentRecordId,
            CentralDocumentVersionId = entity.CentralDocumentVersionId,
            RowVersion = Convert.ToBase64String(entity.RowVersion), PreviewToken = previewToken,
            Issues = issues,
            Lines = entity.Lines.OrderBy(item => item.RowNumber).Select(item => new QuantitySurveyTenderBoqLineDto
            {
                TenderItemId = item.TenderItemId, ProjectBoqVersionLineId = item.ProjectBoqVersionLineId,
                LineKey = item.LineKey, RowNumber = item.RowNumber, LineNumber = item.LineNumber,
                ItemCode = item.ItemCode, Description = item.Description, UnitOfMeasure = item.UnitOfMeasure,
                TenderQuantity = item.TenderQuantity, OfferedQuantity = item.OfferedQuantity,
                UnitPrice = item.UnitPrice, SubmittedLineTotal = item.SubmittedLineTotal,
                CalculatedLineTotal = item.CalculatedLineTotal,
                ArithmeticDifference = item.ArithmeticDifference, ComparisonStatus = item.ComparisonStatus,
                Findings = Deserialize<List<QuantitySurveyTenderBoqIssueDto>>(item.FindingsJson) ?? []
            }).ToArray()
        };
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    private static void EnsureChannel(QsExternalSubmissionValue policy, QuantitySurveyExternalSubmissionChannel channel)
    {
        if (!policy.Channels.Contains(channel))
            throw new InvalidOperationException($"The effective QS external-submission policy does not allow {channel} submissions.");
    }

    private static void ValidateFilePolicy(QsExternalSubmissionValue policy, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != ".xlsx")
            throw new InvalidOperationException("Only protected .xlsx tenderer BoQ workbooks are accepted.");
        if (!policy.AllowedFileExtensions.Any(item => string.Equals(
                item.StartsWith('.') ? item : $".{item}", extension, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The effective QS external-submission policy does not allow .xlsx files.");
    }

    private static bool TryDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue<decimal>(out value)) return true;
        return decimal.TryParse(cell.GetFormattedString().Replace(",", string.Empty),
            NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsError(QuantitySurveyTenderBoqIssueDto issue)
        => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase);

    private static void AddIssue(
        ICollection<QuantitySurveyTenderBoqIssueDto> issues,
        int? rowNumber,
        string code,
        string message,
        string severity = "Error") => issues.Add(new QuantitySurveyTenderBoqIssueDto
    {
        RowNumber = rowNumber, Code = code, Message = message, Severity = severity
    });

    private static string NormalizeFormula(string value)
        => value.Replace("$", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(right ?? string.Empty);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static bool EqualText(string? left, string? right)
        => string.Equals(NormalizeText(left), NormalizeText(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeText(string? value)
        => string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeCorrelationId(string? value)
        => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];

    private static string SafeFileName(string value)
        => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        range.Style.Alignment.WrapText = true;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private sealed record SubmissionContext(
        TenderBid Bid,
        Tender Tender,
        BusinessPartner BusinessPartner,
        Project Project,
        ProjectBoqVersion Publication,
        List<BaselineRow> Rows,
        QsExternalSubmissionValue Policy);

    private sealed record BaselineRow(
        TenderBidItem BidItem,
        TenderItem TenderItem,
        ProjectBoqVersionLine BoqLine);

    private sealed record TemplateControl(
        Guid TenantId,
        Guid TenderBidId,
        Guid TenderId,
        Guid ProjectId,
        Guid BoqPublicationId,
        string BoqSnapshotHash,
        string TemplateVersion);

    private sealed record ParsedLine(
        int RowNumber,
        Guid TenderItemId,
        Guid ProjectBoqVersionLineId,
        Guid LineKey,
        string? LineNumber,
        string? ItemCode,
        string Description,
        string? UnitOfMeasure,
        decimal TenderQuantity,
        decimal OfferedQuantity,
        decimal UnitPrice,
        decimal SubmittedLineTotal,
        decimal CalculatedLineTotal,
        decimal ArithmeticDifference,
        string ComparisonStatus,
        IReadOnlyList<QuantitySurveyTenderBoqIssueDto> Findings)
    {
        public bool IsResolved => TenderItemId != Guid.Empty && ProjectBoqVersionLineId != Guid.Empty && LineKey != Guid.Empty;
        public static ParsedLine Unresolved(int rowNumber) => new(rowNumber, Guid.Empty, Guid.Empty, Guid.Empty,
            null, null, string.Empty, null, 0m, 0m, 0m, 0m, 0m, 0m, "Invalid", []);
    }
}
