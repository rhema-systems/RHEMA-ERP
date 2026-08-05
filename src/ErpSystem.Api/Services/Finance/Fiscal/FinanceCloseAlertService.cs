using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Fiscal;

/// <summary>
/// Finds due Finance close work and delivers idempotent alerts through the ERP's unified
/// notification service. This service intentionally does not complete tasks, approve waivers,
/// or close periods: notification failure must never alter the underlying accounting control.
/// </summary>
public sealed class FinanceCloseAlertService
{
    // TDC operates on GMT throughout the year, so persisted UTC deadlines also read naturally as
    // local business time. The first reminder is one day before due date and management escalation
    // begins one day after a missed task or approval request.
    internal static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(24);
    internal static readonly TimeSpan EscalationAge = TimeSpan.FromHours(24);
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    // TDC's seeded authority chain places day-to-day close ownership with Finance Manager/Chief
    // Accountant and escalates unresolved items to Chief Accountant/Financial Controller. Initial
    // waiver/final-close recipients are permission-derived so tenant-specific role grants continue
    // to work without creating a parallel Finance recipient configuration.
    private static readonly string[] CloseOwnershipRoles = ["Finance Manager", "Chief Accountant"];
    private static readonly string[] HigherTierRoles = ["Chief Accountant", "Financial Controller"];
    private const string ActionUrl = "/finance/fiscal-periods";

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly ILogger<FinanceCloseAlertService> _logger;

