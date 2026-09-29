using System.Data;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class PhysicalCountService
{
    public static void EnsureCounterCanEdit(PhysicalCount count, Guid userId)
    {
        if (!IsAssignedCounter(count, userId)) throw new InvalidOperationException("Only the assigned counter may correct or submit this count.");
        if (count.Status is not ("InProgress" or "UnderReview"))
            throw new InvalidOperationException("Quantities are locked after submission. Resume review after an investigation decision to make corrections.");
    }

    private async Task<bool> InCountTransactionAsync(Guid countId, Func<Task<bool>> operation)
    {
        if (_unitOfWork.HasActiveTransaction) return await operation();
        return await _unitOfWork.ExecuteInStrategyAsync(async () => {
            _unitOfWork.ClearTrackedChanges();
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try {
                var rootId = await _countRepository.GetQueryable(x => x.Id == countId && x.TenantId == RequiredTenantId() && !x.IsDeleted)
                    .Select(x => x.RootPhysicalCountId ?? x.Id).SingleOrDefaultAsync();
                await _unitOfWork.AcquireTransactionLockAsync($"physical-count:{RequiredTenantId():N}:{(rootId == Guid.Empty ? countId : rootId):N}");
                var result = await operation();
                await _unitOfWork.CommitAsync();
                return result;
            } catch {
                // CommitAsync may already have rolled back and disposed its failed
                // transaction. Preserve that original error instead of replacing it
                // with "No transaction to rollback".
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    public Task<bool> ReviewCountAsync(Guid countId, Guid userId, PhysicalCountMutationRequest request) =>
        InCountTransactionAsync(countId, async () => {
            EnsureActor(userId);
            var count = await LoadControlledCountAsync(countId) ?? throw new ArgumentException("Count not found.");
            await EnsureAccessAsync(count, "procurement.inventory.count");
            if (!IsAssignedCounter(count, userId)) throw new InvalidOperationException("Only the assigned counter may review this count.");
            await EnsureCurrentCounterIdentityAsync(count, userId);
            if (await ReplayCountActionAsync(countId, PhysicalCountActionType.ReviewStarted, request.IdempotencyKey,
                    userId, "Counter", request.Comment, new { ReviewRevision = request.RowVersion })) return true;
            EnsureRowVersion(count.RowVersion, request.RowVersion, "The count changed. Refresh before reviewing.");
            if (count.Status is not ("InProgress" or "RecountRequired" or "UnderInvestigation"))
                throw new InvalidOperationException("This count is not available for review.");
            if (count.Items.Count == 0 || count.Items.Any(i => !i.IsCounted))
                throw new InvalidOperationException("Save a counted quantity for every item before reviewing the variance.");
            if (count.StockAdjustmentId.HasValue)
                throw new InvalidOperationException("An active adjustment still exists. Resolve its approval before reviewing.");
            if (count.Status == "UnderInvestigation")
                Required(request.Comment, "Record the investigation findings before resuming review.", 2000);
            var previousStatus = count.Status;
            count.Status = "UnderReview";
            // Recount selections belong to their retained child sheets and must not be reset during review.
            await AddCountActionAsync(count, PhysicalCountActionType.ReviewStarted, userId, request.IdempotencyKey,
                request.Comment, new { ReviewRevision = request.RowVersion, PreviousStatus = previousStatus }, "Counter", request.CorrelationId);
            await _countRepository.UpdateAsync(count);
            await _unitOfWork.SaveChangesAsync();
            return true;
        });

    public Task<bool> SubmitReviewedCountAsync(Guid countId, Guid userId, PhysicalCountMutationRequest request) =>
        InCountTransactionAsync(countId, () => SubmitReviewedCoreAsync(countId, userId, request));

    private async Task<PhysicalCountDecisionOption> ResolveConfiguredDecisionAsync(PhysicalCountDecisionRequest request)
    {
        var setting = await _unitOfWork.Repository<SystemSettings>().GetQueryable(s =>
            s.TenantId == RequiredTenantId() && s.Key == PhysicalCountDecisionPolicy.SettingKey && !s.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync();
        var decision = PhysicalCountDecisionPolicy.Resolve(PhysicalCountDecisionPolicy.Read(setting?.Value),
            request.DecisionCode, request.DecisionRevision);
        if (request.Approved != (decision.Effect == PhysicalCountDecisionPolicy.ApproveAdjustment))
            throw new InvalidOperationException("The selected decision does not match its configured effect. Refresh and retry.");
        return decision;
    }
}
