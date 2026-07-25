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

        var now = DateTime.UtcNow;
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = auditEvent.TenantId,
            UserId = userId,
            Username = string.IsNullOrWhiteSpace(_currentUserService.UserName)
                ? "Unknown"
                : _currentUserService.UserName!,
            Action = auditEvent.EventType.Trim(),
            Resource = ResolveResource(auditEvent),
            ResourceId = ResolveResourceId(auditEvent),
            OldValues = auditEvent.BeforeValues == null
                ? null
                : JsonSerializer.Serialize(auditEvent.BeforeValues, JsonOptions),
            NewValues = JsonSerializer.Serialize(BuildPayload(auditEvent, now), JsonOptions),
            IpAddress = string.IsNullOrWhiteSpace(_currentUserService.IpAddress)
                ? "Unknown"
                : _currentUserService.IpAddress!,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = now,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = userId
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(cancellationToken);
        return auditLog;
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
}
