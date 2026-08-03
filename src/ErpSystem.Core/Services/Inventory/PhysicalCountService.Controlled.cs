using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class PhysicalCountService
{
    private const string StoresManagerRole = "TDC_STORES_MANAGER";
    private const string FinanceReviewerRole = "TDC_FINANCE_REVIEWER";
    private static readonly JsonSerializerOptions CountJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> RecordRecountAsync(
        Guid countId,
        Guid userId,
        RecordPhysicalCountRecountRequest request)
    {
        EnsureActor(userId);
        var count = await LoadControlledCountAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        await EnsureAccessAsync(count, "procurement.inventory.count");
        EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Reload and retry the recount.");
        if (await HasCountActionAsync(count.Id, PhysicalCountActionType.RecountRecorded, request.IdempotencyKey))
            return true;
        if (count.Status != "RecountRequired")
            throw new InvalidOperationException("A recount can only be recorded when the count requires recount.");
        if (count.CountedById == userId || count.InitiatedById == userId)
            throw new InvalidOperationException("The original counter or initiator cannot perform the independent recount.");

        var item = count.Items.SingleOrDefault(x => x.Id == request.PhysicalCountItemId && !x.IsDeleted)
            ?? throw new ArgumentException($"Physical count item {request.PhysicalCountItemId} not found in count {count.CountNumber}.");
        if (!item.RequiresRecount)
            throw new InvalidOperationException("The selected count line does not require recount.");
        EnsureRowVersion(item.RowVersion, request.ItemRowVersion, "The count line changed. Reload and retry the recount.");

        item.RecountedQuantity = request.RecountedQuantity;
        item.RecountedAtUtc = DateTime.UtcNow;
        item.RecountedById = userId;
        item.InvestigationNotes = Required(request.InvestigationNotes, "Investigation notes are required.", 2000);
        item.CountedQuantity = request.RecountedQuantity;
        item.VarianceQuantity = item.CountedQuantity - item.SystemQuantity;
        item.VarianceValue = item.VarianceQuantity * item.UnitCost;
        item.CountAttempts = Math.Max(item.CountAttempts, 2);
        await _countItemRepository.UpdateAsync(item);

        var outstanding = count.Items.Any(x => x.RequiresRecount && x.Id != item.Id && !x.RecountedQuantity.HasValue);
        if (!outstanding)
        {
            count.Status = "PendingStoresApproval";
            count.InvestigationSummary = string.Join(" | ", count.Items
                .Where(x => x.RequiresRecount && !string.IsNullOrWhiteSpace(x.InvestigationNotes))
                .Select(x => $"{x.ItemCode}: {x.InvestigationNotes}"));
        }

        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, PhysicalCountActionType.RecountRecorded, userId,
            request.IdempotencyKey, request.InvestigationNotes,
            new { item.Id, item.FirstCountQuantity, item.RecountedQuantity, item.VarianceQuantity }, "IndependentCounter",
            request.CorrelationId);
        await _unitOfWork.SaveChangesAsync();
        await UpdateCountSummaryAsync(count.Id);

        if (!outstanding)
            await EnsureStockAdjustmentSubmittedAsync(count.Id, userId, request.IdempotencyKey, request.Comment);
        await RecordCountControlEventAsync(count, "Recount", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, request.CorrelationId, request.InvestigationNotes);
        return true;
    }

    public async Task<bool> ApproveStoresAsync(
        Guid countId,
        Guid userId,
        PhysicalCountDecisionRequest request)
    {
        EnsureActor(userId);
        EnsureRole(StoresManagerRole, "Only the assigned Stores Manager may decide the Stores variance stage.");
        var count = await LoadControlledCountAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        await EnsureAccessAsync(count, "procurement.inventory.adjust.approve");
        EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Reload and retry the Stores decision.");
        var action = request.Approved ? PhysicalCountActionType.StoresApproved : PhysicalCountActionType.Rejected;
        if (await HasCountActionAsync(count.Id, action, request.IdempotencyKey)) return true;
        if (count.Status != "PendingStoresApproval")
            throw new InvalidOperationException("The count is not awaiting Stores approval.");
        EnsureIndependentActor(count, userId, includeFinance: false, includeAudit: false);

        StockAdjustmentDetailDto? adjustment = null;
        if (count.TotalVarianceQuantity != 0)
        {
            if (!count.StockAdjustmentId.HasValue)
                throw new InvalidOperationException("The controlled stock adjustment is missing. The independent counter must retry submission.");
            adjustment = await _stockAdjustmentService.GetByIdAsync(count.StockAdjustmentId.Value)
                ?? throw new InvalidOperationException("The linked controlled stock adjustment was not found.");
            adjustment = await _stockAdjustmentService.DecideAsync(adjustment.Id, userId, new DecideStockAdjustmentRequest
            {
                Approved = request.Approved,
                Comment = request.Approved ? request.Comment : Required(request.Reason ?? request.Comment, "A rejection reason is required.", 2000),
                IdempotencyKey = $"pc-stores:{request.IdempotencyKey}"[..Math.Min(100, $"pc-stores:{request.IdempotencyKey}".Length)],
                RowVersion = adjustment.RowVersion
            });
            if (request.Approved && adjustment.Status != "Approved")
                throw new InvalidOperationException("The shared stock-adjustment workflow did not reach an approved outcome.");
            if (!request.Approved && adjustment.Status != "Rejected")
                throw new InvalidOperationException("The shared stock-adjustment workflow did not reach a rejected outcome.");
        }

        count = await LoadControlledCountAsync(countId)
            ?? throw new InvalidOperationException("The physical count disappeared during Stores approval.");
        if (request.Approved)
        {
            count.Status = "PendingFinanceApproval";
            count.StoresApprovedById = userId;
            count.StoresApprovedAtUtc = DateTime.UtcNow;
        }
        else
        {
            ReturnForRecount(count, Required(request.Reason ?? request.Comment, "A rejection reason is required.", 2000));
        }
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, action, userId, request.IdempotencyKey,
            request.Approved ? request.Comment : request.Reason ?? request.Comment,
            new { request.Approved, StockAdjustmentStatus = adjustment?.Status }, StoresManagerRole, request.CorrelationId);
        await _unitOfWork.SaveChangesAsync();
        await RecordCountControlEventAsync(count, request.Approved ? "StoresApprove" : "StoresReject",
            request.Approved ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Rejected,
            request.IdempotencyKey, request.CorrelationId, request.Reason ?? request.Comment);
        return true;
    }

    public async Task<bool> ApproveFinanceAsync(
        Guid countId,
        Guid userId,
        PhysicalCountDecisionRequest request)
    {
        EnsureActor(userId);
        EnsureRole(FinanceReviewerRole, "Only the Finance Reviewer may decide the financial variance stage.");
        var count = await LoadControlledCountAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        await EnsureAccessAsync(count, "procurement.inventory.adjust.approve");
        EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Reload and retry the Finance decision.");
        var action = request.Approved ? PhysicalCountActionType.FinanceApproved : PhysicalCountActionType.Rejected;
        if (await HasCountActionAsync(count.Id, action, request.IdempotencyKey)) return true;
        if (count.Status != "PendingFinanceApproval")
            throw new InvalidOperationException("The count is not awaiting Finance approval.");
        EnsureIndependentActor(count, userId, includeFinance: false, includeAudit: false);

        if (request.Approved)
        {
            count.Status = "PendingAuditAttestation";
            count.FinanceApprovedById = userId;
            count.FinanceApprovedAtUtc = DateTime.UtcNow;
        }
        else
        {
            ReturnForRecount(count, Required(request.Reason ?? request.Comment, "A rejection reason is required.", 2000));
        }
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, action, userId, request.IdempotencyKey,
            request.Approved ? request.Comment : request.Reason ?? request.Comment,
            new { request.Approved }, FinanceReviewerRole, request.CorrelationId);
        await _unitOfWork.SaveChangesAsync();
        await RecordCountControlEventAsync(count, request.Approved ? "FinanceApprove" : "FinanceReject",
            request.Approved ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Rejected,
            request.IdempotencyKey, request.CorrelationId, request.Reason ?? request.Comment);
        return true;
    }

    public async Task<bool> AttestAuditAsync(
        Guid countId,
        Guid userId,
        PhysicalCountDecisionRequest request)
    {
        EnsureActor(userId);
        EnsureRole(ProcurementAccessControlRegistry.InternalAuditRole,
            "Only Internal Audit may record the final read-only control attestation.");
        var count = await LoadControlledCountAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        await EnsureAccessAsync(count, "procurement.inventory.read");
        EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Reload and retry the audit attestation.");
        var action = request.Approved ? PhysicalCountActionType.AuditAttested : PhysicalCountActionType.Rejected;
        if (await HasCountActionAsync(count.Id, action, request.IdempotencyKey)) return true;
        if (count.Status != "PendingAuditAttestation")
            throw new InvalidOperationException("The count is not awaiting Internal Audit attestation.");
        EnsureIndependentActor(count, userId, includeFinance: true, includeAudit: false);

        if (request.Approved)
        {
            count.Status = "ReadyToPost";
            count.AuditAttestedById = userId;
            count.AuditAttestedAtUtc = DateTime.UtcNow;
        }
        else
        {
            ReturnForRecount(count, Required(request.Reason ?? request.Comment, "An audit exception reason is required.", 2000));
        }
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, action, userId, request.IdempotencyKey,
            request.Approved ? request.Comment : request.Reason ?? request.Comment,
            new { request.Approved }, ProcurementAccessControlRegistry.InternalAuditRole, request.CorrelationId);
        await _unitOfWork.SaveChangesAsync();
        await RecordCountControlEventAsync(count, request.Approved ? "AuditAttest" : "AuditException",
            request.Approved ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.ReviewRequired,
            request.IdempotencyKey, request.CorrelationId, request.Reason ?? request.Comment);
        return true;
    }

    public async Task<bool> PostControlledAdjustmentsAsync(
        Guid countId,
        Guid userId,
        PhysicalCountMutationRequest request)
    {
        EnsureActor(userId);
        EnsureRole(FinanceReviewerRole, "Only the independently assigned Finance Reviewer may post the approved count variance.");
        var count = await LoadControlledCountAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        await EnsureAccessAsync(count, "procurement.inventory.adjust.approve");
        EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Reload and retry posting.");
        if (await HasCountActionAsync(count.Id, PhysicalCountActionType.Posted, request.IdempotencyKey)) return true;
        if (count.Status != "ReadyToPost")
            throw new InvalidOperationException("The count must complete Stores, Finance, and Internal Audit stages before posting.");
        if (count.CutoffAtUtc.HasValue && DateTime.UtcNow > count.CutoffAtUtc.Value)
            throw new InvalidOperationException("The count cannot post after its governed year-end cut-off.");
        if (count.FinanceApprovedById != userId)
            throw new InvalidOperationException("The Finance Reviewer who approved the variance must perform the governed Finance posting.");
        if (count.InitiatedById == userId || count.CountedById == userId || count.StoresApprovedById == userId || count.AuditAttestedById == userId)
            throw new InvalidOperationException("The initiator, counter, Stores approver, or Internal Audit attestor cannot post this count.");

        StockAdjustmentDetailDto? posted = null;
        if (count.TotalVarianceQuantity != 0)
        {
            if (!count.StockAdjustmentId.HasValue)
                throw new InvalidOperationException("The count has a variance but no governed stock adjustment.");
            var adjustment = await _stockAdjustmentService.GetByIdAsync(count.StockAdjustmentId.Value)
                ?? throw new InvalidOperationException("The governed stock adjustment was not found.");
            posted = await _stockAdjustmentService.PostAsync(adjustment.Id, userId, new StockAdjustmentActionRequest
            {
                RowVersion = adjustment.RowVersion,
                IdempotencyKey = $"pc-post:{request.IdempotencyKey}"[..Math.Min(100, $"pc-post:{request.IdempotencyKey}".Length)],
                Comment = request.Comment ?? $"Posted from controlled physical count {count.CountNumber}."
            });
            if (posted.Status != "Posted" || !posted.FinancePostingEventId.HasValue)
                throw new InvalidOperationException("The stock adjustment did not complete authoritative Finance posting.");
        }

        count = await LoadControlledCountAsync(countId)
            ?? throw new InvalidOperationException("The physical count disappeared during posting.");
        count.Status = "Posted";
        count.PostedById = userId;
        count.PostedDate = DateTime.UtcNow;
        count.FreezeReleasedAtUtc = DateTime.UtcNow;
        await _countRepository.UpdateAsync(count);
        await AddCountActionAsync(count, PhysicalCountActionType.Posted, userId, request.IdempotencyKey,
            request.Comment, new { posted?.FinancePostingEventId, posted?.FinanceJournalEntryId }, FinanceReviewerRole,
            request.CorrelationId);
        await _unitOfWork.SaveChangesAsync();
        await RecordCountControlEventAsync(count, "Post", ProcurementControlEventResult.Succeeded,
            request.IdempotencyKey, request.CorrelationId, request.Comment);
        return true;
    }

    public async Task<IReadOnlyList<InventoryCycleCountScheduleDto>> GetCycleCountSchedulesAsync()
    {
        var tenantId = RequiredTenantId();
        var schedules = await _unitOfWork.Repository<InventoryCycleCountSchedule>().GetQueryable(x =>
                x.TenantId == tenantId && !x.IsDeleted)
            .AsNoTracking().OrderBy(x => x.WarehouseId).ThenBy(x => x.LocationId).ThenBy(x => x.ABCClass)
            .ToListAsync();
        var result = new List<InventoryCycleCountScheduleDto>();
        foreach (var schedule in schedules)
        {
            if ((await _accessControl.CheckCapabilityAsync(
                    BuildAccessRequest("procurement.inventory.read", schedule.WarehouseId, schedule.LocationId,
                        $"Cycle schedule {schedule.ABCClass}"), Guid.NewGuid().ToString("N"))).Allowed)
                result.Add(MapSchedule(schedule));
        }
        return result;
    }

    public async Task<InventoryCycleCountScheduleDto> SaveCycleCountScheduleAsync(
        Guid? scheduleId,
        SaveInventoryCycleCountScheduleRequest request,
        Guid userId)
    {
        EnsureActor(userId);
        EnsureRole(StoresManagerRole, "Only the Stores Manager may maintain ABC cycle-count schedules.");
        var tenantId = RequiredTenantId();
        await EnsureAccessAsync("procurement.inventory.count", request.WarehouseId, request.LocationId, "ABC cycle schedule");
        var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
                x.Id == request.LocationId && x.TenantId == tenantId && !x.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync()
            ?? throw new ArgumentException("The selected warehouse location was not found.");
        if (location.InventoryWarehouseId != request.WarehouseId)
            throw new InvalidOperationException("The selected location does not belong to the selected warehouse.");
        var calendar = await LoadOpenOccurrenceAsync(request.CalendarOccurrenceId, tenantId,
            ProcurementCalendarEventType.CycleCount, "Cycle-count");
        var cutoff = await LoadOpenOccurrenceAsync(request.CutoffOccurrenceId, tenantId,
            ProcurementCalendarEventType.YearEndClose, "Year-end cut-off");
        var nextDue = EnsureUtc(request.NextDueAtUtc);
        if (nextDue > cutoff.DueAtUtc)
            throw new InvalidOperationException("The next cycle-count due date cannot fall after the year-end cut-off.");
        if (calendar.DueAtUtc > cutoff.DueAtUtc)
            throw new InvalidOperationException("The selected cycle-count occurrence falls after the year-end cut-off.");

        var repo = _unitOfWork.Repository<InventoryCycleCountSchedule>();
        InventoryCycleCountSchedule schedule;
        if (scheduleId.HasValue)
        {
            schedule = await repo.GetQueryable(x => x.Id == scheduleId && x.TenantId == tenantId && !x.IsDeleted)
                .SingleOrDefaultAsync() ?? throw new ArgumentException("Cycle-count schedule not found.");
            EnsureRowVersion(schedule.RowVersion, request.RowVersion, "The schedule changed. Reload and retry.");
        }
        else
        {
            schedule = new InventoryCycleCountSchedule { TenantId = tenantId };
        }
        schedule.WarehouseId = request.WarehouseId;
        schedule.LocationId = request.LocationId;
        schedule.ABCClass = NormalizeAbcClass(request.ABCClass) ?? throw new InvalidOperationException("ABC class is required.");
        schedule.FrequencyDays = request.FrequencyDays;
        schedule.NextDueAtUtc = nextDue;
        schedule.CalendarOccurrenceId = calendar.Id;
        schedule.CutoffOccurrenceId = cutoff.Id;
        schedule.CutoffAtUtc = cutoff.DueAtUtc;
        schedule.FreezeInventory = true;
        schedule.BlindCount = true;
        schedule.RecountQuantityThreshold = request.RecountQuantityThreshold;
        schedule.RecountValueThreshold = request.RecountValueThreshold;
        schedule.IsActive = request.IsActive;
        schedule.Notes = Normalize(request.Notes, 500);
        if (scheduleId.HasValue) await repo.UpdateAsync(schedule); else await repo.AddAsync(schedule);
        await _unitOfWork.SaveChangesAsync();
        return MapSchedule(schedule);
    }

    public async Task<CycleCountGenerationResultDto> GenerateDueCycleCountsAsync(
        Guid tenantId,
        DateTime nowUtc,
        Guid? requestedById = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        var now = EnsureUtc(nowUtc);
        var scheduleRepo = _unitOfWork.Repository<InventoryCycleCountSchedule>();
        var due = await scheduleRepo.GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive &&
                x.NextDueAtUtc <= now && x.CutoffAtUtc >= now)
            .OrderBy(x => x.NextDueAtUtc).ThenBy(x => x.Id).ToListAsync();
        var result = new CycleCountGenerationResultDto { DueSchedules = due.Count };
        foreach (var schedule in due)
        {
            var scheduledFor = schedule.NextDueAtUtc;
            var exists = await _unitOfWork.Repository<PhysicalCount>().ExistsAsync(x =>
                x.TenantId == tenantId && x.CycleCountScheduleId == schedule.Id && x.ScheduledForUtc == scheduledFor && !x.IsDeleted);
            if (!exists)
            {
                var occurrence = await _unitOfWork.Repository<ProcurementCalendarOccurrence>().GetQueryable(x =>
                        x.Id == schedule.CalendarOccurrenceId && x.TenantId == tenantId && !x.IsDeleted)
                    .AsNoTracking().SingleAsync();
                var count = new PhysicalCount
                {
                    TenantId = tenantId,
                    CountNumber = await GenerateCountNumberAsync(),
                    WarehouseId = schedule.WarehouseId,
                    LocationId = schedule.LocationId,
                    CountType = CountType.CycleCount,
                    CountDate = now,
                    ScheduledForUtc = scheduledFor,
                    CutoffAtUtc = schedule.CutoffAtUtc,
                    CycleCountScheduleId = schedule.Id,
                    CalendarOccurrenceId = schedule.CalendarOccurrenceId,
                    CutoffOccurrenceId = schedule.CutoffOccurrenceId,
                    ABCClass = schedule.ABCClass,
                    FreezeInventory = true,
                    BlindCount = true,
                    Status = "Draft",
                    InitiatedById = occurrence.OwnerUserId,
                    Notes = $"Automatically generated ABC-{schedule.ABCClass} cycle count from calendar occurrence {occurrence.OccurrenceKey}."
                };
                await _countRepository.AddAsync(count);
                await _unitOfWork.SaveChangesAsync();
                await PopulateScheduledCountItemsAsync(count, schedule.ABCClass);
                if (count.TotalItems == 0)
                {
                    count.Status = "Cancelled";
                    count.CancellationReason = $"No active ABC-{schedule.ABCClass} inventory was available at generation time.";
                    count.FreezeReleasedAtUtc = now;
                    result.EmptySchedules++;
                }
                await AddCountActionAsync(count, PhysicalCountActionType.Scheduled, occurrence.OwnerUserId,
                    $"schedule:{schedule.Id:N}:{scheduledFor:yyyyMMddHHmm}", count.Notes,
                    new { schedule.Id, schedule.ABCClass, scheduledFor, schedule.CutoffAtUtc },
                    occurrence.OwnerRoleName ?? "CalendarOwner", $"cycle-schedule:{schedule.Id:N}");
                await _countRepository.UpdateAsync(count);
                await _unitOfWork.SaveChangesAsync();
                result.CountsCreated++;
                result.PhysicalCountIds.Add(count.Id);
                schedule.LastPhysicalCountId = count.Id;
            }
            schedule.LastGeneratedAtUtc = now;
            var next = scheduledFor.AddDays(schedule.FrequencyDays);
            if (next > schedule.CutoffAtUtc)
            {
                schedule.IsActive = false;
                schedule.NextDueAtUtc = schedule.CutoffAtUtc;
            }
            else
            {
                schedule.NextDueAtUtc = next;
            }
            await scheduleRepo.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();
        }
        return result;
    }

    private async Task<PhysicalCount?> LoadControlledCountAsync(Guid countId) =>
        await _unitOfWork.Repository<PhysicalCount>().GetQueryable(x =>
                x.Id == countId && x.TenantId == RequiredTenantId() && !x.IsDeleted)
            .Include(x => x.Warehouse).Include(x => x.Location)
            .Include(x => x.InitiatedBy).Include(x => x.CountedBy)
            .Include(x => x.Items).ThenInclude(x => x.Location)
            .Include(x => x.Actions.OrderBy(a => a.Sequence))
            .SingleOrDefaultAsync();

    private async Task<ProcurementCalendarOccurrence> LoadOpenOccurrenceAsync(
        Guid id,
        Guid tenantId,
        ProcurementCalendarEventType eventType,
        string label)
    {
        var occurrence = await _unitOfWork.Repository<ProcurementCalendarOccurrence>().GetQueryable(x =>
                x.Id == id && x.TenantId == tenantId && !x.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync()
            ?? throw new ArgumentException($"{label} occurrence was not found.");
        if (occurrence.EventType != eventType)
            throw new InvalidOperationException($"The selected {label.ToLowerInvariant()} occurrence has the wrong calendar event type.");
        if (occurrence.Status is ProcurementCalendarOccurrenceStatus.Completed or ProcurementCalendarOccurrenceStatus.Cancelled or
            ProcurementCalendarOccurrenceStatus.Failed)
            throw new InvalidOperationException($"The selected {label.ToLowerInvariant()} occurrence is terminal.");
        return occurrence;
    }

    private async Task EnsureStockAdjustmentSubmittedAsync(Guid countId, Guid userId, string operationKey, string? comment)
    {
        var count = await LoadControlledCountAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");
        if (count.TotalVarianceQuantity == 0) return;
        StockAdjustmentDetailDto adjustment;
        if (count.StockAdjustmentId.HasValue)
        {
            adjustment = await _stockAdjustmentService.GetByIdAsync(count.StockAdjustmentId.Value)
                ?? throw new InvalidOperationException("The linked controlled stock adjustment was not found.");
        }
        else
        {
            adjustment = await _stockAdjustmentService.CreateAsync(new CreateStockAdjustmentDto
            {
                WarehouseId = count.WarehouseId,
                ReasonCode = count.CountType == CountType.CycleCount
                    ? StockAdjustmentReasonCodes.CycleCount
                    : StockAdjustmentReasonCodes.PhysicalCount,
                Description = $"Variance from controlled physical count {count.CountNumber}.",
                Reference = count.CountNumber,
                AdjustmentDate = DateTime.UtcNow,
                // A rejected governed adjustment may be returned for recount.  Bind the
                // replacement adjustment to that recount operation so the retry does not
                // resolve to the already-rejected adjustment from the previous cycle.
                IdempotencyKey = $"physical-count:{count.Id:N}:{Hash(operationKey)[..16]}",
                CorrelationId = $"physical-count:{count.Id:N}",
                Items = count.Items.Where(x => x.IsCounted && x.VarianceQuantity != 0).Select(x =>
                    new CreateStockAdjustmentItemDto
                    {
                        InventoryItemId = x.InventoryItemId,
                        LocationId = x.LocationId ?? count.LocationId,
                        LotNumber = x.LotNumber,
                        SerialNumber = x.SerialNumber,
                        AdjustmentQuantity = x.VarianceQuantity,
                        UnitCost = x.UnitCost,
                        Reason = $"Count {count.CountNumber} variance after governed count/recount.",
                        Notes = x.InvestigationNotes ?? x.Notes
                    }).ToList()
            }, userId);
        }
        if (adjustment.Status == "Draft")
        {
            adjustment = await _stockAdjustmentService.SubmitAsync(adjustment.Id, userId, new StockAdjustmentActionRequest
            {
                RowVersion = adjustment.RowVersion,
                IdempotencyKey = $"pc-submit:{operationKey}"[..Math.Min(100, $"pc-submit:{operationKey}".Length)],
                Comment = comment ?? $"Submitted from controlled physical count {count.CountNumber}."
            });
        }
        if (adjustment.Status != "PendingApproval")
            throw new InvalidOperationException($"The linked stock adjustment must await independent approval; current status is {adjustment.Status}.");
        count = await LoadControlledCountAsync(countId)
            ?? throw new InvalidOperationException("The physical count disappeared while linking its adjustment.");
        count.StockAdjustmentId = adjustment.Id;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task PopulateScheduledCountItemsAsync(PhysicalCount count, string abcClass)
    {
        var warehouseQuantities = await _warehouseQuantityRepository.GetByWarehouseAsync(count.WarehouseId);
        var itemIds = warehouseQuantities.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x =>
                x.TenantId == count.TenantId && itemIds.Contains(x.Id) && !x.IsDeleted && x.ABCClass == abcClass)
            .AsNoTracking().ToDictionaryAsync(x => x.Id);
        var total = 0;
        foreach (var quantity in warehouseQuantities.Where(x => items.ContainsKey(x.InventoryItemId)))
        {
            var item = items[quantity.InventoryItemId];
            await _countItemRepository.AddAsync(new PhysicalCountItem
            {
                TenantId = count.TenantId,
                PhysicalCountId = count.Id,
                InventoryItemId = item.Id,
                LocationId = count.LocationId,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                UnitOfMeasure = item.UnitOfMeasure,
                SystemQuantity = quantity.CurrentStock,
                UnitCost = item.StandardCost
            });
            total++;
        }
        count.TotalItems = total;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task AddCountActionAsync(
        PhysicalCount count,
        PhysicalCountActionType actionType,
        Guid actorUserId,
        string idempotencyKey,
        string? comment,
        object? payload,
        string? actorRole = null,
        string? correlationId = null)
    {
        var key = Required(idempotencyKey, "An idempotency key is required.", 100);
        var actionRepo = _unitOfWork.Repository<PhysicalCountAction>();
        if (await actionRepo.ExistsAsync(x => x.TenantId == count.TenantId && x.PhysicalCountId == count.Id &&
            x.ActionType == actionType && x.IdempotencyKey == key && !x.IsDeleted)) return;
        var previous = await actionRepo.GetQueryable(x => x.TenantId == count.TenantId && x.PhysicalCountId == count.Id && !x.IsDeleted)
            .OrderByDescending(x => x.Sequence).AsNoTracking().FirstOrDefaultAsync();
        var snapshot = JsonSerializer.Serialize(new
        {
            count.Id,
            count.CountNumber,
            count.Status,
            count.TotalItems,
            count.CountedItems,
            count.TotalVarianceQuantity,
            count.TotalVarianceValue,
            count.StockAdjustmentId,
            Payload = payload
        }, CountJsonOptions);
        var payloadHash = Hash($"{actionType}|{key}|{comment}|{snapshot}");
        var occurred = DateTime.UtcNow;
        await actionRepo.AddAsync(new PhysicalCountAction
        {
            TenantId = count.TenantId,
            PhysicalCountId = count.Id,
            Sequence = (previous?.Sequence ?? 0) + 1,
            ActionType = actionType,
            ActorUserId = actorUserId,
            ActorRole = Normalize(actorRole, 100) ?? CurrentActorRole(),
            OccurredAtUtc = occurred,
            IdempotencyKey = key,
            PayloadHash = payloadHash,
            CorrelationId = Normalize(correlationId, 100) ?? $"physical-count:{count.Id:N}",
            Comment = Normalize(comment, 2000),
            SnapshotJson = snapshot,
            IntegrityHash = Hash($"{previous?.IntegrityHash}|{count.TenantId:N}|{count.Id:N}|{(int)actionType}|{actorUserId:N}|{occurred:O}|{payloadHash}")
        });
    }

    private async Task<bool> HasCountActionAsync(Guid countId, PhysicalCountActionType actionType, string key)
    {
        var normalized = Required(key, "An idempotency key is required.", 100);
        return await _unitOfWork.Repository<PhysicalCountAction>().ExistsAsync(x =>
            x.TenantId == RequiredTenantId() && x.PhysicalCountId == countId && x.ActionType == actionType &&
            x.IdempotencyKey == normalized && !x.IsDeleted);
    }

    private async Task RecordCountControlEventAsync(
        PhysicalCount count,
        string action,
        ProcurementControlEventResult result,
        string idempotencyKey,
        string? correlationId,
        string? reason)
    {
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = $"physical-count:{count.Id:N}:{action.ToLowerInvariant()}:{Hash(idempotencyKey)[..16]}",
            EventType = "InventoryPhysicalCountControl",
            Action = action,
            Result = result,
            RuleCode = "TDC-0609",
            RuleVersion = "1",
            DecisionKeys = ["INV-012", "INV-013", "DEC-001", "DEC-014"],
            SourceType = "PhysicalCount",
            SourceId = count.Id,
            SourceReference = count.CountNumber,
            Reason = reason,
            ResultValues = new { count.Status, count.TotalVarianceQuantity, count.TotalVarianceValue, count.StockAdjustmentId },
            CorrelationId = Normalize(correlationId, 100) ?? $"physical-count:{count.Id:N}",
            OccurredAtUtc = DateTime.UtcNow
        });
    }

    private static InventoryCycleCountScheduleDto MapSchedule(InventoryCycleCountSchedule schedule) => new()
    {
        Id = schedule.Id,
        WarehouseId = schedule.WarehouseId,
        LocationId = schedule.LocationId,
        ABCClass = schedule.ABCClass,
        FrequencyDays = schedule.FrequencyDays,
        NextDueAtUtc = schedule.NextDueAtUtc,
        CalendarOccurrenceId = schedule.CalendarOccurrenceId,
        CutoffOccurrenceId = schedule.CutoffOccurrenceId,
        CutoffAtUtc = schedule.CutoffAtUtc,
        FreezeInventory = schedule.FreezeInventory,
        BlindCount = schedule.BlindCount,
        RecountQuantityThreshold = schedule.RecountQuantityThreshold,
        RecountValueThreshold = schedule.RecountValueThreshold,
        IsActive = schedule.IsActive,
        LastGeneratedAtUtc = schedule.LastGeneratedAtUtc,
        LastPhysicalCountId = schedule.LastPhysicalCountId,
        Notes = schedule.Notes,
        RowVersion = Convert.ToBase64String(schedule.RowVersion)
    };

    private void EnsureRole(string role, string message)
    {
        if (!_currentUserService.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ProcurementAccessAuthorizationException(message);
    }

    private static void EnsureIndependentActor(
        PhysicalCount count,
        Guid actor,
        bool includeFinance,
        bool includeAudit)
    {
        var prohibited = new List<Guid?>
        {
            count.InitiatedById,
            count.CountedById,
            count.StoresApprovedById
        };
        prohibited.AddRange(count.Items.Select(x => x.RecountedById));
        if (includeFinance) prohibited.Add(count.FinanceApprovedById);
        if (includeAudit) prohibited.Add(count.AuditAttestedById);
        if (prohibited.Any(x => x.HasValue && x.Value == actor))
            throw new InvalidOperationException("Segregation of duties requires an independent actor for this count stage.");
    }

    private static void ReturnForRecount(PhysicalCount count, string reason)
    {
        count.Status = "RecountRequired";
        count.StockAdjustmentId = null;
        count.StoresApprovedById = null;
        count.StoresApprovedAtUtc = null;
        count.FinanceApprovedById = null;
        count.FinanceApprovedAtUtc = null;
        count.AuditAttestedById = null;
        count.AuditAttestedAtUtc = null;
        count.InvestigationSummary = $"{count.InvestigationSummary}\nControl-stage exception: {reason}".Trim();
        foreach (var item in count.Items.Where(x => x.VarianceQuantity != 0)) item.RequiresRecount = true;
    }

    private void EnsureActor(Guid userId)
    {
        if (userId == Guid.Empty || !Guid.TryParse(_currentUserService.UserId, out var current) || current != userId)
            throw new ProcurementAccessAuthorizationException("The authenticated actor is required.");
    }

    private Guid RequiredTenantId() => _currentUserService.TenantId is { } tenantId && tenantId != Guid.Empty
        ? tenantId
        : throw new ProcurementAccessAuthorizationException("An authenticated tenant is required.");

    private string CurrentActorRole() => _currentUserService.Roles
        .FirstOrDefault(x => x.StartsWith("TDC_", StringComparison.OrdinalIgnoreCase)) ?? "AuthenticatedUser";

    private static string? NormalizeAbcClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "C"
            ? normalized
            : throw new InvalidOperationException("ABC class must be A, B, or C.");
    }

    private static void EnsureRowVersion(byte[] current, string? supplied, string message)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(Required(supplied, message, 200)); }
        catch (FormatException) { throw new InvalidOperationException(message); }
        if (!current.SequenceEqual(expected)) throw new InvalidOperationException(message);
    }

    private static string Required(string? value, string message, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(message);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new InvalidOperationException(message);
        return normalized;
    }

    private static string? Normalize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= max ? normalized : normalized[..max];
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
