using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Audit;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Audit;

public sealed class PlatformAuditLogRecordProvider(IUnitOfWork unitOfWork) : IAuditRecordProvider
{
    public const string Key = "platform-audit-log";
    public string StoreKey => Key;
    public string StoreName => "Platform audit log";

    public async Task<AuditRecordDescriptor?> FindAsync(
        Guid tenantId,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        var item = await unitOfWork.Repository<AuditLog>().GetQueryable(value =>
                value.TenantId == tenantId && value.Id == recordId && !value.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? null
            : new AuditRecordDescriptor(
                StoreKey,
                StoreName,
                item.Id,
                item.TenantId,
                EnsureUtc(item.Timestamp),
                $"{item.Resource}:{item.ResourceId ?? item.Id.ToString("N")}",
                new
                {
                    item.Id,
                    item.TenantId,
                    item.UserId,
                    item.Username,
                    item.Action,
                    item.Resource,
                    item.ResourceId,
                    item.OldValues,
                    item.NewValues,
                    item.IpAddress,
                    item.UserAgent,
                    item.Timestamp
                });
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class ProcurementControlEventAuditRecordProvider(IUnitOfWork unitOfWork) : IAuditRecordProvider
{
    public const string Key = "procurement-inventory-control-event";
    public string StoreKey => Key;
    public string StoreName => "Procurement and inventory control event";

    public async Task<AuditRecordDescriptor?> FindAsync(
        Guid tenantId,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        var item = await unitOfWork.Repository<ProcurementControlEvent>().GetQueryable(value =>
                value.TenantId == tenantId && value.Id == recordId && !value.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? null
            : new AuditRecordDescriptor(
                StoreKey,
                StoreName,
                item.Id,
                item.TenantId,
                EnsureUtc(item.OccurredAtUtc),
                item.SourceReference,
                new
                {
                    item.Id,
                    item.TenantId,
                    item.EventKey,
                    item.SchemaVersion,
                    item.EventType,
                    item.Action,
                    item.Operation,
                    item.Result,
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
                    item.OccurredAtUtc,
                    item.IntegrityHash
                },
                item.IntegrityHash);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class AuditGovernanceService : IAuditGovernanceService
{
    public const int MinimumRetentionDays = 2555;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IReadOnlyDictionary<string, IAuditRecordProvider> _providers;

    public AuditGovernanceService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IEnumerable<IAuditRecordProvider> providers)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _providers = providers.ToDictionary(value => value.StoreKey, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AuditRecordGovernanceDto> GetAsync(
        string storeKey,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        var descriptor = await GetDescriptorAsync(storeKey, recordId, cancellationToken);
        var events = await GetEventsAsync(descriptor.StoreKey, recordId, cancellationToken);
        var retentionDays = await GetRetentionDaysAsync(cancellationToken);
        return Map(descriptor, events, retentionDays);
    }

    public Task<AuditRecordGovernanceDto> PlaceLegalHoldAsync(
        string storeKey,
        Guid recordId,
        AuditLifecycleCommandDto request,
        CancellationToken cancellationToken = default)
        => AppendAsync(storeKey, recordId, AuditLifecycleActionType.LegalHoldPlaced, request, cancellationToken);

    public Task<AuditRecordGovernanceDto> ReleaseLegalHoldAsync(
        string storeKey,
        Guid recordId,
        AuditLifecycleCommandDto request,
        CancellationToken cancellationToken = default)
        => AppendAsync(storeKey, recordId, AuditLifecycleActionType.LegalHoldReleased, request, cancellationToken);

    public Task<AuditRecordGovernanceDto> ArchiveAsync(
        string storeKey,
        Guid recordId,
        AuditLifecycleCommandDto request,
        CancellationToken cancellationToken = default)
        => AppendAsync(storeKey, recordId, AuditLifecycleActionType.Archived, request, cancellationToken);

    public Task<AuditRecordGovernanceDto> RestoreAsync(
        string storeKey,
        Guid recordId,
        AuditLifecycleCommandDto request,
        CancellationToken cancellationToken = default)
        => AppendAsync(storeKey, recordId, AuditLifecycleActionType.Restored, request, cancellationToken);

    private async Task<AuditRecordGovernanceDto> AppendAsync(
        string storeKey,
        Guid recordId,
        AuditLifecycleActionType action,
        AuditLifecycleCommandDto request,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        Validate(request);

        // SQL Server's retrying execution strategy must own any transaction it
        // may need to replay. When a caller already owns a transaction, join it
        // and leave commit/rollback responsibility with that caller.
        if (!_unitOfWork.HasActiveTransaction)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(
                () => AppendWithinTransactionAsync(
                    storeKey,
                    recordId,
                    action,
                    request,
                    cancellationToken),
                cancellationToken);
        }

        return await AppendWithinTransactionAsync(
            storeKey,
            recordId,
            action,
            request,
            cancellationToken);
    }

    private async Task<AuditRecordGovernanceDto> AppendWithinTransactionAsync(
        string storeKey,
        Guid recordId,
        AuditLifecycleActionType action,
        AuditLifecycleCommandDto request,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                $"audit-governance:{_currentUser.TenantId:N}:{NormalizeStoreKey(storeKey)}:{recordId:N}",
                cancellationToken);

            var repository = _unitOfWork.Repository<AuditRecordLifecycleEvent>();
            var replay = await repository.GetQueryable(value =>
                    value.TenantId == _currentUser.TenantId && value.RequestKey == request.RequestKey.Trim())
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.StoreKey, storeKey.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    replay.RecordId != recordId || replay.Action != action)
                    throw new AuditGovernanceConflictException("The request key is already bound to a different audit-governance action.");
                if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                return await GetAsync(replay.StoreKey, replay.RecordId, cancellationToken);
            }

            var descriptor = await GetDescriptorAsync(storeKey, recordId, cancellationToken);
            var history = await GetEventsAsync(descriptor.StoreKey, recordId, cancellationToken);
            var current = DeriveState(history);
            EnsureTransition(action, current.IsLegalHold, current.IsArchived);
            var retentionDays = await GetRetentionDaysAsync(cancellationToken);
            var sequence = history.Count + 1;
            var now = DateTime.UtcNow;
            var snapshot = JsonSerializer.Serialize(descriptor.Snapshot, JsonOptions);
            var entity = new AuditRecordLifecycleEvent
            {
                TenantId = _currentUser.TenantId,
                StoreKey = descriptor.StoreKey,
                RecordId = descriptor.RecordId,
                SequenceNumber = sequence,
                Action = action,
                Reason = request.Reason.Trim(),
                RequestKey = request.RequestKey.Trim(),
                CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                    ? Guid.NewGuid().ToString("N")
                    : request.CorrelationId.Trim(),
                ArchiveReference = action == AuditLifecycleActionType.Archived
                    ? $"audit-archive://{_currentUser.TenantId:N}/{descriptor.StoreKey}/{recordId:N}/{sequence}"
                    : current.ArchiveReference,
                SourceOccurredAtUtc = descriptor.OccurredAtUtc,
                RetainUntilUtc = descriptor.OccurredAtUtc.AddDays(retentionDays),
                ActorUserId = _currentUser.UserId,
                ActorName = ActorName(),
                ActorRolesJson = JsonSerializer.Serialize(
                    _currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value),
                    JsonOptions),
                RecordSnapshotJson = snapshot,
                PreviousIntegrityHash = history.LastOrDefault()?.IntegrityHash,
                CreatedAt = now,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            };
            entity.IntegrityHash = ComputeHash(entity);
            await repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
            return await GetAsync(descriptor.StoreKey, recordId, cancellationToken);
        }
        catch
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<AuditRecordDescriptor> GetDescriptorAsync(
        string storeKey,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        if (recordId == Guid.Empty)
            throw new AuditGovernanceValidationException("A valid audit record identifier is required.");
        var normalized = NormalizeStoreKey(storeKey);
        if (!_providers.TryGetValue(normalized, out var provider))
            throw new AuditGovernanceValidationException($"Audit store '{normalized}' is not registered.");
        return await provider.FindAsync(_currentUser.TenantId, recordId, cancellationToken)
            ?? throw new AuditGovernanceNotFoundException("The audit record was not found in the current tenant.");
    }

    private async Task<List<AuditRecordLifecycleEvent>> GetEventsAsync(
        string storeKey,
        Guid recordId,
        CancellationToken cancellationToken)
        => await _unitOfWork.Repository<AuditRecordLifecycleEvent>().GetQueryable(value =>
                value.TenantId == _currentUser.TenantId && value.StoreKey == storeKey &&
                value.RecordId == recordId && !value.IsDeleted)
            .AsNoTracking()
            .OrderBy(value => value.SequenceNumber)
            .ToListAsync(cancellationToken);

    private async Task<int> GetRetentionDaysAsync(CancellationToken cancellationToken)
    {
        var configured = await _unitOfWork.Repository<DataRetentionPolicy>().GetQueryable(value =>
                value.TenantId == _currentUser.TenantId && !value.IsDeleted)
            .AsNoTracking()
            .Select(value => (int?)value.AuditLogRetentionDays)
            .SingleOrDefaultAsync(cancellationToken);
        return Math.Clamp(configured ?? MinimumRetentionDays, MinimumRetentionDays, 36500);
    }

    private static AuditRecordGovernanceDto Map(
        AuditRecordDescriptor descriptor,
        IReadOnlyList<AuditRecordLifecycleEvent> events,
        int retentionDays)
    {
        var state = DeriveState(events);
        return new AuditRecordGovernanceDto
        {
            StoreKey = descriptor.StoreKey,
            StoreName = descriptor.StoreName,
            RecordId = descriptor.RecordId,
            Reference = descriptor.Reference,
            SourceOccurredAtUtc = descriptor.OccurredAtUtc,
            RetentionDays = retentionDays,
            RetainUntilUtc = descriptor.OccurredAtUtc.AddDays(retentionDays),
            IsImmutable = true,
            IsLegalHold = state.IsLegalHold,
            IsArchived = state.IsArchived,
            ArchiveReference = state.ArchiveReference,
            Actions = events.Select(value => new AuditLifecycleActionDto
            {
                Id = value.Id,
                SequenceNumber = value.SequenceNumber,
                Action = value.Action,
                Reason = value.Reason,
                CorrelationId = value.CorrelationId,
                ArchiveReference = value.ArchiveReference,
                OccurredAtUtc = value.CreatedAt,
                RetainUntilUtc = value.RetainUntilUtc,
                ActorUserId = value.ActorUserId,
                ActorName = value.ActorName,
                IntegrityHash = value.IntegrityHash,
                IntegrityValid = string.Equals(value.IntegrityHash, ComputeHash(value), StringComparison.OrdinalIgnoreCase)
            }).ToList()
        };
    }

    private static (bool IsLegalHold, bool IsArchived, string? ArchiveReference) DeriveState(
        IReadOnlyList<AuditRecordLifecycleEvent> events)
    {
        var legalHold = false;
        var archived = false;
        string? archiveReference = null;
        foreach (var item in events)
        {
            switch (item.Action)
            {
                case AuditLifecycleActionType.LegalHoldPlaced: legalHold = true; break;
                case AuditLifecycleActionType.LegalHoldReleased: legalHold = false; break;
                case AuditLifecycleActionType.Archived:
                    archived = true;
                    archiveReference = item.ArchiveReference;
                    break;
                case AuditLifecycleActionType.Restored: archived = false; break;
            }
        }
        return (legalHold, archived, archiveReference);
    }

    private static void EnsureTransition(AuditLifecycleActionType action, bool isLegalHold, bool isArchived)
    {
        if (action == AuditLifecycleActionType.LegalHoldPlaced && isLegalHold)
            throw new AuditGovernanceConflictException("The audit record is already under legal hold.");
        if (action == AuditLifecycleActionType.LegalHoldReleased && !isLegalHold)
            throw new AuditGovernanceConflictException("The audit record is not under legal hold.");
        if (action == AuditLifecycleActionType.Archived && isArchived)
            throw new AuditGovernanceConflictException("The audit record is already archived.");
        if (action == AuditLifecycleActionType.Restored && !isArchived)
            throw new AuditGovernanceConflictException("Only an archived audit record can be restored.");
    }

    private static void Validate(AuditLifecycleCommandDto request)
    {
        if (request is null) throw new AuditGovernanceValidationException("An audit-governance command is required.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
            throw new AuditGovernanceValidationException("A reason of at least 5 characters is required.");
        if (request.Reason.Trim().Length > 1000)
            throw new AuditGovernanceValidationException("The reason cannot exceed 1,000 characters.");
        if (string.IsNullOrWhiteSpace(request.RequestKey) || request.RequestKey.Trim().Length > 200)
            throw new AuditGovernanceValidationException("A request key of at most 200 characters is required.");
        if (!string.IsNullOrWhiteSpace(request.CorrelationId) && request.CorrelationId.Trim().Length > 100)
            throw new AuditGovernanceValidationException("The correlation identifier cannot exceed 100 characters.");
    }

    private void EnsureTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new AuditGovernanceValidationException("An authenticated tenant context is required.");
    }

    private string ActorName()
        => string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName;

    private static string NormalizeStoreKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AuditGovernanceValidationException("An audit store key is required.");
        return value.Trim().ToLowerInvariant();
    }

