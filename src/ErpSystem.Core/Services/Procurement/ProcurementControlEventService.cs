using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

public sealed class ProcurementControlEventService : IProcurementControlEventService
{
    private const int SchemaVersion = 1;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<ProcurementControlEventService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProcurementControlEventService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ILogger<ProcurementControlEventService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IGenericRepository<ProcurementControlEvent> Events => _unitOfWork.Repository<ProcurementControlEvent>();
    private IGenericRepository<WorkflowEvidenceDocument> WorkflowEvidence => _unitOfWork.Repository<WorkflowEvidenceDocument>();
    private IGenericRepository<FileUploadRecord> FileUploads => _unitOfWork.Repository<FileUploadRecord>();

    public async Task<ProcurementControlEventDto> RecordAsync(
        ProcurementControlEventWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        ValidateWrite(request);
        var roles = _currentUser.Roles.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
        var decisionKeys = request.DecisionKeys.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToUpperInvariant()).Distinct().OrderBy(item => item).ToList();
        var evidence = await ResolveEvidenceAsync(request.Evidence, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProcurementControlEvent
        {
            TenantId = _currentUser.TenantId,
            EventKey = request.EventKey.Trim(),
            SchemaVersion = SchemaVersion,
            EventType = request.EventType.Trim(),
            Action = request.Action.Trim(),
            Result = request.Result,
            RuleCode = TrimOrNull(request.RuleCode),
            RuleId = request.RuleId,
            RuleVersion = TrimOrNull(request.RuleVersion),
            DecisionKeysJson = JsonSerializer.Serialize(decisionKeys, JsonOptions),
            SourceType = request.SourceType.Trim(),
            SourceId = request.SourceId,
            SourceReference = request.SourceReference.Trim(),
            ActorUserId = _currentUser.UserId,
            ActorName = Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300),
            ActorRolesJson = JsonSerializer.Serialize(roles, JsonOptions),
            Reason = TruncateNullable(request.Reason, 1000),
            InputValuesJson = SerializeOrNull(request.InputValues),
            ResultValuesJson = SerializeOrNull(request.ResultValues),
            BeforeJson = SerializeOrNull(request.Before),
            AfterJson = SerializeOrNull(request.After),
            CorrelationId = request.CorrelationId.Trim(),
            CausationId = TrimOrNull(request.CausationId),
            OccurredAtUtc = EnsureUtc(request.OccurredAtUtc),
            CreatedAt = now,
            CreatedBy = Truncate(_currentUser.Username, 300),
            CreatedById = _currentUser.UserId,
            EvidenceLinks = evidence
        };
        entity.IntegrityHash = ComputeHash(entity);

