using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.MultiCurrency;

public sealed record ExchangeRateWorkflowReconciliationResult(
    int PendingCount,
    int OrphanCount,
    int RecoveredCount,
    int FailedCount,
    int SkippedWithoutInitiatorCount);

/// <summary>
/// Repairs only pending exchange-rate submissions that have no workflow history.
/// Existing completed, rejected, cancelled, or active workflows are never reopened.
/// </summary>
public sealed class ExchangeRateWorkflowReconciliationService(
    ApplicationDbContext db,
    IWorkflowService workflowService,
    ILogger<ExchangeRateWorkflowReconciliationService> logger)
{
    public async Task<ExchangeRateWorkflowReconciliationResult> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        var exchangeRateEntityTypes = db.WorkflowEntityTypes
            .AsNoTracking()
            .Where(entityType => !entityType.IsDeleted && entityType.IsActive &&
                (entityType.Code == "ExchangeRate" ||
                 entityType.Name == "ExchangeRate" ||
                 entityType.Name == "Exchange Rate"));

        if (!await exchangeRateEntityTypes.AnyAsync(cancellationToken))
        {
            return new ExchangeRateWorkflowReconciliationResult(0, 0, 0, 0, 0);
        }

        var configuredTenantIds = exchangeRateEntityTypes.Select(entityType => entityType.TenantId);
        var pendingRateQuery = db.ExchangeRates
            .AsNoTracking()
            .Where(rate => !rate.IsDeleted && rate.IsActive &&
                rate.ApprovalStatus == RateApprovalStatus.Pending &&
                configuredTenantIds.Contains(rate.TenantId));
        var pendingCount = await pendingRateQuery.CountAsync(cancellationToken);
        var orphanRates = await pendingRateQuery
            .Where(rate => !db.WorkflowInstances
                .AsNoTracking()
                .Any(instance => !instance.IsDeleted &&
                    instance.TenantId == rate.TenantId &&
                    instance.EntityId == rate.Id &&
                    exchangeRateEntityTypes.Any(entityType =>
                        entityType.Id == instance.EntityTypeId &&
                        entityType.TenantId == rate.TenantId)))
            .OrderBy(rate => rate.CreatedDate)
            .Select(rate => new { rate.Id, rate.TenantId, rate.CreatedByUserId })
            .ToListAsync(cancellationToken);

        var recovered = 0;
        var failed = 0;
        var skippedWithoutInitiator = 0;

        foreach (var rate in orphanRates)
        {
            if (rate.CreatedByUserId == Guid.Empty)
            {
                skippedWithoutInitiator++;
                logger.LogWarning(
                    "Pending exchange rate {ExchangeRateId} for tenant {TenantId} has no workflow initiator and was not reconciled",
                    rate.Id,
                    rate.TenantId);
                continue;
            }

            try
            {
                var result = await workflowService.StartApprovalWorkflowAsAsync(
                    "ExchangeRate",
                    rate.Id,
                    rate.CreatedByUserId,
                    rate.TenantId);
                if (result.Success)
                {
                    recovered++;
                    logger.LogInformation(
                        "Recovered orphaned exchange-rate workflow {WorkflowInstanceId} for rate {ExchangeRateId}",
                        result.WorkflowInstanceId,
                        rate.Id);
                }
                else
                {
                    failed++;
                    logger.LogError(
                        "Could not recover orphaned exchange-rate workflow for rate {ExchangeRateId}: {Message}",
                        rate.Id,
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(
                    ex,
                    "Could not recover orphaned exchange-rate workflow for rate {ExchangeRateId}",
                    rate.Id);
            }
        }

        return new ExchangeRateWorkflowReconciliationResult(
            pendingCount,
            orphanRates.Count,
            recovered,
            failed,
            skippedWithoutInitiator);
    }
}
