using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AR;

/// <summary>
/// Connects the existing collection-activity aggregate to Finance's posted AR
/// settlement projection. This service intentionally does not calculate debt from
/// mutable invoice paid fields, so receipts, credit notes, and reversals flow into
/// the work queue through the same auditable source used by AR aging reports.
/// </summary>
public sealed class ArCollectionFollowUpService : IArCollectionFollowUpService
{
    private const string AuditResource = "Finance.ArCollectionTask";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly ILogger<ArCollectionFollowUpService> _logger;

    public ArCollectionFollowUpService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceAuditService audit,
        INotificationService notifications,
        ILogger<ArCollectionFollowUpService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notifications = notifications;
        _logger = logger;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<ArCollectionWorkQueueDto> GetWorkQueueAsync(
        DateTime? asOfDate = null,
        int minimumDaysOverdue = 1,
        string? status = null,
        Guid? assignedToId = null,
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var effectiveDate = (asOfDate ?? DateTime.UtcNow).Date;
        var safeMinimumDays = Math.Clamp(minimumDaysOverdue, 1, 3650);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var rows = BuildExposureQuery(effectiveDate, safeMinimumDays);

        if (assignedToId.HasValue)
        {
            rows = rows.Where(item => item.AssignedToId == assignedToId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            rows = normalizedStatus.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)
                ? rows.Where(item => item.TaskId == null)
                : rows.Where(item => item.TaskStatus == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(item =>
                item.CustomerName.Contains(term) ||
                item.CustomerCode.Contains(term) ||
                item.InvoiceNumber.Contains(term));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            // Oldest obligations appear first so officers address the greatest
            // aging exposure before newer invoices in the same filtered queue.
            .OrderBy(item => item.DueDate)
            .ThenByDescending(item => item.OutstandingAmount)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new ArCollectionWorkQueueDto
        {
            AsOfDate = effectiveDate,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            Items = pageRows.Select(item => Map(item, effectiveDate)).ToList()
        };
    }

    public async Task<ArCollectionSummaryDto> GetSummaryAsync(
        DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveDate = (asOfDate ?? DateTime.UtcNow).Date;
        var rows = await BuildExposureQuery(effectiveDate, minimumDaysOverdue: 1)
            .ToListAsync(cancellationToken);

        var nativeTotals = rows
            .GroupBy(item => NormalizeCurrency(item.CurrencyCode))
            .OrderBy(group => group.Key)
            .Select(group => new ArCollectionCurrencyTotalDto
            {
                CurrencyCode = group.Key,
                OutstandingAmount = RoundMoney(group.Sum(item => item.OutstandingAmount)),
                PromisedAmount = RoundMoney(group.Sum(item => Math.Min(item.PromisedAmount, item.OutstandingAmount)))
            })
            .ToList();
        var functionalSummary = BuildFunctionalSummary(rows);

        return new ArCollectionSummaryDto
        {
            AsOfDate = effectiveDate,
            OverdueExposureCount = rows.Count,
            UnassignedExposureCount = rows.Count(item => item.TaskId == null || !item.AssignedToId.HasValue),
            OverdueFollowUpCount = rows.Count(item =>
                item.FollowUpDate.HasValue &&
                item.FollowUpDate.Value.Date < effectiveDate &&
                !IsTerminal(item.TaskStatus)),
            PromiseToPayCount = rows.Count(item => item.TaskStatus == CollectionActivityValues.PromiseToPayStatus),
            BreachedPromiseCount = rows.Count(item =>
                item.PromisedAmount > 0 &&
                item.PromisedPayDate.HasValue &&
                item.PromisedPayDate.Value.Date < effectiveDate &&
                item.OutstandingAmount > 0),
            NativeCurrencyTotals = nativeTotals,
            FunctionalCurrencyCode = functionalSummary.CurrencyCode,
            FunctionalOutstandingTotal = functionalSummary.OutstandingTotal,
            FunctionalPromisedTotal = functionalSummary.PromisedTotal,
            FunctionalTotalUnavailableReason = functionalSummary.UnavailableReason
        };
    }

    public async Task<IReadOnlyList<ArCollectionAssigneeDto>> GetAssigneesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        return await _db.Users.AsNoTracking()
            .Where(user => user.IsActive &&
                (user.TenantId == tenantId || user.UserTenants.Any(link =>
                    link.TenantId == tenantId &&
                    !link.IsDeleted &&
                    link.Status == UserTenantStatus.Active &&
                    (!link.ExpiresAt.HasValue || link.ExpiresAt > now))))
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Select(user => new ArCollectionAssigneeDto
            {
                UserId = user.Id,
                DisplayName = (user.FirstName + " " + user.LastName).Trim() == string.Empty
                    ? user.UserName ?? "Finance user"
                    : (user.FirstName + " " + user.LastName).Trim()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GenerateArCollectionTasksResultDto> GenerateTasksAsync(
        GenerateArCollectionTasksDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = TenantId;
        var actorId = CurrentUserIdRequired();
        var assignedToId = request.AssignedToId ?? actorId;
        await EnsureAssigneeAsync(assignedToId, cancellationToken);
        var effectiveDate = (request.AsOfDate ?? DateTime.UtcNow).Date;
        var dueCutoff = effectiveDate.AddDays(-Math.Clamp(request.MinimumDaysOverdue, 1, 3650));
        var result = new GenerateArCollectionTasksResultDto();
        var createdTasks = new List<CollectionActivity>();
        var autoResolvedTasks = new List<(CollectionActivity Task, object Before)>();
        var reactivatedTasks = new List<(CollectionActivity Task, object Before)>();

        var exposures = await _db.SubledgerSettlementBalances.AsNoTracking()
            .Where(balance =>
                balance.TenantId == tenantId &&
                !balance.IsDeleted &&
                balance.SourceModule == SubledgerSettlementModules.AccountsReceivable &&
                balance.OutstandingAmount > 0 &&
                balance.DueDate.HasValue &&
                balance.DueDate.Value.Date <= dueCutoff)
            .OrderBy(balance => balance.DueDate)
            .ToListAsync(cancellationToken);
        result.EligibleCount = exposures.Count;

        var existingByInvoice = await _db.CollectionActivities
            .Where(activity =>
                activity.TenantId == tenantId &&
                !activity.IsDeleted &&
                activity.CollectionContext == CollectionActivityValues.FinanceArContext &&
                activity.IsPrimaryTask &&
                activity.InvoiceId.HasValue)
            .ToDictionaryAsync(activity => activity.InvoiceId!.Value, cancellationToken);
        var exposurePartnerIds = exposures.Select(exposure => exposure.CounterpartyId).Distinct().ToList();
        var validPartnerIds = (await _db.BusinessPartners.AsNoTracking()
            .Where(partner =>
                partner.TenantId == tenantId &&
                !partner.IsDeleted &&
                exposurePartnerIds.Contains(partner.Id) &&
                partner.Roles.Any(role => role.TenantId == tenantId && !role.IsDeleted &&
                    role.RoleType == BusinessPartnerRoleType.Customer))
            .Select(partner => partner.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var exposure in exposures)
        {
            if (existingByInvoice.TryGetValue(exposure.SourceDocumentId, out var existing))
            {
                result.ExistingCount++;
                if (IsTerminal(existing.CollectionStatus))
                {
                    var before = Snapshot(existing);
                    // A receipt or credit-note reversal can legitimately restore a
                    // balance after this task was closed. Reuse the durable primary
                    // task so its earlier recovery history is retained, and make the
                    // reinstated exposure actionable again from posted AR evidence.
                    existing.CollectionStatus = CollectionActivityValues.PendingStatus;
                    existing.CompletedAt = null;
                    existing.CompletedById = null;
                    existing.Outcome = "Reactivated";
                    existing.Notes = "Automatically reactivated because posted AR settlement evidence restored an outstanding balance.";
                    existing.OutstandingAmount = exposure.OutstandingAmount;
                    existing.FollowUpDate = effectiveDate.AddDays(Math.Clamp(request.FollowUpInDays, 0, 365));
                    existing.AssignedToId ??= assignedToId;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = ActorName();
                    existing.LastModifiedById = actorId;
                    _db.CollectionActivities.Add(BuildHistory(
                        existing,
                        "AutomaticReactivation",
                        "Collection task automatically reactivated",
                        existing.Notes,
                        actorId));
                    reactivatedTasks.Add((existing, before));
                    result.ReactivatedCount++;
                }
                if (existing.OutstandingAmount != exposure.OutstandingAmount)
                {
                    // The activity amount is only a convenient snapshot. The work
                    // queue always displays the live settlement projection value.
                    existing.OutstandingAmount = exposure.OutstandingAmount;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = ActorName();
                    existing.LastModifiedById = actorId;
                    result.RefreshedCount++;
                }
                continue;
            }

            if (!validPartnerIds.Contains(exposure.CounterpartyId))
            {
                result.SkippedUnresolvedPartnerCount++;
                continue;
            }

            var task = BuildPrimaryTask(
                exposure,
                assignedToId,
                effectiveDate.AddDays(Math.Clamp(request.FollowUpInDays, 0, 365)),
                Math.Clamp(request.Priority, 1, 10),
                notes: "Generated from posted AR aging evidence.",
                actorId);
            _db.CollectionActivities.Add(task);
            existingByInvoice[exposure.SourceDocumentId] = task;
            createdTasks.Add(task);
            result.CreatedCount++;
        }

        // A task remains as immutable collection history after settlement, but it
        // must no longer look actionable once the posted projection reaches zero.
        var activeTasks = existingByInvoice.Values
            .Where(task => !IsTerminal(task.CollectionStatus))
            .ToList();
        if (activeTasks.Count > 0)
        {
            var invoiceIds = activeTasks.Select(task => task.InvoiceId!.Value).ToList();
            var liveInvoiceIds = exposures.Select(item => item.SourceDocumentId).ToHashSet();
            var additionallyLiveIds = await _db.SubledgerSettlementBalances.AsNoTracking()
                .Where(balance =>
                    balance.TenantId == tenantId &&
                    !balance.IsDeleted &&
                    balance.SourceModule == SubledgerSettlementModules.AccountsReceivable &&
                    invoiceIds.Contains(balance.SourceDocumentId) &&
                    balance.OutstandingAmount > 0)
                .Select(balance => balance.SourceDocumentId)
                .ToListAsync(cancellationToken);
            liveInvoiceIds.UnionWith(additionallyLiveIds);

            foreach (var task in activeTasks.Where(task => !liveInvoiceIds.Contains(task.InvoiceId!.Value)))
            {
                var before = Snapshot(task);
                CompleteTask(task, actorId, "Settled", "Automatically resolved from posted settlement evidence.");
                // The primary task holds the current state while this child activity
                // preserves an officer-readable explanation of the automatic change.
                // The Finance audit entry recorded below provides the separate,
                // tamper-evident before/after evidence required for control review.
                _db.CollectionActivities.Add(BuildHistory(
                    task,
                    "AutomaticResolution",
                    "Collection task automatically resolved",
                    task.Notes,
                    actorId));
                autoResolvedTasks.Add((task, before));
                result.AutoResolvedCount++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Record each newly created task separately so an auditor can navigate from
        // a specific invoice/task to its creation event. The aggregate generation
        // event that follows remains useful as evidence of the operator's batch run.
        foreach (var task in createdTasks)
        {
            await RecordAuditAsync(
                FinanceAuditEvents.ArCollectionTaskCreated,
                task.InvoiceId,
                before: null,
                after: Snapshot(task),
                reason: "Generated from posted AR aging evidence.",
                cancellationToken,
                task.Id);
        }

        foreach (var resolved in autoResolvedTasks)
        {
            await RecordAuditAsync(
                FinanceAuditEvents.ArCollectionTaskAutoResolved,
                resolved.Task.InvoiceId,
                resolved.Before,
                Snapshot(resolved.Task),
                resolved.Task.Notes,
                cancellationToken,
                resolved.Task.Id);
        }

        foreach (var reactivated in reactivatedTasks)
        {
            await RecordAuditAsync(
                FinanceAuditEvents.ArCollectionTaskReactivated,
                reactivated.Task.InvoiceId,
                reactivated.Before,
                Snapshot(reactivated.Task),
                reactivated.Task.Notes,
                cancellationToken,
                reactivated.Task.Id);
        }

        await RecordAuditAsync(
            FinanceAuditEvents.ArCollectionTasksGenerated,
            sourceDocumentId: null,
            before: null,
            after: result,
            reason: $"AR collection generation as of {effectiveDate:yyyy-MM-dd}",
            cancellationToken,
            resourceId: $"generation:{effectiveDate:yyyyMMdd}");

        foreach (var task in createdTasks)
        {
            await NotifyAssigneeAsync(task, "AR collection task assigned", cancellationToken);
        }
        return result;
    }

    public async Task<ArCollectionWorkItemDto> CreateTaskAsync(
        CreateArCollectionTaskDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = TenantId;
        var actorId = CurrentUserIdRequired();
        var assignedToId = request.AssignedToId ?? actorId;
        await EnsureAssigneeAsync(assignedToId, cancellationToken);
        var exposure = await _db.SubledgerSettlementBalances.AsNoTracking()
            .SingleOrDefaultAsync(balance =>
                balance.Id == request.SettlementBalanceId &&
                balance.TenantId == tenantId &&
                !balance.IsDeleted &&
                balance.SourceModule == SubledgerSettlementModules.AccountsReceivable,
                cancellationToken)
            ?? throw new KeyNotFoundException("The AR settlement exposure was not found for this tenant.");

        if (exposure.OutstandingAmount <= 0 || !exposure.DueDate.HasValue || exposure.DueDate.Value.Date >= DateTime.UtcNow.Date)
            throw new InvalidOperationException("A collection task can be created only for a posted overdue AR exposure with an outstanding balance.");

        var existingId = await _db.CollectionActivities.AsNoTracking()
            .Where(activity =>
                activity.TenantId == tenantId &&
                !activity.IsDeleted &&
                activity.CollectionContext == CollectionActivityValues.FinanceArContext &&
                activity.IsPrimaryTask &&
                activity.InvoiceId == exposure.SourceDocumentId)
            .Select(activity => (Guid?)activity.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
            return await GetWorkItemByTaskAsync(existingId.Value, cancellationToken);

        var task = BuildPrimaryTask(
            exposure,
            assignedToId,
            (request.FollowUpDate ?? DateTime.UtcNow.AddDays(1)).Date,
            Math.Clamp(request.Priority, 1, 10),
            request.Notes,
            actorId);
        _db.CollectionActivities.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            FinanceAuditEvents.ArCollectionTaskCreated,
            exposure.SourceDocumentId,
            null,
            Snapshot(task),
            request.Notes,
            cancellationToken,
            task.Id);
        await NotifyAssigneeAsync(task, "AR collection task assigned", cancellationToken);
        return await GetWorkItemByTaskAsync(task.Id, cancellationToken);
    }

    public async Task<ArCollectionWorkItemDto> UpdateTaskAsync(
        Guid taskId,
        UpdateArCollectionTaskDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await LoadTaskAsync(taskId, cancellationToken);
        var actorId = CurrentUserIdRequired();
        ValidateStatus(request.CollectionStatus);
        await EnsureAssigneeAsync(request.AssignedToId, cancellationToken);
        ApplyRowVersion(task, request.RowVersion);

        var liveOutstanding = await GetLiveOutstandingAsync(task.InvoiceId!.Value, cancellationToken);
        if (request.CollectionStatus.Equals(CollectionActivityValues.PromiseToPayStatus, StringComparison.OrdinalIgnoreCase))
        {
            if (request.PromisedAmount <= 0 || !request.PromisedPayDate.HasValue)
                throw new InvalidOperationException("Promise-to-pay requires both a positive promised amount and a promised payment date.");
            if (request.PromisedAmount > liveOutstanding)
                throw new InvalidOperationException("Promised amount cannot exceed the currently outstanding posted balance.");
        }
        // An escalation is a controlled hand-off rather than a completed task, but it still
        // requires the officer to record why the normal collection path could not continue.
        // Keeping that distinction explicit avoids falsely removing escalated debt from the
        // active queue while preserving the evidence TDC management needs to review the hand-off.
        if (RequiresOutcome(request.CollectionStatus) && string.IsNullOrWhiteSpace(request.Outcome) && string.IsNullOrWhiteSpace(request.Notes))
            throw new InvalidOperationException("Resolving, writing off, or escalating a collection task requires an outcome or explanatory note.");

        var before = Snapshot(task);
        task.CollectionStatus = NormalizeStatus(request.CollectionStatus);
        task.AssignedToId = request.AssignedToId;
        task.FollowUpDate = request.FollowUpDate?.Date;
        task.Priority = Math.Clamp(request.Priority, 1, 10);
        task.PromisedAmount = request.PromisedAmount;
        task.PromisedPayDate = request.PromisedPayDate?.Date;
        task.Outcome = Clean(request.Outcome, 50);
        task.Notes = Clean(request.Notes, 2000);
        task.OutstandingAmount = liveOutstanding;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = ActorName();
        task.LastModifiedById = actorId;
        if (IsTerminal(task.CollectionStatus))
        {
            task.CompletedAt = DateTime.UtcNow;
            task.CompletedById = actorId;
        }
        else
        {
            task.CompletedAt = null;
            task.CompletedById = null;
        }

        // Preserve a readable operational timeline in the existing activity table;
        // the Finance audit record below separately provides tamper-evident values.
        _db.CollectionActivities.Add(BuildHistory(
            task,
            "StatusChange",
            $"Collection task updated to {task.CollectionStatus}",
            task.Notes,
            actorId));
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            FinanceAuditEvents.ArCollectionTaskUpdated,
            task.InvoiceId,
            before,
            Snapshot(task),
            task.Notes,
            cancellationToken,
            task.Id);
        await NotifyAssigneeAsync(task, "AR collection task updated", cancellationToken);
        return await GetWorkItemByTaskAsync(task.Id, cancellationToken);
    }

    public async Task<ArCollectionHistoryItemDto> RecordReminderAsync(
        Guid taskId,
        RecordArCollectionReminderDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await LoadTaskAsync(taskId, cancellationToken);
        var actorId = CurrentUserIdRequired();
        ApplyRowVersion(task, request.RowVersion);
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("A reminder record requires the message or contact note that was prepared or dispatched.");
        var channel = NormalizeReminderChannel(request.Channel);
        var recipient = Clean(request.Recipient, 250) ?? await ResolveRecipientAsync(task, channel, cancellationToken);
        if (!channel.Equals("Internal", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("An external reminder requires a recorded recipient address, number, or contact reference.");

        var outcome = request.ConfirmedDispatched ? "Dispatched" : "Prepared";
        var history = BuildHistory(
            task,
            CollectionActivityValues.ReminderType,
            $"{channel} reminder {outcome.ToLowerInvariant()}",
            request.Message.Trim(),
            actorId);
        history.ReminderChannel = channel;
        history.ReminderRecipient = recipient;
        history.Outcome = outcome;
        history.ActivityDate = DateTime.UtcNow;
        task.FollowUpDate = request.NextFollowUpDate?.Date ?? task.FollowUpDate;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = ActorName();
        task.LastModifiedById = actorId;
        _db.CollectionActivities.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            FinanceAuditEvents.ArCollectionReminderRecorded,
            task.InvoiceId,
            null,
            new { history.Id, channel, recipient, outcome, request.NextFollowUpDate },
            request.Message,
            cancellationToken,
            task.Id);

        if (channel.Equals("Internal", StringComparison.OrdinalIgnoreCase) && task.AssignedToId.HasValue)
            await NotifyAssigneeAsync(task, "AR follow-up reminder", cancellationToken, request.Message);

        return MapHistory(history);
    }

    public async Task<IReadOnlyList<ArCollectionHistoryItemDto>> GetHistoryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await LoadTaskAsync(taskId, cancellationToken);
        return await _db.CollectionActivities.AsNoTracking()
            .Where(activity =>
                activity.TenantId == TenantId &&
                !activity.IsDeleted &&
                (activity.Id == task.Id || activity.ParentActivityId == task.Id))
            .OrderByDescending(activity => activity.ActivityDate)
            .Select(activity => new ArCollectionHistoryItemDto
            {
                Id = activity.Id,
                ActivityType = activity.ActivityType,
                Subject = activity.Subject,
                CollectionStatus = activity.CollectionStatus,
                Outcome = activity.Outcome,
                ReminderChannel = activity.ReminderChannel,
                ReminderRecipient = activity.ReminderRecipient,
                Description = activity.Description,
                Notes = activity.Notes,
                ActivityDate = activity.ActivityDate,
                ActorName = activity.CreatedBy
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ExposureRow> BuildExposureQuery(DateTime effectiveDate, int minimumDaysOverdue)
    {
        var tenantId = TenantId;
        var dueCutoff = effectiveDate.AddDays(-minimumDaysOverdue);
        var tasks = _db.CollectionActivities.AsNoTracking()
            .Where(activity =>
                activity.TenantId == tenantId &&
                !activity.IsDeleted &&
                activity.CollectionContext == CollectionActivityValues.FinanceArContext &&
                activity.IsPrimaryTask);

        return
            from balance in _db.SubledgerSettlementBalances.AsNoTracking()
            where balance.TenantId == tenantId &&
                  !balance.IsDeleted &&
                  balance.SourceModule == SubledgerSettlementModules.AccountsReceivable &&
                  balance.OutstandingAmount > 0 &&
                  balance.DueDate.HasValue &&
                  balance.DueDate.Value.Date <= dueCutoff
            join partnerCandidate in _db.BusinessPartners.AsNoTracking().Where(item => item.TenantId == tenantId && !item.IsDeleted)
                on balance.CounterpartyId equals partnerCandidate.Id into partnerJoin
            from partner in partnerJoin.DefaultIfEmpty()
            join taskCandidate in tasks on balance.SourceDocumentId equals taskCandidate.InvoiceId into taskJoin
            from task in taskJoin.DefaultIfEmpty()
            join assigneeCandidate in _db.Users.AsNoTracking() on task.AssignedToId equals assigneeCandidate.Id into assigneeJoin
            from assignee in assigneeJoin.DefaultIfEmpty()
            select new ExposureRow
            {
                SettlementBalanceId = balance.Id,
                BusinessPartnerId = balance.CounterpartyId,
                CustomerCode = partner == null ? string.Empty : partner.PartnerCode,
                CustomerName = partner == null ? "Unresolved business partner" : partner.PartnerName,
                CustomerEmail = partner == null ? null : partner.PrimaryEmail,
                CustomerPhone = partner == null ? null : partner.PrimaryPhone,
                CustomerAddress = partner == null ? null : partner.PhysicalAddress,
                CustomerCity = partner == null ? null : partner.PhysicalCity,
                CustomerState = partner == null ? null : partner.PhysicalState,
                CustomerCountry = partner == null ? null : partner.PhysicalCountry,
                CustomerPostalCode = partner == null ? null : partner.PhysicalPostalCode,
                IsPartnerResolved = partner != null,
                InvoiceId = balance.SourceDocumentId,
                InvoiceNumber = balance.SourceDocumentNumber,
                TransactionDate = balance.TransactionDate,
                DueDate = balance.DueDate!.Value,
                CurrencyCode = balance.DocumentCurrencyCode,
                FunctionalCurrencyCode = balance.FunctionalCurrencyCode,
                OriginalDocumentAmount = balance.OriginalDocumentAmount,
                OriginalFunctionalAmount = balance.OriginalFunctionalAmount,
                OutstandingAmount = balance.OutstandingAmount,
                TaskId = task == null ? null : task.Id,
                TaskReference = task == null ? string.Empty : task.ReferenceNumber,
                TaskStatus = task == null ? "Unassigned" : task.CollectionStatus,
                Priority = task == null ? 0 : task.Priority,
                AssignedToId = task == null ? null : task.AssignedToId,
                AssignedToName = assignee == null
                    ? null
                    : ((assignee.FirstName + " " + assignee.LastName).Trim() == string.Empty
                        ? assignee.UserName
                        : (assignee.FirstName + " " + assignee.LastName).Trim()),
                FollowUpDate = task == null ? null : task.FollowUpDate,
                PromisedAmount = task == null ? 0 : task.PromisedAmount,
                PromisedPayDate = task == null ? null : task.PromisedPayDate,
                Outcome = task == null ? null : task.Outcome,
                Notes = task == null ? null : task.Notes,
                LastActivityAt = task == null
                    ? null
                    : _db.CollectionActivities
                        .Where(history => history.TenantId == tenantId && !history.IsDeleted && history.ParentActivityId == task.Id)
                        .Max(history => (DateTime?)history.ActivityDate) ?? task.ActivityDate,
                RowVersion = task == null ? null : task.RowVersion
            };
    }

    private async Task<ArCollectionWorkItemDto> GetWorkItemByTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(taskId, cancellationToken);
        var balance = await _db.SubledgerSettlementBalances.AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                item.SourceModule == SubledgerSettlementModules.AccountsReceivable &&
                item.SourceDocumentId == task.InvoiceId)
            .OrderByDescending(item => item.LastRebuiltAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The task's posted AR settlement evidence is unavailable. Rebuild the settlement read model before continuing.");
        var partner = await _db.BusinessPartners.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == task.BusinessPartnerId && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
        var assignee = task.AssignedToId.HasValue
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == task.AssignedToId.Value, cancellationToken)
            : null;
        var lastActivity = await _db.CollectionActivities.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.ParentActivityId == task.Id)
            .MaxAsync(item => (DateTime?)item.ActivityDate, cancellationToken) ?? task.ActivityDate;
        return Map(new ExposureRow
        {
            SettlementBalanceId = balance.Id,
            BusinessPartnerId = balance.CounterpartyId,
            CustomerCode = partner?.PartnerCode ?? string.Empty,
            CustomerName = partner?.PartnerName ?? "Unresolved business partner",
            CustomerEmail = partner?.PrimaryEmail,
            CustomerPhone = partner?.PrimaryPhone,
            CustomerAddress = partner?.PhysicalAddress,
            CustomerCity = partner?.PhysicalCity,
            CustomerState = partner?.PhysicalState,
            CustomerCountry = partner?.PhysicalCountry,
            CustomerPostalCode = partner?.PhysicalPostalCode,
            IsPartnerResolved = partner != null,
            InvoiceId = balance.SourceDocumentId,
            InvoiceNumber = balance.SourceDocumentNumber,
            TransactionDate = balance.TransactionDate,
            DueDate = balance.DueDate ?? balance.TransactionDate,
            CurrencyCode = balance.DocumentCurrencyCode,
            FunctionalCurrencyCode = balance.FunctionalCurrencyCode,
            OriginalDocumentAmount = balance.OriginalDocumentAmount,
            OriginalFunctionalAmount = balance.OriginalFunctionalAmount,
            OutstandingAmount = balance.OutstandingAmount,
            TaskId = task.Id,
            TaskReference = task.ReferenceNumber,
            TaskStatus = task.CollectionStatus,
            Priority = task.Priority,
            AssignedToId = task.AssignedToId,
            AssignedToName = assignee == null ? null : DisplayName(assignee),
            FollowUpDate = task.FollowUpDate,
            PromisedAmount = task.PromisedAmount,
            PromisedPayDate = task.PromisedPayDate,
            Outcome = task.Outcome,
            Notes = task.Notes,
            LastActivityAt = lastActivity,
            RowVersion = task.RowVersion
        }, DateTime.UtcNow.Date);
    }

    private CollectionActivity BuildPrimaryTask(
        SubledgerSettlementBalance exposure,
        Guid assignedToId,
        DateTime followUpDate,
        int priority,
        string? notes,
        Guid actorId)
    {
        var now = DateTime.UtcNow;
        return new CollectionActivity
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ReferenceNumber = $"ARCOL-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..27].ToUpperInvariant(),
            CollectionContext = CollectionActivityValues.FinanceArContext,
            IsPrimaryTask = true,
            BusinessPartnerId = exposure.CounterpartyId,
            InvoiceId = exposure.SourceDocumentId,
            Subject = $"Follow up overdue invoice {exposure.SourceDocumentNumber}",
            ActivityType = CollectionActivityValues.FollowUpTaskType,
            Description = "Finance AR collection task generated from the posted settlement read model.",
            ActivityDate = now,
            FollowUpDate = followUpDate.Date,
            CollectionStatus = CollectionActivityValues.PendingStatus,
            OutstandingAmount = exposure.OutstandingAmount,
            AssignedToId = assignedToId,
            Priority = priority,
            Notes = Clean(notes, 2000),
            CreatedAt = now,
            CreatedBy = ActorName(),
            CreatedById = actorId
        };
    }

    private CollectionActivity BuildHistory(
        CollectionActivity task,
        string activityType,
        string subject,
        string? description,
        Guid actorId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ReferenceNumber = task.ReferenceNumber,
            CollectionContext = CollectionActivityValues.FinanceArContext,
            IsPrimaryTask = false,
            ParentActivityId = task.Id,
            BusinessPartnerId = task.BusinessPartnerId,
            InvoiceId = task.InvoiceId,
            Subject = Clean(subject, 200) ?? activityType,
            ActivityType = activityType,
            Description = Clean(description, 2000),
            ActivityDate = DateTime.UtcNow,
            CollectionStatus = task.CollectionStatus,
            OutstandingAmount = task.OutstandingAmount,
            PromisedAmount = task.PromisedAmount,
            PromisedPayDate = task.PromisedPayDate,
            AssignedToId = task.AssignedToId,
            Priority = task.Priority,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = ActorName(),
            CreatedById = actorId
        };

    private async Task<CollectionActivity> LoadTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        await _db.CollectionActivities
            .SingleOrDefaultAsync(item =>
                item.Id == taskId &&
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                item.CollectionContext == CollectionActivityValues.FinanceArContext &&
                item.IsPrimaryTask,
                cancellationToken)
        ?? throw new KeyNotFoundException("The Finance AR collection task was not found for this tenant.");

    private async Task<decimal> GetLiveOutstandingAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        await _db.SubledgerSettlementBalances.AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                item.SourceModule == SubledgerSettlementModules.AccountsReceivable &&
                item.SourceDocumentId == invoiceId)
            .Select(item => (decimal?)item.OutstandingAmount)
            .SingleOrDefaultAsync(cancellationToken) ?? 0m;

    private async Task EnsureAssigneeAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return;
        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        var valid = await _db.Users.AsNoTracking().AnyAsync(user =>
            user.Id == userId.Value &&
            user.IsActive &&
            (user.TenantId == tenantId || user.UserTenants.Any(link =>
                link.TenantId == tenantId &&
                !link.IsDeleted &&
                link.Status == UserTenantStatus.Active &&
                (!link.ExpiresAt.HasValue || link.ExpiresAt > now))),
            cancellationToken);
        if (!valid)
            throw new InvalidOperationException("The selected collection officer does not have active access to this tenant.");
    }

    private async Task NotifyAssigneeAsync(
        CollectionActivity task,
        string title,
        CancellationToken cancellationToken,
        string? message = null)
    {
        if (!task.AssignedToId.HasValue)
            return;
        try
        {
            await _notifications.CreateNotificationAsync(new CreateNotificationDto
            {
                RecipientId = task.AssignedToId.Value,
                Type = "Finance.AR.CollectionFollowUp",
                Title = title,
                Message = message ?? $"{task.Subject}. Follow-up: {task.FollowUpDate:dd MMM yyyy}.",
                Priority = task.Priority >= 8 ? "High" : "Normal",
                EntityType = nameof(CollectionActivity),
                EntityId = task.Id,
                ActionUrl = "/finance/ar/collections",
                Metadata = new Dictionary<string, object>
                {
                    ["collectionTaskId"] = task.Id,
                    ["invoiceId"] = task.InvoiceId?.ToString() ?? string.Empty,
                    ["status"] = task.CollectionStatus
                }
            }, CurrentUserIdRequired(), TenantId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Task creation is the accounting-control action; a transient notification
            // problem must be visible in logs but must not erase the durable assignment.
            _logger.LogWarning(exception, "Could not deliver AR collection notification for task {TaskId}.", task.Id);
        }
    }

    private async Task RecordAuditAsync(
        string eventType,
        Guid? sourceDocumentId,
        object? before,
        object? after,
        string? reason,
        CancellationToken cancellationToken,
        Guid? taskId = null,
        string? resourceId = null)
    {
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = "AR",
            SourceDocumentType = "CustomerInvoice",
            SourceDocumentId = sourceDocumentId,
            BeforeValues = before,
            AfterValues = after,
            Reason = reason,
            Resource = AuditResource,
            ResourceId = resourceId ?? (taskId ?? sourceDocumentId)?.ToString()
        }, cancellationToken);
    }

    private static ArCollectionWorkItemDto Map(ExposureRow row, DateTime effectiveDate)
    {
        var daysOverdue = Math.Max(0, (effectiveDate - row.DueDate.Date).Days);
        return new ArCollectionWorkItemDto
        {
            SettlementBalanceId = row.SettlementBalanceId,
            BusinessPartnerId = row.BusinessPartnerId,
            CustomerCode = row.CustomerCode,
            CustomerName = row.CustomerName,
            CustomerEmail = row.CustomerEmail,
            CustomerPhone = row.CustomerPhone,
            CustomerAddress = row.CustomerAddress,
            CustomerCity = row.CustomerCity,
            CustomerState = row.CustomerState,
            CustomerCountry = row.CustomerCountry,
            CustomerPostalCode = row.CustomerPostalCode,
            IsPartnerResolved = row.IsPartnerResolved,
            PartnerResolutionMessage = row.IsPartnerResolved
                ? null
                : "The business partner linked to this posted invoice could not be resolved in the current tenant.",
            InvoiceId = row.InvoiceId,
            InvoiceNumber = row.InvoiceNumber,
            TransactionDate = row.TransactionDate,
            DueDate = row.DueDate,
            CurrencyCode = row.CurrencyCode,
            OutstandingAmount = row.OutstandingAmount,
            DaysOverdue = daysOverdue,
            AgingBucket = daysOverdue <= 30 ? "1-30" : daysOverdue <= 60 ? "31-60" : daysOverdue <= 90 ? "61-90" : "90+",
            TaskId = row.TaskId,
            TaskReference = row.TaskReference,
            TaskStatus = row.TaskStatus,
            Priority = row.Priority,
            AssignedToId = row.AssignedToId,
            AssignedToName = row.AssignedToName,
            FollowUpDate = row.FollowUpDate,
            PromisedAmount = row.PromisedAmount,
            PromisedPayDate = row.PromisedPayDate,
            Outcome = row.Outcome,
            Notes = row.Notes,
            LastActivityAt = row.LastActivityAt,
            IsFollowUpOverdue = row.FollowUpDate.HasValue && row.FollowUpDate.Value.Date < effectiveDate && !IsTerminal(row.TaskStatus),
            IsPromiseBreached = row.PromisedAmount > 0 && row.PromisedPayDate.HasValue && row.PromisedPayDate.Value.Date < effectiveDate && row.OutstandingAmount > 0,
            RowVersion = row.RowVersion is { Length: > 0 } ? Convert.ToBase64String(row.RowVersion) : null
        };
    }

    private static ArCollectionHistoryItemDto MapHistory(CollectionActivity activity) => new()
    {
        Id = activity.Id,
        ActivityType = activity.ActivityType,
        Subject = activity.Subject,
        CollectionStatus = activity.CollectionStatus,
        Outcome = activity.Outcome,
        ReminderChannel = activity.ReminderChannel,
        ReminderRecipient = activity.ReminderRecipient,
        Description = activity.Description,
        Notes = activity.Notes,
        ActivityDate = activity.ActivityDate,
        ActorName = activity.CreatedBy
    };

    private static object Snapshot(CollectionActivity task) => new
    {
        task.Id,
        task.ReferenceNumber,
        task.InvoiceId,
        task.BusinessPartnerId,
        task.CollectionStatus,
        task.AssignedToId,
        task.Priority,
        task.FollowUpDate,
        task.OutstandingAmount,
        task.PromisedAmount,
        task.PromisedPayDate,
        task.Outcome,
        task.CompletedAt,
        task.CompletedById
    };

    private void ApplyRowVersion(CollectionActivity task, string rowVersion)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The collection task version is invalid. Refresh the work queue and retry.");
        }
        if (bytes.Length == 0)
            throw new InvalidOperationException("The collection task version is required. Refresh the work queue and retry.");
        _db.Entry(task).Property(item => item.RowVersion).OriginalValue = bytes;
    }

