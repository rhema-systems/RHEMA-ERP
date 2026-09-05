using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingWaitlistService : ITrainingWaitlistService
{
    private readonly ITrainingWaitlistRepository _waitlistRepository;
    private readonly ITrainingNominationService _nominationService;
    private readonly ITrainingNominationRepository _nominationRepository;
    private readonly ITrainingScheduleRepository _scheduleRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingWaitlistService> _logger;

    public TrainingWaitlistService(
        ITrainingWaitlistRepository waitlistRepository,
        ITrainingNominationService nominationService,
        ITrainingNominationRepository nominationRepository,
        ITrainingScheduleRepository scheduleRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingWaitlistService> logger)
    {
        _waitlistRepository = waitlistRepository;
        _nominationService = nominationService;
        _nominationRepository = nominationRepository;
        _scheduleRepository = scheduleRepository;
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

    // An entry owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<TrainingWaitlist> GetOwnedAsync(Guid id)
    {
        var entity = await _waitlistRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Waitlist entry with ID '{id}' not found.");
        return entity;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<TrainingWaitlistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingWaitlistDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _waitlistRepository.GetByScheduleIdAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingWaitlistDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _waitlistRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<TrainingWaitlistDto>> GetActiveWaitlistAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _waitlistRepository.GetActiveWaitlistAsync(scheduleId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    // ── Waitlist workflow ─────────────────────────────────────────────────────

    public async Task<TrainingWaitlistDto> AddToWaitlistAsync(CreateTrainingWaitlistDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // Use the max existing position (not a count of active rows): once entries are offered/accepted/
        // removed the active count drops and a count-based position would collide with existing rows.
        // Positions are per tenant, so an unscoped max would skip numbers based on other tenants' rows.
        var maxPosition = await _waitlistRepository.GetQueryable()
            .Where(w => w.TenantId == current && w.ScheduleId == dto.ScheduleId)
            .Select(w => (int?)w.Position)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = dto.ToEntity(current, createdByUserId);
        entity.Position = maxPosition + 1;
        entity.AddedDate = DateTime.UtcNow;
        entity.Status = TrainingWaitlistStatus.Active;

        await _waitlistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} added to waitlist for schedule {ScheduleId} at position {Position}", dto.EmployeeId, dto.ScheduleId, entity.Position);

        // Freshly written: no Schedule/Employee loaded, so the response would carry a blank
        // schedule number, programme and nominee.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TrainingWaitlistDto> OfferSlotAsync(OfferWaitlistPositionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.WaitlistId);

        if (entity.Status != TrainingWaitlistStatus.Active)
            throw new InvalidOperationException($"Cannot offer position to waitlist entry with status '{entity.Status}'.");

        entity.Status = TrainingWaitlistStatus.Offered;
        entity.OfferDate = dto.OfferDate;
        entity.OfferExpiryDate = dto.OfferExpiryDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _waitlistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Waitlist position offered to entry {WaitlistId}", dto.WaitlistId);

        return entity.ToDto();
    }

    public async Task<TrainingWaitlistDto> RecordOfferResponseAsync(RespondToWaitlistOfferDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(dto.WaitlistId);

        if (entity.Status != TrainingWaitlistStatus.Offered)
            throw new InvalidOperationException("Cannot respond to an offer that has not been made.");

        entity.Status = dto.OfferAccepted ? TrainingWaitlistStatus.Accepted : TrainingWaitlistStatus.Declined;
        entity.OfferAccepted = dto.OfferAccepted;
        entity.ResponseDate = dto.ResponseDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        // Acceptance deliberately stays a pure employee action: it records that the offer was taken,
        // and nothing more. Enrolment is a separate HR step (PromoteToNominationAsync) because it
        // commits a seat and budget, needs a named HR actor for audit, and is where the seat has to be
        // re-checked — capacity is enforced at *approval*, and its refusal message tells the caller to
        // "add the employee to the waitlist", which is nonsense to show someone just off the waitlist.
        await _waitlistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Waitlist offer {WaitlistId} {Response}", dto.WaitlistId, dto.OfferAccepted ? "accepted" : "declined");

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> RemoveFromWaitlistAsync(Guid waitlistId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(waitlistId);

        entity.Status = TrainingWaitlistStatus.Removed;
        entity.UpdatedAt = DateTime.UtcNow;

        await _waitlistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Waitlist entry {WaitlistId} removed", waitlistId);

        return true;
    }

    /// <summary>
    /// Enrols an employee who has accepted a waitlist offer, creating the nomination and stamping the
    /// audit link back onto the waitlist entry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately separate from <see cref="RecordOfferResponseAsync"/>. Acceptance is the employee's
    /// record; enrolment commits a seat and budget, so it is HR's, and this is where the seat is
    /// re-checked against a schedule that may have filled since the offer went out.
    /// </para>
    /// <para>
    /// <c>CreatedNominationId</c> and <c>CreatedNominationNumber</c> were on the contract from the
    /// start and nothing ever wrote them, so the whole waitlist→nomination step had never executed.
    /// </para>
    /// <para>
    /// Idempotent by necessity, not politeness: <c>CreateAsync</c> refuses a second live nomination for
    /// the same employee and schedule, so a failure between creating the nomination and stamping the
    /// link would wedge the entry permanently — every retry throwing "already has an active
    /// nomination" with nothing to link to. Adopting a pre-existing nomination makes a half-finished
    /// promotion self-heal on retry.
    /// </para>
    /// </remarks>
    public async Task<TrainingWaitlistDto> PromoteToNominationAsync(Guid waitlistId, Guid promotedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entry = await GetOwnedAsync(waitlistId);

        // Already promoted — return what is there rather than erroring, so a double-click or a client
        // retry is harmless.
        if (entry.CreatedNominationId.HasValue)
            return await GetByIdAsync(entry.Id, cancellationToken);

        if (entry.Status != TrainingWaitlistStatus.Accepted)
            throw new InvalidOperationException(
                $"Only an accepted waitlist offer can be enrolled — this entry is '{entry.Status}'. Offer the slot and record the employee's acceptance first.");

        // The whole promotion runs in one transaction under an execution strategy: an explicit
        // transaction and SQL Server's retry policy cannot coexist without one.
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var owned = _unitOfWork.HasActiveTransaction;
            if (!owned) await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // Serialise promotions per schedule: two entries racing the same freed seat would
                // otherwise both pass the capacity check below and both be enrolled.
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"training-schedule-seats:{entry.TenantId:N}:{entry.ScheduleId:N}", cancellationToken);

                await EnsureSeatAvailableAsync(entry, cancellationToken);

                // Adopt an existing live nomination if one is already there (see the idempotency note
                // above); otherwise create one.
                var existing = await _nominationRepository.GetQueryable()
                    .FirstOrDefaultAsync(n => n.TenantId == entry.TenantId
                        && n.ScheduleId == entry.ScheduleId
                        && n.EmployeeId == entry.EmployeeId
                        && n.Status != NominationStatus.Rejected
                        && n.Status != NominationStatus.Withdrawn,
                        cancellationToken);

                if (existing != null)
                {
                    entry.CreatedNominationId = existing.Id;
                    _logger.LogInformation(
                        "Waitlist entry {WaitlistId} linked to pre-existing nomination {NominationNumber}",
                        waitlistId, existing.NominationNumber);
                }
                else
                {
                    var nomination = await _nominationService.CreateAsync(
                        new CreateTrainingNominationDto
                        {
                            ScheduleId = entry.ScheduleId,
                            EmployeeId = entry.EmployeeId,
                            Type = NominationType.HR,
                            NominationDate = DateTime.UtcNow,
                            Justification = $"Enrolled from the waitlist (queue position {entry.Position}).",
                        },
                        entry.TenantId,
                        promotedByEmployeeId,
                        cancellationToken);

                    entry.CreatedNominationId = nomination.Id;
                }

                entry.UpdatedAt = DateTime.UtcNow;
                entry.UpdatedBy = promotedByEmployeeId.ToString();

                await _waitlistRepository.UpdateAsync(entry);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (!owned) await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (!owned)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                }
                throw;
            }
        }, cancellationToken);

        _logger.LogInformation("Waitlist entry {WaitlistId} promoted to a nomination by {PromotedBy}", waitlistId, promotedByEmployeeId);

        // Re-read so CreatedNominationNumber resolves — the Nomination navigation on the tracked entity
        // is still null immediately after its FK is set.
        return await GetByIdAsync(entry.Id, cancellationToken);
    }

    /// <summary>
    /// Refuses the promotion when the schedule has filled since the offer was sent.
    /// Mirrors the count in <c>TrainingNominationService.EnforceScheduleCapacityAsync</c>, but is
    /// raised here so the refusal reaches HR at enrolment time with wording that makes sense — the
    /// approval-time message tells the caller to "add the employee to the waitlist", which is exactly
    /// where this employee already is.
    /// </summary>
    private async Task EnsureSeatAvailableAsync(TrainingWaitlist entry, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(entry.ScheduleId);
        if (schedule == null || schedule.TenantId != entry.TenantId || schedule.MaxParticipants <= 0)
            return;

        var seatsTaken = await _nominationRepository.GetQueryable()
            .CountAsync(n => n.TenantId == entry.TenantId
                && n.ScheduleId == entry.ScheduleId
                && (n.Status == NominationStatus.Approved || n.Status == NominationStatus.Confirmed),
                cancellationToken);

        if (seatsTaken >= schedule.MaxParticipants)
            throw new InvalidOperationException(
                $"This schedule filled up after the offer was made ({schedule.MaxParticipants} of {schedule.MaxParticipants} seats taken). " +
                "Re-offer the slot when one frees up, or move the employee to another schedule.");
    }
}
