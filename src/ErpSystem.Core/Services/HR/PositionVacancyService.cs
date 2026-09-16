using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class PositionVacancyService : IPositionVacancyService
{
    private static readonly PositionVacancyStatus[] OpenStatuses =
    {
        PositionVacancyStatus.Anticipated,
        PositionVacancyStatus.Open,
        PositionVacancyStatus.UnderReview,
        PositionVacancyStatus.RequisitionRaised,
    };

    private readonly IPositionVacancyRepository _repository;
    private readonly IStaffRequisitionService _requisitionService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PositionVacancyService> _logger;

    public PositionVacancyService(
        IPositionVacancyRepository repository,
        IStaffRequisitionService requisitionService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PositionVacancyService> logger)
    {
        _repository = repository;
        _requisitionService = requisitionService;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A vacancy owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PositionVacancy> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Position vacancy '{id}' not found.");
        return entity;
    }

    private async Task<HashSet<Guid>> GetTenantPositionIdsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ids = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .Where(p => p.TenantId == tenantId && p.IsActive && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<PositionEstablishmentDto>> GetEstablishmentOverviewAsync(
        Guid? organizationUnitId = null, bool onlyVacant = false, CancellationToken cancellationToken = default)
    {
        var tenantPositionIds = await GetTenantPositionIdsAsync(cancellationToken);
        var overview = await _repository.GetEstablishmentOverviewAsync(organizationUnitId, onlyVacant);
        return overview.Where(p => tenantPositionIds.Contains(p.PositionId));
    }

    public async Task<IEnumerable<PositionVacancySummaryDto>> GetVacanciesAsync(
        PositionVacancyStatus? status = null,
        Guid? organizationUnitId = null,
        VacancyReason? reason = null,
        VacancyClassification? classification = null,
        bool includeClosed = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetVacanciesAsync(status, organizationUnitId, reason, classification, includeClosed);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PositionVacancyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;

        var currentHeadcount = await _repository.CountActiveOnPositionAsync(entity.TenantId, entity.PositionId);
        return entity.ToDto(currentHeadcount);
    }

    public async Task<PositionVacancyStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var open = await _repository.GetQueryable()
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && !v.IsDeleted && OpenStatuses.Contains(v.Status))
            .Select(v => new { v.Status, v.Classification, v.PositionId })
            .ToListAsync(cancellationToken);

        var totalPositions = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId && p.IsActive && !p.IsDeleted, cancellationToken);

        return new PositionVacancyStatsDto
        {
            TotalOpen = open.Count,
            Anticipated = open.Count(v => v.Status == PositionVacancyStatus.Anticipated),
            UnderReview = open.Count(v => v.Status == PositionVacancyStatus.UnderReview),
            RequisitionRaised = open.Count(v => v.Status == PositionVacancyStatus.RequisitionRaised),
            WithinEstablishment = open.Count(v => v.Classification == VacancyClassification.WithinEstablishment),
            NoShortfallOrOver = open.Count(v => v.Classification != VacancyClassification.WithinEstablishment),
            TotalPositions = totalPositions,
            PositionsWithVacancy = open.Select(v => v.PositionId).Distinct().Count(),
        };
    }

    // ── Mutations ───────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a vacancy for a write response with its live headcount, the way
    /// <see cref="GetByIdAsync"/> does.
    ///
    /// <para>The mutations used to call <c>ToDto()</c> bare, and that overload defaults
    /// <c>currentActiveHeadcount</c> to 0 — so changing a vacancy's status or notes made the
    /// headcount on screen drop to zero until the next read put it back.</para>
    /// </summary>
    private async Task<PositionVacancyDto> ToDtoWithHeadcountAsync(PositionVacancy entity)
    {
        var currentHeadcount = await _repository.CountActiveOnPositionAsync(entity.TenantId, entity.PositionId);
        return entity.ToDto(currentHeadcount);
    }

    public async Task<PositionVacancyDto> UpdateStatusAsync(
        UpdatePositionVacancyStatusDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.VacancyId);

        if (entity.Status is PositionVacancyStatus.Filled or PositionVacancyStatus.Closed)
            throw new InvalidOperationException("A filled or closed vacancy can no longer change status.");

        // Requisition-raised / filled are system-driven — don't allow them to be set by hand here.
        if (dto.NewStatus is PositionVacancyStatus.RequisitionRaised or PositionVacancyStatus.Filled)
            throw new InvalidOperationException(
                "Use 'Raise Requisition' or the hiring flow to move a vacancy to that status.");

        // ⚠ G-3.8 (2026-09-15): this was a back door around the mandatory close reason. Closing
        // through the dedicated dialog requires one — enforced client-side (the button is
        // disabled) and server-side ([Required] on a string, which does bite). Setting the status
        // to Closed through this override dialog reached here instead, which set Status and
        // ClosedDate and left ClosedReason null, because this method's Notes field is optional.
        // Two paths to the same terminal state, one of which dropped the audit reason the other
        // insisted on. The override path now asks for the same thing, and records it in the same
        // column rather than only in the free-text note.
        if (dto.NewStatus == PositionVacancyStatus.Closed && string.IsNullOrWhiteSpace(dto.Notes))
            throw new InvalidOperationException(
                "Say why the vacancy is being closed. Closing a gap is the one status change that " +
                "ends the record, so it carries a reason whichever way it is reached.");

        entity.Status = dto.NewStatus;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = AppendNote(entity.Notes, dto.Notes!);

        if (dto.NewStatus == PositionVacancyStatus.Closed)
        {
            entity.ClosedDate = DateTime.UtcNow;
            entity.ClosedReason = dto.Notes!.Trim().Length > 500
                ? dto.Notes!.Trim()[..500]
                : dto.Notes!.Trim();
        }

        Stamp(entity, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoWithHeadcountAsync(entity);
    }

    public async Task<PositionVacancyDto> UpdateNotesAsync(
        Guid vacancyId, string? notes, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(vacancyId);

        entity.Notes = notes;
        Stamp(entity, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoWithHeadcountAsync(entity);
    }

    public async Task<PositionVacancyDto> CloseAsync(
        ClosePositionVacancyDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.VacancyId);

        if (entity.Status is PositionVacancyStatus.Filled or PositionVacancyStatus.Closed)
            throw new InvalidOperationException("This vacancy is already filled or closed.");

        entity.Status = PositionVacancyStatus.Closed;
        entity.ClosedDate = DateTime.UtcNow;
        entity.ClosedReason = dto.Reason;
        Stamp(entity, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoWithHeadcountAsync(entity);
    }

    public async Task<RaiseRequisitionResultDto> RaiseRequisitionAsync(
        Guid vacancyId, RaiseRequisitionFromVacancyDto dto, Guid tenantId, Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var vacancy = await GetOwnedAsync(vacancyId);

        if (vacancy.Status is PositionVacancyStatus.Filled or PositionVacancyStatus.Closed)
            throw new InvalidOperationException("Cannot raise a requisition for a filled or closed vacancy.");

        if (vacancy.StaffRequisitionId != null)
            throw new InvalidOperationException("A requisition has already been raised for this vacancy.");

        var position = vacancy.Position;

        var createDto = new CreateStaffRequisitionDto
        {
            PositionId = vacancy.PositionId,
            OrganizationLevelId = position?.OrganizationLevelId,
            OrganizationUnitId = vacancy.OrganizationUnitId ?? position?.OrganizationUnitId,
            Type = StaffRequisitionType.Replacement,
            Priority = dto.Priority ?? StaffRequisitionPriority.Medium,
            RequisitionTitle = string.IsNullOrWhiteSpace(dto.RequisitionTitle)
                ? $"Replacement — {position?.Title}".Trim()
                : dto.RequisitionTitle,
            NumberOfPositions = dto.NumberOfPositions is > 0 ? dto.NumberOfPositions!.Value : 1,
            ReplacementForEmployeeId = vacancy.VacatedByEmployeeId,
            ReplacementReason = MapReplacementReason(vacancy.Reason),
            EmployeeDepartureDate = vacancy.VacatedDate,
            DesiredStartDate = dto.DesiredStartDate ?? DateTime.UtcNow.Date.AddDays(30),
            BusinessJustification = string.IsNullOrWhiteSpace(dto.BusinessJustification)
                ? BuildJustification(vacancy)
                : dto.BusinessJustification!,
            // Sensible defaults — the requester tunes these on the requisition form.
            AllowInternalCandidates = true,
            AllowExternalCandidates = true,
        };

        var created = await _requisitionService.CreateAsync(createDto, current, requestedByUserId, cancellationToken);

        vacancy.StaffRequisitionId = created.Id;
        vacancy.Status = PositionVacancyStatus.RequisitionRaised;
        Stamp(vacancy, requestedByUserId);
        await _repository.UpdateAsync(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Requisition {RequisitionNumber} raised from position vacancy {VacancyId}.",
            created.RequisitionNumber, vacancy.Id);

        return new RaiseRequisitionResultDto
        {
            RequisitionId = created.Id,
            RequisitionNumber = created.RequisitionNumber,
            VacancyId = vacancy.Id,
        };
    }

    public async Task<ReconcileVacanciesResultDto> ReconcilePositionVacanciesAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var tenantPositionIds = await GetTenantPositionIdsAsync(cancellationToken);
        var overview = (await _repository.GetEstablishmentOverviewAsync())
            .Where(p => tenantPositionIds.Contains(p.PositionId))
            .ToList();
        var opened = 0;
        var closed = 0;

        // Round 2b, R4a: a vacancy is opened only where a headcount was AUTHORISED. Before this,
        // every unestablished post with nobody in it (its ExpectedHeadcount the column default of
        // 1) got a vacancy — 38 on the live tenant, none of them on an established post. Those
        // are closed below with a reason that says why, not "back at establishment".
        foreach (var p in overview)
        {
            if (p.IsEstablished && p.VacantCount > 0 && p.OpenVacancyId == null)
            {
                // ⚠ G-3.4 / G-3.5 (2026-09-15). Reconcile stamps what it can actually know and no
                // more. It sweeps for gaps nobody logged — so by construction it has no departing
                // employee, no reason and no real vacated date, and the three columns it used to
                // fill with `Other`, null and `DateTime.UtcNow` read on screen as *why the post is
                // empty*, *who left* and *how long it has stood empty*. The last was the
                // misleading one: "Since" meant "when somebody last pressed a button".
                //
                // Those columns now stay honestly empty on a reconcile row, and the note says why.
                // PositionVacancyLog fills them in when a real departure explains the gap — it
                // upgrades an existing row rather than opening a second one — so a reconcile row is
                // a placeholder that the next departure completes, not a fact of its own.
                //
                // Classification is computed rather than hardcoded to WithinEstablishment. This
                // branch only runs on established posts below headcount, so it genuinely is a
                // shortfall within establishment; stating it as a calculation keeps the one writer
                // of that column consistent with PositionVacancyLog's, which can produce all three.
                await _repository.AddAsync(new PositionVacancy
                {
                    TenantId = current,
                    PositionId = p.PositionId,
                    OrganizationUnitId = p.OrganizationUnitId,
                    VacatedByEmployeeId = null,
                    Reason = VacancyReason.Other,
                    VacatedDate = DateTime.UtcNow,
                    IsAnticipated = false,
                    Status = PositionVacancyStatus.Open,
                    Classification = p.FilledCount < p.ExpectedHeadcount
                        ? VacancyClassification.WithinEstablishment
                        : p.FilledCount > p.ExpectedHeadcount
                            ? VacancyClassification.OverEstablishment
                            : VacancyClassification.NoShortfall,
                    ExpectedHeadcount = p.ExpectedHeadcount,
                    ActiveHeadcountAtDetection = p.FilledCount,
                    Notes = "Opened by reconcile — position headcount is below establishment. "
                          + "No departure was logged for this gap, so the reason, the person who "
                          + "vacated and the date it fell empty are not known; \"Since\" is the "
                          + "date of this reconcile, not the date the seat emptied.",
                    CreatedById = userId,
                    CreatedBy = "reconcile",
                });
                opened++;
            }
            else if (p.OpenVacancyId != null && (!p.IsEstablished || p.VacantCount == 0))
            {
                var vac = await _repository.GetByIdAsync(p.OpenVacancyId.Value);
                if (vac != null && vac.TenantId == current && vac.Status is not (PositionVacancyStatus.Filled or PositionVacancyStatus.Closed))
                {
                    // ⚠ A vacancy raised into a requisition is somebody's live work; reconcile does
                    // not close it under them, whatever the establishment now says.
                    if (vac.Status == PositionVacancyStatus.RequisitionRaised) continue;
                    vac.Status = p.IsEstablished ? PositionVacancyStatus.Filled : PositionVacancyStatus.Closed;
                    vac.ClosedDate = DateTime.UtcNow;
                    vac.ClosedReason = p.IsEstablished
                        ? "Closed by reconcile — position is back at establishment."
                        : "Closed by reconcile — the position has no approved establishment, so no gap can be stated for it.";
                    Stamp(vac, userId);
                    await _repository.UpdateAsync(vac);
                    closed++;
                }
            }
        }

        if (opened > 0 || closed > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Position-vacancy reconcile: scanned {Scanned}, opened {Opened}, closed {Closed}.",
            overview.Count, opened, closed);

        return new ReconcileVacanciesResultDto { Opened = opened, Closed = closed, Scanned = overview.Count };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void Stamp(PositionVacancy entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.LastModifiedById = userId;
    }

    private static string AppendNote(string? existing, string addition)
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] {addition.Trim()}";
        return string.IsNullOrWhiteSpace(existing) ? line : $"{existing}\n{line}";
    }

    private static string BuildJustification(PositionVacancy vacancy)
    {
        var who = vacancy.VacatedByEmployee?.FullName ?? "the previous incumbent";
        var role = vacancy.Position?.Title ?? "the position";
        return $"Replacement for {who} ({vacancy.Reason}), who vacated {role} on {vacancy.VacatedDate:d}.";
    }

    private static StaffReplacementReason MapReplacementReason(VacancyReason reason) => reason switch
    {
        VacancyReason.Resignation => StaffReplacementReason.Resignation,
        VacancyReason.Retirement => StaffReplacementReason.Retirement,
        VacancyReason.Termination => StaffReplacementReason.Termination,
        VacancyReason.Promotion => StaffReplacementReason.Promotion,
        VacancyReason.Transfer => StaffReplacementReason.Transfer,
        VacancyReason.Death => StaffReplacementReason.Death,
        _ => StaffReplacementReason.Other,
    };
}