        var existing = await Events.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.EventKey == entity.EventKey && !item.IsDeleted)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (string.Equals(existing.IntegrityHash, entity.IntegrityHash, StringComparison.OrdinalIgnoreCase))
                return Map(existing);
            throw new ProcurementControlEventConflictException("EventKey already identifies a different immutable control event.");
        }

        await Events.AddAsync(entity);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Control event append conflicted for tenant {TenantId}, key {EventKey}",
                _currentUser.TenantId, entity.EventKey);
            throw new ProcurementControlEventConflictException("The control event could not be appended because its immutable key already exists.");
        }

        _logger.LogInformation("Appended procurement control event {EventType}/{Action} with result {Result} and correlation {CorrelationId}",
            entity.EventType, entity.Action, entity.Result, entity.CorrelationId);
        var saved = await Events.GetQueryable(item => item.Id == entity.Id && item.TenantId == _currentUser.TenantId)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking().SingleAsync(cancellationToken);
        return Map(saved);
    }

    public async Task<ProcurementControlEventDto> RecordSystemAsync(
        Guid tenantId,
        string actorName,
        ProcurementControlEventWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ProcurementControlEventValidationException("TENANT_REQUIRED", "A tenant is required for a system control event.");
        if (string.IsNullOrWhiteSpace(actorName))
            throw new ProcurementControlEventValidationException("ACTOR_REQUIRED", "A system actor name is required.");
        ValidateWrite(request);
        if (request.Evidence.Count != 0)
            throw new ProcurementControlEventValidationException("SYSTEM_EVIDENCE_UNSUPPORTED",
                "System-generated control events must reference evidence through a user-authorized action.");

        var systemActorId = await _unitOfWork.Repository<UserTenant>()
            .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == UserTenantStatus.Active &&
                (item.ExpiresAt == null || item.ExpiresAt > DateTime.UtcNow) && item.User.IsActive)
            .OrderByDescending(item => item.User.UserRoles.Any(role => role.Role.Name == "SuperAdmin"))
            .ThenByDescending(item => item.User.UserRoles.Any(role => role.Role.Name == "TenantAdmin"))
            .ThenBy(item => item.User.UserName)
            .Select(item => item.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (systemActorId == Guid.Empty)
            throw new ProcurementControlEventValidationException("SYSTEM_ACTOR_UNAVAILABLE",
                "A system control event requires at least one active user in the tenant for referential audit lineage.");

        var now = DateTime.UtcNow;
        var decisionKeys = request.DecisionKeys.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToUpperInvariant()).Distinct().OrderBy(item => item).ToList();
        var entity = new ProcurementControlEvent
        {
            TenantId = tenantId,
            EventKey = request.EventKey.Trim(),
            SchemaVersion = SchemaVersion,
            EventType = request.EventType.Trim(),
            Action = request.Action.Trim(),
            Result = request.Result,
            RuleCode = TrimOrNull(request.RuleCode),
            RuleId = request.RuleId,
            RuleVersion = TrimOrNull(request.RuleVersion),
            DecisionKeysJson = JsonSerializer.Serialize(decisionKeys, JsonOptions),
            SourceType = request.SourceType.Trim(),
            SourceId = request.SourceId,
            SourceReference = request.SourceReference.Trim(),
            ActorUserId = systemActorId,
            ActorName = Truncate(actorName.Trim(), 300),
            ActorRolesJson = JsonSerializer.Serialize(new[] { "System" }, JsonOptions),
            Reason = TruncateNullable(request.Reason, 1000),
            InputValuesJson = SerializeOrNull(request.InputValues),
            ResultValuesJson = SerializeOrNull(request.ResultValues),
            BeforeJson = SerializeOrNull(request.Before),
            AfterJson = SerializeOrNull(request.After),
            CorrelationId = request.CorrelationId.Trim(),
            CausationId = TrimOrNull(request.CausationId),
            OccurredAtUtc = EnsureUtc(request.OccurredAtUtc),
            CreatedAt = now,
            CreatedBy = Truncate(actorName.Trim(), 300)
        };
        entity.IntegrityHash = ComputeHash(entity);

        var existing = await Events.GetQueryable(item => item.TenantId == tenantId &&
                item.EventKey == entity.EventKey && !item.IsDeleted)
            .Include(item => item.EvidenceLinks).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (string.Equals(existing.IntegrityHash, entity.IntegrityHash, StringComparison.OrdinalIgnoreCase))
                return Map(existing);
            throw new ProcurementControlEventConflictException("EventKey already identifies a different immutable control event.");
        }

        await Events.AddAsync(entity);
        try { await _unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            throw new ProcurementControlEventConflictException(
                "The system control event could not be appended because its immutable key already exists.");
        }
        return Map(entity);
    }

    public async Task<ProcurementControlEventSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var query = Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted).AsNoTracking();
        var grouped = await query.GroupBy(item => item.EventType)
            .Select(group => new { group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        return new ProcurementControlEventSummaryDto
        {
            TotalCount = await query.CountAsync(cancellationToken),
            AllowedCount = await query.CountAsync(item => item.Result == ProcurementControlEventResult.Allowed || item.Result == ProcurementControlEventResult.Succeeded, cancellationToken),
            DeniedOrRejectedCount = await query.CountAsync(item => item.Result == ProcurementControlEventResult.Denied || item.Result == ProcurementControlEventResult.Rejected, cancellationToken),
            FailedCount = await query.CountAsync(item => item.Result == ProcurementControlEventResult.Failed, cancellationToken),
            EvidenceLinkedCount = await query.CountAsync(item => item.EvidenceLinks.Any(), cancellationToken),
            LatestOccurredAtUtc = await query.OrderByDescending(item => item.OccurredAtUtc).Select(item => (DateTime?)item.OccurredAtUtc).FirstOrDefaultAsync(cancellationToken),
            ByEventType = grouped.ToDictionary(item => item.Key, item => item.Count)
        };
    }

    public async Task<ProcurementControlEventPageDto> SearchAsync(
        ProcurementControlEventSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = Filter(request);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking().ToListAsync(cancellationToken);
        return new ProcurementControlEventPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(Map).ToList()
        };
    }

    public async Task<ProcurementControlEventDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var row = await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementControlEventNotFoundException("The control event was not found in the current tenant.");
        return Map(row);
    }

    public async Task<IReadOnlyList<ProcurementControlEventDto>> GetCorrelationAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ProcurementControlEventValidationException("CORRELATION_REQUIRED", "CorrelationId is required.");
        return (await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.CorrelationId == correlationId.Trim() && !item.IsDeleted)
                .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
                .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
                .AsNoTracking().OrderBy(item => item.OccurredAtUtc).ToListAsync(cancellationToken))
            .Select(Map).ToList();
    }

    public async Task<ProcurementControlEventIntegrityDto> VerifyIntegrityAsync(
        int take = 1000,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var rows = await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.EvidenceLinks)
            .AsNoTracking().OrderByDescending(item => item.OccurredAtUtc).Take(Math.Clamp(take, 1, 5000))
            .ToListAsync(cancellationToken);
        var issues = rows.Select(item => new { Item = item, Expected = ComputeHash(item) })
            .Where(item => !string.Equals(item.Expected, item.Item.IntegrityHash, StringComparison.OrdinalIgnoreCase))
            .Select(item => new ProcurementControlEventIntegrityIssueDto
            {
                EventId = item.Item.Id,
                EventKey = item.Item.EventKey,
                ExpectedHash = item.Expected,
                ActualHash = item.Item.IntegrityHash
            }).ToList();
        return new ProcurementControlEventIntegrityDto
        {
            CheckedCount = rows.Count,
            ValidCount = rows.Count - issues.Count,
            InvalidCount = issues.Count,
            IsValid = issues.Count == 0,
            VerifiedAtUtc = DateTime.UtcNow,
            Issues = issues
        };
    }

    private IQueryable<ProcurementControlEvent> Filter(ProcurementControlEventSearchRequest request)
    {
        var query = Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.EventType)) query = query.Where(item => item.EventType == request.EventType.Trim());
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(item => item.Action == request.Action.Trim());
        if (request.Result.HasValue) query = query.Where(item => item.Result == request.Result.Value);
        if (!string.IsNullOrWhiteSpace(request.RuleCode)) query = query.Where(item => item.RuleCode == request.RuleCode.Trim());
        if (!string.IsNullOrWhiteSpace(request.SourceType)) query = query.Where(item => item.SourceType == request.SourceType.Trim());
        if (!string.IsNullOrWhiteSpace(request.SourceReference)) query = query.Where(item => item.SourceReference.Contains(request.SourceReference.Trim()));
        if (!string.IsNullOrWhiteSpace(request.CorrelationId)) query = query.Where(item => item.CorrelationId == request.CorrelationId.Trim());
        if (request.ActorUserId.HasValue) query = query.Where(item => item.ActorUserId == request.ActorUserId.Value);
        if (request.FromUtc.HasValue) query = query.Where(item => item.OccurredAtUtc >= EnsureUtc(request.FromUtc.Value));
        if (request.ToUtc.HasValue) query = query.Where(item => item.OccurredAtUtc <= EnsureUtc(request.ToUtc.Value));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.EventKey.Contains(search) || item.ActorName.Contains(search) ||
                                        item.SourceReference.Contains(search) || item.CorrelationId.Contains(search) ||
                                        (item.RuleCode != null && item.RuleCode.Contains(search)));
        }
        return query;
    }

    private async Task<List<ProcurementControlEventEvidenceLink>> ResolveEvidenceAsync(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> references,
        CancellationToken cancellationToken)
    {
        var rows = new List<ProcurementControlEventEvidenceLink>();
        foreach (var reference in references)
        {
            var row = new ProcurementControlEventEvidenceLink
            {
                TenantId = _currentUser.TenantId,
                ReferenceKind = reference.ReferenceKind,
                Label = TruncateNullable(reference.Label, 300),
                RequirementKey = TruncateNullable(reference.RequirementKey, 200),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Truncate(_currentUser.Username, 300),
                CreatedById = _currentUser.UserId
            };
            if (reference.ReferenceKind == ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument)
            {
                if (!reference.ReferenceId.HasValue)
                    throw new ProcurementControlEventValidationException("EVIDENCE_ID_REQUIRED", "Workflow evidence requires ReferenceId.");
                var evidence = await WorkflowEvidence.GetQueryable(item => item.Id == reference.ReferenceId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ProcurementControlEventNotFoundException("The workflow evidence reference was not found in the current tenant.");
                row.WorkflowEvidenceDocumentId = evidence.Id;
                row.Reference = evidence.AttachmentId;
            }
            else if (reference.ReferenceKind == ProcurementControlEvidenceReferenceKind.FileUploadRecord)
            {
                if (!reference.ReferenceId.HasValue)
                    throw new ProcurementControlEventValidationException("EVIDENCE_ID_REQUIRED", "File evidence requires ReferenceId.");
                var upload = await FileUploads.GetQueryable(item => item.Id == reference.ReferenceId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ProcurementControlEventNotFoundException("The shared file-upload reference was not found in the current tenant.");
                row.FileUploadRecordId = upload.Id;
                row.Reference = upload.Id.ToString("N");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(reference.Reference))
                    throw new ProcurementControlEventValidationException("EVIDENCE_REFERENCE_REQUIRED", "External evidence requires a reference value.");
                row.Reference = Truncate(reference.Reference.Trim(), 200);
            }
            rows.Add(row);
        }
        if (rows.GroupBy(item => new { item.ReferenceKind, item.Reference }).Any(group => group.Count() > 1))
            throw new ProcurementControlEventValidationException("EVIDENCE_DUPLICATE", "Evidence references must be unique within an event.");
        return rows;
    }

    private static ProcurementControlEventDto Map(ProcurementControlEvent item)
    {
        var evidence = item.EvidenceLinks.OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference).Select(link =>
        {
            var workflow = link.WorkflowEvidenceDocument;
            var upload = link.FileUploadRecord;
            return new ProcurementControlEventEvidenceDto
            {
                Id = link.Id,
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
        }).ToList();
        return new ProcurementControlEventDto
        {
            Id = item.Id,
            TenantId = item.TenantId,
            EventKey = item.EventKey,
            SchemaVersion = item.SchemaVersion,
            EventType = item.EventType,
            Action = item.Action,
            Result = item.Result,
            RuleCode = item.RuleCode,
            RuleId = item.RuleId,
            RuleVersion = item.RuleVersion,
            DecisionKeys = DeserializeList(item.DecisionKeysJson),
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            SourceReference = item.SourceReference,
            ActorUserId = item.ActorUserId,
            ActorName = item.ActorName,
            ActorRoles = DeserializeList(item.ActorRolesJson),
            Reason = item.Reason,
            InputValuesJson = item.InputValuesJson,
            ResultValuesJson = item.ResultValuesJson,
            BeforeJson = item.BeforeJson,
            AfterJson = item.AfterJson,
            CorrelationId = item.CorrelationId,
            CausationId = item.CausationId,
            OccurredAtUtc = item.OccurredAtUtc,
            RecordedAtUtc = item.CreatedAt,
            IntegrityHash = item.IntegrityHash,
            IntegrityValid = string.Equals(item.IntegrityHash, ComputeHash(item), StringComparison.OrdinalIgnoreCase),
            Evidence = evidence
        };
    }

    private static string ComputeHash(ProcurementControlEvent item)
    {
        var payload = new
        {
            item.SchemaVersion,
            item.EventKey,
            item.EventType,
            item.Action,
            Result = (int)item.Result,
            item.RuleCode,
            item.RuleId,
            item.RuleVersion,
            item.DecisionKeysJson,
            item.SourceType,
            item.SourceId,
            item.SourceReference,
            item.ActorUserId,
            item.ActorName,
            item.ActorRolesJson,
            item.Reason,
            item.InputValuesJson,
            item.ResultValuesJson,
            item.BeforeJson,
            item.AfterJson,
            item.CorrelationId,
            item.CausationId,
            OccurredAtUtc = item.OccurredAtUtc.ToUniversalTime().ToString("O"),
            Evidence = item.EvidenceLinks.OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference).Select(link => new
            {
                Kind = (int)link.ReferenceKind,
                link.WorkflowEvidenceDocumentId,
                link.FileUploadRecordId,
                link.Reference,
                link.Label,
                link.RequirementKey
            }).ToList()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions))));
    }

    private static void ValidateWrite(ProcurementControlEventWriteRequest request)
    {
        Required(request.EventKey, 200, "EVENT_KEY_REQUIRED", "EventKey");
        Required(request.EventType, 100, "EVENT_TYPE_REQUIRED", "EventType");
        Required(request.Action, 100, "ACTION_REQUIRED", "Action");
        Required(request.SourceType, 100, "SOURCE_TYPE_REQUIRED", "SourceType");
        Required(request.SourceReference, 500, "SOURCE_REFERENCE_REQUIRED", "SourceReference");
        Required(request.CorrelationId, 100, "CORRELATION_REQUIRED", "CorrelationId");
        if (request.OccurredAtUtc == default)
            throw new ProcurementControlEventValidationException("OCCURRED_AT_REQUIRED", "OccurredAtUtc is required.");
        if (request.Evidence.Count > 100)
            throw new ProcurementControlEventValidationException("EVIDENCE_LIMIT", "A control event cannot link more than 100 evidence references.");
    }

    private static void Required(string? value, int maximum, string code, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ProcurementControlEventValidationException(code, $"{name} is required.");
        if (value.Trim().Length > maximum) throw new ProcurementControlEventValidationException($"{name.ToUpperInvariant()}_TOO_LONG", $"{name} cannot exceed {maximum} characters.");
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.Roles.Any(role => role is "SuperAdmin" or "TenantAdmin" or "TDC_INTERNAL_AUDIT"))
            throw new ProcurementControlEventAuthorizationException("Control-event history requires SuperAdmin, TenantAdmin, or TDC Internal Audit.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementControlEventAuthorizationException("An authenticated tenant context is required.");
    }

    private static string? SerializeOrNull(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    private static List<string> DeserializeList(string value) => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new();
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string? TruncateNullable(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), length);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
