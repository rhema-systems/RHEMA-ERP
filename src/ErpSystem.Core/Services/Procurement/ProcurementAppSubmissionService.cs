using ErpSystem.Shared;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementAppSubmissionService : IProcurementAppSubmissionService
{
    private const string SourceType = "ProcurementAppSubmission";
    private const string EventType = "AppSubmissionLifecycle";
    private const string PlanPermission = "procurement.plan.manage";
    private const string ExportTemplateVersion = "TDC-APP-v1";
    private static readonly HashSet<string> SupportedExportFormats = new(["CSV", "JSON", "XML"], StringComparer.OrdinalIgnoreCase);
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ILogger<ProcurementAppSubmissionService> _logger;

    public ProcurementAppSubmissionService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementControlEventService controlEvents,
        IControlledFileUploadService controlledFiles,
        ILogger<ProcurementAppSubmissionService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _controlEvents = controlEvents;
        _controlledFiles = controlledFiles;
        _logger = logger;
    }

    private IGenericRepository<ProcurementAppSubmission> Submissions => _unitOfWork.Repository<ProcurementAppSubmission>();
    private IGenericRepository<ProcurementPlan> Plans => _unitOfWork.Repository<ProcurementPlan>();
    private IGenericRepository<ProcurementControlEvent> Events => _unitOfWork.Repository<ProcurementControlEvent>();

    public async Task<ProcurementAppSubmissionSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var plans = Plans.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
            item.Status == "Active" && item.PublishedDate.HasValue);
        var rows = Submissions.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        return new ProcurementAppSubmissionSummaryDto
        {
            PublishedPlanCount = await plans.CountAsync(cancellationToken),
            RegisteredPlanCount = await rows.Select(item => item.ProcurementPlanId).Distinct().CountAsync(cancellationToken),
            TotalAttemptCount = await rows.CountAsync(cancellationToken),
            ExportedCount = await rows.CountAsync(item => item.Status == ProcurementAppSubmissionStatus.Exported, cancellationToken),
            SubmittedCount = await rows.CountAsync(item => item.Status == ProcurementAppSubmissionStatus.Submitted, cancellationToken),
            AcknowledgedCount = await rows.CountAsync(item => item.Status == ProcurementAppSubmissionStatus.Acknowledged, cancellationToken),
            RejectedCount = await rows.CountAsync(item => item.Status == ProcurementAppSubmissionStatus.Rejected, cancellationToken),
            ResubmissionCount = await rows.CountAsync(item => item.AttemptNumber > 1, cancellationToken),
            LatestActivityAtUtc = await rows.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .Select(item => (DateTime?)(item.UpdatedAt ?? item.CreatedAt)).FirstOrDefaultAsync(cancellationToken)
        };
    }

    public async Task<IReadOnlyList<ProcurementAppSubmissionPlanOptionDto>> GetPublishedPlanOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await Plans.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.Status == "Active" && item.PublishedDate.HasValue && item.Items.Any(planItem => !planItem.IsDeleted))
            .AsNoTracking().OrderByDescending(item => item.FiscalYear).ThenBy(item => item.PlanNumber)
            .Select(item => new ProcurementAppSubmissionPlanOptionDto
            {
                Id = item.Id,
                PlanNumber = item.PlanNumber,
                Title = item.Title,
                FiscalYear = item.FiscalYear,
                RevisionNumber = item.RevisionNumber,
                PublishedDate = item.PublishedDate!.Value,
                ItemCount = item.Items.Count(planItem => !planItem.IsDeleted),
                HasSubmissionRegister = item.TenantId == _currentUser.TenantId &&
                    Submissions.GetQueryable().Any(submission => submission.TenantId == _currentUser.TenantId &&
                        submission.ProcurementPlanId == item.Id && !submission.IsDeleted)
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementAppSubmissionPageDto> SearchAsync(
        ProcurementAppSubmissionSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = QueryWithPlan();
        if (request.ProcurementPlanId.HasValue)
            query = query.Where(item => item.ProcurementPlanId == request.ProcurementPlanId.Value);
        if (request.FiscalYear.HasValue)
            query = query.Where(item => item.ProcurementPlan.FiscalYear == request.FiscalYear.Value);
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.SubmissionNumber.Contains(search) ||
                item.ProcurementPlan.PlanNumber.Contains(search) || item.ProcurementPlan.Title.Contains(search) ||
                (item.ExternalSubmissionReference != null && item.ExternalSubmissionReference.Contains(search)) ||
                (item.AcknowledgementReference != null && item.AcknowledgementReference.Contains(search)));
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.AsNoTracking().OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.AttemptNumber).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new ProcurementAppSubmissionPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(item => Map(item, [])).ToList()
        };
    }

    public async Task<ProcurementAppSubmissionDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var entity = await LoadAsync(id, false, cancellationToken);
        return Map(entity, await LoadTimelineAsync(entity.TimelineCorrelationId, cancellationToken));
    }

    public async Task<ProcurementAppExportFileDto> GetExportFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        _ = await LoadAsync(id, false, cancellationToken);
        var exportEvent = await Events.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EventType == EventType && item.SourceId == id &&
                (item.Action == "ExportRecorded" || item.Action == "ResubmissionRecorded"))
            .Include(item => item.EvidenceLinks)
                .ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking()
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var upload = exportEvent?.EvidenceLinks
            .FirstOrDefault(item => item.ReferenceKind == ProcurementControlEvidenceReferenceKind.FileUploadRecord &&
                item.RequirementKey == "APP_EXPORT_PACKAGE")
            ?.FileUploadRecord;
        if (upload is null || upload.IsDeleted || string.IsNullOrWhiteSpace(upload.FilePath))
            throw new ProcurementAppSubmissionNotFoundException("The generated APP export package was not found.");

        return new ProcurementAppExportFileDto
        {
            FileUploadRecordId = upload.Id,
            FileName = upload.OriginalFileName,
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType,
            FilePath = upload.FilePath
        };
    }

    public async Task<ProcurementAppSubmissionDto> RecordExportAsync(
        RecordProcurementAppExportRequest request,
        string causationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var exportFormat = ValidateExportFormat(request.ExportFormat);
        var plan = await Plans.GetQueryable(item => item.Id == request.ProcurementPlanId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Items.Where(planItem => !planItem.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAppSubmissionNotFoundException("The procurement plan was not found in the current tenant.");
        EnsurePublishedPlan(plan);
        await EnsureManagerAsync(plan.PlanNumber, causationId, cancellationToken);
        if (await Submissions.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.ProcurementPlanId == plan.Id && !item.IsDeleted).AnyAsync(cancellationToken))
            throw new ProcurementAppSubmissionConflictException("This published plan version already has an APP submission register. Continue through its current attempt or resubmit a rejected attempt.");

        var package = BuildExportPackage(plan, exportFormat);
        var uploaded = await UploadPackageAsync(package, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProcurementAppSubmission
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            ProcurementPlanId = plan.Id,
            SubmissionNumber = await NextSubmissionNumberAsync(now.Year, cancellationToken),
            AttemptNumber = 1,
            Status = ProcurementAppSubmissionStatus.Exported,
            TimelineCorrelationId = $"app-{Guid.NewGuid():N}",
            ExportFileName = package.FileName,
            ExportFormat = exportFormat,
            ExportTemplateVersion = ExportTemplateVersion,
            ExportChecksumSha256 = uploaded.ChecksumSha256.ToUpperInvariant(),
            ExportedAtUtc = now,
            ExportedById = _currentUser.UserId,
            ExportedByName = ActorName,
            Notes = TrimOrNull(request.Notes, 2000),
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray(),
            ProcurementPlan = plan
        };

        try
        {
            return await ExecuteMutationAsync(entity, true, "ExportRecorded", null, Snapshot(entity), request.Notes,
                ExportEvidence(request.Evidence, entity, uploaded.Record.Id), causationId, cancellationToken);
        }
        catch
        {
            await TryDeleteGeneratedPackageAsync(uploaded.Record.Id, cancellationToken);
            throw;
        }
    }

    public async Task<ProcurementAppSubmissionDto> SubmitAsync(
        Guid id,
        SubmitProcurementAppRequest request,
        string causationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReference(request.ExternalSubmissionReference, "SUBMISSION_REFERENCE_REQUIRED", "ExternalSubmissionReference is required.");
        var submittedAt = ValidateEventDate(request.SubmittedAtUtc, "SUBMISSION_DATE_REQUIRED");
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureManagerAsync(entity.SubmissionNumber, causationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementAppSubmissionStatus.Exported, "Only an Exported APP attempt can be submitted.");
        if (submittedAt < entity.ExportedAtUtc)
            throw new ProcurementAppSubmissionValidationException("SUBMISSION_DATE_INVALID", "SubmittedAtUtc cannot be before the export was recorded.");
        var before = Snapshot(entity);
        entity.Status = ProcurementAppSubmissionStatus.Submitted;
        entity.ExternalSubmissionReference = request.ExternalSubmissionReference.Trim();
        entity.SubmittedAtUtc = submittedAt;
        entity.SubmittedById = _currentUser.UserId;
        entity.SubmittedByName = ActorName;
        ApplyUpdate(entity, request.Notes);
        var evidence = ExternalEvidence(request.Evidence, entity.ExternalSubmissionReference, "GHANEPS/PPA submission reference", "APP_SUBMISSION");
        return await ExecuteMutationAsync(entity, false, "Submitted", before, Snapshot(entity), request.Notes,
            evidence, causationId, cancellationToken);
    }

    public async Task<ProcurementAppSubmissionDto> AcknowledgeAsync(
        Guid id,
        AcknowledgeProcurementAppRequest request,
        string causationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReference(request.AcknowledgementReference, "ACKNOWLEDGEMENT_REFERENCE_REQUIRED", "AcknowledgementReference is required.");
        var acknowledgedAt = ValidateEventDate(request.AcknowledgedAtUtc, "ACKNOWLEDGEMENT_DATE_REQUIRED");
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureManagerAsync(entity.SubmissionNumber, causationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementAppSubmissionStatus.Submitted, "Only a Submitted APP attempt can be acknowledged.");
        if (acknowledgedAt < entity.SubmittedAtUtc)
            throw new ProcurementAppSubmissionValidationException("ACKNOWLEDGEMENT_DATE_INVALID", "AcknowledgedAtUtc cannot be before submission.");
        var before = Snapshot(entity);
        entity.Status = ProcurementAppSubmissionStatus.Acknowledged;
        entity.AcknowledgementReference = request.AcknowledgementReference.Trim();
        entity.AcknowledgedAtUtc = acknowledgedAt;
        entity.AcknowledgedById = _currentUser.UserId;
        entity.AcknowledgedByName = ActorName;
        ApplyUpdate(entity, request.Notes);
        var evidence = ExternalEvidence(request.Evidence, entity.AcknowledgementReference, "GHANEPS/PPA acknowledgement", "APP_ACKNOWLEDGEMENT");
        return await ExecuteMutationAsync(entity, false, "Acknowledged", before, Snapshot(entity), request.Notes,
            evidence, causationId, cancellationToken);
    }

    public async Task<ProcurementAppSubmissionDto> RejectAsync(
        Guid id,
        RejectProcurementAppRequest request,
        string causationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReference(request.RejectionReference, "REJECTION_REFERENCE_REQUIRED", "RejectionReference is required.");
        EnsureRequired(request.RejectionReason, "REJECTION_REASON_REQUIRED", "RejectionReason is required.");
        var rejectedAt = ValidateEventDate(request.RejectedAtUtc, "REJECTION_DATE_REQUIRED");
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureManagerAsync(entity.SubmissionNumber, causationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementAppSubmissionStatus.Submitted, "Only a Submitted APP attempt can be rejected.");
        if (rejectedAt < entity.SubmittedAtUtc)
            throw new ProcurementAppSubmissionValidationException("REJECTION_DATE_INVALID", "RejectedAtUtc cannot be before submission.");
        var before = Snapshot(entity);
        entity.Status = ProcurementAppSubmissionStatus.Rejected;
        entity.RejectionReference = request.RejectionReference.Trim();
        entity.RejectionReason = Truncate(request.RejectionReason.Trim(), 1000);
        entity.RejectedAtUtc = rejectedAt;
        entity.RejectedById = _currentUser.UserId;
        entity.RejectedByName = ActorName;
        ApplyUpdate(entity, request.Notes);
        var evidence = ExternalEvidence(request.Evidence, entity.RejectionReference, "GHANEPS/PPA rejection notice", "APP_REJECTION");
        return await ExecuteMutationAsync(entity, false, "Rejected", before, Snapshot(entity), entity.RejectionReason,
            evidence, causationId, cancellationToken);
    }

    public async Task<ProcurementAppSubmissionDto> ResubmitAsync(
        Guid id,
        ResubmitProcurementAppRequest request,
        string causationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var exportFormat = ValidateExportFormat(request.ExportFormat);
        var rejected = await LoadAsync(id, true, cancellationToken);
        await EnsureManagerAsync(rejected.SubmissionNumber, causationId, cancellationToken);
        EnsureRowVersion(rejected.RowVersion, request.RowVersion);
        EnsureStatus(rejected, ProcurementAppSubmissionStatus.Rejected, "Only a Rejected APP attempt can be resubmitted.");
        var latestAttempt = await Submissions.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.ProcurementPlanId == rejected.ProcurementPlanId && !item.IsDeleted)
            .MaxAsync(item => item.AttemptNumber, cancellationToken);
        if (latestAttempt != rejected.AttemptNumber)
            throw new ProcurementAppSubmissionConflictException("A later APP attempt already exists for this plan version.");
        var plan = await Plans.GetQueryable(item => item.Id == rejected.ProcurementPlanId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Items.Where(planItem => !planItem.IsDeleted))
            .SingleAsync(cancellationToken);
        EnsurePublishedPlan(plan);
        var package = BuildExportPackage(plan, exportFormat);
        var uploaded = await UploadPackageAsync(package, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProcurementAppSubmission
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            ProcurementPlanId = rejected.ProcurementPlanId,
            SubmissionNumber = rejected.SubmissionNumber,
            AttemptNumber = rejected.AttemptNumber + 1,
            Status = ProcurementAppSubmissionStatus.Exported,
            TimelineCorrelationId = rejected.TimelineCorrelationId,
            SupersedesSubmissionId = rejected.Id,
            ExportFileName = package.FileName,
            ExportFormat = exportFormat,
            ExportTemplateVersion = ExportTemplateVersion,
            ExportChecksumSha256 = uploaded.ChecksumSha256.ToUpperInvariant(),
            ExportedAtUtc = now,
            ExportedById = _currentUser.UserId,
            ExportedByName = ActorName,
            Notes = TrimOrNull(request.Notes, 2000),
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray(),
            ProcurementPlan = plan
        };
        try
        {
            return await ExecuteMutationAsync(entity, true, "ResubmissionRecorded", Snapshot(rejected), Snapshot(entity),
                request.Notes, ExportEvidence(request.Evidence, entity, uploaded.Record.Id), causationId, cancellationToken);
        }
        catch
        {
            await TryDeleteGeneratedPackageAsync(uploaded.Record.Id, cancellationToken);
            throw;
        }
    }

    private async Task<ProcurementAppSubmissionDto> ExecuteMutationAsync(
        ProcurementAppSubmission entity,
        bool add,
        string action,
        object? before,
        object after,
        string? reason,
        List<ProcurementControlEventEvidenceReference> evidence,
        string causationId,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (add) await Submissions.AddAsync(entity);
                else await Submissions.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
                {
                    EventKey = ProcurementControlEventKey.Create("app", _currentUser.TenantId, entity.Id, action),
                    EventType = EventType,
                    Action = action,
                    Result = action == "Rejected" ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Succeeded,
                    RuleCode = "DEC-009",
                    DecisionKeys = ["DEC-009"],
                    SourceType = SourceType,
                    SourceId = entity.Id,
                    SourceReference = $"{entity.SubmissionNumber}/A{entity.AttemptNumber}",
                    Reason = TrimOrNull(reason, 1000),
                    Before = before,
                    After = after,
                    ResultValues = new { entity.Status, entity.AttemptNumber, entity.ProcurementPlanId },
                    CorrelationId = entity.TimelineCorrelationId,
                    CausationId = NormalizeCausation(causationId),
                    OccurredAtUtc = action switch
                    {
                        "Submitted" => entity.SubmittedAtUtc!.Value,
                        "Acknowledged" => entity.AcknowledgedAtUtc!.Value,
                        "Rejected" => entity.RejectedAtUtc!.Value,
                        _ => entity.ExportedAtUtc
                    },
                    Evidence = evidence
                }, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementAppSubmissionConflictException("The APP submission attempt changed by another user. Reload before continuing.");
            }
            catch (ProcurementControlEventNotFoundException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementAppSubmissionNotFoundException(exception.Message);
            }
            catch (ProcurementControlEventValidationException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementAppSubmissionValidationException(exception.Code, exception.Message);
            }
            catch (ProcurementControlEventConflictException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementAppSubmissionConflictException(exception.Message);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            _logger.LogInformation("Recorded APP submission {SubmissionNumber} attempt {AttemptNumber} action {Action} for tenant {TenantId}",
                entity.SubmissionNumber, entity.AttemptNumber, action, entity.TenantId);
            return await GetMappedAsync(entity.Id, cancellationToken);
        }, cancellationToken);
    }

    private async Task<ProcurementAppSubmissionDto> GetMappedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, false, cancellationToken);
        return Map(entity, await LoadTimelineAsync(entity.TimelineCorrelationId, cancellationToken));
    }

    private IQueryable<ProcurementAppSubmission> QueryWithPlan() =>
        Submissions.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.ProcurementPlan);

    private async Task<ProcurementAppSubmission> LoadAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = QueryWithPlan().Where(item => item.Id == id);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAppSubmissionNotFoundException("The APP submission attempt was not found in the current tenant.");
    }

    private async Task<List<ProcurementAppSubmissionTimelineEventDto>> LoadTimelineAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        var rows = await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EventType == EventType && item.CorrelationId == correlationId)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking().OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(item => new ProcurementAppSubmissionTimelineEventDto
        {
            Id = item.Id,
            EventType = item.EventType,
            Action = item.Action,
            Result = item.Result,
            SourceReference = item.SourceReference,
            ActorName = item.ActorName,
            Reason = item.Reason,
            OccurredAtUtc = item.OccurredAtUtc,
            IntegrityHash = item.IntegrityHash,
            Evidence = item.EvidenceLinks.OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference)
                .Select(MapEvidence).ToList()
        }).ToList();
    }

    private static ProcurementAppSubmissionEvidenceDto MapEvidence(ProcurementControlEventEvidenceLink link)
    {
        var workflow = link.WorkflowEvidenceDocument;
        var upload = link.FileUploadRecord;
        return new ProcurementAppSubmissionEvidenceDto
        {
            ReferenceKind = link.ReferenceKind,
            ReferenceId = link.WorkflowEvidenceDocumentId ?? link.FileUploadRecordId,
            Reference = link.Reference,
            Label = link.Label,
            RequirementKey = link.RequirementKey,
            FileName = workflow?.FileName ?? upload?.OriginalFileName,
            Sha256 = workflow?.Sha256,
            VerificationStatus = workflow?.VerificationStatus.ToString() ?? upload?.VirusScanStatus.ToString(),
            ReferenceAvailable = link.ReferenceKind == ProcurementControlEvidenceReferenceKind.ExternalReference || workflow is not null || upload is not null
        };
    }

    private static ProcurementAppSubmissionDto Map(
        ProcurementAppSubmission item,
        List<ProcurementAppSubmissionTimelineEventDto> timeline) => new()
    {
        Id = item.Id,
        ProcurementPlanId = item.ProcurementPlanId,
        PlanNumber = item.ProcurementPlan.PlanNumber,
        PlanTitle = item.ProcurementPlan.Title,
        FiscalYear = item.ProcurementPlan.FiscalYear,
        PlanRevisionNumber = item.ProcurementPlan.RevisionNumber,
        PlanPublishedDate = item.ProcurementPlan.PublishedDate,
        SubmissionNumber = item.SubmissionNumber,
        AttemptNumber = item.AttemptNumber,
        Status = item.Status,
        TimelineCorrelationId = item.TimelineCorrelationId,
        SupersedesSubmissionId = item.SupersedesSubmissionId,
        ExportFileName = item.ExportFileName,
        ExportFormat = item.ExportFormat,
        ExportTemplateVersion = item.ExportTemplateVersion,
        ExportChecksumSha256 = item.ExportChecksumSha256,
        ExportedAtUtc = item.ExportedAtUtc,
        ExportedById = item.ExportedById,
        ExportedByName = item.ExportedByName,
        ExternalSubmissionReference = item.ExternalSubmissionReference,
        SubmittedAtUtc = item.SubmittedAtUtc,
        SubmittedById = item.SubmittedById,
        SubmittedByName = item.SubmittedByName,
        AcknowledgementReference = item.AcknowledgementReference,
        AcknowledgedAtUtc = item.AcknowledgedAtUtc,
        AcknowledgedById = item.AcknowledgedById,
        AcknowledgedByName = item.AcknowledgedByName,
        RejectionReference = item.RejectionReference,
        RejectionReason = item.RejectionReason,
        RejectedAtUtc = item.RejectedAtUtc,
        RejectedById = item.RejectedById,
        RejectedByName = item.RejectedByName,
        Notes = item.Notes,
        CreatedAtUtc = item.CreatedAt,
        UpdatedAtUtc = item.UpdatedAt,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Timeline = timeline
    };

    private async Task EnsureManagerAsync(string sourceReference, string causationId, CancellationToken cancellationToken)
    {
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = PlanPermission,
            SourceType = SourceType,
            SourceReference = sourceReference
        }, causationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementAppSubmissionAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read")) return;
        throw new ProcurementAppSubmissionAuthorizationException("The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementAppSubmissionAuthorizationException("An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() => _currentUser.HasRole(Constants.Roles.SuperAdmin);
    private string ActorName => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);

    private static void EnsurePublishedPlan(ProcurementPlan plan)
    {
        if (!string.Equals(plan.Status, "Active", StringComparison.OrdinalIgnoreCase) || !plan.PublishedDate.HasValue)
            throw new ProcurementAppSubmissionConflictException("Only an approved and published Active procurement-plan version can enter the APP register.");
        if (plan.Items.Count == 0)
            throw new ProcurementAppSubmissionConflictException("A published procurement plan must contain at least one item before APP export is recorded.");
    }

    private static void EnsureStatus(ProcurementAppSubmission entity, ProcurementAppSubmissionStatus required, string message)
    {
        if (entity.Status != required) throw new ProcurementAppSubmissionConflictException(message);
    }

    private static string ValidateExportFormat(string? value)
    {
        var format = string.IsNullOrWhiteSpace(value) ? "CSV" : value.Trim().ToUpperInvariant();
        if (!SupportedExportFormats.Contains(format))
            throw new ProcurementAppSubmissionValidationException(
                "EXPORT_FORMAT_INVALID",
                "APP exports are available as CSV, JSON or XML.");
        return format;
    }

    private async Task<ControlledFileUploadResult> UploadPackageAsync(
        AppExportPackage package,
        CancellationToken cancellationToken) =>
        await _controlledFiles.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = _currentUser.TenantId,
            ActorUserId = _currentUser.UserId,
            ActorName = ActorName,
            Category = ControlledFileUploadCategories.ProcurementAppExchange,
            FileName = package.FileName,
            ContentType = package.ContentType,
            FileSize = package.Content.LongLength,
            OpenReadStream = () => new MemoryStream(package.Content, writable: false)
        }, cancellationToken);

    private async Task TryDeleteGeneratedPackageAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        try
        {
            await _controlledFiles.DeleteAsync(_currentUser.TenantId, uploadId, _currentUser.UserId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to roll back generated APP package {FileUploadRecordId}.", uploadId);
        }
    }

    private static AppExportPackage BuildExportPackage(ProcurementPlan plan, string format)
    {
        var extension = format.ToLowerInvariant();
        var fileName = $"APP-{plan.FiscalYear}-{SafeFilePart(plan.PlanNumber)}-R{plan.RevisionNumber}.{extension}";
        var orderedItems = plan.Items.Where(item => !item.IsDeleted)
            .OrderBy(item => item.ItemDescription)
            .ThenBy(item => item.Id)
            .ToList();
        return format switch
        {
            "JSON" => new AppExportPackage(fileName, "application/json", BuildJson(plan, orderedItems)),
            "XML" => new AppExportPackage(fileName, "application/xml", BuildXml(plan, orderedItems)),
            _ => new AppExportPackage(fileName, "text/csv", BuildCsv(plan, orderedItems))
        };
    }

    private static byte[] BuildJson(ProcurementPlan plan, IReadOnlyList<ProcurementPlanItem> items) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "tdc.annual-procurement-plan.v1",
            templateVersion = ExportTemplateVersion,
            plan = new
            {
                plan.PlanNumber,
                plan.Title,
                plan.FiscalYear,
                plan.RevisionNumber,
                plan.PlanningCycle,
                plan.PlanStartDate,
                plan.PlanEndDate,
                plan.Currency,
                plan.TotalEstimatedBudget,
                plan.ApprovedBudget,
                plan.PublishedDate
            },
            items = items.Select(ItemPayload)
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

    private static byte[] BuildXml(ProcurementPlan plan, IReadOnlyList<ProcurementPlanItem> items)
    {
        var document = new XDocument(
            new XElement("AnnualProcurementPlan",
                new XAttribute("schemaVersion", "tdc.annual-procurement-plan.v1"),
                new XAttribute("templateVersion", ExportTemplateVersion),
                new XElement("PlanNumber", plan.PlanNumber),
                new XElement("Title", plan.Title),
                new XElement("FiscalYear", plan.FiscalYear),
                new XElement("RevisionNumber", plan.RevisionNumber),
                new XElement("PlanningCycle", plan.PlanningCycle),
                new XElement("PlanStartDate", Iso(plan.PlanStartDate)),
                new XElement("PlanEndDate", Iso(plan.PlanEndDate)),
                new XElement("Currency", plan.Currency),
                new XElement("TotalEstimatedBudget", Number(plan.TotalEstimatedBudget)),
                new XElement("ApprovedBudget", Number(plan.ApprovedBudget)),
                new XElement("PublishedDate", Iso(plan.PublishedDate)),
                new XElement("Items", items.Select(item => new XElement("Item",
                    new XElement("Description", item.ItemDescription),
                    new XElement("Category", item.ItemCategory ?? string.Empty),
                    new XElement("Specifications", item.Specifications ?? string.Empty),
                    new XElement("Quantity", Number(item.EstimatedQuantity)),
                    new XElement("UnitOfMeasure", item.UnitOfMeasure),
                    new XElement("UnitPrice", Number(item.EstimatedUnitPrice)),
                    new XElement("TotalCost", Number(item.EstimatedTotalCost)),
                    new XElement("Currency", item.Currency),
                    new XElement("Priority", item.Priority),
                    new XElement("RequiredDate", Iso(item.RequiredDate)),
                    new XElement("Quarter", item.PlannedQuarter ?? string.Empty),
                    new XElement("ProcurementMethod", item.ProcurementMethod ?? string.Empty),
                    new XElement("BudgetLine", item.BudgetLineCode ?? string.Empty),
                    new XElement("BudgetCategory", item.BudgetCategoryName ?? string.Empty),
                    new XElement("ApprovedBudget", Number(item.ApprovedBudgetAmount)))))));
        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static byte[] BuildCsv(ProcurementPlan plan, IReadOnlyList<ProcurementPlanItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Plan Number,Plan Title,Fiscal Year,Revision,Published Date,Item Description,Item Category,Specifications,Quantity,UOM,Unit Cost,Total Cost,Currency,Priority,Required By,Quarter,Procurement Method,Budget Line,Budget Category,Approved Budget");
        foreach (var item in items)
        {
            var values = new[]
            {
                plan.PlanNumber, plan.Title, plan.FiscalYear.ToString(CultureInfo.InvariantCulture),
                plan.RevisionNumber.ToString(CultureInfo.InvariantCulture), Iso(plan.PublishedDate),
                item.ItemDescription, item.ItemCategory, item.Specifications, Number(item.EstimatedQuantity),
                item.UnitOfMeasure, Number(item.EstimatedUnitPrice), Number(item.EstimatedTotalCost),
                item.Currency, item.Priority, Iso(item.RequiredDate), item.PlannedQuarter,
                item.ProcurementMethod, item.BudgetLineCode, item.BudgetCategoryName,
                Number(item.ApprovedBudgetAmount)
            };
            builder.AppendLine(string.Join(',', values.Select(Csv)));
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static object ItemPayload(ProcurementPlanItem item) => new
    {
        description = item.ItemDescription,
        category = item.ItemCategory,
        item.Specifications,
        quantity = item.EstimatedQuantity,
        unitOfMeasure = item.UnitOfMeasure,
        unitCost = item.EstimatedUnitPrice,
        totalCost = item.EstimatedTotalCost,
        item.Currency,
        item.Priority,
        requiredBy = item.RequiredDate,
        quarter = item.PlannedQuarter,
        procurementMethod = item.ProcurementMethod,
        budgetLine = item.BudgetLineCode,
        budgetCategory = item.BudgetCategoryName,
        approvedBudget = item.ApprovedBudgetAmount
    };

    private static string SafeFilePart(string value)
    {
        var safe = Regex.Replace(value.Trim(), "[^A-Za-z0-9_-]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "PLAN" : safe[..Math.Min(safe.Length, 80)];
    }

    private static string Csv(string? value)
    {
        var normalized = value ?? string.Empty;
        if (normalized.Length > 0 && "=+-@\t\r".Contains(normalized[0]))
            normalized = $"'{normalized}";
        return normalized.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? normalized
            : $"\"{normalized.Replace("\"", "\"\"")}\"";
    }

    private static string Number(decimal? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Iso(DateTime? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record AppExportPackage(string FileName, string ContentType, byte[] Content);

    private static void EnsureRequired(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ProcurementAppSubmissionValidationException(code, message);
    }

    private static void EnsureReference(string? value, string code, string message)
    {
        EnsureRequired(value, code, message);
        if (value!.Trim().Length > 200)
            throw new ProcurementAppSubmissionValidationException("REFERENCE_TOO_LONG", "External references cannot exceed 200 characters.");
    }

    private static DateTime ValidateEventDate(DateTime value, string requiredCode)
    {
        if (value == default) throw new ProcurementAppSubmissionValidationException(requiredCode, "The event date is required.");
        var utc = EnsureUtc(value);
        if (utc > DateTime.UtcNow.AddMinutes(5))
            throw new ProcurementAppSubmissionValidationException("EVENT_DATE_FUTURE", "The event date cannot be in the future.");
        return utc;
    }

    private static void EnsureRowVersion(byte[] current, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw new ProcurementAppSubmissionConflictException("The APP submission row version is required. Reload before continuing.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch { throw new ProcurementAppSubmissionConflictException("The APP submission row version is invalid. Reload before continuing."); }
        if (!current.SequenceEqual(parsed))
            throw new ProcurementAppSubmissionConflictException("The APP submission attempt changed by another user. Reload before continuing.");
    }

    private void ApplyUpdate(ProcurementAppSubmission entity, string? notes)
    {
        entity.Notes = TrimOrNull(notes, 2000) ?? entity.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.Username;
        entity.LastModifiedById = _currentUser.UserId;
    }

    private async Task<string> NextSubmissionNumberAsync(int year, CancellationToken cancellationToken)
    {
        var prefix = $"APP-{year}-";
        var count = await Submissions.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
            item.SubmissionNumber.StartsWith(prefix) && item.AttemptNumber == 1).CountAsync(cancellationToken);
        return $"{prefix}{count + 1:00000}";
    }

    private static List<ProcurementControlEventEvidenceReference> ExportEvidence(
        IEnumerable<ProcurementControlEventEvidenceReference> supplied,
        ProcurementAppSubmission entity,
        Guid packageFileUploadRecordId)
    {
        var result = ExternalEvidence(supplied, $"sha256:{entity.ExportChecksumSha256}",
            $"{entity.ExportFileName} ({entity.ExportFormat}, template {entity.ExportTemplateVersion})", "APP_EXPORT");
        result.Add(new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
            ReferenceId = packageFileUploadRecordId,
            Label = "Generated Annual Procurement Plan export package",
            RequirementKey = "APP_EXPORT_PACKAGE"
        });
        return result;
    }

    private static List<ProcurementControlEventEvidenceReference> ExternalEvidence(
        IEnumerable<ProcurementControlEventEvidenceReference> supplied,
        string reference,
        string label,
        string requirementKey)
    {
        var result = supplied.Select(item => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = item.ReferenceKind,
            ReferenceId = item.ReferenceId,
            Reference = item.Reference,
            Label = item.Label,
            RequirementKey = item.RequirementKey
        }).ToList();
        if (!result.Any(item => item.ReferenceKind == ProcurementControlEvidenceReferenceKind.ExternalReference &&
                string.Equals(item.Reference?.Trim(), reference, StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = reference,
                Label = label,
                RequirementKey = requirementKey
            });
        }
        return result;
    }

    private static object Snapshot(ProcurementAppSubmission item) => new
    {
        item.Id,
        item.ProcurementPlanId,
        item.SubmissionNumber,
        item.AttemptNumber,
        item.Status,
        item.TimelineCorrelationId,
        item.SupersedesSubmissionId,
        item.ExportFileName,
        item.ExportFormat,
        item.ExportTemplateVersion,
        item.ExportChecksumSha256,
        item.ExportedAtUtc,
        item.ExternalSubmissionReference,
        item.SubmittedAtUtc,
        item.AcknowledgementReference,
        item.AcknowledgedAtUtc,
        item.RejectionReference,
        item.RejectionReason,
        item.RejectedAtUtc
    };

    private static string NormalizeCausation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N")
        : Truncate(value.Trim(), 100);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string? TrimOrNull(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), length);
}
