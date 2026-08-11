using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
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

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyEscalationDisputeService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IControlledFileUploadService controlledFiles,
    ICentralDocumentRepositoryFileService centralDocuments) : IQuantitySurveyEscalationDisputeService
{
    private const long MaximumAttachmentBytes = 10 * 1024 * 1024;
    private const int MaximumAttachments = 10;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value
        : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<IReadOnlyList<QuantitySurveyEscalationDisputeLookupDto>> GetCalculationLookupsAsync(
        CancellationToken cancellationToken = default)
    {
        var accessible = await AccessibleProjectIdsAsync();
        return await db.QuantitySurveyEscalationCalculationRuns.AsNoTracking()
            .Include(value => value.Project)
            .Include(value => value.Contract)
            .Where(value => value.TenantId == TenantId && accessible.Contains(value.ProjectId) && !value.IsDeleted &&
                            value.Status == "ApprovedPendingApplication" && value.ApprovalStatus == "Approved" &&
                            !db.QuantitySurveyEscalationDisputes.Any(dispute => dispute.TenantId == TenantId &&
                                dispute.CalculationRunId == value.Id && !dispute.IsDeleted))
            .OrderByDescending(value => value.ApprovedAt).ThenBy(value => value.RunReference)
            .Select(value => new QuantitySurveyEscalationDisputeLookupDto
            {
                Id = value.Id,
                Label = value.RunReference + " · " + value.CurrencyCode + " " + value.ApprovedImpactAmount,
                Group = value.Project.ProjectCode + " · " + value.Contract.ContractNumber,
                Status = value.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<QuantitySurveyEscalationDisputePageDto> ListAsync(
        QuantitySurveyEscalationDisputeListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status is not ("Open" or "ContractorResponded" or "Resolved"))
            throw new QuantitySurveyEscalationDisputeValidationException("Select a valid dispute status.");
        var accessible = await AccessibleProjectIdsAsync();
        var query = Query().Where(value => accessible.Contains(value.ProjectId));
        if (request.ProjectId.HasValue) query = query.Where(value => value.ProjectId == request.ProjectId.Value);
        if (request.CalculationRunId.HasValue) query = query.Where(value => value.CalculationRunId == request.CalculationRunId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(value => value.Status == request.Status);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(value => value.OpenedAt).ThenBy(value => value.DisputeReference)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new QuantitySurveyEscalationDisputePageDto
        {
            Items = items.Select(Map).ToList(), Page = request.Page, PageSize = request.PageSize, TotalCount = count
        };
    }

    public async Task<QuantitySurveyEscalationDisputeDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await RequiredAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        return Map(entity);
    }

    public async Task<QuantitySurveyEscalationDisputeDto> OpenAsync(
        CreateQuantitySurveyEscalationDisputeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw new QuantitySurveyEscalationDisputeValidationException("A client request identifier is required.");
        if (request.CalculationRunId == Guid.Empty) throw new QuantitySurveyEscalationDisputeValidationException("Select an approved escalation calculation.");
        var subject = RequiredText(request.Subject, 5, 200, "Dispute subject");
        var reason = RequiredText(request.DisputeReason, 10, 4000, "Dispute reason");
        var requestHash = Hash(new { request.CalculationRunId, Subject = subject, Reason = reason });
        var existing = await db.QuantitySurveyEscalationDisputes.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, cancellationToken);
        if (existing is not null)
        {
            if (!FixedEquals(existing.RequestHash, requestHash)) throw RetryConflict();
            return await GetAsync(existing.Id, cancellationToken);
        }

        var resultId = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var duplicate = await db.QuantitySurveyEscalationDisputes.AsNoTracking()
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, cancellationToken);
            if (duplicate is not null)
            {
                if (!FixedEquals(duplicate.RequestHash, requestHash)) throw RetryConflict();
                resultId = duplicate.Id;
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            var run = await db.QuantitySurveyEscalationCalculationRuns.AsNoTracking()
                .Include(value => value.Contract).ThenInclude(value => value.BusinessPartner)
                .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.Id == request.CalculationRunId && !value.IsDeleted,
                    cancellationToken)
                ?? throw new QuantitySurveyEscalationDisputeValidationException("The selected escalation calculation is unavailable in this tenant.");
            await RequireProjectAccessAsync(run.ProjectId);
            if (run.Status != "ApprovedPendingApplication" || run.ApprovalStatus != "Approved" || run.ApprovedAt is null)
                throw new QuantitySurveyEscalationDisputeValidationException("Only an approved escalation calculation can enter dispute review.");
            if (await db.QuantitySurveyEscalationDisputes.AnyAsync(value => value.TenantId == TenantId &&
                    value.CalculationRunId == run.Id && !value.IsDeleted, cancellationToken))
                throw new QuantitySurveyEscalationDisputeConflictException("A dispute already exists for this escalation calculation.");
            if (run.Contract.TenantId != TenantId || run.Contract.BusinessPartner.TenantId != TenantId ||
                run.Contract.IsDeleted || run.Contract.BusinessPartner.IsDeleted ||
                run.Contract.BusinessPartnerId != run.Contract.BusinessPartner.Id)
                throw new QuantitySurveyEscalationDisputeConflictException("The Works contract contractor lineage is no longer valid.");

            var now = DateTime.UtcNow;
            var entity = new QuantitySurveyEscalationDispute
            {
                Id = Guid.NewGuid(), TenantId = TenantId, CalculationRunId = run.Id, ProjectId = run.ProjectId,
                ContractId = run.ContractId, ContractorBusinessPartnerId = run.Contract.BusinessPartnerId,
                ContractorNameSnapshot = run.Contract.BusinessPartner.PartnerName.Trim(),
                DisputeReference = $"QSD-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
                ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                CalculationSnapshotHash = run.SnapshotHash, Subject = subject, DisputeReason = reason,
                Status = "Open", OpenedById = UserId, OpenedAt = now,
                AuditAction = QuantitySurveyAuditEventMap.OpenEscalationDispute,
                CorrelationId = NormalizeCorrelation(correlationId), ActorRoles = ActorRoles,
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.QuantitySurveyEscalationDisputes.Add(entity);
            var after = Snapshot(entity);
            AddRevision(entity, QuantitySurveyAuditEventMap.OpenEscalationDispute, reason, null, after, correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.OpenEscalationDispute, null, after, correlationId);
            await SaveAsync(cancellationToken);
            resultId = entity.Id;
            await transaction.CommitAsync(cancellationToken);
        });
        return await GetAsync(resultId, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationDisputeDto> RespondAsync(
        Guid id,
        RespondQuantitySurveyEscalationDisputeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw new QuantitySurveyEscalationDisputeValidationException("A client request identifier is required.");
        var response = RequiredText(request.Response, 10, 4000, "Contractor response");
        var requestHash = Hash(new { DisputeId = id, Response = response });
        var entity = await RequiredAsync(id, true, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.ContractorResponseClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(entity.ContractorResponseHash, requestHash)) throw RetryConflict();
            return Map(entity);
        }
        if (entity.Status != "Open") throw new QuantitySurveyEscalationDisputeConflictException("A contractor response can be recorded only while the dispute is open.");
        ValidateCalculationSnapshot(entity);
        ApplyRowVersion(entity, request.RowVersion);
        var before = Snapshot(entity);
        entity.ContractorResponseClientRequestId = request.ClientRequestId;
        entity.ContractorResponseHash = requestHash;
        entity.ContractorResponse = response;
        entity.ContractorRespondedById = UserId;
        entity.ContractorRespondedAt = DateTime.UtcNow;
        entity.Status = "ContractorResponded";
        Touch(entity, QuantitySurveyAuditEventMap.RecordEscalationContractorResponse, correlationId);
        var after = Snapshot(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.RecordEscalationContractorResponse, response, before, after, correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.RecordEscalationContractorResponse, before, after, correlationId);
        await SaveAsync(cancellationToken);
        return await GetAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationDisputeDto> ResolveAsync(
        Guid id,
        ResolveQuantitySurveyEscalationDisputeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw new QuantitySurveyEscalationDisputeValidationException("A client request identifier is required.");
        if (!Enum.IsDefined(request.Outcome)) throw new QuantitySurveyEscalationDisputeValidationException("Select a valid controlled dispute outcome.");
        var notes = RequiredText(request.ResolutionNotes, 10, 4000, "Resolution notes");
        var requestHash = Hash(new { DisputeId = id, request.Outcome, Notes = notes });
        var entity = await RequiredAsync(id, true, cancellationToken);
        await RequireProjectAccessAsync(entity.ProjectId);
        if (entity.ResolutionClientRequestId == request.ClientRequestId)
        {
            if (!FixedEquals(entity.ResolutionRequestHash, requestHash)) throw RetryConflict();
            return Map(entity);
        }
        if (entity.Status != "ContractorResponded")
            throw new QuantitySurveyEscalationDisputeConflictException("Record the contractor response before resolving this dispute.");
        if (UserId == entity.OpenedById || UserId == entity.ContractorRespondedById)
            throw new UnauthorizedAccessException("The dispute resolver must be independent of both the opener and contractor-response recorder.");
        ValidateCalculationSnapshot(entity);
        ApplyRowVersion(entity, request.RowVersion);
        var before = Snapshot(entity);
        entity.ResolutionClientRequestId = request.ClientRequestId;
        entity.ResolutionRequestHash = requestHash;
        entity.Outcome = request.Outcome;
        entity.ResolutionNotes = notes;
        entity.ResolvedById = UserId;
        entity.ResolvedAt = DateTime.UtcNow;
        entity.Status = "Resolved";
        Touch(entity, QuantitySurveyAuditEventMap.ResolveEscalationDispute, correlationId);
        var after = Snapshot(entity);
        AddRevision(entity, QuantitySurveyAuditEventMap.ResolveEscalationDispute, notes, before, after, correlationId);
        AddAudit(entity, QuantitySurveyAuditEventMap.ResolveEscalationDispute, before, after, correlationId);
        await SaveAsync(cancellationToken);
        return await GetAsync(entity.Id, cancellationToken);
    }

    public async Task<QuantitySurveyEscalationDisputeAttachmentDto> AddAttachmentAsync(
        Guid id,
        Stream stream,
        string fileName,
        string contentType,
        AddQuantitySurveyEscalationDisputeAttachmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty) throw new QuantitySurveyEscalationDisputeValidationException("A client request identifier is required.");
        if (!Enum.IsDefined(request.AttachmentType)) throw new QuantitySurveyEscalationDisputeValidationException("Select a valid attachment type.");
        var title = RequiredText(request.Title, 3, 200, "Attachment title");
        var safeName = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 260)
            throw new QuantitySurveyEscalationDisputeValidationException("Select a file with a valid name of at most 260 characters.");
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0 || memory.Length > MaximumAttachmentBytes)
            throw new QuantitySurveyEscalationDisputeValidationException("Evidence files must be between 1 byte and 10 MB.");
        var bytes = memory.ToArray();
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var canonicalContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim().ToLowerInvariant();
        var requestHash = Hash(new { DisputeId = id, request.AttachmentType, Title = title, FileName = safeName, ContentType = canonicalContentType, Checksum = checksum });
        var existing = await db.QuantitySurveyEscalationDisputeAttachments.AsNoTracking()
            .FirstOrDefaultAsync(value => value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, cancellationToken);
        if (existing is not null)
        {
            if (existing.DisputeId != id || !FixedEquals(existing.RequestHash, requestHash)) throw RetryConflict();
            return Map(existing);
        }
        var dispute = await RequiredAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(dispute.ProjectId);
        if (dispute.Status == "Resolved") throw new QuantitySurveyEscalationDisputeConflictException("Resolved disputes are immutable and cannot receive new evidence.");
        ValidateCalculationSnapshot(dispute);
        if (dispute.Attachments.Count >= MaximumAttachments)
            throw new QuantitySurveyEscalationDisputeValidationException($"A dispute can contain at most {MaximumAttachments} evidence files.");

        var upload = await controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
            Category = ControlledFileUploadCategories.QuantitySurveyEscalationDisputeEvidence,
            FileName = safeName, ContentType = canonicalContentType, FileSize = bytes.LongLength,
            OpenReadStream = () => new MemoryStream(bytes, writable: false)
        }, cancellationToken);
        if (upload.Record.VirusScanStatus != FileVirusScanStatus.Clean || !FixedEquals(upload.ChecksumSha256, checksum))
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            throw new QuantitySurveyEscalationDisputeConflictException("The centrally scanned evidence did not pass its integrity check.");
        }
        var attachmentId = Guid.NewGuid();
        CentralDocumentRepositoryLink document;
        try
        {
            document = await centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration
            {
                TenantId = TenantId, ActorUserId = UserId, ActorName = UserName,
                FileUploadRecordId = upload.Record.Id,
                SourceModule = "QuantitySurvey", SourceLabel = "Quantity Survey escalation dispute evidence",
                SourceEntityType = nameof(QuantitySurveyEscalationDisputeAttachment), SourceRecordId = attachmentId,
                SourceRecordReference = dispute.DisputeReference,
                Title = $"{dispute.DisputeReference} · {title}", DocumentType = request.AttachmentType.ToString(),
                AccessProfile = "Quantity Survey restricted", VersionStatus = "Validated",
                ChangeSummary = "Clean scanned escalation-dispute evidence retained for resolution and audit-pack export.",
                RequirePublishedGovernance = false,
                MetadataValues =
                [
                    new("disputeId", "Dispute ID", dispute.Id.ToString(), "guid"),
                    new("projectId", "Project ID", dispute.ProjectId.ToString(), "guid"),
                    new("contractId", "Contract ID", dispute.ContractId.ToString(), "guid"),
                    new("attachmentType", "Attachment type", request.AttachmentType.ToString()),
                    new("checksumSha256", "Checksum SHA-256", checksum)
                ]
            }, cancellationToken);
        }
        catch
        {
            await controlledFiles.DeleteAsync(TenantId, upload.Record.Id, UserId, cancellationToken);
            throw;
        }
        try
        {
            var entity = new QuantitySurveyEscalationDisputeAttachment
            {
                Id = attachmentId, TenantId = TenantId, DisputeId = dispute.Id, ClientRequestId = request.ClientRequestId,
                RequestHash = requestHash, AttachmentType = request.AttachmentType, Title = title,
                OriginalFileName = upload.Record.OriginalFileName, ContentType = canonicalContentType,
                FileSize = bytes.LongLength, ChecksumSha256 = checksum, FileUploadRecordId = document.FileUploadRecordId,
                CentralDocumentRecordId = document.DocumentRecordId, CentralDocumentVersionId = document.DocumentVersionId,
                UploadedById = UserId, UploadedByName = UserName, CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName, CreatedById = UserId
            };
            db.QuantitySurveyEscalationDisputeAttachments.Add(entity);
            var after = new { entity.Id, entity.AttachmentType, entity.Title, entity.OriginalFileName, entity.FileSize, entity.ChecksumSha256, entity.CentralDocumentRecordId, entity.CentralDocumentVersionId };
            AddRevision(dispute, QuantitySurveyAuditEventMap.AttachEscalationDisputeEvidence, title, null, after, correlationId);
            AddAudit(dispute, QuantitySurveyAuditEventMap.AttachEscalationDisputeEvidence, null, after, correlationId);
            await SaveAsync(cancellationToken);
            return Map(entity);
        }
        catch
        {
            await centralDocuments.DeleteAsync(TenantId, document.DocumentRecordId, UserId, cancellationToken);
            throw;
        }
    }

    public async Task<CentralDocumentRepositoryContent> OpenAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var dispute = await RequiredAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(dispute.ProjectId);
        var attachment = dispute.Attachments.SingleOrDefault(value => value.Id == attachmentId && !value.IsDeleted)
            ?? throw new QuantitySurveyEscalationDisputeNotFoundException("The dispute attachment was not found.");
        return await centralDocuments.OpenAsync(TenantId, attachment.CentralDocumentRecordId, attachment.CentralDocumentVersionId, cancellationToken)
            ?? throw new QuantitySurveyEscalationDisputeConflictException("The central-DMS evidence content is unavailable.");
    }

    public async Task<IReadOnlyList<QuantitySurveyEscalationDisputeRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dispute = await RequiredAsync(id, false, cancellationToken);
        await RequireProjectAccessAsync(dispute.ProjectId);
        return await db.QuantitySurveyEscalationDisputeRevisions.AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.DisputeId == id && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            .Select(value => new QuantitySurveyEscalationDisputeRevisionDto
            {
                Id = value.Id, Action = value.Action, ActorUserId = value.ActorUserId, ActorName = value.ActorName,
                ActorRoles = value.ActorRoles, CorrelationId = value.CorrelationId, Reason = value.Reason,
                BeforeJson = value.BeforeJson, AfterJson = value.AfterJson, CreatedAt = value.CreatedAt
            }).ToListAsync(cancellationToken);
    }

    private IQueryable<QuantitySurveyEscalationDispute> Query(bool tracking = false)
    {
        var query = tracking ? db.QuantitySurveyEscalationDisputes.AsTracking() : db.QuantitySurveyEscalationDisputes.AsNoTracking();
        return query.Include(value => value.CalculationRun).ThenInclude(value => value.Lines)
            .Include(value => value.Project).Include(value => value.Contract)
            .Include(value => value.Attachments)
            .Where(value => value.TenantId == TenantId && !value.IsDeleted);
    }

    private async Task<QuantitySurveyEscalationDispute> RequiredAsync(Guid id, bool tracking, CancellationToken cancellationToken)
        => await Query(tracking).FirstOrDefaultAsync(value => value.Id == id, cancellationToken)
           ?? throw new QuantitySurveyEscalationDisputeNotFoundException("The escalation dispute was not found.");

    private async Task<List<Guid>> AccessibleProjectIdsAsync()
        => (await projectService.LookupProjectsAsync(take: 5000)).Select(value => value.Id).Distinct().ToList();

    private async Task RequireProjectAccessAsync(Guid projectId)
    {
        if (await projectService.GetProjectByIdAsync(projectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to access the selected project.");
    }

    private void ValidateCalculationSnapshot(QuantitySurveyEscalationDispute dispute)
    {
        if (dispute.CalculationRun.Status != "ApprovedPendingApplication" ||
            dispute.CalculationRun.ApprovalStatus != "Approved" ||
            !FixedEquals(dispute.CalculationSnapshotHash, dispute.CalculationRun.SnapshotHash) ||
            dispute.CalculationRun.ProjectId != dispute.ProjectId || dispute.CalculationRun.ContractId != dispute.ContractId)
            throw new QuantitySurveyEscalationDisputeConflictException("The approved escalation calculation lineage changed. The dispute cannot continue.");
    }

    private void ApplyRowVersion(QuantitySurveyEscalationDispute entity, string value)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(value ?? string.Empty); }
        catch (FormatException) { throw new QuantitySurveyEscalationDisputeValidationException("The row version is invalid. Refresh and retry."); }
        if (expected.Length == 0) throw new QuantitySurveyEscalationDisputeValidationException("The row version is required. Refresh and retry.");
        db.Entry(entity).Property(item => item.RowVersion).OriginalValue = expected;
    }

    private void Touch(QuantitySurveyEscalationDispute entity, string action, string correlationId)
    {
        entity.AuditAction = action; entity.CorrelationId = NormalizeCorrelation(correlationId); entity.ActorRoles = ActorRoles;
        entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
    }

    private void AddRevision(QuantitySurveyEscalationDispute entity, string action, string reason, object? before, object after, string correlationId)
        => db.QuantitySurveyEscalationDisputeRevisions.Add(new QuantitySurveyEscalationDisputeRevision
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisputeId = entity.Id, Action = action, ActorUserId = UserId,
            ActorName = UserName, ActorRoles = ActorRoles, CorrelationId = NormalizeCorrelation(correlationId),
            Reason = Truncate(reason, 1000), BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName, CreatedById = UserId
        });

    private void AddAudit(QuantitySurveyEscalationDispute entity, string action, object? before, object after, string correlationId)
        => db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = nameof(QuantitySurveyEscalationDispute), ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QS-0304", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new QuantitySurveyEscalationDisputeConflictException("The dispute changed after it was loaded. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql)
        {
            throw new QuantitySurveyEscalationDisputeConflictException(sql.Number switch
            {
                51070 => "The dispute violates its tenant, approved calculation, Works contract, contractor, or DMS lineage controls.",
                51071 => "The dispute lifecycle transition or separation-of-duties control is not permitted.",
                51072 => "Escalation dispute evidence and revision history are append-only.",
                2601 or 2627 => "An equivalent dispute operation already exists. Refresh and retry.",
                _ => "The dispute could not be saved because a database control rejected it."
            });
        }
    }

    private static object Snapshot(QuantitySurveyEscalationDispute value) => new
    {
        value.Id, value.CalculationRunId, value.CalculationSnapshotHash, value.ProjectId, value.ContractId,
        value.ContractorBusinessPartnerId, value.ContractorNameSnapshot, value.DisputeReference, value.Subject,
        value.DisputeReason, value.Status, value.OpenedById, value.OpenedAt, value.ContractorResponse,
        value.ContractorRespondedById, value.ContractorRespondedAt, value.Outcome, value.ResolutionNotes,
        value.ResolvedById, value.ResolvedAt
    };

    private static QuantitySurveyEscalationDisputeDto Map(QuantitySurveyEscalationDispute value) => new()
    {
        Id = value.Id, CalculationRunId = value.CalculationRunId,
        CalculationRunReference = value.CalculationRun.RunReference, CalculationSnapshotHash = value.CalculationSnapshotHash,
        ProjectId = value.ProjectId, ProjectCode = value.Project.ProjectCode, ProjectName = value.Project.Title,
        ContractId = value.ContractId, ContractNumber = value.Contract.ContractNumber,
        ContractorBusinessPartnerId = value.ContractorBusinessPartnerId, ContractorName = value.ContractorNameSnapshot,
        DisputeReference = value.DisputeReference, Subject = value.Subject, DisputeReason = value.DisputeReason,
        Status = value.Status, OpenedById = value.OpenedById, OpenedAt = value.OpenedAt,
        ContractorResponse = value.ContractorResponse, ContractorRespondedById = value.ContractorRespondedById,
        ContractorRespondedAt = value.ContractorRespondedAt, Outcome = value.Outcome,
        ResolutionNotes = value.ResolutionNotes, ResolvedById = value.ResolvedById, ResolvedAt = value.ResolvedAt,
        RowVersion = Convert.ToBase64String(value.RowVersion),
        Attachments = value.Attachments.Where(item => !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(Map).ToList()
    };

    private static QuantitySurveyEscalationDisputeAttachmentDto Map(QuantitySurveyEscalationDisputeAttachment value) => new()
    {
        Id = value.Id, AttachmentType = value.AttachmentType, Title = value.Title,
        OriginalFileName = value.OriginalFileName, ContentType = value.ContentType, FileSize = value.FileSize,
        ChecksumSha256 = value.ChecksumSha256, CentralDocumentRecordId = value.CentralDocumentRecordId,
        CentralDocumentVersionId = value.CentralDocumentVersionId, UploadedById = value.UploadedById,
        UploadedByName = value.UploadedByName, CreatedAt = value.CreatedAt
    };

    private static string RequiredText(string? value, int minimum, int maximum, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < minimum || normalized.Length > maximum)
            throw new QuantitySurveyEscalationDisputeValidationException($"{label} must contain between {minimum} and {maximum} characters.");
        return normalized;
    }

    private static string Hash(object value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
        => left is not null && right is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static string NormalizeCorrelation(string? value) => Truncate(string.IsNullOrWhiteSpace(value) ? "qs-0304" : value.Trim(), 100);
    private static string Truncate(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
    private static QuantitySurveyEscalationDisputeConflictException RetryConflict()
        => new("The client request identifier was already used for different dispute data. Refresh and retry with a new request.");
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