    public FinanceCloseAlertService(
        ApplicationDbContext db,
        INotificationService notificationService,
        ILogger<FinanceCloseAlertService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Processes every currently due task, waiver-review, prepared-close, and period-reopen alert. An explicit
    /// clock value is accepted for deterministic tests and controlled operational diagnostics.
    /// </summary>
    public async Task<FinanceCloseAlertProcessingResult> ProcessDueAlertsAsync(
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = asOfUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var tasks = await _db.FinanceCloseTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(task => task.FinanceCloseCycle)
                .ThenInclude(cycle => cycle.FiscalPeriod)
            .Where(task =>
                !task.IsDeleted &&
                !task.FinanceCloseCycle.IsDeleted &&
                (task.FinanceCloseCycle.Status == FinanceCloseStatuses.InProgress ||
                 task.FinanceCloseCycle.Status == FinanceCloseStatuses.Prepared) &&
                task.Status != FinanceCloseTaskStatuses.Completed &&
                task.DueAt.HasValue &&
                task.DueAt <= now.Add(DueSoonWindow))
            .OrderBy(task => task.DueAt)
            .ToListAsync(cancellationToken);

        var waivers = await _db.FinanceCloseExceptionWaivers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(waiver => waiver.FinanceCloseCycle)
                .ThenInclude(cycle => cycle.FiscalPeriod)
            .Where(waiver =>
                !waiver.IsDeleted &&
                !waiver.FinanceCloseCycle.IsDeleted &&
                waiver.Status == FinanceCloseWaiverStatuses.Requested &&
                (waiver.FinanceCloseCycle.Status == FinanceCloseStatuses.InProgress ||
                 waiver.FinanceCloseCycle.Status == FinanceCloseStatuses.Prepared))
            .OrderBy(waiver => waiver.RequestedAt)
            .ToListAsync(cancellationToken);

        var preparedCycles = await _db.FinanceCloseCycles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(cycle => cycle.FiscalPeriod)
            .Include(cycle => cycle.Certifications)
            .Where(cycle =>
                !cycle.IsDeleted &&
                cycle.Status == FinanceCloseStatuses.Prepared &&
                cycle.PreparedAt.HasValue)
            .OrderBy(cycle => cycle.PreparedAt)
            .ToListAsync(cancellationToken);

        var reopenRequests = await _db.FinancePeriodReopenRequests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(request => request.FinanceCloseCycle)
                .ThenInclude(cycle => cycle.FiscalPeriod)
            .Where(request =>
                !request.IsDeleted &&
                request.Status == FinancePeriodReopenStatuses.PendingApproval &&
                !request.FinanceCloseCycle.IsDeleted &&
                request.FinanceCloseCycle.Status == FinanceCloseStatuses.Closed)
            .OrderBy(request => request.RequestedAt)
            .ToListAsync(cancellationToken);

        var tenantIds = tasks.Select(task => task.TenantId)
            .Concat(waivers.Select(waiver => waiver.TenantId))
            .Concat(preparedCycles.Select(cycle => cycle.TenantId))
            .Concat(reopenRequests.Select(request => request.TenantId))
            .Distinct()
            .ToArray();
        var users = await LoadRecipientDirectoryAsync(tenantIds, now, cancellationToken);

        var candidates = new List<AlertCandidate>();
        AddTaskCandidates(candidates, tasks, users, now);
        AddWaiverCandidates(candidates, waivers, users, now);
        AddPreparedCloseCandidates(candidates, preparedCycles, users, now);
        AddPeriodReopenCandidates(candidates, reopenRequests, users, now);

        // Candidate construction can converge on the same user/event (for example, a user holding
        // two qualifying roles). Grouping here keeps ordinary runs efficient; the database unique
        // key remains the authoritative guard against cross-process races.
        // Do not cap the oldest candidates: a fixed Take would let already-delivered old tasks
        // permanently starve newer tenants. Active close cycles have a deliberately small task
        // catalogue, and idempotent rows make full due-set scanning bounded in normal operation.
        var distinctCandidates = candidates
            .GroupBy(item => item.DedupeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.DueAtUtc)
            .ToArray();
        var result = new FinanceCloseAlertProcessingResult { CandidateCount = distinctCandidates.Length };
        foreach (var candidate in distinctCandidates)
        {
            var outcome = await DeliverCandidateAsync(candidate, now, cancellationToken);
            switch (outcome)
            {
                case AlertDeliveryOutcome.Delivered:
                    result.DeliveredCount++;
                    break;
                case AlertDeliveryOutcome.Failed:
                    result.FailedCount++;
                    break;
                default:
                    result.SkippedCount++;
                    break;
            }
        }

        return result;
    }

    private static void AddTaskCandidates(
        ICollection<AlertCandidate> candidates,
        IEnumerable<FinanceCloseTask> tasks,
        IReadOnlyCollection<Recipient> users,
        DateTime now)
    {
        foreach (var task in tasks)
        {
            var dueAt = task.DueAt!.Value;
            var assignedRecipient = task.AssignedToUserId.HasValue
                ? users.FirstOrDefault(user => user.UserId == task.AssignedToUserId && user.BelongsToTenant(task.TenantId, now))
                : null;

            if (dueAt > now)
            {
                if (assignedRecipient != null)
                {
                    candidates.Add(CreateTaskCandidate(
                        task,
                        assignedRecipient,
                        FinanceCloseAlertTypes.TaskDueSoon,
                        dueAt.Subtract(DueSoonWindow),
                        "Period-close task due within 24 hours",
                        $"{task.Title} for {task.FinanceCloseCycle.FiscalPeriod.PeriodName} is due at {dueAt:u}.",
                        "Normal"));
                }
                else
                {
                    foreach (var recipient in UsersInRoles(users, task.TenantId, CloseOwnershipRoles, now))
                    {
                        candidates.Add(CreateTaskCandidate(
                            task,
                            recipient,
                            FinanceCloseAlertTypes.TaskAssignmentRequired,
                            dueAt.Subtract(DueSoonWindow),
                            "Period-close task requires assignment",
                            $"{task.Title} for {task.FinanceCloseCycle.FiscalPeriod.PeriodName} is due at {dueAt:u} and has no active assignee.",
                            "High"));
                    }
                }

                continue;
            }

            if (assignedRecipient != null)
            {
                candidates.Add(CreateTaskCandidate(
                    task,
                    assignedRecipient,
                    FinanceCloseAlertTypes.TaskOverdue,
                    dueAt,
                    "Period-close task overdue",
                    $"{task.Title} for {task.FinanceCloseCycle.FiscalPeriod.PeriodName} was due at {dueAt:u} and remains incomplete.",
                    "High"));
            }
            else
            {
                foreach (var recipient in UsersInRoles(users, task.TenantId, CloseOwnershipRoles, now))
                {
                    candidates.Add(CreateTaskCandidate(
                        task,
                        recipient,
                        FinanceCloseAlertTypes.TaskAssignmentRequired,
                        dueAt,
                        "Overdue period-close task requires assignment",
                        $"{task.Title} for {task.FinanceCloseCycle.FiscalPeriod.PeriodName} is overdue and has no active assignee.",
                        "High"));
                }
            }

            if (dueAt > now.Subtract(EscalationAge))
                continue;

            foreach (var recipient in UsersInRoles(users, task.TenantId, HigherTierRoles, now))
            {
                candidates.Add(CreateTaskCandidate(
                    task,
                    recipient,
                    FinanceCloseAlertTypes.TaskOverdueEscalation,
                    dueAt.Add(EscalationAge),
                    "Period-close task escalated",
                    $"{task.Title} for {task.FinanceCloseCycle.FiscalPeriod.PeriodName} has remained incomplete for more than 24 hours after its deadline.",
                    "Critical"));
            }
        }
    }

    private static void AddWaiverCandidates(
        ICollection<AlertCandidate> candidates,
        IEnumerable<FinanceCloseExceptionWaiver> waivers,
        IReadOnlyCollection<Recipient> users,
        DateTime now)
    {
        foreach (var waiver in waivers)
        {
            // The requester is excluded even if they also hold approval permission. This mirrors
            // the domain-level maker-checker guard and avoids inviting an impossible self-review.
            foreach (var recipient in UsersWithPermission(
                         users,
                         waiver.TenantId,
                         FinancePermissions.ApproveCloseExceptionWaivers,
                         now,
                         waiver.RequestedByUserId))
            {
                candidates.Add(CreateCycleCandidate(
                    waiver.FinanceCloseCycle,
                    recipient,
                    FinanceCloseAlertTypes.WaiverReviewRequested,
                    waiver.RequestedAt,
                    "Period-close waiver awaiting review",
                    $"{waiver.CheckCode} for {waiver.FinanceCloseCycle.FiscalPeriod.PeriodName} requires an independent waiver decision.",
                    "High",
                    waiverId: waiver.Id));
            }

            if (waiver.RequestedAt > now.Subtract(EscalationAge))
                continue;

            foreach (var recipient in UsersInRoles(users, waiver.TenantId, HigherTierRoles, now, waiver.RequestedByUserId))
            {
                candidates.Add(CreateCycleCandidate(
                    waiver.FinanceCloseCycle,
                    recipient,
                    FinanceCloseAlertTypes.WaiverReviewEscalation,
                    waiver.RequestedAt.Add(EscalationAge),
                    "Period-close waiver review escalated",
                    $"{waiver.CheckCode} for {waiver.FinanceCloseCycle.FiscalPeriod.PeriodName} has awaited independent review for more than 24 hours.",
                    "Critical",
                    waiverId: waiver.Id));
            }
        }
    }

    private static void AddPreparedCloseCandidates(
        ICollection<AlertCandidate> candidates,
        IEnumerable<FinanceCloseCycle> cycles,
        IReadOnlyCollection<Recipient> users,
        DateTime now)
    {
        foreach (var cycle in cycles)
        {
            var preparedAt = cycle.PreparedAt!.Value;
            var activeCertification = cycle.Certifications
                .Where(certification => !certification.IsDeleted && !certification.IsSuperseded)
                .OrderByDescending(certification => certification.PreparedAt)
                .FirstOrDefault();
            var preparerId = activeCertification?.PreparedByUserId;

            foreach (var recipient in UsersWithPermission(
                         users,
                         cycle.TenantId,
                         FinancePermissions.CloseAccountingPeriods,
                         now,
                         preparerId))
            {
                candidates.Add(CreateCycleCandidate(
                    cycle,
                    recipient,
                    FinanceCloseAlertTypes.CloseApprovalRequested,
                    preparedAt,
                    "Fiscal period ready for independent close approval",
                    $"Close cycle {cycle.CycleNumber} for {cycle.FiscalPeriod.PeriodName} is prepared and awaits a different authorised closer.",
                    "High"));
            }

            if (preparedAt > now.Subtract(EscalationAge))
                continue;

            foreach (var recipient in UsersInRoles(users, cycle.TenantId, HigherTierRoles, now, preparerId))
            {
                candidates.Add(CreateCycleCandidate(
                    cycle,
                    recipient,
                    FinanceCloseAlertTypes.CloseApprovalEscalation,
                    preparedAt.Add(EscalationAge),
                    "Fiscal period close approval escalated",
                    $"Close cycle {cycle.CycleNumber} for {cycle.FiscalPeriod.PeriodName} has awaited independent approval for more than 24 hours.",
                    "Critical"));
            }
        }
    }

    private static void AddPeriodReopenCandidates(
        ICollection<AlertCandidate> candidates,
        IEnumerable<FinancePeriodReopenRequest> requests,
        IReadOnlyCollection<Recipient> users,
        DateTime now)
    {
        foreach (var request in requests)
        {
            // Reopen authority is intentionally narrower than ordinary close authority. Initial
            // recipients must hold the explicit higher-tier permission, and the maker is excluded
            // before notification as well as at the domain decision boundary.
            foreach (var recipient in UsersWithPermission(
                         users,
                         request.TenantId,
                         FinancePermissions.ApproveAccountingPeriodReopens,
                         now,
                         request.RequestedByUserId))
            {
                candidates.Add(CreateCycleCandidate(
                    request.FinanceCloseCycle,
                    recipient,
                    FinanceCloseAlertTypes.PeriodReopenApprovalRequested,
                    request.RequestedAt,
                    "Accounting-period reopen awaiting higher-tier approval",
                    $"{request.FinanceCloseCycle.FiscalPeriod.PeriodName} close cycle {request.FinanceCloseCycle.CycleNumber} remains closed pending an independent reopen decision.",
                    "High",
                    reopenRequestId: request.Id));
            }

            if (request.RequestedAt > now.Subtract(EscalationAge))
                continue;

            foreach (var recipient in UsersInRoles(
                         users,
                         request.TenantId,
                         HigherTierRoles,
                         now,
                         request.RequestedByUserId))
            {
                candidates.Add(CreateCycleCandidate(
                    request.FinanceCloseCycle,
                    recipient,
                    FinanceCloseAlertTypes.PeriodReopenApprovalEscalation,
                    request.RequestedAt.Add(EscalationAge),
                    "Accounting-period reopen approval escalated",
                    $"{request.FinanceCloseCycle.FiscalPeriod.PeriodName} has remained closed while its reopen request awaited higher-tier review for more than 24 hours.",
                    "Critical",
                    reopenRequestId: request.Id));
            }
        }
    }

    private async Task<AlertDeliveryOutcome> DeliverCandidateAsync(
        AlertCandidate candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var delivery = await _db.FinanceCloseAlertDeliveries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TenantId == candidate.TenantId &&
                item.DedupeKey == candidate.DedupeKey &&
                !item.IsDeleted,
                cancellationToken);

        if (delivery?.Status == FinanceCloseAlertDeliveryStatuses.Delivered)
            return AlertDeliveryOutcome.Skipped;
        if (delivery?.LastAttemptAtUtc > now.Subtract(RetryDelay))
            return AlertDeliveryOutcome.Skipped;

        if (delivery == null)
        {
            delivery = new FinanceCloseAlertDelivery
            {
                Id = Guid.NewGuid(),
                TenantId = candidate.TenantId,
                FinanceCloseCycleId = candidate.CycleId,
                FinanceCloseTaskId = candidate.TaskId,
                FinanceCloseExceptionWaiverId = candidate.WaiverId,
                FinancePeriodReopenRequestId = candidate.ReopenRequestId,
                AlertType = candidate.AlertType,
                DedupeKey = candidate.DedupeKey,
                RecipientUserId = candidate.Recipient.UserId,
                RecipientUserName = candidate.Recipient.DisplayName,
                Status = FinanceCloseAlertDeliveryStatuses.Pending,
                DueAtUtc = candidate.DueAtUtc,
                CreatedAt = now,
                CreatedBy = "Finance close alert monitor"
            };
            _db.FinanceCloseAlertDeliveries.Add(delivery);
        }

        delivery.Status = FinanceCloseAlertDeliveryStatuses.Pending;
        delivery.LastAttemptAtUtc = now;
        delivery.AttemptCount++;
        delivery.LastError = null;
        delivery.UpdatedAt = now;
        delivery.UpdatedBy = "Finance close alert monitor";

        try
        {
            // Commit the claim before calling the notification service. The unique DedupeKey then
            // serializes competing application nodes without holding a database transaction open
            // across notification persistence.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _db.Entry(delivery).State = EntityState.Detached;
            var competingClaimExists = await _db.FinanceCloseAlertDeliveries
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(item =>
                    item.TenantId == candidate.TenantId &&
                    item.DedupeKey == candidate.DedupeKey &&
                    !item.IsDeleted,
                    cancellationToken);
            if (!competingClaimExists)
            {
                // Only a proven duplicate claim is safe to suppress. Constraint, connectivity,
                // or schema failures must reach the host logger rather than masquerading as an
                // ordinary idempotency skip.
                throw;
            }
            _logger.LogDebug(exception,
                "Finance close alert {DedupeKey} was claimed by another processor.",
                candidate.DedupeKey);
            return AlertDeliveryOutcome.Skipped;
        }

        try
        {
            var notification = await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = candidate.Recipient.UserId,
                    Type = $"FinanceClose.{candidate.AlertType}",
                    Title = candidate.Title,
                    Message = candidate.Message,
                    Priority = candidate.Priority,
                    EntityType = "FinanceCloseCycle",
                    EntityId = candidate.CycleId,
                    ActionUrl = ActionUrl,
                    Metadata = new Dictionary<string, object>
                    {
                        ["financeCloseCycleId"] = candidate.CycleId,
                        ["fiscalPeriodId"] = candidate.PeriodId,
                        ["alertType"] = candidate.AlertType,
                        ["dueAtUtc"] = candidate.DueAtUtc.ToString("O"),
                        ["financeCloseTaskId"] = candidate.TaskId?.ToString() ?? string.Empty,
                        ["financeCloseExceptionWaiverId"] = candidate.WaiverId?.ToString() ?? string.Empty,
                        ["financePeriodReopenRequestId"] = candidate.ReopenRequestId?.ToString() ?? string.Empty
                    }
                },
                candidate.Recipient.UserId,
                candidate.TenantId);