    internal static string ComputeHash(AuditRecordLifecycleEvent value)
    {
        var payload = JsonSerializer.Serialize(new
        {
            value.TenantId,
            value.StoreKey,
            value.RecordId,
            value.SequenceNumber,
            Action = (int)value.Action,
            value.Reason,
            value.RequestKey,
            value.CorrelationId,
            value.ArchiveReference,
            SourceOccurredAtUtc = value.SourceOccurredAtUtc.ToUniversalTime().ToString("O"),
            RetainUntilUtc = value.RetainUntilUtc.ToUniversalTime().ToString("O"),
            value.ActorUserId,
            value.ActorName,
            value.ActorRolesJson,
            value.RecordSnapshotJson,
            value.PreviousIntegrityHash
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

public sealed class ProcurementInventoryAuditEventCoverageContributor : IAuditEventCoverageContributor
{
    private static readonly AuditEventCoverageDefinitionDto[] Definitions =
    [
        Definition(AuditOperationKind.Create, "Lifecycle creation", "CreateSourcingCase", "Created"),
        Definition(AuditOperationKind.Update, "Controlled amendments and master-data changes", "Updated", "Amended"),
        Definition(AuditOperationKind.Approve, "Workflow and independent control decisions", "Approved", "InvoiceMatchExceptionApproved"),
        Definition(AuditOperationKind.Reject, "Workflow and independent control decisions", "Rejected", "InvoiceMatchExceptionRejected"),
        Definition(AuditOperationKind.Override, "Approved exception and emergency use", "OverrideSourcingMethod", "NegativeStockOverrideConsumed"),
        Definition(AuditOperationKind.Post, "Inventory and Finance posting", "Post", "PostStockAdjustment", "PaymentPostingAuthorized"),
        Definition(AuditOperationKind.Reverse, "Controlled reversal", "Reverse", "ReverseShipment", "ReverseStockAdjustment"),
        Definition(AuditOperationKind.Dispatch, "Tender, PO, inventory issue and transfer dispatch", "Dispatch", "Issue"),
        Definition(AuditOperationKind.Receive, "Receipt, acknowledgement and transfer receipt", "Receive", "Acknowledge")
    ];

    public IReadOnlyList<AuditEventCoverageDefinitionDto> GetDefinitions() => Definitions;

    private static AuditEventCoverageDefinitionDto Definition(
        AuditOperationKind operation,
        string control,
        params string[] actions)
        => new("Procurement and Inventory", operation, control, actions);
}

public sealed class AuditEventCoverageService(IEnumerable<IAuditEventCoverageContributor> contributors)
    : IAuditEventCoverageService
{
    private static readonly AuditOperationKind[] Required =
    [
        AuditOperationKind.Create,
        AuditOperationKind.Update,
        AuditOperationKind.Approve,
        AuditOperationKind.Reject,
        AuditOperationKind.Override,
        AuditOperationKind.Post,
        AuditOperationKind.Reverse,
        AuditOperationKind.Dispatch,
        AuditOperationKind.Receive
    ];

    public AuditEventCoverageReportDto GetReport()
    {
        var definitions = contributors.SelectMany(value => value.GetDefinitions())
            .OrderBy(value => value.Module).ThenBy(value => value.Operation).ToList();
        var valid = definitions.Where(value => value.EmittedActions.Count > 0 &&
                value.EmittedActions.All(action => AuditOperationClassifier.Classify(action) == value.Operation))
            .Select(value => value.Operation)
            .Distinct()
            .ToHashSet();
        return new AuditEventCoverageReportDto
        {
            VerifiedAtUtc = DateTime.UtcNow,
            RequiredOperations = Required,
            Definitions = definitions,
            MissingOperations = Required.Where(value => !valid.Contains(value)).ToList()
        };
    }
}
