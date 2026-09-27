using System.Data;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>System-only scheduler entry point. Closure ends submissions; it never opens bids or changes statutory opening evidence.</summary>
public sealed class ProcurementTenderClosingProcessor(IUnitOfWork unitOfWork, IProcurementControlEventService events)
{
    public async Task<int> ProcessTenantAsync(Guid tenantId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The closing clock must be UTC.", nameof(nowUtc));
        if (unitOfWork.HasActiveTransaction) throw new InvalidOperationException("Scheduled tender closing must own its transaction.");

        return await unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await unitOfWork.AcquireTransactionLockAsync($"procurement-tender-close:{tenantId:N}", cancellationToken);
                var enabled = await unitOfWork.Repository<ProcurementSettings>().GetQueryable()
                    .IgnoreQueryFilters().AnyAsync(settings => settings.TenantId == tenantId && !settings.IsDeleted && settings.AutoCloseTenders, cancellationToken);
                if (!enabled)
                {
                    await unitOfWork.CommitAsync(cancellationToken);
                    return 0;
                }

                var due = await unitOfWork.Repository<Tender>().GetQueryable().IgnoreQueryFilters()
                    .Where(tender => tender.TenantId == tenantId && !tender.IsDeleted && tender.Status == "Published" &&
                                     tender.SubmissionDeadline.HasValue && tender.SubmissionDeadline <= nowUtc)
                    .OrderBy(tender => tender.SubmissionDeadline).ThenBy(tender => tender.Id)
                    .Take(100).ToListAsync(cancellationToken);
                foreach (var tender in due)
                {
                    tender.Status = "Closed";
                    tender.UpdatedAt = nowUtc;
                    tender.UpdatedBy = "System (automatic tender closing)";
                    await events.RecordSystemAsync(tenantId, "Procurement tender scheduler", new ProcurementControlEventWriteRequest
                    {
                        EventKey = ProcurementControlEventKey.Create("tender-auto-close", tenantId, tender.Id, tender.SubmissionDeadline!.Value.Ticks.ToString()),
                        EventType = "TenderAutomaticClosure", Action = "Close", Result = ProcurementControlEventResult.Allowed,
                        SourceType = "Tender", SourceId = tender.Id, SourceReference = tender.TenderNumber,
                        Reason = "The submission deadline was reached and automatic tender closing is enabled.",
                        Before = new { Status = "Published" }, After = new { Status = "Closed" },
                        InputValues = new { tender.SubmissionDeadline, AutoCloseTenders = true },
                        CorrelationId = $"auto-close-{tender.Id:N}", OccurredAtUtc = nowUtc
                    }, cancellationToken);
                }
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
                return due.Count;
            }
            catch
            {
                if (unitOfWork.HasActiveTransaction) await unitOfWork.RollbackAsync();
                unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }
}
