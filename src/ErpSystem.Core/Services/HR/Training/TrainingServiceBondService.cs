using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingServiceBondService : ITrainingServiceBondService
{
    private readonly IGenericRepository<TrainingServiceBond> _bondRepository;
    private readonly IGenericRepository<TrainingNomination> _nominationRepository;
    private readonly IGenericRepository<TrainingSchedule> _scheduleRepository;
    private readonly IGenericRepository<TrainingProgram> _programRepository;
    private readonly IGenericRepository<TrainingCompletion> _completionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingServiceBondService> _logger;
    private readonly IHrFinancePostingAdapter _financePosting;

    public TrainingServiceBondService(
        IGenericRepository<TrainingServiceBond> bondRepository,
        IGenericRepository<TrainingNomination> nominationRepository,
        IGenericRepository<TrainingSchedule> scheduleRepository,
        IGenericRepository<TrainingProgram> programRepository,
        IGenericRepository<TrainingCompletion> completionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingServiceBondService> logger,
        IHrFinancePostingAdapter financePosting)
    {
        _bondRepository = bondRepository;
        _nominationRepository = nominationRepository;
        _scheduleRepository = scheduleRepository;
        _programRepository = programRepository;
        _completionRepository = completionRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _financePosting = financePosting;
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

    // A bond owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<TrainingServiceBond> GetOwnedAsync(Guid id)
    {
        var entity = await _bondRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training service bond with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<TrainingServiceBond> BondsWithDetails(Guid tenantId) =>
        _bondRepository.GetQueryable()
            .Where(b => b.TenantId == tenantId)
            .Include(b => b.Nomination)
            .Include(b => b.Employee)
            .Include(b => b.Program)
            .Include(b => b.AcceptanceRecordedBy);

    // ── Queries ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TrainingServiceBondDto>> GetAllAsync(TrainingBondStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = BondsWithDetails(GetTenantId());
        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var bonds = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
        return bonds.ToDtoList();
    }

    public async Task<TrainingServiceBondDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BondsWithDetails(GetTenantId()).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Training service bond with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<TrainingServiceBondDto?> GetByNominationAsync(Guid nominationId, CancellationToken cancellationToken = default)
    {
        var entity = await BondsWithDetails(GetTenantId()).FirstOrDefaultAsync(b => b.NominationId == nominationId, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<TrainingServiceBondDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var bonds = await BondsWithDetails(GetTenantId())
            .Where(b => b.EmployeeId == employeeId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
        return bonds.ToDtoList();
    }

    public async Task<IEnumerable<TrainingServiceBondDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var bonds = await BondsWithDetails(GetTenantId())
            .Where(b => b.Status == TrainingBondStatus.Active)
            .OrderBy(b => b.BondEndDate)
            .ToListAsync(cancellationToken);
        return bonds.ToDtoList();
    }

    // ── Create / update ──────────────────────────────────────────────────────────

    public async Task<TrainingServiceBondDto> CreateAsync(CreateTrainingServiceBondDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var nomination = await _nominationRepository.GetByIdAsync(dto.NominationId);
        if (nomination == null || nomination.TenantId != current)
            throw new ArgumentException($"Training nomination with ID '{dto.NominationId}' not found.");

        var existing = await _bondRepository.GetQueryable()
            .AnyAsync(b => b.TenantId == current && b.NominationId == dto.NominationId, cancellationToken);
        if (existing)
            throw new InvalidOperationException("This nomination already has a service bond.");

        var programId = await ResolveProgramIdAsync(nomination.ScheduleId, current, cancellationToken);

        var entity = new TrainingServiceBond
        {
            TenantId = current,
            NominationId = dto.NominationId,
            EmployeeId = nomination.EmployeeId,
            ProgramId = programId,
            BondDurationMonths = dto.BondDurationMonths,
            BondAmount = dto.BondAmount,
            Currency = dto.Currency,
            TermsText = dto.TermsText,
            Notes = dto.Notes,
            Status = TrainingBondStatus.PendingAcceptance,
            CreatedBy = createdByUserId.ToString(),
        };

        await _bondRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Service bond created for nomination {NominationId}", dto.NominationId);
        return (await GetByIdAsync(entity.Id, cancellationToken));
    }

    public async Task<TrainingServiceBondDto> UpdateAsync(Guid id, UpdateTrainingServiceBondDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status != TrainingBondStatus.PendingAcceptance)
            throw new InvalidOperationException("Bond terms can only be edited while the bond is pending acceptance.");

        entity.BondDurationMonths = dto.BondDurationMonths;
        entity.BondAmount = dto.BondAmount;
        entity.Currency = dto.Currency;
        entity.TermsText = dto.TermsText;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _bondRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    // ── Lifecycle transitions ──────────────────────────────────────────────────

    public async Task<TrainingServiceBondDto> AcceptAsync(AcceptTrainingServiceBondDto dto, Guid actingEmployeeId, bool onBehalf, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.BondId);

        if (entity.Status != TrainingBondStatus.PendingAcceptance)
            throw new InvalidOperationException($"Bond cannot be accepted because it is {entity.Status}.");

        if (!onBehalf && actingEmployeeId != entity.EmployeeId)
            throw new InvalidOperationException("You can only accept your own service bond.");

        var start = (dto.BondStartDate?.Date)
                    ?? (await GetCompletionDateAsync(entity.NominationId, entity.TenantId, cancellationToken))
                    ?? DateTime.UtcNow.Date;

        entity.AcceptedByEmployee = true;
        entity.AcceptedDate = DateTime.UtcNow;
        entity.AcceptanceRecordedById = onBehalf ? actingEmployeeId : null;
        entity.AcceptanceNotes = dto.AcceptanceNotes;
        entity.BondStartDate = start;
        entity.BondEndDate = start.AddMonths(entity.BondDurationMonths);
        entity.Status = TrainingBondStatus.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _bondRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Service bond {BondId} accepted (onBehalf={OnBehalf})", entity.Id, onBehalf);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingServiceBondDto> RecordExitAsync(RecordBondExitDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.BondId);

        if (entity.Status != TrainingBondStatus.Active)
            throw new InvalidOperationException($"Only an active bond can record an exit (current status: {entity.Status}).");

        entity.ExitDate = dto.ExitDate.Date;

        var total = entity.BondDurationMonths;
        var start = entity.BondStartDate ?? entity.AcceptedDate?.Date ?? entity.ExitDate.Value;
        var servedMonths = MonthsBetween(start, entity.ExitDate.Value);
        var remaining = Math.Max(0, total - servedMonths);

        if (remaining <= 0)
        {
            // Served the full obligation — no repayment owed.
            entity.RepaymentAmount = 0m;
            entity.Status = TrainingBondStatus.Fulfilled;
        }
        else
        {
            entity.RepaymentAmount = total > 0
                ? Math.Round(entity.BondAmount * remaining / total, 2, MidpointRounding.AwayFromZero)
                : 0m;
            entity.Status = TrainingBondStatus.Breached;
        }

        if (!string.IsNullOrWhiteSpace(dto.Notes))
            entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        // A breach raises a receivable in Finance; a fulfilled bond posts nothing (lane 8, slice 4).
        await _financePosting.RunAsync(async ct =>
        {
            await _bondRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return entity.Status == TrainingBondStatus.Breached
                ? HrFinancePostingCommandFactory.TrainingBondBreached(entity)
                : null;
        }, userId, cancellationToken);

        _logger.LogInformation("Service bond {BondId} exit recorded → {Status}, repayment {Amount}",
            entity.Id, entity.Status, entity.RepaymentAmount);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingServiceBondDto> WaiveAsync(WaiveTrainingServiceBondDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.BondId);

        if (entity.Status is TrainingBondStatus.Settled or TrainingBondStatus.Cancelled or TrainingBondStatus.Fulfilled)
            throw new InvalidOperationException($"A {entity.Status} bond cannot be waived.");

        // Waiving a breached bond writes its receivable off; a bond waived while still active or
        // pending had no receivable posted and is recorded Skipped (lane 8, slice 4).
        var breachPosted = await _financePosting.IsPostedAsync(HrFinancePostingEventCatalog.TrainingBondBreached, entity.Id, cancellationToken);
        await _financePosting.RunAsync(async ct =>
        {
            entity.Status = TrainingBondStatus.Waived;
            entity.WaivedDate = DateTime.UtcNow;
            entity.WaivedById = userId;
            entity.WaiverReason = dto.WaiverReason;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userId.ToString();

            await _bondRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return HrFinancePostingCommandFactory.TrainingBondWaived(entity, breachPosted);
        }, userId, cancellationToken);

        _logger.LogInformation("Service bond {BondId} waived", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingServiceBondDto> SettleAsync(SettleTrainingServiceBondDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.BondId);

        if (entity.Status != TrainingBondStatus.Breached)
            throw new InvalidOperationException("Only a breached bond (with repayment owed) can be settled.");

        // Settling a breached bond clears its receivable (lane 8, slice 4).
        var breachPosted = await _financePosting.IsPostedAsync(HrFinancePostingEventCatalog.TrainingBondBreached, entity.Id, cancellationToken);
        await _financePosting.RunAsync(async ct =>
        {
            entity.Status = TrainingBondStatus.Settled;
            entity.SettledDate = (dto.SettledDate?.Date) ?? DateTime.UtcNow.Date;
            if (!string.IsNullOrWhiteSpace(dto.Notes))
                entity.Notes = dto.Notes;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userId.ToString();

            await _bondRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return HrFinancePostingCommandFactory.TrainingBondSettled(entity, breachPosted);
        }, userId, cancellationToken);

        _logger.LogInformation("Service bond {BondId} settled", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.Status is not (TrainingBondStatus.PendingAcceptance or TrainingBondStatus.Cancelled))
            throw new InvalidOperationException("Only a pending or cancelled bond can be deleted.");

        await _bondRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Nomination-driven hooks ─────────────────────────────────────────────────

    public async Task EnsureBondForNominationAsync(Guid nominationId, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var already = await _bondRepository.GetQueryable()
            .AnyAsync(b => b.TenantId == current && b.NominationId == nominationId, cancellationToken);
        if (already) return;

        var nomination = await _nominationRepository.GetByIdAsync(nominationId);
        if (nomination == null || nomination.TenantId != current) return;

        var schedule = await _scheduleRepository.GetByIdAsync(nomination.ScheduleId);
        if (schedule == null || schedule.TenantId != current) return;

        // The bond duration, amount and currency are copied off the program, so a cross-tenant program
        // would not just leak: it would stamp another tenant's figures onto this bond.
        var program = await _programRepository.GetByIdAsync(schedule.ProgramId);
        if (program == null || program.TenantId != current || !program.RequiresServiceBond) return;

        var entity = new TrainingServiceBond
        {
            TenantId = current,
            NominationId = nominationId,
            EmployeeId = nomination.EmployeeId,
            ProgramId = program.Id,
            BondDurationMonths = program.ServiceBondMonths ?? 12,
            BondAmount = nomination.ActualCost ?? program.CostPerParticipant,
            Currency = program.Currency,
            TermsText = program.ServiceBondTerms,
            Status = TrainingBondStatus.PendingAcceptance,
            CreatedBy = userId.ToString(),
        };

        await _bondRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Auto-created service bond for approved nomination {NominationId}", nominationId);
    }

    public async Task CancelForNominationAsync(Guid nominationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var entity = await _bondRepository.GetQueryable()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.NominationId == nominationId, cancellationToken);
        if (entity == null || entity.Status != TrainingBondStatus.PendingAcceptance) return;

        entity.Status = TrainingBondStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _bondRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<Guid> ResolveProgramIdAsync(Guid scheduleId, Guid tenantId, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null || schedule.TenantId != tenantId)
            throw new ArgumentException($"Training schedule with ID '{scheduleId}' not found.");
        return schedule.ProgramId;
    }

    // The completion date becomes the bond start (and therefore its end date), so an unscoped lookup would
    // date the bond off another tenant's completion record.
    private async Task<DateTime?> GetCompletionDateAsync(Guid nominationId, Guid tenantId, CancellationToken cancellationToken)
    {
        var completion = await _completionRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.NominationId == nominationId)
            .OrderByDescending(c => c.CompletionDate)
            .FirstOrDefaultAsync(cancellationToken);
        return completion?.CompletionDate.Date;
    }

    /// <summary>Whole calendar months served between two dates (0 if end precedes start).</summary>
    private static int MonthsBetween(DateTime start, DateTime end)
    {
        if (end <= start) return 0;
        var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
        if (end.Day < start.Day) months--;   // not a full final month
        return months < 0 ? 0 : months;
    }
}
