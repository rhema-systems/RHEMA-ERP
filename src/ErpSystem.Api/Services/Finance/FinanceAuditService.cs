using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class FinanceAuditService : IFinanceAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FinanceAuditService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuditLog> RecordAsync(
        FinanceAuditEventDto auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var currentTenantId = _currentUserService.GetRequiredFinanceTenantId();
        if (auditEvent.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Finance audit event requires a valid TenantId.");
        }

        if (auditEvent.TenantId != currentTenantId)
        {
            throw new InvalidOperationException("Finance audit event tenant does not match the current tenant context.");
        }

        if (string.IsNullOrWhiteSpace(auditEvent.EventType))
        {
            throw new InvalidOperationException("Finance audit event type is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Finance audit user context is required.");
        }

        return await RecordCoreAsync(auditEvent, userId,
            string.IsNullOrWhiteSpace(_currentUserService.UserName) ? "Unknown" : _currentUserService.UserName!,
            string.IsNullOrWhiteSpace(_currentUserService.IpAddress) ? "Unknown" : _currentUserService.IpAddress!,
            _currentUserService.UserAgent,
            cancellationToken);
    }

    public async Task<AuditLog> RecordSystemAsync(
        FinanceAuditEventDto auditEvent,
        Guid technicalInitiatorUserId,
        string systemActor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (auditEvent.TenantId == Guid.Empty || technicalInitiatorUserId == Guid.Empty)
            throw new InvalidOperationException("System Finance audit requires a tenant and technical initiator.");
        if (string.IsNullOrWhiteSpace(systemActor))
            throw new InvalidOperationException("System Finance audit actor is required.");
        var initiatorExists = await _context.Users.AsNoTracking().AnyAsync(user =>
            user.Id == technicalInitiatorUserId && user.TenantId == auditEvent.TenantId,
            cancellationToken);
        if (!initiatorExists)
            throw new InvalidOperationException("The system audit technical initiator does not belong to the event tenant.");

        auditEvent.Context = new
        {
            actorType = "System",
            systemActor = systemActor.Trim(),
            technicalInitiatorUserId,
            suppliedContext = auditEvent.Context
        };
        return await RecordCoreAsync(auditEvent, technicalInitiatorUserId, systemActor.Trim(),
            "System", "ERP background worker", cancellationToken);
    }

    private async Task<AuditLog> RecordCoreAsync(
        FinanceAuditEventDto auditEvent,
        Guid userId,
        string username,
        string ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auditEvent.EventType))
            throw new InvalidOperationException("Finance audit event type is required.");

        var idempotencyKey = string.IsNullOrWhiteSpace(auditEvent.IdempotencyKey)
            ? null
            : auditEvent.IdempotencyKey.Trim();
        if (idempotencyKey?.Length > 450)
        {
            throw new InvalidOperationException("Finance audit idempotency key cannot exceed 450 characters.");
        }

        if (idempotencyKey != null)
        {
            var existing = await _context.AuditLogs
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.TenantId == auditEvent.TenantId
                    && item.IdempotencyKey == idempotencyKey
                    && !item.IsDeleted,
                    cancellationToken);
            if (existing != null)
            {
                EnsureIdempotentAuditIdentity(existing, auditEvent);
                return existing;
            }
        }

        var now = DateTime.UtcNow;
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = auditEvent.TenantId,
            UserId = userId,
            Username = username,
            Action = auditEvent.EventType.Trim(),
            Resource = ResolveResource(auditEvent),
            ResourceId = ResolveResourceId(auditEvent),
            IdempotencyKey = idempotencyKey,
            OldValues = auditEvent.BeforeValues == null
                ? null
                : JsonSerializer.Serialize(auditEvent.BeforeValues, JsonOptions),
            NewValues = JsonSerializer.Serialize(BuildPayload(auditEvent, now), JsonOptions),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = now,
            CreatedAt = now,
            CreatedBy = username,
            CreatedById = userId
        };

        _context.AuditLogs.Add(auditLog);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return auditLog;
        }
        catch (DbUpdateException) when (idempotencyKey != null)
        {
            // A concurrent retry may win the tenant/key unique constraint. Detach this
            // losing insert and return the durable winner; unrelated database failures
            // still propagate because no matching row will exist.
            _context.Entry(auditLog).State = EntityState.Detached;
            var existing = await _context.AuditLogs
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.TenantId == auditEvent.TenantId
                    && item.IdempotencyKey == idempotencyKey
                    && !item.IsDeleted,
                    cancellationToken);
            if (existing != null)
            {
                EnsureIdempotentAuditIdentity(existing, auditEvent);
                return existing;
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
        Guid tenantId,
        string resource,
        string resourceId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var currentTenantId = _currentUserService.GetRequiredFinanceTenantId();
        if (tenantId == Guid.Empty || tenantId != currentTenantId)
        {
            throw new InvalidOperationException("Finance audit trail tenant does not match the current tenant context.");
        }

        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("Audit resource is required.", nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Audit resource id is required.", nameof(resourceId));
        }

        var take = Math.Clamp(limit, 1, 500);
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                a.Resource == resource &&
                a.ResourceId == resourceId)
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private object BuildPayload(FinanceAuditEventDto auditEvent, DateTime recordedAt)
    {
        return new
        {
            financeAudit = new
            {
                auditEvent.TenantId,
                eventType = auditEvent.EventType,
                auditEvent.SourceModule,
                auditEvent.SourceDocumentType,
                auditEvent.SourceDocumentId,
                auditEvent.JournalEntryId,
                auditEvent.PostingEventId,
                auditEvent.WorkflowInstanceId,
                auditEvent.WorkflowApprovalId,
                auditEvent.Reason,
                auditEvent.Comment,
                auditEvent.IdempotencyKey,
                correlationId = ResolveCorrelationId(auditEvent),
                recordedAt
            },
            values = auditEvent.AfterValues,
            context = auditEvent.Context
        };
    }

    private string? ResolveCorrelationId(FinanceAuditEventDto auditEvent)
    {
        if (!string.IsNullOrWhiteSpace(auditEvent.CorrelationId))
        {
            return auditEvent.CorrelationId;
        }

        return _httpContextAccessor.HttpContext?.TraceIdentifier;
    }

    private static string ResolveResource(FinanceAuditEventDto auditEvent)
    {
        if (!string.IsNullOrWhiteSpace(auditEvent.Resource))
        {
            return auditEvent.Resource.Trim();
        }

        if (auditEvent.JournalEntryId.HasValue)
        {
            return "Finance.JournalEntry";
        }

        if (auditEvent.PostingEventId.HasValue)
        {
            return "Finance.PostingEvent";
        }

        return "Finance";
    }

    private static string? ResolveResourceId(FinanceAuditEventDto auditEvent)
    {
        if (!string.IsNullOrWhiteSpace(auditEvent.ResourceId))
        {
            return auditEvent.ResourceId.Trim();
        }

        return auditEvent.JournalEntryId?.ToString()
            ?? auditEvent.PostingEventId?.ToString()
            ?? auditEvent.SourceDocumentId?.ToString();
    }

    private static void EnsureIdempotentAuditIdentity(AuditLog existing, FinanceAuditEventDto requested)
    {
        if (!string.Equals(existing.Action, requested.EventType.Trim(), StringComparison.Ordinal)
            || !string.Equals(existing.Resource, ResolveResource(requested), StringComparison.Ordinal)
            || !string.Equals(existing.ResourceId, ResolveResourceId(requested), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Finance audit idempotency key is already bound to different audit evidence.");
        }
    }
}
