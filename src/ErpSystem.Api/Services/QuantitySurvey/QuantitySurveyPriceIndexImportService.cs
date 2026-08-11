using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using ErpSystem.Api.Services.Spreadsheets;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyPriceIndexImportService : IQuantitySurveyPriceIndexImportService
{
    private const string TemplateVersion = "1";
    private const int MaximumFileBytes = 10 * 1024 * 1024;
    private const int MaximumRows = 2_000;
    private const int HeaderRow = 4;
    private const int FirstDataRow = HeaderRow + 1;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlySet<string> ImportStatuses = new HashSet<string>(
        ["Staged", "Invalid", "PendingApproval", "Approved", "Rejected"],
        StringComparer.Ordinal);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;
    private readonly ITimeLimitedDataProtector _templateProtector;

    public QuantitySurveyPriceIndexImportService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
        _templateProtector = dataProtectionProvider
            .CreateProtector("ErpSystem.QuantitySurvey.PriceIndexImport.Template.v1")
            .ToTimeLimitedDataProtector();
    }

    private Guid TenantId => _currentUser.TenantId is { } value && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var value) && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName) ? UserId.ToString() : _currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", _currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyPriceIndexFileDto> CreateTemplateAsync(Guid indexFamilyId, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(indexFamilyId, DateTime.UtcNow, cancellationToken);
        if (context.ImportFormat == "CSV")
        {
            return new QuantitySurveyPriceIndexFileDto
            {
                Content = new UTF8Encoding(true).GetBytes("Period,IndexValue,PublicationDate,SourceReference\r\n2026-01,100.000000,2026-02-15,GSS bulletin reference\r\n"),
                ContentType = "text/csv",
                FileName = $"{SafeName(context.Family.Code)}-price-index-import.csv"
            };
        }

        if (context.ImportFormat != "Controlled Excel")
            throw new QuantitySurveyPriceIndexImportValidationException("The configured API import format requires a provider adapter and cannot use a manual workbook template.");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Index Values");
        sheet.Cell(1, 1).Value = "TDC Quantity Survey Price Index Import";
        sheet.Cell(2, 1).Value = $"Family: {context.Family.Code} · {context.Family.Name} · {context.Family.Source}";
        var headers = new[] { "Period", "IndexValue", "PublicationDate", "SourceReference" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(HeaderRow, index + 1).Value = headers[index];
        sheet.Cell(FirstDataRow, 1).Value = DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day);
        sheet.Cell(FirstDataRow, 1).Style.DateFormat.Format = "yyyy-MM";
        sheet.Cell(FirstDataRow, 2).Value = 100m;
        sheet.Cell(FirstDataRow, 3).Value = DateTime.UtcNow.Date;
        sheet.Cell(FirstDataRow, 3).Style.DateFormat.Format = "yyyy-MM-dd";
        sheet.Cell(FirstDataRow, 4).Value = "Publisher bulletin reference";
        sheet.Range(HeaderRow, 1, HeaderRow, headers.Length).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        var controlSheet = workbook.Worksheets.Add("Control");
        var control = new TemplateControl(TenantId, context.Family.Id, context.Family.Source, TemplateVersion);
        controlSheet.Cell("A1").Value = "TemplateVersion";
        controlSheet.Cell("B1").Value = TemplateVersion;
        controlSheet.Cell("A2").Value = "IndexFamilyId";
        controlSheet.Cell("B2").Value = context.Family.Id.ToString();
        controlSheet.Cell("A3").Value = "ControlToken";
        controlSheet.Cell("B3").Value = _templateProtector.Protect(JsonSerializer.Serialize(control, JsonOptions), TimeSpan.FromDays(30));
        controlSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return new QuantitySurveyPriceIndexFileDto
        {
            Content = memory.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"{SafeName(context.Family.Code)}-price-index-import-v{TemplateVersion}.xlsx"
        };
    }

    public async Task<QuantitySurveyPriceIndexImportDto> StageAsync(
        Guid indexFamilyId,
        Stream stream,
        string fileName,
        string contentType,
        StageQuantitySurveyPriceIndexImportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw new QuantitySurveyPriceIndexImportValidationException("A client request ID is required for safe retry.");
        var reason = RequireText(request.Reason, "Staging reason", 1000);
        var context = await LoadContextAsync(indexFamilyId, DateTime.UtcNow, cancellationToken);
        var role = await RequireAuthorityRoleAsync(request.AuthorityRoleId, context.Authority, cancellationToken);

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0 || memory.Length > MaximumFileBytes)
            throw new QuantitySurveyPriceIndexImportValidationException($"Source file size must be between 1 byte and {MaximumFileBytes / 1024 / 1024} MB.");
        var bytes = memory.ToArray();
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
            throw new QuantitySurveyPriceIndexImportValidationException("Source file name is required and cannot exceed 255 characters.");
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        ValidateExtension(context.ImportFormat, extension);
        var fileHash = Hash(bytes);

        var existing = await ImportQuery().FirstOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            if (!await StageRequestMatchesAsync(existing, indexFamilyId, request.AuthorityRoleId, fileHash, reason, cancellationToken))
                throw new QuantitySurveyPriceIndexImportConflictException("This client request ID was already used with a different index import payload.");
            return Map(existing);
        }

        var issues = new List<QuantitySurveyPriceIndexImportIssueDto>();
        List<ImportRow> rows;
        try
        {
            rows = context.ImportFormat switch
            {
                "Controlled Excel" => ReadExcel(bytes, context, issues),
                "CSV" => ReadCsv(bytes, issues),
                _ => throw new QuantitySurveyPriceIndexImportValidationException("The configured API import format requires a provider adapter and cannot accept a manual file.")
            };
        }
        catch (QuantitySurveyPriceIndexImportException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new QuantitySurveyPriceIndexImportValidationException("The controlled source file could not be read. Download a fresh template and verify that the file is not damaged.");
        }
        ValidateRows(rows, issues);
        var normalizedJson = JsonSerializer.Serialize(rows, JsonOptions);
        var normalizedHash = Hash(Encoding.UTF8.GetBytes(normalizedJson));
        var batchId = Guid.NewGuid();
        var canonicalContentType = extension == ".xlsx"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";

        var upload = await _controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId,
            ActorUserId = UserId,
            ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyPriceIndexImport,
            FileName = safeFileName,
            ContentType = canonicalContentType,
            FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, writable: false)
        }, cancellationToken);
        if (!FixedEquals(fileHash, upload.ChecksumSha256))
        {
            await _controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            throw new QuantitySurveyPriceIndexImportConflictException("The centrally stored source checksum does not match the validated import file.");
        }

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
                SourceLabel = "Quantity Survey price-index import",
                SourceEntityType = nameof(QuantitySurveyPriceIndexImportBatch),
                SourceRecordId = batchId,
                SourceRecordReference = context.Family.Code,
                Title = $"{context.Family.Code} price-index import - {upload.Record.OriginalFileName}",
                DocumentType = "PriceIndexSource",
                AccessProfile = "Module restricted",
                VersionStatus = issues.Count == 0 ? "Validated" : "Validation failed",
                ChangeSummary = "Original price-index source retained for validation, approval and audit history.",
                RequirePublishedGovernance = false,
                MetadataValues =
                [
                    new("indexFamilyId", "Index family ID", context.Family.Id.ToString(), "guid"),
                    new("indexFamilyCode", "Index family code", context.Family.Code),
                    new("indexSource", "Index source", context.Family.Source.ToString()),
                    new("fileHash", "File SHA-256", fileHash),
                    new("lineCount", "Line count", rows.Count.ToString(CultureInfo.InvariantCulture), "number"),
                    new("errorCount", "Error count", issues.Count.ToString(CultureInfo.InvariantCulture), "number")
                ]
            }, cancellationToken);
        }
        catch
        {
            await _controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            throw;
        }

        var now = DateTime.UtcNow;
        var batch = new QuantitySurveyPriceIndexImportBatch
        {
            Id = batchId,
            TenantId = TenantId,
            IndexFamilyId = context.Family.Id,
            IndexSource = context.Family.Source,
            ImportFormat = context.ImportFormat,
            OriginalFileName = upload.Record.OriginalFileName,
            FileHash = fileHash,
            NormalizedPayloadHash = normalizedHash,
            NormalizedPayloadJson = normalizedJson,
            IssuesJson = JsonSerializer.Serialize(issues, JsonOptions),
            CentralDocumentRecordId = document.DocumentRecordId,
            CentralDocumentVersionId = document.DocumentVersionId,
            FileUploadRecordId = document.FileUploadRecordId,
            ConfigurationProfileId = context.Profile.Id,
            ConfigurationDecisionId = context.Decision.Id,
            ApprovalWorkflowDefinitionId = context.Escalation.ApprovalWorkflowDefinitionId,
            AuthorityRoleId = role.Id,
            AuthorityRoleNameSnapshot = role.Name!,
            ClientRequestId = request.ClientRequestId,
            LineCount = rows.Count,
            ErrorCount = issues.Count,
            Status = issues.Count == 0 ? "Staged" : "Invalid",
            ApprovalStatus = "Draft",
            PreparedById = UserId,
            PreparedAt = now,
            AuditAction = QuantitySurveyAuditEventMap.StagePriceIndexImport,
            CorrelationId = NormalizeCorrelation(correlationId),
            ChangeReason = reason,
            ActorRoles = ActorRoles,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        // Invalid files must remain inspectable without attempting to persist placeholder
        // dates/zero values that the database correctly rejects. A valid import has no
        // issues, so all source rows are retained and later covered by the payload hash.
        batch.Values = rows.Select((row, index) => new { Row = row, Sequence = index + 1 })
            .Where(value => IsPersistable(value.Row))
            // Keep invalid duplicate-period sources inspectable without violating
            // the batch/period uniqueness constraint. The validation issue and
            // full normalized source payload still retain every duplicate row.
            .GroupBy(value => value.Row.IndexPeriod)
            .Select(group => group.First())
            .Select(value => new QuantitySurveyPriceIndexValue
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ImportBatchId = batch.Id,
            IndexFamilyId = context.Family.Id,
            Sequence = value.Sequence,
            IndexPeriod = value.Row.IndexPeriod,
            IndexValue = value.Row.IndexValue,
            PublicationDate = value.Row.PublicationDate,
            SourceReference = value.Row.SourceReference,
            Status = "Staged",
            ValueKey = Guid.NewGuid(),
            Version = 1,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        }).ToList();
        _db.QuantitySurveyPriceIndexImportBatches.Add(batch);
        AddRevision(batch, QuantitySurveyAuditEventMap.StagePriceIndexImport, reason, null, Snapshot(batch), correlationId);
        AddAudit(batch, QuantitySurveyAuditEventMap.StagePriceIndexImport, null, Snapshot(batch), correlationId);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            _db.ChangeTracker.Clear();
            await _centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, cancellationToken);
            var concurrent = await ImportQuery().FirstOrDefaultAsync(value => value.ClientRequestId == request.ClientRequestId, cancellationToken);
            if (concurrent is not null && await StageRequestMatchesAsync(concurrent, indexFamilyId, request.AuthorityRoleId, fileHash, reason, cancellationToken))
                return Map(concurrent);
            throw new QuantitySurveyPriceIndexImportConflictException("The same request or index periods were staged concurrently with a different payload. Refresh and try again.");
        }
        catch
        {
            await _centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, cancellationToken);
            throw;
        }
        return await GetAsync(batch.Id, cancellationToken);
    }

    public async Task<QuantitySurveyPriceIndexImportPageDto> ListAsync(QuantitySurveyPriceIndexImportListRequest request, CancellationToken cancellationToken = default)
    {
        var query = ImportQuery();
        if (request.IndexFamilyId.HasValue) query = query.Where(value => value.IndexFamilyId == request.IndexFamilyId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            if (!ImportStatuses.Contains(status))
                throw new QuantitySurveyPriceIndexImportValidationException("Select a valid controlled import status.");
            query = query.Where(value => value.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(value => value.IndexFamily.Code.Contains(search) || value.IndexFamily.Name.Contains(search) || value.OriginalFileName.Contains(search));
        }
        var page = Math.Clamp(request.Page, 1, 1_000_000);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var count = await query.CountAsync(cancellationToken);
        var values = await query.OrderByDescending(value => value.PreparedAt).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new QuantitySurveyPriceIndexImportPageDto { Items = values.Select(Map).ToList(), Page = page, PageSize = size, TotalCount = count };
    }

    public async Task<QuantitySurveyPriceIndexImportDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => Map(await RequiredAsync(id, false, cancellationToken));

    public async Task<QuantitySurveyPriceIndexImportDto> SubmitAsync(Guid id, QuantitySurveyPriceIndexImportLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var entity = await RequiredAsync(id, true, cancellationToken);
            if (entity.Status is "PendingApproval" or "Approved")
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            CheckVersion(entity.RowVersion, request.RowVersion);
            if (entity.Status != "Staged" || entity.ErrorCount != 0 || entity.Values.Count == 0)
                throw new QuantitySurveyPriceIndexImportConflictException("Only a valid Staged import can be submitted.");
            var reason = RequireText(request.Reason, "Submission reason", 1000);
            await ValidateStoredAsync(entity, cancellationToken);
            var before = Snapshot(entity);
            var result = await _workflow.SubmitAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, entity.ApprovalWorkflowDefinitionId);
            if (!result.ExecutionResult.Success)
                throw new QuantitySurveyPriceIndexImportConflictException(result.ExecutionResult.Message ?? "The configured index approval workflow could not be started.");
            _workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplySubmitOutcome(entity, result.Outcome, UserId);
            if (result.Outcome == WorkflowOutcome.Approved)
            {
                entity.Status = "PendingApproval";
                entity.ApprovalStatus = "Pending";
                entity.ApprovedById = null;
                entity.ApprovedAt = null;
            }
            else if (result.Outcome == WorkflowOutcome.Rejected)
            {
                entity.RejectionReason = reason;
            }
            entity.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            entity.SubmittedById = UserId;
            entity.SubmittedAt = DateTime.UtcNow;
            Touch(entity, QuantitySurveyAuditEventMap.SubmitPriceIndexImport, reason, correlationId);
            if (result.Outcome == WorkflowOutcome.Rejected)
            {
                // Persist the parent terminal state before its SQL-guarded child
                // transitions. The surrounding transaction keeps both saves atomic.
                await SaveAsync(cancellationToken);
                foreach (var value in entity.Values) value.Status = "Rejected";
            }
            AddRevision(entity, QuantitySurveyAuditEventMap.SubmitPriceIndexImport, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.SubmitPriceIndexImport, before, Snapshot(entity), correlationId);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return await GetAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyPriceIndexImportDto> ApproveAsync(Guid id, QuantitySurveyPriceIndexImportLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, true, cancellationToken);
        if (entity.Status == "Approved") return Map(entity);
        CheckVersion(entity.RowVersion, request.RowVersion);
        if (entity.Status != "PendingApproval")
            throw new QuantitySurveyPriceIndexImportConflictException("The import must be PendingApproval before approval.");
        if (entity.PreparedById == UserId)
            throw new QuantitySurveyPriceIndexImportConflictException("Maker-checker control prevents the import preparer from approving it.");
        RequireAuthorityRole(entity);
        var reason = RequireText(request.Reason, "Approval reason", 1000);
        await ValidateStoredAsync(entity, cancellationToken);
        var status = await WorkflowStatusAsync(entity, cancellationToken);
        WorkflowOutcome outcome;
        if (status == WorkflowInstanceStatus.Completed) outcome = WorkflowOutcome.Approved;
        else
        {
            if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                throw new QuantitySurveyPriceIndexImportConflictException("The index approval workflow ended without approval.");
            if (!await _workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId))
                throw new UnauthorizedAccessException("You are not assigned to the current index approval step.");
            var result = await _workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId, "Approve", reason);
            if (!result.ExecutionResult.Success)
                throw new QuantitySurveyPriceIndexImportConflictException(result.ExecutionResult.Message ?? "The index import approval could not be processed.");
            outcome = result.Outcome;
        }
        if (outcome == WorkflowOutcome.Approved)
            await FinalizeApprovalAsync(entity.Id, reason, correlationId, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<QuantitySurveyPriceIndexImportDto> RejectAsync(Guid id, QuantitySurveyPriceIndexImportLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var entity = await RequiredAsync(id, true, cancellationToken);
            if (entity.Status == "Rejected") { await transaction.CommitAsync(cancellationToken); return; }
            CheckVersion(entity.RowVersion, request.RowVersion);
            if (entity.Status != "PendingApproval")
                throw new QuantitySurveyPriceIndexImportConflictException("The import must be PendingApproval before rejection.");
            if (entity.PreparedById == UserId)
                throw new QuantitySurveyPriceIndexImportConflictException("Maker-checker control prevents the import preparer from rejecting it.");
            RequireAuthorityRole(entity);
            var reason = RequireText(request.Reason, "Rejection reason", 1000);
            var status = await WorkflowStatusAsync(entity, cancellationToken);
            if (status == WorkflowInstanceStatus.Completed)
                throw new QuantitySurveyPriceIndexImportConflictException("The completed index workflow is approved and cannot be rejected.");
            WorkflowOutcome outcome;
            if (status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed) outcome = WorkflowOutcome.Rejected;
            else
            {
                if (!await _workflow.CanUserApproveAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId))
                    throw new UnauthorizedAccessException("You are not assigned to the current index approval step.");
                var result = await _workflow.ProcessApprovalAsync(QuantitySurveyWorkflowBindingRegistry.Escalation, entity.Id, UserId, "Reject", reason);
                if (!result.ExecutionResult.Success)
                    throw new QuantitySurveyPriceIndexImportConflictException(result.ExecutionResult.Message ?? "The index import rejection could not be processed.");
                outcome = result.Outcome;
            }
            if (outcome != WorkflowOutcome.Rejected)
                throw new QuantitySurveyPriceIndexImportConflictException("The shared workflow did not return a rejected outcome.");
            var before = Snapshot(entity);
            _workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplyApprovalOutcome(entity, outcome, UserId, reason);
            Touch(entity, QuantitySurveyAuditEventMap.RejectPriceIndexImport, reason, correlationId);
            await SaveAsync(cancellationToken);
            foreach (var value in entity.Values) value.Status = "Rejected";
            AddRevision(entity, QuantitySurveyAuditEventMap.RejectPriceIndexImport, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.RejectPriceIndexImport, before, Snapshot(entity), correlationId);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<QuantitySurveyPriceIndexImportRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await RequiredAsync(id, false, cancellationToken);
        return await _db.QuantitySurveyPriceIndexImportRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ImportBatchId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new QuantitySurveyPriceIndexImportRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorUserId = value.ActorUserId, ActorName = value.ActorName,
                ActorRoles = value.ActorRoles, CorrelationId = value.CorrelationId, Reason = value.Reason,
                BeforeJson = value.BeforeJson, AfterJson = value.AfterJson, CreatedAt = value.CreatedAt
            }).ToListAsync(cancellationToken);
    }

    private async Task FinalizeApprovalAsync(Guid id, string reason, string correlationId, CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var entity = await RequiredAsync(id, true, cancellationToken);
            if (entity.Status == "Approved") { await transaction.CommitAsync(cancellationToken); return; }
            if (entity.PreparedById == UserId)
                throw new QuantitySurveyPriceIndexImportConflictException("Maker-checker control prevents the import preparer from approving it.");
            RequireAuthorityRole(entity);
            await ValidateStoredAsync(entity, cancellationToken);
            var before = Snapshot(entity);
            var periods = entity.Values.Select(value => value.IndexPeriod).ToList();
            var current = await _db.QuantitySurveyPriceIndexValues
                .Where(value => value.TenantId == TenantId && value.IndexFamilyId == entity.IndexFamilyId && periods.Contains(value.IndexPeriod) && value.IsCurrent && value.Status == "Approved" && !value.IsDeleted)
                .ToDictionaryAsync(value => value.IndexPeriod, cancellationToken);

            // Retire current rows first inside the same serializable transaction.
            // EF can otherwise send the replacement UPDATE before the retirement
            // UPDATE and transiently violate the filtered one-current-value index.
            foreach (var previous in current.Values)
            {
                previous.IsCurrent = false;
                previous.Status = "Superseded";
                previous.UpdatedAt = DateTime.UtcNow;
                previous.UpdatedBy = UserName;
                previous.LastModifiedById = UserId;
            }
            if (current.Count > 0)
                await SaveAsync(cancellationToken);

            _workflowAdapters.GetAdapter(QuantitySurveyWorkflowBindingRegistry.Escalation).ApplyApprovalOutcome(entity, WorkflowOutcome.Approved, UserId, null);
            Touch(entity, QuantitySurveyAuditEventMap.ApprovePriceIndexImport, reason, correlationId);
            // Persist the parent terminal state before the SQL-guarded value
            // transitions; the transaction prevents any partial visibility.
            await SaveAsync(cancellationToken);

            foreach (var value in entity.Values)
            {
                if (current.TryGetValue(value.IndexPeriod, out var previous))
                {
                    value.ValueKey = previous.ValueKey;
                    value.Version = previous.Version + 1;
                    value.SupersedesValueId = previous.Id;
                }
                value.Status = "Approved";
                value.IsCurrent = true;
                value.UpdatedAt = DateTime.UtcNow;
                value.UpdatedBy = UserName;
                value.LastModifiedById = UserId;
            }
            AddRevision(entity, QuantitySurveyAuditEventMap.ApprovePriceIndexImport, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.ApprovePriceIndexImport, before, Snapshot(entity), correlationId);
            try
            {
                await SaveAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUnique(exception))
            {
                throw new QuantitySurveyPriceIndexImportConflictException("Another approved index revision became current for one or more periods. Refresh and retry against the latest history.");
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task ValidateStoredAsync(QuantitySurveyPriceIndexImportBatch entity, CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(entity.IndexFamilyId, DateTime.UtcNow, cancellationToken);
        if (entity.ConfigurationProfileId != context.Profile.Id || entity.ConfigurationDecisionId != context.Decision.Id || entity.ApprovalWorkflowDefinitionId != context.Escalation.ApprovalWorkflowDefinitionId || entity.IndexSource != context.Family.Source || entity.ImportFormat != context.ImportFormat)
            throw new QuantitySurveyPriceIndexImportConflictException("The effective QS-DEC-006 index policy changed. Stage a new source file under the current policy.");
        if (!context.Authority.ApproverRoleIds.Contains(entity.AuthorityRoleId) || !await _db.Roles.AsNoTracking().AnyAsync(value => value.Id == entity.AuthorityRoleId && value.Name == entity.AuthorityRoleNameSnapshot, cancellationToken))
            throw new QuantitySurveyPriceIndexImportConflictException("The configured approval authority is no longer permitted by QS-DEC-001.");
        var evidenceCurrent = await _db.CentralDocumentVersions.AsNoTracking().Include(value => value.DocumentRecord)
            .AnyAsync(value => value.TenantId == TenantId && value.Id == entity.CentralDocumentVersionId && value.DocumentRecordId == entity.CentralDocumentRecordId && value.Status == "Validated" && value.DocumentRecord.CurrentVersion == value.VersionNumber && value.DocumentRecord.VersionStatus == "Validated" && value.DocumentRecord.SourceModule == "QuantitySurvey" && value.DocumentRecord.SourceEntityType == nameof(QuantitySurveyPriceIndexImportBatch) && value.DocumentRecord.SourceRecordId == entity.Id && value.DocumentRecord.Notes != null && value.DocumentRecord.Notes.StartsWith("Document type: PriceIndexSource") && !value.IsDeleted && !value.DocumentRecord.IsDeleted && value.DocumentRecord.LifecycleStatus == "Active", cancellationToken);
        var uploadClean = await _db.FileUploadRecords.AsNoTracking().AnyAsync(value => value.TenantId == TenantId && value.Id == entity.FileUploadRecordId && !value.IsDeleted && value.VirusScanStatus == FileVirusScanStatus.Clean, cancellationToken);
        if (!evidenceCurrent || !uploadClean)
            throw new QuantitySurveyPriceIndexImportConflictException("The central-DMS source evidence is no longer current and clean.");
        if (entity.ErrorCount != 0 || entity.Values.Count != entity.LineCount || entity.Values.Count == 0)
            throw new QuantitySurveyPriceIndexImportConflictException("The staged index values no longer match the validated import summary.");
        var rows = entity.Values.OrderBy(value => value.Sequence).Select(value => new ImportRow(value.IndexPeriod, value.IndexValue, value.PublicationDate, value.SourceReference)).ToList();
        if (!FixedEquals(entity.NormalizedPayloadHash, Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows, JsonOptions)))))
            throw new QuantitySurveyPriceIndexImportConflictException("The staged price-index payload failed its integrity check.");
    }

    private async Task<ImportContext> LoadContextAsync(Guid familyId, DateTime effectiveAt, CancellationToken cancellationToken)
    {
        if (familyId == Guid.Empty) throw new QuantitySurveyPriceIndexImportValidationException("Select a controlled index family.");
        var date = effectiveAt.Date;
        var profile = await _db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.EffectiveFrom <= date && (!value.EffectiveTo.HasValue || value.EffectiveTo >= date))
            .OrderByDescending(value => value.EffectiveFrom).ThenByDescending(value => value.Version).FirstOrDefaultAsync(cancellationToken)
            ?? throw new QuantitySurveyPriceIndexImportValidationException("No Published QS configuration profile is effective for the selected date.");
        var decisions = await _db.QuantitySurveyConfigurationDecisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted && (value.DecisionKey == "QS-DEC-001" || value.DecisionKey == "QS-DEC-006") && value.Status == QuantitySurveyConfigurationDecisionStatus.Approved && value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved && value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified && (!value.EffectiveFrom.HasValue || value.EffectiveFrom.Value <= date) && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= date))
            .ToListAsync(cancellationToken);
        var authorityDecision = RequireSingleDecision(decisions, "QS-DEC-001", "authority");
        var escalationDecision = RequireSingleDecision(decisions, "QS-DEC-006", "index");
        var authority = ReadDecision<QsRolesAuthorityValue>(authorityDecision, "QS-DEC-001");
        var escalation = ReadDecision<QsEscalationValue>(escalationDecision, "QS-DEC-006");
        if (authority.ApproverRoleIds is not { Count: > 0 })
            throw new QuantitySurveyPriceIndexImportValidationException("QS-DEC-001 must configure at least one approval authority role.");
        if (escalation.IndexSources is not { Count: > 0 })
            throw new QuantitySurveyPriceIndexImportValidationException("QS-DEC-006 must configure at least one permitted index source.");
        var family = await _db.QuantitySurveyPriceIndexFamilies.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == familyId && !value.IsDeleted && value.IsActive, cancellationToken)
            ?? throw new QuantitySurveyPriceIndexImportValidationException("The selected index family is inactive or unavailable in this tenant.");
        if (!escalation.IndexSources.Contains(family.Source))
            throw new QuantitySurveyPriceIndexImportValidationException("The selected index family source is not permitted by QS-DEC-006.");
        var workflowValid = await _db.WorkflowDefinitions.AsNoTracking().Include(value => value.EntityType).AnyAsync(value => value.TenantId == TenantId && value.Id == escalation.ApprovalWorkflowDefinitionId && !value.IsDeleted && value.IsActive && value.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && !value.EntityType.IsDeleted && value.EntityType.IsActive && value.EntityType.Code == QuantitySurveyWorkflowBindingRegistry.Escalation, cancellationToken);
        if (!workflowValid) throw new QuantitySurveyPriceIndexImportValidationException("QS-DEC-006 must reference an active Published QS escalation workflow definition.");
        return new ImportContext(profile, escalationDecision, escalation, authority, family, NormalizeImportFormat(escalation.ImportFormat));
    }

    private async Task<ApplicationRole> RequireAuthorityRoleAsync(Guid roleId, QsRolesAuthorityValue authority, CancellationToken cancellationToken)
    {
        if (!authority.ApproverRoleIds.Contains(roleId))
            throw new QuantitySurveyPriceIndexImportValidationException("Select an approval authority permitted by QS-DEC-001.");
        return await _db.Roles.AsNoTracking().FirstOrDefaultAsync(value => value.Id == roleId && value.Name != null, cancellationToken)
            ?? throw new QuantitySurveyPriceIndexImportValidationException("The selected approval authority role is unavailable.");
    }

    private static QuantitySurveyConfigurationDecision RequireSingleDecision(
        IReadOnlyCollection<QuantitySurveyConfigurationDecision> decisions,
        string decisionKey,
        string decisionName)
    {
        var matches = decisions.Where(value => value.DecisionKey == decisionKey).Take(2).ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new QuantitySurveyPriceIndexImportValidationException($"The effective profile has no approved {decisionKey} {decisionName} decision."),
            _ => throw new QuantitySurveyPriceIndexImportValidationException($"The effective profile has multiple approved {decisionKey} {decisionName} decisions. Correct the configuration before importing an index.")
        };
    }

    private static TDecision ReadDecision<TDecision>(QuantitySurveyConfigurationDecision decision, string decisionKey)
        where TDecision : class
    {
        try
        {
            var value = JsonSerializer.Deserialize<TDecision>(decision.ValueJson, JsonOptions)
                ?? throw new QuantitySurveyPriceIndexImportValidationException($"{decisionKey} could not be read.");
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(value, new ValidationContext(value), validationResults, validateAllProperties: true))
                throw new QuantitySurveyPriceIndexImportValidationException($"{decisionKey} is invalid: {validationResults[0].ErrorMessage}");
            return value;
        }
        catch (JsonException)
        {
            throw new QuantitySurveyPriceIndexImportValidationException($"{decisionKey} contains invalid configuration data.");
        }
        catch (NotSupportedException)
        {
            throw new QuantitySurveyPriceIndexImportValidationException($"{decisionKey} contains unsupported configuration data.");
        }
    }

    private void RequireAuthorityRole(QuantitySurveyPriceIndexImportBatch entity)
    {
        if (!_currentUser.Roles.Any(value => string.Equals(value, entity.AuthorityRoleNameSnapshot, StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("Your assigned roles do not include the configured index approval authority.");
    }

    private async Task<WorkflowInstanceStatus?> WorkflowStatusAsync(QuantitySurveyPriceIndexImportBatch entity, CancellationToken cancellationToken)
        => !entity.WorkflowInstanceId.HasValue ? null : (await _db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == entity.WorkflowInstanceId && value.EntityId == entity.Id, cancellationToken))?.Status;

    private List<ImportRow> ReadExcel(byte[] bytes, ImportContext context, ICollection<QuantitySurveyPriceIndexImportIssueDto> issues)
    {
        SpreadsheetSecurityInspector.ValidateXlsxPackage(bytes, maximumWorksheets: 2);
        if (SpreadsheetSecurityInspector.HasVbaProject(bytes)) throw new QuantitySurveyPriceIndexImportValidationException("VBA projects and macros are not allowed in price-index workbooks.");
        if (SpreadsheetSecurityInspector.HasExternalRelationships(bytes)) throw new QuantitySurveyPriceIndexImportValidationException("External links and workbook relationships are not allowed in price-index workbooks.");
        using var workbook = new XLWorkbook(new MemoryStream(bytes, writable: false));
        ValidateControl(workbook, context, issues);
        if (!workbook.TryGetWorksheet("Index Values", out var sheet))
        {
            AddIssue(issues, null, "Workbook", "SHEET_MISSING", "The 'Index Values' worksheet is missing.");
            return [];
        }
        var headers = new[] { "Period", "IndexValue", "PublicationDate", "SourceReference" };
        for (var index = 0; index < headers.Length; index++)
            if (!string.Equals(sheet.Cell(HeaderRow, index + 1).GetString().Trim(), headers[index], StringComparison.Ordinal))
                AddIssue(issues, HeaderRow, headers[index], "HEADER_INVALID", $"Column {index + 1} must be '{headers[index]}'.");
        var finalAllowedRow = FirstDataRow + MaximumRows - 1;
        var last = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? HeaderRow, finalAllowedRow);
        var result = new List<ImportRow>();
        for (var row = FirstDataRow; row <= last; row++)
        {
            if (sheet.Range(row, 1, row, 4).Cells().All(cell => cell.IsEmpty())) continue;
            if (sheet.Range(row, 1, row, 4).Cells().Any(cell => cell.HasFormula))
            {
                AddIssue(issues, row, "Row", "FORMULA_NOT_ALLOWED", "Formulas are not allowed in price-index data rows.");
                continue;
            }
            result.Add(ParseRow(
                row,
                sheet.Cell(row, 1).GetFormattedString(CultureInfo.InvariantCulture),
                sheet.Cell(row, 2).GetFormattedString(CultureInfo.InvariantCulture),
                sheet.Cell(row, 3).GetFormattedString(CultureInfo.InvariantCulture),
                sheet.Cell(row, 4).GetFormattedString(CultureInfo.InvariantCulture),
                issues));
        }
        if ((sheet.LastRowUsed()?.RowNumber() ?? HeaderRow) > finalAllowedRow)
            AddIssue(issues, null, "Workbook", "ROW_LIMIT", $"A maximum of {MaximumRows} data rows is allowed.");
        return result;
    }

    private void ValidateControl(XLWorkbook workbook, ImportContext context, ICollection<QuantitySurveyPriceIndexImportIssueDto> issues)
    {
        if (!workbook.TryGetWorksheet("Control", out var controlSheet))
        {
            AddIssue(issues, null, "Workbook", "CONTROL_MISSING", "The protected template control sheet is missing.");
            return;
        }
        try
        {
            var json = _templateProtector.Unprotect(controlSheet.Cell("B3").GetString(), out _);
            var control = JsonSerializer.Deserialize<TemplateControl>(json, JsonOptions);
            if (control is null || control.TenantId != TenantId || control.IndexFamilyId != context.Family.Id || control.IndexSource != context.Family.Source || control.TemplateVersion != TemplateVersion)
                AddIssue(issues, null, "Workbook", "CONTROL_TAMPERED", "The protected template controls do not match this tenant, index family, source, or template version.");
        }
        catch
        {
            AddIssue(issues, null, "Workbook", "CONTROL_INVALID", "The protected template control is invalid or expired. Download a fresh template.");
        }
    }

    private static List<ImportRow> ReadCsv(byte[] bytes, ICollection<QuantitySurveyPriceIndexImportIssueDto> issues)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            throw new QuantitySurveyPriceIndexImportValidationException("The CSV source must use valid UTF-8 text encoding.");
        }
        if (text.Contains('\0')) throw new QuantitySurveyPriceIndexImportValidationException("The CSV source contains invalid binary content.");
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (lines.Length == 0 || !TryParseCsvLine(lines[0], out var header) || !header.SequenceEqual(new[] { "Period", "IndexValue", "PublicationDate", "SourceReference" }, StringComparer.Ordinal))
            AddIssue(issues, 1, "Header", "HEADER_INVALID", "CSV header must be Period,IndexValue,PublicationDate,SourceReference.");
        var result = new List<ImportRow>();
        for (var index = 1; index < lines.Length && index <= MaximumRows; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            if (!TryParseCsvLine(lines[index], out var fields))
            {
                AddIssue(issues, index + 1, "Row", "CSV_QUOTE_INVALID", "The CSV row contains an unclosed quoted field.");
                continue;
            }
            if (fields.Count != 4)
            {
                AddIssue(issues, index + 1, "Row", "COLUMN_COUNT", "Each CSV row must contain exactly four columns.");
                continue;
            }
            result.Add(ParseRow(index + 1, fields[0], fields[1], fields[2], fields[3], issues));
        }
        if (lines.Skip(1).Count(line => !string.IsNullOrWhiteSpace(line)) > MaximumRows)
            AddIssue(issues, null, "CSV", "ROW_LIMIT", $"A maximum of {MaximumRows} data rows is allowed.");
        return result;
    }

    private static ImportRow ParseRow(int row, string periodText, string valueText, string publicationText, string referenceText, ICollection<QuantitySurveyPriceIndexImportIssueDto> issues)
    {
        var periodValid = ParsePeriod(periodText, out var periodValue);
        if (!periodValid) AddIssue(issues, row, "Period", "PERIOD_INVALID", "Period must be YYYY-MM or a date whose day is the first of the month.");
        else
        {
            var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            if (periodValue > currentMonth)
                AddIssue(issues, row, "Period", "PERIOD_FUTURE", "Index period cannot be later than the current month.");
        }
        if (!decimal.TryParse(valueText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var indexValue) || indexValue <= 0)
        {
            AddIssue(issues, row, "IndexValue", "VALUE_INVALID", "Index value must be a positive number using a dot decimal separator.");
            indexValue = 0;
        }
        else if (indexValue > 999_999_999_999.999999m)
            AddIssue(issues, row, "IndexValue", "VALUE_OUT_OF_RANGE", "Index value cannot exceed 999999999999.999999.");
        else if (decimal.Round(indexValue, 6) != indexValue)
            AddIssue(issues, row, "IndexValue", "VALUE_PRECISION", "Index value cannot contain more than six decimal places.");
        if (!DateTime.TryParseExact(publicationText.Trim(), new[] { "yyyy-MM-dd", "yyyy/M/d", "M/d/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var publicationDate))
        {
            AddIssue(issues, row, "PublicationDate", "PUBLICATION_DATE_INVALID", "Publication date must be a valid date, preferably YYYY-MM-DD.");
            publicationDate = default;
        }
        else if (publicationDate.Date > DateTime.UtcNow.Date)
            AddIssue(issues, row, "PublicationDate", "PUBLICATION_DATE_FUTURE", "Publication date cannot be in the future.");
        else if (periodValid && publicationDate.Date < periodValue)
            AddIssue(issues, row, "PublicationDate", "PUBLICATION_DATE_BEFORE_PERIOD", "Publication date cannot be before the index period.");
        var reference = referenceText.Trim();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
            AddIssue(issues, row, "SourceReference", "SOURCE_REFERENCE_INVALID", "Source reference is required and cannot exceed 120 characters.");
        return new ImportRow(periodValue, indexValue, publicationDate.Date, reference.Length <= 120 ? reference : reference[..120]);
    }

    private static void ValidateRows(IReadOnlyList<ImportRow> rows, ICollection<QuantitySurveyPriceIndexImportIssueDto> issues)
    {
        if (rows.Count == 0) AddIssue(issues, null, "Rows", "ROWS_REQUIRED", "At least one index row is required.");
        foreach (var duplicate in rows.Where(value => value.IndexPeriod != default).GroupBy(value => value.IndexPeriod).Where(group => group.Count() > 1))
            AddIssue(issues, null, "Period", "PERIOD_DUPLICATE", $"Period {duplicate.Key:yyyy-MM} appears more than once in the source file.");
    }

    private static bool ParsePeriod(string value, out DateTime period)
    {
        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            period = new DateTime(month.Year, month.Month, 1);
            return true;
        }
        if (DateTime.TryParseExact(value.Trim(), new[] { "yyyy-MM-dd", "yyyy/M/d", "M/d/yyyy", "d/M/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Day == 1)
        {
            period = date.Date;
            return true;
        }
        period = default;
        return false;
    }

    private static bool IsPersistable(ImportRow row)
        => row.IndexPeriod != default
           && row.IndexPeriod.Day == 1
           && row.IndexPeriod <= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
           && row.IndexValue > 0
           && row.IndexValue <= 999_999_999_999.999999m
           && decimal.Round(row.IndexValue, 6) == row.IndexValue
           && row.PublicationDate != default
           && row.PublicationDate >= row.IndexPeriod
           && row.PublicationDate <= DateTime.UtcNow.Date
           && !string.IsNullOrWhiteSpace(row.SourceReference)
           && row.SourceReference.Length <= 120;

    private static bool TryParseCsvLine(string line, out List<string> values)
    {
        values = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { values.Add(field.ToString().Trim()); field.Clear(); }
            else field.Append(character);
        }
        if (quoted) return false;
        values.Add(field.ToString().Trim());
        return true;
    }

    private IQueryable<QuantitySurveyPriceIndexImportBatch> ImportQuery() => _db.QuantitySurveyPriceIndexImportBatches.AsNoTracking()
        .Include(value => value.IndexFamily).Include(value => value.CentralDocumentVersion).ThenInclude(value => value.DocumentRecord).Include(value => value.Values)
        .Where(value => value.TenantId == TenantId && !value.IsDeleted);

    private async Task<QuantitySurveyPriceIndexImportBatch> RequiredAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? _db.QuantitySurveyPriceIndexImportBatches.AsTracking() : _db.QuantitySurveyPriceIndexImportBatches.AsNoTracking();
        return await query.Include(value => value.IndexFamily).Include(value => value.CentralDocumentVersion).ThenInclude(value => value.DocumentRecord).Include(value => value.Values)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new QuantitySurveyPriceIndexImportNotFoundException("The price-index import was not found.");
    }

    private async Task<bool> StageRequestMatchesAsync(
        QuantitySurveyPriceIndexImportBatch entity,
        Guid indexFamilyId,
        Guid authorityRoleId,
        string fileHash,
        string reason,
        CancellationToken cancellationToken)
    {
        if (entity.IndexFamilyId != indexFamilyId ||
            entity.AuthorityRoleId != authorityRoleId ||
            !FixedEquals(entity.FileHash, fileHash))
            return false;

        var stagedReason = await _db.QuantitySurveyPriceIndexImportRevisions
            .AsNoTracking()
            .Where(value => value.TenantId == TenantId &&
                            value.ImportBatchId == entity.Id &&
                            value.Action == QuantitySurveyAuditEventMap.StagePriceIndexImport &&
                            !value.IsDeleted)
            .OrderBy(value => value.CreatedAt)
            .Select(value => value.Reason)
            .FirstOrDefaultAsync(cancellationToken);
        return string.Equals(stagedReason, reason, StringComparison.Ordinal);
    }

    private QuantitySurveyPriceIndexImportDto Map(QuantitySurveyPriceIndexImportBatch value)
    {
        var issues = JsonSerializer.Deserialize<List<QuantitySurveyPriceIndexImportIssueDto>>(value.IssuesJson, JsonOptions) ?? [];
        return new QuantitySurveyPriceIndexImportDto
        {
            Id = value.Id, IndexFamilyId = value.IndexFamilyId, IndexFamilyCode = value.IndexFamily.Code, IndexFamilyName = value.IndexFamily.Name,
            IndexSource = value.IndexSource, ImportFormat = value.ImportFormat, OriginalFileName = value.OriginalFileName, FileHash = value.FileHash,
            CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId,
            EvidenceLabel = value.CentralDocumentVersion.DocumentRecord.DocumentReference + " · " + value.CentralDocumentVersion.DocumentRecord.Title,
            ConfigurationProfileId = value.ConfigurationProfileId, ConfigurationDecisionId = value.ConfigurationDecisionId,
            ApprovalWorkflowDefinitionId = value.ApprovalWorkflowDefinitionId, AuthorityRoleId = value.AuthorityRoleId, AuthorityRoleName = value.AuthorityRoleNameSnapshot,
            LineCount = value.LineCount, ErrorCount = value.ErrorCount, Status = value.Status, ApprovalStatus = value.ApprovalStatus,
            PreparedById = value.PreparedById, PreparedAt = value.PreparedAt, SubmittedById = value.SubmittedById, SubmittedAt = value.SubmittedAt,
            ApprovedById = value.ApprovedById, ApprovedAt = value.ApprovedAt, RejectionReason = value.RejectionReason,
            RowVersion = Convert.ToBase64String(value.RowVersion), Issues = issues,
            Values = value.Values.OrderBy(item => item.Sequence).Select(item => new QuantitySurveyPriceIndexValueDto
            {
                Id = item.Id, Sequence = item.Sequence, IndexPeriod = item.IndexPeriod, IndexValue = item.IndexValue,
                PublicationDate = item.PublicationDate, SourceReference = item.SourceReference, Status = item.Status,
                IsCurrent = item.IsCurrent, ValueKey = item.ValueKey, Version = item.Version, SupersedesValueId = item.SupersedesValueId
            }).ToList()
        };
    }

    private static object Snapshot(QuantitySurveyPriceIndexImportBatch value) => new
    {
        value.Id, value.IndexFamilyId, value.IndexSource, value.ImportFormat, value.OriginalFileName, value.FileHash,
        value.NormalizedPayloadHash, value.CentralDocumentRecordId, value.CentralDocumentVersionId, value.ConfigurationProfileId,
        value.ConfigurationDecisionId, value.ApprovalWorkflowDefinitionId, value.AuthorityRoleId, value.LineCount, value.ErrorCount,
        value.Status, value.ApprovalStatus, value.PreparedById, value.SubmittedById, value.ApprovedById, value.ApprovedAt,
        Values = value.Values.OrderBy(item => item.Sequence).Select(item => new { item.Sequence, item.IndexPeriod, item.IndexValue, item.PublicationDate, item.SourceReference, item.Status, item.IsCurrent, item.ValueKey, item.Version, item.SupersedesValueId })
    };

    private void AddRevision(QuantitySurveyPriceIndexImportBatch entity, string action, string reason, object? before, object after, string correlationId)
        => _db.QuantitySurveyPriceIndexImportRevisions.Add(new QuantitySurveyPriceIndexImportRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ImportBatchId = entity.Id, Action = action, ActorUserId = UserId,
            ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = NormalizeCorrelation(correlationId), Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(QuantitySurveyPriceIndexImportBatch entity, string action, object? before, object after, string correlationId)
        => _db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action, Resource = nameof(QuantitySurveyPriceIndexImportBatch),
            ResourceId = entity.Id.ToString(), OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QS-0302", Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void Touch(QuantitySurveyPriceIndexImportBatch entity, string action, string reason, string correlationId)
    {
        entity.AuditAction = action; entity.ChangeReason = reason; entity.CorrelationId = NormalizeCorrelation(correlationId); entity.ActorRoles = ActorRoles;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new QuantitySurveyPriceIndexImportConflictException("The import changed after it was loaded. Refresh and try again."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: >= 51030 and <= 51039 } sql)
        { throw new QuantitySurveyPriceIndexImportConflictException(sql.Number switch
            {
                51030 => "The price-index import lifecycle transition is not permitted.",
                51031 => "The import no longer matches its tenant, family, policy, workflow, authority, clean DMS evidence, or row summary.",
                51032 => "Submitted or Approved price-index input rows are immutable.",
                51033 => "Approved current index values must retain one version per family and period.",
                51034 => "Price-index import revision history is append-only and tenant-bound.",
                _ => "The database rejected the price-index change because a governed control changed. Refresh and try again."
            }); }
    }

    private static void ValidateExtension(string format, string extension)
    {
        if (format == "Controlled Excel" && extension != ".xlsx") throw new QuantitySurveyPriceIndexImportValidationException("QS-DEC-006 requires a controlled .xlsx price-index workbook.");
        if (format == "CSV" && extension != ".csv") throw new QuantitySurveyPriceIndexImportValidationException("QS-DEC-006 requires a .csv price-index source file.");
        if (format is not ("Controlled Excel" or "CSV")) throw new QuantitySurveyPriceIndexImportValidationException("The configured API import format requires a provider adapter and cannot accept a manual file.");
    }

    private static string NormalizeImportFormat(string value) => value.Trim() switch
    {
        "Controlled Excel" => "Controlled Excel",
        "CSV" => "CSV",
        "API" => "API",
        _ => throw new QuantitySurveyPriceIndexImportValidationException("QS-DEC-006 import format must be Controlled Excel, CSV, or API.")
    };
    private static string RequireText(string? value, string label, int maximum) { var result = value?.Trim(); if (string.IsNullOrWhiteSpace(result)) throw new QuantitySurveyPriceIndexImportValidationException(label + " is required."); if (result.Length > maximum) throw new QuantitySurveyPriceIndexImportValidationException(label + $" cannot exceed {maximum} characters."); return result; }
    private static void CheckVersion(byte[] current, string supplied) { byte[] parsed; try { parsed = Convert.FromBase64String(supplied); } catch { throw new QuantitySurveyPriceIndexImportValidationException("A valid import row version is required."); } if (!current.SequenceEqual(parsed)) throw new QuantitySurveyPriceIndexImportConflictException("The import changed after it was loaded. Refresh and try again."); }
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string SafeName(string value) => new(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static bool IsUnique(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
    private static void AddIssue(ICollection<QuantitySurveyPriceIndexImportIssueDto> issues, int? row, string field, string code, string message)
        => issues.Add(new QuantitySurveyPriceIndexImportIssueDto { RowNumber = row, Field = field, Code = code, Message = message });
    private static JsonSerializerOptions CreateJsonOptions() { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); options.Converters.Add(new JsonStringEnumConverter()); return options; }

    private sealed record ImportContext(QuantitySurveyConfigurationProfile Profile, QuantitySurveyConfigurationDecision Decision, QsEscalationValue Escalation, QsRolesAuthorityValue Authority, QuantitySurveyPriceIndexFamily Family, string ImportFormat);
    private sealed record ImportRow(DateTime IndexPeriod, decimal IndexValue, DateTime PublicationDate, string SourceReference);
    private sealed record TemplateControl(Guid TenantId, Guid IndexFamilyId, QuantitySurveyIndexSource IndexSource, string TemplateVersion);
}
