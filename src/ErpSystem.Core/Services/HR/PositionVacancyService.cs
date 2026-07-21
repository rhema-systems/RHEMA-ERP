using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class PositionVacancyService : IPositionVacancyService
{
    private readonly IPositionVacancyRepository _repository;
    private readonly IStaffRequisitionService _requisitionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PositionVacancyService> _logger;

    public PositionVacancyService(
        IPositionVacancyRepository repository,
        IStaffRequisitionService requisitionService,
        IUnitOfWork unitOfWork,
        ILogger<PositionVacancyService> logger)
    {
        _repository = repository;
        _requisitionService = requisitionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<PositionEstablishmentDto>> GetEstablishmentOverviewAsync(
        Guid? organizationUnitId = null, bool onlyVacant = false, CancellationToken cancellationToken = default)
        => await _repository.GetEstablishmentOverviewAsync(organizationUnitId, onlyVacant);

    public async Task<IEnumerable<PositionVacancySummaryDto>> GetVacanciesAsync(
        PositionVacancyStatus? status = null,
        Guid? organizationUnitId = null,
        VacancyReason? reason = null,
        VacancyClassification? classification = null,
        bool includeClosed = false,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetVacanciesAsync(status, organizationUnitId, reason, classification, includeClosed);
        return entities.ToSummaryDtoList();
    }

    public async Task<PositionVacancyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(id);
        if (entity == null)
            return null;

        var currentHeadcount = await _repository.CountActiveOnPositionAsync(entity.TenantId, entity.PositionId);
        return entity.ToDto(currentHeadcount);
    }

    public async Task<PositionVacancyStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
        => await _repository.GetStatsAsync();

    // ── Mutations ───────────────────────────────────────────────────────────

    public async Task<PositionVacancyDto> UpdateStatusAsync(
        UpdatePositionVacancyStatusDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(dto.VacancyId)
            ?? throw new ArgumentException($"Position vacancy '{dto.VacancyId}' not found.");

        if (entity.Status is PositionVacancyStatus.Filled or PositionVacancyStatus.Closed)
            throw new InvalidOperationException("A filled or closed vacancy can no longer change status.");

        // Requisition-raised / filled are system-driven — don't allow them to be set by hand here.
        if (dto.NewStatus is PositionVacancyStatus.RequisitionRaised or PositionVacancyStatus.Filled)
            throw new InvalidOperationException(
                "Use 'Raise Requisition' or the hiring flow to move a vacancy to that status.");

        entity.Status = dto.NewStatus;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = AppendNote(entity.Notes, dto.Notes!);

        if (dto.NewStatus == PositionVacancyStatus.Closed)
            entity.ClosedDate = DateTime.UtcNow;

        Stamp(entity, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<PositionVacancyDto> UpdateNotesAsync(
        Guid vacancyId, string? notes, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(vacancyId)
            ?? throw new ArgumentException($"Position vacancy '{vacancyId}' not found.");

        entity.Notes = notes;
        Stamp(entity, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<PositionVacancyDto> CloseAsync(
        ClosePositionVacancyDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithDetailsAsync(dto.VacancyId)
            ?? throw new ArgumentException($"Position vacancy '{dto.VacancyId}' not found.");

        if (entity.Status is PositionVacancyStatus.Filled or PositionVacancyStatus.Closed)
            throw new InvalidOperationException("This vacancy is already filled or closed.");

        entity.Status = PositionVacancyStatus.Closed;
        entity.ClosedDate = DateTime.UtcNow;
        entity.ClosedReason = dto.Reason;
        Stamp(entity, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<RaiseRequisitionResultDto> RaiseRequisitionAsync(
        Guid vacancyId, RaiseRequisitionFromVacancyDto dto, Guid tenantId, Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var vacancy = await _repository.GetWithDetailsAsync(vacancyId)
            ?? throw new ArgumentException($"Position vacancy '{vacancyId}' not found.");

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

        var created = await _requisitionService.CreateAsync(createDto, tenantId, requestedByUserId, cancellationToken);

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
        var overview = (await _repository.GetEstablishmentOverviewAsync()).ToList();
        var opened = 0;
        var closed = 0;

        foreach (var p in overview)
        {
            if (p.VacantCount > 0 && p.OpenVacancyId == null)
            {
                await _repository.AddAsync(new PositionVacancy
                {
                    TenantId = tenantId,
                    PositionId = p.PositionId,
                    OrganizationUnitId = p.OrganizationUnitId,
                    Reason = VacancyReason.Other,
                    VacatedDate = DateTime.UtcNow,
                    IsAnticipated = false,
                    Status = PositionVacancyStatus.Open,
                    Classification = VacancyClassification.WithinEstablishment,
                    ExpectedHeadcount = p.ExpectedHeadcount,
                    ActiveHeadcountAtDetection = p.FilledCount,
                    Notes = "Opened by reconcile — position headcount is below establishment.",
                    CreatedById = userId,
                    CreatedBy = "reconcile",
                });
                opened++;
            }
            else if (p.VacantCount == 0 && p.OpenVacancyId != null)
            {
                var vac = await _repository.GetByIdAsync(p.OpenVacancyId.Value);
                if (vac != null && vac.Status is not (PositionVacancyStatus.Filled or PositionVacancyStatus.Closed))
                {
                    vac.Status = PositionVacancyStatus.Filled;
                    vac.ClosedDate = DateTime.UtcNow;
                    vac.ClosedReason = "Closed by reconcile — position is back at establishment.";
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