            delivery.NotificationId = notification.Id;
            delivery.Status = FinanceCloseAlertDeliveryStatuses.Delivered;
            delivery.DeliveredAtUtc = now;
            delivery.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return AlertDeliveryOutcome.Delivered;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Leave the durable claim Pending. A later run will retry it after RetryDelay rather
            // than creating a second alert immediately during application shutdown.
            throw;
        }
        catch (Exception exception)
        {
            delivery.Status = FinanceCloseAlertDeliveryStatuses.Failed;
            delivery.LastError = Truncate(exception.GetBaseException().Message, 2000);
            delivery.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(exception,
                "Finance close alert {AlertType} could not be delivered to user {RecipientUserId}.",
                candidate.AlertType,
                candidate.Recipient.UserId);
            return AlertDeliveryOutcome.Failed;
        }
    }

    private async Task<IReadOnlyCollection<Recipient>> LoadRecipientDirectoryAsync(
        IReadOnlyCollection<Guid> tenantIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
            return Array.Empty<Recipient>();

        var users = await _db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(user => user.UserTenants)
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(user =>
                user.IsActive &&
                (tenantIds.Contains(user.TenantId) ||
                 user.UserTenants.Any(userTenant =>
                     tenantIds.Contains(userTenant.TenantId) &&
                     !userTenant.IsDeleted &&
                     userTenant.Status == UserTenantStatus.Active &&
                     (!userTenant.ExpiresAt.HasValue || userTenant.ExpiresAt > now))))
            .ToListAsync(cancellationToken);

        return users.Select(user => new Recipient(
                user.Id,
                string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? user.Id.ToString() : user.FullName,
                user.TenantId,
                user.UserTenants
                    .Where(userTenant =>
                        !userTenant.IsDeleted &&
                        userTenant.Status == UserTenantStatus.Active &&
                        (!userTenant.ExpiresAt.HasValue || userTenant.ExpiresAt > now))
                    .Select(userTenant => userTenant.TenantId)
                    .ToHashSet(),
                user.UserRoles
                    .Select(userRole => userRole.Role.Name)
                    .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                    .Select(roleName => roleName!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                user.UserRoles
                    .SelectMany(userRole => userRole.Role.RolePermissions)
                    .Select(rolePermission => rolePermission.Permission.Name)
                    .Where(permission => !string.IsNullOrWhiteSpace(permission))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static IEnumerable<Recipient> UsersWithPermission(
        IEnumerable<Recipient> users,
        Guid tenantId,
        string permission,
        DateTime now,
        Guid? excludedUserId = null) => users.Where(user =>
            user.UserId != excludedUserId &&
            user.BelongsToTenant(tenantId, now) &&
            user.Permissions.Contains(permission));

    private static IEnumerable<Recipient> UsersInRoles(
        IEnumerable<Recipient> users,
        Guid tenantId,
        IEnumerable<string> roleNames,
        DateTime now,
        Guid? excludedUserId = null)
    {
        var roles = roleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return users.Where(user =>
            user.UserId != excludedUserId &&
            user.BelongsToTenant(tenantId, now) &&
            user.Roles.Overlaps(roles));
    }

    private static AlertCandidate CreateTaskCandidate(
        FinanceCloseTask task,
        Recipient recipient,
        string alertType,
        DateTime dueAtUtc,
        string title,
        string message,
        string priority) => new(
            task.TenantId,
            task.FinanceCloseCycleId,
            task.FinanceCloseCycle.FiscalPeriodId,
            task.Id,
            null,
            null,
            alertType,
            recipient,
            dueAtUtc,
            title,
            message,
            priority);

    private static AlertCandidate CreateCycleCandidate(
        FinanceCloseCycle cycle,
        Recipient recipient,
        string alertType,
        DateTime dueAtUtc,
        string title,
        string message,
        string priority,
        Guid? waiverId = null,
        Guid? reopenRequestId = null) => new(
            cycle.TenantId,
            cycle.Id,
            cycle.FiscalPeriodId,
            null,
            waiverId,
            reopenRequestId,
            alertType,
            recipient,
            dueAtUtc,
            title,
            message,
            priority);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record Recipient(
        Guid UserId,
        string DisplayName,
        Guid PrimaryTenantId,
        HashSet<Guid> AccessibleTenantIds,
        HashSet<string> Roles,
        HashSet<string> Permissions)
    {
        public bool BelongsToTenant(Guid tenantId, DateTime _) =>
            PrimaryTenantId == tenantId || AccessibleTenantIds.Contains(tenantId);
    }

    private sealed record AlertCandidate(
        Guid TenantId,
        Guid CycleId,
        Guid PeriodId,
        Guid? TaskId,
        Guid? WaiverId,
        Guid? ReopenRequestId,
        string AlertType,
        Recipient Recipient,
        DateTime DueAtUtc,
        string Title,
        string Message,
        string Priority)
    {
        public string DedupeKey =>
            $"{AlertType}:{(TaskId ?? WaiverId ?? ReopenRequestId ?? CycleId):N}:{Recipient.UserId:N}";
    }

    private enum AlertDeliveryOutcome
    {
        Skipped,
        Delivered,
        Failed
    }
}

/// <summary>
/// Small operational result returned for tests, logs, and future monitoring endpoints.
/// </summary>
public sealed class FinanceCloseAlertProcessingResult
{
    public int CandidateCount { get; set; }
    public int DeliveredCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
}