    private static void ValidateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            !CollectionActivityValues.TaskStatuses.Any(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Unsupported collection status '{status}'.");
    }

    private static string NormalizeStatus(string status) =>
        CollectionActivityValues.TaskStatuses.First(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string NormalizeReminderChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new InvalidOperationException("A reminder channel is required.");

        var normalized = CollectionActivityValues.ReminderChannels
            .FirstOrDefault(item => item.Equals(channel.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized ?? throw new InvalidOperationException($"Unsupported reminder channel '{channel}'.");
    }

    private static bool IsTerminal(string? status) =>
        status != null && (status.Equals(CollectionActivityValues.ResolvedStatus, StringComparison.OrdinalIgnoreCase) ||
                           status.Equals(CollectionActivityValues.WrittenOffStatus, StringComparison.OrdinalIgnoreCase));

    private static bool RequiresOutcome(string? status) =>
        IsTerminal(status) ||
        status?.Equals(CollectionActivityValues.EscalatedStatus, StringComparison.OrdinalIgnoreCase) == true;

    private static void CompleteTask(CollectionActivity task, Guid actorId, string outcome, string notes)
    {
        task.CollectionStatus = CollectionActivityValues.ResolvedStatus;
        task.Outcome = outcome;
        task.Notes = notes;
        task.OutstandingAmount = 0m;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedById = actorId;
        task.UpdatedAt = DateTime.UtcNow;
        task.LastModifiedById = actorId;
    }

    private async Task<string?> ResolveRecipientAsync(
        CollectionActivity task,
        string channel,
        CancellationToken cancellationToken)
    {
        if (channel.Equals("Internal", StringComparison.OrdinalIgnoreCase))
            return task.AssignedToId?.ToString();

        var partner = await _db.BusinessPartners.AsNoTracking()
            .Where(item => item.Id == task.BusinessPartnerId && item.TenantId == TenantId && !item.IsDeleted)
            .Select(item => new
            {
                item.PrimaryEmail,
                item.PrimaryPhone,
                item.PhysicalAddress
            })
            .SingleOrDefaultAsync(cancellationToken);

        return channel switch
        {
            "Email" => partner?.PrimaryEmail,
            "SMS" or "Phone" => partner?.PrimaryPhone,
            "Letter" or "Visit" => partner?.PhysicalAddress,
            _ => null
        };
    }

    private static FunctionalSummary BuildFunctionalSummary(IReadOnlyList<ExposureRow> rows)
    {
        if (rows.Count == 0)
            return new FunctionalSummary(null, null, null, "No overdue exposure is available to total.");

        var functionalCurrencies = rows
            .Select(item => NormalizeCurrency(item.FunctionalCurrencyCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (functionalCurrencies.Count != 1 || functionalCurrencies[0] == "UNSPECIFIED")
            return new FunctionalSummary(null, null, null, "The exposures do not share one authoritative functional currency.");

        decimal outstandingTotal = 0m;
        decimal promisedTotal = 0m;
        foreach (var row in rows)
        {
            if (row.OriginalDocumentAmount <= 0m || row.OriginalFunctionalAmount <= 0m)
            {
                return new FunctionalSummary(
                    functionalCurrencies[0],
                    null,
                    null,
                    $"Historical functional-currency evidence is unavailable for invoice {row.InvoiceNumber}.");
            }

            var historicalPostingRatio = row.OriginalFunctionalAmount / row.OriginalDocumentAmount;
            outstandingTotal += row.OutstandingAmount * historicalPostingRatio;
            promisedTotal += Math.Min(row.PromisedAmount, row.OutstandingAmount) * historicalPostingRatio;
        }

        return new FunctionalSummary(
            functionalCurrencies[0],
            RoundMoney(outstandingTotal),
            RoundMoney(promisedTotal),
            null);
    }

    private static string NormalizeCurrency(string? currencyCode) =>
        string.IsNullOrWhiteSpace(currencyCode) ? "UNSPECIFIED" : currencyCode.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private Guid CurrentUserIdRequired() =>
        Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("An authenticated Finance user is required.");

    private string ActorName() => string.IsNullOrWhiteSpace(_currentUser.UserName) ? "Finance user" : _currentUser.UserName!;
    private static string DisplayName(ApplicationUser user) =>
        string.IsNullOrWhiteSpace((user.FirstName + " " + user.LastName).Trim())
            ? user.UserName ?? "Finance user"
            : (user.FirstName + " " + user.LastName).Trim();

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private sealed class ExposureRow
    {
        public Guid SettlementBalanceId { get; init; }
        public Guid BusinessPartnerId { get; init; }
        public string CustomerCode { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string? CustomerEmail { get; init; }
        public string? CustomerPhone { get; init; }
        public string? CustomerAddress { get; init; }
        public string? CustomerCity { get; init; }
        public string? CustomerState { get; init; }
        public string? CustomerCountry { get; init; }
        public string? CustomerPostalCode { get; init; }
        public bool IsPartnerResolved { get; init; }
        public Guid InvoiceId { get; init; }
        public string InvoiceNumber { get; init; } = string.Empty;
        public DateTime TransactionDate { get; init; }
        public DateTime DueDate { get; init; }
        public string CurrencyCode { get; init; } = "GHS";
        public string FunctionalCurrencyCode { get; init; } = "GHS";
        public decimal OriginalDocumentAmount { get; init; }
        public decimal OriginalFunctionalAmount { get; init; }
        public decimal OutstandingAmount { get; init; }
        public Guid? TaskId { get; init; }
        public string TaskReference { get; init; } = string.Empty;
        public string TaskStatus { get; init; } = "Unassigned";
        public int Priority { get; init; }
        public Guid? AssignedToId { get; init; }
        public string? AssignedToName { get; init; }
        public DateTime? FollowUpDate { get; init; }
        public decimal PromisedAmount { get; init; }
        public DateTime? PromisedPayDate { get; init; }
        public string? Outcome { get; init; }
        public string? Notes { get; init; }
        public DateTime? LastActivityAt { get; init; }
        public byte[]? RowVersion { get; init; }
    }

    private sealed record FunctionalSummary(
        string? CurrencyCode,
        decimal? OutstandingTotal,
        decimal? PromisedTotal,
        string? UnavailableReason);
}
