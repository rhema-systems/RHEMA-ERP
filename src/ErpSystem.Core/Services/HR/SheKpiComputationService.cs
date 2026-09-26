using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE KPI COMPUTATION (slice 14, FR-SHE-248/230/232, FR-CON-001).
//
// Turns the hand-reported ShePerformanceSnapshot into a computed one: every
// figure derivable from live SHE data is recomputed for the snapshot's period;
// the genuinely hand-entered inputs stay untouched:
//   - TotalManHoursWorked   (no worked-hours source in scope — payroll-adjacent)
//   - InspectionsPlanned    (no inspection plan/schedule entity exists)
//   - EmergencyDrillsPlanned (drills carry only a next-drill pointer, no plan count)
//   - PpeComplianceRate     (preserved while the PPE requirement matrix's
//                            job-role codes match no position codes — the
//                            computation self-activates once they do)
//
// Formula bases (recorded here because the FRD names the KPIs without bases):
//   LTIFR = lost-time injuries × 1,000,000 / man-hours   (per-million, ILO style)
//   TRIR  = recordables (accidents + occupational illness) × 200,000 / man-hours
//           (OSHA base — the name "Total Recordable Incident Rate" is OSHA's)
//   Near-miss frequency rate = near misses × 1,000,000 / man-hours (LTIFR base)
//
// Period scoping: monthly / quarterly / annual windows on each record's own
// event date. When the snapshot names a location, location-bearing records are
// filtered to it; corrective actions (silo rows carry no location), PPE and the
// regulatory register are tenant-wide, and the regulatory + PPE figures are
// point-in-time (the register has no history to reconstruct a past period from).
// ============================================================================

