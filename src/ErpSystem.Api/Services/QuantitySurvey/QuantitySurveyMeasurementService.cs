using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using CentralDocumentMetadataTemplateEntity = ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyMeasurementService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveyMeasurementService
{
    private const long MaximumAttachmentBytes = 10 * 1024 * 1024;
    private const int MaximumAttachments = 20;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyMeasurementLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default)
    {
        if (projectId == Guid.Empty) throw Validation("Select a project.");
        await RequireProjectAccessAsync(projectId);
        var lines = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            value.ItemType == ProjectBoqItemTypes.Item && !value.Version.IsDeleted &&
                            value.Version.Status == ProjectBoqVersionStatuses.Approved && value.Version.PublishedAt != null)
            .OrderByDescending(value => value.Version.VersionNumber).ThenBy(value => value.SortOrder)
            .Select(value => new QuantitySurveyMeasurementLookupDto
            {
                Id = value.Id,
                Label = (value.LineNumber ?? value.ItemCode ?? "Item") + " · " + value.Description,
                Group = $"BoQ v{value.Version.VersionNumber} · {value.UnitOfMeasure ?? "No UOM"}",
                Description = $"Approved quantity {value.Quantity:N4} · {value.MeasurementStandard ?? "No standard"} · {value.MeasurementCode ?? "No code"}"
            }).ToListAsync(token);
        var drawings = await db.Set<ProjectDrawing>().AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.ProjectId == projectId && !value.IsDeleted &&
                            (value.Status == ProjectDrawingStatuses.ApprovedForConstruction || value.Status == ProjectDrawingStatuses.ApprovedAsBuilt))
            .OrderBy(value => value.DrawingNumber).ThenByDescending(value => value.Revision)
            .Select(value => new QuantitySurveyMeasurementLookupDto
            {
                Id = value.Id,
                Label = value.DrawingNumber + " · Rev " + (value.Revision ?? "—"),
                Group = value.Discipline,
                Description = value.Title
            }).ToListAsync(token);
        return new() { ApprovedBoqLines = lines, ApprovedDrawings = drawings };
    }

    public async Task<QuantitySurveyMeasurementPageDto> ListAsync(QuantitySurveyMeasurementListRequest request, CancellationToken token = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status is not ("Draft" or "Recorded"))
            throw Validation("Select a valid measurement status.");
        var accessible = await AccessibleProjectIdsAsync();
        var query = Query().Where(value => accessible.Contains(value.ProjectId));
        if (request.ProjectId.HasValue) query = query.Where(value => value.ProjectId == request.ProjectId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(value => value.Status == request.Status);
        var total = await query.CountAsync(token);
        var items = await query.OrderByDescending(value => value.MeasurementDate).ThenByDescending(value => value.PreparedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(token);
        return new() { Items = items.Select(Map).ToList(), Page = request.Page, PageSize = request.PageSize, TotalCount = total };
    }

    public async Task<QuantitySurveyMeasurementDto> GetAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token);
        await RequireProjectAccessAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyMeasurementDto> CreateAsync(CreateQuantitySurveyMeasurementRequest request, string correlationId, CancellationToken token = default)
    {
        ValidateCreate(request);
        var normalized = Normalize(request.Title, request.SiteLocation, request.Lines);
        var requestHash = Hash(new
        {
            request.ProjectId, request.ProjectBoqVersionLineId, request.ProjectDrawingId, request.SourceType,
            normalized.Title, MeasurementDate = Utc(request.MeasurementDate), normalized.SiteLocation, Lines = normalized.Lines
        });
        var existing = await db.QuantitySurveyMeasurementSheets.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
        if (existing is not null)
        {
            if (!FixedEquals(existing.RequestHash, requestHash)) throw RetryConflict();
            return await GetAsync(existing.Id, token);
        }

        await RequireProjectAccessAsync(request.ProjectId);
        var context = await ResolveContextAsync(request.ProjectId, request.ProjectBoqVersionLineId,
            request.ProjectDrawingId, request.SourceType, Utc(request.MeasurementDate), normalized.SiteLocation, token);
        var resultId = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var duplicate = await db.QuantitySurveyMeasurementSheets.AsNoTracking()
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (duplicate is not null)
            {
                if (!FixedEquals(duplicate.RequestHash, requestHash)) throw RetryConflict();
                resultId = duplicate.Id; await transaction.CommitAsync(token); return;
            }
            var now = DateTime.UtcNow;
            var entity = new QuantitySurveyMeasurementSheet
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = request.ProjectId,
                ProjectBoqVersionId = context.Line.ProjectBoqVersionId, ProjectBoqVersionLineId = context.Line.Id,
                BoqLineKey = context.Line.LineKey, ProjectDrawingId = context.Drawing?.Id, SourceType = request.SourceType,
                SheetReference = $"QSM-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash, Title = normalized.Title,
                MeasurementDate = Utc(request.MeasurementDate), SiteLocation = normalized.SiteLocation,
                DrawingNumberSnapshot = context.Drawing?.DrawingNumber, DrawingRevisionSnapshot = context.Drawing?.Revision,
                DrawingStatusSnapshot = context.Drawing?.Status, BoqLineNumberSnapshot = context.Line.LineNumber,
                BoqItemCodeSnapshot = context.Line.ItemCode, BoqDescriptionSnapshot = context.Line.Description,
                UnitOfMeasureSnapshot = context.Line.UnitOfMeasure, MeasurementStandardSnapshot = context.Line.MeasurementStandard,
                MeasurementCodeSnapshot = context.Line.MeasurementCode, MeasurementRuleSnapshot = context.Line.MeasurementRule,
                BoqQuantitySnapshot = context.Line.Quantity, TotalMeasuredQuantity = QuantitySurveyMeasurementRules.Total(normalized.Lines),
                Status = "Draft", ConfigurationProfileId = context.Profile.Id, ConfigurationDecisionId = context.Decision.Id,
                EvidenceMetadataTemplateId = context.Template.Id, EvidenceMetadataTemplateCodeSnapshot = context.Template.TemplateCode,
                PolicyHash = context.PolicyHash, PreparedById = UserId, PreparedByName = UserName, PreparedAt = now,
                AuditAction = QuantitySurveyAuditEventMap.CreateMeasurementSheet, CorrelationId = Correlation(correlationId),
                ActorRoles = ActorRoles, CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.QuantitySurveyMeasurementSheets.Add(entity);
            AddLines(entity, normalized.Lines);
            var after = Snapshot(entity, normalized.Lines);
            AddRevision(entity, QuantitySurveyAuditEventMap.CreateMeasurementSheet, "Taking-off sheet created.", null, after, correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.CreateMeasurementSheet, null, after, correlationId);
            await SaveAsync(token); resultId = entity.Id; await transaction.CommitAsync(token);
        });
        return await GetAsync(resultId, token);
    }

    public async Task<QuantitySurveyMeasurementDto> UpdateAsync(Guid id, UpdateQuantitySurveyMeasurementRequest request, string correlationId, CancellationToken token = default)
    {
        ValidateMutation(request.ClientRequestId, request.RowVersion);
        var normalized = Normalize(request.Title, request.SiteLocation, request.Lines);
        var requestHash = Hash(new { MeasurementId = id, request.ProjectDrawingId, request.SourceType, normalized.Title,
            MeasurementDate = Utc(request.MeasurementDate), normalized.SiteLocation, Lines = normalized.Lines });
        var entity = await RequiredAsync(id, true, token);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(entity.LastMutationRequestHash, requestHash)) throw RetryConflict();
            return Map(entity);
        }
        EnsureDraft(entity);
        var context = await ResolveContextAsync(entity.ProjectId, entity.ProjectBoqVersionLineId,
            request.ProjectDrawingId, request.SourceType, Utc(request.MeasurementDate), normalized.SiteLocation, token);
        ValidateFrozenLineage(entity, context);
        ApplyRowVersion(entity, request.RowVersion);
        var before = Snapshot(entity, entity.Lines);
        db.QuantitySurveyMeasurementLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        AddLines(entity, normalized.Lines);
        entity.ProjectDrawingId = context.Drawing?.Id; entity.SourceType = request.SourceType;
        entity.Title = normalized.Title; entity.MeasurementDate = Utc(request.MeasurementDate); entity.SiteLocation = normalized.SiteLocation;
        entity.DrawingNumberSnapshot = context.Drawing?.DrawingNumber; entity.DrawingRevisionSnapshot = context.Drawing?.Revision;
        entity.DrawingStatusSnapshot = context.Drawing?.Status; entity.TotalMeasuredQuantity = QuantitySurveyMeasurementRules.Total(normalized.Lines);
        entity.LastMutationClientRequestId = request.ClientRequestId; entity.LastMutationRequestHash = requestHash;
        Touch(entity, QuantitySurveyAuditEventMap.UpdateMeasurementSheet, correlationId);
        var after = Snapshot(entity, normalized.Lines);
        AddRevision(entity, QuantitySurveyAuditEventMap.UpdateMeasurementSheet, "Draft taking-off sheet updated.", before, after, correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.UpdateMeasurementSheet, before, after, correlationId);
        await SaveAsync(token); return await GetAsync(id, token);
    }

    public async Task<QuantitySurveyMeasurementDto> RecordAsync(Guid id, RecordQuantitySurveyMeasurementRequest request, string correlationId, CancellationToken token = default)
    {
        ValidateMutation(request.ClientRequestId, request.RowVersion);
        var requestHash = Hash(new { MeasurementId = id, Action = "Record" });
        var entity = await RequiredAsync(id, true, token);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.LastMutationClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(entity.LastMutationRequestHash, requestHash)) throw RetryConflict();
            return Map(entity);
        }
        EnsureDraft(entity); ApplyRowVersion(entity, request.RowVersion);
        await ValidateFrozenContextAsync(entity, token);
        if (entity.Lines.Count == 0 || entity.TotalMeasuredQuantity <= 0) throw Validation("Add valid measurement rows before recording the sheet.");
        if (entity.Attachments.Count == 0) throw Validation("Attach at least one centrally governed evidence file before recording the sheet.");
        ValidateEvidence(entity);
        var before = Snapshot(entity, entity.Lines);
        entity.Status = "Recorded"; entity.RecordedById = UserId; entity.RecordedByName = UserName;
        entity.RecordedAt = DateTime.UtcNow; entity.LastMutationClientRequestId = request.ClientRequestId;
        entity.LastMutationRequestHash = requestHash; Touch(entity, QuantitySurveyAuditEventMap.RecordMeasurementSheet, correlationId);
        var after = Snapshot(entity, entity.Lines);
        AddRevision(entity, QuantitySurveyAuditEventMap.RecordMeasurementSheet, "Measurement sheet frozen as recorded.", before, after, correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.RecordMeasurementSheet, before, after, correlationId);
        await SaveAsync(token); return await GetAsync(id, token);
    }

    public async Task<QuantitySurveyMeasurementAttachmentDto> AddAttachmentAsync(
        Guid id, Stream stream, string fileName, string contentType,
        AddQuantitySurveyMeasurementAttachmentRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (!Enum.IsDefined(request.EvidenceType)) throw Validation("Select a valid evidence type.");
        var title = RequiredText(request.Title, 3, 200, "Evidence title");
        var safeName = Path.GetFileName(fileName?.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 260) throw Validation("Select a file with a valid name of at most 260 characters.");
        await using var memory = new MemoryStream(); await stream.CopyToAsync(memory, token);
        if (memory.Length is < 1 or > MaximumAttachmentBytes) throw Validation("Evidence files must be between 1 byte and 10 MB.");
        var bytes = memory.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var canonicalType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim().ToLowerInvariant();
        var requestHash = Hash(new { MeasurementId = id, request.EvidenceType, Title = title, FileName = safeName, ContentType = canonicalType, Checksum = checksum });
        var existing = await db.QuantitySurveyMeasurementAttachments.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
        if (existing is not null)
        {
            if (existing.MeasurementSheetId != id || !FixedEquals(existing.RequestHash, requestHash)) throw RetryConflict();
            return Map(existing);
        }
        var sheet = await RequiredAsync(id, true, token); await RequireProjectAccessAsync(sheet.ProjectId); EnsureDraft(sheet);
        await ValidateFrozenContextAsync(sheet, token);
        if (sheet.Attachments.Count >= MaximumAttachments) throw Validation($"A measurement sheet can contain at most {MaximumAttachments} evidence files.");
        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyMeasurementEvidence,
            FileName = safeName, ContentType = canonicalType, FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, writable: false)
        }, token);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token);
            throw new QuantitySurveyMeasurementConflictException("The centrally scanned evidence did not pass its integrity check.");
        }
        var attachmentId = Guid.NewGuid();
        CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName, FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey taking-off and measurement evidence",
                SourceEntityType = nameof(QuantitySurveyMeasurementAttachment), SourceRecordId = attachmentId,
                SourceRecordReference = sheet.SheetReference, Title = $"{sheet.SheetReference} · {title}",
                DocumentType = sheet.EvidenceMetadataTemplate.DocumentType,
                MetadataTemplateCode = sheet.EvidenceMetadataTemplateCodeSnapshot,
                AccessProfile = sheet.EvidenceMetadataTemplate.AccessProfile, VersionStatus = "Validated",
                ChangeSummary = "Clean scanned measurement evidence retained against the governed taking-off sheet.",
                RequirePublishedGovernance = true,
                MetadataValues =
                [
                    new("measurementSheetId", "Measurement sheet ID", sheet.Id.ToString(), "guid"),
                    new("projectId", "Project ID", sheet.ProjectId.ToString(), "guid"),
                    new("boqLineKey", "BoQ line key", sheet.BoqLineKey.ToString(), "guid"),
                    new("evidenceType", "Evidence type", request.EvidenceType.ToString()),
                    new("checksumSha256", "Checksum SHA-256", checksum)
                ]
            }, token);
        }
        catch { await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, token); throw; }
        try
        {
            var entity = new QuantitySurveyMeasurementAttachment
            {
                Id = attachmentId, TenantId = TenantId, MeasurementSheetId = sheet.Id,
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash, EvidenceType = request.EvidenceType,
                Title = title, OriginalFileName = upload.Record.OriginalFileName, ContentType = canonicalType,
                FileSize = bytes.LongLength, ChecksumSha256 = checksum, FileUploadRecordId = document.FileUploadRecordId,
                CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.DocumentVersionId,
                UploadedById = UserId, UploadedByName = UserName, CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName, CreatedById = UserId
            };
            db.QuantitySurveyMeasurementAttachments.Add(entity);
            var after = new { entity.Id, entity.EvidenceType, entity.Title, entity.OriginalFileName, entity.FileSize,
                entity.ChecksumSha256, entity.CentralDocumentRecordId, entity.CentralDocumentVersionId };
            AddRevision(sheet, QuantitySurveyAuditEventMap.AttachMeasurementEvidence, title, null, after, correlationId);
            AddAudit(sheet, QuantitySurveyAuditEventMap.AttachMeasurementEvidence, null, after, correlationId);
            await SaveAsync(token); return Map(entity);
        }
        catch { await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, token); throw; }
    }

    public async Task<CentralDocumentRepositoryContent> OpenAttachmentAsync(Guid id, Guid attachmentId, CancellationToken token = default)
    {
        var sheet = await RequiredAsync(id, false, token); await RequireProjectAccessAsync(sheet.ProjectId);
        var attachment = sheet.Attachments.SingleOrDefault(value => value.Id == attachmentId && !value.IsDeleted)
            ?? throw new QuantitySurveyMeasurementNotFoundException("The measurement evidence was not found.");
        return await centralDocuments.OpenAsync(TenantId, attachment.CentralDocumentRecordId, attachment.CentralDocumentVersionId, token)
            ?? throw new QuantitySurveyMeasurementConflictException("The central-DMS evidence content is unavailable.");
    }

    public async Task<IReadOnlyList<QuantitySurveyMeasurementRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var sheet = await RequiredAsync(id, false, token); await RequireProjectAccessAsync(sheet.ProjectId);
        return await db.QuantitySurveyMeasurementRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.MeasurementSheetId == id && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            .Select(value => new QuantitySurveyMeasurementRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorName = value.ActorName, ActorRoles = value.ActorRoles,
                CorrelationId = value.CorrelationId, Reason = value.Reason, BeforeJson = value.BeforeJson,
                AfterJson = value.AfterJson, CreatedAt = value.CreatedAt
            }).ToListAsync(token);
    }

    private IQueryable<QuantitySurveyMeasurementSheet> Query(bool tracking = false)
    {
        var query = tracking ? db.QuantitySurveyMeasurementSheets.AsTracking() : db.QuantitySurveyMeasurementSheets.AsNoTracking();
        return query.Include(value => value.Project).Include(value => value.ProjectBoqVersion)
            .Include(value => value.ProjectBoqVersionLine).Include(value => value.ProjectDrawing)
            .Include(value => value.EvidenceMetadataTemplate).Include(value => value.Lines)
            .Include(value => value.Attachments).ThenInclude(value => value.FileUploadRecord)
            .Include(value => value.Attachments).ThenInclude(value => value.CentralDocumentRecord)
            .Include(value => value.Attachments).ThenInclude(value => value.CentralDocumentVersion)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
    }

    private async Task<QuantitySurveyMeasurementSheet> RequiredAsync(Guid id, bool tracking, CancellationToken token)
        => await Query(tracking).FirstOrDefaultAsync(value => value.Id == id, token)
           ?? throw new QuantitySurveyMeasurementNotFoundException("The measurement sheet was not found.");

    private async Task<MeasurementContext> ResolveContextAsync(Guid projectId, Guid lineId, Guid? drawingId,
        QuantitySurveyMeasurementSourceType sourceType, DateTime measurementDate, string? siteLocation, CancellationToken token)
    {
        if (!Enum.IsDefined(sourceType)) throw Validation("Select Design or Site as the measurement source.");
        var line = await db.ProjectBoqVersionLines.AsNoTracking().Include(value => value.Version)
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ProjectId == projectId && value.Id == lineId && !value.IsDeleted, token)
            ?? throw Validation("Select an approved BoQ item in this project.");
        if (line.ItemType != ProjectBoqItemTypes.Item || line.Version.TenantId != TenantId || line.Version.ProjectId != projectId ||
            line.Version.IsDeleted || line.Version.Status != ProjectBoqVersionStatuses.Approved || line.Version.PublishedAt is null)
            throw Validation("Measurements can be recorded only against an approved published BoQ item.");
        ProjectDrawing? drawing = null;
        if (drawingId.HasValue)
        {
            drawing = await db.Set<ProjectDrawing>().AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.Id == drawingId.Value && !value.IsDeleted, token)
                ?? throw Validation("Select an approved drawing in this project.");
            if (drawing.Status is not (ProjectDrawingStatuses.ApprovedForConstruction or ProjectDrawingStatuses.ApprovedAsBuilt) ||
                string.IsNullOrWhiteSpace(drawing.Revision))
                throw Validation("The selected drawing requires an approved status and revision.");
        }
        if (sourceType == QuantitySurveyMeasurementSourceType.Design && drawing is null)
            throw Validation("Design measurements require an approved drawing revision.");
        if (sourceType == QuantitySurveyMeasurementSourceType.Site && string.IsNullOrWhiteSpace(siteLocation))
            throw Validation("Site measurements require a site location.");
        var policies = await db.QuantitySurveyConfigurationProfiles.AsNoTracking()
            .Where(value => value.TenantId == TenantId && !value.IsDeleted &&
                            value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published && value.PublishedAt != null &&
                            value.EffectiveFrom <= measurementDate && (!value.EffectiveTo.HasValue || value.EffectiveTo.Value >= measurementDate))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version).ToListAsync(token);
        if (policies.Count == 0) throw Validation("No Published quantity-survey configuration is effective for the measurement date.");
        if (policies.Count > 1 && policies[0].IsDefault == policies[1].IsDefault)
            throw new QuantitySurveyMeasurementConflictException("More than one quantity-survey configuration is effective for the measurement date.");
        var profile = policies[0];
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.DecisionKey == "QS-DEC-007" && !value.IsDeleted, token)
            ?? throw Validation("The effective configuration has no QS-DEC-007 measurement decision.");
        if (decision.Status != QuantitySurveyConfigurationDecisionStatus.Approved ||
            decision.ApprovalStatus != QuantitySurveyConfigurationApprovalStatus.Approved ||
            decision.EvidenceStatus != QuantitySurveyConfigurationEvidenceStatus.Verified ||
            (decision.EffectiveFrom.HasValue && decision.EffectiveFrom.Value > measurementDate) ||
            (decision.EffectiveTo.HasValue && decision.EffectiveTo.Value < measurementDate))
            throw Validation("QS-DEC-007 is not approved, verified, and effective for the measurement date.");
        QsMeasurementValue policy;
        try { policy = JsonSerializer.Deserialize<QsMeasurementValue>(decision.ValueJson, JsonOptions) ?? new(); }
        catch (JsonException) { throw new QuantitySurveyMeasurementConflictException("QS-DEC-007 contains invalid measurement policy data."); }
        if (policy.WorkflowDefinitionId == Guid.Empty || policy.MetadataTemplateId == Guid.Empty ||
            policy.JointAttendanceRoleIds.Count == 0 || policy.ConsultantRoleIds.Count == 0)
            throw new QuantitySurveyMeasurementConflictException("QS-DEC-007 is incomplete and cannot govern a measurement record.");
        var template = await db.Set<CentralDocumentMetadataTemplateEntity>().AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.Id == policy.MetadataTemplateId && !value.IsDeleted, token)
            ?? throw Validation("The QS-DEC-007 evidence metadata template is unavailable.");
        if (!template.IsActive || template.PublishedAt is null)
            throw Validation("The QS-DEC-007 evidence metadata template must be active and published.");
        var policyHash = Hash(new { Profile = profile.Id, profile.Version, Decision = decision.Id, decision.ValueJson,
            Template = template.Id, template.TemplateCode, policy.WorkflowDefinitionId, policy.JointAttendanceRoleIds,
            policy.ConsultantRoleIds, policy.RequireContractorSignature, policy.RequireConsultantSignature });
        return new(line, drawing, profile, decision, template, policyHash);
    }

    private async Task ValidateFrozenContextAsync(QuantitySurveyMeasurementSheet entity, CancellationToken token)
    {
        var context = await ResolveContextAsync(entity.ProjectId, entity.ProjectBoqVersionLineId, entity.ProjectDrawingId,
            entity.SourceType, entity.MeasurementDate, entity.SiteLocation, token);
        ValidateFrozenLineage(entity, context);
        if (!FixedEquals(entity.PolicyHash, context.PolicyHash))
            throw new QuantitySurveyMeasurementConflictException("The governed measurement policy changed. Create a new sheet under the current policy.");
    }

    private static void ValidateFrozenLineage(QuantitySurveyMeasurementSheet entity, MeasurementContext context)
    {
        if (entity.ProjectBoqVersionId != context.Line.ProjectBoqVersionId || entity.BoqLineKey != context.Line.LineKey ||
            entity.ConfigurationProfileId != context.Profile.Id || entity.ConfigurationDecisionId != context.Decision.Id ||
            entity.EvidenceMetadataTemplateId != context.Template.Id || entity.BoqDescriptionSnapshot != context.Line.Description ||
            entity.BoqQuantitySnapshot != context.Line.Quantity || entity.MeasurementCodeSnapshot != context.Line.MeasurementCode ||
            entity.MeasurementRuleSnapshot != context.Line.MeasurementRule)
            throw new QuantitySurveyMeasurementConflictException("The approved BoQ or configuration lineage changed. Create a new measurement sheet.");
    }

    private static void ValidateEvidence(QuantitySurveyMeasurementSheet entity)
    {
        if (entity.Attachments.Any(value => value.IsDeleted || value.TenantId != entity.TenantId ||
            value.FileUploadRecord.TenantId != entity.TenantId || value.FileUploadRecord.IsDeleted ||
            value.FileUploadRecord.Category != ControlledFileUploadCategories.QuantitySurveyMeasurementEvidence ||
            value.FileUploadRecord.VirusScanStatus != FileVirusScanStatus.Clean ||
            value.CentralDocumentRecord.TenantId != entity.TenantId || value.CentralDocumentRecord.IsDeleted ||
            value.CentralDocumentRecord.SourceRecordId != value.Id ||
            value.CentralDocumentRecord.MetadataTemplateCode != entity.EvidenceMetadataTemplateCodeSnapshot ||
            value.CentralDocumentVersion.TenantId != entity.TenantId || value.CentralDocumentVersion.IsDeleted ||
            value.CentralDocumentVersion.DocumentRecordId != value.CentralDocumentRecordId ||
            value.CentralDocumentVersion.FileUploadRecordId != value.FileUploadRecordId || value.CentralDocumentVersion.Status != "Validated"))
            throw new QuantitySurveyMeasurementConflictException("Measurement evidence no longer satisfies the clean-scan and central-DMS lineage controls.");
    }

    private void AddLines(QuantitySurveyMeasurementSheet entity, IReadOnlyCollection<QuantitySurveyMeasurementLineInputDto> lines)
    {
        QuantitySurveyMeasurementRules.Total(lines);
        foreach (var input in lines.OrderBy(value => value.Sequence))
        {
            var calculated = QuantitySurveyMeasurementRules.Calculate(input);
            entity.Lines.Add(new QuantitySurveyMeasurementLine
            {
                Id = Guid.NewGuid(), TenantId = TenantId, MeasurementSheetId = entity.Id,
                ClientLineKey = input.ClientLineKey, Sequence = input.Sequence, Description = input.Description,
                FormulaType = input.FormulaType, Timesing = input.Timesing, Length = input.Length,
                Width = input.Width, Height = input.Height, IsDeduction = input.IsDeduction,
                FormulaSnapshot = calculated.Formula, CalculatedQuantity = calculated.Quantity, Notes = input.Notes,
                CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
            });
        }
    }

    private static NormalizedRequest Normalize(string? title, string? siteLocation, IEnumerable<QuantitySurveyMeasurementLineInputDto> lines)
    {
        var normalizedLines = lines.Select(value => new QuantitySurveyMeasurementLineInputDto
        {
            ClientLineKey = value.ClientLineKey, Sequence = value.Sequence,
            Description = RequiredText(value.Description, 2, 500, "Measurement row description"),
            FormulaType = value.FormulaType, Timesing = Round(value.Timesing), Length = Round(value.Length),
            Width = Round(value.Width), Height = Round(value.Height), IsDeduction = value.IsDeduction,
            Notes = Clean(value.Notes, 1000)
        }).OrderBy(value => value.Sequence).ToList();
        QuantitySurveyMeasurementRules.Total(normalizedLines);
        return new(RequiredText(title, 3, 200, "Sheet title"), Clean(siteLocation, 300), normalizedLines);
    }

    private static void ValidateCreate(CreateQuantitySurveyMeasurementRequest request)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (request.ProjectId == Guid.Empty) throw Validation("Select a project.");
        if (request.ProjectBoqVersionLineId == Guid.Empty) throw Validation("Select an approved BoQ item.");
        if (request.MeasurementDate == default) throw Validation("Select the measurement date.");
    }

    private static void ValidateMutation(Guid clientRequestId, string rowVersion)
    {
        if (clientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(rowVersion)) throw Validation("The row version is required. Refresh and retry.");
    }

    private static void EnsureDraft(QuantitySurveyMeasurementSheet entity)
    {
        if (entity.Status != "Draft") throw new QuantitySurveyMeasurementConflictException("A recorded measurement sheet is immutable.");
    }

    private async Task<List<Guid>> AccessibleProjectIdsAsync()
        => (await projectService.LookupProjectsAsync(take: 5000)).Select(value => value.Id).Distinct().ToList();

    private async Task RequireProjectAccessAsync(Guid projectId)
    {
        if (await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private void ApplyRowVersion(QuantitySurveyMeasurementSheet entity, string value)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(value); }
        catch (FormatException) { throw Validation("The row version is invalid. Refresh and retry."); }
        if (expected.Length == 0) throw Validation("The row version is required. Refresh and retry.");
        db.Entry(entity).Property(item => item.RowVersion).OriginalValue = expected;
    }

    private void Touch(QuantitySurveyMeasurementSheet entity, string action, string correlationId)
    {
        entity.AuditAction = action; entity.CorrelationId = Correlation(correlationId); entity.ActorRoles = ActorRoles;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
    }

    private void AddRevision(QuantitySurveyMeasurementSheet entity, string action, string reason, object? before, object after, string correlationId)
        => db.QuantitySurveyMeasurementRevisions.Add(new QuantitySurveyMeasurementRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, MeasurementSheetId = entity.Id, Action = action,
            ActorUserId = UserId, ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId),
            Reason = Truncate(reason, 1000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(QuantitySurveyMeasurementSheet entity, string action, object? before, object after, string correlationId)
        => db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(QuantitySurveyMeasurementSheet), ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QS-0401", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw new QuantitySurveyMeasurementConflictException("The measurement sheet changed after it was loaded. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql)
        {
            throw new QuantitySurveyMeasurementConflictException(sql.Number switch
            {
                51080 => "The measurement violates its tenant, approved BoQ, drawing, policy, or DMS lineage controls.",
                51081 => "The measurement lifecycle or server-calculated formula control rejected this operation.",
                51082 => "Recorded measurements, evidence, and revision history are immutable or append-only.",
                2601 or 2627 => "An equivalent measurement operation already exists. Refresh and retry.",
                _ => "The measurement could not be saved because a database control rejected it."
            });
        }
    }

    private static object Snapshot(QuantitySurveyMeasurementSheet value, IEnumerable<QuantitySurveyMeasurementLineInputDto> lines) => new
    {
        value.Id, value.ProjectId, value.ProjectBoqVersionId, value.ProjectBoqVersionLineId, value.BoqLineKey,
        value.ProjectDrawingId, value.SourceType, value.SheetReference, value.Title, value.MeasurementDate,
        value.SiteLocation, value.BoqDescriptionSnapshot, value.UnitOfMeasureSnapshot, value.BoqQuantitySnapshot,
        value.MeasurementStandardSnapshot, value.MeasurementCodeSnapshot, value.MeasurementRuleSnapshot,
        value.TotalMeasuredQuantity, value.Status, value.ConfigurationProfileId, value.ConfigurationDecisionId,
        value.EvidenceMetadataTemplateId, value.PolicyHash,
        Lines = lines.Select(line => new { line.ClientLineKey, line.Sequence, line.Description, line.FormulaType,
            line.Timesing, line.Length, line.Width, line.Height, line.IsDeduction,
            Formula = QuantitySurveyMeasurementRules.Calculate(line).Formula,
            Quantity = QuantitySurveyMeasurementRules.Calculate(line).Quantity, line.Notes }).ToList()
    };

    private static object Snapshot(QuantitySurveyMeasurementSheet value, IEnumerable<QuantitySurveyMeasurementLine> lines) => new
    {
        value.Id, value.ProjectId, value.ProjectBoqVersionId, value.ProjectBoqVersionLineId, value.BoqLineKey,
        value.ProjectDrawingId, value.SourceType, value.SheetReference, value.Title, value.MeasurementDate,
        value.SiteLocation, value.BoqDescriptionSnapshot, value.UnitOfMeasureSnapshot, value.BoqQuantitySnapshot,
        value.MeasurementStandardSnapshot, value.MeasurementCodeSnapshot, value.MeasurementRuleSnapshot,
        value.TotalMeasuredQuantity, value.Status, value.ConfigurationProfileId, value.ConfigurationDecisionId,
        value.EvidenceMetadataTemplateId, value.PolicyHash,
        Lines = lines.OrderBy(line => line.Sequence).Select(line => new { line.ClientLineKey, line.Sequence,
            line.Description, line.FormulaType, line.Timesing, line.Length, line.Width, line.Height,
            line.IsDeduction, Formula = line.FormulaSnapshot, Quantity = line.CalculatedQuantity, line.Notes }).ToList()
    };

    private static QuantitySurveyMeasurementDto Map(QuantitySurveyMeasurementSheet value) => new()
    {
        Id = value.Id, ProjectId = value.ProjectId, ProjectCode = value.Project.ProjectCode, ProjectName = value.Project.Title,
        ProjectBoqVersionId = value.ProjectBoqVersionId, ProjectBoqVersionLineId = value.ProjectBoqVersionLineId,
        BoqLineKey = value.BoqLineKey,
        BoqLineLabel = (value.BoqLineNumberSnapshot ?? value.BoqItemCodeSnapshot ?? "Item") + " · " + value.BoqDescriptionSnapshot,
        BoqQuantity = value.BoqQuantitySnapshot, UnitOfMeasure = value.UnitOfMeasureSnapshot,
        ProjectDrawingId = value.ProjectDrawingId,
        DrawingLabel = value.ProjectDrawingId.HasValue ? value.DrawingNumberSnapshot + " · Rev " + value.DrawingRevisionSnapshot : null,
        SourceType = value.SourceType, SheetReference = value.SheetReference, Title = value.Title,
        MeasurementDate = value.MeasurementDate, SiteLocation = value.SiteLocation,
        TotalMeasuredQuantity = value.TotalMeasuredQuantity, Status = value.Status,
        EvidenceMetadataTemplateCode = value.EvidenceMetadataTemplateCodeSnapshot,
        PreparedById = value.PreparedById, PreparedByName = value.PreparedByName, PreparedAt = value.PreparedAt,
        RecordedByName = value.RecordedByName, RecordedAt = value.RecordedAt,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Lines = value.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Sequence).Select(Map).ToList(),
        Attachments = value.Attachments.Where(item => !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(Map).ToList()
    };

    private static QuantitySurveyMeasurementLineDto Map(QuantitySurveyMeasurementLine value) => new()
    {
        Id = value.Id, ClientLineKey = value.ClientLineKey, Sequence = value.Sequence, Description = value.Description,
        FormulaType = value.FormulaType, Timesing = value.Timesing, Length = value.Length, Width = value.Width,
        Height = value.Height, IsDeduction = value.IsDeduction, Formula = value.FormulaSnapshot,
        CalculatedQuantity = value.CalculatedQuantity, Notes = value.Notes
    };

    private static QuantitySurveyMeasurementAttachmentDto Map(QuantitySurveyMeasurementAttachment value) => new()
    {
        Id = value.Id, EvidenceType = value.EvidenceType, Title = value.Title, OriginalFileName = value.OriginalFileName,
        ContentType = value.ContentType, FileSize = value.FileSize, ChecksumSha256 = value.ChecksumSha256,
        CentralDocumentRecordId = value.CentralDocumentRecordId, CentralDocumentVersionId = value.CentralDocumentVersionId,
        UploadedByName = value.UploadedByName, CreatedAt = value.CreatedAt
    };

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    private static decimal? Round(decimal? value) => value.HasValue ? Round(value.Value) : null;
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static string RequiredText(string? value, int minimum, int maximum, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < minimum || normalized.Length > maximum)
            throw Validation($"{label} must contain between {minimum} and {maximum} characters.");
        return normalized;
    }
    private static string? Clean(string? value, int maximum)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maximum) throw Validation($"Text cannot exceed {maximum} characters.");
        return normalized;
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right) => left is not null && right is not null && left.Length == right.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static string Correlation(string? value) => Truncate(string.IsNullOrWhiteSpace(value) ? "qs-0401" : value.Trim(), 100);
    private static string Truncate(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
    private static QuantitySurveyMeasurementValidationException Validation(string message) => new(message);
    private static QuantitySurveyMeasurementConflictException RetryConflict() => new("The client request identifier was already used for different measurement data. Refresh and retry with a new request.");
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter()); return options;
    }

    private sealed record NormalizedRequest(string Title, string? SiteLocation, IReadOnlyList<QuantitySurveyMeasurementLineInputDto> Lines);
    private sealed record MeasurementContext(ProjectBoqVersionLine Line, ProjectDrawing? Drawing,
        QuantitySurveyConfigurationProfile Profile, QuantitySurveyConfigurationDecision Decision,
        CentralDocumentMetadataTemplateEntity Template, string PolicyHash);
}
