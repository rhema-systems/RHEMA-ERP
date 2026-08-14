using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// UNIFIED CORRECTIVE-ACTION TRACKER (slice 14, FR-SHE-245).
//
// A union READ-MODEL over the live corrective-action silos:
//   1. SafetyIncidentCorrectiveAction   (incident investigations)
//   2. SafetyInspectionHazardAction     (workplace inspection findings)
//   3. SafetyEquipmentInspectionAction  (safety-equipment inspections)
//   4. SafetyMeetingActionItem          (committee meetings)
//   5. SheAuditFindingAction            (audit findings — added by slice 15)
//
// The tables stay exactly as ported — no schema consolidation. Each silo is
// projected server-side (no Includes — the navs are reached inside the Select,
// so none of the 8060-byte wide-row risk), unioned in memory and normalised
// onto one status/priority vocabulary.
//
// Deliberately NOT a source: SheHazardCorrectiveAction. Despite the name it is
// a hazard→template configuration link (deadline-days, mandatory flag, display
// order) with no status, assignee or due date — there is nothing to track.
//
// Overdue-ness is recomputed from the due date on every read. The silos' stored
// "Overdue" statuses are display residue — nothing maintains them reliably, so
// the tracker treats them as open work and derives the flag itself.
// ============================================================================

public class SheCorrectiveActionTrackerService : ISheCorrectiveActionTrackerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SheCorrectiveActionTrackerService> _logger;

    public SheCorrectiveActionTrackerService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<SheCorrectiveActionTrackerService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Reads scope to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<IEnumerable<SheUnifiedCorrectiveActionDto>> GetAllAsync(
        SheCorrectiveActionSource? source = null,
        SheUnifiedActionStatus? status = null,
        Guid? assignedToId = null,
        bool overdueOnly = false,
        DateTime? dueFrom = null,
        DateTime? dueTo = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryUnionAsync(GetTenantId(), source, cancellationToken);

        IEnumerable<SheUnifiedCorrectiveActionDto> filtered = rows;
        if (status != null) filtered = filtered.Where(r => r.Status == status);
        if (assignedToId != null) filtered = filtered.Where(r => r.AssignedToId == assignedToId);
        if (overdueOnly) filtered = filtered.Where(r => r.IsOverdue);
        if (dueFrom != null) filtered = filtered.Where(r => r.DueDate != null && r.DueDate >= dueFrom.Value.Date);
        if (dueTo != null) filtered = filtered.Where(r => r.DueDate != null && r.DueDate <= dueTo.Value.Date);

        // Overdue first (deepest first), then nearest due date, then newest.
        return filtered
            .OrderByDescending(r => r.IsOverdue)
            .ThenByDescending(r => r.DaysOverdue ?? 0)
            .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<SheUnifiedCorrectiveActionSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await QueryUnionAsync(GetTenantId(), null, cancellationToken);
        var today = DateTime.UtcNow.Date;

        var summary = new SheUnifiedCorrectiveActionSummaryDto
        {
            Total = rows.Count,
            Open = rows.Count(r => r.Status == SheUnifiedActionStatus.Open),
            InProgress = rows.Count(r => r.Status == SheUnifiedActionStatus.InProgress),
            Completed = rows.Count(r => r.Status == SheUnifiedActionStatus.Completed),
            Cancelled = rows.Count(r => r.Status == SheUnifiedActionStatus.Cancelled),
            Overdue = rows.Count(r => r.IsOverdue),
            OverdueTier1 = rows.Count(r => r.EscalationTier == 1),
            OverdueTier2 = rows.Count(r => r.EscalationTier == 2),
            OverdueTier3 = rows.Count(r => r.EscalationTier == 3),
            DueWithin7Days = rows.Count(r => !r.IsOverdue && IsOpen(r.Status) &&
                r.DueDate != null && r.DueDate.Value.Date >= today && r.DueDate.Value.Date <= today.AddDays(7)),
            WithoutDueDate = rows.Count(r => IsOpen(r.Status) && r.DueDate == null),
        };

        foreach (var group in rows.GroupBy(r => r.Source))
        {
            summary.BySource[group.Key.ToString()] = new SheUnifiedCorrectiveActionSourceSummaryDto
            {
                Total = group.Count(),
                Open = group.Count(r => IsOpen(r.Status)),
                Overdue = group.Count(r => r.IsOverdue),
                Completed = group.Count(r => r.Status == SheUnifiedActionStatus.Completed),
            };
        }
        return summary;
    }

    public async Task<IReadOnlyList<SheUnifiedCorrectiveActionDto>> GetOpenForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => (await GetAllForTenantAsync(tenantId, cancellationToken)).Where(r => IsOpen(r.Status)).ToList();

    public async Task<IReadOnlyList<SheUnifiedCorrectiveActionDto>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to read the corrective-action tracker.");
        return await QueryUnionAsync(tenantId, null, cancellationToken);
    }

    private static bool IsOpen(SheUnifiedActionStatus status)
        => status is SheUnifiedActionStatus.Open or SheUnifiedActionStatus.InProgress;

    // ── the union ────────────────────────────────────────────────────────────

    /// <summary>
    /// Server-side projection shape shared by the four silo queries. Assignee
    /// names travel as parts — Employee.FullName is [NotMapped], so touching it
    /// in the projection would drag the whole Employee row into every query.
    /// </summary>
    private sealed record ActionRow(
        Guid Id,
        Guid ParentId,
        string ParentReference,
        string Description,
        int? Priority,
        int RawStatus,
        Guid? AssignedToId,
        string? AssignedFirstName,
        string? AssignedMiddleName,
        string? AssignedLastName,
        DateTime? DueDate,
        DateTime? CompletionDate,
        bool? EffectivenessVerified,
        DateTime CreatedAt);

    private static string? ComposeName(string? first, string? middle, string? last)
    {
        if (first == null && last == null) return null;
        return string.IsNullOrEmpty(middle) ? $"{first} {last}" : $"{first} {middle} {last}";
    }

    private async Task<List<SheUnifiedCorrectiveActionDto>> QueryUnionAsync(
        Guid tenantId, SheCorrectiveActionSource? source, CancellationToken cancellationToken)
    {
        var result = new List<SheUnifiedCorrectiveActionDto>();

        if (source is null or SheCorrectiveActionSource.Incident)
        {
            var rows = await _unitOfWork.Repository<SafetyIncidentCorrectiveAction>()
                .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && !a.Incident.IsDeleted)
                .Select(a => new ActionRow(
                    a.Id, a.IncidentId, a.Incident.IncidentNumber,
                    a.ActionDescription,
                    (int?)a.Priority, (int)a.Status,
                    (Guid?)a.ResponsiblePersonId,
                    a.ResponsiblePerson.FirstName, a.ResponsiblePerson.MiddleName, a.ResponsiblePerson.LastName,
                    (DateTime?)a.DueDate, a.CompletionDate,
                    (bool?)a.EffectivenessVerified, a.CreatedAt))
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(r => ToUnified(r, SheCorrectiveActionSource.Incident,
                $"/hr/safety/incidents/{r.ParentId}", MapCaStatus((SheCorrectiveActionStatus)r.RawStatus),
                ((SheCorrectiveActionStatus)r.RawStatus).ToString())));
        }

        if (source is null or SheCorrectiveActionSource.Inspection)
        {
            var rows = await _unitOfWork.Repository<SafetyInspectionHazardAction>()
                .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted &&
                                   !a.InspectionHazard.IsDeleted && !a.InspectionHazard.Inspection.IsDeleted)
                .Select(a => new ActionRow(
                    a.Id, a.InspectionHazard.InspectionId, a.InspectionHazard.Inspection.InspectionNumber,
                    a.CorrectiveActionTemplate.Title,
                    null, (int)a.Status,
                    a.AssignedToId,
                    a.AssignedTo != null ? a.AssignedTo.FirstName : null,
                    a.AssignedTo != null ? a.AssignedTo.MiddleName : null,
                    a.AssignedTo != null ? a.AssignedTo.LastName : null,
                    a.DueDate, a.CompletionDate,
                    null, a.CreatedAt))
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(r => ToUnified(r, SheCorrectiveActionSource.Inspection,
                $"/hr/safety/inspections/{r.ParentId}", MapCaStatus((SheCorrectiveActionStatus)r.RawStatus),
                ((SheCorrectiveActionStatus)r.RawStatus).ToString())));
        }

        if (source is null or SheCorrectiveActionSource.Equipment)
        {
            var rows = await _unitOfWork.Repository<SafetyEquipmentInspectionAction>()
                .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted &&
                                   !a.Inspection.IsDeleted && !a.Inspection.Equipment.IsDeleted)
                .Select(a => new ActionRow(
                    a.Id, a.Inspection.EquipmentId,
                    a.Inspection.Equipment.EquipmentNumber + " — " + a.Inspection.Equipment.Name,
                    a.CorrectiveActionTemplate.Title,
                    null, (int)a.Status,
                    a.AssignedToId,
                    a.AssignedTo != null ? a.AssignedTo.FirstName : null,
                    a.AssignedTo != null ? a.AssignedTo.MiddleName : null,
                    a.AssignedTo != null ? a.AssignedTo.LastName : null,
                    a.DueDate, a.CompletionDate,
                    null, a.CreatedAt))
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(r => ToUnified(r, SheCorrectiveActionSource.Equipment,
                $"/hr/safety/equipment/{r.ParentId}", MapCaStatus((SheCorrectiveActionStatus)r.RawStatus),
                ((SheCorrectiveActionStatus)r.RawStatus).ToString())));
        }

        if (source is null or SheCorrectiveActionSource.Audit)
        {
            var rows = await _unitOfWork.Repository<SheAuditFindingAction>()
                .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted &&
                                   !a.Finding.IsDeleted && !a.Finding.Audit.IsDeleted)
                .Select(a => new ActionRow(
                    a.Id, a.Finding.AuditId,
                    a.Finding.Audit.AuditNumber + " finding #" + a.Finding.FindingNumber,
                    a.CorrectiveActionTemplate.Title,
                    null, (int)a.Status,
                    a.AssignedToId,
                    a.AssignedTo != null ? a.AssignedTo.FirstName : null,
                    a.AssignedTo != null ? a.AssignedTo.MiddleName : null,
                    a.AssignedTo != null ? a.AssignedTo.LastName : null,
                    a.DueDate, a.CompletionDate,
                    null, a.CreatedAt))
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(r => ToUnified(r, SheCorrectiveActionSource.Audit,
                $"/hr/safety/audits/{r.ParentId}", MapCaStatus((SheCorrectiveActionStatus)r.RawStatus),
                ((SheCorrectiveActionStatus)r.RawStatus).ToString())));
        }

        if (source is null or SheCorrectiveActionSource.Committee)
        {
            var rows = await _unitOfWork.Repository<SafetyMeetingActionItem>()
                .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && !a.Meeting.IsDeleted)
                .Select(a => new ActionRow(
                    a.Id, a.MeetingId,
                    (a.Meeting.Committee != null ? a.Meeting.Committee.CommitteeName + " — " : "") + a.Meeting.MeetingNumber,
                    a.ActionDescription,
                    (int?)a.Priority, (int)a.Status,
                    a.AssignedToId,
                    a.AssignedTo != null ? a.AssignedTo.FirstName : null,
                    a.AssignedTo != null ? a.AssignedTo.MiddleName : null,
                    a.AssignedTo != null ? a.AssignedTo.LastName : null,
                    a.DueDate, a.CompletionDate,
                    null, a.CreatedAt))
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(r => ToUnified(r, SheCorrectiveActionSource.Committee,
                $"/hr/safety/committees/meetings/{r.ParentId}", MapItemStatus((SheActionItemStatus)r.RawStatus),
                ((SheActionItemStatus)r.RawStatus).ToString())));
        }

        return result;
    }

    /// <summary>Pending/Open → Open; stored Overdue counts as open work (the flag is recomputed); Verified counts as completed.</summary>
    private static SheUnifiedActionStatus MapCaStatus(SheCorrectiveActionStatus status) => status switch
    {
        SheCorrectiveActionStatus.Pending => SheUnifiedActionStatus.Open,
        SheCorrectiveActionStatus.InProgress => SheUnifiedActionStatus.InProgress,
        SheCorrectiveActionStatus.Completed => SheUnifiedActionStatus.Completed,
        SheCorrectiveActionStatus.Verified => SheUnifiedActionStatus.Completed,
        SheCorrectiveActionStatus.Overdue => SheUnifiedActionStatus.Open,
        SheCorrectiveActionStatus.Cancelled => SheUnifiedActionStatus.Cancelled,
        _ => SheUnifiedActionStatus.Open,
    };

    private static SheUnifiedActionStatus MapItemStatus(SheActionItemStatus status) => status switch
    {
        SheActionItemStatus.Open => SheUnifiedActionStatus.Open,
        SheActionItemStatus.InProgress => SheUnifiedActionStatus.InProgress,
        SheActionItemStatus.Completed => SheUnifiedActionStatus.Completed,
        SheActionItemStatus.Overdue => SheUnifiedActionStatus.Open,
        SheActionItemStatus.Cancelled => SheUnifiedActionStatus.Cancelled,
        _ => SheUnifiedActionStatus.Open,
    };

    private static SheUnifiedCorrectiveActionDto ToUnified(
        ActionRow r, SheCorrectiveActionSource source, string parentPath, SheUnifiedActionStatus status, string rawStatusName)
    {
        var today = DateTime.UtcNow.Date;
        var isOpen = status is SheUnifiedActionStatus.Open or SheUnifiedActionStatus.InProgress;
        var isOverdue = isOpen && r.DueDate != null && r.DueDate.Value.Date < today;
        var daysOverdue = isOverdue ? (today - r.DueDate!.Value.Date).Days : (int?)null;

        return new SheUnifiedCorrectiveActionDto
        {
            Id = r.Id,
            Source = source,
            ParentId = r.ParentId,
            ParentReference = r.ParentReference,
            ParentPath = parentPath,
            Description = r.Description,
            // SheActionItemPriority mirrors SheCorrectiveActionPriority value-for-value
            // (Critical=1 … Low=4), so the committee silo casts onto the CA vocabulary.
            Priority = r.Priority is int p ? (SheCorrectiveActionPriority)p : null,
            Status = status,
            RawStatus = rawStatusName,
            AssignedToId = r.AssignedToId,
            AssignedToName = ComposeName(r.AssignedFirstName, r.AssignedMiddleName, r.AssignedLastName),
            DueDate = r.DueDate,
            CompletionDate = r.CompletionDate,
            IsOverdue = isOverdue,
            DaysOverdue = daysOverdue,
            EscalationTier = daysOverdue is int d ? SheReminderLadder.EscalationTier(d) : 0,
            EffectivenessVerified = r.EffectivenessVerified,
            CreatedAt = r.CreatedAt,
        };
    }
}