public class SheKpiComputationService : ISheKpiComputationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISheCorrectiveActionTrackerService _trackerService;
    private readonly IShePerformanceSnapshotRepository _snapshotRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SheKpiComputationService> _logger;

    public SheKpiComputationService(
        IUnitOfWork unitOfWork,
        ISheCorrectiveActionTrackerService trackerService,
        IShePerformanceSnapshotRepository snapshotRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<SheKpiComputationService> logger)
    {
        _unitOfWork = unitOfWork;
        _trackerService = trackerService;
        _snapshotRepository = snapshotRepository;
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

    private static (DateTime Start, DateTime End) PeriodWindow(SheSnapshotPeriodType periodType, int year, int? periodNumber)
    {
        switch (periodType)
        {
            case SheSnapshotPeriodType.Monthly:
                if (periodNumber is not (>= 1 and <= 12))
                    throw new InvalidOperationException("A monthly period needs a period number between 1 and 12.");
                var monthStart = new DateTime(year, periodNumber.Value, 1);
                return (monthStart, monthStart.AddMonths(1));
            case SheSnapshotPeriodType.Quarterly:
                if (periodNumber is not (>= 1 and <= 4))
                    throw new InvalidOperationException("A quarterly period needs a period number between 1 and 4.");
                var quarterStart = new DateTime(year, (periodNumber.Value - 1) * 3 + 1, 1);
                return (quarterStart, quarterStart.AddMonths(3));
            case SheSnapshotPeriodType.Annual:
                return (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
            default:
                throw new InvalidOperationException($"Unknown snapshot period type '{periodType}'.");
        }
    }

    private static decimal? Rate(int numerator, long manHours, int per)
        => manHours > 0 ? Math.Round((decimal)numerator * per / manHours, 2) : null;

    private static decimal? Percent(int part, int whole)
        => whole > 0 ? Math.Round((decimal)part * 100 / whole, 2) : null;

    // ── preview / compute ────────────────────────────────────────────────────

    public async Task<SheComputedKpisDto> PreviewAsync(
        SheSnapshotPeriodType periodType, int year, int? periodNumber, Guid? locationId,
        long manHours = 0, CancellationToken cancellationToken = default)
        => await ComputeAsync(GetTenantId(), periodType, year, periodNumber, locationId, manHours, cancellationToken);

    public async Task<ShePerformanceSnapshotDto> ComputeSnapshotAsync(Guid snapshotId, Guid computedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var snapshot = await _snapshotRepository.GetByIdAsync(snapshotId);
        if (snapshot == null || snapshot.TenantId != tenantId)
            throw new ArgumentException($"Performance snapshot with ID '{snapshotId}' not found.");

        // Same lock as the figure-edit path: a reviewed snapshot is the record
        // management signed off on — recomputing it would silently invalidate the review.
        if (snapshot.ReviewedById != null)
            throw new InvalidOperationException(
                $"Snapshot '{snapshot.SnapshotNumber}' has already been reviewed and is locked. Delete and re-enter it if the figures are wrong.");

        var k = await ComputeAsync(tenantId, snapshot.PeriodType, snapshot.Year, snapshot.PeriodNumber,
            snapshot.LocationId, snapshot.TotalManHoursWorked, cancellationToken);

        snapshot.TotalAccidents = k.TotalAccidents;
        snapshot.TotalIncidents = k.TotalIncidents;
        snapshot.TotalNearMisses = k.TotalNearMisses;
        snapshot.TotalDangerousOccurrences = k.TotalDangerousOccurrences;
        snapshot.TotalFatalities = k.TotalFatalities;
        snapshot.TotalLostTimeInjuries = k.TotalLostTimeInjuries;
        snapshot.TotalLostDays = k.TotalLostDays;
        snapshot.LostTimeInjuryFrequencyRate = k.LostTimeInjuryFrequencyRate;
        snapshot.TotalRecordableIncidentRate = k.TotalRecordableIncidentRate;
        snapshot.NearMissFrequencyRate = k.NearMissFrequencyRate;
        snapshot.InspectionsConducted = k.InspectionsConducted;
        snapshot.InspectionsOverdue = k.InspectionsOverdue;
        snapshot.AverageInspectionComplianceScore = k.AverageInspectionComplianceScore;
        snapshot.HousekeepingComplianceRating = k.HousekeepingComplianceRating;
        snapshot.CorrectiveActionsIssued = k.CorrectiveActionsIssued;
        snapshot.CorrectiveActionsCompleted = k.CorrectiveActionsCompleted;
        snapshot.CorrectiveActionsOverdue = k.CorrectiveActionsOverdue;
        snapshot.CorrectiveActionClosureRate = k.CorrectiveActionClosureRate;
        snapshot.TrainingProgramsPlanned = k.TrainingProgramsPlanned;
        snapshot.TrainingProgramsConducted = k.TrainingProgramsConducted;
        snapshot.TotalTrainingHours = k.TotalTrainingHours;
        snapshot.TrainingCompletionRate = k.TrainingCompletionRate;
        snapshot.ContractorsOnSite = k.ContractorsOnSite;
        snapshot.ContractorInspectionsConducted = k.ContractorInspectionsConducted;
        snapshot.ContractorNonComplianceNoticesIssued = k.ContractorNonComplianceNoticesIssued;
        snapshot.ContractorComplianceRate = k.ContractorComplianceRate;
        snapshot.EnvironmentalIncidents = k.EnvironmentalIncidents;
        snapshot.EnvironmentalIncidentsReportedToEpa = k.EnvironmentalIncidentsReportedToEpa;
        snapshot.WasteRecyclingRate = k.WasteRecyclingRate;
        snapshot.EmergencyDrillsConducted = k.EmergencyDrillsConducted;
        snapshot.FireDrillObjectivesMetRate = k.FireDrillObjectivesMetRate;
        if (k.PpeComplianceRate != null)
            snapshot.PpeComplianceRate = k.PpeComplianceRate;
        snapshot.RegulatoryObligationsTotal = k.RegulatoryObligationsTotal;
        snapshot.RegulatoryObligationsCompliant = k.RegulatoryObligationsCompliant;
        snapshot.RegulatoryObligationsNonCompliant = k.RegulatoryObligationsNonCompliant;
        snapshot.RegulatoryObligationsExpiringSoon = k.RegulatoryObligationsExpiringSoon;

        snapshot.KpisComputedAt = DateTime.UtcNow;
        snapshot.KpisComputedById = computedById;
        snapshot.UpdatedAt = DateTime.UtcNow;
        snapshot.UpdatedBy = computedById.ToString();

        await _snapshotRepository.UpdateAsync(snapshot);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Computed SHE KPIs into snapshot {Number} for tenant {TenantId}", snapshot.SnapshotNumber, tenantId);

        // Re-read through the include path so the response resolves names.
        var reread = await _snapshotRepository.GetByIdAsync(snapshot.Id,
            s => s.Location, s => s.PreparedBy, s => s.ReviewedBy, s => s.KpisComputedBy);
        return (reread ?? snapshot).ToDto();
    }

    private async Task<SheComputedKpisDto> ComputeAsync(
        Guid tenantId, SheSnapshotPeriodType periodType, int year, int? periodNumber, Guid? locationId,
        long manHours, CancellationToken ct)
    {
        var (start, end) = PeriodWindow(periodType, year, periodNumber);
        var result = new SheComputedKpisDto
        {
            PeriodType = periodType,
            Year = year,
            PeriodNumber = periodType == SheSnapshotPeriodType.Annual ? null : periodNumber,
            LocationId = locationId,
            PeriodStart = start,
            PeriodEnd = end.AddDays(-1),
            ManHoursUsed = manHours,
        };

        // ── Incidents ──
        var incidents = await _unitOfWork.Repository<SafetyIncident>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.IncidentDate >= start && i.IncidentDate < end &&
                               (locationId == null || i.LocationId == locationId))
            .Select(i => new
            {
                i.Category,
                i.IsLostTimeInjury,
                i.TotalLostDays,
                Fatalities = i.InvolvedPersons.Count(p => !p.IsDeleted && (p.IsFatal || p.InjuryClassification == SheInjuryClassification.Fatality)),
            })
            .ToListAsync(ct);

        result.TotalIncidents = incidents.Count;
        result.TotalAccidents = incidents.Count(i => i.Category == SheIncidentCategory.Accident);
        result.TotalNearMisses = incidents.Count(i => i.Category == SheIncidentCategory.NearMiss);
        result.TotalDangerousOccurrences = incidents.Count(i => i.Category == SheIncidentCategory.DangerousOccurrence);
        result.TotalFatalities = incidents.Sum(i => i.Fatalities);
        result.TotalLostTimeInjuries = incidents.Count(i => i.IsLostTimeInjury);
        result.TotalLostDays = incidents.Sum(i => i.TotalLostDays);

        var recordables = incidents.Count(i => i.Category is SheIncidentCategory.Accident or SheIncidentCategory.OccupationalIllness);
        result.LostTimeInjuryFrequencyRate = Rate(result.TotalLostTimeInjuries, manHours, 1_000_000);
        result.TotalRecordableIncidentRate = Rate(recordables, manHours, 200_000);
        result.NearMissFrequencyRate = Rate(result.TotalNearMisses, manHours, 1_000_000);

        // ── Inspections ──
        var inspections = await _unitOfWork.Repository<SafetyInspection>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.InspectionDate >= start && i.InspectionDate < end &&
                               (locationId == null || i.LocationId == locationId))
            .Select(i => new { i.Status, i.Category, i.ComplianceScore })
            .ToListAsync(ct);

        result.InspectionsConducted = inspections.Count(i =>
            i.Status is not SheInspectionStatus.Scheduled and not SheInspectionStatus.Overdue);
        result.InspectionsOverdue = inspections.Count(i => i.Status == SheInspectionStatus.Overdue);

        var scored = inspections.Where(i => i.ComplianceScore != null).Select(i => i.ComplianceScore!.Value).ToList();
        result.AverageInspectionComplianceScore = scored.Count > 0 ? Math.Round((decimal)scored.Average(), 2) : null;

        var housekeeping = inspections
            .Where(i => i.Category == SheInspectionCategory.Housekeeping && i.ComplianceScore != null)
            .Select(i => i.ComplianceScore!.Value).ToList();
        result.HousekeepingComplianceRating = housekeeping.Count > 0 ? Math.Round((decimal)housekeeping.Average(), 2) : null;

        // ── Corrective actions (union tracker; tenant-wide — silo rows carry no location) ──
        // Issued = created in the period. Completed = of those, completed (whenever).
        // Overdue = due in the period and still open now. Closure % = completed / issued.
        var allActions = await _trackerService.GetAllForTenantAsync(tenantId, ct);
        var issued = allActions.Where(a => a.CreatedAt >= start && a.CreatedAt < end).ToList();
        result.CorrectiveActionsIssued = issued.Count;
        result.CorrectiveActionsCompleted = issued.Count(a => a.Status == SheUnifiedActionStatus.Completed);
        result.CorrectiveActionsOverdue = allActions.Count(a =>
            a.IsOverdue && a.DueDate != null && a.DueDate.Value >= start && a.DueDate.Value < end);
        result.CorrectiveActionClosureRate = Percent(result.CorrectiveActionsCompleted, result.CorrectiveActionsIssued);

        // ── Training ──
        var programs = await _unitOfWork.Repository<SheTrainingProgram>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted &&
                               (locationId == null || p.LocationId == locationId))
            .Select(p => new
            {
                p.Status,
                p.ScheduledDate,
                p.ActualDate,
                p.DurationMinutes,
                EmployeeRegistered = p.Attendances.Count(a => !a.IsDeleted && a.IsEmployee),
                EmployeeAttended = p.Attendances.Count(a => !a.IsDeleted && a.IsEmployee && a.Attended),
            })
            .ToListAsync(ct);

        bool InWindow(DateTime? d) => d != null && d.Value >= start && d.Value < end;
        var planned = programs.Where(p => p.Status != SheTrainingStatus.Cancelled && InWindow(p.ScheduledDate ?? p.ActualDate)).ToList();
        var conducted = programs.Where(p => p.Status == SheTrainingStatus.Completed && InWindow(p.ActualDate ?? p.ScheduledDate)).ToList();
        result.TrainingProgramsPlanned = planned.Count;
        result.TrainingProgramsConducted = conducted.Count;
        result.TotalTrainingHours = (int)Math.Round(conducted.Sum(p => p.DurationMinutes) / 60.0);
        result.TrainingCompletionRate = Percent(conducted.Sum(p => p.EmployeeAttended), conducted.Sum(p => p.EmployeeRegistered));

        // ── Contractors ──
        result.ContractorsOnSite = await _unitOfWork.Repository<SheContractor>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .CountAsync(ct);

        var contractorInspections = await _unitOfWork.Repository<SheContractorInspection>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && !i.Contractor.IsDeleted &&
                               i.InspectionDate >= start && i.InspectionDate < end &&
                               (locationId == null || i.LocationId == locationId))
            .Select(i => new { i.ComplianceScore })
            .ToListAsync(ct);
        result.ContractorInspectionsConducted = contractorInspections.Count;
        var contractorScored = contractorInspections.Where(i => i.ComplianceScore != null).Select(i => i.ComplianceScore!.Value).ToList();
        result.ContractorComplianceRate = contractorScored.Count > 0 ? Math.Round((decimal)contractorScored.Average(), 2) : null;

        result.ContractorNonComplianceNoticesIssued = await _unitOfWork.Repository<SheContractorNonCompliance>()
            .GetQueryable(n => n.TenantId == tenantId && !n.IsDeleted && !n.Contractor.IsDeleted &&
                               n.IssuedDate >= start && n.IssuedDate < end)
            .CountAsync(ct);

        // ── Environment ──
        var envIncidents = await _unitOfWork.Repository<SheEnvironmentalIncident>()
            .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted &&
                               e.IncidentDate >= start && e.IncidentDate < end &&
                               (locationId == null || e.LocationId == locationId))
            .Select(e => new { e.ReportedToEpa })
            .ToListAsync(ct);
        result.EnvironmentalIncidents = envIncidents.Count;
        result.EnvironmentalIncidentsReportedToEpa = envIncidents.Count(e => e.ReportedToEpa);

        // Recycling rate is mass-based: kilogram and tonne records only (mixed units are
        // incommensurable — litres of effluent cannot be added to kilograms of scrap).
        // Diverted = recycling + composting.
        var disposals = await _unitOfWork.Repository<SheWasteDisposalRecord>()
            .GetQueryable(w => w.TenantId == tenantId && !w.IsDeleted &&
                               w.DisposalDate >= start && w.DisposalDate < end &&
                               (locationId == null || w.LocationId == locationId) &&
                               (w.Unit == SheWasteMeasurementUnit.Kilograms || w.Unit == SheWasteMeasurementUnit.Tonnes))
            .Select(w => new { w.Quantity, w.Unit, w.DisposalMethod })
            .ToListAsync(ct);
        var totalMass = disposals.Sum(w => w.Unit == SheWasteMeasurementUnit.Tonnes ? w.Quantity * 1000 : w.Quantity);
        var divertedMass = disposals
            .Where(w => w.DisposalMethod is SheWasteDisposalMethod.Recycling or SheWasteDisposalMethod.Composting)
            .Sum(w => w.Unit == SheWasteMeasurementUnit.Tonnes ? w.Quantity * 1000 : w.Quantity);
        result.WasteRecyclingRate = totalMass > 0 ? Math.Round(divertedMass * 100 / totalMass, 2) : null;

        // ── Emergency drills ──
        var drills = await _unitOfWork.Repository<EmergencyDrill>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted &&
                               d.DrillDate >= start && d.DrillDate < end &&
                               (locationId == null || d.LocationId == locationId))
            .Select(d => new { d.ObjectivesMet })
            .ToListAsync(ct);
        result.EmergencyDrillsConducted = drills.Count;
        result.FireDrillObjectivesMetRate = Percent(drills.Count(d => d.ObjectivesMet), drills.Count);

        // ── PPE compliance (tenant-wide, point-in-time; self-activating) ──
        var (ppeRate, assessed) = await ComputePpeComplianceAsync(tenantId, ct);
        result.PpeComplianceRate = ppeRate;
        result.PpeEmployeesAssessed = assessed;

        // ── Regulatory register (tenant-wide, point-in-time) ──
        var obligations = await _unitOfWork.Repository<SheRegulatoryObligation>()
            .GetQueryable(o => o.TenantId == tenantId && !o.IsDeleted && o.IsActive)
            .Select(o => new { o.ComplianceStatus, o.NextReviewDate })
            .ToListAsync(ct);
        var soonCutoff = DateTime.UtcNow.Date.AddDays(30);
        result.RegulatoryObligationsTotal = obligations.Count;
        result.RegulatoryObligationsCompliant = obligations.Count(o => o.ComplianceStatus == SheComplianceStatus.Compliant);
        result.RegulatoryObligationsNonCompliant = obligations.Count(o => o.ComplianceStatus == SheComplianceStatus.NonCompliant);
        result.RegulatoryObligationsExpiringSoon = obligations.Count(o => o.NextReviewDate != null && o.NextReviewDate.Value.Date <= soonCutoff);

        return result;
    }

    /// <summary>
    /// % of active employees whose position code appears in the PPE requirement
    /// matrix and who hold an un-returned, un-expired issuance of every mandatory
    /// required type. Null (self-deactivated) when no requirement row matches any
    /// position code — the matrix's JobRoleCode is free text with no FK.
    /// </summary>
    private async Task<(decimal? Rate, int Assessed)> ComputePpeComplianceAsync(Guid tenantId, CancellationToken ct)
    {
        var requirements = await _unitOfWork.Repository<JobRolePpeRequirement>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.IsMandatory)
            .Select(r => new { r.JobRoleCode, r.PpeTypeId })
            .ToListAsync(ct);
        if (requirements.Count == 0) return (null, 0);

        var requiredByRole = requirements
            .GroupBy(r => r.JobRoleCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(r => r.PpeTypeId).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

        // Everybody at work — Active or on probation (HR finish plan lane 11; TDC's call of
        // 2026-09-23: probation is a contract status, not an availability). ⚠ It was Active only,
        // so a new hire — the person most likely to be missing PPE — was outside the figure.
        var employees = await _unitOfWork.Repository<Employee>()
            .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive
                               && (e.StaffStatus == StaffStatus.Active || e.StaffStatus == StaffStatus.Probation))
            .Select(e => new { e.Id, PositionCode = e.Position.Code })
            .ToListAsync(ct);

        var covered = employees
            .Where(e => !string.IsNullOrWhiteSpace(e.PositionCode) && requiredByRole.ContainsKey(e.PositionCode.Trim()))
            .ToList();
        if (covered.Count == 0) return (null, 0);

        var today = DateTime.UtcNow.Date;
        var coveredIds = covered.Select(e => e.Id).ToList();
        var holdings = await _unitOfWork.Repository<PpeIssuance>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && !i.IsReturned &&
                               coveredIds.Contains(i.EmployeeId) &&
                               (i.ExpiryDate == null || i.ExpiryDate >= today))
            .Select(i => new { i.EmployeeId, i.PpeTypeId })
            .ToListAsync(ct);
        var held = holdings
            .GroupBy(h => h.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Select(h => h.PpeTypeId).ToHashSet());

        var compliant = covered.Count(e =>
        {
            var required = requiredByRole[e.PositionCode.Trim()];
            return held.TryGetValue(e.Id, out var owned) && required.All(owned.Contains);
        });

        return (Percent(compliant, covered.Count), covered.Count);
    }

    // ── departmental compliance (FR-SHE-230) ─────────────────────────────────

    public async Task<IEnumerable<SheDepartmentalComplianceDto>> GetDepartmentalComplianceAsync(
        SheSnapshotPeriodType periodType, int year, int? periodNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var (start, end) = PeriodWindow(periodType, year, periodNumber);

        var inspections = await _unitOfWork.Repository<SafetyInspection>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.InspectionDate >= start && i.InspectionDate < end)
            .Select(i => new
            {
                i.OrganizationUnitId,
                UnitName = i.OrganizationUnit != null ? i.OrganizationUnit.Name : null,
                i.Status,
                i.ComplianceScore,
                OpenFindings = i.Hazards.Count(h => !h.IsDeleted &&
                    h.Status != SheHazardStatus.Resolved && h.Status != SheHazardStatus.Closed),
            })
            .ToListAsync(cancellationToken);

        var incidents = await _unitOfWork.Repository<SafetyIncident>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               i.IncidentDate >= start && i.IncidentDate < end)
            .Select(i => new
            {
                i.OrganizationUnitId,
                UnitName = i.OrganizationUnit != null ? i.OrganizationUnit.Name : null,
                i.IsLostTimeInjury,
            })
            .ToListAsync(cancellationToken);

        // Keyed by Guid.Empty for the "no organization unit" bucket — Dictionary refuses null keys.
        var units = new Dictionary<Guid, SheDepartmentalComplianceDto>();
        SheDepartmentalComplianceDto UnitRow(Guid? id, string? name)
        {
            var key = id ?? Guid.Empty;
            if (!units.TryGetValue(key, out var row))
            {
                row = new SheDepartmentalComplianceDto
                {
                    OrganizationUnitId = id,
                    OrganizationUnitName = name ?? "(no organization unit)",
                };
                units[key] = row;
            }
            return row;
        }

        var scores = new Dictionary<Guid, List<int>>();
        foreach (var i in inspections)
        {
            var row = UnitRow(i.OrganizationUnitId, i.UnitName);
            if (i.Status is not SheInspectionStatus.Scheduled and not SheInspectionStatus.Overdue)
                row.InspectionsConducted++;
            row.OpenInspectionFindings += i.OpenFindings;
            if (i.ComplianceScore != null)
            {
                row.InspectionsScored++;
                var key = i.OrganizationUnitId ?? Guid.Empty;
                if (!scores.TryGetValue(key, out var list)) scores[key] = list = new List<int>();
                list.Add(i.ComplianceScore.Value);
            }
        }
        foreach (var i in incidents)
        {
            var row = UnitRow(i.OrganizationUnitId, i.UnitName);
            row.Incidents++;
            if (i.IsLostTimeInjury) row.LostTimeInjuries++;
        }
        foreach (var (unitKey, list) in scores)
            units[unitKey].AverageComplianceScore = Math.Round((decimal)list.Average(), 2);

        return units.Values
            .OrderByDescending(u => u.AverageComplianceScore ?? -1)
            .ThenBy(u => u.OrganizationUnitName)
            .ToList();
    }

    // ── contractor ranking (FR-CON-001) ──────────────────────────────────────

    public async Task<IEnumerable<SheContractorRankingDto>> GetContractorRankingAsync(
        SheSnapshotPeriodType periodType, int year, int? periodNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var (start, end) = PeriodWindow(periodType, year, periodNumber);

        var contractors = await _unitOfWork.Repository<SheContractor>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .Select(c => new
            {
                c.Id,
                c.ContractorCode,
                c.CompanyName,
                c.PreQualificationScore,
                Inspections = c.SheInspections
                    .Where(i => !i.IsDeleted && i.InspectionDate >= start && i.InspectionDate < end)
                    .Select(i => new { i.ComplianceScore })
                    .ToList(),
                NoticesIssued = c.NonCompliances.Count(n => !n.IsDeleted && n.IssuedDate >= start && n.IssuedDate < end),
                OpenNonCompliances = c.NonCompliances.Count(n => !n.IsDeleted && n.ClosedDate == null),
            })
            .ToListAsync(cancellationToken);

        var rows = contractors.Select(c =>
        {
            var scored = c.Inspections.Where(i => i.ComplianceScore != null).Select(i => i.ComplianceScore!.Value).ToList();
            return new SheContractorRankingDto
            {
                ContractorId = c.Id,
                ContractorCode = c.ContractorCode,
                CompanyName = c.CompanyName,
                PreQualificationScore = c.PreQualificationScore,
                InspectionsConducted = c.Inspections.Count,
                InspectionsScored = scored.Count,
                AverageComplianceScore = scored.Count > 0 ? Math.Round((decimal)scored.Average(), 2) : null,
                NonComplianceNoticesIssued = c.NoticesIssued,
                OpenNonCompliances = c.OpenNonCompliances,
            };
        })
        // Best average score first; unscored contractors rank below scored ones,
        // tie-broken by fewer notices, then prequalification score.
        .OrderByDescending(r => r.AverageComplianceScore ?? decimal.MinValue)
        .ThenBy(r => r.NonComplianceNoticesIssued)
        .ThenByDescending(r => r.PreQualificationScore ?? -1)
        .ThenBy(r => r.CompanyName)
        .ToList();

        for (var i = 0; i < rows.Count; i++) rows[i].Rank = i + 1;
        return rows;
    }

    // ── hazard heat-map (FR-SHE-232) ─────────────────────────────────────────

    public async Task<SheHazardHeatmapDto> GetHazardHeatmapAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var hazards = await _unitOfWork.Repository<SheHazard>()
            .GetQueryable(h => h.TenantId == tenantId && !h.IsDeleted && h.IsActive)
            .Select(h => new { h.InherentLikelihood, h.InherentSeverity, h.ResidualLikelihood, h.ResidualSeverity })
            .ToListAsync(cancellationToken);

        static int[][] EmptyGrid() => Enumerable.Range(0, 5).Select(_ => new int[5]).ToArray();
        var inherent = EmptyGrid();
        var residual = EmptyGrid();
        var excluded = 0;

        static bool InRange(int v) => v is >= 1 and <= 5;
        foreach (var h in hazards)
        {
            var ok = false;
            if (InRange(h.InherentLikelihood) && InRange(h.InherentSeverity))
            {
                inherent[h.InherentLikelihood - 1][h.InherentSeverity - 1]++;
                ok = true;
            }
            if (InRange(h.ResidualLikelihood) && InRange(h.ResidualSeverity))
            {
                residual[h.ResidualLikelihood - 1][h.ResidualSeverity - 1]++;
                ok = true;
            }
            if (!ok) excluded++;
        }

        return new SheHazardHeatmapDto
        {
            Inherent = inherent,
            Residual = residual,
            TotalHazards = hazards.Count,
            ExcludedOutOfRange = excluded,
        };
    }
}
