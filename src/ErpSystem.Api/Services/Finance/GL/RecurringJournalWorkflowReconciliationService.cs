using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed record RecurringJournalWorkflowReconciliationResult(
    int PendingCount,
    int OrphanCount,
    int RecoveredCount,
    int FailedCount,
    int SkippedWithoutInitiatorCount);

/// <summary>
/// Repairs only pending recurring-journal templates that have no workflow history.
/// Existing completed, rejected, cancelled, or active workflows are never reopened.
/// Occurrence submission failures are recovered by the recurring-journal processor.
/// </summary>
public sealed class RecurringJournalWorkflowReconciliationService(
    ApplicationDbContext db,
    IWorkflowService workflowService,
    ILogger<RecurringJournalWorkflowReconciliationService> logger)
{
    public async Task<RecurringJournalWorkflowReconciliationResult> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        const string entityCode = nameof(RecurringJournalTemplate);
        var entityTypes = db.WorkflowEntityTypes
            .AsNoTracking()
            .Where(entityType => !entityType.IsDeleted && entityType.IsActive &&
                (entityType.Code == entityCode ||
                 entityType.Name == entityCode ||
                 entityType.Name == "Recurring Journal Template"));

        if (!await entityTypes.AnyAsync(cancellationToken))
            return new RecurringJournalWorkflowReconciliationResult(0, 0, 0, 0, 0);

        var configuredTenantIds = entityTypes.Select(entityType => entityType.TenantId);
        var pendingQuery = db.RecurringJournalTemplates
            .AsNoTracking()
            .Where(template => !template.IsDeleted &&
                template.Status == RecurringJournalStatus.PendingApproval &&
                configuredTenantIds.Contains(template.TenantId));
        var pendingCount = await pendingQuery.CountAsync(cancellationToken);
        var orphans = await pendingQuery
            .Where(template => !db.WorkflowInstances
                .AsNoTracking()
                .Any(instance => !instance.IsDeleted &&
                    instance.TenantId == template.TenantId &&
                    instance.EntityId == template.Id &&
                    entityTypes.Any(entityType =>
                        entityType.Id == instance.EntityTypeId &&
                        entityType.TenantId == template.TenantId)))
            .OrderBy(template => template.SubmittedAt ?? template.CreatedAt)
            .Select(template => new
            {
                template.Id,
                template.TenantId,
                InitiatorId = template.SubmittedByUserId ?? template.CreatedById
            })
            .ToListAsync(cancellationToken);

        var recovered = 0;
        var failed = 0;
        var skippedWithoutInitiator = 0;
        foreach (var template in orphans)
        {
            if (!template.InitiatorId.HasValue || template.InitiatorId == Guid.Empty)
            {
                skippedWithoutInitiator++;
                logger.LogWarning(
                    "Pending recurring-journal template {TemplateId} for tenant {TenantId} has no durable workflow initiator and was not reconciled",
                    template.Id,
                    template.TenantId);
                continue;
            }

            try
            {
                var result = await workflowService.StartApprovalWorkflowAsAsync(
                    entityCode,
                    template.Id,
                    template.InitiatorId.Value,
                    template.TenantId);
                if (result.Success && result.WorkflowInstanceId.HasValue)
                {
                    recovered++;
                    logger.LogInformation(
                        "Recovered orphaned recurring-journal workflow {WorkflowInstanceId} for template {TemplateId}",
                        result.WorkflowInstanceId,
                        template.Id);
                }
                else
                {
                    failed++;
                    logger.LogError(
                        "Could not recover orphaned recurring-journal workflow for template {TemplateId}: {Message}",
                        template.Id,
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(
                    ex,
                    "Could not recover orphaned recurring-journal workflow for template {TemplateId}",
                    template.Id);
            }
        }

        return new RecurringJournalWorkflowReconciliationResult(
            pendingCount,
            orphans.Count,
            recovered,
            failed,
            skippedWithoutInitiator);
    }
}
