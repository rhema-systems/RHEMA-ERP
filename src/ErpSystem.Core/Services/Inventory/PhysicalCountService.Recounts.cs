using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class PhysicalCountService
{
    public async Task<PhysicalCountDto> CreateRecountAsync(Guid countId, Guid userId, CreatePhysicalCountRecountRequest request)
    {
        PhysicalCountDto? result = null;
        await InCountTransactionAsync(countId, async () =>
        {
            EnsureActor(userId);
            var source = await LoadControlledCountAsync(countId) ?? throw new ArgumentException("Count not found.");
            var counterDecision = source.Status == "UnderReview" && !source.ObservationSubmittedAtUtc.HasValue;
            await EnsureAccessAsync(source, counterDecision ? "procurement.inventory.count" : "procurement.inventory.adjust.approve");
            if (counterDecision)
            {
                EnsureCounterCanEdit(source, userId);
                await EnsureCurrentCounterIdentityAsync(source, userId);
            }
            else EnsureIndependentActor(source, userId, includeFinance: false, includeAudit: false);
            var key = Required(request.IdempotencyKey, "A recount request key is required.", 100);
            if (request.Items.Count == 0 || request.Items.Count > 10000 || request.Items.Select(x => x.PhysicalCountItemId).Distinct().Count() != request.Items.Count)
                throw new InvalidOperationException("Select distinct count lines for recount.");
            var ordered = request.Items.OrderBy(x => x.PhysicalCountItemId).ToList();
            var hash = Hash(JsonSerializer.Serialize(new { request.RowVersion, request.Comment, Items = ordered }, CountJsonOptions));
            var prior = await _countRepository.GetQueryable(x => x.TenantId == source.TenantId && x.ParentPhysicalCountId == countId && x.RecountRequestKey == key).SingleOrDefaultAsync();
            if (prior != null)
            {
                if (prior.InitiatedById != userId || prior.RecountRequestHash != hash)
                    throw new InvalidOperationException("This recount request key belongs to another actor or selection.");
                result = MapToDto(prior);
                return true;
            }
            EnsureRowVersion(source.RowVersion, request.RowVersion, "The count changed. Reload before selecting recount lines.");
            if (source.Status is not ("UnderReview" or "UnderInvestigation" or "RecountRequired") || source.StockAdjustmentId.HasValue)
                throw new InvalidOperationException("Select recount lines during review or after the existing adjustment has been governedly retired for investigation.");
            var selected = new List<(PhysicalCountItem Line, string Reason)>();
            foreach (var selection in ordered)
            {
                var line = source.Items.SingleOrDefault(x => x.Id == selection.PhysicalCountItemId && !x.IsDeleted)
                    ?? throw new InvalidOperationException("A selected line does not belong to this count.");
                EnsureRowVersion(line.RowVersion, selection.ItemRowVersion, "A selected count line changed. Reload and retry.");
                if (!line.IsCounted || line.SupersededByPhysicalCountId.HasValue)
                    throw new InvalidOperationException("Select counted lines that do not already have a recount sheet.");
                selected.Add((line, Required(selection.Reason, "Record an investigation reason for every selected line.", 2000)));
            }
            var rootId = source.RootPhysicalCountId ?? source.Id;
            var rootLineIds = selected.Select(x => x.Line.RootPhysicalCountItemId ?? x.Line.Id).ToList();
            if (await _unitOfWork.Repository<PhysicalCountAdjustmentClaim>().GetQueryable(x => x.TenantId == source.TenantId && rootLineIds.Contains(x.RootPhysicalCountItemId)).AnyAsync())
                throw new InvalidOperationException("A selected original line has already been resolved and cannot be adjusted again.");
            var employees = source.Counters.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.EmployeeId).Distinct().ToList();
            if (employees.Count == 0)
                throw new InvalidOperationException("A recount requires an assigned Employee committee. Prepare a governed count with counter assignments for legacy records.");
            var attempt = (await _countRepository.GetQueryable(x => x.TenantId == source.TenantId && x.RootPhysicalCountId == rootId)
                .Select(x => (int?)x.RecountAttempt).MaxAsync() ?? 0) + 1;
            var displayNotes = $"Recount #{attempt} of {source.CountNumber}. {request.Comment}".Trim();
            var child = new PhysicalCount
            {
                Id = Guid.NewGuid(), TenantId = source.TenantId, CountNumber = await GenerateCountNumberAsync(),
                RootPhysicalCountId = rootId, ParentPhysicalCountId = source.Id, RecountAttempt = attempt,
                RecountRequestKey = key, RecountRequestHash = hash, WarehouseId = source.WarehouseId,
                LocationId = source.LocationId, CategoryId = source.CategoryId, CountType = source.CountType,
                ABCClass = source.ABCClass, BlindCount = source.BlindCount, FreezeInventory = true,
                FreezeStartedAtUtc = DateTime.UtcNow, CountDate = DateTime.UtcNow, InitiatedById = userId,
                Status = "Draft", Notes = displayNotes[..Math.Min(displayNotes.Length, 2000)]
            };
            await _countRepository.AddAsync(child);
            await _unitOfWork.SaveChangesAsync();
            var locations = ReadResolvedCountLocations(source);
            foreach (var (line, reason) in selected)
            {
                var copy = new PhysicalCountItem
                {
                    Id = Guid.NewGuid(), TenantId = source.TenantId, PhysicalCountId = child.Id,
                    RootPhysicalCountItemId = line.RootPhysicalCountItemId ?? line.Id, PredecessorPhysicalCountItemId = line.Id,
                    InventoryItemId = line.InventoryItemId, LocationId = EffectiveCountLocation(line, locations),
                    ItemCode = line.ItemCode, ItemName = line.ItemName, UnitOfMeasure = line.UnitOfMeasure,
                    SystemQuantity = line.SystemQuantity, UnitCost = line.UnitCost, LotNumber = line.LotNumber,
                    SerialNumber = line.SerialNumber, RecountReason = reason
                };
                await _countItemRepository.AddAsync(copy);
                child.Items.Add(copy);
                line.RequiresRecount = true;
                line.RecountReason = reason;
                line.SupersededByPhysicalCountId = child.Id;
                await _countItemRepository.UpdateAsync(line);
            }
            child.TotalItems = selected.Count;
            child.TotalSystemQuantity = selected.Sum(x => x.Line.SystemQuantity);
            await ReplaceCountersCoreAsync(child, employees, userId, $"Recount #{attempt} assignment from {source.CountNumber}.");
            await AddCountActionAsync(child, PhysicalCountActionType.Created, userId, $"create:{child.Id:N}", child.Notes,
                new { RootPhysicalCountId = rootId, ParentPhysicalCountId = source.Id, RecountAttempt = attempt }, "Recount");
            await AddCountActionAsync(source, PhysicalCountActionType.RecountRequired, userId, key, request.Comment,
                new { ChildPhysicalCountId = child.Id, Items = ordered }, counterDecision ? "Counter" : "Investigator", request.CorrelationId);
            source.UpdatedAt = DateTime.UtcNow;
            await _countRepository.UpdateAsync(source);
            await _countRepository.UpdateAsync(child);
            await _unitOfWork.SaveChangesAsync();
            result = MapToDto(child);
            return true;
        });
        return result!;
    }

    private async Task PopulateRecountContextAsync(PhysicalCount count, PhysicalCountDetailDto detail)
    {
        var root = count.RootPhysicalCountId ?? count.Id;
        var sheets = await _countRepository.GetQueryable(x => x.TenantId == count.TenantId && !x.IsDeleted && (x.Id == root || x.RootPhysicalCountId == root))
            .AsNoTracking().OrderBy(x => x.RecountAttempt).ToListAsync();
        detail.Sheets = (await FilterReadableAsync(sheets)).Select(MapToDto).ToList();
        var counterDecision = count.Status == "UnderReview" && !count.ObservationSubmittedAtUtc.HasValue;
        var actor = Guid.TryParse(_currentUserService.UserId, out var actorId) ? actorId : Guid.Empty;
        detail.CanCreateRecount = !count.StockAdjustmentId.HasValue && (count.Status is "UnderReview" or "UnderInvestigation" or "RecountRequired")
            && await CanAccessAsync(count, counterDecision ? "procurement.inventory.count" : "procurement.inventory.adjust.approve")
            && (counterDecision ? detail.CanReview : !WasCounter(count, actor) && count.InitiatedById != actor);
        if (await WasRootCountParticipantAsync(count, actor)) { detail.CanDecide = false; detail.CanPost = false; }

    }

    private async Task<bool> WasRootCountParticipantAsync(PhysicalCount count, Guid actor)
    {
        var root = count.RootPhysicalCountId ?? count.Id;
        // Preserve the established explicit-post policy for untouched legacy roots only.
        // Any retained Employee assignment or descendant opts the entire family into committee separation.
        if (!count.ApprovalRequired && !count.RootPhysicalCountId.HasValue && count.Counters.Count == 0 &&
            !await _countRepository.GetQueryable(x => x.TenantId == count.TenantId && x.RootPhysicalCountId == count.Id).AnyAsync())
            return false;
        return await _countRepository.GetQueryable(x => x.TenantId == count.TenantId && (x.Id == root || x.RootPhysicalCountId == root))
            .AnyAsync(x => x.InitiatedById == actor || x.CountedById == actor ||
                x.Counters.Any(c => c.UserId == actor) || x.Items.Any(i => i.CountedById == actor || i.RecountedById == actor));
    }

    private async Task EnsureRootCountIndependenceAsync(PhysicalCount count, Guid actor)
    {
        if (await WasRootCountParticipantAsync(count, actor))
            throw new InvalidOperationException("The original and recount committees cannot approve or post their own count observations.");
    }

    private async Task ClaimResolvedCountLinesAsync(PhysicalCount count, Guid actor)
    {
        var lines = count.Items.Where(x => !x.IsDeleted && x.IsCounted && !x.RequiresRecount && !x.SupersededByPhysicalCountId.HasValue).ToList();
        var claims = _unitOfWork.Repository<PhysicalCountAdjustmentClaim>();
        var rootIds = lines.Select(x => x.RootPhysicalCountItemId ?? x.Id).ToList();
        if (await claims.GetQueryable(x => x.TenantId == count.TenantId && rootIds.Contains(x.RootPhysicalCountItemId)).AnyAsync())
            throw new InvalidOperationException("A root count line has already been resolved. Refresh its retained posting history.");
        var adjustmentLines = count.StockAdjustmentId.HasValue
            ? await _unitOfWork.Repository<StockAdjustmentItem>().GetQueryable(x => x.TenantId == count.TenantId && x.AdjustmentId == count.StockAdjustmentId && !x.IsDeleted).ToListAsync()
            : new List<StockAdjustmentItem>();
        var locations = ReadResolvedCountLocations(count);
        foreach (var line in lines)
        {
            Guid? adjustmentItemId = null;
            if (line.VarianceQuantity != 0)
            {
                var matching = adjustmentLines.Where(x => x.InventoryItemId == line.InventoryItemId && x.LocationId == EffectiveCountLocation(line, locations)
                    && x.AdjustmentQuantity == line.VarianceQuantity && x.UnitCost == line.UnitCost && x.LotNumber == line.LotNumber && x.SerialNumber == line.SerialNumber).ToList();
                if (matching.Count != 1) throw new InvalidOperationException("The governed adjustment does not match one retained count observation.");
                adjustmentItemId = matching[0].Id;
            }
            await claims.AddAsync(new PhysicalCountAdjustmentClaim
            {
                TenantId = count.TenantId, RootPhysicalCountItemId = line.RootPhysicalCountItemId ?? line.Id,
                PhysicalCountId = count.Id, PhysicalCountItemId = line.Id,
                StockAdjustmentId = adjustmentItemId.HasValue ? count.StockAdjustmentId : null, StockAdjustmentItemId = adjustmentItemId,
                ClaimedById = actor, ClaimedAtUtc = DateTime.UtcNow, SystemQuantity = line.SystemQuantity,
                CountedQuantity = line.CountedQuantity, VarianceQuantity = line.VarianceQuantity
            });
        }
        await _unitOfWork.SaveChangesAsync();
    }
}
